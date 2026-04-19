using MIND.Core;
using UnityEngine;

namespace MIND.NPC
{
    /// <summary>
    /// Điều khiển animation NPC qua Animator component.
    /// Code gọi CrossFade trực tiếp theo tên state — không cần setup transition hay parameter.
    ///
    /// Setup:
    ///   1. Tạo AnimatorController, kéo tất cả clip vào (Unity tự tạo state tên = tên clip)
    ///   2. Gắn AnimatorController vào Animator component của NPC
    ///   3. Điền tên state vào các field "State Name" bên dưới (mặc định đã đặt sẵn)
    ///   4. Điền thời lượng clip transition (SitDown, StandUp, Wave) để coroutine chờ đúng
    /// </summary>
    public class NPCAnimatorController : MonoBehaviour, INPCAnimator
    {
        [SerializeField] private Animator animator;

        [Header("Tên state trong AnimatorController (mặc định = tên clip)")]
        [SerializeField] private string stateStandingIdle     = "Idle_01_HUMANIK_TF1";
        [SerializeField] private string stateWalk             = "Walking (1)";
        [SerializeField] private string stateSitDown          = "Stand To Sit";
        [SerializeField] private string stateStandUp          = "Sit To Stand";
        [SerializeField] private string stateSittingIdle      = "Sitting";
        [SerializeField] private string stateSittingTalking   = "Sitting Talking";
        [SerializeField] private string stateSittingListening = "Idle_WatchingSomething_HUMANIK_769";
        [SerializeField] private string stateWave             = "Pilot_Wave_HUMANIK_769";
        [SerializeField] private string stateHeadTilt         = "HeadTilts_mixamo";

        [Header("Thời lượng clip transition (giây) — xem trong Inspector của clip .fbx)")]
        [SerializeField] private float sitDownLength  = 1.5f;
        [SerializeField] private float standUpLength  = 1.5f;
        [SerializeField] private float waveLength     = 2.5f;

        [Header("Blend")]
        [SerializeField] private float crossFadeTime = 0.15f;

        // NPCController đọc để biết chờ bao lâu sau transition
        public float SitDownLength => sitDownLength;
        public float StandUpLength => standUpLength;
        public float WaveLength    => waveLength;

        private bool _isSitting;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        private void CrossFade(string stateName)
        {
            if (animator == null) return;
            animator.CrossFade(stateName, crossFadeTime);
        }

        // ================================================================
        // INPCAnimator
        // ================================================================

        public void SetTalking(bool talking)
        {
            CrossFade(talking
                ? stateSittingTalking
                : (_isSitting ? stateSittingIdle : stateStandingIdle));
        }

        public void SetListening(bool listening)
        {
            CrossFade(listening
                ? stateSittingListening
                : (_isSitting ? stateSittingIdle : stateStandingIdle));
        }

        public void SetWalking(bool walking)
        {
            CrossFade(walking ? stateWalk : stateStandingIdle);
        }

        public void SetSitting(bool sitting)
        {
            _isSitting = sitting;
            CrossFade(sitting ? stateSittingIdle : stateStandingIdle);
        }

        public void TriggerWave()     => CrossFade(stateWave);
        public void TriggerSitDown()  => CrossFade(stateSitDown);
        public void TriggerStandUp()  => CrossFade(stateStandUp);
        public void TriggerHeadTilt() => CrossFade(stateHeadTilt);
    }
}
