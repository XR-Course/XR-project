# Student 3: scenario orchestration and assessment

Owner: Baris Cankül. Owns the training state machine, training rules, safe-choice
branching, session event log and results calculation. Does not own VR input,
environment visuals or UI.

## Layout

| Folder | Assembly | Contents |
| --- | --- | --- |
| `Scripts/Core/` | `Office.Scenario.Core` (no UnityEngine) | `TrainingStateMachine`, `AssessmentService`, `TrainingReport`, `SessionEvent`, `ScenarioRules`, `FeedbackText`, `ScenarioSimulation` |
| `Scripts/` | `Office.Scenario` | `ScenarioCoordinator` (scene component), `ScenarioConfig` (ScriptableObject) |
| `Editor/` | `Office.Scenario.Editor` | **Office Training > Scenario > Run Mock Simulations** menu |
| `Tests/Editor/` | `Office.Scenario.Tests` | EditMode unit and integration tests |

The core has no Unity dependency, so the rules can be tested without a headset
or a scene. Namespace: `OfficeTraining.Scenario`.

## Phases

```
Introduction -> Inspection -> Incident -> Decision -> Practice ---> Evacuation -> Completed
                                  |          |            \-------------^              ^
                                  \----------+---- evacuate ------------/              |
                                             \---- walk straight to assembly point ----/
```

| Command | Accepted in | Result |
| --- | --- | --- |
| `StartTraining()` | Introduction | -> Inspection |
| `ConfirmHazard(id)` | Inspection | Logs hazard; when all are found -> Incident (configurable) |
| `FinishInspection()` | Inspection | -> Incident; unfound hazards are reported as missed |
| `AcknowledgeIncident()` | Incident | -> Decision (optional UI step) |
| `ChooseEvacuate()` | Incident, Decision, Practice | -> Evacuation (from Practice: practice abandoned) |
| `ChoosePractice()` | Incident, Decision | Permitted: -> Practice. Not permitted: recorded as unsafe, -> Evacuation |
| `NotifyGrabbed(extinguisher)` | Incident, Decision | Counts as `ChoosePractice()` (configurable) |
| `NotifyUsed(extinguisher)` | Practice | Outside practice while the fire is live: unsafe, never rewarded |
| `ReportPracticeResult(bool)` | Practice | -> Evacuation; ignored outside Practice |
| `NotifySafeZoneReached()` | Incident .. Evacuation | -> Completed; from Incident/Decision counts as evacuating |
| `EndTraining()` | any but Completed, also paused | -> Completed, marked as ended early |
| `Pause()` / `Resume()` | any but Completed | All commands except reset/end are rejected while paused |
| `ResetTraining()` | always | Clears everything, -> Introduction, resets hazards and incident |

Every call is written to the session log with an outcome: `Accepted`, `Duplicate`,
`WrongPhase`, `WhilePaused`, `UnknownId` or `NotPermitted`. Rejected calls never
change state, so invalid order or duplicate triggers cannot leave the scenario stuck.
There are no timers: the same calls always give the same result.

## Integration

Add `ScenarioCoordinator` to the scene and assign `HazardRegistry`,
`IncidentController` and (optionally) a `ScenarioConfig` asset
(**Create > Office Training > Scenario Config**). Without a registry the coordinator
uses the three default hazard ids, so the UI can be built in a mock scene.

The coordinator drives the environment module:

| Phase | Hazard inspection | Incident state |
| --- | --- | --- |
| Introduction, Inspection | enabled only in Inspection | `Inactive` |
| Incident, Decision, Practice | disabled | `Active` |
| Evacuation | disabled | `Resolved` after a successful practice, otherwise `EvacuationRequired` |
| Completed | disabled | unchanged |

Pause and reset are passed to both `HazardRegistry` and `IncidentController`.

```csharp
// Student 1 (interaction): report what the learner does.
coordinator.ConfirmHazard(hazard.HazardId);           // goes through the HazardRegistry
coordinator.NotifyGrabbed("extinguisher");
coordinator.NotifyUsed("extinguisher");
coordinator.ReportPracticeResult(success: true);       // when the aim condition is met
coordinator.NotifySafeZoneReached();                   // assembly-point trigger

// Student 4 (UI): commands and data.
coordinator.StartTraining(); coordinator.ChooseEvacuate(); coordinator.ChoosePractice();
coordinator.TogglePause(); coordinator.ResetTraining(); coordinator.EndTraining();
coordinator.OnPhaseChanged += phase => ...;            // Action<TrainingPhase>
coordinator.OnEventRecorded += entry => ...;           // Action<SessionEvent>
coordinator.OnPauseChanged += paused => ...;           // Action<bool>
coordinator.OnReportReady += report => ...;            // Action<TrainingReport>, on Completed
coordinator.OnSessionReset += () => ...;
int found = coordinator.HazardsFound, total = coordinator.HazardTotal;
TrainingReport now = coordinator.GetReport();          // any time, e.g. progress view
```

All methods are also usable from UnityEvents in the Inspector.

## Assessment

There is no numeric score. The report lists four objectives, each with the
criterion written out and a short feedback message:

| Id | Achieved when |
| --- | --- |
| `hazards` | At least the required number of hazards were confirmed (default: all) |
| `safe_decision` | Evacuated, or practiced only when permitted, and no extinguisher use outside practice |
| `practice` | Only applies when the permitted practice was chosen: practice completed |
| `evacuation` | The assembly point was reached |

The report also contains found/missed hazards, the decision path, practice
result, unsafe action count, duration and a copy of the event log. All
learner-facing text lives in `FeedbackText.cs` so it can be reviewed against
official local fire-safety guidance in one place. It describes what happened in
the simulation and is not certified training content.

## Mock simulation

**Office Training > Scenario > Run Mock Simulations** runs six scripted sessions
(normal completion, evacuation first, unsafe use, invalid order/duplicates/pause,
reset mid-session, ended early) and logs each report with its event log.

## Tests

**Window > General > Test Runner > EditMode**, assembly `Office.Scenario.Tests`:

- `TrainingStateMachineTests`: normal completion, evacuation first, unsafe actions,
  practice branch, invalid order, duplicates, pause, end early, reset, timing.
- `AssessmentAndRulesTests`: rule validation, objective criteria and the mock simulations.
- `ScenarioCoordinatorTests`: coordinator wired to the real `HazardRegistry` and
  `IncidentController` (inspection gate, incident mapping, pause, reset).

## Open points for the team

- Who owns the assembly-point trigger (`NotifySafeZoneReached`)? The environment
  README says the assembly outline is only a visual marker.
- Student 1: please use the item id `extinguisher` (or change it in `ScenarioConfig`).
- Whether practice is permitted, and all feedback wording, need safety review.
