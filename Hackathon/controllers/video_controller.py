import uuid
import os
import tempfile
from datetime import datetime, timezone
from fastapi import HTTPException, UploadFile
from services.firebase import get_db
from services.analyzer import analyze_selfie_video, to_firestore_payload

ALLOWED_MIME   = {"video/mp4", "video/quicktime", "video/x-msvideo", "video/webm"}
MAX_SIZE_BYTES = 200 * 1024 * 1024


def classify_risk(score: float) -> dict:
    if score < 0.35:
        return {"level": "safe",     "label": "Tốt",    "color": "#2ca02c"}
    elif score <= 0.60:
        return {"level": "warning",  "label": "Chú ý",  "color": "#ff7f0e"}
    else:
        return {"level": "high_risk","label": "Nguy cơ","color": "#d62728"}


async def process_video(file: UploadFile, uid: str) -> dict:
    if file.content_type not in ALLOWED_MIME:
        raise HTTPException(status_code=415, detail=f"Unsupported: {file.content_type}")

    content = await file.read()
    if len(content) > MAX_SIZE_BYTES:
        raise HTTPException(status_code=413, detail="Video exceeds 200 MB limit")

    suffix = os.path.splitext(file.filename or "video.mp4")[1] or ".mp4"
    with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as tmp:
        tmp.write(content)
        tmp_path = tmp.name

    try:
        result       = analyze_selfie_video(tmp_path)
        payload_data = to_firestore_payload(result)
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        os.remove(tmp_path)

    video_id = str(uuid.uuid4())
    risk     = classify_risk(result.behavioral_risk_score)

    doc = {
        "video_id":    video_id,
        "user_id":     uid,
        "result":      payload_data,
        "risk":        risk,
        "confidence":  result.confidence,
        "frames":      result.frames_processed,
        "analyzed_at": datetime.now(timezone.utc).isoformat(),
    }

    db = get_db()
    db.collection("users").document(uid).collection("analyses").document(video_id).set(doc)

    return doc


def get_analysis(uid: str, video_id: str) -> dict:
    doc = (
        get_db().collection("users").document(uid)
        .collection("analyses").document(video_id).get()
    )
    if not doc.exists:
        raise HTTPException(status_code=404, detail="Analysis not found")

    data   = doc.to_dict()
    result = data.get("result", {})

    return {
        "video_id":        data["video_id"],
        "duchenne_smile":  result.get("duchenne_ratio"),
        "flat_affect":     result.get("flat_affect_score"),
        "gaze_instability": result.get("gaze_instability"),
        "head_down_ratio": result.get("head_down_ratio"),
        "behavioral_risk_score": result.get("behavioral_risk_score"),
        "total_frames":    data.get("frames"),
        "confidence":      data.get("confidence"),
        "analyzed_at":     data.get("analyzed_at"),
    }