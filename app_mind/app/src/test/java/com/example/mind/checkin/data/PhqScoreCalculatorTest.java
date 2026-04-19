package com.example.mind.checkin.data;

import static org.junit.Assert.assertArrayEquals;
import static org.junit.Assert.assertEquals;

import org.junit.Test;

/**
 * Unit tests cho {@link PhqScoreCalculator}.
 *
 * Mapping TEST_CASES.md P9-14 + scoring rules tr.10 PDF:
 *   - total = Q1..Q9 (KHÔNG cộng Q10)
 *   - somatic = Q3 + Q4 + Q5 + Q8
 *   - cognitive = Q1 + Q2 + Q6 + Q7 + Q9
 *   - functional_impact = Q10
 *   - q9_value = Q9 (riêng biệt)
 */
public class PhqScoreCalculatorTest {

    /** Helper: scores[0..6] = Q3..Q9, scores[7] = Q10 */
    private static int[] answers(int q3, int q4, int q5, int q6, int q7, int q8, int q9, int q10) {
        return new int[]{q3, q4, q5, q6, q7, q8, q9, q10};
    }

    // ─── total ───

    @Test
    public void total_sumsQ1_throughQ9() {
        int[] q = answers(1, 2, 2, 1, 1, 0, 2, 3);  // Q10=3 nhưng KHÔNG cộng
        // Q1=1, Q2=2, Q3..Q9 = 1+2+2+1+1+0+2 = 9 → total = 3 + 9 = 12
        assertEquals(12, PhqScoreCalculator.total(1, 2, q));
    }

    @Test
    public void total_allZero() {
        assertEquals(0, PhqScoreCalculator.total(0, 0, answers(0, 0, 0, 0, 0, 0, 0, 0)));
    }

    @Test
    public void total_allMax_givesSeverePhq9() {
        // Tất cả = 3 (Q1..Q9) → 27 (maximum PHQ-9 score)
        assertEquals(27, PhqScoreCalculator.total(3, 3, answers(3, 3, 3, 3, 3, 3, 3, 3)));
    }

    @Test
    public void total_ignoresQ10() {
        int[] withQ10Zero = answers(1, 1, 1, 1, 1, 1, 1, 0);
        int[] withQ10Three = answers(1, 1, 1, 1, 1, 1, 1, 3);
        assertEquals(
                PhqScoreCalculator.total(1, 1, withQ10Zero),
                PhqScoreCalculator.total(1, 1, withQ10Three));
    }

    // ─── somatic (Q3 + Q4 + Q5 + Q8) ───

    @Test
    public void somatic_usesQ3_Q4_Q5_Q8_only() {
        // Đặt Q3=1, Q4=2, Q5=3, Q6=3, Q7=3, Q8=2, Q9=3, Q10=3
        int[] q = answers(1, 2, 3, 3, 3, 2, 3, 3);
        // somatic = Q3+Q4+Q5+Q8 = 1+2+3+2 = 8
        assertEquals(8, PhqScoreCalculator.somatic(q));
    }

    @Test
    public void somatic_allZero() {
        assertEquals(0, PhqScoreCalculator.somatic(answers(0, 0, 0, 0, 0, 0, 0, 0)));
    }

    // ─── cognitive (Q1 + Q2 + Q6 + Q7 + Q9) ───

    @Test
    public void cognitive_usesCorrectQuestions() {
        int[] q = answers(3, 3, 3, 1, 2, 3, 3, 3);
        // cognitive = Q1+Q2+Q6+Q7+Q9 = 2+1+1+2+3 = 9
        assertEquals(9, PhqScoreCalculator.cognitive(2, 1, q));
    }

    @Test
    public void cognitive_respectsPhq2Scores() {
        int[] q = answers(0, 0, 0, 0, 0, 0, 0, 0);
        // Chỉ Q1+Q2 (từ PHQ-2) có điểm
        assertEquals(5, PhqScoreCalculator.cognitive(3, 2, q));
    }

    // ─── functionalImpact (Q10) ───

    @Test
    public void functionalImpact_returnsQ10() {
        int[] q = answers(0, 0, 0, 0, 0, 0, 0, 2);
        assertEquals(2, PhqScoreCalculator.functionalImpact(q));
    }

    // ─── q9Value ───

    @Test
    public void q9Value_returnsCorrectIndex() {
        int[] q = answers(0, 0, 0, 0, 0, 0, 3, 0);
        assertEquals(3, PhqScoreCalculator.q9Value(q));
    }

    @Test
    public void q9Value_zero_noSafety() {
        int[] q = answers(3, 3, 3, 3, 3, 3, 0, 3);
        assertEquals(0, PhqScoreCalculator.q9Value(q));
    }

    // ─── buildAllScores ───

    @Test
    public void buildAllScores_assemblesQ1ToQ9() {
        int[] q = answers(3, 4, 5, 6, 7, 8, 9, 99);  // last=Q10 bị bỏ
        int[] all = PhqScoreCalculator.buildAllScores(1, 2, q);

        assertEquals(9, all.length);
        assertArrayEquals(new int[]{1, 2, 3, 4, 5, 6, 7, 8, 9}, all);
    }

    // ─── Integration-like cases từ spec FRONTEND_PHQ_PENDING.md ───

    @Test
    public void spec_example_moderate_case() {
        // Từ FRONTEND_PHQ_PENDING.md submit example:
        //   scores = [1, 2, 2, 1, 1, 0, 2, 1, 0] (Q1..Q9), functional = Q10 = 2
        // Mapping: Q1=1, Q2=2, Q3=2, Q4=1, Q5=1, Q6=0, Q7=2, Q8=1, Q9=0
        int[] q = answers(2, 1, 1, 0, 2, 1, 0, 2);

        assertEquals(10, PhqScoreCalculator.total(1, 2, q));
        // somatic = Q3+Q4+Q5+Q8 = 2+1+1+1 = 5
        assertEquals(5, PhqScoreCalculator.somatic(q));
        // cognitive = Q1+Q2+Q6+Q7+Q9 = 1+2+0+2+0 = 5
        assertEquals(5, PhqScoreCalculator.cognitive(1, 2, q));
        assertEquals(0, PhqScoreCalculator.q9Value(q));
        assertEquals(2, PhqScoreCalculator.functionalImpact(q));
    }
}
