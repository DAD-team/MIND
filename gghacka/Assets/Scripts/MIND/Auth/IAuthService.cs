using System;

namespace MIND.Auth
{
    /// <summary>
    /// Interface for authentication providers.
    /// Implementations: FirebaseAuthService (production), MockAuthService (testing).
    /// </summary>
    public interface IAuthService
    {
        bool IsInitialized { get; }
        bool IsSignedIn { get; }
        UserData CurrentUser { get; }

        void SignInWithEmail(string email, string password, Action<AuthResult> callback);
        void SignUpWithEmail(string email, string password, string displayName, Action<AuthResult> callback);
        void SignInWithGoogle(Action<AuthResult> callback);
        void SignOut();

        event Action<UserData> OnSignedIn;
        event Action OnSignedOut;
    }
}
