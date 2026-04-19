using System;
using UnityEngine;
using UnityEngine.UI;

namespace MIND.UI
{
    /// <summary>
    /// Button to end the VR therapy session.
    /// Fires OnEndRequested event — Session module subscribes to handle the actual end.
    /// </summary>
    public class SessionEndButton : MonoBehaviour
    {
        [SerializeField] private Button endButton;

        public event Action OnEndRequested;

        private void Awake()
        {
            if (endButton == null)
                endButton = GetComponent<Button>();

            if (endButton != null)
                endButton.onClick.AddListener(OnEndClicked);
        }

        private void OnEndClicked()
        {
            OnEndRequested?.Invoke();
        }
    }
}
