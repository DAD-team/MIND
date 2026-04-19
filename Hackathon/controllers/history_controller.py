from fastapi import HTTPException
from services.firebase import get_db


def get_history(uid: str, limit: int) -> dict:
    docs = (
        get_db().collection("users").document(uid).collection("analyses")
        .order_by("analyzed_at", direction="DESCENDING")
        .limit(limit)
        .stream()
    )
    return {"user_id": uid, "records": [d.to_dict() for d in docs]}


def get_single(uid: str, video_id: str) -> dict:
    doc = (
        get_db().collection("users").document(uid)
        .collection("analyses").document(video_id).get()
    )
    if not doc.exists:
        raise HTTPException(status_code=404, detail="Result not found")
    return doc.to_dict()