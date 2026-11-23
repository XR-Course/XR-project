# Student 2: environment, hazards and incident-state demo

Open `Assets/Scenes/OfficeBlockout.unity` and press **Play**. In the **Game** view,
the Editor-only hazard test panel has one button per hazard. Confirmation shows a
green outline around the prop and logs `OnHazardConfirmed(id)` in the Console.
The disabled button and counter provide feedback even when the prop is outside
the fixed preview camera's view. Inspect the green outline in Scene view during
Play mode; a test button does not move the camera.

Use **Reset hazards** to clear confirmations and outlines. The pause/inspection
toggles let you test gated confirmation. Reset works in both states and restores
inspection to its configured starting value, with pause off. Leaving Play mode
also discards the demo session. These controls are not included in player builds.

The **incident test panel** is on the right of the Game view. Start the incident,
mark it resolved, select evacuation-required, or set it inactive. **Cycle states**
is a test shortcut through the four states, not a recommended training sequence.
The current state and last event are shown in the panel, and events are logged to
the Console. **View incident (desktop only)** aims the untracked preview camera at
the heater prototype; **Restore exit-route view** restores its initial pose.
These optional view buttons do not control a headset camera and are omitted from builds.

**Pause incident commands** rejects state commands without changing the current
state, freezes its particles and pauses its sound loops. **Reset incident** restores
`Inactive`, clears particles, stops loops and clears pause. The incident and
hazard panels deliberately have independent resets: the team's coordinator must
call both when resetting the full training session.

## IDs and integration contract

| ID | Neutral display name |
| --- | --- |
| `power_strip` | Power strip |
| `heater_papers` | Heater and papers |
| `damaged_cable` | Damaged cable |

Definitions are assets under `Hazards/Definitions/`. Their IDs are explicit,
case-sensitive, unique and stable; they do not depend on GameObject names.
Definitions contain no mutable session state, safety explanations or scores.

Namespace: `OfficeTraining.Environment`. Assembly: `Office.Environment`.

```csharp
// Student 1's input adapter: pass the selected HazardObject.HazardId.
bool accepted = registry.TryConfirmHazard(hazard.HazardId);

// Student 3's coordinator controls when confirmation is permitted.
registry.SetInspectionEnabled(true);
registry.SetPaused(false);
registry.ResetHazards();

// Subscribe/unsubscribe using your component's OnEnable/OnDisable.
registry.OnHazardConfirmed += HandleHazardConfirmed; // Action<string>
registry.OnHazardsReset += HandleHazardsReset;       // Action
```

`Hazards`, `ConfirmedIds`, `ConfirmedCount`, `InspectionEnabled`, `IsPaused` and
`IsConfigurationValid` expose current state for subscribers that join late.
Calls must occur on Unity's main thread. Exactly one confirmation event fires per
hazard per reset; it reports a confirmation, not that a real-world hazard is fixed.
Unknown/empty IDs, duplicate confirmations, paused/disabled inspection and inactive
hazards return false without emitting an event. Reset emits only the reset event.
Repeated reset calls are safe (each notifies reset listeners).

The registry validates its serialized list once when initialized. Missing references,
duplicate IDs/definitions and blank IDs disable all confirmation and log an error.
Configure references before Play mode; runtime registration is intentionally out of
scope. Destroyed or disabled targets cannot be confirmed. Re-enabling the component
does not clear the session; use explicit reset.

## Incident contract (proposed for Student 3 integration)

`IncidentController` exposes the read-only `CurrentState` and `IsPaused` properties.
Its state is separate from training phases such as Introduction or Inspection.

| State | Environment meaning |
| --- | --- |
| `Inactive` | Incident is not active; the initial/reset state. |
| `Active` | Coordinator has requested an active incident. |
| `Resolved` | Coordinator has declared the incident resolved. |
| `EvacuationRequired` | Coordinator has requested the evacuation-required presentation. |

