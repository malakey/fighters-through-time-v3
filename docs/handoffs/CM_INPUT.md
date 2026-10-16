# CM-INPUT — devices, the pad Ultimate chord, and the CPU jump hold

**Package:** combat and movement update (2026-10-16), Phase A. Plan: `docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md` (dossiers 2.1.5, 2.9.1, 2.9.2; §3.1 row; §3.3 brief).
**Decisions:** 1.1(5), 9.1, 9.2 (`D:/Projects/genre_review_2026-10-13/DECISIONS_combat_movement.md`).
**Branch:** `worktree-wf_408a1af8-cf8-4`, from the plan commit `735296f`.
**Commits:** `2ccc76a` (1.1(5)), `842de2a` (9.2 and the 9.1 InputManager core), `5bb49d9` (9.1 wiring), then this handoff.
**Nobody played the build.** Every claim below is a headless test, a smoke run or a reading of the code. All of it is local input processing before `PlayerInputFrame.Create`: no protocol change, no sim-state or snapshot change, no save change, no Klotho component.

## 1. Shipped

### 1.1(5) The CPU holds Jump for a full hop (`FIX-CPU-JUMP-HOLD`)

- `FighterCpuController` (input source, outside the snapshot): a pulse that carries Jump hands its hold to an input-side sustain (`_jumpSustained`, `_jumpSustainFrames`) instead of releasing at `EdgeHoldFrames` (2). While it is set, Jump is ORed into the frame's held bits.
- The sustain ends (`UpdateJumpSustain`, self-state only, no RNG draw) when any of these holds: `SelfVelocityYRaw <= -2 u/s` (`JumpSustainReleaseVelocityRaw`, just past the apex-hang band); grounded; hitstun; daze; no stocks; the exclusive Block hold of `ApplyHitstunDefense` takes over (it calls `EndJumpSustain`); or `JumpSustainMaxFrames` (45) ticks have passed.
- A pending press that carries Jump while the sustain runs first releases Jump for `EdgeReleaseGapFrames` (1) tick, then presses. So a double jump is still its own edge, one tick later than before. This amends `PROVISIONAL-MOVEMENT-FEEL-RULES` (9): the delivery revalidation (`RecoveryJump && RemainingJumps <= 0` drops the bit) is unchanged and reads the same self-state.
- `MirrorParadoxDecisionAdapter` gained `internal MirrorParadoxDecisionAdapter(int seed, CpuDifficulty difficulty, CpuBandTuning tuningOverride)`. The public constructor forwards `CpuBandTuning.BossOverride(difficulty)`. Nothing else in `MirrorParadoxController.cs` changed.
- The sim reads no held Jump today, so Fighter CPU-driven runs move only through the one-tick-later queued jump. Story's Mirror clone now gets full hops (its `_jumpHeld` latch no longer drops on airborne tick 3).

### 9.2 Pad Ultimate chord leniency and a live chord toggle (`USER-PAD-ULTIMATE-CHORD-LENIENCY`)

- **`FTT.Core.UltimateChordWindow`** (new, `scripts/Core/UltimateChordWindow.cs`). A pure struct in the `BlockToggleLatch` pattern. `Step(movementRaw, special2Raw, chordEnabled, affordable)` returns an `Output` with `MovementHeld`, `Special2Held` and `UltimateHeld`. It also has `Reset()`, `IsWaiting` and `LeniencyFrames = 3`. There is one state machine per component (Up / Waiting / Emitting / Masked):
  - When lenient (shortcut on and affordable), a lone component pressed while its partner is up waits for 3 frames.
  - The partner arriving gives the Ultimate alone, and both components stay masked until each is released.
  - If the window runs out, the component is emitted held, 3 frames late.
  - A release inside the window emits the component held for exactly one frame.
  - Not lenient means pass-through with the pre-9.2 bits (both components plus the Ultimate on the conjunction). The exception: a component a chord masked stays masked until it is released, so letting go of a spent chord never fires its components.
