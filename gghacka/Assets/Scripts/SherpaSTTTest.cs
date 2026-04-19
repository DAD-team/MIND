using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Networking;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// Test script cho 3 model STT voi sherpa-onnx:
/// 1. Zipformer-vi-30M  (transducer, 25 MB, nhe nhat)
/// 2. Zipformer-vi-full (transducer, 57 MB, chinh xac hon)
/// 3. Moonshine-base-vi (moonshine v2, 135 MB, nhanh + chinh xac)
///
/// Ho tro 3 che do: Record (offline), File (offline), Stream (real-time online).
/// Stream chi ho tro Zipformer (transducer), khong ho tro Moonshine.
///
/// Gan script nay vao 1 empty GameObject, chon model trong Inspector, nhan Play.
/// </summary>
public class SherpaSTTTest : MonoBehaviour
{
    public enum ModelType
    {
        ZipformerVi30M,
        ZipformerViFull,
        MoonshineBaseVi
    }

    [Header("Model Selection")]
    [SerializeField] private ModelType selectedModel = ModelType.ZipformerVi30M;

    [Header("Model Paths (relative to StreamingAssets/models/)")]
    [SerializeField] private string zipformer30MDir = "zipformer-vi-30M";
    [SerializeField] private string zipformerFullDir = "zipformer-vi-full";
    [SerializeField] private string moonshineDir = "moonshine-base-vi";

    [Header("Test Audio")]
    [Tooltip("Keo tha AudioClip vao day de test transcribe tu file")]
    [SerializeField] private AudioClip testAudioClip;

    [Header("Settings")]
    [SerializeField] private int numThreads = 4;

    [Header("Streaming Endpoint Detection")]
    [SerializeField] private bool enableEndpoint = true;
    [SerializeField] private float rule1MinTrailingSilence = 2.4f;
    [SerializeField] private float rule2MinTrailingSilence = 1.2f;
    [SerializeField] private float rule3MinUtteranceLength = 20f;

    // UI elements
    private Canvas _canvas;
    private Text _statusText;
    private Text _resultText;
    private Text _timeText;
    private Text _partialText;
    private Text _streamLogText;
    private Button _recordBtn;
    private Button _fileBtn;
    private Button _streamBtn;
    private Button _modelBtn1;
    private Button _modelBtn2;
    private Button _modelBtn3;
    private ScrollRect _scrollRect;

    // Audio (record mode)
    private AudioClip _micClip;
    private bool _isRecording;
    private string _micDevice;

    // Streaming mode
    private SherpaOnnx.OnlineRecognizer _onlineRecognizer;
    private SherpaOnnx.OnlineStream _onlineStream;
    private bool _isStreaming;
    private int _lastMicPos;
    private string _lastPartialText = "";
    private string _streamResultLog = "";
    private int _utteranceCount;

    // Gioi han decode moi frame de tranh treo editor
    private const int MaxDecodePerFrame = 3;

    // Paths resolved at runtime
    private string _modelsBasePath;
    private bool _modelsReady;

    private const int SampleRate = 16000;

    private void Awake()
    {
        BuildUI();

        _recordBtn.onClick.AddListener(OnRecordClicked);
        _fileBtn.onClick.AddListener(OnFileClicked);
        _streamBtn.onClick.AddListener(OnStreamClicked);
        _modelBtn1.onClick.AddListener(() => SwitchModel(ModelType.ZipformerVi30M));
        _modelBtn2.onClick.AddListener(() => SwitchModel(ModelType.ZipformerViFull));
        _modelBtn3.onClick.AddListener(() => SwitchModel(ModelType.MoonshineBaseVi));

        UpdateModelButtons();
    }

    private IEnumerator Start()
    {
        SetStatus("Preparing models...");

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
            SetStatus($"Ready! Model: {selectedModel}");
        }
        else
        {
            SetStatus($"Model not found: {modelPath}\nChay Tools/export_models.py truoc.");
        }

