# Gap dossier — Section G: Enemies & Bosses (design master §6, lines 2899–3032)

Recon only. No repo file was modified. Sources read in full: the section diff
(`G_enemies_bosses.patch`), master lines 2880–3032 plus the boss stat table at master
lines 1611–1631 (cross-section but load-bearing for boss HP), and
`docs-3/docs/{MIRROR_PARADOX,CPU_COMBAT_POLICY,CPU_RECOVERY,DESIGN_BUILD_DEVIATIONS}.md`.

Repo revision inspected: `main` @ `31fed14` (V7.4 stun-lock pass, 2026-08-29), working tree clean.

---

## 0. Executive summary

| # | Design change | Status in repo | Rough size |
|---|---|---|---|
| G1 | **The Eraser** — new Story-only Future Cultist elite | **Absent.** No resource, no manifest row, no keys, no placement, no sprite set. | **L–XL** (the section's dominant item) |
| G1a | **Null Lance** — `Projectile` applying the new `Suppression` status, Basic-class vs block, 8 s CD | Archetype exists; `Suppression` does not; **elite abilities are force-flagged Guard-Crush in code** — a hard blocker | M (blocked on status workstream) |
| G1b | **Siphon Snare (F14 Option A)** — `AreaPulse`-cast meter-drain **tether/channel** | **No archetype, no runtime, no concept.** Needs a new append-only archetype + a channel node. Depends on Time Freeze (absent). | **L** |
| G2 | Boss renames: **Archive Prime → Forge Sentinel**, **Apex Eraser → First Unbound** | Old IDs/keys/paths everywhere (`archive_prime`, `apex_eraser`) | M (rename fan-out) or S (display-name-only) |
| G3 | **First Unbound P2 self-rewind** (T01b Option A) | **Absent.** `BossController` has no history buffer, no self-heal, no combat-suspend. | **L** |
| G4 | **Borrowed Legacies P3** — data-driven boss channelling roster-minus-active-character signature moves | **Absent.** Boss kits are static authored `EnemyAbilityData[]`; nothing reads `CharacterData`/`AbilityData`. | **L** |
| G5 | **Mirror Paradox F20 campaign CPU profile** (difficulty-scaled reactions/defense, full kit on Easy, Hard-only perk mirror) | Partially satisfied by accident: HP scaling already yields 700/1000/1500; CPU bands already 30–45/15–20/4–8 and 10/40/80. But difficulty is **hardcoded Hard** and perks are **always off**. | **M** |
| G6 | **L1–L4 boss HP → 350 / 520 / 700 / 850** | Repo is 500 / 540 / 560 / 590. Only L1 is a cut; L3 and L4 are *increases*. Breaks `BossRosterActITests` band + `EnemyManifestTests`. | **S** (values) + S (tests) — but see §6 conflict |
| G7 | Lore relabels (Apex Archive → Unbound; Neo-Earth → The Unbound Bastion; Alexandria Restoration → The Meridian Founding) | Prose + `en.csv` display strings + code comments | S |
| G8 | Chrono-Warden **has still never been placed in any level** (V7.1 design law, unclosed) | Resource + tests exist; zero spawn sites | S–M |

---

## 1. G1 — The Eraser (V7.6)

### 1.1 What the design requires

Master lines 2926–2931 plus the faction list at 2949.

**Identity / stats / AI**
- Future Cultist **elite**, **Story-only**. "Fighter Mode is untouched — no Suppression source exists in the sim, and none may be added without a separate ruling."
- **~190 HP**, **stun resistance 0.5**, behaviour **`Chase`** (hunter closes; the 85%/110% stand-off band still applies).
- **"ignores siphon-defence positioning": aggro radius is the whole room** — unique among elites.
- Enemy Stagger Discipline (V7.4) applies as to any elite.
- "Draws its authored share of the required encounter budget (F05); **no flat 20-dust award**."
- Two elite abilities, alternating (Null Lance / Siphon Snare).
- Visual: lean, visored, null-lance; "cold-white" lance tip in the two-colour grammar.

**Placement**
- Debut: **Level 4A Legacy Level**, a *scripted single-Eraser ambush* at an independent authored route trigger between Entry and PreBoss (F12 — explicitly **not** dependent on the middle checkpoint), with a non-blocking Sarah bark.
- **Level 13** (Act III entry): a **pair**, scripted ambush, with the naming line.
- Salted through **Levels 7–15**, **never in the same room as a Chrono-Warden until Act III**.

**Explicit "Requires" list from the design** (master 2931): `Suppression` in `StatusType`; a `SuppressionEffect` strategy class; the ability-lock check in `BaseSpecial`/movement/ultimate input handling; the aura-smother + HUD lock visuals; a manifest row; `EnemyData` + two `EnemyAbilityData`; localization keys; pool budgets for 4A and 7–15; one Eraser SFX set; content-test coverage.

### 1.2 What exists

Nothing named Eraser exists as an enemy. `grep -rni eraser scripts/ resources/Enemies/ tests/` returns only the two **bosses** `tidal_eraser` (L5) and `apex_eraser` (L15) — an unrelated name collision that must not be conflated. `Suppression` appears nowhere in `scripts/`, `resources/`, `tests/`, or `localization/`.

The **Chrono-Warden is the exact template** and is a complete worked precedent (V7.1 + V7.3 rework):

| Asset | Path |
|---|---|
| Enemy resource | `D:\Projects\Fighters Through Time - V3\resources\Enemies\chrono_warden.tres` |
| Elite ability 1 | `resources\Enemies\Abilities\chrono_warden\dilation_field.tres` (`Archetype = 8` = `PersistentFieldAtTarget`) |
| Elite ability 2 | `resources\Enemies\Abilities\chrono_warden\phase_skip.tres` (`Archetype = 7` = `Teleport`) |
| Primary attack | `resources\Enemies\Abilities\chrono_warden\chrono_bolt.tres` (`Archetype = 1` = `Projectile`) |
| Manifest row | `resources\Content\content_manifest.csv:115` |
| Keys | `localization\en.csv:193,195,196` (`enemy_chrono_warden_name`, `enemy_ability_dilation_field_name`, `enemy_ability_phase_skip_name`) — plus `:194` `enemy_ability_chrono_bolt_name` |
| Contract test | `tests\unit\TimelineIntegrityTests.cs:200-224` (`TheChronoWardenMatchesItsAuthoredEliteContract`) |
| Behaviour tests | `tests\unit\EnemyControllerTests.cs:229, 259` |

Note the Warden **reuses** `resources/SpriteFrames/Enemies/chrono_guard_elite_frames.tres` for its body art (it has no `chrono_warden_frames.tres`), but **does** have its own `chrono_warden_ability_vfx_frames.tres`. The Eraser can do the same (reuse e.g. `tech_enforcer_frames.tres`).

Runtime that already supports an elite of this shape:
- `scripts\Enemies\EnemyData.cs` — `EliteAbilities[]`, `StunResistance`, `AggroRadius`/`DeAggroRadius`, `EliteAbilityCooldown`, `Behavior`.
- `scripts\Enemies\EnemyController.cs` — sequential elite cycling (`SelectNextAttack`, line 651), reactive Teleport (line 688), stand-off band (lines 500–573), V7.4 stagger discipline (lines 753–827), `ApplyStatusEffect` two-slot (line 1051).
- `scripts\Enemies\EnemyAbilityExecutor.cs` — telegraph→active→recovery, projectile spawning, dual-channel telegraph, `PersistentFieldAtTarget` → `DilationFieldZone`.
- Pools: level configs use **generic tier pools** (`standard_enemy`, `elite_enemy`) — see `resources\Pools\level_pool_configs\level_13_pool_config.tres`. **No new pool ID is needed**; only `WarmUpCount` on the `elite_enemy` definition may need a bump (current values: L02–L04 = 3, L05 = 4, L06 = 3, L07 = 4, L08 = 3, L09 = 4, L10 = 3, L11 = 4, L12 = 5, L13 = 3, L14 = 5, L15 = 6; `MaxCapacity` is 16 everywhere).

### 1.3 Blockers and mismatches

**(a) `DefaultBehavior` has no `Chase` value.** `scripts\Enemies\EnemyData.cs:11` — `enum DefaultBehavior { Ground, Flying, StandGuard }`. The design's "`Chase`" is the *opposite* of `StandGuard` and is already the default `Ground` behaviour plus aggro/chase. Recommendation: author `Behavior = 0` (Ground) with a room-scale `AggroRadius`/`DeAggroRadius` (≈3000 / 4000 px vs. the Warden's 640/940) rather than appending an enum value. If a literal enum entry is wanted it must be **appended** (ordinals serialize).

