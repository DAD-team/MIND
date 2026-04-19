"""
Wellbeing Scout — Tầng 1: Quan sát thụ động.

Chạy mỗi 2 giờ (7:00–23:00), tính 6 tín hiệu cho từng user,
cộng điểm rủi ro hành vi (0–10), rồi chọn đúng 1 hành động.
"""

import logging
import uuid
from datetime import datetime, timezone, timedelta

from services.firebase import get_db

logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
#  Tín hiệu 1 — Khoảng im lặng (giờ)
# ---------------------------------------------------------------------------
def compute_silence_hours(uid: str) -> float:
    db = get_db()
    docs = (
        db.collection("users").document(uid)
        .collection("interactions")
        .order_by("timestamp", direction="DESCENDING")
        .limit(1)
        .stream()
    )
    for d in docs:
        ts = d.to_dict().get("timestamp")
        if ts:
            delta = datetime.now(timezone.utc) - datetime.fromisoformat(ts)
            return round(delta.total_seconds() / 3600, 1)
    return 999.0  # chưa có interaction nào


# ---------------------------------------------------------------------------
#  Tín hiệu 2 — Xu hướng điểm PHQ-2 (độ dốc)
# ---------------------------------------------------------------------------
def compute_phq2_trend(uid: str) -> float:
    db = get_db()
    all_docs = list(
        db.collection("users").document(uid)
        .collection("phq_results")
        .order_by("created_at", direction="DESCENDING")
        .limit(20)
        .stream()
    )
    phq2_docs = [d for d in all_docs if d.to_dict().get("phq_type") == "phq2"][:3]
    scores = [d.to_dict().get("total", 0) for d in phq2_docs]
    if len(scores) < 2:
        return 0.0
    scores.reverse()  # chronological: oldest first
    # Độ dốc = (S_last - S_first) / (n - 1)
    return (scores[-1] - scores[0]) / (len(scores) - 1)


# ---------------------------------------------------------------------------
#  Tín hiệu 3 — Áp lực học thuật (tổng trọng số 3 ngày tới)
# ---------------------------------------------------------------------------
def compute_academic_pressure(uid: str) -> int:
    from controllers.schedule_controller import upcoming_schedules
    result = upcoming_schedules(uid, 3)
    return result.get("total_weight", 0)


# ---------------------------------------------------------------------------
#  Tín hiệu 4 — Tỷ lệ tương tác (7 ngày gần / 7 ngày trước)
# ---------------------------------------------------------------------------
def compute_interaction_ratio(uid: str) -> float:
    db = get_db()
    now = datetime.now(timezone.utc)
    seven_days_ago = (now - timedelta(days=7)).isoformat()
    fourteen_days_ago = (now - timedelta(days=14)).isoformat()

    col = db.collection("users").document(uid).collection("interactions")

    recent = len(list(
        col.where("timestamp", ">=", seven_days_ago).stream()
    ))
    previous = len(list(
        col.where("timestamp", ">=", fourteen_days_ago)
           .where("timestamp", "<", seven_days_ago)
           .stream()
    ))

    if previous == 0:
        return 1.0 if recent > 0 else 1.0  # chưa có dữ liệu cũ → coi như ổn
    return round(recent / previous, 2)


# ---------------------------------------------------------------------------
#  Tín hiệu 5 — Biểu cảm phẳng trung bình 7 ngày (consent ≥ 2)
# ---------------------------------------------------------------------------
def compute_flat_affect_avg(uid: str) -> float | None:
    db = get_db()
    seven_days_ago = (datetime.now(timezone.utc) - timedelta(days=7)).isoformat()
    docs = (
        db.collection("users").document(uid)
        .collection("analyses")
        .where("analyzed_at", ">=", seven_days_ago)
        .stream()
    )
    values = []
    for d in docs:
        data = d.to_dict()
        result = data.get("result", {})
        fa = result.get("flat_affect_score")
        if fa is not None:
            values.append(fa)
    if not values:
        return None
    return round(sum(values) / len(values), 3)


# ---------------------------------------------------------------------------
#  Tín hiệu 6 — Nụ cười thật trung bình 7 ngày (consent ≥ 2)
# ---------------------------------------------------------------------------
def compute_duchenne_avg(uid: str) -> float | None:
    db = get_db()
    seven_days_ago = (datetime.now(timezone.utc) - timedelta(days=7)).isoformat()
    docs = (
        db.collection("users").document(uid)
        .collection("analyses")
        .where("analyzed_at", ">=", seven_days_ago)
        .stream()
    )
    values = []
    for d in docs:
        data = d.to_dict()
        result = data.get("result", {})
        dr = result.get("duchenne_ratio")
        if dr is not None:
            values.append(dr)
    if not values:
        return None
    return round(sum(values) / len(values), 3)


