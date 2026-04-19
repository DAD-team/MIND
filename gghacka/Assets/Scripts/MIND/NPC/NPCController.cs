using System;
using System.Collections;
using MIND.Core;
using UnityEngine;

namespace MIND.NPC
{
    /// <summary>
    /// Luồng hoạt động hoàn chỉnh NPC — tự tìm waypoints của map đang active.
    ///
    /// Intro:  Standing Idle → Wave → Walk to chair → Sit Down → Sitting Idle
    /// Convo:  Sitting Talking ↔ Sitting Listening ↔ Sitting Idle + Head Tilt
    /// Outro:  Stand Up → Wave → Walk to exit
    ///
    /// Mỗi map chỉ cần có 1 GameObject gắn NPCWaypoints với 3 child:
    ///   SpawnPoint, ChairPoint, ExitPoint
    /// NPCController tự FindObjectOfType lúc PlayIntroSequence().
    /// </summary>
    public class NPCController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private NPCAnimatorController animatorController;
        [SerializeField] private SilenceHandler silenceHandler;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 1.2f;
        [SerializeField] private float rotateSpeed = 5f;
        [SerializeField] private float arriveThreshold = 0.1f;

        [Header("Head Tilt")]
        [SerializeField] private float headTiltCooldown = 8f;

        // Events — fired when intro/outro animation sequences complete
        public event Action OnIntroComplete;
        public event Action OnOutroComplete;

        // Waypoints — tìm tự động từ map đang active
        private NPCWaypoints _waypoints;
        private bool _introComplete;
        private bool _isMoving;
        private float _headTiltTimer;

        private void Awake()
        {
            if (animatorController == null)
                animatorController = GetComponent<NPCAnimatorController>();
            if (silenceHandler == null)
                silenceHandler = GetComponent<SilenceHandler>();
        }

        private void OnEnable()
        {
            SessionEvents.OnStateChanged += HandleStateChanged;
            if (silenceHandler != null)
                silenceHandler.OnSilenceAction += HandleSilenceAction;
        }

        private void OnDisable()
        {
            SessionEvents.OnStateChanged -= HandleStateChanged;
            if (silenceHandler != null)
                silenceHandler.OnSilenceAction -= HandleSilenceAction;
        }

        private void Update()
        {
            if (_headTiltTimer > 0f)
                _headTiltTimer -= Time.deltaTime;
        }

        /// <summary>
        /// Tìm NPCWaypoints đang active trong scene (của map hiện tại).
        /// </summary>
        private bool FindWaypoints()
        {
            _waypoints = FindObjectOfType<NPCWaypoints>();
            if (_waypoints == null)
            {
                Debug.LogError("[NPCController] Không tìm thấy NPCWaypoints trong scene! " +
                    "Hãy gắn NPCWaypoints vào environment đang active.");
                return false;
            }

            if (_waypoints.spawnPoint == null || _waypoints.chairPoint == null)
            {
                Debug.LogError($"[NPCController] NPCWaypoints '{_waypoints.gameObject.name}' " +
                    "thiếu spawnPoint hoặc chairPoint!");
                return false;
            }

            Debug.Log($"[NPCController] Dùng waypoints từ: {_waypoints.gameObject.name}");
            return true;
        }

        // ================================================================
        // Intro: Standing Idle → Wave → Walk to chair → Sit Down
        // ================================================================

        public void PlayIntroSequence()
        {
            _introComplete = false;
            StartCoroutine(IntroSequence());
        }

        private IEnumerator IntroSequence()
        {
            // Tìm waypoints của map đang active
            if (!FindWaypoints()) yield break;

            // 1. Đặt NPC tại spawn point
            transform.position = _waypoints.spawnPoint.position;
            transform.rotation = _waypoints.spawnPoint.rotation;

            animatorController.SetWalking(false);
            animatorController.SetSitting(false);
            animatorController.SetTalking(false);
            animatorController.SetListening(false);

            // 2. Quay về phía player rồi wave chào
            yield return StartCoroutine(LookAtPlayer());
            animatorController.TriggerWave();
            yield return new WaitForSeconds(animatorController.WaveLength);

            // 3. Đi bộ thực sự đến ghế
            yield return StartCoroutine(WalkTo(_waypoints.chairPoint.position));

            // 4. Xoay đúng hướng ngồi (rotation của chairPoint)
            yield return StartCoroutine(RotateTo(_waypoints.chairPoint.rotation));

            // 5. Ngồi xuống
            animatorController.TriggerSitDown();
            yield return new WaitForSeconds(animatorController.SitDownLength);

            // 6. Sitting Idle — sẵn sàng conversation
            animatorController.SetSitting(true);

            _introComplete = true;
            Debug.Log("[NPCController] Intro complete — NPC seated, ready for conversation");
            OnIntroComplete?.Invoke();
        }

        // ================================================================
        // Outro: Stand Up → Wave → Walk to exit
        // ================================================================

        public void PlayOutroSequence()
        {
            _introComplete = false;
            StartCoroutine(OutroSequence());
        }

