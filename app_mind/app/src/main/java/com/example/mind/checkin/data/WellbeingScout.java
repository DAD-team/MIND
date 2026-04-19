package com.example.mind.checkin.data;

import android.content.Context;

import com.example.mind.common.UserPrefs;

/**
 * Wellbeing Scout — "mắt thầm lặng" của hệ thống.
 *
 * Tính điểm rủi ro hành vi (0-10) từ 6 tín hiệu, quyết định hành động.
 * Trên server: chạy tự động mỗi 2 giờ (7:00-23:00) qua Cloud Scheduler.
 * Trên client: có thể gọi thủ công để demo hoặc khi mở app.
 */
public class WellbeingScout {

    /** Kết quả 1 chu kỳ Scout */
    public static class ScoutResult {
        public double silenceHours;
        public double phq2Slope;
        public int academicWeight;
        public double interactionRatio;
        public double flatAffect;     // -1 nếu không có dữ liệu
        public double duchenneSmile;  // -1 nếu không có dữ liệu

        public double riskScore;
        public int decision; // 1-5

        // Điểm thành phần
        public double silencePoints;
        public double phq2Points;
        public double academicPoints;
        public double interactionPoints;
        public double flatAffectPoints;
        public double smilePoints;
    }

    // Quyết định
    public static final int DECISION_CRISIS_PROTOCOL = 1;
    public static final int DECISION_SCHEDULE_PHQ2 = 2;
    public static final int DECISION_GENTLE_REMINDER = 3;
    public static final int DECISION_MARK_PRIORITY = 4;
    public static final int DECISION_LOG_ONLY = 5;

    /**
     * Tính điểm rủi ro hành vi từ 6 tín hiệu.
     * Công thức đúng theo bảng trang 4-5 Quy tắc vận hành.
     */
    public static ScoutResult calculate(Context context) {
        ScoutResult r = new ScoutResult();
        MoodStorage mood = new MoodStorage(context);
        PhqHistoryManager phq = new PhqHistoryManager(context);
        UserPrefs prefs = new UserPrefs(context);

        // ─── Tín hiệu 1: Khoảng im lặng ───
        r.silenceHours = mood.getSilenceHours();
        if (r.silenceHours >= 72)      r.silencePoints = 4;
        else if (r.silenceHours >= 48) r.silencePoints = 3;
        else if (r.silenceHours >= 24) r.silencePoints = 2;
        else if (r.silenceHours >= 12) r.silencePoints = 1;
        else                           r.silencePoints = 0;

        // ─── Tín hiệu 2: Xu hướng PHQ-2 ───
        r.phq2Slope = phq.getPhq2Slope();
        if (r.phq2Slope > 0.5)      r.phq2Points = 2;
        else if (r.phq2Slope > 0)   r.phq2Points = 1;
        else                        r.phq2Points = 0;

        // ─── Tín hiệu 3: Áp lực học thuật ───
        // academicWeight phải được set từ bên ngoài (API hoặc local schedule)
        r.academicWeight = 0; // default, caller nên set trước khi gọi decide()
        r.academicPoints = 0;

        // ─── Tín hiệu 4: Tỷ lệ tương tác ───
        r.interactionRatio = mood.getInteractionRatio();
        if (r.interactionRatio < 0.3)      r.interactionPoints = 2;
        else if (r.interactionRatio < 0.5) r.interactionPoints = 1.5;
        else if (r.interactionRatio < 0.7) r.interactionPoints = 0.5;
        else                               r.interactionPoints = 0;

        // ─── Tín hiệu 5 & 6: Chỉ khi consent >= 2 ───
        // Video metrics lưu trên server, không lưu local.
        // ScoutWorker sẽ gọi setVideoSignals() sau khi lấy từ server.
        r.flatAffect = -1;
        r.flatAffectPoints = 0;
        r.duchenneSmile = -1;
        r.smilePoints = 0;

        // Tổng điểm rủi ro (tối đa 10)
        r.riskScore = Math.min(10,
                r.silencePoints + r.phq2Points + r.academicPoints +
                r.interactionPoints + r.flatAffectPoints + r.smilePoints);

        return r;
    }

