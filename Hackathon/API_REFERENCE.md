# API Reference — MIND Backend

**Base URL:** Đọc động từ Firebase Realtime DB tại `server_info/tunnel_url`

---

## Quy ước chung

### Authentication
Tất cả endpoint (trừ `/health`) yêu cầu header:
```
Authorization: Bearer <Firebase_ID_Token>
```
Server decode token để lấy `uid` của user. Token hết hạn trả về `401`.

### Format
- **Content-Type:** `application/json`
- **Timestamps:** ISO 8601 UTC, ví dụ `"2026-04-14T10:30:00+00:00"`
- **IDs:** UUID v4 string
- **Field names:** `snake_case`

### Error response
```json
{ "detail": "mô tả lỗi" }
```

| HTTP | Ý nghĩa |
|------|---------|
| 200 | Thành công |
| 201 | Tạo mới thành công |
| 204 | Xóa thành công (không có body) |
| 401 | Token không hợp lệ hoặc hết hạn |
| 404 | Không tìm thấy resource |
| 422 | Dữ liệu đầu vào không hợp lệ |

---

## 1. Health

### `GET /health`
Kiểm tra server đang chạy. **Không cần auth.**

**Response 200:**
```json
{ "status": "ok" }
```

---

## 2. Auth

### `GET /auth/me`
Tạo mới hoặc lấy profile user hiện tại. Lần đầu đăng nhập sẽ tự tạo document trong Firestore.

**Response 200:**
```json
{
  "user_id": "firebase_uid_abc123",
  "email":   "user@example.com",
  "name":    "Nguyễn Văn A",
  "picture": "https://..."
}
```

---

## 3. Videos

### `POST /videos/upload`
Upload video selfie để phân tích cảm xúc qua MediaPipe.

**Request:** `multipart/form-data`

| Field | Type | Bắt buộc | Mô tả |
|-------|------|----------|-------|
| `file` | file | Có | Video selfie. Hỗ trợ: `mp4`, `mov`, `avi`, `webm`. Tối đa 200 MB |

**Response 200:**
```json
{
  "video_id":    "uuid",
  "user_id":     "firebase_uid",
  "result": {
    "duchenne_ratio":        0.72,
    "flat_affect_score":     0.15,
    "gaze_instability":      0.30,
    "head_down_ratio":       0.10,
    "blink_duration_avg_s":  0.18,
    "behavioral_risk_score": 0.35
  },
  "risk": {
    "level":  "safe",
    "label":  "Tốt",
    "color":  "#2ca02c"
  },
  "confidence":  0.92,
  "frames":      145,
  "analyzed_at": "2026-04-14T10:30:00+00:00"
}
```

**risk.level values:**

| level | label | Ngưỡng behavioral_risk_score |
|-------|-------|------------------------------|
| `safe` | Tốt | < 0.35 |
| `warning` | Chú ý | 0.35 – 0.60 |
| `high_risk` | Nguy cơ | > 0.60 |

**Errors:**
- `415` — Định dạng file không hỗ trợ
- `413` — File vượt quá 200 MB
- `422` — Không phát hiện khuôn mặt trong video

---

### `GET /videos/analysis/{video_id}`
Lấy kết quả phân tích cảm xúc của 1 video đã upload.

**Path param:** `video_id` — UUID của video

**Response 200:**
```json
{
  "video_id":              "uuid",
  "duchenne_smile":        0.72,
  "flat_affect":           0.15,
  "gaze_instability":      0.30,
  "head_down_ratio":       0.10,
  "behavioral_risk_score": 0.35,
  "total_frames":          145,
  "confidence":            0.92,
  "analyzed_at":           "2026-04-14T10:30:00+00:00"
}
```

**Giải thích các chỉ số:**

| Field | Range | Ý nghĩa |
|-------|-------|---------|
| `duchenne_smile` | 0–1 | Tỷ lệ nụ cười thật (cheekSquint + mouthSmile). Cao = nhiều nụ cười thật |
| `flat_affect` | 0–1 | Biểu cảm phẳng. Cao = ít biểu cảm, có thể trầm cảm |
| `gaze_instability` | 0–1 | Độ bất ổn ánh mắt. Cao = tránh nhìn thẳng |
| `head_down_ratio` | 0–1 | Tỷ lệ cúi đầu. Cao = hay cúi |
| `behavioral_risk_score` | 0–1 | Điểm rủi ro tổng hợp |

**Errors:**
- `404` — video_id không tồn tại hoặc không thuộc user

---

