using UnityEngine;

namespace OfficeTraining.Interaction
{
    /// <summary>
    /// Arrival trigger for the assembly point. Checks every frame whether the learner's head
    /// (the XR camera) is inside a box on the floor, so it works the same for teleport, room-scale
    /// walking and the device simulator, with no physics colliders on the rig. Publishes
    /// OnZoneEntered(zoneId) once per entry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SafeZoneTrigger : MonoBehaviour
    {
        [SerializeField] private string zoneId = "assembly_point";
        [SerializeField] private InteractionEvents events;
        [Tooltip("Tracked head. Defaults to Camera.main.")]
        [SerializeField] private Transform head;
        [Tooltip("Zone size in local space (X/Z footprint, Y height from this transform up).")]
        [SerializeField] private Vector3 size = new Vector3(3f, 2.5f, 3f);

        public string ZoneId => zoneId;
        public bool IsInside { get; private set; }
        public InteractionEvents Events { get => events; set => events = value; }
        public Transform Head { get => head; set => head = value; }

        private void OnDisable() => IsInside = false;

        /// <summary>Forget the current state so the next frame inside fires again (session reset).</summary>
        public void ResetZone() => IsInside = false;

        private void Update()
        {
            Transform target = head;
            if (target == null)
            {
                Camera main = Camera.main;
                if (main == null)
                    return;
                target = main.transform;
            }
            bool inside = Contains(target.position);
            if (inside && !IsInside)
            {
                IsInside = true;
                events?.RaiseZoneEntered(zoneId);
            }
            else if (!inside)
            {
                IsInside = false;
            }
        }

        public bool Contains(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            return Mathf.Abs(local.x) <= size.x * 0.5f &&
                   Mathf.Abs(local.z) <= size.z * 0.5f &&
                   local.y >= -0.1f && local.y <= size.y;
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.1f, 0.9f, 0.3f, 0.25f);
            Gizmos.DrawCube(new Vector3(0f, size.y * 0.5f, 0f), size);
            Gizmos.color = new Color(0.1f, 0.9f, 0.3f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(0f, size.y * 0.5f, 0f), size);
        }
    }
}