**(b) Elite signature abilities are force-flagged Guard-Crush — this breaks Null Lance.**
`EnemyController.BeginAttack` (`scripts\Enemies\EnemyController.cs:632-641`):
```csharp
Executor.Begin(ability, targetPosition, _facingRight,
    guardCrush: _lastAttackWasElite || (ability?.IsGuardCrushing ?? false),
    unblockable: false);
```
and `EnemyAbilityData.cs:66-72` documents "Elite-tier signature abilities are Guard-Crush **implicitly regardless of this flag**". The design says Null Lance is **Basic-class vs block** ("blocking is never a trap"). This needs a code change — e.g. an explicit `IsBasicClass`/`ForcesBasicBlockClass` export on `EnemyAbilityData` that overrides the elite implicit, threaded through `BeginAttack` and `TryReactivePhaseSkip` (line 700, which also hardcodes `guardCrush: true`). It also changes the telegraph tint and glyph (`EnemyAbilityExecutor.TelegraphColor` / `ResolveGlyphShape`, lines 387–396) from orange/diamond to white-yellow/circle, which is exactly the design's intent ("the lance's tip goes cold-white in the two-color grammar").
This is a **shared-file change in `EnemyAbilityData.cs` + `EnemyController.cs` + `EnemyAbilityExecutor.cs`** and will move `EnemyAbilityExecutorTests` / `EnemyControllerTests`.

**(c) `Suppression` is owned by the status-schema workstream.** `StatusType` lives in `scripts\Core\EventBus.cs:260-267` with exactly `{None, TimeDilation, Venom, StaticCharge, RadiantBurn, Root}`. The status is consumed here (`EnemyAbilityData.AppliedStatus`, `Hitbox.AppliedStatus`, `StatusController.SlotFor`) but must be **authored there**:
- Append `Suppression` to `StatusType` (ordinals serialize into `.tres` — append-only).
- `StatusController.IsDamageStatus` (`scripts\Combat\StatusController.cs:125-128`) already routes anything not Venom/RadiantBurn to the **control slot** — so Suppression lands in the control slot with no edit, satisfying "it replaces a Root or Time Dilation; never a burn or Venom".
- `SuppressionEffect` strategy + `PlayerController` ability-lock checks (Special 1/2, movement, ultimate cast — cooldowns keep ticking) + the grey aura smother + HUD lock cross-out: all **not** this section.
- `EnemyController.ApplyStatusEffect` (line 1051) would also need a Suppression arm if the player can ever Suppress an enemy (design does not require this — recommend enemies ignore it).

**(d) `Siphon Snare` has no archetype and no runtime.** See §2.

**(e) Time Freeze does not exist in the repo** (`grep -rl TimeFreeze scripts/` → nothing). The Snare's "pause channel duration, drain, cast and cooldown timers during Time Freeze and other world freezes" is therefore a forward dependency on the time-systems workstream. The *existing* world freeze (`ChronalRewindManager.FrozenSimulationGroups` / `IStoryRewindSimulation`) is available today and is what `DilationFieldZone` uses.

**(f) Dust.** `resources\Enemies\tech_enforcer.tres:40` — `ChronalDustDrop = 10`, and `EnemyRosterContentTests.TierDustRewardsConformToTheEconomyAcrossTheWholeRoster` asserts **elites are exactly 10**. The new `docs-3/docs/DUST_ECONOMY.md` (F05) retires that whole model (standard weight 1 / elite-or-Eraser weight 5 allocated out of a per-level pool; bosses 25 not 50). Authoring the Eraser's dust is therefore **blocked behind / must be coordinated with the dust-economy workstream**; in the interim, `10` is the only value that passes the existing gate.

