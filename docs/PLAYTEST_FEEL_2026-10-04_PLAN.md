# Playtest-driven feel, anti-spam and pathing pass (2026-10-04)

**Status:** implemented and measured (2026-10-07). Round 1 (BOT, FEEL, ENEMY, ARENA) and the fix round (fix-enemy, fix-feel, fix-arena, fix-level) are merged into `main`'s working tree, uncommitted on `5d3ce25`; the suite is 2711, green on five consecutive full runs (§3). §4 has the before/after measurements. Nobody has played any of it. **Source:** the user's three complaints of 2026-10-04 — "too easy to bypass everything / enemy pathing is not very good", "string + both specials is basically an insta-kill", and "the game feels sluggish (hitstop / frame counts?)" — measured with the new Story playtest harness (`scripts/Diagnostics/`, `tools/playtest/`, see CLAUDE.md "Automated Story playtests").

The user asked for this pass to iterate on what improves feel and core play. Every change below is either (a) a build bug or an unbuilt part of the design, or (b) a **provisional ruling made under that request**, marked *Provisional* and recorded in `docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md` so the user can reverse it. Items the user previously locked ("Do not re-litigate", `docs/GAMEPLAY_FEEL_2026-08-10_PLAN.md` §2.1–2.2) are **not changed**; they are listed in §5 with the measurements for a ruling.

## 1. Evidence (baseline, before this pass)

Feel probe (`--bot=feel`, Joan, Level 2, frame-exact under `--fixed-fps 60`, `tools/playtest/analyze_feel.py`):

