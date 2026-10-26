# GU-E handoff: encounter locks (framework + Level 2), gameplay update 2026-10-12

Workstream GU-E of `docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md` (§2.3, §4). User request 3: "you enter rooms or areas that you have to clear of enemies … You no longer have the option of jumping past them. The same thing could be done for puzzles in a room." Story only. Branch `worktree-wf_21dd1c67-9be-5`, based on the plan commit `b8f5343`.

**Nobody has played or looked at any of this.** The evidence is headless: the suites below, a 300-frame smoke run of Level 2, and one `play` bot run of Level 2.

## 1. Shipped

### The rulebook: `FTT.Environment.EncounterLockRules` (`scripts/Environment/EncounterLockRules.cs`)

Pure, engine-free (floats, ints, bools and two `FTT.Core` enums; no Godot type, no `GameManager`). Constants:

| Constant | Value | Notes |
|---|---|---|
| `EngageInsetPixels` | 24 | plan §2.3 |
| `FallNetMarginPixels` | 240 | `= BossController.ArenaFallNetMarginPixels` (one canonical value, inlined const) |
| `StallFrames` | 600 | Provisional |
| `StallMovePixels` | 4 | Provisional |
| `StallVerticalGapPixels` | 200 | Provisional |
| `HardCapSeconds` | 300 | Provisional |
| `DefaultBarrierWidthPixels` | 20 | Provisional |
| `BarrierEdgeTolerancePixels` | 20 | Provisional (the hero's half-width) |
| `ViewWorldWidth` / `ViewWorldHeight` | 1280 / 720 | **TODO for the orchestrator:** point both at `StoryCameraRules.ViewWorldSize` once GU-B is merged. Until then this is the only copy. |
| `CameraEaseSeconds` | 0.6 (**superseded 2026-10-25, FU-LOCK: constant deleted, the camera limits ratchet instead**) | Provisional |
| `GoPromptSeconds` | 3 | Provisional |
| `MinAreaWidthPixels` | `= ViewWorldWidth` (1280) | |
| `MaxAreaWidthPixels` | 3200 | Provisional. See Deviations: the plan said "about 2600". |

Rules: `IsInsideArea`, `CanEngage(heroX, heroHalfWidth, heroFeetY, minX, maxX, minY, maxY, heroAlive, timeFrozen, worldHeld, gameplayPaused)`, `ShouldRevertToArmed(state, heroAlive, heroInsideArea)`, `IsClearPredicateMet(living, allWavesSpawned, puzzlesSolved, breakablesDestroyed)`, `ShouldSpawnWave(trigger, aliveAtMost, state, earlierWavesSpawned, living, held)`, `PlannedSpawnCount(tableLength, authoredCount, scaled, difficulty)` (the same per-row `ScaleEncounterCount` prefix the reward manifest compiles), `ExpandCameraSpan(min, max, minimumSize, clipMin, clipMax)`, `ClampMemberX`, `FallNetLineY`, `IsBelowFallNet`, `IsStalledSample(flying, onFloor, inFlight, chasing, ranged, moved, verticalGap)`, `NextStallFrames`, `ShouldRelocate`, `ShouldForceClear`, `RemainingCount`, `GoGlyph` (→ ← ↑ ↓).

Enum `EncounterWaveTrigger { OnBuild, OnEngage, WhenAliveAtMost, AfterPreviousCleared }` lives in the same file.

### Event and HUD

- `scripts/Core/EventBus.cs`: `enum EncounterLockState { Armed, Engaged, Cleared }`, `enum EncounterGoDirection { None, Right, Left, Up, Down }`, `struct EncounterLockPayload { LockID, State, Remaining, Total, GoDirection }`, `event OnEncounterLockChanged` / `RaiseEncounterLockChanged`. The payload is raised on every state edge and, while Engaged, whenever the count changes. `Remaining` = living members + members a wave has yet to spawn; `Total` = everything the lock plans on this difficulty.
- `StoryHUD` (new partial `scripts/UI/StoryHUD.EncounterLock.cs` and four one-line hooks in `StoryHUD.cs`): the authored node `SafeArea/EncounterLockPanel` in `scenes/ui/StoryHUD.tscn`, with `EnemiesLeft` (`HeadingLabel`, "Enemies left: {0}", shown while Engaged when `Total > 0`) and `GoPrompt/{GoText, GoArrow}` (`EmphasisLabel`, the text `hud_encounter_go` plus the arrow glyph, gold, shown for `GoPromptSeconds` after a clear). Theme roles only, no font-size overrides. Test seams: `EncounterCounterVisible`, `EncounterCounterText`, `EncounterGoPromptVisible`, `EncounterGoArrow`, and the const `StoryHUD.EncounterEnemiesLeftKey`. **Superseded 2026-10-25 (FU-LOCK, `docs/handoffs/FU_LOCK.md`):** the panel, `StoryHUD.EncounterLock.cs`, its hooks and both keys were deleted; the Story HUD shows no count and no GO prompt.
- `localization/en.csv` block `# GU-E`: `hud_encounter_enemies_left` ("Enemies left: {0}"), `hud_encounter_go` ("GO"), `encounter_lock_objective_clear` ("Defeat every enemy here to break the Severed barrier."). `--import` was run and `localization/en.en.translation` is committed. **Superseded 2026-10-25 (FU-LOCK, `docs/handoffs/FU_LOCK.md`):** `hud_encounter_enemies_left` and `hud_encounter_go` were deleted from `en.csv`; `encounter_lock_objective_clear` stays.

### The node: `FTT.Environment.EncounterLock` (`scripts/Environment/EncounterLock.cs`)

- Built with `EncounterLock.Create(EncounterLockSpec)`; barriers are built as its children, both open. In `_Ready` it joins `EncounterLock.Group` (`"story_encounter_lock"`) and `TimeFreezeController.FreezableGroup` (`"time_freezable"`), and pins itself to the global frame (`GlobalTransform = Identity`, because the spec is in level coordinates).
- `Arm()`: once only. Plans each wave's count for the current Story difficulty (`PlannedTotal`), closes the exit barrier (pushing an overlapping hero to the area side), and spawns every `OnBuild` wave **dormant**. A lock nobody arms arms itself on its first tick. A lock already `Cleared` spawns nothing.
- `RestoreCleared()`: the F10 baseline path (and the test seam). It latches Cleared silently: no members (any already spawned go back to the pool), both barriers open, no payload. `RestoredCleared` is then true.
- The tick (`_PhysicsProcess` → `internal Tick(double)`):
  1. Poll members.
  2. Hero containment, **every tick, even while held**: revert to Armed on a living hero outside the area.
  3. If held (the Time Freeze latch, `ChronalRewindManager.IsWorldHeld`, or the live freeze controller), stop here.
  4. Armed: engage when `CanEngage`. Engaged: re-engage members that lost their target, spawn due waves (then `TimeFreezeController.FreezeActivatedRoom`), run the stall watchdog, then clear or hard-cap.
- Engage: the entry closes (with the hero push-out), every living member gets `Engage(hero)`, the camera gets `SetLockBounds(CameraBounds)`, then `StateChanged` and the payload. **Superseded 2026-10-25 (FU-LOCK, `docs/handoffs/FU_LOCK.md`):** `SetLockBounds` no longer starts an ease; it opens the limits to the union of the view and the lock's bounds and the limits then ratchet inward only as the follow rig's view passes them.
- Revert: the entry opens, the exit stays closed, members go dormant (HP kept), the camera gets `ClearLockBounds()`.
- Clear: latched. Both barriers open, the camera gets `ClearLockBounds()`, the payload carries `GoDirection`.
- Members are registered at the spawn call (`EnemyFactory.Spawn` under the lock's parent) at their `SpawnGeneration`. Each member subscribes to `Died`. A member counts as removed when it is dead, freed, queued for deletion, recycled (generation changed: logged as `[encounter-lock] … recycled`, counted in `RecycledMembers`) or no longer carries the lock's token (parked by the pool).
- Stall watchdog (Engaged and unheld only): a member stalled for `StallFrames` of the lock's own ticks goes back to its anchor through `EnemyController.RelocateToEncounterAnchor` (never killed). Stalled means either of:
  - off any floor while not in a knockback flight;
  - a melee chaser that moved under 4 px while the hero is more than 200 px above or below it.

  Ranged mobs (`PrimaryAttack.SpawnsProjectiles`) holding still are never stalled.
- Hard cap: an Engaged lock past 300 s with no living member, whose required puzzles and breakables are done, force-clears and logs (`[encounter-lock] … force-cleared`). In practice it only fires for a wave that can never spawn; it never opens a puzzle or breakable the hero can still finish.
- Public read surface: `LockID`, `Area`, `CameraBounds`, `GoDirection`, `ObjectiveKey`, `Spec`, `EntryBarrier`, `ExitBarrier`, `State`, `IsHolding` (the **mover-hold query**: true while Armed or Engaged), `IsArmedOnce`, `RestoredCleared`, `ForceCleared`, `EngagedSeconds`, `EngageCount`, `RevertCount`, `StallRelocations`, `RecycledMembers`, `PlannedTotal`, `RegisteredCount`, `LivingMemberCount`, `LivingMembers`, `AllWavesSpawned`, `RemainingCount`, `IsTimeFrozen`, `Hero` / `Camera` (settable), and `event StateChanged(lock, previous, current)`.
- `internal static bool SuppressForTesting`: while set, every lock builds Cleared.

### `EncounterLockSpec`, `EncounterLockWave` and `EncounterLockSpawn` (`scripts/Environment/EncounterLockSpec.cs`)

Plain data.

- `EncounterLockSpec`: `LockID`, `Area` (`Rect2`; use `EncounterLockSpec.Span(minX, maxX, bottomY)`, which runs from `StoryGateRules.ColumnTopY` to `bottomY`), `CameraBounds?` (null derives it: the area grown to the view and clipped to `LevelBounds`), `LevelBounds?` (the base fills it), `EntryBarrierX?` / `ExitBarrierX?` (barrier centre x; null means no barrier on that side), `EntryBarrierYRange?` / `ExitBarrierYRange?` (`Vector2(top, bottom)`, default `ColumnTopY`..`BarrierFloorY`), `BarrierFloorY`, `BarrierWidth` (20), `Waves`, `RequiredPuzzleIDs`, `RequiredBreakables` (`DamageableEnvironmentObject[]`), `ObjectiveKey`, `GoDirection` (default `Right`).
- `EncounterLockWave`: `RowID` (the manifest row, without `@n`), `Spawns`, `Trigger`, `AliveAtMost`, `Scaled` (false only for an `@0` row), `AuthoredCount` (-1 means the table length; set it to the row's `@n` for a table with Hard-only extras), `EnemyIDs`, and the helper `EncounterLockWave.FromTable(rowID, (string, Vector2, Vector2?, Vector2?)[] table, trigger)`.
- `EncounterLockSpawn(enemyID, position, patrolA?, patrolB?)`.

### The barrier: `FTT.Environment.EncounterLockBarrier : StaticBody2D` (`scripts/Environment/EncounterLockBarrier.cs`)

- Environment layer, mask 0, one rectangle shape. Built **disabled** by `Create(lockID, isEntry, areaSide, centerX, topY, bottomY, width)` and toggled with `SetClosed(bool)` → `SetShapeDisabledSafe` (flush-safe).
- Group `EncounterLockBarrier.Group` (`"story_encounter_barrier"`). Not in `StoryGateRules.DoorGroup` and not a `ForcefieldBarrier`.
- `PushOut(body, halfWidth)` puts an overlapping body just past the face on the area side and zeroes its horizontal speed.
- Read surface: `Footprint`, `CenterX`, `TopY`, `BottomY`, `Width`, `IsEntry`, `AreaSide`, `IsClosed`, `Shape`, `LockID`.
- Placeholder look: an `UnboundCold` / `UnboundColdDim` curtain, drawn only while closed, with zig-zag edges and a column of hollow diamonds (the non-colour channel). Only the part at y ≥ 0 is drawn. **Superseded 2026-10-25 (FU-LOCK, `docs/handoffs/FU_LOCK.md`):** no curtain is drawn any more: the walls are invisible (collision, `IsClosed` and push-out unchanged).
- `EncounterSpawnTear` (`scripts/Environment/EncounterSpawnTear.cs`): a 0.5 s cold jagged tear at each spawn-in. Placeholder, not pooled.

### Members: `EnemyController.Encounter.cs` (new partial) and hooks in `EnemyController.cs`

- `event Action<EnemyController> Died`, raised at the end of `Die()`. Subscribers are dropped on both halves of a pool cycle.
- `int SpawnGeneration`, incremented by every `OnSpawn`.
- Membership read surface: `IsEncounterMember`, `EncounterToken`, `EncounterLockID`, `IsEncounterDormant`, `EncounterBounds`, `EncounterAnchor`, `NeedsEncounterEngage`.
- Methods: `JoinEncounter(token, lockID, bounds, anchor, dormant, facesRight)` (refuses a summon, a `DrillInvulnerable` body or a dead one), `SetEncounterDormant(bool)` (going dormant drops the attack, target, stun and launch, keeps HP, sets `Patrol` and faces the entry), `Engage(PlayerController)` (wakes into `Chase` on the hero), `LeaveEncounter()`, `RelocateToEncounterAnchor(reason)`. The relocation goes through `SafeRelocation.Resolve` from the anchor to the anchor, so it validates the landing's floor and never sweeps up through the floor a fallen body is under; it zeroes the launch and the slam bounce, logs `[encounter-lock] …`, and counts `EncounterRelocations`.
- Test seams: `EncounterFallRecoveries`, `EncounterRelocations`, `EncounterWardedHitsRefused`, `ApplyEncounterBoundsForTest()`.
- **The dormant ward** (`RefuseEncounterHit`, the first line of `OnHurtboxHit`): refuses every hit with no damage, knockback, status, mark or hitstop, and no landed-hit report (so no meter and no Rally). It shows a cold `UnboundCold` flash, plus a grey (`Slate`) "0" for a player-dealt hit. The same guard covers:
  - `ApplyStatusEffect` (a hitless status);
  - the Venom tick;
  - `IsGrabbable`.

  Scripted `TakeDamage(int)` is not warded, matching the boss ward.
- **Bounds**: `ApplyEncounterBounds()` runs after every move (end of `ProcessMobTick`, and after a thrown flight) in every state, dormant included; a held body is the hero's. It clamps the pivot to `[Area.X + halfWidth, Area.End.X − halfWidth]` and zeroes vx on a clamp. A body whose feet are more than 240 px below the area's bottom gets the anchor relocation.
- `KeepsChasingTarget` hook: an engaged member keeps chasing a target anywhere inside its area, so the P4 leash never drops it.
- Hooks in `EnemyController.cs`, one line each: `IsGrabbable`, the thrown-flight branch, `} else if (!TickEncounterDormant(dt)) {` in place of the AI state switch's `else`, after `ResolveStoryOverlaps`, `KeepsChasingTarget`, the end of `Die`, the first lines of `ApplyStatusEffect` and `OnHurtboxHit`, the Venom tick, `OnSpawn`, `OnDespawn`, and the end of the non-preserve `ApplyStoryRewind` branch (`OnEncounterRewindRestored`: re-clamp and re-face, membership kept).

### Camera: the lock-bounds layer in `StoryCameraConfiner`

- `SetLockBounds(Rect2)` / `ClearLockBounds()`. `SetBounds(room)` always stores `RoomBounds`, and while a lock holds the camera it does nothing else. That covers Level 3's and Level 7's repeatable room triggers and the base's `OnRewindLanded` and `ApplyResumeCameraBounds`. **Superseded 2026-10-25 (FU-LOCK, `docs/handoffs/FU_LOCK.md`):** the lock layer is now a ratchet (`StepRatchet`), see below.
- The limits ease toward the target with `StoryCameraRules.DampFactor(EncounterLockRules.CameraEaseSeconds, delta)` in `StepBoundsEase(delta)`. That method is public, and one line at the top of `_PhysicsProcess` calls it. **Superseded 2026-10-25 (FU-LOCK, `docs/handoffs/FU_LOCK.md`):** there is no ease: `StepBoundsEase` and `CameraEaseSeconds` were deleted; `StepRatchet(desiredCentre)` moves each limit side toward the target only when the follow rig's unclamped desired view has passed it, inward and never back.
- `ActiveBounds` reads the target at once. `CurrentLimits` is the eased rect. **Superseded 2026-10-25 (FU-LOCK, `docs/handoffs/FU_LOCK.md`):** `CurrentLimits` is the ratcheting working rect (the target merged with the view at the call), not an eased rect.
- Other read surface: `HasLockBounds`, `LockBounds`, `RoomBounds`, `IsEasingBounds`.
- Without a follow rig the limits apply at once.
- GU-B's `Zoom` / `ViewSize` / framing members were not touched.

### The base level: `StoryLevelControllerBase` (new partial `StoryLevelControllerBase.EncounterLocks.cs` + edits)

- `protected EncounterLock BuildEncounterLock(EncounterLockSpec spec)`: call it in `BuildLevel`. It fills `spec.LevelBounds` from `LevelBounds` and wires `StateChanged`. Read surface: `IReadOnlyList<EncounterLock> EncounterLocks` and `EncounterLock ActiveEncounterLock`.
- `_Ready` now calls `ArmEncounterLocks()` right after `SpawnPlayer()`, so after the resume baseline is applied. It hands each lock `Hero = Player` and `Camera = Camera`, then calls `Arm()`.
- `ApplyResumedEncounterBaseline`: every baseline ID that names a lock rebuilds that lock Cleared (`RestoreEncounterLockCleared`), then the level's own `MarkEncounterCleared(id)` still runs.
- `ExplicitEncounterBaselineVersion` 2 → **3**.
- Objective hook: while a lock is Engaged, a non-empty `ObjectiveKey` is posted. When an Engaged lock clears or reverts, `protected virtual void RestoreLevelObjective()` runs. By default it re-posts the last objective the level set through `SetObjective` (which now remembers it), else `InitialObjectiveKey`.
- `BuildWaveTrigger`'s deferred callback now calls `TimeFreezeController.FreezeActivatedRoom` after `onEntered`.
- The `OnRewindLanded` comment was corrected from 15 s to 5 s.

### Other engine fixes

- `RoomTransitionTrigger.ActivateRoom`: `FreezeActivatedRoom` now runs after `EmitSignal(RoomEntered)`, so waves spawned by `onEntered` handlers (Levels 6, 7, 10, 11) are frozen.
- The three position-writing pulls now move through the body's own collision (`MoveAndCollide`, the Tempest pattern), so walls and barriers stop them:
  - `CleopatraSandstormVortex.PullTargetsTowardCenter`;
  - `EinsteinUltimate.PullTargetsTowardSingularity`;
  - `TeslaLorentzPulse.PullTargetTowardOwner` (Lorentz Attraction).
- `TimelineIntegrityRules.ProvisionalParScale = 1.5f` and `ProvisionallyScaledSeconds(float)`. They are applied once where `StoryManager` receives par: `BeginIntegrityClock` (so `ParSecondsForCurrentLevel` = authored × 1.5) and `SetRecoveryRouteSeconds` (F11 route budgets × 1.5, so the recovery minima keep their point values). The level controllers keep their authored `ParSeconds`. Untimed levels stay at 0.

### Level 2 (Orléans), the worked example

- Three locks built in `BuildLevel` → `BuildEncounterLocks()`, after `WireShieldTowers()`. One wave per manifest row; the manifest needed no change.

| Lock (const) | ID | Area x | Entry barrier | Exit barrier | Wave (row, trigger) | Extra |
|---|---|---|---|---|---|---|
| `LockVanguard` | `level_02_orleans_lock_vanguard` | 300–3200 | centre 290 | centre 3210 | `level_02.room1`, OnBuild | objective `encounter_lock_objective_clear` |
| `LockSiegeLine` | `level_02_orleans_lock_siege_line` | 3220–6290 | centre 3210 | Gate A (6300), y 600–900 under the gate column | `level_02.room2`, **OnEngage** | `RequiredBreakables = { ShieldTowerA }`; keeps the level's tower objective |
| `LockBattlement` | `level_02_orleans_lock_battlement` | 6460–8690 | centre 6450 | Gate B (8700), y 240–900 under the gate column | `level_02.room3`, OnBuild | objective `encounter_lock_objective_clear` |

- Baselines:
  - `checkpoint_0` → none;
  - `checkpoint_1` → vanguard and siege line;
  - `checkpoint_2` → all three.

  The old `_room2_wave` / `_room3_wave` encounter IDs, their latches, the two wave triggers, `SpawnInitialEnemies`, `SpawnWave` and the seams `Room2WaveLatched` / `Room3WaveLatched` were removed.
- **The Middle checkpoint moved from x 5300 (inside the siege-line fight) to `MiddleCheckpointX` = 6370**. That is the corridor between Gate A's forcefield (east face 6324) and the battlement's entry barrier (west face 6440). `MarkWavesClearedThrough(Checkpoint1)` now also force-opens generator A, because a resume there stands past Gate A.
- The siege-line room trigger moved from x 3200 to 3260 (`SiegeLineLockMinX + 40`). At 3200 a hero pressed against the vanguard's exit barrier reached it and posted the siege-line objective early.
- `RestoreLevelObjective()` → `RefreshObjective()`.
- New public read-only constants and properties: `Room1RowID`, `Room2RowID`, `Room3RowID`; the lock min/max x constants; `MiddleCheckpointX`; `VanguardLock`, `SiegeLineLock`, `BattlementLock`.

## 2. Files changed

New:
- `scripts/Environment/EncounterLockRules.cs`
- `scripts/Environment/EncounterLockSpec.cs`
- `scripts/Environment/EncounterLock.cs`
- `scripts/Environment/EncounterLockBarrier.cs`
- `scripts/Environment/EncounterSpawnTear.cs`
- `scripts/Environment/StoryLevelControllerBase.EncounterLocks.cs`
- `scripts/Enemies/EnemyController.Encounter.cs`
- `scripts/UI/StoryHUD.EncounterLock.cs`
- the `.uid` sidecars of all of the above
- `tests/Unit/EncounterLockRulesTests.cs`
- `tests/Unit/EncounterLockTests.cs`
- `tests/ContentValidation/EncounterLockContentTests.cs`
- this handoff

Changed, inside my ownership:
- `scripts/Enemies/EnemyController.cs` (the one-line hooks)
- `scripts/Environment/StoryLevelControllerBase.cs`
- `scripts/Environment/StoryCameraConfiner.cs` (bounds layer only)
- `scripts/Environment/RoomTransitionTrigger.cs`
- `scripts/Core/EventBus.cs`
- `scripts/UI/StoryHUD.cs`
- `scenes/ui/StoryHUD.tscn`
- `scripts/Core/StoryManager.cs`
- `scripts/Core/TimelineIntegrityRules.cs`
- the three pull kits: `CleopatraAbilities.cs`, `EinsteinUltimate.cs`, `TeslaAbilities.cs`
- `scripts/Environment/Level02Controller.cs`
- `localization/en.csv`, `localization/en.en.translation`
- `tests/ContentValidation/Level02ContentTests.cs`
- `tests/Unit/EncounterBaselineTests.cs`

**Outside my ownership** (minimal, needed by my changes):
- `tests/ContentValidation/ArenaGeometryContentTests.cs`: one condition in the `AddReachIssues` helper. A standable rect whose top is at `StoryGateRules.ColumnTopY` is skipped as a launch point, as gate columns already were. A lock barrier is closed from level build and rises to the sky ceiling, so the gate-column reach sweep read it as a perch 58 px from Gate A's column. Every Phase B level with a gate will meet the same thing.
- `tests/Unit/TimelineIntegrityTests.cs` and `tests/Unit/StoryLevelControllerBaseTests.cs`: rewritten in place for the par scale (§5).

No scene other than `StoryHUD.tscn` changed. `level_02_rewards.tres` and `Level_02_Orleans.tscn` needed no edit.

## 3. Decisions (Provisional, for `PROVISIONAL-ENCOUNTER-LOCK-RULES`)

1. **Members are dormant until the engage, then all wake into Chase on the hero, whatever their aggro radius.** The plan's "Engage(PlayerController): wakes into Chase". Turn-taking (GU-F) then spaces them.
2. **Containment is polled even while the world is held.** The engage, the waves and the watchdog wait for the thaw. A landing outside therefore reverts the lock at the landing, not at the thaw, so the hero is never shut out behind a closed entry for the R03 hold's 60 frames.
3. **A dead hero is not judged.** The rewind playback moves it, and the lock judges it again once it lives.
4. **A member keeps chasing a target anywhere in its area** (the P4 leash is bypassed), and an engaged member that lost its target (the hero's death, a de-aggro) is re-engaged each tick.
5. **The dormant ward refuses every hit at the intake**, not only player-dealt ones; the grey "0" is shown only for player-dealt hits. It also refuses hitless statuses, the Venom tick and grabs. Scripted `TakeDamage(int)` is not warded (boss parity). The public `ApplyKnockback` and `ApplyStun` are not guarded. Their only non-hit callers are crowd bowling (a thrown body within 55 px, and barriers separate areas) and Lincoln's bounce stun (which needs a landed spike).
6. **The fall net and the watchdog relocate through `SafeRelocation.Resolve(anchor → anchor)`** so the landing's floor is validated and nothing sweeps up through a floor. The boss net writes its anchor directly; this is the same idea, routed through the rule as the plan asks.
7. **The stall watchdog** samples only while the lock is Engaged and unheld. Stalled means off any floor and not in a knockback flight, or a **melee** chaser that moved under 4 px while the hero is more than 200 px above or below it. Ranged mobs holding still are exempt: shooting from a post is their job. The threshold is 600 consecutive stalled ticks.
8. **The hard cap never bypasses a pending puzzle or breakable.** The plan's literal rule ("no living registered member force-clears") would have opened a puzzle lock after 5 minutes. With waves counted as spawned once attempted (a failed pool spawn is logged and skipped), the cap only rescues a wave whose trigger can never fire.
9. **Remaining = living + unspawned planned.** The counter hides when `Total` is 0 (a puzzle-only lock).
10. **Area vertical span** is authored from `ColumnTopY` to the level bottom (`EncounterLockSpec.Span`), so no jump or movement ability ever leaves an area upward and reverts it. The camera rect clips that span to the level.
11. **The camera rect** is the area clipped to the level, and if narrower than the view it is grown about its centre and shifted inside the level. The barriers sit just outside the camera's limits, so the screen edge itself reads as the lock (the classic beat-'em-up stop). The curtain's glow reaches 10 px past each face. **Superseded 2026-10-25 (FU-LOCK, `docs/handoffs/FU_LOCK.md`):** there is no curtain glow and the walls draw nothing; the stop is felt as collision only.
12. **Barrier defaults**: 20 px wide and full height (`ColumnTopY` to the floor). At a gate with a gate column, the barrier spans only the column's foot to the floor, so it never stands beside the column as a second wall.
13. **Content rules as implemented** (`EncounterLockContentTests`):
    - no checkpoint, font or sealing anchor footprint (±30 px) inside an area;
    - a Reset Puzzle station only inside a lock with `RequiredPuzzleIDs`;
    - no area x-overlap with any boss or Mirror arena, and no member spawn inside one;
    - every spawn inside its area with a floor within 120 px (flyers 700 px), and no ground spawn on a post higher above the area's ground than the roster's weakest double-jump rise;
    - an area 1280–3200 px wide;
    - no lock's barrier inside another lock's area;
    - each wave's enemy IDs, `@n` and `Scaled` equal its manifest row, with no row claimed twice;
    - every barrier is the `EncounterLockBarrier` class on Environment, not in `DoorGroup`, narrower than a hero, with floor under it, outside its own area and within 20 px of its edge;
    - every lock ID starts with the level ID and is listed by exactly the checkpoints at or past its area's end.
14. **Level 2's siege line spawns on engage** (the soldiers come over the earthworks); the vanguard and battlement are pre-placed. This shows both styles. **Lock 2 needs generator A**, because its exit sits at Gate A. **Lock 3 does not need generator B**: Gate B's forcefield still blocks until B breaks, and the plan calls room 3 "the floor fight before the climb".
15. **A standalone lock pins itself to the global frame** (`GlobalTransform = Identity` in `_Ready`).
16. **The par scale is applied in `StoryManager`'s two receiving methods** through one helper, not in the level controllers. That is the brief's "where StoryManager receives par, F11 fractions included".

## 4. Deviations from the plan

- **`MaxAreaWidthPixels` is 3200, not "about 2600".** The plan's own Level 2 areas are 2900 px (300–3200) and 3070 px (3220–6290). The cap is one authored camera room (Level 2's rooms are 3200 px). Phase B can stay narrower.
- **Level 2's areas differ slightly from the plan's 300–3200 · 3200–6300 · 6400–8700.** They are 300–3200 · 3220–6290 · 6460–8690, so that every barrier stands outside its own area and hugs it. The siege line and battlement exits sit at Gate A and Gate B under their columns, and there is a corridor (6324–6440) for the moved Middle anchor.
- **`ArenaGeometryContentTests` needed a one-line helper change** (§2). The plan expected the gate sweeps not to see lock barriers, but an exit barrier is closed from level build, so the reach sweep did see it.
- **The hard cap's condition was narrowed** (Decision 8).
- **"Never members: ArenaGuardians, BossDecoys, bosses, the Mirror clone"** holds by type: none of them is an `EnemyController`. Summons and `DrillInvulnerable` bodies are refused by `JoinEncounter`.
- **The non-preserve `ApplyStoryRewind` branch keeps membership** (the lock owns it). It re-clamps and re-faces instead of resetting. No campaign mob uses a non-preserve policy today.

## 5. Tests

New suites (+35):
- `EncounterLockRulesTests` (pure C#, no `[RequireGodotRuntime]`): **11**. Engage inset and holds; containment and revert; the clear predicate; wave advance by trigger; the per-row difficulty prefix; camera-span expansion; the member clamp and the 240 px fall net; the stall sample, counter and relocation; the hard cap; the HUD count and glyphs; the par scale (and that F11 budgets keep their points) and the area bounds.
- `EncounterLockTests` (real frames): **17**.
  - `ArmingClosesOnlyTheExitAndPlacesThePrePlacedMembersDormantInsideTheArea`
  - `TheHeroWalkingInEngagesTheLockClosesBothBarriersAndWakesAndSpawnsItsMembers` (with the payload)
  - `KillingEveryMemberClearsTheLockOpensBothBarriersAndTheClearLatches`
  - `AHeroLandingOutsideAnEngagedLockRevertsItToArmedAndWalkingBackInReengages`
  - `TimeFreezeHoldsTheEngageAndTheWavesUntilTheThaw` (a real `TimeFreezeController`)
  - `ADormantMemberRefusesEveryPlayerHitButAnEngagedOneTakesIt`
  - `APooledMemberReleasedOrRecycledCountsAsRemoved`
  - `AMemberThatFallsBelowTheAreaIsReturnedToItsSpawnPoint`
  - `AMemberPushedPastTheAreaEdgeIsClampedBackInsideInEveryState`
  - `AWallPhasingRiftPhantomStaysInsideTheAreaItCouldOtherwisePassThrough`
  - `TheHudCountsTheEnemiesLeftAndShowsTheGoArrowAfterTheClear`
  - `TheCameraLockLayerOverridesRoomBoundsAndEasesInAndOut` **Superseded 2026-10-25 (FU-LOCK, `docs/handoffs/FU_LOCK.md`):** renamed `TheCameraLockLayerOverridesRoomBoundsAndRatchetsInAndOut`; `EncounterLockCameraRatchetTests` (7) is new.
  - `TheStallWatchdogRelocatesAMemberStuckOutOfReachAndNeverKillsIt`
  - `TheHardCapForceClearsALockWhoseWaveCanNeverSpawn`
  - `AClosingBarrierPushesAnOverlappingHeroToTheAreaSide`
  - `TheTestSeamBuildsEveryLockCleared`
  - `ABaseLevelArmsItsLocksWithItsHeroAndCameraAndHandsTheObjectiveBackOnTheClear` (a test level built on the base; its non-suite controller `EncounterLockTestLevelController` lives in the same file)
- `EncounterLockContentTests` (all sixteen campaign scenes, 0–15): **7**. One case per content rule in Decision 13. It asserts at least one lock exists, so it can never pass vacuously.

New cases in existing suites (+4):
- `Level02ContentTests` 11 → 14:
  - `TheThreeApproachRoomsAreEncounterLocksOneWavePerManifestRow`
  - `TheMiddleAnchorStandsInTheCorridorPastGateAOutsideEveryLock`
  - `ClearingTheSiegeLineNeedsItsGeneratorAsWellAsItsSoldiers` (real scene, real frames)
- `EncounterBaselineTests` 5 → 6: `ResumingOrleansPastItsLocksBuildsThemClearedAndLeavesTheNextOneArmed` (the real scene resumed at checkpoints 1 and 2).

Rewritten in place, no count change:
- `TimelineIntegrityTests.TheDenominatorIsFixedAtLoadAndNeverRecalculatedFromSurvivors`: the expected live drain is now `100 / (Par × ProvisionalParScale × 1.5) × 1.4 / 1.6`.
- `StoryLevelControllerBaseTests.EveryTimedCampaignLevelAuthorsAParSecondsAndTheClockArmsFromIt`: `ParSecondsForCurrentLevel` is now expected to equal `ParSeconds × ProvisionalParScale`.
- `ArenaGeometryContentTests` (helper only; see §2).

**Delta: +39** (35 new-suite cases + 4 new cases in existing suites). The `[TestCase]` attribute count under `tests/` is 3060.

Suites that boot the real Level 2 or a Level 2 test controller and needed **no** change: `StoryCameraRigTests` (campaign camera), `LevelDeathRewindDiagnosticTests` (the hero stays at its spawn, west of the vanguard), `VolleyCoverTests`, `StageLightingRigTests`, `SceneSmokeTests`, `ScenePoolConfigTests`, `AbsenceBeatContentTests`, `SecretCachePlacementTests`, `ThoroughRunCollectionWalkTests`, `ArenaGeometryContentTests` (cases), `PostBossChainTests`, `StoryHealingLoopTests`, `CompletionOnlyBankingTests`, `LevelStartResourceResetTests`, `ResonanceLiveApplyTests`, `StoryLevelControllerBaseTests` (apart from the par line). None needed `EncounterLock.SuppressForTesting`. The seam is used only by `EncounterLockTests`.

Runs (lock held for each, orphans checked):
- `EncounterLockRulesTests|EncounterLockTests`: 27/28, then a test-only fix (the stall case now waits 30 frames for the spawn drop to land before counting), then 28/28.
- `EncounterLock*|Level02ContentTests|EncounterBaselineTests`: 44/44.
- `EncounterLock|Level02ContentTests|EncounterBaselineTests|ArenaGeometryContentTests|TimelineIntegrityTests|StoryLevelControllerBaseTests|StoryCameraRigTests|StoryHud`: 128/128.
- **Full suite: `Passed! - Failed: 0, Passed: 3060, Skipped: 0, Total: 3060` (1 m 7 s) = 3021 + 39.** The final full run after the last edits is reported in the structured result.

## 6. Measurements

- **Smoke:** `Level_02_Orleans.tscn --quit-after 300` exits 0. It prints only the known at-exit noise: 2 leaked ObjectDB instances and 1 resource in use, the same as the control levels in CLAUDE.md.
- **Bot:** one `play` run on Level 2 (Joan, Normal, seed 0, tag `gue_e1`, `--max-seconds=900`).
  - All three locks engaged and cleared: 8 of 8 standards killed, `passed_while_alive` 0, `bot_gave_up_unreachable` 0, 2 checkpoints, progress 9895 px.
  - It then died 4 times to the Siegemaster Duke (all four deaths at x 9939–10040; 322 of 431 damage taken from the Duke) and ended `left_level:collapse(Death)` at 214.7 s.
  - This is the same end as the GU-F run `guf` on the plan base (4 boss deaths, collapse), and within the pre-change spread of this report directory: `n1`, `n2` and `fxwardn` completed with 3 boss deaths; `t1` and `t4` left with 4.
  - The bots do not know about locks yet (GU-BOT); this run only shows that the lock path does not stop the `play` bot.
- The par scale moves every timed level's clock: e.g. Level 2's 360 s par is now 540 s on the clock, so a par run ends at 66.7 % / 55.6 % / 44.4 % on Easy / Normal / Hard instead of 50 / 33.3 / 16.7, until par is re-measured.

## 7. Not delivered / open

- **Level 0, Florence and Levels 3–15 have no locks yet** (Phase B). `EncounterLockContentTests` passes trivially for them.
- **The camera's view constant is a fallback copy** (`EncounterLockRules.ViewWorldWidth` / `ViewWorldHeight`). After GU-B merges, point both at `StoryCameraRules.ViewWorldSize` and delete the copies. Until then a lock camera rect narrower than 1920 px is centred by the zoom-1.0 confiner, which is harmless.
- **The bots** (GU-BOT) still target by `EngageRadius` and give up after 720 frames. They need the lock queries (`StoryLevelControllerBase.EncounterLocks`, group `story_encounter_lock`, `EncounterLock.LivingMembers`, `IsHolding`) and a nav rebuild rule for barriers. The barriers toggle pre-built shapes, which `PlaytestNav` already notices.
- **Mover hold:** only the query (`IsHolding`, `StateChanged`). No level wires a mover to it yet.
- **Placeholders only:** the curtain, the spawn tear and the GO prompt. Nobody has looked at them; they are untuned against the 1.5 zoom. **Superseded 2026-10-25 (FU-LOCK, `docs/handoffs/FU_LOCK.md`):** the curtain and the GO prompt no longer exist; only the spawn tear remains a placeholder.
- **The Story HUD contract (`HUD_CONTRACT`)** does not yet name `EncounterLockPanel`. That amendment is owed. **Superseded 2026-10-25 (FU-LOCK, `docs/handoffs/FU_LOCK.md`):** moot: there is no `EncounterLockPanel`, so `HUD_CONTRACT` needs no amendment.
- **Not covered:**
  - a hero that grabs a member and is then reverted out keeps holding it (the clamp skips a held body);
  - crowd bowling's direct `TryApplyKnockback` on a dormant member across a barrier is not warded (within 55 px through a 20 px wall);
  - a status restored through `ActiveStatuses` on a dormant member is refused (nothing restores enemy statuses today).
- **`ProvisionalParScale` = 1.5 is a guess.** The Phase C bot batch must review it under `VERIFY-PAR-SECONDS`.
- **No one has played Level 2 with locks.** Pacing (all members of a 3000 px area waking at once), the archer stand-off and the mortar lanes inside the siege-line lock are untested by a person.

## 8. For the closeout

**AGENTS.md:**
- The Scene architecture bullet "Levels 2–15 are built on `StoryLevelControllerBase`" (:200): add the lock builder and the arm step.
- The scene-scoped systems table (:233): add rows for `EncounterLock`, `EncounterLockBarrier`, `EncounterLockRules`, `EncounterSpawnTear` and the `EnemyController` member hooks.
- The Story enemy combat rules bullet (:296): P4 is bypassed inside a lock.
- `VERIFY-PAR-SECONDS` (:357): the provisional ×1.5 par scale.
- The Story progression F10 bullets: baseline version 3, and lock IDs in baselines.
- The Story HUD bullet: the lock widget.
- The Level 2 and checkpoint facts: the Middle anchor at 6370.
- The baseline count.
- Coding conventions: "build locks in `BuildLevel`; never infer membership from a parent".

**CLAUDE.md:**
- The baseline count.
- The bot section: `passed_while_alive` meaning, and comparability across this package.

**`design-godot.md` lines owed** (the recon's index):
- :105 and :245–246 (game loop and encounters)
- :288
- :316 (par "including critical-path combat")
- :406 (the Combat Trial)
- :2122 (F10 baseline encounter IDs now include lock IDs)
- :3208–3211 "Enemy Spawn Rules" and :3213–3216 "Room Transitions" (the lock state machine, barriers, dormant members, the camera override)
- the Story HUD section (the counter and GO prompt) **Superseded 2026-10-25 (FU-LOCK, `docs/handoffs/FU_LOCK.md`):** the counter and GO prompt were removed.

**Ledger rows:**
- Write `USER-ENCOUNTER-LOCKS` (quote request 3 and the scope ruling "every area with enemies, plus puzzle rooms whose puzzle blocks the way forward") and `PROVISIONAL-ENCOUNTER-LOCK-RULES` (Decisions 1–16 and the constants table).
- Amend `VERIFY-PAR-SECONDS` (the ×1.5 scale, its single consumption point, and the F11 route budgets scaled with it).
- Amend `PROVISIONAL-ENEMY-CHASE-LEASH`: its premise "encounters may be skipped" is superseded inside locks, and an engaged member's leash is bypassed.
- Amend `P5-GATE-COLUMN-SWEEP`: the sky-ceiling launch-point skip.
- Note `ARENA-WAKE-REVEAL`: unchanged, because boss arenas are not locks.
- Amend `HUD_CONTRACT` (the `EncounterLockPanel` widget). **Superseded 2026-10-25 (FU-LOCK, `docs/handoffs/FU_LOCK.md`):** withdrawn; there is no widget to name.
- Move the 2026-10-04 Level 2 Middle-anchor fact (5300) to 6370.

**Merge notes:**
- `EnemyController.cs`: my hooks are single lines at `IsGrabbable`, `ProcessMobTick` (three places), `KeepsChasingTarget`, `Die`, `ApplyStatusEffect`, the Venom tick, `OnSpawn`, `OnDespawn`, `ApplyStoryRewind` and `OnHurtboxHit`. GU-F hooks the same `Die` / `OnSpawn` / `OnDespawn` / `ApplyStoryRewind`; GU-D edits `TryApplyKnockback` and `ApplyStunnedKnockbackFriction`, which I did not touch.
- `StoryCameraConfiner.cs`: I edited only `SetBounds`' head and the first line of `_PhysicsProcess`, plus a new region at the end of `_PhysicsProcess`.
- When GU-F lands: a waiting mob is exempt from the stall watchdog by its same-floor gap. If GU-F exposes an `IsWaitingTurn`, AND it into `TickWatchdog` as well.

---

## AUTHORING GUIDE (Phase B: Levels 0, 1, 3–15)

**The recipe for a base level (Levels 3–15), from Level 2:**

```csharp
// Stable IDs: they start with the level ID (EncounterBaselineTests) and are F10 encounter IDs.
public const string LockVanguard = ID + "_lock_vanguard";
public const string Room1RowID = "level_02.room1";          // the manifest RequiredWaves row (no @n)

protected override void BuildLevel() {
    // ... rooms, checkpoints (OUTSIDE every area), towers/puzzles ...
    WireShieldTowers();
    BuildEncounterLocks();   // after anything a lock references (a generator, a puzzle)
}

private void BuildEncounterLocks() {
    _vanguardLock = BuildEncounterLock(new EncounterLockSpec {
        LockID = LockVanguard,
        Area = EncounterLockSpec.Span(300f, 3200f, LevelHeight),       // sky ceiling to level bottom
        EntryBarrierX = 300f - EncounterLockRules.DefaultBarrierWidthPixels * 0.5f,   // hugs the area, outside it
        ExitBarrierX = 3200f + EncounterLockRules.DefaultBarrierWidthPixels * 0.5f,
        BarrierFloorY = GroundY,
        Waves = new[] { EncounterLockWave.FromTable(Room1RowID, Room1Spawns, EncounterWaveTrigger.OnBuild) },
        ObjectiveKey = "encounter_lock_objective_clear",
        GoDirection = EncounterGoDirection.Right
    });
    // A lock whose exit is an existing gate: the barrier sits AT the gate, from the gate column's foot to the floor,
    // and the gate's breakable (or puzzle) is a clear condition.
    _siegeLineLock = BuildEncounterLock(new EncounterLockSpec {
        LockID = LockSiegeLine,
        Area = EncounterLockSpec.Span(3220f, GateAX - 10f, LevelHeight),
        EntryBarrierX = 3210f,
        ExitBarrierX = GateAX,
        ExitBarrierYRange = new Vector2(GateAColumnFootY, GroundY),
        BarrierFloorY = GroundY,
        Waves = new[] { EncounterLockWave.FromTable(Room2RowID, Room2Spawns, EncounterWaveTrigger.OnEngage) },
        RequiredBreakables = new DamageableEnvironmentObject[] { _towerA },   // RequiredPuzzleIDs = { "level_03.beam_routing" } for a puzzle door
    });
}

// Every checkpoint lists every lock whose area ends before it.
private static readonly IReadOnlyDictionary<string, string[]> Baselines = new Dictionary<string, string[]> {
    [Checkpoint0] = System.Array.Empty<string>(),
    [Checkpoint1] = new[] { LockVanguard, LockSiegeLine },
    [Checkpoint2] = new[] { LockVanguard, LockSiegeLine, LockBattlement }
};
protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

protected override void RestoreLevelObjective() => RefreshObjective();   // if your objective is derived from live state
```

Delete the level's `SpawnInitialEnemies`, `SpawnWave`, its wave triggers and their latches for every room that becomes a lock. The base arms the locks and restores Cleared ones from the baseline, and your `MarkEncounterCleared` still hears every baseline ID. Keep `MarkWavesClearedThrough` for non-encounter world state (gates, floods, escapes). A checkpoint past a gate should force the gate open there.

**A standalone level (Level 0, Florence):**
1. `var l = EncounterLock.Create(spec);` with `LevelBounds` set yourself.
2. `AddChild(l); l.Hero = _player; l.Camera = _camera;`
3. On a resume past it, call `l.RestoreCleared()` **before** `l.Arm()`.
4. Then `l.Arm();` (or leave it to the first tick).
5. Subscribe `l.StateChanged` for your objective.

**Rules** (`EncounterLockContentTests` enforces all of them; Decision 13 has the exact numbers):
- No checkpoint, Restoration Font or sealing anchor within 30 px of an area. A Reset Puzzle station only inside a lock with `RequiredPuzzleIDs`.
- No area may overlap a boss or Mirror arena's x-range.
- Every spawn stands inside its area, on a floor within 120 px below it (flyers 700 px), at most the weakest roster double-jump rise above the area's ground. Move posted mobs onto the route.
- An area is 1280–3200 px wide.
- One wave per manifest row, the same order. Set `AuthoredCount` to the row's `@n` for a table with Hard-only extras, and `Scaled = false` for `@0`. Split or merge rows only with the per-level totals kept.
- Barriers stand outside their own area, within 20 px of its edge, and never inside another lock's area. Two adjacent locks share one 20 px slot (lock A's exit and lock B's entry at the same x, as Level 2's 3200–3220).
- Every barrier needs floor under its x.
- A lock with no barrier fails the content test.
- **(FIX-E, 2026-10-13) A checkpoint past a lock is held shut until that lock is Cleared.** `CheckpointTrigger.IsHeldByEncounterLock` refuses activation while a lock the checkpoint's F10 baseline lists is not Cleared, and while the hero stands inside any lock that is not Cleared. The content test fails on any live checkpoint that lists a lock, or stands at or past a lock's area, and is not held at the level start; and on one that is held although no lock comes before it (for example a hero spawn inside an area). The guard reads the baseline **StoryManager** holds, so a standalone level (Level 0, Florence) must call `StoryManager.RegisterEncounterBaseline(checkpointID, lockIDs, StoryLevelControllerBase.ExplicitEncounterBaselineVersion)` for every checkpoint after a lock, or the content test fails.
- **(FIX-E) Keep each Middle and PreBoss strike surface out of basic-swing reach from inside the locks its baseline lists**, as belt and braces: the surface is `x ± 30`, and the longest Story basic hit reaches about 156 px past the pivot of a hero pressed against the closed exit barrier (widest reach profile, AttackRange minor bought). The guard is the real rule; Specials, Ultimates and shape queries ignore barriers, so geometry alone can never be enough. `Level02ContentTests` measures it with the roster's data (`LongestBasicSwingPixels`).
- **(FIX-E) No live checkpoint's respawn point may put the hero's 40 × 64 px body inside a room trigger whose `CameraBounds` do not contain that body** (`EncounterLockContentTests`, every campaign scene). The trigger fires on the first frame after a resume and confines the camera away from the hero; a once-only trigger never gives the room back. Level 2's Middle anchor at 6370 sat inside the battlement trigger (6380–6460, camera from 6400).

**Pitfalls I hit or saw:**
- **Build locks in `BuildLevel`.** Built later, a resume baseline cannot restore them Cleared, and their members spawn behind a resumed hero.
- **The exit barrier is up from level build.** Keep checkpoints and level-start positions off barrier footprints; the push-out moves an overlapping hero to the area side.
- **Room-transition triggers on a seam:** a hero pressed against an exit barrier can reach a trigger centred on the seam. Move the trigger just inside the next area (Level 2 moved the siege line's to 3260), or its `onEntered` fires early.
- **Gates with gate columns:** make the barrier run from the column's foot to the floor (`ExitBarrierYRange`), never full height beside the column.
- **Levels 3 and 7 re-fire room triggers on every crossing.** The camera lock layer swallows those while a lock is engaged; nothing to do.
- **Members never patrol while dormant.** If a level's flavour relied on patrols before the hero arrives, use `OnEngage` waves or accept idle posts.
- **The hard cap does not rescue unreachable members.** The watchdog relocates melee members stuck off the floor or out of reach to their anchor. If the anchor itself is unreachable, fix the content.
- **Escape sequences and timed floods (L5, L6, L15):** start them on a lock's clear (`StateChanged` → `Cleared`), not on an x trigger inside an area.
- **Tests:** a real-scene suite that walks the hero through a level and does not test locks can set `EncounterLock.SuppressForTesting = true` in try/finally. No existing suite needed it for Level 2.
- **(FIX-E) Tests that activate a checkpoint past a lock** (`Activate()`, `HandleStrike`, a strike on its `StrikeSurface`) are refused while that lock holds. Clear the lock first (kill its members, or `RestoreCleared()` for a lock the scenario stands past), or set `SuppressForTesting`.
- **(FIX-E) A test that stops a pooled member's physics** (`SetPhysicsProcess(false)`) must start it again before the body goes back to the pool: the pool does not, and a later case then gets a member that never ticks (the director drops it). `EncounterLockTests`' `LockScene` now does both ends.

---

## Review fixes (2026-10-13)

Workstream FIX-E of the Phase A adversarial review (`D:/Projects/absolum_research/update_2026-10-12/phaseA_review_verified.json`: the six findings on the lock framework, Level 2, zones, pulls and the lock/director seams). Branch `worktree-wf_0734101c-91c-1`, based on `199e260` (merged Phase A). **Nobody has played any of it**; the evidence is the suites below.

### Per finding

1. **Level 2's Middle checkpoint could be struck from inside the engaged siege line, and a reload then skipped the lock and generator A** (high; reported twice). The fix is in the framework, so no Phase B level can repeat it.
   - `CheckpointTrigger.IsHeldByEncounterLock` (`scripts/Environment/LevelManager.cs`) is true while either of these holds:
     - a lock that the checkpoint's F10 baseline lists (`StoryManager.GetEncounterBaseline`) is not Cleared;
     - the hero stands inside any lock that is not Cleared. This is the belt-and-braces half: a strike surface is a PersistentObject area, so an area-to-area hit passes straight through a barrier. It uses the new `EncounterLock.HeroStandsInside`, the same containment test the lock's tick uses.
   - `Activate()` refuses outright while the checkpoint is held. It is the one chokepoint for the strike surface, the Entry self-activation and direct calls. A refused activation does nothing: no `TryActivateCheckpoint`, no Mending, no rewind refresh, no Integrity bank, no `CheckpointReached`/`Committed`, no save.
   - A lock ID with no live lock in the tree never holds.
   - Feedback: the fracture posts the `EnvironmentNotice` `checkpoint_held_by_encounter_lock` ("The Severed barrier holds this fracture. Clear the area first."). It posts at most once per notice lifetime (`EnvironmentNotice.DefaultSeconds` × the physics rate), so a three-hit string shows it once. The key is in an `en.csv` `# FIX-E` block; `--import` was run and `en.en.translation` is committed.
   - **Level 2 moved:**
     - `MiddleCheckpointX` 6370 → **6500**.
     - `BattlementLockMinX` 6460 → **6580**: the entry barrier is now 6560–6580 and the area 6580–8690 (2110 px). The spikes (6690–6810) and both room 3 spawns are still inside it, and the engage line (6624) is still short of the spikes.
     - The anchor's strike surface (6470–6530) now lies past the longest Story basic swing from inside the siege line: a hero pressed against the closed exit stands at 6270, and that swing reaches 6426.
     - The anchor is also clear of the room 3 trigger (finding 3).
     - `MarkWavesClearedThrough(Checkpoint1)` still breaks generator A. That is now consistent, because the checkpoint can only commit once the siege line has been cleared.
   - Not done: the verifiers' second layer, which filters `CheckpointRecord.BaselineEncounterIDs` at commit time. It is redundant, because every activation path goes through `Activate()`.
2. **Relativity Rift zone ticks damaged dormant members** (medium).
   - `PlaceholderZone.ApplyTickTo` now skips a dormant member before damage, status and meter (`if (enemy.IsEncounterDormant) return;`).
   - The skip is silent, like the boss ward's Venom tick. The tick is not routed through the hurtbox, because that would change mob intake (proration, poise, confirm).
   - **Audit** of every player-sourced path that reaches an enemy outside `OnHurtboxHit` (the warded intake):

   | Path | Reaches a dormant member? | Outcome |
   |---|---|---|
   | `PlaceholderZone.ApplyTickTo` → `TakeDamage(int)` + meter credit (the Rift) | yes: through an Armed lock's open entry after a revert, and its 90 px radius spills past a 20 px barrier | **closed** |
   | Crowd bowling, `EnemyController.ProcessThrownFlight` → `TakeDamage(int, Vector2?)`, `TryApplyKnockback`, `ApplyStun` | yes: a non-member thrown in through an Armed lock's open entry | **closed** (a dormant `other` is skipped) |
   | The Story throw, `PlayerController.ResolveStoryThrow` → `TakeDamage(int)`, `LaunchThrown` | no. `IsGrabbable` refuses a dormant member, and a held member cannot go dormant: the revert needs the holder outside the area, and the grab pins the holder beside its victim inside it | left, recorded |
   | Lincoln's Kinetic Splitting bounce, `ResolveGroundBounces` → `ApplyStun` (and `SpikeAirborneTargets`' velocity write) | only an airborne dormant ground member under the overhead arc, reached through an open entry in the few frames it is falling. A dormant flyer never lands, so it is never stunned | left, recorded (no damage, no meter) |
   | The Venom tick | already guarded (GU-E) | — |
   | The D9 wall impact (`TakeDamage` in the wall bounce) | no: going dormant clears the launch and the bounce latch | — |
   | Every kit hit through `Hurtbox.TakeHit` / `Hitbox`: Specials and Ultimates (`DealSingularityHit`), constructs (coil arcs, the coil fence, turret bolts, serpent nest bites), ground waves, bursts (E=mc², Requiem), the Lorentz chain, the Rift Collapse launch, pooled projectiles | warded at `OnHurtboxHit` | — |
   | Kit `ApplyStatusEffect` calls (roots, Macbeth's Venom, Critical Mass's burn, the Rift linger) | warded inside `ApplyStatusEffect` | — |
   | Displacement-only writes: the three pulls, the Rift Collapse pull, the Tempest windbox | can move a dormant member, with no damage, status or meter | left. The member's x-clamp keeps it in its area, and closed barriers stop the three pulls. The Rift Collapse pull is still a direct position write over 6 frames (not one of the plan's three); it can carry any body through a thin wall. Recorded as open |

3. **The Middle checkpoint sat on the room 2/3 camera seam** (medium).
   - Moving the anchor to 6500 resolves it. The respawned body (6480–6520) no longer overlaps the battlement room trigger (6380–6460), and the resume camera picks room 3 (from 6400), which contains it.
   - A new all-level content rule says no live checkpoint's respawn point may put the hero's 40 × 64 px body inside a room trigger whose `CameraBounds` do not enclose that body. It flagged only Level 2's old anchor (confirmed by the revert run below). **No other level needed a change.**
4. **The wall-aware pulls stalled on the target's own footing, and nothing tested the pulls** (low ×2).
   - New `BaseSpecial.PullThroughCollision(body, motion)`: a horizontal `MoveAndCollide`, then a vertical one. Walls and closed barriers still stop the horizontal half; the target's footing can only cancel the vertical half.
   - It is used by Einstein's singularity, Lorentz Attraction and the Sandstorm Vortex. The Vortex is horizontal only, so its effect is unchanged.
5. **A lock's simultaneous wake gave the first melee slot to tree order** (low).
   - `EnemyController.Engage` (`EnemyController.Encounter.cs`) now calls `StoryEngagementDirector.Join(this, hero)` once the member is in Chase.
   - So every member the lock wakes, and every engage-time wave body, is in the director before any member ticks. The first grant then goes by the plan §2.6 priority, which puts the elite first.
   - `Join` is idempotent. A lone mob's ordinary aggro path is untouched.
6. **A reverted (dormant) member kept its director role** (low). `SetEncounterDormant(true)` now calls `StoryEngagementDirector.Leave(this)`, so a dormant body holds no slot or wait role, and a re-engage queues it afresh by priority.

### Files

- `scripts/Environment/LevelManager.cs` (the checkpoint guard)
- `scripts/Environment/EncounterLock.cs` (`HeroStandsInside`)
- `scripts/Environment/Level02Controller.cs`
- `scripts/Combat/PlaceholderZone.cs`
- `scripts/Combat/BaseSpecial.cs` (`PullThroughCollision`)
- `scripts/Characters/Abilities/{EinsteinUltimate,TeslaAbilities,CleopatraAbilities}.cs`
- `scripts/Enemies/EnemyController.Encounter.cs` (`Join` at engage, `Leave` at dormancy)
- `scripts/Enemies/EnemyController.cs` (one line in `ProcessThrownFlight`)
- `localization/en.csv`, `en.en.translation`
- the tests below

**Merge seams:**
- FIX-F's files (`StoryEngagementRules`, `StoryEngagementDirector`, `EnemyController.Engagement.cs`) were not edited; this fix only calls the director's existing internal `Join` / `Leave`.
- `LevelManager.cs`, `BaseSpecial.cs` and `PlaceholderZone.cs` sit outside GU-E's original list. The guard belongs on the checkpoint itself, the pull helper on the kits' shared base, and the zone tick is a ward path.

### Tests: +11 (3109 → 3120)

New cases:
- `EncounterLockTests` +5:
  - `ACheckpointRefusesItsStrikeWhileALockItListsOrTheHeroStandsInIsUncleared`
  - `AZoneTickNeverDamagesOrCreditsADormantMemberButAnEngagedOneTakesIt`
  - `AThrownBodyNeverBowlsADormantMember`
  - `AnEliteListedAfterAStandardTakesTheFirstMeleeSlotWhenTheLockWakes`
  - `ARevertedMemberLeavesTheDirectorAndAReengageGrantsSlotsAfresh`
- New suite `StoryKitPullWallTests` (real frames) +3:
  - `TheSandstormVortexDrawsATargetInButNeverThroughABarrier`
  - `TheSingularityDrawsATargetOnAStepAboveItButNeverThroughABarrier`
  - `LorentzAttractionDrawsATargetOnAStepAboveTeslaButNeverThroughABarrier`
- `EncounterLockContentTests` +2:
  - `EveryCheckpointPastALockRefusesActivationUntilThatLockIsCleared`
  - `NoCheckpointRespawnsTheHeroInsideARoomTriggerWhoseCameraLeavesItOffScreen`
- `Level02ContentTests` +1:
  - `TheMiddleAnchorRefusesAStrikeUntilTheSiegeLineIsCleared` (the review's scenario on the real scene)

Rewritten in place, no count change:
- `Level02ContentTests.TheThreeApproachRoomsAreEncounterLocksOneWavePerManifestRow`: the battlement pin 6460 → 6580.
- `Level02ContentTests.TheMiddleAnchorStandsInTheCorridorPastGateAOutsideEveryLock`: adds the swing-reach clearance, computed from every roster profile and grid (`LongestBasicSwingPixels`).
- `EncounterLockTests`' `LockScene`:
  - `BuildLock` starts every member's physics.
  - `Dispose` restarts the physics of every enemy it hands back to the pool. The stall-watchdog case used to return a frozen pooled slasher, and the next case that drew it got a member that never ticked.
  - A new `BuildCheckpoint` helper.

**Every fix was checked against a revert.** All seven fixes were temporarily undone at once:
- the guard property forced false;
- the zone skip, the bowling skip, the join and the leave removed;
- the pull split back to one diagonal `MoveAndCollide`;
- Level 2 back to 6370 / 6460.

Exactly the 12 expected cases then failed, each for its own reason: the 11 new cases (minus the Vortex case, whose pull was already horizontal) plus the two rewritten Level 2 cases. The respawn rule flagged only Level 2's old anchor. Everything was then restored.

Runs, all holding the lock:
- the four touched suites: 49/49;
- 27 affected suites (locks, Level 2, baselines, the director, zones, checkpoints, the pull kits, the camera rig, wall bounce, rift collapse, `EnemyControllerTests` and others): 302/302;
- **the full suite: `Passed! - Failed: 0, Passed: 3120, Total: 3120` (1 m 34 s) = 3109 + 11.** The `[TestCase]` count under `tests/` is 3120.

### Changes to the Phase B authoring rules

The AUTHORING GUIDE above now marks each of these FIX-E:
- the checkpoint hold;
- registering baselines in standalone levels;
- swing-reach clearance for Middle and PreBoss anchors;
- the respawn and room-trigger camera rule;
- tests that activate a checkpoint past a lock;
- restarting a pooled body's physics.

Still open for Phase B:
- Moving platforms and position-writing kit moves (the Rift Collapse) are still not barrier-aware.
- **GU-BOT:** the `play` bot may strike a held checkpoint and get only refusals until its 900-frame task give-up. It should skip checkpoints while `IsHeldByEncounterLock`.

### For the closeout

- `PROVISIONAL-ENCOUNTER-LOCK-RULES` gains:
  - the checkpoint hold (both rules and the notice);
  - the dormant ward extended to zone ticks and crowd bowling;
  - the lock/director edges (`Join` at engage, `Leave` at dormancy).
- The Level 2 Middle-anchor fact is now **6500** (not 6370), and the battlement lock starts at 6580.
- AGENTS.md: the strike-to-activate checkpoint bullet gains the lock hold.
- Design-master line owed: the "Chronal Rift Checkpoints" section must say a fracture past an uncleared lock cannot be stabilized.
- Open, recorded above:
  - the Rift Collapse position write;
  - the Lincoln bounce stun on an airborne dormant member;
  - the record-time baseline filter.
