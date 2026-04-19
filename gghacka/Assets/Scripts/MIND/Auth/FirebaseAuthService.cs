using System;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using UnityEngine;

namespace MIND.Auth
{
    /// <summary>
    /// Firebase Authentication service.
    /// Ho tro Email/Password va Google Sign-In.
    ///
    /// Setup:
    /// 1. Import FirebaseAuth.unitypackage (da xong)
    /// 2. Dat google-services.json vao Assets/
    /// 3. Bat Email/Password sign-in trong Firebase Console
    /// </summary>
    public class FirebaseAuthService : MonoBehaviour, IAuthService
    {
        public bool IsInitialized { get; private set; }
        public bool IsSignedIn => CurrentUser != null;
        public UserData CurrentUser { get; private set; }

        public event Action<UserData> OnSignedIn;
        public event Action OnSignedOut;

        private FirebaseAuth _auth;

        private void Start()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result == DependencyStatus.Available)
                {
                    _auth = FirebaseAuth.DefaultInstance;
                    _auth.StateChanged += OnAuthStateChanged;
                    IsInitialized = true;

                    if (_auth.CurrentUser != null)
                    {
                        CurrentUser = FirebaseUserToData(_auth.CurrentUser);
                        OnSignedIn?.Invoke(CurrentUser);
                    }

                    Debug.Log("[FirebaseAuth] Initialized successfully");
                }
                else
                {
                    Debug.LogError($"[FirebaseAuth] Could not resolve dependencies: {task.Result}");
                }
            });
        }

        private void OnDestroy()
        {
            if (_auth != null)
                _auth.StateChanged -= OnAuthStateChanged;
        }

        private void OnAuthStateChanged(object sender, EventArgs e)
        {
            if (_auth.CurrentUser != null)
            {
                CurrentUser = FirebaseUserToData(_auth.CurrentUser);
                OnSignedIn?.Invoke(CurrentUser);
            }
            else
            {
                CurrentUser = null;
                OnSignedOut?.Invoke();
            }
        }

        public void SignInWithEmail(string email, string password, Action<AuthResult> callback)
        {
            if (!IsInitialized)
            {
                callback?.Invoke(AuthResult.Fail("Firebase chua khoi tao xong, vui long doi", AuthErrorCode.Unknown));
                return;
            }

            Debug.Log($"[FirebaseAuth] Signing in: {email}");

            _auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled)
                {
                    callback?.Invoke(AuthResult.Fail("Dang nhap bi huy", AuthErrorCode.Cancelled));
                    return;
                }
                if (task.IsFaulted)
                {
                    callback?.Invoke(ParseFirebaseError(task.Exception));
                    return;
                }

                var user = FirebaseUserToData(task.Result.User);
                Debug.Log($"[FirebaseAuth] Sign in success: {user.email}");
                callback?.Invoke(AuthResult.Ok(user));
            });
        }

        public void SignUpWithEmail(string email, string password, string displayName, Action<AuthResult> callback)
        {
            if (!IsInitialized)
            {
                callback?.Invoke(AuthResult.Fail("Firebase chua khoi tao xong, vui long doi", AuthErrorCode.Unknown));
                return;
            }

            Debug.Log($"[FirebaseAuth] Registering: {email} ({displayName})");

            _auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled)
                {
                    callback?.Invoke(AuthResult.Fail("Dang ky bi huy", AuthErrorCode.Cancelled));
                    return;
                }
                if (task.IsFaulted)
                {
                    callback?.Invoke(ParseFirebaseError(task.Exception));
                    return;
                }

                // Update display name
                var profile = new UserProfile { DisplayName = displayName };
                task.Result.User.UpdateUserProfileAsync(profile).ContinueWithOnMainThread(updateTask =>
                {
                    var user = FirebaseUserToData(_auth.CurrentUser);
                    user.displayName = displayName;
                    Debug.Log($"[FirebaseAuth] Register success: {user.email} ({user.displayName})");
                    callback?.Invoke(AuthResult.Ok(user));
                });
            });
        }

        public void SignInWithGoogle(Action<AuthResult> callback)
        {
            // Google Sign-In tren Quest 3 can them plugin rieng
            // Tam thoi chua ho tro
            Debug.LogWarning("[FirebaseAuth] Google Sign-In chua duoc cau hinh");
            callback?.Invoke(AuthResult.Fail("Google Sign-In chua duoc cau hinh", AuthErrorCode.Unknown));
        }

        public void SignOut()
        {
            if (_auth != null)
            {
                _auth.SignOut();
                CurrentUser = null;
                Debug.Log("[FirebaseAuth] Signed out");
            }
        }

        private static UserData FirebaseUserToData(FirebaseUser firebaseUser)
        {
            return new UserData
            {
                uid = firebaseUser.UserId,
                email = firebaseUser.Email,
                displayName = firebaseUser.DisplayName ?? "",
                photoUrl = firebaseUser.PhotoUrl?.ToString() ?? "",
                provider = firebaseUser.ProviderId == "google.com"
                    ? UserData.AuthProvider.Google
                    : UserData.AuthProvider.Email
            };
        }

        private static AuthResult ParseFirebaseError(AggregateException exception)
        {
            foreach (var inner in exception.Flatten().InnerExceptions)
            {
                if (inner is FirebaseException firebaseEx)
                {
                    var code = (AuthError)firebaseEx.ErrorCode;
                    var authErrorCode = code switch
                    {
                        AuthError.InvalidEmail => AuthErrorCode.InvalidEmail,
                        AuthError.WeakPassword => AuthErrorCode.WeakPassword,
                        AuthError.EmailAlreadyInUse => AuthErrorCode.EmailAlreadyInUse,
                        AuthError.UserNotFound => AuthErrorCode.UserNotFound,
                        AuthError.WrongPassword => AuthErrorCode.WrongPassword,
                        AuthError.TooManyRequests => AuthErrorCode.TooManyRequests,
                        AuthError.NetworkRequestFailed => AuthErrorCode.NetworkError,
                        _ => AuthErrorCode.Unknown
                    };
                    Debug.LogWarning($"[FirebaseAuth] Error {code}: {inner.Message}");
                    return AuthResult.Fail(inner.Message, authErrorCode);
                }
            }
            return AuthResult.Fail(exception.Message, AuthErrorCode.Unknown);
        }
    }
}
