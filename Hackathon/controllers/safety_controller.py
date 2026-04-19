import uuid
import logging
from datetime import datetime, timezone, timedelta
from fastapi import HTTPException
from models.safety import SafetyEventCreate
from services.firebase import get_db

logger = logging.getLogger(__name__)

_LEVEL_MAP = {
    1: {"action": "passive_ideation",   "deadline_hours": 24,  "counselor_notified": True},
    2: {"action": "active_ideation",    "deadline_hours": 12,  "counselor_notified": True},
    3: {"action": "crisis_same_day",    "deadline_hours": 0,   "counselor_notified": True},
}


def _now() -> datetime:
    return datetime.now(timezone.utc)


def create_safety_event(uid: str, body: SafetyEventCreate) -> dict:
    if body.q9_value not in (1, 2, 3):
        raise HTTPException(status_code=422, detail="q9_value phải là 1, 2 hoặc 3")
    return _create(uid, body)


def create_safety_event_internal(uid: str, body: SafetyEventCreate) -> str:
    """Gọi nội bộ từ phq_controller — không raise HTTP exception."""
    try:
        doc = _create(uid, body)
        return doc["id"]
    except Exception as e:
        logger.error(f"[Safety] Failed to auto-create safety event for {uid}: {e}")
        return ""


def _create(uid: str, body: SafetyEventCreate) -> dict:
    info    = _LEVEL_MAP[body.q9_value]
    now     = _now()
    event_id = str(uuid.uuid4())

    deadline = None
    if info["deadline_hours"] > 0:
        deadline = (now + timedelta(hours=info["deadline_hours"])).isoformat()
    else:
        deadline = now.isoformat()  # TỨC THÌ

    doc = {
        "id":                        event_id,
        "user_id":                   uid,
        "q9_value":                  body.q9_value,
        "phq9_total":                body.phq9_total,
        "phq9_id":                   body.phq9_id,
        "action_taken":              info["action"],
        "counselor_notified":        info["counselor_notified"],
        "counselor_notify_deadline": deadline,
        "created_at":                now.isoformat(),
        # Safety events KHÔNG BAO GIỜ tự xóa
        "permanent":                 True,
    }

    # Lưu vào top-level collection để tư vấn viên có thể truy vấn
    get_db().collection("safety_events").document(event_id).set(doc)
    # Đồng thời lưu dưới user để dễ query theo uid
    get_db().collection("users").document(uid).collection("safety_events").document(event_id).set(doc)

    logger.warning(f"[Safety] Event created uid={uid} q9={body.q9_value} action={info['action']}")
    return doc
