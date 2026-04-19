# Frontend Integration Guide — MIND Quy Tắc Vận Hành

Tài liệu này mô tả các thay đổi frontend cần thực hiện để ghép với backend mới theo quy tắc vận hành hệ thống MIND 3 tầng.

---

## Mục lục

1. [Hệ thống Consent (Mức đồng thuận)](#1-hệ-thống-consent-mức-đồng-thuận)
2. [**Luồng Trigger & Pending PHQ (MỚI)**](#2-luồng-trigger--pending-phq-mới)
3. [Luồng PHQ-2 mới (4 nhánh)](#3-luồng-phq-2-mới-4-nhánh)
4. [Luồng chuyển tiếp PHQ-2 → PHQ-9](#4-luồng-chuyển-tiếp-phq-2--phq-9)
5. [Xử lý câu 9 — Giao thức an toàn](#5-xử-lý-câu-9--giao-thức-an-toàn)
6. [Từ chối bảng hỏi](#6-từ-chối-bảng-hỏi)
7. ["Để sau" PHQ-9](#7-để-sau-phq-9)
8. [Video upload — kiểm tra consent](#8-video-upload--kiểm-tra-consent)
9. [Notification — thay đổi hành vi](#9-notification--thay-đổi-hành-vi)
10. [Tổng hợp API mới](#10-tổng-hợp-api-mới)
11. [Checklist việc cần làm](#11-checklist-việc-cần-làm)

---

## 1. Hệ thống Consent (Mức đồng thuận)

### Thay đổi gì?

Mỗi user giờ có field `consent_level` (1, 2, hoặc 3). Field này quyết định user được phép dùng tính năng nào.

| Tính năng | Mức 1 | Mức 2 | Mức 3 |
|-----------|-------|-------|-------|
| Trả lời PHQ-2 / PHQ-9 dạng chữ | Được | Được | Được |
| Tự đánh giá tâm trạng (thang 1–5) | Được | Được | Được |
| Quay video 5 giây (phân tích khuôn mặt) | **Không** | Được | Được |
| Nhận thông báo nhắc nhở chủ động | **Không** | **Không** | Được |
| Cảnh báo tư vấn viên khi nguy cơ cao | Được | Được | Được |
| Giao thức an toàn khi có dấu hiệu tự hại | Được | Được | Được |

### Frontend cần làm

**a) Màn hình đăng ký (Onboarding):**
- Sau khi đăng nhập lần đầu, hiển thị màn hình chọn mức đồng thuận.
- Giải thích rõ ràng 3 mức: user chọn 1 trong 3.
- Gọi API để lưu:

```
PUT /auth/consent
Body: { "consent_level": 2 }

Response 200:
{ "consent_level": 2 }
```

**b) Màn hình Cài đặt:**
- Cho phép user đổi mức đồng thuận bất kỳ lúc nào.
- Gọi cùng API `PUT /auth/consent`.

**c) `GET /auth/me` response thay đổi:**

```json
{
  "user_id":       "firebase_uid",
  "email":         "user@example.com",
  "name":          "Nguyễn Văn A",
  "picture":       "https://...",
  "consent_level": 2           // ← MỚI
}
```

**d) Ẩn/hiện tính năng dựa trên consent_level:**
- `consent_level < 2` → Ẩn nút quay video trong check-in cảm xúc.
- `consent_level < 3` → Không hiện badge/thông báo nhắc nhở chủ động (vẫn nhận safety alert).

---

## 2. Luồng Trigger & Pending PHQ (MỚI)

### Vấn đề cũ

Trước đây, **frontend không có cách nào biết khi nào cần hiển thị PHQ** cho user. Backend (Scout engine) chỉ gửi FCM notification — nếu user không nhận được notification thì bỏ lỡ.

### Giải pháp: Pending PHQ system

Backend giờ tạo **"pending PHQ"** mỗi khi hệ thống quyết định user cần làm bảng hỏi. Frontend **poll endpoint `GET /phq/pending`** để biết cần hiển thị gì.

### Luồng hoàn chỉnh:

```
┌──────────────────────────────────────────────────────────────────┐
│  BACKEND                           FRONTEND                     │
│                                                                  │
│  Scout tính risk > 6               App mở / vào foreground      │
│       │                                   │                      │
│       ▼                                   ▼                      │
│  POST /phq/trigger                 GET /phq/pending              │
│  {uid, phq_type: "phq2"}          ─────────────────►             │
│       │                                   │                      │
│       ▼                                   ▼                      │
│  Tạo pending_phq record            Nhận has_phq2: true          │
│  + gửi FCM notification                  │                      │
│                                           ▼                      │
│                                    Hiển thị bộ câu hỏi PHQ-2   │
│                                           │                      │
│                                           ▼                      │
│                                    User trả lời, total >= 3     │
│                                           │                      │
│                                           ▼                      │
│                                    POST /phq/submit (phq2)      │
│                                           │                      │
│                                           ▼                      │
│  Backend trả decision:             Nhận "escalate_phq9"         │
│  "escalate_phq9"                          │                      │
│       │                                   ▼                      │
│       ▼                            GET /phq/pending              │
│  Tự động tạo                       ─────────────────►            │
│  pending PHQ-9                            │                      │
│                                           ▼                      │
│                                    Nhận has_phq9: true          │
│                                           │                      │
│                                           ▼                      │
│                                    Hiển thị bộ câu hỏi PHQ-9   │
│                                           │                      │
│                                           ▼                      │
│                                    POST /phq/submit (phq9)      │
│                                           │                      │
│                                           ▼                      │
│  Backend clear pending             Nhận severity + level        │
│  + cập nhật monitoring             Hiển thị kết quả             │
└──────────────────────────────────────────────────────────────────┘
```

### API mới:

#### `GET /phq/pending` — Kiểm tra PHQ đang chờ

Frontend gọi endpoint này **mỗi khi app vào foreground** hoặc khi mở màn hình chính.

**Cần auth:** Có (Bearer token)

**Response 200:**
```json
{
  "pending": [
    {
      "id":         "uuid",
      "phq_type":   "phq2",
      "reason":     "scout",
      "created_at": "2026-04-15T08:00:00+00:00"
    }
  ],
  "has_phq2": true,
  "has_phq9": false
}
```

| Field | Ý nghĩa |
|-------|---------|
| `has_phq2` | `true` → Frontend **PHẢI** hiển thị bộ câu hỏi PHQ-2 |
| `has_phq9` | `true` → Frontend **PHẢI** hiển thị bộ câu hỏi PHQ-9 |
| `pending[].reason` | Lý do: `"scout"` (hệ thống phát hiện) / `"scheduled"` (đến lịch) / `"escalate_phq9"` (từ PHQ-2) / `"manual"` (admin) |

**Khi không có pending:**
```json
{
  "pending": [],
  "has_phq2": false,
  "has_phq9": false
}
```

#### `POST /phq/trigger` — Kích hoạt PHQ cho user (dùng bởi backend/admin)

**Không cần auth user** — dùng bởi Scout engine, scheduler, hoặc admin dashboard.

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
| `uid` | string | Có | Firebase UID của user cần hiển thị PHQ |
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

**Response 200 — User đang bị paused (từ chối 3 lần):**
```json
{
  "triggered": false,
  "reason":    "User đang tạm dừng đến 2026-05-15T..."
}
```

### Frontend cần làm:

1. **Khi app mở / vào foreground:** gọi `GET /phq/pending`
2. **Nếu `has_phq2: true`:** hiển thị popup/màn hình "Kiểm tra tâm trạng" (PHQ-2)
3. **Nếu `has_phq9: true`:** hiển thị popup "Mình muốn hỏi thêm vài câu" (PHQ-9)
4. **Nếu cả hai `false`:** không hiển thị gì
5. **Sau khi user submit PHQ:** backend tự clear pending — lần poll tiếp sẽ trả `pending: []`
6. **Nếu PHQ-2 trả `escalate_phq9`:** gọi lại `GET /phq/pending` → sẽ thấy `has_phq9: true`

### Tự động hóa:

- **Scout engine** (chạy mỗi 2h) tự gọi `POST /phq/trigger` khi phát hiện risk cao
- **Khi PHQ-2 escalate:** backend tự tạo pending PHQ-9 (frontend chỉ cần poll lại)
- **Khi user submit:** pending tự clear, `rejection_count` reset về 0

## 3. Luồng PHQ-2 mới (4 nhánh)

### Thay đổi gì?

Trước đây `POST /phq/submit` cho PHQ-2 chỉ trả `id` + `created_at`. Giờ trả thêm `decision` để app biết bước tiếp theo.

### Response mới khi submit PHQ-2:

```json
{
  "id":              "uuid",
  "created_at":      "2026-04-15T...",
  "decision":        "escalate_phq9",     // ← MỚI
  "next_phq2_days":  null                 // ← MỚI
}
```

### Bảng `decision` và hành động frontend:

| `decision` | Ý nghĩa | Frontend xử lý |
|------------|----------|-----------------|
| `"escalate_phq9"` | Cần đánh giá sâu PHQ-9 | Hiển thị popup chuyển tiếp PHQ-9 (xem mục 3) |
| `"shorten_interval"` | Theo dõi chặt hơn, PHQ-2 lại sau 7 ngày | Hiển thị thông báo nhẹ: "Cảm ơn bạn! Mình sẽ hỏi lại sau 1 tuần nhé." |
| `"normal"` | Bình thường, PHQ-2 lại sau 14 ngày | Hiển thị thông báo tích cực: "Tuyệt vời! Hẹn gặp lại sau 2 tuần." |

### Lưu ý quan trọng:
- **KHÔNG** hiển thị nhãn "PHQ-2" hay "Sàng lọc trầm cảm" cho user.
- Thay vào đó dùng: **"Kiểm tra tâm trạng"** — đóng khung như hoạt động chăm sóc sức khỏe bình thường.

---

## 4. Luồng chuyển tiếp PHQ-2 → PHQ-9

### Khi nào xảy ra?

Khi response PHQ-2 có `"decision": "escalate_phq9"`.

### Flow UI:

```
┌─────────────────────────────────────────────┐
│  Để hiểu rõ hơn, mình muốn hỏi thêm vài   │
│  câu nữa — chỉ khoảng 2 phút.              │
│  Bạn có muốn tiếp tục không?               │
│                                             │
│  ┌──────────┐    ┌──────────┐               │
│  │ Tiếp tục │    │ Để sau   │               │
│  └──────────┘    └──────────┘               │
└─────────────────────────────────────────────┘
```

**Nếu chọn "Tiếp tục":**
- Hiển thị câu 3 đến câu 10 của PHQ-9 (KHÔNG lặp lại câu 1, 2 — đã trả lời ở PHQ-2).
- Câu 10 (functional_impact): "Nếu bạn gặp bất kỳ vấn đề nào ở trên, chúng làm khó khăn cho việc học tập, làm việc, hoặc hòa hợp với mọi người ở mức nào?" — chọn: Không khó khăn / Hơi khó khăn / Khá khó khăn / Cực kỳ khó khăn (0/1/2/3, không tính vào tổng điểm).
- Khi submit lên backend:

```
POST /phq/submit
{
  "phq_type": "phq9",
  "scores": [q1, q2, q3, q4, q5, q6, q7, q8, q9],  // q1,q2 lấy từ PHQ-2 vừa làm
  "total": tổng 9 câu,
  "source": "scheduled",
  "somatic_score": q3 + q4 + q5 + q8,       // Điểm phụ cơ thể (thang 0–12)
  "cognitive_score": q1 + q2 + q6 + q7 + q9, // Điểm phụ nhận thức (thang 0–15)
  "functional_impact": 0|1|2|3,              // Câu 10, không tính điểm
  "q9_value": q9,                            // Quan trọng! Câu về tự hại
  "triggered_by_phq2_id": "uuid của PHQ-2 vừa làm"
}
```

**Nếu chọn "Để sau":** → Xem mục 6.

### Response PHQ-9:

```json
{
  "id":               "uuid",
  "created_at":       "2026-04-15T...",
  "severity":         "moderate",
  "monitoring_level": 3,
  "next_phq9_date":   "2026-04-29T...",
  "next_phq2_date":   null
}
```

### Hành động frontend theo `severity`:

| severity | Mức | Hiển thị cho user |
|----------|-----|-------------------|
| `"minimal"` | Tối thiểu | Phản hồi tích cực: "Bạn đang ổn!" |
| `"mild"` | Nhẹ | Gợi ý kỹ thuật tự chăm sóc (giấc ngủ, vận động, chánh niệm) |
| `"moderate"` | Trung bình | Đề xuất phiên trị liệu VR |
| `"moderately_severe"` | Trung bình-nặng | Lên lịch phiên trị liệu VR |
| `"severe"` | Nặng | Hiển thị thông tin khẩn cấp (xem mục 4) |

---

## 5. Xử lý câu 9 — Giao thức an toàn

### Quan trọng nhất — KHÔNG BAO GIỜ bị tắt, bất kể consent.

### Khi nào kích hoạt?

Ngay khi user chọn giá trị ≥ 1 cho câu 9 ("Nghĩ rằng mình sẽ tốt hơn nếu chết đi, hoặc nghĩ đến việc tự làm hại bản thân").

### Frontend phải làm NGAY (chưa cần bấm nút hoàn thành):

Hiển thị thông tin hỗ trợ khẩn cấp:

```
┌─────────────────────────────────────────────┐
│  Nếu bạn đang gặp khó khăn, hãy liên hệ:  │
│                                             │
│  📞 Tổng đài sức khỏe tâm thần:            │
│     1800 599 920 (miễn phí)                 │
│                                             │
│  📞 Đường dây nóng thanh thiếu niên:        │
│     1800 599 100 (miễn phí)                 │
│                                             │
│  📞 Cấp cứu: 115                           │
│                                             │
│  📧 Thông tin liên hệ tư vấn viên trường   │
└─────────────────────────────────────────────┘
```

**Hiển thị ngay khi user chọn, không đợi submit.** Phần này là client-side, không cần gọi API.

Phía backend sẽ tự động tạo safety event và thông báo tư vấn viên khi nhận kết quả PHQ-9 có `q9_value >= 1`.

---

## 6. Từ chối bảng hỏi

### Khi nào xảy ra?

Khi app hiển thị PHQ-2 hoặc PHQ-9 (từ notification hoặc lịch) và user bấm đóng / từ chối.

### API:

```
POST /phq/reject
Body: { "phq_type": "phq2" }

Response 200:
{
  "rejection_count": 2,
  "paused": false,
  "paused_until": null
}
```

### Khi `rejection_count` đạt 3:

```json
{
  "rejection_count": 3,
  "paused": true,
  "paused_until": "2026-05-15T..."
}
```

### Frontend xử lý:

| `rejection_count` | Hành vi |
|---------------------|---------|
| 1–2 | Ghi nhận, tiếp tục lịch bình thường. Có thể hiện nhẹ: "OK, hẹn lần sau nhé!" |
| ≥ 3 (`paused: true`) | Dừng hiển thị bảng hỏi. Hiện: "Mình sẽ không hỏi nữa trong thời gian tới." |

**Reset:** Khi user tự hoàn thành bất kỳ bảng hỏi nào, `rejection_count` tự động về 0.

---

## 7. "Để sau" PHQ-9

### Khi nào xảy ra?

Khi popup chuyển tiếp PHQ-2 → PHQ-9 hiện lên và user chọn "Để sau".

### API:

```
POST /phq/defer-phq9
Body: { "phq2_id": "uuid-phq2-vừa-làm" }   // optional

Response 200 (lần 1):
{
  "defer_count": 1,
  "remind_at":   "2026-04-15T18:00:00+00:00",
  "message":     "Sẽ nhắc lại sau 4 giờ."
}

Response 200 (lần 2):
{
  "defer_count":      2,
  "remind_at":        null,
  "message":          "Không nhắc nữa. Mức theo dõi đã được nâng lên.",
  "monitoring_level": 2
}
```

### Frontend xử lý:

| Lần | Hành vi |
|-----|---------|
| 1 | Đóng popup. Sau 4 giờ → hiện lại popup tương tự (dùng local notification hoặc push nếu có) |
| 2 | Đóng popup. Không hiện lại nữa. Có thể hiện toast: "OK, mình sẽ theo dõi bạn." |

---

## 8. Video upload — kiểm tra consent

### Thay đổi gì?

`POST /videos/upload` giờ trả `403` nếu user có `consent_level < 2`.

```json
{ "detail": "Cần mức đồng thuận ≥ 2 để quay video" }
```

### Frontend xử lý:

- Kiểm tra `consent_level` từ `GET /auth/me` trước khi hiện nút quay video.
- Nếu `consent_level < 2`: ẩn nút hoặc hiện popup mời nâng cấp consent.
- Nếu bị lọt `403`: hiện thông báo và link đến trang Cài đặt đồng thuận.

---

## 9. Notification — thay đổi hành vi

### Backend thay đổi gì?

- Không còn gửi random notification cho tất cả user.
- Giờ chỉ gửi khi Scout engine quyết định (dựa trên 6 tín hiệu hành vi).
- Giới hạn: **tối đa 2 lần/ngày**, **5 lần/tuần**.
- **Không gửi từ 22:00 đến 7:00** (giờ VN).
- `consent_level = 3` mới nhận nhắc nhở chủ động. Mức 1, 2 chỉ nhận safety alert.

### Frontend cần làm:

- Không cần thay đổi gì về FCM setup — vẫn dùng `PUT /notifications/fcm-token` như cũ.
- Xử lý notification payload bình thường.
- Nếu notification là loại PHQ → mở màn hình "Kiểm tra tâm trạng" (PHQ-2).

---

## 10. Tổng hợp API mới

### Endpoints mới hoàn toàn:

| Method | Path | Auth | Mô tả |
|--------|------|------|-------|
| `PUT` | `/auth/consent` | Có | Cập nhật mức đồng thuận |
| `GET` | `/phq/pending` | Có | **Frontend poll để biết cần hiển thị PHQ nào** |
| `POST` | `/phq/trigger` | **Không** | **Kích hoạt hiển thị PHQ cho 1 user** (dùng bởi Scout/admin) |
| `POST` | `/phq/reject` | Có | Ghi nhận từ chối bảng hỏi |
| `POST` | `/phq/defer-phq9` | Có | "Để sau" khi chuyển tiếp PHQ-9 |

### Endpoints thay đổi response:

| Method | Path | Thay đổi |
|--------|------|----------|
| `GET` | `/auth/me` | Thêm field `consent_level` |
| `POST` | `/phq/submit` (phq2) | Thêm `decision`, `next_phq2_days`. Khi escalate → tự tạo pending PHQ-9 |
| `POST` | `/phq/submit` (phq9) | Thêm `next_phq9_date`, `next_phq2_date`. Tự clear pending |
| `POST` | `/videos/upload` | Trả `403` nếu consent < 2 |

---

## 11. Checklist việc cần làm

### Ưu tiên cao (phải có trước khi release):

- [ ] **Poll pending PHQ khi mở app** — Gọi `GET /phq/pending` khi app vào foreground. Nếu `has_phq2`/`has_phq9` = true → hiển thị bộ câu hỏi tương ứng
- [ ] **Màn hình chọn Consent** — Hiển thị khi đăng ký lần đầu, giải thích 3 mức
- [ ] **Cài đặt Consent** — Cho đổi mức trong Settings, gọi `PUT /auth/consent`
- [ ] **Ẩn/hiện tính năng theo consent** — Video (≥2), notification nhắc nhở (=3)
- [ ] **Cập nhật luồng PHQ-2** — Đọc `decision` từ response, xử lý 3 trường hợp
- [ ] **Popup chuyển tiếp PHQ-9** — Khi `decision == "escalate_phq9"`, hiển thị "Tiếp tục" / "Để sau"
- [ ] **Poll pending sau escalate** — Sau khi PHQ-2 trả `escalate_phq9`, gọi lại `GET /phq/pending` → sẽ thấy `has_phq9: true`
- [ ] **Màn hình PHQ-9 từ PHQ-2** — Chỉ hiển thị câu 3–10 (giữ lại điểm Q1, Q2 từ PHQ-2)
- [ ] **Câu 10 PHQ-9** — Thêm câu hỏi functional_impact (không tính điểm)
- [ ] **Giao thức an toàn câu 9** — Hiển thị hotline NGAY khi user chọn q9 ≥ 1 (client-side, không đợi submit)
- [ ] **Nút từ chối** — Khi hiển thị PHQ-2/9, thêm nút X/đóng → gọi `POST /phq/reject`
- [ ] **"Để sau" PHQ-9** — Gọi `POST /phq/defer-phq9`, xử lý nhắc lại sau 4h

### Ưu tiên trung bình:

- [ ] **Hiển thị kết quả PHQ-9 theo severity** — Khác nhau cho minimal/mild/moderate/severe
- [ ] **Handle 403 video upload** — Hiện thông báo cần nâng consent
- [ ] **Nhãn hiển thị** — Dùng "Kiểm tra tâm trạng" thay vì "PHQ-2"/"Sàng lọc trầm cảm"
- [ ] **Phản hồi từ chối** — Hiện toast phù hợp khi `paused: true`

### Ưu tiên thấp (nice to have):

- [ ] **Hiển thị lịch PHQ tiếp theo** — Dùng `next_phq2_days` / `next_phq9_date` để hiện countdown
- [ ] **Local notification cho defer** — Nhắc lại sau 4h khi user chọn "Để sau"
- [ ] **Thông báo cải thiện** — Khi PHQ-9 giảm ≥ 5 điểm, hiển thị khích lệ

---

## Lưu ý chung

1. **Không bao giờ** hiển thị từ "trầm cảm", "PHQ", "sàng lọc" cho user. Luôn dùng "Kiểm tra tâm trạng", "Chăm sóc sức khỏe".
2. **Giao thức an toàn** (câu 9) luôn hoạt động bất kể consent level — không được ẩn.
3. **Tính điểm phụ** (somatic_score, cognitive_score) phải tính ở frontend trước khi gửi lên.
4. **Giữ lại điểm PHQ-2** khi chuyển tiếp PHQ-9 — frontend tự ghép Q1+Q2 cũ vào mảng scores 9 phần tử.
