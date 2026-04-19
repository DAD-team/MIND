using System;

namespace MIND.Core
{
    /// <summary>
    /// Fetch du lieu user tu backend (emotion profile, preferences).
    /// Implementation: MockUserDataService (dev), FirebaseUserDataService (prod).
    /// </summary>
    public interface IUserDataService
    {
        /// <summary>
        /// Fetch emotion profile cua user tu server.
        /// Callback tra ve EmotionProfile (null neu loi).
        /// </summary>
        void FetchEmotionProfile(string userId, Action<EmotionProfile> onSuccess, Action<string> onError);
    }
}
