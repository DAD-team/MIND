using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MIND.Speech
{
    /// <summary>
    /// Cloud TTS qua FPT AI Marketplace (hoac bat ky endpoint OpenAI-compat nao).
    ///
    /// Endpoint: POST {baseUrl}/v1/audio/speech
    /// Auth:     Authorization: Bearer {apiKey}
    /// Body:     {"model": "...", "input": "...", "voice": "...", "response_format": "wav"}
    /// Returns:  raw audio bytes (WAV)
    ///
    /// Model tren FPT Marketplace: "FPT.AI-VITs" (Vietnamese TTS).
    /// Neu endpoint cua FPT khac chuan OpenAI, chinh sua BuildRequestBody() va ParseAudio().
    /// </summary>
    public class CloudTTS : MonoBehaviour, Core.ITTS
    {
        [Header("API")]
        [SerializeField] private string baseUrl = "https://mkp-api.fptcloud.com";
        [SerializeField] private string apiKey = "";
        [Tooltip("TTS model name — e.g. FPT.AI-VITs")]
        [SerializeField] private string model = "FPT.AI-VITs";
        [Tooltip("Voice ID. Kiem tra model card tren Marketplace de lay voice hop le.")]
        [SerializeField] private string voice = "";
        [Tooltip("Audio format tra ve (wav khuyen nghi de Unity parse duoc).")]
        [SerializeField] private string responseFormat = "wav";
        [Tooltip("Speed (1.0 = normal). De 0 = khong gui.")]
        [SerializeField] private float speed = 0f;
        [SerializeField] private int requestTimeoutSeconds = 30;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = true;
        [SerializeField] private bool showTestUI = false;
        [SerializeField] private string testText = "Xin chào, tôi là trợ lý ảo.";

        // Audio
        private AudioSource _audioSource;
        private bool _isSpeaking;
        private Coroutine _currentCoroutine;
        private string _statusMsg = "Ready.";

        // ITTS
        public bool IsSpeaking => _isSpeaking;
        public bool IsReady => !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(model);

        /// <summary>
        /// Override config at runtime (called by MindConversation de dong bo config voi LLM).
        /// </summary>
        public void Configure(string newBaseUrl, string newApiKey, string newModel = null, string newVoice = null)
        {
            if (!string.IsNullOrEmpty(newBaseUrl)) baseUrl = newBaseUrl;
            if (!string.IsNullOrEmpty(newApiKey)) apiKey = newApiKey;
            if (!string.IsNullOrEmpty(newModel)) model = newModel;
            if (newVoice != null) voice = newVoice;
        }

        private void Awake()
        {
            EnsureAudioSource();
            if (string.IsNullOrEmpty(apiKey))
                Debug.LogWarning("[CloudTTS] API key is empty!");
        }

        private void EnsureAudioSource()
        {
            if (_audioSource != null) return;
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.volume = 1.0f;
            _audioSource.spatialBlend = 0f;
        }

        public void Speak(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            EnsureAudioSource();

            if (_isSpeaking) Stop();
            _currentCoroutine = StartCoroutine(SpeakCoroutine(text));
        }

        public void Stop()
        {
            if (_currentCoroutine != null)
            {
                StopCoroutine(_currentCoroutine);
                _currentCoroutine = null;
            }
            if (_audioSource != null) _audioSource.Stop();
            _isSpeaking = false;
            _statusMsg = "Stopped.";
        }

        private void OnDestroy()
        {
            if (_audioSource != null && _audioSource.clip != null)
            {
                _audioSource.Stop();
                Destroy(_audioSource.clip);
            }
        }

        // ================================================================
        // HTTP request
        // ================================================================

        private IEnumerator SpeakCoroutine(string text)
        {
            _isSpeaking = true;
            _statusMsg = "Requesting TTS...";

            string url = $"{NormalizeBaseUrl(baseUrl)}/v1/audio/speech";
            string body = BuildRequestBody(text);

            using var req = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(apiKey))
                req.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            req.timeout = requestTimeoutSeconds;

            if (verboseLogs) Debug.Log($"[CloudTTS] POST {url} — {text.Substring(0, Math.Min(text.Length, 50))}...");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[CloudTTS] HTTP {req.responseCode}: {req.error} — {req.downloadHandler?.text}");
                _statusMsg = $"Error: {req.error}";
                _isSpeaking = false;
                yield break;
            }

            byte[] audioBytes = req.downloadHandler.data;
            if (audioBytes == null || audioBytes.Length < 44)
            {
                Debug.LogError($"[CloudTTS] Invalid audio data ({audioBytes?.Length ?? 0} bytes). Raw response: {req.downloadHandler.text}");
                _statusMsg = "Invalid audio.";
                _isSpeaking = false;
                yield break;
            }

            AudioClip clip = WavToAudioClip(audioBytes);
            if (clip == null)
            {
                Debug.LogError("[CloudTTS] Failed to parse WAV. First 4 bytes: " + BitConverter.ToString(audioBytes, 0, Math.Min(4, audioBytes.Length)));
                _statusMsg = "Parse failed.";
                _isSpeaking = false;
                yield break;
            }

            if (_audioSource.clip != null)
            {
                _audioSource.Stop();
                var old = _audioSource.clip;
                _audioSource.clip = null;
                Destroy(old);
            }

            _audioSource.clip = clip;
            _audioSource.Play();
            _statusMsg = $"Playing {clip.length:F1}s";
            if (verboseLogs) Debug.Log($"[CloudTTS] Playing {clip.length:F1}s");

            while (_audioSource.isPlaying)
                yield return null;

            _isSpeaking = false;
            _statusMsg = "Done.";
        }

        private string BuildRequestBody(string text)
        {
            var sb = new StringBuilder(256);
            sb.Append('{');
            sb.Append("\"model\":").Append(Q(model)).Append(',');
            sb.Append("\"input\":").Append(Q(text));
            if (!string.IsNullOrEmpty(voice))
                sb.Append(",\"voice\":").Append(Q(voice));
            if (!string.IsNullOrEmpty(responseFormat))
                sb.Append(",\"response_format\":").Append(Q(responseFormat));
            if (speed > 0.01f)
                sb.Append(",\"speed\":").Append(speed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append('}');
            return sb.ToString();
        }

        private static string Q(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append($"\\u{(int)c:X4}");
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // ================================================================
        // URL normalization — chap nhan ca dang co va khong co /v1
        // ================================================================

        private static string NormalizeBaseUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            string trimmed = url.TrimEnd('/');
            if (trimmed.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - "/openai/v1".Length);
            else if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - "/v1".Length);
            return trimmed.TrimEnd('/');
        }

        // ================================================================
        // WAV parser (16-bit PCM)
        // ================================================================

        private static AudioClip WavToAudioClip(byte[] wavData)
        {
            try
            {
                if (wavData.Length < 44) return null;
                if (Encoding.ASCII.GetString(wavData, 0, 4) != "RIFF") return null;
                if (Encoding.ASCII.GetString(wavData, 8, 4) != "WAVE") return null;

                int channels = BitConverter.ToInt16(wavData, 22);
                int sampleRate = BitConverter.ToInt32(wavData, 24);
                int bitsPerSample = BitConverter.ToInt16(wavData, 34);

                // Seek 'data' chunk
                int dataOffset = 12;
                int dataSize = 0;
                while (dataOffset < wavData.Length - 8)
                {
                    string chunkId = Encoding.ASCII.GetString(wavData, dataOffset, 4);
                    int chunkSize = BitConverter.ToInt32(wavData, dataOffset + 4);

                    if (chunkId == "data")
                    {
                        dataOffset += 8;
                        dataSize = Math.Min(chunkSize, wavData.Length - dataOffset);
                        break;
                    }
                    dataOffset += 8 + chunkSize;
                }

                if (dataSize <= 0) return null;

                int bytesPerSample = bitsPerSample / 8;
                int sampleCount = dataSize / (bytesPerSample * channels);
                float[] samples = new float[sampleCount * channels];

                if (bitsPerSample == 16)
                {
                    for (int i = 0; i < samples.Length; i++)
                    {
                        int offset = dataOffset + i * 2;
                        if (offset + 1 >= wavData.Length) break;
                        short sample = BitConverter.ToInt16(wavData, offset);
                        samples[i] = sample / 32768f;
                    }
                }
                else if (bitsPerSample == 32)
                {
                    // 32-bit float
                    for (int i = 0; i < samples.Length; i++)
                    {
                        int offset = dataOffset + i * 4;
                        if (offset + 3 >= wavData.Length) break;
                        samples[i] = BitConverter.ToSingle(wavData, offset);
                    }
                }
                else
                {
                    Debug.LogWarning($"[CloudTTS] Unsupported bits/sample: {bitsPerSample}");
                    return null;
                }

                var clip = AudioClip.Create("CloudTTS", sampleCount, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CloudTTS] WAV parse error: {e.Message}");
                return null;
            }
        }

        // ================================================================
        // Test UI (optional)
        // ================================================================

        private string _inputText = "";

        private void OnGUI()
        {
            if (!showTestUI) return;
            float x = Screen.width - 420, y = 220, w = 400;
            GUI.Box(new Rect(x, y, w, 140), "Cloud TTS");
            y += 25;
            GUI.Label(new Rect(x + 10, y, w - 20, 20), _statusMsg);
            y += 25;
            _inputText = GUI.TextField(new Rect(x + 10, y, w - 20, 22), _inputText);
            if (string.IsNullOrEmpty(_inputText)) _inputText = testText;
            y += 30;
            GUI.enabled = !_isSpeaking && IsReady;
            if (GUI.Button(new Rect(x + 10, y, 120, 30), "Speak"))
                Speak(_inputText);
            GUI.enabled = _isSpeaking;
            if (GUI.Button(new Rect(x + 140, y, 80, 30), "Stop"))
                Stop();
            GUI.enabled = true;
        }
    }
}
