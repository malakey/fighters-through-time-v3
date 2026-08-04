# Fighters Through Time implementation gap-closure plan

Status: active roadmap based on the repository and `design-godot.md`. Implementation status last audited 2026-08-03; see `docs/IMPLEMENTATION_STATUS.md` for completed work and remaining acceptance criteria.

## Current milestone status

| Milestone | Status | Current boundary |
|---|---|---|
| 0 — Baseline and decisions | Complete | Reproducible toolchain, GdUnit4, smoke tests, ADRs, and repository ignore rules are present. Tracked legacy `.godot/` files still require a separately approved index cleanup. |
| 1 — Data, collision, input | Substantially complete | Character/ability/enemy/boss resources, canonical statuses, collision settings, 60 Hz/fixed aspect, and serializable per-player inputs are implemented. Resonance content remains placeholder-only. |
| 2 — Shared combat/movement | In progress | Typed hits, formulas, block/meter rules, frame timelines, status overwrite, and input-frame consumption are implemented and tested. Full authored animation callbacks, every FSM edge, and pooling conversion remain. |
| 3 — Deterministic Fighter foundation | In progress | Klotho v0.6.1 is pinned; fixed-point 1v1 state, snapshots, hashes, prediction/correction rollback, and the local Test Arena presentation bridge are working. Full kit/projectile/persistent-object/hazard systems and production rollback performance proof remain. |
| 4–10 | Pending | Vertical-slice production, secure saves/meta-game, complete local/online modes, full content, presentation, and release hardening remain future work. |

This plan covers the differences between the current prototype and the initial-release design. It intentionally excludes post-launch features such as four-player free-for-all, 2v2 teams, alternate costumes, cosmetics, full-match replays, detailed post-match stat sheets, online profiles, additional languages, and Training Mode.

## Executive assessment

The repository currently proves several important concepts:

- Godot 4.7.1/.NET 8 project setup and autoload lifecycle.
- Main menu, Story character selection, Fighter character selection, a hub prototype, tutorial prototype, Florence prototype, and a combat test arena.
- All nine `CharacterData` resources and character-specific ability class scaffolding.
- A `PlayerController` FSM with movement, combo, block, special, ultimate, ledge, death, and respawn states.
- Prototype enemies, boss state handling, pooling, events, settings, save slots, localization, dialogue, audio, haptics, camera shake, match state, rewind, and network snapshot classes.

It is not yet a production vertical slice. Only eight scenes and nine `.tres` resources exist; most content is generated in code with placeholder geometry; there is no automated test/addon infrastructure; local input is globally polled; Story rewind and saves do not meet the design; and networking is not deterministic rollback.

The highest-risk mistake would be scaling to fifteen more campaign scenes and ten fighter arenas before correcting the shared data, input, combat, pooling, save, and deterministic-simulation contracts. The plan therefore establishes those contracts first, proves them in one complete vertical slice, and only then scales content.

## Priority definitions

| Priority | Meaning |
|---|---|
| P0 | Architectural or release blocker; later work would be rewritten without it |
| P1 | Required to prove the production vertical slice |
| P2 | Required initial-release feature/content after the slice is stable |
| P3 | Polish, optimization, and release hardening |

## Gap inventory

