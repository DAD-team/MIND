using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace MIND.Speech
{
    /// <summary>
    /// VieNeu-TTS client cho Unity.
    /// Tu dong khoi dong VieNeu-TTS server (background process) khi Play.
    /// </summary>
    public class VieNeuTTS : MonoBehaviour, Core.ITTS
    {
        [Header("Server")]
        [SerializeField] private string serverUrl = "http://localhost:8001";

        [Header("Auto-start Server")]
        [Tooltip("Duong dan tuyet doi den thu muc VieNeu-TTS-tools. De trong = tu dong tim.")]
        [SerializeField] private string vieneuPath = "";
        [SerializeField] private bool autoStartServer = true;

        [Header("TTS Settings")]
        [SerializeField] private string voiceId = "";
        [Tooltip("Text mac dinh de test")]
        [SerializeField] private string testText = "Xin chào, tôi là trợ lý ảo. Tôi có thể giúp gì cho bạn?";

        // Audio
        private AudioSource _audioSource;
        private bool _isSpeaking;

        // Server
        private bool _serverReady;
        private Process _serverProcess;
        private bool _serverStarting;

        private const int TTS_SAMPLE_RATE = 24000;

        public bool IsReady => _serverReady;

        private void EnsureAudioSource()
        {
            if (_audioSource != null) return;
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.volume = 1.0f;
            _audioSource.spatialBlend = 0f;
        }

        private IEnumerator Start()
        {
            yield return CheckServer();

            if (!_serverReady && autoStartServer)
            {
                StartServerProcess();

                float timeout = 60f;
                float waited = 0f;
                while (!_serverReady && waited < timeout)
                {
                    yield return new WaitForSeconds(2f);
                    waited += 2f;
                    yield return CheckServer();
                }

                if (!_serverReady)
                {
                    Debug.LogError("[VieNeuTTS] Server failed to start within 60s. Check console for errors.");
                }
            }
        }

        private void StartServerProcess()
        {
            if (_serverStarting) return;
            _serverStarting = true;

            string path = FindVieNeuPath();
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[VieNeuTTS] Cannot find VieNeu-TTS-tools directory. Set vieneuPath in Inspector.");
                return;
            }

            Debug.Log($"[VieNeuTTS] Starting server from: {path}");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "uv",
                    Arguments = "run vieneu-stream",
                    WorkingDirectory = path,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                _serverProcess = Process.Start(psi);
                _serverProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Debug.Log($"[VieNeuTTS-Server] {e.Data}");
                };
                _serverProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Debug.Log($"[VieNeuTTS-Server] {e.Data}");
                };
                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();

                Debug.Log("[VieNeuTTS] Server process started, waiting for it to be ready...");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VieNeuTTS] Failed to start server: {e.Message}");
                Debug.LogError("[VieNeuTTS] Make sure 'uv' is installed: https://astral.sh/uv/install");
            }
        }

        private string FindVieNeuPath()
        {
            if (!string.IsNullOrEmpty(vieneuPath) && Directory.Exists(vieneuPath))
                return vieneuPath;

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (projectRoot != null)
            {
                foreach (var dir in Directory.GetDirectories(projectRoot, "VieNeu-TTS*"))
                {
                    if (File.Exists(Path.Combine(dir, "pyproject.toml")))
                        return dir;
                }
            }

            return null;
        }

        private IEnumerator CheckServer()
        {
            using var req = UnityWebRequest.Get($"{serverUrl}/models");
            req.timeout = 3;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                if (!_serverReady)
                    Debug.Log($"[VieNeuTTS] Server ready at {serverUrl}");
                _serverReady = true;
            }
            else
            {
                _serverReady = false;
            }
        }

        private static void NormalizeSamples(float[] samples, float targetPeak = 0.9f)
        {
            float maxAbs = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = Mathf.Abs(samples[i]);
                if (abs > maxAbs) maxAbs = abs;
            }

            if (maxAbs < 0.001f || maxAbs >= targetPeak) return;

            float scale = targetPeak / maxAbs;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= scale;
            }
        }

        private void OnDestroy()
        {
            if (_audioSource != null && _audioSource.clip != null)
            {
                _audioSource.Stop();
                Destroy(_audioSource.clip);
            }

            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                Debug.Log("[VieNeuTTS] Stopping server process...");
                try
                {
                    _serverProcess.Kill();
                    _serverProcess.WaitForExit(3000);
                }
                catch (Exception) { }
                _serverProcess = null;
            }
        }

        private void OnApplicationQuit()
        {
            OnDestroy();
        }

        public void Speak(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            EnsureAudioSource();
            if (_isSpeaking)
            {
                Debug.LogWarning("[VieNeuTTS] Already speaking, stopping previous...");
                Stop();
            }
            StartCoroutine(SpeakCoroutine(text));
        }

        public void Stop()
        {
            if (_audioSource != null) _audioSource.Stop();
            _isSpeaking = false;
        }

        public bool IsSpeaking => _isSpeaking;

        private IEnumerator SpeakCoroutine(string text)
        {
            _isSpeaking = true;

            string encodedText = UnityWebRequest.EscapeURL(text);
            string url = $"{serverUrl}/stream?text={encodedText}";
            if (!string.IsNullOrEmpty(voiceId))
                url += $"&voice_id={UnityWebRequest.EscapeURL(voiceId)}";

            Debug.Log($"[VieNeuTTS] Requesting: {text.Substring(0, Math.Min(text.Length, 50))}...");

            using var req = UnityWebRequest.Get(url);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 30;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[VieNeuTTS] Request failed: {req.error}");
                _isSpeaking = false;
                yield break;
            }

            byte[] wavData = req.downloadHandler.data;
            if (wavData == null || wavData.Length < 44)
            {
                Debug.LogError("[VieNeuTTS] Invalid WAV data received");
                _isSpeaking = false;
                yield break;
            }

            AudioClip clip = WavToAudioClip(wavData);
            if (clip == null)
            {
                Debug.LogError("[VieNeuTTS] Failed to parse WAV");
                _isSpeaking = false;
                yield break;
            }

            if (_audioSource.clip != null)
            {
                _audioSource.Stop();
                var oldClip = _audioSource.clip;
                _audioSource.clip = null;
                Destroy(oldClip);
            }

            Debug.Log($"[VieNeuTTS] Playing {clip.length:F1}s audio");
            _audioSource.clip = clip;
            _audioSource.Play();

            while (_audioSource.isPlaying)
                yield return null;

            _isSpeaking = false;
        }

        private AudioClip WavToAudioClip(byte[] wavData)
        {
            try
            {
                if (wavData.Length < 44) return null;

                int channels = BitConverter.ToInt16(wavData, 22);
                int sampleRate = BitConverter.ToInt32(wavData, 24);
                int bitsPerSample = BitConverter.ToInt16(wavData, 34);

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

                float[] samples = new float[sampleCount];

                if (bitsPerSample == 16)
                {
                    for (int i = 0; i < sampleCount; i++)
                    {
                        int offset = dataOffset + i * channels * 2;
                        if (offset + 1 >= wavData.Length) break;
                        short sample = BitConverter.ToInt16(wavData, offset);
                        samples[i] = sample / 32768f;
                    }
                }
                else
                {
                    Debug.LogWarning($"[VieNeuTTS] Unsupported bits per sample: {bitsPerSample}");
                    return null;
                }

                NormalizeSamples(samples);

                var clip = AudioClip.Create("VieNeuTTS", sampleCount, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }
            catch (Exception e)
            {
                Debug.LogError($"[VieNeuTTS] WAV parse error: {e}");
                return null;
            }
        }

        // ================================================================
        // TEST UI
        // ================================================================

        [Header("Test UI (optional)")]
        [SerializeField] private bool showTestUI = true;

        private string _inputText = "";

        private void OnGUI()
        {
            if (!showTestUI) return;

            float x = Screen.width - 420;
            float y = 10;
            float w = 400;

            GUI.Box(new Rect(x, y, w, 200), "VieNeu-TTS Test");

            y += 25;
            string serverStatus = _serverReady ? "Server: Connected" :
                                   _serverStarting ? "Server: Starting... (first time takes ~30s)" :
                                   "Server: Not connected";
            GUI.Label(new Rect(x + 10, y, w - 20, 20), serverStatus);

            y += 25;
            _inputText = GUI.TextField(new Rect(x + 10, y, w - 20, 22), _inputText);
            if (string.IsNullOrEmpty(_inputText))
                _inputText = testText;

            y += 30;
            bool canSpeak = _serverReady && !_isSpeaking;

            if (GUI.Button(new Rect(x + 10, y, 120, 30), canSpeak ? "Speak" : "Speaking..."))
            {
                if (canSpeak)
                {
                    Speak(_inputText);
                }
            }

            if (GUI.Button(new Rect(x + 140, y, 80, 30), "Stop"))
            {
                Stop();
            }

            if (GUI.Button(new Rect(x + 230, y, 80, 30), "Reconnect"))
            {
                StartCoroutine(CheckServer());
            }

            y += 40;
            GUI.Label(new Rect(x + 10, y, w - 20, 60),
                _isSpeaking ? "Playing audio..." : "Ready. Enter text and click Speak.");
        }
    }
}
