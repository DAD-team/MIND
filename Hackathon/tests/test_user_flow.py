"""
Flow test mô phỏng hành vi user thật trên Android app → backend.

Mỗi class = 1 profile user (P1..P8), gồm nhiều bước gọi PUBLIC API giống
ApiClient.java trên Android. State giữa các bước được giữ trong FakeFirestore
y như Firestore thật.

Mapping với TEST_CASES.md + matrix đã thống nhất:
  P1: Healthy L1                 — PHQ-2 [0,0] → normal, next=+14d
  P2: Mild L2 (escalate qua flag)— PHQ-2 [1,1]+2 flag → PHQ-9 total=7 → level=2
  P3: Moderate L3                — PHQ-9 total=12 → level=3 + counselor alert
  P4: Active L4 (Q9 override)    — PHQ-9 total=10 Q9=1 → level=4 + safety event
  P5: Crisis L5                  — PHQ-9 total=22 Q9=3 → level=5 + safety + FCM
  P6: Rejection → pause 30d      — reject x3 liên tiếp
  P7: Scout auto-trigger         — silence 72h → schedule_phq2 → pending PHQ-2
  P8: Quiet hours                — notification bị skip lúc 23:30 VN

Cách chạy:  cd Hackathon && pytest tests/test_user_flow.py -v
"""

from datetime import datetime, timezone, timedelta
from unittest.mock import patch

import pytest

from tests.fake_firestore import FakeFirestore


# ---------------------------------------------------------------------------
#  Fixture chính — setup fake Firestore + patch FCM
# ---------------------------------------------------------------------------

@pytest.fixture
def fake_db():
    return FakeFirestore()


@pytest.fixture
def fcm_log():
    """Danh sách FCM đã 'gửi' — mỗi phần tử dict {token, title, body}."""
    return []


@pytest.fixture
def env(monkeypatch, fake_db, fcm_log):
    """
    Patch toàn bộ: get_db trả FakeFirestore, messaging.send ghi log thay vì gửi thật.
    Trả về namespace tiện dùng trong test.
    """
    monkeypatch.setattr("services.firebase.get_db", lambda: fake_db)
    monkeypatch.setattr("controllers.phq_controller.get_db", lambda: fake_db)
    monkeypatch.setattr("controllers.safety_controller.get_db", lambda: fake_db)
    monkeypatch.setattr("services.scout.get_db", lambda: fake_db)
    monkeypatch.setattr("services.notification.get_db", lambda: fake_db)

    # Chặn FCM thật, ghi vào fcm_log
    def _fake_send_fcm(token, title, body):
        fcm_log.append({"token": token, "title": title, "body": body})
        return True

    monkeypatch.setattr("services.notification.send_fcm", _fake_send_fcm)

    # upcoming_schedules (scout dùng) — mặc định 0
    monkeypatch.setattr("controllers.schedule_controller.upcoming_schedules",
                        lambda uid, days: {"total_weight": 0, "schedules": []})

    return {"db": fake_db, "fcm_log": fcm_log}


# ---------------------------------------------------------------------------
#  Helper: tạo PHQ body giống ApiClient.java gửi lên
# ---------------------------------------------------------------------------

def phq2_body(total: int, source: str = "self_request"):
    from models.phq import PhqSubmit
    # Android gửi scores=[q1, q2] với total=sum
    q1 = min(total, 3)
    q2 = max(0, total - q1)
    return PhqSubmit(
        phq_type="phq2",
        scores=[q1, q2],
        total=total,
        source=source,
    )


def phq9_body(scores: list[int], q9: int | None = None, triggered_by: str | None = None,
              source: str = "self_request"):
    from models.phq import PhqSubmit
    assert len(scores) == 9
    total = sum(scores)
    q9_value = q9 if q9 is not None else scores[8]
    return PhqSubmit(
        phq_type="phq9",
        scores=scores,
        total=total,
        source=source,
        somatic_score=scores[2] + scores[3] + scores[4] + scores[7],
        cognitive_score=scores[0] + scores[1] + scores[5] + scores[6] + scores[8],
        functional_impact=0,
        q9_value=q9_value,
        triggered_by_phq2_id=triggered_by,
    )


