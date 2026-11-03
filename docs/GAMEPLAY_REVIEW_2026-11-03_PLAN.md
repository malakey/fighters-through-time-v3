# Gameplay review package (2026-11-03)

Status: **approved 2026-11-03 (Build the whole plan); in progress on `gr/integ`. Nothing is pushed or merged to `main` without the user.** Baseline `main` `43d54db6`: 4028 tests, save schema v8, protocol v5, highest Klotho component 323. Prefix `GR-`.

## §0 What this package does, and the authority

From the gameplay review (570 bot runs, 6,804 CPU matches) the user made 13 rounds of rulings. This package builds all of them:
- **Crowds:** slots 2/3/4, swarm waves of a new fodder mob, three soft-counter traits, hazards that hit launched or thrown mobs.
- **Rewards and pacing:** a level rank paying dust on top of F05, nine 375-dust capstones, a schedule shifted one level earlier, Momentum regen 9 s, a Normal/Easy target curve.
- **Levels:** traversal stretches carved out of locks, a shortcut-only optional route per level, six boss arena changes.
- **Fighter:** a running attack in both modes, Globe and Hakata pits, a Holodeck training room, the Hard CPU meter rule.
- **Off or added:** Time Freeze switched off with its code kept, a small audible hit-sound set, the matrix tool kept as a gated test, ledger rows for first-slot win rate and Level 6.

**Authority.** `docs/recon-2026-11-03/DECISIONS_gameplay_review.md` wins over this plan; this plan wins over the dossiers (`A_time_freeze`, `B_encounters`, `C_levels_arenas`, `D_progression`, `E_fighter_audio`). If this plan and a decision disagree, stop and report. No ruling is reopened here.

