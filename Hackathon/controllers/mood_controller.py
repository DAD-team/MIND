import uuid
from datetime import datetime, timezone
from fastapi import HTTPException
from models.mood import MoodCheckin
from services.firebase import get_db

_VALID_MOOD_LEVELS = {1, 2, 3, 4, 5, 6}
_VALID_MOOD_SCORES = {1, 2, 3, 4, 5}


def submit_checkin(uid: str, body: MoodCheckin) -> dict:
    if body.mood_level not in _VALID_MOOD_LEVELS:
        raise HTTPException(status_code=422, detail="mood_level phải từ 1 đến 6")
    if body.mood_score not in _VALID_MOOD_SCORES:
        raise HTTPException(status_code=422, detail="mood_score phải từ 1 đến 5")
    if body.has_video and not body.video_id:
        raise HTTPException(status_code=422, detail="has_video=true nhưng thiếu video_id")

    now       = datetime.now(timezone.utc)
    checkin_id = str(uuid.uuid4())

    doc = {
        "id":         checkin_id,
        "user_id":    uid,
        "mood_level": body.mood_level,
        "mood_score": body.mood_score,
        "has_video":  body.has_video,
        "video_id":   body.video_id,
        "created_at": now.isoformat(),
    }

    db = get_db()
    db.collection("users").document(uid).collection("emotion_log").document(checkin_id).set(doc)

    # Lưu interaction vào subcollection
    db.collection("users").document(uid) \
      .collection("interactions").add({
          "type": "mood",
          "timestamp": now.isoformat(),
          "ref_id": checkin_id,
      })

    return {"id": checkin_id, "created_at": doc["created_at"]}