| Area | Current implementation | Design target | Priority |
|---|---|---|---|
| Repository baseline | No `.gitignore`; generated `.godot/` files appear in Git status; no documented clean-build baseline | Reproducible checkout/build with generated output excluded | P0 |
| Automated tests | No test project or GdUnit addon | Headless FSM, combat, status, rewind, save migration, snapshot, and integration coverage | P0 |
| Data ownership | Nine character `.tres` files; ability/enemy/boss/resonance tuning largely constructed in factories | Canonical `.tres` data for characters, abilities, enemies, bosses, movement abilities, and resonance grids | P0 |
| Input | Actions are named, but gameplay directly polls global `Input`; player index does not isolate devices | Per-player input frames usable by keyboard/controllers, CPU, replayable tests, and network prediction | P0 |
| Fighter simulation | Godot float/`CharacterBody2D` state and a short float snapshot queue | Klotho fixed-point deterministic world, complete snapshots, hashes, rollback, and resimulation | P0 |
| Project configuration | 1920×1080 but `stretch/aspect="expand"`; layer names and explicit physics tick target absent | Fixed 16:9 presentation, named collision matrix, explicit 60 Hz configuration | P0 |
| Combat correctness | Core FSM/combo exists, but timing is timer-based; hit context cannot distinguish attack class; weight/block/ultimate rules are incomplete | Animation-frame hit timing, canonical formulas, directional block, special shatter, unblockable ultimate, meter carryover, hyper-armor and cancel rules | P0 |
| Status system | One-status strategy prototype with enum names different from the GDD | Canonical TimeDilation/Venom/StaticCharge/RadiantBurn/Root rules and visuals | P0 |
| Pooling/performance | `PoolManager` exists, but enemies, damage numbers, SFX players, placeholders, and some persistent objects still allocate/free during play | Warmed pools and reset-safe lifecycle for all high-frequency objects | P1 |
| Story rewind | Teleports to checkpoint and resets all difficulties at checkpoints; no frame buffer or HP restore | Five-second safe-frame rewind, difficulty-specific refresh/HP rules, entity/projectile rewind policy | P1 |
| Saves | Three in-memory slots; story data is Base64 JSON, global data is plaintext; no version/migration/corruption handling | Versioned, atomic, migrated, AES-encrypted and HMAC-verified saves with robust recovery | P1 |
| Story progression | Level enum 0–15, but only paths for levels 0 and 1; no complete save-slot or Resonance Grid loop | Locked-character campaign, full routing, Chronal Dust deposit, Story-only grid, autosave, resume/checkpoints | P1/P2 |
| Local Fighter Mode | One selected character enters a test arena; match manager is not a complete playable 1v1 loop | Two-player/CPU character and stage select, rules config, stocks, bottom KO, respawn, timer/draw, results/rematch, stats | P1 |
| Online/LAN | Room flags/code and snapshots only; no transport or matchmaking | LAN plus Steam P2P/relay, private rooms, public queue, rollback session, errors and forfeit flow | P2 |
| Enemy/boss AI | Patrol/chase/attack scaffolding; CPU decision body empty; bosses select but do not execute authored abilities | Standard/elite era behaviors, weighted boss kits/phases, difficulty scaling, functional CPU levels | P1/P2 |
| Scenes/content | Hub, tutorial, Florence, and test arena prototypes | Hub, levels 0–15, ten production Fighter stages, encounters, puzzles, hazards, checkpoints, bosses, dialogue | P2 |
| UI flow | Code-generated prototype screens; settings button disabled; HUD/dialogue/post-match incomplete | Complete boot/menu/save/story/fighter/settings/pause/error/results flow with production scene components | P1/P2 |
| Localization | English CSV exists, but prototype UI is mostly hardcoded | All user-visible strings key-based; English table complete | P1 |
| Audio/feedback | Basic buses and players; no synchronized stem assets; SFX allocates players; partial feedback hooks | Ambient/combat/boss stems, pooled SFX, surface footsteps, complete haptic/shake settings and triggers | P2/P3 |
| Art/animation | Placeholder rectangles/labels and minimal player scene | Production sprites, frame callbacks, VFX, shaders, portraits, tilesets, UI, and animation sets | P2/P3 |
| Performance/export | Budgets documented but not measured; no release export validation | 60 FPS budgets met on target PCs/Steam Deck; Windows/macOS/Linux exports and soak tests | P3 |

## Sequencing principles

