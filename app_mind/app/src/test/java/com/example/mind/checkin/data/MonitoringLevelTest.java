package com.example.mind.checkin.data;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

import org.junit.Test;

/**
 * Unit tests cho {@link MonitoringLevel}.
 *
 * Mapping TEST_CASES.md: P9-30..P9-36 + bảng tần suất tr.12 PDF.
 */
public class MonitoringLevelTest {

    // ─── fromPhq9Score — phân loại total + q9 override ───

    @Test
    public void fromPhq9Score_minimal_whenTotalLow() {
        // P9-30: total=2, q9=0 → STANDARD (1)
        assertEquals(MonitoringLevel.STANDARD, MonitoringLevel.fromPhq9Score(2, 0));
    }

    @Test
    public void fromPhq9Score_mild() {
        // P9-31: total=7, q9=0 → ENHANCED (2)
        assertEquals(MonitoringLevel.ENHANCED, MonitoringLevel.fromPhq9Score(7, 0));
    }

    @Test
    public void fromPhq9Score_moderate() {
        // P9-32: total=12, q9=0 → HIGH (3)
        assertEquals(MonitoringLevel.HIGH, MonitoringLevel.fromPhq9Score(12, 0));
    }

    @Test
    public void fromPhq9Score_moderatelySevere() {
        // P9-33: total=17, q9=0 → ACTIVE (4)
        assertEquals(MonitoringLevel.ACTIVE, MonitoringLevel.fromPhq9Score(17, 0));
    }

    @Test
    public void fromPhq9Score_severe() {
        // P9-34: total=22, q9=0 → CRISIS (5)
        assertEquals(MonitoringLevel.CRISIS, MonitoringLevel.fromPhq9Score(22, 0));
    }

    @Test
    public void q9_equals_1_forcesActive() {
        // PDF tr.11: q9=1 (vài ngày) → ACTIVE, bất kể total
        assertEquals(MonitoringLevel.ACTIVE, MonitoringLevel.fromPhq9Score(0, 1));
        assertEquals(MonitoringLevel.ACTIVE, MonitoringLevel.fromPhq9Score(5, 1));
    }

    @Test
    public void q9_equals_2_forcesCrisis() {
        // PDF tr.11: q9=2 → CRISIS
        assertEquals(MonitoringLevel.CRISIS, MonitoringLevel.fromPhq9Score(0, 2));
        assertEquals(MonitoringLevel.CRISIS, MonitoringLevel.fromPhq9Score(8, 2));
    }

    @Test
    public void q9_equals_3_forcesCrisis() {
        // PDF tr.11: q9=3 → CRISIS
        assertEquals(MonitoringLevel.CRISIS, MonitoringLevel.fromPhq9Score(0, 3));
        assertEquals(MonitoringLevel.CRISIS, MonitoringLevel.fromPhq9Score(22, 3));
    }

    @Test
    public void q9_override_dominates_lowTotal() {
        // P9-35: total thấp nhưng q9 cao vẫn kéo ML lên
        int level = MonitoringLevel.fromPhq9Score(3, 2);
        assertTrue("q9=2 phải kéo ML >= ACTIVE", level >= MonitoringLevel.ACTIVE);
    }

    // ─── getPhq2IntervalDays ───

    @Test
    public void phq2Interval_active_or_higher_uses_7_days() {
        // P2-23, P2-24: ML >= ACTIVE → 7 ngày
        assertEquals(7, MonitoringLevel.getPhq2IntervalDays(MonitoringLevel.ACTIVE));
        assertEquals(7, MonitoringLevel.getPhq2IntervalDays(MonitoringLevel.CRISIS));
    }

    @Test
    public void phq2Interval_low_levels_use_14_days() {
        // P2-20, P2-21, P2-22 (baseline): ML < ACTIVE → 14 ngày
        assertEquals(14, MonitoringLevel.getPhq2IntervalDays(MonitoringLevel.STANDARD));
        assertEquals(14, MonitoringLevel.getPhq2IntervalDays(MonitoringLevel.ENHANCED));
        assertEquals(14, MonitoringLevel.getPhq2IntervalDays(MonitoringLevel.HIGH));
    }

    // ─── getPhq9IntervalDays ───

    @Test
    public void phq9Interval_standard_has_no_followup() {
        assertEquals(Integer.MAX_VALUE,
                MonitoringLevel.getPhq9IntervalDays(MonitoringLevel.STANDARD, 0));
    }

    @Test
    public void phq9Interval_enhanced_is_21_days() {
        assertEquals(21, MonitoringLevel.getPhq9IntervalDays(MonitoringLevel.ENHANCED, 7));
    }

    @Test
    public void phq9Interval_high_is_14_days() {
        assertEquals(14, MonitoringLevel.getPhq9IntervalDays(MonitoringLevel.HIGH, 12));
    }

    @Test
    public void phq9Interval_active_is_10_days() {
        assertEquals(10, MonitoringLevel.getPhq9IntervalDays(MonitoringLevel.ACTIVE, 17));
    }

    @Test
    public void phq9Interval_crisis_score_20plus_is_7_days() {
        assertEquals(7, MonitoringLevel.getPhq9IntervalDays(MonitoringLevel.CRISIS, 22));
    }

    @Test
    public void phq9Interval_crisis_score_below_20_is_3_days() {
        // q9 trigger CRISIS mà tổng điểm thấp → theo dõi sát hơn
        assertEquals(3, MonitoringLevel.getPhq9IntervalDays(MonitoringLevel.CRISIS, 5));
    }

    // ─── getName ───

    @Test
    public void getName_returns_vietnamese() {
        assertEquals("Tiêu chuẩn", MonitoringLevel.getName(MonitoringLevel.STANDARD));
        assertEquals("Nâng cao", MonitoringLevel.getName(MonitoringLevel.ENHANCED));
        assertEquals("Cao", MonitoringLevel.getName(MonitoringLevel.HIGH));
        assertEquals("Tích cực", MonitoringLevel.getName(MonitoringLevel.ACTIVE));
        assertEquals("Khủng hoảng", MonitoringLevel.getName(MonitoringLevel.CRISIS));
    }

    @Test
    public void getName_unknown_level_defaults_to_standard() {
        assertEquals("Tiêu chuẩn", MonitoringLevel.getName(99));
        assertEquals("Tiêu chuẩn", MonitoringLevel.getName(0));
    }
}
