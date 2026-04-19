"""
Flow test cho chức năng Video Selfie → Phân tích khuôn mặt → Lưu 4 chỉ số.

Kịch bản user thật (Android app):
  1. User ở mức đồng thuận ≥ 2 bấm nút Check-in cảm xúc + quay video 5s
  2. App upload file MP4 qua POST /videos/upload (multipart)
  3. Backend validate MIME + size, lưu tạm file
  4. analyzer.analyze_selfie_video() — MediaPipe trích xuất landmarks → 6 số
  5. File tạm XÓA NGAY (PDF tr.14: "video bị xóa ngay sau khi trích 4 chỉ số")
  6. Firestore lưu ONLY các số (duchenne, flat_affect, gaze, head_down, ...)
  7. Scout 2h sau đọc analysis để tính compute_flat_affect_avg

Quy tắc PDF cần verify (tr.1, tr.14, tr.15):
  ✓ Consent < 2 → không cho quay video
  ✓ Video upload → chỉ lưu 4-6 con số, KHÔNG lưu bytes video
  ✓ Temp file XÓA NGAY dù có lỗi phân tích
  ✓ Không phát hiện mặt → trả 422, không crash
  ✓ Analysis ghi vào users/{uid}/analyses → scout đọc được

Chạy: pytest tests/test_video_flow.py -v -s
"""

import asyncio
import os
from datetime import datetime, timezone, timedelta
from unittest.mock import AsyncMock, patch

import pytest

from tests.fake_firestore import FakeFirestore


# ---------------------------------------------------------------------------
#  Fixture: FakeFirestore + bypass MediaPipe thật
# ---------------------------------------------------------------------------

@pytest.fixture
def fake_db():
    return FakeFirestore()


@pytest.fixture
def video_env(monkeypatch, fake_db):
    """
    Setup môi trường: FakeFirestore + mock analyze_selfie_video.
    Test tự cấu hình fake_result trong mỗi case.
    """
    monkeypatch.setattr("services.firebase.get_db", lambda: fake_db)
    monkeypatch.setattr("controllers.video_controller.get_db", lambda: fake_db)
    monkeypatch.setattr("controllers.auth_controller.get_db", lambda: fake_db)
    monkeypatch.setattr("services.scout.get_db", lambda: fake_db)
    return fake_db


class FakeUploadFile:
    """
    Giả lập FastAPI UploadFile. Chỉ cần 3 thuộc tính/method mà
    process_video() đụng đến: .content_type, .filename, await .read()
    """

    def __init__(self, content: bytes, content_type: str = "video/mp4",
                 filename: str = "selfie.mp4"):
        self._content = content
        self.content_type = content_type
        self.filename = filename

    async def read(self) -> bytes:
        return self._content


def make_emotion_result(**overrides):
    """Tạo EmotionResult giả — tránh phải chạy MediaPipe thật."""
    from services.analyzer import EmotionResult
    defaults = dict(
        duchenne_ratio=         0.35,
        flat_affect_score=      0.4,
        gaze_instability=       0.2,
        head_down_ratio=        0.15,
        blink_duration_avg=     0.18,
        behavioral_risk_score=  0.3,
        frames_processed=       120,
        confidence=             0.8,
    )
    defaults.update(overrides)
    return EmotionResult(**defaults)


def run_async(coro):
    return asyncio.run(coro)


def _print_flow(title: str, steps: list[str]):
    print(f"\n=== {title} ===")
    for i, s in enumerate(steps, 1):
        print(f"  {i}. {s}")


# ===========================================================================
#  V1 — Happy path: consent=2, MP4 hợp lệ, mặt bình thường
# ===========================================================================