1. Establish a clean, testable baseline before changing runtime architecture.
2. Make `.tres` resources the source of truth before tuning or producing content.
3. Replace direct input polling with serializable per-player input frames before implementing local 1v1 or rollback.
4. Define Fighter Mode's deterministic boundary before polishing Fighter gameplay. Offline local Fighter Mode should run the same deterministic simulation used online.
5. Share ability definitions and expected outcomes across modes, while using separate execution adapters where required: Godot `CharacterBody2D` for Story and fixed-point Klotho simulation for Fighter.
6. Turn the existing hub → tutorial/Florence → hub and test-arena paths into one production vertical slice before adding more levels.
7. Scale campaign and Fighter content through templates and validators, not one-off scene-controller code.
8. Treat UI, localization, accessibility, pooling, tests, and performance as acceptance criteria in every milestone rather than end-of-project cleanup.

## Milestone 0 — Reproducible baseline and architecture decisions

Priority: P0. Dependency: none.

### Deliverables

- Record a baseline build and Godot smoke-run result without altering gameplay behavior.
- Add repository hygiene for `.godot/`, build output, editor caches, OS files, and local IDE state. Removing already tracked generated files must be handled as a deliberate repository cleanup, preserving user work.
- Pin/document the supported Godot .NET and .NET SDK versions.
- Select and pin the test stack. Prefer GdUnit4 for scene/integration coverage; add NUnit only if a separate pure C# simulation assembly makes it useful.
- Add a headless smoke test for project startup and loading the menu, test arena, hub, tutorial, and Florence scenes.
- Add architecture decision records for:
  - shared ability data with Story and Fighter execution adapters;
  - Klotho integration and fixed-point Fighter ownership;
  - per-player input-frame format;
  - save key/integrity strategy that does not embed a recoverable secret in source;
  - resource IDs, versioning, and canonical tuning ownership.
- Create a gap-tracking board or issue list using the work packages in this document.

### Exit criteria

- A clean checkout restores/builds reproducibly.
- Generated `.godot/` output is not part of ordinary feature diffs.
- Automated smoke tests detect missing scripts/resources and scene load failures.
- The five architecture decisions above are explicit enough that implementation agents do not create competing approaches.

## Milestone 1 — Canonical data, collision, and input foundations

Priority: P0. Dependency: Milestone 0.

### Deliverables

- Add the planned resource folders for abilities, movement abilities, enemies, bosses, resonance grids, sprite frames, and pool configuration.
- Expand `AbilityData` to the final shared definition required by the GDD: stable ID, slot, damage/hit count, knockback, startup/active/recovery timing, cooldown, hitbox geometry, projectile/persistent-object definition, hyper-armor, status, SFX, VFX, animation, shake, and lifetime fields.
- Introduce `MovementAbilityData` if movement-only fields cannot remain type-safe in `AbilityData`.
- Create canonical resources for all 36 initial character slots: Special 1, Special 2, Movement Ability, and Ultimate for each of nine characters.
- Migrate factory tuning into resources. Factories should instantiate configured scenes/resources, not author a second copy of stats.
- Create enemy, elite, boss, and Resonance Grid resource schemas with stable string IDs and version-safe fields.
- Reconcile the status enum with the target design across strategies, events, ability resources, HUD icons, and snapshot/save schemas.
- Add centralized collision-layer constants and configure the named project layers/masks from the GDD matrix.
- Explicitly configure 60 Hz physics and fixed 16:9 presentation. Replace or correct `ViewportEnforcer` so it actually renders letterbox/pillarbox bars rather than only resetting content scale.
- Define a serializable `PlayerInputFrame`/`FighterInputCommand` containing directional values and edge/held bits for every gameplay action.
- Refactor `InputManager` to map keyboard/controller devices to a player index and produce one input frame per player. Replace direct global action polling in controllers and abilities.

### Tests

- Resource validation: unique IDs, complete required fields, valid `res://` paths, and all nine characters owning four valid abilities.
- Collision mask matrix validation.
- Two-device input isolation, keyboard fallback, disconnect/reconnect, and deterministic input-frame serialization.
- Translation/resource key validation for display names and descriptions.

### Exit criteria

- No canonical ability or enemy number is duplicated between a factory and a resource.
- Two local fighters can receive independent commands in the test arena.
- Status names, collision layers, physics tick, and aspect behavior match one documented contract.

