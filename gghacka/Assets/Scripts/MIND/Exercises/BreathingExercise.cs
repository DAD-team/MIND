using System;
using System.Collections;
using MIND.Core;
using UnityEngine;

namespace MIND.Exercises
{
    /// <summary>
    /// 4-7-8 Breathing exercise with VR environment synchronization.
    /// Inhale 4s -> Hold 7s -> Exhale 8s, repeat 3 rounds.
    /// </summary>
    public class BreathingExercise : MonoBehaviour, IExercise
    {
        [Header("Settings")]
        [SerializeField] private float inhaleSeconds = 4f;
        [SerializeField] private float holdSeconds = 7f;
        [SerializeField] private float exhaleSeconds = 8f;
        [SerializeField] private int rounds = 3;

        public string ExerciseId => "breathing_478";
        public string DisplayName => "Hit tho 4-7-8";
        public bool IsRunning { get; private set; }
        public event Action OnCompleted;

        // For environment sync (lighting changes)
        public event Action<BreathPhase, float> OnPhaseChanged;

        public enum BreathPhase
        {
            Inhale,
            Hold,
            Exhale
        }

        public void Begin()
        {
            if (IsRunning) return;
            StartCoroutine(RunExercise());
        }

        public void Cancel()
        {
            if (!IsRunning) return;
            StopAllCoroutines();
            IsRunning = false;
        }

        private IEnumerator RunExercise()
        {
            IsRunning = true;
            Debug.Log($"[BreathingExercise] Starting {rounds} rounds of 4-7-8 breathing");

            for (int round = 0; round < rounds; round++)
            {
                Debug.Log($"[BreathingExercise] Round {round + 1}/{rounds}");

                // Inhale
                OnPhaseChanged?.Invoke(BreathPhase.Inhale, inhaleSeconds);
                yield return new WaitForSeconds(inhaleSeconds);

                // Hold
                OnPhaseChanged?.Invoke(BreathPhase.Hold, holdSeconds);
                yield return new WaitForSeconds(holdSeconds);

                // Exhale
                OnPhaseChanged?.Invoke(BreathPhase.Exhale, exhaleSeconds);
                yield return new WaitForSeconds(exhaleSeconds);
            }

            IsRunning = false;
            Debug.Log("[BreathingExercise] Completed");
            SessionEvents.RaiseExerciseCompleted(ExerciseId);
            OnCompleted?.Invoke();
        }
    }
}
