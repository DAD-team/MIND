from pydantic import BaseModel
from typing import Optional


class MoodCheckin(BaseModel):
    mood_level: int           # 1=Vui 2=Buồn 3=Stress 4=Hào hứng 5=Bình thường 6=Mệt
    mood_score: int           # 1-5
    has_video:  bool
    video_id:   Optional[str] = None
