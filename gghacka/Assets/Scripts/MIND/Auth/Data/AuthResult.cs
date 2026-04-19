namespace MIND.Auth
{
    public class AuthResult
    {
        public bool Success { get; private set; }
        public UserData User { get; private set; }
        public string ErrorMessage { get; private set; }
        public AuthErrorCode ErrorCode { get; private set; }

        public static AuthResult Ok(UserData user) => new()
        {
            Success = true,
            User = user,
            ErrorCode = AuthErrorCode.None
        };

        public static AuthResult Fail(string message, AuthErrorCode code = AuthErrorCode.Unknown) => new()
        {
            Success = false,
            ErrorMessage = message,
            ErrorCode = code
        };
    }

    public enum AuthErrorCode
    {
        None,
        Unknown,
        InvalidEmail,
        WeakPassword,
        EmailAlreadyInUse,
        UserNotFound,
        WrongPassword,
        NetworkError,
        Cancelled,
        TooManyRequests
    }
}
