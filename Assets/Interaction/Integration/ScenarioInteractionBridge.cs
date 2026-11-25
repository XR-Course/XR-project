using System;
using OfficeTraining.Scenario;
using UnityEngine;

namespace OfficeTraining.Interaction.Integration
{
    /// <summary>
    /// Connects the interaction module to Student 3's ScenarioCoordinator. Interaction components
    /// only publish on the InteractionEvents channel; this bridge forwards those events to the
    /// coordinator, which owns every rule and score. In the other direction it arms the
    /// extinguisher practice only in the Practice phase (and not while paused) and puts props,
    /// practice progress and zones back when the session is reset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenarioInteractionBridge : MonoBehaviour
    {
        [SerializeField] private ScenarioCoordinator coordinator;
        [SerializeField] private InteractionEvents events;
        [Tooltip("Zone id that counts as arriving at the assembly point.")]
        [SerializeField] private string safeZoneId = "assembly_point";
        [Tooltip("Pressing the controller menu button toggles pause.")]
        [SerializeField] private bool menuTogglesPause = true;
        [Tooltip("Temporary until Student 4's start menu exists: call StartTraining when Play starts.")]
        [SerializeField] private bool startTrainingOnPlay;
        [Tooltip("Props reset with the session. Empty = every ToolInteractionController in the scene.")]
        [SerializeField] private ToolInteractionController[] tools = Array.Empty<ToolInteractionController>();
        [Tooltip("Practice components armed in the Practice phase. Empty = every ExtinguisherPractice in the scene.")]
        [SerializeField] private ExtinguisherPractice[] practices = Array.Empty<ExtinguisherPractice>();
        [Tooltip("Zones reset with the session. Empty = every SafeZoneTrigger in the scene.")]
        [SerializeField] private SafeZoneTrigger[] zones = Array.Empty<SafeZoneTrigger>();

        private bool subscribed;

        public ScenarioCoordinator Coordinator => coordinator;
        public InteractionEvents Events => events;

        /// <summary>For scene setup code and tests (also works in edit mode, before OnEnable).</summary>
        public void Initialize(ScenarioCoordinator scenarioCoordinator, InteractionEvents interactionEvents)
        {
            Unsubscribe();
            coordinator = scenarioCoordinator;
            events = interactionEvents;
            Subscribe();
            UpdatePracticeArming();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Start()
        {
            CollectSceneComponents();
            UpdatePracticeArming();
            if (startTrainingOnPlay && coordinator != null && coordinator.CurrentPhase == TrainingPhase.Introduction)
                coordinator.StartTraining();
        }

        private void Subscribe()
        {
            if (subscribed || coordinator == null || events == null)
                return;
            events.OnGrabbed += HandleGrabbed;
            events.OnReleased += HandleReleased;
            events.OnUse += HandleUse;
            events.OnInspected += HandleInspected;
            events.OnZoneEntered += HandleZoneEntered;
            events.OnPracticeResult += HandlePracticeResult;
            events.OnMenuPressed += HandleMenuPressed;
            coordinator.OnPhaseChanged += HandlePhaseChanged;
            coordinator.OnPauseChanged += HandlePauseChanged;
            coordinator.OnSessionReset += HandleSessionReset;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;
            if (events != null)
            {
                events.OnGrabbed -= HandleGrabbed;
                events.OnReleased -= HandleReleased;
                events.OnUse -= HandleUse;
                events.OnInspected -= HandleInspected;
                events.OnZoneEntered -= HandleZoneEntered;
                events.OnPracticeResult -= HandlePracticeResult;
                events.OnMenuPressed -= HandleMenuPressed;
            }
            if (coordinator != null)
            {
                coordinator.OnPhaseChanged -= HandlePhaseChanged;
                coordinator.OnPauseChanged -= HandlePauseChanged;
                coordinator.OnSessionReset -= HandleSessionReset;
            }
            subscribed = false;
        }

        // ------------------------------------------------------------ interaction -> scenario

        private void HandleGrabbed(string itemId) => coordinator.NotifyGrabbed(itemId);
        private void HandleReleased(string itemId) => coordinator.NotifyReleased(itemId);
        private void HandleUse(string itemId, Pose pose) => coordinator.NotifyUsed(itemId);
        private void HandleInspected(string targetId) => coordinator.ConfirmHazard(targetId);

        private void HandleZoneEntered(string zoneId)
        {
            if (string.Equals(zoneId, safeZoneId, StringComparison.Ordinal))
                coordinator.NotifySafeZoneReached();
        }

        private void HandlePracticeResult(string itemId, bool success)
        {
            if (string.Equals(itemId, coordinator.Rules.ExtinguisherItemId, StringComparison.Ordinal))
                coordinator.ReportPracticeResult(success);
        }

        private void HandleMenuPressed()
        {
            if (menuTogglesPause && coordinator.CurrentPhase != TrainingPhase.Completed)
                coordinator.TogglePause();
        }

        // ------------------------------------------------------------ scenario -> interaction

        private void HandlePhaseChanged(TrainingPhase phase)
        {
            if (phase == TrainingPhase.Practice)
                foreach (ExtinguisherPractice practice in practices)
                    if (practice != null)
                        practice.ResetPractice();
            UpdatePracticeArming();
        }

        private void HandlePauseChanged(bool paused) => UpdatePracticeArming();

        private void HandleSessionReset()
        {
            CollectSceneComponents();
            foreach (ToolInteractionController tool in tools)
                if (tool != null)
                    tool.ResetToStartPose();
            foreach (ExtinguisherPractice practice in practices)
                if (practice != null)
                    practice.ResetPractice();
            foreach (SafeZoneTrigger zone in zones)
                if (zone != null)
                    zone.ResetZone();
            UpdatePracticeArming();
        }

        private void UpdatePracticeArming()
        {
            if (coordinator == null)
                return;
            bool armed = coordinator.CurrentPhase == TrainingPhase.Practice && !coordinator.IsPaused;
            foreach (ExtinguisherPractice practice in practices)
                if (practice != null)
                    practice.SetArmed(armed);
        }

        private void CollectSceneComponents()
        {
            if (tools == null || tools.Length == 0)
                tools = FindObjectsByType<ToolInteractionController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (practices == null || practices.Length == 0)
                practices = FindObjectsByType<ExtinguisherPractice>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (zones == null || zones.Length == 0)
                zones = FindObjectsByType<SafeZoneTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
    }
}
