using System;
using MIND.Core;
using UnityEngine;

namespace MIND.Exercises
{
    /// <summary>
    /// 5-4-3-2-1 Grounding exercise.
    /// Guides user through 5 senses: see, touch, hear, smell, taste.
    /// </summary>
    public class GroundingExercise : MonoBehaviour, IExercise
    {
        public string ExerciseId => "grounding_54321";
        public string DisplayName => "Grounding 5-4-3-2-1";
        public bool IsRunning { get; private set; }
        public event Action OnCompleted;

        private int _currentStep;

        private static readonly string[] Steps =
        {
            "Hay nhin xung quanh va ke cho minh 5 thu ban nhin thay...",
            "Tot lam. Gio hay tuong tuong 4 thu ban co the cham vao...",
            "Tiep theo, hay lang nghe 3 am thanh xung quanh...",
            "Hay nghi ve 2 mui huong ban co the ngui...",
            "Cuoi cung, 1 vi ma ban co the cam nhan..."
        };

        public event Action<int, string> OnStepChanged;

        public void Begin()
        {
            if (IsRunning) return;
            IsRunning = true;
            _currentStep = 0;
            AdvanceStep();
            Debug.Log("[GroundingExercise] Started");
        }

        public void Cancel()
        {
            IsRunning = false;
            _currentStep = 0;
        }

        /// <summary>
        /// Call this when the user has responded to the current step.
        /// </summary>
        public void UserResponded()
        {
            if (!IsRunning) return;
            _currentStep++;
            if (_currentStep >= Steps.Length)
            {
                Complete();
            }
            else
            {
                AdvanceStep();
            }
        }

        private void AdvanceStep()
        {
            OnStepChanged?.Invoke(_currentStep, Steps[_currentStep]);
            Debug.Log($"[GroundingExercise] Step {_currentStep + 1}/5: {Steps[_currentStep]}");
        }

        private void Complete()
        {
            IsRunning = false;
            Debug.Log("[GroundingExercise] Completed");
            SessionEvents.RaiseExerciseCompleted(ExerciseId);
            OnCompleted?.Invoke();
        }
    }
}
