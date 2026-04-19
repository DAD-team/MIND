from dataclasses import dataclass
from typing import Optional, List
import numpy as np
import cv2
import mediapipe as mp
from config import MODEL_PATH


@dataclass
class FrameData:
    timestamp_ms:          int
    blendshapes:           dict
    landmarks:             list
    transformation_matrix: Optional[np.ndarray] = None

@dataclass
class AffectResult:
    duchenne_ratio:         float
    flat_affect_score:      float
    smiling_duration_ratio: float
    happy_face_ratio:       float

@dataclass
class GazeResult:
    gaze_instability:     float
    gaze_break_rate:      float
    gaze_avoidance_score: float = 0.0

@dataclass
class HeadPoseResult:
    head_down_ratio:        float
    head_movement_variance: float

@dataclass
class BlinkResult:
    blink_rate:         float
    avg_blink_duration: float

@dataclass
class EmotionResult:
    duchenne_ratio:         float
    flat_affect_score:      float
    gaze_instability:       float
    head_down_ratio:        float
    blink_duration_avg:     float
    behavioral_risk_score:  float
    smiling_duration_ratio: float = 0.0
    happy_face_ratio:       float = 0.0
    gaze_break_rate:        float = 0.0
    gaze_avoidance_score:   float = 0.0
    head_movement_variance: float = 0.0
    blink_rate:             float = 0.0
    frames_processed:       int   = 0
    confidence:             float = 0.0


def extract_landmarks_and_blendshapes(video_path: str, frame_skip: int = 1) -> List[FrameData]:
    BaseOptions        = mp.tasks.BaseOptions
    FaceLandmarker     = mp.tasks.vision.FaceLandmarker
    FaceLandmarkerOpts = mp.tasks.vision.FaceLandmarkerOptions
    RunningMode        = mp.tasks.vision.RunningMode

    options = FaceLandmarkerOpts(
        base_options=BaseOptions(model_asset_path=MODEL_PATH),
        running_mode=RunningMode.VIDEO,
        output_face_blendshapes=True,
        output_facial_transformation_matrixes=True,
        num_faces=1,
    )
    frames_data: List[FrameData] = []

    with FaceLandmarker.create_from_options(options) as landmarker:
        cap = cv2.VideoCapture(video_path)
        if not cap.isOpened():
            raise FileNotFoundError(f"Không mở được video: {video_path}")
        fps       = cap.get(cv2.CAP_PROP_FPS) or 30.0
        frame_idx = 0

        while cap.isOpened():
            ret, frame = cap.read()
            if not ret:
                break
            if frame_idx % frame_skip == 0:
                ts_ms  = int(frame_idx * 1000 / fps)
                rgb    = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                mp_img = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
                result = landmarker.detect_for_video(mp_img, ts_ms)

                if result.face_landmarks:
                    bs = {}
                    if result.face_blendshapes:
                        for b in result.face_blendshapes[0]:
                            bs[b.category_name] = b.score
                    tm = None
                    if result.facial_transformation_matrixes:
                        tm = np.array(result.facial_transformation_matrixes[0].data).reshape(4, 4)
                    frames_data.append(FrameData(
                        timestamp_ms=ts_ms,
                        blendshapes=bs,
                        landmarks=result.face_landmarks[0],
                        transformation_matrix=tm,
                    ))
            frame_idx += 1
        cap.release()

    return frames_data


