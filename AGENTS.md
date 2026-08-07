# Fighters Through Time agent context

This file applies to the entire repository. It is the durable orientation document for AI agents working on this project. Keep it synchronized with material architecture or scope changes.

`CLAUDE.md` imports this file and adds the local toolchain (Godot executable locations, verified commands), the `docs/` authority map, and Claude Code configuration. Tool-specific setup belongs there; durable project truth belongs here.

## How to use this context

1. Read this file before changing the project.
2. Read the relevant section of `design-godot.md` before implementing gameplay, content, UI, networking, save, or performance behavior. The design document is the full product and technical specification; this file is a working summary and repository map.
3. Inspect the current scripts, scenes, resources, and `project.godot` before editing. The design document describes the target, while the repository contains a partially implemented prototype.
4. Treat explicit user instructions as highest priority. Otherwise, use `design-godot.md` for intended behavior and the existing code for current behavior. When they disagree, do not silently normalize the difference: call it out, implement toward the design when that is in scope, and avoid expanding the task into an unrelated migration.
5. Clearly distinguish `implemented`, `prototype/placeholder`, `planned`, and `deferred` behavior in plans and handoffs.

## Product identity and non-negotiable pillars

`Fighters Through Time` is a 2D game that combines side-scrolling action-adventure gameplay with a platform-fighter mode. The nine playable characters are historical figures whose exaggerated abilities are manifestations of humanity's collective memory, called Temporal Resonance.

Core design contracts:

- An ability must make sense in both modes. The same move used for Story Mode combat or a puzzle should translate naturally to Fighter Mode.
- Combat uses standard HP, not a Smash-style accumulating knockback percentage. At 0 HP a fighter is knocked out. In Fighter Mode, falling through the bottom blast zone also costs one stock; the sides and top are solid boundaries.
- Story progression and the Temporal Resonance Grid affect Story Mode only. Fighter Mode remains competitively normalized.
- All nine characters are available from the start in the initial build. Character unlocking is a later balance/content phase.
- A campaign save locks the selected character for the whole playthrough; there is no mid-campaign character swapping.
- The initial Fighter Mode scope is 1v1 only: local shared-screen/LAN and online rollback. Four-player free-for-all and 2v2 are post-launch work and must not be added during initial development.
- Delay-based online combat is not acceptable. The intended final online implementation is deterministic GGPO-style rollback.
- Initial builds are English-only but must be localization-ready from day one. User-visible strings use translation keys, not hardcoded English.
- Alternate costumes, cosmetic items, full-match replays, detailed post-match stat sheets, online account/profile systems, and Training Mode are outside the initial scope unless the user explicitly reprioritizes them.

## Narrative and mode context

The Apex Archive is a future technocratic cult siphoning energy from pivotal historical moments. An overload at the Library of Alexandria fractures history, opens Chronal Rifts, displaces the selected hero, and grants Temporal Resonance powers. The Chrono-Resistance, led by Commander Sarah, operates from a hijacked Archive Time-Ship that serves as the Story Mode hub.

The campaign follows only the player's locked-in historical figure and the Chrono-Resistance crew. Other playable roster characters do not appear as hub NPCs or campaign companions. Fighter Mode is narratively framed as sparring in the Time-Ship's Temporal Arena.

Story Mode consists of a hub plus levels 0 through 15:

| Level | Era / purpose |
|---|---|
| 0 | Intro and Chronal Integration tutorial |
| 1 | Florence, 1503 — Steampunk Renaissance |
| 2 | Orléans, 1429 — Siege of Orléans |
| 3 | Chicago, 1893 — World's Fair |
| 4 | Paris, 1789 — Storming of the Bastille |
| 5 | Titanic, 1912 — Act I finale |
| 6 | Pompeii, 79 AD |
| 7 | Nassau, 1715 — Golden Age of Pirates |
| 8 | Alexandria, 30 BC — Cleopatra's palace |
| 9 | Berlin, 1961 |
| 10 | London, 1599 — Globe Theatre |
| 11 | Gettysburg, 1863 |
| 12 | Lunar Landing, 1969 — Act II finale |
| 13 | Chronal Void transition |
| 14 | Neo-Earth / Apex Archive future |
| 15 | Library of Alexandria restoration and final boss |

The full level hazards, puzzles, bosses, dialogue, and asset requirements are in Sections 2, 3, 6–10, 16, and 17 of `design-godot.md`.

## Roster

The initial roster is fixed at nine characters:

| ID | Character | Intended combat identity |
|---|---|---|
| `einstein` | Albert Einstein | Zoner/setup; spacetime and gravity |
| `joan` | Joan of Arc | Beginner-friendly melee rushdown; radiant hyper-armor |
| `leonardo` | Leonardo da Vinci | Gadget/hybrid; inventions and persistent turret |
| `lincoln` | Abraham Lincoln | Heavy/juggernaut; rail strikes and shockwaves |
| `cleopatra` | Cleopatra | High-skill trapper/puppeteer; sand and serpents |
| `tesla` | Nikola Tesla | Ranged setup; linked coils and electricity |
| `shakespeare` | William Shakespeare | Summoner/puppeteer; barriers and spectral actors |
| `mozart` | Wolfgang Amadeus Mozart | Ranged/tempo; sonic waves and musical platforms |
| `pocahontas` | Pocahontas | Mobile scout; glide, roots, and nature spirits |

Canonical kits, talent grids, and tuning live in Section 5 of `design-godot.md`. Current baseline character stats live in `resources/Characters/*_data.tres`.

The design document mentions `character_base_stats.md` and `ability_numeric_data.md`. Those were Unity-era numeric proposals and were removed from `docs/` on 2026-08-06 along with the rest of the pre-Godot archive; copies survive outside the repository in `D:\Projects\fighters-through-time-docs-2\`. Do not treat them as authority or reintroduce them: they assume Unity `ScriptableObject` fields and a ten-character roster including Stephen Hawking, who is not in the current nine. `design-godot.md` Section 5 and the `resources/` `.tres` files are canonical.

## Technology and runtime targets

- Engine in the current repository: Godot .NET 4.7.1 (`Godot.NET.Sdk/4.7.1`). The GDD's minimum is Godot 4.4+.
- Language/runtime: C# on .NET 10 (`net10.0`, SDK 10.0.302 pinned by `global.json`). Root namespace: `FTT`. Migrated from .NET 8 on 2026-08-06; Godot 4.7.1 loads the `net10.0` assembly and the full suite passes. `design-godot.md` Section 12 still says .NET 8 — the project file is authoritative.
- Current package/runtime dependencies: `K4os.Compression.LZ4` 1.3.8, Klotho's `Newtonsoft.Json` 13.0.4 and `LiteNetLib` 2.1.4 runtime dependencies, plus Debug-only GdUnit4 test packages.
- Current renderer setting: `gl_compatibility`, using Godot's 2D `CanvasItem` stack. The intended art pipeline uses `Light2D`, `CanvasModulate`, `LightOccluder2D`, normal maps, and custom `CanvasItem` shaders.
- Target platforms: Windows, macOS, Linux/SteamOS, with Steam Deck performance in mind.
- Simulation and presentation target: locked 60 FPS / 60 Hz physics.
- Reference resolution: 1920×1080. `project.godot` and `ViewportEnforcer` use fixed `keep` aspect behavior with black letterbox/pillarbox clear areas.
- Performance target: 16.67 ms per frame, no gameplay-time GC spikes, at most 4 GB runtime RAM, roughly 150 draw batches, 150,000 visible vertices, 60 active `AnimatedSprite2D` nodes, and 500 active particles. Pool short-lived gameplay objects.

### Dependency status

Klotho v0.6.1 and GdUnit4 v6.2.0 are pinned under `addons/`. Klotho is experimental; the fixed-point simulation and rollback foundation are implemented, but the implementation has not completed production rollback performance/platform gates. Steam Networking Sockets and Steam transport are not installed. Do not describe the existing networking path as production netcode.

## Current repository map

```text
project.godot                    Godot settings, InputMap, autoloads, main scene
FightersThroughTime.csproj       Godot .NET project and NuGet dependencies
AGENTS.md                        This file: durable agent orientation
CLAUDE.md                        Claude Code entry point; imports AGENTS.md, adds toolchain and docs authority map
.claude/settings.json            Committed Claude Code project settings (GODOT_BIN, permissions)
.claude/commands/                Project slash commands (/validate, /godot-run)
design-godot.md                  Full game design and technical specification
localization/en.csv              English translation keys
resources/Characters/            Nine CharacterData .tres resources
resources/Abilities/             Thirty-six canonical character ability resources
resources/Enemies/, Bosses/      Canonical enemy and boss resources plus their Abilities/ EnemyAbilityData sets
resources/Resonance/             Nine authored 3x3 Story Resonance Grids
resources/FighterStages/         Ten-stage Fighter catalog and prototype presentation data
resources/Dialogue/              DialogueSetData resources for hub, tutorial, and Florence
scenes/menus/                    Main menu and character select
scenes/campaign/                 Hub, tutorial, and Florence prototype scenes
scenes/arenas/TestArena.tscn     Current combat test arena
scenes/fighter/                  Production-contract Fighter stage scenes (Florence Workshop)
scenes/ui/                       Authored dialogue box, Story HUD, and level results scenes
scenes/enemies/                  Authored StandardEnemy/EliteEnemy/Boss/EnemyProjectile scenes
scenes/characters/Player.tscn    Minimal reusable player scene
scripts/Core/                    Persistent managers and cross-cutting services
scripts/Characters/              Player, character factory, training dummy, abilities
scripts/Combat/                  Ability, hitbox, block, status, meter, match systems
scripts/Enemies/                 Enemy/boss controllers, AI, and prototype factory
scripts/Environment/             Hub, levels, rewind, hazards, dust, level flow
scripts/UI/                      Menus, HUD, dialogue, settings, pause UI
scripts/Networking/              LAN UDP transport, packets, rollback session, room-code foundation
scripts/FighterSim/              Klotho fixed-point 1v1 simulation and Godot bridge
tests/                            GdUnit4 C# content/unit/integration/determinism tests
addons/gdUnit4/                  Pinned test framework
addons/klotho/                   Pinned experimental deterministic runtime
IMPLEMENTATION_PLAN.md           Ordered delivery plan and package sequencing
IMPLEMENTATION_STATUS.md         Long-form implemented-vs-designed gap analysis
docs/IMPLEMENTATION_STATUS.md    Concise roadmap execution ledger (a different document from the root file of the same name)
docs/PACKAGE3_KIT_AUDIT.md       36-slot character kit audit and conversion order
docs/architecture/               Accepted ADRs 0001-0006
docs/BUILDING.md                 Build and validation procedure
docs/PERFORMANCE_BASELINE.md     Package 0 performance baseline
docs/development-plan.md         Earlier 12-stage Godot plan; superseded by IMPLEMENTATION_PLAN.md
assets/                          Intended visual assets; currently sparse
audio/                           Intended music/SFX assets; currently sparse
```

Do not edit or commit generated `.godot/` cache/output files as part of feature work. Godot-generated `.uid` sidecars beside scripts are project metadata and should stay paired with their source scripts.

## Scene architecture and flow

- Main scene: `res://scenes/menus/MainMenu.tscn`.
- Scene changes use single-scene replacement through `GameManager.LoadScene`, `ResourceLoader.LoadThreadedRequest`, and `SceneTree.ChangeSceneToPacked`.
- `GameManager` owns the persistent loading overlay and currently enforces a two-second minimum display time.
- Cross-scene selections live in `GameManager.CurrentSession` (`SessionData`): selected character, selected stage, active save slot, difficulty, and `MatchSettings`.
- Default match settings are Stock mode, 3 stocks, 480 seconds, items on/high, and hazards on/high.
- `StoryManager` defines target paths for levels 0–15, but only Tutorial and Florence scene resources exist. It validates a route before loading and reports missing production levels instead of changing to a broken scene. Hub/tutorial/Florence are functionally complete flows with placeholder presentation; the rest of the campaign is planned.
- `TestArena` uses `FighterSimulationDriver`: Klotho state is authoritative and the two `PlayerController` nodes are collision-free presentation adapters. The local human/CPU match core, rules, results, rematch flow, stage select, and ten distinct deterministic hazard identities are functional. Florence Workshop (`scenes/fighter/FighterStage_Florence.tscn` with `FighterStageController` and `FighterStageGeometry.Florence`) is the first production-contract stage: unique fixed-point walls, two one-way platforms, authored hazard/orb anchors, authored spawn markers, and `FighterCamera` midpoint/zoom. The other nine catalog entries still reuse the shared Test Arena template; their production geometry, art, and presentation remain incomplete.
- Campaign scenes attach shared services through `StorySceneBootstrapper` (`scripts/Environment/`): a resource-driven `DialogueManager`, reusable `StoryHUD`, pause menu, and (for levels) the rewind manager and presentation overlay. Dialogue content lives in `DialogueSetData`/`DialogueSequenceData` resources under `resources/Dialogue/`.
- The Tutorial (fracture presentation, calibration, mobility gates, combat trial), Florence (gear-puzzle room gating, triggered waves, boss, results, autosave), and hub (Calibration Bay anchor, Holodeck CPU practice loop that returns to the hub, Sarah NPC, dust auto-deposit) flows are functionally complete with placeholder presentation. `StoryDifficultyTuning` scales Story enemy HP/damage at spawn without touching Fighter loadouts.
- Several current scenes build placeholder visuals, geometry, actors, and UI programmatically in their controller scripts. This is valid prototyping state, not the intended final asset/scene-authoring pipeline.

