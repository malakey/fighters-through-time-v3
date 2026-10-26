# Recon C — encounter-area entry: the camera shift, the blue line, "Enemies left" and GO (2026-10-25)

Read-only subagent report at `0eba848`, from code only (the ~600 px shift is calculated, not measured).

## 1. Camera shift (root cause, confirmed)
- `EncounterLock.Engage` (`scripts/Environment/EncounterLock.cs:391-406`) closes both barriers, wakes members, then calls `ResolveCamera()?.SetLockBounds(CameraBounds)` (`:403`).
- `SetLockBounds` (`StoryCameraConfiner.cs:141-149`) calls `BeginBoundsEase`; `StepBoundsEase` (`:176-188`) lerps Camera2D `LimitLeft/Top/Right/Bottom` toward the lock rect over `CameraEaseSeconds` 0.6 (`EncounterLockRules.cs:135`).
- `StepFollow` (`:200-232`) ends with `RigCenter = Confine(...)` and `GlobalPosition = RigCenter`; `Confine` (`:246-251`) uses `StoryCameraRules.ConfineCenter` (`StoryCameraRules.cs:169`). As the left limit eases right, the confine drags the camera right while the hero stands still.
- Lock rect: `DeriveCameraBounds` (`EncounterLock.cs:240-247`) = the area widened to at least the 1280 × 720 view, clipped to level bounds. Level 3 authors explicit `CameraBounds` (`Level03Controller.cs:285, :311`).
- Engage needs the hero at least half-width + 24 px (~44 px) inside the area (`EncounterLockRules.cs:167-177`). Entering from the left, the room-clamped view's left edge is about hero − 640 ≈ area.left − 600; the lock's left bound is ≈ area.left → a ~600 px pan in 0.6 s. Entry from the right mirrors it; vertical locks (e.g. Level 3 `GroundRoomCameraTop`) pan vertically too.
- Room `SetBounds` (`:47-68`) returns early while `_hasLockBounds` is set, storing only `_roomBounds`.
- Clear and revert call `ClearLockBounds` (`:152-156`), which eases back to `RoomBounds`. Callers: `EncounterLock.cs:317` (`RestoreCleared`, only if previous was Engaged), `:417` (revert), `:429` (clear). These and `:403` are the only callers of `SetLockBounds` / `ClearLockBounds`. Level 0's trial lock and Florence's locks use the same `EncounterLock` path.
- A resume into a cleared lock makes no camera call. A rewind landing inside an engaged lock stays Engaged and `SnapToTarget` re-confines to the lock limits. Locks do not use boss/Mirror framing (`FramedFollowX` not traced).

## 2. Plain rooms ruled out
`RoomTransitionTrigger.cs:29` calls `SetBounds`, which writes the limits at once and clamps `RigCenter` (`:63-66`) — an instant snap, and room edges are authored so the old view does not straddle them.

## 3. The blue line
- `EncounterLockBarrier._Draw` (`EncounterLockBarrier.cs:138-172`) draws only while `IsClosed`: `UnboundColdDim` fill, `UnboundCold` zig-zag edges, hollow diamonds. `SetClosed` (`:107-113`) sets `Visible` and calls `QueueRedraw`; `Create` (`:93`) starts it hidden.
- The entry barrier is centred about 10 px outside the area, 20 px wide; after the pan the camera's left edge is at area.left, so the barrier sits on the screen's left edge — the "blue line showing the edge".
- `PushOut` (`:122-136`) acts only on overlap; engage needs the hero ≥ 44 px inside and the footprint is [area.left − 20, area.left], so there is no shove on engage (generic geometry, not each of the 48 locks).
- Not inspected: `EncounterSpawnTear` (2 draw calls), `ForcefieldBarrier`, gate columns.

## 4. The HUD panel
- `scenes/ui/StoryHUD.tscn:534-571`: `SafeArea/EncounterLockPanel` with `EnemiesLeft`, `GoPrompt`, `GoText` ("hud_encounter_go") and `GoArrow`.
- Script: `scripts/UI/StoryHUD.EncounterLock.cs` (whole partial). Hooks in `StoryHUD.cs:236` (subscribe), `:270` (unsubscribe), `:289` (`TickEncounterGoPrompt`), `:1032` (`ResolveEncounterLockUI`).
- Keys: `localization/en.csv:1661-1662` (`hud_encounter_enemies_left`, `hud_encounter_go`).
- The only subscribers of `OnEncounterLockChanged` are the HUD and tests; bots and telemetry read `EncounterLock` directly. The event and `Publish` can stay.
- The objective line (`ObjectiveKey`, `encounter_lock_objective_clear`, `RoomEntryObjectiveDeferredToLock` at `StoryLevelControllerBase.cs:789`, `StoryLevelControllerBase.EncounterLocks.cs:127-151`) is separate.

## 5. Test impact
- `EncounterLockTests.TheCameraLockLayerOverridesRoomBoundsAndEasesInAndOut` (`EncounterLockTests.cs:386-428`) asserts the eased limits equal `CameraBounds` — rewrite. Lines 548-554 assert `Camera.HasLockBounds` true then false on a real level — review. `HasLockBounds`, `IsEasingBounds`, `CurrentLimits`, `LockBounds` are referenced only in this file (8 hits; 22 `[TestCase]`s). `StoryCameraRigTests` (17) does not reference the lock APIs.
- Delete `EncounterLockTests.TheHudCountsTheEnemiesLeftAndShowsTheGoArrowAfterTheClear` (`:355`, 1) and `StoryHudEncounterGoPromptTests` (1). `EncounterLockRulesTests` (`:194`, 1 of 11) asserts `GoPromptSeconds` and `GoGlyph`: keep the constants or trim the case.
- Barrier tests assert only `IsClosed` and collision, not drawing.
- `UnusedTranslationKeyTests` fails on an orphan: delete the two rows and run `--headless --import`. Removing the scene nodes is safe for `SceneVisibleTextTests`.

## 6. Fix design (evaluated)
- **Ratchet:** on engage, set the limits to the union of the current visible rect (`RigCenter ± ViewSize()/2`) and `CameraBounds`. In `StepFollow`, use the unconfined desired view (x, y before `Confine`); each side moves toward the lock bound only when the desired view passes it, and never backs off. Replace the ease machinery at `StoryCameraConfiner.cs:141-195`; keep `SetBounds` swallowing during a lock.
- **Release:** on clear or revert, restore the room limits without a forced pan (an instant write is a no-op if the view is inside the room limits; otherwise use the same union-and-ratchet).
- Edge cases: the union may extend beyond the room rect (fine — the lock rect already overrides the room); `ConfineCenter` centres when the span ≤ the view (the union is always ≥ the view; `ExpandCameraSpan` guarantees the lock span ≥ the view unless the level is smaller); a mid-air engage ratchets Y the same way; Level 7 haul-outs land inside the lock; Levels 12/13 low-gravity locks are fine; a rewind landing / `SnapToTarget` is fine (fold the snapped view into the ratchet state); entry from the right and vertical locks are symmetric.
- Simpler alternative (not chosen; the user ruled the ratchet): drop camera locking entirely (remove `:403` and the three `ClearLockBounds` calls).
- **Barrier:** keep it solid; draw nothing (or drop the `Visible` and `QueueRedraw` lines at `:111-112`).
- **HUD:** delete the `.tscn` subtree, `StoryHUD.EncounterLock.cs`, the four hooks and the two keys; re-run `--import`.
