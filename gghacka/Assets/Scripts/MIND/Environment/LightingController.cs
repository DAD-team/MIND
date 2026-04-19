using UnityEngine;

namespace MIND.Environment
{
    /// <summary>
    /// Controls environment lighting to sync with breathing exercises and mood.
    /// Use SetBreathPhase() from the Exercises module to sync.
    /// </summary>
    public class LightingController : MonoBehaviour
    {
        [SerializeField] private Light mainLight;
        [SerializeField] private float brightIntensity = 1.2f;
        [SerializeField] private float dimIntensity = 0.4f;
        [SerializeField] private float normalIntensity = 0.8f;

        private float _targetIntensity;
        private float _transitionSpeed = 0.5f;

        private void Start()
        {
            _targetIntensity = normalIntensity;
        }

        /// <summary>
        /// Called by Exercises module to sync lighting with breathing phases.
        /// phase: 0=Inhale, 1=Hold, 2=Exhale
        /// </summary>
        public void SetBreathPhase(int phase, float duration)
        {
            _transitionSpeed = 1f / duration;

            _targetIntensity = phase switch
            {
                0 => brightIntensity,   // Inhale - brighten
                1 => brightIntensity,   // Hold - keep bright
                2 => dimIntensity,      // Exhale - dim
                _ => normalIntensity
            };
        }

        public void ResetToNormal()
        {
            _targetIntensity = normalIntensity;
            _transitionSpeed = 0.3f;
        }

        private void Update()
        {
            if (mainLight == null) return;

            mainLight.intensity = Mathf.MoveTowards(
                mainLight.intensity,
                _targetIntensity,
                _transitionSpeed * Time.deltaTime
            );
        }

        public void SetMoodLighting(float intensity)
        {
            _targetIntensity = intensity;
            _transitionSpeed = 0.3f;
        }
    }
}
