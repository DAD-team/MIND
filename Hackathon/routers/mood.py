from fastapi import APIRouter, Depends
from routers.auth import get_current_user
from models.mood import MoodCheckin
from controllers.mood_controller import submit_checkin

router = APIRouter(prefix="/mood", tags=["mood"])


@router.post("/checkin", status_code=201)
def mood_checkin(body: MoodCheckin, user: dict = Depends(get_current_user)):
    return submit_checkin(user["uid"], body)
