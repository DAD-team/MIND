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
import com.example.mind.checkin.data.MoodStorage;
import com.example.mind.checkin.data.PhqHistoryManager;
import com.example.mind.common.UserPrefs;
import com.example.mind.schedule.ScheduleStorage;
import com.example.mind.schedule.model.ScheduleItem;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.Calendar;
import java.util.List;

/**
 * PHQ-2: Sàng lọc nhanh 2 câu hỏi.
 *
 * Logic phân nhánh sau khi hoàn thành:
 *
 * Nhánh 1: Tổng >= 3 (Dương tính rõ ràng)
 *   → PhqTransitionDialog → Phq9Activity
 *
 * Nhánh 2: Tổng = 2 + >= 2 dấu hiệu hành vi
 *   → PhqTransitionDialog → Phq9Activity
 *
 * Nhánh 3: Tổng = 2 + < 2 dấu hiệu hành vi
 *   → Rút ngắn lịch PHQ-2 còn 7 ngày
 *
 * Nhánh 4: Tổng <= 1 (Âm tính)
 *   → Lịch PHQ-2 tiếp = 14 ngày
 */
public class Phq2Activity extends AppCompatActivity {

    private static final int[] QUESTION_ICONS = {
            R.drawable.icon_40, // câu 1: hứng thú
            R.drawable.icon_0   // câu 2: buồn
    };

    private static final int[] QUESTIONS = {
            R.string.phq2_q1, R.string.phq2_q2
    };

    private static final int[] OPTIONS = {
            R.string.phq_opt_0, R.string.phq_opt_1,
            R.string.phq_opt_2, R.string.phq_opt_3
    };

    private int currentIndex = 0;
    private final int[] scores = {-1, -1};

    /** Source gửi kèm khi submit — mapped từ pending.reason hoặc "self_request". */
    private String source = "self_request";
    /** ID pending (nếu có) — để backend biết clear đúng record. */
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

        String intentSource = getIntent().getStringExtra("source");
        if (intentSource != null && !intentSource.isEmpty()) {
            source = intentSource;
        }
        pendingId = getIntent().getStringExtra("pending_id");

        tvProgress = findViewById(R.id.tvProgress);
        progressBar = findViewById(R.id.progressBar);
        imgMindy = findViewById(R.id.imgMindy);
        tvQuestion = findViewById(R.id.tvQuestion);
        answersContainer = findViewById(R.id.answersContainer);
        btnNext = findViewById(R.id.btnNext);
        btnReject = findViewById(R.id.btnReject);

        buildAnswerButtons();
        showQuestion(0);

        btnNext.setOnClickListener(v -> onNext());

        // Cho user quyền từ chối khi PHQ là pending do backend/Scout trigger (có pendingId).
        // Ẩn khi user tự chủ động mở từ card "Kiểm tra tâm trạng" (self_request,
        // pendingId null) — user đã chủ động nên không cần nút từ chối.
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

        tvProgress.setText(getString(R.string.phq_progress, index + 1, 2));
        progressBar.setProgress((index + 1) * 50);
        imgMindy.setImageResource(QUESTION_ICONS[index]);

        if (index == 0) {
            tvQuestion.setText(getString(R.string.phq2_intro) + "\n\n" + getString(QUESTIONS[0]));
        } else {
            tvQuestion.setText(QUESTIONS[index]);
        }

        for (int i = 0; i < 4; i++) {
            answerViews[i].setText(OPTIONS[i]);
            answerViews[i].setSelected(scores[index] == i);
        }

        btnNext.setVisibility(scores[index] >= 0 ? View.VISIBLE : View.INVISIBLE);
        btnNext.setText(index == 1 ? R.string.phq_btn_finish : R.string.phq_btn_next);

