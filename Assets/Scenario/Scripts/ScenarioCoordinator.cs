using System;
using System.Collections.Generic;
using OfficeTraining.Environment;
using UnityEngine;

namespace OfficeTraining.Scenario
{
    /// <summary>
    /// Scene entry point for the training scenario. Owns the TrainingStateMachine, forwards input
    /// from the interaction and UI modules, and drives the environment (hazard inspection gate,
    /// incident state, pause and reset). The environment references are optional so the UI team can
    /// run it in a mock scene without the office.
    ///
    /// Other modules call the public methods (directly or from UnityEvents) and subscribe to the
    /// On* events. All calls must happen on Unity's main thread.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenarioCoordinator : MonoBehaviour
    {
        [SerializeField] private ScenarioConfig config;
        [SerializeField] private HazardRegistry hazardRegistry;
        [SerializeField] private IncidentController incidentController;
        [Tooltip("Write every session event and the final report to the Console.")]
        [SerializeField] private bool logEvents = true;

        private TrainingStateMachine session;
        private bool subscribedToEnvironment;

        public event Action<TrainingPhase> OnPhaseChanged;
        public event Action<SessionEvent> OnEventRecorded;
        public event Action<bool> OnPauseChanged;
        public event Action<TrainingReport> OnReportReady;
        public event Action OnSessionReset;

        public TrainingStateMachine Session { get { EnsureInitialized(); return session; } }
        public TrainingPhase CurrentPhase => Session.Phase;
        public bool IsPaused => Session.IsPaused;
        public ScenarioRules Rules => Session.Rules;
        public int HazardsFound => Session.ConfirmedHazards.Count;
        public int HazardTotal => Session.Rules.Hazards.Count;

        private void Awake() => EnsureInitialized();

        private void OnEnable()
        {
            EnsureInitialized();
            SubscribeToEnvironment();
            ApplyEnvironment();
        }

        private void SubscribeToEnvironment()
        {
            if (hazardRegistry == null || subscribedToEnvironment)
                return;
            hazardRegistry.OnHazardConfirmed += HandleHazardConfirmed;
            subscribedToEnvironment = true;
        }

        private void OnDisable()
        {
            if (hazardRegistry != null && subscribedToEnvironment)
                hazardRegistry.OnHazardConfirmed -= HandleHazardConfirmed;
            subscribedToEnvironment = false;
        }

        // ------------------------------------------------------------ commands (UI / interaction)

        public void StartTraining() => Session.StartTraining();
        public void FinishInspection() => Session.FinishInspection();
        public void AcknowledgeIncident() => Session.AcknowledgeIncident();
        public void ChooseEvacuate() => Session.ChooseEvacuate();
        public void ChoosePractice() => Session.ChoosePractice();
        public void EndTraining() => Session.EndTraining();
        public void Pause() => Session.Pause();
        public void Resume() => Session.Resume();
        public void TogglePause() { if (Session.IsPaused) Session.Resume(); else Session.Pause(); }
        public void ResetTraining() => Session.Reset();

        /// <summary>
        /// With a HazardRegistry assigned this goes through the registry (so its visuals update);
        /// the registry's confirmation event then reaches the state machine. Without one (mock
        /// scenes) the id goes straight to the state machine.
        /// </summary>
        public bool ConfirmHazard(string hazardId)
        {
            if (hazardRegistry != null)
                return hazardRegistry.TryConfirmHazard(hazardId);
            return Session.ConfirmHazard(hazardId);
        }

        // Interaction module (Student 1) events.
        public void NotifyGrabbed(string itemId) => Session.NotifyGrabbed(itemId);
        public void NotifyReleased(string itemId) => Session.NotifyReleased(itemId);
        public void NotifyUsed(string itemId) => Session.NotifyUsed(itemId);
        public void ReportPracticeResult(bool success) => Session.ReportPracticeResult(success);

        // Navigation: call from the assembly-point trigger.
        public void NotifySafeZoneReached() => Session.ReachSafeZone();

        public TrainingReport GetReport() => Session.BuildReport();

        // ------------------------------------------------------------ environment mapping

        /// <summary>
        /// Incident presentation for a training phase. Null means "leave as it is" (Completed keeps
        /// the last presentation so the scene does not change under the results screen).
        /// </summary>
        public static IncidentState? IncidentStateFor(TrainingPhase phase, PracticeResult practice)
        {
            switch (phase)
            {
                case TrainingPhase.Introduction:
                case TrainingPhase.Inspection:
                    return IncidentState.Inactive;
                case TrainingPhase.Incident:
                case TrainingPhase.Decision:
                case TrainingPhase.Practice:
                    return IncidentState.Active;
                case TrainingPhase.Evacuation:
                    return practice == PracticeResult.Succeeded ? IncidentState.Resolved : IncidentState.EvacuationRequired;
                default:
                    return null;
            }
        }

        private void ApplyEnvironment()
        {
            if (session == null)
                return;
            if (hazardRegistry != null)
            {
                hazardRegistry.SetPaused(session.IsPaused);
                hazardRegistry.SetInspectionEnabled(session.Phase == TrainingPhase.Inspection);
            }
            if (incidentController != null)
            {
                incidentController.SetPaused(session.IsPaused);
                IncidentState? target = IncidentStateFor(session.Phase, session.Practice);
                if (target.HasValue && incidentController.CurrentState != target.Value &&
                    !incidentController.TrySetState(target.Value))
                    Debug.LogWarning($"[Scenario] Incident did not accept {target.Value} in phase {session.Phase}.", this);
            }
        }

        // ------------------------------------------------------------ internals

        private void EnsureInitialized()
        {
            if (session != null)
                return;

            List<HazardInfo> hazards = CollectHazards();
            ScenarioRules rules;
            try
            {
                rules = config != null
                    ? config.CreateRules(hazards)
                    : new ScenarioRules(hazards, practicePermitted: true);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError("[Scenario] Invalid scenario configuration, using defaults: " + exception.Message, this);
                rules = ScenarioRules.CreateDefault();
            }

            session = new TrainingStateMachine(rules, () => Time.realtimeSinceStartupAsDouble);
            session.PhaseChanged += HandlePhaseChanged;
            session.EventRecorded += HandleEventRecorded;
            session.PauseChanged += HandlePauseChanged;
            session.SessionReset += HandleSessionReset;

            // Lazy so the coordinator also works before Awake/OnEnable (edit-mode tests, early callers).
            if (isActiveAndEnabled)
                SubscribeToEnvironment();
            ApplyEnvironment();
        }

        private List<HazardInfo> CollectHazards()
        {
            var hazards = new List<HazardInfo>();
            if (hazardRegistry == null)
            {
                hazards.AddRange(ScenarioRules.DefaultHazards);
                return hazards;
            }
            if (!hazardRegistry.IsConfigurationValid)
            {
                Debug.LogError("[Scenario] HazardRegistry configuration is invalid; inspection can only be finished manually.", this);
                return hazards;
            }
            foreach (HazardObject hazard in hazardRegistry.Hazards)
                hazards.Add(new HazardInfo(hazard.HazardId, hazard.Definition.DisplayName));
            return hazards;
        }

        private void HandleHazardConfirmed(string hazardId) => session.ConfirmHazard(hazardId);

        private void HandlePhaseChanged(TrainingPhase previous, TrainingPhase current)
        {
            ApplyEnvironment();
            OnPhaseChanged?.Invoke(current);
            if (current != TrainingPhase.Completed)
                return;
            TrainingReport report = session.BuildReport();
            if (logEvents)
                Debug.Log("[Scenario]\n" + report.ToText(), this);
            OnReportReady?.Invoke(report);
        }

        private void HandleEventRecorded(SessionEvent entry)
        {
            if (logEvents)
                Debug.Log("[Scenario] " + entry, this);
            OnEventRecorded?.Invoke(entry);
        }

        private void HandlePauseChanged(bool paused)
        {
            ApplyEnvironment();
            OnPauseChanged?.Invoke(paused);
        }

        private void HandleSessionReset()
        {
            if (hazardRegistry != null)
                hazardRegistry.ResetHazards();
            if (incidentController != null)
                incidentController.ResetIncident();
            ApplyEnvironment();
            OnSessionReset?.Invoke();
        }
    }
}
