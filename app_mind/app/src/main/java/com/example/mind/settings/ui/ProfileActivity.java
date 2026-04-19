package com.example.mind.settings.ui;

import android.content.Intent;
import android.os.Bundle;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;

import com.example.mind.MainActivity;
import com.example.mind.R;
import com.example.mind.auth.TokenManager;
import com.example.mind.auth.onboarding.ConsentActivity;
import com.example.mind.notification.NotificationHelper;
import com.example.mind.auth.ui.LoginActivity;
import com.example.mind.chat.ChatActivity;
import com.example.mind.common.UserPrefs;
import com.example.mind.journal.JournalActivity;
import com.example.mind.schedule.ui.ScheduleActivity;
import com.example.mind.journal.JournalStorage;
import com.google.android.gms.auth.api.signin.GoogleSignIn;
import com.google.android.gms.auth.api.signin.GoogleSignInClient;
import com.google.android.gms.auth.api.signin.GoogleSignInOptions;
import com.google.firebase.auth.FirebaseAuth;

import java.util.List;

public class ProfileActivity extends AppCompatActivity {

    private FirebaseAuth firebaseAuth;
    private TokenManager tokenManager;
    private UserPrefs userPrefs;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_profile);

        firebaseAuth = FirebaseAuth.getInstance();
        tokenManager = new TokenManager(this);
        userPrefs = new UserPrefs(this);

        // Hiển thị tên user từ UserPrefs (đồng bộ xuyên suốt app)
        ((TextView) findViewById(R.id.tvName)).setText(userPrefs.getDisplayName());

        // Load journal count for memories stat
        List<JournalStorage.Entry> entries = JournalStorage.loadEntries(this);
        ((TextView) findViewById(R.id.tvMemories)).setText(String.valueOf(entries.size()));

        // Menu clicks
        findViewById(R.id.menuSchedule).setOnClickListener(v -> {
            startActivity(new Intent(this, ScheduleActivity.class));
        });

        findViewById(R.id.menuEditProfile).setOnClickListener(v -> {
            Toast.makeText(this, "Chỉnh sửa hồ sơ", Toast.LENGTH_SHORT).show();
        });

        // Hiển thị mức đồng thuận hiện tại
        updateConsentBadge();

        findViewById(R.id.menuConsent).setOnClickListener(v -> {
            Intent intent = new Intent(this, ConsentActivity.class);
            intent.putExtra("from_profile", true);
            startActivity(intent);
        });

        findViewById(R.id.menuNotification).setOnClickListener(v -> {
            Toast.makeText(this, "Thông báo", Toast.LENGTH_SHORT).show();
        });

        // Logout Firebase + Google + clear token
        findViewById(R.id.menuLogout).setOnClickListener(v -> logout());

        // Bottom toolbar navigation
        setupNavBar();
    }

    @Override
    protected void onResume() {
        super.onResume();
        updateConsentBadge();
    }

    private void updateConsentBadge() {
        TextView tvConsent = findViewById(R.id.tvConsentLevel);
        int level = userPrefs.getConsentLevel();
        if (level > 0) {
            tvConsent.setText("Mức " + level);
        } else {
            tvConsent.setText("Chưa chọn");
        }
    }

    private void logout() {
        // 0. Xóa FCM token trên server TRƯỚC khi sign out
        NotificationHelper.unregisterFcmToken(this, () -> runOnUiThread(() -> {
            // 1. Firebase sign out
            firebaseAuth.signOut();

            // 2. Google sign out
            GoogleSignInOptions gso = new GoogleSignInOptions.Builder(GoogleSignInOptions.DEFAULT_SIGN_IN).build();
            GoogleSignInClient googleClient = GoogleSignIn.getClient(this, gso);
            googleClient.signOut();

            // 3. Xóa token + user data
            tokenManager.clearToken();
            userPrefs.clear();

            // 4. Quay về Login
            Intent intent = new Intent(this, LoginActivity.class);
            intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
            startActivity(intent);
            finish();
        }));
    }

    private void setupNavBar() {
        findViewById(R.id.navHome).setOnClickListener(v -> {
            startActivity(new Intent(this, MainActivity.class));
            finish();
        });

        findViewById(R.id.navNote).setOnClickListener(v -> {
            startActivity(new Intent(this, JournalActivity.class));
            finish();
        });

        findViewById(R.id.navChat).setOnClickListener(v -> {
            startActivity(new Intent(this, ChatActivity.class));
            finish();
        });

        findViewById(R.id.navHeart).setOnClickListener(v -> {
            // Already on profile
        });
    }
}
