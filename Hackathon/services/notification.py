import csv
import random
import logging
from datetime import datetime, timezone, timedelta
from pathlib import Path

from firebase_admin import messaging
from services.firebase import get_db

logger = logging.getLogger(__name__)

_NOTI_CSV = Path(__file__).parent.parent / "noti.csv"

# Giới hạn gửi notification
_MAX_PER_DAY  = 2
_MAX_PER_WEEK = 5


def _load_notifications() -> list[dict]:
    rows = []
    with open(_NOTI_CSV, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            rows.append({"title": row["Title"], "body": row["Body"]})
    return rows


_NOTIFICATIONS = _load_notifications()


def get_random_notification() -> dict:
    return random.choice(_NOTIFICATIONS)


def send_fcm(fcm_token: str, title: str, body: str) -> bool:
    try:
        message = messaging.Message(
            notification=messaging.Notification(title=title, body=body),
            token=fcm_token,
        )
        response = messaging.send(message)
        logger.info(f"[FCM] Sent to token ...{fcm_token[-6:]}: {response}")
        return True
    except messaging.UnregisteredError:
        logger.warning(f"[FCM] Token unregistered: ...{fcm_token[-6:]}")
        return False
    except Exception as e:
        logger.error(f"[FCM] Error sending to ...{fcm_token[-6:]}: {e}")
        return False


# ---------------------------------------------------------------------------
#  Kiểm tra giới hạn gửi
# ---------------------------------------------------------------------------
def _check_rate_limit(uid: str) -> bool:
    db = get_db()
    now = datetime.now(timezone.utc)
    today_start = now.replace(hour=0, minute=0, second=0, microsecond=0).isoformat()
    week_start = (now - timedelta(days=7)).isoformat()

    col = db.collection("users").document(uid).collection("notification_log")

    today_count = len(list(col.where("timestamp", ">=", today_start).stream()))
    if today_count >= _MAX_PER_DAY:
        return False

    week_count = len(list(col.where("timestamp", ">=", week_start).stream()))
    if week_count >= _MAX_PER_WEEK:
        return False

    return True


def _is_quiet_hours() -> bool:
    now = datetime.now(timezone.utc)
    hour_vn = (now.hour + 7) % 24
    return hour_vn >= 22 or hour_vn < 7


def _log_notification(uid: str, noti_type: str):
    db = get_db()
    db.collection("users").document(uid).collection("notification_log").add({
        "type":      noti_type,
        "timestamp": datetime.now(timezone.utc).isoformat(),
    })


# ---------------------------------------------------------------------------
#  Gửi notification có kiểm soát (cho Scout / PHQ scheduler)
# ---------------------------------------------------------------------------
def send_scout_notification(uid: str, fcm_token: str,
                            title: str, body: str,
                            noti_type: str = "scout_reminder") -> bool:
    # Safety alert luôn gửi, bỏ qua giới hạn
    if noti_type == "safety_alert":
        success = send_fcm(fcm_token, title, body)
        if success:
            _log_notification(uid, noti_type)
        return success

    # Không gửi 22:00–7:00
    if _is_quiet_hours():
        logger.info(f"[Notification] Quiet hours, skipping for {uid[:8]}...")
        return False

    # Kiểm tra giới hạn 2/ngày, 5/tuần
    if not _check_rate_limit(uid):
        logger.info(f"[Notification] Rate limit reached for {uid[:8]}...")
        return False

    success = send_fcm(fcm_token, title, body)
    if success:
        _log_notification(uid, noti_type)
    else:
        # Xóa token không hợp lệ
        get_db().collection("users").document(uid).update({"fcm_token": None})
    return success


# ---------------------------------------------------------------------------
#  Thông báo tư vấn viên
# ---------------------------------------------------------------------------
def send_counselor_alert(uid: str, data: dict):
    db = get_db()
    db.collection("counselor_alerts").add({
        "user_id":   uid,
        "data":      data,
        "read":      False,
        "created_at": datetime.now(timezone.utc).isoformat(),
    })
    logger.info(f"[Counselor Alert] Created for user {uid[:8]}...")
