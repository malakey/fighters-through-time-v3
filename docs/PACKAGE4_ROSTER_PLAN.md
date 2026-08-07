# Package 4 — Complete enemy and boss roster: implementation plan

Status: authored 2026-08-07. This is the working plan for `IMPLEMENTATION_PLAN.md` Package 4. It is the
shared reference for the parallel implementation passes; agents implementing a workstream follow the
contracts here and record deviations in this file's "Deviations" section at the bottom.

Authority order: explicit user instruction > `design-godot.md` Section 6 > this plan > existing code.
Numbers live in `.tres` resources; this document assigns IDs, archetypes, and target values but the
resources are canonical once authored. Dust values must conform to `docs/DUST_ECONOMY.md`
(standard 1–2, elite 10, boss 50, extractor 15 — flat, difficulty-independent) because
`tests/ContentValidation/DustEconomyTests.cs` locks the campaign model.

## 1. Scope (from IMPLEMENTATION_PLAN.md Package 4)

1. Canonical resources for the complete Section 6 standard/elite roster (26+ era-specific types, no
   recolor/duplicate-ID substitutes).
2. Finish patrol, chase, attack, stunned, dead states with difficulty-scaled reaction delays, attack
   cadence, navigation pacing, and consistent typed-hit behavior.
3. Elite secondary abilities, cooldowns, stun resistance, telegraphs, and drop rules.
4. Resources and encounter controllers for all 15 bosses: weighted attack selection, distance
   filtering, phase thresholds, telegraphs, invincibility windows, interruption rules,
   checkpoint/rewind behavior.
5. Mirror Paradox through the deterministic Fighter CPU/simulation boundary, not standard boss AI.
6. Pool every mob, elite, boss projectile, summon, hazard, and reward; per-level warm-up configs.
7. Temporary silhouettes, animation libraries, health-bar names, telegraphs, impact VFX, and audio
   hooks for every roster entry.
8. AI, phase-transition, no-valid-attack, death, rewind, and repeated pool-cycle tests.

Exit criteria: manifest complete and valid for every enemy/boss; bosses select and execute valid
attacks at melee/ranged distances without phase deadlock; worst-case encounter budgets fit the
performance budgets before level production scales.

Explicitly OUT of scope for Package 4: authoring levels 2–15 (Package 5), boss arena level dressing
and era hazards (Package 5), production art/audio (Packages 8/10), cinematic boss presentation
(Package 8). The Tidal Eraser's drowning-arena hazard and similar arena-coupled mechanics are
delivered as boss abilities + documented level hooks, not as authored level content.

## 2. Current-state gaps this package closes

- `EnemyController` never reads `EliteAbilities`, `EliteAbilityCooldown`, `Behavior` (flying),
  `ReactionDelayMin/MaxFrames`, `JumpForce`, `ItemDropChance`, or `SpriteFramesResource`; there is
  no telegraph/windup, no death animation, and `CaptureCheckpointState`/`ApplyStoryRewind` are
  empty. Attacks fire instantly with a hardcoded hitbox.
- `BossController` rolls a weighted index and then discards it — every boss attack is the same
  hardcoded melee box. Distance thresholds, knockback immunity, telegraphs, interruption rules,
  death cleanup, pooling, status effects, and rewind are unimplemented. `new Random()` is unseeded.
- The Florence boss is broken at the integration level: `Level01Controller.SpawnBoss` builds a raw
  `Area2D` named `Hurtbox` (not `FTT.Combat.Hurtbox`), so the controller never wires damage, and
  `borgia_inquisitor.tres` has no `BossAbilities`, so the boss never attacks.
- `EnemyFactory` hand-builds `ColorRect` enemies with hardcoded per-method colors/sizes, ignores the
  authored `StandardEnemyTemplate.tscn`/`standard_enemy` pool, registers its own
  `story_enemy.{id}` pools (4/25 hardcoded), caches templates in never-cleared static state, and
  shows raw `DisplayName` instead of `Tr(DisplayNameKey)`.
- 22 of 27 manifest enemy entries and 14 of 15 boss entries are `Planned/Placeholder/Pending` with
  nonexistent resource paths.
- No tests cover the enemy state machine, boss phases, weighted selection, no-valid-attack, death
  flow, elite abilities, or boss pool cycles.

## 3. Architecture decisions

### 3.1 EnemyAbilityData — one archetype system for elites and bosses

New resource `scripts/Enemies/EnemyAbilityData.cs`:
`[GlobalClass] public partial class EnemyAbilityData : Resource`. Do NOT reuse
`FTT.Combat.AbilityData` (its `HasValidIdentity()` requires a `CharacterID`; enemy abilities must
never mix into character/Fighter data). Fields:

```
Identity:  SchemaVersion, AbilityID (e.g. "boss.jackal_priest.sand_bolt"), DisplayNameKey,
           Archetype (enum), RangeClass (enum Melee|Ranged|Any)
Timing:    TelegraphFrames (windup, presentation-visible), ActiveFrames, RecoveryFrames,
           CooldownSeconds, SelectionWeight (bosses)
Damage:    Damage, KnockbackForce (Vector2; negative X toward caster = pull), HitstunDuration
Hitbox:    HitboxSize, HitboxOffset
Projectile: ProjectileSpeed, ProjectileCount, ProjectileSpreadDegrees, ProjectileLifetime,
           ProjectileGravity (lob arcs), PiercesTargets
Status:    AppliedStatus (FTT.Core.StatusType), StatusDuration, StatusIntensity
Special:   SummonEnemyID (Summon archetype), SummonCount, ShieldDuration, ShieldDamageReduction,
           DashSpeed, DashDurationFrames (Charge), PulseRadius (AreaPulse),
           TeleportRangeMin/Max (Teleport)
Presentation: TelegraphTint (Color), PresentationEventID (string hook for Package 8 VFX/SFX)
```

Archetype enum (`EnemyAbilityArchetype`): `MeleeStrike`, `Projectile`, `Shockwave` (grounded
horizontal traveling wave), `ChargeDash`, `ShieldBubble`, `AreaPulse` (radial around caster),
`SummonMinions` (boss), `Teleport` (boss reposition, optionally followed by next attack sooner).

Execution lives in one shared static/instance helper `scripts/Enemies/EnemyAbilityExecutor.cs`
used by BOTH `EnemyController` (elite secondary abilities) and `BossController` (all boss attacks).
The executor owns: telegraph phase (tint/scale pulse on the `AnimatedSprite2D` + presentation
event), active phase (spawn hitboxes/projectiles via `PoolManager`), recovery. Enemy projectiles go
through a new shared pool `enemy_projectile` (group `enemy_projectile` — rewind clearing already
targets this group name in `PoolManagerTests`). All hitboxes use `EnemyHitbox` layer semantics from
`CollisionLayers`; damage is difficulty-scaled at spawn via `StoryDifficultyTuning.ScaleEnemyDamage`.

### 3.2 EnemyController completion (Story-side; nothing here touches FighterSim)

- Keep the existing state set (`Patrol, Chase, Attacking, Stunned, Returning, Dead`, plus new
  `Telegraphing` if cleaner as an explicit state) and the existing pooling/status/difficulty logic.
