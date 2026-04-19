"""
Unit tests cho pure functions trong controllers/phq_controller.py.
Không cần Firestore — test logic thuần.

Mapping TEST_CASES.md:
- P9-30..P9-36: severity & monitoring level
- Q9 safety protocol override
"""

import pytest

from controllers.phq_controller import _severity, _monitoring_level


# ---------------------------------------------------------------------------
#  _severity — phân loại PHQ-9 total → 5 nhãn
#  Ref: PDF tr.12, TEST_CASES P9-30..P9-34
# ---------------------------------------------------------------------------

class TestSeverity:
    @pytest.mark.parametrize("total,expected", [
        (0, "minimal"),
        (2, "minimal"),
        (4, "minimal"),
        (5, "mild"),
        (7, "mild"),
        (9, "mild"),
        (10, "moderate"),
        (12, "moderate"),
        (14, "moderate"),
        (15, "moderately_severe"),
        (17, "moderately_severe"),
        (19, "moderately_severe"),
        (20, "severe"),
        (22, "severe"),
        (27, "severe"),
    ])
    def test_severity_all_buckets(self, total, expected):
        assert _severity(total) == expected

    def test_boundary_minimal_mild(self):
        # Biên giữa minimal (0-4) và mild (5-9)
        assert _severity(4) == "minimal"
        assert _severity(5) == "mild"

    def test_boundary_moderate_moderately_severe(self):
        assert _severity(14) == "moderate"
        assert _severity(15) == "moderately_severe"

    def test_boundary_moderately_severe_severe(self):
        assert _severity(19) == "moderately_severe"
        assert _severity(20) == "severe"


# ---------------------------------------------------------------------------
#  _monitoring_level — q9 override + total-based bucketing
#  Ref: PDF tr.11-12, TEST_CASES P9-30..P9-36
# ---------------------------------------------------------------------------

class TestMonitoringLevel:
    @pytest.mark.parametrize("total,q9,expected_level", [
        # P9-30..P9-34: q9=0, level theo total
        (2, 0, 1),   # minimal
        (7, 0, 2),   # mild
        (12, 0, 3),  # moderate
        (17, 0, 4),  # moderately_severe
        (22, 0, 5),  # severe
    ])
    def test_level_by_total_when_q9_zero(self, total, q9, expected_level):
        assert _monitoring_level(total, q9) == expected_level

    @pytest.mark.parametrize("total,q9", [
        (0, 1), (5, 1), (10, 1), (14, 1),
    ])
    def test_q9_one_forces_level_4_minimum(self, total, q9):
        """P9-35: q9 >= 1 → level tối thiểu 4, bất kể total thấp."""
        assert _monitoring_level(total, q9) >= 4

    @pytest.mark.parametrize("total", [0, 5, 10, 15, 22, 27])
    def test_q9_three_forces_crisis(self, total):
        """P9-36: q9 = 3 luôn → level 5 (khủng hoảng), không phụ thuộc total."""
        assert _monitoring_level(total, 3) == 5

    @pytest.mark.parametrize("total", [0, 5, 10, 15, 22, 27])
    def test_q9_two_forces_crisis(self, total):
        """PDF tr.11: q9 = 2 (ý tưởng chủ động, hơn nửa số ngày) → CRISIS (5)."""
        assert _monitoring_level(total, 2) == 5

    def test_q9_override_beats_high_total(self):
        """q9=2 thắng total thấp: kéo thẳng lên level 5 (khủng hoảng)."""
        assert _monitoring_level(8, 2) == 5

    def test_severe_total_still_severe_when_q9_zero(self):
        """Total 22 không có q9 vẫn phải ra level 5."""
        assert _monitoring_level(22, 0) == 5

    def test_boundary_total_19_q9_zero(self):
        """Boundary: total = 19 (top moderately_severe) → level 4."""
        assert _monitoring_level(19, 0) == 4
