using System;
using System.Collections.Generic;

namespace MIND.Core
{
    [Serializable]
    public class SessionConfig
    {
        public string userName;
        public float maxDurationMinutes = 20f;
        public TherapyEnvironment environment = TherapyEnvironment.Garden;
        public EmotionProfile emotionProfile;
    }

    [Serializable]
    public class SessionLog
    {
        public string sessionId;
        public string userId;
        public string timestamp;
        public float durationMinutes;
        public int exchangeCount;
        public List<string> exercisesCompleted = new();
        public SessionEndTrigger endTrigger;
        public bool riskAlertTriggered;
        public int preSessionPhq9;
    }
}
