package com.example.mind.checkin.data;

import android.content.Context;
import android.content.SharedPreferences;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/**
 * Lưu/đọc lịch sử PHQ-2 và PHQ-9 (nhiều lần, không chỉ lần cuối).
 * Dữ liệu giữ 1 năm cho phân tích xu hướng.
 */
public class PhqHistoryManager {

    private static final String PREF_NAME = "phq_history";
    private static final String KEY_PHQ2_HISTORY = "phq2_history";
    private static final String KEY_PHQ9_HISTORY = "phq9_history";
    private static final String KEY_SAFETY_EVENTS = "safety_events";
    private static final long ONE_YEAR_MS = 365L * 24 * 60 * 60 * 1000;

    private final SharedPreferences prefs;

    public PhqHistoryManager(Context context) {
        prefs = context.getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE);
    }

    // ─── PHQ-2 History ───

    public void savePhq2Result(int score1, int score2, int total, String source) {
        JSONObject entry = new JSONObject();
        try {
            entry.put("timestamp", System.currentTimeMillis());
            entry.put("score1", score1);
            entry.put("score2", score2);
            entry.put("total", total);
            entry.put("source", source); // "scheduled", "scout", "self_request"
        } catch (Exception ignored) {}
        appendToArray(KEY_PHQ2_HISTORY, entry);
    }

    public List<JSONObject> getPhq2History() {
        return loadArray(KEY_PHQ2_HISTORY);
    }

    /** Lấy 3 lần PHQ-2 gần nhất (cho tính xu hướng) */
    public int[] getLastThreePhq2Scores() {
        List<JSONObject> history = getPhq2History();
        int size = history.size();
        if (size < 2) return null; // cần ít nhất 2 lần

        int count = Math.min(3, size);
        int[] scores = new int[count];
        for (int i = 0; i < count; i++) {
            scores[i] = history.get(size - count + i).optInt("total", 0);
        }
        return scores;
    }

    /** Tính độ dốc PHQ-2: (S3 - S1) / 2 */
    public double getPhq2Slope() {
        int[] scores = getLastThreePhq2Scores();
        if (scores == null || scores.length < 2) return 0;
        if (scores.length == 2) return (scores[1] - scores[0]);
        return (scores[2] - scores[0]) / 2.0;
    }

    // ─── PHQ-9 History ───

    public void savePhq9Result(int total, int somaticScore, int cognitiveScore,
                                int functionalImpact, int q9Value,
                                int[] questionScores, String trigger) {
        JSONObject entry = new JSONObject();
        try {
            entry.put("timestamp", System.currentTimeMillis());
            entry.put("total", total);
            entry.put("somatic", somaticScore);
            entry.put("cognitive", cognitiveScore);
            entry.put("functional", functionalImpact);
            entry.put("q9", q9Value);
            entry.put("trigger", trigger); // "phq2_escalation", "follow_up", "self_request"

            JSONArray scoresArr = new JSONArray();
            for (int s : questionScores) scoresArr.put(s);
            entry.put("scores", scoresArr);
        } catch (Exception ignored) {}
        appendToArray(KEY_PHQ9_HISTORY, entry);
    }

    public List<JSONObject> getPhq9History() {
        return loadArray(KEY_PHQ9_HISTORY);
    }

    /** Điểm PHQ-9 lần cuối (-1 nếu chưa có) */
    public int getLastPhq9Score() {
        List<JSONObject> history = getPhq9History();
        if (history.isEmpty()) return -1;
        return history.get(history.size() - 1).optInt("total", -1);
    }

    /** Điểm PHQ-9 lần trước đó (-1 nếu chưa có) */
    public int getPreviousPhq9Score() {
        List<JSONObject> history = getPhq9History();
        if (history.size() < 2) return -1;
        return history.get(history.size() - 2).optInt("total", -1);
    }

    /** Kiểm tra thay đổi đáng kể (>= 5 điểm) */
    public int getPhq9Change() {
        int current = getLastPhq9Score();
        int previous = getPreviousPhq9Score();
        if (current < 0 || previous < 0) return 0;
        return current - previous;
    }

    /** Kiểm tra xuống thang: 2 lần PHQ-9 liên tiếp < 5 */
    public boolean shouldDeescalate() {
        List<JSONObject> history = getPhq9History();
        if (history.size() < 2) return false;
        int last = history.get(history.size() - 1).optInt("total", 99);
        int prev = history.get(history.size() - 2).optInt("total", 99);
        return last < 5 && prev < 5;
    }

    // ─── Safety Events ───

    public void saveSafetyEvent(int q9Value, int phq9Total) {
        JSONObject entry = new JSONObject();
        try {
            entry.put("timestamp", System.currentTimeMillis());
            entry.put("q9_value", q9Value);
            entry.put("phq9_total", phq9Total);
            String action;
            if (q9Value >= 3) action = "crisis_immediate_contact";
            else if (q9Value == 2) action = "crisis_same_day";
            else action = "active_within_24h";
            entry.put("action", action);
        } catch (Exception ignored) {}
        // Safety events: append without cleanup (never auto-delete)
        appendToArray(KEY_SAFETY_EVENTS, entry);
    }

    public List<JSONObject> getSafetyEvents() {
        return loadArray(KEY_SAFETY_EVENTS);
    }

    // ─── Timestamps for cooldown ───

    public long getLastPhq2Time() {
        List<JSONObject> history = getPhq2History();
        if (history.isEmpty()) return 0;
        return history.get(history.size() - 1).optLong("timestamp", 0);
    }

    public long getLastPhq9Time() {
        List<JSONObject> history = getPhq9History();
        if (history.isEmpty()) return 0;
        return history.get(history.size() - 1).optLong("timestamp", 0);
    }

    // ─── Internal helpers ───

    private void appendToArray(String key, JSONObject entry) {
        try {
            String raw = prefs.getString(key, "[]");
            JSONArray arr = new JSONArray(raw);
            arr.put(entry);

            // Cleanup: remove entries older than 1 year (except safety events)
            if (!KEY_SAFETY_EVENTS.equals(key)) {
                long cutoff = System.currentTimeMillis() - ONE_YEAR_MS;
                JSONArray cleaned = new JSONArray();
                for (int i = 0; i < arr.length(); i++) {
                    JSONObject obj = arr.getJSONObject(i);
                    if (obj.optLong("timestamp", 0) > cutoff) {
                        cleaned.put(obj);
                    }
                }
                arr = cleaned;
            }

            prefs.edit().putString(key, arr.toString()).apply();
        } catch (Exception ignored) {}
    }

    private List<JSONObject> loadArray(String key) {
        List<JSONObject> result = new ArrayList<>();
        try {
            String raw = prefs.getString(key, "[]");
            JSONArray arr = new JSONArray(raw);
            for (int i = 0; i < arr.length(); i++) {
                result.add(arr.getJSONObject(i));
            }
        } catch (Exception ignored) {}
        return result;
    }
}
