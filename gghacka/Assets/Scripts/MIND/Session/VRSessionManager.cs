using System;
using System.Collections;
using MIND.Core;
using MIND.AI;
using MIND.NPC;
using MIND.Environment;
using UnityEngine;

namespace MIND.Session
{
    /// <summary>
    /// Manages the full VR therapy session lifecycle.
    ///
    /// Everything the NPC says goes through LLM — no hardcoded scripts.
    /// STT stays on the entire session — user can always speak.
    ///
    /// Flow:
    ///   1. Fade in → NPC intro animation
    ///   2. Start conversation → LLM generates greeting
    ///   3. User and NPC talk freely (user can interrupt anytime)
    ///   4. Silence → LLM generates natural response
    ///   5. End trigger → LLM generates farewell → NPC outro
    /// </summary>
    public class VRSessionManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MindConversation conversation;
        [SerializeField] private NPCController npcController;
        [SerializeField] private SilenceHandler silenceHandler;
        [SerializeField] private SessionTimer sessionTimer;
        [SerializeField] private FadeTransition fadeTransition;

        [Header("Session Config")]
        [SerializeField] private float maxDurationMinutes = 20f;

        [Header("End Session Keywords")]
        [SerializeField] private string[] endKeywords = {
            "tạm biệt", "tam biet", "dừng", "dung",
            "kết thúc", "ket thuc", "muốn dừng", "muon dung",
            "bye", "goodbye"
        };

        private SessionConfig _config;
        private SessionLog _log;
        private bool _sessionActive;
        private bool _isEndingSession;

        public bool IsSessionActive => _sessionActive;

        public void StartSession(SessionConfig config)
        {
            if (_sessionActive) return;

            _config = config ?? new SessionConfig();
            _sessionActive = true;
            _isEndingSession = false;

            _log = new SessionLog
            {
                sessionId = Guid.NewGuid().ToString(),
                timestamp = DateTime.UtcNow.ToString("o"),
                preSessionPhq9 = _config.emotionProfile?.phq9Score ?? 0
            };

            SessionEvents.RaiseSessionStarted(_config);
            StartCoroutine(SessionStartSequence());
        }

        private IEnumerator SessionStartSequence()
        {
            // 1. Start timer
            if (sessionTimer != null)
            {
                sessionTimer.StartTimer(_config.maxDurationMinutes > 0
                    ? _config.maxDurationMinutes
                    : maxDurationMinutes);
                sessionTimer.OnTimerExpired += HandleTimerExpired;
            }

            // 2. Fade in
            if (fadeTransition != null)
            {
                fadeTransition.FadeIn();
                yield return new WaitForSeconds(1f);
            }

            // 3. NPC intro animation (walk to chair, sit down)
            if (npcController != null)
            {
                bool introComplete = false;
                void OnIntro() { introComplete = true; }
                npcController.OnIntroComplete += OnIntro;
                npcController.PerformGreeting();

                while (!introComplete)
                    yield return null;

                npcController.OnIntroComplete -= OnIntro;
            }

            // 4. Start conversation (STT begins, always on)
            if (conversation != null)
                conversation.StartConversation(_config.emotionProfile);

            // 5. Ask LLM to greet the user (no hardcoded greeting)
            if (conversation != null)
                conversation.RequestGreeting();

            // 6. Subscribe to events
            SessionEvents.OnUserSaid += HandleUserSaid;
            if (silenceHandler != null)
                silenceHandler.OnSilenceAction += HandleSilenceAction;

            Debug.Log($"[VRSessionManager] Session started: {_log.sessionId}");
        }

        // ================================================================
        // During Session
        // ================================================================

        private void HandleUserSaid(string text)
        {
            if (!_sessionActive || _isEndingSession) return;

            _log.exchangeCount++;

            string lower = text.ToLowerInvariant();
            foreach (var keyword in endKeywords)
            {
                if (lower.Contains(keyword))
                {
                    Debug.Log($"[VRSessionManager] End keyword: '{keyword}'");
                    EndSession(SessionEndTrigger.UserInitiated);
                    return;
                }
            }
        }

        private void HandleSilenceAction(SilenceHandler.SilenceLevel level, float silenceSeconds)
        {
            if (!_sessionActive || _isEndingSession) return;

            if (level == SilenceHandler.SilenceLevel.HeadNod)
            {
                // Just animation, no speech
                return;
            }

            // Ask LLM to generate a response to the silence
            if (conversation != null)
            {
                Debug.Log($"[VRSessionManager] Silence {level} ({silenceSeconds:F0}s) → asking LLM");
                conversation.RequestSilenceResponse(silenceSeconds);
            }
        }

        // ================================================================
        // End Session
        // ================================================================

        public void EndSession(SessionEndTrigger trigger)
        {
            if (!_sessionActive || _isEndingSession) return;
            _isEndingSession = true;

            StartCoroutine(SessionEndSequence(trigger));
        }

        private IEnumerator SessionEndSequence(SessionEndTrigger trigger)
        {
            // Unsubscribe
            SessionEvents.OnUserSaid -= HandleUserSaid;
            if (silenceHandler != null)
                silenceHandler.OnSilenceAction -= HandleSilenceAction;

            // 1. Ask LLM to say farewell (wait for it to finish speaking)
            if (conversation != null)
            {
                bool farewellDone = false;
                conversation.RequestFarewell(onComplete: () => farewellDone = true);

                float timeout = 30f;
                float elapsed = 0f;
                while (!farewellDone && elapsed < timeout)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                }
            }

            // 2. Stop conversation
            if (conversation != null)
                conversation.StopConversation();

            // 3. NPC outro animation
            if (npcController != null)
            {
                bool outroComplete = false;
                void OnOutro() { outroComplete = true; }
                npcController.OnOutroComplete += OnOutro;
                npcController.PerformFarewell();

                while (!outroComplete)
                    yield return null;

                npcController.OnOutroComplete -= OnOutro;
            }

            // 4. Log
            _log.endTrigger = trigger;
            if (sessionTimer != null)
            {
                _log.durationMinutes = sessionTimer.ElapsedMinutes;
                sessionTimer.OnTimerExpired -= HandleTimerExpired;
                sessionTimer.StopTimer();
            }

            _sessionActive = false;
            SessionEvents.RaiseSessionEnded(_log);

            // 5. Fade out
            if (fadeTransition != null)
                fadeTransition.FadeOut();

            Debug.Log($"[VRSessionManager] Session ended: {trigger}, {_log.durationMinutes:F1}min, {_log.exchangeCount} exchanges");
        }

        private void HandleTimerExpired()
        {
            EndSession(SessionEndTrigger.TimerExpired);
        }

        private void OnDestroy()
        {
            SessionEvents.OnUserSaid -= HandleUserSaid;
            if (silenceHandler != null)
                silenceHandler.OnSilenceAction -= HandleSilenceAction;

            if (_sessionActive)
                EndSession(SessionEndTrigger.Error);
        }
    }
}
