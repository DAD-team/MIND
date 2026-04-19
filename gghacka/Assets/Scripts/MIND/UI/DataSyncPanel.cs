using System;
using UnityEngine;

namespace MIND.UI
{
    /// <summary>
    /// Debug OnGUI hien thi trang thai dong bo du lieu.
    /// Khong hien len UI cua user — chi de dev test.
    ///
    /// Setup:
    ///   1. Gan script nay vao GameObject bat ky (vd: AppFlow)
    ///   2. Toggle showTestUI trong Inspector
    /// </summary>
    public class DataSyncPanel : MonoBehaviour
    {
        [SerializeField] private bool showTestUI = true;

        public event Action OnRetryClicked;

        private enum SyncState { Syncing, Success, Error }
        private SyncState _syncState;
        private string _errorMessage;
        private Action _successCallback;

        public void Show()
        {
            enabled = true;
            SetSyncing();
        }

        public void Hide()
        {
            enabled = false;
        }

        public void SetSyncing()
        {
            _syncState = SyncState.Syncing;
            _errorMessage = null;
        }

        public void SetSuccess(Action onComplete = null)
        {
            _syncState = SyncState.Success;
            _successCallback = onComplete;

            // Tu dong chuyen tiep sau 0.5s
            if (onComplete != null)
                StartCoroutine(DelayedCallback(0.5f, onComplete));
        }

        public void SetError(string message)
        {
            _syncState = SyncState.Error;
            _errorMessage = message;
        }

        private System.Collections.IEnumerator DelayedCallback(float delay, Action callback)
        {
            yield return new WaitForSeconds(delay);
            callback?.Invoke();
        }

        private void OnGUI()
        {
            if (!showTestUI) return;

            var boxRect = new Rect(10, 10, 350, 120);
            GUI.Box(boxRect, "[ DataSync Debug ]");

            float y = 35;

            switch (_syncState)
            {
                case SyncState.Syncing:
                    GUI.Label(new Rect(20, y, 330, 25), "Trạng thái: ĐANG ĐỒNG BỘ...");
                    int dots = (int)(Time.time * 3) % 4;
                    GUI.Label(new Rect(20, y + 25, 330, 25), "Đang tải emotion profile" + new string('.', dots));
                    break;

                case SyncState.Success:
                    GUI.Label(new Rect(20, y, 330, 25), "Trạng thái: HOÀN TẤT");
                    GUI.Label(new Rect(20, y + 25, 330, 25), "Profile đã tải về, đang chuyển tiếp...");
                    break;

                case SyncState.Error:
                    GUI.Label(new Rect(20, y, 330, 25), "Trạng thái: LỖI");
                    GUI.Label(new Rect(20, y + 25, 330, 25), _errorMessage ?? "Lỗi không xác định");
                    if (GUI.Button(new Rect(20, y + 55, 100, 30), "Thử lại"))
                    {
                        OnRetryClicked?.Invoke();
                    }
                    break;
            }
        }
    }
}
