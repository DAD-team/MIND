package com.example.mind.notification;

import android.app.NotificationChannel;
import android.app.PendingIntent;
import android.content.Intent;
import android.os.Build;
import android.util.Log;

import androidx.annotation.NonNull;
import androidx.core.app.NotificationCompat;
import androidx.core.app.NotificationManagerCompat;

import com.example.mind.MainActivity;
import com.example.mind.R;
import com.example.mind.checkin.ui.Phq2Activity;
import com.example.mind.checkin.ui.Phq9Activity;
import com.google.firebase.messaging.FirebaseMessagingService;
import com.google.firebase.messaging.RemoteMessage;

import java.util.Map;

/**
 * Service nhận push notification từ FCM (Firebase Cloud Messaging).
 * Backend gửi notification qua FCM khi:
 * - Scout phát hiện rủi ro cao → nhắc check-in
 * - Đến hạn PHQ-2 theo lịch
 * - Nhắc lại PHQ-9 sau 4h defer
 * - Phản hồi khích lệ khi cải thiện
 */
public class MindyMessagingService extends FirebaseMessagingService {

    private static final String TAG = "MindyFCM";
    private static final String CHANNEL_ID = "mindy_notifications";
    private static final int NOTIFICATION_ID = 1001;

    @Override
    public void onMessageReceived(@NonNull RemoteMessage remoteMessage) {
        super.onMessageReceived(remoteMessage);
        Log.d(TAG, "Message received from: " + remoteMessage.getFrom());

        String title = null;
        String body = null;

        // Notification payload (hiển thị tự động khi app background)
        if (remoteMessage.getNotification() != null) {
            title = remoteMessage.getNotification().getTitle();
            body = remoteMessage.getNotification().getBody();
        }

        // Data payload (custom handling)
        String action = null;
        if (remoteMessage.getData().size() > 0) {
            action = remoteMessage.getData().get("action");
            Log.d(TAG, "Action: " + action);
        }

        if (title != null && body != null) {
            showNotification(title, body, action, remoteMessage.getData());
        }
    }

    @Override
    public void onNewToken(@NonNull String token) {
        super.onNewToken(token);
        Log.d(TAG, "New FCM token: " + token);
        // Tự động đăng ký token mới lên backend
        NotificationHelper.registerFcmToken(this);
    }

    private void showNotification(String title, String body, String action, Map<String, String> data) {
        createNotificationChannel();

        // Route đến đúng Activity theo action từ server
        Intent intent;
        if ("open_phq2".equals(action)) {
            intent = new Intent(this, Phq2Activity.class);
            intent.putExtra("source", "notification");
        } else if ("open_phq9".equals(action)) {
            intent = new Intent(this, Phq9Activity.class);
            intent.putExtra("source", "notification");
            // Truyền điểm PHQ-2 nếu có (chuyển tiếp từ PHQ-2 → PHQ-9)
            if (data.containsKey("phq2_score1")) {
                intent.putExtra("phq2_score1", Integer.parseInt(data.get("phq2_score1")));
                intent.putExtra("phq2_score2", Integer.parseInt(data.get("phq2_score2")));
            }
        } else {
            // Mặc định: mở MainActivity (open_checkin, none, hoặc không có action)
            intent = new Intent(this, MainActivity.class);
            if ("open_checkin".equals(action)) {
                intent.putExtra("action", "open_checkin");
            }
        }
        intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);

        PendingIntent pendingIntent = PendingIntent.getActivity(
                this, 0, intent,
                PendingIntent.FLAG_ONE_SHOT | PendingIntent.FLAG_IMMUTABLE);

        NotificationCompat.Builder builder = new NotificationCompat.Builder(this, CHANNEL_ID)
                .setSmallIcon(R.drawable.icon_40) // dùng icon có sẵn
                .setContentTitle(title)
                .setContentText(body)
                .setAutoCancel(true)
                .setPriority(NotificationCompat.PRIORITY_HIGH)
                .setContentIntent(pendingIntent);

        NotificationManagerCompat manager = NotificationManagerCompat.from(this);
        try {
            manager.notify(NOTIFICATION_ID, builder.build());
        } catch (SecurityException e) {
            Log.e(TAG, "Notification permission not granted", e);
        }
    }

    private void createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            NotificationChannel channel = new NotificationChannel(
                    CHANNEL_ID,
                    "Mindy Notifications",
                    android.app.NotificationManager.IMPORTANCE_HIGH);
            channel.setDescription("Nhắc nhở check-in cảm xúc từ Mindy");

            android.app.NotificationManager manager =
                    getSystemService(android.app.NotificationManager.class);
            if (manager != null) {
                manager.createNotificationChannel(channel);
            }
        }
    }
}