## Milestone 2 — Shared combat and movement correctness

Priority: P0/P1. Dependency: Milestone 1.

### Deliverables

- Convert `PlayerController` input consumption to input frames rather than direct `Input` calls.
- Formalize the FSM transition table and interrupt priorities, including stun, death, hyper-armor, recovery-only block cancels, special cancels, ledge damage, and respawn invulnerability.
- Replace timer-approximate attack activation with animation-frame callbacks or frame-indexed simulation events.
- Introduce a typed hit payload containing attacker/target IDs, attack class (basic/special/ultimate/hazard), hitbox ID, damage, knockback, status, hitstun, facing/origin, and feedback values.
- Implement the canonical damage and knockback formulas. Remove the unused defense reduction path and ensure player/enemy code uses the same tested calculator.
- Complete directional blocking, basic-charge consumption, immediate special shatter, ultimate bypass, guard-break daze, charge regeneration, and HUD events.
- Complete Influence meter gain for both damage dealt and taken, consumption, stock-loss 25% penalty, and player-indexed events.
- Implement one-status overwrite behavior using the canonical five effects, including movement/damage changes, duration, visuals, and snapshot safety.
- Make aerial/crouching combo behavior, coyote time, buffering, skid, ledge, drop-through, and character jump counts conform to the GDD.
- Replace frequently created damage labels, projectiles, zones, SFX players, enemies, pickups, and persistent constructs with warmed pools.
- Define reset contracts for every `IPoolable` type and stable snapshot data for persistent objects.

### Tests

- Every FSM transition and forbidden transition.
- Combo damage multipliers and frame windows.
- Weight-adjusted knockback and facing.
- Block behavior for all attack classes and directions.
- Ultimate gain, use, death penalty, and player isolation.
- Status overwrite, tick, expiration, and no-stacking rules.
- Pool overflow/recycle/reject behavior and repeated reset cycles.

### Exit criteria

- Einstein and Joan can complete representative ranged and melee combat scenarios in both Story and local Fighter contexts using the same definition data.
- Combat outcomes are reproducible from an input-frame sequence.
- The active gameplay loop produces no routine instantiate/free spikes for the covered vertical-slice objects.

## Milestone 3 — Deterministic Fighter simulation foundation

Priority: P0. Dependency: Milestones 1–2 contracts. Start a Klotho feasibility spike during Milestone 0.

### Deliverables

- Pin and integrate the approved Klotho version after a small proof verifies Godot 4.7/.NET 8 compatibility, fixed-point collision behavior, license, and target-platform support.
- Create a Fighter simulation assembly/module with no authoritative dependency on Godot floats, `CharacterBody2D`, wall-clock time, scene-tree order, or `System.Random`.
- Represent movement, hitboxes, hurtboxes, projectiles, persistent objects, hazards, cooldowns, status, stocks, match timer, and RNG in fixed-point deterministic state.
- Make local offline Fighter Mode run this simulation. Godot nodes become presentation/input adapters.
- Implement complete save/load snapshots with stable entity IDs and no missing transient state.
- Implement deterministic seeded RNG, state hashing, input prediction, rollback, resimulation, and bounded history.
- Add a presentation bridge that interpolates/animates Godot nodes from authoritative simulation state without feeding presentation state back into gameplay.
- Delete or quarantine the current float snapshot queue once its replacement is verified; do not evolve it in parallel.

### Tests

- Run identical input streams for thousands of ticks and compare hashes across repeated runs.
- Save/load a snapshot mid-ability and confirm the resumed hash sequence matches uninterrupted play.
- Roll back across hit confirmation, projectile spawn, persistent-object spawn/despawn, KO, and hazard activation.
- Simulate delayed, missing, and corrected inputs and assert convergence.
- Measure snapshot, hash, rollback, and resimulation budgets against the GDD.

### Exit criteria

- A headless local 1v1 match can be replayed from inputs with identical hashes.
- Godot rendering can be disabled without changing Fighter outcomes.
- No authoritative Fighter state uses Godot floating-point physics or unseeded randomness.

