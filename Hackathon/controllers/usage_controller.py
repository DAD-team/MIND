import uuid
from datetime import datetime, timezone
from fastapi import HTTPException
from models.usage import UsageSession
from services.firebase import get_db


def log_session(uid: str, body: UsageSession) -> dict:
    if body.duration_seconds < 0:
        raise HTTPException(status_code=422, detail="duration_seconds không được âm")

    session_id = str(uuid.uuid4())
    now        = datetime.now(timezone.utc).isoformat()

    doc = {
        "id":               session_id,
        "user_id":          uid,
        "start_time":       body.start_time,
        "end_time":         body.end_time,
        "duration_seconds": body.duration_seconds,
        "screens":          body.screens or [],
        "created_at":       now,
    }

    get_db().collection("users").document(uid) \
            .collection("usage_sessions").document(session_id).set(doc)

    return {"id": session_id, "created_at": now}
