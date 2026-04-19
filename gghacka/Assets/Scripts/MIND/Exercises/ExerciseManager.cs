using System.Collections.Generic;
using MIND.Core;
using UnityEngine;

namespace MIND.Exercises
{
    /// <summary>
    /// Manages and triggers CBT exercises during the session.
    /// </summary>
    public class ExerciseManager : MonoBehaviour
    {
        [SerializeField] private BreathingExercise breathingExercise;
        [SerializeField] private GroundingExercise groundingExercise;

        private readonly List<string> _completedExercises = new();

        public IReadOnlyList<string> CompletedExercises => _completedExercises;

        public void StartBreathing()
        {
            if (breathingExercise != null && !breathingExercise.IsRunning)
            {
                breathingExercise.Begin();
                SessionEvents.RaiseExerciseStarted(breathingExercise.ExerciseId);
            }
        }

        public void StartGrounding()
        {
            if (groundingExercise != null && !groundingExercise.IsRunning)
            {
                groundingExercise.Begin();
                SessionEvents.RaiseExerciseStarted(groundingExercise.ExerciseId);
            }
        }

        private void OnEnable()
        {
            SessionEvents.OnExerciseCompleted += HandleExerciseCompleted;
        }

        private void OnDisable()
        {
            SessionEvents.OnExerciseCompleted -= HandleExerciseCompleted;
        }

        private void HandleExerciseCompleted(string exerciseId)
        {
            if (!_completedExercises.Contains(exerciseId))
                _completedExercises.Add(exerciseId);
        }
    }
}