**(g) The "never alongside a Chrono-Warden until Act III" rule has no enforcement surface** and no encounter-composition validator exists. It would have to be a content test over the level controllers' spawn tables.

**(h) Level 4A does not exist.** `grep -rln "Legacy Level|Level4A|level_4a|LegacyLevel" scripts/ scenes/ resources/ docs/` → nothing. The debut ambush cannot be authored until the campaign workstream creates the 4A scenes/routes. The **Level 13 pair** *can* be authored today into `scripts\Environment\Level13Controller.cs`.

### 1.4 Concrete change list (G1)

**New resources**
1. `resources/Enemies/eraser.tres` — `EnemyID = "eraser"`, `Tier = 1` (Elite), `MaxHP = 190`, `StunResistance = 0.5`, `Behavior = 0`, `AggroRadius ≈ 3000`, `DeAggroRadius ≈ 4000`, `EliteAbilityCooldown` per the two abilities' CDs, `ChronalDustDrop = 10` (interim; F05 pending), `DisplayNameKey = "enemy_eraser_name"`, `SpriteFramesResource` → reuse `tech_enforcer_frames.tres` until art exists, `PrimaryAttack` → a new basic (e.g. `lance_jab`, `MeleeStrike` or short `Projectile`), `EliteAbilities = [null_lance, siphon_snare]`.
   *ID naming caution:* `eraser` collides conceptually with the bosses `tidal_eraser` / `apex_eraser`. Consider `unbound_eraser` or `null_eraser` to keep greps and the VFX-library owner parse unambiguous.
2. `resources/Enemies/Abilities/<eraserID>/null_lance.tres` — `Archetype = 1` (Projectile), `RangeClass = 1` (Ranged), `TelegraphFrames = 30`, `CooldownSeconds = 8.0`, `AppliedStatus = Suppression`, `StatusDuration = 2.0`, `StatusIntensity = 1.0`, slow `ProjectileSpeed` ("a slow, visible bolt"), `TelegraphTint` = cold-white, **Basic block class** (new flag, see blocker (b)).
3. `resources/Enemies/Abilities/<eraserID>/siphon_snare.tres` — `TelegraphFrames = 45`, `PulseRadius = 180` (3 units × 60 px/unit), `CooldownSeconds = 10.0`, `FieldDurationSeconds = 3.0` (channel cap), `Damage = 0`, `HitstunDuration = 0`, `KnockbackForce = (0,0)`, plus new drain-rate field(s).
4. `resources/Enemies/Abilities/<eraserID>/<primary>.tres`.
5. Optional: `resources/SpriteFrames/Enemies/<eraserID>_ability_vfx_frames.tres` (see test gate below).

**Code**
6. `scripts/Enemies/EnemyAbilityData.cs` — add the Basic-class override export; add Siphon drain fields (`MeterDrainPerSecond = 10`, `MeterDrainCap = 30`); add the new archetype (§2).
7. `scripts/Enemies/EnemyController.cs` — honour the Basic-class override in `BeginAttack` and `TryReactivePhaseSkip`.
8. `scripts/Enemies/EnemyAbilityExecutor.cs` — new archetype branch + `HasValidArchetypeFields` case.
9. New `scripts/Enemies/SiphonTetherChannel.cs` (§2).
10. `scripts/Combat/EnemyAbilityVisualLibrary.cs:15-28` — add the Eraser to `EnemyIDs`.
11. `scripts/Combat/RosterVfxMap.cs:69-99` — **`snare` and `siphon` are not in the verb vocabulary**; `enemy.<id>.siphon_snare` would fall through to the generic default and **fail** `RosterVfxMappingTests.EveryAuthoredPresentationEventResolvesToAnExplicitFamily`. Add `"snare"`/`"siphon"`/`"tether"` (Beam family reads best) — or name the event with an existing token. `null_lance` already matches `"lance"` → Slash family (arguably wrong for a projectile; consider adding `"null"` under Beam ahead of Slash, or name it `null_bolt`).

**Manifest / localization / pools**
12. `resources/Content/content_manifest.csv` — one `Enemy` row after line 115: `Enemy,<id>,6,res://resources/Enemies/<id>.tres,EnemyFactory,Implemented,ReadyForReplacement,Valid,true`.
13. `localization/en.csv` — `enemy_<id>_name`, `enemy_ability_null_lance_name`, `enemy_ability_siphon_snare_name`, `enemy_ability_<primary>_name`, plus the two Sarah bark lines for the 4A and L13 ambushes (dialogue keys — campaign workstream), plus HUD/status strings for Suppression (status workstream). **Re-run `--headless --import`** and commit the regenerated `localization/en.en.translation` — roster tests resolve through the compiled resource, not the CSV.
14. `resources/Pools/level_pool_configs/level_{04,07..15}_pool_config.tres` — bump the `elite_enemy` `WarmUpCount` where a placement pushes concurrency past the current warm count; add a `level_04a_*` config when 4A lands.

**Placement**
15. `scripts/Environment/Level13Controller.cs` — add two Eraser entries to `SpawnTable` (currently `PhantomEnemyID`/`CultistEnemyID`/`EliteEnemyID = chrono_guard_elite`, lines 253–297) behind an authored ambush trigger.
16. Levels 7–15 salt: `Level07/09/12/14/15Controller` already carry `EliteEnemyID` constants; 08, 10, 11 build their spawn tables differently (no constants surfaced by grep) — inspect individually.
17. Level 4A — blocked on the campaign workstream.

