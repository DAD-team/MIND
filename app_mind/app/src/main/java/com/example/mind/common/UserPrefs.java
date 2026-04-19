package com.example.mind.common;

import android.content.Context;
import android.content.SharedPreferences;

/**
 * Lưu thông tin user cục bộ (tên, trạng thái onboarding).
 * Dùng chung cho mọi màn hình cần hiển thị tên user.
 */
public class UserPrefs {

    private static final String PREF_NAME = "mind_user";
    private static final String KEY_DISPLAY_NAME = "display_name";
    private static final String KEY_ONBOARDED = "onboarded";
    private static final String KEY_CONSENT_LEVEL = "consent_level";

    // PHQ scoring
    private static final String KEY_LAST_PHQ2_TOTAL = "last_phq2_total";
    private static final String KEY_LAST_PHQ9_TOTAL = "last_phq9_total";
    private static final String KEY_LAST_PHQ_TYPE = "last_phq_type";   // "phq2" or "phq9"
    private static final String KEY_LAST_PHQ_TIME = "last_phq_time";   // epoch millis

    // Monitoring & rejection
    private static final String KEY_MONITORING_LEVEL = "monitoring_level";
    private static final String KEY_REJECTION_COUNT = "rejection_count";
    private static final String KEY_PAUSE_UNTIL = "pause_until";        // epoch millis
    private static final String KEY_PHQ9_DEFER_COUNT = "phq9_defer_count";
    private static final String KEY_NEXT_PHQ2_DAYS = "next_phq2_days";  // 7 or 14

    private final SharedPreferences prefs;

