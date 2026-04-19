"""
Test các kịch bản PHQ-2 và PHQ-9 khi người dùng KHÔNG ĐẠT yêu cầu.

Sử dụng: .venv/bin/python test_phq_flow.py

Kịch bản test:
  1. PHQ-2 điểm >= 3        → escalate_phq9
  2. PHQ-2 điểm = 2 (biên)  → escalate_phq9 (nếu có đủ cờ hành vi)
  3. PHQ-9 moderate (10-14)  → monitoring level 3
  4. PHQ-9 severe (20+)      → monitoring level 5
  5. PHQ-9 q9 >= 1           → safety event tự động
  6. Reject PHQ 3 lần        → tạm dừng 30 ngày
  7. Defer PHQ-9 2 lần       → nâng mức theo dõi
"""

import json
import os
import sys

import firebase_admin
from firebase_admin import auth, credentials

import requests

# ---------------------------------------------------------------------------
#  Config
# ---------------------------------------------------------------------------
BASE_URL = os.getenv("BASE_URL", "http://localhost:8000")
CRED_PATH = os.getenv("FIREBASE_CREDENTIALS_PATH", "./hackathon_admin.json")
TEST_UID = "test_phq_user_001"  # UID cho user test

# ---------------------------------------------------------------------------
#  Firebase init & token
# ---------------------------------------------------------------------------
if not firebase_admin._apps:
    cred = credentials.Certificate(CRED_PATH)
    firebase_admin.initialize_app(cred)


def get_test_token() -> str:
    """Tạo custom token rồi đổi sang ID token qua Firebase REST API."""
    custom_token = auth.create_custom_token(TEST_UID)

    # Lấy API key từ Firebase project
    # Cần FIREBASE_API_KEY env var hoặc hardcode
    api_key = os.getenv("FIREBASE_API_KEY")
    if not api_key:
        print("ERROR: Cần đặt biến môi trường FIREBASE_API_KEY")
        print("  Lấy từ Firebase Console → Project Settings → Web API Key")
        print("  export FIREBASE_API_KEY=your_key_here")
        sys.exit(1)

    # Exchange custom token → ID token
    resp = requests.post(
        f"https://identitytoolkit.googleapis.com/v1/accounts:signInWithCustomToken?key={api_key}",
        json={"token": custom_token.decode() if isinstance(custom_token, bytes) else custom_token,
              "returnSecureToken": True},
    )
    if resp.status_code != 200:
        print(f"ERROR: Không đổi được token: {resp.text}")
        sys.exit(1)

    return resp.json()["idToken"]


# ---------------------------------------------------------------------------
#  Helper
# ---------------------------------------------------------------------------
def api(method: str, path: str, token: str, body: dict = None) -> dict:
    headers = {"Authorization": f"Bearer {token}"}
    url = f"{BASE_URL}{path}"
    if method == "POST":
        r = requests.post(url, json=body, headers=headers)
    elif method == "GET":
        r = requests.get(url, headers=headers)
    elif method == "PUT":
        r = requests.put(url, json=body, headers=headers)
    else:
        raise ValueError(f"Unknown method: {method}")

    print(f"  [{r.status_code}] {method} {path}")
    try:
        data = r.json()
    except Exception:
        data = {"raw": r.text}
    print(f"  Response: {json.dumps(data, indent=2, ensure_ascii=False)}")
    print()
    return data


def separator(title: str):
    print(f"\n{'='*60}")
    print(f"  {title}")
    print(f"{'='*60}\n")


