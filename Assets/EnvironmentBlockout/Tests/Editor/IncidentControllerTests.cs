using System.Collections.Generic;
using NUnit.Framework;
using OfficeTraining.Environment;
using UnityEngine;

namespace OfficeTraining.Tests
{
    public sealed class IncidentControllerTests
    {
        private GameObject root;
        private IncidentController incident;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Incident test");
            incident = root.AddComponent<IncidentController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void StartsInactiveAndUnpaused()
        {
            Assert.That(incident.CurrentState, Is.EqualTo(IncidentState.Inactive));
            Assert.That(incident.IsPaused, Is.False);
        }

        [Test]
        public void StateIsCommittedBeforeOneNotificationPerChange()
        {
            var events = new List<IncidentState>();
            incident.OnIncidentStateChanged += state =>
            {
                Assert.That(incident.CurrentState, Is.EqualTo(state));
                events.Add(state);
            };
            foreach (var state in new[] { IncidentState.Active, IncidentState.Resolved, IncidentState.EvacuationRequired, IncidentState.Inactive })
            {
                Assert.That(incident.TrySetState(state), Is.True);
                Assert.That(incident.TrySetState(state), Is.False);
            }
            Assert.That(events, Is.EqualTo(new[] { IncidentState.Active, IncidentState.Resolved, IncidentState.EvacuationRequired, IncidentState.Inactive }));
        }

        [TestCase(IncidentState.Inactive)]
        [TestCase(IncidentState.Active)]
        [TestCase(IncidentState.Resolved)]
        [TestCase(IncidentState.EvacuationRequired)]
        public void DoesNotInventTrainingTransitionRules(IncidentState from)
        {
            foreach (IncidentState to in System.Enum.GetValues(typeof(IncidentState)))
            {
                incident.ResetIncident();
                if (from != IncidentState.Inactive)
                    incident.TrySetState(from);
                Assert.That(incident.TrySetState(to), Is.EqualTo(to != from));
                Assert.That(incident.CurrentState, Is.EqualTo(to));
            }
        }

        [TestCase(-1)]
        [TestCase(4)]
        [TestCase(int.MaxValue)]
        public void UndefinedStatesAreRejectedWithoutEvents(int invalid)
        {
            incident.TrySetState(IncidentState.Active);
            int events = 0;
            incident.OnIncidentStateChanged += _ => events++;
            Assert.That(incident.TrySetState((IncidentState)invalid), Is.False);
            Assert.That(incident.CurrentState, Is.EqualTo(IncidentState.Active));
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void PauseRejectsCommandsAndReportsOnlyRealPauseChanges()
        {
            var pauseEvents = new List<bool>();
            incident.OnPauseChanged += pauseEvents.Add;
            incident.SetPaused(true);
            incident.SetPaused(true);
            Assert.That(incident.TrySetState(IncidentState.Active), Is.False);
            Assert.That(incident.CurrentState, Is.EqualTo(IncidentState.Inactive));
            incident.SetPaused(false);
            incident.SetPaused(false);
            Assert.That(incident.TrySetState(IncidentState.Active), Is.True);
            Assert.That(pauseEvents, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void ResetFromPausedActiveCommitsWholeSnapshotBeforeNotifying()
        {
            incident.TrySetState(IncidentState.Active);
            incident.SetPaused(true);
            var events = new List<string>();
            incident.OnPauseChanged += paused =>
            {
                Assert.That(paused, Is.False);
                Assert.That(incident.CurrentState, Is.EqualTo(IncidentState.Inactive));
                events.Add("pause");
            };
            incident.OnIncidentStateChanged += state =>
            {
                Assert.That(state, Is.EqualTo(IncidentState.Inactive));
                Assert.That(incident.IsPaused, Is.False);
                events.Add("state");
            };
            incident.OnIncidentReset += () => events.Add("reset");
            incident.ResetIncident();
            Assert.That(events, Is.EqualTo(new[] { "pause", "state", "reset" }));
            Assert.That(incident.TrySetState(IncidentState.Inactive), Is.False);
        }

        [Test]
        public void RepeatedResetNotifiesWithoutFakeStateOrPauseEvents()
        {
            int changes = 0, pauses = 0, resets = 0;
            incident.OnIncidentStateChanged += _ => changes++;
            incident.OnPauseChanged += _ => pauses++;
            incident.OnIncidentReset += () => resets++;
            incident.ResetIncident();
            incident.ResetIncident();
            Assert.That(changes, Is.Zero);
            Assert.That(pauses, Is.Zero);
            Assert.That(resets, Is.EqualTo(2));
        }

        [Test]
        public void DisabledComponentAndInactiveObjectRejectCommandsButResetWorks()
        {
            incident.TrySetState(IncidentState.Active);
            incident.enabled = false;
            Assert.That(incident.TrySetState(IncidentState.Resolved), Is.False);
            incident.ResetIncident();
            Assert.That(incident.CurrentState, Is.EqualTo(IncidentState.Inactive));
            incident.enabled = true;
            root.SetActive(false);
            Assert.That(incident.TrySetState(IncidentState.Active), Is.False);
            incident.ResetIncident();
            root.SetActive(true);
            Assert.That(incident.TrySetState(IncidentState.Active), Is.True);
        }

        [Test]
        public void ReenablePreservesStateAndPauseUntilExplicitReset()
        {
            incident.TrySetState(IncidentState.EvacuationRequired);
            incident.SetPaused(true);
            incident.enabled = false;
            incident.enabled = true;
            Assert.That(incident.CurrentState, Is.EqualTo(IncidentState.EvacuationRequired));
            Assert.That(incident.IsPaused, Is.True);
        }
    }
}
