using MIND.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MIND.UI
{
    /// <summary>
    /// Displays session summary after the therapy session ends.
    /// </summary>
    public class SessionSummaryUI : MonoBehaviour
    {
        [SerializeField] private GameObject summaryPanel;
        [SerializeField] private Text durationText;
        [SerializeField] private Text exercisesText;

        private void OnEnable()
        {
            SessionEvents.OnSessionEnded += ShowSummary;
        }

        private void OnDisable()
        {
            SessionEvents.OnSessionEnded -= ShowSummary;
        }

        private void Start()
        {
            if (summaryPanel != null)
                summaryPanel.SetActive(false);
        }

        private void ShowSummary(SessionLog log)
        {
            if (summaryPanel == null) return;

            summaryPanel.SetActive(true);

            if (durationText != null)
                durationText.text = $"Phiên hôm nay: {log.durationMinutes:F0} phút";

            if (exercisesText != null)
            {
                string exercises = log.exercisesCompleted.Count > 0
                    ? $"Bạn đã thực hiện {log.exercisesCompleted.Count} bài tập"
                    : "Không có bài tập nào được thực hiện";
                exercisesText.text = exercises;
            }
        }

        public void Hide()
        {
            if (summaryPanel != null)
                summaryPanel.SetActive(false);
        }
    }
}
