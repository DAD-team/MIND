from fastapi import APIRouter, Depends
from routers.auth import get_current_user
from models.safety import SafetyEventCreate
from controllers.safety_controller import create_safety_event

router = APIRouter(prefix="/safety-event", tags=["safety"])


@router.post("", status_code=201)
def post_safety_event(body: SafetyEventCreate, user: dict = Depends(get_current_user)):
    return create_safety_event(user["uid"], body)