class TestV1_VideoHappyPath:
    """
    Bước đầy đủ như user thật: upload → phân tích → lưu → xóa temp.
    Expected: Firestore có 1 analysis doc với 6 chỉ số + metadata.
    """

    UID = "user_v1_happy"

    def test_flow(self, video_env, monkeypatch):
        from controllers.video_controller import process_video

        # Seed user consent=2
        video_env.collection("users").document(self.UID).set({"consent_level": 2})

        # Mock MediaPipe — trả kết quả "bình thường" (flat affect thấp)
        fake_result = make_emotion_result(
            duchenne_ratio=0.55, flat_affect_score=0.3,
            behavioral_risk_score=0.25,
        )
        monkeypatch.setattr(
            "controllers.video_controller.analyze_selfie_video",
            lambda path: fake_result,
        )

        # Track xem temp file có được xóa không
        removed_paths = []
        original_remove = os.remove

        def tracked_remove(path):
            removed_paths.append(path)
            original_remove(path)

        monkeypatch.setattr("controllers.video_controller.os.remove", tracked_remove)

        # User upload video 1MB giả
        fake_file = FakeUploadFile(content=b"\x00" * (1024 * 1024))

        # --- Gọi flow ---
        doc = run_async(process_video(fake_file, self.UID))

        # --- Verify output trả về ---
        assert "video_id" in doc
        assert doc["user_id"] == self.UID
        assert doc["frames"] == 120
        assert doc["confidence"] == 0.8

        # Risk classify: score=0.25 < 0.35 → "safe" / "Tốt"
        assert doc["risk"]["level"] == "safe"
        assert doc["risk"]["label"] == "Tốt"

        # Payload chỉ chứa 6 CON SỐ (PDF tr.14 — không video, không landmarks)
        payload = doc["result"]
        expected_keys = {
            "duchenne_ratio", "flat_affect_score", "gaze_instability",
            "head_down_ratio", "blink_duration_avg_s", "behavioral_risk_score",
        }
        assert set(payload.keys()) == expected_keys
        assert payload["duchenne_ratio"] == 0.55

        # --- Verify Firestore lưu ĐÚNG cấu trúc ---
        saved = video_env.read_doc(
            f"users/{self.UID}/analyses", doc["video_id"],
        )
        assert saved is not None
        assert set(saved["result"].keys()) == expected_keys

        # KHÔNG có key nào chứa raw video/bytes/path
        flat_str = str(saved).lower()
        for forbidden in ("bytes", "b64", "binary", "/tmp", "c:\\", "filepath"):
            assert forbidden not in flat_str, f"LEAK: '{forbidden}' trong Firestore doc"

        # --- Verify temp file đã xóa (PDF: "video xóa ngay") ---
        assert len(removed_paths) == 1, "Temp file phải xóa đúng 1 lần"

        _print_flow("V1 — Video happy path (consent=2)", [
            f"Upload MP4 1MB ✓",
            f"MediaPipe mock → 6 chỉ số ✓",
            f"Risk classify: {doc['risk']['label']} ({doc['risk']['level']}) ✓",
            f"Firestore chỉ lưu 6 số, KHÔNG lưu video bytes ✓",
            f"Temp file xóa ngay: {os.path.basename(removed_paths[0])} ✓",
        ])


# ===========================================================================
#  V2 — Consent level 1: bị chặn ngay (PDF tr.1)
# ===========================================================================

class TestV2_ConsentGate:
    """
    PDF tr.1 bảng đồng thuận: Mức 1 "Không" cho quay video.
    Router kiểm tra consent < 2 → raise HTTP 403.
    """

    UID = "user_v2_consent1"

    def test_consent_1_blocked(self, video_env):
        from routers.videos import upload_video
        from fastapi import HTTPException

        video_env.collection("users").document(self.UID).set({"consent_level": 1})

        fake_file = FakeUploadFile(content=b"\x00" * 1024)

        # Gọi handler trực tiếp (bypass Depends)
        with pytest.raises(HTTPException) as exc:
            run_async(upload_video(file=fake_file,
                                   payload={"uid": self.UID}))

        assert exc.value.status_code == 403
        assert "đồng thuận" in exc.value.detail.lower() \
               or "consent" in exc.value.detail.lower()

        # Không có analysis doc nào được tạo
        assert video_env.read_collection(f"users/{self.UID}/analyses") == []

    def test_consent_3_allowed(self, video_env, monkeypatch):
        from routers.videos import upload_video

        video_env.collection("users").document(self.UID).set({"consent_level": 3})

        monkeypatch.setattr(
            "controllers.video_controller.analyze_selfie_video",
            lambda path: make_emotion_result(),
        )

        fake_file = FakeUploadFile(content=b"\x00" * 1024)
        doc = run_async(upload_video(file=fake_file,
                                     payload={"uid": self.UID}))
        assert "video_id" in doc

        _print_flow("V2 — Consent gate", [
            f"Consent=1 → HTTP 403, không tạo analysis ✓",
            f"Consent=3 → upload thành công ✓",
        ])