**Tests**
18. New `tests/unit/EraserContentTests.cs` (mirror `TheChronoWardenMatchesItsAuthoredEliteContract`): tier/HP/stun-resist/2 elite abilities/archetypes/Suppression status/telegraph frames/CDs.
19. New behaviour cases in `tests/unit/EnemyAbilityExecutorTests.cs`: Null Lance applies Suppression on hit, applies nothing when blocked, is Basic-class (1 charge, circle glyph, white-yellow tint).
20. New `tests/unit/SiphonSnareTests.cs` — the F14 validation matrix (§2.3).
21. **Counter updates in existing suites** (these will fail the moment the resource lands):
    - `tests/ContentValidation/EnemyRosterContentTests.cs:205` — `AssertThat(standards + elites).IsEqual(28)` → **29**.
    - `tests/ContentValidation/EnemyRosterContentTests.cs:131` — `checkedKeys >= 118` → raise.
    - `tests/ContentValidation/EnemyRosterContentTests.cs:41` — `rows.Length >= 26` (manifest Enemy rows) — still passes, but review.
    - `tests/ContentValidation/EnemyRetroSpriteAssetTests.cs:56` — `AssertThat(inspected).IsEqual(28)` → **29**; the Eraser's `SpriteFramesResource` must carry the six animations × 3 frames (`idle, patrol, attack, elite_attack, hitstun, death`) — reusing an existing sheet satisfies this.
    - `tests/ContentValidation/RosterVfxMappingTests.cs:64` — `events.Count > 70` still passes; the explicit-family sweep is the real gate.
    - `tests/ContentValidation/Level13ContentTests.cs:202-213` — `EliteEnemyCount == 1` and `SpawnTable.Length == 9` break if two Erasers are added (→ 3 / 11), and the locked-economy comment points at `docs/DUST_ECONOMY.md`, which F05 supersedes.

**Docs**
22. `docs/PACKAGE4_ROSTER_PLAN.md` §4.1 (roster IDs) + a new §8 deviation entry; `AGENTS.md` roster/status/test-baseline bullets.

**Rough size:** ~6 new resources, ~6 code files touched, 1 new runtime node, ~5 test files touched + 2–3 new, plus placement. **L–XL**, and hard-blocked on `Suppression` (status workstream) for anything past authoring.

---

## 2. G1b — Siphon Snare in detail (F14 Option A)

### 2.1 Requirement

Not a hitbox. It is a **cast → attachment check → channel** with a per-tick meter drain and a long break-condition list. Verbatim numbers:
45-frame / 0.75 s telegraph · 3-unit (180 px) radius · 10 s cooldown · ≤ 3 s / 180 live frames of tether · drain `min(currentMeter, 10 * liveDeltaSeconds)` per live tick preserving fractions · **≤ 30 points per cast** · no lump, no HP damage, no hitstun/knockback/movement lock/status-slot entry/Rally echo/damage-meter credit/hitstop.

Attachment gates: one check at telegraph end; living player within 3 units **centre-to-centre** with **unobstructed line of sight through authored solid cover**; cannot start or attach while the target is Suppressed / at zero meter / already tethered / invulnerable. Miss or block creates **no lingering pulse**.

Block: a legal **grounded, front-facing** block at attachment prevents all drain and consumes **one block charge** with ordinary Basic block response (shieldstun; normal shatter if it was the last charge). Zero-charge / locked / airborne block cannot absorb. **No HP chip** (no HP damage in the cast). Distinct-block perks may trigger once. Raising block *after* attachment does not sever.

Channel: the Eraser is **stationary and cannot attack or use Null Lance** while maintaining. Player may act freely. End at zero meter.

Break (evaluated **before each drain tick**): range > 3 units, blocked LoS, player invulnerability (**including active roll i-frames**), either actor's death/removal, a successful stun/stagger interrupt of the Eraser (**a hit rejected by its armor does not count**), or Suppression newly applied to the player. Broken tethers never reattach during that cast.

Overlap: **one active Siphon tether per player**; simultaneous eligible attachments resolve by **stable encounter/actor ID**; losers fail without drain. No stacked rate/duration.

Cooldown: starts on **accepted cast commitment**, runs 10 live seconds even after miss/block/interrupt, no early refund. No new commitment against a zero-meter / Suppressed / invulnerable / already-tethered target.

Freeze & recovery: pause channel/drain/cast/cooldown timers during Time Freeze and other world freezes; on thaw re-evaluate break conditions **before** any drain; **no catch-up ticks**. F10 checkpoint reconstruction ends the transient tether and retains latest durable meter; load never repeats a tick or grants meter back. Level/room departure ends the tether. F13's seal becomes **Not Ready** when an unused full meter is drained; Ready again only on a legitimate refill; an already-spent seal stays Spent.

Telegraph presentation: show the 3-unit boundary around the Eraser, a **tether/meter-drain glyph beside its Basic block-class cue**, and a rising electric cue; boundary/glyph must read **without colour or flashing**.

Alternation with Null Lance is retained. Multiple Erasers may not combine active Suppression with an ongoing tether.

### 2.2 What exists

- **No archetype.** `EnemyAbilityArchetype` (`scripts\Enemies\EnemyAbilityData.cs:11-29`) has 9 values; `AreaPulse` (5) activates a square hitbox sized `PulseRadius*2` (`EnemyAbilityExecutor.cs:231-235`) — that is a damage pulse, not a channel. The design's "(`AreaPulse`, …)" label describes the *shape*; the mechanics require a new branch.
- **Closest precedent:** `PersistentFieldAtTarget` (archetype 8, added V7.3, documented **append-only**) → `scripts\Enemies\DilationFieldZone.cs`. That file is the right template: `Area2D`, `"persistent_construct"` group (so the rewind/world-freeze sweep reaches it), `IStoryRewindSimulation`, per-step group sweep by distance (deterministic under headless direct calls where no physics flush happens).
- **Meter chokepoint exists:** `PlayerController.DrainUltimateMeter(float points)` at `scripts\Characters\PlayerController.cs:2033-2039` (fractional-safe, routes through `FTT.Combat.UltimateMeter` when present).
- **Block classification exists:** `AttackClass` (`scripts\Combat\HitboxSystem.cs:6-11`), `Hitbox.BlockChargeCost`/`Unblockable`, and the V7.3 block-model closure (shieldstun, shatter lockout) are already in `PlayerController`/`BasicComboRules` — but they are driven by a **hitbox**, and the Snare deals no damage, so the "consume one charge with ordinary Basic block response, no HP chip" path needs an explicit non-damaging entry point.
- **Line-of-sight precedent exists:** `SearchlightZone` already occludes via `DirectSpaceState.IntersectRay` against `PersistentObject` bodies with headless-safe null guards (AGENTS.md Story section) — reuse that pattern for "authored solid cover".
- **Roll invulnerability** is already modelled (first 8 roll-travel frames, `UniversalMovementRules`).
- **Time Freeze does not exist.** The existing freeze surface is `IStoryRewindSimulation` + `ChronalRewindManager.FrozenSimulationGroups`.
- **F13 seal / F10 checkpoint reconstruction** are other workstreams' constructs; neither exists under these names in the repo.

