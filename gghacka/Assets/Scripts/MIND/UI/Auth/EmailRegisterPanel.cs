using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MIND.UI
{
    /// <summary>
    /// Email registration form. Dung TMP_InputField cho VR input.
    ///
    /// Hierarchy:
    ///   EmailRegisterPanel (this script)
    ///   ├── Title (TMP) "Dang ky tai khoan"
    ///   ├── DisplayNameInput (TMP_InputField)
    ///   ├── EmailInput (TMP_InputField)
    ///   ├── PasswordInput (TMP_InputField)
    ///   ├── ConfirmPasswordInput (TMP_InputField)
    ///   ├── RegisterButton
    ///   ├── LoginLink Button (TMP) "Da co tai khoan? Dang nhap"
    ///   ├── BackButton
    ///   └── ErrorText (TMP, an mac dinh)
    /// </summary>
    public class EmailRegisterPanel : MonoBehaviour
    {
        [Header("Input Fields")]
        [SerializeField] private TMP_InputField displayNameInput;
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private TMP_InputField confirmPasswordInput;

        [Header("Buttons")]
        [SerializeField] private Button registerButton;
        [SerializeField] private Button loginLink;
        [SerializeField] private Button backButton;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI errorText;

        public event Action<string, string, string> OnSubmit;
        public event Action OnSwitchToLogin;
        public event Action OnBack;

        private void Awake()
        {
            if (registerButton != null)
                registerButton.onClick.AddListener(HandleRegister);

            if (loginLink != null)
                loginLink.onClick.AddListener(() => OnSwitchToLogin?.Invoke());

            if (backButton != null)
                backButton.onClick.AddListener(() => OnBack?.Invoke());

            if (passwordInput != null)
                passwordInput.contentType = TMP_InputField.ContentType.Password;

            if (confirmPasswordInput != null)
                confirmPasswordInput.contentType = TMP_InputField.ContentType.Password;

            HideError();
        }

        private void HandleRegister()
        {
            HideError();

            string displayName = displayNameInput != null ? displayNameInput.text.Trim() : "";
            string email = emailInput != null ? emailInput.text.Trim() : "";
            string password = passwordInput != null ? passwordInput.text : "";
            string confirmPassword = confirmPasswordInput != null ? confirmPasswordInput.text : "";

            if (string.IsNullOrEmpty(displayName))
            {
                ShowError("Vui lòng nhập tên hiển thị");
                return;
            }

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

            if (password.Length < 6)
            {
                ShowError("Mật khẩu phải có ít nhất 6 ký tự");
                return;
            }

            if (password != confirmPassword)
            {
                ShowError("Mật khẩu xác nhận không khớp");
                return;
            }

            OnSubmit?.Invoke(email, password, displayName);
        }

        public void ClearFields()
        {
            if (displayNameInput != null) displayNameInput.text = "";
            if (emailInput != null) emailInput.text = "";
            if (passwordInput != null) passwordInput.text = "";
            if (confirmPasswordInput != null) confirmPasswordInput.text = "";
            HideError();
        }

        public void SetInteractable(bool interactable)
        {
            if (displayNameInput != null) displayNameInput.interactable = interactable;
            if (emailInput != null) emailInput.interactable = interactable;
            if (passwordInput != null) passwordInput.interactable = interactable;
            if (confirmPasswordInput != null) confirmPasswordInput.interactable = interactable;
            if (registerButton != null) registerButton.interactable = interactable;
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
