using System;
using MIND.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MIND.UI
{
    /// <summary>
    /// Panel cho user chon moi truong tri lieu truoc khi vao phien.
    /// 4 lua chon: Passthrough, Khu vuon, Bai bien, Phong thien.
    ///
    /// Setup:
    ///   1. Tao panel child trong Canvas (World Space)
    ///   2. Title text: "Chon khong gian tri lieu"
    ///   3. 4 Buttons voi text + description
    ///   4. Gan script nay, keo cac field
    ///   5. SetActive(false) mac dinh
    /// </summary>
    public class EnvironmentSelectPanel : MonoBehaviour
    {
        [Header("Title")]
        [SerializeField] private TMP_Text titleText;

        [Header("Environment Buttons")]
        [SerializeField] private Button passthroughBtn;
        [SerializeField] private Button gardenBtn;
        [SerializeField] private Button beachBtn;
        [SerializeField] private Button meditationBtn;

        [Header("Descriptions")]
        [SerializeField] private TMP_Text passthroughDesc;
        [SerializeField] private TMP_Text gardenDesc;
        [SerializeField] private TMP_Text beachDesc;
        [SerializeField] private TMP_Text meditationDesc;

        [Header("PHQ Recommendation")]
        [SerializeField] private TMP_Text recommendText;

        public event Action<TherapyEnvironment> OnEnvironmentSelected;

        private PhqSeverity _severity;

        public void Show(PhqSeverity severity)
        {
            _severity = severity;
            gameObject.SetActive(true);
            UpdateRecommendation();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (passthroughBtn != null) passthroughBtn.onClick.AddListener(SelectPassthrough);
            if (gardenBtn != null) gardenBtn.onClick.AddListener(SelectGarden);
            if (beachBtn != null) beachBtn.onClick.AddListener(SelectBeach);
            if (meditationBtn != null) meditationBtn.onClick.AddListener(SelectMeditation);
        }

        private void OnDisable()
        {
            if (passthroughBtn != null) passthroughBtn.onClick.RemoveListener(SelectPassthrough);
            if (gardenBtn != null) gardenBtn.onClick.RemoveListener(SelectGarden);
            if (beachBtn != null) beachBtn.onClick.RemoveListener(SelectBeach);
            if (meditationBtn != null) meditationBtn.onClick.RemoveListener(SelectMeditation);
        }

        private void UpdateRecommendation()
        {
            if (recommendText == null) return;

            recommendText.text = _severity switch
            {
                PhqSeverity.Severe => "Gợi ý: Phòng thiền tĩnh lặng phù hợp với bạn lúc này",
                PhqSeverity.Moderate => "Gợi ý: Khu vườn yên tĩnh sẽ giúp bạn thư giãn",
                PhqSeverity.Mild => "Gợi ý: Bãi biển hoàng hôn là lựa chọn tuyệt vời",
                _ => "Hãy chọn không gian bạn cảm thấy thoải mái nhất"
            };
        }

        private void SelectPassthrough()
        {
            Debug.Log("[EnvironmentSelect] Selected: Passthrough");
            OnEnvironmentSelected?.Invoke(TherapyEnvironment.Passthrough);
        }

        private void SelectGarden()
        {
            Debug.Log("[EnvironmentSelect] Selected: Garden");
            OnEnvironmentSelected?.Invoke(TherapyEnvironment.Garden);
        }

        private void SelectBeach()
        {
            Debug.Log("[EnvironmentSelect] Selected: Beach");
            OnEnvironmentSelected?.Invoke(TherapyEnvironment.Beach);
        }

        private void SelectMeditation()
        {
            Debug.Log("[EnvironmentSelect] Selected: MeditationRoom");
            OnEnvironmentSelected?.Invoke(TherapyEnvironment.MeditationRoom);
        }
    }
}
