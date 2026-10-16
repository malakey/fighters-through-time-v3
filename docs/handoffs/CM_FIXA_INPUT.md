# CM-FIXA-INPUT: the Phase A input fix round

**Package:** combat and movement update (2026-10-16), Phase A fix round. Plan: `docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md` (dossiers 2.1.5, 2.9.1, 2.9.2). Decisions: 1.1(5), 9.1, 9.2 (`D:/Projects/genre_review_2026-10-13/DECISIONS_combat_movement.md`). Findings: the `INPUT` array of `D:/Projects/genre_review_2026-10-13/fixA_inputs.json` (6 entries), with the orchestrator's rulings (a)–(e).
**Branch:** `worktree-wf_a50df6ba-c2b-3`. **Base:** `main` at `5434151` (Phase A merged, 3332 green). The worktree was created at the stale `d2e52e0` and was fast-forwarded to `main` before any work. The sibling worktrees `worktree-wf_a50df6ba-c2b-1` and `-4` were also at `d2e52e0` when this round started.
**Commits:** `bff742b` (finding 2, the chord-flag repair), `4dd9355` (finding 4, the Mirror case), `2be9623` (findings 1, 3, 5 and 6), then this handoff.
**Nobody played the build.** Every claim below comes from a headless test, a probe test or a reading of the code. All of it is local input processing before `PlayerInputFrame.Create`, plus one load-time save repair. There is no protocol, snapshot, hash, Klotho or save-schema change.

## 1. Findings fixed

### Finding 6: the Local Versus join pre-claimed player 1 (ruling a)

- **Before.** `BeginLocalJoin` kept slot 0 on whatever device had driven the menus. After keyboard- or mouse-driven menus, pad A joined as player 2 and pad B could not join at all.
- **Now.** `InputManager.BeginLocalJoin` empties every slot, slot 0 included. An input source keeps its slot. The first device to make a join press claims player 1, and the next different device claims player 2. This is decision 9.1 read literally.
- `EndLocalJoin` gives an unclaimed slot 0 back to the keyboard, the same fallback an emptied slot 0 gets in FollowLastUsed.
- A mouse button bound to a join action now joins the keyboard-and-mouse device. The left click is bound to Basic Attack. The join-press set (face buttons, plus keys and buttons bound to `ui_accept`, Interact or Basic Attack) was otherwise left as CM-INPUT chose it.
- New read surface: `IsJoinPressFromUnclaimedDevice(InputEvent)`, `HasFreeSlot()` and `AnySlotJoined()`.

### Finding 1: the join press also acted on the screen, and `ui_cancel` was always player 1's (ruling a, "consumed")

**The finding's premise does not hold on this engine build.** A probe test in this worktree (deleted afterwards) showed:

- Godot 4.7.1's built-in `ui_cancel` is **Escape only**.
- `ui_accept` is Enter, Keypad Enter and Space.
- No pad button maps to either action. `ui_select` carries pad Y.
- A pushed pad A press and release did not press a focused `Button`. An Enter press and release did.

So a pad's B never raises `ui_cancel` here, and a pad's A never presses a GUI button. The finding and both verifiers assumed Godot's documented default, which includes B.

What the same mechanism does break, and what was fixed:

- **The join press is consumed.** `CharacterSelectScreen._Input` marks a join press from an unclaimed device handled while a slot is free. This happens before the GUI or `_UnhandledInput` sees it. The poll reads device state, not events, so the claim is unaffected. Before this fix, a keyboard joining with Enter or Space while a footer button had focus pressed that button too: Back threw both players out, and Move List opened the overlay. A joining left click also clicked whatever was under it.
- **`ui_cancel` is routed to the device that pressed it** (`CancellingPlayer`):
  - **In a local join, the device's own slot decides.** The device is classified by event type: a key or mouse event is the keyboard, and a joypad event is its own device. A keyboard player 2's Escape now unwinds player 2's own pick. It used to unlock player 1, abort the countdown on player 1's behalf, or back both players out.
  - **A device no slot holds.** Once anyone has joined, its press is ignored. Before anyone has joined, any device's cancel still leaves the screen, so a keyboard user who entered Local Versus by mistake can press Escape.
  - **Outside a join** (Versus CPU, Holodeck), the press stays player 1's.
  - **Only player 1's cancel can leave the screen.**