def compute_affect_indicators(frames: List[FrameData]) -> AffectResult:
    SMILE_THRESHOLD   = 0.15
    CHEEK_THRESHOLD   = 0.35
    DUCHENNE_COUPLING = 0.60
    MIN_SMILE_FRAMES  = 8
    MIN_SMILE_EVENTS  = 2
    FLAT_SENSITIVITY  = 5.0
    FLAT_TOP_N        = 15

    if not frames:
        return AffectResult(0.0, 1.0, 0.0, 0.0)

    ms_series, cs_series, all_bs_matrix = [], [], []
    for f in frames:
        bs = f.blendshapes
        ms = (bs.get("mouthSmileLeft", 0.0) + bs.get("mouthSmileRight", 0.0)) / 2.0
        cs = max(
            (bs.get("cheekSquintLeft", 0.0) + bs.get("cheekSquintRight", 0.0)) / 2.0,
            (bs.get("eyeSquintLeft",   0.0) + bs.get("eyeSquintRight",   0.0)) / 2.0,
        )
        ms_series.append(ms)
        cs_series.append(cs)
        if bs:
            all_bs_matrix.append(list(bs.values()))

    smile_events = []
    in_smile = False
    event_ms, event_cs, event_start = [], [], 0

    for i, (ms, cs) in enumerate(zip(ms_series, cs_series)):
        if ms > SMILE_THRESHOLD:
            if not in_smile:
                in_smile, event_start, event_ms, event_cs = True, i, [], []
            event_ms.append(ms)
            event_cs.append(cs)
        else:
            if in_smile:
                smile_events.append({"length": i - event_start, "ms_list": event_ms, "cs_list": event_cs})
                in_smile = False
    if in_smile:
        smile_events.append({"length": len(ms_series) - event_start, "ms_list": event_ms, "cs_list": event_cs})

    total_events = len(smile_events)
    genuine_events = total_smile_frames = genuine_smile_frames = 0

    for ev in smile_events:
        length  = ev["length"]
        peak_ms = max(ev["ms_list"])
        peak_cs = max(ev["cs_list"])
        apex_cs = ev["cs_list"][ev["ms_list"].index(peak_ms)]
        total_smile_frames += length
        if length >= MIN_SMILE_FRAMES and peak_cs >= CHEEK_THRESHOLD and apex_cs >= peak_ms * DUCHENNE_COUPLING:
            genuine_events       += 1
            genuine_smile_frames += length

    duchenne_ratio = genuine_events / total_events if total_events >= MIN_SMILE_EVENTS else 0.0
    n = len(frames)

    if len(all_bs_matrix) > 1:
        bs_mat   = np.array(all_bs_matrix)
        top_stds = sorted(np.std(bs_mat, axis=0), reverse=True)[:FLAT_TOP_N]
        flat     = float(np.clip(1.0 - np.mean(top_stds) * FLAT_SENSITIVITY, 0.0, 1.0))
    else:
        flat = 1.0

    return AffectResult(
        duchenne_ratio=         round(float(np.clip(duchenne_ratio, 0.0, 1.0)), 4),
        flat_affect_score=      round(flat, 4),
        smiling_duration_ratio= round(total_smile_frames / n, 4),
        happy_face_ratio=       round(genuine_smile_frames / n, 4),
    )


def compute_gaze_indicators(frames: List[FrameData]) -> GazeResult:
    YAW_REF   = 15.0
    BREAK_REF = 20.0

    if not frames:
        return GazeResult(0.0, 0.0)

    yaws = []
    for f in frames:
        if f.transformation_matrix is None:
            continue
        R = f.transformation_matrix[:3, :3]
        yaws.append(float(np.degrees(np.arctan2(-R[2, 0], np.sqrt(R[2, 1]**2 + R[2, 2]**2)))))

    if not yaws:
        return GazeResult(0.0, 0.0)

    yaw_arr  = np.array(yaws)
    yaw_std  = float(yaw_arr.std())
    yaw_mean = float(yaw_arr.mean())

    instability        = float(np.clip(yaw_std / YAW_REF, 0.0, 1.0))
    personal_break_thr = max(2.0 * yaw_std, 10.0)
    relative_yaws      = yaw_arr - yaw_mean

    break_events = 0
    in_break     = False
    for rv in relative_yaws:
        if abs(rv) > personal_break_thr:
            if not in_break:
                break_events += 1
                in_break      = True
        else:
            in_break = False

    dur_min    = (frames[-1].timestamp_ms - frames[0].timestamp_ms) / 60_000.0 if len(frames) > 1 else 1.0
    break_rate = float(np.clip((break_events / dur_min) / BREAK_REF, 0.0, 1.0)) if dur_min > 0 else 0.0
    avoidance  = float(np.clip((instability + break_rate) / 2.0, 0.0, 1.0))

    return GazeResult(
        gaze_instability=     round(instability, 4),
        gaze_break_rate=      round(break_rate,  4),
        gaze_avoidance_score= round(avoidance,   4),
    )


def compute_head_pose_indicators(frames: List[FrameData]) -> HeadPoseResult:
    HEAD_DOWN_THR = 15.0
    pitches = []
    for f in frames:
        if f.transformation_matrix is None:
            continue
        R = f.transformation_matrix[:3, :3]
        pitches.append(float(np.degrees(np.arctan2(R[2, 1], R[2, 2]))))

    if not pitches:
        return HeadPoseResult(0.0, 0.0)

    diffs = [abs(pitches[i] - pitches[i-1]) for i in range(1, len(pitches))]
    return HeadPoseResult(
        head_down_ratio=        round(sum(1 for p in pitches if p > HEAD_DOWN_THR) / len(pitches), 4),
        head_movement_variance= round(float(np.std(diffs)) if diffs else 0.0, 4),
    )


