package com.example.mind.chat;

import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.widget.EditText;

import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;

import com.example.mind.R;
import com.example.mind.auth.ApiClient;
import com.example.mind.checkin.data.MoodStorage;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;
import java.util.Random;

/**
 * Chat với Mindy — kênh thu thập cảm xúc gián tiếp.
 *
 * Flow:
 * 1. Mindy chào → gửi mood picker (6 icon)
 * 2. Sinh viên chọn mood → Mindy phản hồi → hỏi câu hỏi đời sống
 * 3. Sinh viên trả lời text → Mindy phân tích sentiment cơ bản → phản hồi
 * 4. Mindy tiếp tục hỏi câu khác chiều cảm xúc (không lặp)
 *
 * CÂU HỎI CỦA MINDY KHÁC HOÀN TOÀN VỚI PHQ:
 * - PHQ: "Ít hứng thú hoặc niềm vui khi làm việc" (y khoa)
 * - Mindy: "Dạo này có gì vui vui kể Mindy nghe đi!" (bạn bè)
 * - Cùng đo mức hứng thú, nhưng câu từ hoàn toàn khác
 */
public class ChatActivity extends AppCompatActivity {

    private final List<ChatMessage> messages = new ArrayList<>();
    private ChatAdapter adapter;
    private RecyclerView rvMessages;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Random random = new Random();

    private int lastDimension = -1; // chiều cảm xúc vừa hỏi, tránh lặp
    private int questionCount = 0;  // đếm số câu đã hỏi trong phiên

    private static final String[] MOOD_NAMES = {
            "", "Vui vẻ", "Buồn", "Căng thẳng", "Hào hứng", "Bình thường", "Mệt mỏi"
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_chat);

        findViewById(R.id.btnBack).setOnClickListener(v -> finish());

        rvMessages = findViewById(R.id.rvMessages);
        adapter = new ChatAdapter(messages);
        LinearLayoutManager lm = new LinearLayoutManager(this);
        lm.setStackFromEnd(true);
        rvMessages.setLayoutManager(lm);
        rvMessages.setAdapter(adapter);

        // Mood picker handler
        adapter.setMoodListener((position, mood) -> {
            // Lưu mood vào MoodStorage + cập nhật tương tác
            MoodStorage moodStorage = new MoodStorage(this);
            moodStorage.saveMood(mood, false);

            handler.postDelayed(() -> {
                // Phản hồi theo mood
                addMindyMessage(getMoodReply(mood));

                // Sau đó hỏi câu hỏi đời sống đầu tiên
                handler.postDelayed(() -> askNextQuestion(), 1500);
            }, 800);
        });

        // Welcome flow
        addMindyMessage("Chào bạn! Mình là Mindy~");

        handler.postDelayed(() -> {
            ChatMessage moodMsg = new ChatMessage(
                    "Hôm nay bạn đang cảm thấy thế nào nè?",
                    ChatMessage.TYPE_MOOD_PICKER
            );
            messages.add(moodMsg);
            adapter.notifyItemInserted(messages.size() - 1);
            scrollToBottom();
        }, 600);

