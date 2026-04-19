using System;
using UnityEngine;

namespace MIND.Session
{
    /// <summary>
    /// Tracks session duration with a configurable max time.
    /// </summary>
    public class SessionTimer : MonoBehaviour
    {
        private float _maxSeconds;
        private float _elapsed;
        private bool _running;

        public float ElapsedMinutes => _elapsed / 60f;
        public float RemainingSeconds => Mathf.Max(0, _maxSeconds - _elapsed);
        public bool IsRunning => _running;

        public event Action OnTimerExpired;

        public void StartTimer(float maxMinutes)
        {
            _maxSeconds = maxMinutes * 60f;
            _elapsed = 0f;
            _running = true;
        }

        public void StopTimer()
        {
            _running = false;
        }

        private void Update()
        {
            if (!_running) return;

            _elapsed += Time.deltaTime;

            if (_elapsed >= _maxSeconds)
            {
                _running = false;
                OnTimerExpired?.Invoke();
            }
        }
    }
}