- **`InputManager.SampleGodotDevice`** steps the window for joypad slots only. Keyboard frames are untouched.
  - The chord comes from the recipe's live component bindings (Movement Ability and Special 2 on this pad), behind `ShortcutEnabled(InputShortcuts.UltimateAction, Joypad)`. That flag is now read at runtime, so the Controls tab's chord toggle is wired.
  - `ReadUltimatePressed` stays as the direct route. On the keyboard that is U. On a pad it is a single captured button, or the legacy multi-event conjunction (an unrepaired row); C01c capture stays legal.
- **Affordability.**
  - `InputManager.ReportUltimateAffordable(slot, bool)` stamps `Engine.GetPhysicsFrames()`. `IsUltimateAffordable(slot, tick)` is false when the report is more than `UltimateReadinessMaxAgeTicks` (2) ticks old.
  - Story: `PlayerController.IsUltimateAffordableForInput` (new partial `PlayerController.InputReadiness.cs`). It is true when the meter is full, the Ultimate slot is unlocked, the cast is not Suppressed and the hero is not time-frozen. It is reported once per tick by `ReportUltimateReadiness()`, skipped for `IsStoryHostile`.
  - Fighter: `FighterSimulationDriver.PushUltimateReadiness()` (new partial `FighterSimulationDriver.InputReadiness.cs`), called once after `Simulation.Advance`. It reports slot 0, and slot 1 when no CPU plays it. The condition is Influence ≥ `FighterMeterRules.Cap`, and neither `IsCasterBusy` nor `IsCaptured`.
  - The ability lockout is excluded in both modes.
  - A LAN match pushes nothing, so its chord passes through.
- **Resets.** The windows reset on device reassignment (`AssignDevice` / `UnassignSlot` / a topology drop), on `NotificationPaused` and on `NotificationApplicationFocusOut` (`InputManager._Notification` → `ResetChordWindows()`).
- **`project.godot`.** `gameplay_ultimate` lost its two `InputEventJoypadButton` events (9, 10); only keyboard U remains. This is C01c's "initially Unbound": no default single-button pad Ultimate.
- **Repair.** `InputBindingSet.RepairStaleUltimatePadChord` (called from `RepairGodot3ShoulderRows`, which `SaveManager.ApplySavedInputBindings` already runs on every load) rewrites a saved Ultimate pad half of exactly {9,10} or {4,5} to the project default. That default is now empty, so the half is dropped and the keyboard half is kept. It does nothing when the project default is that same pair. The old {4,5}→default rule for the Ultimate is folded into it. New constants: `InputBindingSet.LeftShoulderButton` (9) and `RightShoulderButton` (10).
- CPU, bot and Mirror sources (`SetInputSource`) bypass all of it.

### 9.1 Last-used device as P1, press-to-join on Fighter select (`USER-LAST-USED-DEVICE`)

- **`FTT.Core.LocalJoinMode { FollowLastUsed = 0 (default), Joined = 1 }`.** New InputManager members: `JoinMode`, `BeginLocalJoin()`, `EndLocalJoin()`, `IsSlotJoined(slot)` (an input source or an assigned device), `ApplyJoinLifetimeForDestination(path)` and `static DestinationKeepsLocalJoin(path)` (true for `FighterFlowRoutes.CharacterSelectScenePath` and any `LoadingVariant.FighterVersus` destination).
- **The device-activity poll** (`PollDeviceActivity`) runs once per tick, at the first `CaptureFrame` of the tick, before any slot is sampled. It reads only per-device state, never input events.
  - Pads: a new press among buttons 0–20 (`PolledPadButtons`), or the left stick crossing out of the device's `StickProfile` deadzone.
  - Keyboard: a new press of a key bound to any `RemappableActions`, `InputShortcuts.DirectActions` or menu action (`ui_accept/cancel/up/down/left/right`, `ui_pause`, `ui_dialogue_log`), or of a mouse button (Left, Right, Middle, plus any bound one). Mouse motion never counts.
  - The key list rebuilds when the new `InputBindingService.Generation` counter changes. It is bumped on every `ApplyAction` and `ResetToProjectDefaults`.
  - A device's first poll only records its state.
