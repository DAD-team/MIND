"""
Behavior tests cho `_process_phq2_result()` — 4 nhánh quyết định PHQ-2.

Mapping TEST_CASES.md:
  - P2-30 (total≥3):             escalate_phq9
  - P2-31 (total=3 biên):         escalate_phq9
  - P2-32 (total=2, ≥2 flags):    escalate_phq9
  - P2-33/34 (total=2, <2 flags): shorten_interval, 7 ngày
  - P2-35/36 (total≤1):           normal, 14 ngày
  - P2-40..P2-44: đếm behavioral flags đúng
"""

import pytest

from controllers.phq_controller import _process_phq2_result, _count_behavioral_flags
from tests.conftest import setup_user_doc


UID = "test_user_phq2"
RECORD_ID = "fake_phq2_record"


# ---------------------------------------------------------------------------
#  Helpers
# ---------------------------------------------------------------------------

def _stub_flags(mocker, silence=0, academic=0, interaction=1.0,
                flat_affect=None, phq2_trend=0.0):
    """Patch scout signal functions mà _count_behavioral_flags gọi vào."""
    mocker.patch("services.scout.compute_silence_hours", return_value=silence)
    mocker.patch("services.scout.compute_academic_pressure", return_value=academic)
    mocker.patch("services.scout.compute_interaction_ratio", return_value=interaction)
    mocker.patch("services.scout.compute_flat_affect_avg", return_value=flat_affect)
    mocker.patch("services.scout.compute_phq2_trend", return_value=phq2_trend)


# ---------------------------------------------------------------------------
#  Nhánh 1 — Total >= 3 (ngưỡng cứng)
# ---------------------------------------------------------------------------

class TestBranch1HardEscalate:
    """P2-30, P2-31: total ≥ 3 luôn escalate, không cần check behavioral flags."""

    @pytest.mark.parametrize("total", [3, 4, 5, 6])
    def test_escalate_when_total_ge_3(self, patch_get_db, total):
        setup_user_doc(patch_get_db, UID, monitoring={})
        result = _process_phq2_result(UID, total, RECORD_ID)
        assert result["decision"] == "escalate_phq9"
        assert result["next_phq2_days"] is None

    def test_boundary_total_3(self, patch_get_db):
        """P2-31: boundary total = 3."""
        setup_user_doc(patch_get_db, UID, monitoring={})
        result = _process_phq2_result(UID, 3, RECORD_ID)
        assert result["decision"] == "escalate_phq9"

    def test_escalate_writes_decision_to_monitoring(self, patch_get_db):
        mocks = setup_user_doc(patch_get_db, UID, monitoring={})
        _process_phq2_result(UID, 5, RECORD_ID)

        mocks["mon_ref"].set.assert_called_once()
        args, kwargs = mocks["mon_ref"].set.call_args
        assert kwargs.get("merge") is True
        assert args[0]["phq2_decision"] == "escalate_phq9"

    def test_hard_threshold_does_not_call_scout(self, patch_get_db, mocker):
        """Total ≥ 3: _count_behavioral_flags KHÔNG được gọi (tối ưu tốc độ)."""
        setup_user_doc(patch_get_db, UID, monitoring={})
        spy = mocker.patch("controllers.phq_controller._count_behavioral_flags")

        _process_phq2_result(UID, 5, RECORD_ID)

        spy.assert_not_called()


# ---------------------------------------------------------------------------
#  Nhánh 2 — Total = 2, ≥ 2 flags → escalate
# ---------------------------------------------------------------------------

class TestBranch2SoftEscalate:

    def test_total_2_with_2_flags_escalates(self, patch_get_db, mocker):
        """P2-32: total=2, 2 flags (A im lặng + C giảm tương tác) → escalate."""
        setup_user_doc(patch_get_db, UID, monitoring={})
        _stub_flags(mocker,
                    silence=30,        # A = 1 (≥24h)
                    interaction=0.4)   # C = 1 (<0.5)

        result = _process_phq2_result(UID, 2, RECORD_ID)

        assert result["decision"] == "escalate_phq9"
        assert result["next_phq2_days"] is None

    def test_total_2_with_3_flags_escalates(self, patch_get_db, mocker):
        """P2-43 variant: nhiều flag hơn vẫn escalate."""
        setup_user_doc(patch_get_db, UID, monitoring={})
        _stub_flags(mocker,
                    silence=25,
                    academic=5,
                    flat_affect=0.6,
                    phq2_trend=0.3)

        result = _process_phq2_result(UID, 2, RECORD_ID)
        assert result["decision"] == "escalate_phq9"


# ---------------------------------------------------------------------------
#  Nhánh 3 — Total = 2, < 2 flags → shorten_interval
# ---------------------------------------------------------------------------

