using System;
using System.Collections;

namespace MIND.Core
{
    public interface IAIProvider
    {
        IEnumerator SendMessage(
            string systemPrompt,
            ConversationHistory history,
            Action<AIResponse> onSuccess,
            Action<string> onError
        );
    }
}