    public UserPrefs(Context context) {
        prefs = context.getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE);
    }

    /** Lưu tên hiển thị */
    public void setDisplayName(String name) {
        prefs.edit().putString(KEY_DISPLAY_NAME, name).apply();
    }

    /** Lấy tên hiển thị (default "Bạn") */
    public String getDisplayName() {
        return prefs.getString(KEY_DISPLAY_NAME, "Bạn");
    }

    /** Đánh dấu đã hoàn thành onboarding */
    public void setOnboarded(boolean done) {
        prefs.edit().putBoolean(KEY_ONBOARDED, done).apply();
    }

    /** Kiểm tra đã onboarding chưa */
    public boolean isOnboarded() {
        return prefs.getBoolean(KEY_ONBOARDED, false);
    }

    /** Lưu mức đồng thuận (1, 2 hoặc 3) */
    public void setConsentLevel(int level) {
        prefs.edit().putInt(KEY_CONSENT_LEVEL, level).apply();
    }

    /** Lấy mức đồng thuận (default 0 = chưa chọn) */
    public int getConsentLevel() {
        return prefs.getInt(KEY_CONSENT_LEVEL, 0);
    }

    // ─── PHQ Score ───

    /** Lưu kết quả PHQ-2 */
    public void savePhq2Result(int total) {
        prefs.edit()
                .putInt(KEY_LAST_PHQ2_TOTAL, total)
                .putString(KEY_LAST_PHQ_TYPE, "phq2")
                .putLong(KEY_LAST_PHQ_TIME, System.currentTimeMillis())
                .apply();
    }

    /** Lưu kết quả PHQ-9 */
    public void savePhq9Result(int total) {
        prefs.edit()
                .putInt(KEY_LAST_PHQ9_TOTAL, total)
                .putString(KEY_LAST_PHQ_TYPE, "phq9")
                .putLong(KEY_LAST_PHQ_TIME, System.currentTimeMillis())
                .apply();
    }

    /** Điểm PHQ-2 lần cuối (-1 = chưa làm) */
    public int getLastPhq2Total() {
        return prefs.getInt(KEY_LAST_PHQ2_TOTAL, -1);
    }

    /** Điểm PHQ-9 lần cuối (-1 = chưa làm) */
    public int getLastPhq9Total() {
        return prefs.getInt(KEY_LAST_PHQ9_TOTAL, -1);
    }

    /** Loại PHQ lần cuối ("phq2" / "phq9" / null) */
    public String getLastPhqType() {
        return prefs.getString(KEY_LAST_PHQ_TYPE, null);
    }

    /** Thời gian làm PHQ lần cuối (epoch ms, 0 = chưa) */
    public long getLastPhqTime() {
        return prefs.getLong(KEY_LAST_PHQ_TIME, 0);
    }

    /**
     * Quyết định lần tới nên cho làm PHQ-2 hay PHQ-9.
     *
     * Quy tắc:
     * - Lần đầu (chưa làm bao giờ) → PHQ-2
     * - PHQ-2 lần trước < 3 điểm → PHQ-2 (nguy cơ thấp, sàng lọc nhanh)
     * - PHQ-2 lần trước >= 3 điểm → PHQ-9 (cần đánh giá sâu)
     * - PHQ-9 lần trước < 5 điểm (minimal) → PHQ-2 (đã ổn, quay lại sàng lọc)
     * - PHQ-9 lần trước >= 5 điểm → PHQ-9 (vẫn cần theo dõi sâu)
     */
    public boolean shouldDoPhq9() {
        String lastType = getLastPhqType();
        if (lastType == null) return false; // lần đầu → PHQ-2

        if ("phq2".equals(lastType)) {
            return getLastPhq2Total() >= 3;
        } else {
            // Đã làm PHQ-9 trước đó
            return getLastPhq9Total() >= 5;
        }
    }

    // ─── Monitoring Level ───

    /** Lưu mức theo dõi (1-5) */
    public void setMonitoringLevel(int level) {
        prefs.edit().putInt(KEY_MONITORING_LEVEL, level).apply();
    }

    /** Lấy mức theo dõi (default 1 = Tiêu chuẩn) */
    public int getMonitoringLevel() {
        return prefs.getInt(KEY_MONITORING_LEVEL, 1);
    }

    // ─── Rejection Handling ───

    /** Tăng bộ đếm từ chối */
    public void incrementRejectionCount() {
        int count = getRejectionCount() + 1;
        prefs.edit().putInt(KEY_REJECTION_COUNT, count).apply();
        if (count >= 3) {
            // Tạm dừng 30 ngày
            long pauseUntil = System.currentTimeMillis() + 30L * 24 * 60 * 60 * 1000;
            prefs.edit().putLong(KEY_PAUSE_UNTIL, pauseUntil).apply();
        }
    }

    /** Reset bộ đếm từ chối (khi hoàn thành bất kỳ bảng hỏi) */
    public void resetRejectionCount() {
        prefs.edit()
                .putInt(KEY_REJECTION_COUNT, 0)
                .putLong(KEY_PAUSE_UNTIL, 0)
                .apply();
    }

    public int getRejectionCount() {
        return prefs.getInt(KEY_REJECTION_COUNT, 0);
    }

    /** Kiểm tra đang bị tạm dừng không */
    public boolean isPaused() {
        long pauseUntil = prefs.getLong(KEY_PAUSE_UNTIL, 0);
        return pauseUntil > System.currentTimeMillis();
    }

    // ─── PHQ-9 Defer ("Để sau") ───

    /** Tăng số lần bấm "Để sau" PHQ-9 */
    public void incrementPhq9DeferCount() {
        int count = getPhq9DeferCount() + 1;
        prefs.edit().putInt(KEY_PHQ9_DEFER_COUNT, count).apply();
    }

    /** Reset bộ đếm "Để sau" */
    public void resetPhq9DeferCount() {
        prefs.edit().putInt(KEY_PHQ9_DEFER_COUNT, 0).apply();
    }

    public int getPhq9DeferCount() {
        return prefs.getInt(KEY_PHQ9_DEFER_COUNT, 0);
    }

    // ─── PHQ-2 Next Interval ───

    /** Đặt khoảng cách PHQ-2 tiếp theo (7 hoặc 14 ngày) */
    public void setNextPhq2Days(int days) {
        prefs.edit().putInt(KEY_NEXT_PHQ2_DAYS, days).apply();
    }

    /** Khoảng cách PHQ-2 tiếp theo (default 14) */
    public int getNextPhq2Days() {
        return prefs.getInt(KEY_NEXT_PHQ2_DAYS, 14);
    }

    /** Xóa tất cả khi logout */
    public void clear() {
        prefs.edit().clear().apply();
    }
}
