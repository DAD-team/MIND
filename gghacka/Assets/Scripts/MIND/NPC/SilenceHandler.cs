using System;
using MIND.Core;
using UnityEngine;

namespace MIND.NPC
{
    /// <summary>
    /// Detects prolonged silence during conversation.
    /// Instead of hardcoded responses, emits events with silence duration
    /// so VRSessionManager can ask LLM to generate a natural response.
    /// </summary>
    public class SilenceHandler : MonoBehaviour
    {
        [Header("Silence Thresholds (seconds)")]
        [SerializeField] private float headNodTime = 5f;
        [SerializeField] private float firstPromptTime = 10f;
        [SerializeField] private float secondPromptTime = 20f;
        [SerializeField] private float thirdPromptTime = 35f;

        private float _silenceTimer;
        private bool _isTracking;
        private SilenceLevel _currentLevel = SilenceLevel.None;

        /// <summary>
        /// Fired with (level, silenceSeconds). No hardcoded text — the caller
        /// decides how to respond (typically by asking LLM).
        /// </summary>
        public event Action<SilenceLevel, float> OnSilenceAction;

        public enum SilenceLevel
        {
            None,
            HeadNod,
            FirstPrompt,
            SecondPrompt,
            ThirdPrompt
        }

        private void OnEnable()
        {
            SessionEvents.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            SessionEvents.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(ConversationState state)
        {
            if (state == ConversationState.Listening)
            {
                _silenceTimer = 0f;
                _isTracking = true;
                _currentLevel = SilenceLevel.None;
            }
            else
            {
                _isTracking = false;
            }
        }

        private void Update()
        {
            if (!_isTracking) return;

            _silenceTimer += Time.deltaTime;

            if (_silenceTimer >= thirdPromptTime && _currentLevel < SilenceLevel.ThirdPrompt)
            {
                _currentLevel = SilenceLevel.ThirdPrompt;
                OnSilenceAction?.Invoke(_currentLevel, _silenceTimer);
            }
            else if (_silenceTimer >= secondPromptTime && _currentLevel < SilenceLevel.SecondPrompt)
            {
                _currentLevel = SilenceLevel.SecondPrompt;
                OnSilenceAction?.Invoke(_currentLevel, _silenceTimer);
            }
            else if (_silenceTimer >= firstPromptTime && _currentLevel < SilenceLevel.FirstPrompt)
            {
                _currentLevel = SilenceLevel.FirstPrompt;
                OnSilenceAction?.Invoke(_currentLevel, _silenceTimer);
            }
            else if (_silenceTimer >= headNodTime && _currentLevel < SilenceLevel.HeadNod)
            {
                _currentLevel = SilenceLevel.HeadNod;
                OnSilenceAction?.Invoke(_currentLevel, _silenceTimer);
            }
        }
    }
}