## Autoloads and system ownership

`project.godot` currently registers these persistent autoloads:

- `EventBus`: typed C# events/payloads for non-adjacent system communication.
- `GameManager`: session data, defaults, and async scene transitions.
- `StoryManager`: campaign level, dust, rewind count, and hub/level flow.
- `PoolManager`: scene-pool registration, warm-up, spawn/release, and overflow policies.
- `InputManager`: action names, local controller assignment, dedicated Roll capture, and deterministic Dash gesture conversion (digital double-tap or analog flick).
- `SaveManager`: three story slots and global settings/statistics.
- `AudioManager`: AudioServer bus control and music/SFX entry points.
- `HapticFeedbackManager`: controller vibration settings and triggers.
- `CameraShake`: shared hit/impact feedback service.
- `LocalizationManager`: translation setup and lookup support.
- `ViewportEnforcer`: viewport/aspect enforcement hook.

Use the existing static `Instance` pattern for these services. Non-adjacent systems should communicate through `EventBus`; adjacent scene relationships should use exported references or explicit typed `GetNode<T>()` paths. Subscribe in `_Ready()` and unsubscribe in `_ExitTree()`. Avoid recursive `FindChild()` and repeated string-based scene-tree searches.

`NetworkManager` and `MatchmakingManager` are not current autoloads and are not wired into production flow.

