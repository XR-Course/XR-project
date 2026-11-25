using UnityEngine;
using UnityEngine.InputSystem;

namespace OfficeTraining.Interaction
{
    /// <summary>
    /// Scene-level input glue for the XR rig: publishes the controller menu button as
    /// OnMenuPressed (pause/resume is decided by the consumer) and offers a keyboard fallback for
    /// desktop testing. Locomotion comfort (teleport + snap turn, no smooth move) is configured on
    /// the XR Origin's ControllerInputActionManager components in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class XRInputSetup : MonoBehaviour
    {
        [SerializeField] private InteractionEvents events;
        [Tooltip("Controller menu button. The XRI default actions have no menu action, so this one is owned here.")]
        [SerializeField] private InputAction menuAction = CreateDefaultMenuAction();
        [Tooltip("Keyboard key for desktop testing. None = off.")]
        [SerializeField] private Key desktopMenuKey = Key.Escape;

        public InteractionEvents Events { get => events; set => events = value; }

        private static InputAction CreateDefaultMenuAction()
        {
            var action = new InputAction("Menu", InputActionType.Button);
            action.AddBinding("<XRController>{LeftHand}/{MenuButton}");
            action.AddBinding("<XRController>{RightHand}/{MenuButton}");
            return action;
        }

        private void OnEnable()
        {
            if (menuAction == null)
                return;
            menuAction.performed += HandleMenu;
            menuAction.Enable();
        }

        private void OnDisable()
        {
            if (menuAction == null)
                return;
            menuAction.performed -= HandleMenu;
            menuAction.Disable();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (desktopMenuKey != Key.None && keyboard != null && keyboard[desktopMenuKey].wasPressedThisFrame)
                events?.RaiseMenuPressed();
        }

        private void HandleMenu(InputAction.CallbackContext context) => events?.RaiseMenuPressed();
    }
}
