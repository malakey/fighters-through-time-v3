# CM-PADUI: pad A confirms and pad B backs out of every menu

**Package:** combat and movement update (2026-10-16), Phase B. **This workstream is not in the plan.** The orchestrator added it after CM-FIXA-INPUT found and confirmed the gap (`docs/handoffs/CM_FIXA_INPUT.md` §1 finding 1 and §4). Decision 9.1 makes pad-only play first-class, and the user's pad session (§5) depends on it.
**Branch:** `worktree-wf_a70ba1d0-06f-5`. **Worktree:** `D:/Projects/Fighters Through Time - V3/.claude/worktrees/wf_a70ba1d0-06f-5`. **Base:** `2d44fa9` (Phase A plus the A-round fixes, 3367 green).
**Commits:** `e3c5739` (menus: project.godot, the press consume, pause, dialogue, Settings, grid, credits, timeline panel, hub), `c8b1cf7` (the Fighter select footer, the player 1 join prompt, the controller-lost notice), then this handoff.
**Resumed work.** A first agent with this brief was stopped by a usage limit; the orchestrator committed its uncommitted tree as the WIP checkpoint `a1aa580`. I mapped it against the brief, kept what was right, fixed what was broken (three test compile errors, and every polled-input case pushed its press inside a physics tick, so none of them could catch the leak it named; see Deviations 3), replaced its hub refactor with one call line per branch, built what was missing (the global button-press consume, the Fighter select footer rules, the regenerated translation), dropped the orphan `tests/Unit/ZzPadUiProbeTests.cs.uid`, then `git reset --soft 2d44fa9` and recommitted in the two logical commits above. The branch carries no scratch file.
**Nobody played the build.** Every claim below comes from a headless test, a headless probe or a reading of the code. There is no sim, snapshot, hash, Klotho, protocol or save-schema change. **Fighter hash moves: no.**

## 1. Shipped

### `project.godot`: pad A is `ui_accept`, pad B is `ui_cancel`

- `ui_accept` = the built-in Enter (4194309), Keypad Enter (4194310) and Space (32) logical keycodes, **plus `InputEventJoypadButton` button 0 (A), device −1** (every pad).
- `ui_cancel` = the built-in Escape (4194305), **plus button 1 (B), device −1**.
- Nothing else in `[input]` changed. A stays Jump and B stays Interact.
- The two actions are menu navigation, not gameplay. They are not in `InputManager.RemappableActions` or `InputShortcuts.DirectActions`, so the Controls tab never lists them, `InputBindingService.BuildOverrides` never saves them, `ApplyAction` refuses to restore them from a saved payload, the load-time repairs (`RepairGodot3ShoulderRows`, `RestoreUnreachableUltimateChord`) never touch them, conflict detection never sees them (so A can be Jump and `ui_accept` at once), and `ResetToProjectDefaults` reloads them with their pad events. `PadMenuBindingTests` pins all of it. No code change was needed there.

### One press does one thing: `InputManager.ConsumeHeldPresses` (new partial `scripts/Core/InputManager.MenuPress.cs`)

- **`public void ConsumeHeldPresses()`**: on the **next** physics tick, every device-driven slot's sample carries `Pressed = Released = None`. Held state, the sticks, the Toggle Block latch and the chord windows are untouched, and a slot driven by an input source (the CPU, a bot, a test) is never touched. It covers exactly one tick (`Engine.GetPhysicsFrames() + 1`), so a call no capture follows cannot swallow a later press.
- **One call line** in `InputManager.CaptureFrame`, beside CM-INPUT's `_seedHeldSlots` reset: `if (ConsumesMenuPress(playerIndex, tick)) { Pressed = Released = None }`.
- **`InputManager._Input`** (new override in the same partial): a `ui_accept` press that a focused, visible, enabled, processing `BaseButton` is about to take (`FTT.UI.PadMenuInput.FocusedButtonTakesAccept`) calls `ConsumeHeldPresses()`. The autoload's `_input` runs after every scene node's (reverse tree order) and before the GUI, so a press a scene already claimed (a join press on the Fighter select) never reaches it, and the focus owner it reads is the one the GUI hands the press to. While the tree is paused it gets no input, and gameplay is not running either.
- **Callers:** `PauseMenuBase.SetPaused(false)` (every resume path, controller-lost included), `DialogueManager.EndSequence` when the sequence paused gameplay, `CharacterSelectScreen.ReturnToSelection`, the Fighter select's Move List / Systems Card `Closed` handlers, and `InputManager._Input`.

