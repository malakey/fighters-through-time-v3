# CM-FIXB-PADUI: the Phase B pad-menu fix round

**Package:** combat and movement update (2026-10-16), Phase B fix round. Plan: `docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md`. Decisions: 9.1 and 9.2, with 5.1 and 6.1 for the sim latch (`D:/Projects/genre_review_2026-10-13/DECISIONS_combat_movement.md`). Findings: the `PADUI` array of `D:/Projects/genre_review_2026-10-13/fixB_inputs.json` (6 entries: B14, B22, B15, B16, B17, B18; B14 and B22 (a) are the same defect). Where a checker's corrected fix differed from the finder's, I followed the corrected fix.
**Branch:** `worktree-wf_a15de89f-42d-4`. **Base:** `main` at `740d712` (Phase A + Phase B merged, 3490 green). The worktree was created at the stale `d2e52e0`, so it was reset to `740d712` before any edit.
**Commits:** `663ec7d` (every fix and its tests), then this handoff.
**Nobody played the build.** Every claim below comes from a headless test, a headless smoke run or a reading of the code. There is no sim, snapshot, hash, Klotho, protocol or save-schema change. **Fighter hash moves: no.**

## 1. Findings fixed

### B14 / B22 (a): the menu-press consume dropped Jump's release edge

- **Before.** `InputManager.CaptureFrame` zeroed both `Pressed` and `Released` on the tick `ConsumeHeldPresses` covers. The Fighter sim's short-hop latch (`FighterMovementSystem.UpdateJumpLatch`, component 323 `ActionFlags` bit 1 `JumpReleasedSincePress`) changes only on edges. So a Jump let go of during a pause (every `PauseMenuBase.SetPaused(false)`, `LocalFighterPause` included, and a focus-loss auto-pause) never reached the sim as released. The rest of that airtime got the full-hop rise and the held-Jump apex hang with Jump up. Story was unaffected, because it reads `Held`.
- **Now.** The consume branch zeroes **only** `frame.Pressed`. A release starts no action. In production the only reader of `Released` is that sim latch (grepped: `UpdateJumpLatch` is the only one).
- **Left as is, per both checkers.** The `_seedHeldSlots` branch still zeroes both edges. A freshly claimed slot's `Released` is already `None`, because `AssignDevice` removes `_frames`.
- **Not changed.** `UpdateJumpLatch` still reads edges, not `Held`. Reading `Held` would turn hand-built test frames into short hops (plan §1.8).

### B22 (b): a shoulder held through a resume came back as a late fresh press

- **Before.** With the Ultimate affordable, an LB or RB pressed during a pause and held through the resume went `Up → Waiting` on the consume tick. Nothing was emitted, so there was nothing to consume. Three ticks later (`LeniencyFrames`) it was emitted as a fresh press, after the consume, and Special 2 or the movement ability cast three frames into the resume. With no affordable Ultimate the same press was correctly swallowed, so the rule depended on the meter.
- **Now.** `UltimateChordWindow.Step(..., bool settleHeld = false)`. `CaptureFrame` passes `settleChordHeld: ConsumesMenuPress(...)` through `SampleGodotDevice`. On that tick a raw-held component in `Up` or `Waiting` goes straight to `Emitting`, so it is in `Held` on the consume tick itself and its edge is consumed, as for every other button. A chord still masks (`maskNow` is unchanged), and a release inside a wait still gives its one held frame.
- **The second checker's correction is honoured.** The bit must be in `Held` on tick T, not first appear at T+1. Settling the state after sampling would have produced a fresh edge at T+1.

### B15: holding pad A for a full jump during a non-pausing bark armed hold-to-skip

- **Before.** `DialogueManager._Process` counted `ui_accept` toward hold-to-skip in every sequence. Pad A has been `ui_accept` since CM-PADUI, and it is also Jump. A full hop held on A during Level 15's `level_15.phase3_bark` (`PausesGameplay = false`) reached the 0.75 s threshold. On a first viewing it opened the skip `ConfirmModal` over the live final boss fight, and that modal ate the next jump.
- **Now.** While gameplay runs under the box (`IsGameplayLiveUnderTheBox`: the sequence did not pause gameplay and nothing else holds `SceneTree.Paused`), a held `ui_accept` that is also a held Jump (pad A, Space) does not count toward the hold.
  - The hold the hint names, **Interact** (`dialogue_hold_skip` formats `DescribeInteractBinding()`), still counts, and so does a menu-only confirm (Enter).
  - So **the send-off and the Homecoming stay hold-skippable** on their advertised button. Both are `PausesGameplay = false`.
  - The press itself still pages a line or finishes its reveal, as before (CM-PADUI Decision 5).
