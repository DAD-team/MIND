package com.example.mind.vr.filter;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Matrix;
import android.util.AttributeSet;
import android.util.SparseArray;
import android.view.View;

import com.google.mediapipe.tasks.components.containers.NormalizedLandmark;
import com.google.mediapipe.tasks.vision.facelandmarker.FaceLandmarkerResult;

import java.util.List;

/**
 * Custom View vẽ filter (monster stickers) lên trên camera preview.
 * CHỈ hiển thị cho user nhìn, KHÔNG ảnh hưởng video gốc.
 */
public class FaceFilterOverlay extends View {

    private volatile FaceLandmarkerResult faceLandmarkResult;
    private volatile FaceFilter.Preset activeFilter;
    private volatile boolean isFrontCamera = true;

    // Cache bitmap để không load lại mỗi frame
    private final SparseArray<Bitmap> bitmapCache = new SparseArray<>();

    public FaceFilterOverlay(Context context) {
        super(context);
    }

    public FaceFilterOverlay(Context context, AttributeSet attrs) {
        super(context, attrs);
    }

    /** Set filter preset hiện tại (null = không filter) */
    public void setActiveFilter(FaceFilter.Preset filter) {
        this.activeFilter = filter;
        invalidate();
    }

    /** Set front/back camera (để mirror đúng) */
    public void setFrontCamera(boolean frontCamera) {
        this.isFrontCamera = frontCamera;
    }

    /** Cập nhật face landmark result từ MediaPipe (gọi mỗi frame) */
    public void updateLandmarks(FaceLandmarkerResult result) {
        this.faceLandmarkResult = result;
        postInvalidate(); // redraw on UI thread
    }

    /** Xóa landmarks (khi không detect được mặt) */
    public void clearLandmarks() {
        this.faceLandmarkResult = null;
        postInvalidate();
    }

    @Override
    protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);

        if (activeFilter == null || faceLandmarkResult == null) return;
        if (faceLandmarkResult.faceLandmarks().isEmpty()) return;

        // Lấy landmarks của face đầu tiên
        List<NormalizedLandmark> landmarks = faceLandmarkResult.faceLandmarks().get(0);
        int viewW = getWidth();
        int viewH = getHeight();

        // Cần ít nhất 153 landmarks (index 0-152) để tính kích thước mặt
        if (landmarks.size() < 153) return;

        // Tính kích thước khuôn mặt (khoảng cách trán → cằm)
        NormalizedLandmark forehead = landmarks.get(10);
        NormalizedLandmark chin = landmarks.get(152);
        float faceHeight = Math.abs(chin.y() - forehead.y()) * viewH;
        if (faceHeight < 1f) return;

        for (FaceFilter.Sticker sticker : activeFilter.stickers) {
            if (sticker.landmarkIndex >= landmarks.size()) continue;

            NormalizedLandmark landmark = landmarks.get(sticker.landmarkIndex);

            // Tọa độ normalized (0-1) → pixel
            float landmarkX = landmark.x() * viewW;
            float landmarkY = landmark.y() * viewH;

            // Mirror cho front camera
            if (isFrontCamera) {
                landmarkX = viewW - landmarkX;
            }

            // Kích thước sticker dựa trên kích thước khuôn mặt
            float stickerSize = faceHeight * sticker.scale;

            // Offset
            float offsetX = sticker.offsetX * faceHeight;
            float offsetY = sticker.offsetY * faceHeight;

            // Load và cache bitmap
            Bitmap bitmap = getCachedBitmap(sticker.drawableRes, (int) stickerSize);
            if (bitmap == null) continue;

            // Vẽ sticker (center tại vị trí landmark + offset)
            float drawX = landmarkX + offsetX - bitmap.getWidth() / 2f;
            float drawY = landmarkY + offsetY - bitmap.getHeight() / 2f;

            canvas.drawBitmap(bitmap, drawX, drawY, null);
        }
    }

    private Bitmap getCachedBitmap(int resId, int targetSize) {
        if (targetSize <= 0) targetSize = 100;

        // Key = resId * 10000 + targetSize (bucket by 10px)
        int sizeKey = (targetSize / 10) * 10;
        int cacheKey = resId * 10000 + sizeKey;

        Bitmap cached = bitmapCache.get(cacheKey);
        if (cached != null && !cached.isRecycled()) return cached;

        try {
            Bitmap original = BitmapFactory.decodeResource(getResources(), resId);
            if (original == null) return null;

            Bitmap scaled = Bitmap.createScaledBitmap(original, sizeKey, sizeKey, true);
            if (scaled != original) original.recycle();

            bitmapCache.put(cacheKey, scaled);
            return scaled;
        } catch (Exception e) {
            return null;
        }
    }

    /** Giải phóng bitmap khi view bị destroy */
    @Override
    protected void onDetachedFromWindow() {
        super.onDetachedFromWindow();
        for (int i = 0; i < bitmapCache.size(); i++) {
            Bitmap bmp = bitmapCache.valueAt(i);
            if (bmp != null && !bmp.isRecycled()) bmp.recycle();
        }
        bitmapCache.clear();
    }
}