Why it lives in InputManager: the game reads A and B from device state on the next tick, while a menu acts on the event, so only the place that builds the frame can stop a menu press becoming a fresh Jump or Interact. Every surface with live polling under a focused button (the Story results overlay, Level 0's calibration prompt, a drill result card, the Fighter select's tokens) needs it, and one global rule at the autoload covers them all, including future screens. Nothing in the device-follow poll, the chord window or the latch changed.

### `scripts/UI/PadMenuInput.cs` (new, static)

`AcceptAction = "ui_accept"`, `CancelAction = "ui_cancel"`, and `FocusedButtonTakesAccept(Viewport, InputEvent)`: true for a `ui_accept` press (no echo) when the focus owner is a visible, enabled, processing `BaseButton`. Godot's buttons act on `ui_accept` on the **release** and never mark the press handled, so any other reader of the same press asks this first.

### The audit, reader by reader

| Surface | Change |
|---|---|
| `PauseMenuBase` (Story `PauseMenu`, Fighter `LocalFighterPause`) | `_UnhandledInput`: `ui_cancel` (pad B) resumes an open pause, exactly as Resume does (`SetPaused(false)`), and is always consumed. It is refused (swallowed) when `CanTogglePause()` refuses, which is the controller-disconnect modal. A child overlay (Settings, the Move List, the Dialogue Log, a `ConfirmModal`) answers its own cancel first, so one B closes one layer. New **`PauseMenuBase.IsAnyOpen`** (a static count of open pause menus; each menu counts once and uncounts in `_ExitTree`, so a freed menu cannot leave it set). |
| `DialogueManager` | `_UnhandledInput` returns while `PauseMenuBase.IsAnyOpen` (pad A on Resume or B backing out never advances a line underneath) and when `FocusedButtonTakesAccept` (a focused button outside the box takes this confirm). Hold-to-skip: a confirm already held when a sequence starts (`_skipHoldAwaitsRelease`) counts only after it has been let go, and an open pause zeroes the hold. `EndSequence` consumes held presses when the sequence paused gameplay. |
| `SettingsMenu` | `_UnhandledInput` closes on `ui_cancel` as well as the pause key. While a **keyboard** slot listens, pad B (which a keyboard slot cannot capture) cancels the listen. A **pad** slot still captures B as a binding, so B stays bindable. |
| `ResonanceGridPanel` | Its own `ui_accept` (buy the highlighted node) stands down when a focused Close or Respec button takes the press. |
| `CreditsController` | `GuiReleaseFocus()` at `_Ready`. The roll owns the screen: the Extras screen's focused Credits button no longer takes the skip press's release and reopens the roll. The opener re-applies its focus chain when the roll finishes (unchanged). |
| `TimelineRestartPanel` | New `_UnhandledInput`: `ui_cancel` that is not also the pause key (pad B) backs out, as the Back button does. Escape still opens the hub pause, as before. Its child confirmation answers its own cancel first. |
| `HubWorldController` *(outside my ownership)* | `_Input`: an Interact press that opened the Holodeck console or the Repository, started a crew dialogue or used the portal is marked handled (`ConsumeInteractPress()`, one call line per branch, no restructuring). The panel or dialogue box it opened is already in the tree when the same event reaches `_UnhandledInput`, and pad B is Interact **and** `ui_cancel`, so without this the B that opened the console or the Repository closed it again at once. The crew dialogue also lost its first line's reveal to the opening press (that half predates CM-PADUI). |
| `CharacterSelectScreen` | See the next section. |
| Unchanged, checked | `ConfirmModal` (already cancels on `ui_cancel` and is always a child of what it guards), `MoveListScreen`, `SystemsCardScreen`, `DialogueLogScreen`, `HolodeckConsolePanel`, `MainMenu`, `FighterPlayOptionsScreen`, `CalibrationDrillPicker` / `List` (all back out on `ui_cancel` and mark it handled), `BootSequenceOverlay` (swallows `ui_cancel`; its controller test hears a pad press in `_Input` before the GUI), `GameOverScreen` and `LevelResultsPanel` (buttons only; no B action, deliberately), `MatchResults` (A presses Rematch; the vote still reads Basic Attack / Interact), `InteractionArea` (opens surfaces in the `_unhandled_input` stage, so a panel it opens does not hear the same press), `ChronalRewindManager`'s collapse-beat skip (polled, unchanged), `TwoOptionPrompt` (the hero is disabled while it is open), the drill result card (the sandbox is disabled while it shows). |

