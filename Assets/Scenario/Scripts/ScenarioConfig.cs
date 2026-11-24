using System.Collections.Generic;
using UnityEngine;

namespace OfficeTraining.Scenario
{
    /// <summary>Designer-editable training rules. Hazard ids come from the scene's HazardRegistry.</summary>
    [CreateAssetMenu(menuName = "Office Training/Scenario Config")]
    public sealed class ScenarioConfig : ScriptableObject
    {
        [Tooltip("Whether the supervised, limited extinguisher practice is allowed. When off, choosing practice " +
                 "is recorded as unsafe and the learner is sent to evacuate. Content needs safety review.")]
        [SerializeField] private bool practicePermitted = true;

        [Tooltip("Hazards needed for the inspection objective. -1 means all hazards.")]
        [SerializeField] private int requiredHazardCount = -1;

        [Tooltip("Start the incident automatically when every hazard has been confirmed.")]
        [SerializeField] private bool autoAdvanceWhenAllHazardsFound = true;

        [Tooltip("Grabbing the extinguisher during the incident counts as choosing the practice branch.")]
        [SerializeField] private bool grabbingExtinguisherChoosesPractice = true;

        [Tooltip("Item id the interaction module reports for the extinguisher.")]
        [SerializeField] private string extinguisherItemId = ScenarioRules.DefaultExtinguisherItemId;

        public bool PracticePermitted => practicePermitted;

        public ScenarioRules CreateRules(IEnumerable<HazardInfo> hazards)
        {
            var list = new List<HazardInfo>(hazards);
            int required = Mathf.Clamp(requiredHazardCount, -1, list.Count);
            string itemId = string.IsNullOrWhiteSpace(extinguisherItemId) ? ScenarioRules.DefaultExtinguisherItemId : extinguisherItemId;
            return new ScenarioRules(list, practicePermitted, required, autoAdvanceWhenAllHazardsFound,
                grabbingExtinguisherChoosesPractice, itemId);
        }
    }
}
