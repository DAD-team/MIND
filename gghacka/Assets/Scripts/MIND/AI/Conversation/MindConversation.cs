using System;
using System.Collections;
using MIND.Core;
using MIND.Speech;
using UnityEngine;

namespace MIND.AI
{
    /// <summary>
    /// Orchestrates the voice conversation pipeline: STT → AI (LLM) → TTS.
    ///
    /// Design principles:
    ///   - STT LUÔN BẬT — không bao giờ tắt mic khi NPC đang nói.
    ///   - User nói 2s+ khi NPC đang nói → NPC ngừng để nghe.
    ///   - Mọi thứ NPC nói đều qua LLM — kể cả lời chào, phản hồi im lặng.
    ///   - Không có kịch bản cố định.
    /// </summary>
    public class MindConversation : MonoBehaviour
    {
        [Header("Components (kéo thả trong Inspector)")]
        [Tooltip("Cloud STT (uu tien, dung FPT AI Marketplace)")]
        [SerializeField] private CloudSTT cloudStt;
        [Tooltip("Local STT (fallback neu khong co cloud)")]
        [SerializeField] private SherpaSTTStreaming stt;
        [Tooltip("Cloud TTS (uu tien, dung FPT AI Marketplace)")]
        [SerializeField] private CloudTTS cloudTts;
        [Tooltip("Local TTS (sherpa-onnx)")]
        [SerializeField] private SherpaOnnxTTS localTts;
        [Tooltip("Local server TTS (VieNeu)")]
        [SerializeField] private VieNeuTTS serverTts;

        [Header("API Mode")]
        [Tooltip("TRUE = dung cloud API cho STT/LLM/TTS (FPT AI Marketplace).\n" +
                 "FALSE = dung local (Sherpa STT + local/server TTS).")]
        [SerializeField] private bool useCloudApi = true;

        [Header("AI Config (Cloud)")]
        [Tooltip("FPT AI Marketplace: https://mkp-api.fptcloud.com")]
        [SerializeField] private string baseUrl = "https://mkp-api.fptcloud.com";
        [Tooltip("API key — dung chung cho ca LLM/STT/TTS (cung la key FPT Marketplace)")]
        [SerializeField] private string apiKey = "";
        [Tooltip("LLM model. VD: Llama-3.3-Swallow-70B-Instruct-v0.4 / SaoLa-Llama3.1-planner / Llama-3.3-70B-Instruct")]
        [SerializeField] private string model = "Llama-3.3-70B-Instruct";
        [Tooltip("STT model. VD: FPT.AI-whisper-large-v3-turbo")]
        [SerializeField] private string sttModel = "FPT.AI-whisper-large-v3-turbo";
        [Tooltip("STT language (vi/en). De trong = auto-detect.")]
        [SerializeField] private string sttLanguage = "vi";
        [Tooltip("TTS model. VD: FPT.AI-VITs")]
        [SerializeField] private string ttsModel = "FPT.AI-VITs";
        [Tooltip("TTS voice ID (optional, check marketplace)")]
        [SerializeField] private string ttsVoice = "";
        [SerializeField] private float temperature = 0.8f;
        [Tooltip("De it nhat 1024 khi dung reasoning model (vd gpt-oss-120b). 512 chi du cho non-reasoning.")]
        [SerializeField] private int maxTokens = 1024;
        [SerializeField] private int maxConversationTurns = 20;

        [Header("User Info")]
        [SerializeField] private string userName = "";

        [Header("Session")]
        [Tooltip("Emotion profile (set before StartConversation)")]
        [SerializeField] private string emotionProfileJson = "";

        [Header("Interruption")]
        [Tooltip("Seconds of user voice during NPC speech before NPC stops")]
        [SerializeField] private float interruptThreshold = 2.0f;

        [Header("Debug")]
        [SerializeField] private bool showDebugUI = true;

        // State
        private ConversationState _state = ConversationState.Idle;
        public ConversationState CurrentState => _state;

        // Runtime interfaces
        private ISTT _stt;
        private ITTS _tts;

        // AI
        private IAIProvider _aiProvider;
        private ConversationHistory _history;
        private string _systemPrompt;
        private EmotionProfile _emotionProfile;

        // TTS mode
        private enum TtsMode { Cloud, Local, Server }
        private TtsMode _ttsMode;

        // Interruption tracking
        private float _userVoiceTimer;
        private bool _userSpeakingDuringTts;
        private bool _interrupted;

        // Processing guard
        private Coroutine _currentProcessing;