        if (Microphone.devices.Length > 0)
        {
            _micDevice = Microphone.devices[0];
            Debug.Log($"[SherpaSTT] Using mic: {_micDevice}");
        }
        else
        {
            SetStatus("No microphone found!");
        }
    }

    private void Update()
    {
        if (!_isStreaming || _micClip == null) return;

        int micPos = Microphone.GetPosition(_micDevice);
        if (micPos == _lastMicPos) return;

        int samplesToRead;
        if (micPos > _lastMicPos)
        {
            samplesToRead = micPos - _lastMicPos;
        }
        else
        {
            samplesToRead = (_micClip.samples - _lastMicPos) + micPos;
        }

        if (samplesToRead <= 0) return;

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

        if (_onlineStream == null || _onlineRecognizer == null) return;

        _onlineStream.AcceptWaveform(SampleRate, samples);

        // Gioi han so lan decode moi frame — tranh vong lap vo han treo editor
        int decoded = 0;
        while (_onlineRecognizer.IsReady(_onlineStream) && decoded < MaxDecodePerFrame)
        {
            _onlineRecognizer.Decode(_onlineStream);
            decoded++;
        }

        var result = _onlineRecognizer.GetResult(_onlineStream);
        string text = result.Text?.Trim() ?? "";

        if (!string.IsNullOrEmpty(text) && text != _lastPartialText)
        {
            _lastPartialText = text;
            _partialText.text = text;
            _partialText.color = Color.yellow;
        }

        if (enableEndpoint && _onlineRecognizer.IsEndpoint(_onlineStream))
        {
            if (!string.IsNullOrEmpty(text))
            {
                _utteranceCount++;
                _streamResultLog += $"[{_utteranceCount}] {text}\n";
                _streamLogText.text = _streamResultLog;
                Debug.Log($"[SherpaSTT] Stream Final #{_utteranceCount}: {text}");

                if (_scrollRect != null)
                    _scrollRect.verticalNormalizedPosition = 0f;
            }

            _onlineRecognizer.Reset(_onlineStream);
            _lastPartialText = "";
            _partialText.text = "";
        }
    }

    private void OnDestroy()
    {
        if (_isStreaming) StopStreaming();
        DisposeOnlineRecognizer();
    }

    private void SwitchModel(ModelType model)
    {
        bool wasStreaming = _isStreaming;
        if (wasStreaming) StopStreaming();

        selectedModel = model;
        UpdateModelButtons();

        string modelPath = GetCurrentModelPath();
        if (Directory.Exists(modelPath))
        {
            _modelsReady = true;
            SetStatus($"Switched to: {model}");
        }
        else
        {
            _modelsReady = false;
            SetStatus($"Model not found: {modelPath}");
        }

        // Update stream button availability
        _streamBtn.interactable = (model != ModelType.MoonshineBaseVi);

        if (wasStreaming && _modelsReady && model != ModelType.MoonshineBaseVi)
            StartStreaming();
    }

    // ================================================================
    // RECORD MODE (offline)
    // ================================================================

    private void OnRecordClicked()
    {
        if (!_modelsReady)
        {
            SetStatus("Model not loaded!");
            return;
        }

        if (_isStreaming) StopStreaming();

        if (_isRecording)
        {
            StopRecordAndTranscribe();
        }
        else
        {
            StartRecord();
        }
    }

    private void StartRecord()
    {
        _resultText.text = "";
        _timeText.text = "";
        _micClip = Microphone.Start(_micDevice, false, 30, SampleRate);
        _isRecording = true;
        SetButtonText(_recordBtn, "Stop");
        SetStatus("Recording... press Stop when done.");
    }

    private void StopRecordAndTranscribe()
    {
        int micPos = Microphone.GetPosition(_micDevice);
        Microphone.End(_micDevice);
        _isRecording = false;
        SetButtonText(_recordBtn, "Record");

        if (micPos == 0)
        {
            SetStatus("No audio recorded.");
            return;
        }

        float[] samples = new float[micPos];
        _micClip.GetData(samples, 0);

        float audioDuration = (float)micPos / (float)SampleRate;
        SetStatus($"Transcribing {audioDuration:F1}s audio with {selectedModel}...");

        StartCoroutine(TranscribeAsync(samples, audioDuration));
    }

    // ================================================================
    // FILE MODE (offline)
    // ================================================================

    private void OnFileClicked()
    {
        if (!_modelsReady)
        {
            SetStatus("Model not loaded!");
            return;
        }

        if (_isStreaming) StopStreaming();

        if (testAudioClip == null)
        {
            SetStatus("No audio clip! Drag one into Inspector.");
            return;
        }

        _resultText.text = "";
        _timeText.text = "";

        float[] rawSamples = new float[testAudioClip.samples * testAudioClip.channels];
        testAudioClip.GetData(rawSamples, 0);

        float[] mono;
        if (testAudioClip.channels > 1)
        {
            mono = new float[testAudioClip.samples];
            for (int i = 0; i < testAudioClip.samples; i++)
            {
                float sum = 0;
                for (int ch = 0; ch < testAudioClip.channels; ch++)
                    sum += rawSamples[i * testAudioClip.channels + ch];
                mono[i] = sum / testAudioClip.channels;
            }
        }
        else
        {
            mono = rawSamples;
        }

        float[] samples;
        if (testAudioClip.frequency != SampleRate)
        {
            int newLen = (int)(mono.Length * (double)SampleRate / testAudioClip.frequency);
            samples = new float[newLen];
            for (int i = 0; i < newLen; i++)
            {
                float srcIdx = i * (float)testAudioClip.frequency / SampleRate;
                int idx0 = Mathf.FloorToInt(srcIdx);
                int idx1 = Mathf.Min(idx0 + 1, mono.Length - 1);
                float frac = srcIdx - idx0;
                samples[i] = mono[idx0] * (1f - frac) + mono[idx1] * frac;
            }
        }
        else
        {
            samples = mono;
        }

        float audioDuration = (float)samples.Length / SampleRate;
        SetStatus($"Transcribing {testAudioClip.name} ({audioDuration:F1}s) with {selectedModel}...");

        StartCoroutine(TranscribeAsync(samples, audioDuration));
    }

    private IEnumerator TranscribeAsync(float[] samples, float audioDuration)
    {
        string result = null;
        long elapsedMs = 0;
        bool done = false;

        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                result = Transcribe(samples);
                sw.Stop();
                elapsedMs = sw.ElapsedMilliseconds;
            }
            catch (Exception e)
            {
                result = $"ERROR: {e.Message}";
            }
            done = true;
        });

        while (!done)
            yield return null;

        _resultText.text = result ?? "(empty)";
        float rtf = audioDuration > 0 ? elapsedMs / 1000f / audioDuration : 0;
        _timeText.text = $"Time: {elapsedMs}ms | Audio: {audioDuration:F1}s | RTF: {rtf:F2}x";
        SetStatus($"Done! ({selectedModel})");

        Debug.Log($"[SherpaSTT] Model={selectedModel} Time={elapsedMs}ms Audio={audioDuration:F1}s Result={result}");
    }

    private string Transcribe(float[] samples)
    {
        string modelPath = GetCurrentModelPath();

        switch (selectedModel)
        {
            case ModelType.ZipformerVi30M:
            case ModelType.ZipformerViFull:
                return TranscribeZipformer(modelPath, samples);
            case ModelType.MoonshineBaseVi:
                return TranscribeMoonshine(modelPath, samples);
            default:
                return "Unknown model";
        }
    }

    private string TranscribeZipformer(string modelDir, float[] samples)
    {
        string encoderName, decoderName, joinerName;
        if (selectedModel == ModelType.ZipformerVi30M)
        {
            encoderName = "encoder.int8.onnx";
            decoderName = "decoder.onnx";
            joinerName = "joiner.int8.onnx";
        }
        else
        {
            encoderName = "encoder-epoch-12-avg-8.int8.onnx";
            decoderName = "decoder-epoch-12-avg-8.onnx";
            joinerName = "joiner-epoch-12-avg-8.int8.onnx";
        }

        var config = SherpaOnnx.OfflineRecognizerConfig.GetDefault();
        config.ModelConfig = SherpaOnnx.OfflineModelConfig.GetDefault();
        config.ModelConfig.Transducer = SherpaOnnx.OfflineTransducerModelConfig.GetDefault();
        config.ModelConfig.Transducer.Encoder = Path.Combine(modelDir, encoderName);
        config.ModelConfig.Transducer.Decoder = Path.Combine(modelDir, decoderName);
        config.ModelConfig.Transducer.Joiner = Path.Combine(modelDir, joinerName);
        config.ModelConfig.Tokens = Path.Combine(modelDir, "tokens.txt");
        config.ModelConfig.NumThreads = numThreads;
        config.ModelConfig.Provider = "cpu";
        config.DecodingMethod = "greedy_search";
        config.FeatConfig.SampleRate = SampleRate;
        config.FeatConfig.FeatureDim = 80;

        using var recognizer = new SherpaOnnx.OfflineRecognizer(config);
        using var stream = recognizer.CreateStream();
        stream.AcceptWaveform(SampleRate, samples);
        recognizer.Decode(stream);
        return stream.Result.Text;
    }

    private string TranscribeMoonshine(string modelDir, float[] samples)
    {
        var config = SherpaOnnx.OfflineRecognizerConfig.GetDefault();
        config.ModelConfig = SherpaOnnx.OfflineModelConfig.GetDefault();
        config.ModelConfig.Moonshine = SherpaOnnx.OfflineMoonshineModelConfig.GetDefault();
        config.ModelConfig.Moonshine.Encoder = Path.Combine(modelDir, "encoder_model.ort");
        config.ModelConfig.Moonshine.MergedDecoder = Path.Combine(modelDir, "decoder_model_merged.ort");
        config.ModelConfig.Tokens = Path.Combine(modelDir, "tokens.txt");
        config.ModelConfig.NumThreads = numThreads;
        config.ModelConfig.Provider = "cpu";
        config.DecodingMethod = "greedy_search";
        config.FeatConfig.SampleRate = SampleRate;
        config.FeatConfig.FeatureDim = 80;

        using var recognizer = new SherpaOnnx.OfflineRecognizer(config);
        using var stream = recognizer.CreateStream();
        stream.AcceptWaveform(SampleRate, samples);
        recognizer.Decode(stream);
        return stream.Result.Text;
    }

    // ================================================================
    // STREAM MODE (online / real-time)
    // ================================================================

    private void OnStreamClicked()
    {
        if (!_modelsReady)
        {
            SetStatus("Model not loaded!");
            return;
        }

        if (selectedModel == ModelType.MoonshineBaseVi)
        {
            SetStatus("Streaming khong ho tro Moonshine! Chon Zipformer.");
            return;
        }

        if (_isRecording)
        {
            Microphone.End(_micDevice);
            _isRecording = false;
            SetButtonText(_recordBtn, "Record");
        }

        if (_isStreaming)
        {
            StopStreaming();
        }
        else
        {
            StartStreaming();
        }
    }

    private void StartStreaming()
    {
        SetStatus($"Loading streaming model {selectedModel}...");

        try
        {
            InitOnlineRecognizer();
        }
        catch (Exception e)
        {
            SetStatus($"Stream init failed: {e.Message}");
            Debug.LogError($"[SherpaSTT] Stream init error: {e}");
            return;
        }

        _micClip = Microphone.Start(_micDevice, true, 30, SampleRate);
        _lastMicPos = 0;
        _isStreaming = true;
        _lastPartialText = "";
        _utteranceCount = 0;
        _streamResultLog = "";
        _streamLogText.text = "";
        _partialText.text = "";
        _resultText.text = "";
        _timeText.text = "";

        SetButtonText(_streamBtn, "Stop Stream");
        _recordBtn.interactable = false;
        _fileBtn.interactable = false;
        SetStatus($"Streaming... ({selectedModel}) Noi di!");
        Debug.Log("[SherpaSTT] Started streaming");
    }

    private void StopStreaming()
    {
        _isStreaming = false;
        Microphone.End(_micDevice);

        if (_onlineStream != null && _onlineRecognizer != null)
        {
            _onlineStream.InputFinished();

            // Xu ly not samples con lai — gioi han de tranh treo
            int maxFinalDecode = 100;
            int decoded = 0;
            while (_onlineRecognizer.IsReady(_onlineStream) && decoded < maxFinalDecode)
            {
                _onlineRecognizer.Decode(_onlineStream);
                decoded++;
            }

            var result = _onlineRecognizer.GetResult(_onlineStream);
            string text = result.Text?.Trim() ?? "";

            if (!string.IsNullOrEmpty(text))
            {
                _utteranceCount++;
                _streamResultLog += $"[{_utteranceCount}] {text}\n";
                _streamLogText.text = _streamResultLog;
                Debug.Log($"[SherpaSTT] Stream Final #{_utteranceCount}: {text}");
            }
        }

        DisposeOnlineRecognizer();

        SetButtonText(_streamBtn, "Stream");
        _recordBtn.interactable = true;
        _fileBtn.interactable = true;
        SetStatus("Stream stopped.");
        Debug.Log("[SherpaSTT] Stopped streaming");
    }

    private void InitOnlineRecognizer()
    {
        DisposeOnlineRecognizer();

        string modelDir = GetCurrentModelPath();
        string encoderName, decoderName, joinerName;

        if (selectedModel == ModelType.ZipformerVi30M)
        {
            encoderName = "encoder.int8.onnx";
            decoderName = "decoder.onnx";
            joinerName = "joiner.int8.onnx";
        }
        else
        {
            encoderName = "encoder-epoch-12-avg-8.int8.onnx";
            decoderName = "decoder-epoch-12-avg-8.onnx";
            joinerName = "joiner-epoch-12-avg-8.int8.onnx";
        }

        var config = SherpaOnnx.OnlineRecognizerConfig.GetDefault();
        config.FeatConfig.SampleRate = SampleRate;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Transducer.Encoder = Path.Combine(modelDir, encoderName);
        config.ModelConfig.Transducer.Decoder = Path.Combine(modelDir, decoderName);
        config.ModelConfig.Transducer.Joiner = Path.Combine(modelDir, joinerName);
        config.ModelConfig.Tokens = Path.Combine(modelDir, "tokens.txt");
        config.ModelConfig.NumThreads = numThreads;
        config.ModelConfig.Provider = "cpu";
        config.DecodingMethod = "greedy_search";
        config.EnableEndpoint = enableEndpoint ? 1 : 0;
        config.Rule1MinTrailingSilence = rule1MinTrailingSilence;
        config.Rule2MinTrailingSilence = rule2MinTrailingSilence;
        config.Rule3MinUtteranceLength = rule3MinUtteranceLength;

        _onlineRecognizer = new SherpaOnnx.OnlineRecognizer(config);
        _onlineStream = _onlineRecognizer.CreateStream();

        Debug.Log($"[SherpaSTT] Online recognizer initialized: {selectedModel}");
    }

    private void DisposeOnlineRecognizer()
    {
        _onlineStream?.Dispose();
        _onlineStream = null;

        _onlineRecognizer?.Dispose();
        _onlineRecognizer = null;
    }

    // ================================================================
    // HELPERS
    // ================================================================

    private string GetCurrentModelPath()
    {
        string dir = selectedModel switch
        {
            ModelType.ZipformerVi30M => zipformer30MDir,
            ModelType.ZipformerViFull => zipformerFullDir,
            ModelType.MoonshineBaseVi => moonshineDir,
            _ => ""
        };
        return Path.Combine(_modelsBasePath ?? Application.streamingAssetsPath, dir);
    }

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
                Debug.Log($"[SherpaSTT] Already copied: {dirs[i]}");
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

        SetStatus("Models ready.");
    }

    private IEnumerator CopyFileFromStreamingAssets(string relativePath, string destPath)
    {
        string srcPath = Path.Combine(Application.streamingAssetsPath, relativePath);
        UnityWebRequest req = UnityWebRequest.Get(srcPath);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            File.WriteAllBytes(destPath, req.downloadHandler.data);
            Debug.Log($"[SherpaSTT] Copied: {relativePath}");
        }
        else
        {
            Debug.LogWarning($"[SherpaSTT] Failed: {relativePath} - {req.error}");
        }
    }

    // ================================================================
    // UI (gap doi kich thuoc: 680x640)
    // ================================================================

    private void BuildUI()
    {
        var canvasGo = new GameObject("SherpaTestCanvas");
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var eventGo = new GameObject("EventSystem");
            eventGo.AddComponent<EventSystem>();
            eventGo.AddComponent<InputSystemUIInputModule>();
        }

        var panel = CreatePanel(canvasGo.transform);
        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        CreateText(panel.transform, "Sherpa-ONNX STT Test", 24, FontStyle.Bold, 34);

        _statusText = CreateText(panel.transform, "Initializing...", 16, FontStyle.Italic, 26);
        _statusText.color = Color.yellow;

        // Model buttons
        var modelRow = CreateRect(panel.transform, "ModelButtons", 0, 48);
        var modelLayout = modelRow.AddComponent<HorizontalLayoutGroup>();
        modelLayout.spacing = 8;
        modelLayout.childControlWidth = true;
        modelLayout.childControlHeight = true;

        _modelBtn1 = CreateButton(modelRow.transform, "Zip30M", new Color(0.2f, 0.6f, 0.3f), 18);
        _modelBtn2 = CreateButton(modelRow.transform, "ZipFull", new Color(0.4f, 0.5f, 0.2f), 18);
        _modelBtn3 = CreateButton(modelRow.transform, "Moonshine", new Color(0.6f, 0.4f, 0.1f), 18);

        // Action buttons (Record, File, Stream)
        var actionRow = CreateRect(panel.transform, "ActionButtons", 0, 52);
        var actionLayout = actionRow.AddComponent<HorizontalLayoutGroup>();
        actionLayout.spacing = 8;
        actionLayout.childControlWidth = true;
        actionLayout.childControlHeight = true;

        _recordBtn = CreateButton(actionRow.transform, "Record", new Color(0.8f, 0.2f, 0.2f), 18);
        _fileBtn = CreateButton(actionRow.transform, "File", new Color(0.5f, 0.4f, 0.1f), 18);
        _streamBtn = CreateButton(actionRow.transform, "Stream", new Color(0.2f, 0.4f, 0.9f), 18);

        // Time info
        _timeText = CreateText(panel.transform, "", 14, FontStyle.Normal, 24);
        _timeText.color = new Color(0.5f, 1f, 0.5f);

        // Offline result
        CreateText(panel.transform, "Result:", 16, FontStyle.Bold, 22);

        _resultText = CreateText(panel.transform, "", 18, FontStyle.Normal, 80);
        _resultText.alignment = TextAnchor.UpperLeft;
        _resultText.color = Color.white;

        // Streaming partial
        CreateText(panel.transform, "Streaming (dang noi):", 16, FontStyle.Bold, 22);

        _partialText = CreateText(panel.transform, "", 18, FontStyle.Italic, 40);
        _partialText.alignment = TextAnchor.UpperLeft;
        _partialText.color = Color.yellow;

        // Streaming final results (scrollable)
        CreateText(panel.transform, "Streaming (hoan thanh):", 16, FontStyle.Bold, 22);

        var scrollGo = CreateRect(panel.transform, "Scroll", 0, 160);
        _scrollRect = scrollGo.AddComponent<ScrollRect>();
        var scrollImg = scrollGo.AddComponent<Image>();
        scrollImg.color = new Color(0.05f, 0.05f, 0.08f, 0.9f);

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(scrollGo.transform, false);
        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);

        var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _streamLogText = contentGo.AddComponent<Text>();
        _streamLogText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _streamLogText.fontSize = 16;
        _streamLogText.color = Color.white;
        _streamLogText.alignment = TextAnchor.UpperLeft;
        _streamLogText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _streamLogText.verticalOverflow = VerticalWrapMode.Overflow;

        _scrollRect.content = contentRect;
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;

        var mask = scrollGo.AddComponent<Mask>();
        mask.showMaskGraphic = true;
    }

    private void UpdateModelButtons()
    {
        SetBtnHighlight(_modelBtn1, selectedModel == ModelType.ZipformerVi30M);
        SetBtnHighlight(_modelBtn2, selectedModel == ModelType.ZipformerViFull);
        SetBtnHighlight(_modelBtn3, selectedModel == ModelType.MoonshineBaseVi);

        if (_streamBtn != null)
            _streamBtn.interactable = (selectedModel != ModelType.MoonshineBaseVi);
    }

    private void SetBtnHighlight(Button btn, bool active)
    {
        var img = btn.GetComponent<Image>();
        if (img == null) return;
        Color baseColor = img.color;
        img.color = active ? baseColor * 1.5f : baseColor * 0.6f;
    }

    private GameObject CreateRect(Transform parent, string name, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        if (width > 0) le.preferredWidth = width;
        if (height > 0) le.preferredHeight = height;
        return go;
    }

    private GameObject CreatePanel(Transform parent)
    {
        var go = new GameObject("Panel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(10, -10);
        rect.sizeDelta = new Vector2(680, 640);
        return go;
    }

    private Text CreateText(Transform parent, string content, int size, FontStyle style, float height)
    {
        var go = CreateRect(parent, "Text", 0, height);
        var txt = go.AddComponent<Text>();
        txt.text = content;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = size;
        txt.fontStyle = style;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return txt;
    }

    private Button CreateButton(Transform parent, string label, Color color, int fontSize = 12)
    {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>();

        var img = go.AddComponent<Image>();
        img.color = color;

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = color * 1.2f;
        colors.pressedColor = color * 0.7f;
        btn.colors = colors;

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var txtRect = txtGo.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        var txt = txtGo.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;

        return btn;
    }

    private void SetButtonText(Button btn, string text)
    {
        btn.GetComponentInChildren<Text>().text = text;
    }

    private void SetStatus(string msg)
    {
        if (_statusText != null) _statusText.text = msg;
        Debug.Log($"[SherpaSTT] {msg}");
    }
}