def compute_blink_indicators(frames: List[FrameData]) -> BlinkResult:
    MAX_BLINK_DURATION_S = 0.50
    MIN_RELIABLE_MAX     = 0.40

    if not frames:
        return BlinkResult(0.0, 0.0)

    if len(frames) > 1:
        intervals     = [frames[i].timestamp_ms - frames[i-1].timestamp_ms for i in range(1, len(frames))]
        median_ms     = float(np.median(intervals))
        effective_fps = 1000.0 / median_ms if median_ms > 0 else 30.0
    else:
        effective_fps = 30.0

    scores = np.array([
        (f.blendshapes.get("eyeBlinkLeft", 0.0) + f.blendshapes.get("eyeBlinkRight", 0.0)) / 2.0
        for f in frames
    ])

    if scores.max() < MIN_RELIABLE_MAX:
        return BlinkResult(0.0, 0.0)

    adaptive_thr = float(np.clip(scores.mean() + 2.0 * scores.std(), 0.45, 0.85))
    in_blink = False
    blink_count = 0
    valid_durs = []
    cur_len = 0

    for s in scores:
        if s > adaptive_thr:
            if not in_blink:
                in_blink, cur_len = True, 0
            cur_len += 1
        else:
            if in_blink:
                dur = cur_len / effective_fps
                blink_count += 1
                if dur <= MAX_BLINK_DURATION_S:
                    valid_durs.append(dur)
                in_blink = False

    if in_blink and cur_len > 0:
        dur = cur_len / effective_fps
        blink_count += 1
        if dur <= MAX_BLINK_DURATION_S:
            valid_durs.append(dur)

    total_s = (frames[-1].timestamp_ms - frames[0].timestamp_ms) / 1000.0 if len(frames) > 1 else 1.0
    return BlinkResult(
        blink_rate=         round(blink_count / total_s if total_s > 0 else 0.0, 4),
        avg_blink_duration= round(float(np.mean(valid_durs)) if valid_durs else 0.0, 4),
    )


def compute_behavioral_risk(affect, gaze, head, blink) -> float:
    WEIGHTS     = {"s_gaze": 0.15, "s_blink": 0.22, "s_happy": 0.25, "s_flat": 0.22, "s_head": 0.16}
    BLINK_REF_S = 0.40
    HC_HAPPY    = 0.35

    def clip01(v): return float(np.clip(v, 0.0, 1.0))

    signals = {
        "s_gaze":  clip01(gaze.gaze_avoidance_score),
        "s_blink": clip01(blink.avg_blink_duration / BLINK_REF_S),
        "s_happy": clip01((HC_HAPPY - affect.happy_face_ratio) / HC_HAPPY),
        "s_flat":  clip01(affect.flat_affect_score),
        "s_head":  clip01(head.head_down_ratio),
    }
    return round(float(np.clip(sum(WEIGHTS[k] * signals[k] for k in WEIGHTS), 0.0, 1.0)), 4)


def analyze_selfie_video(video_path: str, frame_skip: int = 1) -> EmotionResult:
    frames = extract_landmarks_and_blendshapes(video_path, frame_skip)
    if not frames:
        raise ValueError("Không phát hiện khuôn mặt trong video.")

    affect = compute_affect_indicators(frames)
    gaze   = compute_gaze_indicators(frames)
    head   = compute_head_pose_indicators(frames)
    blink  = compute_blink_indicators(frames)
    risk   = compute_behavioral_risk(affect, gaze, head, blink)
    conf   = float(np.clip(len(frames) / 150.0, 0.1, 1.0))

    return EmotionResult(
        duchenne_ratio=         affect.duchenne_ratio,
        flat_affect_score=      affect.flat_affect_score,
        gaze_instability=       gaze.gaze_instability,
        head_down_ratio=        head.head_down_ratio,
        blink_duration_avg=     blink.avg_blink_duration,
        behavioral_risk_score=  risk,
        smiling_duration_ratio= affect.smiling_duration_ratio,
        happy_face_ratio=       affect.happy_face_ratio,
        gaze_break_rate=        gaze.gaze_break_rate,
        gaze_avoidance_score=   gaze.gaze_avoidance_score,
        head_movement_variance= head.head_movement_variance,
        blink_rate=             blink.blink_rate,
        frames_processed=       len(frames),
        confidence=             round(conf, 2),
    )


def to_firestore_payload(result: EmotionResult) -> dict:
    return {
        "duchenne_ratio":        result.duchenne_ratio,
        "flat_affect_score":     result.flat_affect_score,
        "gaze_instability":      result.gaze_instability,
        "head_down_ratio":       result.head_down_ratio,
        "blink_duration_avg_s":  result.blink_duration_avg,
        "behavioral_risk_score": result.behavioral_risk_score,
    }