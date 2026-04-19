package com.example.mind.checkin.data;

/**
 * Map `reason` (từ pending /phq/pending) → `source` (gửi khi submit).
 *
 * Quy tắc theo FRONTEND_PHQ_PENDING.md:
 *   - reason=null / rỗng        → "scheduled"
 *   - reason="escalate_phq9"    → "phq2_escalation"
 *   - còn lại                   → giữ nguyên reason
 */
public final class ReasonMapper {

    private ReasonMapper() {}

    public static String mapReasonToSource(String reason) {
        if (reason == null || reason.isEmpty()) return "scheduled";
        if ("escalate_phq9".equals(reason)) return "phq2_escalation";
        return reason;
    }
}
