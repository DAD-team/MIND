using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using MIND.Auth;
using MIND.UI;
using MIND.Session;
using MIND.Environment;
using MIND.AI;
using MIND.NPC;
using MIND.Exercises;
using MIND.Speech;

namespace MIND.Editor
{
    /// <summary>
    /// One-click setup toan bo AppFlow hierarchy trong scene.
    /// Menu: MIND / Setup AppFlow In Scene
    ///
    /// Tao tat ca GameObjects, gan scripts, wire SerializeFields.
    /// Neu GameObject da ton tai (theo ten) thi skip, khong tao trung.
    /// </summary>
    public static class AppFlowSetupEditor
    {
        [MenuItem("MIND/Setup AppFlow In Scene")]
        public static void SetupAppFlow()
        {
            Undo.SetCurrentGroupName("MIND Setup AppFlow");

            // ================================================================
            // 1. AppFlow root
            // ================================================================
            var appFlow = FindOrCreate("AppFlow");
            var appFlowManager = EnsureComponent<AppFlowManager>(appFlow);
            var mockDataService = EnsureComponent<MockUserDataService>(appFlow);
            var dataSyncPanel = EnsureComponent<DataSyncPanel>(appFlow);

            // ================================================================
            // 2. AuthRoot
            // ================================================================
            var authRoot = FindOrCreate("AuthRoot", appFlow.transform);

            // Auth Canvas (World Space)
            var authCanvas = FindOrCreate("AuthCanvas", authRoot.transform);
            var authCanvasComp = SetupWorldSpaceCanvas(authCanvas, new Vector3(0, 1.5f, 2f));

            // LoginPanel
            var loginPanelGO = FindOrCreate("LoginPanel", authCanvas.transform);
            var loginPanel = EnsureComponent<LoginPanel>(loginPanelGO);
            SetupRectTransform(loginPanelGO, Vector2.zero, new Vector2(600, 500));
            CreateTMPInputChild(loginPanelGO, "[Email Input]", "Email...", false);
            CreateTMPInputChild(loginPanelGO, "[Password Input]", "Password...", true);
            var loginBtn = CreateButtonChild(loginPanelGO, "[Login Button]", "Đăng nhập");
            var googleBtn = CreateButtonChild(loginPanelGO, "[Google Button]", "Đăng nhập Google");
            var switchRegBtn = CreateButtonChild(loginPanelGO, "[Switch Register]", "Tạo tài khoản mới");
            var loginErrorText = CreateTMPChild(loginPanelGO, "ErrorText", "");

            // EmailRegisterPanel
            var registerPanelGO = FindOrCreate("EmailRegisterPanel", authCanvas.transform);
            var registerPanel = EnsureComponent<EmailRegisterPanel>(registerPanelGO);
            SetupRectTransform(registerPanelGO, Vector2.zero, new Vector2(600, 600));
            CreateTMPInputChild(registerPanelGO, "[DisplayName Input]", "Tên hiển thị...", false);
            CreateTMPInputChild(registerPanelGO, "[Reg Email Input]", "Email...", false);
            CreateTMPInputChild(registerPanelGO, "[Reg Password Input]", "Mật khẩu...", true);
            CreateTMPInputChild(registerPanelGO, "[Confirm Password Input]", "Xác nhận mật khẩu...", true);
            CreateButtonChild(registerPanelGO, "[Register Button]", "Đăng ký");
            CreateButtonChild(registerPanelGO, "[Back Button]", "Quay lại");
            CreateTMPChild(registerPanelGO, "RegErrorText", "");
            registerPanelGO.SetActive(false);

            // LoadingOverlay
            var loadingOverlayGO = FindOrCreate("LoadingOverlay", authCanvas.transform);
            EnsureComponent<LoadingOverlay>(loadingOverlayGO);
            SetupRectTransform(loadingOverlayGO, Vector2.zero, new Vector2(600, 600));
            var loadingImage = FindOrCreate("LoadingImage", loadingOverlayGO.transform);
            EnsureComponent<Image>(loadingImage);
            SetupRectTransform(loadingImage, Vector2.zero, new Vector2(100, 100));
            loadingOverlayGO.SetActive(false);

            // AuthUIManager
            var authUIGO = FindOrCreate("AuthUIManager", authRoot.transform);
            var authUIManager = EnsureComponent<AuthUIManager>(authUIGO);

            // AuthManager (singleton)
            var authManagerGO = FindOrCreate("AuthManager", authRoot.transform);
            var authManager = EnsureComponent<AuthManager>(authManagerGO);
            var mockAuth = EnsureComponent<MockAuthService>(authManagerGO);

            // Wire AuthUIManager
            SetField(authUIManager, "loginPanel", loginPanel);
            SetField(authUIManager, "registerPanel", registerPanel);
            SetField(authUIManager, "loadingOverlay", loadingOverlayGO);
            SetField(authUIManager, "authManager", authManager);

            // Wire AuthManager
            SetField(authManager, "authServiceComponent", mockAuth);

            // ================================================================
            // 3. EnvironmentSelect Canvas
            // ================================================================
            var envSelectCanvas = FindOrCreate("EnvironmentSelectCanvas", appFlow.transform);
            SetupWorldSpaceCanvas(envSelectCanvas, new Vector3(0, 1.5f, 2f));

            var envSelectPanelGO = FindOrCreate("EnvironmentSelectPanel", envSelectCanvas.transform);
            var envSelectPanel = EnsureComponent<EnvironmentSelectPanel>(envSelectPanelGO);
            SetupRectTransform(envSelectPanelGO, Vector2.zero, new Vector2(700, 500));

            var envTitle = CreateTMPChild(envSelectPanelGO, "Title", "Chọn không gian trị liệu");
            var passthroughBtn = CreateButtonChild(envSelectPanelGO, "PassthroughBtn", "Thực tế (Passthrough)");
            var gardenBtn = CreateButtonChild(envSelectPanelGO, "GardenBtn", "Khu vườn yên tĩnh");
            var beachBtn = CreateButtonChild(envSelectPanelGO, "BeachBtn", "Bãi biển hoàng hôn");
            var meditationBtn = CreateButtonChild(envSelectPanelGO, "MeditationBtn", "Phòng thiền tĩnh lặng");
            var passthroughDesc = CreateTMPChild(envSelectPanelGO, "PassthroughDesc", "Trị liệu ngay trong không gian thực của bạn");
            var gardenDesc = CreateTMPChild(envSelectPanelGO, "GardenDesc", "Cây xanh, tiếng chim, ánh sáng ấm");
            var beachDesc = CreateTMPChild(envSelectPanelGO, "BeachDesc", "Sóng biển, gió nhẹ, ánh hoàng hôn");
            var meditationDesc = CreateTMPChild(envSelectPanelGO, "MeditationDesc", "Tối giản, ánh nến, nhạc ambient nhẹ");
            var recommendText = CreateTMPChild(envSelectPanelGO, "RecommendText", "");

            // Wire EnvironmentSelectPanel
            SetField(envSelectPanel, "titleText", envTitle.GetComponent<TMP_Text>());
            SetField(envSelectPanel, "passthroughBtn", passthroughBtn.GetComponent<Button>());
            SetField(envSelectPanel, "gardenBtn", gardenBtn.GetComponent<Button>());
            SetField(envSelectPanel, "beachBtn", beachBtn.GetComponent<Button>());
            SetField(envSelectPanel, "meditationBtn", meditationBtn.GetComponent<Button>());
            SetField(envSelectPanel, "passthroughDesc", passthroughDesc.GetComponent<TMP_Text>());
            SetField(envSelectPanel, "gardenDesc", gardenDesc.GetComponent<TMP_Text>());
            SetField(envSelectPanel, "beachDesc", beachDesc.GetComponent<TMP_Text>());
            SetField(envSelectPanel, "meditationDesc", meditationDesc.GetComponent<TMP_Text>());
            SetField(envSelectPanel, "recommendText", recommendText.GetComponent<TMP_Text>());

            envSelectCanvas.SetActive(false);

            // ================================================================
            // 4. TherapyRoot
            // ================================================================
            var therapyRoot = FindOrCreate("TherapyRoot", appFlow.transform);

            // --- SessionManager ---
            var sessionManagerGO = FindOrCreate("SessionManager", therapyRoot.transform);
            var sessionManager = EnsureComponent<VRSessionManager>(sessionManagerGO);
            var sessionTimer = EnsureComponent<SessionTimer>(sessionManagerGO);
            EnsureComponent<SessionLogger>(sessionManagerGO);

            // --- Environment ---
            var envGO = FindOrCreate("Environment", therapyRoot.transform);
            var envManager = EnsureComponent<EnvironmentManager>(envGO);
            var passthroughCtrl = EnsureComponent<PassthroughController>(envGO);
            var lightingCtrl = EnsureComponent<LightingController>(envGO);
            var ambientAudio = EnsureComponent<AmbientAudioController>(envGO);

            // MainLight
            var mainLightGO = FindOrCreate("MainLight", envGO.transform);
            var mainLight = EnsureComponent<Light>(mainLightGO);
            mainLight.type = LightType.Directional;
            mainLight.intensity = 0.8f;
            mainLight.color = new Color(1f, 0.95f, 0.85f);
            SetField(lightingCtrl, "mainLight", mainLight);

            // Environment roots (empty — user keo props vao sau)
            var gardenRoot = FindOrCreate("GardenRoot", envGO.transform);
            var beachRoot = FindOrCreate("BeachRoot", envGO.transform);
            var meditationRoot = FindOrCreate("MeditationRoot", envGO.transform);
            beachRoot.SetActive(false);
            meditationRoot.SetActive(false);

            // Tao ground + seat co ban trong moi env root
            CreateBasicEnvironmentProps(gardenRoot);
            CreateBasicEnvironmentProps(beachRoot);
            CreateBasicEnvironmentProps(meditationRoot);

            // Wire EnvironmentManager
            SetField(envManager, "gardenRoot", gardenRoot);
            SetField(envManager, "beachRoot", beachRoot);
            SetField(envManager, "meditationRoot", meditationRoot);
            SetField(envManager, "passthroughController", passthroughCtrl);

            // Wire PassthroughController — user can keo OVRPassthroughLayer va Camera sau
            // Tim trong scene neu co
            var ovrPassthrough = GameObject.Find("[BuildingBlock] Passthrough");
            if (ovrPassthrough != null)
            {
                var ptLayer = ovrPassthrough.GetComponent<MonoBehaviour>();
                if (ptLayer != null && ptLayer.GetType().Name == "OVRPassthroughLayer")
                    SetField(passthroughCtrl, "passthroughLayer", ptLayer);
            }

            var centerEye = GameObject.Find("CenterEyeAnchor");
            if (centerEye != null)
            {
                var cam = centerEye.GetComponent<Camera>();
                if (cam != null)
                    SetField(passthroughCtrl, "vrCamera", cam);
            }

            // --- NPC ---
            var npcRoot = FindOrCreate("NPC", therapyRoot.transform);
            var npcModelGO = FindOrCreate("NPCModel", npcRoot.transform);
            var npcController = EnsureComponent<NPCController>(npcModelGO);
            var npcAnimCtrl = EnsureComponent<NPCAnimatorController>(npcModelGO);
            var npcDialogue = EnsureComponent<NPCDialogueAdapter>(npcModelGO);
            npcModelGO.transform.localPosition = new Vector3(0, 0, 2f);

            // --- Conversation ---
            var conversationGO = FindOrCreate("Conversation", therapyRoot.transform);
            var conversation = EnsureComponent<MindConversation>(conversationGO);
            var silenceHandler = EnsureComponent<SilenceHandler>(conversationGO);

            var sttGO = FindOrCreate("STT", conversationGO.transform);
            var stt = EnsureComponent<SherpaSTTStreaming>(sttGO);

            var localTtsGO = FindOrCreate("LocalTTS", conversationGO.transform);
            var localTts = EnsureComponent<SherpaOnnxTTS>(localTtsGO);

            var serverTtsGO = FindOrCreate("ServerTTS", conversationGO.transform);
            var serverTts = EnsureComponent<VieNeuTTS>(serverTtsGO);

            // Wire MindConversation
            SetField(conversation, "stt", stt);
            SetField(conversation, "localTts", localTts);
            SetField(conversation, "serverTts", serverTts);

            // --- Exercises ---
            var exercisesGO = FindOrCreate("Exercises", therapyRoot.transform);
            var exerciseManager = EnsureComponent<ExerciseManager>(exercisesGO);

            var breathingGO = FindOrCreate("BreathingExercise", exercisesGO.transform);
            var breathing = EnsureComponent<BreathingExercise>(breathingGO);

            var groundingGO = FindOrCreate("GroundingExercise", exercisesGO.transform);
            var grounding = EnsureComponent<GroundingExercise>(groundingGO);

            SetField(exerciseManager, "breathingExercise", breathing);
            SetField(exerciseManager, "groundingExercise", grounding);

            // --- TherapyCanvas ---
            var therapyCanvas = FindOrCreate("TherapyCanvas", therapyRoot.transform);
            SetupWorldSpaceCanvas(therapyCanvas, new Vector3(0, 1.2f, 2.5f));

            var subtitleGO = FindOrCreate("SubtitleDisplay", therapyCanvas.transform);
            EnsureComponent<SubtitleDisplay>(subtitleGO);
            var subtitleTMP = EnsureComponent<TextMeshProUGUI>(subtitleGO);
            subtitleTMP.fontSize = 24;
            subtitleTMP.alignment = TextAlignmentOptions.Center;
            SetupRectTransform(subtitleGO, new Vector2(0, -100), new Vector2(800, 100));

            var timerDisplayGO = FindOrCreate("SessionTimerDisplay", therapyCanvas.transform);
            EnsureComponent<SessionTimerDisplay>(timerDisplayGO);
            var timerTMP = EnsureComponent<TextMeshProUGUI>(timerDisplayGO);
            timerTMP.fontSize = 20;
            timerTMP.alignment = TextAlignmentOptions.TopRight;
            SetupRectTransform(timerDisplayGO, new Vector2(250, 200), new Vector2(200, 50));

            var endBtnGO = CreateButtonChild(therapyCanvas, "SessionEndButton", "Kết thúc phiên");
            EnsureComponent<SessionEndButton>(endBtnGO);
            SetupRectTransform(endBtnGO, new Vector2(0, -220), new Vector2(250, 50));

            var summaryGO = FindOrCreate("SessionSummaryUI", therapyCanvas.transform);
            EnsureComponent<SessionSummaryUI>(summaryGO);
            SetupRectTransform(summaryGO, Vector2.zero, new Vector2(600, 400));
            summaryGO.SetActive(false);

            // FadePanel (stretch full)
            var fadeGO = FindOrCreate("FadePanel", therapyCanvas.transform);
            var fadeTransition = EnsureComponent<FadeTransition>(fadeGO);
            var fadeCG = EnsureComponent<CanvasGroup>(fadeGO);
            var fadeImage = EnsureComponent<Image>(fadeGO);
            fadeImage.color = Color.black;
            fadeCG.alpha = 0f;
            var fadeRect = fadeGO.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;
            SetField(fadeTransition, "fadeCanvasGroup", fadeCG);

            // --- Wire VRSessionManager ---
            SetField(sessionManager, "conversation", conversation);
            SetField(sessionManager, "npcController", npcController);
            SetField(sessionManager, "dialogueAdapter", npcDialogue);
            SetField(sessionManager, "sessionTimer", sessionTimer);
            SetField(sessionManager, "fadeTransition", fadeTransition);

            // ================================================================
            // 5. SplashCanvas
            // ================================================================
            var splashCanvas = FindOrCreate("SplashCanvas");
            SetupWorldSpaceCanvas(splashCanvas, new Vector3(0, 1.5f, 1.8f));
            var splashGO = FindOrCreate("SplashScreen", splashCanvas.transform);
            var splash = EnsureComponent<SplashScreen>(splashGO);
            var splashCG = EnsureComponent<CanvasGroup>(splashGO);
            SetupRectTransform(splashGO, Vector2.zero, new Vector2(600, 400));
            var splashTitle = CreateTMPChild(splashGO, "SplashTitle", "MIND VR");
            var splashTitleTMP = splashTitle.GetComponent<TextMeshProUGUI>();
            splashTitleTMP.fontSize = 48;
            splashTitleTMP.alignment = TextAlignmentOptions.Center;
            var splashMsg = CreateTMPChild(splashGO, "SplashMessage", "Không gian này thuộc về bạn");
            var splashMsgTMP = splashMsg.GetComponent<TextMeshProUGUI>();
            splashMsgTMP.fontSize = 24;
            splashMsgTMP.alignment = TextAlignmentOptions.Center;
            SetField(splash, "canvasGroup", splashCG);

            // ================================================================
            // 6. Wire AppFlowManager
            // ================================================================
            SetField(appFlowManager, "authUI", authUIManager);
            SetField(appFlowManager, "authRoot", authRoot);
            SetField(appFlowManager, "dataSyncPanel", dataSyncPanel);
            SetField(appFlowManager, "userDataServiceComponent", mockDataService as MonoBehaviour);
            SetField(appFlowManager, "environmentSelectPanel", envSelectPanel);
            SetField(appFlowManager, "environmentManager", envManager);
            SetField(appFlowManager, "therapyRoot", therapyRoot);
            SetField(appFlowManager, "sessionManager", sessionManager);

            // Mac dinh: therapyRoot tat, envSelect tat
            therapyRoot.SetActive(false);

            // ================================================================
            // Done
            // ================================================================
            Selection.activeGameObject = appFlow;
            EditorUtility.SetDirty(appFlow);

            Debug.Log("[MIND Setup] AppFlow hierarchy created!\n" +
                      "- AuthRoot: LoginPanel, RegisterPanel, LoadingOverlay\n" +
                      "- EnvironmentSelectCanvas: 4 buttons (Passthrough/Garden/Beach/Meditation)\n" +
                      "- TherapyRoot: SessionManager, Environment, NPC, Conversation, Exercises, TherapyCanvas\n" +
                      "- SplashCanvas\n\n" +
                      "TODO thu cong:\n" +
                      "  1. Keo skybox HDRI materials vao EnvironmentManager\n" +
                      "  2. Keo NPC 3D model vao NPCModel + gan Animator\n" +
                      "  3. Keo ambient audio clips vao AmbientAudioController\n" +
                      "  4. Dien API key/URL vao MindConversation (Inspector)\n" +
                      "  5. Chinh vi tri/scale cac Canvas cho phu hop VR");
        }

