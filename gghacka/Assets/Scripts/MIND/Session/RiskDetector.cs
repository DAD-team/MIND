using MIND.Core;
using UnityEngine;

namespace MIND.Session
{
    /// <summary>
    /// Detects risk keywords in user speech (self-harm, suicide).
    /// Triggers alert and shifts NPC to emotional stabilization mode.
    /// </summary>
    public class RiskDetector : MonoBehaviour
    {
        private static readonly string[] RiskKeywords =
        {
            "tu tu", "tu sat", "muon chet", "khong muon song",
            "tu hai", "cat tay", "ket thuc tat ca"
        };

        private void OnEnable()
        {
            SessionEvents.OnUserSaid += CheckForRisk;
        }

        private void OnDisable()
        {
            SessionEvents.OnUserSaid -= CheckForRisk;
        }

        private void CheckForRisk(string userText)
        {
            string lower = userText.ToLowerInvariant();

            foreach (var keyword in RiskKeywords)
            {
                if (lower.Contains(keyword))
                {
                    Debug.LogWarning($"[RiskDetector] Risk keyword detected: '{keyword}'");
                    SessionEvents.RaiseRiskDetected();
                    return;
                }
            }
        }
    }
}