def _print_flow(title: str, steps: list[str]):
    """Helper để in log flow ra stdout khi test chạy với -s."""
    print(f"\n=== {title} ===")
    for i, s in enumerate(steps, 1):
        print(f"  {i}. {s}")


# ===========================================================================
#  P1 — Healthy user, level 1 (Tiêu chuẩn)
# ===========================================================================

class TestP1_HealthyStandard:
    """
    Kịch bản: user mới, không có triệu chứng.
    Bước: GET /phq/pending (empty) → POST /phq/submit PHQ-2 [0,0] → kết quả.
    Expected: decision=normal, next_phq2 = +14 ngày, KHÔNG có counselor alert, KHÔNG FCM.
    """

    UID = "user_p1_healthy"

    def test_flow(self, env):
        from controllers.phq_controller import get_pending_phq, submit_phq

        # Bước 1: App mở → kiểm tra pending
        pending = get_pending_phq(self.UID)
        assert pending["pending"] == []
        assert pending["has_phq2"] is False

        # Bước 2: User tự làm PHQ-2 với điểm 0/0
        response = submit_phq(self.UID, phq2_body(total=0))

        # --- Output kỳ vọng ---
        assert response["decision"] == "normal"
        assert response["next_phq2_days"] == 14
        assert "id" in response and "created_at" in response

        # Monitoring được cập nhật next_phq2_date
        mon = env["db"].read_doc(f"users/{self.UID}/monitoring", "current")
        assert mon is not None
        assert mon["phq2_decision"] == "normal"
        assert "next_phq2_date" in mon

        # Không có counselor_alert, không có safety_event, không có FCM
        assert env["db"].read_collection("counselor_alerts") == []
        assert env["db"].read_collection("safety_events") == []
        assert env["fcm_log"] == []

        _print_flow("P1 — Healthy L1", [
            f"Pending check: empty ✓",
            f"Submit PHQ-2 [0,0] → decision={response['decision']}, "
            f"next={response['next_phq2_days']}d ✓",
            f"No counselor alert, no FCM ✓",
        ])


# ===========================================================================
#  P2 — Mild (escalate PHQ-2 qua behavioral flag) → PHQ-9 level 2
# ===========================================================================

class TestP2_MildViaFlags:
    """
    Kịch bản: user có tổng PHQ-2 = 2 và ≥2 dấu hiệu hành vi (im lặng + ít tương tác)
    → escalate sang PHQ-9 → submit PHQ-9 tổng 7 điểm → level 2 (Nâng cao).
    """

    UID = "user_p2_mild"

    def test_flow(self, env, monkeypatch):
        from controllers.phq_controller import submit_phq, get_pending_phq

        # Setup: stub scout signals để có 2 flag (A + C)
        monkeypatch.setattr("services.scout.compute_silence_hours", lambda uid: 30.0)
        monkeypatch.setattr("services.scout.compute_academic_pressure", lambda uid: 0)
        monkeypatch.setattr("services.scout.compute_interaction_ratio", lambda uid: 0.4)
        monkeypatch.setattr("services.scout.compute_flat_affect_avg", lambda uid: None)
        monkeypatch.setattr("services.scout.compute_phq2_trend", lambda uid: 0.0)

        # Bước 1: Submit PHQ-2 total=2
        r2 = submit_phq(self.UID, phq2_body(total=2))
        assert r2["decision"] == "escalate_phq9"
        assert r2["next_phq2_days"] is None
        phq2_id = r2["id"]

        # Bước 2: Pending PHQ-9 được tự tạo (trigger_phq chạy trong submit)
        pending = get_pending_phq(self.UID)
        assert pending["has_phq9"] is True
        assert pending["pending"][0]["reason"] == "escalate_phq9"

        # Bước 3: User làm PHQ-9 (tổng = 7 → mild → level 2)
        # scores: 1 ở 7 câu đầu + 0 ở Q8,Q9 → total=7, Q9=0
        r9 = submit_phq(self.UID, phq9_body(
            scores=[1, 1, 1, 1, 1, 1, 1, 0, 0],
            triggered_by=phq2_id,
        ))

        # --- Output kỳ vọng ---
        assert r9["severity"] == "mild"
        assert r9["monitoring_level"] == 2
        assert r9["next_phq9_date"] is not None  # mild = 21d
        # Chưa đủ level 3 → KHÔNG có counselor alert
        assert env["db"].read_collection("counselor_alerts") == []
        # Q9=0 → KHÔNG có safety event
        assert env["db"].read_collection("safety_events") == []

        mon = env["db"].read_doc(f"users/{self.UID}/monitoring", "current")
        assert mon["level"] == 2
        assert mon["phq9_total"] == 7
        assert mon["q9_value"] == 0

        _print_flow("P2 — Mild via flags → Level 2", [
            f"PHQ-2 total=2 + 2 flags → escalate_phq9 ✓",
            f"Pending PHQ-9 tự tạo ✓",
            f"PHQ-9 total=7 → severity={r9['severity']}, level={r9['monitoring_level']} ✓",
            f"Không counselor alert (chưa đủ L3) ✓",
        ])


