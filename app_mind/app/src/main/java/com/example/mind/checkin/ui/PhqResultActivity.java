package com.example.mind.checkin.ui;

import android.content.Intent;
import android.os.Bundle;
import android.view.View;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;

import com.example.mind.MainActivity;
import com.example.mind.R;
import com.example.mind.checkin.data.MonitoringLevel;
import com.example.mind.checkin.data.PhqHistoryManager;
import com.example.mind.common.UserPrefs;

/**
 * Màn hình kết quả PHQ-9.
 *
 * Phân loại mức độ:
 *  0-4:  Tối thiểu → phản hồi tích cực
 *  5-9:  Nhẹ → gợi ý tự chăm sóc
 * 10-14: Trung bình → đề xuất liên hệ tư vấn viên
 * 15-19: Trung bình-nặng → khuyến nghị mạnh tư vấn viên
 * 20-27: Nặng → hotline khẩn cấp + cảnh báo tức thì
 */
public class PhqResultActivity extends AppCompatActivity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_phq_result);

        int totalScore = getIntent().getIntExtra("total_score", 0);
        int somaticScore = getIntent().getIntExtra("somatic_score", 0);
        int cognitiveScore = getIntent().getIntExtra("cognitive_score", 0);
        int q9Value = getIntent().getIntExtra("q9_value", 0);

        ImageView imgIcon = findViewById(R.id.imgResultIcon);
        TextView tvTitle = findViewById(R.id.tvResultTitle);
        TextView tvDesc = findViewById(R.id.tvResultDescription);
        TextView tvRecommendation = findViewById(R.id.tvRecommendation);
        TextView tvSomatic = findViewById(R.id.tvSomatic);
        TextView tvCognitive = findViewById(R.id.tvCognitive);
        LinearLayout cardEmergency = findViewById(R.id.cardEmergency);
        LinearLayout cardChange = findViewById(R.id.cardChange);
        TextView tvChangeTitle = findViewById(R.id.tvChangeTitle);
        TextView tvChangeDetail = findViewById(R.id.tvChangeDetail);

        // Điểm phụ
        tvSomatic.setText(somaticScore + "/12");
        tvCognitive.setText(cognitiveScore + "/15");

        // Phân loại mức độ
        if (totalScore <= 4) {
            imgIcon.setImageResource(R.drawable.icon_40);
            tvTitle.setText("Bạn đang ổn!");
            tvDesc.setText("Kết quả cho thấy bạn đang ở trạng thái tốt. Hãy tiếp tục giữ gìn sức khỏe nhé!");
            tvRecommendation.setText("Tiếp tục duy trì thói quen tốt:\n"
                    + "- Ngủ đủ giấc\n"
                    + "- Vận động nhẹ nhàng\n"
                    + "- Giữ liên lạc với bạn bè và gia đình");
        } else if (totalScore <= 9) {
            imgIcon.setImageResource(R.drawable.icon_4);
            tvTitle.setText("Có vài dấu hiệu nhẹ");
            tvDesc.setText("Bạn có một số biểu hiện nhẹ. Đừng lo, đây là điều rất bình thường và có thể cải thiện được.");
            tvRecommendation.setText("Một số gợi ý chăm sóc bản thân:\n"
                    + "- Giấc ngủ: cố gắng ngủ 7-8 tiếng mỗi đêm\n"
                    + "- Vận động: đi bộ 15-30 phút mỗi ngày\n"
                    + "- Chánh niệm: thử hít thở sâu 5 phút mỗi sáng\n"
                    + "- Kết nối: tâm sự với người bạn tin tưởng");
        } else if (totalScore <= 14) {
            imgIcon.setImageResource(R.drawable.icon_0);
            tvTitle.setText("Cần chú ý hơn");
            tvDesc.setText("Kết quả cho thấy bạn đang gặp một số khó khăn. Bạn không đơn độc — hãy để chúng mình hỗ trợ.");
            tvRecommendation.setText("Chúng mình khuyến khích bạn:\n"
                    + "- Liên hệ tư vấn viên trường để được hỗ trợ\n"
                    + "- Áp dụng kỹ thuật tự chăm sóc hàng ngày\n"
                    + "- Theo dõi tâm trạng thường xuyên qua app\n\n"
                    + "Tư vấn viên sẽ được thông báo trong 48 giờ tới.");
        } else if (totalScore <= 19) {
            imgIcon.setImageResource(R.drawable.icon_0);
            tvTitle.setText("Bạn cần được hỗ trợ");
            tvDesc.setText("Kết quả cho thấy bạn đang trải qua giai đoạn khó khăn. Hãy để chuyên gia hỗ trợ bạn.");
            tvRecommendation.setText("Bước tiếp theo:\n"
                    + "- Tư vấn viên sẽ được thông báo trong 24 giờ\n"
                    + "- Hãy liên hệ trực tiếp nếu bạn cần nói chuyện ngay\n"
                    + "- Đừng ngần ngại tìm kiếm sự giúp đỡ\n\n"
                    + "Bạn xứng đáng được chăm sóc.");
            cardEmergency.setVisibility(View.VISIBLE);
        } else {
            imgIcon.setImageResource(R.drawable.icon_0);
            tvTitle.setText("Chúng mình rất lo cho bạn");
            tvDesc.setText("Kết quả cho thấy bạn đang cần được hỗ trợ ngay. Bạn không đơn độc.");
            tvRecommendation.setText("Hành động ngay:\n"
                    + "- Tư vấn viên đã được cảnh báo TỨC THÌ\n"
                    + "- Nếu bạn cần hỗ trợ ngay, hãy gọi các số dưới đây\n"
                    + "- Hãy ở bên cạnh người bạn tin tưởng");
            cardEmergency.setVisibility(View.VISIBLE);
        }

        // Phát hiện thay đổi đáng kể (>= 5 điểm)
        PhqHistoryManager history = new PhqHistoryManager(this);
        int change = history.getPhq9Change();
        if (change >= 5) {
            cardChange.setVisibility(View.VISIBLE);
            tvChangeTitle.setText(R.string.result_change_worse);
            tvChangeDetail.setText("Điểm của bạn tăng " + change + " điểm so với lần trước. "
                    + "Tư vấn viên sẽ được thông báo về xu hướng này.");
        } else if (change <= -5) {
            cardChange.setVisibility(View.VISIBLE);
            tvChangeTitle.setText(R.string.result_change_better);
            tvChangeDetail.setText("Điểm của bạn giảm " + Math.abs(change) + " điểm so với lần trước. "
                    + "Bạn đang tiến triển rất tốt!");
        }

        // Cập nhật monitoring level
        UserPrefs userPrefs = new UserPrefs(this);
        int newLevel = MonitoringLevel.fromPhq9Score(totalScore, q9Value);
        userPrefs.setMonitoringLevel(newLevel);

        // Kiểm tra xuống thang
        if (history.shouldDeescalate()) {
            userPrefs.setMonitoringLevel(MonitoringLevel.STANDARD);
            userPrefs.setNextPhq2Days(14);
        }

        // Nút về Home
        findViewById(R.id.btnBackHome).setOnClickListener(v -> {
            Intent intent = new Intent(this, MainActivity.class);
            intent.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
            startActivity(intent);
            finish();
        });
    }

    @Override
    public void onBackPressed() {
        // Chặn back → phải bấm nút "Về trang chủ"
        Intent intent = new Intent(this, MainActivity.class);
        intent.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
        startActivity(intent);
        finish();
    }
}
