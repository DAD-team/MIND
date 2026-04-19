package com.example.mind;

import android.content.Intent;
import android.os.Bundle;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;

import com.example.mind.auth.ApiClient;
import com.example.mind.chat.ChatActivity;
import com.example.mind.checkin.data.MoodStorage;
import com.example.mind.checkin.data.ReasonMapper;
import com.example.mind.checkin.ui.CheckinDialog;
import com.example.mind.checkin.ui.Phq2Activity;
import com.example.mind.checkin.ui.Phq9Activity;
import com.example.mind.common.UserPrefs;
import com.example.mind.journal.JournalActivity;
import com.example.mind.notification.NotificationHelper;
import com.example.mind.settings.ui.ProfileActivity;
import com.example.mind.vr.PhotoCaptureActivity;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.Calendar;

public class MainActivity extends AppCompatActivity {

    /** Tránh mở PHQ liên tiếp nếu onResume bị gọi nhiều lần trong cùng session. */
    private boolean phqDialogOpened = false;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        UserPrefs userPrefs = new UserPrefs(this);
        String name = userPrefs.getDisplayName();

        // Lời chào động theo giờ
        String greeting = getDynamicGreeting();
        ((TextView) findViewById(R.id.tvGreeting)).setText(greeting + ", " + name + "!");

        setupCardListeners();
        setupNavBar();

        // Đăng ký FCM token + xin quyền notification
        NotificationHelper.registerFcmToken(this);

        // Sync monitoring level từ server (không còn đọc pending_action ở đây —
        // pending PHQ giờ lấy từ /phq/pending, xem checkPhqPending())
        syncMonitoringStatus(userPrefs);

        // Check xem có action từ notification không (user bấm vào notification mở app)
        handleIntentAction(getIntent());

