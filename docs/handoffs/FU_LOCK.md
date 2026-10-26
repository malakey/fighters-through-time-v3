# FU-LOCK handoff (feel and UI update 2026-10-25, ruling R4)

## 1. Summary

Walking into a Story encounter area no longer moves the camera, draws no blue line, and shows no "Enemies left" count or GO prompt. The area still holds the hero in until it is cleared (invisible walls). The camera only moves when the hero moves, and the view tightens onto the area as the hero walks in.

## 2. What shipped

- `scripts/Environment/StoryCameraConfiner.cs`: the GU-E limit ease (`BeginBoundsEase`, `StepBoundsEase`, `IsEasingBounds`, `_boundsEasing`, `_easedBounds`, `_easeTarget`) is replaced by a **ratchet**. New: `IsRatchetingBounds`, `StepRatchet(Vector2 desiredCentre)`, private `BeginRatchet(target)`. `HasLockBounds`, `LockBounds`, `RoomBounds`, `CurrentLimits`, `SetLockBounds`, `ClearLockBounds` keep their names and meaning; room `SetBounds` is still swallowed while a lock holds.
  - **Rule.** Working limits W, target T (the lock's `CameraBounds`, or the room rect on release). On `SetLockBounds` / `ClearLockBounds`: `W = T.Merge(VisibleWorldRect)` (the view as it stands, at the current zoom), written to the Camera2D limits at once; `ActiveBounds = T`. Ratchet is on while `W != T`. Each `StepFollow`, with the unclamped desired centre D (view = D +- ViewSize/2) and before `Confine`: `if T.left > W.left: W.left = max(W.left, min(T.left, D.left))`; `if T.top > W.top: W.top = max(W.top, min(T.top, D.top))`; `if T.right < W.right: W.right = min(W.right, max(T.right, D.right))`; `if T.bottom < W.bottom: W.bottom = min(W.bottom, max(T.bottom, D.bottom))`; W is written to the limits. Sides only move toward T, never back. With no rig (not following / not in tree) the target is written at once. `SnapToTarget` folds the snapped view into W while the ratchet is on. A room `SetBounds` ends the ratchet and writes at once (unchanged room-change behaviour).
  - Boss and Mirror framing (`StoryCameraConfiner.Framing.cs`, `FramedFollowX`, `TargetZoom`) is untouched and still runs before the ratchet; locks never frame.
- `scripts/Environment/EncounterLockRules.cs`: removed `CameraEaseSeconds`, `GoPromptSeconds`, `GoGlyph`. `RemainingCount` kept (payload `Remaining`).
- `scripts/Environment/EncounterLockBarrier.cs`: `_Draw`, the `Visible` / `QueueRedraw` toggling and the glow / pattern constants deleted. Collision, `IsClosed`, `PushOut`, groups, `Create` are unchanged (`Create` still sets `Visible = false`).
- HUD: `scripts/UI/StoryHUD.EncounterLock.cs` (+ `.uid`) deleted; four hooks removed from `StoryHUD.cs`; `SafeArea/EncounterLockPanel` subtree removed from `scenes/ui/StoryHUD.tscn`; `hud_encounter_enemies_left` and `hud_encounter_go` removed from `localization/en.csv` (the compiled `.translation` was regenerated locally by `--import`, not committed).
- Kept: `EventBus.OnEncounterLockChanged` and `EncounterLockPayload` (including `GoDirection`), the authored `GoDirection` on every lock spec (now **unread by any presentation**), the `EncounterGoDirection` enum, and the lock objective line (`encounter_lock_objective_clear`).

## 3. Tests

Baseline 3529. `[TestCase]` count under `tests/` verified by counting: **3534** (+5).
- Deleted: `EncounterLockTests.TheHudCountsTheEnemiesLeftAndShowsTheGoArrowAfterTheClear` (-1); `StoryHudEncounterGoPromptTests` file with its one case (-1).
- Rewritten in place (no count change): `EncounterLockTests.TheCameraLockLayerOverridesRoomBoundsAndEasesInAndOut` -> `...RatchetsInAndOut` (opened limits contain the lock rect, `StepRatchet` settles on it, the clear heads back to the room rect; the `HasLockBounds` assertions at the base-level case stay valid); `EncounterLockRulesTests.TheHudCountsTheLivingAndTheUnspawnedAndTheGoPromptReadsByItsArrow` -> `TheRemainingCountIsTheLivingPlusTheUnspawnedAndZeroOnceCleared` (glyph / seconds lines dropped).
- New (+7), `tests/Unit/EncounterLockCameraRatchetTests.cs` (one `[TestSuite]`, `[RequireGodotRuntime]`): hero engages then stands still (camera stays within 120 px of the hero and never jumps), walking in tightens the limits monotonically onto the rect, the clear does not move the camera, a closed barrier blocks (ray hit) but is not visible, right-to-left entry, a vertical lock rect (camera-only, tops/bottoms), the Story HUD has no `EncounterLockPanel`.
- Arithmetic: 3529 - 2 + 7 = 3534.
- Fails-on-old check: with the new `BeginRatchet` temporarily forced to write T directly (the old pan, sharper), 3 of the 7 failed, including the stand-still case (camera 588 px from the hero). Restored afterwards.
- Verified: build 0 errors, 2 warnings (both pre-existing: `KLSG_ECS004`, `CS8632`); filtered run (EncounterLock*, StoryCameraRig*, StoryHud*, AnchorApproach*, UnusedTranslationKey*, SceneVisibleText*, Level0*) 275/275; full run `Failed: 0, Passed: 3534, Total: 3534`, 1 m 44 s.

## 4. Provisional numbers

None added. The stand-still test's 120 px tolerance is the rig's own 64 px dead zone plus damping, not a design value.

## 5. Shared files and merge notes

`StoryHUD.cs` (four deleted lines), `StoryHUD.tscn` (one subtree at the end of the SafeArea nodes), `localization/en.csv` (two adjacent rows, near line 1661). Other FU agents touching `StoryHUD*` or `en.csv` may conflict textually; resolve by keeping their additions. After merging, run `--headless --import` once.

## 6. Not delivered / risks / open questions

- The walls are now invisible: a player walking back into a closed west barrier gets no cue why they stop. Worth a hands-on check.
- After a clear the rig simply resumes normal follow; if the view is outside the room rect (rare) the limits head back to the room by the same ratchet, which is correct but unplayed.
- Level 3's vertical lock rects and haul-out landings are covered only by the unit-level vertical case, not played. Nobody has looked at any of it.
- No hands-on camera feel check; headless only.

## 7. Drafted doc changes (not applied)

- **AGENTS.md, encounter-lock bullet.** Replace "the placeholder look is an `UnboundCold` curtain with a column of hollow diamonds" with: "the walls are invisible since 2026-10-25 (`EncounterLockBarrier` keeps its collision and push-out and draws nothing)". Replace the **Camera** sentence ("`StoryCameraConfiner.SetLockBounds` / `ClearLockBounds` ease the limits over 0.6 s to the lock's `CameraBounds` ... and swallow room `SetBounds` calls meanwhile; locks do not frame") with: "`SetLockBounds` / `ClearLockBounds` open the camera's limits to the union of the current view and the lock's `CameraBounds` (or the room rect on release) and the limits then tighten onto that rect one side at a time only as the follow rig's desired view passes the side, never back (`StepRatchet`), so entering or leaving an area never pans the camera; room `SetBounds` calls are swallowed meanwhile; locks do not frame". Replace the **HUD** sentence ("`EventBus.OnEncounterLockChanged(...)` drives `StoryHUD`'s authored `SafeArea/EncounterLockPanel`: 'Enemies left: N' ... and a 3 s GO prompt ...") with: "`EventBus.OnEncounterLockChanged(...)` is still raised but the Story HUD shows nothing for it (the 'Enemies left' count and the GO prompt were removed 2026-10-25); a lock with an `ObjectiveKey` still posts its objective line; `GoDirection` is authored but unread". Update the Story HUD section's sentence about `SafeArea/EncounterLockPanel` the same way, the suite list (`StoryHudEncounterGoPromptTests` gone, `EncounterLockCameraRatchetTests` 7), and the `EncounterLockTests` count 22 -> 21; the baseline moves by this stream's delta.
- **`docs/handoffs/GU_E.md`**: the HUD paragraph (line 36), "Placeholders only: the curtain, the spawn tear and the GO prompt" (line 283), the owed `HUD_CONTRACT` amendment (lines 284, 316, 324) and the camera-ease notes are superseded: no curtain, no panel, no ease; add "see `FU_LOCK.md`".
- **`docs/design-contracts/HUD_CONTRACT.md`**: no `EncounterLockPanel` widget; the owed amendment is withdrawn.
- **Ledger rows** (owner Unassigned):
  - `USER-LOCK-NO-CAMERA-SHIFT`: intended — entering or leaving an encounter area never moves the camera by itself; the view tightens onto the area only as the hero walks (R4, 2026-10-25). Observed at this branch: `StoryCameraConfiner` ratchet. Acceptance: hands-on entry from either side and from above/below shows no pan; `EncounterLockCameraRatchetTests`.
  - `USER-LOCK-BARRIERS-INVISIBLE`: intended — encounter walls hold the hero in with nothing drawn (R4). Observed: `EncounterLockBarrier` has no drawing. Acceptance: no visible line at any of the 48 locks; a hands-on check that an invisible wall reads acceptably.
  - `USER-LOCK-HUD-REMOVED`: intended — no "Enemies left" count and no GO prompt in Story (R4). Observed: panel, script and two keys removed; event and `GoDirection` data kept. Acceptance: no lock text on the Story HUD; objective line unchanged.