## Milestone 4 — Production vertical slice

Priority: P1. Dependency: Milestones 1–3, except transport networking.

Scope: Main Menu → save/character selection → Tutorial → Hub → Florence → Hub, plus one complete Florence Fighter arena.

### Deliverables

- Replace code-generated placeholder player/enemy geometry with reusable scenes, resources, initial sprites/animations, authored hitbox callback tracks, and production-ready node contracts.
- Convert hub, tutorial, Florence, and test/Fighter arena from monolithic code-built layouts toward authored scenes and reusable components.
- Implement one standard mob, one elite, and the Florence boss through data-driven scenes and complete encounter behavior.
- Complete checkpoints, a safe-frame Chronal Rewind, Chronal Dust drops/collection/deposit, one usable Resonance Grid branch, and Story difficulty scaling.
- Complete the tutorial's required movement, combat, rewind, dialogue, and calibration gates.
- Complete Florence's rooms, hazards, cog/platform puzzle, enemy waves, boss, exit dialogue, completion, autosave, and hub return.
- Build the production Story HUD and Fighter HUD for two players: HP, ultimate, block charges, cooldowns, statuses, stocks, and timer.
- Implement typewriter dialogue with portraits/emotions, confirm-to-complete/advance behavior, data lookup by dialogue ID, pause/non-pause strategy, and no full-sequence skip.
- Add production loading treatments for Story portal travel and Fighter matchup cards.
- Add English translation keys for every vertical-slice string and remove hardcoded user-visible copy in this flow.
- Add representative ambient/combat/boss music stems, pooled SFX, surface footsteps, shake, haptics, VFX, and accessibility settings.

### Exit criteria

- A new Story slot can finish Tutorial and Florence, return to the hub, quit, and resume without losing progress.
- Rewind, dust, upgrades, combat, dialogue, settings, and checkpoint resume work at all three difficulties.
- Two players or one player plus CPU can finish a complete local Florence Fighter match.
- The slice meets the 60 FPS/frame-budget targets on the agreed minimum test machine and has automated smoke/integration coverage.

## Milestone 5 — Story meta-game, secure saves, and hub completion

Priority: P1. Dependency: Milestone 4 save/progression slice.

### Deliverables

- Implement versioned save envelopes, migrations, atomic write/replace, corruption handling, backups, AES encryption, and HMAC verification using an approved platform-safe key strategy.
- Complete three-slot New/Load/Delete UI, confirmation, timestamps, character lock, difficulty selection, completion state, and checkpoint resume.
- Store all required campaign fields: current level, selected character, difficulty, collected/deposited dust, unlocked Resonance nodes, checkpoint, rewind state where appropriate, playtime, and completion.
- Complete exact autosave triggers for checkpoint, level completion, upgrades/unlocks, and global versus statistics.
- Implement the Chronal Repository and full per-character Resonance Grid data/UI with prerequisites, costs, minor/major nodes, and Story-only modifier application.
- Implement hub portal selection/progression gates and all initial-release interactive stations described by the GDD.
- Make `StoryManager` data-driven rather than a two-entry static path array; validate all campaign level IDs and paths at startup/build time.
- Complete Chronal Dust risk/deposit behavior and make save/UI totals consistent.
- Implement difficulty-specific rewind checkpoint refresh correctly: Easy full refill, Normal +1 capped, Hard no refill.
- Rewind or reset enemies/projectiles/persistent objects according to the Story rewind rules.

### Exit criteria

- All save operations round-trip, migrate from prior test versions, recover from a corrupt primary file, and reject tampering without crashing.
- Upgrades affect Story values only; starting a Fighter match always uses normalized base definitions.
- The hub exposes the complete campaign meta loop without placeholder buttons or hidden debug shortcuts.

## Milestone 6 — Complete local Fighter Mode

Priority: P1/P2. Dependency: Milestones 3–4.

### Deliverables

