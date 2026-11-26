using System;
using System.Collections;
using System.Text;
using OfficeTraining.Environment;
using OfficeTraining.Scenario;
using OfficeTraining.Interaction.Onboarding;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace OfficeTraining.Interaction.Integration

{
    /// <summary>
    /// Minimal world-space PoC guidance. It presents coordinator state without owning training rules.
    /// Created only for OfficeTraining so rebuilding that scene does not remove the guidance.
    /// </summary>
    public sealed class TrainingGuidance : MonoBehaviour
    {
        private ScenarioCoordinator coordinator;
        private TutorialController tutorialController;
        private Transform board;
        private TextMeshPro heading;
        private TextMeshPro body;
        private GameObject[] buttons;
        private TextMeshPro[] buttonLabels;
        private Action[] actions;
        private bool restartRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "OfficeTraining") return;
            foreach (HazardDebugPanel panel in FindObjectsByType<HazardDebugPanel>(FindObjectsSortMode.None))
                if (panel.gameObject.scene == scene) panel.enabled = false;
            foreach (IncidentDebugPanel panel in FindObjectsByType<IncidentDebugPanel>(FindObjectsSortMode.None))
                if (panel.gameObject.scene == scene) panel.enabled = false;
            if (FindAnyObjectByType<TrainingGuidance>() == null)
                new GameObject("Training Guidance (Student 4)").AddComponent<TrainingGuidance>();
        }

        private void Start()
        {
            coordinator = FindAnyObjectByType<ScenarioCoordinator>();
            if (coordinator == null)
            {
                Debug.LogError("[Guidance] No ScenarioCoordinator found; guidance cannot start.");
                enabled = false;
                return;
            }

            CreateBoard();
            tutorialController = FindAnyObjectByType<TutorialController>();
            if (tutorialController != null)
            {
                SetBoardVisible(false);
                tutorialController.OnTutorialComplete += ShowSimulationBoard;
            }
            coordinator.OnPhaseChanged += OnPhaseChanged;
            coordinator.OnEventRecorded += OnEventRecorded;
            coordinator.OnPauseChanged += OnPauseChanged;
            coordinator.OnSessionReset += OnSessionReset;
            PlaceBoard();
            Refresh();
            StartCoroutine(RepositionWhenTrackingStarts());
        }

        private IEnumerator RepositionWhenTrackingStarts()
        {
            // At scene load the tracked camera can still be at the tracking origin (the floor).
            // Reposition once tracking is available, rather than anchoring the board there.
            while (coordinator != null && coordinator.CurrentPhase == TrainingPhase.Introduction)
            {
                InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if (head.isValid && head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    if (coordinator != null && coordinator.CurrentPhase == TrainingPhase.Introduction)
                        PlaceBoard();
                    yield break;
                }
                yield return null;
            }
        }

        private void OnDestroy()
        {
            if (coordinator == null) return;
            coordinator.OnPhaseChanged -= OnPhaseChanged;
            coordinator.OnEventRecorded -= OnEventRecorded;
            coordinator.OnPauseChanged -= OnPauseChanged;
            coordinator.OnSessionReset -= OnSessionReset;
        }

        private void OnPhaseChanged(TrainingPhase phase) { PlaceBoard(); Refresh(); }
        private void OnEventRecorded(SessionEvent entry)
        {
            if (entry.Type == SessionEventType.HazardConfirmed && entry.Accepted)
                Refresh();
        }
        private void OnPauseChanged(bool paused) { PlaceBoard(); Refresh(); }
        private void OnSessionReset() { PlaceBoard(); Refresh(); }

        public void SetBoardVisible(bool visible)
        {
            if (board == null)
                return;
            
            board.gameObject.SetActive(visible);

            if (visible)
            {
                PlaceBoard();
                Refresh();
            }
        }

        public void ShowSimulationBoard()
        {
            SetBoardVisible(true);
        }

        private void PlaceBoard()
        {
            if (board == null || Camera.main == null) return;
            Transform head = Camera.main.transform;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            board.position = head.position + forward * 1.6f + Vector3.down * 0.05f;
            board.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private void CreateBoard()
        {
            board = new GameObject("Guidance board").transform;
            board.SetParent(transform, false);
            var background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "Background";
            background.transform.SetParent(board, false);
            background.transform.localScale = new Vector3(1.42f, 1.05f, 0.025f);
            Destroy(background.GetComponent<Collider>());
            SetBoardMaterial(background, new Color(0.035f, 0.07f, 0.12f));
            heading = MakeText("Heading", new Vector3(0f, 0.42f, -0.025f), new Vector2(1.30f, 0.16f), 0.35f);
            body = MakeText("Instructions", new Vector3(0f, 0.10f, -0.025f), new Vector2(1.30f, 0.53f), 0.24f);
            body.enableAutoSizing = true;
            body.fontSizeMin = 0.12f;
            body.fontSizeMax = 0.24f;
            buttons = new GameObject[3];
            buttonLabels = new TextMeshPro[3];
            actions = new Action[3];
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
                button.name = "Guidance button " + (i + 1);
                button.transform.SetParent(board, false);
                button.transform.localPosition = new Vector3((i - 1) * 0.44f, -0.39f, -0.045f);
                button.transform.localScale = new Vector3(0.40f, 0.16f, 0.05f);
                SetBoardMaterial(button, new Color(0.08f, 0.34f, 0.48f));
                var interactable = button.AddComponent<XRSimpleInteractable>();
                interactable.selectEntered.AddListener(_ => actions[index]?.Invoke());
                buttons[i] = button;
                buttonLabels[i] = MakeText("Button label", new Vector3((i - 1) * 0.44f, -0.39f, -0.08f),
                    new Vector2(0.38f, 0.14f), 0.20f);
                buttonLabels[i].alignment = TextAlignmentOptions.Center;
            }
        }

        private static void SetBoardMaterial(GameObject target, Color color)
        {
            Material source = Resources.Load<Material>("GuidanceUnlit");
            if (source == null)
            {
                Debug.LogError("[Guidance] Missing Resources/GuidanceUnlit material.");
                return;
            }
            var material = new Material(source);
            material.SetColor("_BaseColor", color);
            target.GetComponent<Renderer>().material = material;
        }

        private TextMeshPro MakeText(string name, Vector3 position, Vector2 size, float fontSize)
        {
            var text = new GameObject(name).AddComponent<TextMeshPro>();
            text.transform.SetParent(board, false);
            text.transform.localPosition = position;
            text.rectTransform.sizeDelta = size;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            return text;
        }

        private void SetButton(int index, string label, Action action)
        {
            actions[index] = action;
            buttons[index].SetActive(action != null);
            buttonLabels[index].gameObject.SetActive(action != null);
            buttonLabels[index].text = label;
        }

        private void Refresh()
        {
            if (coordinator == null || board == null) return;
            for (int i = 0; i < buttons.Length; i++) SetButton(i, "", null);

            if (coordinator.IsPaused)
            {
                heading.text = "PAUSED";
                body.text = "The simulation is paused.\nYou can resume, restart or end the session.";
                SetButton(0, "RESUME", coordinator.Resume);
                SetButton(1, "RESTART", RestartScene);
                SetButton(2, "END", coordinator.EndTraining);
                return;
            }

            switch (coordinator.CurrentPhase)
            {
                case TrainingPhase.Introduction:
                    heading.text = "FIRE SAFETY | PROTOTYPE";
                    body.text = "Use the controller ray and select to press buttons and inspect objects. " +
                        "Teleport to move; use grip to grab.\n\nThis is a simulation, not certified safety training.";
                    SetButton(1, "START", coordinator.StartTraining);
                    break;
                case TrainingPhase.Inspection:
                    heading.text = $"INSPECT THE OFFICE  {coordinator.HazardsFound}/{coordinator.HazardTotal}";
                    body.text = "Find and select three objects in the office. " +
                        "You may continue before finding all of them; missed items appear in the report.";
                    SetButton(0, "CONTINUE", coordinator.FinishInspection);
                    SetButton(2, "PAUSE", coordinator.Pause);
                    break;
                case TrainingPhase.Incident:
                case TrainingPhase.Decision:
                    heading.text = "SIMULATED INCIDENT";
                    body.text = "Choose to evacuate or use the marked practice interaction. " +
                        "Evacuation remains available. This is not real-world emergency guidance.";
                    SetButton(0, "EVACUATE", coordinator.ChooseEvacuate);
                    if (coordinator.Rules.PracticePermitted)
                        SetButton(1, "PRACTICE", coordinator.ChoosePractice);
                    SetButton(2, "PAUSE", coordinator.Pause);
                    break;
                case TrainingPhase.Practice:
                    heading.text = "SIMULATED PRACTICE";
                    body.text = "Grab the extinguisher prop. Hold the trigger and aim at the marked target. " +
                        "You may stop the exercise and evacuate at any time.";
                    SetButton(0, "EVACUATE", coordinator.ChooseEvacuate);
                    SetButton(2, "PAUSE", coordinator.Pause);
                    break;
                case TrainingPhase.Evacuation:
                    heading.text = "GO TO THE ASSEMBLY POINT";
                    body.text = "Follow the marked route and enter the assembly area to finish the simulation.";
                    SetButton(2, "PAUSE", coordinator.Pause);
                    break;
                case TrainingPhase.Completed:
                    heading.text = "SESSION SUMMARY";
                    body.text = FormatReport(coordinator.GetReport());
                    SetButton(1, "RETRY", RestartScene);
                    break;
            }
        }

        private void RestartScene()
        {
            if (restartRequested) return;
            restartRequested = true;
            SceneManager.LoadSceneAsync(gameObject.scene.buildIndex);
        }

        private static string FormatReport(TrainingReport report)
        {
            var text = new StringBuilder();
            text.AppendLine(report.EndedEarly ? "Session ended early." : "Simulation completed.");
            text.Append("Found: ").AppendLine(Names(report.HazardsFound));
            text.Append("Missed: ").AppendLine(Names(report.HazardsMissed));
            text.Append("Decision: ").AppendLine(DecisionLabel(report.Decision));
            text.Append("Practice: ").AppendLine(PracticeLabel(report.Practice));
            text.Append("Assembly point: ").AppendLine(report.ReachedSafeZone ? "reached" : "not reached");
            foreach (ObjectiveResult objective in report.Objectives)
                text.Append(objective.Title).Append(": ").AppendLine(objective.Feedback);
            text.Append("Prototype only; not certified training.");
            return text.ToString();
        }

        private static string DecisionLabel(DecisionPath decision)
        {
            switch (decision)
            {
                case DecisionPath.Evacuate: return "evacuation";
                case DecisionPath.PracticeInPermittedCondition: return "practice";
                case DecisionPath.PracticeWhenNotPermitted: return "practice not permitted";
                default: return "none";
            }
        }

        private static string PracticeLabel(PracticeResult practice)
        {
            switch (practice)
            {
                case PracticeResult.Succeeded: return "completed";
                case PracticeResult.Failed: return "not completed";
                case PracticeResult.Abandoned: return "stopped";
                case PracticeResult.NotPermitted: return "not permitted";
                default: return "not attempted";
            }
        }

        private static string Names(System.Collections.Generic.IReadOnlyList<HazardInfo> hazards)
        {
            if (hazards.Count == 0) return "none";
            var names = new string[hazards.Count];
            for (int i = 0; i < hazards.Count; i++) names[i] = hazards[i].DisplayName;
            return string.Join(", ", names);
        }
    }
}
