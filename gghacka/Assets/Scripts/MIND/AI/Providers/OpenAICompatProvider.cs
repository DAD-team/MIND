using System;
using System.Collections;
using System.Globalization;
using System.Text;
using MIND.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace MIND.AI
{
    /// <summary>
    /// OpenAI-compatible API provider.
    /// Works with: OpenAI, Gemini (OpenAI-compat), Groq, Together, Ollama, LM Studio, etc.
    /// </summary>
    public class OpenAICompatProvider : IAIProvider
    {
        private readonly ConversationConfig _config;

        /// <summary>Bat de in full request body (system + history) moi lan goi API.</summary>
        public static bool DebugLogRequest = true;

        public OpenAICompatProvider(ConversationConfig config)
        {
            _config = config;
        }

        public IEnumerator SendMessage(
            string systemPrompt,
            ConversationHistory history,
            Action<AIResponse> onSuccess,
            Action<string> onError)
        {
            string url = $"{_config.baseUrl.TrimEnd('/')}/chat/completions";
            string body = BuildRequestBody(systemPrompt, history);

            if (DebugLogRequest)
            {
                var dump = new StringBuilder();
                dump.Append("[AIProvider] === REQUEST DUMP ===\n");
                dump.Append($"URL: {url}\n");
                dump.Append($"Model: {_config.model} | temp={_config.temperature} | max_tokens={_config.maxTokens}\n");
                dump.Append($"History count: {history?.Count ?? 0} messages\n");
                dump.Append("--- System prompt ---\n");
                dump.Append(systemPrompt).Append('\n');
                dump.Append("--- Messages sent ---\n");
                if (history != null)
                {
                    int i = 0;
                    foreach (var m in history.Messages)
                    {
                        dump.Append($"[{i++}] {m.role}: {m.content}\n");
                    }
                }
                dump.Append("=== END DUMP ===");
                Debug.Log(dump.ToString());
            }

            using var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            if (!string.IsNullOrEmpty(_config.apiKey))
                request.SetRequestHeader("Authorization", $"Bearer {_config.apiKey}");

            request.timeout = 15;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"HTTP {request.responseCode}: {request.error}");
                yield break;
            }

            string responseJson = request.downloadHandler.text;

            if (DebugLogRequest)
                Debug.Log($"[AIProvider] === RAW RESPONSE ===\n{responseJson}\n=== END ===");

            var aiResponse = ParseResponse(responseJson);

            if (aiResponse != null)
                onSuccess?.Invoke(aiResponse);
            else
                onError?.Invoke($"Failed to parse response: {responseJson}");
        }

        private string BuildRequestBody(string systemPrompt, ConversationHistory history)
        {
            var sb = new StringBuilder(1024);
            sb.Append("{\"model\":\"").Append(EscapeJson(_config.model)).Append("\",");
            sb.Append("\"messages\":[");

            sb.Append("{\"role\":\"system\",\"content\":").Append(QuoteJson(systemPrompt)).Append("}");

            foreach (var msg in history.Messages)
            {
                sb.Append(",{\"role\":\"").Append(EscapeJson(msg.role)).Append("\",");
                sb.Append("\"content\":").Append(QuoteJson(msg.content)).Append("}");
            }

            sb.Append("],");
            // Dung InvariantCulture de tranh locale VN xuat "0,8" (lam JSON invalid)
            sb.Append("\"temperature\":").Append(_config.temperature.ToString("F2", CultureInfo.InvariantCulture)).Append(",");
            sb.Append("\"top_p\":1,");
            sb.Append("\"max_tokens\":").Append(_config.maxTokens);
            sb.Append("}");

            return sb.ToString();
        }

        private AIResponse ParseResponse(string json)
        {
            try
            {
                string contentText = ExtractField(json, "content", afterField: "message");

                // Reasoning model (vd gpt-oss-120b) co the tra "content": null
                // khi xai het max_tokens cho reasoning noi bo. Fallback sang reasoning_content.
                if (string.IsNullOrEmpty(contentText))
                {
                    string reasoning = ExtractField(json, "reasoning_content", afterField: "message");
                    if (!string.IsNullOrEmpty(reasoning))
                    {
                        Debug.LogWarning("[AIProvider] content=null, dung reasoning_content. " +
                                         "Khuyen tang max_tokens hoac doi sang model non-reasoning.");
                        contentText = reasoning;
                    }
                    else
                    {
                        Debug.LogWarning("[AIProvider] Model tra content rong (content=null va reasoning_content rong). " +
                                         "Nguyen nhan thuong gap: max_tokens qua thap cho reasoning model, hoac model tu choi tra loi.");
                        return null;
                    }
                }

                var response = TryParseAIResponse(contentText);

                if (response == null)
                {
                    response = new AIResponse { text = contentText };
                }

                return response;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIProvider] Parse error: {e.Message}");
                return null;
            }
        }

        private AIResponse TryParseAIResponse(string content)
        {
            string trimmed = content.Trim();

            if (trimmed.StartsWith("```"))
            {
                int firstNewline = trimmed.IndexOf('\n');
                int lastFence = trimmed.LastIndexOf("```");
                if (firstNewline > 0 && lastFence > firstNewline)
                    trimmed = trimmed.Substring(firstNewline + 1, lastFence - firstNewline - 1).Trim();
            }

            int jsonStart = trimmed.IndexOf('{');
            int jsonEnd = trimmed.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd <= jsonStart)
                return null;

            string jsonStr = trimmed.Substring(jsonStart, jsonEnd - jsonStart + 1);

            try
            {
                return JsonUtility.FromJson<AIResponse>(jsonStr);
            }
            catch
            {
                return null;
            }
        }

        private string ExtractField(string json, string fieldName, string afterField = null)
        {
            int searchStart = 0;

            if (afterField != null)
            {
                int afterIdx = json.IndexOf($"\"{afterField}\"", StringComparison.Ordinal);
                if (afterIdx >= 0)
                    searchStart = afterIdx;
            }

            string pattern = $"\"{fieldName}\"";
            int fieldIdx = json.IndexOf(pattern, searchStart, StringComparison.Ordinal);
            if (fieldIdx < 0) return null;

            int colonIdx = json.IndexOf(':', fieldIdx + pattern.Length);
            if (colonIdx < 0) return null;

            int i = colonIdx + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

            if (i >= json.Length || json[i] != '"') return null;

            var sb = new StringBuilder();
            i++;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(next); break;
                    }
                    i += 2;
                    continue;
                }
                if (c == '"') break;
                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        private static string QuoteJson(string s)
        {
            return "\"" + EscapeJson(s) + "\"";
        }
    }
}