## 4. History

### `GET /history/me`
Lấy lịch sử tất cả video đã phân tích của user.

**Query params:**

| Param | Type | Default | Mô tả |
|-------|------|---------|-------|
| `limit` | int | 20 | Số bản ghi tối đa (1–100) |

**Response 200:**
```json
{
  "user_id": "firebase_uid",
  "records": [
    {
      "video_id":    "uuid",
      "result":      { "duchenne_ratio": 0.72, "..." : "..." },
      "risk":        { "level": "safe", "label": "Tốt", "color": "#2ca02c" },
      "confidence":  0.92,
      "frames":      145,
      "analyzed_at": "2026-04-14T10:30:00+00:00"
    }
  ]
}
```

---

### `GET /history/me/{video_id}`
Lấy chi tiết 1 bản ghi phân tích.

**Response 200:** Giống 1 phần tử trong `records` ở trên.

**Errors:**
- `404` — Không tìm thấy

---

## 5. Schedules (Lịch học)

### `GET /schedules`
Lấy toàn bộ lịch học của user, sắp xếp theo `day_of_week` → `start_time`.

**Response 200:**
```json
{
  "schedules": [
    {
      "id":          "uuid",
      "user_id":     "firebase_uid",
      "subject":     "Toán cao cấp",
      "day_of_week": 2,
      "start_time":  "09:00",
      "end_time":    "11:00",
      "room":        "A101",
      "event_type":  0,
      "created_at":  "2026-04-14T10:00:00+00:00",
      "updated_at":  "2026-04-14T10:00:00+00:00"
    }
  ]
}
```

---

### `POST /schedules`
Tạo mới 1 buổi học / sự kiện.

**Request body:**
```json
{
  "subject":     "Đồ án tốt nghiệp",
  "day_of_week": 3,
  "start_time":  "14:00",
  "end_time":    "16:00",
  "room":        "B205",
  "event_type":  2
}
```

| Field | Type | Bắt buộc | Mô tả |
|-------|------|----------|-------|
| `subject` | string | Có | Tên môn học / sự kiện |
| `day_of_week` | int | Có | 0=Thứ 2 … 6=Chủ nhật (Python weekday) |
| `start_time` | string | Có | Format `HH:mm` |
| `end_time` | string | Có | Format `HH:mm` |
| `room` | string | Không | Phòng học |
| `event_type` | int | Không | Xem bảng bên dưới. Mặc định `0` |

**Bảng event_type:**

| Giá trị | Tên | Trọng số áp lực |
|---------|-----|----------------|
| `0` | Học thường | 0 |
| `1` | Thi | 3 |
| `2` | Deadline đồ án | 2 |
| `3` | Nộp bài tập | 1 |
| `4` | Thuyết trình | 1 |

**Response 201:** Object schedule vừa tạo (giống format trong GET /schedules).

**Errors:**
- `422` — `day_of_week` ngoài 0–6 hoặc `start_time`/`end_time` sai format

---

### `PATCH /schedules/{schedule_id}`
Cập nhật một phần lịch học. Chỉ gửi field cần thay đổi.

**Path param:** `schedule_id`

**Request body** (tất cả optional):
```json
{
  "subject":     "Toán B1",
  "day_of_week": 4,
  "start_time":  "08:00",
  "end_time":    "10:00",
  "room":        "C301",
  "event_type":  1
}
```

**Response 200:** Object schedule sau khi cập nhật.

**Errors:**
- `404` — schedule_id không tồn tại
- `422` — Không có field nào hoặc giá trị không hợp lệ

---

### `DELETE /schedules/{schedule_id}`
Xóa 1 buổi học.

**Response 204:** Không có body.

**Errors:**
- `404` — schedule_id không tồn tại

---

### `GET /schedules/upcoming`
Lấy các sự kiện học thuật trong N ngày tới kèm tổng trọng số áp lực. Dùng cho Wellbeing Scout.

**Query params:**

| Param | Type | Default | Mô tả |
|-------|------|---------|-------|
| `days` | int | 3 | Số ngày phía trước (1–30) |

**Response 200:**
```json
{
  "events": [
    {
      "subject":         "Toán cao cấp",
      "event_type":      1,
      "event_type_name": "Thi",
      "event_weight":    3,
      "day_of_week":     2,
      "start_time":      "09:00"
    },
    {
      "subject":         "Đồ án tốt nghiệp",
      "event_type":      2,
      "event_type_name": "Deadline đồ án",
      "event_weight":    2,
      "day_of_week":     3,
      "start_time":      "23:59"
    }
  ],
  "total_weight": 5,
  "event_count":  2
}
```