```csharp
// Only the coordinator decides when training rules permit these commands.
bool changed = incident.TrySetState(IncidentState.Active);
incident.SetPaused(true);
incident.ResetIncident();

incident.OnIncidentStateChanged += HandleIncidentStateChanged; // Action<IncidentState>
incident.OnPauseChanged += HandleIncidentPauseChanged;         // Action<bool>
incident.OnIncidentReset += HandleIncidentReset;               // Action
```

- All four enum values can be applied directly by the coordinator. This component
  deliberately does not encode a training state machine or extinguisher success.
- Invalid enum values, duplicate states, paused commands and commands sent to a
  disabled controller/inactive object return false with no state-change event.
- Accepted commands update state before notifying listeners. Nothing starts or
  resolves automatically, including when all hazards have been confirmed.
- `SetPaused` publishes only actual pause changes. It does not alter `Time.timeScale`
  or pause another module. Future effect/audio listeners can respond to this event.
- `ResetIncident` works even when paused/disabled. It first commits inactive/unpaused,
  then emits a pause change if needed, a state change if needed, and finally the
  reset event. Repeated resets still emit reset, without fake state changes.
- Subscribers must initialize from the current properties when enabled and detach
  in `OnDisable`. The controller keeps session state across disable/re-enable.
- `FireScenarioVisuals` and `EnvironmentAudio` subscribe to the controller. They
  initialize from its current state when enabled and unsubscribe/clear on disable.

## Fire, smoke and audio prototype

`Incident Presentation` is a separate scene object at a fixed point on the heater.
This placement is illustrative, not a simulation of ignition, heat or electrical load.

| Incident state | Presentation |
| --- | --- |
| `Inactive` | No particles or incident sound. |
| `Active` | Small flames, light smoke, quiet fire loop and prototype alarm tone. |
| `Resolved` | Particles cleared and loops stopped immediately. Not a claim that a real space is safe. |
| `EvacuationRequired` | Same bounded fire/smoke/audio; the coordinator/UI owns evacuation instructions. |

- Two particle systems are capped at **56 particles total** (40 flame + 16 smoke).
  They use one 64x64 soft-particle texture and URP unlit materials. No particle
  collision, soft-particle depth texture, realtime fire light or smoke propagation.
- Pause freezes current particles and pauses loops; resume continues playback.
  Explicit reset clears leftovers even if the state was already `Inactive`.
- Confirmation now also plays one short spatial chime per hazard. Reset stops it;
  duplicate/invalid confirmations do not replay it. Hazard pause and incident pause
  remain separate; the coordinator must gate/pause the relevant modules together.
- Fire and alarm use mono spatial AudioSources with distance falloff, low default
  volume, no Doppler and no play-on-awake. At the default exit-facing camera the
  fire/chime may be quiet; use the incident view and begin with low speaker/headphone volume.
- The three WAVs and soft-particle texture were generated specifically for this
  prototype. No externally downloaded audio is used. The synthesized alarm is
  **not an official/local alarm recording**, and must not be taught as one.
- Listener checks and desktop renders do not establish Quest performance, stereo
  comfort or audio usability. Test those on campus before claiming acceptance.

## Ownership and remaining work

- No XR input, raycast selection, scoring or training-phase rules are implemented.
- Green outlines are separate child objects; source materials/furniture are not recoloured.
- The mock panel is for development only; Student 4 owns final headset guidance/UI.
- The assembly-area outline is a visual marker, not an arrival trigger.
- Incident states, simple fire/smoke and spatial audio are implemented. Final lighting
  refinement, safety review, team integration and target-headset profiling remain.
- Safety explanations and sign conventions still need appropriate local review.
  A populated power strip alone does not establish an electrical overload.

## Tests

Run **Window > General > Test Runner > EditMode** for `Office.Environment.Tests`.
Tests cover all three IDs, duplicate/invalid attempts, pause and inspection gating,
reset, configuration errors, inactive targets, read-only state and feedback restoration.
Incident tests cover all state pairs, invalid/duplicate commands, pause, reset and
disabled components. A saved-scene test enters Play mode, checks confirmations and
all incident states, and verifies that the two modules reset independently.
Additional Play-mode scene checks verify emission, particle budgets, spatial audio
configuration/playback, pause/resume, resolution, disable/re-enable and repeated reset.
