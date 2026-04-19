from pydantic import BaseModel
from typing import Optional, List


class UsageSession(BaseModel):
    start_time:       str            # ISO 8601
    end_time:         str            # ISO 8601
    duration_seconds: int            # tổng giây dùng app
    screens:          Optional[List[str]] = None  # màn hình đã đi qua