## Gameplay architecture

### Character data and runtime controller

- Static identity and baseline tuning are Godot `Resource` objects (`CharacterData`) stored as `.tres` files.
- Story runtime behavior is owned by `PlayerController : CharacterBody2D`. Fighter runtime behavior in the Test Arena is owned by `FighterSimulation`; `PlayerController` is presentation-only there.
- The canonical state set is `Idle`, `Running`, `Dashing`, `Rolling`, `Skidding`, `Crouching`, `Airborne`, `Attacking`, `UsingSpecial`, `UsingUltimate`, `Blocking`, `Stunned`, `Dazed`, `LedgeHanging`, `Dead`, `Respawning`, and `UsingMovementAbility`.
- Universal movement uses an eight-frame grounded run ramp, a 12-frame non-invulnerable `1.35x` dash, and a 4-startup/12-travel/10-recovery evasive roll. The first eight roll-travel frames are invulnerable. Dash keeps the combatant pushbox; roll travel disables it. These timings live in `UniversalMovementRules` and must remain aligned between Story and Fighter simulation.
- Movement also includes direction-reversal skid, coyote time, jump buffering, short-hop behavior, character-specific jump counts, air control, crouching, one-way platform drop-through, ledge hanging, and recovery.
- Story Mode may use native Godot `CharacterBody2D` physics. Local Test Arena Fighter Mode already uses Klotho fixed-point authority and must remain on that boundary; online work must not move authority back into `PhysicsServer2D`.

### Combat

- Hit detection is conceptually `Area2D` hitboxes against `Area2D` hurtboxes. Final activation timing should come from exact animation-frame callbacks, not broad body collisions.
- The basic combo has three hits using `0.8×`, `1.0×`, and `1.5×` `CharacterData.BasicAttackDamage`. Crouched and aerial attack inputs use the same three-hit string unless the design is explicitly revised.
- Specials and ultimates use `AbilityData.BaseDamage`; multi-hit totals derive from per-hit damage and hit count.
- Knockback target: `baseKnockback / (1 + target.Weight)`. There is no armor/defense stat that reduces HP damage.
- A special may cancel a basic attack when allowed by the FSM/cooldown rules. Block may cancel only during recovery frames.
- Blocking stops movement and is front-facing. Three basic hits exhaust the three shield charges; any special shatters the shield; ultimates bypass it. Guard break causes a one-second daze.
- The Influence/Ultimate meter ranges from 0 to 100. Damage dealt adds 1 point per HP; damage taken adds 0.25 per HP. Using the ultimate resets it. Fighter stock loss retains 75% of the current meter.
- Fighter Mode is HP plus stock, not ring-out-only combat. A timer draw compares stocks, then HP percentage; a true tie is logged as a match but not a win or loss.

### Ability implementation

- Shared ability metadata belongs in `[GlobalClass] AbilityData : Resource`.
- Executable abilities derive from `BaseSpecial` and follow startup → active → recovery → cleanup phases.
- Character-specific classes live in `scripts/Characters/Abilities/` and use the `FTT.Characters.Abilities` namespace.
- Persistent constructs such as Tesla coils, Leonardo's turret, vine snares, serpent nests, projectiles, VFX, enemies, pickups, and damage numbers should use `PoolManager`/`IPoolable` rather than repeated instantiate/free churn during gameplay.
- Persistent objects must have stable owner/type/state data so Story rewind and Fighter rollback can snapshot them.
- The current `CharacterFactory` constructs placeholder character visuals and combat child nodes in code, but consumes canonical character/ability `.tres` tuning. Long-term presentation should move toward reusable authored scenes without reintroducing duplicate numbers.
- `PlaceholderProjectile`, `PlaceholderZone`, color rectangles, and code-generated labels are scaffolding. Do not mistake them for production art or final VFX.

