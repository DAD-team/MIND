import uuid
from datetime import datetime, timezone, date, timedelta
from fastapi import HTTPException
from models.schedule import ScheduleCreate, ScheduleUpdate
from services.firebase import get_db

_EVENT_WEIGHTS = {0: 0, 1: 3, 2: 2, 3: 1, 4: 1}
_EVENT_NAMES   = {0: "Học thường", 1: "Thi", 2: "Deadline đồ án", 3: "Nộp bài tập", 4: "Thuyết trình"}


def _now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _ref(uid: str, schedule_id: str):
    return get_db().collection("users").document(uid).collection("schedules").document(schedule_id)


def _validate(day: int = None, start_time: str = None, end_time: str = None):
    if day is not None and day not in range(7):
        raise HTTPException(status_code=422, detail="day_of_week phải từ 0 đến 6")

    for val, field in [(start_time, "start_time"), (end_time, "end_time")]:
        if val is not None:
            try:
                h, m = val.split(":")
                assert 0 <= int(h) <= 23 and 0 <= int(m) <= 59
            except:
                raise HTTPException(status_code=422, detail=f"{field} phải có format HH:mm")


def list_schedules(uid: str) -> dict:
    docs = (
        get_db().collection("users").document(uid).collection("schedules")
        .stream()
    )
    schedules = sorted(
        [d.to_dict() for d in docs],
        key=lambda s: (s.get("day_of_week", 0), s.get("start_time", "")),
    )
    return {"schedules": schedules}


def create_schedule(uid: str, body: ScheduleCreate) -> dict:
    _validate(body.day_of_week, body.start_time, body.end_time)

    schedule_id = str(uuid.uuid4())
    now         = _now()
    doc = {
        "id":          schedule_id,
        "user_id":     uid,
        "subject":     body.subject,
        "day_of_week": body.day_of_week,
        "start_time":  body.start_time,
        "end_time":    body.end_time,
        "room":        body.room,
        "event_type":  body.event_type,
        "created_at":  now,
        "updated_at":  now,
    }
    _ref(uid, schedule_id).set(doc)
    return doc


def update_schedule(uid: str, schedule_id: str, body: ScheduleUpdate) -> dict:
    ref = _ref(uid, schedule_id)
    if not ref.get().exists:
        raise HTTPException(status_code=404, detail="Schedule not found")

    updates = body.model_dump(exclude_none=True)
    if not updates:
        raise HTTPException(status_code=422, detail="Không có field nào để cập nhật")

    _validate(
        updates.get("day_of_week"),
        updates.get("start_time"),
        updates.get("end_time"),
    )

    updates["updated_at"] = _now()
    ref.update(updates)
    return ref.get().to_dict()


def delete_schedule(uid: str, schedule_id: str):
    ref = _ref(uid, schedule_id)
    if not ref.get().exists:
        raise HTTPException(status_code=404, detail="Schedule not found")
    ref.delete()


def upcoming_schedules(uid: str, days: int) -> dict:
    today         = date.today()
    target_days   = {(today + timedelta(days=i)).weekday() for i in range(days)}

    all_docs = (
        get_db().collection("users").document(uid).collection("schedules")
        .stream()
    )

    events = []
    for d in all_docs:
        s = d.to_dict()
        if s.get("day_of_week") not in target_days:
            continue
        etype  = s.get("event_type", 0)
        weight = _EVENT_WEIGHTS.get(etype, 0)
        events.append({
            "subject":         s["subject"],
            "event_type":      etype,
            "event_type_name": _EVENT_NAMES.get(etype, ""),
            "event_weight":    weight,
            "day_of_week":     s["day_of_week"],
            "start_time":      s["start_time"],
        })

    total_weight = sum(e["event_weight"] for e in events)
    return {"events": events, "total_weight": total_weight, "event_count": len(events)}