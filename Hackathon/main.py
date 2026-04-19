import re
import threading
import subprocess

import firebase_admin
import uvicorn
from firebase_admin import credentials
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from apscheduler.schedulers.background import BackgroundScheduler

from config import FIREBASE_CREDENTIALS_PATH
from routers import auth, videos, history, schedules, notifications, phq, mood, safety, monitoring, usage, chat, journals, scout

FIREBASE_DB_URL = "https://hackathon-493013-default-rtdb.asia-southeast1.firebasedatabase.app/"

if not firebase_admin._apps:
    firebase_admin.initialize_app(
        credentials.Certificate(FIREBASE_CREDENTIALS_PATH),
        {"databaseURL": FIREBASE_DB_URL},
    )

app = FastAPI(title="MIND Backend API", version="2.0.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(auth.router)
app.include_router(videos.router)
app.include_router(history.router)
app.include_router(schedules.router)
app.include_router(notifications.router)
app.include_router(phq.router)
app.include_router(mood.router)
app.include_router(safety.router)
app.include_router(monitoring.router)
app.include_router(usage.router)
app.include_router(chat.router)
app.include_router(journals.router)
app.include_router(scout.router)


@app.get("/health")
def health():
    return {"status": "ok"}


def _start_notification_scheduler():
    from services.scout import run_scout_cycle
    from services.data_retention import run_data_retention
    scheduler = BackgroundScheduler()
    scheduler.add_job(
        run_scout_cycle,
        trigger="interval",
        hours=2,
        id="scout_cycle_job",
    )
    scheduler.add_job(
        run_data_retention,
        trigger="interval",
        hours=24,
        id="data_retention_job",
    )
    scheduler.start()
    print("[Scheduler] Scout cycle job started (every 2 hours)")
    print("[Scheduler] Data retention job started (every 24 hours)")


def _start_cloudflared():
    process = subprocess.Popen(
        ["cloudflared", "tunnel", "--url", "http://localhost:8000"],
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        bufsize=1,
    )

    url_pattern = re.compile(r"https://[a-z0-9\-]+\.trycloudflare\.com")
    url_saved = False

    for line in process.stdout:
        if not url_saved:
            match = url_pattern.search(line)
            if match:
                tunnel_url = match.group(0)
                try:
                    import requests
                    res = requests.put(
                        "https://hackathon-493013-default-rtdb.asia-southeast1.firebasedatabase.app/server_info/tunnel_url.json",
                        json=tunnel_url,
                        timeout=10,
                    )
                    res.raise_for_status()
                    print(f"[firebase] tunnel_url updated → {tunnel_url}")
                except Exception as e:
                    print(f"[firebase] Failed to update tunnel_url: {e}")
                url_saved = True

    process.wait()


if __name__ == "__main__":
    tunnel_thread = threading.Thread(target=_start_cloudflared, daemon=True)
    tunnel_thread.start()

    _start_notification_scheduler()

    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=False)