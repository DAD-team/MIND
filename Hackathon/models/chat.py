from pydantic import BaseModel
from typing import Optional, List


class ChatMessage(BaseModel):
    role:    str   # "user" | "bot"
    content: str


class ChatInteraction(BaseModel):
    messages:         List[ChatMessage]
    conversation_id:  Optional[str] = None   # client tự sinh UUID để nhóm tin nhắn
    mood_before:      Optional[int] = None   # mood_level trước khi chat (1–6)
    mood_after:       Optional[int] = None   # mood_level sau khi chat (1–6)
    duration_seconds: Optional[int] = None   # tổng thời gian hội thoại
