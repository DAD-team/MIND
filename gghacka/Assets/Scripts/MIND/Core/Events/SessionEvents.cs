using System;

namespace MIND.Core
{
    public static class SessionEvents
    {
        public static event Action<SessionConfig> OnSessionStarted;
        public static event Action<SessionLog> OnSessionEnded;
        public static event Action<ConversationState> OnStateChanged;
        public static event Action<string> OnUserSaid;
        public static event Action<AIResponse> OnAIResponded;
        public static event Action<string> OnExerciseStarted;
        public static event Action<string> OnExerciseCompleted;
        public static event Action OnRiskDetected;

        public static void RaiseSessionStarted(SessionConfig config) => OnSessionStarted?.Invoke(config);
        public static void RaiseSessionEnded(SessionLog log) => OnSessionEnded?.Invoke(log);
        public static void RaiseStateChanged(ConversationState state) => OnStateChanged?.Invoke(state);
        public static void RaiseUserSaid(string text) => OnUserSaid?.Invoke(text);
        public static void RaiseAIResponded(AIResponse response) => OnAIResponded?.Invoke(response);
        public static void RaiseExerciseStarted(string exerciseId) => OnExerciseStarted?.Invoke(exerciseId);
        public static void RaiseExerciseCompleted(string exerciseId) => OnExerciseCompleted?.Invoke(exerciseId);
        public static void RaiseRiskDetected() => OnRiskDetected?.Invoke();
    }
}
