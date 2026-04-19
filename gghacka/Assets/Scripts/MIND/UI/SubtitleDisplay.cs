using MIND.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MIND.UI
{
    /// <summary>
    /// Displays subtitles in VR for NPC speech and user speech.
    /// </summary>
    public class SubtitleDisplay : MonoBehaviour
    {
        [SerializeField] private Text subtitleText;
        [SerializeField] private float displayDuration = 5f;

        private float _hideTimer;

        private void OnEnable()
        {
            SessionEvents.OnUserSaid += ShowUserText;
            SessionEvents.OnAIResponded += ShowAIText;
        }

        private void OnDisable()
        {
            SessionEvents.OnUserSaid -= ShowUserText;
            SessionEvents.OnAIResponded -= ShowAIText;
        }

        private void ShowUserText(string text)
        {
            if (subtitleText == null) return;
            subtitleText.text = text;
            subtitleText.color = new Color(0.8f, 0.9f, 1f);
            _hideTimer = displayDuration;
        }

        private void ShowAIText(AIResponse response)
        {
            if (subtitleText == null || response == null) return;
            subtitleText.text = response.text;
            subtitleText.color = Color.white;
            _hideTimer = displayDuration;
        }

        private void Update()
        {
            if (_hideTimer > 0)
            {
                _hideTimer -= Time.deltaTime;
                if (_hideTimer <= 0 && subtitleText != null)
                    subtitleText.text = "";
            }
        }
    }
}
