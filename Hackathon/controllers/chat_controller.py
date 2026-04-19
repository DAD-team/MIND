import uuid
from datetime import datetime, timezone
from fastapi import HTTPException
from models.chat import ChatInteraction
from services.firebase import get_db

_VALID_ROLES   = {"user", "bot"}
_VALID_MOODS   = set(range(1, 7))


def save_interaction(uid: str, body: ChatInteraction) -> dict:
    if not body.messages:
        raise HTTPException(status_code=422, detail="messages không được rỗng")

    for msg in body.messages:
        if msg.role not in _VALID_ROLES:
            raise HTTPException(status_code=422, detail=f"role phải là 'user' hoặc 'bot', nhận: {msg.role}")
        if not msg.content.strip():
            raise HTTPException(status_code=422, detail="content không được rỗng")

    if body.mood_before is not None and body.mood_before not in _VALID_MOODS:
        raise HTTPException(status_code=422, detail="mood_before phải từ 1 đến 6")
    if body.mood_after is not None and body.mood_after not in _VALID_MOODS:
        raise HTTPException(status_code=422, detail="mood_after phải từ 1 đến 6")

    interaction_id  = str(uuid.uuid4())
    conversation_id = body.conversation_id or interaction_id
    now             = datetime.now(timezone.utc).isoformat()

    doc = {
        "id":               interaction_id,
        "user_id":          uid,
        "conversation_id":  conversation_id,
        "messages":         [m.model_dump() for m in body.messages],
        "message_count":    len(body.messages),
        "mood_before":      body.mood_before,
        "mood_after":       body.mood_after,
        "duration_seconds": body.duration_seconds,
        "created_at":       now,
    }

    get_db().collection("users").document(uid) \
            .collection("chat_interactions").document(interaction_id).set(doc)

    # Lưu interaction vào subcollection
    get_db().collection("users").document(uid) \
            .collection("interactions").add({
                "type": "chat",
                "timestamp": now,
                "ref_id": interaction_id,
            })

    return {"id": interaction_id, "conversation_id": conversation_id, "created_at": now}
