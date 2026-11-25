using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace OfficeTraining.Interaction
{
    /// <summary>
    /// Reusable grab + use behaviour for a hand-held prop. Wraps an XRGrabInteractable, gives the
    /// prop a stable item id and publishes OnGrabbed / OnReleased / OnUse / OnUseEnded on the
    /// InteractionEvents channel. Remembers its start pose so a session reset can put it back.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class ToolInteractionController : MonoBehaviour
    {
        [Tooltip("Stable id reported to the scenario (the extinguisher must use 'extinguisher').")]
        [SerializeField] private string itemId = "extinguisher";
        [SerializeField] private InteractionEvents events;
        [Tooltip("Point and direction the tool acts from (for example the nozzle). Defaults to this transform.")]
        [SerializeField] private Transform usePoint;
        [Tooltip("Short controller vibration when the use action starts. 0 = off.")]
        [Range(0f, 1f)]
        [SerializeField] private float useHapticAmplitude = 0.3f;

        private XRGrabInteractable grab;
        private Rigidbody body;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private Transform startParent;
        private bool startPoseCaptured;

        public string ItemId => itemId;
        public InteractionEvents Events { get => events; set => events = value; }
        public Transform UsePoint => usePoint != null ? usePoint : transform;
        public bool IsHeld { get; private set; }
        public bool IsUsing { get; private set; }

        /// <summary>Local notifications for components on the same prop (ExtinguisherPractice).</summary>
        public event Action UseStarted;
        public event Action UseStopped;
        public event Action Released;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            CaptureStartPose();
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(HandleSelectEntered);
            grab.selectExited.AddListener(HandleSelectExited);
            grab.activated.AddListener(HandleActivated);
            grab.deactivated.AddListener(HandleDeactivated);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(HandleSelectEntered);
            grab.selectExited.RemoveListener(HandleSelectExited);
            grab.activated.RemoveListener(HandleActivated);
            grab.deactivated.RemoveListener(HandleDeactivated);
            StopUse();
            IsHeld = false;
        }

        /// <summary>Use the current pose as the reset pose (call after moving the prop in a scene setup).</summary>
        public void CaptureStartPose()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
            startParent = transform.parent;
            startPoseCaptured = true;
        }

        /// <summary>Drops the prop if held and returns it to its start pose. Publishes no grab events.</summary>
        public void ResetToStartPose()
        {
            if (grab != null && grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);

            StopUse(publish: false);
            IsHeld = false;
            if (!startPoseCaptured)
                return;

            if (transform.parent != startParent)
                transform.SetParent(startParent, true);
            transform.SetPositionAndRotation(startPosition, startRotation);
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private void HandleSelectEntered(SelectEnterEventArgs args)
        {
            if (IsHeld)
                return;
            IsHeld = true;
            events?.RaiseGrabbed(itemId);
        }

        private void HandleSelectExited(SelectExitEventArgs args)
        {
            // Cancelled selections come from resets: no learner action to report.
            if (grab.isSelected)
                return;
            bool wasHeld = IsHeld;
            IsHeld = false;
            StopUse(publish: !args.isCanceled);
            if (wasHeld && !args.isCanceled)
                events?.RaiseReleased(itemId);
            Released?.Invoke();
        }

        private void HandleActivated(ActivateEventArgs args)
        {
            if (IsUsing)
                return;
            IsUsing = true;
            Transform point = UsePoint;
            events?.RaiseUse(itemId, new Pose(point.position, point.rotation));
            if (useHapticAmplitude > 0f && args.interactorObject is XRBaseInputInteractor inputInteractor)
                inputInteractor.SendHapticImpulse(useHapticAmplitude, 0.1f);
            UseStarted?.Invoke();
        }

        private void HandleDeactivated(DeactivateEventArgs args) => StopUse();

        private void StopUse(bool publish = true)
        {
            if (!IsUsing)
                return;
            IsUsing = false;
            if (publish)
                events?.RaiseUseEnded(itemId);
            UseStopped?.Invoke();
        }
    }
}
