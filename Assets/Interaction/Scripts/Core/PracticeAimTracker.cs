using System;

namespace OfficeTraining.Interaction
{
    public enum PracticeOutcome
    {
        InProgress = 0,
        Succeeded = 1,
        Failed = 2
    }

    /// <summary>
    /// Plain C# rule for the simplified extinguisher practice: the learner must hold the trigger
    /// while aiming at the practice target for <see cref="RequiredAimSeconds"/> in total. Optionally,
    /// spraying for longer than <see cref="MaxSpraySeconds"/> without success counts as a failed
    /// attempt. Time only counts while the tracker is armed (the scenario allows practice).
    /// No timers of its own: the owner calls <see cref="Tick"/> with the frame delta.
    /// </summary>
    public sealed class PracticeAimTracker
    {
        public PracticeAimTracker(float requiredAimSeconds, float maxSpraySeconds = 0f)
        {
            if (requiredAimSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(requiredAimSeconds), "Required aim time must be positive.");
            if (maxSpraySeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(maxSpraySeconds), "Max spray time must be 0 (no limit) or positive.");
            if (maxSpraySeconds > 0f && maxSpraySeconds < requiredAimSeconds)
                throw new ArgumentOutOfRangeException(nameof(maxSpraySeconds), "Max spray time must not be shorter than the required aim time.");

            RequiredAimSeconds = requiredAimSeconds;
            MaxSpraySeconds = maxSpraySeconds;
        }

        public float RequiredAimSeconds { get; }
        /// <summary>0 means no limit.</summary>
        public float MaxSpraySeconds { get; }
        public bool Armed { get; private set; }
        public float AimSeconds { get; private set; }
        public float SpraySeconds { get; private set; }
        public PracticeOutcome Outcome { get; private set; }
        public bool IsFinished => Outcome != PracticeOutcome.InProgress;
        /// <summary>0..1 share of the required aim time.</summary>
        public float Progress => Math.Min(1f, AimSeconds / RequiredAimSeconds);

        /// <summary>Raised once per attempt when the outcome becomes Succeeded or Failed.</summary>
        public event Action<PracticeOutcome> Finished;

        /// <summary>Arming does not clear progress; use <see cref="Reset"/> for a new attempt.</summary>
        public void SetArmed(bool armed) => Armed = armed;

        /// <param name="deltaSeconds">Frame time; negative values are ignored.</param>
        /// <param name="spraying">Trigger held while the extinguisher is grabbed.</param>
        /// <param name="onTarget">Spray direction hits the practice target.</param>
        /// <returns>True when this tick finished the attempt.</returns>
        public bool Tick(float deltaSeconds, bool spraying, bool onTarget)
        {
            if (!Armed || IsFinished || !spraying || deltaSeconds <= 0f)
                return false;

            SpraySeconds += deltaSeconds;
            if (onTarget)
                AimSeconds += deltaSeconds;

            if (AimSeconds >= RequiredAimSeconds)
                return Finish(PracticeOutcome.Succeeded);
            if (MaxSpraySeconds > 0f && SpraySeconds >= MaxSpraySeconds)
                return Finish(PracticeOutcome.Failed);
            return false;
        }

        /// <summary>Clears progress and outcome for a new attempt. Keeps the armed flag.</summary>
        public void Reset()
        {
            AimSeconds = 0f;
            SpraySeconds = 0f;
            Outcome = PracticeOutcome.InProgress;
        }

        private bool Finish(PracticeOutcome outcome)
        {
            Outcome = outcome;
            Finished?.Invoke(outcome);
            return true;
        }
    }
}
