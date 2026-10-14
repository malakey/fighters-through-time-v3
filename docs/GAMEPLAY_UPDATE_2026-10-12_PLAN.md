# Gameplay update: single aerial strike, closer camera, area locks, −20% movement, static Story knockback, enemy turn-taking (2026-10-12)

**Status:** complete 2026-10-13 — Phases A, B and C merged on `main`, closeout report in §10. *(Planned 2026-10-12.)* Base commit: `2e9ca79` (the combat-flow pass, committed for this package) plus this plan's commit. Baseline **3021** passing tests.

**Source.** The user's six requests of 2026-10-12, verbatim:

1. "For aerial attacks, it should just be a standard attack and no longer chain the combo. It should be a single attack."
2. "The viewport should be closer to the character and enemies. Right now it's zoomed out too far, so it should be a closer view on the player."
3. "The combat in the game should function similar to Absolum and other beat-em-ups, where you enter rooms or areas that you have to clear of enemies. As you move through the levels, enemies either spawn in or are already there when you enter an area, and you have to fight and defeat all of the enemies in that area. You no longer have the option of jumping past them. The same thing could be done for puzzles in a room."
4. "I want to reduce the movement speed by about 20%, and the jump distance by the same. The jump height should remain the same."
5. "In story mode, the knockback of enemies should be static. It should not be dependent on enemy health. So any abilities that knock the enemy back and cause the added wall bounce should be the same no matter what their health is. The health effects of the knockback should only apply to fighting mode."
6. "Currently the pathing of enemies, when they are in range or in aggro, is that they all come at the player until they get to the player. I want the enemies to maintain more separation, especially on normal mode. They shouldn't just make a beeline for the character. They should wait their turn to come and attack the character. Right now it doesn't feel right. Something feels wrong about the way the pathing is functioning."

**The user's rulings on the scope questions (2026-10-12):**

| Question | Answer |
|---|---|
| Modes for requests 1 and 4 | **Both modes** (Story and Fighter) |
| Which areas lock | **Every area with enemies, plus puzzle rooms whose puzzle blocks the way forward**, across all levels |
| Attackers at once (request 6) | **Easy 1 / Normal 2 / Hard 3** |
| Uncommitted WIP | Committed to `main` first (`2e9ca79`), then parallel worktree agents |

The recon dossiers this plan was written from live outside the repository in `D:/Projects/absolum_research/update_2026-10-12/recon/` (aerial, camera, encounter-framework, levels-0-3 / 4-7 / 8-11 / 12-15, movement, knockback, enemy-ai, bots-docs-design, critic). Read the one for your workstream before you start: it has exact file and line references.

Every number below that the user did not state is **Provisional**: an agent's choice under the user's request, recorded at closeout in a `PROVISIONAL-*` ledger row so the user can reverse it.

---

## 1. Standing decisions (every workstream)

