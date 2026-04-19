package com.example.mind.vr;

import android.Manifest;
import android.content.ContentValues;
import android.content.pm.PackageManager;
import android.graphics.Bitmap;
import android.os.Bundle;
import android.os.SystemClock;
import android.provider.MediaStore;
import android.util.Log;
import android.view.View;
import android.widget.TextView;
import android.widget.Toast;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.annotation.NonNull;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;
import androidx.camera.core.CameraSelector;
import androidx.camera.core.ImageAnalysis;
import androidx.camera.core.ImageProxy;
import androidx.camera.core.Preview;
import androidx.camera.lifecycle.ProcessCameraProvider;
import androidx.camera.video.MediaStoreOutputOptions;
import androidx.camera.video.Quality;
import androidx.camera.video.QualitySelector;
import androidx.camera.video.Recorder;
import androidx.camera.video.Recording;
import androidx.camera.video.VideoCapture;
import androidx.camera.video.VideoRecordEvent;
import androidx.camera.view.PreviewView;
import androidx.core.content.ContextCompat;

import com.example.mind.R;
import com.example.mind.auth.ApiClient;
import com.example.mind.checkin.data.MoodStorage;
import com.example.mind.common.UserPrefs;

import org.json.JSONObject;
// FILTER DISABLED — bật lại khi fix xong
// import com.example.mind.vr.filter.FaceFilter;
// import com.example.mind.vr.filter.FaceFilterOverlay;
import com.google.mediapipe.framework.image.BitmapImageBuilder;
import com.google.mediapipe.framework.image.MPImage;
import com.google.mediapipe.tasks.core.BaseOptions;
import com.google.mediapipe.tasks.vision.core.RunningMode;
import com.google.mediapipe.tasks.vision.facelandmarker.FaceLandmarker;
import com.google.mediapipe.tasks.vision.facelandmarker.FaceLandmarkerResult;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

public class VideoRecordActivity extends AppCompatActivity {

    private static final String TAG = "VideoRecord";
    private static final long RECORD_DURATION_MS = 5000;

    // Views
    private PreviewView previewView;
    // private FaceFilterOverlay filterOverlay; // FILTER DISABLED
    private TextView tvStatus;
    private View btnRecord, viewRecordDot;
    private RecordProgressView progressRing;
    // private LinearLayout filterSelector; // FILTER DISABLED
    // private View filterScroll; // FILTER DISABLED

    // Camera
    private VideoCapture<Recorder> videoCapture;
    private Recording activeRecording;
    private boolean isRecording = false;
    private boolean isFrontCamera = true;

    // MediaPipe
    private FaceLandmarker faceLandmarker;
    private final LandmarkCollector landmarkCollector = new LandmarkCollector();

