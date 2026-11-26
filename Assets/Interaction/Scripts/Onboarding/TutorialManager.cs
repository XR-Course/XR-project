using System;

namespace OfficeTraining.Interaction.Onboarding
{
    public class TutorialManager
    {
        public TutorialStep CurrentStep { get; private set; } = TutorialStep.Welcome;

        public event Action<TutorialStep> OnStepChanged;
        
        public bool IsCompleted => CurrentStep == TutorialStep.Completed;

        public bool CompleteStep(TutorialStep step)
        {
            if (IsCompleted || CurrentStep != step)
            {
                return false;
            }

            switch (CurrentStep)
            {
                case TutorialStep.Welcome:
                    ChangeStep(TutorialStep.Select);
                    break;
                case TutorialStep.Select:
                    ChangeStep(TutorialStep.Grab);
                    break;
                case TutorialStep.Grab:
                    ChangeStep(TutorialStep.Teleport);
                    break;
                case TutorialStep.Teleport:
                    ChangeStep(TutorialStep.Completed);
                    break;
                
                default:
                    return false;
                
            }
            
            return true;
        }

        public void Reset()
        {
            ChangeStep(TutorialStep.Welcome);
        }

        private void ChangeStep(TutorialStep nextStep)
        {
            CurrentStep = nextStep;
            OnStepChanged?.Invoke(CurrentStep);
        }

    }
}