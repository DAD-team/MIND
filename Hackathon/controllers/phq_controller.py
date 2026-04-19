import uuid
import logging
from datetime import datetime, timezone, timedelta
from fastapi import HTTPException
from models.phq import PhqSubmit
from services.firebase import get_db

logger = logging.getLogger(__name__)


def _now() -> datetime:
    return datetime.now(timezone.utc)


def _severity(total: int) -> str:
    if total <= 4:  return "minimal"
    if total <= 9:  return "mild"
    if total <= 14: return "moderate"
    if total <= 19: return "moderately_severe"
    return "severe"


def _monitoring_level(total: int, q9: int) -> int:
    # Q9 override theo PDF tr.11:
    #   q9=1 (vài ngày)        → ACTIVE (4)
    #   q9=2 (hơn nửa số ngày) → CRISIS (5)
    #   q9=3 (gần như mỗi ngày)→ CRISIS (5)
    if q9 >= 2: return 5
    if q9 >= 1: return 4
    if total <= 4:  return 1
    if total <= 9:  return 2
    if total <= 14: return 3
    if total <= 19: return 4
    return 5


# PHQ-9: số ngày đến lần đánh giá tiếp
_PHQ9_NEXT_DAYS = {
    "minimal": 0, "mild": 21, "moderate": 14,
    "moderately_severe": 10, "severe": 7,
}


# ---------------------------------------------------------------------------
#  Xóa pending PHQ khi user đã hoàn thành
# ---------------------------------------------------------------------------
def _clear_pending(uid: str, phq_type: str):
    db = get_db()
    # Query 1 field, filter còn lại trong Python để tránh composite index
    pending_docs = (
        db.collection("users").document(uid)
        .collection("pending_phq")
        .where("status", "==", "pending")
        .stream()
    )
    for d in pending_docs:
        if d.to_dict().get("phq_type") == phq_type:
            d.reference.update({"status": "completed", "completed_at": _now().isoformat()})

    # Xóa flag trong monitoring
    mon_ref = db.collection("users").document(uid) \
                .collection("monitoring").document("current")
    mon_ref.set({
        f"pending_{phq_type}": False,
        f"pending_{phq_type}_id": None,
        f"pending_{phq_type}_reason": None,
    }, merge=True)


# ---------------------------------------------------------------------------
#  Xử lý kết quả PHQ-2 — 4 nhánh
# ---------------------------------------------------------------------------
def _count_behavioral_flags(uid: str) -> int:
    from services.scout import (
        compute_silence_hours, compute_academic_pressure,
        compute_interaction_ratio, compute_flat_affect_avg,
        compute_phq2_trend,
    )
    flags = 0
    # A. Im lặng kéo dài ≥ 24 giờ
    if compute_silence_hours(uid) >= 24:
        flags += 1
    # B. Áp lực học thuật cao (tổng trọng số ≥ 3)
    if compute_academic_pressure(uid) >= 3:
        flags += 1
    # C. Giảm tương tác (tỷ lệ < 0.5)
    if compute_interaction_ratio(uid) < 0.5:
        flags += 1
    # D. Biểu cảm phẳng (trung bình > 0.5, chỉ khi có dữ liệu video)
    fa = compute_flat_affect_avg(uid)
    if fa is not None and fa > 0.5:
        flags += 1
    # E. Xu hướng PHQ-2 xấu dần (độ dốc > 0)
    if compute_phq2_trend(uid) > 0:
        flags += 1
    return flags


def _process_phq2_result(uid: str, total: int, record_id: str) -> dict:
    db = get_db()
    mon_ref = db.collection("users").document(uid) \
                .collection("monitoring").document("current")

    # Nhánh 1: Dương tính rõ ràng
    if total >= 3:
        mon_ref.set({"phq2_decision": "escalate_phq9"}, merge=True)
        return {"decision": "escalate_phq9", "next_phq2_days": None}

    # Nhánh 2 & 3: Biên giới (total = 2)
    if total == 2:
        flags = _count_behavioral_flags(uid)
        if flags >= 2:
            # Nhánh 2: Đủ dấu hiệu → chuyển PHQ-9
            mon_ref.set({"phq2_decision": "escalate_phq9"}, merge=True)
            return {"decision": "escalate_phq9", "next_phq2_days": None}
        else:
            # Nhánh 3: Chưa đủ → rút ngắn lịch PHQ-2 còn 7 ngày
            next_date = (_now() + timedelta(days=7)).isoformat()
            mon_ref.set({
                "next_phq2_date": next_date,
                "phq2_decision":  "shorten_interval",
            }, merge=True)
            return {"decision": "shorten_interval", "next_phq2_days": 7}

    # Nhánh 4: Âm tính (total ≤ 1)
    next_date = (_now() + timedelta(days=14)).isoformat()
    mon_ref.set({
        "next_phq2_date": next_date,
        "phq2_decision":  "normal",
    }, merge=True)
    return {"decision": "normal", "next_phq2_days": 14}


