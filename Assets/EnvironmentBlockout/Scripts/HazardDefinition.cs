using UnityEngine;

namespace OfficeTraining.Environment
{
    /// <summary>Shared identity and neutral display text, never session state or safety rules.</summary>
    [CreateAssetMenu(menuName = "Office Training/Hazard Definition")]
    public sealed class HazardDefinition : ScriptableObject
    {
        [SerializeField] private string hazardId;
        [SerializeField] private string displayName;

        public string HazardId => hazardId;
        public string DisplayName => displayName;
    }
}
