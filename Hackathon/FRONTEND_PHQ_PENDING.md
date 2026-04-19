# Hướng dẫn Frontend: Hiển thị PHQ khi mở app

## Tổng quan

Khi Wellbeing Scout phát hiện sinh viên có nguy cơ, backend tự động tạo **pending PHQ** trong database. Frontend cần kiểm tra pending mỗi khi app vào foreground, nếu có thì hiển thị bộ câu hỏi PHQ tương ứng.

---

## Flow tổng thể

```
App vào foreground
       │
       ▼
GET /phq/pending
       │
       ├── has_phq9 = true  →  Hiển thị PHQ-9 (9 câu)
       │
       ├── has_phq2 = true  →  Hiển thị PHQ-2 (2 câu)
       │
       └── cả hai false     →  Không làm gì
```

---

## Bước 1: Gọi API kiểm tra pending

Gọi mỗi khi app resume từ background hoặc mới mở.

```
GET /phq/pending
Authorization: Bearer <firebase_id_token>
```

**Response mẫu — có pending:**
```json
{
  "pending": [
    {
      "id":         "550e8400-e29b-41d4-a716-446655440000",
      "phq_type":   "phq9",
      "reason":     "escalate_phq9",
      "created_at": "2026-04-18T14:30:45+00:00"
    }
  ],
  "has_phq2": false,
  "has_phq9": true
}
```

**Response mẫu — không có pending:**
```json
{
  "pending": [],
  "has_phq2": false,
  "has_phq9": false
}
```

### Logic xử lý

```
if (has_phq9)       → mở màn hình PHQ-9
else if (has_phq2)  → mở màn hình PHQ-2
else                → vào Home bình thường
```

PHQ-9 ưu tiên hơn PHQ-2 vì nghiêm trọng hơn.

---

## Bước 2: Hiển thị bộ câu hỏi

### PHQ-2 (2 câu)

Mở đầu: *"Trong 2 tuần qua, bạn có thường xuyên bị phiền bởi những vấn đề sau không?"*

| Câu | Nội dung |
|-----|----------|
| 1 | Ít hứng thú hoặc niềm vui khi làm việc |
| 2 | Cảm thấy buồn chán, trầm uất, hoặc tuyệt vọng |

Mỗi câu chọn 1 trong 4 mức:

| Lựa chọn | Điểm |
|-----------|------|
| Không bao giờ | 0 |
| Vài ngày | 1 |
| Hơn nửa số ngày | 2 |
| Gần như mỗi ngày | 3 |

**Ghi chú UI**: Trên giao diện, **không** gắn nhãn "PHQ-2" hay "Sàng lọc trầm cảm". Hiển thị là **"Kiểm tra tâm trạng"**.

### PHQ-9 (9 câu + 1 câu phụ)

Cùng mở đầu và thang điểm như PHQ-2. Thêm 7 câu:

| Câu | Nội dung |
|-----|----------|
| 3 | Khó ngủ, ngủ không sâu, hoặc ngủ quá nhiều |
| 4 | Cảm thấy mệt mỏi hoặc ít năng lượng |
| 5 | Ăn không ngon hoặc ăn quá nhiều |
| 6 | Cảm thấy tồi tệ về bản thân — hoặc rằng mình là người thất bại |
| 7 | Khó tập trung vào việc, như đọc sách hoặc xem điện thoại |
| 8 | Nói hoặc cử động chậm hơn bình thường; hoặc ngược lại — bồn chồn, bất an |
| 9 | Nghĩ rằng mình sẽ tốt hơn nếu chết đi, hoặc nghĩ đến việc tự làm hại bản thân |

**Câu 10** (không tính điểm): *"Nếu bạn gặp bất kỳ vấn đề nào ở trên, chúng làm khó khăn cho việc học tập, làm việc, hoặc hòa hợp với mọi người ở mức nào?"*
- Không khó khăn (0) / Hơi khó khăn (1) / Khá khó khăn (2) / Cực kỳ khó khăn (3)

---

## Bước 3: Gửi kết quả

### Submit PHQ-2

```
POST /phq/submit
Authorization: Bearer <firebase_id_token>
Content-Type: application/json
```

```json
{
  "phq_type": "phq2",
  "scores":   [2, 3],
  "total":    5,
  "source":   "scout"
}
```

`source` lấy từ `pending[].reason`. Nếu reason là `"escalate_phq9"` thì gửi `"phq2_escalation"`, các trường hợp khác gửi nguyên reason.

**Response:**
```json
{
  "id":             "uuid",
  "created_at":     "2026-04-18T14:35:20+00:00",
  "decision":       "escalate_phq9",
  "next_phq2_days": null
}
```

| decision | Frontend làm gì |
|----------|-----------------|
| `"escalate_phq9"` | Gọi lại `GET /phq/pending` — backend đã tự tạo pending PHQ-9, hiển thị tiếp PHQ-9 |
| `"shorten_interval"` | Hiển thị thông báo nhẹ, quay về Home |
| `"normal"` | Quay về Home |

