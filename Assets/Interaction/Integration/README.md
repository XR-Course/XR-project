# PoC guidance and demo

Open `Assets/Scenes/OfficeTraining.unity` and press Play, or build the scene from
Build Profiles. The integrated scene is the enabled build scene. The guidance
component is created at runtime only in `OfficeTraining`; it also survives
regenerating that scene with the interaction scene builder.

Point a controller ray at a button and use select. Start the simulation, inspect
the three objects, or choose Continue to demonstrate missed objects. During the
incident choose Evacuate or Practice. Practice uses the extinguisher prop and
target; both paths end by entering the assembly area. The results board offers
Retry. Pause is available on the board and on the controller menu button (Esc
on desktop); while paused, Resume, Restart and End are available. Retry and
Restart reload the training scene, including the rig and environment. Physical
headset position within the user's play space is not reset. Phase changes and
pause place the board in front of the headset.

`TrainingGuidance` subscribes to `ScenarioCoordinator` and uses its existing
commands and report. It does not determine safety rules or assessment results.
The scene builder keeps automatic training start disabled. The display is an
early prototype and requires testing for readability and controller interaction
in a real headset. It must not be presented as certified safety training.

Run all EditMode tests from Unity's Test Runner and manually verify both paths,
pause, early end, retry, locomotion, audio and headset performance before a demo.