### 2.3 Concrete change list (G1b)

1. `EnemyAbilityArchetype` — **append** `SiphonTether = 9` (never reorder; the ordinal is serialized). Add its `HasValidArchetypeFields` case (`PulseRadius > 0 && FieldDurationSeconds > 0 && MeterDrainPerSecond > 0`).
2. `EnemyAbilityData` — new exports: `MeterDrainPerSecond = 10f`, `MeterDrainCap = 30f` (reuse `PulseRadius` for the 3-unit radius and `FieldDurationSeconds` for the 3 s cap).
3. `EnemyAbilityExecutor.ExecuteArchetype` — new branch performing the **single attachment check** at active-start and constructing the channel node; must also expose an `IsChannelling` state so `EnemyController` can hold the Eraser stationary and suppress its own attack selection.
4. New `scripts/Enemies/SiphonTetherChannel.cs` modelled on `DilationFieldZone`: owner reference, target reference, per-live-tick drain via `PlayerController.DrainUltimateMeter`, accumulated-drain cap, the six break conditions, `IStoryRewindSimulation` freeze participation, `"persistent_construct"` group membership.
5. `EnemyController` — a "channelling" gate in `ProcessAttacking` (currently only `ChargeDash` gets special treatment, line 616) so the Eraser cannot move or select another attack; plus wiring the "successful stun/stagger interrupt" break (which must distinguish an armor-rejected hit — `IsStaggerArmored`, line 235 — from a real interrupt).
6. Per-player tether registry for the one-tether rule and the stable-ID tiebreak.
7. Non-damaging block absorption entry point on `PlayerController` (one charge, shieldstun, shatter-if-last, no HP chip, distinct-block perk once).
8. Telegraph presentation: a new `TelegraphGlyphShape` or a second glyph channel — `scripts\Enemies\TelegraphGlyph.cs:6-13` has exactly three shapes (`Basic`/`GuardCrush`/`Unblockable`) resolved from the class flags; the design wants a **tether/drain glyph beside the Basic cue**, i.e. an additive second glyph, plus a drawn 3-unit boundary ring.
9. Tests: the F14 validation matrix is spelled out in the design and should be transcribed 1:1 — front/rear/zero-charge/final-charge blocks; roll / cover / range boundaries; interruption vs. armor; starting meter 0 (cast rejected) / 5 (drains to 0) / 100 (ends at 70 if uninterrupted); two Erasers; Suppression break; Time Freeze; checkpoint load; Defy/Ultimate spend ordering; **absence from Fighter Mode**.

**Rough size: L.** This is the single largest new mechanic in the section.

---

## 3. G2/G3/G4 — The final boss (First Unbound) and Forge Sentinel

### 3.1 Renames (G2)

| Design name | Repo ID | Files |
|---|---|---|
| **The Forge Sentinel** (L14, "The Unbound Bastion") | `archive_prime` | `resources/Bosses/archive_prime.tres`, `resources/Bosses/Abilities/archive_prime/`, manifest line 131, `en.csv:145` (`boss_archive_prime_name,Archive Prime`), `scripts/Combat/EnemyAbilityVisualLibrary.cs:30`, `resources/SpriteFrames/Bosses/archive_prime_*`, `scripts/Environment/Level14Controller.cs` |
| **The First Unbound** (L15, "The Meridian Founding") | `apex_eraser` | `resources/Bosses/apex_eraser.tres`, `resources/Bosses/Abilities/apex_eraser/` (5 abilities), manifest line 132, `en.csv:146`, `EnemyAbilityVisualLibrary.cs:29`, `scripts/Environment/Level15Controller.cs` (comments at lines 28, 60–62, 79 read "the Eraser") |

Recommendation: **change display strings only** (`en.csv` values + the `DisplayName` fields), keep the resource IDs. A full ID rename fans out into the manifest, the VFX library's hardcoded `BossIDs` set, sprite-frame filenames, `AbilityID`/`PresentationEventID` strings in five ability resources, boss-intro seen-set keys persisted in saves (`StoryManager.RecordBossIntroSeen`, `scripts/Core/StoryManager.cs:484-489`), and every level-content test. Size: **S** for display-only, **M** for a true rename.

Also in scope for G2/G7: Level 14's era label ("Neo-Earth" → "The Unbound Bastion") and Level 15's ("Library of Alexandria Restoration" → "The Meridian Founding") in `en.csv` level-title keys — coordinate with the campaign/narrative workstream, which owns those keys.

### 3.2 First Unbound P2 self-rewind (T01b Option A) — G3

**Requires:** once per encounter, on a **nonlethal** crossing of 66% HP: rewind its position by 3 s, recover toward sampled HP capped at **20% of difficulty-scaled max HP** (provisional); use the oldest real history if younger; validate historical position → current position → a safe authored anchor; **suspend combat for 1.5 s** preserving remaining timers/world state; **no player charge spend**; latch phase progression; retain cooldowns/statuses; resume with a full attack telegraph. Lethal damage wins. A nonlethal crossing of 33% queues P3 even if healing raises HP. No discarded damage, phase regression, or repeat rewind. Contract: `docs/TEMPORAL_STATE_CONTRACT.md#first-unbound-capped-historical-recovery-t01b--option-a` (docs-3).

**Exists:** nothing. `scripts\Enemies\BossController.cs` has `CheckPhaseTransition` (line 552), `PhaseTransitioning` invincibility (`IsPhaseInvincible`, line 89), and `IStoryRewindable`/`IStoryRewindSimulation` **only** for the player's rewind (`CaptureCheckpointState` line 831, `ApplyStoryRewind` line 838 — checkpoint snap, not a rolling buffer). There is no position/HP history ring, no heal path (`ApplyBossDamage` only subtracts), and no combat-suspend other than the phase-transition state.

