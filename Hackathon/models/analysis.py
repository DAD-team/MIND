from pydantic import BaseModel
from typing import Optional


class AnalysisRecord(BaseModel):
    video_id:    str
    user_id:     str
    result:      dict
    risk:        dict
    confidence:  float
    frames:      int
    analyzed_at: str