from datetime import datetime, timezone, timedelta
from fastapi import HTTPException
from models.monitoring import MonitoringUpdate
from services.firebase import get_db


_LEVEL_NAMES = {
    1: "Tiêu chuẩn",
    2: "Nâng cao",
    3: "Cao",
    4: "Tích cực",
    5: "Khủng hoảng",
}

# Số ngày đến lần PHQ kế tiếp theo mức theo dõi
_PHQ2_DAYS = {1: 90, 2: 30, 3: 14, 4: 7,  5: 3}
_PHQ9_DAYS = {1: 0,  2: 90, 3: 14, 4: 14, 5: 7}


def _now() -> datetime:
    return datetime.now(timezone.utc)


def _ref(uid: str):
    return get_db().collection("users").document(uid).collection("monitoring").document("current")


def update_monitoring(uid: str, body: MonitoringUpdate) -> dict:
    if body.level not in range(1, 6):
        raise HTTPException(status_code=422, detail="level phải từ 1 đến 5")

    now  = _now()
    ref  = _ref(uid)
    prev = ref.get()

    next_phq2 = (now + timedelta(days=_PHQ2_DAYS[body.level])).isoformat() \
                if _PHQ2_DAYS[body.level] > 0 else None
    next_phq9 = (now + timedelta(days=_PHQ9_DAYS[body.level])).isoformat() \
                if _PHQ9_DAYS[body.level] > 0 else None

    doc = {
        "level":           body.level,
        "level_name":      _LEVEL_NAMES[body.level],
        "reason":          body.reason,
        "phq9_total":      body.phq9_total,
        "q9_value":        body.q9_value,
        "next_phq2_date":  next_phq2,
        "next_phq9_date":  next_phq9,
        "rejection_count": prev.to_dict().get("rejection_count", 0) if prev.exists else 0,
        "paused_until":    None,
        "updated_at":      now.isoformat(),
    }
    ref.set(doc)

    return {
        "level":          body.level,
        "level_name":     _LEVEL_NAMES[body.level],
        "next_phq2_days": _PHQ2_DAYS[body.level],
        "next_phq9_days": _PHQ9_DAYS[body.level],
        "updated_at":     now.isoformat(),
    }


def get_monitoring_status(uid: str) -> dict:
    doc = _ref(uid).get()
    if not doc.exists:
        # Default: level 1 cho user mới
        now = _now()
        return {
            "level":               1,
            "level_name":          _LEVEL_NAMES[1],
            "next_phq2_date":      (now + timedelta(days=90)).isoformat(),
            "next_phq9_date":      None,
            "rejection_count":     0,
            "paused_until":        None,
            "last_interaction_at": None,
            "silence_hours":       None,
        }

    data = doc.to_dict()

    # Lấy interaction gần nhất từ subcollection
    latest_docs = (
        get_db().collection("users").document(uid)
        .collection("interactions")
        .order_by("timestamp", direction="DESCENDING")
        .limit(1)
        .stream()
    )
    last_interaction = None
    silence_hours    = None
    for d in latest_docs:
        last_interaction = d.to_dict().get("timestamp")
    if last_interaction:
        delta         = _now() - datetime.fromisoformat(last_interaction)
        silence_hours = round(delta.total_seconds() / 3600, 1)

    return {
        "level":               data.get("level"),
        "level_name":          data.get("level_name"),
        "next_phq2_date":      data.get("next_phq2_date"),
        "next_phq9_date":      data.get("next_phq9_date"),
        "rejection_count":     data.get("rejection_count", 0),
        "paused_until":        data.get("paused_until"),
        "last_interaction_at": last_interaction,
        "silence_hours":       silence_hours,
    }