# ===========================================================================
#  P3 — Moderate L3 (counselor alert xuất hiện)
# ===========================================================================

class TestP3_ModerateWithAlert:
    """
    Kịch bản: PHQ-9 tổng 12 → level 3 (Cao) → có counselor alert deadline 48h.
    """

    UID = "user_p3_moderate"

    def test_flow(self, env):
        from controllers.phq_controller import submit_phq

        r = submit_phq(self.UID, phq9_body(
            scores=[2, 2, 1, 1, 2, 1, 1, 2, 0],  # total=12, Q9=0
        ))

        assert r["severity"] == "moderate"
        assert r["monitoring_level"] == 3

        alerts = env["db"].read_collection("counselor_alerts")
        assert len(alerts) == 1
        assert alerts[0]["data"]["type"] == "phq9_result"
        assert alerts[0]["data"]["level"] == 3
        assert alerts[0]["data"]["deadline_hours"] == 48

        # Q9=0 → không safety event
        assert env["db"].read_collection("safety_events") == []

        _print_flow("P3 — Moderate L3", [
            f"PHQ-9 total=12 → severity=moderate, level=3 ✓",
            f"counselor_alert deadline=48h ✓",
            f"No safety event (Q9=0) ✓",
        ])


# ===========================================================================
#  P4 — Active L4 qua Q9 override (tổng thấp nhưng Q9=1)
# ===========================================================================

class TestP4_Q9OverrideActive:
    """
    Kịch bản quan trọng về AN TOÀN:
    Tổng PHQ-9 chỉ 10 (moderate theo bảng) nhưng Q9=1 (có ý định vài ngày)
    → ép lên level 4 (Tích cực) + TỰ TẠO safety event.
    """

    UID = "user_p4_q9_active"

    def test_flow(self, env):
        from controllers.phq_controller import submit_phq

        r = submit_phq(self.UID, phq9_body(
            scores=[2, 2, 1, 1, 1, 1, 1, 0, 1],  # total=10, Q9=1
        ))

        # Severity vẫn theo total (moderate), nhưng monitoring_level bị Q9 override lên 4
        assert r["severity"] == "moderate"
        assert r["monitoring_level"] == 4

        # Safety event phải tự tạo (top-level + subcol)
        top_events = env["db"].read_collection("safety_events")
        assert len(top_events) == 1
        assert top_events[0]["q9_value"] == 1
        assert top_events[0]["action_taken"] == "passive_ideation"
        assert top_events[0]["counselor_notified"] is True
        assert top_events[0]["permanent"] is True

        user_events = env["db"].read_collection(f"users/{self.UID}/safety_events")
        assert len(user_events) == 1  # cũng copy xuống user

        # Counselor alert vì level ≥ 3 (24h deadline cho L4)
        alerts = env["db"].read_collection("counselor_alerts")
        assert any(a["data"]["deadline_hours"] == 24 for a in alerts)

        _print_flow("P4 — Q9=1 Active L4", [
            f"PHQ-9 total=10 nhưng Q9=1 → level bị override lên 4 ✓",
            f"Safety event tự tạo: action=passive_ideation ✓",
            f"Counselor alert deadline=24h ✓",
        ])


