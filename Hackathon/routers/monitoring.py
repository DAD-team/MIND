from fastapi import APIRouter, Depends
from routers.auth import get_current_user
from models.monitoring import MonitoringUpdate
from controllers.monitoring_controller import update_monitoring, get_monitoring_status

router = APIRouter(prefix="/monitoring", tags=["monitoring"])


@router.put("/update")
def monitoring_update(body: MonitoringUpdate, user: dict = Depends(get_current_user)):
    return update_monitoring(user["uid"], body)


@router.get("/status")
def monitoring_status(user: dict = Depends(get_current_user)):
    return get_monitoring_status(user["uid"])