# ---------------------------------------------------------------------------
#  Xử lý kết quả PHQ-9
# ---------------------------------------------------------------------------
def _process_phq9_result(uid: str, total: int, q9_value: int,
                         record_id: str, severity: str) -> dict:
    db  = get_db()
    now = _now()
    mon_ref = db.collection("users").document(uid) \
                .collection("monitoring").document("current")

    level = _monitoring_level(total, q9_value)

    # Next PHQ-9: Q9 override ưu tiên theo PDF tr.12
    #   Q9=1 (vài ngày)         → Tích cực, sau 7 ngày
    #   Q9≥2 (hơn nửa / mỗi ngày) → Khủng hoảng, sau 3 ngày
    # Nếu Q9=0 → dùng bảng theo severity (tr.13)
    if q9_value >= 2:
        next_days = 3
    elif q9_value >= 1:
        next_days = 7
    else:
        next_days = _PHQ9_NEXT_DAYS[severity]
    next_phq9_date = (now + timedelta(days=next_days)).isoformat() if next_days > 0 else None

    # Nếu minimal → quay về chỉ PHQ-2 mỗi 14 ngày
    next_phq2_date = None
    if severity == "minimal":
        next_phq2_date = (now + timedelta(days=14)).isoformat()

    mon_ref.set({
        "level":          level,
        "level_name":     _LEVEL_NAMES[level],
        "reason":         f"PHQ-9 total={total}, severity={severity}",
        "phq9_total":     total,
        "q9_value":       q9_value,
        "next_phq9_date": next_phq9_date,
        "next_phq2_date": next_phq2_date,
        "updated_at":     now.isoformat(),
    }, merge=True)

    # Thông báo tư vấn viên theo mức
    _notify_counselor_by_level(uid, level, total, q9_value, severity)

    # Phát hiện thay đổi đáng kể so với PHQ-9 trước
    _detect_significant_change(uid, total)

    # Kiểm tra quy tắc xuống thang
    _check_de_escalation(uid)

    return {
        "monitoring_level": level,
        "next_phq9_date":   next_phq9_date,
        "next_phq2_date":   next_phq2_date,
    }


_LEVEL_NAMES = {
    1: "Tiêu chuẩn", 2: "Nâng cao", 3: "Cao",
    4: "Tích cực", 5: "Khủng hoảng",
}


def _notify_counselor_by_level(uid: str, level: int, total: int,
                               q9_value: int, severity: str):
    from services.notification import send_counselor_alert
    if level >= 3:
        deadline_hours = {3: 48, 4: 24, 5: 0}[level]
        send_counselor_alert(uid, {
            "type":           "phq9_result",
            "phq9_total":     total,
            "q9_value":       q9_value,
            "severity":       severity,
            "level":          level,
            "deadline_hours": deadline_hours,
        })


def _detect_significant_change(uid: str, new_total: int):
    db = get_db()
    # Lấy tất cả rồi filter trong Python để tránh composite index
    all_docs = list(
        db.collection("users").document(uid)
        .collection("phq_results")
        .order_by("created_at", direction="DESCENDING")
        .limit(20)
        .stream()
    )
    prev_docs = [d for d in all_docs if d.to_dict().get("phq_type") == "phq9"][:2]
    if len(prev_docs) < 2:
        return
    prev_total = prev_docs[1].to_dict().get("total", 0)
    diff = new_total - prev_total

    if diff >= 5:
        from services.notification import send_counselor_alert
        send_counselor_alert(uid, {
            "type":       "significant_worsening",
            "prev_total": prev_total,
            "new_total":  new_total,
            "change":     diff,
        })
    elif diff <= -5:
        logger.info(f"[PHQ-9] Significant improvement for {uid[:8]}: {prev_total}→{new_total}")