# ===========================================================================
#  V3 — MIME không hợp lệ: 415
# ===========================================================================

class TestV3_InvalidMime:

    UID = "user_v3_mime"

    def test_text_file_rejected(self, video_env):
        from controllers.video_controller import process_video
        from fastapi import HTTPException

        fake_file = FakeUploadFile(
            content=b"hello", content_type="text/plain", filename="hack.txt",
        )
        with pytest.raises(HTTPException) as exc:
            run_async(process_video(fake_file, self.UID))

        assert exc.value.status_code == 415
        assert video_env.read_collection(f"users/{self.UID}/analyses") == []

    @pytest.mark.parametrize("mime", [
        "video/mp4", "video/quicktime", "video/x-msvideo", "video/webm",
    ])
    def test_valid_mimes_accepted(self, video_env, monkeypatch, mime):
        from controllers.video_controller import process_video

        monkeypatch.setattr(
            "controllers.video_controller.analyze_selfie_video",
            lambda path: make_emotion_result(),
        )

        fake_file = FakeUploadFile(content=b"\x00" * 1024, content_type=mime)
        doc = run_async(process_video(fake_file, self.UID))
        assert "video_id" in doc

        _print_flow(f"V3 — MIME {mime}", [f"Accepted ✓"])


# ===========================================================================
#  V4 — File quá lớn (> 200MB): 413
# ===========================================================================

class TestV4_FileTooLarge:

    UID = "user_v4_large"

    def test_over_200mb(self, video_env):
        from controllers.video_controller import process_video, MAX_SIZE_BYTES
        from fastapi import HTTPException

        # Payload > 200MB (chỉ cần 1 byte vượt)
        huge_content = b"\x00" * (MAX_SIZE_BYTES + 1)
        fake_file = FakeUploadFile(content=huge_content)

        with pytest.raises(HTTPException) as exc:
            run_async(process_video(fake_file, self.UID))

        assert exc.value.status_code == 413
        # Không ghi analysis
        assert video_env.read_collection(f"users/{self.UID}/analyses") == []


# ===========================================================================
#  V5 — MediaPipe không phát hiện mặt → 422 + TEMP FILE VẪN XÓA
# ===========================================================================

