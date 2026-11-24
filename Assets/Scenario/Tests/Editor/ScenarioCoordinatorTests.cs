using System.Collections.Generic;
using NUnit.Framework;
using OfficeTraining.Environment;
using UnityEditor;
using UnityEngine;

namespace OfficeTraining.Scenario.Tests
{
    /// <summary>Coordinator wired to the real environment components (no scene, no headset).</summary>
    public sealed class ScenarioCoordinatorTests
    {
        private static readonly string[] HazardIds = { "power_strip", "heater_papers", "damaged_cable" };
        private readonly List<Object> created = new List<Object>();
        private HazardRegistry registry;
        private IncidentController incident;
        private ScenarioCoordinator coordinator;

        [SetUp]
        public void SetUp()
        {
            var root = new GameObject("Scenario test");
            created.Add(root);
            root.SetActive(false);

            var hazards = new HazardObject[HazardIds.Length];
            for (int i = 0; i < HazardIds.Length; i++)
            {
                var definition = ScriptableObject.CreateInstance<HazardDefinition>();
                created.Add(definition);
                SetString(definition, "hazardId", HazardIds[i]);
                SetString(definition, "displayName", HazardIds[i] + " name");
                var obj = new GameObject(HazardIds[i]);
                obj.transform.SetParent(root.transform);
                hazards[i] = obj.AddComponent<HazardObject>();
                SetObject(hazards[i], "definition", definition);
            }

            registry = root.AddComponent<HazardRegistry>();
            var serializedRegistry = new SerializedObject(registry);
            SerializedProperty list = serializedRegistry.FindProperty("hazards");
            list.arraySize = hazards.Length;
            for (int i = 0; i < hazards.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = hazards[i];
            serializedRegistry.ApplyModifiedPropertiesWithoutUndo();

            incident = root.AddComponent<IncidentController>();
            coordinator = root.AddComponent<ScenarioCoordinator>();
            SetObject(coordinator, "hazardRegistry", registry);
            SetObject(coordinator, "incidentController", incident);
            var serializedCoordinator = new SerializedObject(coordinator);
            serializedCoordinator.FindProperty("logEvents").boolValue = false;
            serializedCoordinator.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created)
                if (obj != null)
                    Object.DestroyImmediate(obj);
            created.Clear();
        }

        [Test]
        public void RulesComeFromTheRegistry()
        {
            CollectionAssert.AreEqual(HazardIds, ToIds(coordinator.Rules.Hazards));
            Assert.AreEqual("heater_papers name", coordinator.Rules.DisplayNameFor("heater_papers"));
        }

        [Test]
        public void InspectionIsOnlyEnabledDuringInspection()
        {
            Assert.AreEqual(TrainingPhase.Introduction, coordinator.CurrentPhase);
            Assert.IsFalse(registry.InspectionEnabled);
            Assert.IsFalse(registry.TryConfirmHazard("power_strip"));

            coordinator.StartTraining();
            Assert.IsTrue(registry.InspectionEnabled);
            coordinator.FinishInspection();
            Assert.IsFalse(registry.InspectionEnabled);
        }

        [Test]
        public void RegistryConfirmationsDriveTheScenarioAndIncident()
        {
            var phases = new List<TrainingPhase>();
            coordinator.OnPhaseChanged += phases.Add;
            coordinator.StartTraining();
            foreach (string id in HazardIds)
                Assert.IsTrue(coordinator.ConfirmHazard(id));

            CollectionAssert.AreEqual(HazardIds, coordinator.Session.ConfirmedHazards);
            Assert.AreEqual(TrainingPhase.Incident, coordinator.CurrentPhase);
            Assert.AreEqual(IncidentState.Active, incident.CurrentState);
            CollectionAssert.AreEqual(new[] { TrainingPhase.Inspection, TrainingPhase.Incident }, phases);
        }

        [Test]
        public void PracticeSuccessResolvesIncidentAndReportIsPublished()
        {
            TrainingReport published = null;
            coordinator.OnReportReady += report => published = report;
            coordinator.StartTraining();
            foreach (string id in HazardIds)
                coordinator.ConfirmHazard(id);
            coordinator.NotifyGrabbed(ScenarioRules.DefaultExtinguisherItemId);
            coordinator.NotifyUsed(ScenarioRules.DefaultExtinguisherItemId);
            coordinator.ReportPracticeResult(true);
            Assert.AreEqual(IncidentState.Resolved, incident.CurrentState);

            coordinator.NotifySafeZoneReached();
            Assert.IsNotNull(published);
            Assert.AreEqual(4, published.ObjectivesAchieved);
            Assert.AreEqual(IncidentState.Resolved, incident.CurrentState, "completion keeps the last presentation");
        }

