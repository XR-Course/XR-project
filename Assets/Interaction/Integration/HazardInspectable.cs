using OfficeTraining.Environment;
using UnityEngine;

namespace OfficeTraining.Interaction.Integration
{
    /// <summary>
    /// Input adapter for Student 2's hazards: selecting this object with the controller ray publishes
    /// OnInspected with the HazardObject's stable id (from its HazardDefinition), so ids are never
    /// typed twice. Put it on the hazard prop (needs a collider) under the HazardObject.
    /// </summary>
    public sealed class HazardInspectable : InspectTarget
    {
        [Tooltip("Defaults to the nearest HazardObject in this object or its parents.")]
        [SerializeField] private HazardObject hazard;

        public HazardObject Hazard => hazard != null ? hazard : hazard = GetComponentInParent<HazardObject>();
        public override string TargetId => Hazard != null ? Hazard.HazardId : base.TargetId;
    }
}
