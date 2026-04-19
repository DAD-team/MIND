from pydantic import BaseModel
from typing import Optional, List


class PhqSubmit(BaseModel):
    phq_type:             str        # "phq2" | "phq9"
    scores:               List[int]
    total:                int
    source:               str        # "self_request" | "scheduled" | "scout"
    somatic_score:        Optional[int]   = None  # PHQ-9 only
    cognitive_score:      Optional[int]   = None  # PHQ-9 only
    functional_impact:    Optional[int]   = None  # PHQ-9 only
    q9_value:             Optional[int]   = None  # PHQ-9 only; >= 1 triggers safety event
    triggered_by_phq2_id: Optional[str]  = None
    behavior_risk_score:  Optional[float] = None


class PhqReject(BaseModel):
    phq_type: str  # "phq2" | "phq9"


class PhqDefer(BaseModel):
    phq2_id: Optional[str] = None  # ID của PHQ-2 đã kích hoạt chuyển tiếp


class PhqTrigger(BaseModel):
    uid:      str                  # UID của user cần hiển thị PHQ
    phq_type: str                  # "phq2" | "phq9"
    reason:   str = "scheduled"    # "scheduled" | "scout" | "escalate_phq9" | "manual"
