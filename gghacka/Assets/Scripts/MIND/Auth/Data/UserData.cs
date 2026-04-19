using System;

namespace MIND.Auth
{
    [Serializable]
    public class UserData
    {
        public string uid;
        public string email;
        public string displayName;
        public string photoUrl;
        public AuthProvider provider;

        public enum AuthProvider
        {
            Email,
            Google
        }
    }
}