- **Reaction delay:** on entering attack range, roll `ReactionDelayMinFrames..MaxFrames` (seeded
  per-instance RNG; seed from spawn counter, not `new Random()` per call) and scale it by a new
  `StoryDifficultyTuning.ScaleReactionDelayFrames(frames, difficulty)` (Easy ×1.5, Normal ×1.0,
  Hard ×0.6; keep the exact multipliers in `StoryDifficultyTuning` with tests).
- **Telegraph:** every attack (standard and elite) runs `TelegraphFrames` of visible windup (sprite
  tint via `TelegraphTint`, presentation event raised on `EventBus`) before the hitbox activates.
  Standard mob basic attacks get telegraph timing from new `EnemyData` fields
  (`AttackTelegraphFrames`, `AttackActiveFrames`, `AttackRecoveryFrames`) instead of the hardcoded
  0.2 s window.
- **Primary attack as data:** add `EnemyData.PrimaryAttack : EnemyAbilityData` (nullable; when null,
  fall back to the legacy melee using the scalar fields so existing resources keep working). Ranged
  standards (Laser Archer etc.) author a `Projectile` archetype primary.
- **Elite abilities:** when `Tier == Elite`, alternate standard attack ↔ next entry of
  `EliteAbilities` (sequential cycle per design) when in range and `EliteAbilityCooldown` elapsed.
  Change `EnemyData.EliteAbilities` type from `FTT.Combat.AbilityData[]` to `EnemyAbilityData[]`
  (nothing authored uses the old field — verified; note this in the PR).
- **Flying:** implement `DefaultBehavior.Flying` — no gravity, vertical tracking toward the target
  with `MoveSpeed`, patrol between waypoints ignoring floors, hover bob optional.
- **Phasing:** new `EnemyData.PhasesThroughWalls` (Rift Phantom): while chasing, drop the
  Environment bit from the body mask; restore it otherwise. Pushbox behavior unchanged.
- **Frontal shield:** new `EnemyData.FrontalDamageReduction` (0–1) applied when the incoming hit
  origin is on the enemy's facing side (Shock-Shield Legionnaire, Cyber-Cavalry Commander).
- **Item drops:** on death, roll `EnemyData.ItemDropChance` as a per-enemy multiplier on the
  difficulty profile's `RandomItemChance` (final chance = profile × per-enemy multiplier; default
  1.0 keeps today's behavior for existing resources — set existing five resources' field to 1.0).
  Keep dust exactly as-is (flat `ChronalDustDrop`).
- **Death:** play `death` animation for a short fixed window (~0.5 s) with collision disabled, then
  release to pool. Keep the `EnemyKilledPayload` raise at the moment of death, not after the
  animation.
- **Rewind:** implement `CaptureCheckpointState`/`ApplyStoryRewind` for the
  `ResetToCheckpointState` policy (position, HP, state → Patrol, cooldowns cleared); keep
  `PreserveCurrentState` as the default. Freeze already works; add tests.
- **Visuals:** build from `SpriteFramesResource` (fallback
  `resources/SpriteFrames/placeholder_enemy_frames.tres`) + new `EnemyData.PlaceholderTint` modulate
  so each roster entry is visually distinct without new art. Animation names follow the template
  contract: `idle, patrol, attack, hitstun, death` (+ `elite_attack` for elites). Name label uses
  `Tr(DisplayNameKey)`.

### 3.3 EnemyFactory → authored scenes + shared pools

- Author `scenes/enemies/StandardEnemy.tscn` and `scenes/enemies/EliteEnemy.tscn` from the Package 0
  templates, with `EnemyController` attached and the template node contract
  (`Presentation/AnimatedSprite2D`, `Hurtbox`, `Hitbox`, `Pushbox`, `Waypoints/Left`,
  `Waypoints/Right`, `ContentContract`). Update `EnemyController` to the template paths (it
  currently expects flat `AttackHitbox`/`AnimatedSprite2D`). The scenes must still pass
  `ContentSceneContractValidator` if pointed at the same contracts; if the production scenes get
  their own contract entries, add them to the manifest as Template rows only if the 14-count test
  is updated deliberately — prefer reusing the existing contracts unchanged.
- `EnemyFactory` becomes generic: `Spawn(string enemyID, Vector2 pos, Node parent)` loads
  `res://resources/Enemies/{id}.tres`, instantiates through pools `standard_enemy` or `elite_enemy`
  keyed by tier (matching the authored `ScenePoolConfig` definitions), assigns `Data`, and calls
  `OnSpawn`. Keep the existing `Spawn*`/`Create*` methods as thin wrappers so
  `Level00Controller`/`Level01Controller` compile unchanged. Delete the static
  runtime-packed-template cache. Add an `elite_enemy` pool definition to the Florence/Tutorial pool
  configs (Florence uses steam automatons today via the standard pool — move elites to the elite
  pool and adjust warm-up counts within budget).

### 3.4 BossController completion + boss events

- Execute the selected ability: weighted random over `BossAbilities` (now `EnemyAbilityData[]`;
  weights come from `SelectionWeight` on each ability — remove `BossData.AbilityWeights` or keep it
  as an override; prefer per-ability weight and drop the parallel array), filtered first by
  `RangeClass` against player distance vs `MeleeRangeThreshold`/`RangedRangeThreshold`
  (design: DistanceBased override). **No-valid-attack rule:** if the filtered set is empty, fall
  back to the full set — a boss must never deadlock; add an explicit test.
- Seeded RNG (`BossController.Rng` seeded from an exported seed or spawn counter) for testability.
- Telegraphs, active, recovery through `EnemyAbilityExecutor` (same code path as elites).
- Reaction delay from `ReactionDelayMin/MaxFrames`, difficulty-scaled like enemies.
- `IsKnockbackImmune` honored; bosses take HP damage but suppress hitstun/knockback when set
  (matches the hyper-armor rule: damage yes, stun no).
- **Interruption rules:** a new `BossData.InterruptibleDuringTelegraph` flag — when true, taking a
  hit ≥ a damage threshold (`BossData.InterruptDamageThreshold`) during telegraph cancels the attack
  into recovery. Default false.
- Phase transitions: keep threshold crossing + invincibility window; add per-phase modifiers
  `BossData.PhaseSpeedMultipliers[]`/`PhaseUnlockAbilityIndices[]` (abilities can be phase-gated:
  `EnemyAbilityData` entries listed in `BossAbilities` but only selectable from phase N via a
  parallel `int[] AbilityMinPhase`). Bypass rest cooldown during transition (design). Ensure the
  `while` loop can't double-fire or skip a phase; test crossing two thresholds in one hit.
- Status effects: give bosses the same one-slot status handling enemies have (bosses can be
  slowed/burned; `Root` on a knockback-immune boss still zeroes movement — that is HP-unrelated and
  allowed).
- **Death:** enter `Dead`, disable collision, play `death`, raise a new
  `EventBus.OnBossDefeated(BossDefeatedPayload{BossID, Position, ChronalDustDrop})`. Also add
  `OnBossSpawned` and `OnBossHPChanged(current, max)` and convert `Level01Controller` from polling
  to events (keep the proximity-triggered bar reveal). Boss stays in scene (not pooled) but must be
  releasable/`QueueFree`-safe; its projectiles/summons ARE pooled.
- **Hurtbox wiring fix:** boss scenes/spawns must use a real `FTT.Combat.Hurtbox`; fix
  `Level01Controller.SpawnBoss` (or better, replace it with the authored boss scene below).
