package com.example.mind.checkin.ui;

import android.app.AlertDialog;
import android.content.Context;

import com.example.mind.R;
import com.example.mind.auth.ApiClient;
import com.example.mind.common.UserPrefs;

/**
 * Dialog xác nhận chuyển từ PHQ-2 sang PHQ-9.
 *
 * Spec: "Để hiểu rõ hơn, mình muốn hỏi thêm vài câu nữa
 *        — chỉ khoảng 2 phút. Bạn có muốn tiếp tục không?"
 *
 * - "Tiếp tục" → mở Phq9Activity
 * - "Để sau" → gọi POST /phq/defer-phq9 (backend xử lý defer_count)
 *              + local đếm dự phòng offline.
 */
public class PhqTransitionDialog {

    public interface OnDecisionListener {
        void onContinue();
        void onDefer();
    }

    /** Backward-compatible overload (không có phq2Id). */
    public static void show(Context context, int phq2Score1, int phq2Score2,
                             OnDecisionListener listener) {
        show(context, phq2Score1, phq2Score2, null, listener);
    }

    public static void show(Context context, int phq2Score1, int phq2Score2,
                             String phq2Id, OnDecisionListener listener) {
        UserPrefs prefs = new UserPrefs(context);

        new AlertDialog.Builder(context)
                .setTitle(context.getString(R.string.phq9_title))
                .setMessage(context.getString(R.string.phq_transition_message))
                .setPositiveButton(context.getString(R.string.phq_transition_continue), (d, w) -> {
                    prefs.resetPhq9DeferCount();
                    if (listener != null) listener.onContinue();
                })
                .setNegativeButton(context.getString(R.string.phq_transition_later), (d, w) -> {
                    // Đếm local dự phòng offline — backend là nguồn chính.
                    prefs.incrementPhq9DeferCount();
                    int deferCount = prefs.getPhq9DeferCount();
                    if (deferCount >= 2) {
                        int currentLevel = prefs.getMonitoringLevel();
                        if (currentLevel < 2) {
                            prefs.setMonitoringLevel(2);
                        }
                    }

                    // Gọi backend để đồng bộ defer_count + nhắc lại đúng lịch.
                    new ApiClient(context).deferPhq9(phq2Id, new ApiClient.ApiCallback() {
                        @Override public void onSuccess(String r) { /* ok */ }
                        @Override public void onFailure(String e) { /* offline — local đã đếm */ }
                    });

                    if (listener != null) listener.onDefer();
                })
                .setCancelable(false)
                .show();
    }
}