> `total_weight` = tổng `event_weight` của tất cả sự kiện, dùng để tính điểm áp lực học thuật trong Scout.

---

## 6. Notifications

### `PUT /notifications/fcm-token`
Lưu hoặc cập nhật FCM token của thiết bị. Gọi sau khi đăng nhập hoặc khi FCM cấp token mới.

**Request body:**
```json
{ "fcm_token": "dXXXXX..." }
```

**Response 200:**
```json
{ "message": "FCM token updated" }
```

---

### `DELETE /notifications/fcm-token`
Xóa FCM token. Gọi khi user đăng xuất để ngừng nhận notification.

**Response 200:**
```json
{ "message": "FCM token removed" }
```

---

## 7. PHQ (Sàng lọc trầm cảm)

### `POST /phq/submit`
Lưu kết quả làm bài PHQ-2 hoặc PHQ-9.

**Request body — PHQ-2:**
```json
{
  "phq_type":           "phq2",
  "scores":             [1, 2],
  "total":              3,
  "source":             "self_request",
  "behavior_risk_score": 4.5
}
```

**Request body — PHQ-9:**
```json
{
  "phq_type":              "phq9",
  "scores":                [1, 2, 2, 1, 1, 0, 2, 1, 0],
  "total":                 10,
  "somatic_score":         4,
  "cognitive_score":       5,
  "functional_impact":     2,
  "q9_value":              0,
  "source":                "phq2_escalation",
  "triggered_by_phq2_id": "uuid-phq2-record",
  "behavior_risk_score":   4.5
}
```

**Tất cả các field:**

| Field | Type | Bắt buộc | Mô tả |
|-------|------|----------|-------|
| `phq_type` | string | Có | `"phq2"` hoặc `"phq9"` |
| `scores` | int[] | Có | Điểm từng câu (0–3). PHQ-2: 2 phần tử. PHQ-9: 9 phần tử |
| `total` | int | Có | Tổng điểm. PHQ-2: 0–6. PHQ-9: 0–27 |
| `source` | string | Có | `"self_request"` / `"scheduled"` / `"scout"` |
| `somatic_score` | int | Chỉ PHQ-9 | Q3+Q4+Q5+Q8 (0–12) |
| `cognitive_score` | int | Chỉ PHQ-9 | Q1+Q2+Q6+Q7+Q9 (0–15) |
| `functional_impact` | int | Chỉ PHQ-9 | Câu 10 (0–3) |
| `q9_value` | int | Chỉ PHQ-9 | Câu 9 về tự hại (0–3). **Nếu >= 1 → tự động tạo Safety Event** |
| `triggered_by_phq2_id` | string | Không | ID lần PHQ-2 đã trigger PHQ-9 này |
| `behavior_risk_score` | float | Không | Điểm rủi ro hành vi từ video (0–10) |

**Response 201 — PHQ-2:**
```json
{
  "id":              "uuid",
  "created_at":      "2026-04-14T10:30:00+00:00",
  "decision":        "escalate_phq9",
  "next_phq2_days":  null
}
```

| `decision` | Ý nghĩa | `next_phq2_days` |
|------------|---------|-------------------|
| `"escalate_phq9"` | Cần đánh giá PHQ-9 ngay | `null` |
| `"shorten_interval"` | PHQ-2 lại sau 7 ngày | `7` |
| `"normal"` | Bình thường, PHQ-2 sau 14 ngày | `14` |

> Khi `decision == "escalate_phq9"`, backend tự động tạo pending PHQ-9. Frontend gọi `GET /phq/pending` để lấy.

**Response 201 — PHQ-9:**
```json
{
  "id":               "uuid",
  "created_at":       "2026-04-14T10:30:00+00:00",
  "severity":         "moderate",
  "monitoring_level": 3,
  "next_phq9_date":   "2026-04-28T10:30:00+00:00",
  "next_phq2_date":   null
}
```

> `next_phq2_date` chỉ có giá trị khi `severity == "minimal"` (quay về PHQ-2 mỗi 14 ngày).

**Bảng severity (PHQ-9):**

| Tổng điểm | severity | monitoring_level |
|-----------|----------|-----------------|
| 0–4 | `minimal` | 1 |
| 5–9 | `mild` | 2 |
| 10–14 | `moderate` | 3 |
| 15–19 | `moderately_severe` | 4 |
| 20–27 | `severe` | 5 |
| q9 >= 1 | — | 4 (override) |
| q9 >= 3 | — | 5 (override) |

