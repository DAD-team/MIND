package com.example.mind.auth.onboarding;

import android.content.Intent;
import android.os.Bundle;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;

import com.example.mind.MainActivity;
import com.example.mind.R;
import com.example.mind.common.UserPrefs;
import com.example.mind.notification.NotificationHelper;

public class ConsentActivity extends AppCompatActivity {

    private UserPrefs userPrefs;
    private int selectedLevel = 0;

    private LinearLayout card1, card2, card3;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_consent);

        userPrefs = new UserPrefs(this);

        card1 = findViewById(R.id.cardConsent1);
        card2 = findViewById(R.id.cardConsent2);
        card3 = findViewById(R.id.cardConsent3);

        // Nếu đã có mức đồng thuận (đổi từ Profile), hiển thị mức hiện tại
        int current = userPrefs.getConsentLevel();
        if (current > 0) {
            selectLevel(current);
        }

        card1.setOnClickListener(v -> selectLevel(1));
        card2.setOnClickListener(v -> selectLevel(2));
        card3.setOnClickListener(v -> selectLevel(3));

        findViewById(R.id.btnConsentContinue).setOnClickListener(v -> onConfirm());
    }

    private void selectLevel(int level) {
        selectedLevel = level;

        card1.setSelected(level == 1);
        card2.setSelected(level == 2);
        card3.setSelected(level == 3);

        // Show/hide check icons
        findViewById(R.id.checkConsent1).setVisibility(level == 1 ? View.VISIBLE : View.GONE);
        findViewById(R.id.checkConsent2).setVisibility(level == 2 ? View.VISIBLE : View.GONE);
        findViewById(R.id.checkConsent3).setVisibility(level == 3 ? View.VISIBLE : View.GONE);
    }

    private void onConfirm() {
        if (selectedLevel == 0) {
            Toast.makeText(this, R.string.consent_toast_select, Toast.LENGTH_SHORT).show();
            return;
        }

        userPrefs.setConsentLevel(selectedLevel);

        // Cập nhật consent level lên backend để server biết được phép gửi gì
        NotificationHelper.registerFcmToken(this);

        // Nếu gọi từ Profile (đổi mức), chỉ cần finish
        boolean fromProfile = getIntent().getBooleanExtra("from_profile", false);
        if (fromProfile) {
            Toast.makeText(this, "Đã cập nhật mức " + selectedLevel, Toast.LENGTH_SHORT).show();
            finish();
            return;
        }

        // Onboarding flow → vào Home
        startActivity(new Intent(this, MainActivity.class));
        finish();
    }
}
