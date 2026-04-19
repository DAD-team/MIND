package com.example.mind;

import android.app.Activity;
import android.app.Application;
import android.os.Bundle;
import android.util.Log;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.example.mind.checkin.data.MoodStorage;
import com.example.mind.common.SessionTracker;

/**
 * Application class — track session toàn app.
 *
 * Khi bất kỳ Activity nào vào foreground/background,
 * tự động gửi sự kiện lên server để server tính khoảng im lặng (tín hiệu 1).
 * Server Scout dùng thời điểm tắt app gần nhất để tính silence_hours.
 */
public class MindApp extends Application {

    private static final String TAG = "MindApp";

    private int activityCount = 0;
    private SessionTracker sessionTracker;

    @Override
    public void onCreate() {
        super.onCreate();
        sessionTracker = new SessionTracker(this);

        registerActivityLifecycleCallbacks(new ActivityLifecycleCallbacks() {
            @Override
            public void onActivityStarted(@NonNull Activity activity) {
                if (activityCount == 0) {
                    // App vừa vào foreground (mở app hoặc quay lại từ background)
                    Log.d(TAG, "App foreground");
                    sessionTracker.startSession();
                    new MoodStorage(MindApp.this)
                            .updateLastInteraction(System.currentTimeMillis());
                }
                activityCount++;
            }

            @Override
            public void onActivityStopped(@NonNull Activity activity) {
                activityCount--;
                if (activityCount == 0) {
                    // App vừa vào background (tắt app hoặc chuyển sang app khác)
                    Log.d(TAG, "App background");
                    sessionTracker.endSession();
                }
            }

            // Các callback không cần xử lý
            @Override public void onActivityCreated(@NonNull Activity a, @Nullable Bundle b) {}
            @Override public void onActivityResumed(@NonNull Activity a) {}
            @Override public void onActivityPaused(@NonNull Activity a) {}
            @Override public void onActivitySaveInstanceState(@NonNull Activity a, @NonNull Bundle b) {}
            @Override public void onActivityDestroyed(@NonNull Activity a) {}
        });
    }
}