**Side effects:**
- Cập nhật `last_interaction_time` của user
- Nếu `q9_value >= 1` → tự động tạo Safety Event (xem mục 9)

**Errors:**
- `422` — `phq_type` không hợp lệ hoặc số lượng `scores` sai

---

### `GET /phq/pending`
Kiểm tra user có bảng hỏi PHQ nào đang chờ hiển thị không. **Frontend gọi khi app vào foreground.**

**Response 200 — Có pending:**
```json
{
  "pending": [
    {
      "id":         "uuid",
      "phq_type":   "phq9",
      "reason":     "escalate_phq9",
      "created_at": "2026-04-15T08:00:00+00:00"
    }
  ],
  "has_phq2": false,
  "has_phq9": true
}
```

**Response 200 — Không có pending:**
```json
{
  "pending": [],
  "has_phq2": false,
  "has_phq9": false
}
```

| Field | Ý nghĩa |
|-------|---------|
| `has_phq2` | `true` → hiển thị bộ câu hỏi PHQ-2 |
| `has_phq9` | `true` → hiển thị bộ câu hỏi PHQ-9 |
| `pending[].reason` | `"scout"` / `"scheduled"` / `"escalate_phq9"` / `"manual"` |

> Pending tự clear khi user submit PHQ tương ứng. Khi PHQ-2 trả `escalate_phq9`, backend tự tạo pending PHQ-9.

---

### `POST /phq/trigger`
Kích hoạt hiển thị bộ câu hỏi PHQ cho 1 user cụ thể. **Không cần auth user** — dùng bởi Scout engine, scheduler, hoặc admin.

**Request body:**
```json
{
  "uid":      "firebase_uid_of_user",
  "phq_type": "phq2",
  "reason":   "scout"
}
```

| Field | Type | Bắt buộc | Mô tả |
|-------|------|----------|-------|
| `uid` | string | Có | Firebase UID của user |
| `phq_type` | string | Có | `"phq2"` hoặc `"phq9"` |
| `reason` | string | Không | `"scheduled"` (default) / `"scout"` / `"escalate_phq9"` / `"manual"` |

**Response 201 — Thành công:**
```json
{
  "triggered":  true,
  "pending_id": "uuid",
  "phq_type":   "phq2",
  "reason":     "scout",
  "created_at": "2026-04-15T08:00:00+00:00"
}
```

**Response 200 — User đang bị paused:**
```json
{
  "triggered": false,
  "reason":    "User đang tạm dừng đến 2026-05-15T..."
}
```

**Errors:**
- `422` — `phq_type` không hợp lệ

---

### `POST /phq/reject`
User bấm "Để sau" — snooze bộ câu hỏi PHQ đến khung giờ tối ưu tiếp theo (17:00 VN hôm sau). Sau 3 lần từ chối liên tiếp, tạm dừng 30 ngày.

**Request body:**
```json
{ "phq_type": "phq2" }
```

**Response 200 — Snooze (lần 1–2):**
```json
{
  "rejection_count": 1,
  "paused":          false,
  "paused_until":    null,
  "show_after":      "2026-04-19T10:00:00+00:00"
}
```

> `show_after` là thời điểm UTC mà `GET /phq/pending` sẽ trả về item này trở lại. Frontend không cần xử lý — chỉ cần gọi `GET /phq/pending` khi app resume, backend tự lọc.

**Response 200 — Tạm dừng (lần 3+):**
```json
{
  "rejection_count": 3,
  "paused":          true,
  "paused_until":    "2026-05-18T10:00:00+00:00",
  "show_after":      null
}
```

> Pending bị đánh dấu `dismissed`. Không hiển thị nữa cho đến khi hết pause hoặc có trigger mới.

---

### `GET /phq/history`
Lấy lịch sử các lần làm PHQ.

**Query params:**

| Param | Type | Default | Mô tả |
|-------|------|---------|-------|
| `type` | string | `"all"` | `"phq2"` / `"phq9"` / `"all"` |
| `limit` | int | 20 | Số bản ghi tối đa (1–100) |

