using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace OfficeTraining.Scenario.Tests
{
    public sealed class TrainingStateMachineTests
    {
        private const string Extinguisher = ScenarioRules.DefaultExtinguisherItemId;
        private ManualClock clock;
        private TrainingStateMachine session;
        private List<(TrainingPhase previous, TrainingPhase next)> phaseChanges;

        private void Create(bool practicePermitted = true, ScenarioRules rules = null)
        {
            clock = new ManualClock();
            session = new TrainingStateMachine(rules ?? ScenarioRules.CreateDefault(practicePermitted), clock.AsFunc());
            phaseChanges = new List<(TrainingPhase, TrainingPhase)>();
            session.PhaseChanged += (previous, next) => phaseChanges.Add((previous, next));
        }

        [SetUp]
        public void SetUp() => Create();

        private void ConfirmAll()
        {
            session.ConfirmHazard("power_strip");
            session.ConfirmHazard("heater_papers");
            session.ConfirmHazard("damaged_cable");
        }

        private SessionEvent LastEvent => session.Events[session.Events.Count - 1];

        private SessionEvent LastOf(SessionEventType type) => session.Events.Last(e => e.Type == type);

        // ------------------------------------------------------------ normal paths

        [Test]
        public void NormalCompletionWithPermittedPractice()
        {
            Assert.AreEqual(TrainingPhase.Introduction, session.Phase);
            Assert.IsTrue(session.StartTraining());
            Assert.AreEqual(TrainingPhase.Inspection, session.Phase);

            ConfirmAll();
            Assert.AreEqual(TrainingPhase.Incident, session.Phase, "all hazards found auto-starts the incident");

            Assert.IsTrue(session.AcknowledgeIncident());
            Assert.AreEqual(TrainingPhase.Decision, session.Phase);
            Assert.IsTrue(session.ChoosePractice());
            Assert.AreEqual(TrainingPhase.Practice, session.Phase);
            Assert.IsTrue(session.NotifyUsed(Extinguisher));
            Assert.IsTrue(session.ReportPracticeResult(true));
            Assert.AreEqual(TrainingPhase.Evacuation, session.Phase);
            Assert.IsTrue(session.ReachSafeZone());

            Assert.AreEqual(TrainingPhase.Completed, session.Phase);
            Assert.AreEqual(DecisionPath.PracticeInPermittedCondition, session.Decision);
            Assert.AreEqual(PracticeResult.Succeeded, session.Practice);
            Assert.AreEqual(0, session.UnsafeActionCount);
            CollectionAssert.AreEqual(new[]
            {
                TrainingPhase.Inspection, TrainingPhase.Incident, TrainingPhase.Decision,
                TrainingPhase.Practice, TrainingPhase.Evacuation, TrainingPhase.Completed
            }, phaseChanges.Select(c => c.next));

            TrainingReport report = session.BuildReport();
            Assert.IsTrue(report.Completed);
            Assert.AreEqual(4, report.ObjectivesApplicable);
            Assert.AreEqual(4, report.ObjectivesAchieved);
        }

        [Test]
        public void EvacuationFirstSkipsPracticeAndPassesDecision()
        {
            session.StartTraining();
            ConfirmAll();
            Assert.IsTrue(session.ChooseEvacuate(), "evacuation is accepted directly in the incident phase");
            Assert.AreEqual(TrainingPhase.Evacuation, session.Phase);
            session.ReachSafeZone();

            TrainingReport report = session.BuildReport();
            Assert.AreEqual(DecisionPath.Evacuate, report.Decision);
            Assert.IsFalse(report.DecisionWasImplicit);
            Assert.AreEqual(PracticeResult.NotAttempted, report.Practice);
            Assert.AreEqual(ObjectiveStatus.Achieved, report.GetObjective(AssessmentService.ObjectiveIds.Decision).Status);
            Assert.AreEqual(ObjectiveStatus.NotApplicable, report.GetObjective(AssessmentService.ObjectiveIds.Practice).Status);
            Assert.AreEqual(3, report.ObjectivesAchieved);
            Assert.AreEqual(3, report.ObjectivesApplicable);
        }

        [Test]
        public void WalkingStraightToTheExitCountsAsImplicitEvacuation()
        {
            session.StartTraining();
            session.FinishInspection();
            Assert.IsTrue(session.ReachSafeZone());
            Assert.AreEqual(TrainingPhase.Completed, session.Phase);
            Assert.AreEqual(DecisionPath.Evacuate, session.Decision);
            Assert.IsTrue(session.DecisionWasImplicit);
        }

        [Test]
        public void FinishInspectionWithMissedHazardsStillContinues()
        {
            session.StartTraining();
            session.ConfirmHazard("heater_papers");
            Assert.IsTrue(session.FinishInspection());
            Assert.AreEqual(TrainingPhase.Incident, session.Phase);

            TrainingReport report = session.BuildReport();
            CollectionAssert.AreEqual(new[] { "heater_papers" }, report.HazardsFound.Select(h => h.Id));
            CollectionAssert.AreEqual(new[] { "power_strip", "damaged_cable" }, report.HazardsMissed.Select(h => h.Id));
            Assert.AreEqual(ObjectiveStatus.Missed, report.GetObjective(AssessmentService.ObjectiveIds.Hazards).Status);
        }

        [Test]
        public void AutoAdvanceCanBeDisabled()
        {
            Create(rules: new ScenarioRules(ScenarioRules.DefaultHazards, true, autoAdvanceWhenAllHazardsFound: false));
            session.StartTraining();
            ConfirmAll();
            Assert.AreEqual(TrainingPhase.Inspection, session.Phase);
            Assert.IsTrue(session.FinishInspection());
            Assert.AreEqual(TrainingPhase.Incident, session.Phase);
        }

        // ------------------------------------------------------------ unsafe actions

        [Test]
        public void ChoosingPracticeWhenNotPermittedIsUnsafeAndSendsToEvacuation()
        {
            Create(practicePermitted: false);
            session.StartTraining();
            ConfirmAll();

            Assert.IsFalse(session.ChoosePractice());
            Assert.AreEqual(EventOutcome.NotPermitted, LastOf(SessionEventType.PracticeChosen).Outcome);
            Assert.AreEqual(TrainingPhase.Evacuation, session.Phase, "never enters the practice branch");
            Assert.AreEqual(DecisionPath.PracticeWhenNotPermitted, session.Decision);
            Assert.AreEqual(PracticeResult.NotPermitted, session.Practice);
            Assert.AreEqual(1, session.UnsafeActionCount);

            Assert.IsFalse(session.ReportPracticeResult(true), "a practice success outside the branch is not rewarded");
            Assert.AreEqual(PracticeResult.NotPermitted, session.Practice);

            Assert.IsTrue(session.ReachSafeZone(), "evacuation route remains available");
            TrainingReport report = session.BuildReport();
            Assert.AreEqual(ObjectiveStatus.Missed, report.GetObjective(AssessmentService.ObjectiveIds.Decision).Status);
            Assert.AreEqual(ObjectiveStatus.Achieved, report.GetObjective(AssessmentService.ObjectiveIds.Evacuation).Status);
        }

        [Test]
        public void GrabbingExtinguisherDuringIncidentChoosesPractice()
        {
            session.StartTraining();
            ConfirmAll();
            Assert.IsTrue(session.NotifyGrabbed(Extinguisher));
            Assert.AreEqual(TrainingPhase.Practice, session.Phase);
            Assert.IsTrue(session.DecisionWasImplicit);
        }

        [Test]
        public void GrabbingExtinguisherWhenNotPermittedIsUnsafe()
        {
            Create(practicePermitted: false);
            session.StartTraining();
            ConfirmAll();
            Assert.IsFalse(session.NotifyGrabbed(Extinguisher));
            Assert.AreEqual(TrainingPhase.Evacuation, session.Phase);
            Assert.AreEqual(DecisionPath.PracticeWhenNotPermitted, session.Decision);
        }

        [Test]
        public void GrabbingExtinguisherDuringInspectionIsOnlyLogged()
        {
            session.StartTraining();
            Assert.IsTrue(session.NotifyGrabbed(Extinguisher));
            Assert.AreEqual(TrainingPhase.Inspection, session.Phase);
            Assert.AreEqual(DecisionPath.None, session.Decision);
        }

        [Test]
        public void GrabCanBeConfiguredNotToChoosePractice()
        {
            Create(rules: new ScenarioRules(ScenarioRules.DefaultHazards, true, grabbingExtinguisherChoosesPractice: false));
            session.StartTraining();
            ConfirmAll();
            session.NotifyGrabbed(Extinguisher);
            Assert.AreEqual(TrainingPhase.Incident, session.Phase);
        }

        [Test]
        public void UsingExtinguisherOutsidePracticeIsUnsafeAndNotRewarded()
        {
            session.StartTraining();
            ConfirmAll();
            Assert.IsFalse(session.NotifyUsed(Extinguisher));
            Assert.AreEqual(EventOutcome.NotPermitted, LastEvent.Outcome);
            Assert.AreEqual(1, session.UnsafeActionCount);

            session.ChooseEvacuate();
            Assert.IsFalse(session.NotifyUsed(Extinguisher), "still unsafe while evacuating from a live fire");
            Assert.AreEqual(2, session.UnsafeActionCount);
            session.ReachSafeZone();

            TrainingReport report = session.BuildReport();
            Assert.AreEqual(DecisionPath.Evacuate, report.Decision);
            Assert.AreEqual(ObjectiveStatus.Missed, report.GetObjective(AssessmentService.ObjectiveIds.Decision).Status);
        }

        [Test]
        public void UsingExtinguisherAfterSuccessfulPracticeIsHarmless()
        {
            session.StartTraining();
            ConfirmAll();
            session.ChoosePractice();
            session.ReportPracticeResult(true);
            Assert.IsFalse(session.NotifyUsed(Extinguisher));
            Assert.AreEqual(EventOutcome.WrongPhase, LastEvent.Outcome);
            Assert.AreEqual(0, session.UnsafeActionCount);
        }

        [Test]
        public void UsingOtherItemsIsJustLogged()
        {
            session.StartTraining();
            ConfirmAll();
            Assert.IsTrue(session.NotifyUsed("door_handle"));
            Assert.AreEqual(0, session.UnsafeActionCount);
        }

        // ------------------------------------------------------------ practice branch

        [Test]
        public void FailedPracticeGoesToEvacuation()
        {
            session.StartTraining();
            ConfirmAll();
            session.ChoosePractice();
            Assert.IsTrue(session.ReportPracticeResult(false));
            Assert.AreEqual(TrainingPhase.Evacuation, session.Phase);
            Assert.AreEqual(PracticeResult.Failed, session.Practice);
            Assert.AreEqual(ObjectiveStatus.Missed,
                session.BuildReport().GetObjective(AssessmentService.ObjectiveIds.Practice).Status);
        }

        [Test]
        public void EvacuatingDuringPracticeAbandonsIt()
        {
            session.StartTraining();
            ConfirmAll();
            session.ChoosePractice();
            Assert.IsTrue(session.ChooseEvacuate());
            Assert.AreEqual(PracticeResult.Abandoned, session.Practice);
            Assert.AreEqual(DecisionPath.PracticeInPermittedCondition, session.Decision);
            Assert.AreEqual(TrainingPhase.Evacuation, session.Phase);
        }

        [Test]
        public void ReachingExitDuringPracticeAbandonsAndCompletes()
        {
            session.StartTraining();
            ConfirmAll();
            session.ChoosePractice();
            Assert.IsTrue(session.ReachSafeZone());
            Assert.AreEqual(PracticeResult.Abandoned, session.Practice);
            Assert.AreEqual(TrainingPhase.Completed, session.Phase);
        }

        // ------------------------------------------------------------ invalid order and duplicates

        [Test]
        public void EventsBeforeStartAreRejectedAndLogged()
        {
            Assert.IsFalse(session.ConfirmHazard("power_strip"));
            Assert.IsFalse(session.ReachSafeZone());
            Assert.IsFalse(session.ChooseEvacuate());
            Assert.IsFalse(session.ChoosePractice());
            Assert.IsFalse(session.ReportPracticeResult(true));
            Assert.IsFalse(session.AcknowledgeIncident());
            Assert.IsFalse(session.FinishInspection());

            Assert.AreEqual(TrainingPhase.Introduction, session.Phase);
            Assert.AreEqual(7, session.Events.Count);
            Assert.IsTrue(session.Events.All(e => e.Outcome == EventOutcome.WrongPhase));
            Assert.IsEmpty(phaseChanges);
        }

        [Test]
        public void DuplicateAndUnknownHazardsAreRejected()
        {
            session.StartTraining();
            Assert.IsTrue(session.ConfirmHazard("power_strip"));
            Assert.IsFalse(session.ConfirmHazard("power_strip"));
            Assert.AreEqual(EventOutcome.Duplicate, LastEvent.Outcome);
            Assert.IsFalse(session.ConfirmHazard("fire_door"));
            Assert.AreEqual(EventOutcome.UnknownId, LastEvent.Outcome);
            Assert.IsFalse(session.ConfirmHazard(null));
            Assert.IsFalse(session.ConfirmHazard("Power_Strip"), "ids are case-sensitive");
            CollectionAssert.AreEqual(new[] { "power_strip" }, session.ConfirmedHazards);
        }

        [Test]
        public void HazardsAfterInspectionAreRejected()
        {
            session.StartTraining();
            session.FinishInspection();
            Assert.IsFalse(session.ConfirmHazard("power_strip"));
            Assert.AreEqual(EventOutcome.WrongPhase, LastEvent.Outcome);
            Assert.IsEmpty(session.ConfirmedHazards);
        }

        [Test]
        public void DuplicateCommandsDoNotRepeatTransitions()
        {
            session.StartTraining();
            Assert.IsFalse(session.StartTraining());
            session.FinishInspection();
            session.ChooseEvacuate();
            Assert.IsFalse(session.ChooseEvacuate());
            Assert.AreEqual(EventOutcome.Duplicate, LastEvent.Outcome);
            Assert.IsFalse(session.ChoosePractice(), "cannot switch to practice after evacuating");
            session.ReachSafeZone();
            Assert.IsFalse(session.ReachSafeZone());
            Assert.IsFalse(session.EndTraining());
            Assert.AreEqual(1, phaseChanges.Count(c => c.next == TrainingPhase.Completed));
        }

        [Test]
        public void ChoosingPracticeTwiceIsDuplicate()
        {
            session.StartTraining();
            ConfirmAll();
            session.ChoosePractice();
            Assert.IsFalse(session.ChoosePractice());
            Assert.AreEqual(EventOutcome.Duplicate, LastEvent.Outcome);
        }

        // ------------------------------------------------------------ pause, end, reset

        [Test]
        public void PauseBlocksCommandsUntilResumed()
        {
            var pauseEvents = new List<bool>();
            session.PauseChanged += pauseEvents.Add;
            session.StartTraining();
            Assert.IsTrue(session.Pause());
            Assert.IsFalse(session.Pause());

            Assert.IsFalse(session.ConfirmHazard("power_strip"));
            Assert.AreEqual(EventOutcome.WhilePaused, LastEvent.Outcome);
            Assert.IsFalse(session.FinishInspection());
            Assert.AreEqual(TrainingPhase.Inspection, session.Phase);

            Assert.IsTrue(session.Resume());
            Assert.IsFalse(session.Resume());
            Assert.IsTrue(session.ConfirmHazard("power_strip"));
            CollectionAssert.AreEqual(new[] { true, false }, pauseEvents);
        }

        [Test]
        public void EndTrainingWorksWhilePausedAndMarksEndedEarly()
        {
            var pauseEvents = new List<bool>();
            session.PauseChanged += pauseEvents.Add;
            session.StartTraining();
            session.Pause();
            Assert.IsTrue(session.EndTraining());
            Assert.IsFalse(session.IsPaused);
            Assert.AreEqual(TrainingPhase.Completed, session.Phase);
            CollectionAssert.AreEqual(new[] { true, false }, pauseEvents);

            TrainingReport report = session.BuildReport();
            Assert.IsTrue(report.EndedEarly);
            Assert.AreEqual(ObjectiveStatus.Missed, report.GetObjective(AssessmentService.ObjectiveIds.Decision).Status);
            Assert.AreEqual(ObjectiveStatus.Missed, report.GetObjective(AssessmentService.ObjectiveIds.Evacuation).Status);
            Assert.IsFalse(session.Pause(), "nothing to pause after completion");
        }

        [Test]
        public void ResetRestoresTheInitialStateFromAnyPhase()
        {
            var order = new List<string>();
            session.StartTraining();
            ConfirmAll();
            session.ChoosePractice();
            session.NotifyUsed(Extinguisher);
            session.Pause();
            session.PauseChanged += p => order.Add("pause:" + p);
            session.PhaseChanged += (previous, next) => order.Add("phase:" + next);
            session.SessionReset += () => order.Add("reset");

            session.Reset();

            Assert.AreEqual(TrainingPhase.Introduction, session.Phase);
            Assert.IsFalse(session.IsPaused);
            Assert.IsEmpty(session.Events);
            Assert.IsEmpty(session.ConfirmedHazards);
            Assert.AreEqual(DecisionPath.None, session.Decision);
            Assert.AreEqual(PracticeResult.NotAttempted, session.Practice);
            Assert.AreEqual(0, session.UnsafeActionCount);
            Assert.IsFalse(session.ReachedSafeZone);
            Assert.IsFalse(session.EndedEarly);
            CollectionAssert.AreEqual(new[] { "pause:False", "phase:Introduction", "reset" }, order);

            // A full new run works after reset.
            session.StartTraining();
            ConfirmAll();
            session.ChooseEvacuate();
            session.ReachSafeZone();
            Assert.AreEqual(3, session.BuildReport().ObjectivesAchieved);
        }

        [Test]
        public void RepeatedResetIsSafe()
        {
            int resets = 0;
            session.SessionReset += () => resets++;
            session.Reset();
            session.Reset();
            Assert.AreEqual(2, resets);
            Assert.IsEmpty(phaseChanges, "no fake phase change when already in Introduction");
        }

        [Test]
        public void ResetFromAListenerStopsTheInterruptedCommand()
        {
            session.StartTraining();
            session.ConfirmHazard("power_strip");
            session.ConfirmHazard("heater_papers");
            session.EventRecorded += e =>
            {
                if (e.Type == SessionEventType.HazardConfirmed && e.Accepted)
                    session.Reset();
            };

            session.ConfirmHazard("damaged_cable"); // would normally auto-advance to Incident

            Assert.AreEqual(TrainingPhase.Introduction, session.Phase);
            Assert.IsEmpty(session.Events);
        }

        // ------------------------------------------------------------ log and timing

        [Test]
        public void LogIsOrderedAndTimedFromStart()
        {
            clock.Advance(100); // time spent in the introduction is not counted
            session.StartTraining();
            clock.Advance(12.5);
            session.ConfirmHazard("power_strip");

            SessionEvent confirmed = LastOf(SessionEventType.HazardConfirmed);
            Assert.AreEqual(12.5, confirmed.TimeSeconds, 1e-9);
            Assert.AreEqual("power_strip", confirmed.Subject);
            Assert.AreEqual("1/3", confirmed.Detail);
            for (int i = 0; i < session.Events.Count; i++)
                Assert.AreEqual(i + 1, session.Events[i].Sequence);
        }

        [Test]
        public void DurationStopsAtCompletion()
        {
            session.StartTraining();
            clock.Advance(30);
            session.EndTraining();
            clock.Advance(500);
            Assert.AreEqual(30, session.BuildReport().DurationSeconds, 1e-9);
        }

        [Test]
        public void EmptyHazardListCanStillBeCompleted()
        {
            Create(rules: new ScenarioRules(new HazardInfo[0], true));
            session.StartTraining();
            Assert.IsTrue(session.FinishInspection());
            session.ChooseEvacuate();
            session.ReachSafeZone();
            TrainingReport report = session.BuildReport();
            Assert.AreEqual(ObjectiveStatus.NotApplicable, report.GetObjective(AssessmentService.ObjectiveIds.Hazards).Status);
            Assert.AreEqual(2, report.ObjectivesAchieved);
        }
    }
}
