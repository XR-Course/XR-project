using UnityEngine;

namespace OfficeTraining.Interaction
{
    /// <summary>
    /// Marks a collider as the extinguisher practice target and gives simple aim feedback.
    /// It has no fire logic: the environment module owns fire visuals and incident state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PracticeTarget : MonoBehaviour
    {
        [Tooltip("Optional object shown while the nozzle points at the target (for example a ring).")]
        [SerializeField] private GameObject aimedIndicator;
        [Tooltip("Optional object shown after a successful practice on this target.")]
        [SerializeField] private GameObject completedIndicator;

        public bool IsAimed { get; private set; }
        public bool IsCompleted { get; private set; }

        private void Awake() => ResetTarget();

        public void SetAimed(bool aimed)
        {
            IsAimed = aimed;
            if (aimedIndicator != null)
                aimedIndicator.SetActive(aimed && !IsCompleted);
        }

        public void MarkCompleted()
        {
            IsCompleted = true;
            if (aimedIndicator != null)
                aimedIndicator.SetActive(false);
            if (completedIndicator != null)
                completedIndicator.SetActive(true);
        }

        public void ResetTarget()
        {
            IsCompleted = false;
            IsAimed = false;
            if (aimedIndicator != null)
                aimedIndicator.SetActive(false);
            if (completedIndicator != null)
                completedIndicator.SetActive(false);
        }
    }
}
