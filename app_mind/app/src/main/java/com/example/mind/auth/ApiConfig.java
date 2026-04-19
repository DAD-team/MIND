package com.example.mind.auth;

import android.util.Log;

import androidx.annotation.NonNull;

import com.google.firebase.database.DataSnapshot;
import com.google.firebase.database.DatabaseError;
import com.google.firebase.database.DatabaseReference;
import com.google.firebase.database.FirebaseDatabase;
import com.google.firebase.database.ValueEventListener;

/**
 * Đọc Base URL động từ Firebase Realtime Database.
 *
 * Backend dùng Cloudflare Tunnel → URL đổi mỗi lần restart.
 * Backend mỗi lần start sẽ ghi URL mới vào RTDB tại: server_info/tunnel_url
 *
 * Flow:
 *   Backend restart → ghi URL mới vào RTDB
 *     → Realtime listener trên app bắt thay đổi
 *     → Cập nhật cachedUrl
 *     → Tất cả API call tiếp theo dùng URL mới
 *
 * App KHÔNG cần restart khi backend restart.
 */
public class ApiConfig {

    private static final String TAG = "ApiConfig";
    private static final String RTDB_PATH = "server_info/tunnel_url";

    // Firebase RTDB URL — project hackathon-493013, region asia-southeast1
    private static final String RTDB_URL = "https://hackathon-493013-default-rtdb.asia-southeast1.firebasedatabase.app/";

    // Fallback khi RTDB chưa có gì (lần đầu chạy, chưa start backend)
    private static final String FALLBACK_URL = "https://sewing-coupons-intense-lab.trycloudflare.com";

    private static volatile String cachedUrl = null;
    private static ValueEventListener realtimeListener = null;
    private static DatabaseReference listenerRef = null;

    public interface Callback {
        void onReady(String baseUrl);
        void onError(String error);
    }

    /**
     * Lấy base URL hiện tại.
     * Lần đầu: đọc RTDB + attach realtime listener.
     * Các lần sau: trả cache ngay (realtime listener tự cập nhật khi URL đổi).
     */
    public static void getBaseUrl(Callback callback) {
        // Đã có URL → trả ngay
        if (cachedUrl != null) {
            callback.onReady(cachedUrl);
            attachRealtimeListenerOnce();
            return;
        }

        // Chưa có → đọc 1 lần từ RTDB
        DatabaseReference ref = FirebaseDatabase.getInstance(RTDB_URL).getReference(RTDB_PATH);

        ref.addListenerForSingleValueEvent(new ValueEventListener() {
            @Override
            public void onDataChange(@NonNull DataSnapshot snapshot) {
                String url = snapshot.getValue(String.class);
                if (url != null && !url.isEmpty()) {
                    cachedUrl = url;
                    Log.d(TAG, "URL from RTDB: " + url);
                } else {
                    cachedUrl = FALLBACK_URL;
                    Log.w(TAG, "RTDB empty → fallback: " + FALLBACK_URL);
                }
                callback.onReady(cachedUrl);
                attachRealtimeListenerOnce();
            }

            @Override
            public void onCancelled(@NonNull DatabaseError error) {
                cachedUrl = FALLBACK_URL;
                Log.w(TAG, "RTDB error → fallback: " + error.getMessage());
                callback.onReady(cachedUrl);
                attachRealtimeListenerOnce();
            }
        });
    }

    /**
     * Realtime listener — TỰ ĐỘNG cập nhật cachedUrl khi backend restart.
     *
     * Backend restart → ghi URL mới vào RTDB
     *   → onDataChange fire
     *   → cachedUrl = URL mới
     *   → API call tiếp theo tự dùng URL mới
     *
     * Chỉ attach 1 lần duy nhất (singleton pattern).
     */
    private static synchronized void attachRealtimeListenerOnce() {
        if (realtimeListener != null) return; // đã attach rồi

        listenerRef = FirebaseDatabase.getInstance(RTDB_URL).getReference(RTDB_PATH);

        realtimeListener = new ValueEventListener() {
            @Override
            public void onDataChange(@NonNull DataSnapshot snapshot) {
                String url = snapshot.getValue(String.class);
                if (url != null && !url.isEmpty()) {
                    if (!url.equals(cachedUrl)) {
                        String oldUrl = cachedUrl;
                        cachedUrl = url;
                        Log.i(TAG, "URL updated: " + oldUrl + " → " + url);
                    }
                }
            }

            @Override
            public void onCancelled(@NonNull DatabaseError error) {
                Log.w(TAG, "Listener cancelled: " + error.getMessage());
            }
        };

        listenerRef.addValueEventListener(realtimeListener);
        Log.d(TAG, "Realtime listener attached");
    }

    /**
     * Force re-read từ RTDB.
     * Gọi khi gặp nhiều lỗi liên tiếp (server có thể đã đổi URL
     * nhưng listener chưa kịp fire).
     */
    public static void invalidateCache() {
        cachedUrl = null;
        // KHÔNG remove listener — nó sẽ tự cập nhật khi có data mới
        Log.d(TAG, "Cache invalidated, next call will re-read RTDB");
    }

    /** Detach listener khi app bị destroy (tránh leak). Gọi từ Application.onTerminate() nếu cần */
    public static synchronized void detach() {
        if (realtimeListener != null && listenerRef != null) {
            listenerRef.removeEventListener(realtimeListener);
            realtimeListener = null;
            listenerRef = null;
            Log.d(TAG, "Listener detached");
        }
        cachedUrl = null;
    }

    /** URL hiện tại (không async, dùng cho logging) */
    public static String getCurrentUrl() {
        return cachedUrl != null ? cachedUrl : FALLBACK_URL;
    }
}