**Response 200:**
```json
{
  "records": [
    {
      "id":         "uuid",
      "phq_type":   "phq2",
      "scores":     [1, 2],
      "total":      3,
      "source":     "self_request",
      "created_at": "2026-04-14T10:30:00+00:00"
    },
    {
      "id":               "uuid",
      "phq_type":         "phq9",
      "scores":           [1, 2, 2, 1, 1, 0, 2, 1, 0],
      "total":            10,
      "somatic_score":    4,
      "cognitive_score":  5,
      "q9_value":         0,
      "severity":         "moderate",
      "monitoring_level": 3,
      "created_at":       "2026-04-01T15:00:00+00:00"
    }
  ]
}
```

---

## 8. Mood (Check-in cảm xúc)

### `POST /mood/checkin`
Lưu check-in cảm xúc nhanh. Cũng cập nhật `last_interaction_time`.

**Request body:**
```json
{
  "mood_level": 2,
  "mood_score": 2,
  "has_video":  false,
  "video_id":   null
}
```

| Field | Type | Bắt buộc | Mô tả |
|-------|------|----------|-------|
| `mood_level` | int | Có | 1=Vui, 2=Buồn, 3=Stress, 4=Hào hứng, 5=Bình thường, 6=Mệt |
| `mood_score` | int | Có | Mức độ cảm xúc theo thang 1–5 |
| `has_video` | bool | Có | `true` nếu kèm video selfie |
| `video_id` | string | Không | UUID video đã upload (bắt buộc khi `has_video=true`) |

**Response 201:**
```json
{
  "id":         "uuid",
  "created_at": "2026-04-14T10:30:00+00:00"
}
```

**Errors:**
- `422` — `mood_level` ngoài 1–6, `mood_score` ngoài 1–5, hoặc `has_video=true` mà thiếu `video_id`

---

## 9. Safety Event (Sự kiện an toàn)

> **Đây là endpoint quan trọng nhất.** Bản ghi safety event **không bao giờ tự xóa**.

### `POST /safety-event`
Ghi nhận sự kiện an toàn khi câu 9 PHQ-9 >= 1. Thường được gọi tự động từ `POST /phq/submit`, nhưng có thể gọi trực tiếp.

**Request body:**
```json
{
  "q9_value":   2,
  "phq9_total": 18,
  "phq9_id":    "uuid-phq9-record"
}
```

| Field | Type | Bắt buộc | Mô tả |
|-------|------|----------|-------|
| `q9_value` | int | Có | `1`, `2`, hoặc `3` |
| `phq9_total` | int | Có | Tổng điểm PHQ-9 tại thời điểm |
| `phq9_id` | string | Không | UUID liên kết bản ghi PHQ-9 |

**Response 201:**
```json
{
  "id":                        "uuid",
  "user_id":                   "firebase_uid",
  "q9_value":                  2,
  "phq9_total":                18,
  "phq9_id":                   "uuid-phq9-record",
  "action_taken":              "active_ideation",
  "counselor_notified":        true,
  "counselor_notify_deadline": "2026-04-14T22:30:00+00:00",
  "created_at":                "2026-04-14T10:30:00+00:00",
  "permanent":                 true
}
```

**Phân tầng theo q9_value:**

| q9_value | Ý nghĩa | action_taken | Deadline thông báo tư vấn viên |
|---------|---------|--------------|-------------------------------|
| `1` | Ý tưởng thụ động | `passive_ideation` | 24 giờ |
| `2` | Ý tưởng chủ động | `active_ideation` | 12 giờ |
| `3` | Khủng hoảng | `crisis_same_day` | Tức thì (= thời điểm tạo) |

**Lưu ý:**
- Bản ghi được lưu cả ở `safety_events/{id}` (top-level) lẫn `users/{uid}/safety_events/{id}`
- `permanent: true` — không bao giờ tự xóa

**Errors:**
- `422` — `q9_value` không phải 1, 2, hoặc 3

---

## 10. Monitoring (Mức theo dõi)

### `PUT /monitoring/update`
Cập nhật mức theo dõi của user (1–5).

**Request body:**
```json
{
  "level":      3,
  "reason":     "phq9_score_12",
  "phq9_total": 12,
  "q9_value":   0
}
```

| Field | Type | Mô tả |
|-------|------|-------|
| `level` | int | 1–5 (xem bảng bên dưới) |
| `reason` | string | Lý do thay đổi mức |
| `phq9_total` | int | Điểm PHQ-9 trigger thay đổi |
| `q9_value` | int | Giá trị câu 9 (0–3) |

**Bảng monitoring level:**

| level | Tên | Chu kỳ PHQ-2 | Chu kỳ PHQ-9 |
|-------|-----|-------------|-------------|
| `1` | Tiêu chuẩn | 90 ngày | — |
| `2` | Nâng cao | 30 ngày | 90 ngày |
| `3` | Cao | 14 ngày | 14 ngày |
| `4` | Tích cực | 7 ngày | 14 ngày |
| `5` | Khủng hoảng | 3 ngày | 7 ngày |

