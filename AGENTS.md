# Fighters Through Time agent context

This file applies to the entire repository. It is the durable orientation document for AI agents working on this project. Keep it synchronized with material architecture or scope changes.

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

Canonical kits, talent grids, and tuning live in Section 5 of `design-godot.md`. Current baseline character stats live in `resources/Characters/*_data.tres`. The design document mentions `character_base_stats.md` and `ability_numeric_data.md`, but those files are not currently present; do not invent them or assume they are authoritative.

## Technology and runtime targets

- Engine in the current repository: Godot .NET 4.7.1 (`Godot.NET.Sdk/4.7.1`). The GDD's minimum is Godot 4.4+.
- Language/runtime: C# on .NET 8. Root namespace: `FTT`.
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
design-godot.md                  Full game design and technical specification
localization/en.csv              English translation keys
resources/Characters/            Nine CharacterData .tres resources
resources/Abilities/             Thirty-six canonical character ability resources
resources/Enemies/, Bosses/      Canonical prototype enemy and boss resources
scenes/menus/                    Main menu and character select
scenes/campaign/                 Hub, tutorial, and Florence prototype scenes
scenes/arenas/TestArena.tscn     Current combat test arena
scenes/characters/Player.tscn    Minimal reusable player scene
scripts/Core/                    Persistent managers and cross-cutting services
scripts/Characters/              Player, character factory, training dummy, abilities
scripts/Combat/                  Ability, hitbox, block, status, meter, match systems
scripts/Enemies/                 Enemy/boss controllers, AI, and prototype factory
scripts/Environment/             Hub, levels, rewind, hazards, dust, level flow
scripts/UI/                      Menus, HUD, dialogue, settings, pause UI
scripts/Networking/              Snapshot/room-code prototype only
scripts/FighterSim/               Klotho fixed-point 1v1 simulation and Godot bridge
tests/                            GdUnit4 C# content/unit/integration/determinism tests
addons/gdUnit4/                  Pinned test framework
addons/klotho/                   Pinned experimental deterministic runtime
docs/IMPLEMENTATION_STATUS.md    Current roadmap execution ledger
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
- `StoryManager` defines the complete level 0–15 enum, but its current scene-path table contains only Tutorial and Florence. Hub/tutorial/Florence and the test arena are implemented as prototype scenes; the rest of the campaign and production fighter stages are planned.
- `TestArena` now uses `FighterSimulationDriver`: Klotho state is authoritative and the two `PlayerController` nodes are collision-free presentation adapters. This is an offline deterministic foundation, not a complete local or online match flow.
- Several current scenes build placeholder visuals, geometry, actors, and UI programmatically in their controller scripts. This is valid prototyping state, not the intended final asset/scene-authoring pipeline.

## Autoloads and system ownership

`project.godot` currently registers these persistent autoloads:

- `EventBus`: typed C# events/payloads for non-adjacent system communication.
- `GameManager`: session data, defaults, and async scene transitions.
- `StoryManager`: campaign level, dust, rewind count, and hub/level flow.
- `PoolManager`: scene-pool registration, warm-up, spawn/release, and overflow policies.
- `InputManager`: action names and local controller assignment.
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
- The canonical state set is `Idle`, `Running`, `Skidding`, `Crouching`, `Airborne`, `Attacking`, `UsingSpecial`, `UsingUltimate`, `Blocking`, `Stunned`, `Dazed`, `LedgeHanging`, `Dead`, `Respawning`, and `UsingMovementAbility`.
- Movement includes acceleration/deceleration, direction-reversal skid, coyote time, jump buffering, short-hop behavior, character-specific jump counts, air control, crouching, one-way platform drop-through, ledge hanging, and recovery.
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

Layers 1–11 are named Player, Enemy, PlayerHitbox, EnemyHitbox, PlayerHurtbox, EnemyHurtbox, Environment, OneWayPlatform, Trigger, PersistentObject, and Projectile. `CollisionLayers` centralizes constants and masks, and tests validate the matrix. Update project settings, constants, scene masks, and tests together.

## Story progression, rewind, and saves

