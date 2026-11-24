using System.Collections.Generic;
using NUnit.Framework;
using OfficeTraining.Interaction.Integration;
using OfficeTraining.Scenario;
using UnityEditor;
using UnityEngine;

namespace OfficeTraining.Interaction.Tests
{
    /// <summary>
    /// Interaction events published on the channel reach Student 3's coordinator (mock scene: no
    /// HazardRegistry, so the coordinator uses the three default hazard ids).
    /// </summary>
    public sealed class ScenarioInteractionBridgeTests
    {
        private readonly List<Object> created = new List<Object>();
        private InteractionEvents events;
        private ScenarioCoordinator coordinator;

        [SetUp]
        public void SetUp()
        {
            events = ScriptableObject.CreateInstance<InteractionEvents>();
            created.Add(events);
            var root = new GameObject("Bridge test");
            created.Add(root);
            coordinator = root.AddComponent<ScenarioCoordinator>();
            var serialized = new SerializedObject(coordinator);
            serialized.FindProperty("logEvents").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<ScenarioInteractionBridge>().Initialize(coordinator, events);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created)
                if (obj != null)
                    Object.DestroyImmediate(obj);
            created.Clear();
        }

        private void InspectAllHazards()
        {
            foreach (HazardInfo hazard in ScenarioRules.DefaultHazards)
                events.RaiseInspected(hazard.Id);
        }

        [Test]
        public void InspectionEventsConfirmHazards()
        {
            coordinator.StartTraining();
            events.RaiseInspected("power_strip");
            events.RaiseInspected("power_strip");
            events.RaiseInspected("not_a_hazard");
            Assert.AreEqual(1, coordinator.HazardsFound);
        }

        [Test]
        public void PracticePathCompletesThroughInteractionEvents()
        {
            coordinator.StartTraining();
            InspectAllHazards();
            Assert.AreEqual(TrainingPhase.Incident, coordinator.CurrentPhase);

            events.RaiseGrabbed(ScenarioRules.DefaultExtinguisherItemId);
            Assert.AreEqual(TrainingPhase.Practice, coordinator.CurrentPhase);

            events.RaiseUse(ScenarioRules.DefaultExtinguisherItemId, Pose.identity);
            events.RaisePracticeResult(ScenarioRules.DefaultExtinguisherItemId, true);
            Assert.AreEqual(TrainingPhase.Evacuation, coordinator.CurrentPhase);
            Assert.AreEqual(PracticeResult.Succeeded, coordinator.Session.Practice);

            events.RaiseZoneEntered("assembly_point");
            Assert.AreEqual(TrainingPhase.Completed, coordinator.CurrentPhase);
            Assert.AreEqual(0, coordinator.Session.UnsafeActionCount);
        }

        [Test]
        public void EvacuationFirstOnlyNeedsTheAssemblyPoint()
        {
            coordinator.StartTraining();
            InspectAllHazards();
            events.RaiseZoneEntered("some_other_zone");
            Assert.AreEqual(TrainingPhase.Incident, coordinator.CurrentPhase);

            events.RaiseZoneEntered("assembly_point");
            Assert.AreEqual(TrainingPhase.Completed, coordinator.CurrentPhase);
            Assert.AreEqual(DecisionPath.Evacuate, coordinator.Session.Decision);
        }

        [Test]
        public void PracticeResultOutsidePracticeIsNotRewarded()
        {
            coordinator.StartTraining();
            events.RaisePracticeResult(ScenarioRules.DefaultExtinguisherItemId, true);
            Assert.AreEqual(TrainingPhase.Inspection, coordinator.CurrentPhase);
            Assert.AreEqual(PracticeResult.NotAttempted, coordinator.Session.Practice);
        }

        [Test]
        public void ResultsFromOtherItemsAreIgnored()
        {
            coordinator.StartTraining();
            InspectAllHazards();
            events.RaiseGrabbed(ScenarioRules.DefaultExtinguisherItemId);
            events.RaisePracticeResult("mug", true);
            Assert.AreEqual(TrainingPhase.Practice, coordinator.CurrentPhase);
        }

        [Test]
        public void UsingTheExtinguisherOutsidePracticeIsUnsafe()
        {
            coordinator.StartTraining();
            InspectAllHazards();
            coordinator.ChooseEvacuate();
            events.RaiseUse(ScenarioRules.DefaultExtinguisherItemId, Pose.identity);
            Assert.AreEqual(1, coordinator.Session.UnsafeActionCount);
        }

        [Test]
        public void MenuButtonTogglesPause()
        {
            coordinator.StartTraining();
            events.RaiseMenuPressed();
            Assert.IsTrue(coordinator.IsPaused);
            events.RaiseInspected("power_strip");
            Assert.AreEqual(0, coordinator.HazardsFound, "Inspection is ignored while paused.");
            events.RaiseMenuPressed();
            Assert.IsFalse(coordinator.IsPaused);
        }

        [Test]
        public void ReinitializingDoesNotDoubleForward()
        {
            var bridge = coordinator.GetComponent<ScenarioInteractionBridge>();
            bridge.Initialize(coordinator, events);
            coordinator.StartTraining();
            events.RaiseGrabbed("mug");
            int grabs = 0;
            foreach (SessionEvent entry in coordinator.Session.Events)
                if (entry.Type == SessionEventType.ItemGrabbed)
                    grabs++;
            Assert.AreEqual(1, grabs);
        }
    }
}
