package com.example.mind.chat;

import java.util.Random;

/**
 * Bộ câu hỏi của Mindy — thu thập cảm xúc gián tiếp qua trò chuyện.
 *
 * NGUYÊN TẮC:
 * - KHÔNG BAO GIỜ dùng từ ngữ giống PHQ (không dùng "trầm uất", "tuyệt vọng",
 *   "tự hại", "thất bại", "bồn chồn")
 * - Hỏi về CUỘC SỐNG HÀNG NGÀY, không hỏi về TRIỆU CHỨNG
 * - Giọng bạn bè, vui vẻ, nhẹ nhàng
 * - Mỗi câu hỏi map đến 1 chiều cảm xúc (dimension) để phân tích
 *
 * Dữ liệu từ chat Mindy dùng cho Tầng 1 (tín hiệu hành vi).
 * Dữ liệu từ PHQ dùng cho Tầng 2-3 (sàng lọc lâm sàng).
 * Hai luồng KHÔNG TRỘN LẪN.
 */
public class MindyQuestions {

    // ─── Chiều cảm xúc (emotion dimensions) ───
    public static final int DIM_INTEREST = 0;    // Mức hứng thú / niềm vui
    public static final int DIM_MOOD = 1;         // Tâm trạng chung
    public static final int DIM_SLEEP = 2;        // Giấc ngủ
    public static final int DIM_ENERGY = 3;       // Năng lượng
    public static final int DIM_APPETITE = 4;     // Ăn uống
    public static final int DIM_FOCUS = 5;        // Tập trung
    public static final int DIM_SOCIAL = 6;       // Kết nối xã hội
    public static final int DIM_SELFCARE = 7;     // Chăm sóc bản thân

    public static final String[] DIM_NAMES = {
            "interest", "mood", "sleep", "energy",
            "appetite", "focus", "social", "selfcare"
    };

    // ─── Câu hỏi theo từng chiều ───
    // Mỗi chiều có nhiều biến thể → Mindy không hỏi lặp lại

    private static final String[][] QUESTIONS = {
            // DIM_INTEREST — Hứng thú
            {
                    "Dạo này có gì vui vui kể Mindy nghe đi!",
                    "Tuần này cậu có làm gì thú vị không?",
                    "Có hoạt động nào cậu đang háo hức muốn làm không?",
                    "Cậu dạo này có thích làm gì vào lúc rảnh?"
            },
            // DIM_MOOD — Tâm trạng
            {
                    "Hôm nay trời đẹp ghê, tâm trạng cậu thế nào?",
                    "Cuối tuần rồi cậu làm gì cho vui nè?",
                    "Cậu ơi, hôm nay có chuyện gì đặc biệt không?",
                    "Mindy tò mò quá, ngày hôm nay của cậu thế nào rồi?"
            },
            // DIM_SLEEP — Giấc ngủ
            {
                    "Tối qua cậu ngủ có ngon giấc không?",
                    "Dạo này cậu ngủ đủ giấc chưa?",
                    "Cậu hay đi ngủ lúc mấy giờ vậy?",
                    "Sáng nay thức dậy có thấy sảng khoái không?"
            },
            // DIM_ENERGY — Năng lượng
            {
                    "Hôm nay cậu có năng lượng để làm gì nè?",
                    "Cậu thấy mình hôm nay có khỏe không?",
                    "Pin của cậu hôm nay đang bao nhiêu phần trăm?",
                    "Cậu có thấy mình hoạt bát hôm nay không?"
            },
            // DIM_APPETITE — Ăn uống
            {
                    "Cậu ơi, trưa nay ăn gì ngon không?",
                    "Dạo này cậu có ăn uống đều đặn không?",
                    "Cậu có món nào thích ăn dạo này không?",
                    "Sáng nay cậu có ăn sáng chưa?"
            },
            // DIM_FOCUS — Tập trung
            {
                    "Dạo này học hành thế nào rồi cậu?",
                    "Cậu có tập trung được khi học không?",
                    "Hôm nay cậu làm được nhiều việc chưa?",
                    "Cậu có thấy dễ bị phân tâm dạo này không?"
            },
            // DIM_SOCIAL — Kết nối xã hội
            {
                    "Cậu có gặp bạn bè hôm nay không?",
                    "Dạo này cậu có hay đi chơi với ai không?",
                    "Cậu có ai để tâm sự khi cần không?",
                    "Tuần này cậu có giao lưu với ai vui không?"
            },
            // DIM_SELFCARE — Chăm sóc bản thân
            {
                    "Cậu có dành thời gian cho bản thân hôm nay chưa?",
                    "Dạo này cậu có tập thể dục gì không?",
                    "Cậu có làm gì để thư giãn dạo này không?",
                    "Hôm nay cậu uống đủ nước chưa nè?"
            }
    };

