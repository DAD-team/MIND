package com.example.mind.checkin.ui;

import android.app.Dialog;
import android.content.Context;
import android.content.Intent;
import android.view.Gravity;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.widget.LinearLayout;
import android.widget.Toast;

import com.example.mind.R;
import com.example.mind.common.UserPrefs;
import com.example.mind.vr.VideoRecordActivity;

/**
 * Dialog check-in cảm xúc nhanh — 6 loại cảm xúc với icon cartoon.
 * Mapping: 1=Vui vẻ, 2=Buồn, 3=Căng thẳng, 4=Hào hứng, 5=Bình thường, 6=Mệt mỏi
 * Nếu consent >= 2, hiển thị thêm nút quay video.
 */
public class CheckinDialog {

    public interface OnCheckinListener {
        void onCheckinDone(int moodLevel);
    }

    // Mood IDs: phải khớp thứ tự trong layout
    private static final int[] MOOD_VIEW_IDS = {
            R.id.moodHappy,     // 1
            R.id.moodSad,       // 2
            R.id.moodStressed,  // 3
            R.id.moodExcited,   // 4
            R.id.moodNeutral,   // 5
            R.id.moodTired      // 6
    };

    private final Context context;
    private final OnCheckinListener listener;
    private Dialog dialog;
    private int selectedMood = 0;

    private final LinearLayout[] moodBtns = new LinearLayout[6];

    public CheckinDialog(Context context, OnCheckinListener listener) {
        this.context = context;
        this.listener = listener;
    }

    public void show() {
        dialog = new Dialog(context);
        dialog.requestWindowFeature(Window.FEATURE_NO_TITLE);
        dialog.setContentView(R.layout.dialog_checkin);
        dialog.setCancelable(true);

        // Make dialog appear at bottom with full width
        Window window = dialog.getWindow();
        if (window != null) {
            window.setLayout(WindowManager.LayoutParams.MATCH_PARENT,
                    WindowManager.LayoutParams.WRAP_CONTENT);
            window.setGravity(Gravity.BOTTOM);
            window.setBackgroundDrawableResource(android.R.color.transparent);
        }

        // Bind mood buttons
        for (int i = 0; i < MOOD_VIEW_IDS.length; i++) {
            moodBtns[i] = dialog.findViewById(MOOD_VIEW_IDS[i]);
            final int mood = i + 1;
            moodBtns[i].setOnClickListener(v -> selectMood(mood));
        }

        // Video button — only if consent >= 2
        View btnVideo = dialog.findViewById(R.id.btnCheckinVideo);
        UserPrefs prefs = new UserPrefs(context);
        if (prefs.getConsentLevel() >= 2) {
            btnVideo.setVisibility(View.VISIBLE);
            btnVideo.setOnClickListener(v -> {
                context.startActivity(new Intent(context, VideoRecordActivity.class));
            });
        }

        // Confirm button
        dialog.findViewById(R.id.btnCheckinConfirm).setOnClickListener(v -> {
            if (selectedMood == 0) {
                Toast.makeText(context, R.string.checkin_toast_select, Toast.LENGTH_SHORT).show();
                return;
            }

            Toast.makeText(context, R.string.checkin_toast_done, Toast.LENGTH_SHORT).show();

            if (listener != null) {
                listener.onCheckinDone(selectedMood);
            }
            dialog.dismiss();
        });

        dialog.show();
    }

    private void selectMood(int mood) {
        selectedMood = mood;
        for (int i = 0; i < 6; i++) {
            moodBtns[i].setSelected(i + 1 == mood);
        }
    }
}
