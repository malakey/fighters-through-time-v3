# GR-TRAIN handoff (gameplay review 2026-11-03, ruling 9c, Q5)

**Nobody has played or looked at any of it.** Every claim below is a headless suite, a smoke run or a mutation check.

## What shipped
A third Holodeck console entry, **hub console only** (the front-end Versus CPU panel has none): `TrainingRoomButton` after the Calibration Drills entry. It opens `scenes/ui/TrainingRoom.tscn`, a sandbox on the drills' Sealed stage (`florence_workshop`) with the campaign hero (full normalized kit, `applyStoryProgression: false`) against a dummy (`joan`). No simulation change: the dummy is local input through `InputManager.SetInputSource(1, ...)`, and every readout comes from read-only getters.

## API
- `FighterMatchOrigin.Training = 4` (appended). `FighterFlowRoutes`: `PauseExit`, `ResultsActions`, `ResultsLabelKey`, `ResultsDestination` and `SelectBack` treat Training as the Holodeck (hub in front of the console, `ArriveAtHolodeckConsole`); `ReturnToShip` also ends the Training origin (back to `LocalVersus`).
- `HolodeckConsolePanel.ApplyTrainingRouteToSession()` (sets the origin, clears a stale calibration route, returns `TrainingRoom.ScenePath`); `MakeSettingsRow`, `MakeRowLabel`, `AddOptionRow` became `internal static` for reuse.
- `scripts/UI/TrainingRoomDummy.cs`: `TrainingBlockMode`, `TrainingJumpMode`, `TrainingHitstunResponse`, `TrainingDummySettings`, `TrainingDummyContext`, `TrainingDummyCommand`, pure `TrainingDummyRules.Step`, `TrainingDummySource : IPlayerInputSource`.
- `scripts/UI/TrainingReadouts.cs`: pure `TrainingReadoutSample`, `TrainingReadoutTracker` (last hit and total damage, stun applied, startup/active/recovery frames of the last move, frame advantage measured when the student is free again, `FramesSinceHit`).
- `scripts/UI/TrainingRoomPanel.cs`: centred glass settings menu (Block, Jump, Hitstun response; Resume, Reset, Return to Time-Ship). Owns the tree pause while open, hands it back on close and in `_ExitTree`; only lowers a pause it raised.
- `scripts/UI/TrainingRoom.cs`: the scene root. Intercepts the Pause action in `_Input` (unless a pause menu is already open) and opens the panel; the driver's own `LocalFighterPause` stays reachable through focus loss and its Exit lands in the same place. `_ExitTree` clears player 1's input source and restores `FighterOpponentType`. A dummy KO (after 150 frames) or a finished match rebuilds the sandbox, as Reset does.
- Settings are per visit and never saved (they live on the room and the panel). Nothing touches the save, the session's match settings or `ActiveSaveSlot`.
- UI capture targets: `select_training_settings`, `training_room`.
- `en.csv` block `# GR-TRAIN`: 17 keys (declared "about 14"). **`localization/en.en.translation` is not committed; the orchestrator must run `--import`.** Two tests (`HolodeckConsoleTests`, `TrainingRoomPanelTests`) assert resolved text and fail on an uncompiled translation.

## Provisional numbers (`PROVISIONAL-TRAINING-ROOM`)
Block-after-hit window 60 frames; jump cycle 60 (Always) / 180 (Timed), held 24 frames (a full hop); DI is the stick toward the stage centre; dummy facing never changes (a student crossing over makes it block the wrong way); KO reset delay 150 frames; the 180-frame countdown replays on every Reset; readout slab 900 px at the bottom centre; the dummy character is fixed (`joan`, as in the drills); the panel's rows reuse the console's metrics.

## Tests: declared +14, actual +14
New: `TrainingDummyRulesTests` 4 (pure), `TrainingReadoutTrackerTests` 3 (pure), `TrainingRoomPanelTests` 3, `TrainingRoomSceneTests` 2, `FighterFlowRoutesTests` +1, `HolodeckConsoleTests` +1. In place, no count change: `MenuLayoutBoundsTests` (new surface `TrainingRoomPanel`, in `ClearsPromptBar`, measured at UI scale 1.0 and 1.4). Filtered run of the seven suites: `Passed! Failed: 0, Passed: 31, Total: 31`. Mutation checks seen failing: jump-wins-over-block, input source cleared on exit, hitstop skipped in phase counting.
Hash, save, protocol, component effects: none (schema v8, protocol v5, no component).

## Deviations
- The plan's "MenuSafeAreaFit" and layout-sweep row apply to the settings panel (centred overlay); the always-on readout slab is a HUD and is not in the sweep.
- Settings open through the Pause action rather than permanently on screen, because a focusable panel beside a live sandbox would take the pad's A and the arrow keys from the student.
- `FighterMatchOrigin.Training` shares the Holodeck's results list; a results screen is not reachable in practice.

## What the docs owe
AGENTS.md: the "Training Mode out of scope" exclusion becomes "a basic Holodeck training room only"; the origin enum; ledger rows `USER-HOLODECK-TRAINING`, `PROVISIONAL-TRAINING-ROOM`; the design master's Training Mode scope line. Not shipped: dummy character choice, frame-data for Specials beyond the phase counters, recording/replay, a dummy that faces the student.
