"""
Behavior tests cho `reject_phq()` trong controllers/phq_controller.py.

Mapping TEST_CASES.md:
  - RJ-01: Reject lần 1 → count=1, paused=False, show_after set, pending snoozed
  - RJ-02: Reject lần 2 → count=2, paused=False
  - RJ-03: Reject lần 3 → count=3, paused=True, paused_until=T+30d, pending dismissed
  - RJ-04: Submit sau reject → count reset (test riêng trong test_submit_flow)
  - Error case: phq_type không hợp lệ → 422

Các test này dùng fixture `patch_get_db` + helper `setup_user_doc` từ conftest.
"""

from datetime import datetime, timezone, timedelta

import pytest

from controllers.phq_controller import reject_phq
from tests.conftest import setup_user_doc, make_pending_doc


UID = "test_user_001"


# ---------------------------------------------------------------------------
#  Input validation
# ---------------------------------------------------------------------------

class TestRejectValidation:
    def test_invalid_phq_type_raises_422(self, patch_get_db):
        from fastapi import HTTPException
        with pytest.raises(HTTPException) as exc_info:
            reject_phq(UID, "phq5")
        assert exc_info.value.status_code == 422

    def test_empty_phq_type_raises(self, patch_get_db):
        from fastapi import HTTPException
        with pytest.raises(HTTPException):
            reject_phq(UID, "")


# ---------------------------------------------------------------------------
#  RJ-01, RJ-02 — Reject lần 1 và 2: snooze, không paused
# ---------------------------------------------------------------------------

class TestRejectNotYetPaused:

    def test_first_reject_sets_count_to_1(self, patch_get_db):
        """RJ-01: Reject lần đầu (count 0 → 1), chưa paused."""
        pending = make_pending_doc("phq2")
        mocks = setup_user_doc(patch_get_db, UID,
                               monitoring={"rejection_count": 0},
                               pending=[pending])

        result = reject_phq(UID, "phq2")

        assert result["rejection_count"] == 1
        assert result["paused"] is False
        assert result["paused_until"] is None
        assert result["show_after"] is not None  # ISO timestamp

    def test_first_reject_no_existing_monitoring(self, patch_get_db):
        """RJ-01 biên: user chưa có monitoring doc → count mặc định 0 → 1."""
        # monitoring=None → mon_snap.exists=False
        setup_user_doc(patch_get_db, UID, monitoring=None, pending=[])

        result = reject_phq(UID, "phq2")

        assert result["rejection_count"] == 1
        assert result["paused"] is False

    def test_second_reject_sets_count_to_2(self, patch_get_db):
        """RJ-02: Reject lần 2 (count 1 → 2), vẫn chưa paused."""
        setup_user_doc(patch_get_db, UID,
                       monitoring={"rejection_count": 1},
                       pending=[make_pending_doc("phq2")])

        result = reject_phq(UID, "phq2")

        assert result["rejection_count"] == 2
        assert result["paused"] is False

    def test_snooze_writes_show_after_to_pending(self, patch_get_db):
        """RJ-01: pending PHQ được update với show_after, KHÔNG bị dismissed."""
        pending = make_pending_doc("phq2")
        setup_user_doc(patch_get_db, UID,
                       monitoring={"rejection_count": 0},
                       pending=[pending])

        result = reject_phq(UID, "phq2")

        # Pending được update đúng 1 lần với show_after (không phải dismissed)
        pending.reference.update.assert_called_once()
        update_payload = pending.reference.update.call_args[0][0]
        assert "show_after" in update_payload
        assert update_payload["show_after"] == result["show_after"]
        # Không set status=dismissed khi chưa đủ 3 lần
        assert "status" not in update_payload

    def test_snooze_ignores_other_phq_type(self, patch_get_db):
        """User reject phq2 nhưng chỉ có pending phq9 → không update phq9 pending."""
        pending_phq9 = make_pending_doc("phq9")
        setup_user_doc(patch_get_db, UID,
                       monitoring={"rejection_count": 0},
                       pending=[pending_phq9])

        reject_phq(UID, "phq2")

        # pending phq9 KHÔNG bị đụng vào
        pending_phq9.reference.update.assert_not_called()

    def test_monitoring_written_with_merge(self, patch_get_db):
        """mon_ref.set phải được gọi với merge=True để không đè dữ liệu khác."""
        mocks = setup_user_doc(patch_get_db, UID,
                               monitoring={"rejection_count": 0, "level": 2},
                               pending=[])

        reject_phq(UID, "phq2")

        mocks["mon_ref"].set.assert_called_once()
        args, kwargs = mocks["mon_ref"].set.call_args
        assert kwargs.get("merge") is True
        assert args[0]["rejection_count"] == 1
        assert "last_rejected_at" in args[0]


