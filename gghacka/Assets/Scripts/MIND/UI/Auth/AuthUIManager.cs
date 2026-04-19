using System;
using MIND.Auth;
using UnityEngine;

namespace MIND.UI
{
    /// <summary>
    /// Manages auth UI flow: LoginPanel (dang nhap) ↔ RegisterPanel (dang ky).
    /// Chi 2 panel, chuyen qua lai.
    ///
    /// Setup trong Scene (1 scene duy nhat):
    ///   1. Tao Canvas (World Space), dat truoc mat user
    ///   2. Tao 2 child panels: LoginPanel, EmailRegisterPanel
    ///   3. Keo cac panel vao fields tuong ung
    ///   4. Keo AuthManager vao field authManager
    /// </summary>
    public class AuthUIManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private LoginPanel loginPanel;
        [SerializeField] private EmailRegisterPanel registerPanel;

        [Header("Loading")]
        [SerializeField] private GameObject loadingOverlay;

        [Header("References")]
        [SerializeField] private AuthManager authManager;

        public event Action OnAuthSuccess;

        private void Start()
        {
            if (authManager == null)
                authManager = AuthManager.Instance;

            // Wire up LoginPanel
            if (loginPanel != null)
            {
                loginPanel.OnEmailSubmit += HandleEmailLogin;
                loginPanel.OnGoogleLoginClicked += HandleGoogleLogin;
                loginPanel.OnSwitchToRegister += ShowRegister;
            }

            // Wire up RegisterPanel
            if (registerPanel != null)
            {
                registerPanel.OnSubmit += HandleEmailRegister;
                registerPanel.OnSwitchToLogin += ShowLogin;
                registerPanel.OnBack += ShowLogin;
            }

            // Check if already signed in
            if (authManager != null && authManager.IsSignedIn)
            {
                Debug.Log("[AuthUI] Already signed in, skipping login");
                OnAuthSuccess?.Invoke();
                return;
            }

            ShowLogin();
        }

        private void OnDestroy()
        {
            if (loginPanel != null)
            {
                loginPanel.OnEmailSubmit -= HandleEmailLogin;
                loginPanel.OnGoogleLoginClicked -= HandleGoogleLogin;
                loginPanel.OnSwitchToRegister -= ShowRegister;
            }
            if (registerPanel != null)
            {
                registerPanel.OnSubmit -= HandleEmailRegister;
                registerPanel.OnSwitchToLogin -= ShowLogin;
                registerPanel.OnBack -= ShowLogin;
            }
        }

        // ================================================================
        // Navigation (chi 2 panel)
        // ================================================================

        private void ShowLogin()
        {
            if (loginPanel != null) loginPanel.gameObject.SetActive(true);
            if (registerPanel != null) registerPanel.gameObject.SetActive(false);
            loginPanel?.ClearFields();
        }

        private void ShowRegister()
        {
            if (loginPanel != null) loginPanel.gameObject.SetActive(false);
            if (registerPanel != null) registerPanel.gameObject.SetActive(true);
            registerPanel?.ClearFields();
        }

        /// <summary>
        /// Show auth UI and reset to login panel.
        /// Called by AppFlowManager when user signs out.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            ShowLogin();
        }

        // ================================================================
        // Auth handlers
        // ================================================================

        private void HandleEmailLogin(string email, string password)
        {
            if (authManager == null) return;

            SetLoading(true);
            loginPanel?.SetInteractable(false);

            authManager.SignInWithEmail(email, password, result =>
            {
                SetLoading(false);
                loginPanel?.SetInteractable(true);

                if (result.Success)
                {
                    Debug.Log($"[AuthUI] Login success: {result.User.email}");
                    OnAuthSuccess?.Invoke();
                }
                else
                {
                    Debug.LogWarning($"[AuthUI] Login failed: {result.ErrorMessage}");
                    loginPanel?.ShowError(GetVietnameseError(result.ErrorCode, result.ErrorMessage));
                }
            });
        }

        private void HandleEmailRegister(string email, string password, string displayName)
        {
            if (authManager == null) return;

            SetLoading(true);
            registerPanel?.SetInteractable(false);

            authManager.SignUpWithEmail(email, password, displayName, result =>
            {
                SetLoading(false);
                registerPanel?.SetInteractable(true);

                if (result.Success)
                {
                    Debug.Log($"[AuthUI] Register success: {result.User.email}");
                    OnAuthSuccess?.Invoke();
                }
                else
                {
                    Debug.LogWarning($"[AuthUI] Register failed: {result.ErrorMessage}");
                    registerPanel?.ShowError(GetVietnameseError(result.ErrorCode, result.ErrorMessage));
                }
            });
        }

        private void HandleGoogleLogin()
        {
            if (authManager == null) return;

            SetLoading(true);
            loginPanel?.SetInteractable(false);

            authManager.SignInWithGoogle(result =>
            {
                SetLoading(false);
                loginPanel?.SetInteractable(true);

                if (result.Success)
                {
                    Debug.Log($"[AuthUI] Google login success: {result.User.email}");
                    OnAuthSuccess?.Invoke();
                }
                else
                {
                    Debug.LogWarning($"[AuthUI] Google login failed: {result.ErrorMessage}");
                    loginPanel?.ShowError(result.ErrorMessage);
                }
            });
        }

        private void SetLoading(bool loading)
        {
            if (loadingOverlay != null)
                loadingOverlay.SetActive(loading);
        }

        private static string GetVietnameseError(AuthErrorCode code, string fallback)
        {
            return code switch
            {
                AuthErrorCode.InvalidEmail => "Email không hợp lệ",
                AuthErrorCode.WeakPassword => "Mật khẩu phải có ít nhất 6 ký tự",
                AuthErrorCode.EmailAlreadyInUse => "Email đã được sử dụng",
                AuthErrorCode.UserNotFound => "Không tìm thấy tài khoản",
                AuthErrorCode.WrongPassword => "Sai mật khẩu",
                AuthErrorCode.NetworkError => "Lỗi kết nối mạng",
                AuthErrorCode.TooManyRequests => "Quá nhiều lần thử, vui lòng đợi",
                _ => fallback ?? "Đã có lỗi xảy ra"
            };
        }
    }
}
