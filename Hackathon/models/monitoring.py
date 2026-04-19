from pydantic import BaseModel


class MonitoringUpdate(BaseModel):
    level:      int   # 1-5
    reason:     str
    phq9_total: int
    q9_value:   int