def _check_de_escalation(uid: str):
    db = get_db()
    all_docs = list(
        db.collection("users").document(uid)
        .collection("phq_results")
        .order_by("created_at", direction="DESCENDING")
        .limit(20)
        .stream()
    )
    recent = [d for d in all_docs if d.to_dict().get("phq_type") == "phq9"][:2]
    if len(recent) < 2:
        return
    totals = [d.to_dict().get("total", 99) for d in recent]
    # 2 lần liên tiếp < 5 → xuống Tiêu chuẩn
    if all(t < 5 for t in totals):
        now = _now()
        mon_ref = db.collection("users").document(uid) \
                    .collection("monitoring").document("current")
        mon_ref.set({
            "level":          1,
            "level_name":     "Tiêu chuẩn",
            "reason":         "2 lần PHQ-9 liên tiếp < 5 — xuống thang",
            "next_phq9_date": None,
            "next_phq2_date": (now + timedelta(days=14)).isoformat(),
            "updated_at":     now.isoformat(),
        }, merge=True)
        logger.info(f"[PHQ-9] De-escalation for {uid[:8]}: back to standard.")


# ---------------------------------------------------------------------------
#  Submit PHQ (entry point)
# ---------------------------------------------------------------------------
def submit_phq(uid: str, body: PhqSubmit) -> dict:
    if body.phq_type not in ("phq2", "phq9"):
        raise HTTPException(status_code=422, detail="phq_type phải là 'phq2' hoặc 'phq9'")

    expected = 2 if body.phq_type == "phq2" else 9
    if len(body.scores) != expected:
        raise HTTPException(status_code=422, detail=f"{body.phq_type.upper()} cần {expected} điểm")

    now       = _now()
    record_id = str(uuid.uuid4())

    doc = {
        "id":         record_id,
        "user_id":    uid,
        "phq_type":   body.phq_type,
        "scores":     body.scores,
        "total":      body.total,
        "source":     body.source,
        "created_at": now.isoformat(),
    }

    if body.behavior_risk_score is not None:
        doc["behavior_risk_score"] = body.behavior_risk_score

    severity         = None
    monitoring_level = None

    if body.phq_type == "phq9":
        q9       = body.q9_value or 0
        severity = _severity(body.total)
        monitoring_level = _monitoring_level(body.total, q9)

        doc.update({
            "somatic_score":        body.somatic_score,
            "cognitive_score":      body.cognitive_score,
            "functional_impact":    body.functional_impact,
            "q9_value":             q9,
            "triggered_by_phq2_id": body.triggered_by_phq2_id,
            "severity":             severity,
            "monitoring_level":     monitoring_level,
        })

    get_db().collection("users").document(uid) \
            .collection("phq_results").document(record_id).set(doc)

    # Lưu interaction
    get_db().collection("users").document(uid) \
            .collection("interactions").add({
                "type": "phq",
                "timestamp": now.isoformat(),
                "ref_id": record_id,
            })

    # Reset bộ đếm từ chối khi hoàn thành bảng hỏi
    get_db().collection("users").document(uid) \
            .collection("monitoring").document("current") \
            .set({"rejection_count": 0}, merge=True)

    # Xóa pending PHQ tương ứng
    _clear_pending(uid, body.phq_type)

    # --- Xử lý kết quả theo loại ---
    response = {"id": record_id, "created_at": doc["created_at"]}

    if body.phq_type == "phq2":
        phq2_result = _process_phq2_result(uid, body.total, record_id)
        response.update(phq2_result)

        # Tự động tạo pending PHQ-9 khi escalate
        if phq2_result.get("decision") == "escalate_phq9":
            trigger_phq(uid, "phq9", reason="escalate_phq9")

    elif body.phq_type == "phq9":
        # Auto-trigger safety event nếu q9 ≥ 1
        if (body.q9_value or 0) >= 1:
            from controllers.safety_controller import create_safety_event_internal
            from models.safety import SafetyEventCreate
            create_safety_event_internal(
                uid,
                SafetyEventCreate(
                    q9_value=body.q9_value,
                    phq9_total=body.total,
                    phq9_id=record_id,
                ),
            )

        phq9_extra = _process_phq9_result(
            uid, body.total, body.q9_value or 0, record_id, severity,
        )
        response.update({
            "severity":         severity,
            "monitoring_level": monitoring_level,
        })
        response.update(phq9_extra)

    return response