- **A pad button that is also a token confirm never cancels while the token flow runs.** The token confirms are Interact (B), Basic Attack and `ui_accept`. On this build this guards a binding rather than a live path. The two B cases add B to `ui_cancel` for their duration (the reported configuration), and both reproduce the reported failures before the fix.
- New test seam: `CharacterSelectScreen.BackOverrideForTesting` (internal). When it is set, `OnBack` calls it instead of loading a scene.

### Finding 5: device follow ignored the trigger axes (ruling b)

- `PollDeviceActivity` now reads every bound pad axis except the left stick (`PolledPadAxis`). The list is built from the `InputEventJoypadMotion` events of the gameplay, direct and menu actions:
  - one entry per axis and direction, at the smaller deadzone when two actions share it;
  - at most 32 entries;
  - an entry counts as a join press if a join action uses it.
- An axis counts as "out" while `raw × sign ≥ the action's deadzone`, which is exactly when `ReadActionPressed` reads the action as held. Crossing out counts as activity, like a new button press.
- The per-pad bitmasks are re-read when a binding change rebuilds the list, so a trigger that is already held is not counted as a new press.
- Result: at defaults, LT (Block) and RT (Roll) move player 1 onto the pad, and that same pull reaches the game as a fresh press.

### Finding 3: LB+RB held across a pause fired both components on resume (ruling d)

- `InputManager._Notification`, on pause or focus loss, now calls `ForgetChordWaits()`. Before, it called `ResetChordWindows()`, which dropped the whole window.
- `ForgetChordWaits` applies the new `UltimateChordWindow.ForgetWaits()` to each slot's window:
  - **Waiting → Up.** A shoulder that was released while the game was paused does not come back as a one-frame tap.
  - **Masked stays masked until it is released.**
  - **Emitting stays held**, so it creates no new edge.
- A device hand-over still drops the whole window.
- `ResetChordWindows` is removed; nothing else called it. `UltimateChordWindow.Reset()` stays as the full clear.

### Finding 2: a saved pad chord set to "off" lost the pad Ultimate after 9.2 (ruling c)

- **New method:** `InputBindingSet.RestoreUnreachableUltimateChord(projectDefaults)`.
- **When it fires:** the gamepad Ultimate shortcut flag is off and the Ultimate has no direct pad route. The pad route is resolved the way `AnotherActionHoldsJoyButton` resolves one:
  - nothing, when the pad slot is explicitly Unbound;
  - otherwise the saved row's pad half;
  - otherwise the project default.
- **What it does:** removes the flag. A missing flag reads as On.
- **Where it runs:** `SaveManager.ApplySavedInputBindings` calls it on every load, after `RepairGodot3ShoulderRows`, because that repair can drop a stale LB+RB direct half. It sits outside that method, because `RepairGodot3ShoulderRows` returns early when the payload has no action rows, and that is exactly the payload of a player who only flipped the toggle.
- It logs a `[SaveManager]` line. It is idempotent, pure data, and makes no schema change.
- After 9.2 the Controls tab refuses to commit an unreachable profile, so a saved "off" with no route can only predate the update.

### Finding 4: the Mirror full-hop case measured a double-jump arc (ruling e)

