from fastapi import APIRouter, Depends, Query
from routers.auth import get_current_user
from controllers.history_controller import get_history, get_single

router = APIRouter(prefix="/history", tags=["history"])


@router.get("/me")
def list_history(
    limit: int = Query(default=20, ge=1, le=100),
    payload: dict = Depends(get_current_user),
):
    return get_history(payload["uid"], limit)


@router.get("/me/{video_id}")
def single_result(
    video_id: str,
    payload: dict = Depends(get_current_user),
):
    return get_single(payload["uid"], video_id)