**The honest risk.** This stacks a lot of unjudged change and the human session comes last (ruling 11). The bot already completes only 36 % on Normal and 0 of 135 on Hard. Slots, swarms, traits and the loss of Time Freeze all press one way; bosses, Hard and hero balance stay untouched. §5 therefore measures at every phase and **reports**, then keeps building (the user's Q2 ruling); it never tunes silently.

**Dossier corrections found in the spot check** (this plan's text is the corrected one):
1. D's speed formula `(integrity − 50) / 40` is not difficulty-normalised, against 3a+5: a par run reads 66.7 / 55.6 / 44.4 % on Easy / Normal / Hard. §1.9 fixes it.
2. B calls ranged mobs poor Anti-air fits, yet both its carriers are projectile primaries (`plasma_spear_ward` `energy_javelin`, `holo_page` `holo_dagger`). §1.12 keeps the spear ward knowingly.
3. E says Story grab cues are "outside the eight"; grab is one of the eight. GR-AUDIO adds the Story raise.
4. A's "`RemappableActions` static readonly initialised from the flag, plus an override seam" cannot work per test: the list is read once at boot (`InputManager.cs:820`). §1.3 removes the entry outright.
5. D's "strict superset for every history" fails for a history with no `level_00_tutorial` (old S1 came from Level 2, new S1 from Level 0). Real saves carry the saved key, so no derivation is needed, but fixtures that hand-build `CompletedLevels` will move.
6. E's Hard Mirror note is superseded by 9d+; `BossOverride(Hard)` returns `Hard` itself (`FighterCpuController.cs`, `BossOverride`), so a separate band is required (§2 GR-CPU).

Confirmed by reading code: `AttackFlags` 1024 unused; the `<= 0` sentinel sites plus `MoveListScreen.cs:378` `> 0`; `LevelNumberOf` returns −1 for unrecognised IDs, so a 0 milestone cannot be granted by a stray or retired ID; `GravityFieldZone` already cycles and its 0–4 export fits 0.15 / 2.5; `MaxWarmCombatants` 30 with warm 18 + 6 on L15, so +6 lands exactly on 30; `FinalGateIntegrity` is −1 until PreBoss locks.

## §1 Standing decisions (*P* = agent-chosen, provisional, recorded in the ledger)

1. **Godot use and worktrees.** Every Godot launch holds `D:/Projects/.ftt-gdunit-lock` (drain `testhost` and `*_console` first, never the user's editor). All merge builds and full-suite runs happen in the detached worktree `D:/Projects/ftt-wt/gr-integ`, so an editor open on the main checkout is undisturbed. Agents work in `D:/Projects/ftt-wt/gr-<ws>` on `gr/<ws>`, run filtered suites, never merge, never push, never touch `.godot/`.
2. **"Story leads until online exists" (ruling 15).** Add under Core design contracts in AGENTS.md: *"Until online exists Story leads: a shared rule is tuned for Story feel first and Fighter adapts, or the difference is recorded in the ledger as a deliberate exception. An ability must still make sense in both modes, and Fighter stays competitively normalized."* It amends how shared numbers are chosen, not the pillar; Story progression still never reaches the sim.
3. **Time Freeze scope (10b++).** Dormant: the controller node, its input, the HUD indicator, the Level 0 lesson, the first-use tooltip. **Live:** `TimeFreezeController`'s statics, `IStoryTimeFreezable` (31 files), the thaw cue, the `AudioSnapshot.TimeFreeze` ladder and the freeze sweeps, because the Post-Landing Hold and boss T01b use them (`ChronalRewindManager.cs:580…867`). One rule, `FTT.Core.TimeFreezeRules.Enabled` (false, with an `internal static bool?` test override), read by the bootstrapper, tooltip attach, `TutorialCalibration`, Level 0 and `StoryHUD`. `gameplay_time_freeze` stays in `project.godot` with `"events": []`; its entry leaves `RemappableActions` and `DirectActions` unconditionally, so a re-enable is one reviewed edit. R and L3 stay unassigned. `TimeFreezeCooldownSeconds` stays dead save data, unclamped. No catalog row or key is deleted.
4. **Momentum unit.** `MomentumUnitsPerPip` **5400**, `MomentumRegenFramesPerPip` 540, `MomentumRegenUnitsPerFrame` 10 (unchanged), gains 270 / 270 / 1080 (exactly 0.05 / 0.05 / 0.2 pip), max 21,600, start 10,800. Everything derives from these; `LingeringKitRules` already reads them. Story saves pips, so no save change. Every Fighter hash moves.
5. **`NotGated = -1`.** `LegacyUnlockSchedule.MilestoneLevelFor` returns −1 for ungated slots; the sites become `< 0` / `>= 0`. Table: S1 **0**, Movement **1**, S2 **2**, Ultimate **3** (completion of that level grants the slot). Level 0's existing results beat shows S1's restoration.
6. **No schedule derivation.** Keys and history union in `UnlockedSlotsFor`; a test pins that saved keys survive and a ledger line declares the no-op. This corrects 3b+'s "will need a declared derivation", as the dossier established.
7. **Save, protocol, components.** Schema stays **v8**: additive fields with initializers, no bump, no derivation, all engine-free: `StoryAttemptState.RankBaseDamage`, `RankHits`; `StorySaveData.RankByLevel`, `RankDustPaidByLevel`; `StoryCompletionTransaction.RankBonusDust`; `GlobalSaveData.PlaceholderCuesMuted`. `RatingByLevel` stays dead. Protocol stays **v5** (no new bit; LAN stays de-scoped, the 2026-10-16 precedent). **No component ID is allocated**; the run attack is `AttackFlags` 1024 on 305.
8. **One canonical value.**

   | Value | Home |
   |---|---|
   | Slots | `StoryEngagementRules` |
   | Momentum, run attack frames | `BasicComboRules` regions |
   | Rank | new `FTT.Core.LevelRankRules` (pure) |
   | Traits | new `FTT.Combat.EnemyTraitRules` (pure), Catcher constants in `StoryEnemyCombatRules` |
   | Hazard impact | new `FTT.Combat.HazardMobRules` (pure) |
   | Capstone price | the nine `.tres` |
   | Target curve | `tools/playtest/target_curve.json`; the ledger names the file, never the numbers |
   | CPU reserve | `CpuBandTuning`, code-owned, never a resource |

9. **Rank (*P* except the ruled dust and sources).** Levels 1–15; Level 0 pays nothing. Tally on the attempt: discrete Hit and Hazard `DamageTaken` observations only (ticks and Status excluded), divided by `StoryDifficultyTuning.GetEnemyDamageMultiplier`.
   - `bars = baseDamage / maxHP`; `damageScore = 100·clamp(1 − bars / BudgetBars)`, `BudgetBars` 1.5.
   - `I` = `FinalGateIntegrity` if ≥ 0, else `LastLevelIntegrityPercent` (the N05 fallback). **Corrected:** `parUse = (100 − I)/100 × DifficultyTimeMultiplier × ProvisionalParScale`, which is 1.0 at authored par on every difficulty; `speedScore = 100·clamp((1.125 − parUse) / 0.9)`, equal to the dossier's band on Normal. Read both factors from `TimelineIntegrityRules`, never copy them.
   - `points = 0.6·damage + 0.4·speed`; Level 1 (no clock, 3c+) is damage only.
   - Letters S ≥ 90, A ≥ 75, B ≥ 55, C ≥ 35, else D.
   - **Dust (3a+5), an authored table:** Normal and Hard 50 / 35 / 20 / 8 / 0; Easy 25 / 18 / 10 / 4 / 0.
   - Applied in `OnLevelComplete` **after** `ApplyIntegrityTierBonus` (no compounding, F05) and before `CommitCompletionTransaction`; pays `max(0, new − paid)`. Resume, Collapse, Snap and quit keep the tally; a Restart mints a fresh attempt and resets it with the wallet. Only the campaign hero feeds it, never the Mirror clone.
10. **F05 stays exact.** The 720 / 1,000 ledger and every level total are untouched. Rank is a separate line; maxima become 1,537 / 1,844 with tier. Fodder has weight 0.
11. **Fodder (*P*).** ID `chrono_swarmling` (display name "Chrono Swarmling"), Standard tier, grabbable, `standard_enemy` pool. HP 10, MoveSpeed 5.2, Weight 0.6, AttackDamage 5, AttackCooldown 2.5, 12 / 6 / 14 scalar melee, ChronalDustDrop 1 (advisory; roster test floor), `EnemyData.DustWeightZero` true (1b+). Placeholder look: `chrono_slasher_frames` with a tint and a small `EnemyData.SpriteScaleFactor` applied in the normalizer. `RewardAllocator` needs no change (weight 0 stays 0, no pickup).
    Placement (1b++): one row per level, two only on the largest locks, none on L0, 12, 13, 14; rows 6–10 authored at the Hard truth (default 8, 10 on a lock ≥ 2,800 px after carving; Easy's prefix gives 5–7); `WhenAliveAtMost(2)` trigger. Warm `standard_enemy` +6 on swarm levels, cap stays 30. Two-row candidates: L9 beach, L11 line.
12. **Traits (*P*; Standard mobs only, soft, data-driven `EnemyData.Trait`).**

    | Trait | Carrier | Rule | Answer |
    |---|---|---|---|
    | Armoured | `shock_shield_legionnaire` (L6); replaces its `FrontalDamageReduction` | ×0.5 on Basic and Special until `LaunchThrown`; gold glow while intact; Ultimate, bowling, wall impact and hazards unreduced | Throw |
    | Catcher | `cyber_guard` (L1, where S1 now exists) | Basic string hits 1–2 deal damage but no stun or knockback; the second answers with a blockable counter on a 12-frame diamond telegraph, 150-frame cooldown | Throw or Special |
    | Anti-air | `plasma_spear_ward` (L8) | Hero Airborne, descending, in reach (never Rolling or a movement ability): rising strike, diamond glyph; 40-frame recovery on a miss | Grounded pressure |

    A content sweep fails if a required objective sits behind a trait mob. `FrontalDamageReduction` is not reused: it sits on the private path a throw takes.
13. **Hazards vs mobs (*P* numbers).** `IsHazardVulnerable = (_thrownFlight || _wallBounceArmed || _finisherFlight) && !dormant && alive`. A finisher floor slide counts until the carry ends; a Defy push does not. Standards 30 or die, fodder die, elites 25 % of MaxHP and never die to one hazard, bosses never. Once per launch per source; no stun, hitstop, meter, Momentum, Rally or D1a; world-held mobs are refused. Contact, cyclic, extractor pulse, flood and lava qualify; searchlight and tremor debris stay hero-only. Masks gain the Enemy bit only (no new layer); kills run under `PhysicsCallbackGuard`. A kill pays ledger dust as any kill, and survives a rewind.
14. **Traversal (7a, 7b).** Every lock stays, with the same ID and wave table apart from the swarm row. **Carve by default:** shrink the widest lock to 1,700–2,000 px (rule range 1,280–3,200) and use the freed span or an existing gap; insert width only for a stretch over 1,300 px. Each stretch: own `RoomTransitionTrigger` and camera rect; base-kit pieces only (no new verb, **no new ropes**); gaps ≤ 280 px and rises ≤ 240 px per hop; non-lethal trenches with walkable exits (`VERIFY-STORY-PITS`); a rope-free path to every Middle and PreBoss anchor (`AnchorApproachContentTests`). L6 stays clear of 5390–5660. L0 is excluded.
    **Par rule:** `par_new = ceil30(par_old + 1.25 × slowest-hero median stretch seconds, play bot, Normal)`, minimum +30 per stretch. Authors apply +30; the orchestrator's measured pass (§3) corrects upward. F11 fractions are untouched and the clock stays a fail state (ruling 6).
15. **Optional routes (7c, 7c+).** Shortcut only. Every hero reaches the reward on a harder base-kit line; each level offers a horizontal-burst shortcut and a rise-or-glide shortcut. The reward is the existing secret or an Extractor share: **no new dust**. L1 is a skill route holding an Extractor (no movement ability yet, no secret).
16. **Isolation.** Nothing under `scripts/FighterSim/` names rank, swarm, traits, hazard rules or `TimeFreezeRules`. No float there; a `KLOTHO_DET002` warning is a bug. Fighter gains only: regen, the run attack, stage geometry, the CPU rule, presentation cues.
17. **No tuning.** Bosses (4a), Hard (4b), hero balance (8a), Fighter balance (8b) and Levels 1–4 (Q3) get no number changes. The curve flags only.
18. **Conventions.** One `[TestSuite]` per file; pure suites touch no engine API; restore session, pause and `ScriptedHPFloor` in `finally`; a test must be seen failing. Theme changes go through `tools/ui_theme/build_ftt_theme.py`, never `ftt_theme.tres`. Quote commas in `en.csv`; each workstream adds one block at the end; **only the orchestrator commits `en.en.translation`**, after `--import`, in `gr-integ`. Handoff `docs/handoffs/GR_<WS>.md` per workstream; commits end with the attribution line. `git checkout -- assets` before each commit.
19. **Nobody plays the build.** Every handoff says so; every acceptance claim is a headless suite, a capture or a bot run.

## §2 Workstreams

### Phase A: rule foundations (parallel; merge order §3)

**GR-MOM** (10a, 10a+). Unit change and regen per §1.4. Files: `BasicComboRules.cs` Momentum region; comments in `CpuDecisionObservation`, `FighterSimulationComponents`, `FighterSimulationSystems`, `LingeringKitRules`, `FighterHudModel`; ~16 test files, replacing literals with constants; `RollbackReadinessTests` spam thresholds.

**GR-TF** (10b, 10b+, 10b++). Per §1.3. Level 0 becomes eight lessons: `RegisterRewindComplete` goes to Done when off; the enum member and methods stay. Files: new `scripts/Core/TimeFreezeRules.cs`; `StorySceneBootstrapper`, `FirstUseTooltipPresenter`, `TutorialCalibration`, `Level00Controller`, `InputManager`, `InputBindings`, `StoryHUD.cs` (hide the indicator; the slab stays 484 px), `project.godot`.

**GR-SCHED** (3b, 3b+). Per §1.5–6. One tooltip `special_momentum` for S1 on Level 1 (count 9 → 10). Files: `LegacyUnlockSchedule`, `PlayerController.cs:418`, `MoveListScreen.cs:378`, `FirstUseTooltips`, `LevelResultsPanel.cs:342` comment; about 25 test files.

**GR-CROWD** (1a, 1a+, 1b, 1b+, 4c, 4c+). Slot tables (Total 2/3/4, PerSide 1/2/2). Fodder resource, `content_manifest.csv` row, `EnemyData` flags, `LevelRewardDirectory.WeightForEnemy` branch, normalizer factor, `SwarmWaveContentTests`. Telemetry: `slot_idle_with_waiters_frames`, `melee_candidates`, per-swarm records, plus runner options `--slot-table=legacy|current` and `--swarm=on|off` for same-build A/B (the `--story-meter-scale` precedent; diagnostics only, the rule constants stay single). `tools/playtest/target_curve.json` (Easy 0.90; Normal `0.75 − (L−1)·0.25/14`; Hard null) and `compare_runs.py --curve` (80 % Wilson flags; gap notes on L6, 7, 10; Hard `-`). No level placement here.

**GR-RANK** (3a, 3a+, 3a++, 3a+5, 3c, 3c+). Per §1.9–10. New `LevelRankRules`, `LevelRankTracker` (attached by the bootstrapper, covers Florence). `StoryManager`, `SaveManager`, `StoryAttemptState`, `LevelResultsPanel` (`RankLine` after `DustTierBonus`), two caller hunks (`StoryLevelControllerBase.cs:2127`, `Level01Controller.cs:1334`). Rewrite `DustEconomyTests:289-316`. Keys `results_rank`, `results_rank_dust`, `results_rank_detail`. The Integrity clock is otherwise unchanged.

**GR-HAZ** (7d). Per §1.13. New `HazardMobRules`, `EnemyController.Hazard.cs`; `StoryCyclicHazard`, `StageHazard`, `ChronalExtractor`, `RisingWaterZone`, `EscapeSequenceController`, one `BuildHazardSpikes` hunk; `CollisionLayerTests`. Telemetry `hazard_kills`, `thrown_kills`. Bots never grab, so thrown kills will read near zero (§5).

**GR-TRAITS** (2b, 2b+). Per §1.12. New `EnemyTraitRules`, `EnemyController.Traits.cs`; `EnemyData.Trait`; three `.tres`; hook lines in `ResolveIntakeScale`, `OnHurtboxHit`, `BeginHeld`, `LaunchThrown`; trait counters.

**GR-RUN** (9b, 15). `SelectAttackVariant(..., running = false)` returns new `VariantRunAttack = 4`; priority Up > Down-Air > air strike > run attack > chain; the grab chord still wins; D3 cancels pass false. Story predicate: `Running` state and speed ≥ 60 % of max; the sim approximates with grounded, MoveX toward facing and velocity (the recorded exception under ruling 15: the sim has no Running state). *P* frames: startup template 9, Joan 7, Lincoln 11, Einstein and Shakespeare 10, active 6, recovery 22; 1.2× damage, hitstun 30, non-launching, knockback ×1.5, 0.5 u lunge, reach +0.4 u; no chain, no buffer; Momentum +0.05. Pose `basic_attack_2`. Files: `BasicComboRules` new region, `FighterBasicAttackRules` (`FlagRunAttack = 1024`, in `VariantMask`), `FighterSimulationSystems`, `PlayerController`, `StoryEnemyCombatRules` (S4, S5, S2 sets), `StringHitboxGeometryTests`, `movelist_*` row. CPU unchanged.

### Phase B1: pilot and independent streams (parallel)

**GR-PILOT** (7a, 7b, 7c, 7c+; 14 Duke; 1b++ on L2). First: `StoryLevelControllerBase.Stretches.cs` (stretch, trench and route helpers), shared `TraversalStretchContentTests`, per-hero `OptionalRouteReachTests` on `StoryAirPhysics`, and measurement cases for what C left unmeasured (Einstein and Tesla pass-through, Joan and Leonardo rise). Then Level 2 exactly as C §2's worked example (vanguard lock 300–2000, Rampart run 2020–3200, Parapet route), par 360 → 390 or as measured, a swarm row, and Duke's two steam edges on phase 1. Its result is the template.

**GR-CAP** (3a+++, 3a++++). One tenth node per hero: Major, `Tier = 4`, 375, `PrerequisiteMode` All of two Majors, total 1,350; a Story perk with a reader. Re-flow `LayoutPosition`; `ResonanceTierMark` reuses tier 3 art. Files: nine `resources/Resonance/*_grid.tres`, kit scripts, about 20 keys, pins (`GridTotalCost`, node and tier counts, `TubmanContentTests:109`, `ResonanceGridFitTests`). **Effects are suggestions for the user (Q1):**

| Hero | Capstone | Effect |
|---|---|---|
| Joan | Banner of Orleans | Righteous Smite's wave leaves a short radiant strip |
| Einstein | Unified Field | Warp's arrival detonates his Rift |
| Leonardo | Codex Atlanticus | Synced bolts pierce; each construct +1 charge |
| Lincoln | Emancipation | Rail Charge leaves a ground wave behind |
| Cleopatra | Dynasty's Curse | A Venom kill spreads Venom to nearby mobs |
| Tesla | World System | The discharge chains to charged coils and refunds a pip |
| Shakespeare | Curtain Call | Midsummer Gust leaves a windbox that also arms 7d vulnerability |
| Mozart | Symphony No. 41 | Different Specials within 6 s stack a damage lane |
| Tubman | Freedom Line | A Foresight counter refunds a pip and grants a Resonance Barrier |

**GR-STAGE** (9a, 9a+, 9a++). Globe: centred pit [−1.5, 1.5], segments ±5.25 half 3.75, respawn (−4, 3). Hakata (`FighterStage_Berlin`): gap [7, 10], segment centre −1.5 half 8.5; platform and orb from 6.5 to 5.5. Files: `FighterStageGeometry`, two `.tscn` (split colliders), catalog `.tres` `IsOpenStage`, per-stage suites, `CpuRecoveryMatrixTests` +6. Grep every consumer treating these as flat first. Previews stay placeholder plates.

**GR-CPU** (9d, 9d+; 8b tool). `DefyReserveInfluence` 70 and `DefyNearDeathHPPercent` 25 on Hard, a `DefyAllowedByReserve` predicate before the reflex and in the corner branch; the A02 wind-up always passes. New band `HardBoss = Hard with { reserve off }`, so `BossOverride(Hard)` keeps today's clone. The matrix tool moves to `tests/Tools/FighterMatrixMeasureTests.cs` and `tools/fighter_matrix/` (config example, `analyse.py`, README), a no-op unless `FTT_MATRIX` is set. No balance change.

**GR-TRAIN** (9c). A third Holodeck console entry, hub console only (hub only, ruled). `scenes/ui/TrainingRoom.tscn`, `scripts/UI/TrainingRoom*.cs`, `TrainingDummySource : IPlayerInputSource`, reusing `CalibrationDrillRunner.BuildSandbox`'s pattern. Session-only settings: Block, Jump, hitstun response; frame and damage readouts from read-only getters; `FighterMatchOrigin.Training` appended; exits through `FighterFlowRoutes`. Glass kit, 18 px floor, `MenuSafeAreaFit`, `MenuLayoutBoundsTests` row. About 14 keys. Input source cleared and pause handed back in `_ExitTree`.

**GR-AUDIO** (13). New rows `hit_basic`, `block`, `perfect_block`, `swing_whiff`; reused `grab_catch`, `ko_stinger`, `defy_burst`, `ultimate_activation_windup` become audible. Story gains a `grab_catch` raise. Per-asset `RENDER_SILENT` in `tools/generate_placeholder_audio.py` (original synthesis, peak ≤ 0.3, no third-party material); `CoreMechanicCueEntry.VolumeDb` (new rows −12); per-cue throttle; `IsForwardCounterStep` on Fighter edges. `PlaceholderCuesMuted` and a Settings row. All other streams stay silent; `resources/Audio/README.md` records the amendment.
### Phase B2: level fan-out (parallel, after the pilot gate; additive)

Each agent owns its controllers, scenes, `level_NN_rewards.tres`, `level_NN_pool_config.tres` and `LevelNNContentTests`, and for each level delivers a stretch, an optional route, a swarm row where §1.11 allows, and par per §1.14. Arenas follow `PhaseEntered`: an idempotent public method, built disabled, deferred out of the physics flush, inside `BossArenaBounds`, frozen while `IsWorldHeld`; retire or retune the stand-in in `BossPhaseRuleMatrixTests`.

| WS (7a–7c+, 1b++ on its levels) | Levels | Arenas (14, 14+) | Notes |
|---|---|---|---|
| **GR-LV1** | 1, 3 | none | L1 standalone: table, `florence_pool_config`, skill route; workshop swarm as the 7d showcase |
| **GR-LV2** | 4, 5, 6 | Vulcan: 4–5 cyclic vents, debris ×2 | L4 gap holds a puzzle; L6 clear of 5390–5660 |
| **GR-LV3** | 7, 8, 9 | Admiral: sloped ≤ 8° collision or raised ramp joints, rolling cannonballs with Area2D hurt triggers; General: 2–3 `SearchlightZone` delayed-strike beams | L7's existing stretch is the model; no new ropes |
| **GR-LV4** | 10, 11, 12 | Siege Cannon: `rail_warp` after each volley, fences destructible on phase 1; Overseer P2: cycling `GravityFieldZone` 0.15 / 2.5, 1 s telegraph, no inversion; P3: telegraphed vent gusts toward the edges via knockback hazards (*P*, no new modifier) | L10 swarm away from the stillness area; L12 no swarm |
| **GR-LV5** | 13, 14, 15 | none | Swarm on L15 only |

### Phase C: closeout

**GR-DOCS** (every ledger and document consequence, incl. 2a, 5a, 5b, 9e, 12, 15). AGENTS, CLAUDE, ledger, design master and mirror, contracts (§6). The orchestrator runs the gates (§5), the merged translation, three green full runs, §9 and §10.

### File-ownership matrix (shared files; everything else has one owner)

| File | Who, and how |
|---|---|
| `StoryLevelControllerBase.cs` | Phase A: GR-RANK and GR-HAZ one hunk each. Then **frozen**; `.Stretches.cs` is GR-PILOT's; any B2 need goes through the orchestrator |
| `Level00Controller` / `Level01Controller` / other level controllers | GR-TF / GR-RANK one hunk, then GR-LV1 / GR-PILOT and GR-LV* exclusively |
| `reward_manifests/*`, `level_pool_configs/*` | Only the level's owner; no Phase A edit |
| `content_manifest.csv` | GR-CROWD one row; nobody else |
| `en.csv` | Per-WS block at the end; orchestrator resolves the union and alone commits `en.en.translation` |
| `BasicComboRules.cs` | GR-MOM Momentum region; GR-RUN new region and startup table. No B-phase edit |
| `StoryEnemyCombatRules.cs` | GR-TRAITS (Catcher), GR-RUN (S4, S5, S2 sets), distinct regions |
| `project.godot` | GR-TF only |
| `EnemyController.cs` | Call lines only: GR-HAZ, GR-TRAITS, GR-RUN; bodies in partials |
| `EnemyData.cs` | GR-CROWD and GR-TRAITS, appended exports |
| `PlayerController.cs` | GR-SCHED :418; GR-RUN swing methods; GR-AUDIO call lines via `BlockFeedback` |
| `FighterSimulationDriver.cs` | GR-RUN pose; GR-AUDIO call lines |
| `SaveManager.cs` | GR-RANK story fields; GR-AUDIO global field |
| `PlaytestTelemetry` / `compare_runs.py` | GR-CROWD owns; GR-HAZ, GR-TRAITS add counters after it merges |
| `FirstUseTooltips.cs` / `Presenter` | GR-SCHED / GR-TF |
| Shared tests (`ArenaGeometryContentTests`, `EncounterLockContentTests`, `AnchorApproachContentTests`, `SecretCachePlacementTests`, `BossPhaseRuleMatrixTests`, `ScenePoolConfigTests`) | Method and row owners; additive rows; orchestrator resolves |
| `AGENTS.md`, `CLAUDE.md`, ledger, `design-godot.md` | GR-DOCS only |

## §3 Merge order, dependencies, sizing

| Phase | Agents | Runs parallel? | Merge order | Then |
|---|---|---|---|---|
| 0 | orchestrator | n/a | Plan and recon commit; create `gr-integ` | |
| A | 8 | Yes; start four, then four as the lock queue allows | MOM → TF → SCHED → CROWD → RANK → HAZ → TRAITS → RUN | Full run per merge against §4; Codex; **gate A** |
| B1 | 6 | Yes; disjoint files | CPU → STAGE → AUDIO → TRAIN → CAP → PILOT | Codex; **gate B** (Level 2 first).  |
| B2 | 5 | Yes; disjoint controllers | LV1 → LV2 → LV3 → LV4 → LV5 | Measured par pass; Codex; **gate C** |
| C | 1 + orchestrator | Docs after code | | Three green runs; Codex; prune worktrees |

**Not parallel, on purpose:** GR-RUN merges last, because its fixture retiming must see Momentum and traits. No B2 agent starts before the pilot gate: the carve idiom, helper API, par rule and route tests must be proven on one level. Bot runs and par measurement are serial under the one lock, so the orchestrator batches them rather than each agent.

**Dependencies:** GR-CROWD precedes every swarm row. GR-HAZ precedes the L1 showcase. GR-MOM's constants precede GR-CAP's pip refunds. GR-SCHED precedes the L1–4 bot read.

**Size:** 20 agents plus fix steps, 4 build phases, 4 Codex rounds. About 30–35 agent sessions on levels alone (dossier C). Expect fix rounds; the meter package ran +17 % over its declared delta.

## §4 Test reconciliation (from 4028; `[TestCase]` count)

| WS | New | In place (0 delta) | Delta | Running |
|---|---|---|---|---|
| GR-MOM | 2 pins | ~16 files of literals | +2 | 4030 |
| GR-TF | off-state suite 8 | `GamepadDefaultBindingTests` rewrite; `TimeFreezeTests`, `TutorialCalibrationTests`, HUD model cases forced on | +8 | 4038 |
| GR-SCHED | 4 + 1 tooltip | `LegacyUnlockScheduleTests`, `MoveListDormantTests`, ~35 assertions | +5 | 4043 |
| GR-CROWD | slots 2, swarm 12, telemetry 3 | `StoryEngagementRulesTests`, `PlaytestEncounterTelemetryTests:435` | +17 | 4060 |
| GR-RANK | `LevelRankRulesTests` 14, `StoryLevelRankTests` 8, tracker 3, panel 3, save 2 | `DustEconomyTests`, `LevelResultsStatsTests`, `CompletionOnlyBankingTests`, `StoryLevelControllerBaseTests` | +30 | 4090 |
| GR-HAZ | rules 6, per hazard 8, matrix 6, dust and rewind 3 | `CollisionLayerTests` | +23 | 4113 |
| GR-TRAITS | rules 8, controller 12, sweep 3 | legionnaire pins | +23 | 4136 |
| GR-RUN | rules 6, sim 8, Story 8, geometry 1, profile 2 | 10–20 fixtures that hold a direction and press Attack | +25 | 4161 |
| GR-CPU | policy 5, matrix tool 1 | `CpuDefyPolicyTests` Hard pins (lower fixture influence) | +6 | 4167 |
| GR-STAGE | per stage 19, CPU matrix 6 | `FighterStageCatalogTests` | +25 | 4192 |
| GR-AUDIO | 10 | `PlaceholderAudioKitTests`, `CoreMechanicCueCatalogTests` | +10 | 4202 |
| GR-TRAIN | 14 | `HolodeckConsoleTests` | +14 | 4216 |
| GR-CAP | kit 9, content and layout 5 | grid pins, `ResonanceGridSceneTests` | +14 | 4230 |
| GR-PILOT | shared 5, reach 15, L2 3, Duke 4 | `Level02ContentTests` | +27 | 4257 |
| GR-LV1 | 2 levels × 3 | L1, L3 suites | +6 | 4263 |
| GR-LV2 | 9 + Vulcan 4 | L4–6 suites | +13 | 4276 |
| GR-LV3 | 9 + 8 | L7–9 suites | +17 | 4293 |
| GR-LV4 | 9 + 8 | L10–12 suites | +17 | 4310 |
| GR-LV5 | 9 | L13–15 suites | +9 | **4319** |

**Declared total: +291 = 4319.** Fix rounds add to it; every delta is reconciled case by case. The matrix case is a no-op without its env var. Reading `Total:` against the running figure is mandatory (signatures 1, 2, 4, 5, 7, 8).

## §5 Validation gates

### Bot batches
Reference: `an11n`, `an11d`, `an11b`, `an11s` at `43d54db6`. Each gate runs the same commands as `D:/Projects/ftt-scratch/analysis-2026-11-02/run_batch.ps1`, `run_batch2.ps1` and `run_batch3.ps1` (570 runs), from `gr-integ` under the lock, with tags swapped:

| Gate | When | Tags | Read |
|---|---|---|---|
| A | After Phase A | `gra11n`, `gra11d`, `gra11b`, `gra11s` | Rule changes alone, on unchanged levels |
| A/B | Same commit | `gra11lg` Normal, seed 0, `--slot-table=legacy --swarm=off` | Same-build control for the slot counters |
| B | After B1 | Level 2 only: nine heroes, Easy and Normal, seeds 0–1, `grbL2*` | Pilot stretch; trace with `--trace-every=3 --nav-dump=1` |
| C | After B2 | `grc11n`, `grc11d`, `grc11b`, `grc11s`, plus `grc11ns` with `--swarm=off` | The whole package |

Compare with `python tools/playtest/compare_runs.py "an11*" "gra11*" "grc11*"`. Batches from before and after are not comparable in absolute terms; CLAUDE.md records this.

**Report rule (Q2, ruled: report and keep building).** The orchestrator reports to the user and carries on, without tuning, if a gate reads Normal under 20 % completion (now 36 %), Easy under 75 % (now 92 %), any new `stuck_lock` or `fell_out_of_world` cluster, or any `frames_gated_melee_over_slots` or `frames_holders_over_slots` above 0.
### `mob_engagement` re-measure
`slot_idle_with_waiters_frames`: a free slot with a ringing candidate, the direct test of whether slot rules limit pressure. `melee_candidates` mean: the supply. Per-swarm clear time, peak living, damage taken, holders, attackers within 250 px. Dossier B's prediction to confirm or refute: pressure rises in swarms, not from slots alone. Waiters are not redesigned (the user declined pokes); a null result is reported as data.

### Curve, matrix, rollback, the rest
- `python tools/playtest/compare_runs.py --curve "grc11*"`: flags only; L6, 7, 10 carry gap notes; Hard shows `-`. L1–4 read as a separate table under the new kit.
- **Matrix on the new Open stages:** `tools/fighter_matrix` with `FTT_MATRIX` set, Globe and Hakata, reporting blast-zone KO share beside Paris 52 %, Vesuvius 24 %, Nassau 18 %. Data only (8b).
- **Rollback re-measure:** `RollbackReadinessTests` detailed log into `docs/PERFORMANCE_BASELINE.md`; 8 ms budget, no breaches, snapshot size recorded. Faster regen means more live entities; re-check spam scripts.
- **Captures:** `huds_*` (indicator hidden, slab 484), results with rank, ten-chip grids at UI 1.0 and 1.4, the training room, Settings' mute row.
- **Smokes:** each changed level `--quit-after 300`, hub, Globe, Berlin, training room. Compare against a control as CLAUDE.md says.
- **Closeout:** three consecutive green full runs at the final commit, orphans drained, lock held. `KLOTHO_DET002` absent; `CS8632` the only warning.

### Risk register

| # | Risk | Mitigation |
|---|---|---|
| 1 | Stacked unjudged pressure: Normal and Easy fall, and nobody can tell which change did it | Gates after each phase, same-build A/B flags, the report rule; §8 session reads the data |
| 2 | Fan-out across 15 level controllers: absolute coordinate pins, spawn re-lays, camera bounds, anchor approach | Carve first (no shifts); pilot gate; helper frozen; disjoint ownership; content sweeps |
| 3 | Bot blind spots hide real faults: bots never grab, use no shortcuts, have gaps on L6, 7, 10, and a route or arena may simply stall them | Per-hero reach tests; traces; thrown-kill and shortcut claims stated as unmeasured; L6 vault stays a logged soft-lock risk |
| 4 | Rank pays players already doing well and funds the grid by Levels 8–10; `BudgetBars` is a guess, and Normal bots rarely finish | Pure rulebook, one value; ruled, recorded; session judges |
| 5 | Time Freeze off removes the zero-cost escape as pressure rises | Force-on seam keeps tests honest; death-rewind counts in gates |
| 6 | Armoured and Catcher lean on the grab; the chord can be off and the direct bind unbound | Soft ×0.5; Special answers; content sweep |
| 7 | Every Fighter hash moves twice; run attack shifts CPU play | No suite pins a hash; matrix re-run as data |
| 8 | Hazard masks: walking mobs filtered only by a predicate | A regression case per hazard type |
| 9 | Lock contention across eight parallel agents | Stagger; filtered suites; orchestrator owns full runs |

## §6 Ledger rows; design master and contract lines owed

**New rows:** `USER-SWARM-WAVES`, `USER-SOFT-COUNTER-TRAITS`, `USER-LEVEL-RANK-DUST`, `USER-LEGACY-SCHEDULE-SHIFT`, `USER-GRID-CAPSTONE`, `USER-LEVEL-TRAVERSAL`, `USER-OPTIONAL-ROUTES-SHORTCUT-ONLY`, `USER-HAZARDS-HIT-LAUNCHED-MOBS`, `USER-OPEN-GLOBE-HAKATA`, `USER-RUN-ATTACK`, `USER-HOLODECK-TRAINING`, `USER-HARD-CPU-METER-RESERVE`, `USER-MOMENTUM-REGEN-9S`, `USER-TIME-FREEZE-OFF`, `DEFER-TIME-FREEZE-OFF` (the session confirms or deletes), `USER-HIT-SOUND-SET`, `USER-BOSS-ARENAS-BUILT`, `USER-STORY-LEADS-UNTIL-ONLINE`, `USER-HUMAN-SESSION-LAST`, `USER-NO-REPLAY-MODES`, `USER-STORY-COOP-NEVER`, `TARGET-COMPLETION-CURVE`, `VERIFY-FIGHTER-FIRST-SLOT-WINRATE` (Florence 54.3 %, Paris 48.4 %; acceptance: revisit with Package 7), `VERIFY-L06-VAULT-BOT-STALL` (five heroes, 12 runs; acceptance: a person clears it with each), `VERIFY-GAMEPLAY-REVIEW-HANDS-ON`.

**Provisional:** `PROVISIONAL-ENEMY-TRAITS`, `PROVISIONAL-FODDER-STATS`, `PROVISIONAL-HAZARD-MOB-RULES`, `PROVISIONAL-LEVEL-RANK-RULES`, `PROVISIONAL-TRAVERSAL-RULES`, `PROVISIONAL-RUN-ATTACK`, `PROVISIONAL-OPEN-STAGE-GEOMETRY`, `PROVISIONAL-BOSS-ARENA-RULES`, `PROVISIONAL-TRAINING-ROOM`, `PROVISIONAL-HIT-SOUNDS`, `PROVISIONAL-CAPSTONE-EFFECTS`.

**Amended:** `USER-ENEMY-TURN-TAKING`, `PROVISIONAL-ENGAGEMENT-DIRECTOR-RULES`, `USER-MOMENTUM-GAUGE`, `USER-COMBAT-FLOW-NOT-NOW` (grid expansion reopened), `VERIFY-BOSS-PHASE-ARENAS` (close when built), `VERIFY-CPU-DEFY-ULTIMATE-STARVE` (narrowed, Mirror exempt), `VERIFY-PAR-SECONDS`, `VERIFY-TIME-FREEZE-ON-BACK-SHARED` (moot). **Kept open, untouched:** M-5, `VERIFY-BOSS-HP`, `VERIFY-STORY-PITS`.

**Master and contracts**, edited together with the mirror (`D:/Projects/fighters-through-time-docs-3/design-godot-v7.md`, `design-godot.md`; hand-edit `DOC-MASTER-UNBOUND-BINDING` lines; check docs-3 for the kit pass's uncommitted edits first): F03 and Controls, Level 0 lessons, Legacy Unlock table, turn-taking numbers, Momentum 9 s, F05 rank section (partly reverses ruling 2.A), grid "9 nodes, 975" → 10, 1,350, the §6 boss table's Overseer P2, Open stages five of ten, Training Mode scope, the silence directive, running attack. Contracts: `DUST_ECONOMY` and its mirror, `TEMPORAL_STATE_CONTRACT`, `HUD_CONTRACT`, `COMFORT_SETTINGS`, `STORY_PERSISTENCE`, `CHECKPOINT_RECOVERY`, `DEFENSIVE_EFFECTS`, `CAMPAIGN_VALIDATION`, `MIRROR_PARADOX`, `ABILITY_DATA`, `CPU_COMBAT_POLICY`. AGENTS: the pillars (Story leads; five Open stages; the Training Mode exclusion), baseline arithmetic, new subsection. CLAUDE: baseline, bot comparability note, runner options.

## §7 Out of scope

- The six authored elites stay unplaced (M-5 open).
- No replay modes: no level replay, Boss Rush or New Game+; level select stays debug only.
- Story co-op: never.
- Boss HP and boss kits beyond the six arena changes.
- Hard and hero balance, Fighter balance, Levels 1–4 numbers: wait for human data (Q3).
- Integrity keeps its fail state; par retune from human timings stays owed.
- Level 6's vault and the first-slot edge: ledger rows only.
- No wall jump, dash or pogo. No waiter pokes or waiter redesign. Ranged permits stay 1/2/3.
- No literal gravity inversion. No art, final audio or VFX beyond placeholders; bots do not learn to grab or use shortcuts.

## §8 The human session (after this whole list)

| # | Judge | Decides |
|---|---|---|
| 1 | Slots 2/3/4 and swarm feel, per difficulty | Slots, swarm sizes, Hard |
| 2 | Each trait: is the tell read, is the answer found, is it fair? | Trait numbers |
| 3 | Throw a mob into each hazard | Hazard numbers |
| 4 | Rank letters over a full run; speed read at the gate | `BudgetBars`, thresholds, rank size |
| 5 | When does the grid finish? Capstones | Capstone effects, rank size |
| 6 | S1 on L1, full kit on L4 | L1–4 retune |
| 7 | Regen 9 s; Specials per minute | Regen |
| 8 | Play without Time Freeze | Delete or re-enable (`DEFER-TIME-FREEZE-OFF`) |
| 9 | Eight-lesson Level 0 | Onboarding |
| 10 | Every stretch and route, with Lincoln and Joan | Traversal, par, `VERIFY-PAR-SECONDS` |
| 11 | Each of 15 bosses; six new arenas | `VERIFY-BOSS-HP` |
| 12 | Level 6 vault with the five stalling heroes | `VERIFY-L06-VAULT-BOT-STALL` |
| 13 | Run attack in both modes, poses per hero | `PROVISIONAL-RUN-ATTACK` |
| 14 | Globe and Hakata pits; Hard CPU's bank | Stages, CPU reserve |
| 15 | Training room usability | `PROVISIONAL-TRAINING-ROOM` |
| 16 | Sound set levels; mute | `PROVISIONAL-HIT-SOUNDS` |
| 17 | Story hero spread (Lincoln, Mozart) | Hero balance |
| 18 | The owed earlier sessions: 2026-10-16 pad checklist, gauge, Defy, kits, glass UI | Their own rows |

## §9 Deviation log (append-only)

(empty)

## §10 Closeout report

(placeholder: delivered; not delivered, honestly; open rows; final counts.)

## Appendix A: traceability (every ruling, one home)

| Ruling | Home | Ruling | Home |
|---|---|---|---|
| 1a, 1a+ | GR-CROWD | 7a, 7b | §1.14, GR-PILOT, GR-LV* |
| 1b, 1b+ | §1.11, GR-CROWD | 7c, 7c+ | §1.15, GR-PILOT, GR-LV* |
| 1b++ | §1.11, level owners | 7d | §1.13, GR-HAZ |
| 2a | §7 | 8a | §7 |
| 2b, 2b+ | §1.12, GR-TRAITS | 8b | GR-CPU (tool), §7 |
| 3a, 3a+, 3a++ | §1.9–10, GR-RANK | 9a, 9a+, 9a++ | GR-STAGE |
| 3a+++, 3a++++ | GR-CAP | 9b | GR-RUN |
| 3a+5 | §1.9, GR-RANK | 9c | GR-TRAIN |
| 3b, 3b+ | §1.5–6, GR-SCHED | 9d, 9d+ | GR-CPU |
| 3c, 3c+ | §1.9, GR-RANK | 9e | §6 row |
| 4a, 4b | §1.17, §7 | 10a, 10a+ | §1.4, GR-MOM |
| 4c, 4c+ | GR-CROWD, §5 | 10b, 10b+, 10b++ | §1.3, GR-TF |
| 5a, 5b | §7, §6 rows | 11 | §8 |
| 6 | §1.14, §7 | 12 | §6 row, GR-LV2 |
| 13 | GR-AUDIO | 14, 14+ | GR-PILOT (Duke), GR-LV2–4 |
| 15 | §1.2, GR-RUN | Next | This plan's approval |

## Appendix B: questions for the user before building

| Q | Question | Recommended |
|---|---|---|
| Q1 | Approve the nine capstone effects (§2 GR-CAP)? | **Ruled:** accepted as listed, provisional |
| Q2 | Stop rule (§5) at Normal < 20 % or Easy < 75 %? | **Ruled:** report and keep building, no auto-tuning |
| Q3 | 3b+ said Levels 1–4 "need retuning"; 4c+ and 11 say no tuning yet. Flags only now? | **Ruled:** flags only; retune after the session unless a level is plainly broken |
| Q4 | Fodder display name? | **Ruled:** "Chrono Swarmling" (ID `chrono_swarmling`) |
| Q5 | Training room in the hub console only, or also the Fighter menu's console? | **Ruled:** hub only |