package com.example.mind.common;

import android.content.Context;
import android.content.SharedPreferences;
import android.util.Log;

import com.example.mind.auth.ApiClient;

import org.json.JSONObject;

/**
 * Theo dõi thời gian sử dụng app (session tracking).
 *
 * Gọi start() trong onResume() của mỗi Activity chính.
 * Gọi end() trong onPause().
 * Dữ liệu lưu local, sync lên server khi session kết thúc.
 *
 * Backend cần endpoint: POST /usage/session (chưa có — cần bổ sung)
 */
public class SessionTracker {

    private static final String TAG = "SessionTracker";
    private static final String PREF_NAME = "session_tracker";
    private static final String KEY_SESSION_START = "session_start";
    private static final String KEY_TOTAL_SECONDS_TODAY = "total_seconds_today";
    private static final String KEY_SESSION_COUNT_TODAY = "session_count_today";
    private static final String KEY_LAST_DATE = "last_date";

    private final SharedPreferences prefs;
    private final Context context;

    public SessionTracker(Context context) {
        this.context = context;
        prefs = context.getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE);
    }

    /** Gọi khi app vào foreground (onResume) */
    public void startSession() {
        long now = System.currentTimeMillis();
        prefs.edit().putLong(KEY_SESSION_START, now).apply();
        resetIfNewDay();
        prefs.edit().putInt(KEY_SESSION_COUNT_TODAY,
                getSessionCountToday() + 1).apply();
        Log.d(TAG, "Session started");
    }

    /** Gọi khi app vào background (onPause). Trả về duration giây */
    public long endSession() {
        long start = prefs.getLong(KEY_SESSION_START, 0);
        if (start == 0) return 0;

        long duration = (System.currentTimeMillis() - start) / 1000;
        prefs.edit()
                .putLong(KEY_SESSION_START, 0)
                .putLong(KEY_TOTAL_SECONDS_TODAY,
                        getTotalSecondsToday() + duration)
                .apply();

        Log.d(TAG, "Session ended: " + duration + "s");

        // Sync lên server: POST /usage/session
        if (duration > 5) { // bỏ qua session < 5 giây
            syncSessionToServer(start, duration);
        }

        return duration;
    }

    private void syncSessionToServer(long startMs, long durationSec) {
        try {
            java.time.Instant startInstant = java.time.Instant.ofEpochMilli(startMs);
            java.time.Instant endInstant = java.time.Instant.now();

            JSONObject json = new JSONObject();
            json.put("start_time", startInstant.toString());
            json.put("end_time", endInstant.toString());
            json.put("duration_seconds", durationSec);

            new ApiClient(context).submitUsageSession(json.toString(), new ApiClient.ApiCallback() {
                @Override public void onSuccess(String r) { Log.d(TAG, "Session synced"); }
                @Override public void onFailure(String e) { /* offline ok */ }
            });
        } catch (Exception ignored) {}
    }

    /** Tổng thời gian sử dụng hôm nay (giây) */
    public long getTotalSecondsToday() {
        resetIfNewDay();
        return prefs.getLong(KEY_TOTAL_SECONDS_TODAY, 0);
    }

    /** Số session hôm nay */
    public int getSessionCountToday() {
        resetIfNewDay();
        return prefs.getInt(KEY_SESSION_COUNT_TODAY, 0);
    }

    /** Reset nếu sang ngày mới */
    private void resetIfNewDay() {
        String today = java.time.LocalDate.now().toString();
        String lastDate = prefs.getString(KEY_LAST_DATE, "");
        if (!today.equals(lastDate)) {
            prefs.edit()
                    .putLong(KEY_TOTAL_SECONDS_TODAY, 0)
                    .putInt(KEY_SESSION_COUNT_TODAY, 0)
                    .putString(KEY_LAST_DATE, today)
                    .apply();
        }
    }
}