| Measure | Value |
|---|---|
| Run to 95% top speed / stop to zero | 15 / 13 frames, 50 px stop slide |
| Press → first active frame, hit 1 | 6 frames |
| Mashed 3-hit string, press to back in control | 99 frames (1.65 s) |
| Three presses on a 12-frame rhythm | **2 hits** — the third press is dropped |
| Hitstop on every basic hit | 3 frames (the curve's floor; basics were halved on 2026-08-10) |
| Pose display during a swing | 3 poses at a flat 8 fps; the last held up to 41 frames (chain hold) |
| Enemy horizontal knockback on a stunned hit | **0** — zeroed every stunned frame |
| Hitstop on a killing blow | 0 (lethal hits skip hitstop) |

Bot matrix (Levels 2 and 5, Normal, Joan/Lincoln, mashed string):

| Bot | Median frames from first hit to kill | Mobs dead inside one 2 s burst |
|---|---|---|
| spam (string + S1 + S2) | 62–124 | 5–8 per level |
| basics (string only) | 330–470 | 0 |
| bypass (never attacks) | — | passes 8–15 mobs alive; only 4–6 ever hit it again |

### 1.1 Baseline with the hardened bots (same gameplay, nav-graph bots, 2026-10-04)

All nine heroes, feel probe (`--tag=fbase`; jump measured at the Level 2 spawn under open sky with a full 40-frame hold):

| Measure | All heroes |
|---|---|
| Run to 95% / stop to zero / stop slide | 15 / 13 frames / 31–50 px |
| Full-hold single jump: air time / height | 43–65 frames / 129–209 px (Lincoln lowest, Cleopatra highest) |
| Three presses on a 12-frame rhythm → hits | 2 |
| Whiffed string / mashed string attack lock | 80–83 / 99 frames |
| Longest single pose hold in a whiff | 41 frames |
| Hitstop frames across a two-hit string on a mob | 6 (3 + 3) |
| Mob horizontal displacement from the string | 0 px |
| String + S1 + S2 on a 40 HP slasher | kills Joan/Lincoln/Mozart; leaves 4–33 HP otherwise |

Bot matrix, Normal, Levels 5/7/8/9/11 × Joan/Lincoln/Einstein/Mozart (`summary_normal_base.csv`, 60 runs):

| Bot | Completed | Mean deaths | Median frames first-hit→kill | Kills inside one 2 s burst |
|---|---|---|---|---|
| spam | 2/20 | 4.0 | 240 | 24 |
| basics | 2/20 | 3.9 | 436 | 0 |
| play | 8/20 | 3.2 | 288 | 38 |

Killing blows across those 60 runs: Level 8 Jackal Priest 39, Level 7 environment (sea undertow) 32, Level 11 Cyber Cavalry Commander 19, Level 5 Tidal Overseer 18 (almost all *before* its arena, x 7300–8200 vs arena start 8800), Level 11 siege cannons 16, Level 9 Thunderbomb General 15, Level 7 Dread Admiral 13. `hit_confirms` records only basic hits for every hero: Specials that deliver through shape queries raise no hit confirm (no caster hitstop, no impact VFX, no haptic).

Bypass (Joan, Normal, Levels 2–12): reaches the boss in 80–150 s on most levels with 5–13 mobs left alive behind it; dies on every level, mostly to bosses that roam out of their arenas.

Hardened `play` bot completions (Joan): Easy 9–10 of 11 (2, 3, 4, 5, 7, 8, 9, 11, 12), Normal 7 of 11 (2, 3, 5, 7, 8, 9, 11). Level 6 and Level 10 collapse on the timer for every hero (bot route issues still open); Lincoln's low jump fails several routes.

Geometry and AI defects the bots exposed: Level 2 Gate B jumpable over the top from the Extractor perch; Level 2 floor hole at x 8900–8960 (fall out of the world); the Level 2/5 bosses leave their arenas from level load and (Level 2) hit through the battlement footing wall; `InteractionArea` only hears raw Godot input events (no `InputManager` route).

## 2. Decisions

### 2.1 Feel (player side)

| # | Change | Authority |
|---|---|---|
| F1 | Drive the three sprite poses from the attack's frame windows (wind-up through startup, strike pose from the first active frame, follow-through through recovery) for basics, directional attacks and Specials, in Story and the Fighter presentation. | Build bug vs `design-godot.md:3878` (the 3-frame contract stretches poses across authored windows). |
| F2 | One attack clock: every basic swing runs on the physics frame clock that hitstop extends. | Build inconsistency (two clocks). |
| F3 | Chain buffer: a BasicAttack press during active frames or hitstop is buffered, a press made after the next swing started counts for the following swing, and holding attack through the chain window continues the chain. Both modes. | `design-godot.md:1032` (hold continues; 24-frame window) — the active-frame exclusion is not in the design. |
| F4 | Animation during the chain hold returns to run/idle; the Story player plays `hitstun` / `dazed` when stunned. | Presentation; H03 contract already lists the animations. |
| F5 | Specials and Ultimates freeze the caster on a landed direct hit (once per execution); enemies and bosses skip victim hitstop for Tick / Construct / Hazard deliveries and `ExemptFromHitstop`. | `design-godot.md:1021-1023`. |
| F6 | *Provisional:* hitstop gains weight — a launching hit adds +3 frames (finisher, Up-Attack, Down-Air, launching Specials) and the floor rises 3 → 4. Both modes through the one `BasicComboRules.HitstopFrames` rule. Story mob kills get a kill freeze instead of no hitstop. | Changes the locked curve at `design-godot.md:1021`; user named hitstop explicitly. Ledger row `PROVISIONAL-HITSTOP-WEIGHT`. |
| F7 | *Provisional:* ordinary standard-mob attacks no longer launch the Story player (grounded hitstun + slide); elites, bosses, hazards and abilities that author a launch keep launching. | Resolves the open `VERIFY-ABILITY-LAUNCHES` enemy/boss/hazard half. |
| F8 | Story camera per `design-godot.md:3119-3132`: horizontal dead zone, look-ahead in the movement direction, separate X/Y damping. | Build deviation from the design. |
| F9 | Player-dealt hits on mobs give camera shake (scaled by damage / launch) and floating damage numbers (respecting the damage-number setting). | Presentation; design §8 shake/haptics table. |

### 2.2 Anti-spam (Story only — nothing reaches `scripts/FighterSim/`)

| # | Change | Authority |
|---|---|---|
| S1 | Enemy knockback while stunned decays with friction instead of being zeroed, so the string and finisher move mobs. | Build bug (the zeroing predates "knockback replaces velocity"). |
| S2 | *Provisional:* **Confirm proration** on Story mobs. A Special-class hit landing on an enemy already in hitstun deals ×0.6; within one uninterrupted stun chain each hit after the third decays ×0.85, floored at ×0.5. Ultimates and DoT/zone ticks exempt. | New PvE rule; amends Enemy Stagger Discipline. Ledger `PROVISIONAL-STORY-CONFIRM-PRORATION`. |
| S3 | *Provisional:* **Standard poise.** Standard mobs get a small stagger budget so a full string completes but a string + Special confirm trips Armored Recovery and a committed counterattack. | Extends V7.4 elite budget to standards. Ledger `PROVISIONAL-STANDARD-POISE`. |
| S4 | *Provisional:* basics pacing — Basic-class damage against Standard-tier mobs ×1.25 (Story intake), so dropping specials is not an 8-second slog. | Story-only intake; the shared `.tres` damages are untouched. |
| S5 | Hit-indexed basic stun floor so resistant mobs are not released mid-string (the floor's own comment claims coverage it lacks). | Build bug. |

### 2.3 Pathing / anti-bypass

| # | Change | Authority |
|---|---|---|
| P1 | Bosses stay dormant until their encounter is revealed by the player entering the arena, and are leashed to the arena's x-range. | `design-godot.md:3517`, `:1624`. |
| P2 | Enemy melee / charge hitboxes are clipped by walls (Environment line of sight). | Build bug (Area2D ignores walls; projectiles already stop). |
| P3 | Enemy projectiles lead a moving target and spawn from a facing-mirrored `AbilityOrigin`. | Build bug (stale feet aim; un-mirrored origin). |
| P4 | *Provisional:* chase persistence — an aggroed mob keeps chasing while the player is in its room (room-bounds leash) instead of dropping at a fixed radius; ledge/wall awareness so mobs stop at drop-offs and walls instead of pushing forever, and stop jittering under a player standing above. | Design says mobs are slower than the player and encounters may be skipped; this keeps that but removes the free disengage. Ledger `PROVISIONAL-ENEMY-CHASE-LEASH`. |
| P5 | Level geometry: close the Level 2 floor hole (x 8900–8960); extend Gate B's column above the reachable jump height; audit the same pattern (L9 RadarGate, L6 rockfall, L12 curtain, L3 court door). | Design intent (`PACKAGE5_CAMPAIGN_PLAN.md` L02: "gates cannot be jumped"). |

### 2.4 Harness

Bots: `bypass`, `spam`, `basics`, `play` (competent: blocks, avoids projectiles, specials when ready, Ultimate at full meter, seals the level) and the frame-exact `feel` probe with optional windowed PNG capture and contact sheets. The runner suppresses save writes, applies the campaign kit for the level by default, mirrors Interact into Godot's input pipeline, and ends a run that falls out of the world.

## 3. Deviations log

Round 1 ran the harness (BOT, in the main tree) and three worktrees (FEEL, ENEMY, ARENA), each reviewed. The fix round ran four isolated trees (fix-enemy, fix-feel, fix-arena, fix-level) through fix → review → fixup, and the orchestrator merged both rounds into `main`'s working tree (uncommitted on `5d3ce25`). Every ledger row named here is in `docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md` (the sections for workstreams ARENA, ENEMY and FEEL, and the fix round's level-bug section). Suite: 2561 → **2660** after round 1 (+99) → **2711** after the fix round (+51).

### 3.1 Round 1

**BOT — the Story playtest harness** (`scripts/Diagnostics/`, `scenes/diagnostics/PlaytestRunner.tscn`, `tools/playtest/`; CLAUDE.md "Automated Story playtests").
- Shipped: `PlaytestNav` (a platform graph from rect, circle and polygon colliders; walk / fall / jump / double-jump / drop-through / break / ride / rope-catch / rope-release edges, every jump and rope release simulated frame by frame against a copy of `PlayerController`'s air physics) and `PlaytestWorld`; Interact routed to `InteractionArea.TryInteract`; hazard handling (cyclic hazards by phase, `VolleyCover`, spikes, Level 7's `Sea_*` as pits, Extractor telegraphs, escape fronts, rising water, searchlights) with per-source `damage_by_source`; Middle/PreBoss checkpoint strikes, low-HP block/retreat, boss targeting by the live body; `NearCaptureBeat` handling; `stuck_paused:<what>` after 30 s paused, `fell_out_of_world@x,y`, the collapse cause in `end_reason`; `--nav-dump`, `--trace-every`, `--debug-ropes` and `--max-seconds=0` (Integrity budget + 60 s); `run_playtests.ps1` with `-ExtraArgs` and `summary_<difficulty>_<tag>.csv`; `tools/playtest/trace.py`.
- Deviations: no tests (`tests/` was outside its paths). `PlaytestRouteHints` is wired but its table is empty — the one hint written (waking Level 6's pumice) could not get round game bug 5 below. Dialogue IDs are polled from `DialogueManager.ActiveDialogueID` because `EventBus.OnDialogueTriggered` is never raised (bug 15). `--debug-ropes` and a 600-frame back-off after a refused Ultimate were added as diagnostics. The runner depends on two seams already in the tree, `SaveManager.SuppressWritesForDiagnostics` and `EnemyController.HitstopFramesRemaining`.
- Measured baseline: §1.1. Final Easy batch (`e6`, Joan, `play`): 9 of 11 complete, Levels 6 and 10 collapsing on the timer. Normal (`n2`): 4 of 11, lost mostly to bosses dealing 160–370 damage against Joan's 110 HP — read as a weak bot first, not a confirmed balance problem; other workstreams were changing combat code between the Normal batches.
- Test delta: 0.

**FEEL — player side (F1–F7).**
- Shipped as AGENTS.md's FEEL bullet: posed sheets from the frame windows in both modes (`AttackPoseRules`, F1); one physics frame clock, with `CombatAnimationPlayer` and `resources/Animations/placeholder_combat_animation_library.tres` deleted (F2); the chain buffer through startup, active frames and hitstop, hold-to-continue (`BasicComboRules.ChainContinues`) and Time Freeze discarding presses (F3); locomotion through the chain hold and the Story `hitstun` / `dazed` / `roll_recovery` reels (F4); `BaseSpecial.ConfirmAbilityHit` as the one landed-ability-hit feedback point, with the once-per-execution caster freeze and `CastClockSuspended` (F5); the hitstop curve's 4-frame floor, +3 launch bonus and 11 cap in both modes (F6, `PROVISIONAL-HITSTOP-WEIGHT`); `HitPayload.SourceIsStandardMob` (F7, `PROVISIONAL-STANDARD-MOB-NO-LAUNCH`).
- Deviations from §2: F3 adds a one-deep queued press beyond the plan's wording, because without it three presses on a 12-frame rhythm still gave two hits (`PROVISIONAL-QUEUED-CHAIN-PRESS`). F2 contradicts `design-godot.md:1018`/`:1035` (`F2-SINGLE-FRAME-CLOCK`). Sim Specials still fire on press, so the Fighter driver poses them in thirds of an 18-frame cosmetic hold (`SIM-SPECIAL-POSE-THIRDS`). The optional buffered Jump/Roll/Block cancel was not added. F5's enemy/boss victim launch bonus was left to ENEMY. Outside the plan, as crash hygiene for CLAUDE.md failure signature 2: `AuthoredResources.Pin` on scene-assigned scripted resources (`ContentTemplateMarker.Contract` / `PoolConfig`, `ChronalDustPickup.VisualTiers`), guarded by `SceneAssignedResourcePinTests` (+2).
- Review (ship, all minor): AGENTS.md still described animation-frame callbacks; two deviations were missing from the ledger; zone-resolved Specials and Ultimates freeze in Story but not in the sim (`ZONE-RESOLVED-HIT-FREEZE`); the driver posed `movement_ability` in thirds and never reached the whiff-recovery pose; a late shot could spend the next cast's freeze. Each was fixed in the fix round or recorded.
- Test delta: +27 (`FeelPassRulesTests` 3, `FeelPassStoryTests` 18, `FeelPassSimTests` 6). Rewritten in place: two `StoryCombatRulesTests` cases, and the hitstop expectations in `DirectionalAttackTests`, `FighterBasicStringParityTests`, `TutorialCalibrationV76Tests` and `FighterPresentationSyncTests`.

**ENEMY — Story enemy combat (S1–S5, F5 victim half, F9, the P3 AbilityOrigin mirror, P4).**
- Shipped as AGENTS.md's ENEMY bullet; every number in `StoryEnemyCombatRules`.
- Deviations from §2 (the plan gave no numbers; each is a ledger row): S1 friction 1800 / 600 px/s² with a 64 / 480 px ledge cancel (`S1-STUNNED-KNOCKBACK-FRICTION`). S2 judged once per Special per chain (keyed by AttackID), the chain spanning the armored window its own trip opens, the ×0.5 floor applying to the decay term only (so ×0.3 at worst), constructs counting, never below 1 damage (`PROVISIONAL-STORY-CONFIRM-PRORATION`). S3 budget 1.9 s, Special stun ×1.5, 0.6 s armored recovery, 3 s/s drain, elites charged at the pre-S5 floor, throws, bowling and frozen bodies exempt (`PROVISIONAL-STANDARD-POISE`). S4 limited to the universal basic set, constructs and throws excluded (`PROVISIONAL-BASICS-PACING`). S5 floors 34 / 44 frames from the template gaps plus 6 (`S5-HIT-INDEXED-STUN-FLOOR`). F9 shake from the authored intensity ×12 or a damage curve (`F9-DEALT-HIT-FEEDBACK`). The kill freeze is mobs only (`KILL-FREEZE-VICTIM`). P4's room is the first room trigger spanning the spawn x, with a 1.5× radius fallback and instant ledge/wall stops (`PROVISIONAL-ENEMY-CHASE-LEASH`).
- Review (fix first): S1's friction also slid non-knockback velocity (a ChargeDash mob stunned by a turret bolt slid 100–225 px); Ultimate stuns tripped standard poise (Lincoln's five smashes); bowled elites silently stopped charging their V7.4 budget. Fixed by fix-enemy E1–E3.
- Test delta: +34 (`StoryEnemyCombatRulesTests` 13, `StoryEnemyCombatTests` 21); two `EnemyControllerTests` cases rewritten in place for the 34-frame floor and S4.

**ARENA — pathing and anti-bypass (P1, P2, P3 lead, P5, F8).**
- Shipped as AGENTS.md's ARENA bullet: dormant, leashed boss arenas revealed on entry; wall-clipped enemy MeleeStrike/ChargeDash hitboxes; shots that re-acquire and lead at fire time; Level 2's floor hole closed (x 8900–8960); gate columns over Florence's workshop door, Level 2's Gates A and B, Level 3's court door, Level 6's rockfall, Level 9's RadarGate and Level 12's curtain; the Story follow camera.
- Deviations from §2: arena west edges on the old reveal line (`ARENA-WEST-EDGE-AT-OLD-REVEAL-LINE`); a pinned boss answers outside targets only inside its engagement range and never summons or teleports at them (`ARENA-LEASH-EDGE-ANSWER`); a dormant boss hit from outside wakes into a reveal without the intro beat (`ARENA-WAKE-REVEAL`); Florence's boss room could not be entered at all (its arena west wall ran full height), so a ground-level doorway was cut — a layout change beyond P5's gates (`ARENA-FLORENCE-DOORWAY`); camera lead and shot lead are horizontal only (`F8-LEAD-AXIS`, `P3-HORIZONTAL-LEAD`); the unspecified numbers (`ARENA-PROVISIONAL-CONSTANTS`). AreaPulse hitboxes were left unclipped (`VERIFY-AREAPULSE-WALLS`).
- Review (fix first): the gate audit missed Level 4's CourtyardGate and Level 11's breastwork; the Mirror clamp ran before the clone moved; the wall probe allocated per ray per frame; arena edges were hand-computed literals beside a dead reveal radius; F8 and P3 rows were missing; the leash edge left a plink spot. Fixed by fix-arena A1–A5 or recorded.
- Test delta: +36 (`BossArenaLeashTests` 13, `ArenaGeometryContentTests` 8, `ArenaEnemyAttackGeometryTests` 8, `StoryCameraRigTests` 7).

Round 1: 2561 + 34 + 27 + 36 + 2 (`SceneAssignedResourcePinTests`) = **2660**.

### 3.2 Fix round

**fix-enemy (E1–E5).** Reviewed "ship" (two wording findings), then a fixup.
- E1: a stun *opened* without a knockback write stops the mob horizontally (a flyer vertically too); a refresh keeps the slide (`S1-STUNNED-KNOCKBACK-FRICTION`). E2: Ultimate-class or Ultimate-origin stuns charge no standard poise (`StoryEnemyCombatRules.ChargesStandardPoise`; the V7.4 elite budget is unchanged). E3: a bowled elite keeps its V7.4 budget charge, and the bowling sweep disposes its group array (`PROVISIONAL-STANDARD-POISE`). E4: `StoryEnemyCombatRules.VictimHitstopFrames` is `BasicComboRules.HitstopFrames(dealt, launches)`, so mob, corpse and boss freeze for the attacker's launch-aware frames (`PROVISIONAL-HITSTOP-WEIGHT`, `KILL-FREEZE-VICTIM`). E5: `EnemyController.ResetGrabAndSlamState` on spawn, despawn and rewind restore, and a throw whose damage killed the mob launches nothing.
- Deviations: E5 reaches older V7.2 grab and Package 13 A12 slam-bounce latches the pass's own resets missed (each shown by a failing test). E1 also governs direct `ApplyStun` callers (a standalone Static Charge, Kinetic Splitting's bounce) when they open a stun. Noted, not changed: `LincolnSplittingStrike.ResolveGroundBounces`' doc comment says the bounce stun runs through Special-class intake and the diminishing special stun, but its `ApplyStun(duration)` passes `fromSpecial: false`.
- Test delta: +11 (`StoryEnemyCombatReviewTests` 10; `StoryEnemyCombatRulesTests` +1, with the victim-hitstop pin rewritten in place).