- **Both checkers warned against gating all hold-to-skip on `PausesGameplay`.** That gate would have removed the Homecoming's designed skip, so it was not used.

### B16: resuming a pause opened over a gameplay-pausing dialogue unpaused the world

- **Before.** `PauseMenuBase.SetPaused(false)` and `_ExitTree` wrote `SceneTree.Paused = false` unconditionally. The pause can open over a `PausesGameplay` sequence (Start, or a window focus loss). Resume, pad B, or a controller-lost resolution then ran the hero, mobs and boss under the still-open box on free time, because the Integrity clock stays held by the Dialogue flag. Every pad A then both paged a line and jumped.
- **Now.** The tree pause is owner-aware between the two Story owners:
  - **`DialogueManager.HoldsGameplayPause`** (new, public) is true while the sequence took the tree pause.
  - **`PauseMenuBase.SetPaused`** writes `tree.Paused = paused || DialogueHoldsGameplayPause()`. **`_ExitTree`** writes `tree.Paused = DialogueHoldsGameplayPause()`. Every resume path and a teardown therefore hand the tree back to a dialogue that still holds it. The `PauseMenu` Integrity flag and the one-tick press consume are unchanged.
  - **`DialogueManager.ReleaseGameplayPause`** skips its `tree.Paused = false` write while `PauseMenuBase.IsAnyOpen`, so a dialogue that ends or is freed under an open pause cannot unfreeze the world behind the menu. Either teardown order ends unpaused.
- **A hazard the fix had to close.** `GameManager` is an Inherit-mode autoload, and its threaded scene load completes in `_Process`, which a paused tree stops. With the hand-back alone, **Restart Level** and **Quit to Main Menu** from a pause opened over a pausing dialogue would have kept the tree paused and stalled the load forever. Both now close through the new **`PauseMenuBase.ClosePauseForSceneExit()`** (`protected internal`), which releases the tree outright. The dialogue box leaves with the scene, and its own `_ExitTree` hands back its share. Behaviour there is exactly as before the fix.
- **Corrected fix followed.** The second checker rejected "record the paused state at open and restore it", because a restored pause with no owner is failure signature 4. The state is derived from the live owner instead.

### B17: a keyboard player 2's Up arrow moved player 1's footer focus