# ===========================================================================
#  P5 — Crisis L5 (case nặng nhất)
# ===========================================================================

class TestP5_Crisis:
    """
    Kịch bản khủng hoảng: PHQ-9 total=22, Q9=3.
    Expected: severity=severe, level=5, safety event crisis_same_day,
              counselor alert deadline=0h, next_phq9=+7d (severe).
    """

    UID = "user_p5_crisis"

    def test_flow(self, env):
        from controllers.phq_controller import submit_phq

        r = submit_phq(self.UID, phq9_body(
            scores=[3, 3, 3, 2, 2, 2, 2, 2, 3],  # total=22, Q9=3
        ))

        assert r["severity"] == "severe"
        assert r["monitoring_level"] == 5

        # next_phq9 = 3 ngày (Q9=3 override, PDF tr.12 — không phải 7d của severe)
        assert r["next_phq9_date"] is not None
        next_dt = datetime.fromisoformat(r["next_phq9_date"])
        days_diff = (next_dt - datetime.now(timezone.utc)).days
        assert 2 <= days_diff <= 3

        # Safety event cấp cao nhất
        events = env["db"].read_collection("safety_events")
        assert len(events) == 1
        assert events[0]["q9_value"] == 3
        assert events[0]["action_taken"] == "crisis_same_day"

        # Counselor alert deadline = 0 (lập tức)
        alerts = env["db"].read_collection("counselor_alerts")
        deadlines = [a["data"]["deadline_hours"] for a in alerts]
        assert 0 in deadlines

        _print_flow("P5 — Crisis L5", [
            f"PHQ-9 total=22 Q9=3 → severity=severe, level=5 ✓",
            f"next_phq9 ≈ +3 ngày (Q9 override theo PDF tr.12) ✓",
            f"Safety event crisis_same_day ✓",
            f"Counselor alert deadline=0h (tức thì) ✓",
        ])


# ===========================================================================
#  P6 — Rejection → pause 30 ngày sau 3 lần
# ===========================================================================

class TestP6_RejectionPause:
    """
    Kịch bản: user từ chối PHQ-2 liên tục 3 lần → hệ thống tạm dừng 30 ngày.
    """

    UID = "user_p6_reject"

    def test_flow(self, env):
        from controllers.phq_controller import reject_phq, trigger_phq

        # Setup 1 pending PHQ-2 (giả lập đang bị gợi)
        trigger_phq(self.UID, "phq2", reason="scheduled")

        # Lần 1 & 2: chỉ snooze, không pause
        r1 = reject_phq(self.UID, "phq2")
        assert r1["rejection_count"] == 1
        assert r1["paused"] is False
        assert r1["show_after"] is not None

        r2 = reject_phq(self.UID, "phq2")
        assert r2["rejection_count"] == 2
        assert r2["paused"] is False

        # Lần 3: pause 30 ngày
        r3 = reject_phq(self.UID, "phq2")
        assert r3["rejection_count"] == 3
        assert r3["paused"] is True
        assert r3["paused_until"] is not None

        paused_dt = datetime.fromisoformat(r3["paused_until"])
        days = (paused_dt - datetime.now(timezone.utc)).days
        assert 29 <= days <= 30

        # Thử trigger mới → bị reject vì đang pause
        r_trigger = trigger_phq(self.UID, "phq2", reason="scheduled")
        assert r_trigger["triggered"] is False
        assert "tạm dừng" in r_trigger["reason"]

        _print_flow("P6 — Reject 3x → Pause", [
            f"Reject #1: count=1, paused=False, snooze ✓",
            f"Reject #2: count=2, paused=False ✓",
            f"Reject #3: count=3, paused=True, paused_until=+30d ✓",
            f"Trigger mới bị chặn (đang paused) ✓",
        ])


# ===========================================================================
#  P7 — Scout auto-trigger → pending PHQ-2 + FCM
# ===========================================================================