### Status effects

The target design allows one active status at a time; the newest status completely replaces the previous one. Effects do not stack, and there is no general cleanse or post-effect immunity.

The enum, strategies, ability data, events, and deterministic runtime use the canonical `TimeDilation`, `Venom`, `StaticCharge`, `RadiantBurn`, and `Root` names. Both Story and Fighter adapters enforce newest-status replacement. Update both snapshot paths and tests when changing status semantics.

### Collision layers

Layers 1–11 are named Player, Enemy, PlayerHitbox, EnemyHitbox, PlayerHurtbox, EnemyHurtbox, Environment, OneWayPlatform, Trigger, PersistentObject, and Projectile. `CollisionLayers` centralizes constants and masks, and tests validate the matrix. Player/Enemy body masks deliberately remain physically separate; child `CombatantPushbox` Areas use those body layers for explicit soft horizontal jostling without making hurtboxes solid or allowing combatants to become floors. Ordinary enemies are roll-through, while large bosses can set `BlocksRollThrough`. Update project settings, constants, scene masks, pushboxes, and tests together.

## Story progression, rewind, and saves

- Story difficulty scales enemy HP/damage/spawn pressure/drop rates and Chronal Rewind behavior; it must not alter Fighter Mode balance.
- Rewind pools: Easy 5 with 100% HP restore and checkpoint refill; Normal 3 with 50% HP restore and +1 per new checkpoint; Hard 1 with 30% HP restore and no checkpoint refill during the level.
- Rewind state records at 60 Hz and searches backward for a safe grounded frame, retaining a last-known grounded fallback beyond the normal five-second/300-frame buffer. If none exists, use the physical checkpoint.
- Chronal Dust funds the selected character's Story-only Resonance Grid. Progress and unlocked node IDs belong to the story save slot. All nine baseline 3x3 grids are authored; the generic Story stat resolver and character-scoped modifier queries exist. `CharacterFactory` populates `PlayerController.StoryAbilityPerks` from the active save (Story-only; abilities gate bespoke behavior via `HasStoryPerk`). Einstein's three major perks are wired; the other eight characters' perks land with their kit passes (`docs/PACKAGE3_KIT_AUDIT.md`).
- There are three story slots plus global settings/statistics. Autosave occurs at checkpoints, level completion, and unlock events. The main menu supports localized slot summaries plus confirmed deletion of the exact primary/backup/temp/corrupt candidates for a slot.
- Save schema version 3 uses AES-256-CBC, encrypt-then-HMAC-SHA-256, distinct derived encryption/authentication keys, and a random per-install key stored at `user://saves/.savekey`. Writes are atomic and verified, backups can recover a bad primary, corrupt candidates are preserved, legacy Base64/plain JSON is migrated, completed puzzle IDs persist per slot, and newer schemas are rejected without rewriting them. Never replace the per-install strategy with a hardcoded source secret.

## Fighter networking and determinism

`scripts/FighterSim/` implements a Klotho fixed-point (`FP64`/`FPVector2`) 1v1 foundation with quantized inputs, stable state IDs, full snapshots, deterministic hashes, a 120-tick history, prediction, corrected-input rollback, and resimulation. `FighterLoadoutFactory` accepts normalized base resources only, preserving Story/Fighter isolation. The Test Arena renders synchronized results without feeding Godot state back into gameplay.

