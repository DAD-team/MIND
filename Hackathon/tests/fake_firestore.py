"""
FakeFirestore — in-memory giả lập Firestore cho flow test.

Support các pattern mà backend thật dùng:
  db.collection("users").document(uid).set(data, merge=True)
  db.collection("users").document(uid).collection("phq_results").document(id).set(doc)
  db.collection("users").document(uid).collection("interactions").add({...})
  col.where("status", "==", "pending").stream()
  col.order_by("created_at", direction="DESCENDING").limit(20).stream()
  doc.reference.update({...})

Không hỗ trợ composite index / transaction / subscription — chỉ những phép
mà code hiện tại đang gọi.
"""

import uuid
from copy import deepcopy
from typing import Any


class FakeFirestore:
    """Top-level db. Lưu mọi doc theo path/id trong 1 dict."""

    def __init__(self):
        # store: { "users/uid_p5/phq_results": { "<id>": {...doc...} }, ... }
        self._store: dict[str, dict[str, dict]] = {}

    def collection(self, name: str) -> "FakeCollection":
        return FakeCollection(name, self._store)

    # ---- helpers cho test: đọc state trực tiếp ----
    def read_collection(self, path: str) -> list[dict]:
        return [deepcopy(v) for v in self._store.get(path, {}).values()]

    def read_doc(self, path: str, doc_id: str) -> dict | None:
        bucket = self._store.get(path, {})
        return deepcopy(bucket.get(doc_id)) if doc_id in bucket else None


class FakeCollection:
    def __init__(self, path: str, store: dict, filters=None, order=None, limit=None):
        self._path = path
        self._store = store
        self._filters = list(filters or [])
        self._order = order
        self._limit = limit

    def document(self, doc_id: str | None = None) -> "FakeDocRef":
        if doc_id is None:
            doc_id = str(uuid.uuid4())
        return FakeDocRef(self._path, doc_id, self._store)

    def where(self, field: str, op: str, value: Any) -> "FakeCollection":
        return FakeCollection(self._path, self._store,
                              filters=self._filters + [(field, op, value)],
                              order=self._order, limit=self._limit)

    def order_by(self, field: str, direction: str = "ASCENDING") -> "FakeCollection":
        return FakeCollection(self._path, self._store, filters=self._filters,
                              order=(field, direction), limit=self._limit)

    def limit(self, n: int) -> "FakeCollection":
        return FakeCollection(self._path, self._store, filters=self._filters,
                              order=self._order, limit=n)

    def stream(self):
        bucket = self._store.get(self._path, {})
        items = [(doc_id, data) for doc_id, data in bucket.items()]

        for field, op, value in self._filters:
            def _match(item):
                _, data = item
                dv = data.get(field)
                if op == "==":  return dv == value
                if op == ">=":  return dv is not None and dv >= value
                if op == ">":   return dv is not None and dv > value
                if op == "<=":  return dv is not None and dv <= value
                if op == "<":   return dv is not None and dv < value
                return False
            items = [i for i in items if _match(i)]

        if self._order:
            field, direction = self._order
            reverse = direction.upper() in ("DESCENDING", "DESC")
            items.sort(key=lambda it: it[1].get(field, ""), reverse=reverse)

        if self._limit is not None:
            items = items[: self._limit]

        for doc_id, _ in items:
            yield FakeDocSnapshot(self._path, doc_id, self._store)

    def add(self, data: dict):
        doc_id = str(uuid.uuid4())
        ref = FakeDocRef(self._path, doc_id, self._store)
        ref.set(data)
        return (None, ref)


class FakeDocRef:
    """Reference tới 1 doc cụ thể — có thể set/update/get/collection subcol."""

    def __init__(self, path: str, doc_id: str, store: dict):
        self._path = path
        self.id = doc_id
        self._store = store

    def _bucket(self) -> dict:
        return self._store.setdefault(self._path, {})

    def set(self, data: dict, merge: bool = False):
        bucket = self._bucket()
        if merge and self.id in bucket:
            bucket[self.id].update(deepcopy(data))
        else:
            bucket[self.id] = deepcopy(data)

    def update(self, data: dict):
        bucket = self._bucket()
        bucket.setdefault(self.id, {}).update(deepcopy(data))

    def get(self) -> "FakeDocSnapshot":
        return FakeDocSnapshot(self._path, self.id, self._store)

    def collection(self, sub_name: str) -> FakeCollection:
        sub_path = f"{self._path}/{self.id}/{sub_name}"
        return FakeCollection(sub_path, self._store)


class FakeDocSnapshot:
    """Kết quả của .get() hoặc 1 phần tử trong .stream()."""

    def __init__(self, path: str, doc_id: str, store: dict):
        self._path = path
        self.id = doc_id
        self._store = store

    @property
    def exists(self) -> bool:
        return self.id in self._store.get(self._path, {})

    def to_dict(self) -> dict | None:
        bucket = self._store.get(self._path, {})
        return deepcopy(bucket[self.id]) if self.id in bucket else None

    @property
    def reference(self) -> FakeDocRef:
        return FakeDocRef(self._path, self.id, self._store)
