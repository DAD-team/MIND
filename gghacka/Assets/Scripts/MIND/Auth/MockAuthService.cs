using System;
using UnityEngine;

namespace MIND.Auth
{
    /// <summary>
    /// Mock auth service for testing in Editor without Firebase.
    /// Auto-succeeds all operations with fake user data.
    /// </summary>
    public class MockAuthService : MonoBehaviour, IAuthService
    {
        [Header("Mock Settings")]
        [SerializeField] private float simulatedDelay = 0.5f;
        [SerializeField] private bool simulateErrors;

        public bool IsInitialized => true;
        public bool IsSignedIn => CurrentUser != null;
        public UserData CurrentUser { get; private set; }

        public event Action<UserData> OnSignedIn;
        public event Action OnSignedOut;

        public void SignInWithEmail(string email, string password, Action<AuthResult> callback)
        {
            if (simulateErrors)
            {
                DelayedCallback(() => callback?.Invoke(
                    AuthResult.Fail("Mock error: wrong password", AuthErrorCode.WrongPassword)));
                return;
            }

            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            {
                callback?.Invoke(AuthResult.Fail("Email khong hop le", AuthErrorCode.InvalidEmail));
                return;
            }

            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                callback?.Invoke(AuthResult.Fail("Mat khau phai co it nhat 6 ky tu", AuthErrorCode.WeakPassword));
                return;
            }

            var user = new UserData
            {
                uid = "mock_" + Guid.NewGuid().ToString("N")[..8],
                email = email,
                displayName = email.Split('@')[0],
                provider = UserData.AuthProvider.Email
            };

            DelayedCallback(() =>
            {
                CurrentUser = user;
                OnSignedIn?.Invoke(user);
                callback?.Invoke(AuthResult.Ok(user));
                Debug.Log($"[MockAuth] Signed in: {user.email}");
            });
        }

        public void SignUpWithEmail(string email, string password, string displayName, Action<AuthResult> callback)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            {
                callback?.Invoke(AuthResult.Fail("Email khong hop le", AuthErrorCode.InvalidEmail));
                return;
            }

            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                callback?.Invoke(AuthResult.Fail("Mat khau phai co it nhat 6 ky tu", AuthErrorCode.WeakPassword));
                return;
            }

            var user = new UserData
            {
                uid = "mock_" + Guid.NewGuid().ToString("N")[..8],
                email = email,
                displayName = displayName ?? email.Split('@')[0],
                provider = UserData.AuthProvider.Email
            };

            DelayedCallback(() =>
            {
                CurrentUser = user;
                OnSignedIn?.Invoke(user);
                callback?.Invoke(AuthResult.Ok(user));
                Debug.Log($"[MockAuth] Registered & signed in: {user.email} ({user.displayName})");
            });
        }

        public void SignInWithGoogle(Action<AuthResult> callback)
        {
            var user = new UserData
            {
                uid = "google_mock_" + Guid.NewGuid().ToString("N")[..8],
                email = "mockuser@gmail.com",
                displayName = "Mock Google User",
                photoUrl = "",
                provider = UserData.AuthProvider.Google
            };

            DelayedCallback(() =>
            {
                CurrentUser = user;
                OnSignedIn?.Invoke(user);
                callback?.Invoke(AuthResult.Ok(user));
                Debug.Log("[MockAuth] Google sign in (mock)");
            });
        }

        public void SignOut()
        {
            CurrentUser = null;
            OnSignedOut?.Invoke();
            Debug.Log("[MockAuth] Signed out");
        }

        private void DelayedCallback(Action action)
        {
            if (simulatedDelay <= 0)
            {
                action();
                return;
            }
            StartCoroutine(DelayCoroutine(action));
        }

        private System.Collections.IEnumerator DelayCoroutine(Action action)
        {
            yield return new WaitForSeconds(simulatedDelay);
            action();
        }
    }
}