        // Cập nhật tương tác (user mở app = interaction)
        new MoodStorage(this).updateLastInteraction(System.currentTimeMillis());
    }

    @Override
    protected void onResume() {
        super.onResume();
        // Mỗi lần app vào foreground → check pending PHQ từ backend.
        phqDialogOpened = false;
        checkPhqPending();
    }

    /**
     * GET /phq/pending → nếu có pending thì mở PHQ tương ứng.
     * PHQ-9 ưu tiên hơn PHQ-2 (nghiêm trọng hơn).
     */
    private void checkPhqPending() {
        new ApiClient(this).getPhqPending(new ApiClient.ApiCallback() {
            @Override
            public void onSuccess(String responseBody) {
                runOnUiThread(() -> routePendingPhq(responseBody));
            }

            @Override
            public void onFailure(String error) {
                // Offline — bỏ qua, sẽ check lại ở lần resume sau
            }
        });
    }

    private void routePendingPhq(String responseBody) {
        if (phqDialogOpened) return;
        try {
            JSONObject json = new JSONObject(responseBody);
            boolean hasPhq9 = json.optBoolean("has_phq9", false);
            boolean hasPhq2 = json.optBoolean("has_phq2", false);
            if (!hasPhq9 && !hasPhq2) return;

            JSONArray pending = json.optJSONArray("pending");
            if (pending == null) return;

            String targetType = hasPhq9 ? "phq9" : "phq2";
            String pendingId = null;
            String reason = null;
            for (int i = 0; i < pending.length(); i++) {
                JSONObject item = pending.optJSONObject(i);
                if (item == null) continue;
                if (targetType.equals(item.optString("phq_type"))) {
                    pendingId = item.optString("id", null);
                    reason = item.optString("reason", null);
                    break;
                }
            }

            phqDialogOpened = true;
            Class<?> target = hasPhq9 ? Phq9Activity.class : Phq2Activity.class;
            Intent intent = new Intent(MainActivity.this, target)
                    .putExtra("pending_id", pendingId)
                    .putExtra("pending_reason", reason)
                    .putExtra("source", ReasonMapper.mapReasonToSource(reason));
            startActivity(intent);
        } catch (Exception ignored) {}
    }

    /** Lấy monitoring status từ server → cập nhật local. */
    private void syncMonitoringStatus(UserPrefs prefs) {
        new ApiClient(this).getMonitoringStatus(new ApiClient.ApiCallback() {
            @Override
            public void onSuccess(String responseBody) {
                try {
                    JSONObject json = new JSONObject(responseBody);
                    int level = json.optInt("level", 1);
                    prefs.setMonitoringLevel(level);
                } catch (Exception ignored) {}
            }

            @Override
            public void onFailure(String error) {
                // Offline — dùng local data
            }
        });
    }

    /** Xử lý action từ notification khi user bấm vào notification mở app */
    private void handleIntentAction(Intent intent) {
        if (intent == null) return;
        String action = intent.getStringExtra("action");
        if ("open_checkin".equals(action)) {
            // Mở dialog check-in cảm xúc
            new CheckinDialog(this, moodLevel -> {
                MoodStorage moodStorage = new MoodStorage(this);
                moodStorage.saveMood(moodLevel, false);
                moodStorage.updateLastInteraction(System.currentTimeMillis());
                try {
                    JSONObject json = new JSONObject();
                    json.put("mood_level", moodLevel);
                    json.put("mood_score", MoodStorage.getMoodScore(moodLevel));
                    json.put("has_video", false);
                    new ApiClient(this).submitMoodCheckin(json.toString(), new ApiClient.ApiCallback() {
                        @Override public void onSuccess(String r) {}
                        @Override public void onFailure(String e) {}
                    });
                } catch (Exception ignored) {}
            }).show();
        }
        // Xóa action để không trigger lại khi rotate
        intent.removeExtra("action");
    }

    /** Lời chào theo khung giờ */
    private String getDynamicGreeting() {
        int hour = Calendar.getInstance().get(Calendar.HOUR_OF_DAY);
        if (hour >= 5 && hour < 12) return "Chào buổi sáng";
        if (hour >= 12 && hour < 18) return "Chào buổi chiều";
        if (hour >= 18 && hour < 22) return "Chào buổi tối";
        return "Chúc ngủ ngon";
    }

    private void setupCardListeners() {
        // Mood card → check-in cảm xúc dialog (lưu local + sync server)
        findViewById(R.id.btnUpdateMood).setOnClickListener(v -> {
            new CheckinDialog(this, moodLevel -> {
                MoodStorage moodStorage = new MoodStorage(this);
                moodStorage.saveMood(moodLevel, false);
                moodStorage.updateLastInteraction(System.currentTimeMillis());

                // Sync lên server: POST /mood/checkin
                try {
                    JSONObject json = new JSONObject();
                    json.put("mood_level", moodLevel);
                    json.put("mood_score", MoodStorage.getMoodScore(moodLevel));
                    json.put("has_video", false);
                    new ApiClient(this).submitMoodCheckin(json.toString(), new ApiClient.ApiCallback() {
                        @Override public void onSuccess(String r) { /* ok */ }
                        @Override public void onFailure(String e) { /* local đã lưu */ }
                    });
                } catch (Exception ignored) {}
            }).show();
        });

        // Feature cards
        findViewById(R.id.cardCamera).setOnClickListener(v -> {
            startActivity(new Intent(this, PhotoCaptureActivity.class));
        });

        findViewById(R.id.cardJournal).setOnClickListener(v -> {
            startActivity(new Intent(this, JournalActivity.class));
        });

        findViewById(R.id.cardMusic).setOnClickListener(v -> {
            Toast.makeText(this, "Chill chill", Toast.LENGTH_SHORT).show();
        });

        // Kiểm tra tâm trạng → luôn bắt đầu từ PHQ-2
        // Đây là Nguồn 3 (sinh viên tự yêu cầu) → KHÔNG áp dụng cooldown
        // Server sẽ quyết định có cần chuyển PHQ-9 sau khi nhận kết quả PHQ-2
        findViewById(R.id.cardCheckin).setOnClickListener(v -> {
            startActivity(new Intent(this, Phq2Activity.class)
                    .putExtra("source", "self_request"));
        });
    }

    private void setupNavBar() {
        findViewById(R.id.navHome).setOnClickListener(v -> {
            // Already on home
        });

        findViewById(R.id.navNote).setOnClickListener(v -> {
            startActivity(new Intent(this, JournalActivity.class));
        });

        findViewById(R.id.navChat).setOnClickListener(v -> {
            startActivity(new Intent(this, ChatActivity.class));
        });

        findViewById(R.id.navHeart).setOnClickListener(v -> {
            startActivity(new Intent(this, ProfileActivity.class));
        });
    }
}
