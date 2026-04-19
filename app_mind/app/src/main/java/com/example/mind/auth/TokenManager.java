package com.example.mind.auth;

import android.content.Context;
import android.content.SharedPreferences;

import com.google.firebase.auth.FirebaseAuth;
import com.google.firebase.auth.FirebaseUser;

/**
 * Quản lý Firebase ID Token.
 * - Lưu/đọc token từ SharedPreferences
 * - Refresh token trước mỗi request (token hết hạn sau 1h)
 */
public class TokenManager {

    private static final String PREF_NAME = "mind_auth";
    private static final String KEY_TOKEN = "firebase_token";

    private final SharedPreferences prefs;

    public TokenManager(Context context) {
        prefs = context.getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE);
    }

    /** Lưu token vào SharedPreferences */
    public void saveToken(String token) {
        prefs.edit().putString(KEY_TOKEN, token).apply();
    }

    /** Đọc token đã lưu (có thể null nếu chưa login) */
    public String getSavedToken() {
        return prefs.getString(KEY_TOKEN, null);
    }

    /** Xóa token khi logout */
    public void clearToken() {
        prefs.edit().remove(KEY_TOKEN).apply();
    }

    /**
     * Lấy token mới nhất (force refresh).
     * Gọi callback khi có token hoặc lỗi.
     */
    public void getValidToken(TokenCallback callback) {
        FirebaseUser user = FirebaseAuth.getInstance().getCurrentUser();
        if (user == null) {
            callback.onError("User not logged in");
            return;
        }

        user.getIdToken(true).addOnSuccessListener(result -> {
            String token = result.getToken();
            saveToken(token);
            callback.onToken(token);
        }).addOnFailureListener(e -> {
            callback.onError(e.getMessage());
        });
    }

    public interface TokenCallback {
        void onToken(String token);
        void onError(String error);
    }
}
