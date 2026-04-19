using UnityEngine;

namespace MIND.Environment
{
    /// <summary>
    /// Bat/tat Quest 3 passthrough va chuyen doi voi skybox.
    ///
    /// - Passthrough ON:  OVRPassthroughLayer enabled, camera clearFlags = SolidColor (transparent)
    /// - Passthrough OFF: OVRPassthroughLayer disabled, camera clearFlags = Skybox
    ///
    /// Setup:
    ///   1. Gan script nay vao GameObject trong therapyRoot
    ///   2. Keo OVRPassthroughLayer component (tu [BuildingBlock] Passthrough) vao field
    ///   3. Keo Camera (CenterEyeAnchor) vao field vrCamera
    ///   4. Mac dinh passthrough bat (Quest 3 default)
    /// </summary>
    public class PassthroughController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MonoBehaviour passthroughLayer;
        [SerializeField] private Camera vrCamera;

        private bool _passthroughActive;

        public bool IsPassthroughActive => _passthroughActive;

        /// <summary>
        /// Bat passthrough — user nhin thay the gioi thuc qua camera Quest 3.
        /// </summary>
        public void EnablePassthrough()
        {
            _passthroughActive = true;

            // Bat OVRPassthroughLayer
            if (passthroughLayer != null)
                passthroughLayer.enabled = true;

            // Camera can SolidColor voi alpha = 0 de passthrough hien qua
            if (vrCamera != null)
            {
                vrCamera.clearFlags = CameraClearFlags.SolidColor;
                vrCamera.backgroundColor = Color.clear;
            }

            Debug.Log("[PassthroughController] Passthrough ON");
        }

        /// <summary>
        /// Tat passthrough — hien skybox thay the.
        /// Goi SetSkybox() truoc hoac sau de set material.
        /// </summary>
        public void DisablePassthrough()
        {
            _passthroughActive = false;

            // Tat OVRPassthroughLayer
            if (passthroughLayer != null)
                passthroughLayer.enabled = false;

            // Camera render skybox
            if (vrCamera != null)
                vrCamera.clearFlags = CameraClearFlags.Skybox;

            Debug.Log("[PassthroughController] Passthrough OFF (skybox mode)");
        }

        /// <summary>
        /// Set skybox material cho RenderSettings.
        /// Neu material null thi giu nguyen skybox hien tai.
        /// </summary>
        public void SetSkybox(Material skyboxMaterial)
        {
            if (skyboxMaterial == null) return;

            RenderSettings.skybox = skyboxMaterial;
            DynamicGI.UpdateEnvironment();

            Debug.Log($"[PassthroughController] Skybox set: {skyboxMaterial.name}");
        }
    }
}
