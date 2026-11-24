using System;
using NUnit.Framework;

namespace OfficeTraining.Interaction.Tests
{
    public sealed class PracticeAimTrackerTests
    {
        [Test]
        public void SucceedsAfterRequiredAimTime()
        {
            var tracker = new PracticeAimTracker(2f);
            tracker.SetArmed(true);
            int finished = 0;
            tracker.Finished += _ => finished++;

            Assert.IsFalse(tracker.Tick(1f, spraying: true, onTarget: true));
            Assert.AreEqual(0.5f, tracker.Progress, 1e-4f);
            Assert.IsTrue(tracker.Tick(1f, spraying: true, onTarget: true));
            Assert.AreEqual(PracticeOutcome.Succeeded, tracker.Outcome);
            Assert.AreEqual(1, finished);
        }

        [Test]
        public void DoesNotCountWhenDisarmed()
        {
            var tracker = new PracticeAimTracker(1f);
            Assert.IsFalse(tracker.Tick(5f, true, true));
            Assert.AreEqual(0f, tracker.AimSeconds);
            Assert.AreEqual(PracticeOutcome.InProgress, tracker.Outcome);
        }

        [Test]
        public void DoesNotCountWithoutSprayOrOffTarget()
        {
            var tracker = new PracticeAimTracker(1f);
            tracker.SetArmed(true);
            tracker.Tick(5f, spraying: false, onTarget: true);
            tracker.Tick(5f, spraying: true, onTarget: false);
            Assert.AreEqual(0f, tracker.AimSeconds);
            Assert.AreEqual(5f, tracker.SpraySeconds, 1e-4f);
            Assert.AreEqual(PracticeOutcome.InProgress, tracker.Outcome);
        }

        [Test]
        public void AimTimeAccumulatesAcrossInterruptions()
        {
            var tracker = new PracticeAimTracker(2f);
            tracker.SetArmed(true);
            tracker.Tick(1f, true, true);
            tracker.Tick(3f, true, false);
            tracker.Tick(1f, false, true);
            Assert.IsTrue(tracker.Tick(1f, true, true));
            Assert.AreEqual(PracticeOutcome.Succeeded, tracker.Outcome);
        }

        [Test]
        public void FailsWhenSprayLimitIsReachedFirst()
        {
            var tracker = new PracticeAimTracker(2f, maxSpraySeconds: 4f);
            tracker.SetArmed(true);
            PracticeOutcome? result = null;
            tracker.Finished += outcome => result = outcome;

            tracker.Tick(3f, true, false);
            Assert.IsTrue(tracker.Tick(1f, true, true));
            Assert.AreEqual(PracticeOutcome.Failed, result);
        }

        [Test]
        public void FinishesOnlyOnceUntilReset()
        {
            var tracker = new PracticeAimTracker(1f);
            tracker.SetArmed(true);
            int finished = 0;
            tracker.Finished += _ => finished++;

            tracker.Tick(1f, true, true);
            Assert.IsFalse(tracker.Tick(1f, true, true));
            Assert.AreEqual(1, finished);

            tracker.Reset();
            Assert.AreEqual(PracticeOutcome.InProgress, tracker.Outcome);
            Assert.AreEqual(0f, tracker.Progress);
            Assert.IsTrue(tracker.Armed, "Reset keeps the armed flag.");
            Assert.IsTrue(tracker.Tick(1f, true, true));
            Assert.AreEqual(2, finished);
        }

        [Test]
        public void IgnoresNonPositiveDelta()
        {
            var tracker = new PracticeAimTracker(1f);
            tracker.SetArmed(true);
            tracker.Tick(-1f, true, true);
            tracker.Tick(0f, true, true);
            Assert.AreEqual(0f, tracker.SpraySeconds);
        }

        [Test]
        public void RejectsInvalidConfiguration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PracticeAimTracker(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PracticeAimTracker(1f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PracticeAimTracker(2f, 1f));
        }
    }
}