### The Fighter select's footer (new partial `scripts/UI/CharacterSelectScreen.PadMenu.cs`)

The same pad drives a polled token: the stick and d-pad step it and are also Godot's built-in `ui_left` / `ui_right` / `ui_down` focus moves, A steps it up (Jump) and B locks it (Interact). Before CM-PADUI a pad could not press a GUI button, so a focus that wandered with the stick did no harm. Now it would: one stick Down took focus from the Remote Play notice to Move List, Up wrapped it onto Back, and the next A (meant as the token's Up) pressed Back and threw both players out. The rules:

- **The footer is player 1's** (`IsAnotherPlayersMenuPress`, called from `_Input` after CM-FIXA-INPUT's join-press guard). In a local join, once a **device** holds slot 0, a key or pad event that is a GUI confirm or focus move (`ui_accept`, `ui_up` / `down` / `left` / `right`, `ui_focus_next` / `prev`) **and** one of that player's token inputs (move left / right, Down, Up, Jump, Basic Attack, Interact, Block, Roll) from any other device is marked handled before the GUI sees it. The poll reads device state, so that player's own token still moves. Player 2's A is only ever player 2's token Up. A press that is only a menu key (Enter, Tab) still reaches the GUI, which keeps CM-FIXA-INPUT's pinned control (`AKeyboardJoinPressOnAFocusedButtonOnlyJoins`: a joined keyboard player 2's Enter presses the focused button). Mouse input and anything while an input source drives slot 0 are never swallowed. The stage phase is not touched: it is focus-driven UI that either player may drive, as before.
- **Up walks the footer, any other token input leaves it** (`ReleaseFooterFocusOnTokenInput`, one call line at the top of `HandleFrame` for player 0). Up is the one direction the token flow does not read, so Up (stick, d-pad, the Up arrow) walks player 1's focus round the wrapping chain (notice → Back → Systems Card → Move List → notice) and A presses the focused button. Any token input player 1 makes (Block, Roll, Basic Attack, Interact, Down or Jump pressed, or a horizontal axis step), whether or not it moved the token, hands a footer button's focus back to the chain's first entry (the notice). So a focus move that rode along with a token step never leaves A pointing at Back.
- **The press that closes an overlay or backs out is spent** (from the checkpoint): `OnOverlayClosed` (Move List, Systems Card) and `ReturnToSelection` (the stage phase, the Versus CPU panel) call `ConsumeHeldPresses`, because B is `ui_cancel` and also the token flow's Interact confirm. Without it, in Versus CPU, the B that closed the configuration panel re-locked player 1 and reopened the panel on the next tick.
- **The player 1 join prompt.** A new authored label, `SelectPhase/Root/PlayersRow/P1Panel/P1JoinPrompt` (`EmphasisLabel`, in cyan; the player 2 prompt is violet), text key `select_press_to_join_player_one`. It shows while **`CharacterSelectScreen.IsPlayerOneJoinPromptShown`** (Local Versus and slot 0 not joined: no device, no input source), refreshed on `DeviceAssigned` for slot 0 or 1. It never shows in Versus CPU or the Holodeck, where player 1 follows the last-used device.
- The stale `CancellingPlayer` comment ("today this guards a binding, not a live path") now says the B guard is live.