### Submit PHQ-9

```json
{
  "phq_type":           "phq9",
  "scores":             [1, 2, 2, 1, 1, 0, 2, 1, 0],
  "total":              10,
  "somatic_score":      4,
  "cognitive_score":    5,
  "functional_impact":  2,
  "q9_value":           0,
  "source":             "phq2_escalation",
  "triggered_by_phq2_id": "uuid-phq2"
}
```

Cách tính điểm phụ:
- `somatic_score` = câu 3 + câu 4 + câu 5 + câu 8
- `cognitive_score` = câu 1 + câu 2 + câu 6 + câu 7 + câu 9
- `functional_impact` = câu 10
- `q9_value` = câu 9 (riêng biệt vì liên quan tự hại)

**Response:**
```json
{
  "id":               "uuid",
  "created_at":       "2026-04-18T14:35:20+00:00",
  "severity":         "moderate",
  "monitoring_level": 3,
  "next_phq9_date":   "2026-05-02T14:35:20+00:00",
  "next_phq2_date":   null
}
```

Sau khi submit thành công, pending tự động được clear ở backend.

---

## Bước 4: Xử lý "Để sau"

Khi user bấm "Để sau", **phải gọi API reject** để backend snooze pending:

```
POST /phq/reject
Authorization: Bearer <firebase_id_token>
Content-Type: application/json
```

```json
{ "phq_type": "phq2" }
```

**Response:**
```json
{
  "rejection_count": 1,
  "paused":          false,
  "paused_until":    null,
  "show_after":      "2026-04-19T10:00:00+00:00"
}
```

### Backend tự xử lý:
- **Lần 1–2**: Snooze pending → `GET /phq/pending` sẽ ẩn item này cho đến `show_after` (17:00 VN hôm sau)
- **Lần 3**: Tạm dừng 30 ngày, pending bị `dismissed` → không hiện nữa
- Frontend **không cần** tự đếm hay tính giờ — chỉ cần gọi `POST /phq/reject` rồi đóng popup

### Lưu ý quan trọng:
- Nếu **không gọi** `/phq/reject` mà chỉ tắt popup, lần sau mở app `GET /phq/pending` sẽ trả về pending lại ngay
- Khi user hoàn thành PHQ (`POST /phq/submit`), rejection_count tự động reset về 0

---

## Tóm tắt code cần sửa ở Frontend

### 1. Thêm check pending khi app resume

```dart
// Flutter / React Native pseudo-code

void onAppResume() async {
  final response = await api.get('/phq/pending');
  
  if (response['has_phq9']) {
    navigateTo(PHQ9Screen(pending: response['pending']));
  } else if (response['has_phq2']) {
    navigateTo(PHQ2Screen(pending: response['pending']));
  }
  // else: không làm gì, vào Home
}
```

### 2. Đăng ký lifecycle listener

```dart
// Flutter
class _AppState extends State<App> with WidgetsBindingObserver {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _checkPending();  // check lần đầu mở app
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _checkPending();  // check mỗi khi quay lại app
    }
  }

  Future<void> _checkPending() async {
    final res = await api.get('/phq/pending');
    if (res['has_phq9']) {
      Navigator.push(context, PHQ9Screen(pending: res['pending']));
    } else if (res['has_phq2']) {
      Navigator.push(context, PHQ2Screen(pending: res['pending']));
    }
  }
}
```

### 3. Xử lý nút "Để sau"

```dart
Future<void> onDismiss(String phqType) async {
  // GỌI API để backend snooze — KHÔNG chỉ tắt popup
  await api.post('/phq/reject', body: {'phq_type': phqType});
  Navigator.pop(context);
}
```

### 4. Sau khi user submit PHQ

```dart
Future<void> submitPHQ2(List<int> scores) async {
  final total = scores.reduce((a, b) => a + b);
  final res = await api.post('/phq/submit', body: {
    'phq_type': 'phq2',
    'scores':   scores,
    'total':    total,
    'source':   pending.reason,  // "scout", "scheduled", ...
  });

  if (res['decision'] == 'escalate_phq9') {
    // Backend đã tạo pending PHQ-9, check lại
    _checkPending();
  } else {
    Navigator.pop(context);  // quay về Home
  }
}
```

---

## Test thử

1. Chạy seed script để tạo pending:
   ```bash
   python scripts/fake_schedule_phq2.py <UID>
   ```

2. Kích hoạt Scout:
   ```
   POST /scout/run?uid=<UID>
   ```
   Scout sẽ tạo pending PHQ-2 (action = `schedule_phq2`)

3. Kiểm tra pending:
   ```
   GET /phq/pending
   ```
   Phải trả về `has_phq2: true`

4. Mở app — frontend nên tự hiển thị PHQ-2