        // Events
        public event Action<ConversationState> OnStateChanged;
        public event Action<string> OnUserSaid;
        public event Action<AIResponse> OnAIResponded;

        private static readonly string[] FallbackResponses =
        {
            "Xin lỗi, bạn nói lại được không?",
            "Mình đang nghe đây, bạn cứ tiếp tục nhé.",
            "Bạn nói thêm đi, mình muốn hiểu hơn.",
            "Ừm, mình hiểu. Bạn cảm thấy sao về chuyện đó?"
        };
        private int _fallbackIndex;

        private string _debugInput = "Tôi cảm thấy rất áp lực gần đây.";

        private void Awake()
        {
            Application.runInBackground = true;

            if (cloudStt == null) cloudStt = GetComponent<CloudSTT>();
            if (stt == null) stt = GetComponent<SherpaSTTStreaming>();
            if (cloudTts == null) cloudTts = GetComponent<CloudTTS>();
            if (localTts == null) localTts = GetComponent<SherpaOnnxTTS>();
            if (serverTts == null) serverTts = GetComponent<VieNeuTTS>();
        }

        // ================================================================
        // Lifecycle
        // ================================================================

        /// <summary>
        /// Start a conversation session. STT starts immediately and stays on.
        /// Call RequestGreeting() after this to have NPC greet the user via LLM.
        /// </summary>
        public void StartConversation(EmotionProfile profile = null)
        {
            if (_state != ConversationState.Idle)
            {
                Debug.LogWarning("[MindConversation] Already in a conversation session");
                return;
            }

            // Propagate API config xuong cloud STT/TTS truoc khi select
            // (de CloudTTS.IsReady tra ve dung)
            if (useCloudApi)
            {
                if (cloudStt != null)
                    cloudStt.Configure(baseUrl, apiKey, sttModel, sttLanguage);
                if (cloudTts != null)
                    cloudTts.Configure(baseUrl, apiKey, ttsModel, ttsVoice);
            }

            // Resolve STT — theo useCloudApi
            _stt = SelectStt();
            if (_stt == null)
            {
                Debug.LogError("[MindConversation] No STT available! " +
                    (useCloudApi ? "Cloud mode: thieu CloudSTT component." : "Local mode: thieu SherpaSTTStreaming."));
                return;
            }

            // Resolve TTS — theo useCloudApi
            SelectTts();
            if (_tts == null)
            {
                Debug.LogError("[MindConversation] No TTS available! " +
                    (useCloudApi ? "Cloud mode: thieu CloudTTS component." : "Local mode: thieu local/server TTS."));
                return;
            }

            // Set emotion profile
            _emotionProfile = profile;
            if (_emotionProfile == null && !string.IsNullOrEmpty(emotionProfileJson))
            {
                try { _emotionProfile = JsonUtility.FromJson<EmotionProfile>(emotionProfileJson); }
                catch { Debug.LogWarning("[MindConversation] Failed to parse emotionProfileJson"); }
            }

            // Init AI
            var config = new ConversationConfig
            {
                baseUrl = baseUrl,
                apiKey = apiKey,
                model = model,
                temperature = temperature,
                maxTokens = maxTokens
            };
            _aiProvider = new OpenAICompatProvider(config);

            // Init conversation
            _history = new ConversationHistory(maxConversationTurns);
            _systemPrompt = SystemPromptBuilder.Build(_emotionProfile, userName);

            // Subscribe events
            _stt.OnTranscriptionResult += HandleTranscriptionResult;
            _stt.OnVoiceDetected += HandleVoiceDetected;

            // Start STT — stays on for the entire session
            if (!_stt.IsStreaming)
                _stt.StartStreaming();

            _interrupted = false;
            _userVoiceTimer = 0f;
            _userSpeakingDuringTts = false;

            SetState(ConversationState.Listening);
            Debug.Log("[MindConversation] Session started — STT always on");
        }

        /// <summary>
        /// Stop the conversation session.
        /// </summary>
        public void StopConversation()
        {
            if (_state == ConversationState.Idle) return;

            if (_stt != null)
            {
                _stt.OnTranscriptionResult -= HandleTranscriptionResult;
                _stt.OnVoiceDetected -= HandleVoiceDetected;
                if (_stt.IsStreaming)
                    _stt.StopStreaming();
            }

            _tts?.Stop();
            if (_currentProcessing != null)
            {
                StopCoroutine(_currentProcessing);
                _currentProcessing = null;
            }

            _history?.Clear();
            SetState(ConversationState.Idle);
            Debug.Log("[MindConversation] Session stopped");
        }

