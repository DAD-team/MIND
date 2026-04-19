from fastapi import APIRouter, Depends
from routers.auth import get_current_user
from models.usage import UsageSession
from controllers.usage_controller import log_session

router = APIRouter(prefix="/usage", tags=["usage"])


@router.post("/session", status_code=201)
def post_session(body: UsageSession, user: dict = Depends(get_current_user)):
    return log_session(user["uid"], body)