### `localization/en.csv` (regenerated `en.en.translation` with `--import`)

- New block `# CM-PADUI`: `select_press_to_join_player_one` = "Press confirm on any controller or keyboard to join as Player 1".
- In place: `pause_controller_disconnected` = "Controller disconnected. Reconnect it and press any face button, press any key to carry on with the keyboard, or choose Resume to continue." (since CM-INPUT a key press resumes a Story controller loss; CM-FIXA-INPUT §5 owed the wording).

## 2. Files changed

| File | Ownership | Why |
|---|---|---|
| `project.godot` | mine (`[input]` `ui_accept` / `ui_cancel` only) | The two pad events. Edited as text; the editor was never opened. |
| `scripts/Core/InputManager.MenuPress.cs` (new) + `.uid` | **outside** (the brief allows what the audit requires) | `ConsumeHeldPresses`, `ConsumesMenuPress`, `_Input`. Justified in §1: the only place that can stop a menu press becoming a fresh gameplay edge. |
| `scripts/Core/InputManager.cs` | **outside** | One call line in `CaptureFrame`. |
| `scripts/UI/PadMenuInput.cs` (new) + `.uid` | mine | The shared rule. |
| `scripts/UI/PauseMenuBase.cs`, `DialogueManager.cs`, `SettingsMenu.cs`, `ResonanceGridPanel.cs`, `CreditsController.cs`, `TimelineRestartPanel.cs` | mine (`scripts/UI` readers) | §1 audit table. |
| `scripts/UI/CharacterSelectScreen.cs`, `CharacterSelectScreen.PadMenu.cs` (new) + `.uid`, `scenes/menus/CharacterSelect.tscn` | mine | The footer rules, the consumes, the join prompt. `CharacterSelectScreen.cs` carries only call lines, the prompt label field and its refresh, and the comment fix. |
| `scripts/Environment/HubWorldController.cs` | **outside** | Six `ConsumeInteractPress()` call lines and the one-line helper (16 inserted lines, no restructuring). Without it pad B opens and closes the hub's panels with one press. |
| `localization/en.csv`, `localization/en.en.translation` | mine (`# CM-PADUI` block, plus the one owed wording) | §1. |
| `tests/Unit/PadMenuBindingTests.cs`, `PadMenuConfirmCancelTests.cs`, `PadMenuFighterSelectTests.cs` (new) + `.uid`s | mine | §5. |

Not touched: the sim, saves, the protocol, `InputBindings.cs`, `SaveManager.cs`, the design, the contracts, the ledger, AGENTS.md, CLAUDE.md.

## 3. Decisions (each becomes a `PROVISIONAL-PAD-MENU-RULES` item unless it says otherwise)