        private void OnDestroy()
        {
            if (_state != ConversationState.Idle)
                StopConversation();
        }

        // ================================================================
        // User Interruption
        // ================================================================

        private void Update()
        {
            // Track how long user has been speaking while NPC talks
            if (_state == ConversationState.Speaking && _userSpeakingDuringTts)
            {
                _userVoiceTimer += Time.deltaTime;

                if (_userVoiceTimer >= interruptThreshold && !_interrupted)
                {
                    _interrupted = true;
                    Debug.Log($"[MindConversation] User interrupted NPC after {_userVoiceTimer:F1}s");
                    InterruptNpcSpeech();
                }
            }
        }

        private void HandleVoiceDetected()
        {
            if (_state == ConversationState.Speaking)
            {
                _userSpeakingDuringTts = true;
                _userVoiceTimer = 0f;
                Debug.Log("[MindConversation] User voice detected during NPC speech");
            }
        }

        private void InterruptNpcSpeech()
        {
            _tts?.Stop();

            if (_currentProcessing != null)
            {
                StopCoroutine(_currentProcessing);
                _currentProcessing = null;
            }

            _userSpeakingDuringTts = false;
            _userVoiceTimer = 0f;

            SetState(ConversationState.Listening);
            Debug.Log("[MindConversation] NPC speech interrupted — now listening");
        }

        // ================================================================
        // STT Pipeline
        // ================================================================

        private void HandleTranscriptionResult(string text)
        {
            if (_state == ConversationState.Idle) return;
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 2) return;

            string userText = text.Trim();
            Debug.Log($"[MindConversation] User said: {userText}");

            // If NPC was speaking and user interrupted, the TTS is already stopped
            if (_state == ConversationState.Speaking)
            {
                InterruptNpcSpeech();
            }

            // If already processing a previous request, cancel it
            if (_state == ConversationState.Processing && _currentProcessing != null)
            {
                StopCoroutine(_currentProcessing);
                _currentProcessing = null;
            }

            OnUserSaid?.Invoke(userText);
            SessionEvents.RaiseUserSaid(userText);
            _history.Add("user", userText);

            SetState(ConversationState.Processing);
            _currentProcessing = StartCoroutine(ProcessAndRespond());
        }

        // ================================================================
        // LLM Processing
        // ================================================================

        private IEnumerator ProcessAndRespond()
        {
            AIResponse aiResponse = null;
            string error = null;

            Debug.Log("[MindConversation] Sending to AI...");

            yield return _aiProvider.SendMessage(
                _systemPrompt,
                _history,
                response => aiResponse = response,
                err => error = err
            );

            // If interrupted during processing, abort
            if (_state != ConversationState.Processing)
            {
                _currentProcessing = null;
                yield break;
            }

            string responseText;

            if (aiResponse != null && !string.IsNullOrEmpty(aiResponse.text))
            {
                responseText = aiResponse.text;
                _history.Add("assistant", responseText);
                OnAIResponded?.Invoke(aiResponse);
                SessionEvents.RaiseAIResponded(aiResponse);
                Debug.Log($"[MindConversation] AI: {responseText}");
            }
            else
            {
                if (error != null)
                    Debug.LogWarning($"[MindConversation] AI error: {error}");

                responseText = GetFallbackResponse();
                // KHONG Add vao _history — fallback la cau che san, khong phai AI sinh ra.
                // Neu Add se lam nhiem context: luot sau LLM thay "assistant" da noi cau no
                // khong bao gio tao → context bi loi.
                var fallback = new AIResponse { text = responseText };
                OnAIResponded?.Invoke(fallback);
                SessionEvents.RaiseAIResponded(fallback);
            }

            // Speak — STT stays on, user can interrupt
            yield return SpeakAndListen(responseText);
            _currentProcessing = null;
        }

        private IEnumerator SpeakAndListen(string text)
        {
            if (!_tts.IsReady)
            {
                Debug.LogWarning("[MindConversation] TTS not ready, skipping speech");
                SetState(ConversationState.Listening);
                yield break;
            }

            _interrupted = false;
            _userSpeakingDuringTts = false;
            _userVoiceTimer = 0f;

            SetState(ConversationState.Speaking);
            _tts.Speak(text);

            // Wait for TTS to start (with timeout)
            float waitTime = 0f;
            while (!_tts.IsSpeaking && waitTime < 15f && !_interrupted)
            {
                yield return null;
                waitTime += Time.deltaTime;
            }

            // Wait for TTS to finish (or user interrupts)
            while (_tts.IsSpeaking && !_interrupted)
                yield return null;

            if (_interrupted)
            {
                Debug.Log("[MindConversation] Speech was interrupted by user");
                // State already set to Listening by InterruptNpcSpeech
            }
            else
            {
                SetState(ConversationState.Listening);
            }
        }

