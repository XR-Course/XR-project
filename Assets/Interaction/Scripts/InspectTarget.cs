using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace OfficeTraining.Interaction
{
    /// <summary>
    /// Makes an object selectable with the controller ray (or a direct hand) and publishes
    /// OnInspected(targetId) when selected. Shows an optional hover indicator so learners can see
    /// what they are pointing at. Whether the selection counts is decided by the consumer.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class InspectTarget : MonoBehaviour
    {
        [SerializeField] private string targetId;
        [SerializeField] private InteractionEvents events;
        [Tooltip("Optional object shown while a controller hovers this target.")]
        [SerializeField] private GameObject hoverIndicator;

        private XRSimpleInteractable interactable;

        public virtual string TargetId => targetId;
        public InteractionEvents Events { get => events; set => events = value; }
        public XRSimpleInteractable Interactable => interactable != null ? interactable : interactable = GetComponent<XRSimpleInteractable>();

        protected virtual void Awake()
        {
            SetHover(false);
        }

        protected virtual void OnEnable()
        {
            Interactable.selectEntered.AddListener(HandleSelectEntered);
            Interactable.hoverEntered.AddListener(HandleHoverEntered);
            Interactable.hoverExited.AddListener(HandleHoverExited);
        }

        protected virtual void OnDisable()
        {
            Interactable.selectEntered.RemoveListener(HandleSelectEntered);
            Interactable.hoverEntered.RemoveListener(HandleHoverEntered);
            Interactable.hoverExited.RemoveListener(HandleHoverExited);
            SetHover(false);
        }

        /// <summary>Same as a learner selection; useful for tests and desktop debugging.</summary>
        public void Inspect()
        {
            string id = TargetId;
            if (!string.IsNullOrWhiteSpace(id))
                events?.RaiseInspected(id);
        }

        private void HandleSelectEntered(SelectEnterEventArgs args) => Inspect();
        private void HandleHoverEntered(HoverEnterEventArgs args) => SetHover(true);
        private void HandleHoverExited(HoverExitEventArgs args) => SetHover(Interactable.isHovered);

        private void SetHover(bool on)
        {
            if (hoverIndicator != null)
                hoverIndicator.SetActive(on);
        }
    }
}
