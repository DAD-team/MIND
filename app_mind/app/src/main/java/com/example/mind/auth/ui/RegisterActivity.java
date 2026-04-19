package com.example.mind.auth.ui;

import android.content.Intent;
import android.os.Bundle;
import android.util.Log;
import android.widget.EditText;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;

import com.example.mind.R;
import com.example.mind.auth.TokenManager;
import com.example.mind.auth.onboarding.OnboardingActivity;
import com.google.firebase.auth.FirebaseAuth;
import com.google.firebase.auth.FirebaseUser;
import com.google.firebase.auth.UserProfileChangeRequest;

public class RegisterActivity extends AppCompatActivity {

    private static final String TAG = "RegisterActivity";

    private FirebaseAuth firebaseAuth;
    private TokenManager tokenManager;

    private EditText edtFullName, edtEmail, edtPassword;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_register);

        firebaseAuth = FirebaseAuth.getInstance();
        tokenManager = new TokenManager(this);

        // Bind views
        edtFullName = findViewById(R.id.edtFullName);
        edtEmail = findViewById(R.id.edtEmail);
        edtPassword = findViewById(R.id.edtPassword);

        // Đăng ký
        findViewById(R.id.btnRegister).setOnClickListener(v -> registerWithEmail());

        // Chuyển sang trang đăng nhập
        findViewById(R.id.tvLogin).setOnClickListener(v -> {
            startActivity(new Intent(this, LoginActivity.class));
            finish();
        });
    }

    // ──────────────────────────────────────────────
    // EMAIL / PASSWORD REGISTER
    // ──────────────────────────────────────────────

    private void registerWithEmail() {
        String fullName = edtFullName.getText().toString().trim();
        String email = edtEmail.getText().toString().trim();
        String password = edtPassword.getText().toString().trim();

        // Validate
        if (fullName.isEmpty()) {
            edtFullName.setError("Vui lòng nhập họ tên");
            edtFullName.requestFocus();
            return;
        }
        if (email.isEmpty()) {
            edtEmail.setError("Vui lòng nhập email");
            edtEmail.requestFocus();
            return;
        }
        if (password.length() < 6) {
            edtPassword.setError("Mật khẩu tối thiểu 6 ký tự");
            edtPassword.requestFocus();
            return;
        }

        setLoading(true);

        // Bước 1: Tạo tài khoản Firebase
        firebaseAuth.createUserWithEmailAndPassword(email, password)
                .addOnSuccessListener(authResult -> {
                    FirebaseUser user = authResult.getUser();
                    if (user == null) {
                        setLoading(false);
                        return;
                    }

                    // Bước 2: Cập nhật displayName
                    UserProfileChangeRequest profileUpdate = new UserProfileChangeRequest.Builder()
                            .setDisplayName(fullName)
                            .build();

                    user.updateProfile(profileUpdate)
                            .addOnCompleteListener(task -> {
                                // Bước 3: Lấy token và vào Home
                                saveTokenAndGoHome(user);
                            });
                })
                .addOnFailureListener(e -> {
                    setLoading(false);
                    Log.e(TAG, "Register failed", e);

                    String msg = e.getLocalizedMessage();
                    if (msg != null && msg.contains("email address is already in use")) {
                        Toast.makeText(this,
                                "Email này đã được sử dụng",
                                Toast.LENGTH_LONG).show();
                    } else {
                        Toast.makeText(this,
                                "Đăng ký thất bại: " + msg,
                                Toast.LENGTH_LONG).show();
                    }
                });
    }

    // ──────────────────────────────────────────────
    // COMMON
    // ──────────────────────────────────────────────

    private void saveTokenAndGoHome(FirebaseUser user) {
        user.getIdToken(true).addOnSuccessListener(result -> {
            tokenManager.saveToken(result.getToken());
            Log.d(TAG, "Registered + token saved, navigating to Onboarding");

            // Đăng ký mới → luôn vào Onboarding để nhập tên
            startActivity(new Intent(this, OnboardingActivity.class));
            finish();
        }).addOnFailureListener(e -> {
            setLoading(false);
            Log.e(TAG, "Failed to get token after register", e);
            Toast.makeText(this, "Lỗi lấy token", Toast.LENGTH_SHORT).show();
        });
    }

    private void setLoading(boolean loading) {
        findViewById(R.id.btnRegister).setEnabled(!loading);
    }
}
