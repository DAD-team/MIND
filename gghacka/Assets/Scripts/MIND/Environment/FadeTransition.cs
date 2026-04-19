using System.Collections;
using UnityEngine;

namespace MIND.Environment
{
    /// <summary>
    /// Smooth fade in/out transition for VR scene changes.
    /// </summary>
    public class FadeTransition : MonoBehaviour
    {
        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField] private float fadeDuration = 1.5f;

        public void FadeIn()
        {
            if (fadeCanvasGroup == null) return;
            StopAllCoroutines();
            StartCoroutine(DoFade(1f, 0f));
        }

        public void FadeOut()
        {
            if (fadeCanvasGroup == null) return;
            StopAllCoroutines();
            StartCoroutine(DoFade(0f, 1f));
        }

        private IEnumerator DoFade(float from, float to)
        {
            fadeCanvasGroup.alpha = from;
            fadeCanvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
                yield return null;
            }

            fadeCanvasGroup.alpha = to;
            fadeCanvasGroup.blocksRaycasts = to > 0.5f;
        }
    }
}
