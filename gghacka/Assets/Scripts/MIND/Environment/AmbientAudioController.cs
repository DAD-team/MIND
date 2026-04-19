using MIND.Core;
using UnityEngine;

namespace MIND.Environment
{
    /// <summary>
    /// Controls ambient audio based on selected therapy environment.
    /// </summary>
    public class AmbientAudioController : MonoBehaviour
    {
        [Header("Ambient Clips")]
        [SerializeField] private AudioClip gardenAmbient;
        [SerializeField] private AudioClip beachAmbient;
        [SerializeField] private AudioClip meditationAmbient;

        [Header("Settings")]
        [SerializeField] private float volume = 0.3f;
        [SerializeField] private float fadeDuration = 2f;

        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.volume = 0f;
            _audioSource.spatialBlend = 0f;
        }

        public void PlayForEnvironment(TherapyEnvironment environment)
        {
            AudioClip clip = environment switch
            {
                TherapyEnvironment.Garden => gardenAmbient,
                TherapyEnvironment.Beach => beachAmbient,
                TherapyEnvironment.MeditationRoom => meditationAmbient,
                _ => null
            };

            if (clip == null)
            {
                Debug.LogWarning($"[AmbientAudio] No clip for {environment}");
                return;
            }

            _audioSource.clip = clip;
            _audioSource.Play();
            StartCoroutine(FadeVolume(0f, volume, fadeDuration));
        }

        public void StopAmbient()
        {
            StartCoroutine(FadeVolume(_audioSource.volume, 0f, fadeDuration));
        }

        private System.Collections.IEnumerator FadeVolume(float from, float to, float duration)
        {
            float elapsed = 0f;
            _audioSource.volume = from;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _audioSource.volume = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            _audioSource.volume = to;
            if (to <= 0f)
                _audioSource.Stop();
        }
    }
}
