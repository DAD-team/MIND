from fastapi import APIRouter, Depends
from routers.auth import get_current_user
from models.chat import ChatInteraction
from controllers.chat_controller import save_interaction

router = APIRouter(prefix="/chat", tags=["chat"])


@router.post("/interaction", status_code=201)
def post_interaction(body: ChatInteraction, user: dict = Depends(get_current_user)):
    return save_interaction(user["uid"], body)
