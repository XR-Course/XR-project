using TMPro;
using UnityEngine;

namespace OfficeTraining.Interaction.Onboarding
{
    public class TutorialUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text heading;
        [SerializeField] private TMP_Text instructions;
        [SerializeField] private TMP_Text progress;

        private TutorialManager manager;

        public void Initialize(TutorialManager tutorialManager)
        {
            if (manager != null)
            {
                manager.OnStepChanged -= UpdateUI;
            }
            
            manager = tutorialManager;
            manager.OnStepChanged += UpdateUI;
            
            UpdateUI(manager.CurrentStep);
        }

        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.OnStepChanged -= UpdateUI;
            }
        }

        private void UpdateUI(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Welcome:
                    heading.text = "Welcome to VR";
                    instructions.text = "Learn the basic controls before starting the training";
                    progress.text = "Step 1/4";
                    break;
                
                case TutorialStep.Select:
                    heading.text = "Select an Object";
                    instructions.text = "Point the controller at the highlighted object and press the grip button";
                    progress.text = "Step 2/4";
                    break;
                
                case TutorialStep.Grab:
                    heading.text = "Grab Object";
                    instructions.text = "Press and hold the grip button to grab the object. Release it to let go";
                    progress.text = "Step 3/4";
                    break;
                
                case TutorialStep.Teleport:
                    heading.text = "Move around";
                    instructions.text = "Use the thumbstick to aim and teleport to a new location";
                    progress.text = "Step 4/4";
                    break;
                
                case TutorialStep.Completed:
                    heading.text = "Tutorial Completed";
                    instructions.text = "You are now ready to begin the fire safety training";
                    progress.text = "Completed";
                    break;
            }
            
        }
    }
}