# ---------------------------------------------------------------------------
#  Tính điểm rủi ro hành vi (0–10)
# ---------------------------------------------------------------------------
def _score_silence(hours: float) -> float:
    if hours >= 72:
        return 4.0
    if hours >= 48:
        return 3.0
    if hours >= 24:
        return 2.0
    if hours >= 12:
        return 1.0
    return 0.0


def _score_phq2_trend(slope: float) -> float:
    if slope > 0.5:
        return 2.0
    if slope > 0:
        return 1.0
    return 0.0


def _score_academic(total_weight: int) -> float:
    if total_weight >= 6:
        return 2.0
    if total_weight >= 3:
        return 1.5
    if total_weight >= 1:
        return 1.0
    return 0.0


def _score_interaction_ratio(ratio: float) -> float:
    if ratio < 0.3:
        return 2.0
    if ratio < 0.5:
        return 1.5
    if ratio < 0.7:
        return 0.5
    return 0.0


def _score_flat_affect(avg: float | None) -> float:
    if avg is None:
        return 0.0
    if avg > 0.7:
        return 1.5
    if avg >= 0.5:
        return 0.5
    return 0.0


def _score_duchenne(avg: float | None) -> float:
    if avg is None:
        return 0.0
    if avg < 0.2:
        return 1.0
    return 0.0


def compute_risk_score(signals: dict) -> float:
    score = (
        _score_silence(signals["silence_hours"])
        + _score_phq2_trend(signals["phq2_trend"])
        + _score_academic(signals["academic_pressure"])
        + _score_interaction_ratio(signals["interaction_ratio"])
        + _score_flat_affect(signals.get("flat_affect_avg"))
        + _score_duchenne(signals.get("duchenne_avg"))
    )
    return min(score, 10.0)


# ---------------------------------------------------------------------------
#  Quyết định hành động
# ---------------------------------------------------------------------------
# Hành động trả về:
#   "safety_protocol"   — Kích hoạt giao thức khủng hoảng
#   "schedule_phq2"     — Lên lịch gửi PHQ-2 sớm
#   "gentle_reminder"   — Gửi nhắc nhở nhẹ nhàng
#   "mark_priority"     — Đánh dấu ưu tiên (khi đến hạn PHQ-2)
#   "log_only"          — Chỉ ghi nhật ký

def decide_action(uid: str, risk_score: float, signals: dict) -> str:
    db = get_db()

    # --- Ưu tiên 1: Kiểm tra q9 gần nhất ---
    mon_snap = (
        db.collection("users").document(uid)
        .collection("monitoring").document("current").get()
    )
    mon = mon_snap.to_dict() if mon_snap.exists else {}
    current_level = mon.get("level", 1)

    if current_level not in (4, 5):  # chưa Tích cực hoặc Khủng hoảng
        all_docs = list(
            db.collection("users").document(uid)
            .collection("phq_results")
            .order_by("created_at", direction="DESCENDING")
            .limit(10)
            .stream()
        )
        phq9_docs = [d for d in all_docs if d.to_dict().get("phq_type") == "phq9"][:1]
        if phq9_docs:
            last_phq9 = phq9_docs[0].to_dict()
            if (last_phq9.get("q9_value") or 0) >= 1:
                return "safety_protocol"

    # --- Ưu tiên 2: Risk > 6 + PHQ-2 cách ≥ 7 ngày ---
    if risk_score > 6:
        all_phq_docs = list(
            db.collection("users").document(uid)
            .collection("phq_results")
            .order_by("created_at", direction="DESCENDING")
            .limit(10)
            .stream()
        )
        last_phq2_docs = [d for d in all_phq_docs if d.to_dict().get("phq_type") == "phq2"][:1]
        days_since_phq2 = 999
        if last_phq2_docs:
            last_created = last_phq2_docs[0].to_dict().get("created_at", "")
            if last_created:
                delta = datetime.now(timezone.utc) - datetime.fromisoformat(last_created)
                days_since_phq2 = delta.days

        if days_since_phq2 >= 7:
            return "schedule_phq2"

        # --- Ưu tiên 3: Risk > 6 + PHQ-2 < 7 ngày + consent ≥ 3 ---
        user_snap = db.collection("users").document(uid).get()
        consent = (user_snap.to_dict() or {}).get("consent_level", 1)
        if consent >= 3:
            return "gentle_reminder"

    # --- Ưu tiên 4: Risk >= 4 ---
    # Bao gồm cả trường hợp risk > 6 nhưng không thỏa ưu tiên 2, 3
    # (PHQ-2 < 7 ngày + consent < 3)
    if risk_score >= 4:
        return "mark_priority"

    # --- Ưu tiên 5: Risk < 4 ---
    return "log_only"


# ---------------------------------------------------------------------------
#  Lưu scout log
# ---------------------------------------------------------------------------
def _save_scout_log(uid: str, signals: dict, risk_score: float,
                    action: str, notification_sent: bool):
    db = get_db()
    log_id = str(uuid.uuid4())
    now = datetime.now(timezone.utc).isoformat()
    db.collection("users").document(uid).collection("scout_logs").document(log_id).set({
        "id":                log_id,
        "timestamp":         now,
        "signals":           signals,
        "risk_score":        risk_score,
        "action_chosen":     action,
        "notification_sent": notification_sent,
        "ttl_delete_after":  (datetime.now(timezone.utc) + timedelta(days=90)).isoformat(),
    })


