using MIND.Auth;
using MIND.Core;
using MIND.Environment;
using MIND.UI;
using UnityEngine;

namespace MIND.Session
{
    /// <summary>
    /// Dieu phoi toan bo app flow trong 1 scene duy nhat.
    /// Khong chuyen scene — chi bat/tat cac group GameObject.
    ///
    /// Flow:  Auth → DataSync → EnvironmentSelect → Therapy
    ///
    /// Setup:
    ///   1. Tao GameObject "AppFlow" gan script nay
    ///   2. Keo AuthUIManager (Canvas auth) vao field
    ///   3. Keo therapyRoot (parent chua tat ca therapy objects) vao field
    ///   4. Keo authRoot (parent chua auth UI) vao field
    ///   5. Keo DataSyncPanel vao field
    ///   6. Keo EnvironmentSelectPanel vao field
    ///   7. Keo MockUserDataService (hoac real service) vao field userDataServiceComponent
    /// </summary>
    public class AppFlowManager : MonoBehaviour
    {
        [Header("Auth")]
        [SerializeField] private AuthUIManager authUI;
        [SerializeField] private GameObject authRoot;

        [Header("Data Sync")]
        [SerializeField] private DataSyncPanel dataSyncPanel;
        [SerializeField] private MonoBehaviour userDataServiceComponent;

        [Header("Environment Select")]
        [SerializeField] private EnvironmentSelectPanel environmentSelectPanel;

        [Header("Environment")]
        [SerializeField] private EnvironmentManager environmentManager;

        [Header("Therapy")]
        [SerializeField] private GameObject therapyRoot;
        [SerializeField] private VRSessionManager sessionManager;

        private enum AppState { Auth, DataSync, EnvironmentSelect, Therapy }
        private AppState _state;

        private IUserDataService _userDataService;
        private EmotionProfile _currentProfile;

        private void Awake()
        {
            // Resolve IUserDataService from serialized component
            if (userDataServiceComponent != null)
                _userDataService = userDataServiceComponent as IUserDataService;

            if (_userDataService == null)
                Debug.LogWarning("[AppFlow] userDataServiceComponent khong implement IUserDataService");

            // Auto-find EnvironmentManager neu chua wire trong Inspector
            if (environmentManager == null)
            {
                environmentManager = FindObjectOfType<EnvironmentManager>(true);
                if (environmentManager != null)
                    Debug.Log("[AppFlow] Auto-found EnvironmentManager");
                else
                    Debug.LogError("[AppFlow] KHONG TIM THAY EnvironmentManager trong scene!");
            }
        }

        private void Start()
        {
            // Subscribe auth success
            if (authUI != null)
                authUI.OnAuthSuccess += OnLoginSuccess;

            // Subscribe sign out to return to auth
            if (AuthManager.Instance != null)
                AuthManager.Instance.OnSignedOut += OnSignedOut;

            // Subscribe data sync retry
            if (dataSyncPanel != null)
                dataSyncPanel.OnRetryClicked += OnRetryDataSync;

            // Subscribe environment select
            if (environmentSelectPanel != null)
                environmentSelectPanel.OnEnvironmentSelected += OnEnvironmentSelected;

            // Check if already signed in
            if (AuthManager.Instance != null && AuthManager.Instance.IsSignedIn)
            {
                Debug.Log("[AppFlow] Already signed in, starting data sync");
                EnterDataSync();
            }
            else
            {
                EnterAuth();
            }
        }

        private void OnDestroy()
        {
            if (authUI != null)
                authUI.OnAuthSuccess -= OnLoginSuccess;

            if (AuthManager.Instance != null)
                AuthManager.Instance.OnSignedOut -= OnSignedOut;

            if (dataSyncPanel != null)
                dataSyncPanel.OnRetryClicked -= OnRetryDataSync;

            if (environmentSelectPanel != null)
                environmentSelectPanel.OnEnvironmentSelected -= OnEnvironmentSelected;
        }

        // ================================================================
        // State transitions
        // ================================================================

        private void EnterAuth()
        {
            _state = AppState.Auth;
            _currentProfile = null;

            SetActiveGroups(auth: true, dataSync: false, envSelect: false, therapy: false);
            Debug.Log("[AppFlow] → Auth");
        }