**Response 200:**
```json
{
  "level":          3,
  "level_name":     "Cao",
  "next_phq2_days": 14,
  "next_phq9_days": 14,
  "updated_at":     "2026-04-14T10:30:00+00:00"
}
```

**Errors:**
- `422` — `level` ngoài 1–5

---

### `GET /monitoring/status`
Lấy trạng thái theo dõi hiện tại. Gọi khi mở app để đồng bộ.

**Response 200:**
```json
{
  "level":               2,
  "level_name":          "Nâng cao",
  "next_phq2_date":      "2026-05-14T10:30:00+00:00",
  "next_phq9_date":      "2026-07-13T10:30:00+00:00",
  "rejection_count":     0,
  "paused_until":        null,
  "last_interaction_at": "2026-04-14T08:15:00+00:00",
  "silence_hours":       2.5
}
```

| Field | Mô tả |
|-------|-------|
| `level` | Mức theo dõi hiện tại (1–5) |
| `next_phq2_date` | Thời điểm cần làm PHQ-2 tiếp theo |
| `next_phq9_date` | Thời điểm cần làm PHQ-9 tiếp theo (`null` nếu level 1) |
| `rejection_count` | Số lần user bấm "Để sau" |
| `paused_until` | Tạm dừng nhắc nhở đến khi nào (`null` = không tạm dừng) |
| `last_interaction_at` | Thời điểm tương tác gần nhất |
| `silence_hours` | Số giờ kể từ tương tác cuối (`null` nếu chưa có) |

> User mới chưa có bản ghi monitoring sẽ nhận về level 1 mặc định.

---

## 11. Usage (Thời gian sử dụng app)

### `POST /usage/session`
Ghi lại 1 phiên sử dụng app. Gọi khi user thoát app hoặc chuyển sang background.

**Request body:**
```json
{
  "start_time":       "2026-04-14T10:00:00+00:00",
  "end_time":         "2026-04-14T10:15:00+00:00",
  "duration_seconds": 900,
  "screens":          ["home", "checkin", "chat"]
}
```

| Field | Type | Bắt buộc | Mô tả |
|-------|------|----------|-------|
| `start_time` | string | Có | ISO 8601 — thời điểm bắt đầu phiên |
| `end_time` | string | Có | ISO 8601 — thời điểm kết thúc phiên |
| `duration_seconds` | int | Có | Tổng số giây (>= 0) |
| `screens` | string[] | Không | Danh sách màn hình đã đi qua trong phiên |

**Response 201:**
```json
{
  "id":         "uuid",
  "created_at": "2026-04-14T10:15:00+00:00"
}
```

**Errors:**
- `422` — `duration_seconds` âm

---

## 12. Chat (Hội thoại với Mindy)

### `POST /chat/interaction`
Lưu toàn bộ 1 hội thoại với Mindy sau khi kết thúc. Cũng cập nhật `last_interaction_time`.

**Request body:**
```json
{
  "messages": [
    { "role": "user", "content": "Hôm nay mình buồn lắm" },
    { "role": "bot",  "content": "Mình hiểu, bạn có muốn kể thêm không?" },
    { "role": "user", "content": "Mình bị điểm kém môn Toán" },
    { "role": "bot",  "content": "Ôi, nghe có vẻ khó chịu thật. Bạn cảm thấy thế nào bây giờ?" }
  ],
  "conversation_id":  "uuid-do-client-tạo",
  "mood_before":      2,
  "mood_after":       4,
  "duration_seconds": 180
}
```

| Field | Type | Bắt buộc | Mô tả |
|-------|------|----------|-------|
| `messages` | object[] | Có | Danh sách tin nhắn. Mỗi tin gồm `role` (`"user"` hoặc `"bot"`) và `content` |
| `conversation_id` | string | Không | UUID do client sinh để nhóm nhiều interaction cùng 1 cuộc hội thoại dài |
| `mood_before` | int | Không | Tâm trạng trước khi chat (1–6) |
| `mood_after` | int | Không | Tâm trạng sau khi chat (1–6) |
| `duration_seconds` | int | Không | Tổng thời gian hội thoại (giây) |

**Response 201:**
```json
{
  "id":              "uuid-interaction",
  "conversation_id": "uuid-conversation",
  "created_at":      "2026-04-14T10:30:00+00:00"
}
```