- Implement mode selection for local human vs human and human vs CPU, with two-player device assignment.
- Implement two-sided character selection, stage selection, and the full match-settings UI: mode, stocks, timer, items/frequency, hazards/frequency.
- Complete pre-match load/cards, countdown, input lock, match start, bounded stage camera, and fixed competitive viewport.
- Implement HP KO, bottom-blast-zone stock loss, stock decrement, respawn portal, invulnerability, meter carryover, and return to active play.
- Complete match timer resolution by stocks then HP percentage; implement true draw behavior.
- Implement KO freeze/slow-motion/audio/fanfare, winner/defeat presentation, global/character statistics, rematch consensus, and return-to-character-select flow.
- Implement Story/local/online-specific pause rules and controller disconnect prompts.
- Implement Chronal Orb spawning/effects and hazard timing/rules using deterministic state.
- Complete CPU decision making, reaction models, recovery, attack/block/special choices, and GDD difficulty bands without exposing post-launch Training Mode.
- Produce and validate the ten initial Fighter stages from the campaign-equivalent eras. Stage-specific art/content can continue in Milestone 8, but the reusable arena/hazard contracts must be complete here.

### Exit criteria

- Every match setting changes the authoritative match behavior and survives rematches.
- Human-vs-human and human-vs-CPU matches can finish by HP, stock depletion, bottom fall, timer decision, and draw.
- Results and global statistics follow the design exactly; draws do not change wins/losses.
- Local Fighter Mode uses the same deterministic simulation and content definitions intended for online play.

## Milestone 7 — LAN, online rollback, and matchmaking

Priority: P2 release blocker. Dependency: Milestones 3 and 6 core loop.

### Deliverables

- Integrate Steam Networking Sockets for encrypted P2P, NAT traversal, and relay fallback; add direct LAN discovery/join for LAN play.
- Define compact input packets, acknowledgements, confirmed-frame tracking, connection state, pause/forfeit messages, and protocol version compatibility.
- Wire transport input into the deterministic prediction/rollback/resimulation loop.
- Implement private rooms/codes and public 1v1 queue. Keep account/profile systems deferred.
- Implement online pre-match character selection, stage/rules agreement, synchronized load/start, rematch/return flow, and public-queue return behavior.
- Implement desync hash exchange, diagnostic capture, mismatch handling, peer timeout, host/peer departure, latency/jitter warnings, and clean disconnect/forfeit UX.
- Add network simulation tests for latency, jitter, reordering, duplication, packet loss, disconnects, and reconnect-to-menu behavior.
- Add protocol/build version gates so incompatible builds cannot start a match.

### Exit criteria

- LAN and Steam-relayed private matches complete without divergent hashes under the approved network test matrix.
- Public queue creates only 1v1 matches and returns players to the correct flow after completion/failure.
- Network interruption produces the specified warning/forfeit/error behavior without corrupting saves or match statistics.
- No delay-based fallback is used for gameplay.

## Milestone 8 — Full campaign, roster, arena, and asset production

Priority: P2. Dependency: Milestone 4 templates. Can run in parallel with Milestones 5–7 once contracts are stable.

### Deliverables

- Produce Story levels 2–15 in act order using the validated level template: room graph, tile layers, checkpoints, encounter budgets, puzzles, hazards, boss, dialogue, completion, pool warm-up, audio stems, and performance metadata.
- Implement the full enemy/elite era roster and all fifteen boss encounters with authored phase behavior.
- Finish production kits for all nine characters, including sprites, portraits, animations, hit callbacks, projectiles/persistent objects, VFX, SFX, ultimates, and Resonance Grids.
- Finish the ten Fighter stages and their deterministic hazards/Chronal Orb spawn points.
- Implement all required Story pickups, Chronal Extractors, Chronal Dust visuals, and drop tables.
- Author the complete campaign dialogue/cinematics that are in initial scope. Keep character-specific home-era variants deferred unless reprioritized.
- Build reusable validation tools that report missing IDs, resources, animation names, dialogue keys, pool definitions, collision masks, stage bounds, checkpoints, and audio stems.
- Track every Section 17 asset requirement in an asset manifest with owner/status/import settings.

