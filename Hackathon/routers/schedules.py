from fastapi import APIRouter, Depends, Query
from routers.auth import get_current_user
from models.schedule import ScheduleCreate, ScheduleUpdate
from controllers.schedule_controller import (
    list_schedules, create_schedule, update_schedule, delete_schedule, upcoming_schedules
)

router = APIRouter(prefix="/schedules", tags=["schedules"])


@router.get("")
def get_schedules(payload: dict = Depends(get_current_user)):
    return list_schedules(payload["uid"])


@router.post("", status_code=201)
def add_schedule(body: ScheduleCreate, payload: dict = Depends(get_current_user)):
    return create_schedule(payload["uid"], body)


@router.patch("/{schedule_id}")
def edit_schedule(
    schedule_id: str,
    body: ScheduleUpdate,
    payload: dict = Depends(get_current_user),
):
    return update_schedule(payload["uid"], schedule_id, body)


@router.delete("/{schedule_id}", status_code=204)
def remove_schedule(schedule_id: str, payload: dict = Depends(get_current_user)):
    delete_schedule(payload["uid"], schedule_id)


@router.get("/upcoming")
def get_upcoming(
    days: int = Query(default=3, ge=1, le=30),
    payload: dict = Depends(get_current_user),
):
    return upcoming_schedules(payload["uid"], days)