- Author `scenes/enemies/Boss.tscn` from `BossTemplate.tscn` with `BossController` attached
  (contract: `Presentation/AnimatedSprite2D`, `Hurtbox`, `Hitbox`, `Pushbox`, `AbilityOrigin`,
  `ContentContract`; animations `idle, move, melee_attack, ranged_attack, phase_transition, death`).
- **BossEncounterController** (`scripts/Enemies/BossEncounterController.cs`): reusable encounter
  wrapper a level scene instantiates — exports `BossData`, spawn marker, trigger `Area2D`, arena
  bounds; owns spawn-on-trigger, HUD boss-bar wiring through the new events, defeat → dust award +
  completion event, checkpoint/rewind policy (freeze during rewind like enemies; on
  checkpoint-reset policy restore full HP and Idle). Convert Florence's room-4 boss to it as the
  proving integration.
- Rest window, tracking at reduced speed, and `bossRestCooldown` default 1.5 s stay per design.

### 3.5 Mirror Paradox (Level 13 boss) — CPU/simulation boundary

Per design: a clone of the player's locked character, 1000 HP, Hard CPU utility AI with 4–8 frame
reaction delay, single phase, no `BossData` attack patterns. Implementation:

- `resources/Bosses/mirror_paradox.tres` (`BossData`: MaxHP 1000, empty `BossAbilities`,
  `PhaseThresholds = []`, DisplayNameKey `boss_mirror_paradox_name`).
- `scripts/Enemies/MirrorParadoxController.cs`: builds the clone through `CharacterFactory` from the
  session's locked `CharacterData` (normalized base resources ONLY — no `StoryAbilityPerks`, no
  Resonance: the mirror is explicitly a normalized copy; add a test asserting story perks are not
  copied). Decisions come from the same decision logic as the Hard Fighter CPU: extract/reuse the
  utility-decision core of `scripts/FighterSim/FighterCpuController.cs` behind an interface that can
  be driven by a Story-side adapter feeding it player-relative distance/state and receiving
  quantized intents (move/jump/attack/special). The deterministic Fighter code itself must not gain
  Godot/Story dependencies — the adapter lives on the Story side and converts intents to
  `PlayerController`-style actions on the clone. If reuse requires invasive refactoring of the
  deterministic core, the fallback is a faithful Story-side port of the Hard-CPU decision table with
  a shared constants source and a test pinning parity of the reaction-delay bounds (4–8 frames);
  document the choice here.
- Encounter: `MirrorParadoxEncounterController` parallel to `BossEncounterController` (boss bar,
  defeat flow, dust 50). Level 13 itself is Package 5; Package 4 ships the controllers, resource,
  and a test scene/harness proving the clone spawns, mirrors the character, fights, and dies.

### 3.6 Pooling and per-level warm-up

- New shared pools: `elite_enemy` (EliteEnemy.tscn), `enemy_projectile` (a pooled
  `EnemyProjectile.tscn` — pooled node in group `enemy_projectile`, cleared on rewind), plus reuse
  of `standard_enemy`, `chronal_dust`, `story_item`, VFX pools. Boss summons spawn through
  `standard_enemy`.
- Author `resources/Pools/level_pool_configs/level_{02..15}_pool_config.tres` — per-level
  `ScenePoolConfig` with warm-ups sized to each level's era roster + boss (projectiles, summons,
  dust, items), all within a validated budget (`HasValidBudget`). Catalog wiring happens in
  Package 5 when the scenes exist; the configs are the Package 4 deliverable and get budget tests
  now. Update Florence/Tutorial configs for `elite_enemy`/`enemy_projectile`.
- Worst-case encounter budget test: the largest authored per-level warm-up must stay within the
  documented budgets (≤60 active AnimatedSprite2D, ≤500 particles proxies — assert pool caps:
  e.g. standard+elite warm ≤ 30 per level, projectiles ≤ 40).

### 3.7 Presentation, localization, manifest

- Every roster entry: `DisplayNameKey` + `localization/en.csv` entry (`enemy_{id}_name` /
  `boss_{id}_name`), `PlaceholderTint`, SpriteFrames fallback, telegraph tint, presentation event
  IDs (strings only; Package 8 binds real VFX/SFX). Add a localization test asserting every
  enemy/boss resource's `DisplayNameKey` exists in `en.csv`.
