from fastapi import APIRouter, UploadFile, File, Depends, HTTPException
from routers.auth import get_current_user
from controllers.auth_controller import get_consent_level
from controllers.video_controller import process_video, get_analysis

router = APIRouter(prefix="/videos", tags=["videos"])


@router.post("/upload")
async def upload_video(
    file: UploadFile = File(...),
    payload: dict = Depends(get_current_user),
):
    uid = payload["uid"]
    if get_consent_level(uid) < 2:
        raise HTTPException(status_code=403, detail="Cần mức đồng thuận ≥ 2 để quay video")
    return await process_video(file, uid)


@router.get("/analysis/{video_id}")
def video_analysis(video_id: str, payload: dict = Depends(get_current_user)):
    return get_analysis(payload["uid"], video_id)