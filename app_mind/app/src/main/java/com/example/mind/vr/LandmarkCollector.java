package com.example.mind.vr;

import com.google.mediapipe.tasks.components.containers.Category;
import com.google.mediapipe.tasks.components.containers.NormalizedLandmark;
import com.google.mediapipe.tasks.vision.facelandmarker.FaceLandmarkerResult;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/**
 * Thu thập landmark + blendshape data từ mỗi frame
 * để đóng gói gửi lên backend phân tích cảm xúc.
 */
public class LandmarkCollector {

    private final List<FrameData> frames = new ArrayList<>();
    private long startTime = 0;

    public void start() {
        frames.clear();
        startTime = System.currentTimeMillis();
    }

    /** Thêm data từ 1 frame */
    public void addFrame(FaceLandmarkerResult result, long timestampMs) {
        if (result == null || result.faceLandmarks().isEmpty()) return;

        FrameData frame = new FrameData();
        frame.timestampMs = timestampMs - startTime;

        // Lấy blendshapes (52 biểu cảm)
        if (result.faceBlendshapes().isPresent()
                && !result.faceBlendshapes().get().isEmpty()) {
            List<Category> blendshapes = result.faceBlendshapes().get().get(0);
            for (Category bs : blendshapes) {
                frame.blendshapes.add(new BlendshapeEntry(bs.categoryName(), bs.score()));
            }
        }

        // Lấy landmarks chính (giảm data: chỉ lấy 68 điểm quan trọng thay vì 478)
        List<NormalizedLandmark> allLandmarks = result.faceLandmarks().get(0);
        for (int idx : KEY_LANDMARKS) {
            if (idx < allLandmarks.size()) {
                NormalizedLandmark lm = allLandmarks.get(idx);
                frame.landmarks.add(new LandmarkEntry(idx, lm.x(), lm.y(), lm.z()));
            }
        }

        frames.add(frame);
    }

    /** Xuất toàn bộ data thành JSON để gửi backend */
    public String toJson() {
        try {
            JSONObject root = new JSONObject();
            root.put("total_frames", frames.size());
            root.put("duration_ms", frames.isEmpty() ? 0
                    : frames.get(frames.size() - 1).timestampMs);

            JSONArray framesArray = new JSONArray();
            for (FrameData frame : frames) {
                JSONObject frameObj = new JSONObject();
                frameObj.put("t", frame.timestampMs);

                // Blendshapes
                JSONObject bsObj = new JSONObject();
                for (BlendshapeEntry bs : frame.blendshapes) {
                    bsObj.put(bs.name, Math.round(bs.score * 1000) / 1000.0);
                }
                frameObj.put("blendshapes", bsObj);

                // Landmarks
                JSONArray lmArray = new JSONArray();
                for (LandmarkEntry lm : frame.landmarks) {
                    JSONObject lmObj = new JSONObject();
                    lmObj.put("i", lm.index);
                    lmObj.put("x", Math.round(lm.x * 10000) / 10000.0);
                    lmObj.put("y", Math.round(lm.y * 10000) / 10000.0);
                    lmObj.put("z", Math.round(lm.z * 10000) / 10000.0);
                    lmArray.put(lmObj);
                }
                frameObj.put("landmarks", lmArray);

                framesArray.put(frameObj);
            }
            root.put("frames", framesArray);

            return root.toString();
        } catch (JSONException e) {
            return "{}";
        }
    }

    public int getFrameCount() {
        return frames.size();
    }

    // ──────────────────────────────────────────────
    // Data classes
    // ──────────────────────────────────────────────

    private static class FrameData {
        long timestampMs;
        List<BlendshapeEntry> blendshapes = new ArrayList<>();
        List<LandmarkEntry> landmarks = new ArrayList<>();
    }

    private static class BlendshapeEntry {
        String name;
        float score;
        BlendshapeEntry(String name, float score) {
            this.name = name;
            this.score = score;
        }
    }

    private static class LandmarkEntry {
        int index;
        float x, y, z;
        LandmarkEntry(int index, float x, float y, float z) {
            this.index = index;
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    // 68 key landmarks (mắt, mũi, miệng, lông mày, viền mặt)
    private static final int[] KEY_LANDMARKS = {
            // Viền mặt (17 điểm)
            10, 338, 297, 332, 284, 251, 389, 356, 454, 323, 361, 288, 397, 365, 379, 378, 400,
            // Lông mày trái (5)
            70, 63, 105, 66, 107,
            // Lông mày phải (5)
            336, 296, 334, 293, 300,
            // Mũi (9)
            168, 6, 197, 195, 5, 4, 1, 2, 98,
            // Mắt trái (6)
            33, 160, 158, 133, 153, 144,
            // Mắt phải (6)
            362, 385, 387, 263, 373, 380,
            // Miệng ngoài (12)
            61, 185, 40, 39, 37, 0, 267, 269, 270, 409, 291, 375,
            // Miệng trong (8)
            78, 191, 80, 81, 82, 13, 312, 311
    };
}
