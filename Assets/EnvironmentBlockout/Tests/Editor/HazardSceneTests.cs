using System.Collections;
using NUnit.Framework;
using OfficeTraining.Environment;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace OfficeTraining.Tests
{
    public sealed class HazardSceneTests
    {
        [UnityTest]
        public IEnumerator SavedOfficeSceneHazardsAndIncidentResetIndependentlyInPlayMode()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/OfficeBlockout.unity");
            yield return new EnterPlayMode();
            var registry = Object.FindFirstObjectByType<HazardRegistry>();
            Assert.That(registry, Is.Not.Null);
            Assert.That(registry.IsConfigurationValid, Is.True);
            Assert.That(registry.HazardCount, Is.EqualTo(3));
            Assert.That(registry.ConfirmedCount, Is.Zero);
            var incident = Object.FindFirstObjectByType<IncidentController>();
            Assert.That(incident, Is.Not.Null);
            Assert.That(incident.CurrentState, Is.EqualTo(IncidentState.Inactive));
            foreach (var hazard in registry.Hazards)
            {
                var outline = hazard.transform.Find("Confirmation outline - shown after discovery");
                Assert.That(outline, Is.Not.Null);
                Assert.That(outline.gameObject.activeSelf, Is.False);
                Assert.That(registry.TryConfirmHazard(hazard.HazardId), Is.True);
                Assert.That(outline.gameObject.activeSelf, Is.True);
                Assert.That(registry.TryConfirmHazard(hazard.HazardId), Is.False);
            }
            // Let another frame run to detect lifecycle methods undoing the confirmed state.
            yield return null;
            Assert.That(registry.ConfirmedCount, Is.EqualTo(3));
            // Hazard completion never starts the incident by itself; the coordinator owns that.
            Assert.That(incident.CurrentState, Is.EqualTo(IncidentState.Inactive));
            Assert.That(incident.TrySetState(IncidentState.Active), Is.True);
            Assert.That(incident.TrySetState(IncidentState.Active), Is.False);
            incident.SetPaused(true);
            Assert.That(incident.TrySetState(IncidentState.Resolved), Is.False);
            incident.ResetIncident();
            Assert.That(incident.CurrentState, Is.EqualTo(IncidentState.Inactive));
            Assert.That(incident.IsPaused, Is.False);
            Assert.That(registry.ConfirmedCount, Is.EqualTo(3));
            Assert.That(incident.TrySetState(IncidentState.Active), Is.True);
            Assert.That(incident.TrySetState(IncidentState.Resolved), Is.True);
            Assert.That(incident.TrySetState(IncidentState.EvacuationRequired), Is.True);
            registry.SetPaused(true);
            registry.ResetHazards();
            foreach (var hazard in registry.Hazards)
            {
                Assert.That(hazard.IsConfirmed, Is.False);
                Assert.That(hazard.transform.Find("Confirmation outline - shown after discovery").gameObject.activeSelf, Is.False);
            }
            Assert.That(registry.ConfirmedCount, Is.Zero);
            Assert.That(registry.IsPaused, Is.False);
            Assert.That(incident.CurrentState, Is.EqualTo(IncidentState.EvacuationRequired));
            incident.ResetIncident();
            yield return new ExitPlayMode();
        }
    }
}
