# MIND VR - Luồng sử dụng phiên trị liệu thực tế ảo

## Tổng quan

Phiên trị liệu VR trên Meta Quest 3 là nơi sinh viên được đồng hành bởi NPC tư vấn viên trong không gian riêng tư hoàn toàn. NPC sử dụng nguyên tắc CBT (Cognitive Behavioral Therapy), thích nghi theo emotion_profile của sinh viên.

---

## Luồng tổng thể

```
[Module Can thiệp phát hiện cần VR]
        ↓
[Mobile gửi push notification đề xuất VR]
        ↓
[User đeo Quest 3, mở app MIND]
        ↓
[Splash → Auto-load emotion profile]
        ↓
[Fade in: Khu vườn yên tĩnh + NPC đứng chờ]
        ↓
[NPC: "Chào bạn, hôm nay bạn cảm thấy thế nào?"]
        ↓
[User nói chuyện tự nhiên ↔ NPC phản hồi CBT]
        ↓
[~10 phút: NPC đề xuất bài tập hít thở]
        ↓
[User thực hiện hoặc tiếp tục nói chuyện]
        ↓
[User kết thúc hoặc hết 20 phút]
        ↓
[NPC: "Cảm ơn bạn đã chia sẻ. Hẹn gặp lại."]
        ↓
[Fade out → Log session → Quay lại menu]
```

---

## Giai đoạn 1: TRƯỚC PHIÊN (Pre-Session)

### 1.1 Trigger từ Mobile

Module Lập kế hoạch Can thiệp quyết định mức can thiệp "phiên trị liệu VR" khi:
- PHQ-9 >= 10 (trầm cảm vừa trở lên)
- Hoặc tín hiệu hành vi xấu đi liên tục (Flat Affect cao, khoảng im lặng dài)

Mobile app hiển thị đề xuất: "Bạn có muốn thử phiên trị liệu VR?"

### 1.2 Trên Quest 3 - Khởi động

| Bước | Màn hình | Mô tả |
|------|----------|-------|
| 1 | Splash Screen | Logo MIND, thông điệp nhẹ nhàng: "Không gian này thuộc về bạn" |
| 2 | Đồng bộ dữ liệu | App fetch emotion_profile từ backend (4 chỉ số cảm xúc + điểm PHQ) |
| 3 | Chọn không gian | User chọn môi trường trị liệu |
| 4 | Fade in | Chuyển cảnh mượt mà vào môi trường VR |
| 5 | NPC xuất hiện | Giới thiệu: "Xin chào, mình là [tên]. Hôm nay mình sẽ ở đây cùng bạn." |

### 1.3 Môi trường trị liệu (chọn 1)

| Môi trường | Phù hợp với | Đặc điểm |
|------------|-------------|----------|
| Khu vườn yên tĩnh | Lo âu (mặc định) | Cây xanh, tiếng chim, ánh sáng ấm |
| Bãi biển hoàng hôn | Căng thẳng | Sóng biển, gió nhẹ, ánh hoàng hôn |
| Phòng thiền tĩnh lặng | Trầm cảm nặng | Tối giản, ánh nến, nhạc ambient nhẹ |

### 1.4 Dữ liệu emotion_profile truyền vào

```json
{
  "phq9_score": 12,
  "duchenne_smile_proxy": 0.3,
  "flat_affect_score": 0.7,
  "ear_ratio": 0.25,
  "head_pitch": -15.2,
  "silence_hours": 48,
  "last_intervention": "notification",
  "academic_events": ["Thi giữa kỳ trong 2 ngày"]
}
```

---

## Giai đoạn 2: TRONG PHIÊN (In-Session)

### 2.1 Pipeline kỹ thuật

