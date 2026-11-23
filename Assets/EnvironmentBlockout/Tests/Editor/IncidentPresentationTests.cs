using System.Collections;
using NUnit.Framework;
using OfficeTraining.Environment;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace OfficeTraining.Tests
{
    public sealed class IncidentPresentationTests
    {
        [UnityTest, Timeout(30000)]
        public IEnumerator SavedSceneEffectsAndAudioFollowStatePauseResetAndReenable()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/OfficeBlockout.unity");
            yield return new EnterPlayMode();
            var incident = Object.FindFirstObjectByType<IncidentController>();
            var visuals = Object.FindFirstObjectByType<FireScenarioVisuals>();
            var audio = Object.FindFirstObjectByType<EnvironmentAudio>();
            Assert.That(visuals, Is.Not.Null);
            Assert.That(audio, Is.Not.Null);
            var systems = visuals.GetComponentsInChildren<ParticleSystem>(true);
            var sources = audio.GetComponentsInChildren<AudioSource>(true);
            Assert.That(systems.Length, Is.EqualTo(2));
            Assert.That(sources.Length, Is.EqualTo(2));
            int cap = 0;
            foreach (var system in systems)
            {
                Assert.That(system.isStopped, Is.True);
                Assert.That(system.particleCount, Is.Zero);
                Assert.That(system.main.playOnAwake, Is.False);
                cap += system.main.maxParticles;
                // Batch-mode tests don't reliably render visibility frames. Override only
                // the runtime test instance, leaving the saved scene's offscreen culling intact.
                var main = system.main;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            }
            Assert.That(cap, Is.LessThanOrEqualTo(56));
            foreach (var source in sources)
            {
                Assert.That(source.clip, Is.Not.Null);
                Assert.That(source.playOnAwake, Is.False);
                Assert.That(source.spatialBlend, Is.EqualTo(1f));
                Assert.That(source.loop, Is.True);
                Assert.That(source.isPlaying, Is.False);
            }
            // This is the explicit untracked desktop camera, not an XR rig.
            var view = GameObject.Find("Incident desktop viewpoint").transform;
            Camera.main.transform.SetPositionAndRotation(view.position, view.rotation);
            Assert.That(incident.TrySetState(IncidentState.Active), Is.True);
            // This fixture is launched from EditMode and enters Play mode. Its runner
            // does not wait on WaitForSeconds; wait on actual player frames instead.
            float emissionDeadline = Time.time + .5f;
            while (Time.time < emissionDeadline)
                yield return null;
            foreach (var system in systems)
            {
                Assert.That(system.isPlaying, Is.True);
                Assert.That(system.particleCount, Is.GreaterThan(0),
                    $"{system.name}: time={system.time}, emitting={system.isEmitting}, active={system.gameObject.activeInHierarchy}, rate={system.emission.rateOverTime.constant}, timeScale={Time.timeScale}");
            }
            foreach (var source in sources)
                Assert.That(source.isPlaying, Is.True);

            incident.SetPaused(true);
            foreach (var system in systems)
                Assert.That(system.isPaused, Is.True);
            foreach (var source in sources)
                Assert.That(source.isPlaying, Is.False);
            float pausedTime = systems[0].time;
            float pauseDeadline = Time.time + .1f;
            while (Time.time < pauseDeadline)
                yield return null;
            Assert.That(systems[0].time, Is.EqualTo(pausedTime).Within(.001f));
            incident.SetPaused(false);
            foreach (var system in systems)
                Assert.That(system.isPlaying, Is.True);
            foreach (var source in sources)
                Assert.That(source.isPlaying, Is.True);

            Assert.That(incident.TrySetState(IncidentState.EvacuationRequired), Is.True);
            Assert.That(systems[0].isPlaying, Is.True);
            Assert.That(incident.TrySetState(IncidentState.Resolved), Is.True);
            foreach (var system in systems)
            {
                Assert.That(system.isStopped, Is.True);
                Assert.That(system.particleCount, Is.Zero);
            }
            foreach (var source in sources)
                Assert.That(source.isPlaying, Is.False);

            incident.TrySetState(IncidentState.Active);
            visuals.enabled = false;
            audio.enabled = false;
            foreach (var system in systems)
                Assert.That(system.particleCount, Is.Zero);
            foreach (var source in sources)
                Assert.That(source.isPlaying, Is.False);
            incident.SetPaused(true);
            visuals.enabled = true;
            audio.enabled = true;
            foreach (var system in systems)
                Assert.That(system.isPaused, Is.True);
            foreach (var source in sources)
                Assert.That(source.isPlaying, Is.False);

            incident.ResetIncident();
            foreach (var system in systems)
            {
                Assert.That(system.isStopped, Is.True);
                Assert.That(system.particleCount, Is.Zero);
            }
            // Even a reset with no state change clears presentation leftovers.
            systems[0].Emit(2);
            Assert.That(systems[0].particleCount, Is.EqualTo(2));
            incident.ResetIncident();
            Assert.That(systems[0].particleCount, Is.Zero);
            foreach (var source in sources)
                Assert.That(source.isPlaying, Is.False);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ConfirmationChimeIsSpatialAndResetStopsIt()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/OfficeBlockout.unity");
            yield return new EnterPlayMode();
            var registry = Object.FindFirstObjectByType<HazardRegistry>();
            foreach (var hazard in registry.Hazards)
            {
                var source = hazard.transform.Find("Confirmation sound - spatial").GetComponent<AudioSource>();
                Assert.That(source.clip, Is.Not.Null);
                Assert.That(source.spatialBlend, Is.EqualTo(1f));
                Assert.That(source.loop, Is.False);
                Assert.That(source.isPlaying, Is.False);
                Assert.That(registry.TryConfirmHazard(hazard.HazardId), Is.True);
                Assert.That(source.isPlaying, Is.True);
                Assert.That(registry.TryConfirmHazard(hazard.HazardId), Is.False);
                registry.ResetHazards();
                Assert.That(source.isPlaying, Is.False);
            }
            yield return new ExitPlayMode();
        }
    }
}
