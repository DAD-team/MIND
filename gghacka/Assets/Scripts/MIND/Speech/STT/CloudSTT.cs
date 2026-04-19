using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MIND.Speech
{
    /// <summary>
    /// Cloud STT qua FPT AI Marketplace (hoac bat ky endpoint OpenAI-compat nao).
    ///
    /// Hoat dong:
    ///   1. Thu am lien tuc tu mic (16 kHz mono)
    ///   2. VAD phat hien khoang lang sau tieng noi -> cat thanh 1 segment
    ///   3. Encode segment -> WAV 16-bit PCM trong memory
    ///   4. POST multipart/form-data len /v1/audio/transcriptions
    ///   5. Fire OnTranscriptionResult khi nhan duoc text
    ///
    /// Endpoint: {baseUrl}/v1/audio/transcriptions
    /// Auth:     Authorization: Bearer {apiKey}
    /// Body:     model, file (wav), language, response_format=json
    /// </summary>
    public class CloudSTT : MonoBehaviour, Core.ISTT
    {
        [Header("API")]
        [Tooltip("Base URL — e.g. https://mkp-api.fptcloud.com")]
        [SerializeField] private string baseUrl = "https://mkp-api.fptcloud.com";
        [SerializeField] private string apiKey = "";
        [Tooltip("Model name tren FPT Marketplace. VD: FPT.AI-whisper-large-v3-turbo")]
        [SerializeField] private string model = "FPT.AI-whisper-large-v3-turbo";
        [Tooltip("Language code (vi, en, ...). De trong = auto.")]
        [SerializeField] private string language = "vi";
        [SerializeField] private int requestTimeoutSeconds = 30;

        [Header("VAD Settings")]
        [Tooltip("Nguong energy de xac dinh co tieng noi (0.001 - 0.05). Giam de nhat am nhe hon.")]
        [SerializeField] private float vadThreshold = 0.005f;
        [Tooltip("Thoi gian im lang (giay) truoc khi cat segment. 1.8s phu hop hoi thoai tieng Viet.")]
        [SerializeField] private float silenceTimeout = 1.8f;
        [Tooltip("Do dai toi thieu cua segment (giay) de xu ly. FPT whisper tra 500 neu audio < 1s, giu >= 1.0f.")]
        [SerializeField] private float minSegmentLength = 1.0f;
        [Tooltip("Do dai toi da cua segment (giay)")]
        [SerializeField] private float maxSegmentLength = 15f;
        [Tooltip("Tan so kiem tra VAD (giay)")]
        [SerializeField] private float vadCheckInterval = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = true;
        [SerializeField] private bool showTestUI = false;
        [Tooltip("Dump WAV ra persistentDataPath/stt_dumps/ de debug bang cURL")]
        [SerializeField] private bool dumpWavToDisk = false;

        // Audio
        private string _micDevice;
        private AudioClip _micClip;
        private int _lastMicPos;
        private bool _isStreaming;

        // VAD state
        private enum VadState { Silence, Speaking }
        private VadState _vadState = VadState.Silence;
        private readonly List<float> _speechBuffer = new List<float>();
        private float _silenceTimer;
        private float _speechTimer;
        private float _lastVadCheck;
        private bool _vadActive;

        // In-flight request tracking (cho phep chong xep)
        private int _inFlightRequests;
        private int _utteranceCount;
        private string _lastTranscription = "";
        private string _statusMsg = "Idle.";

        // Public API
        public event Action<string> OnTranscriptionResult;
        public event Action OnVoiceDetected;
        public bool IsStreaming => _isStreaming;

        /// <summary>
        /// Override config at runtime (called by MindConversation de dong bo config voi LLM).
        /// </summary>
        public void Configure(string newBaseUrl, string newApiKey, string newModel = null, string newLanguage = null)
        {
            if (!string.IsNullOrEmpty(newBaseUrl)) baseUrl = newBaseUrl;
            if (!string.IsNullOrEmpty(newApiKey)) apiKey = newApiKey;
            if (!string.IsNullOrEmpty(newModel)) model = newModel;
            if (newLanguage != null) language = newLanguage;
        }

        private const int SampleRate = 16000;

        private void Start()
        {
            if (Microphone.devices.Length > 0)
            {
                _micDevice = Microphone.devices[0];
                if (verboseLogs) Debug.Log($"[CloudSTT] Mic: {_micDevice}");
            }
            else
            {
                _statusMsg = "No microphone!";
                Debug.LogError("[CloudSTT] No microphone available");
            }

            if (string.IsNullOrEmpty(apiKey))
                Debug.LogWarning("[CloudSTT] API key is empty!");
        }

        private void Update()
        {
            if (!_isStreaming || _micClip == null) return;

            float[] newSamples = ReadMicSamples();
            if (newSamples == null) return;

            float now = Time.realtimeSinceStartup;
            if (now - _lastVadCheck < vadCheckInterval)
            {
                if (_vadState == VadState.Speaking)
                    _speechBuffer.AddRange(newSamples);
                return;
            }
            _lastVadCheck = now;

            bool hasVoice = CheckVoiceActivity(newSamples);
            _vadActive = hasVoice;

            switch (_vadState)
            {
                case VadState.Silence:
                    if (hasVoice)
                    {
                        _vadState = VadState.Speaking;
                        _speechBuffer.Clear();
                        _speechBuffer.AddRange(newSamples);
                        _silenceTimer = 0f;
                        _speechTimer = 0f;
                        if (verboseLogs) Debug.Log("[CloudSTT] VAD: speech started");
                        OnVoiceDetected?.Invoke();
                    }
                    break;

                case VadState.Speaking:
                    _speechBuffer.AddRange(newSamples);
                    _speechTimer += vadCheckInterval;

                    if (!hasVoice) _silenceTimer += vadCheckInterval;
                    else _silenceTimer = 0f;

                    bool silenceEnd = _silenceTimer >= silenceTimeout;
                    bool tooLong = _speechTimer >= maxSegmentLength;

                    if (silenceEnd || tooLong)
                    {
                        float segmentDuration = (float)_speechBuffer.Count / SampleRate;

                        if (segmentDuration >= minSegmentLength)
                        {
                            // Trim trailing silence
                            int trimSamples = silenceEnd
                                ? Mathf.Min((int)(silenceTimeout * SampleRate), _speechBuffer.Count / 2)
                                : 0;
                            int keepCount = _speechBuffer.Count - trimSamples;

                            // Re-check duration after trimming — trimmed segment can fall below min
                            float trimmedDuration = (float)keepCount / SampleRate;
                            if (trimmedDuration < HardMinSegmentSeconds)
                            {
                                // Keep enough samples to meet the hard minimum
                                keepCount = Mathf.Max(keepCount, (int)(HardMinSegmentSeconds * SampleRate) + SampleRate / 2);
                                keepCount = Mathf.Min(keepCount, _speechBuffer.Count);
                                trimmedDuration = (float)keepCount / SampleRate;
                            }

                            float[] segment = _speechBuffer.GetRange(0, keepCount).ToArray();

                            if (verboseLogs)
                                Debug.Log($"[CloudSTT] Segment ready: {trimmedDuration:F1}s (pre-trim {segmentDuration:F1}s), {segment.Length} samples");

                            StartCoroutine(SendSegment(segment));
                        }
                        else if (verboseLogs)
                        {
                            Debug.Log($"[CloudSTT] Segment too short ({segmentDuration:F2}s), skipped");
                        }

                        _speechBuffer.Clear();
                        _vadState = VadState.Silence;
                        _silenceTimer = 0f;
                        _speechTimer = 0f;
                    }
                    break;
            }
        }

        // ================================================================
        // Public API
        // ================================================================

        public void StartStreaming()
        {
            if (_isStreaming) return;
            if (_micDevice == null)
            {
                Debug.LogWarning("[CloudSTT] No microphone!");
                return;
            }

            _micClip = Microphone.Start(_micDevice, true, 60, SampleRate);
            _lastMicPos = 0;
            _isStreaming = true;
            _vadState = VadState.Silence;
            _speechBuffer.Clear();
            _silenceTimer = 0f;
            _speechTimer = 0f;
            _utteranceCount = 0;
            _lastTranscription = "";
            _lastVadCheck = Time.realtimeSinceStartup;
            _statusMsg = "Streaming...";

            if (verboseLogs) Debug.Log("[CloudSTT] Started streaming");
        }

        public void StopStreaming()
        {
            if (!_isStreaming) return;
            _isStreaming = false;

            Microphone.End(_micDevice);

            // Flush segment cuoi neu dang noi
            if (_vadState == VadState.Speaking && _speechBuffer.Count > 0)
            {
                float segmentDuration = (float)_speechBuffer.Count / SampleRate;
                if (segmentDuration >= minSegmentLength)
                {
                    float[] segment = _speechBuffer.ToArray();
                    StartCoroutine(SendSegment(segment));
                }
            }

            _speechBuffer.Clear();
            _vadState = VadState.Silence;
            _statusMsg = "Stopped.";
            if (verboseLogs) Debug.Log("[CloudSTT] Stopped streaming");
        }

        private void OnDestroy()
        {
            if (_isStreaming)
            {
                _isStreaming = false;
                Microphone.End(_micDevice);
            }
        }

        // ================================================================
        // Mic / VAD helpers
        // ================================================================

        private float[] ReadMicSamples()
        {
            int micPos = Microphone.GetPosition(_micDevice);
            if (micPos == _lastMicPos) return null;

            int samplesToRead = micPos > _lastMicPos
                ? micPos - _lastMicPos
                : (_micClip.samples - _lastMicPos) + micPos;

            if (samplesToRead <= 0) return null;

            float[] samples = new float[samplesToRead];

            if (micPos > _lastMicPos)
            {
                _micClip.GetData(samples, _lastMicPos);
            }
            else
            {
                int firstPart = _micClip.samples - _lastMicPos;
                float[] part1 = new float[firstPart];
                float[] part2 = new float[micPos];
                _micClip.GetData(part1, _lastMicPos);
                if (micPos > 0) _micClip.GetData(part2, 0);
                Array.Copy(part1, 0, samples, 0, firstPart);
                Array.Copy(part2, 0, samples, firstPart, micPos);
            }

            _lastMicPos = micPos;
            return samples;
        }

        private bool CheckVoiceActivity(float[] samples)
        {
            if (samples.Length == 0) return false;
            float energy = 0f;
            for (int i = 0; i < samples.Length; i++)
                energy += Mathf.Abs(samples[i]);
            energy /= samples.Length;
            return energy > vadThreshold;
        }

        // ================================================================
        // Cloud request
        // ================================================================

        // FPT whisper tu choi audio ngan (< 1s) voi HTTP 500
        private const float HardMinSegmentSeconds = 1.0f;

        private IEnumerator SendSegment(float[] samples)
        {
            // Compute audio stats for debugging
            float audioDuration = (float)samples.Length / SampleRate;

            // Hard guard: FPT whisper returns 500 cho audio < 1s
            if (audioDuration < HardMinSegmentSeconds)
            {
                if (verboseLogs)
                    Debug.LogWarning($"[CloudSTT] Segment {audioDuration:F2}s < {HardMinSegmentSeconds:F1}s hard-min, skipped (FPT whisper refuses short audio)");
                yield break;
            }

            _inFlightRequests++;
            _statusMsg = $"Transcribing... ({_inFlightRequests} pending)";

            byte[] wavBytes = FloatSamplesToWav(samples, SampleRate, 1);
            float peak = 0f, rms = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float a = Mathf.Abs(samples[i]);
                if (a > peak) peak = a;
                rms += samples[i] * samples[i];
            }
            rms = Mathf.Sqrt(rms / samples.Length);

            if (verboseLogs)
                Debug.Log($"[CloudSTT] Sending {audioDuration:F2}s, {wavBytes.Length} bytes, peak={peak:F3}, rms={rms:F4}");

            if (dumpWavToDisk)
            {
                string dumpDir = System.IO.Path.Combine(Application.persistentDataPath, "stt_dumps");
                System.IO.Directory.CreateDirectory(dumpDir);
                string dumpPath = System.IO.Path.Combine(dumpDir, $"seg_{DateTime.Now:yyyyMMdd_HHmmss_fff}.wav");
                System.IO.File.WriteAllBytes(dumpPath, wavBytes);
                Debug.Log($"[CloudSTT] WAV dumped: {dumpPath}");
            }

            if (peak < 0.01f)
            {
                Debug.LogWarning($"[CloudSTT] Segment nearly silent (peak={peak:F4}), skipping");
                _inFlightRequests--;
                yield break;
            }

            string url = $"{NormalizeBaseUrl(baseUrl)}/v1/audio/transcriptions";

            var form = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("model", model),
                new MultipartFormDataSection("response_format", "json"),
                new MultipartFormFileSection("file", wavBytes, "speech.wav", "audio/wav"),
            };
            if (!string.IsNullOrEmpty(language))
                form.Add(new MultipartFormDataSection("language", language));

            using var req = UnityWebRequest.Post(url, form);
            if (!string.IsNullOrEmpty(apiKey))
                req.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            req.timeout = requestTimeoutSeconds;

            float startTime = Time.realtimeSinceStartup;
            yield return req.SendWebRequest();
            float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;

            _inFlightRequests--;

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[CloudSTT] HTTP {req.responseCode}: {req.error} — {req.downloadHandler?.text}");
                _statusMsg = $"Error: {req.error}";
                yield break;
            }

            string text = ExtractJsonStringField(req.downloadHandler.text, "text")?.Trim();

            if (string.IsNullOrEmpty(text))
            {
                if (verboseLogs) Debug.Log($"[CloudSTT] Empty transcription. Raw: {req.downloadHandler.text}");
                _statusMsg = "Empty result.";
                yield break;
            }

            _utteranceCount++;
            _lastTranscription = $"[{_utteranceCount}] ({elapsedMs:F0}ms) {text}";
            _statusMsg = _isStreaming ? "Streaming..." : "Stopped.";
            if (verboseLogs) Debug.Log($"[CloudSTT] #{_utteranceCount}: {text} ({elapsedMs:F0}ms)");

            OnTranscriptionResult?.Invoke(text);
        }

        // ================================================================
        // URL normalization — chap nhan ca dang co va khong co /v1 hay /openai/v1
        // ================================================================

        private static string NormalizeBaseUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            string trimmed = url.TrimEnd('/');
            // Strip trailing /v1 or /openai/v1 because code se tu them /v1/...
            if (trimmed.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - "/openai/v1".Length);
            else if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - "/v1".Length);
            return trimmed.TrimEnd('/');
        }

        // ================================================================
        // WAV encoding (16-bit PCM)
        // ================================================================

        private static byte[] FloatSamplesToWav(float[] samples, int sampleRate, int channels)
        {
            int byteCount = samples.Length * 2; // 16-bit
            int fileSize = 36 + byteCount;

            using var ms = new MemoryStream(44 + byteCount);
            using var bw = new BinaryWriter(ms);

            // RIFF header
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(fileSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);                                   // PCM chunk size
            bw.Write((short)1);                             // format = PCM
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(sampleRate * channels * 2);            // byte rate
            bw.Write((short)(channels * 2));                // block align
            bw.Write((short)16);                            // bits per sample

            // data chunk
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(byteCount);

            for (int i = 0; i < samples.Length; i++)
            {
                float s = Mathf.Clamp(samples[i], -1f, 1f);
                short pcm = (short)(s * 32767f);
                bw.Write(pcm);
            }

            bw.Flush();
            return ms.ToArray();
        }

        // ================================================================
        // Minimal JSON field extractor (avoid JsonUtility for robust parsing)
        // ================================================================

        private static string ExtractJsonStringField(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string pattern = $"\"{fieldName}\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + pattern.Length);
            if (colon < 0) return null;
            int i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '"') return null;
            i++;
            var sb = new StringBuilder();
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
                        case 'u':
                            if (i + 5 < json.Length)
                            {
                                string hex = json.Substring(i + 2, 4);
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
                                    sb.Append((char)code);
                                i += 4;
                            }
                            break;
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

        // ================================================================
        // Test UI (optional, off by default)
        // ================================================================

        private void OnGUI()
        {
            if (!showTestUI) return;

            float x = 10, y = 250, w = 400;
            GUI.Box(new Rect(x, y, w, 180), "Cloud STT");

            y += 25;
            GUI.Label(new Rect(x + 10, y, w - 20, 20), _statusMsg);

            y += 20;
            string vadLabel = _vadState == VadState.Speaking ? "Speaking" : "Idle";
            GUI.Label(new Rect(x + 10, y, w - 20, 20),
                $"VAD: {vadLabel} ({(_vadActive ? "VOICE" : "silence")}) | Mic: {_micDevice ?? "none"}");

            y += 25;
            GUI.enabled = !_isStreaming;
            if (GUI.Button(new Rect(x + 10, y, 120, 30), "Start"))
                StartStreaming();
            GUI.enabled = _isStreaming;
            if (GUI.Button(new Rect(x + 140, y, 120, 30), "Stop"))
                StopStreaming();
            GUI.enabled = true;

            y += 40;
            GUI.Label(new Rect(x + 10, y, w - 20, 40),
                string.IsNullOrEmpty(_lastTranscription) ? "(no result yet)" : _lastTranscription);
        }
    }
}
