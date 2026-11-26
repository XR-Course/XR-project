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
    }
}