### Content gates per level/character/stage

- Design/data review complete.
- Scene opens with no missing dependencies.
- Gameplay completion path and failure/rewind path pass.
- Localization keys and dialogue pass.
- Pool/memory/performance pass.
- Controller, pause, save, and accessibility pass.
- Story/Fighter cross-mode ability behavior remains coherent.

### Exit criteria

- Levels 0–15 are reachable, completable, saved, and restored in sequence.
- All nine characters are playable from the start in Story and Fighter Mode.
- Ten Fighter stages, all required campaign bosses, and the complete initial-release enemy roster pass their content gates.

## Milestone 9 — UI, audio, accessibility, and presentation completion

Priority: P2/P3. Dependency: continuous; final gate after Milestone 8.

### Deliverables

- Replace remaining code-generated prototype UI with reusable `.tscn` components and theme resources.
- Complete boot sequence, main menu, save flow, Story flow, Fighter flow, settings, pause variants, network errors, post-match results, credits, and campaign-completion return behavior.
- Remove all remaining hardcoded user-visible strings and complete the English translation table.
- Complete settings persistence for Master/Music/SFX/UI, resolution, fullscreen/borderless/windowed, VSync, input remapping, damage numbers, HUD opacity, screen shake, haptics, and default difficulty.
- Serialize/restore remapped InputMap actions and provide reset-to-default behavior.
- Complete dynamic three-stem music, crossfades, bus effects, surface audio, environmental SFX, combat SFX, and UI/dialogue SFX.
- Complete haptic trigger coverage and ensure haptics/shake/damage-number/HUD-opacity accessibility controls apply everywhere.
- Implement final outline/glow shaders, `PointLight2D` support, visual status indicators, hit feedback, and animation priority/interrupt presentation.
- Verify fixed 16:9 output and readable UI at every supported window resolution/aspect ratio.

### Exit criteria

- Every initial-release screen is controller-navigable and key-localized.
- Settings survive restart and affect all relevant systems.
- No prototype/debug/placeholder copy, geometry, or disabled production button remains in a release flow.

## Milestone 10 — QA, optimization, balance, and release hardening

Priority: P3. Dependency: all release features/content integrated.

### Deliverables

- Complete automated unit, integration, content-validation, determinism, rollback, save migration, and scene smoke suites.
- Add long-running combat, pool, save, scene-transition, and network soak tests.
- Profile against the GDD budgets: physics, AI, rendering, scripts, allocations, RAM, draw calls, vertices, sprites, and particles.
- Eliminate gameplay-frame allocations and ensure off-screen animation/AI pacing rules work.
- Validate Windows, macOS, Linux/SteamOS, and Steam Deck exports, input devices, display modes, filesystem permissions, save paths, and network transport.
- Run structured playtests, then tune frame data, damage, cooldowns, AI, drops, difficulty, hazards, and bosses. Detailed balance work starts here, after systems/content are stable.
- Validate all campaign branches, dialogue, completion/credits, match outcomes, disconnects, and corrupted-save behavior.
- Produce release checklists, known-issues documentation, crash/desync diagnostics, and a rollback plan for save/protocol incompatibilities.

### Exit criteria

- No P0/P1/P2 initial-release gap remains open.
- Target platforms hold 60 FPS within the documented budgets in representative worst-case scenes.
- Determinism and migration suites are stable; network soak tests do not desync.
- The full campaign and all 1v1 local/LAN/online flows pass release acceptance.

## Recommended first implementation batch

The first implementation batch should stop before large content production and complete these items in order:

1. Establish repository hygiene and a clean build/smoke-test baseline.
2. Write the Klotho/input/save/data architecture decisions.
3. Add test infrastructure and the first FSM/damage/resource validation tests.
4. Introduce per-player serializable input frames and convert the test arena/player controller.
5. Configure collision layers, explicit 60 Hz physics, and fixed-aspect rendering.
6. Reconcile status types and add a typed attack/hit payload.
7. Move Einstein and Joan's four abilities each into canonical resources as the ranged/melee migration pilots.
8. Correct combat formulas, block rules, meter rules, and animation-frame hit timing for those two characters.
9. Pool the vertical-slice projectiles, zones, enemies, damage numbers, pickups, and SFX players.
10. Complete the Klotho deterministic proof using Einstein vs Joan inputs and snapshot/hash tests.
11. Promote hub/tutorial/Florence and one Florence Fighter arena into the production vertical slice.
12. Only after the slice passes, migrate the remaining seven characters and begin level/stage production.

## Parallel work after the vertical slice

Once Milestone 4 exits, work can split safely into these parallel tracks:

- Story systems: secure saves, hub, Resonance Grid, progression, rewind.
- Fighter systems: local match flow, CPU AI, deterministic stages/items/hazards.
- Networking: transport, rollback session, lobby/matchmaking, failure handling.
- Content: campaign levels, enemies, bosses, arenas, dialogue.
- Presentation: character/environment art, animation, VFX, UI, audio, localization.
- QA/tooling: validators, automated tests, profiling, exports, soak infrastructure.

All tracks must share versioned resource schemas, stable IDs, input frames, event/hit payloads, save migrations, and deterministic snapshot definitions. Schema changes require coordinated updates rather than local workarounds.

## Major risks and mitigations

| Risk | Consequence | Mitigation |
|---|---|---|
| Klotho is integrated late or proves incompatible | Fighter gameplay must be rewritten for rollback | Run the compatibility spike in Milestone 0 and make the deterministic local match a P0 gate |
| Story and Fighter implementations drift | Abilities feel inconsistent and balancing duplicates | Share definition data and expected-outcome tests; keep mode-specific execution behind explicit adapters |
| Code-generated prototype content becomes permanent | Tuning duplication, fragile node names, difficult art integration | Migrate the vertical slice to reusable scenes/resources before scaling content |
| Global input polling survives into 1v1 | Both fighters respond to one device; network inputs cannot be injected | Make input frames a Milestone 1 exit criterion |
| Save security is implemented with a hardcoded key | Encryption is cosmetic and migrations can destroy progress | Decide key strategy first; use versioned envelopes, atomic backups, migration and tamper tests |
| Content volume overwhelms engineering | Sixteen levels, fifteen bosses, nine full kits, and ten arenas stall integration | Use one validated template, asset manifests, automated validators, and act-based production gates |
| Generated `.godot/` changes obscure source diffs | User work is overwritten or reviews become unreliable | Clean repository hygiene deliberately and preserve unrelated existing changes |
| Performance is deferred | Pooling/determinism/content require late redesign | Include allocation and frame-budget checks in every content gate |

## Definition of initial-release completion

The gap-closure program is complete when:

- All nine characters are available immediately and have production-complete, cross-mode-coherent kits.
- Story Mode supports a locked-character campaign through levels 0–15, hub progression, Chronal Dust, Resonance Grids, checkpoints, difficulty-specific rewinds, save/resume, bosses, ending, and credits.
- Fighter Mode supports complete 1v1 human/local CPU/LAN/online rollback matches, ten stages, settings, hazards/items, stocks/HP/bottom KO, timer/draw, results, rematch, and statistics.
- Fighter simulation is deterministic and passes rollback/desync tests; no delay-based gameplay fallback exists.
- All initial UI is English-key-localized, controller accessible, settings-complete, and visually authored rather than debug scaffolding.
- Save versioning, migration, encryption, integrity, corruption recovery, and autosave behavior pass automated tests.
- Runtime allocation and rendering/physics/AI budgets meet the 60 FPS target on supported platforms.
- Every non-deferred requirement in `design-godot.md` is either implemented and verified or explicitly accepted as a documented design change.

After each milestone, update `AGENTS.md` to move completed systems from prototype/planned to implemented and remove resolved gap warnings.
