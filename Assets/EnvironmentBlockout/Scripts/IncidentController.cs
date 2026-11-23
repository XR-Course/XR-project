using System;
using UnityEngine;

namespace OfficeTraining.Environment
{
    /// <summary>
    /// Receives incident commands from a coordinator (or test harness) and publishes state.
    /// No timers, hazard dependencies, fire simulation, extinguisher rules or assessment.
    /// All calls and events occur synchronously on Unity's main thread.
    /// </summary>
    public sealed class IncidentController : MonoBehaviour
    {
        public IncidentState CurrentState { get; private set; } = IncidentState.Inactive;
        public bool IsPaused { get; private set; }

        public event Action<IncidentState> OnIncidentStateChanged;
        public event Action<bool> OnPauseChanged;
        public event Action OnIncidentReset;

        /// <summary>
        /// Applies any valid, different state while enabled and unpaused.
        /// Student 3 decides which transitions are permitted by the training scenario.
        /// </summary>
        public bool TrySetState(IncidentState state)
        {
            if (!isActiveAndEnabled || IsPaused || !Enum.IsDefined(typeof(IncidentState), state) || state == CurrentState)
                return false;

            CurrentState = state;
            OnIncidentStateChanged?.Invoke(state);
            return true;
        }

        /// <summary>Pause gates incident commands, not Time.timeScale or any other module.</summary>
        public void SetPaused(bool paused)
        {
            if (IsPaused == paused)
                return;
            IsPaused = paused;
            OnPauseChanged?.Invoke(paused);
        }

        /// <summary>
        /// Always available, including while paused/disabled. Restores inactive and unpaused.
        /// Reset listeners are notified even if the state was already inactive, so future
        /// effects/audio can clear residual presentation without fake state transitions.
        /// </summary>
        public void ResetIncident()
        {
            bool wasPaused = IsPaused;
            bool stateChanged = CurrentState != IncidentState.Inactive;
            CurrentState = IncidentState.Inactive;
            IsPaused = false;

            if (wasPaused)
                OnPauseChanged?.Invoke(false);
            if (stateChanged)
                OnIncidentStateChanged?.Invoke(CurrentState);
            OnIncidentReset?.Invoke();
        }
    }
}
