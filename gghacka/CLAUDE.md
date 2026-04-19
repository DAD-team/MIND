# MIND VR - Project Conventions

## Project Overview
MIND VR - Ung dung tri lieu tam ly bang thuc te ao tren Meta Quest 3.
Pipeline: STT (Sherpa-ONNX) -> LLM (Groq/Gemini) -> TTS (SherpaOnnx/VieNeu) -> NPC animation.

## Module Structure

Tat ca code nam trong `Assets/Scripts/MIND/`, moi module co assembly definition rieng (.asmdef).

```
MIND/
├── Core/        [MIND.Core]        - Interfaces, data models, enums, events (base layer, khong ref module khac)
├── Auth/        [MIND.Auth]        - Firebase Auth (Google Sign-In + Email/Password)
├── Speech/      [MIND.Speech]      - STT (SherpaSTTStreaming) + TTS (SherpaOnnxTTS, VieNeuTTS)
├── AI/          [MIND.AI]          - LLM provider (OpenAI-compat), MindConversation orchestrator
├── NPC/         [MIND.NPC]         - NPC animation, silence handler, dialogue adapter
├── Session/     [MIND.Session]     - Session lifecycle, timer, logger, risk detector
├── Exercises/   [MIND.Exercises]   - CBT exercises (breathing 4-7-8, grounding 5-4-3-2-1)
├── UI/          [MIND.UI]          - VR UI (subtitle, timer, login panels, splash)
└── Environment/ [MIND.Environment] - Scene management, fade, lighting, ambient audio
```

## Assembly Dependency Rules

- Core KHONG reference bat ky module nao khac
- Cac module chi reference Core + nhung module can thiet (tranh circular dependency)
- Giao tiep giua module qua: interfaces (Core), events (SessionEvents), hoac method calls
- KHONG duoc tao circular dependency giua 2 asmdef

## Coding Conventions

### Namespace
- Moi file PHAI co namespace theo module: `MIND.Core`, `MIND.Auth`, `MIND.Speech`, ...
- KHONG de class o global namespace

### Unity MonoBehaviour
- [SerializeField] cho Inspector fields, KHONG dung public fields
- GetComponent<T>() chi goi trong Awake(), cache ket qua
- Coroutine cho async Unity operations (UnityWebRequest, fade, timer)
- Dung events/Actions de giao tiep giua components, KHONG FindObjectOfType trong Update

### Naming
- PascalCase: class, method, property, event
- _camelCase: private fields (underscore prefix)
- camelCase: local variables, parameters
- UPPER_CASE: constants

### Error Handling
- Debug.Log/LogWarning/LogError voi prefix [ClassName]
- Fallback gracefully: neu TTS khong ready, skip speech va tiep tuc
- KHONG throw exception trong MonoBehaviour methods (Awake, Start, Update)

### Firebase / Auth
- Firebase SDK dung #if FIREBASE_AUTH de compile khi chua cai SDK
- IAuthService interface trong Core, implementation trong Auth module
- Tat ca async Firebase calls wrap trong Task hoac Coroutine
- KHONG luu password hay token vao PlayerPrefs plaintext

### VR / Quest 3
- TAT CA trong 1 scene duy nhat — KHONG chuyen scene (hay loi tren VR)
- Dung bat/tat GameObject groups de chuyen flow: Auth → Therapy
- AppFlowManager dieu phoi toan bo flow
- World-space Canvas cho UI trong VR (renderMode = WorldSpace)
- XR Interaction Toolkit cho VR input (ray interactor + poke)
- Test tren Editor truoc (2D fallback), deploy Quest 3 sau
- KHONG dung DontDestroyOnLoad (chi co 1 scene)

### Test UI
- Moi module co the co OnGUI debug UI
- Toggle bang [SerializeField] bool showTestUI = true
- KHONG dung BuildUI() tao Canvas bang code cho test UI (qua nang)

### Git
- KHONG commit: google-services.json, .env, API keys
- Commit message ngan gon, tieng Anh
