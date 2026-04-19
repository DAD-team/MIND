package com.example.mind.vr;

import android.animation.ValueAnimator;
import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.RectF;
import android.util.AttributeSet;
import android.view.View;
import android.view.animation.LinearInterpolator;

/**
 * Vòng tròn tiến trình quay video.
 * Viền chạy từ 0° → 360° trong duration cho trước.
 */
public class RecordProgressView extends View {

    private final Paint bgPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint progressPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF arcRect = new RectF();

    private float sweepAngle = 0f;
    private ValueAnimator animator;

    private Runnable onCompleteListener;

    public RecordProgressView(Context context) {
        super(context);
        init();
    }

    public RecordProgressView(Context context, AttributeSet attrs) {
        super(context, attrs);
        init();
    }

    private void init() {
        // Viền nền (xám mờ)
        bgPaint.setStyle(Paint.Style.STROKE);
        bgPaint.setStrokeWidth(6f);
        bgPaint.setColor(0x55FFFFFF);
        bgPaint.setStrokeCap(Paint.Cap.ROUND);

        // Viền tiến trình (đỏ)
        progressPaint.setStyle(Paint.Style.STROKE);
        progressPaint.setStrokeWidth(6f);
        progressPaint.setColor(0xFFFF4444);
        progressPaint.setStrokeCap(Paint.Cap.ROUND);
    }

    public void setOnCompleteListener(Runnable listener) {
        this.onCompleteListener = listener;
    }

    /** Bắt đầu animation vòng tròn chạy trong durationMs */
    public void startProgress(long durationMs) {
        sweepAngle = 0f;
        if (animator != null) animator.cancel();

        animator = ValueAnimator.ofFloat(0f, 360f);
        animator.setDuration(durationMs);
        animator.setInterpolator(new LinearInterpolator());
        animator.addUpdateListener(anim -> {
            sweepAngle = (float) anim.getAnimatedValue();
            invalidate();
        });
        animator.addListener(new android.animation.AnimatorListenerAdapter() {
            @Override
            public void onAnimationEnd(android.animation.Animator animation) {
                if (onCompleteListener != null) onCompleteListener.run();
            }
        });
        animator.start();
    }

    /** Reset về trạng thái ban đầu */
    public void reset() {
        if (animator != null) animator.cancel();
        sweepAngle = 0f;
        invalidate();
    }

    @Override
    protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);

        float stroke = progressPaint.getStrokeWidth();
        float pad = stroke / 2f + 2f;
        arcRect.set(pad, pad, getWidth() - pad, getHeight() - pad);

        // Vẽ viền nền (vòng tròn đầy đủ)
        canvas.drawOval(arcRect, bgPaint);

        // Vẽ viền tiến trình (từ 12h chạy theo chiều kim đồng hồ)
        if (sweepAngle > 0) {
            canvas.drawArc(arcRect, -90f, sweepAngle, false, progressPaint);
        }
    }

    @Override
    protected void onDetachedFromWindow() {
        super.onDetachedFromWindow();
        if (animator != null) animator.cancel();
    }
}
