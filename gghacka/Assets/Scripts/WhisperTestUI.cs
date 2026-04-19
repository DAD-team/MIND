using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Whisper;
using Whisper.Utils;

/// <summary>
/// Tự động tạo toàn bộ UI để test Whisper.
/// Chỉ cần gắn script này vào 1 empty GameObject, nhấn Play là chạy.
/// Model path mặc định: StreamingAssets/ggml-base.bin
/// </summary>
public class WhisperTestUI : MonoBehaviour
{
    [Header("Whisper Settings")]
    [SerializeField] private string modelPath = "ggml-large-v3-turbo-q8_0.bin";
    [SerializeField] private string language = "vi";
    [SerializeField] private bool useVad = true;
    [SerializeField] private bool useGpu = true;
    [SerializeField] private bool flashAttention = true;
    [SerializeField] private bool noContext = true;

    [Header("Performance")]
    [Tooltip("Số thread CPU (0 = tự động, khuyến nghị 4-8)")]
    [SerializeField] private int threadCount = 4;
    [Tooltip("Giới hạn audio context giúp xử lý nhanh hơn nhưng giảm chất lượng (0 = mặc định)")]
    [SerializeField] private int audioCtx = 0;

    [Header("Test Audio")]
    [Tooltip("Kéo thả AudioClip vào đây để test transcribe từ file")]
    [SerializeField] private AudioClip testAudioClip;

    private WhisperManager _whisper;
    private MicrophoneRecord _mic;

    private Canvas _canvas;
    private Text _statusText;
    private Text _resultText;
    private Button _recordBtn;
    private Button _streamBtn;
    private Button _fileBtn;
    private Image _progressFill;

    private WhisperStream _stream;
    private bool _isStreaming;

    private void Awake()
    {
        // --- Setup Whisper on a child GameObject ---
        // Create disabled child so we can configure fields before Awake runs
        var whisperGo = new GameObject("WhisperRuntime");
        whisperGo.SetActive(false);
        whisperGo.transform.SetParent(transform);

        _whisper = whisperGo.AddComponent<WhisperManager>();
        _mic = whisperGo.AddComponent<MicrophoneRecord>();

        // Set private serialized fields via reflection before enabling
        var initField = typeof(WhisperManager).GetField("initOnAwake", BindingFlags.NonPublic | BindingFlags.Instance);
        var pathField = typeof(WhisperManager).GetField("modelPath", BindingFlags.NonPublic | BindingFlags.Instance);
        initField.SetValue(_whisper, false);
        pathField.SetValue(_whisper, modelPath);
        _whisper.language = language;
        _whisper.noContext = noContext;

        var gpuField = typeof(WhisperManager).GetField("useGpu", BindingFlags.NonPublic | BindingFlags.Instance);
        gpuField.SetValue(_whisper, useGpu);
        var flashField = typeof(WhisperManager).GetField("flashAttention", BindingFlags.NonPublic | BindingFlags.Instance);
        flashField.SetValue(_whisper, flashAttention);
        _whisper.audioCtx = audioCtx;
        _mic.useVad = useVad;

        // Now enable - Awake runs but won't auto-load model
        whisperGo.SetActive(true);

        // --- Build UI ---
        BuildUI();

        // --- Wire events ---
        _recordBtn.onClick.AddListener(OnRecordClicked);
        _streamBtn.onClick.AddListener(OnStreamClicked);
        _fileBtn.onClick.AddListener(OnFileClicked);

        _whisper.OnNewSegment += seg =>
            Debug.Log($"[Whisper] {seg.TimestampToString()} {seg.Text}");

        _whisper.OnProgress += progress =>
        {
            if (_progressFill != null)
                _progressFill.fillAmount = progress / 100f;
        };

        _mic.OnRecordStop += OnRecordStop;

        SetStatus("Loading model...");
    }

    private async void Start()
    {
        await _whisper.InitModel();

        if (_whisper.IsLoaded)
        {
            // Set thread count sau khi model đã init (vì _params được tạo trong InitModel)
            if (threadCount > 0)
            {
                var paramsField = typeof(WhisperManager).GetField("_params", BindingFlags.NonPublic | BindingFlags.Instance);
                var whisperParams = (Whisper.WhisperParams)paramsField.GetValue(_whisper);
                whisperParams.ThreadsCount = threadCount;
            }
            SetStatus("Ready. Press [Record] or [Stream].");
        }
        else
            SetStatus("Failed to load model!");
    }

    // ===================== RECORD MODE =====================
    private void OnRecordClicked()
    {
        if (_mic.IsRecording)
        {
            _mic.StopRecord();
            SetButtonText(_recordBtn, "Record");
            SetStatus("Processing...");
        }
        else
        {
            _resultText.text = "";
            _mic.StartRecord();
            SetButtonText(_recordBtn, "Stop");
            SetStatus("Recording... press Stop when done.");
        }
    }

