from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel

from routers.auth import get_current_user
from services.firebase import get_db

router = APIRouter(prefix="/notifications", tags=["notifications"])


class FcmTokenUpdate(BaseModel):
    fcm_token: str


@router.put("/fcm-token")
def update_fcm_token(
    body: FcmTokenUpdate,
    user: dict = Depends(get_current_user),
):
    """Mobile app gọi endpoint này để lưu / cập nhật FCM token."""
    uid = user["uid"]
    db = get_db()
    db.collection("users").document(uid).set({"fcm_token": body.fcm_token}, merge=True)
    return {"message": "FCM token updated"}


@router.delete("/fcm-token")
def remove_fcm_token(user: dict = Depends(get_current_user)):
    """Xóa FCM token khi user đăng xuất."""
    uid = user["uid"]
    db = get_db()
    db.collection("users").document(uid).set({"fcm_token": None}, merge=True)
    return {"message": "FCM token removed"}
