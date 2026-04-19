import uuid
from datetime import datetime, timezone
from fastapi import HTTPException
from models.journal import JournalCreate, JournalUpdate
from services.firebase import get_db

_VALID_MOODS = set(range(1, 7))


def _now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _ref(uid: str, journal_id: str):
    return get_db().collection("users").document(uid) \
                   .collection("journals").document(journal_id)


def create_journal(uid: str, body: JournalCreate) -> dict:
    if not body.title.strip():
        raise HTTPException(status_code=422, detail="title không được rỗng")
    if not body.content.strip():
        raise HTTPException(status_code=422, detail="content không được rỗng")
    if body.mood_level is not None and body.mood_level not in _VALID_MOODS:
        raise HTTPException(status_code=422, detail="mood_level phải từ 1 đến 6")

    journal_id = str(uuid.uuid4())
    now        = _now()

    doc = {
        "id":         journal_id,
        "user_id":    uid,
        "title":      body.title.strip(),
        "content":    body.content.strip(),
        "mood_level": body.mood_level,
        "tags":       body.tags or [],
        "created_at": now,
        "updated_at": now,
    }
    _ref(uid, journal_id).set(doc)

    # Lưu interaction vào subcollection
    get_db().collection("users").document(uid) \
            .collection("interactions").add({
                "type": "journal",
                "timestamp": now,
                "ref_id": journal_id,
            })

    return doc


def list_journals(uid: str, limit: int) -> dict:
    docs = (
        get_db().collection("users").document(uid).collection("journals")
        .order_by("created_at", direction="DESCENDING")
        .limit(limit)
        .stream()
    )
    records = []
    for d in docs:
        data = d.to_dict()
        data.pop("user_id", None)
        records.append(data)
    return {"records": records, "count": len(records)}


def get_journal(uid: str, journal_id: str) -> dict:
    doc = _ref(uid, journal_id).get()
    if not doc.exists:
        raise HTTPException(status_code=404, detail="Journal not found")
    data = doc.to_dict()
    data.pop("user_id", None)
    return data


def update_journal(uid: str, journal_id: str, body: JournalUpdate) -> dict:
    ref = _ref(uid, journal_id)
    if not ref.get().exists:
        raise HTTPException(status_code=404, detail="Journal not found")

    updates = body.model_dump(exclude_none=True)
    if not updates:
        raise HTTPException(status_code=422, detail="Không có field nào để cập nhật")

    if "mood_level" in updates and updates["mood_level"] not in _VALID_MOODS:
        raise HTTPException(status_code=422, detail="mood_level phải từ 1 đến 6")
    if "title" in updates and not updates["title"].strip():
        raise HTTPException(status_code=422, detail="title không được rỗng")
    if "content" in updates and not updates["content"].strip():
        raise HTTPException(status_code=422, detail="content không được rỗng")

    updates["updated_at"] = _now()
    ref.update(updates)
    data = ref.get().to_dict()
    data.pop("user_id", None)
    return data


def delete_journal(uid: str, journal_id: str):
    ref = _ref(uid, journal_id)
    if not ref.get().exists:
        raise HTTPException(status_code=404, detail="Journal not found")
    ref.delete()