        // ================================================================
        // Public API for external triggers (greeting, silence, farewell)
        // ================================================================

        /// <summary>
        /// Ask LLM to generate a greeting. Call after StartConversation().
        /// The LLM will produce a natural, non-scripted greeting based on the emotion profile.
        /// </summary>
        public void RequestGreeting()
        {
            if (_state == ConversationState.Idle) return;

            string greetingPrompt = "Đây là lần đầu gặp người này trong phiên hôm nay. "
                + "Hãy chào hỏi tự nhiên, ấm áp, phù hợp với trạng thái cảm xúc của họ. "
                + "Mỗi lần chào phải khác nhau — đừng lặp lại.";

            _history.Add("user", $"[HỆ_THỐNG: {greetingPrompt}]");

            SetState(ConversationState.Processing);
            _currentProcessing = StartCoroutine(ProcessAndRespond());
        }

        /// <summary>
        /// Called by SilenceHandler — injects silence context into LLM to generate a natural response.
        /// </summary>
        public void RequestSilenceResponse(float silenceSeconds)
        {
            if (_state != ConversationState.Listening) return;

            string context;
            if (silenceSeconds < 15f)
                context = $"[HỆ_THỐNG: Người nói chuyện đã im lặng {silenceSeconds:F0} giây. "
                    + "Phản hồi nhẹ nhàng, tự nhiên. Có thể chỉ là một câu ngắn hoặc im lặng cùng họ.]";
            else
                context = $"[HỆ_THỐNG: Người nói chuyện đã im lặng {silenceSeconds:F0} giây. "
                    + "Có thể gợi ý nhẹ nhàng một bài tập thở hoặc đơn giản là cho họ biết bạn vẫn ở đây. "
                    + "Mỗi lần phải phản hồi KHÁC NHAU.]";

            _history.Add("user", context);

            SetState(ConversationState.Processing);
            _currentProcessing = StartCoroutine(ProcessAndRespond());
        }

        /// <summary>
        /// Ask LLM to generate a farewell. Call before ending the session.
        /// </summary>
        public void RequestFarewell(Action onComplete = null)
        {
            if (_state == ConversationState.Idle) { onComplete?.Invoke(); return; }

            string farewellPrompt = "[HỆ_THỐNG: Phiên sắp kết thúc. Hãy nói lời tạm biệt ấm áp, "
                + "có thể gợi ý nhẹ một điều tích cực cho ngày mai. Tự nhiên và chân thành.]";

            _history.Add("user", farewellPrompt);
            StartCoroutine(FarewellCoroutine(onComplete));
        }

        private IEnumerator FarewellCoroutine(Action onComplete)
        {
            SetState(ConversationState.Processing);
            _currentProcessing = StartCoroutine(ProcessAndRespond());

            // Wait for processing + speaking to complete
            while (_currentProcessing != null)
                yield return null;

            // Extra wait if still speaking
            while (_state == ConversationState.Speaking)
                yield return null;

            onComplete?.Invoke();
        }

        // ================================================================
        // For testing: manually inject text
        // ================================================================

        public void InjectUserText(string text)
        {
            HandleTranscriptionResult(text);
        }

        // ================================================================
        // TTS Selection
        // ================================================================

        private ISTT SelectStt()
        {
            if (useCloudApi)
            {
                if (cloudStt != null)
                {
                    Debug.Log("[MindConversation] STT = Cloud (FPT Marketplace)");
                    return cloudStt;
                }
                Debug.LogWarning("[MindConversation] useCloudApi=TRUE nhung thieu CloudSTT, fallback local.");
            }
            if (stt != null)
            {
                Debug.Log("[MindConversation] STT = Local (Sherpa)");
                return stt;
            }
            return null;
        }

