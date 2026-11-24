using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OfficeTraining.Scenario
{
    /// <summary>
    /// Deterministic training state machine. Has no timers and no Unity dependency: every
    /// change is caused by a command. Every command (accepted or not) is written to the
    /// session log, so invalid order, duplicates and pause are visible in the report.
    ///
    /// Commands return true only when accepted as requested. State is committed before any
    /// event is raised. If a listener resets the session from inside an event, the rest of the
    /// interrupted command is dropped.
    /// </summary>
    public sealed class TrainingStateMachine
    {
        private readonly Func<double> clock;
        private readonly List<SessionEvent> events = new List<SessionEvent>();
        private readonly List<string> confirmedHazards = new List<string>();
        private readonly HashSet<string> confirmedSet = new HashSet<string>(StringComparer.Ordinal);
        private double startTime;
        private double? endTime;
        private int sessionVersion;

        public TrainingStateMachine(ScenarioRules rules, Func<double> clock = null)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.clock = clock ?? (() => 0d);
            Events = events.AsReadOnly();
            ConfirmedHazards = confirmedHazards.AsReadOnly();
            startTime = this.clock();
        }

        public ScenarioRules Rules { get; }
        public TrainingPhase Phase { get; private set; } = TrainingPhase.Introduction;
        public bool IsPaused { get; private set; }
        public DecisionPath Decision { get; private set; }
        /// <summary>True when the decision was inferred (walking to the exit, grabbing the extinguisher).</summary>
        public bool DecisionWasImplicit { get; private set; }
        public PracticeResult Practice { get; private set; }
        public bool ReachedSafeZone { get; private set; }
        public bool EndedEarly { get; private set; }
        /// <summary>Practice chosen when not permitted, or extinguisher used outside the permitted practice.</summary>
        public int UnsafeActionCount { get; private set; }
        public IReadOnlyList<SessionEvent> Events { get; }
        /// <summary>Confirmed hazard ids in confirmation order.</summary>
        public IReadOnlyList<string> ConfirmedHazards { get; }
        public double ElapsedSeconds => (endTime ?? clock()) - startTime;
        public bool IsCompleted => Phase == TrainingPhase.Completed;

        /// <summary>(previous, current). Also raised by Reset when the phase was not Introduction.</summary>
        public event Action<TrainingPhase, TrainingPhase> PhaseChanged;
        public event Action<SessionEvent> EventRecorded;
        public event Action<bool> PauseChanged;
        public event Action SessionReset;

        // ---------------------------------------------------------------- flow commands

        /// <summary>Introduction -> Inspection.</summary>
        public bool StartTraining()
        {
            const SessionEventType type = SessionEventType.TrainingStarted;
            if (IsPaused)
                return Reject(type, EventOutcome.WhilePaused);
            if (Phase != TrainingPhase.Introduction)
                return Reject(type, EventOutcome.WrongPhase, detail: "training already started");

            startTime = clock();
            if (Emit(type, EventOutcome.Accepted))
                EnterPhase(TrainingPhase.Inspection);
            return true;
        }

        /// <summary>Accepted once per hazard during Inspection. Auto-advances when configured and all are found.</summary>
        public bool ConfirmHazard(string hazardId)
        {
            const SessionEventType type = SessionEventType.HazardConfirmed;
            if (IsPaused)
                return Reject(type, EventOutcome.WhilePaused, hazardId);
            if (Phase != TrainingPhase.Inspection)
                return Reject(type, EventOutcome.WrongPhase, hazardId);
            if (!Rules.IsKnownHazard(hazardId))
                return Reject(type, EventOutcome.UnknownId, hazardId);
            if (confirmedSet.Contains(hazardId))
                return Reject(type, EventOutcome.Duplicate, hazardId);

            confirmedSet.Add(hazardId);
            confirmedHazards.Add(hazardId);
            string progress = confirmedHazards.Count + "/" + Rules.Hazards.Count;
            if (!Emit(type, EventOutcome.Accepted, hazardId, progress))
                return true;

            if (Rules.AutoAdvanceWhenAllHazardsFound && confirmedHazards.Count == Rules.Hazards.Count &&
                Emit(SessionEventType.InspectionFinished, EventOutcome.Accepted, detail: "all hazards found"))
                EnterPhase(TrainingPhase.Incident);
            return true;
        }

        /// <summary>Inspection -> Incident, even if hazards were missed (they are reported as missed).</summary>
        public bool FinishInspection()
        {
            const SessionEventType type = SessionEventType.InspectionFinished;
            if (IsPaused)
                return Reject(type, EventOutcome.WhilePaused);
            if (Phase != TrainingPhase.Inspection)
                return Reject(type, EventOutcome.WrongPhase);

            string detail = confirmedHazards.Count + "/" + Rules.Hazards.Count + " hazards found";
            if (Emit(type, EventOutcome.Accepted, detail: detail))
                EnterPhase(TrainingPhase.Incident);
            return true;
        }

        /// <summary>Incident -> Decision (the learner has seen the alarm message). Optional: decisions are also accepted during Incident.</summary>
        public bool AcknowledgeIncident()
        {
            const SessionEventType type = SessionEventType.IncidentAcknowledged;
            if (IsPaused)
                return Reject(type, EventOutcome.WhilePaused);
            if (Phase != TrainingPhase.Incident)
                return Reject(type, EventOutcome.WrongPhase);

            if (Emit(type, EventOutcome.Accepted))
                EnterPhase(TrainingPhase.Decision);
            return true;
        }

        /// <summary>Always available once the incident has started. From Practice it abandons the practice.</summary>
        public bool ChooseEvacuate()
        {
            const SessionEventType type = SessionEventType.EvacuationChosen;
            if (IsPaused)
                return Reject(type, EventOutcome.WhilePaused);
            if (Phase == TrainingPhase.Evacuation)
                return Reject(type, EventOutcome.Duplicate);
            if (!IsIncidentLive(Phase))
                return Reject(type, EventOutcome.WrongPhase);

            string detail;
            if (Phase == TrainingPhase.Practice)
            {
                Practice = PracticeResult.Abandoned;
                detail = "practice stopped";
            }
            else
            {
                Decision = DecisionPath.Evacuate;
                DecisionWasImplicit = false;
                detail = null;
            }

            if (Emit(type, EventOutcome.Accepted, detail: detail))
                EnterPhase(TrainingPhase.Evacuation);
            return true;
        }

        /// <summary>
        /// Incident/Decision -> Practice when practice is permitted. When it is not permitted the
        /// choice is recorded as unsafe, no practice is offered and the scenario moves to Evacuation.
        /// Returns false in that case.
        /// </summary>
        public bool ChoosePractice() => ChoosePracticeCore(false);

        private bool ChoosePracticeCore(bool implicitChoice)
        {
            const SessionEventType type = SessionEventType.PracticeChosen;
            string how = implicitChoice ? "grabbed extinguisher" : null;
            if (IsPaused)
                return Reject(type, EventOutcome.WhilePaused, detail: how);
            if (Phase == TrainingPhase.Practice)
                return Reject(type, EventOutcome.Duplicate, detail: how);
            if (Phase != TrainingPhase.Incident && Phase != TrainingPhase.Decision)
                return Reject(type, EventOutcome.WrongPhase, detail: how);

            DecisionWasImplicit = implicitChoice;
            if (Rules.PracticePermitted)
            {
                Decision = DecisionPath.PracticeInPermittedCondition;
                if (Emit(type, EventOutcome.Accepted, detail: how))
                    EnterPhase(TrainingPhase.Practice);
                return true;
            }

            Decision = DecisionPath.PracticeWhenNotPermitted;
            Practice = PracticeResult.NotPermitted;
            UnsafeActionCount++;
            string detail = "practice is not permitted in this scenario; evacuation required" + (how != null ? " (" + how + ")" : "");
            if (Emit(type, EventOutcome.NotPermitted, detail: detail))
                EnterPhase(TrainingPhase.Evacuation);
            return false;
        }

        /// <summary>Practice -> Evacuation. Reported by the interaction module; ignored outside Practice.</summary>
        public bool ReportPracticeResult(bool success)
        {
            const SessionEventType type = SessionEventType.PracticeResultReported;
            string subject = success ? "success" : "failure";
            if (IsPaused)
                return Reject(type, EventOutcome.WhilePaused, subject);
            if (Phase != TrainingPhase.Practice)
                return Reject(type, EventOutcome.WrongPhase, subject, "not in the practice branch; not rewarded");

            Practice = success ? PracticeResult.Succeeded : PracticeResult.Failed;
            if (Emit(type, EventOutcome.Accepted, subject))
                EnterPhase(TrainingPhase.Evacuation);
            return true;
        }

        /// <summary>
        /// Arrival at the assembly point completes the training. Accepted from any phase after the
        /// incident started; going straight to the exit counts as choosing to evacuate.
        /// </summary>
        public bool ReachSafeZone()
        {
            const SessionEventType type = SessionEventType.SafeZoneReached;
            if (IsPaused)
                return Reject(type, EventOutcome.WhilePaused);
            if (Phase == TrainingPhase.Completed)
                return Reject(type, EventOutcome.Duplicate);
            if (Phase != TrainingPhase.Evacuation && !IsIncidentLive(Phase))
                return Reject(type, EventOutcome.WrongPhase, detail: "no incident yet");

            string detail = null;
            if (Phase == TrainingPhase.Practice)
            {
                Practice = PracticeResult.Abandoned;
                detail = "left practice for the exit";
            }
            else if (Decision == DecisionPath.None)
            {
                Decision = DecisionPath.Evacuate;
                DecisionWasImplicit = true;
                detail = "went straight to the exit";
            }

            ReachedSafeZone = true;
            if (Emit(type, EventOutcome.Accepted, detail: detail))
                EnterPhase(TrainingPhase.Completed);
            return true;
        }

        /// <summary>Ends the session from any phase (also while paused). The report marks it as ended early.</summary>
        public bool EndTraining()
        {
            const SessionEventType type = SessionEventType.TrainingEnded;
            if (Phase == TrainingPhase.Completed)
                return Reject(type, EventOutcome.WrongPhase, detail: "already completed");

            int version = sessionVersion;
            if (IsPaused)
            {
                IsPaused = false;
                PauseChanged?.Invoke(false);
                if (version != sessionVersion)
                    return true;
            }

            EndedEarly = true;
            if (Emit(type, EventOutcome.Accepted))
                EnterPhase(TrainingPhase.Completed);
            return true;
        }

        // ---------------------------------------------------------------- interaction events

        /// <summary>
        /// Logged for every item. Grabbing the extinguisher during Incident/Decision counts as
        /// choosing practice when the rules say so.
        /// </summary>
        public bool NotifyGrabbed(string itemId)
        {
            const SessionEventType type = SessionEventType.ItemGrabbed;
            if (IsPaused)
                return Reject(type, EventOutcome.WhilePaused, itemId);
            if (string.IsNullOrWhiteSpace(itemId))
                return Reject(type, EventOutcome.UnknownId, itemId);

            if (!Emit(type, EventOutcome.Accepted, itemId))
                return true;

            if (IsExtinguisher(itemId) && Rules.GrabbingExtinguisherChoosesPractice &&
                (Phase == TrainingPhase.Incident || Phase == TrainingPhase.Decision))
                return ChoosePracticeCore(true);
            return true;
        }

        public bool NotifyReleased(string itemId)
        {
            const SessionEventType type = SessionEventType.ItemReleased;
            if (IsPaused)
                return Reject(type, EventOutcome.WhilePaused, itemId);
            if (string.IsNullOrWhiteSpace(itemId))
                return Reject(type, EventOutcome.UnknownId, itemId);
            Emit(type, EventOutcome.Accepted, itemId);
            return true;
        }

        /// <summary>
        /// Extinguisher use counts only inside the permitted practice. Using it while the incident is
        /// live outside that branch is recorded as an unsafe action and never rewarded.
        /// </summary>
        public bool NotifyUsed(string itemId)
        {
            const SessionEventType type = SessionEventType.ItemUsed;
            if (IsPaused)
                return Reject(type, EventOutcome.WhilePaused, itemId);
            if (string.IsNullOrWhiteSpace(itemId))
                return Reject(type, EventOutcome.UnknownId, itemId);
            if (!IsExtinguisher(itemId) || Phase == TrainingPhase.Practice)
            {
                Emit(type, EventOutcome.Accepted, itemId);
                return true;
            }

            bool fireStillLive = IsIncidentLive(Phase) ||
                                 (Phase == TrainingPhase.Evacuation && Practice != PracticeResult.Succeeded);
            if (!fireStillLive)
                return Reject(type, EventOutcome.WrongPhase, itemId, "no live incident");

            UnsafeActionCount++;
            return Reject(type, EventOutcome.NotPermitted, itemId, "extinguisher used outside the permitted practice");
        }

        // ---------------------------------------------------------------- cross-cutting

        public bool Pause()
        {
            if (Phase == TrainingPhase.Completed)
                return Reject(SessionEventType.Paused, EventOutcome.WrongPhase);
            if (IsPaused)
                return Reject(SessionEventType.Paused, EventOutcome.Duplicate);

            IsPaused = true;
            if (Emit(SessionEventType.Paused, EventOutcome.Accepted))
                PauseChanged?.Invoke(true);
            return true;
        }

        public bool Resume()
        {
            if (!IsPaused)
                return Reject(SessionEventType.Resumed, EventOutcome.Duplicate);

            IsPaused = false;
            if (Emit(SessionEventType.Resumed, EventOutcome.Accepted))
                PauseChanged?.Invoke(false);
            return true;
        }

        /// <summary>
        /// Always available. Clears the whole session (log included) and returns to Introduction, unpaused.
        /// Raises PauseChanged(false) if paused, PhaseChanged if not in Introduction, then SessionReset.
        /// </summary>
        public void Reset()
        {
            bool wasPaused = IsPaused;
            TrainingPhase previous = Phase;

            sessionVersion++;
            int version = sessionVersion;
            events.Clear();
            confirmedHazards.Clear();
            confirmedSet.Clear();
            Phase = TrainingPhase.Introduction;
            IsPaused = false;
            Decision = DecisionPath.None;
            DecisionWasImplicit = false;
            Practice = PracticeResult.NotAttempted;
            ReachedSafeZone = false;
            EndedEarly = false;
            UnsafeActionCount = 0;
            startTime = clock();
            endTime = null;

            if (wasPaused)
            {
                PauseChanged?.Invoke(false);
                if (version != sessionVersion)
                    return;
            }
            if (previous != TrainingPhase.Introduction)
            {
                PhaseChanged?.Invoke(previous, TrainingPhase.Introduction);
                if (version != sessionVersion)
                    return;
            }
            SessionReset?.Invoke();
        }

        /// <summary>Report for the current state; callable at any time (e.g. for a progress view).</summary>
        public TrainingReport BuildReport() => AssessmentService.Evaluate(this);

        // ---------------------------------------------------------------- helpers

        private static bool IsIncidentLive(TrainingPhase phase) =>
            phase == TrainingPhase.Incident || phase == TrainingPhase.Decision || phase == TrainingPhase.Practice;

        private bool IsExtinguisher(string itemId) => string.Equals(itemId, Rules.ExtinguisherItemId, StringComparison.Ordinal);

        private void EnterPhase(TrainingPhase next)
        {
            if (Phase == next)
                return;
            TrainingPhase previous = Phase;
            Phase = next;
            if (next == TrainingPhase.Completed)
                endTime = clock();
            if (Emit(SessionEventType.PhaseChanged, EventOutcome.Accepted, next.ToString(), "from " + previous))
                PhaseChanged?.Invoke(previous, next);
        }

        private bool Reject(SessionEventType type, EventOutcome outcome, string subject = null, string detail = null)
        {
            Emit(type, outcome, subject, detail);
            return false;
        }

        /// <summary>Appends to the log and notifies. Returns false if a listener reset the session.</summary>
        private bool Emit(SessionEventType type, EventOutcome outcome, string subject = null, string detail = null)
        {
            int version = sessionVersion;
            var entry = new SessionEvent(events.Count + 1, ElapsedSeconds, type, Phase, outcome, subject, detail);
            events.Add(entry);
            EventRecorded?.Invoke(entry);
            return version == sessionVersion;
        }
    }
}
