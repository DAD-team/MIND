package com.example.mind.checkin.data;

/**
 * 5 mức theo dõi sức khỏe tâm thần.
 * Quyết định tần suất sàng lọc và mức can thiệp.
 */
public class MonitoringLevel {

    public static final int STANDARD = 1;    // Tiêu chuẩn
    public static final int ENHANCED = 2;    // Nâng cao
    public static final int HIGH = 3;        // Cao
    public static final int ACTIVE = 4;      // Tích cực
    public static final int CRISIS = 5;      // Khủng hoảng

    public static String getName(int level) {
        switch (level) {
            case STANDARD: return "Tiêu chuẩn";
            case ENHANCED: return "Nâng cao";
            case HIGH:     return "Cao";
            case ACTIVE:   return "Tích cực";
            case CRISIS:   return "Khủng hoảng";
            default:       return "Tiêu chuẩn";
        }
    }

    /** Khoảng cách PHQ-2 tối thiểu (ngày) */
    public static int getPhq2IntervalDays(int level) {
        return (level >= ACTIVE) ? 7 : 14;
    }

    /** Khoảng cách PHQ-9 tối thiểu (ngày) */
    public static int getPhq9IntervalDays(int level, int lastPhq9Score) {
        if (level == CRISIS) return (lastPhq9Score >= 20) ? 7 : 3;
        if (level == ACTIVE) return 10;
        if (level == HIGH) return 14;
        if (level == ENHANCED) return 21;
        return Integer.MAX_VALUE; // Tiêu chuẩn: không cần PHQ-9
    }

    /** Xác định mức theo dõi từ điểm PHQ-9 */
    public static int fromPhq9Score(int totalScore, int q9Value) {
        // Câu 9 override
        if (q9Value >= 2) return CRISIS;
        if (q9Value == 1) return ACTIVE;

        // Theo tổng điểm
        if (totalScore >= 20) return CRISIS;
        if (totalScore >= 15) return ACTIVE;
        if (totalScore >= 10) return HIGH;
        if (totalScore >= 5)  return ENHANCED;
        return STANDARD;
    }
}
