using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace MIND.Speech
{
    /// <summary>
    /// Streaming STT dung VAD + OfflineRecognizer.
    ///
    /// Thay vi dung OnlineRecognizer (decode moi frame, treo editor),
    /// dung cach tiep can giong Whisper:
    ///   1. Thu am lien tuc tu mic
    ///   2. VAD phat hien khoang lang sau tieng noi → cat thanh 1 segment
    ///   3. Gui segment do len ThreadPool → OfflineRecognizer.Decode (da chung minh hoat dong tot)
    ///   4. Hien thi ket qua tren main thread
    /// </summary>
    public class SherpaSTTStreaming : MonoBehaviour, Core.ISTT
    {
        public enum StreamingModelType
        {
            ZipformerVi30M,
            ZipformerViFull,
            MoonshineBaseVi
        }

        [Header("Model Selection")]
        [SerializeField] private StreamingModelType selectedModel = StreamingModelType.ZipformerVi30M;

        [Header("Model Paths (relative to StreamingAssets/models/)")]
        [SerializeField] private string zipformer30MDir = "zipformer-vi-30M";
        [SerializeField] private string zipformerFullDir = "zipformer-vi-full";
        [SerializeField] private string moonshineDir = "moonshine-base-vi";

        [Header("Settings")]
        [SerializeField] private int numThreads = 2;

        [Header("VAD Settings")]
        [Tooltip("Nguong energy de xac dinh co tieng noi (0.001 - 0.05)")]
        [SerializeField] private float vadThreshold = 0.008f;
        [Tooltip("Thoi gian im lang (giay) truoc khi cat segment")]
        [SerializeField] private float silenceTimeout = 1.0f;
        [Tooltip("Do dai toi thieu cua segment (giay) de xu ly")]
        [SerializeField] private float minSegmentLength = 0.3f;
        [Tooltip("Do dai toi da cua segment (giay) — tu dong cat neu noi qua lau")]
        [SerializeField] private float maxSegmentLength = 15f;
        [Tooltip("Tan so kiem tra VAD (giay)")]
        [SerializeField] private float vadCheckInterval = 0.05f;

        [Header("Test UI")]
        [SerializeField] private bool showTestUI = true;

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

        // Cached recognizer (tao 1 lan, dung lai cho moi segment)
        private SherpaOnnx.OfflineRecognizer _cachedRecognizer;
        private readonly object _recognizerLock = new object();
        private StreamingModelType _cachedModelType;

        // Offline transcription (chay tren ThreadPool)
        private volatile bool _isTranscribing;
        private string _pendingResult;
        private long _pendingElapsedMs;
        private volatile bool _hasResult;
        private int _utteranceCount;
        private string _lastTranscription = "";

        // Public API
        public event Action<string> OnTranscriptionResult;
        public event Action OnVoiceDetected;
        public bool IsStreaming => _isStreaming;

        // Paths
        private string _modelsBasePath;
        private bool _modelsReady;

        // Status for debug UI
        private string _statusMsg = "Initializing...";
        private bool _vadActive;

        private const int SampleRate = 16000;

        private IEnumerator Start()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                _modelsBasePath = Path.Combine(Application.persistentDataPath, "models");
                yield return CopyModelsFromStreamingAssets();
            }
            else
            {
                _modelsBasePath = Path.Combine(Application.streamingAssetsPath, "models");
            }

            string modelPath = GetCurrentModelPath();
            if (Directory.Exists(modelPath))
            {
                _modelsReady = true;
                _statusMsg = $"Ready. Model: {selectedModel}";
            }
            else
            {
                _statusMsg = $"Model not found: {modelPath}";
            }

            if (Microphone.devices.Length > 0)
            {
                _micDevice = Microphone.devices[0];
                Debug.Log($"[SherpaStreaming] Using mic: {_micDevice}");
            }
            else
            {
                _statusMsg = "No microphone found!";
            }
        }

        private void Update()
        {
            // Nhan ket qua tu ThreadPool
            if (_hasResult)
            {
                _hasResult = false;
                _isTranscribing = false;

                if (!string.IsNullOrEmpty(_pendingResult))
                {
                    _utteranceCount++;
                    _lastTranscription = $"[{_utteranceCount}] ({_pendingElapsedMs}ms) {_pendingResult}";
                    Debug.Log($"[SherpaStreaming] #{_utteranceCount}: {_pendingResult} ({_pendingElapsedMs}ms)");
                    OnTranscriptionResult?.Invoke(_pendingResult);
                }

                _statusMsg = _isStreaming ? $"Streaming... ({selectedModel})" : "Stopped.";
            }

            if (!_isStreaming || _micClip == null) return;

            // Doc samples moi tu mic
            float[] newSamples = ReadMicSamples();
            if (newSamples == null) return;

            // VAD check
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
                        Debug.Log("[SherpaStreaming] VAD: speech started");
                        OnVoiceDetected?.Invoke();
                    }
                    break;

                case VadState.Speaking:
                    _speechBuffer.AddRange(newSamples);
                    _speechTimer += vadCheckInterval;

                    if (!hasVoice)
                        _silenceTimer += vadCheckInterval;
                    else
                        _silenceTimer = 0f;

                    bool silenceEnd = _silenceTimer >= silenceTimeout;
                    bool tooLong = _speechTimer >= maxSegmentLength;

                    if (silenceEnd || tooLong)
                    {
                        float segmentDuration = (float)_speechBuffer.Count / SampleRate;

                        if (segmentDuration >= minSegmentLength)
                        {
                            int trimSamples = silenceEnd
                                ? Mathf.Min((int)(silenceTimeout * SampleRate), _speechBuffer.Count / 2)
                                : 0;
                            int keepCount = _speechBuffer.Count - trimSamples;
                            float[] segment = _speechBuffer.GetRange(0, keepCount).ToArray();

                            Debug.Log($"[SherpaStreaming] VAD: segment ready, {segmentDuration:F1}s, {segment.Length} samples");
                            TranscribeSegment(segment);
                        }
                        else
                        {
                            Debug.Log($"[SherpaStreaming] VAD: segment too short ({segmentDuration:F2}s), skipped");
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
        // Audio / VAD
        // ================================================================

        private float[] ReadMicSamples()
        {
            int micPos = Microphone.GetPosition(_micDevice);
            if (micPos == _lastMicPos) return null;

            int samplesToRead;
            if (micPos > _lastMicPos)
                samplesToRead = micPos - _lastMicPos;
            else
                samplesToRead = (_micClip.samples - _lastMicPos) + micPos;

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
        // Transcription
        // ================================================================

        private void TranscribeSegment(float[] samples)
        {
            if (_isTranscribing)
            {
                Debug.LogWarning("[SherpaStreaming] Still transcribing previous segment, skipping");
                return;
            }

            _isTranscribing = true;
            _statusMsg = "Transcribing...";

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    string result = TranscribeWithCache(samples);
                    sw.Stop();

                    float audioDuration = (float)samples.Length / SampleRate;
                    Debug.Log($"[SherpaStreaming] Transcribed {audioDuration:F1}s in {sw.ElapsedMilliseconds}ms");

                    _pendingResult = result?.Trim();
                    _pendingElapsedMs = sw.ElapsedMilliseconds;
                    _hasResult = true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SherpaStreaming] Transcribe error: {e}");
                    _pendingResult = null;
                    _pendingElapsedMs = 0;
                    _hasResult = true;
                }
            });
        }

        private string TranscribeWithCache(float[] samples)
        {
            lock (_recognizerLock)
            {
                EnsureRecognizerCached();

                using var stream = _cachedRecognizer.CreateStream();
                stream.AcceptWaveform(SampleRate, samples);
                _cachedRecognizer.Decode(stream);
                return stream.Result.Text;
            }
        }

        private void EnsureRecognizerCached()
        {
            if (_cachedRecognizer != null && _cachedModelType == selectedModel)
                return;

            DisposeCachedRecognizer();

            string modelDir = GetCurrentModelPath();
            var config = SherpaOnnx.OfflineRecognizerConfig.GetDefault();
            config.ModelConfig = SherpaOnnx.OfflineModelConfig.GetDefault();
            config.ModelConfig.NumThreads = numThreads;
            config.ModelConfig.Provider = "cpu";
            config.DecodingMethod = "greedy_search";
            config.FeatConfig.SampleRate = SampleRate;
            config.FeatConfig.FeatureDim = 80;
            config.ModelConfig.Tokens = Path.Combine(modelDir, "tokens.txt");

            switch (selectedModel)
            {
                case StreamingModelType.ZipformerVi30M:
                    config.ModelConfig.Transducer = SherpaOnnx.OfflineTransducerModelConfig.GetDefault();
                    config.ModelConfig.Transducer.Encoder = Path.Combine(modelDir, "encoder.int8.onnx");
                    config.ModelConfig.Transducer.Decoder = Path.Combine(modelDir, "decoder.onnx");
                    config.ModelConfig.Transducer.Joiner = Path.Combine(modelDir, "joiner.int8.onnx");
                    break;
                case StreamingModelType.ZipformerViFull:
                    config.ModelConfig.Transducer = SherpaOnnx.OfflineTransducerModelConfig.GetDefault();
                    config.ModelConfig.Transducer.Encoder = Path.Combine(modelDir, "encoder-epoch-12-avg-8.int8.onnx");
                    config.ModelConfig.Transducer.Decoder = Path.Combine(modelDir, "decoder-epoch-12-avg-8.onnx");
                    config.ModelConfig.Transducer.Joiner = Path.Combine(modelDir, "joiner-epoch-12-avg-8.int8.onnx");
                    break;
                case StreamingModelType.MoonshineBaseVi:
                    config.ModelConfig.Moonshine = SherpaOnnx.OfflineMoonshineModelConfig.GetDefault();
                    config.ModelConfig.Moonshine.Encoder = Path.Combine(modelDir, "encoder_model.ort");
                    config.ModelConfig.Moonshine.MergedDecoder = Path.Combine(modelDir, "decoder_model_merged.ort");
                    break;
            }

            var sw = Stopwatch.StartNew();
            _cachedRecognizer = new SherpaOnnx.OfflineRecognizer(config);
            _cachedModelType = selectedModel;
            sw.Stop();
            Debug.Log($"[SherpaStreaming] Recognizer cached for {selectedModel} in {sw.ElapsedMilliseconds}ms");
        }

        private void DisposeCachedRecognizer()
        {
            lock (_recognizerLock)
            {
                _cachedRecognizer?.Dispose();
                _cachedRecognizer = null;
            }
        }

        // ================================================================
        // Public API
        // ================================================================

        public void StartStreaming()
        {
            if (!_modelsReady)
            {
                Debug.LogWarning("[SherpaStreaming] Model not loaded!");
                return;
            }
            if (_micDevice == null)
            {
                Debug.LogWarning("[SherpaStreaming] No microphone!");
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

            _statusMsg = $"Streaming... ({selectedModel})";
            Debug.Log("[SherpaStreaming] Started streaming (VAD + Offline mode)");
        }

        public void StopStreaming()
        {
            _isStreaming = false;
            Microphone.End(_micDevice);

            // Xu ly segment cuoi neu dang noi do
            if (_vadState == VadState.Speaking && _speechBuffer.Count > 0)
            {
                float segmentDuration = (float)_speechBuffer.Count / SampleRate;
                if (segmentDuration >= minSegmentLength)
                {
                    float[] segment = _speechBuffer.ToArray();
                    TranscribeSegment(segment);
                }
            }
            _speechBuffer.Clear();
            _vadState = VadState.Silence;

            if (!_isTranscribing)
                _statusMsg = "Stopped.";
            Debug.Log("[SherpaStreaming] Stopped streaming");
        }

        public void SwitchModel(StreamingModelType model)
        {
            bool wasStreaming = _isStreaming;
            if (wasStreaming) StopStreaming();

            selectedModel = model;
            DisposeCachedRecognizer();

            string modelPath = GetCurrentModelPath();
            if (Directory.Exists(modelPath))
            {
                _modelsReady = true;
                _statusMsg = $"Switched to: {model}";
            }
            else
            {
                _modelsReady = false;
                _statusMsg = $"Model not found: {modelPath}";
            }

            if (wasStreaming && _modelsReady) StartStreaming();
        }

        public string GetCurrentModelPath()
        {
            string dir = selectedModel switch
            {
                StreamingModelType.ZipformerVi30M => zipformer30MDir,
                StreamingModelType.ZipformerViFull => zipformerFullDir,
                StreamingModelType.MoonshineBaseVi => moonshineDir,
                _ => ""
            };
            return Path.Combine(_modelsBasePath ?? Application.streamingAssetsPath, dir);
        }

        private void OnDestroy()
        {
            _isStreaming = false;
            if (_micClip != null) Microphone.End(_micDevice);
            DisposeCachedRecognizer();
        }

        // ================================================================
        // ANDROID: Copy models tu StreamingAssets
        // ================================================================

        private IEnumerator CopyModelsFromStreamingAssets()
        {
            string[] dirs = { zipformer30MDir, zipformerFullDir, moonshineDir };
            string[][] filesByModel = {
                new[] { "encoder.int8.onnx", "decoder.onnx", "joiner.int8.onnx", "tokens.txt", "bpe.model" },
                new[] { "encoder-epoch-12-avg-8.int8.onnx", "decoder-epoch-12-avg-8.onnx",
                        "joiner-epoch-12-avg-8.int8.onnx", "tokens.txt", "bpe.model" },
                new[] { "encoder_model.ort", "decoder_model_merged.ort", "tokens.txt" }
            };

            for (int i = 0; i < dirs.Length; i++)
            {
                string dstBase = Path.Combine(Application.persistentDataPath, "models", dirs[i]);

                if (Directory.Exists(dstBase))
                {
                    Debug.Log($"[SherpaStreaming] Already copied: {dirs[i]}");
                    continue;
                }

                Directory.CreateDirectory(dstBase);
                foreach (var file in filesByModel[i])
                {
                    yield return CopyFileFromStreamingAssets(
                        Path.Combine("models", dirs[i], file),
                        Path.Combine(dstBase, file)
                    );
                }
            }

            _statusMsg = "Models ready.";
        }

        private IEnumerator CopyFileFromStreamingAssets(string relativePath, string destPath)
        {
            string srcPath = Path.Combine(Application.streamingAssetsPath, relativePath);
            UnityWebRequest req = UnityWebRequest.Get(srcPath);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllBytes(destPath, req.downloadHandler.data);
                Debug.Log($"[SherpaStreaming] Copied: {relativePath}");
            }
            else
            {
                Debug.LogWarning($"[SherpaStreaming] Failed: {relativePath} - {req.error}");
            }
        }

        // ================================================================
        // Test UI (OnGUI)
        // ================================================================

        private void OnGUI()
        {
            if (!showTestUI) return;

            float x = 10;
            float y = 10;
            float w = 400;

            GUI.Box(new Rect(x, y, w, 230), "Sherpa-ONNX VAD+Offline STT");

            y += 25;
            GUI.Label(new Rect(x + 10, y, w - 20, 20), _statusMsg);

            y += 20;
            string vadColor = _vadActive ? "<color=green>VOICE</color>" : "<color=red>SILENCE</color>";
            string vadLabel = _vadState == VadState.Speaking ? "Speaking" : "Idle";
            GUI.Label(new Rect(x + 10, y, w - 20, 20),
                $"VAD: {vadLabel} | Mic: {(_micDevice ?? "none")}");

            // Model selection
            y += 25;
            GUI.Label(new Rect(x + 10, y, 60, 20), "Model:");
            bool m1 = selectedModel == StreamingModelType.ZipformerVi30M;
            bool m2 = selectedModel == StreamingModelType.ZipformerViFull;
            bool m3 = selectedModel == StreamingModelType.MoonshineBaseVi;

            if (GUI.Toggle(new Rect(x + 70, y, 90, 20), m1, "Zip30M") && !m1)
                SwitchModel(StreamingModelType.ZipformerVi30M);
            if (GUI.Toggle(new Rect(x + 165, y, 90, 20), m2, "ZipFull") && !m2)
                SwitchModel(StreamingModelType.ZipformerViFull);
            if (GUI.Toggle(new Rect(x + 260, y, 110, 20), m3, "Moonshine") && !m3)
                SwitchModel(StreamingModelType.MoonshineBaseVi);

            // Start / Stop
            y += 30;
            GUI.enabled = !_isStreaming && _modelsReady;
            if (GUI.Button(new Rect(x + 10, y, 120, 30), "Start"))
                StartStreaming();
            GUI.enabled = _isStreaming;
            if (GUI.Button(new Rect(x + 140, y, 120, 30), "Stop"))
                StopStreaming();
            GUI.enabled = true;

            // Last transcription
            y += 40;
            GUI.Label(new Rect(x + 10, y, 60, 20), "Result:");
            y += 20;
            GUI.Label(new Rect(x + 10, y, w - 20, 40),
                string.IsNullOrEmpty(_lastTranscription) ? "(chua co)" : _lastTranscription);
        }
    }
}
