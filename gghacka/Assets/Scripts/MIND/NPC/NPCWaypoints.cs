using UnityEngine;

namespace MIND.NPC
{
    /// <summary>
    /// Đặt component này trong mỗi environment/map.
    /// NPCController sẽ tự tìm NPCWaypoints đang active trong scene.
    ///
    /// Setup:
    ///   1. Trong mỗi environment (Garden, Beach, MeditationRoom...),
    ///      tạo 1 Empty "NPCWaypoints" và gắn script này
    ///   2. Tạo 3 child Empty: SpawnPoint, ChairPoint, ExitPoint
    ///   3. Đặt vị trí + rotation cho từng point
    ///   4. Kéo 3 child vào Inspector
    ///
    /// Hierarchy ví dụ:
    ///   Environment_Garden (parent bật/tắt theo map)
    ///     ├── Trees, Lighting, ...
    ///     └── NPCWaypoints            ← gắn script này
    ///         ├── SpawnPoint           ← NPC đứng ban đầu
    ///         ├── ChairPoint           ← vị trí + hướng ngồi
    ///         └── ExitPoint            ← NPC đi ra
    /// </summary>
    public class NPCWaypoints : MonoBehaviour
    {
        [Tooltip("NPC đứng ban đầu")]
        public Transform spawnPoint;

        [Tooltip("Vị trí ghế + rotation = hướng mặt NPC khi ngồi")]
        public Transform chairPoint;

        [Tooltip("NPC đi ra khi kết thúc")]
        public Transform exitPoint;

        [Header("Gizmo")]
        [SerializeField] private float _gizmoArrowLength = 0.8f;
        [SerializeField] private float _gizmoSphereRadius = 0.15f;

        private void OnDrawGizmos()
        {
            if (spawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(spawnPoint.position, _gizmoSphereRadius);
            }

            if (chairPoint != null)
            {
                // Vị trí ghế
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(chairPoint.position, _gizmoSphereRadius);

                // Mũi tên hướng mặt NPC khi ngồi (forward = trục Z)
                Vector3 forward = chairPoint.forward * _gizmoArrowLength;
                Vector3 tip = chairPoint.position + forward;
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(chairPoint.position, tip);

                // Đầu mũi tên
                Vector3 right = chairPoint.right * 0.15f;
                Vector3 arrowBack = -forward.normalized * 0.25f;
                Gizmos.DrawLine(tip, tip + arrowBack + right);
                Gizmos.DrawLine(tip, tip + arrowBack - right);
            }

            if (exitPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(exitPoint.position, _gizmoSphereRadius);
            }

            // Đường nối spawn → chair → exit
            Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
            if (spawnPoint != null && chairPoint != null)
                Gizmos.DrawLine(spawnPoint.position, chairPoint.position);
            if (chairPoint != null && exitPoint != null)
                Gizmos.DrawLine(chairPoint.position, exitPoint.position);
        }
    }
}