**fix-feel (G2, G7, M1, R11, R12).** Reviewed "fix first" (the Rift Phantom and an untested deferred path), then two fixups.
- G2: `UltimateMeter.SetValue` syncs `PlayerController.CurrentUltimateMeter` on every write (`SyncUltimateMeterFromNode`), so an Ultimate's own `Consume` no longer leaves Defy History, the Defy seal and the saves reading 100 (the seal still publishes on the next update). G7: Idle and Running read the Down tap before the crouch (`CheckDropThrough` returns whether it dropped), so a double-tap of Down drops through a one-way from standing and running. M1: the player's `MeleeHitbox` needs Environment line of sight to enemy and boss hurtboxes, cast from the target back to the swinger and skipping the bodies that contain the target's centre (`EnvironmentProbe` `ignoreBodiesAtOrigin`), after review found the first cut left a mid-wall Rift Phantom immune to basics (`PROVISIONAL-PLAYER-MELEE-LINE-OF-SIGHT`). R11: the Fighter driver plays `movement_ability` as a reel and poses the whole activation strike from its phase (`SIM-SPECIAL-POSE-THIRDS`). R12: caster-freeze attribution by the firing `ExecutionSerial`, carried by pooled shots and the deferred second stages, claimed monotonically (`F5-CASTER-FREEZE-ATTRIBUTION`). Also fixed: the Level 13 Mirror read every released pooled player shot (parked in the pool, still in `story_projectile`, owner slot kept) as live incoming fire; `PlaceholderProjectile.IsLive` now gates its scan.
- Deviations: G2 syncs on every node write, wider than a resync after the cast; R11 holds the cinematic on the strike pose (by analogy with Story's Mirror clone); R12 accepts a rare under-freeze. Noted, not changed: `TimeFreezeController`'s process-fallback parking also sweeps pool-parked shots (latent — nothing fires a Story projectile during a freeze); `MozartRequiemChord` tracks one chord at a time (unreachable at the authored cooldown).
- Test delta: +12 (`FeelPassStoryTests` +6, `ArenaEnemyAttackGeometryTests` +3, `FeelPassSimTests` +2, `MirrorParadoxTests` +1).

**fix-arena (A1–A5, G5).** Reviewed "fix first" (the ward's margin), then a fixup.
- A1: the gate sweep enumerates every door (`StoryGateRules.DoorGroup`) and `ForcefieldBarrier`; new columns over Level 4's CourtyardGate, Level 8's VaultDoor and Level 11's breastwork and both lane roofs (`P5-GATE-COLUMN-SWEEP`). A2: the Mirror clamp runs after the clone's own step (`MirrorParadoxController.LeashPhysicsPriority` = 100). A3: one reused ray query per executor; a strike clips once, a charge re-clips only when its owner moved. A4: one canonical arena edge per level (`BossArenaWestX = BossSpawnX − BossArenaApproachPixels`, through the arena overload of `BuildBossEncounter`). A5: the arena ward, made relative to the boss's engagement range by the fixup and extended to hitless statuses and Venom ticks (`ARENA-LEASH-EDGE-ANSWER`; the Mirror has none, `ARENA-MIRROR-NO-WARD`). G5: no standable surface wedged under a ceiling — Level 4's tower top step 5200/380 → 4880/380, Level 6's west roof 1000/700 → 1000/720, Level 11's battery-ridge step 2680/730 → 2680/750 (`LAYOUT-CRAWLSPACE-MOVES`).
- Deviations: scope went beyond the named gates (Level 8, Level 11's lane roofs) and moved geometry outside Level 4 — same-class bug fixes, against P5's "no layout changes beyond the gate fixes". `SecretCachePlacementTests` and `AbsenceBeatContentTests` now read `ArenaBounds` instead of hand-copied radii (no count change). After the fixup, Easy `fxward2` (Levels 2, 4, 5, 7, 11) all completed and Normal `fxwardn` (Levels 2, 5, 8, 9; Joan and Einstein) had no timeouts and every death inside an arena, but no run ever re-approached a boss from outside its arena (the runner has no start-position option), so that path is covered only by `BossArenaLeashTests`. Level 6 still collapsed on the timer, as on every earlier run.
- Test delta: +11 (`BossArenaLeashTests` +6, `ArenaGeometryContentTests` +4, `ArenaEnemyAttackGeometryTests` +1).

**fix-level (G1, G3, G4, G6, G8).** Reviewed "ship" (four minor findings, addressed before integration).
- G1: Pompeii's winch — plate mask 513 → 65 and a destroyed support wakes the resting weights (`FIX-L06-WINCH-DETECTION`). G3: moving-platform endpoints stand clear or flush; the Level 12 spire lift stops flush (scene 7050/1916, waypoint −1516) with `Level12Controller.Lifts` matched to the scene; Level 7's SkiffA west stop moved out of the gun pier; and a descending deck holds above a hero (`LIFT-STOP-RULE`, `PROVISIONAL-LIFT-HOLD-OVER-HERO`). G4: post-boss and exit beats wait for the screen, and Levels 5, 12 and 15 route their pre-boss triggers through `StartPreBossDialogue` (`PREBOSS-BEAT-AFTER-DEFEAT`). G6: two Level 9 Extractors and the `FeedRelayTower` moved (`FIX-L09-EXTRACTOR-SLIVERS`). G8: "enemies hit through closed doors" was not a bug — the Level 6 hits were Collapse Tremor debris (`env`) and the Level 3 hits predated the P1 leash — and a pin now covers both real doors; the unclipped AreaPulse is `VERIFY-AREAPULSE-WALLS`.
- Deviations: an interim change to `DialogueManager.StartSequence`'s return contract broke four suites and was reverted, so G4 lives entirely in `StoryLevelControllerBase`. The review's Level 14 finding (an interim move that turned the cargo lift's flush walk-on stop into a 32 px step on the critical path) was answered by allowing flush stops in the endpoint contract and leaving the Level 14 lift as authored; the review's Level 12 table and undisposed exclude-array findings were fixed. Bot runs on the fix tree: Levels 3, 9 and 12 complete; on Level 6 the pumice now reaches the east pan, but the bot stalls at the rockfall (bug 5).
- Test delta: +17 (`PostBossChainTests` 6, `LiftDescentPhysicsTests` 3, `Level06RockfallPhysicsTests` 2, `ArenaGeometryContentTests` +3, `ArenaEnemyAttackGeometryTests` +1, `Level12ContentTests` +1, `Level14ContentTests` +1).

**Integration (orchestrator).**
- `scripts/Combat/EnvironmentProbe.cs` merges fix-arena's reusable-query overload with fix-feel's `ignoreBodiesAtOrigin`; a reused query's `Exclude` list is emptied after each call, so it never carries a stale exclusion.
- `--seed=N` (`PlaytestRunner`): a non-zero seed holds neutral input for a seed-derived 5–60 frames before the bot takes control and appends `_s<N>` to the run name, so repeated runs desynchronize from the otherwise frame-identical world (seed 0 is the classic run).
- `tools/playtest/compare_runs.py <tag> ...`: one table per bot per tag pattern (completion, boss down, deaths, damage taken, standard-mob kill time, 2-second burst kills, Special-majority kills, elite kill time, mobs passed alive, hits after the pass).

Fix round: 2660 + 11 + 12 + 11 + 17 = **2711**, verified across five consecutive full runs on 2026-10-07.

### Game bugs the bots found

| # | Bug (as the bots reported it) | Disposition |
|---|---|---|
| 1 | Level 2 floor hole at x 8900–8960 (fall out of the world) | Fixed, round 1 P5: room 3's floor runs to 8960. |
| 2 | Level 2 Gate B hopped from the Extractor perch | Fixed, round 1 P5 (gate column); the audit was completed by fix-arena A1 (`P5-GATE-COLUMN-SWEEP`). |
| 3 | Bosses leave their arenas from level load (Levels 2, 5, 10, 12); the Level 2 boss hits through the battlement footing wall | Fixed, round 1 P1 (dormant, leashed arenas) and P2 (wall-clipped enemy hitboxes). |
| 4 | `InteractionArea` hears only raw Godot input events, not `InputManager` | Open. Harmless for a human player (the raw event exists); it matters only to scripted input sources, and the harness calls `InteractionArea.TryInteract` instead. |
| 5 | Level 6 hard blocker: the pumice stays asleep after the wedge breaks and the pans never see the weights (plate mask 513), so the rockfall never opens | Fixed, fix-level G1 (`FIX-L06-WINCH-DETECTION`). **Still open on the bot side:** once the pumice reaches the east pan (RightPan 2 of 3) the `play` bot does not stand on the pan as the missing counterweight; it idles at the rockfall (≈5352, 900) for minutes, gets through only by chance and still collapses on the timer. A `PlaytestRouteHints` step for Level 6 is the fix. |
| 6 | Stale `PlayerController.CurrentUltimateMeter` after an Ultimate's own `Consume` (Defy History could still fire; saves stored 100) | Fixed, fix-feel G2. |
| 7 | Level 12 spire lift drives a hero through the floor | Fixed, fix-level G3 (`LIFT-STOP-RULE`, `PROVISIONAL-LIFT-HOLD-OVER-HERO`). |
| 8 | Level 5 PreBoss fracture struck after the boss falls: the sealing anchor never arms, or `level_05.preboss` plays after `level_05.postboss` | Fixed, fix-level G4 (`PREBOSS-BEAT-AFTER-DEFEAT`). |
| 9 | Enemies hit through closed doors (Level 6 rockfall, Level 3 court door) | Not a bug (fix-level G8): Collapse Tremor debris and pre-P1 boss roaming. Residual: `VERIFY-AREAPULSE-WALLS`. |
| 10 | Level 9 watchtower Extractor leaves about 14 px of deck | Fixed, fix-level G6, with the radar-mast Extractor (`FIX-L09-EXTRACTOR-SLIVERS`). |
| 11 | Level 4 crawlspace: a one-way 62 px under the walkway | Fixed, fix-arena G5, with the same wedge on Levels 6 and 11 (`LAYOUT-CRAWLSPACE-MOVES`). |
| 12 | Swinging-rope bars are solid floor: heroes land on them, bump into them and ride them, and a release from behind the rope hits its own bar | **Open.** `PendulumAnchorTemplate.tscn` is still an `AnimatableBody2D` on `collision_layer = 64` (Environment) with a 16 × 320 rope shape, and no Level 7 or Level 10 instance overrides it; the bots model the bars as solid. |
| 13 | Level 10's rope galleries are unreliable (completion varied run to run on one build) | **Open**, a bot-model limitation made harder by bug 12 and the audience throws; Level 10 still collapses on the timer for the bots. |
| 14 | Story drop-through reads the double-tap of Down only in Blocking and Attacking | Fixed, fix-feel G7. The bots still tap from Block, which keeps working. |
| 15 | `EventBus.OnDialogueTriggered` is never raised for level sequences | **Open question, not a code defect:** `OnDialogueTriggered` is an inbound request channel — `DialogueManager` subscribes and starts the named sequence — and nothing raises it; the build has no "sequence started" notification. The runner polls `DialogueManager.ActiveDialogueID`. |
| 16 | Lincoln's low jump fails several routes | Held: jump forces are user-locked (§5). |

## 4. Results (after this pass)

Measured 2026-10-07 with identical bots and settings on two builds: **before** = `5d3ce25` plus only the harness (a detached worktree, `D:/Projects/ftt-wt/baseline`), **after** = the merged `main` working tree. Batches: the feel probe on all nine heroes (Level 2, Normal; tags `fbefore` / `fafter`); the combat matrix on the full-kit levels 5/7/8/9/11 × Joan/Lincoln/Einstein/Mozart × `spam`/`basics`/`play`, Normal, two seeds each (`--seed=0` and `--seed=1`; 40 runs per bot per build; tags `mbefore*` / `mafter*`); `bypass` on Levels 2–12 (Joan, Normal; `bbefore` / `bafter`); and `play` on Levels 2–12 (Joan, Easy; `ebefore` / `eafter`). Reproduce the tables with `python tools/playtest/compare_runs.py "mbefore*" "mafter*" bbefore bafter ebefore eafter` and `python tools/playtest/analyze_feel.py --table <feel csvs>`. Windowed captures of Joan's and Lincoln's probe (`capbefore` / `capafter`) were tiled with `tools/playtest/contact_sheet.gd` and looked at.

### 4.1 Feel probe (all nine heroes)

| Measure | Before | After | Reading |
|---|---|---|---|
| Three presses on a 12-frame rhythm → swings | 2 | **3** | F3 fixed: the third press is no longer dropped. |
| Longest single pose hold in a whiff | 41 frames | **17–19** | F1/F4: poses follow the frame windows; the chain hold plays idle/run. |
| Hitstop across a two-hit string on a mob | 6 (3 + 3) | **8** (4 + 4); Lincoln **10** (his hit 2 launches, +3) | F6 as designed. |
| Mob horizontal displacement from the string | 0 px | **5–19 px** | S1: knockback now carries. |
| Whiffed-string attack lock | 80–83 | 99 | Not slower per swing: the third swing now happens (the old 80 was a two-swing string). Equals the mashed-string lock (99–100). |
| Special 1 lock on a landed hit | 30–52 | 34–52 | F5 caster freeze adds the hit's hitstop once per cast. |
| Run / stop / jump | unchanged | unchanged | Movement was not touched (§5). |
| String + S1 + S2 on a 40-HP slasher (HP left) | 0 for Joan, Lincoln, Mozart; 4–33 others | **0 for Joan, Lincoln only**; 10–31 others | S2 proration + S3 poise: the trip into Armored Recovery is visible in the after capture (after Joan's Special 1 the slasher stands in the gold Armored Recovery glow at frames 564–566, where before it died at frame ~562; her Special 2 still finishes it). |

### 4.2 Combat matrix (Normal, full kit, 40 runs per bot per build)

| Bot | Completed | Deaths / run | Damage taken / run | Std-mob median frames first-hit→kill | Std mobs killed inside one 2 s burst | Elite median frames→kill | Mobs passed that later hit the bot |
|---|---|---|---|---|---|---|---|
| spam | 3 → **1** | 3.95 → 4.22 | 385 → 414 | 231 → **236** | 12% → **9%** | 1519 → 2267 | 36 → **75** |
| basics | 2 → **0** | 4.08 → 4.62 | 396 → 445 | 347 → **314** | 0% → 0% | 2169 → 4168 | 41 → **69** |
| play | 15 → **15** | 3.33 → 3.70 | 346 → 392 | 273 → 270 | 19% → 15% | 1566 → **1430** | 51 → **67** |

Easy `play`, Levels 2–12: 8 of 10 → 8 of 11 completed (the before batch lost one report), deaths 0.9 → **0.36**, damage taken 194 → 163, std-mob median kill 198 → **158** frames. `bypass`, Normal, Levels 2–12: still 0 of 11 (it fights only the boss), deaths 4.27 → 4.18, mobs passed that later hit it 41 → 50.

### 4.3 What the numbers say

- **Sluggishness (F1–F6): addressed where it was measurable.** The dropped third press, the 41-frame frozen pose, the 3-frame weightless hitstop and the zero-displacement hits are all gone in the probe for every hero. Movement ramps and gravity were not touched (held, §5), so run/stop/jump read the same.
- **Spam (S1–S5): the gap closed from both ends.** Spam's burst kills fell (12% → 9% of standard mobs in one 2 s burst; Special-majority kills 6% → 4%) while its median kill time held (231 → 236), and basics got faster (347 → 314) — the spam-to-basics ratio went from 1 : 1.50 to 1 : 1.33. String + both Specials still kills a fresh 40-HP slasher for the two heaviest kits (Joan, Lincoln); everyone else now leaves it standing and in its counterattack.
- **Bypass (P1–P5, P4): the free disengage is gone.** Mobs a bot ran past and that later hit it rose 36–51 → 67–75 per bot, and Level 5's Tech Enforcer, previously never engaged, now follows the bot and fights. Bosses fight in their arenas: Level 5 deaths moved from x 7000–8999 (the Tidal Overseer roaming the corridor) to x 9000–10999 (inside its arena), and Levels 8 and 11 shifted the same way.
- **Normal got harder for the weaker bots.** Deaths rose 0.3–0.5 per run and `spam`/`basics` completions fell to 1/0. The measured causes: bosses are now fought in full in their arenas (Tidal Overseer damage 1173 → 2386, Level 5 deaths moved there); led enemy shots land more often on bots that never dodge (siege cannon 998 → 1867, laser rifle infantry 689 → 981, plasma spear ward 667 → 967); and chasing mobs join fights. The competent `play` bot held its 15 completions and got faster on most elites (Chrono-Warden 1222 → 1079, Cyber Cavalry Commander 2360 → 1472). On Easy everything got easier.
- **Open finding — string-only elite fights are ~2× slower** (`basics`: Chrono-Warden median 1816 → 3300 frames; Neural Mech Walker 1727 → 2382; the Level 5 Eraser rows are not comparable — it leaves the level with 50–120 of its 190 HP lost). Not diagnosed. Candidates: S1 knockback carrying the elite out of reach after each finisher, the M1 wall line-of-sight check, and elites' P4 ledge/wall stops. A probe against the Chrono-Warden cannot isolate it (its reactive Phase Skip dodges the probe's string on both builds). Next step: an ablation batch (one build per candidate toggled off, Level 9 `basics`).

### 4.4 Not measured

Human play. Fighter Mode feel (the sim changes F3/F6/R11 are pinned by tests, not measured by a bot). Hard difficulty. Levels 6 and 10 for completion (the bots' route issues on those levels are bot limits, §3 bug table). Every visual change (posing, damage numbers, ward flash, gate columns) beyond the two contact sheets.

## 5. Held for a user ruling — measured, not changed

- **Movement ramps** (14-frame run accel, 12-frame decel, −15% run, −20% jump): user-locked 2026-08-10. Probe: 15 frames to top speed, 13 to stop, ~50 px stop slide, ~26-frame reversal.
- **Story gravity H-7** (18 / 1.8 / 2.5 / 10 u/s vs design 30 / 2.5 / 3.5 / 20): "do not close opportunistically". Joan's full jump is airborne 42–43 frames.
- **Ground mobs jumping to platforms**: `design-godot.md:2984` says standard ground mobs do not jump (one deliberate jumper per act). Not changed.
- **Solid Extractor / generator bodies** (Environment layer) act as step stools; AGENTS.md says strike surfaces are never stood on.