The deterministic systems cover accelerated ground/air movement, universal dash/roll phases, fixed-point fighter jostling, jump counts, solid side/top bounds, bottom-zone stocks, generic basics/specials/ultimates, block, meter, status, Stock/Time/Hybrid resolution, projectiles, movement abilities (including input-directional Warp teleports with a reduced-gravity float window), deploy-limited persistent constructs, Area-execution zones (`FighterZoneComponent`/`FighterZoneSystem`: impulse-free periodic damage/status pulses that bypass shields and never zero velocity, plus owner-overlap speed bonuses like Einstein's Relativity Rift), stage hazards, and Chronal Orbs. `FighterStageGeometry` supplies per-stage fixed-point bounds, one-way platforms (with drop-through and edge-walk-off rules), spawn distance, and authored hazard/orb anchors; stages without authored geometry fall back to the legacy flat arena. Stages with platforms have a solid base floor. Character-specific production behavior and rollback performance/platform gates remain incomplete. Klotho v0.6.1 is experimental and this is not production online netcode.

`scripts/Networking/` now defines a versioned fixed-size input/hash protocol (currently protocol v2, including Roll/Dash button bits), acknowledgements, confirmed-frame hash exchange, direct-IP UDP LAN transport, deterministic in-memory test transport, remote prediction/correction, bounded rollback/resimulation, desync events, rollback-budget diagnostics, and cryptographically generated room codes. It does not yet have LAN discovery, handshake/rules negotiation, full-state resync, matchmaking UI, Steam Networking Sockets/relay, public queue, or production failure/forfeit flows.

Initial online/local Fighter Mode is 1v1. Even if code contains generalized player-count fields, do not build four-player behavior in this phase.

## UI, localization, audio, and accessibility

- UI is authored for a 1920×1080 reference canvas with Godot `Control` anchors and layout containers.
- Use `Tr("translation_key")` / `TranslationServer.Translate(...)` or the localization manager for all visible copy. Add English entries to `localization/en.csv`. Do not add localized JSON payloads or language selectors until localization production is scheduled.
- `project.godot` registers `res://localization/en.en.translation` with English fallback. Keep that resource registration intact: Godot `Node.Tr()` bypasses the custom `LocalizationManager` and otherwise displays raw translation keys as placeholder text.
- Dialogue uses a 30-characters-per-second typewriter reveal. Confirm completes the active line, then advances on the next confirm. Full dialogue-sequence skipping is not supported.
- Dynamic level music is designed as synchronized ambient, combat, and boss/climax stems routed through `AudioServer` buses.
- Settings include audio buses, resolution/window mode/VSync, input remapping, damage-number visibility, HUD opacity, screen-shake scaling, and haptics. Persist settings in global save data.
- Controller haptics and screen shake must respect their global accessibility intensity/enable settings.

## Coding and content conventions

- Match folder namespaces: `FTT.Core`, `FTT.Characters`, `FTT.Characters.Abilities`, `FTT.Combat`, `FTT.Enemies`, `FTT.Environment`, `FTT.UI`, `FTT.Networking`, and `FTT.FighterSim`.
- Godot C# node/resource classes are `partial`; reusable inspector data classes should use `[GlobalClass]` and `[Export]` fields/groups.
- Use `res://` paths for Godot resources. Prefer exported `PackedScene`, `Resource`, and typed node references over scattered magic paths.
- Keep static tuning in Resources and runtime mutable state in controllers/snapshots. Avoid creating a second canonical value in a factory or UI script.
- Poll named InputMap actions through `InputManager.Actions`. Preserve device/player separation for local 1v1; direct global input polling can make both fighters react to one device.
- Gameplay state changes belong in `_PhysicsProcess()` at 60 Hz. `_Process()` is appropriate for presentation/loading/UI, not authoritative combat simulation.
- Use `EventBus` for cross-system events, but keep payloads sufficient for consumers and avoid hidden global coupling.
- Pool frequently spawned or destroyed gameplay objects. Reset every mutable field in `OnSpawn()`/`OnDespawn()`.
- Keep Story-only progression modifiers out of shared Fighter stats and ability resources.
- Preserve historical framing and the selected character's ability identity when adding mechanics; mechanics should not become generic solely for implementation convenience.
- Update translation keys, save schemas/migrations, rollback snapshots, pools, and tests whenever a changed data field affects them.
- Preserve unrelated user changes in this working tree. Do not clean, reset, or rewrite generated/user-modified files outside the task.

## Validation expectations

For ordinary C# changes:

1. Run `dotnet build FightersThroughTime.csproj` from the repository root.
2. Open/run the affected Godot scene when a Godot executable is available. The main flow starts at `scenes/menus/MainMenu.tscn`; `scenes/arenas/TestArena.tscn` is the current focused combat sandbox.
3. Check the Godot output for missing scripts, invalid `res://` paths, node-name mismatches, invalid exported resources, and signal/event lifecycle errors.
4. For gameplay changes, test state interruption, death/respawn, pause, and scene transitions in addition to the happy path.
5. For combat/system changes, consider both Story Mode and Fighter Mode and explicitly confirm that Story progression cannot leak into competitive values.
6. For input changes, test keyboard and two-controller device assignment.
7. For pooled objects, exercise capacity/overflow and repeated spawn-release cycles.
8. For save changes, test empty slots, round-trip serialization, corrupt/old data handling, and migration.
9. For future deterministic networking work, add headless repeatability/desync tests before treating the system as usable online.

GdUnit4 v6.2.0 is installed. Run the headless suite with a valid console Godot path. On the current development machine both Godot 4.7.1 .NET executables live in `D:\Projects\`, one directory above the repository:

```powershell
$env:GODOT_BIN = "D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe"
dotnet test FightersThroughTime.csproj --settings .runsettings
```

Verify the reported `Total:` count against the baseline below. If GdUnit4 cannot launch Godot it logs `Rebuilding Godot Project ends with exit code: -1073741819`, runs only the ~21 pure-C# tests, and still exits 0 — a green exit code alone does not mean the suite passed. That happens when `D:\Projects\GodotSharp\` is missing or the Godot binary is separated from it; Godot then aborts with `.NET: Assemblies not found`. The full suite was last verified green on 2026-08-07 at 352/352 (Package 4 workstream B6, including the B6 stabilization fix).

The current baseline is 352 passing tests, including the Level 13 Mirror Paradox (Package 4 B6: authored single-phase 1000-HP resource, clone construction from normalized base resources with Resonance perk/stat isolation proven against a populated save, Enemy-slot collision placement, `CpuDecisionObservation` lossless projection, frame-identical reuse of the Hard Fighter CPU decision table through the Story adapter, 4–8 frame reaction parity, boss HP/defeat republication with 50 dust, and rewind freeze), the Package 4 enemy/boss runtime foundation (shared EnemyAbilityData archetype execution with telegraph/active/recovery frame accounting, pooled enemy projectiles, elite ability cycling, flying/phasing/frontal-reduction rules, enemy death and rewind policies, boss weighted selection with distance filtering and the empty-filter no-deadlock fallback, phase threshold single and double crossing, transition invincibility, telegraph interruption, knockback immunity, and authored enemy/boss scene contracts), canonical kit and ultimate determinism, per-character presentation contracts, Resonance grid UI/economy/minor-key coverage and content contracts for all nine characters (constructs, zones, ultimate dispatch, impulse rules, glide floats, block interactions, rollback convergence), placeholder audio kit contracts (synchronized loopable stems, non-looping SFX), Fighter zone spawn/status/owner-buff determinism and rollback convergence, Warp float behavior, Resonance perk collection/isolation, minimal enemy status semantics, Story difficulty scaling, Fighter stage geometry/platform/anchor determinism, Roll default bindings, dash gesture/input serialization, Story pushbox geometry/resolution, authored combat timelines, ledge/one-way/aerial/crouch/hyper-armor rules, puzzle/environment toolkit behavior, stable-ID pool capacity/recycle/reject/reuse, difficulty drop contracts, Dust visual tiers, post-rewind immunity/presentation contracts, deterministic Fighter run acceleration/dash/roll/jostling, entity lifecycles, match modes, CPU decisions, delayed-input convergence, packet validation, safe-frame rewind behavior, all nine Resonance grid manifests/prerequisites/isolation, Fighter stage catalog/hazard identity, localization registration, schema-v3 save envelopes, deletion, tamper rejection, migration, and atomic backup recovery.

## Known prototype gaps to remember

- Only the menu, character select, test arena, hub, tutorial, and Florence scenes exist.
- Campaign target routing names levels 0–15, but scene resources only exist for levels 0 and 1.
- Much of the environment and ability presentation is code-generated placeholder geometry. Player characters now render from per-character placeholder `SpriteFrames`/portraits wired through `CharacterData` (2026-08-07); production art replaces those resources by reassignment. Enemies and bosses render from the authored `scenes/enemies/` scenes with `EnemyData.PlaceholderTint`/`SpriteFramesResource` (Package 4 A1).
- Enemy and boss runtime is data-driven as of Package 4 A1: `EnemyAbilityData` (`MeleeStrike, Projectile, Shockwave, ChargeDash, ShieldBubble, AreaPulse, SummonMinions, Teleport`) executed by the shared `EnemyAbilityExecutor` for both elite secondary abilities and every boss attack, with telegraph/active/recovery frames at 60 Hz, pooled `enemy_projectile` shots, difficulty-scaled reaction delays (`StoryDifficultyTuning.ScaleReactionDelayFrames`), weighted boss selection with distance filtering and an explicit empty-filter fallback, phase gating/speed multipliers, telegraph interruption, and `BossEncounterController` for HUD/dust/rewind wiring. The 22 remaining roster enemies, 13 remaining bosses, and per-level pool configs are Package 4 Phase B/C work.
- The Level 13 Mirror Paradox (Package 4 B6) deliberately bypasses `BossController`. `MirrorParadoxController` builds a clone of the session's locked character through `CharacterFactory.CreateCharacter(..., applyStoryProgression: false)` — normalized base resources only, no Resonance stats and no `StoryAbilityPerks` — gives it the authored 1000-HP pool through the new `PlayerController.EncounterMaxHPOverride`, and drives it with the real deterministic Hard CPU engine. `FighterCpuController` now reads a mode-neutral `CpuDecisionObservation` (raw `FP64` world-unit positions, still no Godot dependency), so `MirrorParadoxDecisionAdapter` can project Story state into the same decision table and hand the resulting `PlayerInputFrame` to the clone through `InputManager.SetInputSource`; nothing flows back into `scripts/FighterSim/`. `MirrorParadoxEncounterController` parallels `BossEncounterController` for the HUD bar, 50-dust award, and rewind freeze. Level 13 authoring and the content-manifest row remain Package 5 / Phase C.
- **Never author an empty Script-typed array in a `.tres` or `.tscn`.** `Prop = Array[ExtResource("N")]([])`, where resource `N` is a `.cs` script, corrupts the Godot 4.7.1 .NET heap when the resource is marshalled to its C# array export. The process then dies at an unrelated site — a silent GdUnit child death (exit `-1073741819` / `-1073741795`), a hang, or `ResourceLoader.Load` returning null for an unrelated resource — so the crash never points at the cause. To express "no entries", omit the property and let the C# field default apply. Non-empty Script-typed arrays are fine, and so are empty arrays of builtin types (`Array[float]([])`, `Array[int]([])`). `tests/ContentValidation/ScriptTypedEmptyArrayTests.cs` scans the content directories and fails with the offending file and line. This cost a full debugging cycle in Package 4 B6; see that plan's Deviations section.
- The deterministic Fighter and local match foundations work, and stage select exposes ten playable variants with distinct deterministic hazard identities. Florence Workshop is the first production-contract stage with authored geometry, scene, and camera wiring; the remaining nine stages still share the Test Arena template. All nine character kits (specials and movement) are converted to canonical mechanics in both modes with five authored construct scenes under `scenes/constructs/` and all 27 Resonance major perks wired Story-only; the 36-slot audit with per-slot approximations lives in `docs/PACKAGE3_KIT_AUDIT.md`. All nine ultimates are canonical multi-hit structures via the `FighterUltimateRules` dispatch (X7 closed; cinematic presentation is Package 8); rollback performance validation and presentation polish remain incomplete. `BaseSpecial` abilities should use `UseAuthoredPhaseFrames()` so `.tres` startup/active/recovery frames are authoritative rather than hardcoded `PhaseTimer` constants.
- Direct-IP LAN packets/rollback are implemented as a foundation. Steam Networking Sockets, relay, public matchmaking, connection negotiation, state resync, and production online UI are absent.
- Secure schema-v3 saves, confirmed slot deletion, localized slot summaries, puzzle completion persistence, and all nine baseline Resonance Grid resources are implemented. Bespoke perk execution is wired for Einstein (Story-only via `StoryAbilityPerks`); the other eight characters' perks and complete hub stations/gates remain.
- The shared puzzle/environment toolkit, Story drop tables/pickups, world-aware rewind freeze/projectile clearing, 120-frame landing immunity, and Timeline Collapse restart choice are implemented with replaceable placeholders. Full level authoring and final rewind/drop presentation remain later-package work.
- The implemented menu, save, hub, tutorial, Florence, and Fighter stage-selection flows use translation keys. Continue auditing new and less-traveled prototype UI for hardcoded visible copy.
- Stable-ID Story pools now cover placeholder projectiles/zones, the shared `standard_enemy`/`elite_enemy` tier pools, `enemy_projectile`, Dust/helper loot, placeholder VFX, and persistent constructs; production kit/boss/hazard scenes added later must enter these pools and preserve reset contracts. Placeholder authored animation callbacks cover the P0 combat path, while production animation libraries and broader FSM/content coverage remain incomplete.
- Settings persist for implemented audio/display/accessibility options, but complete input-remapping persistence and reset-to-default behavior remain.
- The Tutorial three-part flow, Florence four-room progression with gear puzzle and boss, hub Holodeck/NPC/dust-deposit loop, resource-driven dialogue system, Story HUD, results overlay, and Story difficulty scaling are functionally complete with placeholder presentation.
- Levels 2–15, the nine remaining independently authored production Fighter stages, production character/environment art, complete music/VFX, performance profiling, exports, and platform validation remain.
- `.gitignore` excludes generated output, but legacy `.godot/` files are already tracked and still appear in Git status. Do not remove them from the index without explicit repository-cleanup authorization.

When a task closes one of these gaps, update this file so later agents inherit the new reality.