- Story difficulty scales enemy HP/damage/spawn pressure/drop rates and Chronal Rewind behavior; it must not alter Fighter Mode balance.
- Rewind pools: Easy 5 with 100% HP restore and checkpoint refill; Normal 3 with 50% HP restore and +1 per new checkpoint; Hard 1 with 30% HP restore and no checkpoint refill during the level.
- Rewind state records at 60 Hz and searches backward for a safe grounded frame, retaining a last-known grounded fallback beyond the normal five-second/300-frame buffer. If none exists, use the physical checkpoint.
- Chronal Dust funds the selected character's Story-only Resonance Grid. Progress and unlocked node IDs belong to the story save slot.
- There are three story slots plus global settings/statistics. Autosave at checkpoints, level completion, and unlock events.
- The final save design requires schema versioning/migration, AES encryption, and HMAC integrity protection. The current story save implementation only Base64-encodes JSON, and global data is plain JSON. Treat that as prototype persistence, not security. Never add secrets as hardcoded source constants when implementing the secure design.

## Fighter networking and determinism

`scripts/FighterSim/` implements a Klotho fixed-point (`FP64`/`FPVector2`) 1v1 foundation with quantized inputs, stable state IDs, full snapshots, deterministic hashes, a 120-tick history, prediction, corrected-input rollback, and resimulation. `FighterLoadoutFactory` accepts normalized base resources only, preserving Story/Fighter isolation. The Test Arena renders synchronized results without feeding Godot state back into gameplay.

The current deterministic systems cover core ground/air movement, jump counts, solid side/top bounds, bottom-zone stocks, generic basics/specials/ultimates, block, meter, status, timer, and match resolution. Projectile/persistent-object/hazard components exist, but their full character-specific systems and lifecycle rollback tests remain incomplete. Klotho v0.6.1 is experimental and this is not production online netcode.

The current `scripts/Networking/NetworkManager.cs` is only a placeholder: it toggles host/join flags, stores a short queue of float-based Godot snapshots, calculates a simple checksum, generates room codes, and has no transport, input prediction, resimulation, matchmaking service, Klotho world, or Steam integration. Do not extend this float snapshot queue as if it were the final rollback core. Networking changes should begin from the GDD's deterministic state schema and explicitly introduce the missing dependency/runtime boundary.

Initial online/local Fighter Mode is 1v1. Even if code contains generalized player-count fields, do not build four-player behavior in this phase.

## UI, localization, audio, and accessibility

- UI is authored for a 1920×1080 reference canvas with Godot `Control` anchors and layout containers.
- Use `Tr("translation_key")` / `TranslationServer.Translate(...)` or the localization manager for all visible copy. Add English entries to `localization/en.csv`. Do not add localized JSON payloads or language selectors until localization production is scheduled.
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

GdUnit4 v6.2.0 is installed. Run the headless suite with a valid console Godot path:

```powershell
$env:GODOT_BIN = "C:\path\to\Godot_v4.7.1-stable_mono_win64_console.exe"
dotnet test FightersThroughTime.csproj --settings .runsettings
```

The current baseline is 38 passing tests. Rewind safe-frame and save migration tests remain absent because those target systems are not implemented yet.

## Known prototype gaps to remember

- Only the menu, character select, test arena, hub, tutorial, and Florence scenes exist.
- Campaign scene routing only knows levels 0 and 1 even though the enum covers 0–15.
- Much of the environment, character, enemy, and ability presentation is code-generated placeholder geometry.
- The deterministic Fighter foundation is working offline, but character-specific constructs/projectiles/hazards, production rollback performance validation, transport, and matchmaking remain incomplete.
- Steam Networking Sockets is not installed; `NetworkManager` remains legacy float snapshot/room-code scaffolding and is not authoritative.
- Save encryption/integrity and version migration are not implemented.
- Several UI strings in prototype scripts are still hardcoded despite the localization target.
- Pool conversion, authored animation callbacks, and full FSM coverage are incomplete.
- `.gitignore` excludes generated output, but legacy `.godot/` files are already tracked and still appear in Git status. Do not remove them from the index without explicit repository-cleanup authorization.

When a task closes one of these gaps, update this file so later agents inherit the new reality.
