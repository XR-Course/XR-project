using System;
using UnityEngine;

namespace OfficeTraining.Interaction
{
    /// <summary>
    /// Event channel published by the interaction module (Student 1). Interaction components only
    /// raise events here; they never change scenario state or scores. The scenario bridge, UI or a
    /// test harness subscribe. Create one asset (Create > Office Training > Interaction Events)
    /// and assign the same asset to every publisher and subscriber in a scene.
    /// All calls happen on Unity's main thread.
    /// </summary>
    [CreateAssetMenu(menuName = "Office Training/Interaction Events")]
    public sealed class InteractionEvents : ScriptableObject
    {
        [Tooltip("Write every published event to the Console.")]
        [SerializeField] private bool logEvents;

        /// <summary>An item was picked up (first hand only).</summary>
        public event Action<string> OnGrabbed;
        /// <summary>An item was let go by the last hand holding it.</summary>
        public event Action<string> OnReleased;
        /// <summary>The use (trigger) action started on a held item. Pose is the item's use point.</summary>
        public event Action<string, Pose> OnUse;
        /// <summary>The use action stopped (trigger released or item dropped).</summary>
        public event Action<string> OnUseEnded;
        /// <summary>The learner selected an inspectable target (for example a hazard) with a ray or hand.</summary>
        public event Action<string> OnInspected;
        /// <summary>The learner's head entered a zone (for example the assembly point).</summary>
        public event Action<string> OnZoneEntered;
        /// <summary>Practice aim progress 0..1 for an item. Raised while it changes.</summary>
        public event Action<string, float> OnPracticeProgress;
        /// <summary>A practice attempt finished: true = success, false = failed attempt.</summary>
        public event Action<string, bool> OnPracticeResult;
        /// <summary>The controller menu button (or desktop fallback key) was pressed.</summary>
        public event Action OnMenuPressed;

        public void RaiseGrabbed(string itemId) { Log("Grabbed", itemId); OnGrabbed?.Invoke(itemId); }
        public void RaiseReleased(string itemId) { Log("Released", itemId); OnReleased?.Invoke(itemId); }
        public void RaiseUse(string itemId, Pose pose) { Log("Use", itemId); OnUse?.Invoke(itemId, pose); }
        public void RaiseUseEnded(string itemId) { Log("UseEnded", itemId); OnUseEnded?.Invoke(itemId); }
        public void RaiseInspected(string targetId) { Log("Inspected", targetId); OnInspected?.Invoke(targetId); }
        public void RaiseZoneEntered(string zoneId) { Log("ZoneEntered", zoneId); OnZoneEntered?.Invoke(zoneId); }
        public void RaisePracticeProgress(string itemId, float progress) => OnPracticeProgress?.Invoke(itemId, progress);
        public void RaisePracticeResult(string itemId, bool success) { Log("PracticeResult", itemId + " " + (success ? "success" : "failure")); OnPracticeResult?.Invoke(itemId, success); }
        public void RaiseMenuPressed() { Log("MenuPressed", null); OnMenuPressed?.Invoke(); }

        private void Log(string name, string subject)
        {
            if (logEvents)
                Debug.Log("[Interaction] " + name + (string.IsNullOrEmpty(subject) ? string.Empty : "(" + subject + ")"), this);
        }
    }
}
