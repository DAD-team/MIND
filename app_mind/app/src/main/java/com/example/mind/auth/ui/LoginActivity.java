package com.example.mind.auth.ui;

import android.content.Intent;
import android.os.Bundle;
import android.util.Log;
import android.widget.EditText;
import android.widget.Toast;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;

import com.example.mind.MainActivity;
import com.example.mind.R;
import com.example.mind.notification.NotificationHelper;
import com.example.mind.auth.TokenManager;
import com.example.mind.auth.onboarding.OnboardingActivity;
import com.example.mind.common.UserPrefs;
import com.google.android.gms.auth.api.signin.GoogleSignIn;
import com.google.android.gms.auth.api.signin.GoogleSignInAccount;
import com.google.android.gms.auth.api.signin.GoogleSignInClient;
import com.google.android.gms.auth.api.signin.GoogleSignInOptions;
import com.google.android.gms.common.api.ApiException;
import com.google.android.gms.tasks.Task;
import com.google.firebase.auth.AuthCredential;
import com.google.firebase.auth.FirebaseAuth;
import com.google.firebase.auth.FirebaseUser;
import com.google.firebase.auth.GoogleAuthProvider;

public class LoginActivity extends AppCompatActivity {

    private static final String TAG = "LoginActivity";

    // Web client ID từ google-services.json (client_type = 3)
    private static final String WEB_CLIENT_ID =
            "744625845495-ea2almju6n3cebbuh5v9qhubqhq77pt0.apps.googleusercontent.com";

    private FirebaseAuth firebaseAuth;
    private GoogleSignInClient googleSignInClient;
    private TokenManager tokenManager;

    private EditText edtEmail, edtPassword;

