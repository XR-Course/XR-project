using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using System;

namespace OfficeTraining.Interaction.Onboarding
{
    public class TutorialController : MonoBehaviour
    {
        [SerializeField] private TutorialUI tutorialUI;
        [SerializeField] private GameObject selectTarget;
        [SerializeField] private GameObject grabTarget;
        [SerializeField] private TeleportationProvider teleportProvider;
        
        private TutorialManager manager;
        public event Action OnTutorialComplete;

        private void Start()
        {
            manager = new  TutorialManager();
            manager.OnStepChanged += HandleStepChanged;
            UpdateSelectTarget(manager.CurrentStep);
            tutorialUI.Initialize(manager);
            teleportProvider.locomotionEnded += HandleTeleportEnded;
        }

        private void HandleTeleportEnded(LocomotionProvider provider)
        {
            CompleteTeleport();
        }

        private void OnDestroy()
        {
            if (teleportProvider != null)
            {
                teleportProvider.locomotionEnded -= HandleTeleportEnded;
            }
        }

        private void UpdateSelectTarget(TutorialStep step)
        {
           selectTarget.SetActive(step == TutorialStep.Select);
           grabTarget.SetActive(step == TutorialStep.Grab);
        }

        public void StartTutorial()
        {
            manager.CompleteStep(TutorialStep.Welcome);
        }

        private void HandleStepChanged(TutorialStep step)
        {
            UpdateSelectTarget(step);
            
            if (step == TutorialStep.Completed)
            {
                OnTutorialComplete?.Invoke();
                gameObject.SetActive(false);
            }
        }

        public void CompleteSelect()
        {
            manager.CompleteStep(TutorialStep.Select);
        }

        public void CompleteGrab()
        {
            manager.CompleteStep(TutorialStep.Grab);
        }
        
        public void CompleteTeleport()
        {
            manager.CompleteStep(TutorialStep.Teleport);
        }
    }
}