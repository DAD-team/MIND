from pydantic import BaseModel
from typing import Optional, List


class JournalCreate(BaseModel):
    title:      str
    content:    str
    mood_level: Optional[int]        = None  # 1–6
    tags:       Optional[List[str]]  = None


class JournalUpdate(BaseModel):
    title:      Optional[str]        = None
    content:    Optional[str]        = None
    mood_level: Optional[int]        = None
    tags:       Optional[List[str]]  = None