    private async void OnRecordStop(AudioChunk audio)
    {
        if (_isStreaming) return;

        SetStatus("Transcribing...");
        var result = await _whisper.GetTextAsync(audio.Data, audio.Frequency, audio.Channels);

        if (result != null)
        {
            _resultText.text = result.Result;
            SetStatus($"Done. (lang: {result.Language})");
        }
        else
        {
            SetStatus("Transcription failed.");
        }
    }

    // ===================== FILE MODE =====================
    private async void OnFileClicked()
    {
        if (testAudioClip == null)
        {
            SetStatus("No audio clip! Drag one into Inspector.");
            return;
        }

        _resultText.text = "";
        SetStatus($"Transcribing: {testAudioClip.name}...");
        var result = await _whisper.GetTextAsync(testAudioClip);

        if (result != null)
        {
            _resultText.text = result.Result;
            SetStatus($"Done. (lang: {result.Language}, clip: {testAudioClip.name})");
        }
        else
        {
            SetStatus("Transcription failed.");
        }
    }

    // ===================== STREAM MODE =====================
    private async void OnStreamClicked()
    {
        if (_isStreaming)
        {
            StopStreaming();
            return;
        }

        _resultText.text = "";
        _stream = await _whisper.CreateStream(_mic);
        if (_stream == null)
        {
            SetStatus("Failed to create stream.");
            return;
        }

        _stream.OnResultUpdated += s => _resultText.text = s;
        _stream.OnStreamFinished += s =>
        {
            _resultText.text = s;
            _isStreaming = false;
            SetButtonText(_streamBtn, "Stream");
            SetStatus("Stream finished.");
        };

        _mic.StartRecord();
        _stream.StartStream();
        _isStreaming = true;

        SetButtonText(_streamBtn, "Stop Stream");
        SetStatus("Streaming... speak now.");
    }

    private void StopStreaming()
    {
        _stream?.StopStream();
        if (_mic.IsRecording) _mic.StopRecord();
        _isStreaming = false;
        SetButtonText(_streamBtn, "Stream");
        SetStatus("Stream stopped.");
    }

    // ===================== BUILD UI =====================
    private void BuildUI()
    {
        // Canvas
        var canvasGo = new GameObject("WhisperTestCanvas");
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // EventSystem with New Input System module
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var eventGo = new GameObject("EventSystem");
            eventGo.AddComponent<EventSystem>();
            eventGo.AddComponent<InputSystemUIInputModule>();
        }

        // Panel background
        var panel = CreatePanel(canvasGo.transform);

        // Layout
        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 2;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        // Title
        CreateText(panel.transform, "Whisper", 8, FontStyle.Bold, 10);

        // Status
        _statusText = CreateText(panel.transform, "Initializing...", 6, FontStyle.Italic, 9);
        _statusText.color = Color.yellow;

        // Progress bar
        var progressGo = CreateRect(panel.transform, "Progress", 0, 4);
        var progressBg = progressGo.AddComponent<Image>();
        progressBg.color = new Color(0.2f, 0.2f, 0.2f);
        var fillGo = CreateRect(progressGo.transform, "Fill", 0, 0);
        _progressFill = fillGo.AddComponent<Image>();
        _progressFill.color = new Color(0.2f, 0.8f, 0.3f);
        _progressFill.type = Image.Type.Filled;
        _progressFill.fillMethod = Image.FillMethod.Horizontal;
        _progressFill.fillAmount = 0;
        var fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        // Buttons row
        var row = CreateRect(panel.transform, "Buttons", 0, 14);
        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 3;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;

        _recordBtn = CreateButton(row.transform, "Rec", new Color(0.8f, 0.2f, 0.2f));
        _streamBtn = CreateButton(row.transform, "Stream", new Color(0.2f, 0.4f, 0.9f));
        _fileBtn = CreateButton(row.transform, "File", new Color(0.5f, 0.4f, 0.1f));

        // Result label
        CreateText(panel.transform, "Result:", 6, FontStyle.Bold, 8);

        // Result output
        _resultText = CreateText(panel.transform, "", 6, FontStyle.Normal, 40);
        _resultText.alignment = TextAnchor.UpperLeft;
        _resultText.color = Color.white;
    }

    // ===================== UI HELPERS =====================
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
        img.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-10, -10);
        rect.sizeDelta = new Vector2(150, 120);
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

    private Button CreateButton(Transform parent, string label, Color color)
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
        txt.fontSize = 7;
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
        Debug.Log($"[WhisperUI] {msg}");
    }
}