```
Microphone (Quest 3)
    ↓
SherpaSTTStreaming (offline Vietnamese, 16kHz)
    - VAD phát hiện giọng nói
    - Im lặng 1-2s → coi là nói xong
    ↓
MindConversation (state machine)
    - State: Idle → Listening → Processing → Speaking
    - Inject emotion_profile vào system prompt
    ↓
LLM API (Groq/Gemini)
    - System prompt: tư vấn viên CBT tiếng Việt
    - Output: JSON {"text": "phản hồi"}
    ↓
TTS (SherpaOnnxTTS offline HOẶC VieNeuTTS server)
    ↓
AudioSource → NPC nói + animation talking
```

### 2.2 State Machine chi tiết

```
                    ┌──────────┐
                    │   IDLE   │ ← NPC đã nói xong, chờ user
                    └────┬─────┘
                         │ VAD phát hiện giọng nói
                         ↓
                    ┌──────────┐
                    │LISTENING │ ← Đang ghi nhận giọng user
                    └────┬─────┘
                         │ Im lặng > 1.5s (user nói xong)
                         ↓
                    ┌───────────┐
                    │PROCESSING │ ← Gửi text → LLM → chờ phản hồi
                    └────┬──────┘
                         │ Nhận phản hồi từ LLM
                         ↓
                    ┌──────────┐
                    │ SPEAKING │ ← TTS → NPC nói + animation
                    └────┬─────┘
                         │ Audio phát xong
                         ↓
                    ┌──────────┐
                    │   IDLE   │ → Quay lại chờ
                    └──────────┘
```

### 2.3 NPC mở đầu thích nghi theo PHQ

| PHQ-9 Score | Giọng điệu NPC | Ví dụ câu mở đầu |
|-------------|-----------------|-------------------|
| 5-9 (nhẹ) | Tươi sáng, tò mò | "Chào bạn! Hôm nay có chuyện gì vui không?" |
| 10-14 (vừa) | Ấm áp, quan tâm | "Chào bạn, mình nghe nói gần đây bạn hơi mệt. Bạn muốn chia sẻ không?" |
| 15+ (nặng) | Rất nhẹ nhàng, lắng nghe | "Chào bạn, mình ở đây rồi. Không cần vội, bạn cứ thoải mái nhé." |

### 2.4 Xử lý im lặng

| Thời gian im lặng | Hành động NPC |
|-------------------|---------------|
| 0-5s | Chờ bình thường (NPC idle animation) |
| 5-10s | NPC gật đầu nhẹ, thể hiện đang lắng nghe |
| 10-15s | NPC nhẹ nhàng: "Bạn vẫn ổn chứ? Không cần vội đâu." |
| 15-30s | NPC: "Nếu bạn muốn, mình có thể cùng làm một bài tập thở nhẹ nhé?" |
| >30s | NPC: "Mình vẫn ở đây. Khi nào bạn sẵn sàng thì nói nhé." (rồi chờ) |

### 2.5 Bài tập CBT xen kẽ

#### Bài tập 1: Hít thở 4-7-8

```
NPC: "Bạn có muốn thử bài tập hít thở không?"
    ↓ User đồng ý
NPC: "Hít vào trong 4 giây..."
    → Môi trường VR: ánh sáng sáng dần
NPC: "Giữ hơi thở trong 7 giây..."
    → Ánh sáng giữ nguyên
NPC: "Thở ra từ từ trong 8 giây..."
    → Ánh sáng mờ dần
    (Lặp 3 vòng)
```

#### Bài tập 2: Grounding 5-4-3-2-1

```
NPC: "Hãy nhìn xung quanh và kể cho mình 5 thứ bạn nhìn thấy..."
    → User nói: "Cái cây, bầu trời, con bướm..."
NPC: "Tốt lắm. Giờ hãy tưởng tượng 4 thứ bạn có thể chạm vào..."
    (tiếp tục 3 nghe, 2 ngửi, 1 nếm)
```

#### Bài tập 3: CBT Reframing

