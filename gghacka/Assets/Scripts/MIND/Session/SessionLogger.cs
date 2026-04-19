using MIND.Core;
using UnityEngine;

namespace MIND.Session
{
    /// <summary>
    /// Logs session metadata locally (and optionally to Firestore).
    /// Does NOT log conversation content (privacy-first).
    /// </summary>
    public class SessionLogger : MonoBehaviour
    {
        private void OnEnable()
        {
            SessionEvents.OnSessionEnded += LogSession;
        }

        private void OnDisable()
        {
            SessionEvents.OnSessionEnded -= LogSession;
        }

        private void LogSession(SessionLog log)
        {
            string json = JsonUtility.ToJson(log, true);
            Debug.Log($"[SessionLogger] Session log:\n{json}");

            // TODO: Save to local file or send to Firestore
        }
    }
}
