import firebase_admin
from firebase_admin import firestore


def get_db() -> firestore.Client:
    # Dùng app đã được initialize trong main.py, không initialize lại
    return firestore.client()