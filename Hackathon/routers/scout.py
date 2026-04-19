"""
Router test cho Wellbeing Scout — kích hoạt thủ công để kiểm tra logic.
"""

from fastapi import APIRouter, Query
from services.scout import (
    compute_silence_hours,
    compute_phq2_trend,
    compute_academic_pressure,
    compute_interaction_ratio,
    compute_flat_affect_avg,
    compute_duchenne_avg,
    compute_risk_score,
    decide_action,
    _execute_action,
    _save_scout_log,
    _score_silence,
    _score_phq2_trend,
    _score_academic,
    _score_interaction_ratio,
    _score_flat_affect,
    _score_duchenne,
)
from services.firebase import get_db

_ACTION_DETAIL = {
    "safety_protocol": {
        "label":       "Kích hoạt giao thức khủng hoảng",
        "description": "Tạo Safety Event + thông báo tư vấn viên + gửi FCM cho user (bypass rate limit)",
    },
    "schedule_phq2": {
        "label":       "Lên lịch gửi PHQ-2 sớm",
        "description": "Tạo pending PHQ-2 để frontend hiển thị + gửi FCM nhắc user",
    },
    "gentle_reminder": {
        "label":       "Gửi nhắc nhở nhẹ nhàng",
        "description": "Gửi FCM nhắc user quay lại app (chỉ khi consent >= 3)",
    },
    "mark_priority": {
        "label":       "Đánh dấu ưu tiên",
        "description": "Ghi flag scout_priority vào monitoring — khi đến hạn PHQ-2 sẽ được gửi sớm",
    },
    "log_only": {
        "label":       "Chỉ ghi nhật ký",
        "description": "Không gửi notification, không ghi thêm gì — user bình thường",
    },
}

router = APIRouter(prefix="/scout", tags=["scout"])


@router.post("/run")
def run_scout_now(uid: str | None = Query(None, description="UID cụ thể. Bỏ trống = chạy tất cả users")):
    """
    Kích hoạt Wellbeing Scout ngay lập tức (bỏ qua giới hạn giờ 7:00–23:00).

    - Truyền `uid` để test 1 user cụ thể.
    - Bỏ trống `uid` để chạy toàn bộ users (giống chu kỳ 2h).
    """
    db = get_db()

    if uid:
        user_doc = db.collection("users").document(uid).get()
        if not user_doc.exists:
            return {"detail": f"User {uid} không tồn tại"}
        users = [(uid, user_doc.to_dict())]
    else:
        users = [(doc.id, doc.to_dict()) for doc in db.collection("users").stream()]

    results = []
    for user_uid, user_data in users:
        consent = user_data.get("consent_level", 1)
        fcm_token = user_data.get("fcm_token")

        signals = {
            "silence_hours":     compute_silence_hours(user_uid),
            "phq2_trend":        compute_phq2_trend(user_uid),
            "academic_pressure": compute_academic_pressure(user_uid),
            "interaction_ratio": compute_interaction_ratio(user_uid),
        }

        if consent >= 2:
            signals["flat_affect_avg"] = compute_flat_affect_avg(user_uid)
            signals["duchenne_avg"]    = compute_duchenne_avg(user_uid)
        else:
            signals["flat_affect_avg"] = None
            signals["duchenne_avg"]    = None

        risk_score = compute_risk_score(signals)
        action = decide_action(user_uid, risk_score, signals)
        notification_sent = _execute_action(user_uid, action, fcm_token)

        _save_scout_log(user_uid, signals, risk_score, action, notification_sent)

        score_breakdown = {
            "silence":     _score_silence(signals["silence_hours"]),
            "phq2_trend":  _score_phq2_trend(signals["phq2_trend"]),
            "academic":    _score_academic(signals["academic_pressure"]),
            "interaction": _score_interaction_ratio(signals["interaction_ratio"]),
            "flat_affect": _score_flat_affect(signals.get("flat_affect_avg")),
            "duchenne":    _score_duchenne(signals.get("duchenne_avg")),
        }

        results.append({
            "uid":               user_uid,
            "consent_level":     consent,
            "signals":           signals,
            "score_breakdown":   score_breakdown,
            "risk_score":        risk_score,
            "action":            action,
            "action_detail":     _ACTION_DETAIL.get(action),
            "notification_sent": notification_sent,
        })

    return {
        "triggered":   True,
        "users_count": len(results),
        "results":     results,
    }