- **Before.** `CharacterSelectScreen.IsAnotherPlayersMenuPress` swallowed another device's GUI event only when it matched a menu-drive action **and** a token action. The keyboard Up arrow is only Godot's built-in `ui_up` (`gameplay_up` is W, stick up and d-pad up). So the arrow walked player 1's focus from the Remote Play notice onto Back, and player 1's next pad A (meant as the token's Up) pressed Back and threw both players out.
- **Now.** Every directional focus move (`ui_up` / `ui_down` / `ui_left` / `ui_right`, new `DirectionalFocusActions`) from a device that is not player 1's is swallowed during the token flow, whether or not it is a token action. The old intersection still governs confirms and Tab. So a joined keyboard player 2's **Enter** still presses the focused button (CM-FIXA-INPUT's pin `AKeyboardJoinPressOnAFocusedButtonOnlyJoins`), and Tab still passes. The class comment and the method summary now describe the rule.

### B18: player 1's join prompt did not return when player 1's pad disconnected

- **Before.** A pad disconnect reaches `InputManager.ApplyDeviceTopology`, whose drop loop removed the slot directly without raising `DeviceAssigned`. The select repaints its join prompts only on `DeviceAssigned`, so slot 0 was unclaimed while `P1JoinPrompt` stayed hidden. Player 2's prompt had the same gap.
- **Now.** The drop loop collects each dropped slot. After the loop, and before the FollowLastUsed keyboard fallback or the Joined reconnect refill, it raises `DeviceAssigned(slot, UnassignedDevice)` for each, the same contract `UnassignSlot` documents. A refill's own `DeviceAssigned` then follows. Existing subscribers are safe: `LocalFighterPause.OnDeviceAssigned` returns on `UnassignedDevice`, and `CharacterSelectScreen.OnDeviceAssigned` repaints both prompts. The loop iterates by index over a reused scratch list (`_droppedSlotScratch`), so it allocates nothing.
- **Why this fix and not a `PlayerDeviceDisconnected` subscription.** The test seam `SetConnectedJoypadsForTesting` calls `ApplyDeviceTopology` directly and never raises `PlayerDeviceDisconnected`, so only this fix is testable (both checkers).

## 2. Files changed

| File | Why |
|---|---|
| `scripts/Core/InputManager.cs` | B14 / B22: the consume keeps `Released`; `settleChordHeld` reaches the chord window. B18: `DeviceAssigned` on a topology drop. |
| `scripts/Core/InputManager.MenuPress.cs` | Doc comment only: release edges pass; a held shoulder is settled. |
| `scripts/Core/UltimateChordWindow.cs` | B22 (b): the optional `settleHeld` parameter, three lines of logic. |
| `scripts/UI/DialogueManager.cs` | B15: the Jump-shared confirm rule in `_Process` plus `IsGameplayLiveUnderTheBox`. B16: `HoldsGameplayPause`; `ReleaseGameplayPause` leaves the tree to an open pause. |
| `scripts/UI/PauseMenuBase.cs` | B16: the owner-aware tree write in `SetPaused` and `_ExitTree`, `ClosePauseForSceneExit`, `DialogueHoldsGameplayPause`. |
| `scripts/UI/PauseMenu.cs` | B16: Restart Level and Quit to Main Menu call `ClosePauseForSceneExit()` instead of `SetPaused(false)`. |
| `scripts/UI/CharacterSelectScreen.PadMenu.cs` | B17: `DirectionalFocusActions` and the swallow rule, plus comments. |
| `tests/Unit/PadMenuConfirmCancelTests.cs`, `PadMenuFighterSelectTests.cs`, `UltimateChordWindowTests.cs` | §4. |

`DialogueManager.StartSequence`, the hold's `_skipHoldAwaitsRelease` guard, `LocalFighterPause` and `CharacterSelectScreen.cs` itself were not touched. Also untouched: the design, the contracts, the ledger, AGENTS.md and CLAUDE.md.

## 3. Decisions (agent choices; each belongs under `PROVISIONAL-PAD-MENU-RULES`)

1. **The consume spends press edges only** (amends CM-PADUI Decision 2). Release edges pass. The converse case is in §6.
2. **On the consume tick a held chord component is emitted at once** rather than held back for its partner. A chord whose partner is also down still masks into the Ultimate, which then reaches the game held with no edge.
3. **Hold-to-skip while gameplay runs:** a confirm that is also Jump never counts toward the hold. Interact and a menu-only confirm still do. This also applies to keyboard Space, which had the same exposure since Package 11 A5. "Gameplay runs" means the sequence did not pause gameplay and the tree is not paused by anything else.
4. **The tree pause is shared by the pause menu and a gameplay-pausing dialogue.** Each hands it back only once the other no longer holds it.
5. **A flow that leaves the scene from the pause releases the tree outright** (`ClosePauseForSceneExit`), so the threaded load can run.
6. **On the Fighter select, another player's directional focus move is always swallowed.** Its confirm and Tab follow CM-PADUI's token-intersection rule.
7. **A slot emptied by a disconnect raises `DeviceAssigned(slot, UnassignedDevice)`**, as an emptied join slot already did.

## 4. Tests

**New, +8. No case anywhere was rewritten.**

- **`PadMenuConfirmCancelTests` +5 (10 → 15):**
  - `AJumpLetGoOfDuringAPauseReachesTheFighterSimAsReleased` (B14 / B22 (a)): a fake pad's frames from the live capture drive a `FighterSimulation`. Jump is held into a rise, the pause opens, Jump is let go of, and the pause resumes. Asserts the resume frame's `Released` Jump, 323 bit 1 set after the next `Advance`, and the ×2.5 short-hop step.
  - `AShoulderPressedDuringAPauseReachesTheGameAsHeldNeverAsALatePress` (B22 (b)): the Ultimate is reported affordable each tick, RB is pressed during a pause, and it is held for 7 ticks after the resume. Asserts held and never pressed. Control: a fresh press after release still waits and presses once.
  - `APadAJumpHeldThroughANonPausingSequenceNeverArmsHoldToSkip` (B15): pad A still finishes the reveal; 80 frames of `ui_accept` + Jump held give 0 progress and no modal. Control: an Interact hold counts.
  - `ResumingAPauseOverAGameplayPausingDialogueLeavesTheWorldPausedUntilTheDialogueEnds` (B16): pad B, pad A on Resume, a `HandleFocusLost` pause resumed, and a `ResolveControllerLost` all leave `tree.Paused` true. Ending the sequence unpauses.
  - `APauseAndAGameplayPausingDialogueHandTheTreeBackInEitherTeardownOrder` (B16): a pause freed over the dialogue keeps the tree paused until the dialogue ends. A dialogue freed under an open pause keeps it paused until the pause closes. `ClosePauseForSceneExit` over a pausing dialogue releases the tree.
- **`PadMenuFighterSelectTests` +2 (6 → 8):**
  - `AKeyboardPlayerTwosUpArrowNeverMovesPlayerOnesFooterFocus` (B17): pad 0 is player 1 and the keyboard is player 2. The Up arrow leaves focus on the notice, and player 1's pad A then does not press Back. Control: player 1's d-pad Up still walks onto Back.
  - `AJoinPromptReturnsWhenThatPlayersPadDisconnects` (B18): drop pad 0 and `P1JoinPrompt` shows; reconnect it and the prompt hides; drop pad 1 and `P2JoinPrompt` shows.
- **`UltimateChordWindowTests` +1 (8 → 9, pure C#):** `OnAConsumeTickAShoulderAlreadyDownIsEmittedHeldAtOnceAndAChordStillMasks`. The test helper `Pad.Step` gained an optional `settle` argument.

**Red first.** With the tests in and the fixes out (only the default-off `settleHeld` parameter and the `ClosePauseForSceneExit` method existing, so the file compiled), all seven engine cases failed for exactly their named reason:

- "The resume tick dropped the release of a Jump let go of during the pause."
- "Tick 0 after the resume: the shoulder held through the pause is not held."
- "A jump held on pad A during a non-pausing sequence counted toward hold-to-skip."
- "Pad B backing out of the pause unpaused the world under a gameplay-pausing dialogue."
- "A pause freed over a gameplay-pausing dialogue unpaused the world under the box."
- "A keyboard Player 2's Up arrow moved the footer focus Player 1 holds."
- "Player 1's pad disconnected, but Player 1's panel does not say how to join again."

The run was `Failed: 7, Passed: 25, Total: 32`. The pure window case tests the new parameter itself. `ClosePauseForSceneExit`'s sub-case guards the hazard the fix introduced, so it could not be red first.

**Runs (lock held, `testhost` / `Godot*_console` drained first):**

- `--headless --import` (worktree setup): exit 0. `en.csv` was not edited, so no translation was regenerated.
- **Build:** 0 errors, and the two expected warnings (`KLSG_ECS004` on `FighterTuningComponent`, `CS8632` in the vendored GdUnit file). There is no `KLOTHO_DET002`.
- **Filtered, final code: 263 / 263.** The filter covered 31 suites touching the changed systems: the three `PadMenu*` suites, both `UltimateChord*` suites, `CharacterSelectScene`, `InputDeviceFollow`, `LocalFighterPause`, `StoryPause`, `PauseFocusLoss`, the four `Dialogue*` suites, `CampaignCompletion`, `EpilogueFlow`, `HubDifficultyLowering`, `HubDialogueSelector`, `MenuSceneContent`, `SessionExitGuard`, `PostBossChain`, `DrillSceneSmoke`, `FighterMatchFlowContent`, `FighterShortHop`, `HubArrival`, `MainMenuScene`, `Level00FlowScene`, `StoryLevelControllerBase`, `GamepadDefaultBinding`, `InputBindingSchema` and `FighterSpecialRecoveryCancel`.
- CM-FIXA-INPUT's consumed join press (`AKeyboardJoinPressOnAFocusedButtonOnlyJoins`) and every CM-PADUI no-double-act case pass unchanged.
- **Full suite (one run):**

  ```text
  Passed!  - Failed:     0, Passed:  3498, Skipped:     0, Total:  3498, Duration: 1 m 43 s - FightersThroughTime.dll (net10.0)
  ```

  That is **3490 + 8 = 3498** exactly, and `git grep -c "\[TestCase"` under `tests/` totals 3498 (3490 at `740d712`). The run has no abort or crash line and no runner exit code, so it is not signature 1, 2, 4, 5, 7 or 8.
- **Smokes, `--quit-after 240`:** `MainMenu.tscn` exit 0 and `CharacterSelect.tscn` exit 0, with no `ERROR`, `SCRIPT ERROR` or exception lines.

**Declared delta: +8 → 3498.**

## 5. Fighter hash moves

**No.** No file under `scripts/FighterSim/` changed. The only Fighter-visible change is an input frame on a consume tick. It now carries a `Released` edge the old consume wrongly dropped, and a held chord shoulder arrives held on that tick instead of as a fresh press three ticks later. No suite pins an absolute hash, and none moved.

## 6. Not fixed / open

- **The converse of B14 stays.** Suppose Jump is pressed during a pause and held through the resume while the latch already read "released" (a short hop in progress before the pause). The consume spends the press, so the sim keeps cutting the rise ×2.5 although Jump is now held. Story, which reads `Held`, does not. Fixing it would need the sim latch to read `Held`, which flips hand-built test frames to short hops (plan §1.8). Both checkers recorded it as consistent with the consume rule. It lasts at most one airtime.
- **`AssignDevice`'s `_frames.Remove` also loses a held Jump's release edge** on a mid-air device switch: in FollowLastUsed mode, pressing another device's button while holding Jump. This was noted by the first B14 checker. It is outside the finding and was left alone.
- **`NearCaptureBeat` (Level 9) pauses the tree itself** during its struggle and is not a dialogue. A Story pause opened over the struggle still unpauses the world on Resume. That is the same last-writer-wins class as B16, with a third owner. It was not in the findings, so it is not fixed. The two-owner query in `PauseMenuBase.DialogueHoldsGameplayPause` is where a general owner set would go.
- **An Interact hold still arms hold-to-skip during a non-pausing bark.** That is the advertised skip button. The finding and both corrected fixes covered the Jump-shared confirm only. Interact is also the world's Interact (channels, the Resonance Hold), but no channel sits inside the Level 15 boss arena.
- **Keyboard Space no longer arms hold-to-skip in a non-pausing sequence while gameplay runs.** This is the same rule as pad A (§3 item 3). Enter still does, and so does E (Interact).
- **The latent auto-advance variant** (a pausing, auto-advancing sequence ending under an open pause) is now harmless. The dialogue leaves the tree to the open pause. No authored sequence is both, as the checkers found.
- **`LocalFighterPause`'s exit flows still call `SetPaused(false)`.** Fighter and drill scenes have no `DialogueManager`, because the hub's leaves with the hub, so they are unaffected. They would need `ClosePauseForSceneExit` only if a Fighter scene ever gains a gameplay-pausing dialogue.
- **`docs/handoffs/CM_PADUI.md` §1** still says the consume gives "`Pressed = Released = None`" and that "the chord windows [are] untouched". Both are now stale. The handoff was left as the historical record.

## 7. For the closeout

**AGENTS.md:**

- **The `InputManager` autoload bullet** (CM-PADUI's owed text): `ConsumeHeldPresses` spends **press** edges only; release edges pass, for the sim's short-hop latch. On that tick a held Ultimate-chord shoulder is emitted held at once (`UltimateChordWindow.Step(settleHeld)`). A pad that disconnects raises `DeviceAssigned(slot, UnassignedDevice)` from `ApplyDeviceTopology`.
- **UI section, the pause contract** (beside "`PauseMenuBase` owns `SceneTree.Paused`"): the tree pause is now shared with a gameplay-pausing dialogue. A pause opened over one hands the tree back to it on every resume and on teardown (`DialogueManager.HoldsGameplayPause`). A dialogue that ends under an open pause leaves the tree to the pause. Restart Level and Quit to Main Menu close through `ClosePauseForSceneExit`, because the scene load needs an unpaused tree. The "whoever sets `SceneTree.Paused` hands it back" coding convention gains that sentence.
- **UI section, dialogue:** while gameplay runs under a non-pausing sequence, a Jump-shared confirm never counts toward hold-to-skip. Interact (the hint's button) and Enter do.
- **UI section, the Fighter select footer:** another player's directional focus moves are always swallowed.
- **Validation baseline:** 3498 on this branch alone (3490 + 8). Reconcile with the other Phase B fix branches at merge.

**CLAUDE.md:** the baseline paragraph only. Failure signature 4 is now less reachable from Story, because a Resume over a pausing dialogue no longer drops the pause. Its text needs no change.

**Ledger:**

- **Amend `PROVISIONAL-PAD-MENU-RULES`** (proposed by CM-PADUI) with §3 items 1–7. Item 2 of CM-PADUI's list ("the consume covers every device-driven slot for exactly the next tick") gains "press edges only; a held chord shoulder is settled". Item 5 (dialogue) gains the Jump-shared confirm rule and the shared tree pause. Item 6 (the footer) gains the directional swallow.
- **Optionally, a new owner-less row** for `NearCaptureBeat`'s third tree-pause owner (§6).

**Design master:** nothing new. `design-godot.md:2011` ("a `PausesGameplay` sequence suspends gameplay physics and combat inputs") and `:4053` (the pause menu "does not change dialogue … rules beyond the ordinary pause behavior") are now true of the build under an opened pause.