        // ================================================================
        // Helper methods
        // ================================================================

        private static GameObject FindOrCreate(string name, Transform parent = null)
        {
            Transform found = parent != null
                ? parent.Find(name)
                : GameObject.Find(name)?.transform;

            if (found != null)
                return found.gameObject;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

            if (parent != null)
                go.transform.SetParent(parent, false);

            return go;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null)
            {
                comp = Undo.AddComponent<T>(go);
            }
            return comp;
        }

        private static Canvas SetupWorldSpaceCanvas(GameObject go, Vector3 position)
        {
            var canvas = EnsureComponent<Canvas>(go);
            canvas.renderMode = RenderMode.WorldSpace;

            var scaler = EnsureComponent<CanvasScaler>(go);
            scaler.dynamicPixelsPerUnit = 10f;

            EnsureComponent<GraphicRaycaster>(go);

            var rect = go.GetComponent<RectTransform>();
            rect.localPosition = position;
            rect.localScale = Vector3.one * 0.001f;
            rect.sizeDelta = new Vector2(800, 600);

            return canvas;
        }

        private static void SetupRectTransform(GameObject go, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var rect = EnsureComponent<RectTransform>(go);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
        }

        private static GameObject CreateTMPChild(GameObject parent, string name, string text)
        {
            var go = FindOrCreate(name, parent.transform);
            var tmp = EnsureComponent<TextMeshProUGUI>(go);
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.color = Color.white;
            EnsureComponent<RectTransform>(go);
            return go;
        }