        private void SelectTts()
        {
            if (useCloudApi)
            {
                if (cloudTts != null)
                {
                    _tts = cloudTts;
                    _ttsMode = TtsMode.Cloud;
                    if (!cloudTts.IsReady)
                        Debug.LogWarning("[MindConversation] TTS = Cloud (FPT) — WARNING: chua ready (check apiKey/model)");
                    else
                        Debug.Log("[MindConversation] TTS = Cloud (FPT Marketplace)");
                    return;
                }
                Debug.LogWarning("[MindConversation] useCloudApi=TRUE nhung thieu CloudTTS, fallback local.");
            }

            // Local / server fallback
            if (localTts != null && localTts.IsReady)
            {
                _tts = localTts;
                _ttsMode = TtsMode.Local;
                Debug.Log("[MindConversation] TTS = Local (SherpaOnnx)");
            }
            else if (serverTts != null && serverTts.IsReady)
            {
                _tts = serverTts;
                _ttsMode = TtsMode.Server;
                Debug.Log("[MindConversation] TTS = Server (VieNeu)");
            }
            else if (localTts != null)
            {
                _tts = localTts;
                _ttsMode = TtsMode.Local;
                Debug.LogWarning("[MindConversation] TTS = Local (not ready, using anyway)");
            }
            else if (serverTts != null)
            {
                _tts = serverTts;
                _ttsMode = TtsMode.Server;
                Debug.LogWarning("[MindConversation] TTS = Server (not ready, using anyway)");
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void SetState(ConversationState newState)
        {
            if (_state == newState) return;
            _state = newState;
            OnStateChanged?.Invoke(_state);
            SessionEvents.RaiseStateChanged(_state);
            Debug.Log($"[MindConversation] State → {_state}");
        }

        private string GetFallbackResponse()
        {
            string response = FallbackResponses[_fallbackIndex % FallbackResponses.Length];
            _fallbackIndex++;
            return response;
        }

        // ================================================================
        // Debug UI
        // ================================================================

        private void OnGUI()
        {
            if (!showDebugUI) return;

            float x = Screen.width - 420;
            float y = 10;
            float w = 400;

            GUI.Box(new Rect(x, y, w, 270), "MIND Conversation");

            y += 25;
            GUI.color = useCloudApi ? Color.cyan : Color.gray;
            GUI.Label(new Rect(x + 10, y, w - 20, 20),
                $"Mode: {(useCloudApi ? "CLOUD (API)" : "LOCAL")} | State: {_state} | Hist: {_history?.Count ?? 0}");
            GUI.color = Color.white;

            y += 20;
            string ttsInfo = _ttsMode switch
            {
                TtsMode.Cloud => "Cloud (FPT)",
                TtsMode.Local => "Local (SherpaOnnx)",
                TtsMode.Server => "Server (VieNeu)",
                _ => "?"
            };
            string sttInfo = _stt is CloudSTT ? "Cloud" : "Local";
            GUI.Label(new Rect(x + 10, y, w - 20, 20),
                $"STT: {sttInfo} | TTS: {ttsInfo} | Model: {model}");

            y += 20;
            if (_state == ConversationState.Speaking)
            {
                GUI.color = _userSpeakingDuringTts ? Color.yellow : Color.green;
                GUI.Label(new Rect(x + 10, y, w - 20, 20),
                    _userSpeakingDuringTts
                        ? $"User speaking during TTS: {_userVoiceTimer:F1}s / {interruptThreshold}s"
                        : "NPC speaking...");
                GUI.color = Color.white;
            }

            y += 30;
            if (_state == ConversationState.Idle)
            {
                if (GUI.Button(new Rect(x + 10, y, 180, 30), "Start Conversation"))
                    StartConversation();
            }
            else
            {
                if (GUI.Button(new Rect(x + 10, y, 130, 30), "Stop"))
                    StopConversation();
                if (GUI.Button(new Rect(x + 150, y, 130, 30), "Request Greeting"))
                    RequestGreeting();
            }

            y += 40;
            GUI.Label(new Rect(x + 10, y, 60, 20), "Test:");
            _debugInput = GUI.TextField(new Rect(x + 60, y, w - 130, 22), _debugInput);

            bool canSend = _state == ConversationState.Listening && !string.IsNullOrEmpty(_debugInput);
            GUI.enabled = canSend;
            if (GUI.Button(new Rect(x + w - 60, y, 50, 22), "Send"))
                InjectUserText(_debugInput);
            GUI.enabled = true;

            y += 30;
            GUI.Label(new Rect(x + 10, y, w - 20, 20), $"API: {baseUrl}");

            y += 20;
            if (string.IsNullOrEmpty(apiKey))
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(x + 10, y, w - 20, 20), "Warning: API key not set");
                GUI.color = Color.white;
            }
        }
    }
}
