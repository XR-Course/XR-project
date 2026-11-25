# Student 1: XR interaction and input

Owner: Eser Sariyar. Owns the XR rig, controller input, locomotion, grabbing,
releasing, aiming and activating the extinguisher prop, hazard selection input and
the assembly-point arrival trigger. Does not own scoring, training rules,
environment visuals or UI.

Interaction components only **publish events**. They never change the training
state or score. `ScenarioInteractionBridge` forwards the events to Student 3's
`ScenarioCoordinator`, which decides what counts.

## Scenes

| Scene | Purpose |
| --- | --- |
| `Assets/Interaction/Scenes/InteractionTestScene.unity` | Stand-alone module demo with no scenario or office. Pick up the extinguisher, hold the trigger, aim at the orange target; the event board shows every event and the practice progress. Select **RESET** to put everything back, or **INSPECT** to test ray selection. Walk or teleport onto the green square to test the zone trigger. |
| `Assets/Scenes/OfficeTraining.unity` | Integrated training: a copy of Student 2's `OfficeBlockout` plus the XR rig, the props, Student 3's `ScenarioCoordinator` and the bridge. Training starts automatically on Play (temporary, until Student 4's start menu exists). |

`OfficeBlockout.unity` is **not modified**: the environment's saved-scene test
expects that scene to have no coordinator. After the environment scene changes,
regenerate the integrated scene with **Office Training > Interaction > Build
Integrated Office Training Scene**. **Build Interaction Test Scene** regenerates
the demo scene.

## Rig and locomotion

- `Complete XR Origin Set Up Variant` from the VR template: headset, two controllers,
  near-far (ray + direct) interactors, poke, teleport interactor and snap turn.
- Comfort (proposal section 5): smooth motion and smooth turn are turned off on both
  controllers, so locomotion is teleport plus snap turn only.
- Teleport areas are on `OfficeFloor`, `CorridorFloor` and `AssemblyFloor`
  (interaction layer `Teleport`). Furniture is not a teleport target.
- The rig starts at `SuggestedPlayerStart`. `DesktopPreview_ReplaceWithStudent1Rig`
  is disabled in the integrated scene but kept for the environment debug panel.

## Components

| Script | What it does |
| --- | --- |
| `InteractionEvents` (ScriptableObject, `InteractionEvents.asset`) | Event channel: `OnGrabbed(itemId)`, `OnReleased(itemId)`, `OnUse(itemId, pose)`, `OnUseEnded`, `OnInspected(targetId)`, `OnZoneEntered(zoneId)`, `OnPracticeProgress`, `OnPracticeResult(itemId, success)`, `OnMenuPressed`. Tick **Log Events** to see them in the Console. |
| `ToolInteractionController` | Reusable grab + use on an `XRGrabInteractable`. Stable item id, use point, short haptic on use. `ResetToStartPose()` drops the prop and puts it back without sending grab events. |
| `ExtinguisherPractice` | While held and triggered, a ray from the nozzle looks for a `PracticeTarget`. Aim time counts only while **armed** (Practice phase, not paused). Shows an aim line (green on target), a spray particle effect and publishes progress and the result. |
| `PracticeAimTracker` (plain C#) | The practice rule: `requiredAimSeconds` of spraying at the target (default 2.5 s) succeeds. An optional `maxSpraySeconds` makes long spraying without success a failed attempt (0 = off). |
| `PracticeTarget` | Aim volume around the fire (trigger collider) with "aimed" and "completed" rings. No fire logic. |
| `InspectTarget` / `HazardInspectable` | Ray/hand selection publishes `OnInspected`. `HazardInspectable` takes the id from Student 2's `HazardObject`, so ids are never typed twice. The office hazards get an invisible `Inspect volume (Student 1)` box because the props have no colliders. |
| `SafeZoneTrigger` | Fires `OnZoneEntered("assembly_point")` when the headset enters the box. It checks the head position every frame, so it works the same for teleport, walking and the simulator without physics on the rig. |
| `XRInputSetup` | Controller menu button (either hand), plus **Esc** on desktop, publishes `OnMenuPressed`. |
| `ScenarioInteractionBridge` (Integration) | Forwards events to the coordinator, arms the practice only in the Practice phase, toggles pause on the menu button, and resets props, practice and zones on `OnSessionReset`. |

Item and zone ids: the extinguisher is `extinguisher` (matches
`ScenarioRules.DefaultExtinguisherItemId`), and the assembly zone is `assembly_point`.

## Flow in the office

1. Inspection: point the ray at a hazard and press grip/select, and `ConfirmHazard(id)` is called.
2. Incident: grabbing the extinguisher (next to the office doorway) counts as choosing
   practice (Student 3's rule). Walking to the assembly point counts as evacuating.
3. Practice: hold the trigger and aim at the fire for 2.5 s, and `ReportPracticeResult(true)` is called.
4. Arrival: entering the assembly-point box calls `NotifySafeZoneReached()`, which completes the training.
5. Menu button: pause/resume. `ResetTraining()` puts the extinguisher back and clears the practice.

Using the extinguisher outside the practice branch is still published. The
coordinator records it as unsafe and never rewards it (FR-06).

## Assemblies

| Assembly | Depends on |
| --- | --- |
| `Office.Interaction.Core` | nothing (no UnityEngine) |
| `Office.Interaction` | Core, XR Interaction Toolkit, Input System, TextMeshPro |
| `Office.Interaction.Integration` | Interaction, `Office.Scenario`, `Office.Environment` |
| `Office.Interaction.Editor` | scene builder menu |
| `Office.Interaction.Tests` | EditMode tests |

The interaction assembly does not reference the scenario or environment, so it can
be reused or tested on its own.

## Tests

**Window > General > Test Runner > EditMode**, assembly `Office.Interaction.Tests`:

- `PracticeAimTrackerTests`: success time, disarmed, off target, interruptions,
  spray limit failure, single result per attempt, reset, invalid configuration.
- `ScenarioInteractionBridgeTests`: inspection, practice path to completion,
  evacuation first, practice result outside practice not rewarded, other items
  ignored, unsafe use, menu pause, no double forwarding.

Checked in the editor (Play mode, no headset): full practice path through the
real components in `OfficeTraining.unity` reaches Completed with 4/4 objectives,
and `ResetTraining()` restores the extinguisher pose and practice state.

## Not done yet / needs the headset

- Not yet tested on a Quest: grip/aim feel, the attach pose of the extinguisher in
  the hand, teleport comfort and frame rate (proposal section 9).
- The extinguisher placement (stand next to the office doorway), aim time and
  target size are first guesses for usability testing.
- `startTrainingOnPlay` on the bridge is a stand-in for Student 4's start menu;
  turn it off when the menu calls `StartTraining()`.
- Practice permission and all safety wording remain Student 3's / safety review.
