# Backend Unit Tests

## Install

```bash
cd Hackathon
pip install -r requirements-dev.txt
```

## Run

```bash
pytest                       # tất cả
pytest tests/test_phq_scoring.py    # chỉ PHQ scoring
pytest -m unit              # chỉ marker unit
pytest --cov=controllers --cov=services  # kèm coverage
```

## Cấu trúc

| File | Phạm vi |
|------|---------|
| `conftest.py` | Stub firebase_admin/fastapi/pydantic để không cần cài deps nặng. Fixtures `mock_db`, `patch_get_db`, helper `make_doc`. |
| `test_phq_scoring.py` | Pure functions `_severity`, `_monitoring_level` từ `controllers/phq_controller.py`. |
| `test_scout_scoring.py` | Pure scoring helpers `_score_*`, `compute_risk_score` từ `services/scout.py`. |

## Mapping với TEST_CASES.md

Mỗi test class/method có docstring chỉ rõ test case ID tương ứng (SC-*, P9-*, P2-*).

## Thêm test mới

Pure function → thêm vào file tương ứng, dùng `@pytest.mark.parametrize` cho nhiều case.

Function gọi Firestore → dùng fixture `patch_get_db`:

```python
def test_reject_phq_increments_count(patch_get_db):
    # patch_get_db đã mock services.firebase.get_db
    # Setup mock_db.collection(...).document(...).get() trả về doc bạn muốn
    mock_db = patch_get_db
    mock_db.collection.return_value.document.return_value.collection.return_value \
        .document.return_value.get.return_value = make_doc({"rejection_count": 0})
    # ...
```