- `MirrorParadoxTests.TheCloneHoldsJumpForAFullHopInsteadOfShortHoppingEveryJump` was rewritten in place with the same name. The hero stands ten units away for the first decision only, then 4.5 units away, which is inside the 5-unit far range. So only that first decision queues an approach jump.
- The case now asserts exactly one Jump press in the arc, and an apex at or above 95 % of `StoryAirPhysics.SingleJumpRise`.
- **Measured** (Einstein, Hard boss band): **186.4 px**, against **186.4 px** for a held single jump.
- **Proof that the case now catches the regression the finding describes.** I added a temporary mutation to `FighterCpuController.BeginJumpSustain`: the first sustain (the ground jump's) ends at once, and air jumps keep theirs. Under that mutation:
  - the new case fails at **89.1 px**;
  - a verbatim copy of the old case **passed**.

  The mutation and the copy were then removed. `FighterCpuController.cs` is unchanged on this branch.

## 2. Files changed

| File | Why |
|---|---|
| `scripts/Core/InputManager.cs` | Findings 6, 1 (the predicate), 5 and 3: `BeginLocalJoin`, `EndLocalJoin`, `IsJoinPressFromUnclaimedDevice`, `HasFreeSlot`, `AnySlotJoined`, `PolledPadAxis` and the axis poll, the mouse join flag, `_Notification` / `ForgetChordWaits`. |
| `scripts/Core/UltimateChordWindow.cs` | `ForgetWaits()`. |
| `scripts/UI/CharacterSelectScreen.cs` | `_Input` (consumes join presses), the `_UnhandledInput` routing (`CancellingPlayer`, `IsTokenConfirm`), the `BackOverrideForTesting` seam, comments. |
| `scripts/Core/InputBindings.cs`, `scripts/Core/SaveManager.cs` | Finding 2. |
| `tests/Unit/*` | Listed in §3. `FakeInputDeviceProbe` gained `MouseButtonFor(action)`. |

`project.godot`, `en.csv`, scenes, the sim, saves and the protocol were not touched.

## 3. Tests

**New, +12. Every one failed against the pre-fix behaviour before the fix.** The repro build kept only the new seams and API stubs. All 14 new and rewritten cases failed there, for the intended reasons.

| Suite | + | Cases |
|---|---|---|
| `CharacterSelectSceneTests` | 5 | `TwoPadsBothJoinAfterKeyboardDrivenMenus`, `AKeyboardJoinPressOnAFocusedButtonOnlyJoins`, `EscapeCancelsOnlyThePickOfThePlayerWhoPressedIt`, `WithBInUiCancelAPadsBJoinOrConfirmNeverCancelsOrLeaves`, `APadOnlyPlayerOneConfirmsWithBInVersusCpuWithoutLeaving` (real frames plus real `InputEvent`s pushed through the viewport) |
| `InputDeviceFollowTests` | 2 | `ATriggerPullMovesPlayerOneAndItsFirstFrameCarriesTheVerb`, `ALocalJoinPreClaimsNoSlotAndTheFirstTwoDevicesToPressAreTheTwoPlayers` |
| `UltimateChordWindowTests` (pure C#) | 2 | `APauseKeepsAHeldChordsMasksSoItsComponentsNeverFireOnTheWayBack`, `APauseForgetsAWaitingShoulderSoALetGoNeverComesBackAsATap` |
| `UltimateChordStoryTests` | 1 | `AChordHeldAcrossAPauseFiresNoComponentOnTheWayBack` (the live manager's notification, in real frames) |
| `GamepadOverrideRepairTests` (pure C#) | 1 | `AChordSwitchedOffBeforeTheUpdateComesBackWhenItLeavesThePadUltimateNoRoute` |
| `InputBindingSchemaTests` | 1 | `SaveManagerReEnablesAPadUltimateChordThatLeavesNoRoute` |

**Rewritten in place, no count change:**

- `CharacterSelectSceneTests.NoPlayerTwoTokenAppearsUntilPadBPressesAndThatPressConfirmsNothing`: the keyboard now claims player 1 first.
- `CharacterSelectSceneTests.PlayerOnesDeviceCannotTakeTheSecondSlot`: the menus' pad is not pre-claimed; its own press joins it, and later presses never take slot 1.
- `CharacterSelectSceneTests.TheKeyboardCanJoinAsPlayerTwo`: pad 0 claims player 1 first.
- `MirrorParadoxTests.TheCloneHoldsJumpForAFullHopInsteadOfShortHoppingEveryJump`: see finding 4.

**Runs (lock held, leftovers drained first):**

- **Touched and related suites, fixed code: 231/231.** Filter: `CharacterSelectScene`, `InputDeviceFollow`, `UltimateChord*`, `Gamepad*`, `InputBindingSchema`, `MirrorParadox`, `LocalFighterPause`, `PauseFocusLoss`, `StoryPause`, `HapticDeviceRouting`, `MainMenuScene`, `PlayerInputFrame`, `FighterCpu*`, `CpuRecoveryMatrix`, `StoryAbilityLockout`, `SceneVisibleText`, `ScriptTranslationKey`, `UnusedTranslationKey`.
- **Full suite (one run):**

  ```text
  Passed!  - Failed:     0, Passed:  3344, Skipped:     0, Total:  3344, Duration: 1 m 37 s
  ```

  That is **3332 + 12 = 3344** exactly, and the `[TestCase]` count under `tests/` is 3344.
  - The total is full, so this is not failure signature 1, 2, 4 or 5.
  - There is no abort or crash line, so it is not signature 7.
  - There are no Story cast failures, so it is not signature 8.
- **Build:** `dotnet build --no-incremental` gives 0 errors and the two warnings CM-INPUT recorded (`CS8632` and `KLSG_ECS004`). There is no `KLOTHO_DET002`.
- **Import:** `--headless --import` exit 0. No scene or resource changed, so no smoke run was made.

**Fighter hash moves: no.** Nothing under `scripts/FighterSim/` changed. The CPU sustain mutation was temporary and is not on the branch.

## 4. Not fixed, and recorded limits

- **The pad-B half of finding 1 is not reproducible on this engine,** because `ui_cancel` carries no pad button (see §1). It is guarded, and pinned with B added to `ui_cancel`.
- **Pre-existing, found while probing: pads cannot activate any GUI button on this build,** because `ui_accept` is keyboard-only. On the Fighter select a pad-only player can pick (through the polled token flow), but cannot press Back, Move List or Systems Card. Block and Roll unwind a pick but never leave the screen.
  - I did not check the other menus. Any menu that relies on a focused `Button` and `ui_accept` / `ui_cancel` is affected the same way unless it polls `InputManager`.
  - This is outside this round's scope and needs an owner. The possible fix, adding pad events to the `ui_*` actions in `project.godot`, touches every screen.
- **No player 1 "press to join" prompt.** Player 1's token is drawn while slot 0 is still unclaimed, and only player 2's panel has the authored prompt. Adding one needs a scene label and an `en.csv` key, which is presentation work outside this round.
- **A mouse user's first click on the Local Versus select only joins.** The ruling says the join press must be consumed. The second click then acts.
- **Before anyone joins, an unclaimed device's cancel leaves the screen.** This is an agent choice, made so a keyboard user can still back out with Escape. Once anyone has joined, only player 1's cancel leaves.

## 5. For the closeout

**AGENTS.md:**

- **The `InputManager` autoload bullet:**
  - no slot is pre-claimed at `BeginLocalJoin`;
  - `EndLocalJoin` gives an unclaimed slot 0 back to the keyboard;
  - a mouse button bound to a join action joins;
  - bound pad axes (the triggers) count as activity;
  - a pause or focus loss forgets chord waits but keeps masks;
  - the load-time chord-flag restore.
- **The validation baseline:** 3344 on this branch alone. Reconcile it with the other fix agents' deltas at merge.

**CLAUDE.md:** nothing beyond the baseline paragraph.

**Ledger:**

- **Amend `PROVISIONAL-DEVICE-FOLLOW-RULES`:**
  - no pre-claim;
  - the axis poll and its deadzone rule;
  - the mouse join;
  - join-press consumption on the select;
  - `ui_cancel` routing, including that an unclaimed device's cancel is ignored after anyone has joined but leaves the screen before then;
  - the guard for pad confirms.
- **Amend `PROVISIONAL-ULTIMATE-CHORD-LENIENCY`:** a pause or focus loss keeps masks and forgets waits.
- **Amend `PROVISIONAL-GAMEPAD-OVERRIDE-NO-MIGRATION`:** add `RestoreUnreachableUltimateChord`.
- **`FIX-CPU-JUMP-HOLD`:** the Mirror acceptance is now a measured single ground jump at 186.4 px.
- **New owner-less row, if the closeout agrees:** pads have no `ui_accept` / `ui_cancel` on Godot 4.7.1 (§4).

**Updates to `docs/handoffs/CM_INPUT.md`:**

- **§7 "Two pads after keyboard-driven menus"** is resolved.
- **§4 deviation 3** (the Mirror apex includes a queued air jump) is superseded.

**Design master and mirror, by hand in each copy:**

- **:1834–1837:** the first press on any device claims player 1, and the next different device claims player 2.
- **`COMFORT_SETTINGS.md` :134:** the join wording, edited by hand in both copies.
