package com.example.mind.checkin.data;

/**
 * Pure helper tính điểm PHQ-9 từ mảng đáp án.
 *
 * Ánh xạ index trong Phq9Activity:
 *   scores[0..6] = Q3..Q9 (7 câu hỏi mà PHQ-9 activity thu)
 *   scores[7]    = Q10 (functional impact)
 * Điểm Q1, Q2 lấy từ PHQ-2 truyền qua intent extras.
 *
 * Ref: FRONTEND_PHQ_PENDING.md + MIND Quy Tac Van Hanh tr.10.
 */
public final class PhqScoreCalculator {

    private PhqScoreCalculator() {}

    /** Tổng PHQ-9 = Q1..Q9. Q10 KHÔNG tính vào total (chỉ đo functional impact). */
    public static int total(int q1, int q2, int[] q3ToQ10) {
        int sum = q1 + q2;
        for (int i = 0; i < 7; i++) sum += q3ToQ10[i];
        return sum;
    }

    /** Điểm phụ cơ thể: Q3 + Q4 + Q5 + Q8. */
    public static int somatic(int[] q3ToQ10) {
        return q3ToQ10[0] + q3ToQ10[1] + q3ToQ10[2] + q3ToQ10[5];
    }

    /** Điểm phụ nhận thức: Q1 + Q2 + Q6 + Q7 + Q9. */
    public static int cognitive(int q1, int q2, int[] q3ToQ10) {
        return q1 + q2 + q3ToQ10[3] + q3ToQ10[4] + q3ToQ10[6];
    }

    /** Functional impact: Q10 (không tính vào total). */
    public static int functionalImpact(int[] q3ToQ10) {
        return q3ToQ10[7];
    }

    /** Giá trị câu 9 (riêng biệt vì liên quan tự hại). */
    public static int q9Value(int[] q3ToQ10) {
        return q3ToQ10[6];
    }

    /**
     * Build đầy đủ 9 điểm Q1..Q9 vào 1 mảng để lưu/submit.
     * allScores[0] = Q1, allScores[1] = Q2, allScores[2..8] = Q3..Q9.
     */
    public static int[] buildAllScores(int q1, int q2, int[] q3ToQ10) {
        int[] all = new int[9];
        all[0] = q1;
        all[1] = q2;
        System.arraycopy(q3ToQ10, 0, all, 2, 7);
        return all;
    }
}
