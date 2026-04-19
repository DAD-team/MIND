from pydantic import BaseModel
from typing import Optional


class ScheduleCreate(BaseModel):
    subject:     str
    day_of_week: int
    start_time:  str
    end_time:    str
    room:        Optional[str] = None
    event_type:  int = 0  # 0=Học thường 1=Thi 2=Deadline 3=Nộp bài 4=Thuyết trình


class ScheduleUpdate(BaseModel):
    subject:     Optional[str] = None
    day_of_week: Optional[int] = None
    start_time:  Optional[str] = None
    end_time:    Optional[str] = None
    room:        Optional[str] = None
    event_type:  Optional[int] = None