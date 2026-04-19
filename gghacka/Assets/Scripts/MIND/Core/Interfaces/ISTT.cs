using System;

namespace MIND.Core
{
    public interface ISTT
    {
        /// <summary>Fired when a complete utterance is transcribed (after silence detected).</summary>
        event Action<string> OnTranscriptionResult;

        /// <summary>Fired when VAD detects voice activity starts (user begins speaking).</summary>
        event Action OnVoiceDetected;

        bool IsStreaming { get; }
        void StartStreaming();
        void StopStreaming();
    }
}