> `conversation_id` trong response là giá trị client truyền vào, hoặc bằng `id` nếu client không truyền.

**Errors:**
- `422` — `messages` rỗng, `role` không hợp lệ, `content` rỗng, `mood_before`/`mood_after` ngoài 1–6

---

## 13. Journals (Nhật ký)

### `POST /journals`
Tạo mới 1 bản ghi nhật ký. Cũng cập nhật `last_interaction_time`.

**Request body:**
```json
{
  "title":      "Ngày hôm nay của mình",
  "content":    "Hôm nay mình cảm thấy khá hơn rồi...",
  "mood_level": 4,
  "tags":       ["học tập", "gia đình"]
}
```

| Field | Type | Bắt buộc | Mô tả |
|-------|------|----------|-------|
| `title` | string | Có | Tiêu đề nhật ký |
| `content` | string | Có | Nội dung nhật ký |
| `mood_level` | int | Không | Tâm trạng khi viết (1–6) |
| `tags` | string[] | Không | Nhãn phân loại |

**Response 201:**
```json
{
  "id":         "uuid",
  "user_id":    "firebase_uid",
  "title":      "Ngày hôm nay của mình",
  "content":    "Hôm nay mình cảm thấy khá hơn rồi...",
  "mood_level": 4,
  "tags":       ["học tập", "gia đình"],
  "created_at": "2026-04-14T10:30:00+00:00",
  "updated_at": "2026-04-14T10:30:00+00:00"
}
```

**Errors:**
- `422` — `title` hoặc `content` rỗng, `mood_level` ngoài 1–6

---

### `GET /journals`
Lấy danh sách nhật ký, sắp xếp mới nhất trước.

**Query params:**

| Param | Type | Default | Mô tả |
|-------|------|---------|-------|
| `limit` | int | 20 | Số bản ghi tối đa (1–100) |

**Response 200:**
```json
{
  "records": [
    {
      "id":         "uuid",
      "title":      "Ngày hôm nay",
      "content":    "...",
      "mood_level": 4,
      "tags":       ["học tập"],
      "created_at": "2026-04-14T10:30:00+00:00",
      "updated_at": "2026-04-14T10:30:00+00:00"
    }
  ],
  "count": 1
}
```

---

### `GET /journals/{journal_id}`
Lấy chi tiết 1 bản ghi nhật ký.

**Response 200:** Object journal đầy đủ (giống 1 phần tử trong `records`).

**Errors:**
- `404` — journal_id không tồn tại hoặc không thuộc user

---

### `PATCH /journals/{journal_id}`
Cập nhật một phần nhật ký. Chỉ gửi field cần thay đổi.

**Request body** (tất cả optional):
```json
{
  "title":      "Tiêu đề mới",
  "content":    "Nội dung đã chỉnh sửa...",
  "mood_level": 3,
  "tags":       ["stress"]
}
```

**Response 200:** Object journal sau khi cập nhật.

**Errors:**
- `404` — journal_id không tồn tại
- `422` — Không có field nào hoặc giá trị không hợp lệ

---

### `DELETE /journals/{journal_id}`
Xóa 1 bản ghi nhật ký.

**Response 204:** Không có body.

**Errors:**
- `404` — journal_id không tồn tại

---

## Tổng hợp tất cả endpoints