        [Test]
        public void EvacuationSetsEvacuationRequired()
        {
            coordinator.StartTraining();
            coordinator.FinishInspection();
            coordinator.ChooseEvacuate();
            Assert.AreEqual(IncidentState.EvacuationRequired, incident.CurrentState);
        }

        [Test]
        public void PauseGatesAllModulesTogether()
        {
            coordinator.StartTraining();
            coordinator.Pause();
            Assert.IsTrue(registry.IsPaused);
            Assert.IsTrue(incident.IsPaused);
            Assert.IsFalse(coordinator.ConfirmHazard("power_strip"), "registry rejects while paused");

            coordinator.TogglePause();
            Assert.IsFalse(registry.IsPaused);
            Assert.IsFalse(incident.IsPaused);
            Assert.IsTrue(coordinator.ConfirmHazard("power_strip"));
        }

        [Test]
        public void ResetRestoresScenarioAndEnvironment()
        {
            int resets = 0;
            coordinator.OnSessionReset += () => resets++;
            coordinator.StartTraining();
            foreach (string id in HazardIds)
                coordinator.ConfirmHazard(id);
            coordinator.Pause();

            coordinator.ResetTraining();

            Assert.AreEqual(1, resets);
            Assert.AreEqual(TrainingPhase.Introduction, coordinator.CurrentPhase);
            Assert.AreEqual(0, registry.ConfirmedCount);
            Assert.IsFalse(registry.InspectionEnabled, "inspection stays closed until training starts again");
            Assert.IsFalse(registry.IsPaused);
            Assert.AreEqual(IncidentState.Inactive, incident.CurrentState);
            Assert.IsFalse(incident.IsPaused);

            coordinator.StartTraining();
            Assert.IsTrue(coordinator.ConfirmHazard("power_strip"), "hazards can be confirmed again after reset");
        }

        [TestCase(TrainingPhase.Introduction, PracticeResult.NotAttempted, IncidentState.Inactive)]
        [TestCase(TrainingPhase.Inspection, PracticeResult.NotAttempted, IncidentState.Inactive)]
        [TestCase(TrainingPhase.Incident, PracticeResult.NotAttempted, IncidentState.Active)]
        [TestCase(TrainingPhase.Decision, PracticeResult.NotAttempted, IncidentState.Active)]
        [TestCase(TrainingPhase.Practice, PracticeResult.NotAttempted, IncidentState.Active)]
        [TestCase(TrainingPhase.Evacuation, PracticeResult.Succeeded, IncidentState.Resolved)]
        [TestCase(TrainingPhase.Evacuation, PracticeResult.Failed, IncidentState.EvacuationRequired)]
        [TestCase(TrainingPhase.Evacuation, PracticeResult.NotPermitted, IncidentState.EvacuationRequired)]
        public void PhaseMapsToIncidentState(TrainingPhase phase, PracticeResult practice, IncidentState expected)
        {
            Assert.AreEqual(expected, ScenarioCoordinator.IncidentStateFor(phase, practice));
        }

        [Test]
        public void CompletedLeavesIncidentUnchanged()
        {
            Assert.IsNull(ScenarioCoordinator.IncidentStateFor(TrainingPhase.Completed, PracticeResult.Succeeded));
        }

        [Test]
        public void WorksWithoutEnvironmentForMockScenes()
        {
            var obj = new GameObject("Mock coordinator");
            created.Add(obj);
            var mock = obj.AddComponent<ScenarioCoordinator>();
            mock.StartTraining();
            Assert.IsTrue(mock.ConfirmHazard("power_strip"));
            Assert.AreEqual(3, mock.HazardTotal);
            Assert.AreEqual(1, mock.HazardsFound);
        }

        private static List<string> ToIds(IReadOnlyList<HazardInfo> hazards)
        {
            var ids = new List<string>();
            foreach (HazardInfo hazard in hazards)
                ids.Add(hazard.Id);
            return ids;
        }

        private static void SetString(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