- Manifest: flip each delivered entry to `implementation_state=implemented`,
  `asset_status=ready_for_replacement`, `validation_state=valid` (exact enum spellings per
  `ContentManifest.cs`). Add a test cross-checking every manifest Enemy/Boss ID has a matching
  on-disk resource with the same ID inside (closing the survey's noted gap).
- Audio hooks: `PresentationEventID`-driven `EventBus` presentation events for telegraph, attack,
  impact, death; no new audio assets required (placeholder SFX exist from Package 0).

## 4. Roster specification

### 4.1 Standard/elite enemies (27 manifest IDs)

Existing (update to new schema fields, keep IDs/dust): `chrono_slasher` (S, melee),
`tech_enforcer` (E, projectile primary + ShieldBubble elite), `cyber_guard` (S, melee +
StaticCharge on hit), `steam_automaton` (E, melee + Shockwave elite), `hologram_drone`
(S, **make it actually Flying** + weak Projectile primary).

New — one `.tres` each under `resources/Enemies/`, localized name, distinct tint. S=Standard ~30–60
HP, dust 1–2; E=Elite ~140–220 HP, dust 10, `StunResistance ≥ 0.4`, one elite ability:

| ID | Era | Tier | Primary | Elite ability / notes |
|---|---|---|---|---|
| `laser_archer` | Orléans | S | Projectile (energy arrow) | — |
| `neural_linked_knight` | Orléans | E | MeleeStrike (broadsword) | ChargeDash lunge; high stun resist |
| `voltaic_shock_drone` | Chicago | S | Projectile, StaticCharge | Flying |
| `tesla_exo_baron` | Chicago | E | MeleeStrike | AreaPulse (electric nova, StaticCharge) |
| `chrono_rioter` | Paris | S | MeleeStrike (laser-scythe) | — |
| `plasma_sabre_captain` | Paris | E | MeleeStrike | ChargeDash (sabre rush) |
| `shock_shield_legionnaire` | Pompeii | S | MeleeStrike | FrontalDamageReduction 0.5 |
| `cyber_centurion` | Pompeii | E | MeleeStrike | Projectile volley (mortar flares, count 3, lobbed) |
| `laser_pistol_deckhand` | Nassau | S | Projectile (rapid, low dmg, short cooldown) | — |
| `overcharged_cannon_master` | Nassau | E | Projectile (mortar, lobbed) | AreaPulse (shell burst) |
| `plasma_spear_ward` | Egypt | S | Projectile (javelin) | — |
| `chrono_chariot_raider` | Egypt | E | MeleeStrike | ChargeDash leaving RadiantBurn on hit; high MoveSpeed |
| `infrared_border_sentry` | Berlin | S | Projectile (long range, long telegraph, high dmg) | — |
| `neural_mech_walker` | Berlin | E | MeleeStrike (stomp) | Projectile volley |
| `holo_page` | London | S | Projectile applying TimeDilation | — |
| `kinetic_royal_guard` | London | E | MeleeStrike (halberd) | Shockwave |
| `laser_rifle_infantry` | Gettysburg | S | Projectile (fast beam-bolt) | — |
| `cyber_cavalry_commander` | Gettysburg | E | MeleeStrike | ChargeDash; FrontalDamageReduction 0.4 |
| `vacuum_digger` | Lunar | S | Projectile with pull (negative knockback toward caster) | — |
| `void_enforcer` | Lunar | E | Projectile (rocket, small AoE feel via knockback) | AreaPulse (cold vent, TimeDilation) |
| `rift_phantom` | Alexandria | S | MeleeStrike | Flying + PhasesThroughWalls |
| `chrono_guard_elite` | Alexandria | E | MeleeStrike applying TimeDilation (0.75 intensity) | ChargeDash |

Titanic (level 5) uses cultist-only roster (chrono_slasher/tech_enforcer) — no new types. Act III
levels 13–15 use cultist roster + bespoke bosses per the design's deferral note.

### 4.2 Bosses (15 manifest IDs)

All `BossData` + `EnemyAbilityData[]` sets under `resources/Bosses/` (abilities in
`resources/Bosses/Abilities/{boss_id}/`). Dust 50 each. 2-phase bosses: `PhaseThresholds=[0.5]`;
3-phase: `[0.66, 0.33]`. Every boss needs ≥1 Melee-class and ≥1 Ranged-class ability (no-deadlock
rule) unless noted. `borgia_inquisitor` exists but must gain authored abilities (dual-blade swift
assassin: fast melee combo, ChargeDash cross-slash, phase 2 speed up).

| ID | Level | Phases | Kit sketch (archetypes) |
|---|---|---|---|
| `siegemaster_duke` | 2 Orléans | 2 | Melee smash, Shockwave, ChargeDash; P2 faster |
| `chronal_inventor` | 3 Chicago | 2 | Projectile arcs (StaticCharge), AreaPulse, weak melee; ranged-biased weights |
| `revolutionary_tribunal` | 4 Paris | 2 | SummonMinions (`chrono_rioter` ×2), Projectile, melee; P2 unlocks bigger summon |
| `tidal_eraser` | 5 Titanic | 2 | Shockwave (wave), Projectile (water burst), AreaPulse; arena flooding is a Package 5 level hook |
| `vulcan_decimator` | 6 Pompeii | 2 | Lobbed Projectile (RadiantBurn), Shockwave, melee smash; knockback-immune heavy |
| `dread_admiral` | 7 Nassau | 2 | Projectile volley (gatling), AreaPulse (broadside), melee |
| `jackal_priest` | 8 Egypt | 2 | Teleport, Projectile (sand bolt, Root), AreaPulse (sandstorm, TimeDilation); interruptible telegraphs |
| `iron_chancellor` | 9 Berlin | 2 | Projectile shells (lobbed volley), Shockwave, melee; very slow, knockback-immune |
| `tragedy_king` | 10 London | 2 | SummonMinions (`holo_page` ×2), Projectile, Teleport |
| `siege_cannon` | 11 Gettysburg | 2 | Long-range Projectile barrage, Shockwave; melee fallback stomp; DistanceBased pattern showcase |
| `gravity_overseer` | 12 Lunar | 3 | Projectile, AreaPulse with pull (gravity well), ChargeDash; P3 unlocks well |
| `mirror_paradox` | 13 Void | 1 | CPU-driven clone (Section 3.5); no BossData abilities |
| `archive_prime` | 14 Neo-Earth | 3 | Projectile patterns (laser grid), AreaPulse, SummonMinions (`hologram_drone` ×2) |
| `apex_eraser` | 15 Alexandria | 3 | Teleport, Projectile, AreaPulse (TimeDilation), SummonMinions (`chrono_slasher` ×2); final-boss stat block |

## 5. Test plan (new/extended, all GdUnit4)

- `tests/unit/EnemyControllerTests.cs`: patrol↔chase↔attack↔stunned↔dead transitions, de-aggro
  return, reaction-delay window respected and difficulty-scaled, telegraph precedes hitbox
  activation, flying vertical tracking, phasing mask toggle, frontal damage reduction, elite
  sequential ability cycle + cooldown, death → payload + pool release, item-drop multiplier.
- `tests/unit/BossControllerTests.cs`: seeded weighted selection distribution, distance filtering,
  **empty-filter fallback (no deadlock)**, rest window, phase threshold crossing (single and
  double-cross in one hit), transition invincibility, interruption rule, knockback immunity,
  status application, death event.
- `tests/unit/EnemyAbilityExecutorTests.cs`: each archetype's telegraph/active/recovery frame
  accounting, projectile pooling + group membership, pull knockback sign, summon goes through the
  enemy pool, shield reduces damage for its duration.
- `tests/unit/MirrorParadoxTests.cs`: clone uses locked character's normalized data, story perks
  absent, 1000 HP, single phase, reaction delay within 4–8 frames, defeat flow.
- `tests/unit/EnemyRewindTests.cs` (or extend StoryDropsAndRewindTests): freeze stops telegraphs and
  projectiles, checkpoint-reset policy restores authored state, rewind clears `enemy_projectile`
  group.
- `tests/ContentValidation/EnemyRosterContentTests.cs`: every manifest Enemy/Boss row has an
  existing resource whose internal ID matches; every `DisplayNameKey` resolves in `en.csv`; tier
  dust conforms to DUST_ECONOMY (S ≤ 2, E = 10, Boss = 50); every non-mirror boss has ≥1 melee and
  ≥1 ranged ability; elite resources have ≥1 elite ability and StunResistance > 0; all
  `EnemyAbilityData` have valid archetype-required fields.
- Pool-cycle tests: repeated spawn/kill/release ×N through `standard_enemy`/`elite_enemy`/
  `enemy_projectile` retains full reset (extend `PoolManagerTests` pattern); per-level pool config
  budget validation for levels 2–15.
- Update `EnemyManifestTests.cs` counts; keep `ContentManifestTests` green (Enemy ≥ 26, Boss = 15).

## 6. Workstreams and sequencing

Phase A (serial — foundation, one agent, main tree):
- **A1 Runtime foundation:** Sections 3.1–3.4 + 3.6 shared pieces (EnemyAbilityData, executor,
  EnemyController, EnemyFactory/scenes, BossController, boss events, BossEncounterController,
  Florence boss conversion, enemy_projectile pool, StoryDifficultyTuning reaction scaling), the
  five existing enemy resources migrated, `borgia_inquisitor` abilities authored, foundation tests
  (5.1–5.3 core), build + suite green.

Phase B (parallel worktree agents, after A merges):
- **B1 Era enemies, Act I** (Orléans, Chicago, Paris): 6 resources + abilities + en.csv + manifest + tests.
- **B2 Era enemies, Act II west** (Pompeii, Nassau, Egypt, Berlin): 8 resources + same.
- **B3 Era enemies, Act II east + Alexandria** (London, Gettysburg, Lunar, Alexandria): 8 resources + same.
- **B4 Bosses, Act I** (`siegemaster_duke`..`dread_admiral`, levels 2–7): 6 boss kits + tests.
- **B5 Bosses, Act II/III** (`jackal_priest`..`apex_eraser` minus mirror, levels 8–15): 7 boss kits + tests.
- **B6 Mirror Paradox** (Section 3.5).
Shared-file discipline: en.csv/manifest additions appended in plan order under a per-workstream
comment marker; merge conflicts resolved by union at integration.

Phase C (serial closeout, one agent or orchestrator):
- **C1:** per-level pool configs (2–15) + budget tests, manifest flips + cross-check test,
  roster content tests, localization audit, `AGENTS.md`/`IMPLEMENTATION_PLAN.md`/status ledgers
  update, full validation (build, full suite ×, headless import, Florence smoke).

## 7. Validation gates (every phase)

1. `dotnet build FightersThroughTime.csproj --nologo` — no new warnings.
2. `dotnet test FightersThroughTime.csproj --settings .runsettings` — read the `Total:` count
   (baseline 287 + new tests; a bare green exit is not evidence — see CLAUDE.md failure signature).
3. Headless import + Florence/Tutorial/TestArena smoke for scene-touching changes.
4. Story/Fighter isolation: no enemy/boss type leaks into `scripts/FighterSim/` state, loadouts, or
   hashes (Mirror Paradox consumes Fighter decision logic read-only through an adapter).

## 8. Deviations

### A1 — Runtime foundation (2026-08-07)

1. **`Hitbox`/`Hurtbox` split out of `HitboxSystem.cs`.** Godot C# resolves a scene script by matching
   the class name to the file name, so `FTT.Combat.Hitbox`/`Hurtbox` could not be attached in an
   authored `.tscn` while they lived in `HitboxSystem.cs`. They now live in
   `scripts/Combat/Hitbox.cs` and `scripts/Combat/Hurtbox.cs`; `HitboxSystem.cs` keeps `AttackClass`
   and `HitPayload`. No behavior change, no namespace change.
2. **Enemy/boss scene roots are the controllers themselves.** The Package 0 templates use a `Node2D`
   root with a child `Body : CharacterBody2D`. `scenes/enemies/StandardEnemy.tscn`, `EliteEnemy.tscn`,
   and `Boss.tscn` instead use `CharacterBody2D` roots carrying `EnemyController`/`BossController`,
   because the controllers are `CharacterBody2D` and the pool spawns/positions the root. Every
   contract requirement (`Presentation/AnimatedSprite2D`, `Hurtbox`, `Hitbox`, `Pushbox`,
   `Waypoints/Left|Right`, `AbilityOrigin`, `ContentContract`, root groups, animation names, event
   callbacks, `IPoolable`) is still satisfied and asserted by `EnemyControllerTests`. No new manifest
   Template rows were added; the scenes reuse the existing `standard_enemy_v1`, `elite_enemy_v1`, and
   `boss_v1` contracts.
3. **`standard_enemy` pool template repointed.** `florence_pool_config.tres` and
   `tutorial_pool_config.tres` now warm `standard_enemy` from `res://scenes/enemies/StandardEnemy.tscn`
   instead of `StandardEnemyTemplate.tscn` (the template scene has no controller). Florence warm-ups
   were rebalanced to stay inside the 210 budget: `standard_enemy` 20 -> 12, `damage_numbers` 50 -> 40,
   plus `elite_enemy` 4 and `enemy_projectile` 16 (total 206). Tutorial adds `elite_enemy` 2 and
   `enemy_projectile` 12 (total 173 of 180). Both enemy pools use `RecycleOldest`.
4. **`BossData.AbilityWeights` removed** in favor of per-ability `SelectionWeight`, as the plan
   preferred. `BossAttackPattern` (`WeightedRandom`/`DistanceBased`, default `DistanceBased`) was added
   so the design's "DistanceBased override" is data-driven rather than implicit.
5. **`EnemyKilledPayload.ItemDropChanceMultiplier` added** and `StoryDropProfile.ShouldDrop` gained a
   `(roll, perEnemyMultiplier)` overload. That is how `EnemyData.ItemDropChance` reaches
   `StoryDropSystem` without the drop system needing to reload enemy resources. A payload value of 0
   (older raisers) is treated as the neutral 1.0.
6. **Enemy death holds its pool slot for `EnemyController.DeathAnimationSeconds` (0.5 s).** The
   `EnemyKilled` payload still fires at the instant of death, but the release happens after the
   animation window. `PoolManagerTests.StoryEnemyFactoryReusesAResetControllerInstance...` was renamed
   and now pumps the death frames and registers a one-instance pool so the recycle identity is
   deterministic instead of round-robin over the warm-up queue.
7. **Boss stays unpooled but implements `IPoolable`** — required by `boss_v1`'s
   `RequiredInterfaceNames`, and it makes `BossEncounterController` reuse cheap later. Boss
   projectiles/summons are pooled as planned.
8. **Presentation events** are delivered through one new `EventBus.OnEnemyPresentation`
   (`EnemyPresentationPayload` with `SourceID`/`AbilityID`/`PresentationEventID`/`Phase`/`Position`)
   rather than per-phase events. Boss events are `OnBossSpawned`, `OnBossHPChanged`, `OnBossDefeated`.
9. **`EnemyData.EliteAbilities` retyped** from `FTT.Combat.AbilityData[]` to `EnemyAbilityData[]` as
   planned; nothing authored referenced the old field.
10. **Test seams.** `EnemyController.SelectNextAttack()`, `EnemyController.BeginAttack(ability)`,
    `EnemyController.ResolveBodyMask(...)`, `BossController.SelectAbilityIndex(distance)`,
    `BossController.BeginAbility(...)`, and `BossController.TickAbility(...)` are public so the state
    machine is testable headlessly without driving a full scene with a live player.

### B2 — Era enemies, Act II west (2026-08-07)

1. **B2: Melee elites omit `PrimaryAttack`.** `cyber_centurion`, `chrono_chariot_raider`, and
   `neural_mech_walker` author only their elite ability and let `EnemyController` synthesize the
   MeleeStrike primary from the scalar `AttackDamage`/`AttackKnockback`/`Attack*Frames` fields —
   the same shape A1 used for `steam_automaton`. Only the four ranged/lobbed primaries
   (`laser_pistol_deckhand`, `overcharged_cannon_master`, `plasma_spear_ward`,
   `infrared_border_sentry`) are authored as `EnemyAbilityData`, so the subset ships 8 ability
   resources, not 12.
2. **B2: `infrared_border_sentry` is a glass sniper at the band floor.** 40 HP (bottom of the
   40–60 Act II standard band) paired with the subset's highest standard damage (24) and longest
   telegraph (46 frames). `shock_shield_legionnaire` takes the band ceiling (55 HP) to pay for its
   `FrontalDamageReduction = 0.5`.
3. **B2: Range unit split is deliberate, not an inconsistency.** `EnemyData.AttackRange` and
   `MoveSpeed` are world units (`EnemyController.PixelsPerUnit = 60`) while
   `AggroRadius`/`DeAggroRadius` and every `EnemyAbilityData` distance (`HitboxSize`, `PulseRadius`,
   `DashSpeed`, `TeleportRange*`) are raw pixels. `EnemyController.AttackRangePixels` additionally
   clamps projectile-primary range up to `AggroRadius * 0.9`, so the sentry's authored
   `AttackRange = 13.0` / `AggroRadius = 900` both matter: the aggro radius is what actually gates
   the shot.
4. **B2: `shell_burst` is authored `RangeClass = Melee`.** Elite cycling ignores `RangeClass`
   (only boss selection filters on it), but the close radial burst is tagged honestly so the
   resource stays correct if a boss ever reuses it.
5. **B2: en.csv additions are one contiguous 16-row block** (8 enemy names then 8 ability names,
   in plan table order) appended directly after `boss_ability_cross_slash_dash_name`, rather than
   split into the existing separate enemy-name and ability-name groups. One block per workstream
   keeps B1/B2/B3 merges to a clean union.
6. **B2: the worktree branched before A1 landed.** It was merged up from `main` (836d5c7) before
   authoring. Unrelated to content, but it means B2's branch carries the A1 merge commit.
7. **B2: worktrees need an explicit `--import` pass.** `.godot/imported/` is gitignored, so a fresh
   worktree fails the headless import check with `Unable to open file:
   res://.godot/imported/*.ctex` for every SVG — and `--headless --quit` does **not** rebuild it.
   Run `--headless --path <worktree> --import` once first; the import check then exits 0. Same
   class of cold-cache artifact as the `dotnet test` timeout in CLAUDE.md.

### B3 — Era enemies, Act II east + Alexandria (2026-08-07)

London, Gettysburg, Lunar Landing, and Alexandria: 8 `EnemyData` + 9 `EnemyAbilityData` resources.

1. **B3: `StatusIntensity` is not a slow percentage.** `TimeDilationStrategy` computes
   `StatusMovementMultiplier = max(0.1, 1 - 0.5 * intensity)`, so intensity `1.0` is a 50% slow and
   the design's "Chrono-Guard Elite ... slow speed by 75%" needs `StatusIntensity = 1.5`
   (`1 - 0.5 * 1.5 = 0.25`). Authored as 1.5, not 0.75, and pinned by
   `EnemyRosterActIIEastTests.TimeDilationCarriersApplyTheirAuthoredSlow`. The milder carriers use
   `holo_page` 0.6 (30% slow) and `void_enforcer` 0.8 (40% slow). Section 4.1's "(0.75 intensity)"
   parenthetical is the design's slow *percentage*, not the resource field value.
2. **B3: `void_enforcer`'s rocket is a plain `Projectile` with heavy knockback.** Section 4.1 asks
   for a "small AoE feel via knockback"; there is no AoE-projectile archetype, and adding one is a
   runtime change (out of B3's scope), so the feel comes from `KnockbackForce = (7.0, -3.0)` on a
   single non-piercing shot. The elite `AreaPulse` cold-gas vent supplies the actual radial hit.
3. **B3: melee entries without a status omit `PrimaryAttack`.** `rift_phantom`,
   `kinetic_royal_guard`, and `cyber_cavalry_commander` use the scalar
   `AttackDamage`/`AttackKnockback`/`AttackTelegraphFrames`/`AttackActiveFrames`/
   `AttackRecoveryFrames` fallback per Section 3.2. Only `chrono_guard_elite` authors an explicit
   `MeleeStrike` primary, because its status fields have nowhere else to live.
4. **B3: `AttackRange` on ranged entries is documentary.** `EnemyController.AttackRangePixels`
   overrides any projectile-primary range with `max(AttackRange * 60, AggroRadius * 0.9)`, so the
   authored `5.0`–`8.0` unit values on `holo_page`/`laser_rifle_infantry`/`vacuum_digger`/
   `void_enforcer` record intent rather than drive behavior. This matches `tech_enforcer`'s existing
   `8.0`; the prototype `hologram_drone`'s `1.5` is the inconsistent one. Unit reminder:
   `AggroRadius`/`DeAggroRadius` and every ability `HitboxSize`/`HitboxOffset`/`PulseRadius` are
   **pixels**; `AttackRange`/`MoveSpeed`/`JumpForce` are **units** (`PixelsPerUnit = 60`).
5. **B3: elite HP floor raised to the workstream brief's 160.** Section 4.1 says elites are
   ~140–220; the B3 assignment narrowed that to 160–220 and Alexandria sits at the top. Authored
   195/200/205/220 with `StunResistance` 0.55–0.65 (the brief's floor is 0.4). Standards are 46/48/
   50/54 inside the 40–60 band. All dust values conform to `docs/DUST_ECONOMY.md` unchanged
   (standards 1–2, elites exactly 10).
6. **B3: no manifest, pool-config, or script edits.** `resources/Content/content_manifest.csv` and
   the per-level pool configs stay untouched for Phase C, so these eight IDs are on disk and tested
   but not yet manifest-registered or pool-warmed.

### B4 — Bosses, Act I, levels 2-7 (2026-08-07)

1. **B4: the worktree branched before A1 landed.** The assigned worktree was cut from `4f2e3b3`
   (Package 3 close), one commit behind `main`'s `836d5c7` (Package 4 A1). A1 is the stated
   foundation for this workstream, so `main` was merged into the B4 branch before authoring. The
   merge was clean and touched no B4 deliverable. Later Phase B agents cut from the same base should
   check for this.
2. **B4: `revolutionary_tribunal`'s phase-2 summon is `plasma_sabre_captain`, not a bigger
   `chrono_rioter` wave.** The brief pinned the base summon (`mob_call`: `chrono_rioter` x2) but left
   "a stronger summon" open. Section 4.1 lists `plasma_sabre_captain` as the Paris elite, so
   `tribunal_levy` (`AbilityMinPhase = 1`) summons one of those instead of more rioters — a genuine
   escalation rather than a count bump, and era-correct. **Both** `chrono_rioter` and
   `plasma_sabre_captain` are B1 deliverables and did not exist in the B4 worktree;
   `BossRosterActITests` therefore asserts only the ID strings, never resource existence. Phase C
   must validate both cross-references after B1 merges.
3. **B4: every Act I boss is `IsKnockbackImmune = true`.** The kit table flagged immunity only for
   `vulcan_decimator`, but `BossData` defaults to `true`, `borgia_inquisitor` authors `true`, and
   Section 3.4's hyper-armor rule (damage yes, stun no) is the boss norm. Making the lighter bosses
   knockback-able would have been the deviation. Weight class is expressed through
   `InterruptibleDuringTelegraph` instead: `chronal_inventor` (20), `revolutionary_tribunal` (22),
   and `dread_admiral` (28) are interruptible; `siegemaster_duke`, `tidal_eraser`, and
   `vulcan_decimator` ride out their telegraphs.
4. **B4: `tidal_eraser`'s melee coverage is its `AreaPulse`, not a melee swing.** The kit sketch lists
   no melee attack, which would deadlock the no-melee-class rule. `undertow_pulse` is authored
   `RangeClass = Melee` (radial, centred on the caster) with pull knockback
   (`KnockbackForce.X = -4`), satisfying the rule in-fiction. The arena flooding stays a Package 5
   level hook as instructed; nothing level-side was built.
5. **B4: Act I HP band is 540/560/590/640/660/700** for levels 2-7, ascending, above the
   `borgia_inquisitor` 500 anchor and inside the 500-700 band. `BossRosterActITests` pins the
   ascent and the band.
6. **B4: 19 abilities across 6 bosses** (3 each except `revolutionary_tribunal`'s 4, which carries
   the phase-gated summon). All follow the `boss.{boss_id}.{ability}` ID convention with
   `PresentationEventID` equal to `AbilityID`, and all telegraph >= 16 frames.

### B1 — Era enemies, Act I: Orleans, Chicago, Paris (2026-08-07)

1. **B1: Ability `DisplayNameKey`s use the Phase A short-name convention**, `enemy_ability_{move}_name`
   (`enemy_ability_energy_arrow_name`, `_lance_lunge_`, `_arc_bolt_`, `_electric_nova_`, `_sabre_rush_`),
   not a per-enemy namespaced key. That matches the five migrated abilities. `AbilityID` and
   `PresentationEventID` *are* enemy-namespaced and are authored identically to each other
   (`enemy.{enemy_id}.{move}`), also following Phase A.
2. **B1: Elite abilities are authored `RangeClass = Any`** rather than Melee/Ranged. Only
   `BossController` filters by `RangeClass`; `EnemyController` cycles elite abilities sequentially with
   no distance filter, so a narrower class would be dead data that later reads could misinterpret.
3. **B1: Four of the six omit `PrimaryAttack`** (`neural_linked_knight`, `tesla_exo_baron`,
   `chrono_rioter`, `plasma_sabre_captain`) and rely on the controller's synthesized legacy MeleeStrike.
   For the two that do author one (`laser_archer`, `voltaic_shock_drone`), the legacy scalar fields
   (`AttackDamage`/`AttackKnockback`/`AttackCooldown` and the three frame counts) mirror the ability
   exactly, continuing the Phase A convention. `EnemyRosterActITests` pins that mirror.
4. **B1: New roster-wide invariant — placeholder tints must stay visually separated.**
   `EnemyRosterActITests.PlaceholderTintsStayDistinctAcrossTheAuthoredRoster` asserts a manhattan RGB
   distance > 0.25 between every authored `EnemyData.PlaceholderTint` (Phase A five + B1 six). B2/B3
   must pick tints that clear the same margin against all eleven; the Act I values are
   `laser_archer (0.90,0.25,0.25)`, `neural_linked_knight (0.55,0.62,0.80)`,
   `voltaic_shock_drone (0.75,0.95,1.00)`, `tesla_exo_baron (0.35,0.25,0.85)`,
   `chrono_rioter (0.90,0.20,0.60)`, `plasma_sabre_captain (0.35,0.90,0.50)`.
5. **B1: Tests live in `tests/ContentValidation/EnemyRosterActITests.cs`**, not the plan's single
   `EnemyRosterContentTests.cs`, to keep the three parallel Phase B roster workstreams off one shared
   file. Phase C owns the roster-wide manifest cross-check file.
6. **B1: No manifest rows, pool configs, or `scripts/` changes.** Manifest flips and per-level pool
   warm-ups for these six IDs remain Phase C work as scoped.

### B5 — Bosses, Act II/III, levels 8-15 excluding mirror_paradox (2026-08-07)

1. **B5: Phase A was uncommitted when the B worktrees branched.** The A1 foundation existed only as
   working-tree changes in the shared checkout, so branches cut from `4f2e3b3` did not contain
   `EnemyAbilityData`, the executor, the rewritten `BossController`, `scenes/enemies/`, or the
   `borgia_inquisitor` abilities. B5 mirrored that working tree into its worktree byte-for-byte as a
   separate, clearly labelled first commit ("Mirror uncommitted Package 4 Phase A foundation from the
   shared checkout") so the B5 work could build and test. The content is identical to the shared
   checkout; drop or fast-forward past that commit at integration if Phase A lands on main first.
   The B5 deliverable itself is the second commit and touches only new boss resources, the new test
   suite, `localization/en.csv`, and this section.
2. **B5: every boss got a melee-class option, including the teleport/summon-heavy kits.** Per the
   no-deadlock rule `jackal_priest` gained `khopesh_sweep` and `tragedy_king` gained
   `sceptre_strike`, neither of which appears in the Section 4.2 kit sketch. `gravity_overseer` and
   `apex_eraser` likewise carry a dedicated `MeleeStrike` (`mass_slam`, `erasure_blade`) because
   their sketched `ChargeDash`/`Teleport` entries are gap-closers authored as `Ranged`/`Any` class.
3. **B5: phase gating goes beyond the plan's single call-out.** Section 4.2 only gates
   `gravity_overseer`'s well to P3 (`AbilityMinPhase[gravity_well] = 2`, as specified).
   B5 additionally gates `jackal_priest.sandstorm_veil` to P2, `archive_prime.archive_purge` and
   `archive_prime.drone_deployment` to P2, `apex_eraser.chronal_collapse` to P2, and
   `apex_eraser.archive_remnants` to P3, so each fight escalates. A new test
   (`OpeningPhaseAlreadyCoversBothDistanceBands`) pins the invariant that gating never leaves phase 0
   without both a melee and a ranged option.
4. **B5: `holo_page` does not exist yet.** `tragedy_king.curtain_call` authors
   `SummonEnemyID = "holo_page"` as instructed; the London enemy workstream owns that resource.
   `BossRosterActIIandIIITests.SummonAbilitiesNameARealRosterEnemyID` asserts the ID string for
   `holo_page` and asserts on-disk existence only for `hologram_drone` and `chrono_slasher`. Phase C
   must extend that on-disk check to `holo_page` once the enemy lands.
5. **B5: HP bands.** `jackal_priest` 700, `iron_chancellor` 780, `tragedy_king` 800,
   `siege_cannon` 880, `gravity_overseer` 950, `archive_prime` 1050, `apex_eraser` 1200. All seven
   are `IsKnockbackImmune = true` and `AttackPattern = DistanceBased`, matching `borgia_inquisitor`.
6. **B5: worktree had no `.godot/imported/` cache.** The headless `--quit` check initially failed
   with `Parse Error: [ext_resource] referenced non-existent resource` for every placeholder SVG.
   That is a missing import cache in a fresh worktree, not a content fault: `--headless --import`
   warms it, after which `--quit` exits 0 cleanly. The import pass rewrites every `.import` file
   with line-ending-only churn; those were reverted rather than committed.
### B6 — Mirror Paradox (2026-08-07)

1. **B6: Reuse, not port — the decision table was made mode-neutral in place.** The plan offered
   "extract the utility core behind an interface" or "faithful Story-side port". Neither was needed
   as written: `FighterCpuController` was already Godot-free, and its only Fighter-specific coupling
   was the `FighterStateComponent`/`FighterRuntimeComponent` parameter shape. A new
   `scripts/FighterSim/CpuDecisionObservation.cs` (plain struct, raw `FP64` X positions in world
   units, no Godot) now sits between the components and `Decide(...)`, plus a
   `Sample(tick, in CpuDecisionObservation, in previousFrame)` overload and a static
   `FighterCpuController.Observe(components...)` projection. The original
   `Sample(tick, components..., previousFrame)` is a one-line delegation, so the RNG stream, the
   three-tick decision interval, the 128-slot schedule ring, and every branch of the decision table
   are byte-identical — the whole Determinism suite passes unchanged. **The Story side therefore
   drives the real engine, not a copy: there is no duplicated decision table anywhere.**
   `MirrorParadoxDecisionAdapter` (in `MirrorParadoxController.cs`) is the only Story-side code, and
   it is a one-way projection from Godot state into the observation. Nothing flows back into
   `scripts/FighterSim/`.
2. **B6: Intents reach the clone through `InputManager.SetInputSource`.** The adapter implements the
   existing `IPlayerInputSource`, so the quantized `PlayerInputFrame` the CPU emits is consumed by
   the clone's `PlayerController` exactly like a gamepad. No new intent enum, no bespoke action
   dispatch, and no changes to the `PlayerController` state machine. The mirror occupies local slot
   1, which already maps to the Enemy body/hitbox/hurtbox collision layers via
   `CollisionLayers.*ForFighterSlot(1)`, so player-versus-mirror damage works in both directions
   with no collision changes.
3. **B6: `PlayerController.EncounterMaxHPOverride` added** (outside Phase A code). The clone needs
   the authored 1000-HP `BossData` pool, and `MaximumHP` was `CharacterData.MaxHP + StoryMaxHPBonus`.
   Overloading `StoryMaxHPBonus` would have smuggled a Resonance-shaped value into a boss pool, so
   `MaximumHP` now prefers a positive `EncounterMaxHPOverride` and falls back to the old expression.
   Default 0 keeps every existing caller identical; `MirrorParadoxTests` asserts `StoryMaxHPBonus`
   stays 0 on the clone.
4. **B6: The clone leaves the `StoryPlayer` group and joins `MirrorParadox`.**
   `CharacterFactory.CreateCharacter` puts every `PlayerController` in `Players` and `_Ready` adds
   `StoryPlayer`. Level flow, the Story camera, rewind, and the HUD all resolve `StoryPlayer`, so
   `MirrorParadoxController` removes the clone from it immediately after `AddChild` and tags it
   `MirrorParadoxController.MirrorGroup` instead. It stays in `Players` so the adapter can find the
   campaign avatar by exclusion.
5. **B6: Reaction delay is not Story-difficulty scaled.** `BossController` runs authored
   `ReactionDelayMin/MaxFrames` through `StoryDifficultyTuning.ScaleReactionDelayFrames`. The mirror
   does not: design pins it to the Hard CPU engine's 4–8 frame window regardless of campaign
   difficulty, and the engine owns that window. `mirror_paradox.tres` still authors 4/8, and a
   parity test asserts the resource matches `FighterCpuController.GetReactionDelayBounds(Hard)` so
   the two can never drift. HP *is* difficulty-scaled through `StoryDifficultyTuning.ScaleEnemyHP`,
   consistent with every other boss.
6. **B6: Boss HP/defeat events are republished, not duplicated.** The mirror has no
   `BossController`, so `MirrorParadoxController` listens to the clone's `OnPlayerHPChanged` /
   `OnPlayerDied` (filtered to slot 1) and re-raises the Phase A `OnBossSpawned` /
   `OnBossHPChanged` / `OnBossDefeated` payloads under `BossID = "mirror_paradox"`. No new EventBus
   payloads or events were added.
7. **B6: No test harness scene.** `MirrorParadoxController` and `MirrorParadoxEncounterController`
   build their own children in code, so `tests/Unit/MirrorParadoxTests.cs` instantiates them
   directly against the headless scene tree. Nothing under `scenes/` was added; Level 13 authoring
   stays Package 5. `mirror_paradox.tres` is also not registered in
   `resources/Content/content_manifest.csv` — that flip belongs to Phase C1.
8. **B6: Unused `BossData` fields are authored as zero.** The mirror ignores `MoveSpeed`,
   `AttackDamage`, `AttackKnockback`, `AttackRange`, `RestCooldown`,
   `PhaseTransitionInvincibilityDuration`, and `InterruptDamageThreshold` — the clone's own
   `CharacterData` supplies all of that. They are authored as 0 to make "not used by this boss"
   explicit. `MeleeRangeThreshold`/`RangedRangeThreshold` are authored as 2.0/5.0 to document the
   CPU decision table's close/far world-unit thresholds.

9. **B6 (fix): an empty Script-typed Godot array in `mirror_paradox.tres` was corrupting
   the .NET heap.** After B6 merged, the full suite began truncating nondeterministically —
   the GdUnit Godot child died silently (exit `-1073741819` / `-1073741795`) or hung, reporting a
   partial `Total:` with everything that ran passing, and `ResourceLoader.Load` intermittently
   returning null in unrelated content suites. Root cause: the line

   ```
   BossAbilities = Array[ExtResource("2")]([])
   ```

   where ext_resource 2 is `EnemyAbilityData.cs`. An **empty** typed array whose element type is
   declared by a C# Script corrupts memory in Godot 4.7.1 .NET when the resource is marshalled to
   its `EnemyAbilityData[]` export. The corruption is silent; the process dies later at an
   unrelated allocation, which is why the crash site never pointed at the cause.

   Isolation was by subtractive bisection against a harness that reproduced the crash 4/4
   (five inert `MirrorParadoxController` create/free cycles). Constructing the controller with
   `Data = null` was 4/4 clean; with the resource, 4/4 crash. Deleting just that one line made it
   4/4 clean. Non-empty Script-typed arrays (`borgia_inquisitor.tres`) and empty arrays of builtin
   types (`Array[float]([])`, `Array[int]([])`) are unaffected — both are still present in
   `mirror_paradox.tres` and were proven safe. The fix omits the property entirely so the C# field
   default (null) applies; `BossController.SelectAbilityIndex` and the B6 tests already treat null
   and empty identically. A comment in the `.tres` warns against reintroducing it.

   **This is a repository-wide landmine, not a B6 quirk** — any workstream authoring a boss or
   enemy resource with no abilities could hit it. `tests/ContentValidation/ScriptTypedEmptyArrayTests.cs`
   now scans every `.tres` under the content directories for the pattern and fails with the
   offending file and line. It also asserts it reached more than 50 files so a broken directory
   walk cannot pass vacuously. A repo-wide grep (`.tres` and `.tscn`) confirms no other instance
   exists today.

10. **B6 (fix): hypotheses that were tested and rejected.** Recorded so nobody re-litigates them.
    Synchronous `Free()` of the in-tree clone hierarchy, `QueueFree()` instead, detach-then-free,
    the eight monitoring `Area2D` children, the `InputManager` input-source registration, EventBus
    subscription lifetime, group churn, nesting the clone under a parent, and `CharacterFactory`
    itself were each isolated and each proved clean (3–9 runs apiece). The input source is
    correctly released on `_ExitTree`; `CharacterFactory` survives create/free/leak cycles with no
    crash at all.

11. **B6 (fix): teardown hardening kept, but it is hygiene, not the cure.** `ActivateOnSpawn` /
    `BeginEncounter` gating means the clone no longer simulates before the encounter is revealed
    (matching `BossEncounterController`'s reveal semantics) and stops on defeat, so an unrevealed
    boss cannot spray AI-driven attacks, VFX, and pooled objects into the level or the shared test
    tree. `DespawnMirror()` gives levels an explicit teardown that makes the clone inert, drops its
    groups, detaches it, then frees it out of tree. `MirrorParadoxEncounterController._ExitTree`
    calls it. Tests detach before freeing. None of this changed the crash rate on its own —
    `MakeSubtreeInert` was deliberately *not* left in `_ExitTree`, because mutating physics state
    during tree removal is itself unsupported and it fixed nothing.

12. **B6 (fix): note on the ~24-test total.** While looping the suite, one run reported
    `Total: 24` with no crash. That is the separate, pre-existing "GdUnit could not launch Godot"
    signature already documented in `CLAUDE.md` (only the pure-C# tests run, exit code still 0) and
    is unrelated to this fix. Distinguish it from a corruption truncation by the runner exit code:
    corruption gives a large partial total plus a negative exit code, a launch failure gives ~24
    and `exit=none`.

