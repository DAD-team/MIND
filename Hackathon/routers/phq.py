from fastapi import APIRouter, Depends, Query
from routers.auth import get_current_user
from models.phq import PhqSubmit, PhqReject, PhqDefer, PhqTrigger
from controllers.phq_controller import (
    submit_phq, get_phq_history, reject_phq, defer_phq9,
    trigger_phq, get_pending_phq,
)

router = APIRouter(prefix="/phq", tags=["phq"])


@router.post("/submit", status_code=201)
def phq_submit(body: PhqSubmit, user: dict = Depends(get_current_user)):
    return submit_phq(user["uid"], body)


@router.get("/pending")
def phq_pending(user: dict = Depends(get_current_user)):
    """Frontend gọi để kiểm tra user có PHQ đang chờ hiển thị không."""
    return get_pending_phq(user["uid"])


@router.post("/trigger", status_code=201)
def phq_trigger(body: PhqTrigger):
    """Kích hoạt hiển thị bộ câu hỏi PHQ cho 1 user cụ thể.
    Gọi bởi Scout, scheduler, hoặc admin — KHÔNG cần auth của user."""
    return trigger_phq(body.uid, body.phq_type, body.reason)


@router.get("/history")
def phq_history(
    type:  str = Query(default="all", description="phq2 | phq9 | all"),
    limit: int = Query(default=20, ge=1, le=100),
    user: dict = Depends(get_current_user),
):
    return get_phq_history(user["uid"], type, limit)


@router.post("/reject")
def phq_reject(body: PhqReject, user: dict = Depends(get_current_user)):
    return reject_phq(user["uid"], body.phq_type)


@router.post("/defer-phq9")
def phq_defer(body: PhqDefer, user: dict = Depends(get_current_user)):
    return defer_phq9(user["uid"])