        Animation fadeIn = new AlphaAnimation(0f, 1f);
        fadeIn.setDuration(250);
        answersContainer.startAnimation(fadeIn);
    }

    private void selectAnswer(int optIndex) {
        scores[currentIndex] = optIndex;
        for (int i = 0; i < 4; i++) {
            answerViews[i].setSelected(i == optIndex);
        }
        btnNext.setVisibility(View.VISIBLE);
        btnNext.setText(currentIndex == 1 ? R.string.phq_btn_finish : R.string.phq_btn_next);
    }

    private void onNext() {
        if (scores[currentIndex] < 0) {
            Toast.makeText(this, "Vui lòng chọn một đáp án", Toast.LENGTH_SHORT).show();
            return;
        }

        if (currentIndex < 1) {
            showQuestion(currentIndex + 1);
        } else {
            onFinish();
        }
    }

    private void onFinish() {
        int total = scores[0] + scores[1];
        UserPrefs prefs = new UserPrefs(this);
        PhqHistoryManager history = new PhqHistoryManager(this);

        // Cập nhật tương tác (tín hiệu 1 - khoảng im lặng)
        new MoodStorage(this).updateLastInteraction(System.currentTimeMillis());

        // Lưu kết quả local
        prefs.savePhq2Result(total);
        history.savePhq2Result(scores[0], scores[1], total, source);
        prefs.resetRejectionCount();

        // Gửi kết quả lên server → server phân tích và trả về quyết định
        submitAndWaitForDecision(total, prefs);
    }

    /**
     * Gửi kết quả PHQ-2 lên server → server phân tích (điểm + tín hiệu hành vi)
     * → trả về quyết định cho mobile thực thi.
     *
     * Server response JSON (theo FRONTEND_PHQ_PENDING.md):
     * {
     *   "id": "uuid",
     *   "created_at": "...",
     *   "decision": "escalate_phq9" | "shorten_interval" | "normal",
     *   "next_phq2_days": 7 | 14 | null
     * }
     */
    private void submitAndWaitForDecision(int total, UserPrefs prefs) {
        try {
            JSONObject json = new JSONObject();
            json.put("phq_type", "phq2");
            JSONArray arr = new JSONArray();
            arr.put(scores[0]);
            arr.put(scores[1]);
            json.put("scores", arr);
            json.put("total", total);
            json.put("source", source);

            new ApiClient(this).submitPhqResult(json.toString(), new ApiClient.ApiCallback() {
                @Override
                public void onSuccess(String responseBody) {
                    runOnUiThread(() -> handleServerDecision(responseBody, prefs));
                }

                @Override
                public void onFailure(String error) {
                    // Offline fallback: dùng logic local đơn giản
                    runOnUiThread(() -> {
                        if (total >= 3) {
                            showTransitionDialog(null);
                        } else {
                            Toast.makeText(Phq2Activity.this,
                                    R.string.phq2_toast_done, Toast.LENGTH_SHORT).show();
                            finish();
                        }
                    });
                }
            });
        } catch (Exception e) {
            Toast.makeText(this, R.string.phq2_toast_done, Toast.LENGTH_SHORT).show();
            finish();
        }
    }

    /** Thực thi quyết định từ server. */
    private void handleServerDecision(String responseBody, UserPrefs prefs) {
        try {
            JSONObject json = new JSONObject(responseBody);
            String decision = json.optString("decision", "normal");
            int nextPhq2Days = json.optInt("next_phq2_days", 14);
            String submittedPhq2Id = json.optString("id", null);

            prefs.setNextPhq2Days(nextPhq2Days);

            if ("escalate_phq9".equals(decision)) {
                // Backend đã tự tạo pending PHQ-9. Hiển thị dialog chuyển tiếp,
                // truyền kèm phq2_id để PHQ-9 gửi triggered_by_phq2_id khi submit.
                showTransitionDialog(submittedPhq2Id);
            } else {
                // normal hoặc shorten_interval → hoàn thành
                Toast.makeText(this, R.string.phq2_toast_done, Toast.LENGTH_SHORT).show();
                finish();
            }
        } catch (Exception e) {
            Toast.makeText(this, R.string.phq2_toast_done, Toast.LENGTH_SHORT).show();
            finish();
        }
    }

    private void showTransitionDialog(String submittedPhq2Id) {
        PhqTransitionDialog.show(this, scores[0], scores[1], submittedPhq2Id,
                new PhqTransitionDialog.OnDecisionListener() {
                    @Override
                    public void onContinue() {
                        Intent intent = new Intent(Phq2Activity.this, Phq9Activity.class);
                        intent.putExtra("phq2_score1", scores[0]);
                        intent.putExtra("phq2_score2", scores[1]);
                        intent.putExtra("triggered_by_phq2_id", submittedPhq2Id);
                        intent.putExtra("source", "phq2_escalation");
                        startActivity(intent);
                        finish();
                    }

                    @Override
                    public void onDefer() {
                        finish();
                    }
                });
    }

    /**
     * Back button: nếu đây là pending do backend trigger, PHẢI gọi /phq/reject
     * để backend snooze. Nếu không, GET /phq/pending lần sau sẽ trả về pending
     * này ngay → user bị hỏi lại liên tục.
     *
     * Theo FRONTEND_PHQ_PENDING spec (Bước 4): chỉ cần gọi API rồi đóng popup,
     * backend tự đếm rejection_count và tính thời điểm snooze tiếp theo.
     */
    @Override
    public void onBackPressed() {
        if (pendingId != null && !pendingId.isEmpty()) {
            sendReject(false);
            return;
        }
        super.onBackPressed();
    }

    /** Hỏi xác nhận trước khi gửi POST /phq/reject. */
    private void confirmReject() {
        new AlertDialog.Builder(this)
                .setTitle(R.string.phq_reject_confirm_title)
                .setMessage(R.string.phq_reject_confirm_message)
                .setPositiveButton(R.string.phq_reject_confirm_ok, (d, w) -> sendReject(true))
                .setNegativeButton(R.string.phq_reject_confirm_cancel, null)
                .show();
    }

    /**
     * Gửi POST /phq/reject. Backend là nguồn chính cho rejection_count —
     * frontend KHÔNG tự đếm (theo spec).
     *
     * @param showToast true nếu user chủ động bấm nút reject; false nếu back-press.
     */
    private void sendReject(boolean showToast) {
        new ApiClient(this).rejectPhq("phq2", new ApiClient.ApiCallback() {
            @Override
            public void onSuccess(String responseBody) {
                runOnUiThread(() -> {
                    if (showToast) {
                        boolean paused = false;
                        try {
                            paused = new JSONObject(responseBody).optBoolean("paused", false);
                        } catch (Exception ignored) {}
                        int msgRes = paused ? R.string.phq_reject_paused : R.string.phq_reject_done;
                        Toast.makeText(Phq2Activity.this, msgRes, Toast.LENGTH_SHORT).show();
                    }
                    finish();
                });
            }

            @Override
            public void onFailure(String error) {
                // Offline — đóng UI. Lần sau khi online, pending sẽ vẫn còn
                // (chấp nhận được — user có thể reject lại khi mạng khôi phục).
                runOnUiThread(() -> {
                    if (showToast) {
                        Toast.makeText(Phq2Activity.this,
                                R.string.phq_reject_done, Toast.LENGTH_SHORT).show();
                    }
                    finish();
                });
            }
        });
    }

    /**
     * Tính tổng trọng số áp lực học thuật trong 3 ngày tới.
     * Dùng dữ liệu lịch học đã load từ API (local cache via ScheduleStorage).
     * Trọng số: Thi=3, Deadline đồ án=2, Nộp bài=1, Thuyết trình=1, Học thường=0.
     */
    private int getAcademicPressureWeight() {
        // Tính dayOfWeek hôm nay (0=T2..6=CN) và 2 ngày tiếp theo
        Calendar cal = Calendar.getInstance();
        int javaDow = cal.get(Calendar.DAY_OF_WEEK); // 1=CN, 2=T2, ..., 7=T7
        // Chuyển sang 0=T2..6=CN
        int today = (javaDow == Calendar.SUNDAY) ? 6 : javaDow - 2;

        int[] next3Days = new int[3];
        for (int i = 0; i < 3; i++) {
            next3Days[i] = (today + i) % 7;
        }

        // Lấy lịch từ SharedPrefs cache (nếu đã load từ API trước đó)
        // Dùng sync call đơn giản — dữ liệu schedule đã có local
        final int[] totalWeight = {0};
        ScheduleStorage.load(this, new ScheduleStorage.ScheduleListCallback() {
            @Override
            public void onSuccess(List<ScheduleItem> items) {
                for (ScheduleItem item : items) {
                    for (int day : next3Days) {
                        if (item.dayOfWeek == day && item.eventType > 0) {
                            totalWeight[0] += item.getEventWeight();
                        }
                    }
                }
            }

            @Override
            public void onFailure(String error) {
                // Nếu API fail, bỏ qua tín hiệu này
            }
        });

        return totalWeight[0];
    }

    private int dpToPx(int dp) {
        return (int) (dp * getResources().getDisplayMetrics().density);
    }
}
