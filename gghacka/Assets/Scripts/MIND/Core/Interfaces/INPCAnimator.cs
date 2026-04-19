namespace MIND.Core
{
    public interface INPCAnimator
    {
        void SetTalking(bool talking);
        void SetListening(bool listening);
        void SetWalking(bool walking);
        void SetSitting(bool sitting);
        void TriggerWave();
        void TriggerSitDown();
        void TriggerStandUp();
        void TriggerHeadTilt();
    }
}
