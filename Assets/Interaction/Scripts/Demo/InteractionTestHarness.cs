using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace OfficeTraining.Interaction.Demo
{
    /// <summary>
    /// Stand-alone demo for the interaction module (no scenario, no office): shows the last
    /// interaction events and practice progress on a world-space board, and resets every prop when
    /// the target with <see cref="resetTargetId"/> is selected or the reset key is pressed.
    /// Only used in InteractionTestScene.
    /// </summary>
    public sealed class InteractionTestHarness : MonoBehaviour
    {
        [SerializeField] private InteractionEvents events;
        [SerializeField] private TMP_Text board;
        [SerializeField] private string resetTargetId = "reset";
        [SerializeField] private int maxLines = 8;

        private readonly Queue<string> lines = new Queue<string>();
        private float progress;
        private string practiceState = "not finished";

        private void OnEnable()
        {
            if (events == null)
                return;
            events.OnGrabbed += HandleGrabbed;
            events.OnReleased += HandleReleased;
            events.OnUse += HandleUse;
            events.OnUseEnded += HandleUseEnded;
            events.OnInspected += HandleInspected;
            events.OnZoneEntered += HandleZoneEntered;
            events.OnPracticeProgress += HandleProgress;
            events.OnPracticeResult += HandleResult;
            events.OnMenuPressed += HandleMenu;
            Refresh();
        }

        // The channel asset outlives the scene, so every handler is removed again.
        private void OnDisable()
        {
            if (events == null)
                return;
            events.OnGrabbed -= HandleGrabbed;
            events.OnReleased -= HandleReleased;
            events.OnUse -= HandleUse;
            events.OnUseEnded -= HandleUseEnded;
            events.OnInspected -= HandleInspected;
            events.OnZoneEntered -= HandleZoneEntered;
            events.OnPracticeProgress -= HandleProgress;
            events.OnPracticeResult -= HandleResult;
            events.OnMenuPressed -= HandleMenu;
        }

        private void HandleGrabbed(string id) => Add("Grabbed " + id);
        private void HandleReleased(string id) => Add("Released " + id);
        private void HandleUse(string id, Pose pose) => Add("Use " + id);
        private void HandleUseEnded(string id) => Add("Use ended " + id);
        private void HandleZoneEntered(string id) => Add("Entered zone " + id);
        private void HandleMenu() => Add("Menu pressed");
        private void HandleProgress(string id, float value) { progress = value; Refresh(); }
        private void HandleResult(string id, bool success) { practiceState = success ? "SUCCESS" : "failed"; Add("Practice " + practiceState); }

        public void ResetDemo()
        {
            foreach (ToolInteractionController tool in FindObjectsByType<ToolInteractionController>(FindObjectsSortMode.None))
                tool.ResetToStartPose();
            foreach (ExtinguisherPractice practice in FindObjectsByType<ExtinguisherPractice>(FindObjectsSortMode.None))
            {
                practice.ResetPractice();
                practice.SetArmed(true);
            }
            foreach (SafeZoneTrigger zone in FindObjectsByType<SafeZoneTrigger>(FindObjectsSortMode.None))
                zone.ResetZone();
            progress = 0f;
            practiceState = "not finished";
            lines.Clear();
            Add("Reset");
        }

        private void HandleInspected(string id)
        {
            if (id == resetTargetId)
                ResetDemo();
            else
                Add("Inspected " + id);
        }

        private void Add(string line)
        {
            lines.Enqueue(Time.time.ToString("0.0") + "s  " + line);
            while (lines.Count > maxLines)
                lines.Dequeue();
            Refresh();
        }

        private void Refresh()
        {
            if (board == null)
                return;
            board.text = "<b>Interaction test</b>\n" +
                         "Grab the extinguisher, hold trigger, aim at the target.\n" +
                         "Practice: " + Mathf.RoundToInt(progress * 100f) + "%  (" + practiceState + ")\n\n" +
                         string.Join("\n", lines);
        }
    }
}
