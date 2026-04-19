from datetime import datetime, timezone
from fastapi import HTTPException
from models.user import ConsentUpdate
from services.firebase import get_db


def get_or_create_user(payload: dict) -> dict:
    uid = payload["uid"]
    ref = get_db().collection("users").document(uid)
    snap = ref.get()

    if not snap.exists:
        ref.set({
            "user_id":       uid,
            "email":         payload.get("email"),
            "name":          payload.get("name"),
            "picture":       payload.get("picture"),
            "consent_level": 1,
            "created_at":    datetime.now(timezone.utc).isoformat(),
        })
        consent_level = 1
    else:
        consent_level = snap.to_dict().get("consent_level", 1)

    return {
        "user_id":       uid,
        "email":         payload.get("email"),
        "name":          payload.get("name"),
        "picture":       payload.get("picture"),
        "consent_level": consent_level,
    }


def update_consent(uid: str, body: ConsentUpdate) -> dict:
    if body.consent_level not in (1, 2, 3):
        raise HTTPException(status_code=422, detail="consent_level phải là 1, 2, hoặc 3")

    ref = get_db().collection("users").document(uid)
    ref.set({"consent_level": body.consent_level}, merge=True)
    return {"consent_level": body.consent_level}


def get_consent_level(uid: str) -> int:
    snap = get_db().collection("users").document(uid).get()
    if not snap.exists:
        return 1
    return snap.to_dict().get("consent_level", 1)