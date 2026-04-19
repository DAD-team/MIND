package com.example.mind.auth.onboarding;

import android.content.Intent;
import android.os.Bundle;
import android.widget.EditText;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;

import com.example.mind.R;
import com.example.mind.common.UserPrefs;
import com.google.firebase.auth.FirebaseAuth;
import com.google.firebase.auth.FirebaseUser;
import com.google.firebase.auth.UserProfileChangeRequest;

public class OnboardingActivity extends AppCompatActivity {

    private EditText edtName;
    private UserPrefs userPrefs;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_onboarding);

        userPrefs = new UserPrefs(this);
        edtName = findViewById(R.id.edtName);

        // Nếu Firebase đã có displayName -> điền sẵn
        FirebaseUser user = FirebaseAuth.getInstance().getCurrentUser();
        if (user != null && user.getDisplayName() != null && !user.getDisplayName().isEmpty()) {
            edtName.setText(user.getDisplayName());
        }

        findViewById(R.id.btnContinue).setOnClickListener(v -> onContinue());
    }

    private void onContinue() {
        String name = edtName.getText().toString().trim();

        if (name.isEmpty()) {
            edtName.setError("Vui lòng nhập tên của bạn");
            edtName.requestFocus();
            return;
        }

        // Lưu tên vào SharedPreferences (dùng xuyên suốt app)
        userPrefs.setDisplayName(name);
        userPrefs.setOnboarded(true);

        // Cập nhật displayName trên Firebase (đồng bộ cloud)
        FirebaseUser user = FirebaseAuth.getInstance().getCurrentUser();
        if (user != null) {
            UserProfileChangeRequest profileUpdate = new UserProfileChangeRequest.Builder()
                    .setDisplayName(name)
                    .build();
            user.updateProfile(profileUpdate);
        }

        // Vào màn hình chọn mức đồng thuận
        startActivity(new Intent(this, ConsentActivity.class));
        finish();
    }
}