        private static GameObject CreateTMPInputChild(GameObject parent, string name, string placeholder, bool isPassword)
        {
            var go = FindOrCreate(name, parent.transform);
            var input = EnsureComponent<TMP_InputField>(go);
            SetupRectTransform(go, Vector2.zero, new Vector2(400, 50));

            // Text area
            var textArea = FindOrCreate("Text Area", go.transform);
            EnsureComponent<RectMask2D>(textArea);
            var textAreaRect = EnsureComponent<RectTransform>(textArea);
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 5);
            textAreaRect.offsetMax = new Vector2(-10, -5);

            // Input text
            var textGO = FindOrCreate("Text", textArea.transform);
            var textTMP = EnsureComponent<TextMeshProUGUI>(textGO);
            textTMP.fontSize = 16;
            textTMP.color = Color.white;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // Placeholder
            var placeholderGO = FindOrCreate("Placeholder", textArea.transform);
            var placeholderTMP = EnsureComponent<TextMeshProUGUI>(placeholderGO);
            placeholderTMP.text = placeholder;
            placeholderTMP.fontSize = 16;
            placeholderTMP.fontStyle = FontStyles.Italic;
            placeholderTMP.color = new Color(1f, 1f, 1f, 0.5f);
            var phRect = placeholderGO.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = Vector2.zero;
            phRect.offsetMax = Vector2.zero;

