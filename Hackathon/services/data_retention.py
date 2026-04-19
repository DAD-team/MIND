"""
Auto-delete dữ liệu theo quy tắc lưu trữ MIND.

- Nhật ký cảm xúc (emotion_log): 90 ngày
- Nhật ký Scout (scout_logs): 90 ngày
- Nhật ký can thiệp (journals): 6 tháng
- Kết quả PHQ-2/PHQ-9 (phq_results): 1 năm
- Notification log: 90 ngày
- Sự kiện an toàn (safety_events): KHÔNG xóa tự động
"""

import logging
from datetime import datetime, timezone, timedelta

from services.firebase import get_db

logger = logging.getLogger(__name__)


def _delete_old_docs(collection_ref, date_field: str, cutoff_iso: str) -> int:
    deleted = 0
    docs = collection_ref.where(date_field, "<", cutoff_iso).stream()
    for doc in docs:
        doc.reference.delete()
        deleted += 1
    return deleted


def run_data_retention():
    db  = get_db()
    now = datetime.now(timezone.utc)

    cutoff_90d  = (now - timedelta(days=90)).isoformat()
    cutoff_180d = (now - timedelta(days=180)).isoformat()
    cutoff_365d = (now - timedelta(days=365)).isoformat()

    users = db.collection("users").stream()
    total = {"emotion_log": 0, "scout_logs": 0, "journals": 0,
             "phq_results": 0, "notification_log": 0}

    for user_doc in users:
        uid = user_doc.id
        user_ref = db.collection("users").document(uid)

        # Emotion log — 90 ngày
        total["emotion_log"] += _delete_old_docs(
            user_ref.collection("emotion_log"), "created_at", cutoff_90d)

        # Scout logs — 90 ngày
        total["scout_logs"] += _delete_old_docs(
            user_ref.collection("scout_logs"), "timestamp", cutoff_90d)

        # Notification log — 90 ngày
        total["notification_log"] += _delete_old_docs(
            user_ref.collection("notification_log"), "timestamp", cutoff_90d)

        # Journals — 6 tháng
        total["journals"] += _delete_old_docs(
            user_ref.collection("journals"), "created_at", cutoff_180d)

        # PHQ results — 1 năm
        total["phq_results"] += _delete_old_docs(
            user_ref.collection("phq_results"), "created_at", cutoff_365d)

    logger.info(f"[Data Retention] Cleanup complete: {total}")