1. **Authority.** `design-godot.md` and the adopted contracts define intent (AGENTS.md's authority rule). This package changes the design in six places. Workstream agents **do not edit** `design-godot.md`, `AGENTS.md`, `CLAUDE.md`, `docs/design-contracts/*.md` or the ledger. The closeout writes the `USER-*` / `PROVISIONAL-*` ledger rows, the AGENTS.md / CLAUDE.md updates and the "design-master lines owed" list from the workstream handoffs (§7).
2. **Mode scope.** Requests 1 and 4 apply to **both modes**. Requests 2, 3, 5 and 6 are **Story only**: nothing under `scripts/FighterSim/` changes for them (comments excepted), and no Story-only constant may be named from `scripts/FighterSim/`.
3. **No save schema bump, no Klotho component ID, no protocol change.** The sim aerial strike uses a spare `AttackFlags` bit (128) on component 305. Bumping `StoryLevelControllerBase.ExplicitEncounterBaselineVersion` 2 → 3 is a checkpoint-record layout version, not a save schema, and is allowed (GU-E).
4. **One canonical value.** Every new number lives once: in a rulebook class (`BasicComboRules`, `UniversalMovementRules`, `StoryCameraRules`, the new `EncounterLockRules` and `StoryEngagementRules`) or a `.tres`. Pure rulebooks are engine-free and take difficulty as a parameter, never reading `GameManager` (CLAUDE.md failure signature 7).
5. **Localization.** Every visible string is a key in `localization/en.csv` under a `# GU-<id>` comment-marker block. Run `--headless --import` after editing `en.csv` and commit the regenerated `localization/en.en.translation`; the orchestrator regenerates it once more after each merge.
6. **Tests.** One `[TestSuite]` per file. New pure-C# suites carry no `[RequireGodotRuntime]` and touch no engine API. A suite that writes `GameManager.CurrentSession` snapshots and restores it in `finally`; a suite that sets `SceneTree.Paused` restores it; grouped nodes added to the root are freed in `finally`. Use the `OverrideFailureMessage` idiom from CLAUDE.md. Every agent reports its **exact** test delta (new cases, rewritten cases) and reconciles its full-suite `Total:` against `3021 + delta`.
7. **GdUnit and Godot launches are serialized across every checkout** (CLAUDE.md signature 5). Before **any** Godot launch (`dotnet test`, `--import`, `--quit`, a smoke run, a bot run): acquire the lock with `mkdir "D:/Projects/.ftt-gdunit-lock"`. Retry every 15–30 s while it exists; if it is older than 20 minutes **and** no `testhost` or `Godot*_console` process is running, remove it. Drain `testhost` processes first. Release with `rmdir` immediately after the run, even on failure. Never stop a non-console `Godot_v4.7.1-stable_mono_win64` process (the user's editor). Never accept a `Total:` without reconciling it.
8. **Worktree hygiene.** A fresh worktree has no `.godot/` cache. Before your first Godot launch you may copy the main checkout's cache to skip most of the re-import: `robocopy "D:\Projects\Fighters Through Time - V3\.godot" "<your worktree>\.godot" /E /XD mono` (`.godot/` is git-ignored; never commit it). Then run `--headless --import` once, holding the lock. Export `GODOT_BIN=D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe` explicitly. `git checkout -- assets` discards the `*.png.import` line-ending churn before you commit.
9. **Commit and report.** Commit your work on your worktree branch (message `GU-<id>: <summary>`, ending with the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` trailer). Do not merge into `main`. Write `docs/handoffs/GU_<id>.md` (format in §6) and commit it with your work. Report your branch name, final commit SHA and worktree path.
10. **Shared files.** Edit only the files and regions your workstream owns (§3). Where two workstreams own different regions of one file, keep edits localized and additive (new members, new partial files) so the merge is mechanical. If you must touch something you don't own, keep it minimal and list it in the handoff under "outside my ownership".
11. **Nobody plays the build.** Headless tests, smoke runs and bot runs are the evidence. Say so in the handoff.

---

## 2. Decisions per request

### 2.1 Request 1 — the single aerial strike (both modes) · workstream GU-A

- **Selection** (`BasicComboRules.SelectAttackVariant(up, down, airborne)`): Up → `VariantUpAttack`; Down and airborne → `VariantDownAir`; **airborne → `VariantAirAttack` (new, = 3)**; otherwise `VariantChain`. Up-Attack and Down-Air keep their frames, boxes and payloads. Grounded Down stays the chain.
- **It never chains.** A press during the strike is dropped (no buffer, no hold-to-continue), like the directional strikes. One strike per press; no per-airtime limit (*Provisional*).
- **No chain hit ever starts in the air, in either mode** (*Provisional*): a chain continuation (buffered, held or pressed in the chain hold) that would begin while the fighter is airborne ends the string instead. Story's walk-off-a-ledge case and the sim's `StartSwing` re-derivation both end there.
- **Numbers** (*Provisional*, named constants in `BasicComboRules`):

| Property | Value |
|---|---|
| Frames | the profile's aerial opener: `AerialStartupFrames[0]` / `AerialActiveFrames[0]` (7) / `AerialRecoveryFrames[0]` (13) — Joan 4/7/13, template 5/7/13, Einstein and Shakespeare 6/7/13, Lincoln 7/7/13 |
| Damage | `AirAttackDamageMultiplier = 1.0` × `BasicAttackDamage` (the two directional single strikes are 1.0×) |
| Hitstun | `AirAttackHitstunFrames = 30` |
| Knockback | string hit 1's: Story `(baseKB × KnockbackMultipliers[0], −1)`; sim `BasicKnockback × KnockbackMultipliers[0]` on the scalar path |
| Launch | **false** (no DI, tumble, D6a carry or D9 bounce) |
| Hitbox ID (Story) | `air_attack` (`BasicComboRules.AirAttackHitboxID`), added to `IsBasicStringHitbox` and to `MomentumRefundFramesForHitbox` (20). It is not `combo_1`, so no hit-1 block-cancel gate (matching the sim's single strikes) and the S5 stun floor is the default 24 |
| Story box | string hit 1's x-span from `ResolveGroundStringBoxPixels(0, …)` (reach %, AttackRange minor, front-only, facing at active start), and a **vertical span that reaches below the feet**: centre `AirAttackBoxCentreYUnits = −12/60`, height `AirAttackBoxHeightUnits = 104/60` (the retired aerial hit-3 span), so a jump-in connects on a grounded foe |
| Sim box | `ResolveStringBox(0)` (the sim's aerial string already used the grounded boxes) |
| Sim flag | `FlagAirAttack = 128`, in `VariantMask` |
| Pose | `basic_attack_1` |

- **Retire the aerial string's dead data**: the per-hit `AerialStringBoxCentreYUnits` / `AerialStringBoxHeightUnits` arrays and `ResolveAerialStringBoxPixels(step…)` give way to the air-attack constants and a single resolver. The profile's aerial hit-2/hit-3 frame entries become unread; remove them only if `BasicStringProfile` stays simple to construct, otherwise document them as retired. No dead reads.
- Everything chain-only stays chain-only: `NextChainStep` / `NextChainStepForPress` return 0 for the new variant, so the CPU melee band, the Mirror projection and the bots see "the next press is an opener". Telemetry finisher records (keyed on `combo_3`) no longer arise in the air.
- Joan's Wing-Dive cancel now yields the air strike. Wings Refresh and Zealous Vigor keep triggering on the grounded finisher only (record it).
- Move List: replace the `movelist_aerial_startups` row with an air-attack row (new key; delete the old key from `en.csv`).

### 2.2 Request 2 — the closer Story camera (Story only) · workstream GU-B

- `StoryCameraRules.ViewZoom = 1.5f` (*Provisional*, user: "closer"), applied **inside `StoryCameraConfiner`** before the first `SetBounds` / `SnapToTarget`, so the hub, Level 0, Florence and Levels 2–15 all get it, and `FighterCamera`, the Test Arena and the Holodeck drills cannot. Add `StoryCameraRules.ViewWorldSize` (1920/zoom × 1080/zoom) for tests and content rules. At 1.5 the view is 1280 × 720 world px (21.3 × 12 units) and the idle hero is about 14% of screen height.
- Dead and soft zones stay as view fractions; `MaxLookAheadPixels`, `FollowOffsetPixels` and `SnapDistancePixels` stay in world pixels.
- **Boss framing** (*Provisional*): `StoryCameraConfiner.RequestFraming(object owner, Rect2 area, float minZoom)` / `ReleaseFraming(object owner)`. While a request is live, the target zoom is `clamp(1920 / (area.Size.X + 2 × FramingMarginPixels), minZoom, ViewZoom)`, eased with `ZoomDampingSeconds`. `BossEncounterController` requests its `ArenaBounds` with `BossFramingMinZoom = 1.0` on the reveal and releases it on defeat (and on teardown); the Level 13 Mirror encounter does the same with its arena. Constants: `FramingMarginPixels = 120`, `ZoomDampingSeconds = 0.6`, `BossFramingMinZoom = 1.0`. Encounter locks (§2.3) do **not** request framing; they keep `ViewZoom`.
- **Shake** keeps today's on-screen amplitude: `CameraShake` divides the `Offset` it writes by the camera's current zoom when the camera is a `StoryCameraConfiner` (Fighter shake unchanged; `_shakeIntensity` stays in world pixels so the reflection test holds).
- **Level 0's Eraser watcher** is placed relative to the visible view (for example the view's left edge + 160 px, on the floor), so it is on screen at 1.5.
- Not fixed, recorded: room-name labels at world y 30 are off-screen; high optional extractors (L2 8560/260, L4 5700/228, L6 4980/280) are hidden from the floor; L3's routing hall and the L4/L9 searchlight heads are no longer one screen.
- **GU-E owns** the lock-bounds override layer in the same class (`SetLockBounds` / `ClearLockBounds`, §2.3). GU-B owns `Zoom`, `ViewSize`, framing and shake. Keep your edits to your own members.

### 2.3 Request 3 — encounter locks (Story only) · workstreams GU-E (framework + Level 2) and the four Phase B level workstreams

**Vocabulary.** An **encounter lock** ("lock") is an authored `Area` (a `Rect2`) the hero must clear. Do not call it a "seal": `SealReadiness` / `AcceptSeal` already mean N01.

**States.** `Armed` (built; exit barrier closed; members dormant) → `Engaged` (hero inside; both barriers closed; members active; waves spawning) → `Cleared` (latched; both barriers open). An `Engaged` lock whose hero ends a physics tick outside its `Area` (Echo Step, a death-rewind landing, a rift snap, a scripted move) **reverts to `Armed`**: the entry opens, the exit stays closed, members keep their HP, go dormant and stay clamped, already-spawned waves stay spawned, and the remaining waves spawn on the next engage.

**Engage** on the lock's own `_PhysicsProcess` (not a one-shot trigger) when all hold: the hero's body is at least its half-width + `EngageInsetPixels` (24) inside `Area` horizontally and inside it vertically; no Time Freeze; `!ChronalRewindManager.IsWorldHeld(tree)`; no gameplay-pausing dialogue.

**Barriers.** A new `EncounterLockBarrier : StaticBody2D`: Environment layer, mask 0, one rect spanning an authored y-range (default: from `StoryGateRules.ColumnTopY` down to the floor), so it needs no gate column and cannot be jumped. **Pre-built at level build with its shape disabled** and toggled with `SetShapeDisabledSafe` (flush-safe, and `PlaytestNav` notices a re-enabled shape). Group `story_encounter_barrier`; **not** `StoryGateRules.DoorGroup` and not a `ForcefieldBarrier`, so the gate-column sweeps and counts in `ArenaGeometryContentTests` do not move. Placeholder look: a cold, jagged energy curtain in `UIPalette.UnboundCold` (the Severed's "kind of door"), with a non-colour channel (pattern or edge glyphs). The **exit barrier is closed from `Armed`** (level build), so no area can be skipped even before it engages; the entry closes on `Engaged`; both open on `Cleared`. If the hero overlaps a closing barrier, push it to the `Area` side.

**Camera.** `StoryCameraConfiner.SetLockBounds(Rect2)` / `ClearLockBounds()`: while a lock rect is set, room `SetBounds` calls only update the stored room rect (so Level 3's and Level 7's repeatable room-trigger pairs cannot overwrite it), and `OnRewindLanded` / `ApplyResumeCameraBounds` defer to it. The camera eases into and out of a lock rect (no snap). A lock's `CameraBounds` = its `Area` expanded symmetrically to at least `StoryCameraRules.ViewWorldSize` and clipped to the level bounds.

**Members.** Pre-placed members spawn at level build (or level load) **dormant**; wave members spawn on engage, or by wave trigger `OnEngage | WhenAliveAtMost(n) | AfterPreviousCleared`. Membership is registered at the spawn call, never inferred from the node parent. Per member, on `EnemyController` (GU-E adds them in a new partial `EnemyController.Encounter.cs`):
- `IsEncounterDormant`: no aggro, chase or attack; idle facing the entry. **A dormant member refuses player hits** exactly like the boss arena ward (no damage, knockback, status, mark, hitstop, meter or Rally; grey "0"), so a future area cannot be emptied from outside or through a barrier.
- `EncounterBounds`: an x-clamp into `Area` after every move (all states, dormant included), and a **fall net**: feet more than 240 px below `Area`'s bottom → back to the member's authored spawn point through `SafeRelocation`, velocity zeroed.
- `Engage(PlayerController)`: wakes into Chase on the hero.
- a per-instance `event Action<EnemyController> Died` raised in `Die()`, and `SpawnGeneration` incremented in `OnSpawn` (a pool-recycled body whose generation changed counts as removed, logged).
- all reset in `OnSpawn`, `OnDespawn` and the non-preserve `ApplyStoryRewind` branch.
- Never members: summons (`IsSummoned`), `DrillInvulnerable` bodies, `ArenaGuardian`s, `BossDecoy`s, bosses, the Mirror clone.

**Clear.** All spawned members dead or removed, every wave spawned, every `RequiredPuzzleID` `IsSolvedOrPersisted`, every required breakable destroyed. Easy's smaller prefix is counted from what actually spawned. Cleared **latches** (a later puzzle reset never re-closes it). On clear: open both barriers, `ClearLockBounds`, restore the level objective through a base-class hook, and raise `EventBus.OnEncounterLockChanged`.

**HUD.** `EventBus.OnEncounterLockChanged(EncounterLockPayload { LockID, State, Remaining, Total, GoDirection })`. `StoryHUD` shows "Enemies left: N" while a lock is engaged (new key) and a **GO →** prompt for about 3 s after a clear (new key, an arrow glyph plus text, never colour alone). Authored node in `StoryHUD.tscn`; theme roles, no font-size overrides. The `HUD_CONTRACT` amendment is a closeout ledger item.

**Persistence (F10, baseline model).** A lock ID is an F10 encounter ID. Every lock ID is listed in the baseline of **every** checkpoint after it (`EncounterBaselineMap`), and `ExplicitEncounterBaselineVersion` goes 2 → 3 so stale records fall back to the authored baseline. A lock in the resumed baseline is built `Cleared` (no members, barriers open). Anything else re-arms in full on any reload (claimed dust sources pay 0). This replaces today's two resume models (wave 1 respawning on resume in L2, L4, L6, L8–L11 versus not in L3, L5, L7, L12–15) with one.

**Content rules** (enforced by GU-E's data-driven `EncounterLockContentTests` over every campaign scene):
- No checkpoint, Restoration Font or sealing anchor inside any `Area`. A Reset Puzzle station may sit inside a **puzzle** lock's `Area`.
- No member spawn inside a boss arena rect; no `Area` overlaps a boss arena.
- Every member spawn stands on a floor inside its `Area`, reachable by the slowest hero after request 4 (no posted-on-a-ledge-only mobs).
- `Area` width is at least `ViewWorldSize.X` (1280 px at 1.5) and at most about 2600 px (*Provisional*). Adjacent or overlapping areas are allowed only if their barriers do not conflict.
- Each lock wave's enemy-ID multiset equals its manifest `RequiredWaves` row (`RowID` = the manifest row ID; per-row `ScaleEncounterCount` prefix as today). Per-level dust totals stay fixed (`DustEconomyTests`).
- Every barrier is a `StaticBody2D` (not `AnimatableBody2D`), with floor under its x.

**Puzzle rooms.** A lock may list `RequiredPuzzleIDs` and required breakables. Where a puzzle door already blocks the way forward, the lock's exit barrier sits at the door (the door still opens on solve; the barrier opens on clear, which requires the solve). A room whose puzzle blocks progress but holds no enemies is a lock with zero members that clears on the solve.

**Engine interplay (GU-E).**
- The lock joins `time_freezable` (`IStoryTimeFreezable`, a pure latch): no engage, wave advance or watchdog tick while frozen or world-held. Wave spawn-ins call `TimeFreezeController.FreezeActivatedRoom(GetTree())` after spawning. Fix `RoomTransitionTrigger`'s order defect (the `onEntered` wave spawns run after `FreezeActivatedRoom`).
- Stall watchdog (*Provisional*): a member engaged for `StallFrames` (600) that is off any standable surface inside `Area`, or has not moved while not waiting its turn and the hero cannot reach it, is relocated to its spawn anchor (`SafeRelocation`). Never auto-kill. A lock engaged for over `HardCapSeconds` (300) with no living registered member force-clears and logs.
- Kit pulls that write positions directly — Cleopatra's Vortex pull, Einstein's Ultimate singularity pull, Tesla's Lorentz Attraction — become wall-aware (`MoveAndCollide` or an `EnvironmentProbe` clip, the Tempest pattern), so they cannot drag a body through a barrier.
- Moving platforms whose path crosses a barrier: the framework exposes a query (for example `EncounterLock.IsHolding` / an event) so a level can hold a mover at its near stop while its lock is `Armed` or `Engaged`; the level workstreams wire it.
- **Test seam:** `EncounterLock.SuppressForTesting` (internal static, default false): when set, locks build `Cleared`. Existing real-scene suites that walk the hero through a level and don't test locks set it in `try`/`finally`.
- **Integrity par** (*Provisional*): locks, slower movement and turn-taking lengthen every run. Until par is re-measured (`VERIFY-PAR-SECONDS`), one named constant `TimelineIntegrityRules.ProvisionalParScale = 1.5` scales the par handed to `StoryManager.BeginIntegrityClock` (the single consumption point, `StoryManager.cs` ~:591), F11 route fractions included. Florence and the Tutorial stay untimed.
- Every lock node joins group `story_encounter_lock`; `StoryLevelControllerBase.EncounterLocks` (read-only list) is the seam for tests and bots.

**Boss arenas** keep `BossEncounterController` unchanged in this package (no hero seal); the camera frames them (§2.2).

### 2.4 Request 4 — movement −20%, jump distance −20%, jump height unchanged (both modes) · workstream GU-C

- The nine `resources/Characters/*_data.tres` `MaxMoveSpeed` × 0.8 exactly: cleopatra 5.8, einstein 5.4, joan 6.2, leonardo 5.2, lincoln 3.8, mozart 5.4, shakespeare 5.2, tesla 4.8, tubman 5.4. `MaxJumpForce`, `AirControlMultiplier` and `MaxJumpCount` unchanged. In both modes horizontal air speed is capped by run speed, so jump distance falls exactly 20% and height is unchanged.
- **The roll keeps its distance** (*Provisional*): `UniversalMovementRules.RollSpeedMultiplier` 1.5 → **1.875** (both modes), so roll travel and the roll get-up are unchanged and Lincoln keeps his roll-through (Story) and cross-up (sim).
- `FighterLoadout.Default.MoveSpeed` stays 7 (the hand-built test loadout).
- Absolute-speed kit motions (Lightning Blink, North Star Leap, Rail Charge, sand rush, the warp, the gust, the Wing-Dive, the glissando, Leonardo's 380 px/s glide) are unchanged; record them.
- `Level14Controller.ReferenceWalkSpeed` 285 → **228** (and its comment).
- Escape fronts × 0.8: `Level_06_Pompeii.tscn` lava 280 → **224**; `Level_15_Alexandria.tscn` firestorm 270 → **216**.
- `TubmanContentTests` pins 5.4.
- Audit the movement-sensitive spots at ×0.8 with `StoryAirPhysics` for the slowest hero (Lincoln, run 228 px/s; single jump ~201 px, double ~350 px): Level 10's 220 px trapdoor gaps, Level 7's rope hops and skiff crossings, Level 12/13 low-gravity crossings, Level 0's mobility gates. Fix only what breaks, with the smallest geometry change, and record it.
- Run the Fighter suites that walk real arcs: `FighterStageGeometryTests`, `CpuRecoveryMatrixTests`, `FighterKnockbackScalingTests`, `RollbackReadinessTests`, `FighterCpuBehaviorTests`, `FighterMovementFeelTests`, plus `Level07ContentTests`, `Level14ContentTests`, `Level06ContentTests`, `Level15ContentTests`, `StoryMovementFeelTests`, `ResonanceLiveApplyTests`. Every Fighter hash moves; no suite pins one.

### 2.5 Request 5 — static Story knockback (Story only) · workstream GU-D

- Delete `DamageCalculator`'s victim-HP overload. Every Story victim takes `CalculateKnockback(base, weight, facing)`: the hero and the Mirror clone (`PlayerController.ResolveIncomingHit`), the Story throw launch (`ResolveStoryThrow`), mobs and elites (`EnemyController.TryApplyKnockback`, which crowd bowling also uses). Bosses were already static. The weight term and the attacker-side Resonance knockback lane stay.
- **D6a fix in the same change:** a finisher flight uses its carry deceleration alone (`EnemyController` ~:1656: `if (_finisherFlight && _finisherCarryDeceleration > 0f) decel = _finisherCarryDeceleration;`), so Cleopatra's slower static launch still lands one step out. Update the comment at `StoryEnemyCombatRules` ~:655–663.
- `BasicComboRules.LowHealthKnockbackScale` stays as the float statement of the sim's `ScaleByMissingHP`, documented **Fighter-only**. The sim (`FighterEntitySystems.ApplyFighterHit`) is untouched except its comment; no Fighter hash moves.
- Tests: rewrite `StoryCombatRulesTests.StoryKnockbackScalesWithTheVictimsMissingHPAfterTheHit` in place as a static-knockback pin; add "an identical finisher on a mob at full HP and at 3 HP writes an identical velocity"; add a source scan that nothing outside `scripts/FighterSim/` and `BasicComboRules` reads `LowHealthKnockbackScale`; re-run and re-measure `StoryFinisherCarryTests` and `StoryWallBounceTests` (report the new carry and bounce numbers for the ledger).

### 2.6 Request 6 — the engagement director (Story only) · workstream GU-F

A Story-only, per-target **engagement director**: only a few mobs may commit to melee on the hero at once; the rest hold at spaced positions on both sides and rotate in.

- **Gate decisions, not actions.** Gate only the AI's commit points: `ProcessChase`'s commit and `TryEngageBlink`. Never gate `BeginAttack`, `CommitArmoredAnswer`, `TryPressureExitAttack` or `TryReactivePhaseSkip` (retaliation stays V7.4-legal and only fires in reach). Scripted callers and the Punish suite are unaffected.
- **A lone mob behaves exactly as today**: it takes a slot on its first Chase tick, and a holder keeps its slot through its cooldown while nobody eligible waits. Every existing lone-mob test stays green unchanged.
- **Table** (`StoryEngagementRules`, pure C#, difficulty as a parameter; *Provisional* except the user's slot totals):

| Rule | Easy | Normal | Hard |
|---|---|---|---|
| Melee slots, total (**user**) | 1 | 2 | 3 |
| Melee slots per side | 1 | 1 | 2 |
| Ranged permits (concurrent ranged attacks) | 1 | 2 | 3 |
| Wait-ring rank-0 gap, pivot to pivot (px) | 200 | 170 | 150 |
| Rest after a handoff (own ticks) | 90 | 45 | 20 |

  Shared: rank spacing 64 px; ring tolerance ±12 px; stale slot after 120 frames (a holder that cannot reach or commit while an eligible waiter exists); elites first; a boss never holds a slot, its summons do; the ring stays above 125 px (the reactive Phase Skip radius), above the farthest melee commit (77 px) and above the hero's hit-1 reach.
- **Classes** from `PreviewNextAttack()`: melee slot = MeleeStrike, ChargeDash, AreaPulse, the standard engage blink; ranged permit = Projectile, Shockwave, PersistentFieldAtTarget, SiphonTether (stateless: counted from live members in Attacking with a ranged-class ability); free = ShieldBubble, SummonMinions, the reactive Phase Skip.
- **Priority:** elite before standard → rest elapsed → the side with fewer holders → waited frames (round-robin) → gap in 32 px buckets → instance id.
- **Waiters** move to `aim.X − side × WaitGapForRank(rank)` on their current side (side rebalanced toward the emptier side when one side is crowded or blocked by a wall or barrier), stopping at ledges and walls in either direction. Waiter speeds are capped **absolutely**: approach `min(mobSpeed × 0.8, 0.9 × hero run speed)`, back-off `min(mobSpeed × 0.6, 0.9 × hero run speed)`, the hero's speed read at runtime. Flyers keep their vertical clamp.
- **Ranged hold cap:** a projectile mob holds no farther than `RangedHoldMaxPixels = 560` (the 1.5-zoom half-view less 80), so shooters stay on screen.
- **Handoff:** at the end of `ProcessAttacking`, a holder hands its slot to an eligible waiter (`ShouldHandOff`) and becomes a waiter with its rest counter. Kept on stun, grab, thrown flight and freezes; handed off at finisher touchdown only if a waiter is eligible.
- **Registry:** a static `Dictionary<ulong targetId, List<ulong>>` of instance ids resolved with `InstanceFromId`, pruned on every access (invalid, queued for deletion, out of tree, pool-parked, physics-processing off, Dead/Patrol/Returning, other target). Slot counts are recomputed from live members' own flags, so a missed `Leave` cannot leak a slot. No clock of the director's own; counters live on the mob and tick only in its unfrozen ticks; ties break on instance id. `internal static ResetForTest()`.
- **Targets:** only the campaign hero. `FindNearestPlayer` in `EnemyController` and `BossController` skips `IsStoryHostile`, so no mob ever targets the Mirror clone. Dormant lock members never join.
- Code: `scripts/Combat/StoryEngagementRules.cs`, `scripts/Enemies/StoryEngagementDirector.cs`, `scripts/Enemies/EnemyController.Engagement.cs` (new partial) plus minimal hooks in `EnemyController.cs`. Mob↔mob pushbox jostling is **deferred** (record it).

---

## 3. Phases, workstreams and file ownership

**Phase A** (parallel worktrees, from the plan commit): GU-A, GU-B, GU-C, GU-D, GU-E, GU-F. The orchestrator merges them in the order **D → C → A → B → F → E**, regenerates the translation, runs the full suite, and runs an adversarial review round with fixes.

**Phase B** (parallel worktrees, after Phase A is merged): GU-L0 (Levels 0, 1, 3), GU-L4 (Levels 4–7), GU-L8 (Levels 8–11), GU-L12 (Levels 12–15), GU-BOT (bots and telemetry). Level 2 is converted by GU-E in Phase A as the worked example.

**Phase C** (serial, main checkout): integration, three green full runs, headless import and smoke runs, a bot batch, the par-scale review, and the documentation closeout.

### 3.1 Phase A ownership

| Workstream | Owns (may edit) | Must not edit |
|---|---|---|
| **GU-A** aerial | `BasicComboRules.cs` (variant selection, aerial constants and docs); `PlayerController.cs` (attack selection, `StartComboHit`, `StartDirectionalAttack`, `OnAttackActiveStarted`, the aerial box resolver, chain continuation, hold); `FighterSimulationSystems.cs` (basic-attack rules only); `FighterSimulationComponents.cs` (the `AttackFlags` doc); `FighterCpuController.cs` and `MirrorParadoxController.cs` only if needed; `MoveListScreen.cs`; `en.csv` block `# GU-A`; comment-only edits in `JoanAbilities.cs`, `FighterKitMotion*.cs`, `scripts/Diagnostics/*`; the aerial tests | knockback lines, camera, enemies |
| **GU-B** camera | `StoryCameraRules.cs`; `StoryCameraConfiner.cs` (`Zoom`, `ViewSize`, framing, follow); `CameraShake.cs`; `BossEncounterController.cs` and the Level 13 Mirror encounter (framing calls only); `Level00Controller.cs` (watcher placement only); camera tests | the confiner's bounds/override logic (GU-E) |
| **GU-C** speed | the nine character `.tres`; `UniversalMovementRules.cs` (`RollSpeedMultiplier`); `Level14Controller.cs` (`ReferenceWalkSpeed`); the two escape `.tscn` `AdvanceSpeed` values; minimal geometry fixes the audit proves necessary; tests whose pinned speeds move | combat rules, enemies, camera |
| **GU-D** knockback | `DamageCalculator.cs`; `PlayerController.cs` (the knockback lines in `ResolveIncomingHit` and `ResolveStoryThrow` only); `EnemyController.cs` (`TryApplyKnockback`, the finisher-flight deceleration line, their comments); comment-only edits in `BasicComboRules.cs`, `FighterEntitySystems.cs`, `StoryEnemyCombatRules.cs`; knockback tests | anything else in those files |
| **GU-E** locks | new `EncounterLock*.cs`, `EncounterLockRules.cs`, `EncounterLockBarrier.cs`; new `EnemyController.Encounter.cs` + minimal hooks in `EnemyController.cs` (`OnSpawn`, `OnDespawn`, `Die`, hit intake for the dormant ward, the move clamp); `StoryLevelControllerBase*.cs`; `StoryCameraConfiner.cs` (the lock-bounds override layer only); `RoomTransitionTrigger.cs`; `EventBus.cs`; `StoryHUD.cs` / `StoryHUD.tscn`; `StoryManager.cs` (par scale); `TimelineIntegrityRules.cs`; the three pull kits; `Level02Controller.cs`, its scene, `level_02_rewards.tres` and `Level02ContentTests`; `EncounterBaselineTests` / `EncounterCompositionTests` / `RewardManifestTests` rows for Level 2; `en.csv` block `# GU-E`; new lock suites | `ProcessChase` decision logic (GU-F), knockback (GU-D) |
| **GU-F** director | new `StoryEngagementRules.cs`, `StoryEngagementDirector.cs`, `EnemyController.Engagement.cs`; minimal hooks in `EnemyController.cs` (`ProcessChase` commit gate, `ProcessAttacking` end, `StartReturning`, `Die`, `OnSpawn`, `OnDespawn`, `ApplyStoryRewind`, `_ExitTree`, `FindNearestPlayer`); `BossController.cs` (`FindNearestPlayer` only); director tests | lock membership (GU-E), knockback (GU-D) |

`EnemyController.cs` is shared by D, E and F; `PlayerController.cs` by A and D; `StoryCameraConfiner.cs` by B and E; `BasicComboRules.cs` by A and D (D: doc only). Put new code in new partial files or new members wherever you can.

### 3.2 Phase B ownership

| Workstream | Owns |
|---|---|
| **GU-L0** | `Level00Controller.cs`, `Level01Controller.cs`, `Level03Controller.cs`, their scenes, `level_00/01/03_rewards.tres`, their content tests (`Level00*`, `Level01*`, `Level03*`, `TutorialCalibrationContentTests`) |
| **GU-L4** | Levels 4, 5, 6, 7 (controllers, scenes, manifests, content tests, `EraserDebutTrigger` placement data) |
| **GU-L8** | Levels 8, 9, 10, 11 (controllers, scenes, manifests, content tests, `NearCaptureBeat` placement data) |
| **GU-L12** | Levels 12, 13, 14, 15 (controllers, scenes, manifests, content tests) |
| **GU-BOT** | `scripts/Diagnostics/*`, `tools/playtest/*`, `PlaytestCombatStatsTests`, `PlaytestBotStrikeRangeTests` |

Shared test files (`EncounterBaselineTests`, `EncounterCompositionTests`, `RewardManifestTests`, `DustEconomyTests`, `SecretCachePlacementTests`, `StoryHealingLoopTests`, `ArenaGeometryContentTests`, `EncounterLockContentTests`, `PuzzleResetPlacementTests`, `EraserDebutTriggerTests`, `NearCaptureBeatTests`, `CaptainSmithTakingTests`, `PostBossChainTests`, `LevelDeathRewindDiagnosticTests`, `Level10BossRelocationTests`, `BossArenaFallNetTests`): each level workstream edits **only its own levels' rows and cases**, additively. Framework files (`EncounterLock*.cs`, `StoryLevelControllerBase*.cs`, `EnemyController*.cs`) are **read-only in Phase B**; a framework bug goes in the handoff with a minimal repro, and the orchestrator fixes it.

---

## 4. Phase A workstream briefs

Each brief adds to §2. Read your recon dossier first.

- **GU-A (aerial)** — dossier `aerial.md`. Implement §2.1 in both modes. Tests that break and how to fix them are listed in the dossier's §(d) (≈10 Story cases: the FeelPass chain cases move to a grounded harness; `StoryAerialStringReachTests` is rewritten for the single box; `StoryCombatRulesTests.AerialComboUsesIndependentState…`, `DirectionalAttacksRouteToTheirOwnSingleStrikeOutsideTheChain` (air press → `air_attack`), `StorySpecialRecoveryCancelTests` line ~337, `MirrorParadoxTests` ~:365). New: the selection row in `DirectionalAttackTests`; a Story strike that never buffers, holds or continues and leaves to `Airborne`; its payload, box and Momentum; the walk-off-a-ledge string end (both modes); the sim flag, `NextChainStep == 0`, damage and hitstun; a `CpuMeleeBandTests` row; `AbilityLockoutRulesTests` Momentum for `air_attack`.
- **GU-B (camera)** — dossier `camera.md`. Implement §2.2. Extend `StoryCameraRigTests` (`ViewZoom` in the design-table pin; zoom on the campaign camera, and on the hub, Level 0 and Florence cameras; framing request eases toward the fit zoom, release eases back, two owners; shake compensation keeps `_shakeIntensity` in world px and divides the written offset). A headless smoke run of one campaign level and the hub must stay clean.
- **GU-C (speed)** — dossier `movement.md`. Implement §2.4.
- **GU-D (knockback)** — dossier `knockback.md`. Implement §2.5.
- **GU-E (locks)** — dossiers `encounter-framework.md`, `levels-0-3.md` (Level 2), `critic.md` §3. Implement §2.3: the framework, the camera lock layer, the HUD widget, the persistence model, the engine interplay, the par scale, the three pull kits, and **Level 2 as the worked example** (locks for its three rooms per `levels-0-3.md`: 300–3200 · 3200–6300 with tower A · 6400–8700 with the floor fight before the climb; checkpoints and the font outside every area; manifest rows matched). New suites: `EncounterLockRulesTests` (pure C#: engage eligibility, the clear predicate, wave advance, revert-to-Armed, camera-bounds expansion), `EncounterLockTests` (real frames: engage, barriers, clear, revert on a rewind landing outside, Time Freeze holds, dormant ward, pool recycle, fall net, x-clamp, a phaser clamped, the HUD payload, the test seam), and `EncounterLockContentTests` (data-driven over every campaign scene: the §2.3 content rules; passes trivially for levels with no locks yet). Write a short authoring guide for Phase B into your handoff: the exact API, a Level 2 excerpt, and the pitfalls you hit.
- **GU-F (director)** — dossier `enemy-ai.md`. Implement §2.6. New: `StoryEngagementRulesTests` (pure C#, ~8–10 cases) and `StoryEngagementDirectorTests` (real frames through `OutOfBandPhysicsStep`: three slashers on one side commit one at a time with waiters ≥ 64 px apart; both sides on Normal; handoff round-robin; death / `OnDespawn` / `Free()` free the slot; a lone mob is frame-identical to a control; elite priority; ranged permits; no mob targets the Mirror clone; Time Freeze leaves roles untouched; two identical runs are identical; Easy/Normal/Hard counts, restoring `CurrentSession` in `finally`), and a source scan that `scripts/FighterSim/` never names `StoryEngagement`.

## 5. Phase B: level rules and per-level rulings

All of §2.3's content rules apply. Lock-area proposals per level are in the four `levels-*.md` dossiers and in `critic.md` §1 and §3; treat them as a starting point, not a mandate. Re-authoring is allowed: move spawns, split or merge manifest rows (keeping per-level totals), move checkpoints and fonts out of areas (keeping their placement tests' intent), add walls, and add or move geometry. Record every move.

Per-level rulings (*Provisional*):
- **Level 0:** the hold stays scripted; the Combat Trial becomes one lock over the bay (roughly x 2400–4780) with its two waves, replacing the global kill counter; the Time Freeze drill stays exempt (its point is passing an unkillable enemy); the grab-lesson drone is released after its lesson.
- **Level 1 (Florence):** no base class, so wire standalone `EncounterLock` nodes; add the missing west wall at x 0; move `florence_checkpoint_0` out of the fighting.
- **Level 5:** hull flood steps advance on lock clears, not on x triggers or the 14 s timer starting inside a lock; the step-4 timer starts after area 3 clears. The Eraser debut stays scripted; the debut Eraser is a member of its area's lock (or its own lock), never paying dust (`@0` row unchanged).
- **Level 6:** the Ash Road enemies are fought in lock(s) **before** the lava front; the front starts when the last road lock clears.
- **Level 7:** re-lay areas at least 1280 px wide; the Eraser moves onto solid deck; the sea haul-out never lands the hero beyond an uncleared lock's exit; skiffs and rope grabs that cross a barrier are held while their lock is `Armed`/`Engaged`.
- **Level 9:** the Lantern Watch area clears on "relays cut **and** enemies cleared"; the lanterns stay live; the near-capture beat fires before the area-C lock engages; close the partial wall below y 800 into room 4 with the lock barrier.
- **Level 10:** the audience's stillness timer pauses while a lock is engaged (turn-taking makes standing still normal); move the upper-tier slasher onto the route.
- **Level 11:** artillery stays live; spawns that overlap cover move clear of it.
- **Level 12:** move the dead-end R3 floor enemies onto the route or hold the spire lift until clear; move the Eraser west of the arena (9800).
- **Level 13:** lower the high phantoms onto the shelf; replace the y-limited pre-arena trigger with a full-height lock; move both Erasers onto the shelf, out of the Mirror arena; rift snaps are handled by revert-to-Armed.
- **Level 14:** keep lock areas out of cycling laser corridors, or pause a corridor's grid while its lock is engaged; move the drone at 10450 out of the arena.
- **Level 15:** the hall's lock(s) clear **before** the firestorm, which starts on the last clear; re-author wave 4 out of the boss arena (the 350 px apron is too narrow for a lock).
- **Everywhere:** fix the soft-lock content the recon found (members that fall, never aggro, or stand on unreachable posts). The fall net is a backstop, not a fix.

Each level workstream: run its level suites, the shared suites listed in §3.2, `EncounterLockContentTests`, and one full suite; run a headless smoke of each level scene; and, holding the lock, run the `play` bot once per level (Joan, Normal) to check each level can still be completed (record the end reason).

**GU-BOT:** read the active lock and its remaining members through `StoryLevelControllerBase.EncounterLocks` / group `story_encounter_lock`; give engaged-lock members boss-like targeting (no 620 px cap inside the lock, no 720-frame give-up) and report a truly unreachable member as a nav note and `end_reason` `stuck_lock:<id>`; redefine `bypass` as "fights only what a lock or boss requires" and update `run_playtests.ps1`'s help text; teach telemetry `air_attack`; add a `mob_engagement` block (max and mean concurrent `Attacking` mobs within 250 px, slot handoffs, median waiter gap, ranged permits in use) and a `locks` block (per lock: engage frame, clear frame, members, relocations); include `TimelineIntegrityRules.ProvisionalParScale` in the auto `--max-seconds`; extend `compare_runs.py` and `trace.py`; tests in `PlaytestCombatStatsTests`.

## 6. Handoff format (`docs/handoffs/GU_<id>.md`)

1. **Shipped** — what changed, with exact API names, constants and their values.
2. **Files changed** — including any outside your ownership, with the reason.
3. **Decisions** — every *Provisional* number or behaviour you chose, with one line of reasoning (these become ledger rows).
4. **Deviations from this plan** — what you did differently and why.
5. **Tests** — new suites and cases (+N), rewritten cases (name before → after), and the exact suites you ran with their totals. Your final full-suite `Total:` and the reconciliation against `3021 + delta`.
6. **Measurements** — anything the ledger needs (for example D6a carry and D9 bounce numbers after GU-D, roll distances after GU-C).
7. **Not delivered / open** — honest list.
8. **For the closeout** — AGENTS.md bullets to change, design-master lines owed (cite `design-godot.md` line numbers from the recon), ledger rows to write or amend.

## 7. Phase C closeout (orchestrator)

1. Merge Phase B (L0, L4, L8, L12, BOT), regenerate `en.en.translation`, headless import, full suite.
2. Adversarial review of the package diff; fix confirmed defects.
3. Three consecutive green full runs with the lock held and orphans drained; reconcile `Total:` exactly.
4. Headless smoke runs of the hub, Level 0, Florence and two campaign levels.
5. Bot batch (all campaign levels, `play`, Easy/Normal/Hard, two heroes, seeds 0–1): completion, deaths, run time, `mob_engagement`, `locks`. Review `ProvisionalParScale` against measured run times and record the result under `VERIFY-PAR-SECONDS`.
6. Docs: the ledger section "Confirmed open deviations — gameplay update, 2026-10-12" with `USER-AERIAL-SINGLE-STRIKE`, `USER-STORY-CAMERA-ZOOM`, `USER-ENCOUNTER-LOCKS`, `USER-MOVEMENT-SPEED-CUT`, `USER-STORY-STATIC-KNOCKBACK`, `USER-ENEMY-TURN-TAKING` (each quoting the user verbatim plus the scope ruling) and their `PROVISIONAL-*` rows; amendments to the rows the recon lists (`USER-STORY-AERIAL-REACH`, `USER-FINISHER-CARRY`, `USER-STORY-WALL-BOUNCE`, `PROVISIONAL-SPACING-WALLS-RULES`, `S1-STUNNED-KNOCKBACK-FRICTION`, `USER-MOB-MELEE-RANGE`, `PROVISIONAL-ENEMY-CHASE-LEASH`, `USER-MOVEMENT-FEEL`, `VERIFY-PAR-SECONDS`, …); the design-master lines owed (the 2026-10-11 convention: the mirror is not edited); AGENTS.md and CLAUDE.md (baseline, bot notes, comparability, the new systems table rows); `docs/GAMEPLAY_FEEL_2026-08-10_PLAN.md` supersession notes; this plan's §9 and §10.
7. Commit on `main`; prune merged worktrees and branches.

## 8. Out of scope

Boss-arena hero seals; mob↔mob pushbox jostling; production art for barriers, spawn-in tears and the GO prompt (placeholders only); a full par re-measurement per level (one provisional scale plus a bot check only); re-tuning enemy speeds against the slower hero; Fighter-side kit-motion speeds; the design-master edits themselves (listed as owed).

## 9. Deviations log (append-only, filled from the handoffs)

Each workstream's handoff (`docs/handoffs/GU_<id>.md`) is the full record. The entries below list only what departed from §1–§5 and why, each workstream's test delta, and its commits. Phase A's subsections follow §3's merge order (D → C → A → B → F → E); for the later phases the actual merge order is stated at each phase's head. Every agent choice has a ledger row in `docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md`, section "Confirmed open deviations — gameplay update, 2026-10-12", indexed in "The 2026-10-12 gameplay update — index and design-master lines owed".

### Phase A

#### GU-D — static Story knockback (`75843b7`, merged `6737ea4`)

- §2.5 applied as written. The carry deceleration also replaces S1's ground friction, as the plan's line reads, so a finisher flight that touches down still stunned keeps braking at the carry rate.
- One pin beyond the plan's list, `StoryFinisherCarryTests.AFinisherFlightBrakesAtItsCarryDecelerationAloneEvenWhereS1IsStronger`, which fails under the old `max`. `StoryFinisherCarryTests`' ±6 px landing gate was re-premised on `CarryStoppedInAir`, because the old gate encoded the retired `max(S1, carry)`.
- Test delta: **+3** (3021 → 3024); `StoryCombatRulesTests.StoryKnockbackScalesWithTheVictimsMissingHPAfterTheHit` rewritten in place as `StoryKnockbackOnTheHeroIsStaticAtEveryHP`. Handoff: [GU_D.md](handoffs/GU_D.md).

#### GU-C — movement −20 % (`2302376`, merged `79a5a23`)

- Two new suites (+5) pin the cut, beyond the plan's "tests whose pinned speeds move".
- The roll compensation's stated reason does not hold: with the multiplier left at 1.5, Lincoln still rolls through a slasher (Story) and crosses up (sim) from pushbox contact. `RollSpeedMultiplier` 1.875 keeps the roll's distance and every margin, not the pass-through itself. The decision stands; the ledger row states the corrected reason.
- One geometry fix the plan did not anticipate: Level 11's railcut perch 220 → 340 px, the only jump edge lost without an alternative route in the bots' nav-graph closure for Lincoln over all sixteen levels. GU-L8 owns Level 11 in Phase B and kept it.
- A comment-only edit outside its region: `FighterLoadout.Default`'s comment in `FighterSimulationComponents.cs`.
- Test delta: **+5** (3021 → 3026); `TubmanContentTests` pins 5.4 (was 6.75). Handoff: [GU_C.md](handoffs/GU_C.md).

#### GU-A — the single aerial strike (`edc03dd`, merged `1740519`)

- In the sim a walk-off cannot reach the "no chain hit starts in the air" rule: every authored edge is a grabbable ledge whose grab cancels the string, and every platform is too low for a drop-through to outlast hit 1. The sim rule is pinned on `ProcessBasicAttackPhase` with a hand-built airborne fighter, so that method became `internal`.
- The aerial frame and box data were retired outright (§2.1 allowed either), and `AerialComboCounter` deleted; the profile constructor lost an argument.
- A leak the plan did not name was fixed: `StartDirectionalAttack` never cleared Tesla's Conductive combo mark.
- Outside its ownership: three additive `air_attack` rows in `StoryEnemyCombatRulesTests`, and one comment in `FighterEntitySystems.cs`.
- The worktree was created at `d2e52e0` and fast-forwarded to the plan commit before any edit (as were GU-B's and GU-F's).
- Test delta: **+7** (3021 → 3028); the in-place rewrites are listed in the handoff. Handoff: [GU_A.md](handoffs/GU_A.md).

#### GU-B — the closer camera (`57573e4`, merged `b504c49`)

- The framing adds a **horizontal pull** onto the arena; the plan gave only the zoom formula. Without it, Level 2's Duke stayed off-screen for a hero who had just stepped in. Reverting it is one statement in `StepFollow`.
- A **room floor** on the framing zoom: a framing never widens past the confinement rect. It binds only on Level 4, at 1.2.
- The plan's either/or on two framing owners was decided as "the widest view wins".
- Test delta: **+10** (3021 → 3031), all in `StoryCameraRigTests`. Handoff: [GU_B.md](handoffs/GU_B.md).

#### GU-F — the engagement director (`c5cb7b7`, merged `bbcba61`)

- Side rebalancing reads "crowded" as "out of room", not as a head-count imbalance. On open floor nobody crosses; a head-count rule would have had a waiter walk through the hero every time.
- Added: a ranged-fairness rule (the shooter refused longest fires next) and `CrossingCooldownFrames` 60.
- Hooks outside §3.1's list that §2.6's own text needs: `ProcessMobTick` (the counter tick), `ResolveFinisherTouchdown` (the touchdown hand-off) and `StandOffBandFor` (the ranged hold cap).
- The boss half of "no mob targets the Mirror clone" is a one-line change with no test.
- Test delta: **+24** (3021 → 3045). Handoff: [GU_F.md](handoffs/GU_F.md).

#### GU-E — the encounter-lock framework and Level 2 (`072a26a`, merged `fbb1097`)

- `MaxAreaWidthPixels` is 3200, not "about 2600": the plan's own Level 2 areas are 2900 and 3070 px.
- Level 2's areas shifted to 300–3200 · 3220–6290 · 6460–8690 so that every barrier stands outside its own area, with a corridor for the Middle anchor, which moved 5300 → 6370 (and to 6500 in FIX-E).
- The hard cap never opens a lock whose required puzzle or breakable is still pending; the plan's literal rule would have opened a puzzle lock after five minutes.
- "Never members: ArenaGuardians, BossDecoys, bosses, the Mirror clone" holds by type; summons and `DrillInvulnerable` bodies are refused by `JoinEncounter`. The non-preserve `ApplyStoryRewind` branch keeps membership.
- Outside its ownership: one condition in `ArenaGeometryContentTests.AddReachIssues` (an exit barrier closed from level build read as a perch at `ColumnTopY`), and `TimelineIntegrityTests` / `StoryLevelControllerBaseTests` rewritten in place for the par scale.
- The merge resolved an `en.csv` / `en.en.translation` conflict; the orchestrator then regenerated the translation and pointed `EncounterLockRules.ViewWorldWidth` / `ViewWorldHeight` at `StoryCameraRules` (the handoff's TODO; `199e260`).
- Test delta: **+39** (3021 → 3060). Handoff: [GU_E.md](handoffs/GU_E.md), which also holds the Phase B authoring guide.

**Phase A merged: 3109 = 3021 + 88** (`199e260`).

### Phase A review fixes

Not in the plan. The adversarial review of the Phase A diff confirmed 13 findings and refuted one. FIX-E took the eight (six distinct findings; two were reported twice) on the lock framework, Level 2, zones, pulls and the lock / director seams; FIX-F took the three on the director and the two test defects in GU-A's and GU-D's work. Both ran from `199e260`; FIX-F merged first.

#### FIX-E (`675fdde`, merged `7aaae7d`)

- Level 2's Middle checkpoint could be struck from inside the engaged siege line (high; reported twice). It was fixed in the framework rather than by geometry alone, so no Phase B level could repeat it: `CheckpointTrigger.IsHeldByEncounterLock` and a new key, `checkpoint_held_by_encounter_lock`. Level 2's anchor moved 6370 → 6500 and the battlement's west edge 6460 → 6580, which also took the anchor off the room 2/3 camera seam.
- Relativity Rift zone ticks and crowd bowling skip a dormant member; the three pulls go through `BaseSpecial.PullThroughCollision` (horizontal, then vertical); a lock's wake joins the director and dormancy leaves it.
- Six authoring rules added to `GU_E.md`'s guide (marked FIX-E). Outside GU-E's file list: `LevelManager.cs`, `BaseSpecial.cs`, `PlaceholderZone.cs`.
- Not done: the commit-time baseline filter, redundant because every activation goes through `Activate()`.
- Test delta: **+11** (`EncounterLockTests` +5, the new `StoryKitPullWallTests` 3, `EncounterLockContentTests` +2, `Level02ContentTests` +1). Handoff: [GU_E.md, "Review fixes"](handoffs/GU_E.md).

#### FIX-F (`c80f316`, merged `6e1a25b`)

- **A plan deviation:** the director's priority is now rest elapsed → elite → …. §2.6's literal "elite before standard → rest elapsed" let an elite that handed off its slot take it straight back, which cancelled the plan's own hand-off and stale rules for an elite holder.
- The engage blink is judged on the side it lands on (`EnemyAbilityExecutor.TeleportLandingSide`); a capped ranged band's release line is capped at 600 px; both hold lines now derive from `StoryCameraRules`.
- GU-A's lost `NextChainStepForPress` coverage was restored (`MirrorParadoxTests` +1), and GU-D's hero pin now reaches its "fifth of max HP" case.
- Test delta: **+5** (`StoryEngagementDirectorTests` +4, `MirrorParadoxTests` +1). Handoffs: [GU_F.md, "Review fixes"](handoffs/GU_F.md), [GU_A.md, "Review fixes"](handoffs/GU_A.md), [GU_D.md, "Review fixes"](handoffs/GU_D.md).

**After the review fixes: 3125 = 3109 + 16** (`7aaae7d`).

### Phase B

All five ran from `7aaae7d`. Actual merge order: GU-BOT (`70f4554`), GU-L0 (`037e452`), GU-L4 (`54480ab`), GU-L8 (`2abd6a8`, resolving an additive `RewardManifestTests` conflict), GU-L12 (`c4885a9`).

#### GU-L0 — Levels 0, 1 and 3 (`f005196`)

- Level 0's lock is built when the Combat Trial begins, not at level build, because the bay is shared with the hold, the calibration and the mobility gates. So `EncounterLockContentTests` never sees it, and `Level00FlowSceneTests` pins its layout. Its waves name no manifest row (Level 0 pays no persistent dust), and its area starts at 2580, not ~2400.
- Florence's room 3 is one lock, not two (1920 px cannot hold two 1280 px areas and an anchor corridor). Its room-1 manifest row was split in two, the only manifest change.
- Framework issues reported, not edited (the framework was read-only in Phase B): the GO prompt showed for `GoDirection.None` (fixed by FIX-LEVEL); the content sweep sees only locks that exist at load; the test seam completes Level 0 the moment the trial begins.
- Test delta: **+10** (3125 → 3135). Handoff: [GU_L0.md](handoffs/GU_L0.md).

#### GU-L4 — Levels 4–7 (`d6740d6`)

- "The debut Eraser … never paying dust" was read as "never difficulty-scaled": `RewardManifestTests` requires the `@0` row to pay its share.
- Level 5's flood steps rise after a lock's clear once the hero is clear of them, not at the clear's instant, which would have flooded the deck the hero still stood on.
- Level 7's movers are not held, because none crosses a barrier (pinned).
- Edited outside its ownership: two additive members in `EraserDebutTrigger.cs` (`RouteTriggerSpawns`, `AdoptEraserDebut`), because the read-only framework cannot adopt a body it did not spawn.
- Level 6's Chrono-Warden and Level 7's Eraser now pay dust (a lock wave must be a manifest row), and Level 7's Easy count fell 8 → 7.
- Test delta: **+13** (3125 → 3138). Handoff: [GU_L4.md](handoffs/GU_L4.md).

#### GU-L8 — Levels 8–11 (`48d3996`)

- `EncounterLockContentTests` was edited beyond a row: a one-entry per-lock table (`EntryFloorByLock`) for the Globe's stage-with-cellar, which the post-height rule cannot express.
- Level 9's Middle moved past the Lantern Watch rather than into a split of it, and Level 8's Middle up to the shaft lip.
- Two pre-existing geometry bugs fixed in passing: Level 8's open tomb-corridor end and its room 3 camera.
- Level 9's Chrono-Warden is now ledgered.
- Test delta: **+17** (3125 → 3142). Handoff: [GU_L8.md](handoffs/GU_L8.md).

#### GU-L12 — Levels 12–15 (`4b9e386`, `da54178`)

- The pre-boss garrisons fold into the last approach lock (the aprons are narrower than a lock), so Level 12's wave 4 is not fought east of the curtain, and Level 14's drone left the arena with the rest of its wave.
- The salted hunters pay dust (a lock member needs a manifest row), against the plan's Level 5 wording.
- Outside its ownership: a held-mover exemption in `ArenaGeometryContentTests.NoMovingPlatformEndpointOverlapsStaticGeometry`.
- Found and fixed a pre-existing content bug: Level 15's escape never completed for a walking hero (a level-side hook, later generalised by FIX-LEVEL). It recorded the firestorm halting after a death mid-escape as `VERIFY-L15-ESCAPE-REWIND-HALT`, which never reached the ledger before FIX-LEVEL fixed it.
- Test delta: **+9** (3125 → 3134). Handoff: [GU_L12.md](handoffs/GU_L12.md).

#### GU-BOT — lock-aware bots and telemetry (`9735c21`)

- `passed_while_alive` is reported both ways (its old meaning kept, a new `left_lock_with_members_alive`) instead of redefined.
- The stuck-lock watchdog adds a 120 s bound to the plan's 60 s rule.
- `AutoMaxSeconds` lives on `PlaytestOptions`. Added beyond the brief: the far-waiter log, `swings.by_variant`, `nav_barrier_rebuilds`, raw sums and 8 px histograms, and the escape-front guard.
- Only Level 2 had locks on its base, so `stuck_lock`, `lock_hunt`, `lock_wait` and the 120 s bound ran only in the unit case.
- Test delta: **+6** (3125 → 3131). Handoff: [GU_BOT.md](handoffs/GU_BOT.md).

**Phase B merged: 3180 = 3125 + 55** (`c4885a9`).

### Phase B review and the first Phase C bot batch

Not in the plan as a fix source. The adversarial review of Phase B confirmed 19 findings (none refuted; several duplicates): lock-required objectives that a death rewind un-met (Florence, Chicago, Hakata Bay, Gettysburg twice); Level 7's unreachable Middle anchor and its channel-seam objective; the Level 6 and Level 15 escapes (never completed, the head start floored inside the lock, the firestorm halted by a death, an assertion that could not fail); Level 9's font; Level 10's Middle-resume objective; the GO prompt; the bot pinned against a breakable on Level 4; and the lock telemetry merging instances across an Anchor Snap. The first Phase C bot batch (`guce*` / `gucn*` / `guch*`, 180 runs: Levels 1–15, Joan and Lincoln, `play`, Easy / Normal / Hard, seeds 0–1) ended `stuck_lock` on Levels 1, 4, 5, 6, 7, 9 (Hard), 10 and 11, and Level 12 Easy collapsed on the Timer. Together they became the Phase C fix round, four parallel workstreams from `c4885a9`.

### Phase C fix round

Actual merge order: FIX-OBJECTIVE (`f161f1a`), FIX-LEVEL (`0711759`), FIX-ENEMY (`9ed0070`), FIX-BOT (`147ac11`).

#### FIX-ENEMY — enemy behaviour inside engaged locks (`d2beec5`)

- Six items from the batch: flyers held out of reach (the 160 px hover cap, the flyer stall); a thrown flyer that rose forever (the drag, for every flyer); ground members posted on ledges (the step-off, and the watchdog's beside-the-hero destination); Level 5's "staircase" stall (the listing ramps never met their decks; re-authored by their joints); Level 6's untouchable legionnaire (a bot gap: the basalt boulder blocks M1's line of sight); Level 13's melee over the slot count (the deliberate V7.4 retaliation, which the telemetry now separates).
- It edited framework files that were read-only in Phase B (`EncounterLockRules.cs`, `EncounterLock`'s watchdog, `EnemyController*.cs`) and made additive edits to GU-BOT's telemetry files.
- Scope choices left open: flyers outside locks are unchanged, and ranged ground members keep their posts.
- Test delta: **+17** (3180 → 3197). Handoff: [GU_FIX_ENEMY.md](handoffs/GU_FIX_ENEMY.md).

#### FIX-OBJECTIVE — lock-required objectives under the death rewind (`f7db68f`)

- Review items #0, #1, #5 / #13, #6 and #7.
- The framework does more than the brief's latch: it also switches the objective itself to `PreserveCurrentState`, which fixes Level 2's tower and Level 11's arrays with no level edit. Level 11 needed no level change.
- No objective re-post on `PuzzleReset`: no reset can happen after a required solve.
- Item #5's case (a), a Middle resume, did not reproduce in a scratch-save harness (a scratch save re-parks at the Entry; `VERIFY-SCRATCH-SAVE-ENTRY-REPARK`); the Hard, Middle-not-struck path does and is pinned.
- Test delta: **+10** (3180 → 3190). Handoff: [GU_FIX_OBJECTIVE.md](handoffs/GU_FIX_OBJECTIVE.md).

#### FIX-LEVEL — per-level and HUD fixes (`80e1d11`, `cb96fa7`)

- Review items #2, #3 / #14, #4, #8, #9 / #16, #10, #15 and #18.
- Item #2: Level 7's anchor moved onto the receiving yard (the verifiers' preferred option), not the lock; a new all-level rule, `AnchorApproachContentTests`.
- Item #4: one small shared guard in `StoryLevelControllerBase` (a room entry's objective defers to an engaged lock's), outside its ownership as its brief allowed, scoped to room triggers so the levels' own mid-fight warnings still post.
- Items #9 / #16 and #15 were fixed on Level 6 as well as Level 15 (the same dead `IsCompleted` read); the escape finish rule moved into `EscapeSequenceController`, and Level 15's GU-L12 hook was removed.
- Item #18's case lives in a new suite rather than `EncounterLockTests`, clear of the framework workstreams' edits.
- It closes `VERIFY-L15-ESCAPE-REWIND-HALT`.
- Test delta: **+11** (3180 → 3191). Handoff: [GU_FIX_LEVEL.md](handoffs/GU_FIX_LEVEL.md).

#### FIX-BOT — the bots and the lock telemetry (`c8682d8`)

- Review items #11, #12 and #17, and every bot gap of the batch: the walk-in pin against breakables and props, gear trains, the Level 6 winch (three bot problems, not the "missing counterweight" CLAUDE.md recorded), members posted above the hero, Level 11's Alpha struck from the floor, and Level 12's dodges, lift and ferry.
- Five changes apply outside locks too (the escape finish, ledge-safe dodges, the flush-stop boarding edge, moving-platform landing tracking, the platform-transfer penalty), because the stalls they fix are not lock stalls.
- Its final Level 11 run still ended `stuck_lock` on the Angle: the rewind-restores-Alpha game bug that FIX-OBJECTIVE fixed in parallel.
- Test delta: **+9** (3180 → 3189). Handoff: [GU_FIX_BOT.md](handoffs/GU_FIX_BOT.md).

### The merge fix-up (`681a525`)

Two `PlaytestEncounterTelemetryTests` cases had been written against behaviour the parallel fixes changed, and were rewritten in place with no count change: `TheBotClimbsOntoTheLedgeALockMemberHoldsInsteadOfJumpingAtItFromBelow` now posts a ranged `laser_pistol_deckhand` (an engaged melee member steps down since FIX-ENEMY, and shooters keep their posts), and `TheBotGoesOnPastAFinishLineWhoseEscapeNeverCompleted` freezes its escape to hold it running (an escape completes past `FinishX` since FIX-LEVEL). **Full suite: 3227 = 3180 + 47** (FIX-ENEMY 17, FIX-OBJECTIVE 10, FIX-LEVEL 11, FIX-BOT 9).

### The closeout's Level 7 ramp fix (`4ca4280`)

FIX-ENEMY found Level 7's two boss-arena listing ramps built the way Level 5's had been (centre, width and a 10.5° tilt), each leaving a 5.2 px riser under its upper deck's east end, and recorded it outside its ownership (`VERIFY-L07-ARENA-RAMP-RISERS`). The orchestrator moved Level 5's joint builder into `StoryLevelControllerBase.BuildRampBetweenJoints` (either joint may be the western one; `ListingRampLowJointOverlapPixels` 16), kept Level 5 on it unchanged and authored `Level07Controller.ListingRampJoints`. Test delta: **+1** (`Level07ContentTests.TheFlagshipDecksTopIsContinuousAcrossBothListingRamps`), 3227 → 3228. The row is closed. The build landed while the `gud*` batch was running, so the runs launched after about 19:04 carry it (only the Level 7 boss arena's two ramps differ).

## 10. Closeout report

**Delivered.** All six requests, with the four scope rulings, on `main` (the plan `b8f5343`, Phase A merged at `199e260`, its review fixes at `7aaae7d`, Phase B at `c4885a9`, the Phase C fix round at `681a525`, the closeout's ramp fix `4ca4280`, the closeout docs merged at `de0e26c`, then the closeout commit). In one line each (the ledger section "Confirmed open deviations — gameplay update, 2026-10-12" has the rows, its index the design-master lines owed):

1. **One air strike** (`USER-AERIAL-SINGLE-STRIKE`, both modes): an airborne neutral press is `BasicComboRules.VariantAirAttack`, never chaining; the sim's `FlagAirAttack`.
2. **A closer Story camera** (`USER-STORY-CAMERA-ZOOM`): `StoryCameraRules.ViewZoom` 1.5, boss and Mirror framing eased out to `BossFramingMinZoom`; Fighter untouched.
3. **Areas to clear** (`USER-ENCOUNTER-LOCKS`, Story): 48 encounter locks across Levels 0–15, every area with enemies and every progress-blocking puzzle room, with barriers, warded dormant members, waves and spawn tears, a stall watchdog, a checkpoint hold, the one-way objective latch, a HUD counter and GO prompt, and F10 baselines (v3).
4. **Movement −20 %** (`USER-MOVEMENT-SPEED-CUT`, both modes): the nine `MaxMoveSpeed` × 0.8, jump height unchanged, jump distance × 0.8, the roll's distance kept (`RollSpeedMultiplier` 1.875).
5. **Static Story knockback** (`USER-STORY-STATIC-KNOCKBACK`): no victim-HP term in Story; the Fighter keeps its HP ramp.
6. **Enemies take turns** (`USER-ENEMY-TURN-TAKING`, Story): the engagement director's melee slots Easy 1 / Normal 2 / Hard 3, ranged permits, a wait ring and rest; a struck waiter may still answer (the retaliation exception).

**Tests.** 3021 → **3228** (+207): Phase A 88, its review fixes 16, Phase B 55, the Phase C fix round 47, the closeout's ramp fix 1; the arithmetic per suite is in `AGENTS.md`'s validation section. Every merge point's full run reconciled exactly (3109, 3125, 3180, 3227, 3228). The closeout's three consecutive full runs on 2026-10-13 (orphans drained, lock held): `Failed: 0, Passed: 3228, Total: 3228`, 84–87 s each.

**Reviews.** Two adversarial reviews with two refuting verifiers per finding: Phase A's confirmed 13 findings (FIX-E, FIX-F), Phase B's 19 (15 distinct; the Phase C fix round). The largest was systemic: a death rewind restored a lock's required puzzle or breakable to a snapshot taken before the solve, which could hold a lock forever on Hakata Bay and Gettysburg; it is now a one-way latch in the lock (`PROVISIONAL-LOCK-OBJECTIVE-LATCH`).

**Bots** (`D:/Projects/absolum_research/update_2026-10-12/phaseC_batch2.md`; Levels 1–15, Joan and Lincoln, `play`, Easy / Normal / Hard, seeds 0–1, 180 runs a batch):

| | `guc*` (before the fix round) | `gud*` (after) |
|---|---|---|
| Easy completions | 36 / 60 | **57 / 60** |
| Normal completions | 11 / 60 | **31 / 60** |
| Hard completions | 0 / 60 | 0 / 60 |
| `stuck_lock` endings (E / N / H) | 20 / 33 / 30 | **1 / 1 / 0** |
| Locks cleared (E / N / H, of 188) | 144 / 125 / 81 | **186 / 185 / 141** |
| Deaths per run (E / N / H) | 0.52 / 2.75 / 1.43 | 0.88 / 4.35 / 2.40 |
| Gated melee attackers over the slots | — | **0 frames** on every difficulty |

Deaths rose because runs now reach the fights they used to stall before. The two remaining stalls and the one Timer collapse are bot gaps (CLAUDE.md, the bots section): Level 7's skiff channel, and Level 10's trapdoor recess after the boss falls. **Hard is the bot's ceiling, not a regression**: the pre-update build `2e9ca79` completed 0 of 16 Hard runs on Levels 2, 5, 8 and 11 and downed no boss; after the update the same runs get further on median (8472 px against 7601), Level 8 excepted (its locks force every fight). Normal's Act III is the steepest part of the campaign for the bot: no Normal completions on Levels 12, 14 and 15, at 12–13 deaths a run on 14 and 15.

**`VERIFY-PAR-SECONDS`.** Every completed run reached its PreBoss lock with at least 66 % Integrity on Easy and 72 % on Normal; without the × 1.5 scale the worst would end near 49 % and 61 %, so no completion depends on it. The bots are faster than a person exploring, so `ProvisionalParScale` stays 1.5, provisional, until a human pass.

**Smoke runs** (`--headless --quit` and `--quit-after 300`): the project load, the hub, Level 0, Florence, Level 5 and Level 7 all exit 0 with no script error; Level 0 prints its known 6 ObjectDB / 3 resources at exit and Level 7 the control levels' 2 / 1 (CLAUDE.md, the smoke-run noise note).

**Not delivered, and the honest list.**

- **Nobody has played or looked at any of it**: not the air strike, the zoom and its boss framing, a single lock fight, a barrier, a spawn tear, the GO prompt, the slower movement, the static launches, a waiter's ring, or a member put down beside the hero by the watchdog. Every acceptance claim above is a headless suite, a smoke run or a bot run.
- Every number the agents chose is provisional (the `PROVISIONAL-*` rows of the section), and the open `VERIFY-*` / `DEFER-*` rows stand: `VERIFY-PAR-SECONDS`, `VERIFY-L07-ROPE-OVERSHOOT-BOARDING-ENGAGE`, `VERIFY-LOCK-PUZZLE-PROPS-REWIND`, `VERIFY-SCRATCH-SAVE-ENTRY-REPARK`, `VERIFY-ENGAGEMENT-CROSSING-PRESENTATION`, `DEFER-MOB-PUSHBOX-JOSTLE`.
- The design-master edits are owed, not made (the 2026-10-11 convention: the mirror `design-godot.md` is not edited); the index lists them, and `HUD_CONTRACT.md` owes the lock panel.
- §8 stands: no boss-arena hero seals, no mob-to-mob jostling, placeholder barriers, tears and GO prompt, no per-level par re-measurement, enemy speeds not re-tuned against the slower hero (17 mob types now outrun Lincoln), no Fighter-side kit-motion speed change.
- Scope choices left to the user: flyers outside locks keep their altitude, ranged members keep their posts, and a member stalled for 10 s is moved visibly.
