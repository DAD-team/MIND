using MIND.Core;
using UnityEngine;

namespace MIND.Environment
{
    /// <summary>
    /// Manages therapy environment selection and loading.
    /// Ho tro ca skybox environments va passthrough mode.
    ///
    /// Setup:
    ///   1. Gan script nay vao GameObject trong therapyRoot
    ///   2. Keo cac environment root GameObjects (neu co)
    ///   3. Keo skybox materials (HDRI) vao cac field tuong ung
    ///   4. Keo PassthroughController vao field
    /// </summary>
    public class EnvironmentManager : MonoBehaviour
    {
        [Header("Environment Roots")]
        [SerializeField] private GameObject gardenRoot;
        [SerializeField] private GameObject beachRoot;
        [SerializeField] private GameObject meditationRoot;

        [Header("Skybox Materials (HDRI)")]
        [SerializeField] private Material gardenSkybox;
        [SerializeField] private Material beachSkybox;
        [SerializeField] private Material meditationSkybox;

        [Header("Passthrough")]
        [SerializeField] private PassthroughController passthroughController;

        [Header("NPC")]
        [SerializeField] private GameObject npcRoot;

        private TherapyEnvironment _currentEnvironment;

        public TherapyEnvironment CurrentEnvironment => _currentEnvironment;

        private void Awake()
        {
            if (npcRoot != null)
                npcRoot.SetActive(false);
        }

        public void LoadEnvironment(TherapyEnvironment environment)
        {
            _currentEnvironment = environment;

            if (npcRoot != null)
                npcRoot.SetActive(true);

            // Tat tat ca environment roots truoc
            if (gardenRoot != null) gardenRoot.SetActive(false);
            if (beachRoot != null) beachRoot.SetActive(false);
            if (meditationRoot != null) meditationRoot.SetActive(false);

            if (environment == TherapyEnvironment.Passthrough)
            {
                // Passthrough mode — bat camera thuc, khong can skybox/objects
                if (passthroughController != null)
                    passthroughController.EnablePassthrough();
            }
            else
            {
                // Skybox mode — tat passthrough, bat skybox + environment objects
                if (passthroughController != null)
                    passthroughController.DisablePassthrough();

                // Set skybox material
                Material skybox = environment switch
                {
                    TherapyEnvironment.Garden => gardenSkybox,
                    TherapyEnvironment.Beach => beachSkybox,
                    TherapyEnvironment.MeditationRoom => meditationSkybox,
                    _ => null
                };

                if (passthroughController != null && skybox != null)
                    passthroughController.SetSkybox(skybox);

                // Bat environment root tuong ung (neu co gan trong Inspector)
                switch (environment)
                {
                    case TherapyEnvironment.Garden:
                        if (gardenRoot != null)
                            gardenRoot.SetActive(true);
                        else
                            Debug.LogWarning("[EnvironmentManager] gardenRoot is NULL! Keo GardenRoot vao Inspector.");
                        break;
                    case TherapyEnvironment.Beach:
                        if (beachRoot != null)
                            beachRoot.SetActive(true);
                        else
                            Debug.LogWarning("[EnvironmentManager] beachRoot is NULL!");
                        break;
                    case TherapyEnvironment.MeditationRoom:
                        if (meditationRoot != null)
                            meditationRoot.SetActive(true);
                        else
                            Debug.LogWarning("[EnvironmentManager] meditationRoot is NULL!");
                        break;
                }
            }

            Debug.Log($"[EnvironmentManager] Loaded environment: {environment}");
        }

        public TherapyEnvironment SuggestEnvironment(EmotionProfile profile)
        {
            if (profile == null) return TherapyEnvironment.Garden;

            return profile.GetSeverity() switch
            {
                PhqSeverity.Severe => TherapyEnvironment.MeditationRoom,
                PhqSeverity.Moderate => TherapyEnvironment.Beach,
                _ => TherapyEnvironment.Garden
            };
        }
    }
}