    private final ActivityResultLauncher<String[]> permissionLauncher =
            registerForActivityResult(new ActivityResultContracts.RequestMultiplePermissions(), result -> {
                boolean allGranted = true;
                for (Boolean granted : result.values()) {
                    if (!granted) allGranted = false;
                }
                if (allGranted) {
                    setupMediaPipe();
                    startCamera();
                } else {
                    Toast.makeText(this, R.string.video_permission_denied, Toast.LENGTH_LONG).show();
                    finish();
                }
            });

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_video_record);

        // Guard: chỉ cho phép quay video khi consent >= 2
        UserPrefs userPrefs = new UserPrefs(this);
        if (userPrefs.getConsentLevel() < 2) {
            Toast.makeText(this, "Tính năng này cần mức đồng thuận 2 trở lên", Toast.LENGTH_SHORT).show();
            finish();
            return;
        }

        previewView = findViewById(R.id.previewView);
        // filterOverlay = findViewById(R.id.filterOverlay); // FILTER DISABLED
        tvStatus = findViewById(R.id.tvStatus);
        btnRecord = findViewById(R.id.btnRecord);
        viewRecordDot = findViewById(R.id.viewRecordDot);
        progressRing = findViewById(R.id.progressRing);
        // filterSelector = findViewById(R.id.filterSelector); // FILTER DISABLED
        // filterScroll = findViewById(R.id.filterScroll); // FILTER DISABLED

        // Ẩn filter UI
        View filterScroll = findViewById(R.id.filterScroll);
        if (filterScroll != null) filterScroll.setVisibility(View.GONE);

        btnRecord.setOnClickListener(v -> { if (!isRecording) startRecording(); });
        findViewById(R.id.btnClose).setOnClickListener(v -> finish());
        findViewById(R.id.btnSwitchCamera).setOnClickListener(v -> {
            isFrontCamera = !isFrontCamera;
            // filterOverlay.setFrontCamera(isFrontCamera); // FILTER DISABLED
            startCamera();
        });

        // setupFilterSelector(); // FILTER DISABLED
        // filterOverlay.setFrontCamera(isFrontCamera); // FILTER DISABLED

        progressRing.setOnCompleteListener(this::stopRecording);

        if (hasPermissions()) {
            setupMediaPipe();
            startCamera();
        } else {
            permissionLauncher.launch(new String[]{
                    Manifest.permission.CAMERA,
                    Manifest.permission.RECORD_AUDIO
            });
        }
    }

    // ──────────────────────────────────────────────
    // UI STATES
    // ──────────────────────────────────────────────

    // Voice prompts — câu hướng dẫn người dùng nói khi quay
    private static final String[] VOICE_PROMPTS = {
            "Hãy nói: Hôm nay mình cảm thấy...",
            "Hãy nói: Điều mình mong muốn nhất là...",
            "Hãy nói: Gần đây mình hay nghĩ về...",
            "Hãy kể về một ngày bình thường của bạn~",
            "Hãy nói: Mình muốn chia sẻ rằng..."
    };

    private void setStateReady() {
        viewRecordDot.setVisibility(View.VISIBLE);
        viewRecordDot.setAlpha(1f);
        progressRing.reset();
        btnRecord.setEnabled(true);
        // Hiển thị voice prompt ngẫu nhiên
        String prompt = VOICE_PROMPTS[(int) (Math.random() * VOICE_PROMPTS.length)];
        tvStatus.setText(prompt);
        tvStatus.setVisibility(View.VISIBLE);
    }

    private void setStateRecording() {
        viewRecordDot.animate().scaleX(0.6f).scaleY(0.6f).setDuration(200).start();
        tvStatus.setText("Đang ghi...");
        tvStatus.setVisibility(View.VISIBLE);
        btnRecord.setEnabled(false);
        progressRing.startProgress(RECORD_DURATION_MS);
    }

    private void setStateDone() {
        viewRecordDot.animate().scaleX(1f).scaleY(1f).setDuration(200).start();
        viewRecordDot.setVisibility(View.VISIBLE);
        progressRing.reset();
        btnRecord.setEnabled(true);
        Toast.makeText(this, "Đã hoàn thành!", Toast.LENGTH_SHORT).show();
    }

    // ──────────────────────────────────────────────
    // FILTER SELECTOR — DISABLED
    // ──────────────────────────────────────────────
    /*
    private void setupFilterSelector() {
        addFilterButton(null, -1);
        for (int i = 0; i < FaceFilter.PRESETS.length; i++) {
            addFilterButton(FaceFilter.PRESETS[i], i);
        }
    }

    private void addFilterButton(FaceFilter.Preset preset, int index) {
        // ... filter button logic ...
    }
    */

    // ──────────────────────────────────────────────
    // MEDIAPIPE
    // ──────────────────────────────────────────────

    private void setupMediaPipe() {
        try {
            BaseOptions baseOptions = BaseOptions.builder()
                    .setModelAssetPath("face_landmarker.task")
                    .build();

            FaceLandmarker.FaceLandmarkerOptions options =
                    FaceLandmarker.FaceLandmarkerOptions.builder()
                            .setBaseOptions(baseOptions)
                            .setRunningMode(RunningMode.LIVE_STREAM)
                            .setNumFaces(1)
                            .setMinFaceDetectionConfidence(0.5f)
                            .setMinFacePresenceConfidence(0.5f)
                            .setMinTrackingConfidence(0.5f)
                            .setOutputFaceBlendshapes(true)
                            .setResultListener(this::onFaceLandmarkResult)
                            .setErrorListener(e -> Log.e(TAG, "MediaPipe error", e))
                            .build();

            faceLandmarker = FaceLandmarker.createFromOptions(this, options);
        } catch (Exception e) {
            Log.e(TAG, "Failed to setup MediaPipe", e);
        }
    }

    private void onFaceLandmarkResult(FaceLandmarkerResult result, MPImage input) {
        // filterOverlay.updateLandmarks(result); // FILTER DISABLED
        if (isRecording) {
            landmarkCollector.addFrame(result, System.currentTimeMillis());
        }
    }

    // ──────────────────────────────────────────────
    // CAMERA
    // ──────────────────────────────────────────────

    private boolean hasPermissions() {
        return ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA)
                == PackageManager.PERMISSION_GRANTED
                && ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO)
                == PackageManager.PERMISSION_GRANTED;
    }

    private void startCamera() {
        ProcessCameraProvider.getInstance(this).addListener(() -> {
            try {
                ProcessCameraProvider cameraProvider = ProcessCameraProvider.getInstance(this).get();
                cameraProvider.unbindAll();

                Preview preview = new Preview.Builder().build();
                preview.setSurfaceProvider(previewView.getSurfaceProvider());

                Recorder recorder = new Recorder.Builder()
                        .setQualitySelector(QualitySelector.from(Quality.HD))
                        .build();
                videoCapture = VideoCapture.withOutput(recorder);

                ImageAnalysis imageAnalysis = new ImageAnalysis.Builder()
                        .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                        .setOutputImageFormat(ImageAnalysis.OUTPUT_IMAGE_FORMAT_RGBA_8888)
                        .build();
                imageAnalysis.setAnalyzer(ContextCompat.getMainExecutor(this), this::analyzeFrame);

                CameraSelector cameraSelector = isFrontCamera
                        ? CameraSelector.DEFAULT_FRONT_CAMERA
                        : CameraSelector.DEFAULT_BACK_CAMERA;

                cameraProvider.bindToLifecycle(this, cameraSelector,
                        preview, videoCapture, imageAnalysis);
            } catch (Exception e) {
                Log.e(TAG, "Camera setup failed", e);
            }
        }, ContextCompat.getMainExecutor(this));
    }

    private void analyzeFrame(@NonNull ImageProxy imageProxy) {
        if (faceLandmarker == null) { imageProxy.close(); return; }
        try {
            Bitmap bitmap = imageProxy.toBitmap();
            MPImage mpImage = new BitmapImageBuilder(bitmap).build();
            faceLandmarker.detectAsync(mpImage, SystemClock.uptimeMillis());
        } catch (Exception e) {
            Log.e(TAG, "Frame analysis error", e);
        } finally {
            imageProxy.close();
        }
    }

    // ──────────────────────────────────────────────
    // RECORDING
    // ──────────────────────────────────────────────

    private void startRecording() {
        if (videoCapture == null) return;

        isRecording = true;
        landmarkCollector.start();
        setStateRecording();

        String timestamp = new SimpleDateFormat("yyyyMMdd_HHmmss", Locale.getDefault()).format(new Date());
        ContentValues contentValues = new ContentValues();
        contentValues.put(MediaStore.MediaColumns.DISPLAY_NAME, "MIND_" + timestamp);
        contentValues.put(MediaStore.MediaColumns.MIME_TYPE, "video/mp4");

        MediaStoreOutputOptions outputOptions = new MediaStoreOutputOptions.Builder(
                getContentResolver(), MediaStore.Video.Media.EXTERNAL_CONTENT_URI)
                .setContentValues(contentValues)
                .build();

        activeRecording = videoCapture.getOutput()
                .prepareRecording(this, outputOptions)
                .withAudioEnabled()
                .start(ContextCompat.getMainExecutor(this), this::onVideoEvent);
    }

    private void stopRecording() {
        if (activeRecording != null) {
            activeRecording.stop();
            activeRecording = null;
            isRecording = false;
        }
    }

    private void onVideoEvent(@NonNull VideoRecordEvent event) {
        if (event instanceof VideoRecordEvent.Finalize) {
            VideoRecordEvent.Finalize finalizeEvent = (VideoRecordEvent.Finalize) event;
            if (!finalizeEvent.hasError()) {
                android.net.Uri videoUri = finalizeEvent.getOutputResults().getOutputUri();
                Log.d(TAG, "Video saved: " + videoUri);

                setStateDone();
                uploadVideoToBackend(videoUri);
            } else {
                Log.e(TAG, "Recording error: " + finalizeEvent.getError());
                Toast.makeText(this, "Lỗi quay video", Toast.LENGTH_SHORT).show();
                setStateReady();
            }
        }
    }

    // ──────────────────────────────────────────────
    // UPLOAD TO BACKEND
    // ──────────────────────────────────────────────

    private void uploadVideoToBackend(android.net.Uri videoUri) {
        new Thread(() -> {
            try {
                File tempFile = new File(getCacheDir(), "upload_video.mp4");
                InputStream is = getContentResolver().openInputStream(videoUri);
                FileOutputStream fos = new FileOutputStream(tempFile);
                byte[] buffer = new byte[8192];
                int len;
                while ((len = is.read(buffer)) != -1) {
                    fos.write(buffer, 0, len);
                }
                fos.close();
                is.close();

                ApiClient apiClient = new ApiClient(this);
                apiClient.uploadVideo(tempFile, new ApiClient.ApiCallback() {
                    @Override
                    public void onSuccess(String responseBody) {
                        Log.d(TAG, "Video upload success: " + responseBody);
                        tempFile.delete();

                        // Parse server response → lưu emotion metrics
                        try {
                            JSONObject json = new JSONObject(responseBody);
                            JSONObject result = json.optJSONObject("result");
                            if (result != null) {
                                double duchenne = result.optDouble("duchenne_ratio", -1);
                                double flatAffect = result.optDouble("flat_affect_score", -1);
                                double gazeInstability = result.optDouble("gaze_instability", -1);
                                double headDown = result.optDouble("head_down_ratio", -1);
                                double riskScore = result.optDouble("behavioral_risk_score", -1);

                                Log.d(TAG, "Emotion metrics: duchenne=" + duchenne
                                        + " flatAffect=" + flatAffect
                                        + " risk=" + riskScore);

                                // Cập nhật tương tác
                                new MoodStorage(VideoRecordActivity.this)
                                        .updateLastInteraction(System.currentTimeMillis());
                            }
                        } catch (Exception e) {
                            Log.e(TAG, "Failed to parse video response", e);
                        }
                    }

                    @Override
                    public void onFailure(String error) {
                        Log.e(TAG, "Upload failed: " + error);
                        tempFile.delete();
                    }
                });
            } catch (Exception e) {
                Log.e(TAG, "Upload prep failed", e);
            }
        }).start();
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        if (activeRecording != null) activeRecording.stop();
        if (faceLandmarker != null) faceLandmarker.close();
    }
}
