"""
Pytest fixtures cho test suite MIND.

Stub các external deps (firebase_admin, fastapi) ở module level để unit test
pure functions không cần cài firebase-admin hay fastapi. Stubs chạy TRƯỚC khi
test module nào được import → các chain import `services.firebase`,
`controllers.phq_controller` hoạt động bình thường.
"""

import os
import sys
import types
from unittest.mock import MagicMock

import pytest

# ---------------------------------------------------------------------------
#  sys.path: đảm bảo Hackathon/ root nằm trong path để `from controllers...`,
#  `from services...`, `from models...` resolve được.
# ---------------------------------------------------------------------------
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)

# config.py đọc env var này lúc import — set giá trị fake trước khi bất kỳ test
# module nào gián tiếp import config (vd. services.analyzer).
os.environ.setdefault("FIREBASE_CREDENTIALS_PATH", "fake-credentials.json")


# ---------------------------------------------------------------------------
#  Stub firebase_admin & submodules — tránh yêu cầu cài firebase-admin khi
#  chỉ test pure logic. Các hàm chạm Firestore dùng fixture patch_get_db.
# ---------------------------------------------------------------------------
def _stub_module(name: str):
    """Tạo module ảo trong sys.modules nếu chưa có."""
    if name not in sys.modules:
        sys.modules[name] = types.ModuleType(name)
    return sys.modules[name]


if "firebase_admin" not in sys.modules:
    fb = _stub_module("firebase_admin")
    firestore_mod = _stub_module("firebase_admin.firestore")
    firestore_mod.client = MagicMock(return_value=MagicMock())
    firestore_mod.Client = MagicMock
    fb.firestore = firestore_mod
    fb.initialize_app = MagicMock()
    fb.auth = _stub_module("firebase_admin.auth")
    fb.credentials = _stub_module("firebase_admin.credentials")
    fb.credentials.Certificate = MagicMock()

    # messaging — stub cho services.notification
    messaging_mod = _stub_module("firebase_admin.messaging")

    class _UnregisteredError(Exception):
        pass

    messaging_mod.UnregisteredError = _UnregisteredError
    messaging_mod.Message = MagicMock()
    messaging_mod.Notification = MagicMock()
    messaging_mod.send = MagicMock(return_value="fake-msg-id")
    fb.messaging = messaging_mod

# fastapi có thể đã cài (dùng chạy server), nhưng stub phòng môi trường test
# tối thiểu. HTTPException cần là class để raise được.
if "fastapi" not in sys.modules:
    fastapi_mod = _stub_module("fastapi")

    class _HTTPException(Exception):
        def __init__(self, status_code=500, detail=""):
            self.status_code = status_code
            self.detail = detail
            super().__init__(detail)

    fastapi_mod.HTTPException = _HTTPException
    fastapi_mod.Depends = MagicMock()

    # APIRouter: decorator phải pass-through để test gọi trực tiếp function được.
    class _FakeAPIRouter:
        def __init__(self, *a, **kw):
            pass

        def _dec(self, *a, **kw):
            def _wrap(fn):
                return fn
            return _wrap

        get = post = put = delete = patch = _dec

    fastapi_mod.APIRouter = _FakeAPIRouter
    fastapi_mod.Request = MagicMock()
    fastapi_mod.UploadFile = MagicMock
    fastapi_mod.File = lambda *a, **kw: None

    # fastapi.security dùng trong routers/auth.py
    security_mod = _stub_module("fastapi.security")
    security_mod.HTTPBearer = MagicMock
    security_mod.HTTPAuthorizationCredentials = MagicMock
    fastapi_mod.security = security_mod

# pydantic cần cho models.phq; stub BaseModel nếu không cài
if "pydantic" not in sys.modules:
    pydantic_mod = _stub_module("pydantic")

    class _BaseModel:
        def __init__(self, **kwargs):
            for k, v in kwargs.items():
                setattr(self, k, v)

        def dict(self):
            return {k: v for k, v in self.__dict__.items() if not k.startswith("_")}

        model_dump = dict

    pydantic_mod.BaseModel = _BaseModel
    pydantic_mod.Field = lambda *a, **kw: None


# ---------------------------------------------------------------------------
#  Shared fixtures
# ---------------------------------------------------------------------------

@pytest.fixture
def mock_db():
    """MagicMock của Firestore client; test tự cấu hình .get()/.stream()."""
    return MagicMock()


@pytest.fixture
def patch_get_db(monkeypatch, mock_db):
    """Patch services.firebase.get_db + các point-of-use import trong controllers."""
    monkeypatch.setattr("services.firebase.get_db", lambda: mock_db)
    try:
        monkeypatch.setattr("controllers.phq_controller.get_db", lambda: mock_db)
    except AttributeError:
        pass
    try:
        monkeypatch.setattr("services.scout.get_db", lambda: mock_db)
    except AttributeError:
        pass
    return mock_db


def make_doc(data: dict, exists: bool = True):
    """Helper: fake Firestore DocumentSnapshot."""
    doc = MagicMock()
    doc.exists = exists
    doc.to_dict.return_value = data if exists else None
    doc.id = data.get("id", "fake_id") if exists else "fake_id"
    return doc


def make_pending_doc(phq_type: str, doc_id: str = "pending_1", status: str = "pending"):
    """
    Helper: fake Firestore pending_phq DocumentSnapshot.
    Có `.reference.update(...)` để assert backend gọi update đúng payload.
    """
    data = {"id": doc_id, "phq_type": phq_type, "status": status}
    doc = make_doc(data)
    doc.reference = MagicMock()  # để test assert update được
    return doc


def setup_user_doc(mock_db, uid: str, *,
                   monitoring: dict | None = None,
                   pending: list | None = None):
    """
    Setup chain mock: db.collection("users").document(uid) với 2 subcollection:
      - monitoring.document("current") → trả về `monitoring` dict
      - pending_phq.where(...).stream() → trả về list `pending` docs

    Returns:
        dict gồm các mock quan trọng để assert:
          {"mon_ref", "mon_snap", "pending_docs", "user_ref"}
    """
    # monitoring subcollection
    mon_snap = make_doc(monitoring) if monitoring is not None else make_doc({}, exists=False)
    mon_ref = MagicMock()
    mon_ref.get.return_value = mon_snap

    mon_collection = MagicMock()
    mon_collection.document.return_value = mon_ref

    # pending_phq subcollection
    pending_docs = pending or []
    pending_query = MagicMock()
    pending_query.stream.return_value = iter(pending_docs)

    pending_collection = MagicMock()
    pending_collection.where.return_value = pending_query

    # User ref routes collection() by name
    user_ref = MagicMock()

    def collection_router(name):
        if name == "monitoring":
            return mon_collection
        if name == "pending_phq":
            return pending_collection
        # Các sub-col khác (phq_results, scout_logs, ...) trả mock mặc định
        return MagicMock()

    user_ref.collection.side_effect = collection_router

    # Root: db.collection("users").document(uid)
    mock_db.collection.return_value.document.return_value = user_ref

    return {
        "mon_ref":      mon_ref,
        "mon_snap":     mon_snap,
        "pending_docs": pending_docs,
        "user_ref":     user_ref,
    }
