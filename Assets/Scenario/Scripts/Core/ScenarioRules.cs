using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OfficeTraining.Scenario
{
    /// <summary>Hazard identity as seen by the scenario. Ids match the environment's HazardDefinition ids.</summary>
    public sealed class HazardInfo
    {
        public HazardInfo(string id, string displayName)
        {
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        }

        public string Id { get; }
        public string DisplayName { get; }
    }

    /// <summary>
    /// Immutable training rules for one scenario. Plain C# so it can be built from a
    /// ScriptableObject in Unity or directly in tests.
    /// </summary>
    public sealed class ScenarioRules
    {
        public const string DefaultExtinguisherItemId = "extinguisher";

        private readonly Dictionary<string, HazardInfo> byId = new Dictionary<string, HazardInfo>(StringComparer.Ordinal);

        /// <param name="hazards">Hazards the learner can confirm. May be empty (inspection is then finished manually).</param>
        /// <param name="practicePermitted">Whether the supervised, limited extinguisher practice is allowed in this scenario.</param>
        /// <param name="requiredHazardCount">Hazards needed for the inspection objective; -1 means all.</param>
        /// <param name="autoAdvanceWhenAllHazardsFound">Start the incident automatically once every hazard is confirmed.</param>
        /// <param name="grabbingExtinguisherChoosesPractice">Grabbing the extinguisher during the incident counts as choosing practice.</param>
        /// <param name="extinguisherItemId">Item id the interaction module reports for the extinguisher.</param>
        public ScenarioRules(IEnumerable<HazardInfo> hazards, bool practicePermitted, int requiredHazardCount = -1,
            bool autoAdvanceWhenAllHazardsFound = true, bool grabbingExtinguisherChoosesPractice = true,
            string extinguisherItemId = DefaultExtinguisherItemId)
        {
            if (hazards == null)
                throw new ArgumentNullException(nameof(hazards));
            if (string.IsNullOrWhiteSpace(extinguisherItemId))
                throw new ArgumentException("Extinguisher item id must not be blank.", nameof(extinguisherItemId));

            var list = new List<HazardInfo>();
            foreach (HazardInfo hazard in hazards)
            {
                if (hazard == null || string.IsNullOrWhiteSpace(hazard.Id))
                    throw new ArgumentException("Hazard ids must not be null or blank.", nameof(hazards));
                if (byId.ContainsKey(hazard.Id))
                    throw new ArgumentException("Duplicate hazard id: " + hazard.Id, nameof(hazards));
                byId.Add(hazard.Id, hazard);
                list.Add(hazard);
            }

            if (requiredHazardCount < -1 || requiredHazardCount > list.Count)
                throw new ArgumentOutOfRangeException(nameof(requiredHazardCount),
                    "Required hazard count must be -1 (all) or between 0 and the number of hazards.");

            Hazards = new ReadOnlyCollection<HazardInfo>(list);
            RequiredHazardCount = requiredHazardCount == -1 ? list.Count : requiredHazardCount;
            PracticePermitted = practicePermitted;
            AutoAdvanceWhenAllHazardsFound = autoAdvanceWhenAllHazardsFound;
            GrabbingExtinguisherChoosesPractice = grabbingExtinguisherChoosesPractice;
            ExtinguisherItemId = extinguisherItemId;
        }

        public IReadOnlyList<HazardInfo> Hazards { get; }
        public int RequiredHazardCount { get; }
        public bool PracticePermitted { get; }
        public bool AutoAdvanceWhenAllHazardsFound { get; }
        public bool GrabbingExtinguisherChoosesPractice { get; }
        public string ExtinguisherItemId { get; }

        public bool IsKnownHazard(string id) => id != null && byId.ContainsKey(id);

        public string DisplayNameFor(string id) =>
            id != null && byId.TryGetValue(id, out HazardInfo hazard) ? hazard.DisplayName : id;

        /// <summary>The three hazards currently built by the environment module.</summary>
        public static IReadOnlyList<HazardInfo> DefaultHazards { get; } = new ReadOnlyCollection<HazardInfo>(new[]
        {
            new HazardInfo("power_strip", "Power strip"),
            new HazardInfo("heater_papers", "Heater and papers"),
            new HazardInfo("damaged_cable", "Damaged cable")
        });

        public static ScenarioRules CreateDefault(bool practicePermitted = true) =>
            new ScenarioRules(DefaultHazards, practicePermitted);
    }
}