**Change list:** a bounded position/HP ring on `BossController` (3 s = 180 frames; the player's equivalent is `ChronalRewindBuffer`, which could be generalized); a once-per-encounter latch; a heal-with-cap path; destination validation against the three fallbacks; a 1.5 s suspend distinct from `PhaseTransitioning` that preserves timers; and the 33%-queues-P3-regardless rule in `CheckPhaseTransition`. **Size: L.** Note the contract doc lives in docs-3 and is not mirrored into the repo.

### 3.3 Borrowed Legacies P3 (V7.5) — G4

**Requires:** in P3 the villain borrows **signature moves from the roster characters the player did not pick**, reusing existing kit assets, each move visually tearing from a specific cradle; the **borrowed set is data-driven from the current roster minus the active character** so it scales as the roster grows.

**Exists:** nothing remotely like it. A boss's kit is a static authored array:
`BossData.BossAbilities : EnemyAbilityData[]` + `AbilityMinPhase : int[]` (`scripts\Enemies\BossData.cs:43-47`), consumed by `BossController.SelectAbilityIndex` (line 433). `apex_eraser.tres` already gates by phase (`AbilityMinPhase = Array[int]([0, 0, 1, 0, 2])`, so a phase-3 slot exists) but every entry is a fixed `EnemyAbilityData`. Nothing in `scripts/Enemies/` reads `CharacterData` or `FTT.Combat.AbilityData`.

**The only existing bridge is the Mirror Paradox**, which builds a real `PlayerController` clone via `CharacterFactory.CreateCharacter(..., applyStoryProgression: false)` and drives it with the Fighter CPU (`scripts\Enemies\MirrorParadoxController.cs:152-193`). That is an *entire character*, not a borrowed move.

**Change list (design decision needed first):** either
(a) a **projection layer** that maps each character's `AbilityData` (`resources/Abilities/*.tres`) onto an `EnemyAbilityData`-shaped execution for the shared executor — needs an archetype/damage/frame mapping per ability and will not cover bespoke construct/zone abilities; or
(b) a **runtime composite kit** on `BossController` that appends dynamically-built abilities when P3 opens, selected from `GameManager.CurrentSession.SelectedCharacterID` complement.
Either way: a `BorrowedLegacies` selector keyed off the session character, phase-3 gating, per-move cradle VFX (`AbilityVisualLibrary` already resolves every canonical ability ID for Story cast/projectile/zone paths — a real asset-reuse lever), and a scaling-safe content test ("for each of the 9 characters, the borrowed set is the other 8"). **Size: L**, and it is the section's biggest *design*-shaped unknown.

---

## 4. G5 — Mirror Paradox (F20 Option A)

### 4.1 Requirement vs. repo, parameter by parameter

| F20 parameter | Design | Repo today | Verdict |
|---|---|---|---|
| Reaction delay E/N/H | 30–45 / 15–20 / 4–8 frames | `FighterCpuController.GetReactionDelayBounds` (`scripts\FighterSim\FighterCpuController.cs:362-376`) = 30–45 / 15–20 / Hard consts | **Already matches** — only the tier selection is wrong |
| Block attempt E/N/H | 10 / 40 / 80 % | `CpuBandTuning.BlockPercent` = 10 / 40 / 80 (lines 751+, 785+, 820+) | **Already matches** |
| Block-through-hitstun, once per instance | 10 / 45 / 85 % | `HitstunDefensePercent` = 10 / 45 / 85 (V7.4) | **Already matches** |
| Launching-hit DI | none / 40 / 80 % | `DiPercent` = 0 / 40 / 80 (V7.4) | **Already matches** |
| HP from 1,000 base | 700 / 1,000 / 1,500 | `MirrorParadoxController.SpawnMirror` line 161 → `StoryDifficultyTuning.ScaleEnemyHP(1000, difficulty)` with multipliers 0.7 / 1.0 / 1.5 (`scripts\Core\StoryDifficultyTuning.cs:13-18`) = **700 / 1000 / 1500** | **Already exact** |
| Outgoing damage ×0.5 / 1.0 / 1.5 | yes | `GetEnemyDamageMultiplier` = 0.5 / 1.0 / 1.5 — but the clone is a `PlayerController`, and it is unclear whether that multiplier is applied to it at all | **Verify** — likely a gap |
| CPU difficulty from **Story** difficulty, not Holodeck | required | `MirrorParadoxDecisionAdapter` ctor hardcodes `new FighterCpuController(CpuDifficulty.Hard, seed)` (`MirrorParadoxController.cs:360`) | **Gap** |
| **Full core kit on every difficulty** (both Specials, movement, Ultimate) even on Easy | required; "do not inherit Easy practice-CPU kit restrictions" | `CpuBandTuning.Easy` zeroes `SpecialOneClosePercent`, `SpecialOneRangedPercent`, `SpecialTwoPercent`, `MovementAbilityPercent`, `UltimatePercent` | **Gap** — needs a boss-override `CpuBandTuning` (the ctor already accepts `CpuBandTuning? tuningOverride`, line 113 — the seam exists) |
| Easy/Normal Ultimate at first legal opportunity at full meter | required | `Normal.UltimatePercent = 100, RequiresUltimateSetup = false`; `Easy = 0` | **Gap on Easy only** |
| Grab / Echo Step tiering (F19) | Easy none / Medium occasional / Hard tactical | **`FighterCpuController` contains no `Grab` or `EchoStep` at all** (grep confirms; `EchoStep` exists in `PlayerController`, `BasicComboRules`, `FighterSimulation*` but not in the CPU) | **Gap — and it is the Fighter-CPU workstream's, not this section's** |
| Mirror purchased grid perks **on Hard only** | required | `CreateCharacter(..., applyStoryProgression: false)` always — perks are **never** mirrored | **Gap** |
| Holodeck setting must not change it | required | Already true (nothing reads the Holodeck CPU setting here) | **Already satisfied** |
| One phase, existing knockback setting, once-only physical dust | required | `PhaseCount => 1` (line 76), `mirror_paradox.tres` `PhaseThresholds = []`, 50 dust via `MirrorParadoxEncounterController` | **Satisfied**, except dust becomes 25 under F05 |
| Per-character recovery profiles (`CPU_RECOVERY.md`) | required | The Story adapter deliberately zeroes `HasStageBounds`/`HasOrb`/`HasHazard` so the shared table's **off-stage recovery branch never fires** in Story (`MirrorParadoxController.cs` `Observe()` remarks) — correct for a campaign level with no blast zone, but it means `CPU_RECOVERY.md` is a **Fighter-side** contract, not a Mirror one | Note, not a gap here |

### 4.2 Change list (G5)

1. `MirrorParadoxController` / `MirrorParadoxDecisionAdapter` — take a `CpuDifficulty` mapped from `StoryDifficultyTuning.CurrentStoryDifficulty` instead of the hardcoded `Hard`; thread a **boss-override `CpuBandTuning`** through the existing `tuningOverride` seam that re-enables Specials/movement/Ultimate on Easy while leaving grabs/Echo Step off.
2. Hard-only perk mirroring: call `CreateCharacter(..., applyStoryProgression: true)` on Hard **and** populate `StoryAbilityPerks` from the save's purchased nodes — while keeping "do not copy wallet, current HP, cooldowns, spent flags"; confirm the F20 "single application of stat modifiers" rule against `EncounterMaxHPOverride` (line 171), which currently *replaces* the HP pool, so a perk `MaxHP` bonus must not stack a second base pool.
3. Verify/wire the outgoing campaign damage multiplier for the clone.
4. `resources/Bosses/mirror_paradox.tres` — the design table now annotates melee/ranged 3.0 / 8.0 vs. the repo's 2.0 / 5.0 and `DistanceBased` vs. the repo's `WeightedRandom` (0). Design says that metadata is "generic … F20 campaign CPU profile governs decisions", so this is cosmetic — but `EnemyRosterContentTests` already exempts the mirror from the distance-band rule, so leaving it is safe.
5. Tests: extend `tests/unit/MirrorParadoxTests.cs` with a per-difficulty matrix (reaction bounds, block rate, HP 700/1000/1500, full kit on Easy, Hard-only perks, Holodeck-setting independence).

**Size: M** (excluding F19 grab/Echo Step, which is another workstream).

---

## 5. G6 — L1–L4 boss HP, and the boss-table conflict

### 5.1 The numbers

| Level | Boss ID | Repo `.tres` | Old GDD table | New GDD target | Δ vs repo |
|---|---|---|---|---|---|
| 1 | `borgia_inquisitor` | **500** | 500 | **350** | −150 (a cut) |
| 2 | `siegemaster_duke` | **540** | 650 | **520** | −20 |
| 3 | `chronal_inventor` | **560** | 800 | **700** | **+140 (an increase)** |
| 4 | `revolutionary_tribunal` | **590** | 900 | **850** | **+260 (an increase)** |

### 5.2 The conflict, stated plainly

The GDD's own boss table has **never** matched the authored resources, and both documents claim authority over the other:

- The GDD table's own preamble (master line 1612 / repo mirror line 1426) says **"The authored `resources/Bosses/*.tres` files are canonical"**.
- `docs/PACKAGE4_ROSTER_PLAN.md` §4.1 (repo) explicitly records that all four Wave A agents found "the design doc's boss list … was superseded by Package 4" and lists the corrections, e.g. *"`chronal_inventor` 560/2.5 (not 800/3.5)"*, *"`apex_eraser` is 1200 HP, not 3000"*.
- The V7.6 ruling annotates the cut as *"was 500 / 650 / 800 / 900"* — i.e. it was applied to the **GDD table**, not to the shipped resources. Two of the four "cuts" are increases against the build.

The disagreement extends far past L1–L4: every Act II/III boss differs (repo `tidal_eraser` 640 vs 1050, `vulcan_decimator` 660 vs 1200, `dread_admiral` 700 vs 1350, `jackal_priest` 700 vs 1500, `iron_chancellor` 780 vs 1700, `tragedy_king` 800 vs 1850, `siege_cannon` 880 vs 2000, `gravity_overseer` 950 vs 2200, `archive_prime` 1050 vs 2500, `apex_eraser` 1200 vs 3000). The design table also lists `Sequential` / `Random` patterns and a not-knockback-immune Tribunal with `[0.66, 0.33]`, none of which the build has (`BossAttackPattern` has only `WeightedRandom` and `DistanceBased`; `revolutionary_tribunal.tres` is `[0.5]`, `DistanceBased`, knockback-immune).

`docs-3/docs/DESIGN_BUILD_DEVIATIONS.md` (P04, 2026-09-13) is the governing rule here: the GDD and its adopted decisions define **intended behavior**; a silent discrepancy is "unresolved documentation, not permission to pick either version". **This needs an explicit user ruling before any `.tres` is touched.**

### 5.3 Change list if the new numbers are adopted

1. `resources/Bosses/{borgia_inquisitor,siegemaster_duke,chronal_inventor,revolutionary_tribunal}.tres` — `MaxHP` → 350 / 520 / 700 / 850.
2. `tests/ContentValidation/BossRosterActITests.cs:57-67` — `ActIHealthPoolsStayInBandAndAscendWithTheCampaignLevel` hardcodes `AssertThat(previous).IsEqual(500)` for Borgia and a **band of 500–700** for levels 2–7, strictly ascending. 350 is below the floor, 850 above the ceiling, and 850 > 640 (`tidal_eraser`, L5) breaks ascending order. This test must be rewritten, and the rewrite is only coherent if the whole 15-boss curve is re-tuned together.
3. `tests/ContentValidation/EnemyManifestTests.cs:36` — `AssertThat(boss.MaxHP).IsEqual(500)` for Borgia → 350.
4. `docs/PACKAGE4_ROSTER_PLAN.md` §4.1 table + its "corrections this table supersedes" paragraph.
5. `docs/PACKAGE5_CAMPAIGN_PLAN.md` references, and `AGENTS.md` if the baseline moves.
6. Boss-arena width checks: §4.1 notes `BossController.PixelsPerUnit = 60` and that per-level content tests assert `RangedRangeThreshold * 60 < arenaWidth` — HP changes alone do not move those, but adopting the design table's melee/ranged thresholds would.

**Size: S** for the four values; **M** if the Act II/III curve is reconciled at the same time; the *decision* is the expensive part.

---

## 6. G7/G8 — Smaller items

**G7 — Lore relabels.** "Apex Archive" → "the Unbound" throughout §6 prose; the Chrono-Warden's name is explicitly retained ("a stolen title worn mockingly"); "Future Cultist" is explicitly **retained as the mechanical class label in manifests and tests**, so no manifest/test churn. Repo surfaces: `en.csv` display strings for boss/level names, and a large number of code comments referencing "Archive"/"Apex Archive" (`scripts/Environment/Level14Controller.cs`, `Level15Controller.cs`, `docs/`). Coordinate with the narrative workstream — it owns `en.csv` dialogue and level titles. **Size: S.**

**G8 — The Chrono-Warden is authored but never spawned.** `grep -rln chrono_warden scenes/ scripts/Environment/ resources/Pools/` returns **nothing** — the only non-test consumer is `scripts/Combat/EnemyAbilityVisualLibrary.cs`. V7.1 design law says "first appearance Level 6, then salted through Levels 7–15 alongside the Tech-Enforcer"; the build never placed it. The Eraser is about to inherit exactly the same failure mode unless placement is part of the same change. Also relevant to the new "never alongside a Chrono-Warden in the same room until Act III" rule, which is currently vacuously true. **Size: S–M** (spawn-table entries in Level06–Level15 controllers + their per-level content-test encounter counts + elite pool warm counts).

**Also unclosed from the same bullet (design law, "stay law"):** mob hazard immunity, "mobs cannot recover from pits", elites/bosses bounded to platforms — none implemented; and the Behaviour Variety Directive's **SummonMinions mob line** ("an Unbound drone-carrier variant") still has no mob owner (the 5 `SummonMinions` abilities in the repo all belong to **bosses**: `revolutionary_tribunal` ×2, `tragedy_king`, `archive_prime`, `apex_eraser` — pinned by `EnemyRosterContentTests:162`).

---

## 7. Shared files and cross-workstream dependencies

| Shared surface | Who owns it | What this section needs from it |
|---|---|---|
| `scripts/Core/EventBus.cs` — `StatusType` enum (line 260) | **status-schema workstream** | Append `Suppression` (control slot). Ordinals serialize into `.tres` — append-only. **Hard blocker for Null Lance.** |
| `scripts/Characters/PlayerController.cs`, `scripts/Combat/StatusController.cs`, `BaseSpecial` | **status-schema workstream** | `SuppressionEffect` strategy; the Special 1/2/movement/ultimate cast lock with cooldowns still ticking; grey-aura smother; HUD lock cross-out. Also a non-damaging block-absorption entry point for Siphon Snare. |
| `localization/en.csv` (+ compiled `en.en.translation`) | shared by everyone | ~4 Eraser keys + Suppression HUD/status strings + 2 ambush bark lines + boss/level rename values. **Must re-run `--headless --import` and commit the regenerated `.translation`** — the roster tests resolve through the compiled resource. |
| `resources/Content/content_manifest.csv` | shared | 1 new `Enemy` row; boss rename rows if IDs change. |
| `resources/Enemies/*.tres`, `resources/Bosses/*.tres` — `ChronalDustDrop` | **dust-economy workstream (F05)** | Elites lose the flat 10; bosses 50 → 25. Directly rewrites `EnemyRosterContentTests.TierDustRewardsConformToTheEconomyAcrossTheWholeRoster`. The Eraser's dust cannot be finalized independently. |
| `scripts/FighterSim/FighterCpuController.cs` (`CpuBandTuning`, F19 grabs/Echo Step) | **Fighter-CPU workstream** | Mirror Paradox F20 needs the boss-override band and, for Normal/Hard parity, `Grab`/`EchoStep` actions that do not exist. |
| Time Freeze (F03) | **time-systems workstream** | Siphon Snare must pause its channel/drain/cast/cooldown during it and re-evaluate breaks on thaw with no catch-up ticks. Absent from the repo entirely. |
| F10 checkpoint reconstruction, F13 seal | **persistence / survivability workstreams** | Snare must end the transient tether on reconstruction, never repeat a tick or refund meter; and must flip the F13 seal to Not Ready when an unused full meter is drained. |
| Level 4A Legacy Level scenes/routes | **campaign workstream** | The Eraser's scripted debut and its independent route trigger (F12, no middle-checkpoint dependency); plus a `level_04a_pool_config`. |
| `scripts/Environment/Level*Controller.cs` spawn tables + `tests/ContentValidation/Level*ContentTests.cs` | **campaign workstream** | Every Eraser/Warden placement moves a locked encounter-count assertion (e.g. `Level13ContentTests:202-213`). |
| `AGENTS.md` | shared | Roster counts, status-effect list, elite behaviour, test baseline (currently **1638**), known-gap bullets. |
| `docs/PACKAGE4_ROSTER_PLAN.md` | this section | §4.1 roster IDs + a new §8 deviation entry. |

---

## 8. Ordering recommendation

1. **Decide G6** (the boss-HP authority conflict) before touching any boss `.tres` — it is a one-line ruling that unblocks a test rewrite.
2. **Status workstream lands `Suppression`** (enum + effect + ability lock + visuals). Nothing in G1a can be authored past a placeholder until then.
3. Land the **Basic-class override** on `EnemyAbilityData`/`EnemyController`/`EnemyAbilityExecutor` (blocker (b)) — small, self-contained, and needed by Null Lance.
4. Author the **Eraser resources + manifest + keys + VFX vocabulary**, with Siphon Snare initially stubbed as a telegraph-only cast; ship the Level 13 pair.
5. Build **`SiphonTetherChannel`** once Time Freeze exists (or with a documented deviation that the freeze rule is deferred).
6. **G5 Mirror Paradox** is independent and can run in parallel — it is mostly wiring existing, already-correct numbers to the Story difficulty.
7. **G3/G4** (First Unbound self-rewind and Borrowed Legacies) are large and independent; G4 needs a design decision on the projection mechanism before any code.
8. Close **G8** (Warden placement) in the same pass as the Eraser salt, so the two elites' placement rules are authored and tested together.
