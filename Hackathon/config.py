from dotenv import load_dotenv
import os

load_dotenv()

FIREBASE_CREDENTIALS_PATH = os.environ["FIREBASE_CREDENTIALS_PATH"]
MODEL_PATH                = os.getenv("MODEL_PATH")