1. **A on `ui_accept`, B on `ui_cancel`, device −1.** From the brief (decision 9.1). Any pad confirms and backs out, which suits the shared menus; the Fighter select's per-player rule is item 6.
2. **The consume covers every device-driven slot for exactly the next tick.** On an unpause, a button either player pressed during the pause and still holds would otherwise come back as a fresh edge (a Jump, a perfect-block arming, a Story Interact). Cost: a press the other player makes on exactly that tick reads as held, and must be pressed again.
3. **A confirm a focused button takes is the menu's, everywhere** (`InputManager._Input`). If a button holds visible focus, A presses it, so suppressing the Jump edge is right by definition. Hidden, disabled and non-processing buttons do not count.
4. **Pad B resumes an open pause** exactly like Resume. It is refused (swallowed) under the controller-disconnect modal, like the pause key.
5. **Dialogue.** No line advances under an open pause menu. A confirm held from before a sequence starts never counts toward hold-to-skip until it is let go. This guards a held full hop on A (the apex hang now rewards holding Jump) and a held Interact channel. **In a non-pausing sequence, pad A now pages lines** like Basic Attack and Interact already did, and like keyboard Space (also Jump) always has: parity with the keyboard, not a new rule.
6. **The Fighter select footer is player 1's** (§1): another device's GUI events that are also its token inputs are swallowed, while pure menu keys pass (CM-FIXA-INPUT's pin). Up walks the footer and any other token input leaves it. **This also changes the keyboard player 1:** Down / Left / Right arrows step the token and no longer leave focus in the footer, so the keyboard reaches the footer with Up, Tab or the mouse.
7. **Settings listen:** pad B cancels a keyboard-slot listen; a pad-slot listen still binds B (only Escape cancels a pad-slot listen, as before).
8. **`TimelineRestartPanel`:** B = Back. Escape keeps opening the hub pause.
9. **Copy:** "Press confirm on any controller or keyboard to join as Player 1" follows CM-INPUT's player 2 string. There are no device glyphs (§3.4 risk 8), so "confirm" stands for the join-press set CM-INPUT chose: any face button, and the keys and mouse buttons bound to `ui_accept`, Interact or Basic Attack.

## 4. Deviations from the plan

1. **The workstream is not in the plan.** The orchestrator added it after CM-FIXA-INPUT §4 recorded "pads cannot activate any GUI button on this build" as an owner-less gap. Its delta (+18) is not in the plan's Phase B arithmetic (§3.2 declares +96 for four workstreams).
2. **`InputManager` is touched beyond one guard.** The brief limited InputManager changes to what the audit requires. The checkpoint's `ConsumeHeldPresses` and its `CaptureFrame` call line are kept, and I added the `_Input` override, which replaces the checkpoint's select-only consume in `CharacterSelectScreen._Input` with one global rule (§1). Nothing in the gameplay sampling, the device-follow poll, the chord window or the Toggle Block latch changed.
3. **The checkpoint's polled-input tests could not fail.** They pressed after `await physics_frame`. A headless probe (since deleted) measured the order: `Engine.GetPhysicsFrames()` increments **before** a step's physics processing, the `physics_frame` signal fires at the top of the step before any node's `_PhysicsProcess`, and an idle-time read sees the last completed step (`node_phys 115 → sig_proc 115 → node_proc 115 → sig_phys 116 → node_phys 116`). So a press pushed at `physics_frame` reached **that** step's capture as a fresh edge, while the test read the next one. The cases now press in idle time (`await process_frame`), where real input arrives, and read the next tick's frame at its top. Every no-double-act case was then mutation-checked: with each consume, the swallow, the focus return or the hub call disabled, its case fails (7 of 7, then 3 of 3 for the overlay and stage-phase consumes), and all pass with the code in place. The same probe confirms `ConsumeHeldPresses`' `+1` lands on the next real tick.
4. **The hub fix is not the checkpoint's.** The checkpoint restructured `HubWorldController._Input` into a new `HandleInteractPress()` (116 changed lines). I replaced it with one `ConsumeInteractPress()` call per acting branch (16 inserted lines), because the file is outside my ownership.
5. **The predecessor's `PadBBacksOutOfTheStagePhaseWithoutRelockingAndPadAPressesBack`** is renamed `…AndUpThenAPressesBack`: it now reaches Back through the Up walk instead of `GrabFocus`. This is a case in my own new file, not a merged one.

## 5. Tests

**New, +18, all in three new suites (one `[TestSuite]` each, all `[RequireGodotRuntime]`):**

- **`PadMenuBindingTests` (2):**
  - `TheProjectAddsPadAToUiAcceptAndPadBToUiCancelAndKeepsTheKeyboardDefaults`: reads `project.godot`, then the live map, for every device.
  - `TheControlsTabAndTheSavedProfileNeverTreatUiAcceptOrUiCancelAsBindable`: bindable list, overrides, a tampered save through both repairs and `Apply`, and `ResetToProjectDefaults`; restores the map in `finally`.
- **`PadMenuConfirmCancelTests` (10):**
  - `PadAPressesTheFocusedModalButtonAndPadBCancelsTheModal` (the `ConfirmModal`)
  - `PadBBacksOutOfTheStoryPauseAndItsPressNeverReachesTheHero`
  - `PadAWorksTheStoryPauseAndPadBClosesItsOverlayBeforeThePause` (the Dialogue Log inside the pause)
  - `PadBCancelsAKeyboardListenThenClosesSettings`
  - `PadAStepsADialogueOneLineAtATimeAndItsLastPressNeverReachesTheHero`
  - `AnOpenPauseMenuOwnsPadPressesOverADialogue`
  - `ADialogueConfirmHeldFromBeforeItStartedCannotHoldToSkipIt`
  - `PadASkipsACreditsReplayWithoutReopeningIt` (the real main menu)
  - `PadAPressesAFocusedResultsButtonAndThePressNeverReachesTheHero` (the global consume)
  - `PadBOpensTheHubHolodeckConsoleAndOnlyTheNextBClosesIt` (the real hub scene)
- **`PadMenuFighterSelectTests` (6):**
  - `PadAPressesAFocusedFooterButtonWithoutSteppingTheTokenAndPadBClosesItsOverlayWithoutConfirming`
  - `PadBBacksOutOfTheStagePhaseWithoutRelockingAndUpThenAPressesBack`
  - `PadBClosesTheVersusCpuPanelWithoutReopeningIt`
  - `ATokenStepNeverLeavesPlayerOnesAOnAFooterButton`
  - `PlayerTwosPadNeverDrivesTheSharedFooter`
  - `PlayerOnesJoinPromptShowsUntilADeviceClaimsSlotZero`

Every case restores what it touches in `finally`: the session (signature 8), `SceneTree.Paused` (signature 4), the devices, the join, the global seen set and the InputMap. Polled cases use `FakeInputDeviceProbe`, real physics frames and real `InputEvent`s pushed through the root viewport. Every press a menu acts on is made in idle time (Deviations 3).

**Rewritten in other suites: none.** No shared test file was touched. CM-FIXA-INPUT's `CharacterSelectSceneTests.AKeyboardJoinPressOnAFocusedButtonOnlyJoins` broke under my first version of the footer rule (which swallowed every GUI event from player 2); the rule was narrowed to token inputs, and the case passes unchanged.

**Runs (lock held, `testhost` / `Godot*_console` drained first):**

- `--headless --import`: exit 0, which regenerated `en.en.translation`.
- `--headless --quit`: exit 0.
- Smokes, `--quit-after 300`: `MainMenu.tscn` exit 0, `CharacterSelect.tscn` exit 0. Neither logged an error.
- **Filtered, final code: 138 / 138.** Filter: `PadMenu`, `CharacterSelectScene`, `ConfirmModal`, `StoryPause`, `LocalFighterPause`, `PauseFocusLoss`, `DialoguePresentation`, `DialogueLog`, `HubArrival`, `MainMenuScene`, `SettingsMenu`, `ResonanceGridScene`, `InputDeviceFollow`.
- **Build:** `dotnet build --no-incremental` gives 0 errors and the two expected warnings (`CS8632`, `KLSG_ECS004` on `FighterTuningComponent`). There is no `KLOTHO_DET002`.
- **Full suite (one run):**

  ```text
  Passed!  - Failed:     0, Passed:  3385, Skipped:     0, Total:  3385, Duration: 1 m 28 s
  ```

  That is **3367 + 18 = 3385** exactly, and `git grep -c "\[TestCase"` under `tests/` at `c8b1cf7` totals 3385.
  - The total is full, so this is not signature 1, 2, 4 or 5.
  - There is no abort line, so it is not signature 7.
  - There are no Story cast failures, so it is not signature 8.

**Declared delta: +18 → 3385.**

## 6. Measurements

- **Physics-frame counter order** (Godot 4.7.1, headless, `--fixed-fps 60`): the counter increments before physics processing, `physics_frame` fires before nodes' `_PhysicsProcess`, and idle sees the last completed step. This is what `ConsumeHeldPresses`' `+1` relies on (Deviations 3).
- **SpinBox line edit** (the stage phase's stock and time fields): a `SpinBox`'s `LineEdit` is in editing mode as soon as it takes focus. Pad A does nothing there. D-pad Down moves focus on to the next control, and D-pad Up does not change the value.
- **OptionButton popup:** pad A opens it. **A pad B pushed through the root viewport did not close it headless**, and neither would Escape on the same path. This is engine behaviour I could not verify headless (§7).

## 7. Not delivered / open

- **No device glyphs.** The prompts say "confirm" (§3.4 risk 8).
- **The pad session must check these by hand** (§5 row 22 covers devices):
  - pad B on an open `OptionButton` popup (the Fighter stage phase, Settings): does it close the popup, or does it reach the screen behind?
  - the focus look of a footer button that the Up walk lands on;
  - the player 1 prompt's layout in the panel.
- **A pad-only player cannot cancel a pad-slot listen in Settings.** Any pad button binds, and only Escape cancels. This is unchanged.
- **A joined keyboard player 2's Enter still presses a footer button player 1's focus is on** (Back included), because CM-FIXA-INPUT pinned it. Their Space and arrow keys no longer do.
- **In a non-pausing dialogue, jumping on pad A now pages lines,** as keyboard Space and attacking always did (Decision 5). Whether jumping or attacking should page barks at all is a question for the user.
- **The Warden Beacon's grid panel in Act III levels** is opened by `InteractionArea` in the `_unhandled_input` stage. By Godot's per-stage copy of the node list, the same B cannot reach the new panel, but no case pins it.
- **The consumed tick drops a press the other player makes on that exact tick** (Decision 2).

## 8. For the closeout

**AGENTS.md:**

- **The `InputManager` autoload bullet:** `project.godot` adds pad A to `ui_accept` and pad B to `ui_cancel`, keyboard defaults kept, and the two are never remappable. Also `ConsumeHeldPresses` (one tick, device-driven slots only) and its callers, and `InputManager._Input` consuming a confirm that a focused button takes.
- **UI section:**
  - `PauseMenuBase`: `ui_cancel` resumes; `IsAnyOpen`.
  - The dialogue box: stands down under a pause and on a focused button's confirm; a hold-to-skip needs a fresh hold.
  - Settings: B closes it.
  - The Fighter select's footer rules and the player 1 join prompt.
  - The hub spends an Interact that opened something.
- **Validation baseline:** 3385 on this branch alone (3367 + 18). Reconcile with the other Phase B branches at merge.

**CLAUDE.md:** a test-authoring note, beside the `OverrideFailureMessage("")` footgun. A press pushed after `await physics_frame` reaches that same tick's `InputManager` capture, because the signal fires before nodes' `_PhysicsProcess`, so a "the menu press never reaches the game" case written that way passes vacuously. Push in idle time (`await process_frame`), then read `GetFrame` at the next `physics_frame`. Also the baseline paragraph.

**Design master and mirror** (`design-godot.md` at `e2c655a`, neither line is a `DOC-MASTER-UNBOUND-BINDING` line):

- **:3383** (Control Focus System): add that pad A confirms (`ui_accept`) and pad B backs out (`ui_cancel`). On the Fighter select the footer is player 1's: Up walks it, and any token input leaves it.
- **:1834–1837** (the join workflow, already owed by CM-INPUT / CM-FIXA-INPUT): add player 1's press-to-join prompt.
- **:4051–4052** (controller lost): the Story notice also resumes on a key press. CM-INPUT owes this line; my `en.csv` value now says it.

**Contracts:** `COMFORT_SETTINGS.md` (C01c) could say that `ui_accept` / `ui_cancel` are fixed menu navigation, outside the Controls tab. The line is optional and the copies are edited by hand (the master and the repo copy differ on the Unbound lines only).

**Ledger:**

- **New `PROVISIONAL-PAD-MENU-RULES`** (owner Unassigned): Decisions 1–9. Acceptance is the §5 pad session: a pad-only player confirms and backs out of every menu, one press never acts twice, and the Fighter select's footer is reachable on purpose and never by accident.
- **Close CM-FIXA-INPUT's proposed owner-less row** "pads have no `ui_accept` / `ui_cancel` on Godot 4.7.1", fixed by CM-PADUI.
- **Amend `PROVISIONAL-DEVICE-FOLLOW-RULES`:** the player 1 join prompt, and the other-player GUI swallow on the select.
