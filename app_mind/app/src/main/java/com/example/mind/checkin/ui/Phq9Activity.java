package com.example.mind.checkin.ui;

import android.app.AlertDialog;
import android.content.Intent;
import android.os.Bundle;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.animation.AlphaAnimation;
import android.view.animation.Animation;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;
import androidx.core.content.res.ResourcesCompat;

import com.example.mind.R;
import com.example.mind.auth.ApiClient;
import com.example.mind.checkin.data.MonitoringLevel;
import com.example.mind.checkin.data.PhqHistoryManager;
import com.example.mind.checkin.data.PhqScoreCalculator;
import com.example.mind.common.UserPrefs;

import org.json.JSONArray;
import org.json.JSONObject;

/**
 * PHQ-9: Đánh giá sâu 8 câu hỏi (Q3-Q9 scored + Q10 functional).
 *
 * Nhận điểm câu 1-2 từ intent extras (phq2_score1, phq2_score2).
 *
 * Giao thức an toàn câu 9: phân tầng theo mức 1/2/3.
 * Kết thúc → PhqResultActivity (hiển thị mức độ + khuyến nghị).
 */
public class Phq9Activity extends AppCompatActivity {

    private static final int[] QUESTION_ICONS = {
            R.drawable.icon_0,  // Q3: Giấc ngủ
            R.drawable.icon_4,  // Q4: Mệt mỏi
            R.drawable.icon_37, // Q5: Ăn uống
            R.drawable.icon_40, // Q6: Tự đánh giá
            R.drawable.icon_2,  // Q7: Tập trung
            R.drawable.icon_39, // Q8: Cử động
            R.drawable.icon_38, // Q9: Tự hại
            R.drawable.icon_1   // Q10: Functional
    };

    private static final int[] QUESTIONS = {
            R.string.phq9_q3, R.string.phq9_q4, R.string.phq9_q5,
            R.string.phq9_q6, R.string.phq9_q7, R.string.phq9_q8,
            R.string.phq9_q9, R.string.phq9_q10
    };

    private static final int[] STANDARD_OPTIONS = {
            R.string.phq_opt_0, R.string.phq_opt_1,
            R.string.phq_opt_2, R.string.phq_opt_3
    };

    private static final int[] Q10_OPTIONS = {
            R.string.phq9_q10_opt_0, R.string.phq9_q10_opt_1,
            R.string.phq9_q10_opt_2, R.string.phq9_q10_opt_3
    };

    private static final int TOTAL_QUESTIONS = 8;

    private int currentIndex = 0;
    private final int[] scores = new int[TOTAL_QUESTIONS];
    private int phq2Score1, phq2Score2;

    /** Source gửi kèm khi submit. Mặc định phq2_escalation; override nếu intent truyền khác. */
    private String source = "phq2_escalation";
    /** ID của PHQ-2 đã trigger escalation (nếu có) — gửi kèm submit. */
    private String triggeredByPhq2Id = null;
    /** ID pending PHQ-9 từ backend (nếu có). */
    private String pendingId = null;