def get_phq_history(uid: str, phq_type: str, limit: int) -> dict:
    db    = get_db()
    query = (
        db.collection("users").document(uid).collection("phq_results")
        .order_by("created_at", direction="DESCENDING")
        .limit(limit if phq_type == "all" else limit * 4)
    )

    records = []
    for d in query.stream():
        data = d.to_dict()
        if phq_type != "all" and data.get("phq_type") != phq_type:
            continue
        data.pop("user_id", None)
        records.append(data)
        if len(records) >= limit:
            break

    return {"records": records}


# ---------------------------------------------------------------------------
#  Tính thời điểm hiển thị lại theo quy tắc vận hành
# ---------------------------------------------------------------------------
def _compute_show_after() -> str:
    """Trả về ISO timestamp cho lần hiển thị tiếp theo, dựa vào giờ VN hiện tại."""
    now = _now()
    hour_vn = (now.hour + 7) % 24

    if 17 <= hour_vn < 20:
        # Đang trong khung giờ tốt nhất → chờ ngày mai 17:00 VN (= 10:00 UTC)
        tomorrow = now + timedelta(days=1)
        show_after = tomorrow.replace(hour=10, minute=0, second=0, microsecond=0)
    elif 10 <= hour_vn < 17 or 20 <= hour_vn < 22:
        # Khung giờ chấp nhận được → chờ 17:00 VN hôm sau
        tomorrow = now + timedelta(days=1)
        show_after = tomorrow.replace(hour=10, minute=0, second=0, microsecond=0)
    else:
        # 22:00–6:59 VN → chờ 17:00 VN hôm sau
        # Nếu 22–23h VN (15–16h UTC) → ngày mai
        # Nếu 0–6h VN (17–23h UTC ngày trước) → hôm nay
        if hour_vn >= 22:
            target = now + timedelta(days=1)
        else:
            target = now
        show_after = target.replace(hour=10, minute=0, second=0, microsecond=0)

    return show_after.isoformat()


# ---------------------------------------------------------------------------
#  Từ chối PHQ
# ---------------------------------------------------------------------------
def reject_phq(uid: str, phq_type: str) -> dict:
    if phq_type not in ("phq2", "phq9"):
        raise HTTPException(status_code=422, detail="phq_type phải là 'phq2' hoặc 'phq9'")

    db  = get_db()
    now = _now()
    mon_ref = db.collection("users").document(uid) \
                .collection("monitoring").document("current")
    mon_snap = mon_ref.get()
    mon = mon_snap.to_dict() if mon_snap.exists else {}

    count = mon.get("rejection_count", 0) + 1

    update = {
        "rejection_count":  count,
        "last_rejected_at": now.isoformat(),
    }

    paused = False
    show_after = None

    if count >= 3:
        # Tạm dừng 30 ngày — đánh dấu tất cả pending thành dismissed
        update["paused_until"] = (now + timedelta(days=30)).isoformat()
        paused = True
        logger.info(f"[PHQ] User {uid[:8]} paused for 30 days after {count} rejections.")

        pending_docs = (
            db.collection("users").document(uid)
            .collection("pending_phq")
            .where("status", "==", "pending")
            .stream()
        )
        for d in pending_docs:
            if d.to_dict().get("phq_type") == phq_type:
                d.reference.update({
                    "status":       "dismissed",
                    "dismissed_at": now.isoformat(),
                })
    else:
        # Chưa đủ 3 lần → snooze pending, hiển thị lại vào khung giờ tối ưu
        show_after = _compute_show_after()

        pending_docs = (
            db.collection("users").document(uid)
            .collection("pending_phq")
            .where("status", "==", "pending")
            .stream()
        )
        for d in pending_docs:
            if d.to_dict().get("phq_type") == phq_type:
                d.reference.update({"show_after": show_after})

    mon_ref.set(update, merge=True)

    return {
        "rejection_count": count,
        "paused":          paused,
        "paused_until":    update.get("paused_until"),
        "show_after":      show_after,
    }