| # | Method | Path | Auth | Mô tả ngắn |
|---|--------|------|------|------------|
| 1 | GET | `/health` | Không | Health check |
| 2 | GET | `/auth/me` | Có | Tạo/lấy profile user |
| 3 | POST | `/videos/upload` | Có | Upload video phân tích cảm xúc |
| 4 | GET | `/videos/analysis/{video_id}` | Có | Lấy kết quả phân tích video |
| 5 | GET | `/history/me` | Có | Lịch sử phân tích video |
| 6 | GET | `/history/me/{video_id}` | Có | Chi tiết 1 bản ghi |
| 7 | GET | `/schedules` | Có | Danh sách lịch học |
| 8 | POST | `/schedules` | Có | Tạo buổi học mới |
| 9 | PATCH | `/schedules/{id}` | Có | Cập nhật buổi học |
| 10 | DELETE | `/schedules/{id}` | Có | Xóa buổi học |
| 11 | GET | `/schedules/upcoming` | Có | Sự kiện sắp tới + tổng áp lực |
| 12 | PUT | `/notifications/fcm-token` | Có | Đăng ký FCM token |
| 13 | DELETE | `/notifications/fcm-token` | Có | Xóa FCM token (logout) |
| 14 | POST | `/phq/submit` | Có | Nộp kết quả PHQ-2/9 |
| 15 | GET | `/phq/pending` | Có | Kiểm tra PHQ đang chờ hiển thị |
| 16 | POST | `/phq/trigger` | **Không** | Kích hoạt PHQ cho 1 user (Scout/admin) |
| 17 | POST | `/phq/reject` | Có | User bấm "Để sau" — snooze pending |
| 18 | GET | `/phq/history` | Có | Lịch sử PHQ |
| 18 | POST | `/mood/checkin` | Có | Check-in cảm xúc |
| 19 | POST | `/safety-event` | Có | Ghi nhận sự kiện an toàn |
| 20 | PUT | `/monitoring/update` | Có | Cập nhật mức theo dõi |
| 21 | GET | `/monitoring/status` | Có | Lấy trạng thái theo dõi |
| 22 | POST | `/usage/session` | Có | Ghi phiên sử dụng app |
| 23 | POST | `/chat/interaction` | Có | Lưu hội thoại với Mindy |
| 24 | POST | `/journals` | Có | Tạo nhật ký mới |
| 25 | GET | `/journals` | Có | Danh sách nhật ký |
| 26 | GET | `/journals/{id}` | Có | Chi tiết 1 nhật ký |
| 27 | PATCH | `/journals/{id}` | Có | Cập nhật nhật ký |
| 28 | DELETE | `/journals/{id}` | Có | Xóa nhật ký |
| 29 | POST | `/scout/run` | **Không** | Kích hoạt Wellbeing Scout ngay (test) |

---

## 14. Scout (Test — Kích hoạt thủ công)

### `POST /scout/run`
Kích hoạt Wellbeing Scout ngay lập tức, bỏ qua giới hạn giờ 7:00–23:00. **Không cần auth** — dùng để test.

**Query params:**

| Param | Type | Default | Mô tả |
|-------|------|---------|-------|
| `uid` | string | `null` | Firebase UID. Bỏ trống = chạy tất cả users |

**Response 200 — Thành công:**
```json
{
  "triggered":   true,
  "users_count": 1,
  "results": [
    {
      "uid":               "firebase_uid",
      "consent_level":     2,
      "signals": {
        "silence_hours":     20.5,
        "phq2_trend":        0.5,
        "academic_pressure": 3,
        "interaction_ratio": 0.67,
        "flat_affect_avg":   0.35,
        "duchenne_avg":      0.45
      },
      "risk_score":        4.5,
      "action":            "mark_priority",
      "notification_sent": false
    }
  ]
}
```

**Các giá trị `action` có thể:**

| action | Ý nghĩa |
|--------|---------|
| `safety_protocol` | Kích hoạt giao thức khủng hoảng |
| `schedule_phq2` | Tạo pending PHQ-2 + gửi notification |
| `gentle_reminder` | Gửi nhắc nhở nhẹ nhàng |
| `mark_priority` | Đánh dấu ưu tiên (không gửi notification) |
| `log_only` | Chỉ ghi log |

**Response 200 — User không tồn tại:**
```json
{ "detail": "User abc123 không tồn tại" }
```

---

## Firestore Collections

| Collection | Lưu dưới | Mô tả | Tự xóa sau |
|-----------|---------|-------|-----------|
| `users` | top-level | Profile user + last_interaction_time + fcm_token | Không |
| `users/{uid}/analyses` | user | Kết quả phân tích video | Không xóa |
| `users/{uid}/schedules` | user | Lịch học | Không xóa |
| `users/{uid}/phq_results` | user | Kết quả PHQ-2/9 | 1 năm |
| `users/{uid}/pending_phq` | user | PHQ đang chờ hiển thị (trigger/pending system) | Tự clear khi submit |
| `users/{uid}/emotion_log` | user | Check-in cảm xúc | 90 ngày |
| `users/{uid}/safety_events` | user | Safety events (bản sao) | **Không bao giờ** |
| `users/{uid}/monitoring` | user | Trạng thái theo dõi (doc "current") | Không xóa |
| `users/{uid}/usage_sessions` | user | Phiên sử dụng app | 90 ngày |
| `users/{uid}/chat_interactions` | user | Lịch sử chat với Mindy | 90 ngày |
| `users/{uid}/journals` | user | Nhật ký cá nhân | Không xóa |
| `safety_events` | top-level | Safety events cho tư vấn viên query | **Không bao giờ** |
