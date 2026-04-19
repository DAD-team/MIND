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
import android.widget.Toast;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.annotation.NonNull;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;
import androidx.camera.core.CameraSelector;
import androidx.camera.core.ImageAnalysis;
import androidx.camera.core.ImageCapture;
import androidx.camera.core.ImageCaptureException;
import androidx.camera.core.ImageProxy;
import androidx.camera.core.Preview;
import androidx.camera.lifecycle.ProcessCameraProvider;
import androidx.camera.view.PreviewView;
import androidx.core.content.ContextCompat;

import com.example.mind.R;
import com.example.mind.checkin.data.MoodStorage;
// FILTER DISABLED — bật lại khi fix xong
// import com.example.mind.vr.filter.FaceFilter;
// import com.example.mind.vr.filter.FaceFilterOverlay;
import com.google.mediapipe.framework.image.BitmapImageBuilder;
import com.google.mediapipe.framework.image.MPImage;
import com.google.mediapipe.tasks.core.BaseOptions;
import com.google.mediapipe.tasks.vision.core.RunningMode;
import com.google.mediapipe.tasks.vision.facelandmarker.FaceLandmarker;
import com.google.mediapipe.tasks.vision.facelandmarker.FaceLandmarkerResult;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

public class PhotoCaptureActivity extends AppCompatActivity {

    private static final String TAG = "PhotoCapture";

    private PreviewView previewView;
    // private FaceFilterOverlay filterOverlay; // FILTER DISABLED
    private View btnCapture;

    private ImageCapture imageCapture;
    private FaceLandmarker faceLandmarker;
    private boolean isFrontCamera = true;

    private final ActivityResultLauncher<String> permissionLauncher =
            registerForActivityResult(new ActivityResultContracts.RequestPermission(), granted -> {
                if (granted) {
                    setupMediaPipe();
                    startCamera();
                } else {
                    Toast.makeText(this, "Cần cấp quyền camera để chụp ảnh", Toast.LENGTH_LONG).show();
                    finish();
                }
            });

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_photo_capture);

        previewView = findViewById(R.id.previewView);
        // filterOverlay = findViewById(R.id.filterOverlay); // FILTER DISABLED
        btnCapture = findViewById(R.id.btnCapture);

        // Ẩn filter UI
        View filterScroll = findViewById(R.id.filterScroll);
        if (filterScroll != null) filterScroll.setVisibility(View.GONE);

        btnCapture.setOnClickListener(v -> takePhoto());
        findViewById(R.id.btnClose).setOnClickListener(v -> finish());
        findViewById(R.id.btnSwitchCamera).setOnClickListener(v -> {
            isFrontCamera = !isFrontCamera;
            // filterOverlay.setFrontCamera(isFrontCamera); // FILTER DISABLED
            startCamera();
        });

        // setupFilterSelector(); // FILTER DISABLED
        // filterOverlay.setFrontCamera(isFrontCamera); // FILTER DISABLED

        if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA)
                == PackageManager.PERMISSION_GRANTED) {
            setupMediaPipe();
            startCamera();
        } else {
            permissionLauncher.launch(Manifest.permission.CAMERA);
        }
    }

    // ──────────────────────────────────────────────
    // FILTER SELECTOR — DISABLED
    // ──────────────────────────────────────────────
    /*
    private void setupFilterSelector() { ... }
    private void addFilterButton(FaceFilter.Preset preset, int index) { ... }
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
                            .setOutputFaceBlendshapes(false)
                            .setResultListener(this::onFaceResult)
                            .setErrorListener(e -> Log.e(TAG, "MediaPipe error", e))
                            .build();

            faceLandmarker = FaceLandmarker.createFromOptions(this, options);
        } catch (Exception e) {
            Log.e(TAG, "MediaPipe setup failed", e);
        }
    }

    private void onFaceResult(FaceLandmarkerResult result, MPImage input) {
        // filterOverlay.updateLandmarks(result); // FILTER DISABLED
    }

    // ──────────────────────────────────────────────
    // CAMERA
    // ──────────────────────────────────────────────

    private void startCamera() {
        ProcessCameraProvider.getInstance(this).addListener(() -> {
            try {
                ProcessCameraProvider cameraProvider = ProcessCameraProvider.getInstance(this).get();
                cameraProvider.unbindAll();

                Preview preview = new Preview.Builder().build();
                preview.setSurfaceProvider(previewView.getSurfaceProvider());

                imageCapture = new ImageCapture.Builder()
                        .setCaptureMode(ImageCapture.CAPTURE_MODE_MINIMIZE_LATENCY)
                        .build();

                ImageAnalysis imageAnalysis = new ImageAnalysis.Builder()
                        .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                        .setOutputImageFormat(ImageAnalysis.OUTPUT_IMAGE_FORMAT_RGBA_8888)
                        .build();
                imageAnalysis.setAnalyzer(ContextCompat.getMainExecutor(this), this::analyzeFrame);

                CameraSelector cameraSelector = isFrontCamera
                        ? CameraSelector.DEFAULT_FRONT_CAMERA
                        : CameraSelector.DEFAULT_BACK_CAMERA;

                cameraProvider.bindToLifecycle(this, cameraSelector,
                        preview, imageCapture, imageAnalysis);

            } catch (Exception e) {
                Log.e(TAG, "Camera setup failed", e);
            }
        }, ContextCompat.getMainExecutor(this));
    }

    private void analyzeFrame(@NonNull ImageProxy imageProxy) {
        if (faceLandmarker == null) {
            imageProxy.close();
            return;
        }
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
    // CAPTURE (ảnh GỐC, không filter)
    // ──────────────────────────────────────────────

    private void takePhoto() {
        if (imageCapture == null) return;

        btnCapture.setEnabled(false);

        String timestamp = new SimpleDateFormat("yyyyMMdd_HHmmss", Locale.getDefault()).format(new Date());
        ContentValues contentValues = new ContentValues();
        contentValues.put(MediaStore.MediaColumns.DISPLAY_NAME, "MIND_" + timestamp);
        contentValues.put(MediaStore.MediaColumns.MIME_TYPE, "image/jpeg");

        ImageCapture.OutputFileOptions outputOptions = new ImageCapture.OutputFileOptions.Builder(
                getContentResolver(), MediaStore.Images.Media.EXTERNAL_CONTENT_URI, contentValues)
                .build();

        imageCapture.takePicture(outputOptions, ContextCompat.getMainExecutor(this),
                new ImageCapture.OnImageSavedCallback() {
                    @Override
                    public void onImageSaved(@NonNull ImageCapture.OutputFileResults output) {
                        Log.d(TAG, "Photo saved: " + output.getSavedUri());
                        new MoodStorage(PhotoCaptureActivity.this)
                                .updateLastInteraction(System.currentTimeMillis());
                        Toast.makeText(PhotoCaptureActivity.this,
                                "Ảnh đã lưu!", Toast.LENGTH_SHORT).show();
                        btnCapture.setEnabled(true);
                    }

                    @Override
                    public void onError(@NonNull ImageCaptureException e) {
                        Log.e(TAG, "Photo capture failed", e);
                        Toast.makeText(PhotoCaptureActivity.this,
                                "Lỗi chụp ảnh", Toast.LENGTH_SHORT).show();
                        btnCapture.setEnabled(true);
                    }
                });
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        if (faceLandmarker != null) faceLandmarker.close();
    }
}