- **FollowLastUsed.** Slot 0 moves to the active device (`AssignDevice(device, 0)`). The switching press is a fresh edge because the slot's previous frame is dropped. The switch also drops the Toggle Block latch and the chord window.
  - If several devices move in one tick, the current device keeps the slot; otherwise the keyboard wins, then the lowest pad id.
  - A slot 0 with an input source is never moved.
  - Slot 1 is never filled.
  - `ApplyDeviceTopology` gives an emptied slot 0 the keyboard and fills nothing else (before 9.1 it handed slot 1 the first free pad).
  - `AutoAssignDevices()` is now "reset to FollowLastUsed with the keyboard on 0".
- **Joined.**
  - `BeginLocalJoin` keeps slot 0 and empties the other slots. It is a no-op while already joined, so Rematch and Back-to-select keep the join.
  - Each unclaimed device that makes a join press claims the lowest free slot (`ClaimLowestFreeSlot`). A join press is a face button A/B/X/Y, or any key or pad button bound to `ui_accept`, `gameplay_interact` or `gameplay_basic_attack`.
  - The claimed slot's next sample is seeded (`Pressed` and `Released` cleared), so the joining press does nothing else.
  - A slot whose pad dropped during the join takes the first free pad when one reconnects (`_slotsAwaitingReconnect`).
  - `EndLocalJoin` returns to FollowLastUsed and empties slots ≥ 1. `UnassignSlot` raises `DeviceAssigned(slot, UnassignedDevice)`.
- **Test seam: `InputManager.DeviceActivityProbeForTesting`** (`internal IInputDeviceProbe`, with the production default `GodotInputDeviceProbe` over `Input`). It replaces every raw device read, both in the poll and in the per-device sample, so a probed press is the same press the frame then carries. Setting it clears the poll's snapshots. Also new: `RestoreConnectedDevicesForTesting()`.
- **`CharacterSelectScreen`.**
  - `_Ready` calls `BeginLocalJoin()` for `FighterMatchOrigin.LocalVersus` only, and subscribes `DeviceAssigned` (unsubscribed in the new `_ExitTree`).
  - `IsPlayerTwoJoined` mirrors `InputManager.IsSlotJoined(1)` (true without an InputManager).
  - Player 2's token is painted only when P2 is joined or has locked a pick (`OpponentCursorActive`).
  - The P2 panel shows the authored `SelectPhase/Root/PlayersRow/P2Panel/P2JoinPrompt` label (`select_press_to_join`) and hides `P2Stats` until P2 joins.
- **`GameManager.LoadScene`.** One line: `InputManager.Instance?.ApplyJoinLifetimeForDestination(scenePath)`.
- **Controller lost.**
  - `PauseMenu.ResolveControllerLost(InputManager.KeyboardDevice)` assigns the keyboard to slot 0 and resumes. `_Input` routes any non-echo key press there.
  - `LocalFighterPause._Input` (new): outside a local join (Versus CPU, Holodeck, drills), a lost player-1 controller resolves on a key press (keyboard) or a pad face button (that pad), through `DeviceAssigned`. In Joined mode the per-slot modal is unchanged (Rebind / Forfeit, plus the reconnect refill above).
- Haptics (`ResolveDevice(0)`) and the dialogue glyph (`IsJoypadConnected(0)`) follow the active device with no change.
- `en.csv`: `# CM-INPUT` block with `select_press_to_join` ("Press confirm on any controller or keyboard to join"). `en.en.translation` was regenerated with `--import`.

## 2. Files changed

