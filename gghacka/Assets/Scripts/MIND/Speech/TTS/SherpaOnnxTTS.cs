using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MIND.Speech
{
    /// <summary>
    /// Sherpa-ONNX Offline TTS — chay hoan toan local, khong can server.
    /// Dung model Piper Vietnamese (vits-piper-vi_VN-vais1000-medium).
    /// </summary>
    public class SherpaOnnxTTS : MonoBehaviour, Core.ITTS
    {
        [Header("Model Settings")]
        [SerializeField] private string modelDir = "vits-piper-vi_VN-vais1000-medium";
        [SerializeField] private int numThreads = 2;

        [Header("TTS Settings")]
        [SerializeField] private float speed = 1.0f;
        [SerializeField] private int speakerId = 0;

        [Header("Test")]
        [SerializeField] private string testText = "Xin chào, tôi là trợ lý ảo. Tôi có thể giúp gì cho bạn?";

        private SherpaOnnx.OfflineTtsSimple _tts;
        private AudioSource _audioSource;
        private bool _isReady;
        private bool _isSpeaking;

        // Background generate
        private volatile bool _generating;
        private float[] _pendingSamples;
        private int _pendingSampleRate;
        private volatile bool _hasResult;
        private long _pendingMs;

        private void Awake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        private IEnumerator Start()
        {
            string modelsBase = Path.Combine(Application.streamingAssetsPath, "models");
            string modelPath = Path.Combine(modelsBase, modelDir);
            string onnxFile = "";
            string tokensFile = Path.Combine(modelPath, "tokens.txt");
            string espeakDataDir = Path.Combine(modelPath, "espeak-ng-data");

            if (Directory.Exists(modelPath))
            {
                foreach (var f in Directory.GetFiles(modelPath, "*.onnx"))
                {
                    onnxFile = f;
                    break;
                }
            }

            if (!Directory.Exists(espeakDataDir))
            {
                espeakDataDir = Path.Combine(modelsBase, "espeak-ng-data");
            }

            if (string.IsNullOrEmpty(onnxFile) || !File.Exists(onnxFile))
            {
                Debug.LogError($"[SherpaOnnxTTS] Model not found in {modelPath}");
                yield break;
            }

            Debug.Log($"[SherpaOnnxTTS] Model ONNX: {onnxFile}");
            Debug.Log($"[SherpaOnnxTTS] Tokens: {tokensFile}");
            Debug.Log($"[SherpaOnnxTTS] eSpeak data: {espeakDataDir}");

            try
            {
                _tts = SherpaOnnx.OfflineTtsSimple.Create(
                    modelPath: onnxFile,
                    tokensPath: tokensFile,
                    dataDirPath: espeakDataDir,
                    numThreads: numThreads
                );
                Debug.Log($"[SherpaOnnxTTS] Model loaded. SampleRate={_tts.SampleRate}, Speakers={_tts.NumSpeakers}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SherpaOnnxTTS] Init failed: {e}");
                yield break;
            }

            _isReady = true;
            Debug.Log("[SherpaOnnxTTS] Ready!");
        }

        private void Update()
        {
            if (_hasResult)
            {
                _hasResult = false;
                _generating = false;

                if (_pendingSamples != null && _pendingSamples.Length > 0)
                {
                    if (_audioSource.clip != null)
                    {
                        _audioSource.Stop();
                        UnityEngine.Object.Destroy(_audioSource.clip);
                        _audioSource.clip = null;
                    }

                    // Check peak amplitude
                    float peak = 0f;
                    for (int i = 0; i < _pendingSamples.Length; i++)
                    {
                        float abs = Mathf.Abs(_pendingSamples[i]);
                        if (abs > peak) peak = abs;
                    }
                    Debug.Log($"[SherpaOnnxTTS] Peak amplitude: {peak:F4} (0=silence, 1=max)");

                    var clip = AudioClip.Create("TTS", _pendingSamples.Length, 1, _pendingSampleRate, false);
                    clip.SetData(_pendingSamples, 0);

                    _audioSource.clip = clip;
                    _audioSource.Play();
                    _isSpeaking = true;

                    float duration = (float)_pendingSamples.Length / _pendingSampleRate;
                    Debug.Log($"[SherpaOnnxTTS] Playing {duration:F1}s audio (generated in {_pendingMs}ms) | samples={_pendingSamples.Length}, sampleRate={_pendingSampleRate}, volume={_audioSource.volume}, mute={_audioSource.mute}, spatialBlend={_audioSource.spatialBlend}");

                    var listener = FindObjectOfType<AudioListener>();
                    if (listener != null)
                        Debug.Log($"[SherpaOnnxTTS] AudioListener on: {listener.gameObject.name} (active={listener.enabled}, go={listener.gameObject.activeInHierarchy})");
                    else
                        Debug.LogWarning("[SherpaOnnxTTS] NO AudioListener found in scene!");
                }

                _pendingSamples = null;
            }

            if (_isSpeaking && !_audioSource.isPlaying)
            {
                _isSpeaking = false;
            }
        }

        public void Speak(string text)
        {
            if (!_isReady || _tts == null)
            {
                Debug.LogWarning($"[SherpaOnnxTTS] Not ready (isReady={_isReady}, tts={((_tts != null) ? "ok" : "NULL")})");
                return;
            }
            if (_generating)
            {
                Debug.LogWarning("[SherpaOnnxTTS] Already generating");
                return;
            }
            if (string.IsNullOrEmpty(text)) return;

            Debug.Log($"[SherpaOnnxTTS] Speak called: \"{text.Substring(0, Math.Min(text.Length, 60))}\"");
            _generating = true;

            float spd = speed;
            int sid = speakerId;
            var tts = _tts;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (tts == null)
                    {
                        Debug.LogError("[SherpaOnnxTTS] tts ref is null in thread");
                        _generating = false;
                        _hasResult = true;
                        return;
                    }

                    var sw = Stopwatch.StartNew();
                    using var audio = tts.Generate(text, sid, spd);
                    sw.Stop();

                    if (audio != null)
                    {
                        _pendingSamples = audio.Samples;
                        _pendingSampleRate = audio.SampleRate;
                        _pendingMs = sw.ElapsedMilliseconds;
                    }
                    else
                    {
                        Debug.LogWarning("[SherpaOnnxTTS] Generate returned null");
                    }

                    _hasResult = true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SherpaOnnxTTS] Generate error: {e}");
                    _generating = false;
                    _hasResult = true;
                }
            });
        }

        public void Stop()
        {
            _audioSource.Stop();
            _isSpeaking = false;
        }

        public bool IsReady => _isReady;
        public bool IsSpeaking => _isSpeaking || _generating;

        private void OnDestroy()
        {
            _isReady = false;
            if (_audioSource != null && _audioSource.clip != null)
            {
                _audioSource.Stop();
                UnityEngine.Object.Destroy(_audioSource.clip);
            }
            _tts?.Dispose();
            _tts = null;
        }

        // ================================================================
        // Simple test UI
        // ================================================================

        [Header("Test UI")]
        [SerializeField] private bool showTestUI = true;
        private string _inputText = "";

        private void OnGUI()
        {
            if (!showTestUI) return;

            float x = Screen.width - 420;
            float y = Screen.height - 180;
            float w = 400;

            GUI.Box(new Rect(x, y, w, 170), "Sherpa-ONNX TTS (Offline)");

            y += 25;
            string status = !_isReady ? "Loading model..." :
                            _generating ? "Generating..." :
                            _isSpeaking ? "Playing..." : "Ready";
            GUI.Label(new Rect(x + 10, y, w - 20, 20), status);

            y += 25;
            if (string.IsNullOrEmpty(_inputText)) _inputText = testText;
            _inputText = GUI.TextField(new Rect(x + 10, y, w - 20, 22), _inputText);

            y += 30;
            GUI.enabled = _isReady && !_generating;
            if (GUI.Button(new Rect(x + 10, y, 120, 30), "Speak"))
            {
                Speak(_inputText);
            }
            GUI.enabled = true;

            if (GUI.Button(new Rect(x + 140, y, 80, 30), "Stop"))
            {
                Stop();
            }

            y += 35;
            if (_tts != null)
            {
                GUI.Label(new Rect(x + 10, y, w - 20, 20),
                    $"Model: {modelDir} | SR: {_tts.SampleRate}Hz");
            }
        }
    }
}