# ---------------------------------------------------------------------------
#  Test cases
# ---------------------------------------------------------------------------
def main():
    print("Đang lấy Firebase token...")
    token = get_test_token()
    print(f"Token OK (first 20 chars): {token[:20]}...\n")

    # Khởi tạo user
    separator("0. Khởi tạo user test")
    api("GET", "/auth/me", token)

    # ----- Kịch bản 1: PHQ-2 >= 3 → escalate -----
    separator("1. PHQ-2 điểm = 4 (>= 3) → PHẢI escalate PHQ-9")
    result = api("POST", "/phq/submit", token, {
        "phq_type": "phq2",
        "scores": [2, 2],
        "total": 4,
        "source": "self_request",
    })
    assert result.get("decision") == "escalate_phq9", \
        f"Expected escalate_phq9, got {result.get('decision')}"
    print("  ✓ PHQ-2 >= 3 → đúng escalate_phq9")
    print("  → Frontend cần hiển thị bộ câu hỏi PHQ-9 ngay lập tức")

    # ----- Kịch bản 2: PHQ-2 = 2 (biên giới) -----
    separator("2. PHQ-2 điểm = 2 (biên giới) → tuỳ cờ hành vi")
    result = api("POST", "/phq/submit", token, {
        "phq_type": "phq2",
        "scores": [1, 1],
        "total": 2,
        "source": "scheduled",
    })
    decision = result.get("decision")
    print(f"  Decision: {decision}")
    if decision == "escalate_phq9":
        print("  → Có >= 2 cờ hành vi → Frontend hiển thị PHQ-9")
    elif decision == "shorten_interval":
        print("  → Chưa đủ cờ hành vi → PHQ-2 lại sau 7 ngày")
        print("  → Frontend KHÔNG cần hiển thị PHQ-9 ngay")

    # ----- Kịch bản 3: PHQ-9 moderate (total 12) -----
    separator("3. PHQ-9 moderate (total=12) → monitoring level 3")
    result = api("POST", "/phq/submit", token, {
        "phq_type": "phq9",
        "scores": [2, 1, 2, 1, 2, 1, 1, 1, 1],
        "total": 12,
        "source": "self_request",
        "somatic_score": 5,
        "cognitive_score": 4,
        "functional_impact": 2,
        "q9_value": 0,
    })
    assert result.get("severity") == "moderate", \
        f"Expected moderate, got {result.get('severity')}"
    assert result.get("monitoring_level") == 3, \
        f"Expected level 3, got {result.get('monitoring_level')}"
    print("  ✓ PHQ-9 total=12 → moderate, level 3")
    print(f"  → PHQ-9 tiếp theo: {result.get('next_phq9_date')}")
    print("  → Frontend: hiển thị mức 'Cao', lên lịch PHQ-9 sau 14 ngày")

    # ----- Kịch bản 4: PHQ-9 severe (total 22) -----
    separator("4. PHQ-9 severe (total=22) → monitoring level 5")
    result = api("POST", "/phq/submit", token, {
        "phq_type": "phq9",
        "scores": [3, 3, 3, 2, 3, 2, 2, 2, 2],
        "total": 22,
        "source": "scheduled",
        "somatic_score": 9,
        "cognitive_score": 8,
        "functional_impact": 3,
        "q9_value": 0,
    })
    assert result.get("severity") == "severe", \
        f"Expected severe, got {result.get('severity')}"
    assert result.get("monitoring_level") == 5, \
        f"Expected level 5, got {result.get('monitoring_level')}"
    print("  ✓ PHQ-9 total=22 → severe, level 5 (Khủng hoảng)")
    print(f"  → PHQ-9 tiếp theo: {result.get('next_phq9_date')}")
    print("  → Frontend: hiển thị cảnh báo khẩn, PHQ-9 lại sau 7 ngày")

    # ----- Kịch bản 5: PHQ-9 với q9 >= 1 → safety event -----
    separator("5. PHQ-9 q9_value=2 → TỰ ĐỘNG tạo safety event")
    result = api("POST", "/phq/submit", token, {
        "phq_type": "phq9",
        "scores": [2, 1, 1, 1, 1, 1, 1, 1, 2],
        "total": 11,
        "source": "self_request",
        "somatic_score": 4,
        "cognitive_score": 3,
        "functional_impact": 2,
        "q9_value": 2,
    })
    print(f"  Monitoring level: {result.get('monitoring_level')}")
    print("  ✓ q9_value >= 1 → safety event đã được tạo tự động")
    print("  → Frontend: hiển thị thông tin hỗ trợ khẩn cấp")
    print("  → Tư vấn viên nhận cảnh báo ngay lập tức")

    # Kiểm tra monitoring status
    separator("5b. Kiểm tra monitoring status sau safety event")
    api("GET", "/monitoring/status", token)

    # ----- Kịch bản 6: Reject PHQ 3 lần → paused -----
    separator("6. Từ chối PHQ 3 lần → tạm dừng 30 ngày")
    for i in range(1, 4):
        print(f"  --- Lần từ chối thứ {i} ---")
        result = api("POST", "/phq/reject", token, {
            "phq_type": "phq2",
        })
    assert result.get("paused") is True, "Expected paused=True after 3 rejections"
    print("  ✓ 3 lần reject → paused 30 ngày")
    print(f"  → Tạm dừng đến: {result.get('paused_until')}")
    print("  → Frontend: KHÔNG hiện bảng hỏi trong 30 ngày tới")

    # ----- Kịch bản 7: Defer PHQ-9 2 lần → nâng mức -----
    separator("7. Hoãn PHQ-9 2 lần → nâng mức theo dõi")
    for i in range(1, 3):
        print(f"  --- Lần hoãn thứ {i} ---")
        result = api("POST", "/phq/defer-phq9", token, {})
    assert result.get("remind_at") is None, "Expected remind_at=None after 2 defers"
    print("  ✓ 2 lần defer → nâng mức theo dõi, không nhắc nữa")
    print(f"  → Monitoring level: {result.get('monitoring_level')}")
    print("  → Frontend: không hiện nhắc nhở PHQ-9 nữa")

    # ----- Xem lịch sử -----
    separator("8. Xem lịch sử PHQ")
    api("GET", "/phq/history?type=all&limit=10", token)

    separator("HOÀN TẤT - Tất cả kịch bản đã test xong")


if __name__ == "__main__":
    main()
