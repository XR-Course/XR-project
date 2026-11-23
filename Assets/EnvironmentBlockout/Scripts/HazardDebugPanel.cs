using UnityEngine;

namespace OfficeTraining.Environment
{
    /// <summary>Editor Game-view test harness, not the final VR user interface.</summary>
    public sealed class HazardDebugPanel : MonoBehaviour
    {
        [SerializeField] private HazardRegistry registry;

#if UNITY_EDITOR
        private string lastEvent = "No events yet.";
        private GUIStyle heading;

        private void OnEnable()
        {
            if (registry == null)
                return;
            registry.OnHazardConfirmed += HandleConfirmed;
            registry.OnHazardsReset += HandleReset;
        }

        private void OnDisable()
        {
            if (registry == null)
                return;
            registry.OnHazardConfirmed -= HandleConfirmed;
            registry.OnHazardsReset -= HandleReset;
        }

        private void HandleConfirmed(string id)
        {
            lastEvent = "OnHazardConfirmed: " + id;
            Debug.Log(lastEvent, registry);
        }

        private void HandleReset()
        {
            lastEvent = "OnHazardsReset";
            Debug.Log(lastEvent, registry);
        }

        private void OnGUI()
        {
            if (registry == null)
                return;
            if (heading == null)
                heading = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.BeginArea(new Rect(12f, 12f, 330f, 365f), GUI.skin.box);
            GUILayout.Label("Hazard test panel (Editor only)", heading);
            GUILayout.Label("Play-mode test controls - not VR guidance.");
            if (!registry.IsConfigurationValid)
                GUILayout.Label("Invalid registry configuration; see Console.");
            GUILayout.Label($"Confirmed: {registry.ConfirmedCount} / {registry.HazardCount}");
            foreach (HazardObject hazard in registry.Hazards)
            {
                if (hazard == null || hazard.Definition == null)
                    continue;
                bool previous = GUI.enabled;
                GUI.enabled = registry.IsConfigurationValid && registry.isActiveAndEnabled &&
                    registry.InspectionEnabled && !registry.IsPaused && hazard.isActiveAndEnabled && !hazard.IsConfirmed;
                string label = hazard.Definition.DisplayName + (hazard.IsConfirmed ? " [confirmed]" : "");
                if (GUILayout.Button(label, GUILayout.Height(32f)))
                    registry.TryConfirmHazard(hazard.HazardId);
                GUI.enabled = previous;
            }
            registry.SetInspectionEnabled(GUILayout.Toggle(registry.InspectionEnabled, "Enable inspection"));
            registry.SetPaused(GUILayout.Toggle(registry.IsPaused, "Pause confirmation"));
            if (GUILayout.Button("Reset hazards", GUILayout.Height(30f)))
                registry.ResetHazards();
            GUILayout.Label(lastEvent);
            GUILayout.EndArea();
        }
#endif
    }
}
