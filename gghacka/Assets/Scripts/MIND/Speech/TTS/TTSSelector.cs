using MIND.Core;
using UnityEngine;

namespace MIND.Speech
{
    /// <summary>
    /// Auto-selects the best available TTS implementation.
    /// Prefers local (SherpaOnnx) if ready, falls back to server (VieNeu).
    /// </summary>
    public class TTSSelector : MonoBehaviour
    {
        [SerializeField] private SherpaOnnxTTS localTts;
        [SerializeField] private VieNeuTTS serverTts;

        public ITTS ActiveTTS { get; private set; }

        public void Select()
        {
            if (localTts != null && localTts.IsReady)
            {
                ActiveTTS = localTts;
                Debug.Log("[TTSSelector] Using local TTS (SherpaOnnx)");
            }
            else if (serverTts != null && serverTts.IsReady)
            {
                ActiveTTS = serverTts;
                Debug.Log("[TTSSelector] Using server TTS (VieNeu)");
            }
            else
            {
                ActiveTTS = localTts;
                Debug.LogWarning("[TTSSelector] No TTS ready, defaulting to local");
            }
        }
    }
}