        // Text input handler
        EditText edtMessage = findViewById(R.id.edtMessage);
        findViewById(R.id.btnSend).setOnClickListener(v -> {
            String text = edtMessage.getText().toString().trim();
            if (text.isEmpty()) return;

            messages.add(new ChatMessage(text, ChatMessage.TYPE_USER));
            adapter.notifyItemInserted(messages.size() - 1);
            scrollToBottom();
            edtMessage.setText("");

            // Phân tích sentiment cơ bản
            handler.postDelayed(() -> {
                boolean positive = analyzePositive(text);

                // Phản hồi theo chiều vừa hỏi + sentiment
                if (lastDimension >= 0) {
                    addMindyMessage(MindyQuestions.getReply(lastDimension, positive));
                    // Lưu tương tác → cập nhật thời điểm hoạt động
                    new MoodStorage(ChatActivity.this).updateLastInteraction(System.currentTimeMillis());
                } else {
                    addMindyMessage(getGenericReply(text));
                }

                // Hỏi câu tiếp (tối đa 3 câu/phiên, không spam)
                if (questionCount < 3) {
                    handler.postDelayed(() -> askNextQuestion(), 2000);
                }
            }, 800);
        });
    }

    /**
     * Hỏi câu hỏi tiếp theo — chiều khác với lần trước.
     */
    private void askNextQuestion() {
        MindyQuestions.QuestionPick pick = (lastDimension < 0)
                ? MindyQuestions.getRandomQuestion()
                : MindyQuestions.getNextQuestion(lastDimension);

        lastDimension = pick.dimension;
        questionCount++;
        addMindyMessage(pick.question);
    }

    private String getMoodReply(int mood) {
        switch (mood) {
            case 1: return "Yay! Vui quá, Mindy cũng vui lây nè!";
            case 2: return "Mindy ở đây nè, cậu cứ tâm sự thoải mái nha~";
            case 3: return "Hít thở sâu nào... Cậu có muốn chia sẻ gì không?";
            case 4: return "Wow năng lượng dồi dào ghê! Kể nghe đi!";
            case 5: return "Oke oke, ngày bình yên cũng là ngày tốt mà~";
            case 6: return "Mệt rồi hả? Cậu nhớ nghỉ ngơi nha!";
            default: return "Mindy nghe nè!";
        }
    }

    /**
     * Phân tích sentiment đơn giản từ text.
     */
    private boolean analyzePositive(String text) {
        String lower = text.toLowerCase();

        // Từ tích cực
        String[] positiveWords = {
                "vui", "tốt", "ổn", "ngon", "khỏe", "thích", "yêu",
                "giỏi", "hay", "tuyệt", "sảng khoái", "năng lượng",
                "ok", "được", "rồi", "có", "nhiều", "lắm"
        };
        // Từ tiêu cực
        String[] negativeWords = {
                "buồn", "chán", "mệt", "không", "chưa", "khó", "tệ",
                "ít", "thiếu", "mất", "stress", "áp lực", "lo",
                "sợ", "một mình", "cô đơn", "đau"
        };

        int posCount = 0, negCount = 0;
        for (String w : positiveWords) if (lower.contains(w)) posCount++;
        for (String w : negativeWords) if (lower.contains(w)) negCount++;

        return posCount >= negCount;
    }

    private String getGenericReply(String text) {
        String lower = text.toLowerCase();
        if (lower.contains("cảm ơn") || lower.contains("thank")) {
            return "Hehe không có gì đâu, Mindy luôn ở đây mà!";
        }
        String[] fallbacks = {
                "Mindy nghe nè, kể thêm cho Mindy đi!",
                "Uu nghe hay ghê, rồi sao nữa?",
                "Cậu có muốn tâm sự thêm gì không?",
                "Mindy luôn ở đây nếu cậu cần!"
        };
        return fallbacks[random.nextInt(fallbacks.length)];
    }

    private void addMindyMessage(String text) {
        messages.add(new ChatMessage(text, ChatMessage.TYPE_MINDY));
        adapter.notifyItemInserted(messages.size() - 1);
        scrollToBottom();
    }

    private void scrollToBottom() {
        if (!messages.isEmpty()) {
            rvMessages.scrollToPosition(messages.size() - 1);
        }
    }

    /** Sync toàn bộ hội thoại lên server khi rời màn hình */
    @Override
    protected void onDestroy() {
        super.onDestroy();
        if (messages.size() > 1) { // có ít nhất 1 tin nhắn thật (ngoài greeting)
            syncChatToServer();
        }
    }

    private void syncChatToServer() {
        try {
            JSONArray msgArray = new JSONArray();
            for (ChatMessage msg : messages) {
                if (msg.type == ChatMessage.TYPE_MOOD_PICKER) continue;
                JSONObject m = new JSONObject();
                m.put("role", msg.type == ChatMessage.TYPE_USER ? "user" : "bot");
                m.put("content", msg.text);
                msgArray.put(m);
            }

            JSONObject json = new JSONObject();
            json.put("messages", msgArray);
            json.put("duration_seconds", questionCount * 30); // ước lượng

            new ApiClient(this).submitChatInteraction(json.toString(), new ApiClient.ApiCallback() {
                @Override public void onSuccess(String r) { /* ok */ }
                @Override public void onFailure(String e) { /* offline ok */ }
            });
        } catch (Exception ignored) {}
    }
}
