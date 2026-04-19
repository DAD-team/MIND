using System;
using System.Collections;
using System.Collections.Generic;
using MIND.Core;
using UnityEngine;

namespace MIND.Session
{
    /// <summary>
    /// Mock implementation cua IUserDataService cho dev/test.
    /// Tra ve emotion profile co dinh voi delay gia lap network.
    ///
    /// Setup:
    ///   1. Gan script nay vao GameObject bat ky (vd: AppFlow)
    ///   2. Chinh mockDelay, mockPhq9Score, v.v. trong Inspector
    ///   3. Keo vao field userDataService cua AppFlowManager
    /// </summary>
    public class MockUserDataService : MonoBehaviour, IUserDataService
    {
        [Header("Mock Settings")]
        [SerializeField] private float mockDelay = 1.5f;
        [SerializeField] private bool simulateError;

        [Header("Mock Emotion Profile")]
        [SerializeField] private int mockPhq9Score = 12;
        [SerializeField] private float mockDuchenneSmile = 0.3f;
        [SerializeField] private float mockFlatAffect = 0.7f;
        [SerializeField] private float mockEarRatio = 0.25f;
        [SerializeField] private float mockHeadPitch = -15.2f;
        [SerializeField] private float mockSilenceHours = 48f;
        [SerializeField] private string mockLastIntervention = "notification";
        [SerializeField] private List<string> mockAcademicEvents = new() { "Thi giua ky trong 2 ngay" };

        public void FetchEmotionProfile(string userId, Action<EmotionProfile> onSuccess, Action<string> onError)
        {
            Debug.Log($"[MockUserDataService] Fetching emotion profile for user: {userId}");
            StartCoroutine(FetchCoroutine(userId, onSuccess, onError));
        }

        private IEnumerator FetchCoroutine(string userId, Action<EmotionProfile> onSuccess, Action<string> onError)
        {
            yield return new WaitForSeconds(mockDelay);

            if (simulateError)
            {
                Debug.LogWarning("[MockUserDataService] Simulated error");
                onError?.Invoke("Không thể kết nối đến server (mock error)");
                yield break;
            }

            var profile = new EmotionProfile
            {
                phq9Score = mockPhq9Score,
                duchenneSmileProxy = mockDuchenneSmile,
                flatAffectScore = mockFlatAffect,
                earRatio = mockEarRatio,
                headPitch = mockHeadPitch,
                silenceHours = mockSilenceHours,
                lastIntervention = mockLastIntervention,
                academicEvents = new List<string>(mockAcademicEvents)
            };

            Debug.Log($"[MockUserDataService] Fetched profile: PHQ-9={profile.phq9Score}, severity={profile.GetSeverity()}");
            onSuccess?.Invoke(profile);
        }
    }
}