    // Google Sign-In launcher (thay thế onActivityResult deprecated)
    private final ActivityResultLauncher<Intent> googleSignInLauncher =
            registerForActivityResult(new ActivityResultContracts.StartActivityForResult(), result -> {
                Task<GoogleSignInAccount> task = GoogleSignIn.getSignedInAccountFromIntent(result.getData());
                handleGoogleSignInResult(task);
            });

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_login);

        // Init Firebase
        firebaseAuth = FirebaseAuth.getInstance();
        tokenManager = new TokenManager(this);

        // Init Google Sign-In
        GoogleSignInOptions gso = new GoogleSignInOptions.Builder(GoogleSignInOptions.DEFAULT_SIGN_IN)
                .requestIdToken(WEB_CLIENT_ID)
                .requestEmail()
                .build();
        googleSignInClient = GoogleSignIn.getClient(this, gso);

        // Bind views
        edtEmail = findViewById(R.id.edtEmail);
        edtPassword = findViewById(R.id.edtPassword);

        // Login bằng email/password
        findViewById(R.id.btnLogin).setOnClickListener(v -> loginWithEmail());

        // Login bằng Google
        findViewById(R.id.btnGoogle).setOnClickListener(v -> loginWithGoogle());

        // Chuyển sang trang đăng ký
        findViewById(R.id.tvRegister).setOnClickListener(v -> {
            startActivity(new Intent(this, RegisterActivity.class));
            finish();
        });
    }

    @Override
    protected void onStart() {
        super.onStart();
        // Nếu user đã login trước đó
        FirebaseUser currentUser = firebaseAuth.getCurrentUser();
        if (currentUser != null) {
            // User cũ đã có displayName trên Firebase → coi như đã onboarded
            UserPrefs prefs = new UserPrefs(this);
            if (!prefs.isOnboarded() && currentUser.getDisplayName() != null
                    && !currentUser.getDisplayName().isEmpty()) {
                prefs.setDisplayName(currentUser.getDisplayName());
                prefs.setOnboarded(true);
            }
            saveTokenAndGoHome(currentUser);
        }
    }

    // ──────────────────────────────────────────────
    // EMAIL / PASSWORD LOGIN
    // ──────────────────────────────────────────────

    private void loginWithEmail() {
        String email = edtEmail.getText().toString().trim();
        String password = edtPassword.getText().toString().trim();

        if (email.isEmpty()) {
            edtEmail.setError("Vui lòng nhập email");
            edtEmail.requestFocus();
            return;
        }
        if (password.isEmpty()) {
            edtPassword.setError("Vui lòng nhập mật khẩu");
            edtPassword.requestFocus();
            return;
        }

        setLoading(true);

        firebaseAuth.signInWithEmailAndPassword(email, password)
                .addOnSuccessListener(authResult -> {
                    Log.d(TAG, "Email login success");
                    saveTokenAndGoHome(authResult.getUser());
                })
                .addOnFailureListener(e -> {
                    setLoading(false);
                    Log.e(TAG, "Email login failed", e);
                    Toast.makeText(this,
                            "Đăng nhập thất bại: " + e.getLocalizedMessage(),
                            Toast.LENGTH_LONG).show();
                });
    }

    // ──────────────────────────────────────────────
    // GOOGLE SIGN-IN
    // ──────────────────────────────────────────────

    private void loginWithGoogle() {
        setLoading(true);
        Intent signInIntent = googleSignInClient.getSignInIntent();
        googleSignInLauncher.launch(signInIntent);
    }

    private void handleGoogleSignInResult(Task<GoogleSignInAccount> task) {
        try {
            GoogleSignInAccount account = task.getResult(ApiException.class);
            firebaseAuthWithGoogle(account.getIdToken());
        } catch (ApiException e) {
            setLoading(false);
            Log.e(TAG, "Google sign-in failed, code=" + e.getStatusCode(), e);
            Toast.makeText(this,
                    "Đăng nhập Google thất bại",
                    Toast.LENGTH_SHORT).show();
        }
    }

    private void firebaseAuthWithGoogle(String googleIdToken) {
        AuthCredential credential = GoogleAuthProvider.getCredential(googleIdToken, null);

        firebaseAuth.signInWithCredential(credential)
                .addOnSuccessListener(authResult -> {
                    Log.d(TAG, "Google -> Firebase auth success");
                    saveTokenAndGoHome(authResult.getUser());
                })
                .addOnFailureListener(e -> {
                    setLoading(false);
                    Log.e(TAG, "Google -> Firebase auth failed", e);
                    Toast.makeText(this,
                            "Xác thực Firebase thất bại",
                            Toast.LENGTH_SHORT).show();
                });
    }

    // ──────────────────────────────────────────────
    // COMMON
    // ──────────────────────────────────────────────

    private void saveTokenAndGoHome(FirebaseUser user) {
        if (user == null) return;

        user.getIdToken(true).addOnSuccessListener(result -> {
            tokenManager.saveToken(result.getToken());
            Log.d(TAG, "Token saved");

            // Đăng ký FCM token ngay sau login
            NotificationHelper.registerFcmToken(this);

            UserPrefs userPrefs = new UserPrefs(this);

            // User đã có tên trên Firebase → khôi phục local, vào Home
            String displayName = user.getDisplayName();
            if (!userPrefs.isOnboarded() && displayName != null && !displayName.isEmpty()) {
                userPrefs.setDisplayName(displayName);
                userPrefs.setOnboarded(true);
            }

            if (userPrefs.isOnboarded()) {
                startActivity(new Intent(this, MainActivity.class));
            } else {
                // Chỉ hiện Onboarding khi user mới, chưa có tên
                startActivity(new Intent(this, OnboardingActivity.class));
            }
            finish();
        }).addOnFailureListener(e -> {
            setLoading(false);
            Log.e(TAG, "Failed to get token", e);
            Toast.makeText(this, "Lỗi lấy token", Toast.LENGTH_SHORT).show();
        });
    }

    private void setLoading(boolean loading) {
        findViewById(R.id.btnLogin).setEnabled(!loading);
        findViewById(R.id.btnGoogle).setEnabled(!loading);
    }
}