# ---------------------------------------------------------------------------
#  Để sau PHQ-9 (khi chuyển tiếp từ PHQ-2)
# ---------------------------------------------------------------------------
# ---------------------------------------------------------------------------
#  Trigger PHQ — tạo pending PHQ cho user
# ---------------------------------------------------------------------------
def trigger_phq(uid: str, phq_type: str, reason: str) -> dict:
    if phq_type not in ("phq2", "phq9"):
        raise HTTPException(status_code=422, detail="phq_type phải là 'phq2' hoặc 'phq9'")

    db  = get_db()
    now = _now()

    pending_ref = db.collection("users").document(uid) \
                    .collection("monitoring").document("current")
    pending_snap = pending_ref.get()
    mon = pending_snap.to_dict() if pending_snap.exists else {}

    # Kiểm tra paused
    paused_until = mon.get("paused_until")
    if paused_until:
        paused_dt = datetime.fromisoformat(paused_until)
        if now < paused_dt:
            return {
                "triggered": False,
                "reason": f"User đang tạm dừng đến {paused_until}",
            }

    # Tạo pending PHQ
    pending_id = str(uuid.uuid4())
    pending_doc = {
        "id":         pending_id,
        "phq_type":   phq_type,
        "reason":     reason,
        "status":     "pending",
        "created_at": now.isoformat(),
    }

    db.collection("users").document(uid) \
      .collection("pending_phq").document(pending_id).set(pending_doc)

    # Cập nhật monitoring để ghi nhận
    pending_ref.set({
        f"pending_{phq_type}": True,
        f"pending_{phq_type}_id": pending_id,
        f"pending_{phq_type}_reason": reason,
        "updated_at": now.isoformat(),
    }, merge=True)

    return {
        "triggered":  True,
        "pending_id": pending_id,
        "phq_type":   phq_type,
        "reason":     reason,
        "created_at": pending_doc["created_at"],
    }


# ---------------------------------------------------------------------------
#  Lấy danh sách pending PHQ cho user
# ---------------------------------------------------------------------------
def get_pending_phq(uid: str) -> dict:
    db  = get_db()
    now = _now().isoformat()

    # Query chỉ dùng 1 field để tránh cần composite index
    pending_docs = (
        db.collection("users").document(uid)
        .collection("pending_phq")
        .where("status", "==", "pending")
        .stream()
    )

    items = []
    for d in pending_docs:
        data = d.to_dict()
        # Lọc bỏ item đang bị snooze (show_after > hiện tại)
        show_after = data.get("show_after")
        if show_after and show_after > now:
            continue
        items.append({
            "id":         data.get("id"),
            "phq_type":   data.get("phq_type"),
            "reason":     data.get("reason"),
            "created_at": data.get("created_at"),
        })
    # Sắp xếp trong Python thay vì Firestore
    items.sort(key=lambda x: x.get("created_at", ""), reverse=True)

    return {
        "pending": items,
        "has_phq2": any(i["phq_type"] == "phq2" for i in items),
        "has_phq9": any(i["phq_type"] == "phq9" for i in items),
    }


# ---------------------------------------------------------------------------
#  Để sau PHQ-9 (khi chuyển tiếp từ PHQ-2)
# ---------------------------------------------------------------------------
def defer_phq9(uid: str) -> dict:
    db  = get_db()
    now = _now()
    mon_ref = db.collection("users").document(uid) \
                .collection("monitoring").document("current")
    mon_snap = mon_ref.get()
    mon = mon_snap.to_dict() if mon_snap.exists else {}

    defer_count = mon.get("phq9_defer_count", 0) + 1

    if defer_count >= 2:
        # Lần 2: không nhắc nữa, tăng mức theo dõi lên ít nhất Nâng cao
        current_level = mon.get("level", 1)
        new_level = max(current_level, 2)
        mon_ref.set({
            "phq9_defer_count": defer_count,
            "phq9_defer_until": None,
            "level":            new_level,
            "level_name":       _LEVEL_NAMES[new_level],
            "updated_at":       now.isoformat(),
        }, merge=True)
        return {
            "defer_count":    defer_count,
            "remind_at":      None,
            "message":        "Không nhắc nữa. Mức theo dõi đã được nâng lên.",
            "monitoring_level": new_level,
        }

    # Lần 1: nhắc lại sau 4 giờ
    remind_at = (now + timedelta(hours=4)).isoformat()
    mon_ref.set({
        "phq9_defer_count": defer_count,
        "phq9_defer_until": remind_at,
        "updated_at":       now.isoformat(),
    }, merge=True)
    return {
        "defer_count": defer_count,
        "remind_at":   remind_at,
        "message":     "Sẽ nhắc lại sau 4 giờ.",
    }