# ---------------------------------------------------------------------------
#  RJ-03 — Reject lần 3: paused 30 ngày, pending dismissed
# ---------------------------------------------------------------------------

class TestRejectThirdStrike:

    def test_third_reject_sets_paused_true(self, patch_get_db):
        """RJ-03: count 2 → 3, paused=True."""
        setup_user_doc(patch_get_db, UID,
                       monitoring={"rejection_count": 2},
                       pending=[make_pending_doc("phq2")])

        result = reject_phq(UID, "phq2")

        assert result["rejection_count"] == 3
        assert result["paused"] is True
        assert result["paused_until"] is not None
        assert result["show_after"] is None  # không snooze, đã paused

    def test_paused_until_is_30_days_from_now(self, patch_get_db):
        """RJ-03: paused_until phải ~30 ngày kể từ hiện tại."""
        setup_user_doc(patch_get_db, UID,
                       monitoring={"rejection_count": 2},
                       pending=[])

        before = datetime.now(timezone.utc)
        result = reject_phq(UID, "phq2")
        after = datetime.now(timezone.utc)

        paused_until = datetime.fromisoformat(result["paused_until"])
        # Trong khoảng [before+30d, after+30d]
        assert before + timedelta(days=30) <= paused_until <= after + timedelta(days=30, seconds=1)

    def test_third_reject_dismisses_pending(self, patch_get_db):
        """RJ-03: pending PHQ-2 bị mark dismissed, KHÔNG dùng show_after."""
        pending = make_pending_doc("phq2")
        setup_user_doc(patch_get_db, UID,
                       monitoring={"rejection_count": 2},
                       pending=[pending])

        reject_phq(UID, "phq2")

        pending.reference.update.assert_called_once()
        payload = pending.reference.update.call_args[0][0]
        assert payload["status"] == "dismissed"
        assert "dismissed_at" in payload
        # Khi paused, KHÔNG có show_after
        assert "show_after" not in payload

    def test_fourth_reject_still_paused(self, patch_get_db):
        """Biên: count đã 3 → reject tiếp thành 4, vẫn paused."""
        setup_user_doc(patch_get_db, UID,
                       monitoring={"rejection_count": 3, "paused_until": "2099-01-01T00:00:00+00:00"},
                       pending=[])

        result = reject_phq(UID, "phq2")

        assert result["rejection_count"] == 4
        assert result["paused"] is True

    def test_dismiss_only_matching_phq_type(self, patch_get_db):
        """RJ-03: dismiss đúng phq_type được reject, các type khác giữ nguyên."""
        pending_phq2 = make_pending_doc("phq2", doc_id="pending_phq2")
        pending_phq9 = make_pending_doc("phq9", doc_id="pending_phq9")
        setup_user_doc(patch_get_db, UID,
                       monitoring={"rejection_count": 2},
                       pending=[pending_phq2, pending_phq9])

        reject_phq(UID, "phq2")

        pending_phq2.reference.update.assert_called_once()
        pending_phq9.reference.update.assert_not_called()


# ---------------------------------------------------------------------------
#  RJ-05 proxy — Response contract (frontend parse được)
# ---------------------------------------------------------------------------

class TestRejectResponseContract:
    """Response phải có 4 field chuẩn: rejection_count, paused, paused_until, show_after."""

    def test_response_shape_not_paused(self, patch_get_db):
        setup_user_doc(patch_get_db, UID,
                       monitoring={"rejection_count": 0},
                       pending=[])

        result = reject_phq(UID, "phq2")

        assert set(result.keys()) == {"rejection_count", "paused", "paused_until", "show_after"}
        assert isinstance(result["rejection_count"], int)
        assert isinstance(result["paused"], bool)
        assert result["paused_until"] is None
        assert isinstance(result["show_after"], str)  # ISO string

    def test_response_shape_when_paused(self, patch_get_db):
        setup_user_doc(patch_get_db, UID,
                       monitoring={"rejection_count": 2},
                       pending=[])

        result = reject_phq(UID, "phq2")

        assert set(result.keys()) == {"rejection_count", "paused", "paused_until", "show_after"}
        assert result["paused"] is True
        assert isinstance(result["paused_until"], str)
        assert result["show_after"] is None
