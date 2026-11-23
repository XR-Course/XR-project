using UnityEngine;

namespace OfficeTraining.Environment
{
    /// <summary>Editor-only incident test controls. Not final VR UI or scenario logic.</summary>
    public sealed class IncidentDebugPanel : MonoBehaviour
    {
        [SerializeField] private IncidentController incident;
        [Tooltip("Only the untracked desktop preview camera, never an XR camera.")]
        [SerializeField] private Camera desktopCamera;
        [SerializeField] private Transform incidentViewpoint;

#if UNITY_EDITOR
        private string lastEvent = "No incident events yet.";
        private GUIStyle wrappedLabel;
        private Vector2 scroll;
        private Vector3 initialCameraPosition;
        private Quaternion initialCameraRotation;

        private void OnEnable()
        {
            if (incident == null)
                return;
            incident.OnIncidentStateChanged += HandleStateChanged;
            incident.OnPauseChanged += HandlePauseChanged;
            incident.OnIncidentReset += HandleReset;
            if (desktopCamera != null)
            {
                initialCameraPosition = desktopCamera.transform.position;
                initialCameraRotation = desktopCamera.transform.rotation;
            }
        }

        private void OnDisable()
        {
            if (incident == null)
                return;
            incident.OnIncidentStateChanged -= HandleStateChanged;
            incident.OnPauseChanged -= HandlePauseChanged;
            incident.OnIncidentReset -= HandleReset;
        }

        private void HandleStateChanged(IncidentState state)
        {
            lastEvent = "OnIncidentStateChanged: " + state;
            Debug.Log(lastEvent, incident);
        }

        private void HandlePauseChanged(bool paused)
        {
            lastEvent = "OnPauseChanged: " + paused;
            Debug.Log(lastEvent, incident);
        }

        private void HandleReset()
        {
            lastEvent = "OnIncidentReset";
            Debug.Log(lastEvent, incident);
        }

        private void OnGUI()
        {
            if (incident == null)
                return;
            if (wrappedLabel == null)
                wrappedLabel = new GUIStyle(GUI.skin.label) { wordWrap = true };
            float width = Mathf.Min(330f, Screen.width - 24f);
            GUILayout.BeginArea(new Rect(Mathf.Max(12f, Screen.width - width - 12f), 12f,
                width, Mathf.Max(100f, Mathf.Min(520f, Screen.height - 24f))), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Incident test panel (Editor only)");
            GUILayout.Label("State: " + incident.CurrentState);
            GUILayout.Label("Prototype fire, smoke and audio. Buttons simulate coordinator commands, not training decisions.", wrappedLabel);
            if (desktopCamera != null && incidentViewpoint != null)
            {
                if (GUILayout.Button("View incident (desktop only)", GUILayout.Height(26f)))
                    desktopCamera.transform.SetPositionAndRotation(incidentViewpoint.position, incidentViewpoint.rotation);
                if (GUILayout.Button("Restore exit-route view", GUILayout.Height(26f)))
                    desktopCamera.transform.SetPositionAndRotation(initialCameraPosition, initialCameraRotation);
            }
            DrawStateButton("Start incident", IncidentState.Active);
            DrawStateButton("Mark resolved", IncidentState.Resolved);
            DrawStateButton("Set evacuation required", IncidentState.EvacuationRequired);
            DrawStateButton("Set inactive", IncidentState.Inactive);
            bool previous = GUI.enabled;
            GUI.enabled = previous && incident.isActiveAndEnabled && !incident.IsPaused;
            if (GUILayout.Button("Cycle states", GUILayout.Height(28f)))
            {
                // Test harness only. This order does not define permitted scenario transitions.
                var next = (IncidentState)(((int)incident.CurrentState + 1) % 4);
                incident.TrySetState(next);
            }
            GUI.enabled = previous;
            incident.SetPaused(GUILayout.Toggle(incident.IsPaused, "Pause incident commands"));
            if (GUILayout.Button("Reset incident", GUILayout.Height(30f)))
                incident.ResetIncident();
            GUILayout.Label("Reset affects only this incident. Hazard confirmations have their own reset.", wrappedLabel);
            GUILayout.Label(lastEvent, wrappedLabel);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawStateButton(string label, IncidentState state)
        {
            bool previous = GUI.enabled;
            GUI.enabled = previous && incident.isActiveAndEnabled && !incident.IsPaused && incident.CurrentState != state;
            if (GUILayout.Button(label, GUILayout.Height(28f)))
                incident.TrySetState(state);
            GUI.enabled = previous;
        }
#endif
    }
}
