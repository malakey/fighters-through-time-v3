# GR-TF: Time Freeze off, code kept (decision 10b++, plan §1.3)

Branch `gr/tf`. **Nobody has played or looked at any of it.** Every claim is a headless suite.

## What shipped
- **`FTT.Core.TimeFreezeRules`** (`scripts/Core/TimeFreezeRules.cs`, engine-free): `Default` (const false), `Enabled`, and `internal static bool? EnabledOverrideForTesting`. A code constant, not a Settings toggle (which would be save state). Story only; nothing under `scripts/FighterSim/` names it.
- **Dormant while off:**
  - `StorySceneBootstrapper.Attach` builds no `TimeFreezeController` (`services.TimeFreeze` stays null) and attaches `FirstUseTooltipPresenter` with `sceneHasTimeFreeze: false`, so the tooltip is never offered, the Skip path included.
  - `TutorialCalibrationScript.RegisterRewindComplete` goes straight to `Done`; the `UseTimeFreeze` member and `RegisterTimeFreezeComplete` / `SkipTimeFreezeLesson` stay.
  - `Level00Controller.BeginTimeFreezeDrill` skips the drill when the rule is off. The `level_00.station_rewind` flavour line still plays, then the mobility intro follows. Level 0 is eight lessons.
  - `gameplay_time_freeze` stays in `project.godot` with `"events": []`. R (physical 82) and L3 (button 7) are held by no action. The entry is removed unconditionally from `InputManager.RemappableActions` and `InputShortcuts.DirectActions`, so it is not polled, not a Controls row, and not in the conflict set. A saved row stays in the file and is ignored on apply. `ActionLabelKey` still resolves.
  - `StoryHUD.RefreshTimeFreeze` hides `TimeFreezeIndicator` and returns while off. The node stays in the scene; the slab keeps its 484 px minimum width.
- **Still live (proved):** `TimeFreezeController`'s statics, `IStoryTimeFreezable`, the thaw cue and catalog rows, the `AudioSnapshot.TimeFreeze` ladder, the freeze sweeps and `StoryHUD.OnTimeFreezeStateChanged`'s `StatusClockSuspended`. `TimeFreezeOffSceneTests.ThePostLandingHoldAndTheFreezeSweepsStayLiveWithNoControllerInTheTree` runs a real death-rewind landing with the rule off and no controller in the tree: the hold runs and freezes a `story_hazard` probe, then thaws it.
- `TimeFreezeCooldownSeconds` stays dead save data, unclamped above (only negatives go to 0). No save, schema, protocol or component change. No `en.csv` key added or removed; no catalog row deleted.

## Tests
Declared +8, actual **+8**: `TimeFreezeOffRulesTests` (pure, 4: shipped rule and seam; eight-lesson chain with the verb off; legacy chain forced on; cooldown unclamped) and `TimeFreezeOffSceneTests` (engine, 4: bootstrapper and tooltip off and forced on; action defined with no events and absent from both lists and `BindableActions`; HUD indicator hidden, slab 484, visible again when forced on; Post-Landing Hold and sweeps live).
Changed in place, no count change: `GamepadDefaultBindingTests.TimeFreezeDefaultsToTheLeftStickClickAlone` became `TimeFreezeIsAuthoredWithNoEventsAndRAndLeftStickAreHeldByNoAction` (also pins that no action holds R or L3). Forced on through the seam in `try`/`finally`: `TimeFreezeTests.NoAuthoredLevelGatesProgressionOnTimeFreeze`, `TutorialCalibrationTests` (2 cases), `StoryHudPulpTests` (2), `StoryHudCompactTests` (1). `TimeFreezeBindingMigrationTests.ACustomizedRewindBindingMovesOntoTheTimeFreezeAction` now pins that the action is not remappable (the row still migrates).
Evidence: the first filtered run, with production changed and no test edits, failed exactly those 8 existing cases. With `Default` flipped to true, `TheShippedRuleIsOff…` fails (the other off-state cases set the override themselves).
Filtered run: `Passed! Failed: 0, Passed: 282, Total: 282` (TimeFreeze*, TutorialCalibration*, GamepadDefaultBinding, GamepadOverrideRepair, StoryHud*, FirstUseTooltips, InputBindingSchema, DefyBinding, SaveEnvelope, Level00*, PostLandingHold, ChronalRewind, HudAbilityIndicator, CoreMechanicCue, AudioSnapshotMixer, StoryLevelControllerBase, MenuLayoutBounds, UnusedTranslationKey, StoryAudioDirector, MoveList, SystemsCard).

## Provisional / deviations
- No provisional number. No deviation from the plan. `FirstUseTooltips.TimeFreeze` stays in `All` (nine ids; `GR-SCHED` owns the count change).
- `InputBindings.cs` was touched only for the `DirectActions` list and its comment.

## The docs owe (GR-DOCS)
Ledger `DEFER-TIME-FREEZE-OFF` (re-enable = flip `TimeFreezeRules.Default`, restore the action's events, `RemappableActions`, `DirectActions`; delete or re-enable after the human session); `VERIFY-TIME-FREEZE-ON-BACK-SHARED`, the Defy-refusal-under-freeze and "discard, never buffer" rules are moot but live in code. Amend: `design-godot.md` and its mirror (F03, Controls, Level 0 lessons nine to eight, HUD, audio), `TEMPORAL_STATE_CONTRACT`, `HUD_CONTRACT` (the indicator is hidden, slab 484), `COMFORT_SETTINGS`, `STORY_PERSISTENCE` (cooldown is dead data), `CHECKPOINT_RECOVERY`, `DEFENSIVE_EFFECTS`, `CAMPAIGN_VALIDATION`, `MIRROR_PARADOX`; `AGENTS.md` and `CLAUDE.md` (input, HUD, Level 0, baseline 4036 after this workstream).
`UiCaptureTargets.Huds.cs` still raises Time Freeze payloads for the `huds_*` targets; with the rule off those captures show no indicator, so re-capture and read them as such. Bots never pressed the verb, so bot batches are unaffected, but with slots rising and no escape tool the death-rewind pool is the only recovery besides Fonts (dossier risk 10).