```
NPC: "Bạn vừa nói là 'mình học dở lắm'. 
      Nếu bạn thân bạn nói vậy, bạn sẽ trả lời thế nào?"
    → User suy nghĩ và trả lời
NPC: "Đúng rồi. Vậy bạn có thể nói với chính mình 
      điều tương tự không?"
```

### 2.6 Phát hiện rủi ro trong phiên

Nếu user nói các từ khóa nguy hiểm (tự harm, tự tử):

```
NPC: "Mình nghe thấy bạn đang rất khó khăn. 
      Bạn không đơn độc. 
      Mình muốn kết nối bạn với người có thể hỗ trợ tốt hơn."
    → Ghi log cảnh báo → Gửi alert cho tư vấn viên nhà trường
    → NPC KHÔNG tiếp tục thảo luận chủ đề này
    → Chuyển sang hỗ trợ ổn định cảm xúc
```

---

## Giai đoạn 3: KẾT THÚC PHIÊN (Post-Session)

### 3.1 Trigger kết thúc

- User nói "tạm biệt" / "mình muốn dừng"
- User nhấn nút kết thúc trên controller
- Hết thời gian tối đa (20 phút)
- NPC cảm nhận cuộc trò chuyện đã đến hồi kết tự nhiên

### 3.2 Luồng kết thúc

```
NPC: "Cảm ơn bạn đã chia sẻ hôm nay. 
      Mình rất vui vì bạn đã dành thời gian cho bản thân."
    ↓
NPC: "Một điều nhỏ cho ngày mai: [gợi ý cá nhân hóa]"
    ↓
Fade out (3-5 giây)
    ↓
Màn hình tóm tắt (tùy chọn):
    - "Phiên hôm nay: 15 phút"
    - "Bạn đã thực hiện 1 bài tập hít thở"
    ↓
Quay lại menu chính hoặc thoát app
```

### 3.3 Dữ liệu log (gửi lên Firestore)

```json
{
  "session_id": "uuid",
  "user_id": "user_123",
  "timestamp": "2026-04-11T14:30:00Z",
  "duration_minutes": 15,
  "exchange_count": 12,
  "exercises_completed": ["breathing_478"],
  "end_trigger": "user_initiated",
  "risk_alert_triggered": false,
  "pre_session_phq9": 12
}
```

**KHÔNG lưu:** nội dung hội thoại, audio, transcript (privacy-first).

---

## Hackathon 10 ngày - Scope cắt giảm

### Ưu tiên P0 (Bắt buộc cho demo)

- [ ] Pipeline STT → LLM → TTS hoạt động trên Quest 3 (đã có)
- [ ] 1 môi trường VR cơ bản (khu vườn)
- [ ] NPC 3D đơn giản với animation idle/talking
- [ ] System prompt CBT tiếng Việt (đã có)
- [ ] Emotion profile injection vào prompt (cơ chế đã có)
- [ ] Nút kết thúc phiên
- [ ] Session timer (20 phút max)

### Ưu tiên P1 (Nên có)

- [ ] Bài tập hít thở 4-7-8 với hiệu ứng ánh sáng
- [ ] Xử lý im lặng dài (NPC chủ động hỏi)
- [ ] Subtitle UI trong VR
- [ ] NPC thích nghi theo PHQ score
- [ ] Fade in/out transition

### Ưu tiên P2 (Nice to have)

- [ ] 3 môi trường lựa chọn
- [ ] NPC lip sync
- [ ] Grounding exercise
- [ ] CBT reframing exercise
- [ ] Session summary screen
- [ ] Firestore session logging
- [ ] Risk keyword detection + alert

---

## Files cần tạo/sửa

