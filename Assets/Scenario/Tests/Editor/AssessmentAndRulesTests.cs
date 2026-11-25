using System;
using System.Linq;
using NUnit.Framework;

namespace OfficeTraining.Scenario.Tests
{
    public sealed class AssessmentAndRulesTests
    {
        [Test]
        public void RulesRejectInvalidConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() => new ScenarioRules(null, true));
            Assert.Throws<ArgumentException>(() => new ScenarioRules(new[] { new HazardInfo(" ", "x") }, true));
            Assert.Throws<ArgumentException>(() =>
                new ScenarioRules(new[] { new HazardInfo("a", "A"), new HazardInfo("a", "B") }, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScenarioRules(ScenarioRules.DefaultHazards, true, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScenarioRules(ScenarioRules.DefaultHazards, true, -2));
            Assert.Throws<ArgumentException>(() =>
                new ScenarioRules(ScenarioRules.DefaultHazards, true, extinguisherItemId: ""));
        }

        [Test]
        public void DefaultRulesMatchEnvironmentHazardIds()
        {
            ScenarioRules rules = ScenarioRules.CreateDefault();
            CollectionAssert.AreEqual(new[] { "power_strip", "heater_papers", "damaged_cable" }, rules.Hazards.Select(h => h.Id));
            Assert.AreEqual(3, rules.RequiredHazardCount);
            Assert.AreEqual("Heater and papers", rules.DisplayNameFor("heater_papers"));
        }

        [Test]
        public void PartialRequirementPassesWithMissedHazardsListed()
        {
            var session = new TrainingStateMachine(new ScenarioRules(ScenarioRules.DefaultHazards, true, requiredHazardCount: 2));
            session.StartTraining();
            session.ConfirmHazard("power_strip");
            session.ConfirmHazard("damaged_cable");
            session.FinishInspection();

            ObjectiveResult hazards = session.BuildReport().GetObjective(AssessmentService.ObjectiveIds.Hazards);
            Assert.AreEqual(ObjectiveStatus.Achieved, hazards.Status);
            StringAssert.Contains("Heater and papers", hazards.Feedback);
            StringAssert.Contains("at least 2 of the 3", hazards.Criterion);
        }

        [Test]
        public void ReportIsASnapshot()
        {
            var session = new TrainingStateMachine(ScenarioRules.CreateDefault());
            session.StartTraining();
            TrainingReport report = session.BuildReport();
            int count = report.Events.Count;
            session.ConfirmHazard("power_strip");
            Assert.AreEqual(count, report.Events.Count);
            Assert.IsFalse(report.Completed);
        }

        [Test]
        public void EveryObjectiveHasCriterionAndFeedback()
        {
            foreach (ScenarioSimulation.Script script in ScenarioSimulation.Scripts)
            {
                TrainingReport report = ScenarioSimulation.Run(script);
                Assert.AreEqual(4, report.Objectives.Count, script.Name);
                foreach (ObjectiveResult objective in report.Objectives)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(objective.Criterion), script.Name);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(objective.Feedback), script.Name);
                    StringAssert.DoesNotContain("{", objective.Feedback, "unformatted template in " + script.Name);
                }
                StringAssert.Contains("not certified", report.ToText());
            }
        }

        [Test]
        public void MockSimulationsProduceExpectedOutcomes()
        {
            TrainingReport Run(int index) => ScenarioSimulation.Run(ScenarioSimulation.Scripts[index]);

            TrainingReport normal = Run(0);
            Assert.AreEqual(4, normal.ObjectivesAchieved);
            Assert.AreEqual(4, normal.ObjectivesApplicable);

            TrainingReport evacuateFirst = Run(1);
            Assert.AreEqual(DecisionPath.Evacuate, evacuateFirst.Decision);
            Assert.AreEqual(1, evacuateFirst.HazardsMissed.Count);

            TrainingReport unsafeRun = Run(2);
            Assert.AreEqual(DecisionPath.PracticeWhenNotPermitted, unsafeRun.Decision);
            Assert.AreEqual(2, unsafeRun.UnsafeActionCount);
            Assert.IsTrue(unsafeRun.ReachedSafeZone);

            TrainingReport invalid = Run(3);
            Assert.IsTrue(invalid.Completed);
            Assert.IsTrue(invalid.DecisionWasImplicit);
            Assert.IsTrue(invalid.Events.Any(e => e.Outcome == EventOutcome.WhilePaused));
            Assert.IsTrue(invalid.Events.Any(e => e.Outcome == EventOutcome.Duplicate));
            Assert.IsTrue(invalid.Events.Any(e => e.Outcome == EventOutcome.UnknownId));

            TrainingReport afterReset = Run(4);
            Assert.AreEqual(0, afterReset.HazardsFound.Count, "the hazard from before the reset is gone");
            Assert.IsTrue(afterReset.ReachedSafeZone);

            TrainingReport ended = Run(5);
            Assert.IsTrue(ended.EndedEarly);
            Assert.IsFalse(ended.ReachedSafeZone);
        }
    }
}
