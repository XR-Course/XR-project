using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OfficeTraining.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace OfficeTraining.Tests
{
    public sealed class HazardRegistryTests
    {
        private GameObject root;
        private HazardRegistry registry;
        private HazardObject[] hazards;
        private readonly List<HazardDefinition> definitions = new List<HazardDefinition>();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Hazard test");
            root.SetActive(false);
            string[] ids = { "power_strip", "heater_papers", "damaged_cable" };
            hazards = new HazardObject[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                var definition = ScriptableObject.CreateInstance<HazardDefinition>();
                definitions.Add(definition);
                SetField(definition, "hazardId", ids[i]);
                var obj = new GameObject(ids[i]);
                obj.transform.SetParent(root.transform);
                hazards[i] = obj.AddComponent<HazardObject>();
                var indicator = new GameObject("Confirmation outline");
                indicator.transform.SetParent(obj.transform);
                indicator.SetActive(false);
                SetField(hazards[i], "definition", definition);
                SetField(hazards[i], "confirmedVisual", indicator);
            }
            registry = root.AddComponent<HazardRegistry>();
            var serialized = new SerializedObject(registry);
            var list = serialized.FindProperty("hazards");
            list.arraySize = hazards.Length;
            for (int i = 0; i < hazards.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = hazards[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            foreach (var definition in definitions)
                Object.DestroyImmediate(definition);
            definitions.Clear();
        }

        [Test]
        public void AllHazardsConfirmOnceWithStateVisibleBeforeEvent()
        {
            var events = new List<string>();
            registry.OnHazardConfirmed += id =>
            {
                events.Add(id);
                Assert.That(registry.ConfirmedIds, Does.Contain(id));
                Assert.That(registry.ConfirmedCount, Is.EqualTo(events.Count));
            };
            foreach (var hazard in hazards)
            {
                Assert.That(registry.TryConfirmHazard(hazard.HazardId), Is.True);
                Assert.That(registry.TryConfirmHazard(hazard.HazardId), Is.False);
                Assert.That(hazard.IsConfirmed, Is.True);
                Assert.That(hazard.transform.GetChild(0).gameObject.activeSelf, Is.True);
            }
            Assert.That(events, Is.EqualTo(new[] { "power_strip", "heater_papers", "damaged_cable" }));
            Assert.That(registry.HazardCount, Is.EqualTo(3));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("unknown")]
        [TestCase("POWER_STRIP")]
        [TestCase(" power_strip")]
        public void InvalidIdsDoNotChangeStateOrEmitEvents(string id)
        {
            int events = 0;
            registry.OnHazardConfirmed += _ => events++;
            Assert.That(registry.TryConfirmHazard(id), Is.False);
            Assert.That(registry.ConfirmedCount, Is.Zero);
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void PauseAndInspectionGateConfirmation()
        {
            registry.SetPaused(true);
            Assert.That(registry.TryConfirmHazard("power_strip"), Is.False);
            registry.SetPaused(false);
            registry.SetInspectionEnabled(false);
            Assert.That(registry.TryConfirmHazard("power_strip"), Is.False);
            registry.SetInspectionEnabled(true);
            Assert.That(registry.TryConfirmHazard("power_strip"), Is.True);
        }

        [Test]
        public void ResetClearsFeedbackWithoutConfirmationEventsAndAllowsAnotherSession()
        {
            int confirmations = 0, resets = 0;
            registry.OnHazardConfirmed += _ => confirmations++;
            registry.OnHazardsReset += () => resets++;
            Assert.That(registry.TryConfirmHazard("power_strip"), Is.True);
            registry.SetPaused(true);
            registry.SetInspectionEnabled(false);
            registry.ResetHazards();
            registry.ResetHazards();
            Assert.That(registry.ConfirmedCount, Is.Zero);
            Assert.That(registry.ConfirmedIds, Is.Empty);
            Assert.That(hazards[0].IsConfirmed, Is.False);
            Assert.That(hazards[0].transform.GetChild(0).gameObject.activeSelf, Is.False);
            Assert.That(registry.IsPaused, Is.False);
            Assert.That(registry.InspectionEnabled, Is.True);
            Assert.That(confirmations, Is.EqualTo(1));
            Assert.That(resets, Is.EqualTo(2));
            Assert.That(registry.TryConfirmHazard("power_strip"), Is.True);
            Assert.That(confirmations, Is.EqualTo(2));
        }

        [Test]
        public void DisabledTargetsAndRegistryRejectConfirmation()
        {
            hazards[0].enabled = false;
            Assert.That(registry.TryConfirmHazard("power_strip"), Is.False);
            hazards[0].enabled = true;
            hazards[0].gameObject.SetActive(false);
            Assert.That(registry.TryConfirmHazard("power_strip"), Is.False);
            hazards[0].gameObject.SetActive(true);
            registry.enabled = false;
            Assert.That(registry.TryConfirmHazard("power_strip"), Is.False);
            registry.enabled = true;
            Assert.That(registry.TryConfirmHazard("power_strip"), Is.True);
        }

        [Test]
        public void DestroyedTargetRejectsConfirmationAndResetIsSafe()
        {
            Assert.That(registry.IsConfigurationValid, Is.True);
            Object.DestroyImmediate(hazards[0].gameObject);
            Assert.That(registry.TryConfirmHazard("power_strip"), Is.False);
            Assert.DoesNotThrow(() => registry.ResetHazards());
        }

        [Test]
        public void DuplicateIdsDisableAllConfirmations()
        {
            SetField(definitions[1], "hazardId", "power_strip");
            LogAssert.Expect(LogType.Error, new Regex("HazardRegistry requires"));
            Assert.That(registry.IsConfigurationValid, Is.False);
            Assert.That(registry.TryConfirmHazard("damaged_cable"), Is.False);
        }

        [Test]
        public void MissingDefinitionDisablesAllConfirmations()
        {
            SetField(hazards[1], "definition", (Object)null);
            LogAssert.Expect(LogType.Error, new Regex("HazardRegistry requires"));
            Assert.That(registry.IsConfigurationValid, Is.False);
            Assert.That(registry.TryConfirmHazard("power_strip"), Is.False);
        }

        [Test]
        public void PublishedCollectionsCannotBeMutatedAndReflectReset()
        {
            var ids = (IList<string>)registry.ConfirmedIds;
            Assert.That(ids.IsReadOnly, Is.True);
            Assert.Throws<System.NotSupportedException>(() => ids.Add("fake"));
            var entries = (IList<HazardObject>)registry.Hazards;
            Assert.That(entries.IsReadOnly, Is.True);
            Assert.Throws<System.NotSupportedException>(() => entries[0] = null);
            registry.TryConfirmHazard("power_strip");
            Assert.That(ids, Does.Contain("power_strip"));
            registry.ResetHazards();
            Assert.That(ids, Is.Empty);
        }

        private static void SetField(Object target, string name, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetField(Object target, string name, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