| File | Hành động | Mô tả |
|------|-----------|-------|
| `Assets/Scripts/VRTherapy/MindConversation.cs` | Sửa | Thêm session timer, emotion profile auto-load, xử lý im lặng dài |
| `Assets/Scripts/VRTherapy/VRSessionManager.cs` | Tạo mới | Quản lý lifecycle phiên VR (pre → in → post), fade transitions |
| `Assets/Scripts/VRTherapy/NPCController.cs` | Tạo mới | Điều khiển NPC animation (idle/talking/listening), lip sync cơ bản |
| `Assets/Scripts/VRTherapy/BreathingExercise.cs` | Tạo mới | Bài tập hít thở 4-7-8 có hướng dẫn, đồng bộ ánh sáng |
| `Assets/Scripts/VRTherapy/VRUIManager.cs` | Tạo mới | Subtitle, session timer display, nút kết thúc trong VR |
| `Assets/Scripts/VRTherapy/SessionLogger.cs` | Tạo mới | Log metadata phiên (local + Firestore) |
| `Assets/Scripts/VRTherapy/SilenceHandler.cs` | Tạo mới | Phát hiện im lặng dài, trigger NPC phản hồi chủ động |
| `Assets/Scenes/TherapyGarden.unity` | Tạo mới | Scene môi trường khu vườn trị liệu |



---

## NPC Animation Plan

### Danh sách 10 animation tối thiểu (tất cả đã có sẵn)

#### Nhóm 1: Di chuyển

| Animation | File | Nguồn |
|-----------|------|-------|
| Walk | `Walking (1).fbx` | animation/ |
| Sit Down | `Stand To Sit.fbx` | animation/ |
| Stand Up | `Sit To Stand.fbx` | animation/ |

#### Nhóm 2: Đứng

| Animation | File | Nguồn |
|-----------|------|-------|
| Standing Idle | `Idle_01_HUMANIK_TF1.fbx` | Rokoko |
| Standing Talking | `Idle_Conversation_HUMANIK_769.fbx` | Rokoko |
| Wave (chào + tạm biệt) | `Pilot_Wave_HUMANIK_769.fbx` | Rokoko |

#### Nhóm 3: Ngồi

| Animation | File | Nguồn |
|-----------|------|-------|
| Sitting Idle | `Sitting.fbx` | animation/ |
| Sitting Talking | `Sitting Talking.fbx` | animation/ |
| Sitting Listening | `Idle_WatchingSomething_HUMANIK_769.fbx` | Rokoko |
| Head Tilt | `HeadTilts_mixamo.fbx` | Rokoko |

### Luồng hoạt động NPC trong phiên

```
NPC đứng chờ          → Standing Idle
User vào              → Wave
NPC đi đến ghế        → Walk
NPC ngồi xuống        → Sit Down → Sitting Idle
NPC nói               → Sitting Talking
User nói, NPC nghe    → Sitting Listening
User nói hay          → Head Tilt (xen kẽ)
Kết thúc              → Stand Up → Wave → Walk (đi ra)
```

### Animator Parameters

| Parameter | Type | Mục đích |
|-----------|------|----------|
| `isWalking` | Bool | Đang đi |
| `isTalking` | Bool | Đang nói |
| `isSitting` | Bool | Đang ngồi |
| `sit` | Trigger | Ngồi xuống |
| `stand` | Trigger | Đứng dậy |
| `wave` | Trigger | Vẫy tay |
| `headTilt` | Trigger | Nghiêng đầu |

### Animation bổ sung (nếu có thời gian)

Các file có sẵn trong Rokoko có thể thêm sau:

| Animation | File | Mục đích |
|-----------|------|----------|
| Shrug | `Shrugging_mixamo.fbx` | "Không sao đâu" |
| Clap | `Clap_SlowClap_mixamo.fbx` | Khích lệ sau bài tập |
| Pointing | `Idle_Pointing_HUMANIK_769.fbx` | Chỉ về phía user |
| Laughs | `Laughs_mixamo.fbx` | Phản hồi vui |
| Left Turn (đứng) | `Left Turn.fbx` | Xoay người khi đứng |
| Right Turn (đứng) | `Right Turn.fbx` | Xoay người khi đứng |