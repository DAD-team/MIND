"""
Unit tests cho pure scoring helpers trong services/scout.py.
Không cần Firestore.

Mapping TEST_CASES.md:
- SC-10..SC-14: _score_silence
- SC-20..SC-25: _score_phq2_trend
- SC-30..SC-34: _score_academic
- SC-40..SC-44: _score_interaction_ratio
- SC-50..SC-54: _score_flat_affect, _score_duchenne
- Composite: compute_risk_score
"""

import pytest

from services.scout import (
    _score_silence,
    _score_phq2_trend,
    _score_academic,
    _score_interaction_ratio,
    _score_flat_affect,
    _score_duchenne,
    compute_risk_score,
)


# ---------------------------------------------------------------------------
#  _score_silence — tr.3
# ---------------------------------------------------------------------------

class TestScoreSilence:
    @pytest.mark.parametrize("hours,expected", [
        (80, 4.0),   # SC-10: >= 72h
        (72, 4.0),   # biên
        (48, 3.0),   # SC-11
        (30, 2.0),   # SC-12
        (24, 2.0),   # biên
        (18, 1.0),   # SC-13
        (12, 1.0),   # biên
        (6, 0.0),    # SC-14
        (0, 0.0),
    ])
    def test_silence_buckets(self, hours, expected):
        assert _score_silence(hours) == expected


# ---------------------------------------------------------------------------
#  _score_phq2_trend — tr.3 (độ dốc)
# ---------------------------------------------------------------------------

class TestScorePhq2Trend:
    @pytest.mark.parametrize("slope,expected", [
        (2.0, 2.0),    # SC-20: tăng rõ rệt
        (0.6, 2.0),    # > 0.5 → +2
        (0.5, 1.0),    # SC-21: biên (> 0 nhưng không > 0.5)
        (0.1, 1.0),    # SC-22: tăng nhẹ
        (0.0, 0.0),    # SC-23: ổn định
        (-2.0, 0.0),   # SC-24: giảm = ổn định/cải thiện
    ])
    def test_trend_buckets(self, slope, expected):
        assert _score_phq2_trend(slope) == expected


# ---------------------------------------------------------------------------
#  _score_academic — tr.4
# ---------------------------------------------------------------------------

class TestScoreAcademic:
    @pytest.mark.parametrize("weight,expected", [
        (6, 2.0),    # SC-30: >= 6 (2 bài thi)
        (10, 2.0),
        (5, 1.5),    # SC-31: 1 thi + 1 deadline
        (3, 1.5),    # biên
        (2, 1.0),    # SC-32
        (1, 1.0),    # SC-33: 1 thuyết trình
        (0, 0.0),    # SC-34
    ])
    def test_academic_buckets(self, weight, expected):
        assert _score_academic(weight) == expected


# ---------------------------------------------------------------------------
#  _score_interaction_ratio — tr.4
# ---------------------------------------------------------------------------

class TestScoreInteractionRatio:
    @pytest.mark.parametrize("ratio,expected", [
        (0.17, 2.0),   # SC-40: 1/6, giảm hơn 70%
        (0.29, 2.0),   # < 0.3
        (0.33, 1.5),   # SC-41: giảm 50-70%
        (0.49, 1.5),
        (0.60, 0.5),   # SC-42: giảm 30-50%
        (0.69, 0.5),
        (0.83, 0.0),   # SC-43: gần như không giảm
        (1.0, 0.0),
    ])
    def test_ratio_buckets(self, ratio, expected):
        assert _score_interaction_ratio(ratio) == expected


# ---------------------------------------------------------------------------
#  _score_flat_affect & _score_duchenne — tr.4-5
# ---------------------------------------------------------------------------

class TestScoreFlatAffect:
    def test_none_returns_zero(self):
        assert _score_flat_affect(None) == 0.0

    @pytest.mark.parametrize("avg,expected", [
        (0.8, 1.5),    # SC-50: > 0.7
        (0.71, 1.5),
        (0.7, 0.5),    # biên
        (0.6, 0.5),    # SC-51
        (0.5, 0.5),
        (0.49, 0.0),
        (0.3, 0.0),    # SC-52
    ])
    def test_flat_affect_buckets(self, avg, expected):
        assert _score_flat_affect(avg) == expected


class TestScoreDuchenne:
    def test_none_returns_zero(self):
        assert _score_duchenne(None) == 0.0

    @pytest.mark.parametrize("avg,expected", [
        (0.15, 1.0),   # SC-52: < 0.2
        (0.1, 1.0),
        (0.2, 0.0),    # biên
        (0.3, 0.0),
        (0.5, 0.0),    # SC-50 (không có smile issue)
    ])
    def test_duchenne_buckets(self, avg, expected):
        assert _score_duchenne(avg) == expected


# ---------------------------------------------------------------------------
#  compute_risk_score — composite, trần 10
# ---------------------------------------------------------------------------

class TestComputeRiskScore:
    def _signals(self, **overrides):
        """Default signals: mọi thứ ở mức an toàn (RS=0)."""
        base = {
            "silence_hours": 0,
            "phq2_trend": 0,
            "academic_pressure": 0,
            "interaction_ratio": 1.0,
            "flat_affect_avg": None,
            "duchenne_avg": None,
        }
        base.update(overrides)
        return base

    def test_all_zero_returns_zero(self):
        assert compute_risk_score(self._signals()) == 0.0

    def test_silence_only(self):
        # im lặng >= 72h → +4
        assert compute_risk_score(self._signals(silence_hours=80)) == 4.0

    def test_cap_at_10(self):
        """Worst case tất cả max: 4 + 2 + 2 + 2 + 1.5 + 1 = 12.5 → capped 10."""
        score = compute_risk_score(self._signals(
            silence_hours=100,
            phq2_trend=1.0,
            academic_pressure=6,
            interaction_ratio=0.1,
            flat_affect_avg=0.9,
            duchenne_avg=0.1,
        ))
        assert score == 10.0

    def test_composite_typical_at_risk(self):
        """Ví dụ P2-40: im lặng 30h (+2) + tương tác 0.4 (+1.5) = 3.5."""
        score = compute_risk_score(self._signals(
            silence_hours=30,
            interaction_ratio=0.4,
        ))
        assert score == pytest.approx(3.5)

    def test_threshold_gt_6_for_early_phq2(self):
        """SC-61: RS > 6 → Scout đủ điều kiện gửi PHQ-2 sớm."""
        score = compute_risk_score(self._signals(
            silence_hours=80,         # +4
            phq2_trend=1.0,           # +2
            academic_pressure=3,      # +1.5
        ))
        # 4 + 2 + 1.5 = 7.5, vượt ngưỡng 6
        assert score > 6.0

    def test_threshold_ge_4_for_priority(self):
        """SC-64: RS >= 4 → đánh dấu ưu tiên."""
        score = compute_risk_score(self._signals(
            silence_hours=48,     # +3
            interaction_ratio=0.4,  # +1.5
        ))
        assert score >= 4.0

    def test_signals_without_video_data(self):
        """SC-53/SC-54: consent=1 hoặc không có video → skip flat/duchenne."""
        score = compute_risk_score(self._signals(
            silence_hours=30,
            flat_affect_avg=None,
            duchenne_avg=None,
        ))
        # Chỉ silence đóng góp
        assert score == 2.0