class TestP7_ScoutTrigger:
    """
    Kịch bản: Scout phát hiện rủi ro cao (silence 72h, flat affect 0.8)
    → action = schedule_phq2 → tự tạo pending PHQ-2 + gửi FCM "Kiểm tra tâm trạng".

    Để Scout cho ra risk > 6 và PHQ-2 cuối > 7 ngày trước.
    """

    UID = "user_p7_scout"

    def test_flow(self, env, monkeypatch):
        from services.scout import (
            compute_risk_score, decide_action, _execute_action, _save_scout_log,
        )

        # Giả time hiện tại là 14:00 VN (07:00 UTC) → KHÔNG phải quiet hour
        fixed_now = datetime(2026, 4, 18, 7, 0, 0, tzinfo=timezone.utc)
        _patch_now(monkeypatch, fixed_now)

        # Seed 1 PHQ-2 cũ 10 ngày trước để thỏa điều kiện ≥ 7 ngày
        old_phq2 = (fixed_now - timedelta(days=10)).isoformat()
        env["db"].collection("users").document(self.UID).collection("phq_results") \
            .document("old_phq2").set({
                "id": "old_phq2",
                "phq_type": "phq2",
                "total": 1,
                "created_at": old_phq2,
            })

        # Signals cho risk score cao: silence 72h (+4) + flat affect 0.8 (+1.5)
        # + interaction 0.2 (+2) + academic 6 (+2) → tổng ~9.5 > 6
        signals = {
            "silence_hours":     72.0,
            "phq2_trend":        0.0,
            "academic_pressure": 6,
            "interaction_ratio": 0.2,
            "flat_affect_avg":   0.8,
            "duchenne_avg":      None,
        }
        risk = compute_risk_score(signals)
        assert risk > 6, f"risk score {risk} phải > 6"

        action = decide_action(self.UID, risk, signals)
        assert action == "schedule_phq2"

        # Thực thi: tạo pending PHQ-2 + gửi FCM
        sent = _execute_action(self.UID, action, fcm_token="fake_fcm_token_abc")
        assert sent is True

        # Kiểm tra pending PHQ-2 đã tạo
        from controllers.phq_controller import get_pending_phq
        pending = get_pending_phq(self.UID)
        assert pending["has_phq2"] is True
        assert pending["pending"][0]["reason"] == "scout"

        # FCM đã gửi với title "Kiểm tra tâm trạng" (không dùng chữ PHQ-2 — theo UI-14)
        assert len(env["fcm_log"]) == 1
        assert env["fcm_log"][0]["title"] == "Kiểm tra tâm trạng"
        assert "PHQ" not in env["fcm_log"][0]["title"]
        assert "PHQ" not in env["fcm_log"][0]["body"]

        _print_flow("P7 — Scout schedule_phq2", [
            f"Signals → risk={risk:.1f} (>6) ✓",
            f"decide_action → schedule_phq2 ✓",
            f"Pending PHQ-2 tạo với reason=scout ✓",
            f"FCM gửi title='Kiểm tra tâm trạng' (không lộ PHQ) ✓",
        ])


# ===========================================================================
#  P8 — Quiet hours (22:00–6:59 VN) → FCM bị chặn
# ===========================================================================

class TestP8_QuietHours:
    """
    Kịch bản: Scout quyết định gửi nhắc nhở nhưng đang 23:30 VN → skip FCM.
    Safety alert thì bypass quiet hour (test luôn).
    """

    UID = "user_p8_quiet"

    def test_quiet_hour_blocks_regular_notification(self, env, monkeypatch):
        from services.notification import send_scout_notification

        # 23:30 VN = 16:30 UTC → quiet
        fixed_now = datetime(2026, 4, 18, 16, 30, 0, tzinfo=timezone.utc)
        _patch_now(monkeypatch, fixed_now)

        sent = send_scout_notification(
            self.UID, "fake_token",
            title="Mình nhớ bạn!",
            body="Ghé check-in nha",
            noti_type="scout_reminder",
        )
        assert sent is False
        assert env["fcm_log"] == []  # không gửi FCM

    def test_quiet_hour_bypassed_for_safety_alert(self, env, monkeypatch):
        from services.notification import send_scout_notification

        fixed_now = datetime(2026, 4, 18, 16, 30, 0, tzinfo=timezone.utc)
        _patch_now(monkeypatch, fixed_now)

        sent = send_scout_notification(
            self.UID, "fake_token",
            title="Bạn không đơn độc",
            body="Mình luôn ở đây",
            noti_type="safety_alert",
        )
        assert sent is True
        assert len(env["fcm_log"]) == 1

        _print_flow("P8 — Quiet hours behavior", [
            f"23:30 VN + scout_reminder → FCM BỊ CHẶN ✓",
            f"23:30 VN + safety_alert → FCM VẪN GỬI (bypass) ✓",
        ])


