from pydantic import BaseModel


class ConsentUpdate(BaseModel):
    consent_level: int          # 1, 2, hoặc 3