    /**
     * Cập nhật tín hiệu áp lực học thuật (gọi sau khi có dữ liệu schedule).
     */
    public static void setAcademicWeight(ScoutResult r, int totalWeight) {
        r.academicWeight = totalWeight;
        if (totalWeight >= 6)      r.academicPoints = 2;
        else if (totalWeight >= 3) r.academicPoints = 1.5;
        else if (totalWeight >= 1) r.academicPoints = 1;
        else                       r.academicPoints = 0;

        // Tính lại tổng
        r.riskScore = Math.min(10,
                r.silencePoints + r.phq2Points + r.academicPoints +
                r.interactionPoints + r.flatAffectPoints + r.smilePoints);
    }

    /**
     * Cập nhật tín hiệu biểu cảm + nụ cười (consent >= 2, từ video analysis).
     */
    public static void setVideoSignals(ScoutResult r, double flatAffect, double duchenneSmile) {
        r.flatAffect = flatAffect;
        if (flatAffect > 0.7)      r.flatAffectPoints = 1.5;
        else if (flatAffect > 0.5) r.flatAffectPoints = 0.5;
        else                       r.flatAffectPoints = 0;

        r.duchenneSmile = duchenneSmile;
        if (duchenneSmile >= 0 && duchenneSmile < 0.2) r.smilePoints = 1;
        else                                           r.smilePoints = 0;

        // Tính lại tổng
        r.riskScore = Math.min(10,
                r.silencePoints + r.phq2Points + r.academicPoints +
                r.interactionPoints + r.flatAffectPoints + r.smilePoints);
    }

    /**
     * Quyết định hành động (kiểm tra từ trên xuống, dừng ở điều kiện đầu tiên).
     * Theo bảng trang 5 Quy tắc vận hành.
     */
    public static int decide(Context context, ScoutResult r) {
        UserPrefs prefs = new UserPrefs(context);
        PhqHistoryManager phq = new PhqHistoryManager(context);

        // 1. PHQ-9 gần nhất có câu 9 >= 1 VÀ mức chưa phải Tích cực/Khủng hoảng
        int lastQ9 = getLastQ9Value(phq);
        int currentLevel = prefs.getMonitoringLevel();
        if (lastQ9 >= 1 && currentLevel < MonitoringLevel.ACTIVE) {
            r.decision = DECISION_CRISIS_PROTOCOL;
            return r.decision;
        }

        // 2. Điểm rủi ro > 6 VÀ PHQ-2 gần nhất >= 7 ngày
        long daysSincePhq2 = getDaysSinceLastPhq2(phq);
        if (r.riskScore > 6 && daysSincePhq2 >= 7) {
            r.decision = DECISION_SCHEDULE_PHQ2;
            return r.decision;
        }

        // 3. Điểm rủi ro > 6 VÀ PHQ-2 < 7 ngày VÀ consent >= 3
        if (r.riskScore > 6 && daysSincePhq2 < 7 && prefs.getConsentLevel() >= 3) {
            r.decision = DECISION_GENTLE_REMINDER;
            return r.decision;
        }

        // 4. Điểm rủi ro 4-6
        if (r.riskScore >= 4) {
            r.decision = DECISION_MARK_PRIORITY;
            return r.decision;
        }

        // 5. Điểm rủi ro < 4
        r.decision = DECISION_LOG_ONLY;
        return r.decision;
    }

    public static String getDecisionName(int decision) {
        switch (decision) {
            case DECISION_CRISIS_PROTOCOL: return "Kích hoạt giao thức khủng hoảng";
            case DECISION_SCHEDULE_PHQ2:   return "Lên lịch gửi PHQ-2 sớm";
            case DECISION_GENTLE_REMINDER: return "Gửi nhắc nhở nhẹ nhàng";
            case DECISION_MARK_PRIORITY:   return "Đánh dấu ưu tiên";
            case DECISION_LOG_ONLY:        return "Chỉ ghi nhật ký";
            default:                       return "Không xác định";
        }
    }

    private static int getLastQ9Value(PhqHistoryManager phq) {
        java.util.List<org.json.JSONObject> history = phq.getPhq9History();
        if (history.isEmpty()) return 0;
        return history.get(history.size() - 1).optInt("q9", 0);
    }

    private static long getDaysSinceLastPhq2(PhqHistoryManager phq) {
        long lastTime = phq.getLastPhq2Time();
        if (lastTime == 0) return 999; // Chưa bao giờ làm
        long diff = System.currentTimeMillis() - lastTime;
        return diff / (24L * 60 * 60 * 1000);
    }
}