    private TextView tvProgress;
    private ProgressBar progressBar;
    private ImageView imgMindy;
    private TextView tvQuestion;
    private LinearLayout answersContainer;
    private TextView btnNext;
    private TextView btnReject;
    private final TextView[] answerViews = new TextView[4];

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_phq);

        tvProgress = findViewById(R.id.tvProgress);
        progressBar = findViewById(R.id.progressBar);
        imgMindy = findViewById(R.id.imgMindy);
        tvQuestion = findViewById(R.id.tvQuestion);
        answersContainer = findViewById(R.id.answersContainer);
        btnNext = findViewById(R.id.btnNext);
        btnReject = findViewById(R.id.btnReject);

        phq2Score1 = getIntent().getIntExtra("phq2_score1", 0);
        phq2Score2 = getIntent().getIntExtra("phq2_score2", 0);

        String intentSource = getIntent().getStringExtra("source");
        if (intentSource != null && !intentSource.isEmpty()) {
            source = intentSource;
        }
        triggeredByPhq2Id = getIntent().getStringExtra("triggered_by_phq2_id");
        pendingId = getIntent().getStringExtra("pending_id");

        for (int i = 0; i < TOTAL_QUESTIONS; i++) scores[i] = -1;

        buildAnswerButtons();
        showQuestion(0);

        btnNext.setOnClickListener(v -> onNext());

        // Cho user quyền từ chối khi PHQ-9 là pending do backend trigger (có pendingId).
        // Ẩn khi user vừa hoàn thành PHQ-2 và đang trong flow escalation liền mạch
        // (pendingId null vì chưa refresh /phq/pending).
        if (pendingId != null && !pendingId.isEmpty()) {
            btnReject.setVisibility(View.VISIBLE);
            btnReject.setOnClickListener(v -> confirmReject());
        }
    }

    private void buildAnswerButtons() {
        for (int i = 0; i < 4; i++) {
            TextView tv = new TextView(this);
            tv.setBackgroundResource(R.drawable.bg_phq_answer);
            tv.setTextColor(getColor(R.color.text_primary));
            tv.setTextSize(TypedValue.COMPLEX_UNIT_SP, 15);
            tv.setTypeface(ResourcesCompat.getFont(this, R.font.nunito));
            tv.setGravity(Gravity.CENTER);
            int pad = dpToPx(14);
            tv.setPadding(pad, 0, pad, 0);

            LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT, dpToPx(52));
            params.topMargin = dpToPx(i == 0 ? 0 : 10);
            tv.setLayoutParams(params);

            final int optIndex = i;
            tv.setOnClickListener(v -> selectAnswer(optIndex));

            answerViews[i] = tv;
            answersContainer.addView(tv);
        }
    }

    private void showQuestion(int index) {
        currentIndex = index;

        tvProgress.setText(getString(R.string.phq_progress, index + 1, TOTAL_QUESTIONS));
        progressBar.setProgress((int) ((index + 1) * 100f / TOTAL_QUESTIONS));
        imgMindy.setImageResource(QUESTION_ICONS[index]);
        tvQuestion.setText(QUESTIONS[index]);

        boolean isQ10 = index == TOTAL_QUESTIONS - 1;
        int[] opts = isQ10 ? Q10_OPTIONS : STANDARD_OPTIONS;

        for (int i = 0; i < 4; i++) {
            answerViews[i].setText(opts[i]);
            answerViews[i].setSelected(scores[index] == i);
        }

        boolean isLast = index == TOTAL_QUESTIONS - 1;
        btnNext.setText(isLast ? R.string.phq_btn_finish : R.string.phq_btn_next);
        btnNext.setVisibility(scores[index] >= 0 ? View.VISIBLE : View.INVISIBLE);

        Animation fadeIn = new AlphaAnimation(0f, 1f);
        fadeIn.setDuration(250);
        answersContainer.startAnimation(fadeIn);
    }

    private void selectAnswer(int optIndex) {
        scores[currentIndex] = optIndex;
        for (int i = 0; i < 4; i++) {
            answerViews[i].setSelected(i == optIndex);
        }

        boolean isLast = currentIndex == TOTAL_QUESTIONS - 1;
        btnNext.setText(isLast ? R.string.phq_btn_finish : R.string.phq_btn_next);
        btnNext.setVisibility(View.VISIBLE);

        // Giao thức an toàn: câu 9 (index 6) — tự hại
        if (currentIndex == 6 && optIndex >= 1) {
            showSafetyInfo(optIndex);
        }
    }

    private void onNext() {
        if (scores[currentIndex] < 0) {
            Toast.makeText(this, "Vui lòng chọn một đáp án", Toast.LENGTH_SHORT).show();
            return;
        }

        if (currentIndex < TOTAL_QUESTIONS - 1) {
            showQuestion(currentIndex + 1);
        } else {
            onFinish();
        }
    }

    private void onFinish() {
        int totalPhq9 = PhqScoreCalculator.total(phq2Score1, phq2Score2, scores);
        int somaticScore = PhqScoreCalculator.somatic(scores);
        int cognitiveScore = PhqScoreCalculator.cognitive(phq2Score1, phq2Score2, scores);
        int functionalImpact = PhqScoreCalculator.functionalImpact(scores);
        int q9Value = PhqScoreCalculator.q9Value(scores);

        // Cập nhật tương tác (tín hiệu 1 - khoảng im lặng)
        new com.example.mind.checkin.data.MoodStorage(this)
                .updateLastInteraction(System.currentTimeMillis());

        // Lưu kết quả vào UserPrefs (backward compat)
        UserPrefs prefs = new UserPrefs(this);
        prefs.savePhq9Result(totalPhq9);
        prefs.resetRejectionCount();
        prefs.resetPhq9DeferCount();

        // Lưu vào lịch sử PHQ (nhiều lần)
        PhqHistoryManager history = new PhqHistoryManager(this);
        int[] allScores = PhqScoreCalculator.buildAllScores(phq2Score1, phq2Score2, scores);
        history.savePhq9Result(totalPhq9, somaticScore, cognitiveScore,
                functionalImpact, q9Value, allScores, source);

        // Giao thức an toàn câu 9 — lưu sự kiện an toàn
        if (q9Value >= 1) {
            history.saveSafetyEvent(q9Value, totalPhq9);
        }

        // Cập nhật monitoring level
        int newLevel = MonitoringLevel.fromPhq9Score(totalPhq9, q9Value);
        prefs.setMonitoringLevel(newLevel);

        // ─── Sync lên server ───
        syncPhq9ToServer(totalPhq9, somaticScore, cognitiveScore,
                functionalImpact, q9Value, allScores);
        if (q9Value >= 1) {
            syncSafetyEventToServer(q9Value, totalPhq9);
        }
        syncMonitoringToServer(newLevel, totalPhq9, q9Value);

        // Chuyển sang màn hình kết quả
        Intent intent = new Intent(this, PhqResultActivity.class);
        intent.putExtra("total_score", totalPhq9);
        intent.putExtra("somatic_score", somaticScore);
        intent.putExtra("cognitive_score", cognitiveScore);
        intent.putExtra("q9_value", q9Value);
        startActivity(intent);
        finish();
    }

    /**
     * Giao thức an toàn câu 9 — phân tầng theo mức độ.
     *
     * Câu 9 = 1: Ý tưởng thụ động → thông báo tư vấn viên trong 24h
     * Câu 9 = 2: Ý tưởng chủ động → thông báo trong ngày
     * Câu 9 = 3: Khủng hoảng → tức thì + liên hệ trực tiếp
     */
    private void showSafetyInfo(int q9Value) {
        String title;
        String extraMessage = "";

        if (q9Value >= 3) {
            title = "Bạn cần hỗ trợ ngay";
            extraMessage = "\n\nTư vấn viên của bạn đã được thông báo TỨC THÌ "
                    + "và sẽ liên hệ trực tiếp với bạn.";
        } else if (q9Value == 2) {
            title = "Bạn không đơn độc";
            extraMessage = "\n\nTư vấn viên sẽ được thông báo trong ngày hôm nay.";
        } else {
            title = "Bạn không đơn độc";
            extraMessage = "\n\nTư vấn viên sẽ được thông báo trong 24 giờ tới.";
        }

        new AlertDialog.Builder(this)
                .setTitle(title)
                .setMessage("Nếu bạn cần hỗ trợ ngay, hãy liên hệ:\n\n"
                        + "Tổng đài sức khỏe tâm thần: 1800 599 920\n"
                        + "Đường dây nóng thanh thiếu niên: 1800 599 100\n"
                        + "Cấp cứu: 115\n\n"
                        + "Hoặc liên hệ tư vấn viên của trường."
                        + extraMessage)
                .setPositiveButton("Đã hiểu", null)
                .setCancelable(false)
                .show();
    }

    // ─── Server sync helpers ───

    private void syncPhq9ToServer(int total, int somatic, int cognitive,
                                   int functional, int q9, int[] allScores) {
        try {
            JSONObject json = new JSONObject();
            json.put("phq_type", "phq9");
            JSONArray arr = new JSONArray();
            for (int s : allScores) arr.put(s);
            json.put("scores", arr);
            json.put("total", total);
            json.put("somatic_score", somatic);
            json.put("cognitive_score", cognitive);
            json.put("functional_impact", functional);
            json.put("q9_value", q9);
            json.put("source", source);
            if (triggeredByPhq2Id != null && !triggeredByPhq2Id.isEmpty()) {
                json.put("triggered_by_phq2_id", triggeredByPhq2Id);
            }

            new ApiClient(this).submitPhqResult(json.toString(), new ApiClient.ApiCallback() {
                @Override public void onSuccess(String r) { /* ok */ }
                @Override public void onFailure(String e) { /* local đã lưu */ }
            });
        } catch (Exception ignored) {}
    }

    /** Hỏi xác nhận trước khi gửi POST /phq/reject cho PHQ-9. */
    /**
     * Back button: nếu đây là pending do backend trigger, PHẢI gọi /phq/reject
     * để backend snooze. Không gọi → GET /phq/pending lần sau trả về pending ngay.
     */
    @Override
    public void onBackPressed() {
        if (pendingId != null && !pendingId.isEmpty()) {
            sendReject(false);
            return;
        }
        super.onBackPressed();
    }

    private void confirmReject() {
        new AlertDialog.Builder(this)
                .setTitle(R.string.phq_reject_confirm_title)
                .setMessage(R.string.phq_reject_confirm_message)
                .setPositiveButton(R.string.phq_reject_confirm_ok, (d, w) -> sendReject(true))
                .setNegativeButton(R.string.phq_reject_confirm_cancel, null)
                .show();
    }

    /**
     * Gửi POST /phq/reject. Backend tự tính rejection_count, paused, show_after —
     * frontend không đếm local (theo FRONTEND_PHQ_PENDING spec Bước 4).
     *
     * @param showToast true nếu user chủ động; false nếu back-press.
     */
    private void sendReject(boolean showToast) {
        new ApiClient(this).rejectPhq("phq9", new ApiClient.ApiCallback() {
            @Override
            public void onSuccess(String responseBody) {
                runOnUiThread(() -> {
                    if (showToast) {
                        boolean paused = false;
                        try {
                            paused = new JSONObject(responseBody).optBoolean("paused", false);
                        } catch (Exception ignored) {}
                        int msgRes = paused ? R.string.phq_reject_paused : R.string.phq_reject_done;
                        Toast.makeText(Phq9Activity.this, msgRes, Toast.LENGTH_SHORT).show();
                    }
                    finish();
                });
            }

            @Override
            public void onFailure(String error) {
                runOnUiThread(() -> {
                    if (showToast) {
                        Toast.makeText(Phq9Activity.this,
                                R.string.phq_reject_done, Toast.LENGTH_SHORT).show();
                    }
                    finish();
                });
            }
        });
    }

    private void syncSafetyEventToServer(int q9Value, int phq9Total) {
        try {
            JSONObject json = new JSONObject();
            json.put("q9_value", q9Value);
            json.put("phq9_total", phq9Total);

            new ApiClient(this).submitSafetyEvent(json.toString(), new ApiClient.ApiCallback() {
                @Override public void onSuccess(String r) { /* ok */ }
                @Override public void onFailure(String e) { /* local đã lưu */ }
            });
        } catch (Exception ignored) {}
    }

    private void syncMonitoringToServer(int level, int phq9Total, int q9Value) {
        try {
            JSONObject json = new JSONObject();
            json.put("level", level);
            json.put("reason", "phq9_score_" + phq9Total);
            json.put("phq9_total", phq9Total);
            json.put("q9_value", q9Value);

            new ApiClient(this).updateMonitoringLevel(json.toString(), new ApiClient.ApiCallback() {
                @Override public void onSuccess(String r) { /* ok */ }
                @Override public void onFailure(String e) { /* local đã lưu */ }
            });
        } catch (Exception ignored) {}
    }

    private int dpToPx(int dp) {
        return (int) (dp * getResources().getDisplayMetrics().density);
    }
}