# ===========================================================================
#  P9 — Q9 override next_phq9_date (regression cho bug đã fix)
# ===========================================================================

class TestP9_Q9OverrideInterval:
    """
    PDF tr.12 — bảng Q9:
      Q9=1 → next PHQ-9 sau 7 ngày (Tích cực)
      Q9=2 → next PHQ-9 sau 3 ngày (Khủng hoảng)
      Q9=3 → next PHQ-9 sau 3 ngày (Khủng hoảng)

    Bug cũ: code dùng severity-by-total nên Q9=1 với total=6 (mild)
    sẽ ra 21 ngày thay vì 7. Đây là vấn đề AN TOÀN — sinh viên có ý tưởng
    tự hại bị hẹn đánh giá lại quá xa.

    Class này test 3 kịch bản Q9 × 3 mức total để bảo đảm override đúng
    bất kể severity.
    """

    @pytest.mark.parametrize("total, q9, expected_days", [
        # Q9=1: luôn 7 ngày, không phụ thuộc total
        (6,  1, 7),   # mild + Q9=1
        (12, 1, 7),   # moderate + Q9=1
        (17, 1, 7),   # moderately_severe + Q9=1
        # Q9=2: luôn 3 ngày
        (8,  2, 3),
        (14, 2, 3),
        (20, 2, 3),
        # Q9=3: luôn 3 ngày
        (10, 3, 3),
        (22, 3, 3),
        # Q9=0: theo bảng severity (chứng minh không ảnh hưởng case bình thường)
        (7,  0, 21),  # mild → 21d
        (12, 0, 14),  # moderate → 14d
        (17, 0, 10),  # moderately_severe → 10d
        (22, 0, 7),   # severe → 7d
    ])
    def test_next_phq9_interval(self, env, total, q9, expected_days):
        from controllers.phq_controller import submit_phq

        # Dựng scores hợp lệ: Q9 = q9, còn lại chia đều
        remaining = total - q9
        base = remaining // 8
        scores = [base] * 8 + [q9]
        # Thêm phần dư vào các câu đầu
        for i in range(remaining - base * 8):
            scores[i] += 1
        assert sum(scores) == total
        assert scores[8] == q9

        uid = f"user_p9_t{total}_q{q9}"
        r = submit_phq(uid, phq9_body(scores=scores, q9=q9))

        if expected_days == 0:
            assert r["next_phq9_date"] is None
        else:
            assert r["next_phq9_date"] is not None
            next_dt = datetime.fromisoformat(r["next_phq9_date"])
            days = (next_dt - datetime.now(timezone.utc)).days
            # Tolerance 1 ngày cho test chạy cuối ngày
            assert expected_days - 1 <= days <= expected_days, \
                f"total={total}, Q9={q9}: expect {expected_days}d, got {days}d"


# ---------------------------------------------------------------------------
#  Helper: patch datetime.now(timezone.utc) trong notification + scout
# ---------------------------------------------------------------------------

def _patch_now(monkeypatch, fixed_now: datetime):
    """
    Patch datetime.now trong 3 module để cho phép freeze time.
    Dùng class wrapper thay vì replace cả datetime (tránh vỡ fromisoformat).
    """

    class _FrozenDatetime(datetime):
        @classmethod
        def now(cls, tz=None):
            if tz is None:
                return fixed_now.replace(tzinfo=None)
            return fixed_now.astimezone(tz)

    monkeypatch.setattr("services.notification.datetime", _FrozenDatetime)
    monkeypatch.setattr("services.scout.datetime", _FrozenDatetime)
    monkeypatch.setattr("controllers.phq_controller.datetime", _FrozenDatetime)
