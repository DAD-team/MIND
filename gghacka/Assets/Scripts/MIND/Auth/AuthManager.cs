using System;
using UnityEngine;

namespace MIND.Auth
{
    /// <summary>
    /// Central auth manager. Routes to Firebase or Mock depending on configuration.
    /// Tat ca chay trong 1 scene duy nhat (khong DontDestroyOnLoad).
    ///
    /// Setup:
    ///   - Keo FirebaseAuthService HOAC MockAuthService vao field authService
    ///   - MockAuthService de test trong Editor khong can Firebase
    /// </summary>
    public class AuthManager : MonoBehaviour
    {
        [Header("Auth Service (keo 1 trong 2 vao day)")]
        [SerializeField] private MonoBehaviour authServiceComponent;

        private IAuthService _authService;

        public static AuthManager Instance { get; private set; }

        public IAuthService Auth => _authService;
        public bool IsSignedIn => _authService?.IsSignedIn ?? false;
        public UserData CurrentUser => _authService?.CurrentUser;

        // Events relay
        public event Action<UserData> OnSignedIn;
        public event Action OnSignedOut;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            // Resolve auth service
            if (authServiceComponent is IAuthService service)
            {
                _authService = service;
            }
            else
            {
                _authService = GetComponent<IAuthService>();
            }

            if (_authService == null)
            {
                Debug.LogError("[AuthManager] No IAuthService found! Assign FirebaseAuthService or MockAuthService.");
                return;
            }

            _authService.OnSignedIn += HandleSignedIn;
            _authService.OnSignedOut += HandleSignedOut;

            Debug.Log($"[AuthManager] Using auth service: {_authService.GetType().Name}");
        }

        private void OnDestroy()
        {
            if (_authService != null)
            {
                _authService.OnSignedIn -= HandleSignedIn;
                _authService.OnSignedOut -= HandleSignedOut;
            }

            if (Instance == this)
                Instance = null;
        }

        private void HandleSignedIn(UserData user)
        {
            Debug.Log($"[AuthManager] User signed in: {user.email} ({user.provider})");
            OnSignedIn?.Invoke(user);
        }

        private void HandleSignedOut()
        {
            Debug.Log("[AuthManager] User signed out");
            OnSignedOut?.Invoke();
        }

        // ================================================================
        // Public API (delegates to auth service)
        // ================================================================

        public void SignInWithEmail(string email, string password, Action<AuthResult> callback = null)
        {
            if (_authService == null)
            {
                callback?.Invoke(AuthResult.Fail("Auth service not initialized"));
                return;
            }
            _authService.SignInWithEmail(email, password, callback);
        }

        public void SignUpWithEmail(string email, string password, string displayName, Action<AuthResult> callback = null)
        {
            if (_authService == null)
            {
                callback?.Invoke(AuthResult.Fail("Auth service not initialized"));
                return;
            }
            _authService.SignUpWithEmail(email, password, displayName, callback);
        }

        public void SignInWithGoogle(Action<AuthResult> callback = null)
        {
            if (_authService == null)
            {
                callback?.Invoke(AuthResult.Fail("Auth service not initialized"));
                return;
            }
            _authService.SignInWithGoogle(callback);
        }

        public void SignOut()
        {
            _authService?.SignOut();
        }
    }
}
