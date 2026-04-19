using UnityEngine;

namespace MIND.UI
{
    /// <summary>
    /// Loading overlay voi hinh anh lac lu dang yeu.
    /// Gan anh "Do An Tot Nghiep.png" vao loadingImage.
    ///
    /// Setup:
    ///   1. Tao child "LoadingOverlay" trong Auth Canvas
    ///   2. Image background (stretch full, mau den alpha ~150/255)
    ///   3. Child Image: keo anh loading vao, dat giua
    ///   4. Gan script nay, keo Image RectTransform vao field loadingImage
    ///   5. SetActive(false) mac dinh
    /// </summary>
    public class LoadingOverlay : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private RectTransform loadingImage;

        [Header("Wobble (lac lu)")]
        [SerializeField] private float wobbleAngle = 12f;
        [SerializeField] private float wobbleSpeed = 3f;

        [Header("Bounce (nhun)")]
        [SerializeField] private float bounceHeight = 8f;
        [SerializeField] private float bounceSpeed = 2f;

        [Header("Scale Pulse (phong to nhe)")]
        [SerializeField] private float scaleMin = 0.95f;
        [SerializeField] private float scaleMax = 1.05f;
        [SerializeField] private float scaleSpeed = 2.5f;

        private Vector3 _startPos;

        private void OnEnable()
        {
            if (loadingImage != null)
            {
                _startPos = loadingImage.localPosition;
                loadingImage.localRotation = Quaternion.identity;
                loadingImage.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            if (loadingImage == null) return;

            float t = Time.time;

            // Lac lu qua lai
            float angle = Mathf.Sin(t * wobbleSpeed) * wobbleAngle;
            loadingImage.localRotation = Quaternion.Euler(0, 0, angle);

            // Nhun len xuong
            float yOffset = Mathf.Sin(t * bounceSpeed) * bounceHeight;
            loadingImage.localPosition = _startPos + new Vector3(0, yOffset, 0);

            // Phong to nhe theo nhip
            float scale = Mathf.Lerp(scaleMin, scaleMax, (Mathf.Sin(t * scaleSpeed) + 1f) * 0.5f);
            loadingImage.localScale = new Vector3(scale, scale, 1f);
        }

        private void OnDisable()
        {
            if (loadingImage != null)
            {
                loadingImage.localPosition = _startPos;
                loadingImage.localRotation = Quaternion.identity;
                loadingImage.localScale = Vector3.one;
            }
        }
    }
}
