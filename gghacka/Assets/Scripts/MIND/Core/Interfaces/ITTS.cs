namespace MIND.Core
{
    public interface ITTS
    {
        bool IsSpeaking { get; }
        bool IsReady { get; }
        void Speak(string text);
        void Stop();
    }
}
