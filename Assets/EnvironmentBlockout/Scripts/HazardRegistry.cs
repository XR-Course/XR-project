using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace OfficeTraining.Environment
{
    /// <summary>
    /// Scene-local hazard confirmations. No input handling, training phase decisions or scoring.
    /// Call from Unity's main thread. Events report successful changes, not attempts.
    /// </summary>
    public sealed class HazardRegistry : MonoBehaviour
    {
        [SerializeField] private HazardObject[] hazards = Array.Empty<HazardObject>();
        [SerializeField] private bool inspectionEnabledOnStart = true;

        private readonly Dictionary<string, HazardObject> byId = new Dictionary<string, HazardObject>(StringComparer.Ordinal);
        private readonly List<string> confirmedIds = new List<string>();
        private ReadOnlyCollection<string> confirmedView;
        private ReadOnlyCollection<HazardObject> hazardView;
        private bool initialized;
        private bool configurationValid;
        private bool inspectionEnabled;
        private bool paused;

        public event Action<string> OnHazardConfirmed;
        public event Action OnHazardsReset;

        public bool IsConfigurationValid { get { EnsureInitialized(); return configurationValid; } }
        public bool InspectionEnabled { get { EnsureInitialized(); return inspectionEnabled; } }
        public bool IsPaused => paused;
        public int HazardCount { get { EnsureInitialized(); return byId.Count; } }
        public int ConfirmedCount => confirmedIds.Count;
        public IReadOnlyList<HazardObject> Hazards { get { EnsureInitialized(); return hazardView; } }
        public IReadOnlyList<string> ConfirmedIds { get { EnsureInitialized(); return confirmedView; } }

        private void Awake()
        {
            EnsureInitialized();
        }

        public bool TryConfirmHazard(string hazardId)
        {
            EnsureInitialized();
            if (!configurationValid || !isActiveAndEnabled || !inspectionEnabled || paused || string.IsNullOrWhiteSpace(hazardId))
                return false;
            if (!byId.TryGetValue(hazardId, out HazardObject hazard) || hazard == null ||
                !hazard.isActiveAndEnabled || hazard.IsConfirmed)
                return false;

            // Commit state and feedback before notifying consumers.
            confirmedIds.Add(hazardId);
            hazard.SetConfirmed(true);
            OnHazardConfirmed?.Invoke(hazardId);
            return true;
        }

        public void SetInspectionEnabled(bool enabled)
        {
            EnsureInitialized();
            inspectionEnabled = enabled;
        }

        public void SetPaused(bool value)
        {
            EnsureInitialized();
            paused = value;
        }

        /// <summary>Reset stays available while paused or inspection is disabled; no confirmation events fire.</summary>
        public void ResetHazards()
        {
            EnsureInitialized();
            confirmedIds.Clear();
            foreach (HazardObject hazard in hazards)
                if (hazard != null)
                    hazard.SetConfirmed(false);
            inspectionEnabled = inspectionEnabledOnStart;
            paused = false;
            OnHazardsReset?.Invoke();
        }

        private void EnsureInitialized()
        {
            if (initialized)
                return;
            initialized = true;
            inspectionEnabled = inspectionEnabledOnStart;
            confirmedView = confirmedIds.AsReadOnly();
            // Copy the serialized list so consumers cannot change the registration order.
            hazards = hazards != null ? (HazardObject[])hazards.Clone() : Array.Empty<HazardObject>();
            hazardView = Array.AsReadOnly(hazards);
            configurationValid = hazards.Length > 0;
            var uniqueDefinitions = new HashSet<HazardDefinition>();
            foreach (HazardObject hazard in hazards)
            {
                if (hazard == null || hazard.Definition == null || string.IsNullOrWhiteSpace(hazard.HazardId) ||
                    !string.Equals(hazard.HazardId, hazard.HazardId.Trim(), StringComparison.Ordinal) ||
                    byId.ContainsKey(hazard.HazardId) || !uniqueDefinitions.Add(hazard.Definition))
                {
                    configurationValid = false;
                    continue;
                }
                byId.Add(hazard.HazardId, hazard);
                hazard.SetConfirmed(false);
            }
            if (!configurationValid)
                Debug.LogError("HazardRegistry requires a non-empty list of hazards with unique definitions and non-blank, unique IDs. Confirmations are disabled.", this);
        }
    }
}