# ---------------------------------------------------------------------------
#  Thực thi hành động
# ---------------------------------------------------------------------------
def _execute_action(uid: str, action: str, fcm_token: str | None) -> bool:
    from services.notification import send_scout_notification, send_counselor_alert

    if action == "safety_protocol":
        from controllers.safety_controller import create_safety_event_internal
        from models.safety import SafetyEventCreate
        # Lấy lại q9 value từ PHQ-9 gần nhất
        db = get_db()
        phq9_docs = list(
            db.collection("users").document(uid)
            .collection("phq_results")
            .where("phq_type", "==", "phq9")
            .order_by("created_at", direction="DESCENDING")
            .limit(1)
            .stream()
        )
        if phq9_docs:
            data = phq9_docs[0].to_dict()
            q9 = data.get("q9_value", 1)
            total = data.get("total", 0)
            phq9_id = data.get("id")
            create_safety_event_internal(uid, SafetyEventCreate(
                q9_value=q9, phq9_total=total, phq9_id=phq9_id,
            ))
            # Thông báo tư vấn viên
            send_counselor_alert(uid, {
                "type":       "safety_protocol",
                "q9_value":   q9,
                "phq9_total": total,
                "phq9_id":    phq9_id,
            })
        # Gửi FCM cho user (bypass rate limit)
        if fcm_token:
            send_scout_notification(
                uid, fcm_token,
                title="Bạn không đơn độc",
                body="Mình luôn ở đây. Bấm vào để xem thông tin hỗ trợ nhé.",
                noti_type="safety_alert",
            )
        return True

    if action == "schedule_phq2":
        # Tạo pending PHQ-2 để frontend biết cần hiển thị
        from controllers.phq_controller import trigger_phq
        trigger_phq(uid, "phq2", reason="scout")

        if fcm_token:
            return send_scout_notification(
                uid, fcm_token,
                title="Kiểm tra tâm trạng",
                body="Mình muốn hỏi bạn 2 câu ngắn — chưa đến 30 giây nhé!",
                noti_type="phq_schedule",
            )
        return True  # vẫn trigger dù không gửi được notification

    if action == "gentle_reminder":
        if fcm_token:
            return send_scout_notification(
                uid, fcm_token,
                title="Mình nhớ bạn!",
                body="Lâu rồi không gặp, ghé check-in nhanh nha 💙",
                noti_type="scout_reminder",
            )
        logger.info(f"[Scout] gentle_reminder cho {uid[:8]}... nhưng không có FCM token")
        return False

    if action == "mark_priority":
        # Đánh dấu ưu tiên trong monitoring → khi đến hạn PHQ-2 sẽ được gửi sớm
        db = get_db()
        db.collection("users").document(uid) \
          .collection("monitoring").document("current").set({
            "scout_priority":    True,
            "scout_priority_at": datetime.now(timezone.utc).isoformat(),
        }, merge=True)
        return False

    # log_only → không gửi notification, không ghi thêm gì
    return False


# ---------------------------------------------------------------------------
#  Chu kỳ chính — gọi từ scheduler
# ---------------------------------------------------------------------------
def run_scout_cycle():
    now = datetime.now(timezone.utc)
    hour_vn = (now.hour + 7) % 24  # UTC+7

    if hour_vn < 7 or hour_vn >= 23:
        logger.info("[Scout] Ngoài giờ hoạt động (7:00–23:00 VN), bỏ qua.")
        return

    db = get_db()
    users = db.collection("users").stream()

    processed = 0
    for user_doc in users:
        uid = user_doc.id
        user = user_doc.to_dict()
        consent = user.get("consent_level", 1)
        fcm_token = user.get("fcm_token")

        # Tính 6 tín hiệu
        signals = {
            "silence_hours":     compute_silence_hours(uid),
            "phq2_trend":        compute_phq2_trend(uid),
            "academic_pressure": compute_academic_pressure(uid),
            "interaction_ratio": compute_interaction_ratio(uid),
        }

        # Tín hiệu 5, 6 chỉ khi consent ≥ 2
        if consent >= 2:
            signals["flat_affect_avg"] = compute_flat_affect_avg(uid)
            signals["duchenne_avg"]    = compute_duchenne_avg(uid)
        else:
            signals["flat_affect_avg"] = None
            signals["duchenne_avg"]    = None

        risk_score = compute_risk_score(signals)
        action = decide_action(uid, risk_score, signals)
        notification_sent = _execute_action(uid, action, fcm_token)

        _save_scout_log(uid, signals, risk_score, action, notification_sent)
        processed += 1

    logger.info(f"[Scout] Cycle complete — {processed} users processed.")
