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