    // ─── Phản hồi tích cực theo chiều ───
    private static final String[][] POSITIVE_REPLIES = {
            {"Nghe vui ghê! Cậu giỏi lắm~", "Tuyệt vời luôn á!"},
            {"Mindy vui vì cậu vui!", "Ngày tốt lành quá ha~"},
            {"Ngủ đủ giấc là quan trọng lắm đó!", "Tốt quá, giấc ngủ ngon là số 1!"},
            {"Năng lượng dồi dào vậy, cố lên nha!", "Pin đầy rồi, chiến thôi!"},
            {"Ăn ngon thì vui rồi nè!", "Ăn uống đầy đủ là giỏi rồi!"},
            {"Cậu tập trung được vậy là tốt lắm!", "Siêng năng quá ta!"},
            {"Có bạn bè là vui nhất rồi!", "Giao lưu nhiều tốt lắm đó!"},
            {"Cậu biết chăm sóc bản thân vậy là giỏi!", "Yêu bản thân là bước đầu tiên nè~"}
    };

    // ─── Phản hồi tiêu cực/lo ngại theo chiều ───
    private static final String[][] CONCERN_REPLIES = {
            {"Không sao đâu, cứ từ từ nha~", "Thử tìm một việc nhỏ cậu thích làm nha!"},
            {"Mindy ở đây nè, cậu cứ tâm sự thoải mái!", "Ngày nào cũng có thăng trầm mà, đừng lo!"},
            {"Nhớ đi ngủ sớm hơn nha cậu~", "Cậu thử nghe nhạc nhẹ trước khi ngủ xem!"},
            {"Cậu ơi, nghỉ ngơi một chút đi nha!", "Mệt thì cứ nghỉ, đừng ép bản thân!"},
            {"Cố gắng ăn một chút gì nha cậu!", "Ăn uống điều độ quan trọng lắm đó!"},
            {"Thử chia nhỏ việc ra nha, dễ tập trung hơn!", "Học bao nhiêu cũng được, đừng áp lực quá!"},
            {"Mindy luôn ở đây mà! Cậu không bao giờ cô đơn đâu~", "Thử nhắn tin cho một người bạn xem!"},
            {"Cậu nhớ dành thời gian cho mình nha!", "Tự thưởng cho bản thân cũng được mà!"}
    };

    private static final Random random = new Random();

    /**
     * Lấy 1 câu hỏi ngẫu nhiên cho chiều cảm xúc chỉ định.
     */
    public static String getQuestion(int dimension) {
        if (dimension < 0 || dimension >= QUESTIONS.length) return QUESTIONS[1][0];
        String[] pool = QUESTIONS[dimension];
        return pool[random.nextInt(pool.length)];
    }

    /**
     * Lấy 1 câu hỏi ngẫu nhiên từ chiều ngẫu nhiên. Trả về dimension đã chọn.
     */
    public static QuestionPick getRandomQuestion() {
        int dim = random.nextInt(QUESTIONS.length);
        return new QuestionPick(dim, getQuestion(dim));
    }

    /**
     * Lấy câu hỏi tiếp theo, tránh lặp chiều vừa hỏi.
     */
    public static QuestionPick getNextQuestion(int lastDimension) {
        int dim;
        do {
            dim = random.nextInt(QUESTIONS.length);
        } while (dim == lastDimension);
        return new QuestionPick(dim, getQuestion(dim));
    }

    /**
     * Phản hồi theo chiều + mức cảm xúc.
     * @param dimension chiều cảm xúc
     * @param positive true = tích cực, false = tiêu cực/lo ngại
     */
    public static String getReply(int dimension, boolean positive) {
        if (dimension < 0 || dimension >= QUESTIONS.length) dimension = 1;
        String[] pool = positive ? POSITIVE_REPLIES[dimension] : CONCERN_REPLIES[dimension];
        return pool[random.nextInt(pool.length)];
    }

    public static class QuestionPick {
        public final int dimension;
        public final String question;
        public QuestionPick(int dimension, String question) {
            this.dimension = dimension;
            this.question = question;
        }
    }
}