        private void EnterDataSync()
        {
            _state = AppState.DataSync;

            SetActiveGroups(auth: false, dataSync: true, envSelect: false, therapy: false);
            Debug.Log("[AppFlow] → DataSync");

            StartDataSync();
        }

        private void EnterEnvironmentSelect()
        {
            _state = AppState.EnvironmentSelect;

            SetActiveGroups(auth: false, dataSync: false, envSelect: true, therapy: false);

            var severity = _currentProfile?.GetSeverity() ?? PhqSeverity.Minimal;
            if (environmentSelectPanel != null)
                environmentSelectPanel.Show(severity);

            Debug.Log("[AppFlow] → EnvironmentSelect");
        }

        private void EnterTherapy(TherapyEnvironment environment)
        {
            _state = AppState.Therapy;

            SetActiveGroups(auth: false, dataSync: false, envSelect: false, therapy: true);

            // Load environment (skybox/passthrough + objects)
            if (environmentManager != null)
            {
                Debug.Log($"[AppFlow] Loading environment: {environment}");
                environmentManager.LoadEnvironment(environment);
            }
            else
            {
                Debug.LogError("[AppFlow] environmentManager is NULL! Environment se khong duoc load.");
            }

            // Build SessionConfig voi emotion profile da fetch
            var config = new SessionConfig
            {
                emotionProfile = _currentProfile,
                environment = environment
            };

            // Start VR session
            if (sessionManager != null)
                sessionManager.StartSession(config);

            var user = AuthManager.Instance?.CurrentUser;
            Debug.Log($"[AppFlow] → Therapy (user: {user?.displayName ?? "unknown"}, env: {environment}, PHQ: {_currentProfile?.phq9Score ?? 0})");
        }

        private void SetActiveGroups(bool auth, bool dataSync, bool envSelect, bool therapy)
        {
            if (authRoot != null) authRoot.SetActive(auth);
            if (authUI != null) authUI.gameObject.SetActive(auth);

            // DataSyncPanel nam chung GameObject voi AppFlowManager
            // Chi bat/tat component, KHONG tat gameObject (se tat luon AppFlow)
            if (dataSyncPanel != null) dataSyncPanel.enabled = dataSync;

            if (environmentSelectPanel != null) environmentSelectPanel.gameObject.SetActive(envSelect);
            if (therapyRoot != null) therapyRoot.SetActive(therapy);
        }

        // ================================================================
        // Data Sync
        // ================================================================

        private void StartDataSync()
        {
            if (dataSyncPanel != null)
                dataSyncPanel.Show();

            if (_userDataService == null)
            {
                Debug.LogWarning("[AppFlow] No IUserDataService, using default profile");
                _currentProfile = new EmotionProfile();
                OnDataSyncComplete();
                return;
            }

            var userId = AuthManager.Instance?.CurrentUser?.uid ?? "unknown";
            _userDataService.FetchEmotionProfile(userId, OnProfileFetched, OnProfileFetchError);
        }

        private void OnProfileFetched(EmotionProfile profile)
        {
            _currentProfile = profile;
            Debug.Log($"[AppFlow] Profile fetched: PHQ-9={profile.phq9Score}, severity={profile.GetSeverity()}");
            OnDataSyncComplete();
        }

        private void OnProfileFetchError(string error)
        {
            Debug.LogWarning($"[AppFlow] Profile fetch error: {error}");
            if (dataSyncPanel != null)
                dataSyncPanel.SetError(error);
        }

        private void OnDataSyncComplete()
        {
            if (dataSyncPanel != null)
                dataSyncPanel.SetSuccess(() => EnterEnvironmentSelect());
            else
                EnterEnvironmentSelect();
        }

        private void OnRetryDataSync()
        {
            Debug.Log("[AppFlow] Retrying data sync...");
            StartDataSync();
        }

        // ================================================================
        // Event handlers
        // ================================================================

        private void OnLoginSuccess()
        {
            EnterDataSync();
        }

        private void OnEnvironmentSelected(TherapyEnvironment environment)
        {
            EnterTherapy(environment);
        }

        private void OnSignedOut()
        {
            // Stop session if active
            if (sessionManager != null && sessionManager.IsSessionActive)
                sessionManager.EndSession(SessionEndTrigger.UserInitiated);

            EnterAuth();
        }
    }
}