            // Wire input field
            input.textViewport = textAreaRect;
            input.textComponent = textTMP;
            input.placeholder = placeholderTMP;

            if (isPassword)
                input.contentType = TMP_InputField.ContentType.Password;

            // Background image
            var bg = EnsureComponent<Image>(go);
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            return go;
        }

        private static GameObject CreateButtonChild(GameObject parent, string name, string label)
        {
            var go = FindOrCreate(name, parent.transform);
            var btn = EnsureComponent<Button>(go);
            var img = EnsureComponent<Image>(go);
            img.color = new Color(0.2f, 0.5f, 0.8f, 1f);
            SetupRectTransform(go, Vector2.zero, new Vector2(300, 45));

            var textGO = FindOrCreate("Text", go.transform);
            var tmp = EnsureComponent<TextMeshProUGUI>(textGO);
            tmp.text = label;
            tmp.fontSize = 18;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return go;
        }

        private static void CreateBasicEnvironmentProps(GameObject envRoot)
        {
            var defaultMat = GetDefaultMaterial();

            // Ground plane
            var ground = FindOrCreate("GroundPlane", envRoot.transform);
            var groundMf = EnsureComponent<MeshFilter>(ground);
            groundMf.sharedMesh = GetPrimitiveMesh(PrimitiveType.Plane);
            var groundMr = EnsureComponent<MeshRenderer>(ground);
            groundMr.sharedMaterial = defaultMat;
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localScale = new Vector3(3, 1, 3);

            // NPC seat (cube placeholder)
            var npcSeat = FindOrCreate("NPCSeat", envRoot.transform);
            var npcSeatMf = EnsureComponent<MeshFilter>(npcSeat);
            npcSeatMf.sharedMesh = GetPrimitiveMesh(PrimitiveType.Cube);
            var npcSeatMr = EnsureComponent<MeshRenderer>(npcSeat);
            npcSeatMr.sharedMaterial = defaultMat;
            npcSeat.transform.localPosition = new Vector3(0, 0.25f, 2f);
            npcSeat.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

            // User seat (cube placeholder)
            var userSeat = FindOrCreate("UserSeat", envRoot.transform);
            var userSeatMf = EnsureComponent<MeshFilter>(userSeat);
            userSeatMf.sharedMesh = GetPrimitiveMesh(PrimitiveType.Cube);
            var userSeatMr = EnsureComponent<MeshRenderer>(userSeat);
            userSeatMr.sharedMaterial = defaultMat;
            userSeat.transform.localPosition = new Vector3(0, 0.25f, 0);
            userSeat.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
        }

        private static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            var temp = GameObject.CreatePrimitive(type);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        private static Material GetDefaultMaterial()
        {
            // URP project: dung Default-Line.mat hoac tim URP Lit shader
            // Tao material moi voi URP Lit shader cho tuong thich Quest 3
            var urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader != null)
            {
                var mat = new Material(urpLitShader);
                mat.name = "MIND_Default";
                mat.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                return mat;
            }

            // Fallback: Built-in (se bi tim tren URP nhung khong crash)
            return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
        }

        private static void SetField(object target, string fieldName, object value)
        {
            if (target == null || value == null) return;

            var type = target.GetType();
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                if (target is Object unityObj)
                    Undo.RecordObject(unityObj, $"Set {fieldName}");

                field.SetValue(target, value);

                if (target is Object obj)
                    EditorUtility.SetDirty(obj);
            }
            else
            {
                Debug.LogWarning($"[MIND Setup] Field '{fieldName}' not found on {type.Name}");
            }
        }
    }
}
