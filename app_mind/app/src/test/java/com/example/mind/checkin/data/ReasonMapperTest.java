package com.example.mind.checkin.data;

import static org.junit.Assert.assertEquals;

import org.junit.Test;

/**
 * Unit tests cho {@link ReasonMapper}.
 *
 * Mapping TEST_CASES.md + FRONTEND_PHQ_PENDING.md Bước 3.
 */
public class ReasonMapperTest {

    @Test
    public void null_reason_mapsTo_scheduled() {
        assertEquals("scheduled", ReasonMapper.mapReasonToSource(null));
    }

    @Test
    public void empty_reason_mapsTo_scheduled() {
        assertEquals("scheduled", ReasonMapper.mapReasonToSource(""));
    }

    @Test
    public void escalate_phq9_mapsTo_phq2_escalation() {
        // Spec: reason="escalate_phq9" → source="phq2_escalation"
        assertEquals("phq2_escalation", ReasonMapper.mapReasonToSource("escalate_phq9"));
    }

    @Test
    public void scout_reason_preservedAsIs() {
        assertEquals("scout", ReasonMapper.mapReasonToSource("scout"));
    }

    @Test
    public void scheduled_reason_preservedAsIs() {
        assertEquals("scheduled", ReasonMapper.mapReasonToSource("scheduled"));
    }

    @Test
    public void manual_reason_preservedAsIs() {
        assertEquals("manual", ReasonMapper.mapReasonToSource("manual"));
    }

    @Test
    public void unknown_reason_preservedAsIs() {
        // Forward-compatible: reason mới từ backend không crash
        assertEquals("future_reason", ReasonMapper.mapReasonToSource("future_reason"));
    }
}
