using System;
using System.Collections.Generic;

namespace MIND.Core
{
    [Serializable]
    public class ConversationMessage
    {
        public string role;
        public string content;

        public ConversationMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    [Serializable]
    public class AIResponse
    {
        public string text;
    }

    [Serializable]
    public class ConversationConfig
    {
        public string baseUrl = "https://api.groq.com/openai/v1";
        public string apiKey = "";
        public string model = "llama-3.3-70b-versatile";
        public float temperature = 0.7f;
        public int maxTokens = 512;
    }

    public class ConversationHistory
    {
        private readonly List<ConversationMessage> _messages = new();
        private readonly int _maxTurns;

        public IReadOnlyList<ConversationMessage> Messages => _messages;
        public int Count => _messages.Count;

        public ConversationHistory(int maxTurns = 20)
        {
            _maxTurns = maxTurns;
        }

        public void Add(string role, string content)
        {
            _messages.Add(new ConversationMessage(role, content));
            TrimIfNeeded();
        }

        public void Clear()
        {
            _messages.Clear();
        }

        private void TrimIfNeeded()
        {
            int maxMessages = _maxTurns * 2;
            while (_messages.Count > maxMessages)
            {
                _messages.RemoveAt(0);
            }
        }
    }
}
