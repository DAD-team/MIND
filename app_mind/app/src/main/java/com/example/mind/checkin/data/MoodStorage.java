package com.example.mind.checkin.data;

import android.content.Context;
import android.content.SharedPreferences;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/**
 * Lưu/đọc lịch sử check-in cảm xúc.
 * Dữ liệu giữ 90 ngày, tự động xóa.
 */
public class MoodStorage {

    private static final String PREF_NAME = "mood_storage";
    private static final String KEY_MOODS = "mood_history";
    private static final String KEY_LAST_INTERACTION = "last_interaction_time";
    private static final long NINETY_DAYS_MS = 90L * 24 * 60 * 60 * 1000;

    // Mood ID → Thang 1-5
    private static final int[] MOOD_TO_SCORE = {0, 5, 2, 2, 5, 3, 2};
    // Index:                                   0  1  2  3  4  5  6
    // Mood:                                    -  Vui Buồn Stress Hào hứng Bình thường Mệt

    private final SharedPreferences prefs;

    public MoodStorage(Context context) {
        prefs = context.getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE);
    }

    /** Lưu 1 lần check-in cảm xúc */
    public void saveMood(int moodLevel, boolean hasVideo) {
        long now = System.currentTimeMillis();
        JSONObject entry = new JSONObject();
        try {
            entry.put("timestamp", now);
            entry.put("mood", moodLevel);
            entry.put("score", getMoodScore(moodLevel));
            entry.put("hasVideo", hasVideo);
        } catch (Exception ignored) {}

        appendMood(entry);
        updateLastInteraction(now);
    }

    /** Thang tâm trạng 1-5 từ mood ID */
    public static int getMoodScore(int moodLevel) {
        if (moodLevel >= 1 && moodLevel <= 6) return MOOD_TO_SCORE[moodLevel];
        return 3;
    }

    /** Cập nhật thời điểm tương tác gần nhất */
    public void updateLastInteraction(long timestamp) {
        prefs.edit().putLong(KEY_LAST_INTERACTION, timestamp).apply();
    }

    /** Thời điểm tương tác gần nhất (epoch ms) */
    public long getLastInteractionTime() {
        return prefs.getLong(KEY_LAST_INTERACTION, System.currentTimeMillis());
    }

    /** Khoảng im lặng tính bằng giờ */
    public double getSilenceHours() {
        long last = getLastInteractionTime();
        long now = System.currentTimeMillis();
        return (now - last) / (1000.0 * 60 * 60);
    }

    /** Số lần check-in trong N ngày gần nhất */
    public int getCheckinCount(int days) {
        long cutoff = System.currentTimeMillis() - (days * 24L * 60 * 60 * 1000);
        List<JSONObject> moods = loadMoods();
        int count = 0;
        for (JSONObject m : moods) {
            if (m.optLong("timestamp", 0) > cutoff) count++;
        }
        return count;
    }

    /** Tỷ lệ tương tác: 7 ngày gần / 7 ngày trước */
    public double getInteractionRatio() {
        int recent = getCheckinCount(7);
        int previous = getCheckinCountBetween(8, 14);
        if (previous == 0) return (recent > 0) ? 1.0 : 0.0;
        return (double) recent / previous;
    }

    /** Đếm check-in từ ngày X đến ngày Y trước */
    private int getCheckinCountBetween(int fromDaysAgo, int toDaysAgo) {
        long now = System.currentTimeMillis();
        long start = now - (toDaysAgo * 24L * 60 * 60 * 1000);
        long end = now - (fromDaysAgo * 24L * 60 * 60 * 1000);
        List<JSONObject> moods = loadMoods();
        int count = 0;
        for (JSONObject m : moods) {
            long t = m.optLong("timestamp", 0);
            if (t >= start && t <= end) count++;
        }
        return count;
    }

    // Video metrics (tín hiệu 5-6) lưu trên server, không lưu local.
    // ScoutWorker lấy từ GET /monitoring/status khi chạy chu kỳ.

    // ─── Internal ───

    private void appendMood(JSONObject entry) {
        try {
            String raw = prefs.getString(KEY_MOODS, "[]");
            JSONArray arr = new JSONArray(raw);
            arr.put(entry);

            // Cleanup > 90 days
            long cutoff = System.currentTimeMillis() - NINETY_DAYS_MS;
            JSONArray cleaned = new JSONArray();
            for (int i = 0; i < arr.length(); i++) {
                JSONObject obj = arr.getJSONObject(i);
                if (obj.optLong("timestamp", 0) > cutoff) {
                    cleaned.put(obj);
                }
            }
            prefs.edit().putString(KEY_MOODS, cleaned.toString()).apply();
        } catch (Exception ignored) {}
    }

    private List<JSONObject> loadMoods() {
        List<JSONObject> result = new ArrayList<>();
        try {
            String raw = prefs.getString(KEY_MOODS, "[]");
            JSONArray arr = new JSONArray(raw);
            for (int i = 0; i < arr.length(); i++) {
                result.add(arr.getJSONObject(i));
            }
        } catch (Exception ignored) {}
        return result;
    }
}
