using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MIND.UI
{
    /// <summary>
    /// Panel dang nhap gom Google Sign-In + Email/Password trong 1 panel.
    /// Dung TMP_InputField cho VR input.
    ///
    /// Hierarchy:
    ///   LoginPanel (this script)
    ///   ├── Title (TMP) "MIND VR"
    ///   ├── Subtitle (TMP) "Khong gian tri lieu cua ban"
    ///   ├── GoogleButton
    ///   ├── Divider (TMP) "─── hoac ───"
    ///   ├── EmailInput (TMP_InputField)
    ///   ├── PasswordInput (TMP_InputField)
    ///   ├── LoginButton
    ///   ├── RegisterLink Button (TMP) "Chua co tai khoan? Dang ky"
    ///   └── ErrorText (TMP, an mac dinh)
    /// </summary>
    public class LoginPanel : MonoBehaviour
    {
        [Header("Google")]
        [SerializeField] private Button googleButton;

        [Header("Email Login")]
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button loginButton;

        [Header("Navigation")]
        [SerializeField] private Button registerLink;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI errorText;

        public event Action<string, string> OnEmailSubmit;
        public event Action OnGoogleLoginClicked;
        public event Action OnSwitchToRegister;

        private void Awake()
        {
            if (googleButton != null)
                googleButton.onClick.AddListener(() => OnGoogleLoginClicked?.Invoke());

            if (loginButton != null)
                loginButton.onClick.AddListener(HandleLogin);

            if (registerLink != null)
                registerLink.onClick.AddListener(() => OnSwitchToRegister?.Invoke());

            if (passwordInput != null)
                passwordInput.contentType = TMP_InputField.ContentType.Password;

            HideError();
        }

        private void HandleLogin()
        {
            HideError();

            string email = emailInput != null ? emailInput.text.Trim() : "";
            string password = passwordInput != null ? passwordInput.text : "";

            if (string.IsNullOrEmpty(email))
            {
                ShowError("Vui lòng nhập email");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Vui lòng nhập mật khẩu");
                return;
            }

            OnEmailSubmit?.Invoke(email, password);
        }

        public void ClearFields()
        {
            if (emailInput != null) emailInput.text = "";
            if (passwordInput != null) passwordInput.text = "";
            HideError();
        }

        public void SetInteractable(bool interactable)
        {
            if (emailInput != null) emailInput.interactable = interactable;
            if (passwordInput != null) passwordInput.interactable = interactable;
            if (loginButton != null) loginButton.interactable = interactable;
            if (googleButton != null) googleButton.interactable = interactable;
        }

        public void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.gameObject.SetActive(true);
            }
        }

        public void HideError()
        {
            if (errorText != null)
                errorText.gameObject.SetActive(false);
        }
    }
}
