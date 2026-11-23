using UnityEngine;

namespace OfficeTraining.Environment
{
    /// <summary>Scene identity and presentation. Confirmation decisions belong to HazardRegistry.</summary>
    public sealed class HazardObject : MonoBehaviour
    {
        [SerializeField] private HazardDefinition definition;
        [Tooltip("Separate confirmation visual, not the hazard model itself.")]
        [SerializeField] private GameObject confirmedVisual;
        [SerializeField] private AudioSource confirmationAudio;

        public HazardDefinition Definition => definition;
        public string HazardId => definition != null ? definition.HazardId : string.Empty;
        public bool IsConfirmed { get; private set; }

        private void Awake()
        {
            SetConfirmed(false);
        }

        internal void SetConfirmed(bool confirmed)
        {
            IsConfirmed = confirmed;
            if (confirmedVisual != null && confirmedVisual != gameObject)
                confirmedVisual.SetActive(confirmed);
            if (confirmationAudio != null)
            {
                if (confirmed)
                    confirmationAudio.Play();
                else
                    confirmationAudio.Stop();
            }
        }
    }
}
