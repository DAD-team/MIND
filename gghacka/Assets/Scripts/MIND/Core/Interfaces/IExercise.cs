using System;

namespace MIND.Core
{
    public interface IExercise
    {
        string ExerciseId { get; }
        string DisplayName { get; }
        bool IsRunning { get; }
        event Action OnCompleted;
        void Begin();
        void Cancel();
    }
}
