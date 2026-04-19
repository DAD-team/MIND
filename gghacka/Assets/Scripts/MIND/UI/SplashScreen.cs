using System;
using System.Collections;
using UnityEngine;

namespace MIND.UI
{
    /// <summary>
    /// Splash screen with loading message: "Khong gian nay thuoc ve ban".
    /// </summary>
    public class SplashScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float displayDuration = 3f;
        [SerializeField] private float fadeDuration = 1f;

        public event Action OnSplashComplete;

        public void Show()
        {
            if (canvasGroup == null) return;
            gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            StartCoroutine(SplashSequence());
        }

        private IEnumerator SplashSequence()
        {
            yield return new WaitForSeconds(displayDuration);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = 1f - (elapsed / fadeDuration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            OnSplashComplete?.Invoke();
        }
    }
}