| File | Why |
|---|---|
| `scripts/FighterSim/FighterCpuController.cs` | 1.1(5): `EdgeHoldFrames` doc, the sustain fields and constants, `Sample`, `AdvanceEdgePulse`, `ApplyHitstunDefense` (one call), `UpdateJumpSustain` / `Begin/EndJumpSustain`. |
| `scripts/Enemies/MirrorParadoxController.cs` | The adapter constructor overload only. |
| `scripts/Core/InputManager.cs` | 9.1 and 9.2 (owned). Top-level `LocalJoinMode` and `IInputDeviceProbe` live in this file. The private sampling helpers became instance methods so they read the probe. |
| `scripts/Core/UltimateChordWindow.cs` (+ `.uid`) | New. |
| `scripts/Core/InputBindings.cs` | `RepairStaleUltimatePadChord`, the shoulder constants, `InputBindingService.Generation`, doc updates. |
| `project.godot` | `gameplay_ultimate` joypad events removed. Nothing else. |
| `scripts/Characters/PlayerController.cs` | One line: `ReportUltimateReadiness();` right after `CurrentInputFrame` is read at the top of `_PhysicsProcess` (see Deviations 2). |
| `scripts/Characters/PlayerController.InputReadiness.cs` (+ `.uid`) | New partial. |
| `scripts/FighterSim/FighterSimulationDriver.cs` | One line: `PushUltimateReadiness();` after `Simulation.Advance` in `_PhysicsProcess`. |
| `scripts/FighterSim/FighterSimulationDriver.InputReadiness.cs` (+ `.uid`) | New partial. Reads committed sim state only; nothing flows back into the sim. |
| `scripts/UI/CharacterSelectScreen.cs`, `scenes/menus/CharacterSelect.tscn` | The join and the prompt label. |
| `scripts/UI/PauseMenu.cs`, `scripts/UI/LocalFighterPause.cs` | Controller lost. |
| `scripts/Core/GameManager.cs` | The join-lifetime line in `LoadScene`. |
| `localization/en.csv`, `localization/en.en.translation` | `# CM-INPUT` block. |

Outside my ownership: none.

## 3. Decisions (each becomes a `PROVISIONAL-*` row)

- **`PROVISIONAL-ULTIMATE-CHORD-LENIENCY`**
  - `LeniencyFrames` = 3. The user said "2–3 frames" and the plan said 3.
  - While lenient, *any* tick with both components held is the chord. That includes a partner arriving after the first component's window ran out, which then masks the already-emitted component (the pre-9.2 conjunction also fired then).
  - A component a chord masked stays masked through a loss of affordability until it is released, so the post-cast release of LB+RB never casts the movement ability or Special 2.
  - The Story report goes at the top of `_PhysicsProcess` (Deviations 2).
  - Report freshness is 2 ticks (as planned). LAN pushes no report.
