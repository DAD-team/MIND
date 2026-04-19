using UnityEngine;
using UnityEngine.UI;

namespace MIND.UI
{
    /// <summary>
    /// Displays the remaining session time in the VR HUD.
    /// Set remaining time via UpdateTimer() from the Session module.
    /// </summary>
    public class SessionTimerDisplay : MonoBehaviour
    {
        [SerializeField] private Text timerText;

        private float _remainingSeconds;
        private bool _active;

        public void UpdateTimer(float remainingSeconds)
        {
            _remainingSeconds = remainingSeconds;
            _active = remainingSeconds > 0;
        }

        public void Hide()
        {
            _active = false;
            if (timerText != null)
                timerText.text = "";
        }

        private void Update()
        {
            if (timerText == null || !_active) return;

            int minutes = Mathf.FloorToInt(_remainingSeconds / 60f);
            int seconds = Mathf.FloorToInt(_remainingSeconds % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }
}
