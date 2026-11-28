using NUnit.Framework;
using OfficeTraining.Interaction.Onboarding;

namespace OfficeTraining.Interaction.Tests
{
    public class TutorialManagerTests
    {
        private TutorialManager manager;

        [SetUp]
        public void SetUp()
        {
            manager = new TutorialManager();
        }

        [Test]
        public void InitialStep_IsWelcome()
        {
            Assert.AreEqual(
                TutorialStep.Welcome,
                manager.CurrentStep);
        }

        [Test]
        public void CompleteCorrectStep_AdvancesToNextStep()
        {
            bool result = manager.CompleteStep(
                TutorialStep.Welcome);

            Assert.IsTrue(result);
            Assert.AreEqual(
                TutorialStep.Select,
                manager.CurrentStep);
        }

        [Test]
        public void CompleteIncorrectStep_DoesNotAdvance()
        {
            bool result = manager.CompleteStep(
                TutorialStep.Grab);

            Assert.IsFalse(result);
            Assert.AreEqual(
                TutorialStep.Welcome,
                manager.CurrentStep);
        }

        [Test]
        public void CompleteAllSteps_TutorialIsAllCompleted()
        {
            manager.CompleteStep(TutorialStep.Welcome);
            manager.CompleteStep(TutorialStep.Select);
            manager.CompleteStep(TutorialStep.Grab);
            manager.CompleteStep(TutorialStep.Teleport);
            
            Assert.IsTrue(manager.IsCompleted);
            Assert.AreEqual(TutorialStep.Completed, manager.CurrentStep);
        }

        [Test]
        public void Reset_ReturnsToWelcome()
        {
            manager.CompleteStep(TutorialStep.Welcome);
            
            manager.Reset();
            
            Assert.AreEqual(TutorialStep.Welcome, manager.CurrentStep);
            Assert.IsFalse(manager.IsCompleted);
        }

        [Test]
        public void CompleteStep_TriggerOnStepChanged()
        {
            TutorialStep receivedStep = TutorialStep.Welcome;
            manager.OnStepChanged += step => receivedStep = step;
            manager.CompleteStep(TutorialStep.Welcome);
            Assert.AreEqual(TutorialStep.Select, receivedStep);
        }
    }
}