- **`PROVISIONAL-DEVICE-FOLLOW-RULES`**
  - The polled sets are pad buttons 0–20 (no triggers, no right stick), the left stick by the device's own deadzone, and keyboard keys from the gameplay, direct and menu actions plus three mouse buttons.
  - Tie-break: the current device keeps slot 0, then the keyboard, then the lowest pad id.
  - The join-press set: face buttons plus anything bound to `ui_accept`, Interact or Basic Attack (the select screen's confirm). This amends design :1836.
  - In Joined mode a reconnecting pad refills the slot a disconnect emptied (this keeps the pre-9.1 mid-match reconnect).
  - In FollowLastUsed an emptied slot 0 falls back to the keyboard.
  - A device's first poll reports nothing.
  - The P2 token shows when joined or locked.
  - The prompt copy.
  - The FollowLastUsed P1 resolution in `LocalFighterPause`.
- **`FIX-CPU-JUMP-HOLD`**
  - The release speed is 2 u/s (plan).
  - The cap is 45 ticks (plan).
  - The release-before-press lasts `EdgeReleaseGapFrames` (1) tick, so a sustained hop's next Jump edge arrives one tick later.
  - Hitstun, daze and no stocks end the hold; Suppress-input does not (the sim ignores input then, and the cap bounds it).

## 4. Deviations from the plan

1. **The probe seam is wider than "replaces the poll".** `DeviceActivityProbeForTesting` also replaces the per-device sample reads (`Input.IsJoyButtonPressed`, `GetJoyAxis`, `IsPhysicalKeyPressed`, `IsMouseButtonPressed`). Without that, a test cannot show the plan's own requirement that the switching press "acts as a fresh edge", because the frame would read the real Input singleton.
2. **The Story readiness line sits at the top of `_PhysicsProcess`, not the end.** `_PhysicsProcess` returns early in hitstop (up to 11 frames) and in the landing-tech lock (12). A report at the end would go stale (> 2 ticks) there and switch the window off mid-freeze. At the top it runs every tick and reports the state the previous tick and its physics callbacks left. That is the state the next tick's sample reads anyway, because the InputManager autoload samples before the hero processes. It is still one line in `_PhysicsProcess`; no other workstream declared that spot.
3. **The Mirror apex includes a queued air jump.** With `ApproachJumpPercent = 100`, the grounded decisions made before the first press is observed queue more jumps, so the clone double-jumps a few ticks into the arc. The case asserts the decided floor (apex ≥ 95 % of `StoryAirPhysics.SingleJumpRise`). Before the fix the whole two-press arc still peaked well below one held single jump (§6).
4. **`LocalFighterPauseTests`: two of the "three topology cases" changed.** `ReconnectingAJoypadKeepsExistingAssignmentsInsteadOfReshufflingThem` passes unchanged. `HapticDeviceRoutingTests` needed no change. `StoryPauseTests` has no controller-loss case, so nothing changed there.
5. **`GamepadOverrideRepairTests`: more in-place edits than the one listed.** Three other cases expected the Ultimate repaired *onto* LB + RB. Since 9.2 they expect the pad half dropped (see §5).
6. **`LocalFighterPause` gained a FollowLastUsed resolution path.** The plan only says the modal stays in Joined mode. Outside a join, P1 follows the last-used device, and without this path a pad-only Versus CPU / Holodeck player whose pad dropped could only Forfeit or reconnect and press Rebind, even with the keyboard right there. It mirrors the Story pause.
7. **The build now shows two of the three expected warnings.** `CS0649` on `InputManager.BlockModeOverrideForTesting` is gone, because `FakeInputDeviceProbe.Restore` and `InputDeviceFollowTests` assign it. `CS8632` and `KLSG_ECS004` remain. No `KLOTHO_DET002`.
8. **`CharacterSelectSceneTests`' `Teardown` helper (not a case) now calls `InputManager.Instance?.EndLocalJoin()`.** A Local Versus screen begins a join that only a scene load ends, and a test never loads one, so without this the join would outlive the suite.

## 5. Tests

**New, +24 (matches the declared +24):**

| Suite | + | Cases |
|---|---|---|
| `MirrorParadoxTests` | 1 | `TheCloneHoldsJumpForAFullHopInsteadOfShortHoppingEveryJump` |
| `FighterCpuBehaviorTests` | 2 | `JumpStaysHeldAfterItsPulseUntilTheFighterFallsAtTwoUnitsPerSecond`, `TwoQueuedJumpsStillProduceTwoPressedEdgesWithOneReleaseTickBetween` |
| `UltimateChordWindowTests` (new, pure C#) | 6 | `ALoneShoulderIsHeldBackExactlyTheWindowThenPressed`, `APartnerInsideTheWindowGivesOnlyTheUltimateWithBothMaskedUntilReleased`, `AShortTapStillYieldsExactlyOnePress`, `WithoutAnAffordableUltimateNothingIsDelayed`, `WithTheShortcutOffTheComponentsGiveNoUltimate`, `ARePressAfterTheChordIsAFreshEdge` |
| `UltimateChordStoryTests` (new, real physics frames) | 1 | `AFullMeterChordInsideTheLockoutCastsNoMovementAbility` (with a control: a lone LB still casts after its window) |
| `GamepadDefaultBindingTests` | 1 | `TheGamepadUltimateDirectSlotIsAuthoredEmptyAndTheKeyboardKeepsU` |
| `GamepadOverrideRepairTests` | 1 | `StaleShoulderAndGodot3UltimateRowsAreDroppedAndTheKeyboardIsKept` |
| `InputBindingSchemaTests` | 1 | `WithThePadChordOffAndNoDirectBindTheUltimateIsUnreachableAndNamed` |
| `InputDeviceFollowTests` (new) | 7 | `APadPressMovesPlayerOneAndAKeyMovesItBack`, `MouseMotionAndSubDeadzoneStickDriftDoNotMovePlayerOne`, `SlotTwoStaysUnassignedWithTwoPadsOutsideALocalJoin`, `ADeviceSwitchDropsTheToggleBlockLatch`, `AnInputSourceOnSlotZeroIsUnaffectedByDeviceActivity`, `APressTheSceneAlreadyHandledStillMovesPlayerOne` (the real `Input` singleton plus a node that marks every event handled), `ALoadToAStageKeepsTheJoinAndALoadToTheMenuOrHubEndsIt` |
| `CharacterSelectSceneTests` | 3 | `NoPlayerTwoTokenAppearsUntilPadBPressesAndThatPressConfirmsNothing`, `PlayerOnesDeviceCannotTakeTheSecondSlot`, `TheKeyboardCanJoinAsPlayerTwo` (all real frames) |
| `PauseFocusLossTests` | 1 | `AKeyboardKeyResumesAStoryControllerLoss` |

**Rewritten in place (before → after):**

- `GamepadDefaultBindingTests`:
  - `TheUltimateChordIsBothShouldersAndSpellsItsPresetRecipe` → `TheUltimateChordIsItsPresetRecipeOnBothShoulders` (pins the recipe's components on 9 + 10, not the Ultimate's own events).
  - `NoTwoActionsShareADefaultGamepadButtonOutsideTheUltimateChord` → `NoTwoActionsShareADefaultGamepadButton` (the exception is gone).
- `GamepadOverrideRepairTests`:
  - `AStaleUltimateChordBesideAReboundKeyMovesOntoTheShoulders` → `AStaleGodot3UltimateChordBesideAReboundKeyIsDroppedToTheEmptyPadDefault`.
  - Same name, with the Ultimate expectation changed from {Y, 9, 10} to {Y}: `AStaleMovementAbilityOnBackSharedWithTimeFreezeMovesToTheLeftShoulder` and `AStaleSpecialTwoOnGuideIsRepairedBesideTheStaleUltimateChord`.
  - `CurrentRowsEmptyPayloadsAndOldNumberDefaultsAreLeftAlone`: its "current" Ultimate row is now {Y}.
  - The synthetic `PostFixDefaults()` Ultimate is {U}.
- `InputBindingSchemaTests.SaveManagerRepairsAStaleGodot3UltimateChordBeforeApplyingIt`: the applied row is {Y} with an empty pad half (it was {Y, 9, 10}).
- `LocalFighterPauseTests`:
  - `ADisconnectOnlyClearsTheAffectedSlot`: it now runs inside `BeginLocalJoin`, where the reconnect refill still applies, and adds the FollowLastUsed half (slot 1 never filled).
  - `RebindingTakesTheFirstUnclaimedConnectedDevice`: a new pad is not auto-assigned; `TryAssignFirstFreeDevice(1)` takes it.
- `PauseFocusLossTests.ControllerLossPausesStoryAndAFaceButtonOnAConnectedPadResumes`: the `finally` restores the real pads (`RestoreConnectedDevicesForTesting`) instead of leaving an empty fake set.
- Helpers, not cases: `CharacterSelectSceneTests.Teardown` (ends the join), and the new non-suite `tests/Unit/FakeInputDeviceProbe.cs`.

**Shared test files touched (§3 maps):** `FighterCpuBehaviorTests`, `CpuRecoveryMatrixTests` (re-run only, no edit) and `MirrorParadoxTests` (+1, a separate method). No other workstream's case was edited.

**Suites run (filtered, lock held):**
- `MirrorParadoxTests|FighterCpu|CpuRecoveryMatrix|CpuMeleeBand|CpuVerbPolicy|FighterLedge`: **123/123** after the fix (the Mirror case failed before it, as intended).
- `UltimateChord|Gamepad|InputBindingSchema|PlayerInputFrame|StoryAbilityLockout|HapticDeviceRouting|LocalFighterPause|PauseFocusLoss|StoryPause|CharacterSelectScene`: 96/98 before the two topology rewrites, which were the expected failures.
- The full CM-INPUT gate list plus `SceneVisibleText|UnusedTranslationKey|ScriptTranslationKey|FighterLocalization|MainMenuScene|LoadingScreen|MenuSceneContent|PlayerInputFrame`: **217/217**.

**Load check and smokes (lock held):**
- `--headless --import` and `--quit`: exit 0, no errors.
- `MainMenu.tscn` and `HubWorld.tscn --quit-after 300`: exit 0, clean.
- `TestArena.tscn --quit-after 600` ×3: exit 0; "4 / 0 / 0 resources still in use" across identical runs, which is the documented nondeterministic `AuthoredResources` noise.
- `FighterStage_Paris.tscn` ×2: exit 0, 3 resources each time; no script error.

**Full suite (one run, lock held, leftovers drained first; `dotnet test FightersThroughTime.csproj --settings .runsettings`):**

```text
Passed!  - Failed:     0, Passed:  3252, Skipped:     0, Total:  3252, Duration: 1 m 29 s
```

That is **3228 + 24 = 3252**, exactly the declared running total in this worktree, and the `[TestCase]` attribute count under `tests/` is also 3252. Signatures checked:
- the total is full, so not signature 1, 2, 4 or 5;
- there is no abort or crash line after the summary, so not signature 7;
- there are no Story cast failures, so not signature 8.

The build in that run showed only `KLSG_ECS004` and `CS8632` (Deviations 7). GdUnit's "orphan nodes" warnings appeared as usual.

## 6. Measurements

- **The Mirror apex** (Einstein clone, Hard boss band with `ApproachJumpPercent = 100`; the approach arc, including the queued air jump; first Jump press at tick 4):
  - before **131.4 px**;
  - after **239.1 px**;
  - a held single jump rises **186.4 px**.
- No Fighter hash is pinned by any suite. CPU-driven Fighter runs move by the one-tick-later queued jump; Story clone jumps move by the held hop.

## 7. Not delivered / open

- **No device glyphs** (STR-16, out of scope). The join prompt is text only.
- **The poll does not run while the tree is paused**, unless a paused UI captures a frame (the plan's recorded limit). So a join press or a device switch made in a paused menu takes effect on the first unpaused capture. The controller-lost paths use input events for that reason.
- **Two pads after keyboard-driven menus.** P1 is fixed at `BeginLocalJoin`. If the menus were driven by keyboard or mouse, P1 stays the keyboard: pad A then joins as P2, and pad B cannot join until a slot frees. This is inherent in "P1 follows until a second human joins" as the plan resolved it. §5 row 22 should check it.
- **A Fighter stage or `TestArena.tscn` launched directly** (outside the select) never begins a join, so a local human P2 has no device. This is a dev path only.
- **`pause_controller_disconnected`'s English** still says "press any face button, or choose Resume". A key press now also resumes; the copy was not changed (it is a shared row outside my block).
- **A drifting stick** that crosses its deadzone edge repeatedly can take P1 from the keyboard each time. The deadzone is per device in Settings.
- **LAN** reports no readiness, so the chord passes through there (LAN is de-scoped to Package 7).

## 8. For the closeout

- **AGENTS.md:**
  - The `InputManager` autoload bullet: device follow / press-to-join; the chord window and readiness reports; the pad Ultimate is the C01c recipe chord with an empty direct slot; `GamepadDefaultBindingTests` now pins no shared default button with no chord exception; the Ultimate repair drops {9,10} and {4,5}.
  - The C01c bullet: the pad Ultimate *is* the recipe at runtime; the toggle is wired; close the `VERIFY-GAMEPAD-ULTIMATE-CHORD` (a) / (c) sentences.
  - The CPU bullets (F19 recovery: a recovery jump is a held full hop, and a queued second jump arrives one tick later).
  - The Mirror bullet (the clone's jumps are full height).
  - The Fighter-menu bullet (Local Versus press-to-join).
  - The validation baseline.
- **CLAUDE.md:** the build paragraph's warning list (`CS0649` no longer appears); the baseline paragraph.
- **Design master + mirror** (line numbers at `e2c655a`; `design-godot.md` is unchanged since then):
  - **:660 and :715.** `InputManager` assigns devices by last use and local join, not only by `GetConnectedJoypads`.
  - **:1834–1837.** The join workflow. There is no `PlayerJoinManager`; `InputManager.BeginLocalJoin` on the Local Versus select does the job. A face, confirm or Interact press on an unclaimed device claims the lowest free slot, and the keyboard can join as P2. P1 follows the last-used device outside a join. "Players 2–4 require dedicated gamepads" no longer holds.
  - **:2286. This is a `DOC-MASTER-UNBOUND-BINDING` line: edit it by hand in each copy, and keep each copy's own Unbound / Severed wording.** The last cell becomes the chord leniency: while affordable, a lone LB or RB waits 3 frames for its partner; with the partner only the Ultimate fires. A captured direct pad bind stays legal.
  - **:4048–4053.** Single-player modes (Story, Versus CPU, Holodeck, drills) also resume a lost P1 controller on a keyboard press. In a local join a reconnected pad refills its own slot.
- **Contracts:**
  - `COMFORT_SETTINGS.md` :106 / :114 (C01c: the pad Ultimate recipe is read from the components through the leniency window while affordable; the direct pad slot is initially empty) and :134 (validation: last-used follow and press-to-join). **Both copies, by hand**; mind the Unbound lines :101–103, :128, :130 and :136.
  - `CPU_RECOVERY.md`: recovery jumps are held full hops; a queued second jump waits one release tick. Both copies.
  - `MIRROR_PARADOX.md`: the clone holds Jump for a full hop. Both copies.
  - `MULTIPLAYER_DELIVERY.md` :35: its wording still holds, so no edit is needed.
- **Ledger:**
  - New: `FIX-CPU-JUMP-HOLD`, `USER-LAST-USED-DEVICE` (KIT-03), `PROVISIONAL-DEVICE-FOLLOW-RULES`, `USER-PAD-ULTIMATE-CHORD-LENIENCY` (CMB-04, KIT-10), `PROVISIONAL-ULTIMATE-CHORD-LENIENCY`.
  - Amend: `PROVISIONAL-MOVEMENT-FEEL-RULES` (9) (the queued jump arrives one tick later behind the release tick) and `PROVISIONAL-GAMEPAD-OVERRIDE-NO-MIGRATION` (1) (the Ultimate pad half {9,10} or {4,5} is dropped to the empty default).
  - Close: `VERIFY-GAMEPAD-ULTIMATE-CHORD` (a) and (c).