class TestBranch3ShortenInterval:

    def test_total_2_no_flags_shortens(self, patch_get_db, mocker):
        """P2-34: total=2, 0 flag → shorten_interval 7 ngày."""
        setup_user_doc(patch_get_db, UID, monitoring={})
        _stub_flags(mocker,
                    silence=5, academic=0, interaction=0.9,
                    flat_affect=None, phq2_trend=-0.5)

        result = _process_phq2_result(UID, 2, RECORD_ID)

        assert result["decision"] == "shorten_interval"
        assert result["next_phq2_days"] == 7

    def test_total_2_one_flag_shortens(self, patch_get_db, mocker):
        """P2-33/P2-41: total=2, chỉ 1 flag → chưa đủ để escalate."""
        setup_user_doc(patch_get_db, UID, monitoring={})
        _stub_flags(mocker, silence=30)  # chỉ A=1

        result = _process_phq2_result(UID, 2, RECORD_ID)

        assert result["decision"] == "shorten_interval"

    def test_shorten_writes_next_phq2_date_and_decision(self, patch_get_db, mocker):
        mocks = setup_user_doc(patch_get_db, UID, monitoring={})
        _stub_flags(mocker)  # all at safe defaults → 0 flags

        _process_phq2_result(UID, 2, RECORD_ID)

        mocks["mon_ref"].set.assert_called_once()
        payload = mocks["mon_ref"].set.call_args[0][0]
        assert payload["phq2_decision"] == "shorten_interval"
        assert "next_phq2_date" in payload


# ---------------------------------------------------------------------------
#  Nhánh 4 — Total ≤ 1 → normal
# ---------------------------------------------------------------------------

class TestBranch4Normal:

    @pytest.mark.parametrize("total", [0, 1])
    def test_total_le_1_normal(self, patch_get_db, total):
        """P2-35, P2-36: total ≤ 1 → normal, 14 ngày."""
        setup_user_doc(patch_get_db, UID, monitoring={})

        result = _process_phq2_result(UID, total, RECORD_ID)

        assert result["decision"] == "normal"
        assert result["next_phq2_days"] == 14

    def test_normal_does_not_call_scout(self, patch_get_db, mocker):
        """Total ≤ 1: không cần đếm flags."""
        setup_user_doc(patch_get_db, UID, monitoring={})
        spy = mocker.patch("controllers.phq_controller._count_behavioral_flags")

        _process_phq2_result(UID, 0, RECORD_ID)

        spy.assert_not_called()

    def test_normal_writes_next_phq2_date(self, patch_get_db):
        mocks = setup_user_doc(patch_get_db, UID, monitoring={})
        _process_phq2_result(UID, 1, RECORD_ID)

        payload = mocks["mon_ref"].set.call_args[0][0]
        assert payload["phq2_decision"] == "normal"
        assert "next_phq2_date" in payload


# ---------------------------------------------------------------------------
#  _count_behavioral_flags — test riêng từng dấu hiệu
#  PDF tr.8 + TEST_CASES P2-40..P2-44
# ---------------------------------------------------------------------------

class TestBehavioralFlagCounting:

    def test_no_flags(self, patch_get_db, mocker):
        _stub_flags(mocker,
                    silence=10, academic=0, interaction=1.0,
                    flat_affect=0.2, phq2_trend=-0.5)
        assert _count_behavioral_flags(UID) == 0

    def test_flag_a_silence(self, patch_get_db, mocker):
        """A: im lặng ≥ 24h."""
        _stub_flags(mocker, silence=30)
        assert _count_behavioral_flags(UID) == 1

    def test_flag_b_academic(self, patch_get_db, mocker):
        """B: áp lực học thuật ≥ 3."""
        _stub_flags(mocker, academic=3)
        assert _count_behavioral_flags(UID) == 1

    def test_flag_c_interaction(self, patch_get_db, mocker):
        """C: tỷ lệ tương tác < 0.5."""
        _stub_flags(mocker, interaction=0.4)
        assert _count_behavioral_flags(UID) == 1

    def test_flag_d_flat_affect_with_data(self, patch_get_db, mocker):
        """D: biểu cảm phẳng trung bình > 0.5 (có video)."""
        _stub_flags(mocker, flat_affect=0.6)
        assert _count_behavioral_flags(UID) == 1

    def test_flag_d_no_video_data(self, patch_get_db, mocker):
        """P2-44: consent=1 / không có video → D không tính."""
        _stub_flags(mocker, flat_affect=None)
        # Dù consent=1, các flag khác đều không có → 0
        assert _count_behavioral_flags(UID) == 0

    def test_flag_e_phq2_trend(self, patch_get_db, mocker):
        """E: độ dốc PHQ-2 > 0 (xu hướng xấu dần)."""
        _stub_flags(mocker, phq2_trend=0.3)
        assert _count_behavioral_flags(UID) == 1

    def test_flag_e_trend_zero_not_counted(self, patch_get_db, mocker):
        """Biên: độ dốc = 0 KHÔNG tính (strict >)."""
        _stub_flags(mocker, phq2_trend=0)
        assert _count_behavioral_flags(UID) == 0

    def test_all_five_flags(self, patch_get_db, mocker):
        _stub_flags(mocker,
                    silence=30, academic=5, interaction=0.3,
                    flat_affect=0.8, phq2_trend=0.7)
        assert _count_behavioral_flags(UID) == 5

    def test_flag_c_boundary_exactly_0_5(self, patch_get_db, mocker):
        """Biên: interaction = 0.5 KHÔNG tính (strict <)."""
        _stub_flags(mocker, interaction=0.5)
        assert _count_behavioral_flags(UID) == 0

    def test_flag_a_boundary_exactly_24h(self, patch_get_db, mocker):
        """Biên: silence = 24h TÍNH (>= 24)."""
        _stub_flags(mocker, silence=24)
        assert _count_behavioral_flags(UID) == 1