        private IEnumerator OutroSequence()
        {
            animatorController.SetTalking(false);
            animatorController.SetListening(false);

            // 1. Đứng dậy
            animatorController.TriggerStandUp();
            yield return new WaitForSeconds(animatorController.StandUpLength);
            animatorController.SetSitting(false);

            // 2. Quay về phía player rồi wave tạm biệt
            yield return StartCoroutine(LookAtPlayer());
            animatorController.TriggerWave();
            yield return new WaitForSeconds(animatorController.WaveLength);

            // 3. Đi bộ đến exit (nếu có)
            if (_waypoints != null && _waypoints.exitPoint != null)
                yield return StartCoroutine(WalkTo(_waypoints.exitPoint.position));

            Debug.Log("[NPCController] Outro complete");
            OnOutroComplete?.Invoke();
        }

        // ================================================================
        // Movement
        // ================================================================

        private IEnumerator WalkTo(Vector3 targetPos)
        {
            _isMoving = true;
            animatorController.SetWalking(true);

            Vector3 target = new Vector3(targetPos.x, transform.position.y, targetPos.z);

            while (Vector3.Distance(transform.position, target) > arriveThreshold)
            {
                Vector3 direction = (target - transform.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, lookRotation, rotateSpeed * Time.deltaTime);
                }

                transform.position = Vector3.MoveTowards(
                    transform.position, target, walkSpeed * Time.deltaTime);

                yield return null;
            }

            transform.position = target;
            animatorController.SetWalking(false);
            _isMoving = false;
        }

        private IEnumerator LookAtPlayer()
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;

            Vector3 dir = cam.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) yield break;

            Quaternion targetRot = Quaternion.LookRotation(dir);
            yield return StartCoroutine(RotateTo(targetRot));
        }

        private IEnumerator RotateTo(Quaternion targetRotation)
        {
            while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
                yield return null;
            }
            transform.rotation = targetRotation;
        }

        // ================================================================
        // Conversation State
        // ================================================================

        private void HandleStateChanged(ConversationState state)
        {
            if (!_introComplete) return;

            switch (state)
            {
                case ConversationState.Idle:
                    animatorController.SetTalking(false);
                    animatorController.SetListening(false);
                    break;

                case ConversationState.Listening:
                    animatorController.SetTalking(false);
                    animatorController.SetListening(true);
                    TryTriggerHeadTilt();
                    break;

                case ConversationState.Processing:
                    animatorController.SetTalking(false);
                    animatorController.SetListening(false);
                    break;

                case ConversationState.Speaking:
                    animatorController.SetListening(false);
                    animatorController.SetTalking(true);
                    break;
            }
        }

        // ================================================================
        // Silence Handler
        // ================================================================

        private void HandleSilenceAction(SilenceHandler.SilenceLevel level, float silenceSeconds)
        {
            if (level == SilenceHandler.SilenceLevel.HeadNod)
                TryTriggerHeadTilt();
        }

        public void TryTriggerHeadTilt()
        {
            if (_headTiltTimer > 0f) return;
            animatorController.TriggerHeadTilt();
            _headTiltTimer = headTiltCooldown;
        }

        // Alias tương thích VRSessionManager
        public void PerformGreeting() => PlayIntroSequence();
        public void PerformFarewell() => PlayOutroSequence();

        // ================================================================
        // Debug UI
        // ================================================================

        [Header("Debug")]
        [SerializeField] private bool showTestUI = true;

        private void OnGUI()
        {
            if (!showTestUI) return;

            float x = 10, y = 10, w = 200, h = 30, gap = 5;

            GUI.Box(new Rect(x - 5, y - 5, w + 10, 400), "NPC Controller Test");
            y += 25;

            if (GUI.Button(new Rect(x, y, w, h), "1. Intro (Wave→Walk→Sit)"))
                PlayIntroSequence();
            y += h + gap;

            if (GUI.Button(new Rect(x, y, w, h), "2. Sitting Talking"))
            { _introComplete = true; animatorController.SetTalking(true); }
            y += h + gap;

            if (GUI.Button(new Rect(x, y, w, h), "3. Sitting Listening"))
            { _introComplete = true; animatorController.SetListening(true); }
            y += h + gap;

            if (GUI.Button(new Rect(x, y, w, h), "4. Sitting Idle"))
            { _introComplete = true; animatorController.SetTalking(false); animatorController.SetListening(false); }
            y += h + gap;

            if (GUI.Button(new Rect(x, y, w, h), "5. Head Tilt"))
            { _headTiltTimer = 0f; TryTriggerHeadTilt(); }
            y += h + gap;

            if (GUI.Button(new Rect(x, y, w, h), "6. Outro (Stand→Wave→Walk)"))
                PlayOutroSequence();
            y += h + gap;

            // Status
            y += 10;
            GUI.color = Color.yellow;
            string status = _isMoving ? "WALKING" : (_introComplete ? "SEATED" : "STANDING");
            GUI.Label(new Rect(x, y, w, h), $"Status: {status}");
            y += 20;
            string mapName = _waypoints != null ? _waypoints.gameObject.name : "chưa tìm";
            GUI.Label(new Rect(x, y, w, h), $"Map: {mapName}");
            y += 20;
            GUI.Label(new Rect(x, y, w, h), $"Pos: {transform.position:F1}");
            y += 20;
            GUI.Label(new Rect(x, y, w, h), $"HeadTilt CD: {_headTiltTimer:F1}s");
            GUI.color = Color.white;
        }
    }
}