class TestV5_NoFaceDetected:
    """
    Analyzer raise ValueError("Không phát hiện khuôn mặt"). Backend:
      - Trả HTTP 422
      - Vẫn xóa temp file (quy tắc privacy tuyệt đối của PDF tr.14-15)
    """

    UID = "user_v5_noface"

    def test_no_face_returns_422_and_cleans_temp(self, video_env, monkeypatch):
        from controllers.video_controller import process_video
        from fastapi import HTTPException

        def raising_analyze(path):
            raise ValueError("Không phát hiện khuôn mặt trong video.")

        monkeypatch.setattr(
            "controllers.video_controller.analyze_selfie_video", raising_analyze,
        )

        removed = []
        original_remove = os.remove

        def tracked_remove(path):
            removed.append(path)
            original_remove(path)

        monkeypatch.setattr("controllers.video_controller.os.remove", tracked_remove)

        fake_file = FakeUploadFile(content=b"\x00" * 1024)
        with pytest.raises(HTTPException) as exc:
            run_async(process_video(fake_file, self.UID))

        assert exc.value.status_code == 422
        # Quan trọng: dù phân tích fail, temp file vẫn phải bị xóa
        assert len(removed) == 1

        # Không có analysis doc được ghi khi lỗi
        assert video_env.read_collection(f"users/{self.UID}/analyses") == []

        _print_flow("V5 — No face detected", [
            f"Analyzer raise ValueError ✓",
            f"HTTP 422 + temp file vẫn xóa (privacy) ✓",
            f"Không ghi Firestore khi lỗi ✓",
        ])

    def test_generic_error_still_cleans_temp(self, video_env, monkeypatch):
        """Lỗi bất ngờ khác cũng phải xóa temp file."""
        from controllers.video_controller import process_video
        from fastapi import HTTPException

        monkeypatch.setattr(
            "controllers.video_controller.analyze_selfie_video",
            lambda p: (_ for _ in ()).throw(RuntimeError("mediapipe crash")),
        )

        removed = []
        original_remove = os.remove
        monkeypatch.setattr("controllers.video_controller.os.remove",
                            lambda p: (removed.append(p), original_remove(p)))

        fake_file = FakeUploadFile(content=b"\x00" * 1024)
        with pytest.raises(HTTPException) as exc:
            run_async(process_video(fake_file, self.UID))

        assert exc.value.status_code == 500
        assert len(removed) == 1


# ===========================================================================
#  V6 — Risk classification 3 ngưỡng
# ===========================================================================

class TestV6_RiskClassification:
    """
    classify_risk: < 0.35 safe, 0.35-0.60 warning, > 0.60 high_risk.
    """

    @pytest.mark.parametrize("score, expected_level", [
        (0.0,   "safe"),
        (0.34,  "safe"),
        (0.35,  "warning"),
        (0.50,  "warning"),
        (0.60,  "warning"),
        (0.61,  "high_risk"),
        (1.0,   "high_risk"),
    ])
    def test_risk_buckets(self, score, expected_level):
        from controllers.video_controller import classify_risk
        assert classify_risk(score)["level"] == expected_level


# ===========================================================================
#  V7 — Tích hợp Scout: analysis → compute_flat_affect_avg
# ===========================================================================

class TestV7_ScoutIntegration:
    """
    PDF tr.3 Tín hiệu 5: "Biểu cảm phẳng trung bình 7 ngày".
    Sau khi user upload nhiều video, scout.compute_flat_affect_avg() phải
    đọc đúng giá trị từ collection analyses.
    """

    UID = "user_v7_scout"

    def test_analysis_feeds_scout(self, video_env, monkeypatch):
        from controllers.video_controller import process_video
        from services.scout import compute_flat_affect_avg

        video_env.collection("users").document(self.UID).set({"consent_level": 2})

        # Mô phỏng 3 lần upload trong 7 ngày, flat_affect tăng dần
        flat_values = [0.4, 0.6, 0.8]
        for i, flat in enumerate(flat_values):
            monkeypatch.setattr(
                "controllers.video_controller.analyze_selfie_video",
                lambda path, f=flat: make_emotion_result(flat_affect_score=f),
            )
            run_async(process_video(
                FakeUploadFile(content=b"\x00" * 1024), self.UID,
            ))

        # Scout đọc lại
        avg = compute_flat_affect_avg(self.UID)
        assert avg is not None
        expected_avg = round(sum(flat_values) / len(flat_values), 3)
        assert abs(avg - expected_avg) < 0.01, f"avg={avg}, expected≈{expected_avg}"

        _print_flow("V7 — Video ↔ Scout integration", [
            f"3 video uploaded với flat_affect {flat_values} ✓",
            f"compute_flat_affect_avg → {avg} (≈ {expected_avg}) ✓",
            f"Scout đọc được dữ liệu video đã phân tích ✓",
        ])
