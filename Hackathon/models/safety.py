from pydantic import BaseModel
from typing import Optional


class SafetyEventCreate(BaseModel):
    q9_value:   int            # 1 | 2 | 3
    phq9_total: int
    phq9_id:    Optional[str] = None
