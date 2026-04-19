package com.example.mind.notification;

import android.content.Context;
import android.util.Log;

import com.example.mind.auth.ApiClient;
import com.example.mind.common.UserPrefs;
import com.google.firebase.messaging.FirebaseMessaging;

/**
 * Helper đăng ký / hủy FCM token với backend.
 * Gọi sau khi login thành công hoặc khi FCM cấp token mới.
 */
public class NotificationHelper {

    private static final String TAG = "NotificationHelper";

    /**
     * Lấy FCM token → gửi lên backend qua PUT /notifications/fcm-token.
     * Gọi sau khi login hoặc khi token refresh.
     */
    public static void registerFcmToken(Context context) {
        FirebaseMessaging.getInstance().getToken()
                .addOnSuccessListener(fcmToken -> {
                    Log.d(TAG, "FCM token: " + fcmToken.substring(0, 20) + "...");

                    UserPrefs prefs = new UserPrefs(context);
                    int consentLevel = prefs.getConsentLevel();

                    ApiClient api = new ApiClient(context);
                    api.registerFcmToken(fcmToken, consentLevel, new ApiClient.ApiCallback() {
                        @Override
                        public void onSuccess(String responseBody) {
                            Log.d(TAG, "FCM token registered successfully");
                        }

                        @Override
                        public void onFailure(String error) {
                            Log.e(TAG, "FCM token registration failed: " + error);
                        }
                    });
                })
                .addOnFailureListener(e -> {
                    Log.e(TAG, "Failed to get FCM token", e);
                });
    }

    /**
     * Xóa FCM token trên backend khi logout.
     * Gọi TRƯỚC khi Firebase signOut.
     */
    public static void unregisterFcmToken(Context context, Runnable onComplete) {
        ApiClient api = new ApiClient(context);
        api.unregisterFcmToken(new ApiClient.ApiCallback() {
            @Override
            public void onSuccess(String responseBody) {
                Log.d(TAG, "FCM token removed");
                if (onComplete != null) onComplete.run();
            }

            @Override
            public void onFailure(String error) {
                Log.e(TAG, "FCM token removal failed: " + error);
                if (onComplete != null) onComplete.run();
            }
        });
    }
}
