# Fighters Through Time: remaining-gap implementation plan

Status: Packages 0-1 (all P0 items) completed and verified on 2026-08-05. The next implementation gate is Package 2 (P1 production vertical slice with placeholders).

P0 validation record: `dotnet build` passed with only the existing vendored GdUnit4 nullable-context warning; 118 GdUnit4 tests passed; Godot 4.7.1 headless import and Tutorial, Florence, and Hub smoke runs completed without runtime diagnostics.

This plan closes every incomplete initial-release item in the gap analysis without redoing foundations that are already implemented. Functional placeholders are the default for art, animation, audio, UI decoration, and level dressing until replacement assets are available.

## Goal and completion states

The work has three meaningful completion states:

1. **Feature complete with placeholders** - The complete Story and 1v1 Fighter experiences work end to end with graybox levels, simple authored animations, geometric VFX, temporary portraits, and temporary audio. No gameplay system is waiting for production art.
2. **Content and platform complete** - Levels 0-15, all nine kits, the enemy/boss roster, ten Fighter stages, LAN/online flows, saves, settings, tests, performance gates, and target-platform exports are complete.
3. **Asset complete** - Approved production art and audio replace placeholders through stable resource contracts without changing gameplay code or deterministic data.

The first two states can be completed before final assets arrive. The third depends on the later asset replacement pass.

## Scope

### Required for the initial release

- Story Mode with a character-locked campaign, hub, levels 0-15, puzzles, enemies, bosses, dialogue, progression, rewind, completion, credits, and save/resume.
- Fighter Mode as normalized 1v1 human-vs-human and human-vs-CPU locally, plus LAN and online rollback play.
- All nine characters available from the start with coherent Story and Fighter implementations of their canonical kits.
- Ten independently authored Fighter stage scenes with deterministic hazards and Chronal Orbs.
- English-only but fully key-localized UI and dialogue.
- Complete initial settings, input remapping, controller support, feedback controls, performance gates, exports, and release QA.

### Explicitly deferred and not an initial-release blocker

The following incomplete entries remain tracked but are not pulled into the initial release unless explicitly reprioritized:

- NPC-gated portal activation; the current automatic sequential activation remains the initial target.
- Character-specific home-era dialogue variations.
- Additional languages, language selection, and CJK font fallback.
- Colorblind filters, screen-reader support, tap-to-hold, toggle sprint, and deadzone sliders.
- Four-player free-for-all, 2v2, Training Mode, alternate costumes, cosmetics, full-match replays, detailed post-match statistics, and online accounts/profiles.

Deferral is the disposition for these gaps; they must not be silently mixed into initial-release work.

## Placeholder-first production rules

Placeholders are deliverables, not ad hoc debug objects. They must exercise the final integration points.

- Use reusable `.tscn`, `.tres`, `AnimationLibrary`, `SpriteFrames`, shader, audio, and theme resources instead of adding more one-off controller-drawn content.
- Give every placeholder the stable resource ID, node contract, animation name, event track, dimensions, origin, collision shape, and import slot expected by its final replacement.
- Character and enemy placeholders may be colored silhouettes or simple sprite sheets, but must include every required animation and exact hitbox callback timing.
- Level placeholders should be authored grayboxes with final room graphs, platforms, checkpoints, interaction points, camera bounds, hazards, encounters, and completion paths.
- Fighter stage placeholders must be separate scenes with distinct geometry; catalog entries may not continue aliasing `TestArena.tscn`.
- Temporary portraits, icons, particles, outlines, normal-map stand-ins, and backgrounds should be visually distinct and accessible enough to test UI and gameplay feedback.
- Temporary audio may use short tones, noise, and simple loopable stems, but must prove synchronized layers, crossfades, snapshots, routing, pooling, ducking, and reverb behavior.
- Keep placeholder tuning out of factories. Canonical gameplay values remain in resources and deterministic loadouts.
- Track every placeholder in an asset manifest with `placeholder`, `ready_for_replacement`, `final`, or `not_required` status.
- Replacing a placeholder must not change gameplay scripts, save IDs, deterministic state, collision geometry, or translation keys unless a separately reviewed design change requires it.

## Priority and sequencing model

| Priority | Meaning |
|---|---|
| P0 | Shared contract or correctness blocker that would cause downstream rework |
| P1 | Required to make the complete game playable with placeholders |
| P2 | Required for LAN/online, content completion, and full presentation behavior |
| P3 | Production asset replacement, optimization, balance, and release hardening |

The ordered work packages below are dependency gates, not date estimates. Work may run in parallel only where the dependency column permits it.

| Package | Priority | Depends on | Primary result |
|---|---|---|---|
| 0 - Production contracts and validators | P0 | Current baseline | Repeatable templates and replaceable placeholder pipeline |
| 1 - Remaining shared gameplay systems | P0 | Package 0 | Complete combat edges, puzzles, pools, items, and rewind contracts |
| 2 - Production vertical slice | P1 | Packages 0-1 | Tutorial, hub, Florence, and one Fighter stage complete with placeholders |
| 3 - Nine character kits and Resonance perks | P1 | Packages 0-2 contracts | Canonical cross-mode roster behavior |
| 4 - Enemy and boss roster | P1 | Packages 0-2 contracts | Data-driven, pooled Story opposition |
| 5 - Campaign levels 2-15 | P1/P2 | Packages 2 and 4; Package 3 interfaces stable | Complete placeholder campaign |
| 6 - Ten-stage local Fighter completion | P1/P2 | Packages 2-3 | Complete local 1v1 experience |
| 7 - LAN and online rollback completion | P2 | Packages 3 and 6; Package 6 rollback-readiness gate | Production networking and user flow |
| 8 - Product-wide UI, audio, visuals, controls | P2 | Continuous; final pass after Packages 5-7 | Feature-complete presentation with placeholders |
| 9 - QA, performance, balance, and exports | P3 | Packages 5-8 | Content/platform-complete candidate |
| 10 - Production asset replacement | P3 | Approved final assets and Package 9 contracts | Asset-complete release candidate |

## Definition of done for every package

Every package must satisfy the applicable checks below before dependent work starts:

- `dotnet build FightersThroughTime.csproj` passes without new project warnings.
- Relevant GdUnit4 tests and headless scene smoke runs pass.
- New gameplay behavior is driven from `_PhysicsProcess()` at 60 Hz where authoritative.
- New Fighter behavior is fixed-point, snapshot-complete, hash-visible, rollback-safe, and isolated from Godot physics authority.
- Story progression modifiers do not enter normalized Fighter values.
- New high-frequency objects use `PoolManager` and fully reset mutable state on spawn/despawn.
- New visible copy uses translation keys and has an English entry.
- Keyboard and two-controller assignment are tested for affected flows.
- Pause, death/respawn or rewind, checkpoint/save, and scene-transition behavior are tested when relevant.
- Placeholder resources conform to final replacement contracts and are recorded in the asset manifest.
- The status ledger, relevant tests, and `AGENTS.md` are updated when the repository's implemented boundary materially changes.

## Package 0 - Production contracts, templates, and validation

Priority: P0. This package prevents content work from becoming another set of code-generated one-offs.

### Deliverables

- [x] Create one authoritative content manifest covering 16 Story levels, 10 Fighter stages, 9 characters, 36 abilities, 9 Resonance grids, the complete enemy roster, 15 bosses, dialogue sequences, UI screens, pools, audio sets, and visual assets.
- [x] Give every manifest entry a stable ID, source design section, resource path, owner system, placeholder/final status, and validation state.
- [x] Create reusable authored templates for Story levels, Fighter stages, player presentation, standard enemies, elites, bosses, projectiles, persistent constructs, pickups, checkpoints, dialogue triggers, puzzle objects, hazards, and pool configurations.
- [x] Define required node paths, groups, signals/events, animation names, event-track callbacks, collision layers, camera anchors, and spawn/rewind/snapshot interfaces for each template.
- [x] Build a placeholder kit: character/enemy silhouettes, portrait/icon set, graybox tile palette, parallax layers, basic particles/materials, and test audio stems/SFX.
- [x] Add validators for missing IDs/resources, duplicate tuning, invalid paths, translation keys, animation callbacks, collision masks, pool registrations, level checkpoints, stage bounds, hazard IDs, audio layers, and unresolved placeholder manifest entries.
- [x] Add per-scene `ScenePoolConfig` resources and warm-up budgeting support before scaling Story content.
- [x] Record current frame time, allocations, memory, draw batches, vertices, active sprites, and particles in Tutorial, Florence, and Test Arena as the comparison baseline.

### Exit criteria

- A new level, enemy, boss, character presentation, or Fighter stage can be created from a template without adding canonical tuning to a controller.
- Validators fail clearly when a required content contract is missing.
- Placeholder assets can be swapped by resource reassignment while node paths and gameplay behavior remain unchanged.

## Package 1 - Remaining shared gameplay systems

Priority: P0. Complete these shared systems before multiplying levels and encounters.

### Combat and movement completion

- [x] Move Story hitbox activation/deactivation to authored animation event tracks; placeholder animations use the final frame timings.
- [x] Finish ledge trigger geometry, single-occupancy rules, five-second timeout, pull-up/drop/jump choices, ledge damage response, and recovery tests.
- [x] Finish one-way platform authoring and double-tap-down collision behavior in representative scenes.
- [x] Complete independent aerial combo state, landing cancel, aerial frame data, crouched combo behavior, and the 30 percent crouched hurtbox-height reduction.
- [x] Verify hyper-armor suppresses hitstun/knockback but not HP damage in both modes; add the temporary shell/outline presentation hook.
- [x] Add FSM tests for every newly completed transition, interruption, death, respawn, pause, and scene-exit path.

### Puzzle and environment toolkit

- [x] Implement an event-driven `PuzzleManager` with stable puzzle IDs, prerequisite state, completion/reset events, checkpoint integration, and save/rewind policy.
- [x] Implement reusable levers and 90-degree rotating platforms for Florence.
- [x] Implement pressure plates, counterweights, movable weights, and weight-comparison objectives.
- [x] Implement signal/beam routing nodes and conductive-coil routing for Chicago-style puzzles.
- [x] Implement 100 HP Chronal Extractors with hazard emission, damaged/destroyed states, 25-dust reward, and rewind/reset behavior.
- [x] Implement destructible blocks, chests, floating interaction prompts, and a common `IInteractable` contract.
- [x] Implement room-transition triggers that update camera confiners and encounter activation.
- [x] Implement crumbling platforms, configurable cyclic hazards, and Temporal/Chronal Rift zones with Time-Loop Snap reset behavior.

### Pooling, drops, and rewind integration

- [x] Pool Story projectiles/zones, VFX, enemy instances, loot, Chronal Dust, healing items, buffs, and persistent constructs with the designed overflow policies.
- [x] Exercise capacity, recycle, reject, and repeated spawn-release behavior in automated tests.
- [x] Implement Story healing pickups at Easy 50 HP, Normal 25 HP, and Hard 10 HP.
- [x] Implement temporary damage/speed buffs with difficulty-scaled magnitude and duration, data-driven drop tables, auto-collect radius, and magnetization.
- [x] Implement small/medium/large drop visual tiers as replaceable placeholder resources.
- [x] During rewind, freeze enemy simulation, clear enemy projectiles, and rewind or reset enemies/constructs according to explicit type policies.
- [x] Complete two-second post-rewind invincibility and Timeline Collapse: hub return, 20 percent carried-dust penalty, and level restart choice.
- [x] Add presentation events for rewind ghosting, tint/scanline effects, music ducking, reverse sweep, and clock tick; Package 8 supplies the product-wide presentation.

### Exit criteria

- The shared systems can support every planned level without level-specific forks of core behavior.
- Rewind cannot duplicate loot, leave live projectiles, corrupt encounter state, or bypass puzzle/checkpoint progression.
- Representative gameplay loops have no routine instantiate/free churn for projectiles, enemies, VFX, or loot.

## Package 2 - Production vertical slice with placeholders

Priority: P1. Scope: New Story slot -> Tutorial -> Hub -> Florence -> Hub, plus one independently authored Florence Fighter stage.

### Tutorial, hub, and Florence

- [ ] Convert Tutorial and Florence from controller-built layouts to authored scenes using the level template and graybox tile resources.
- [ ] Complete all three Tutorial parts: fracture presentation, calibration movement/combat/rewind instruction, and advanced mobility gates.
- [ ] Complete Florence's four-room progression, rotating-gear puzzle, enemy waves, checkpoints, hazards, Borgia Inquisitor boss, entrance/exit dialogue, completion results, autosave, and hub return.
- [ ] Author the hub's placeholder spatial layout, walking paths, Calibration Bay return anchor, Chronal Repository terminal, Holodeck console, Resistance NPC interaction points, and sequential Temporal Portal.
- [ ] Wire the Holodeck to configured human-vs-CPU practice matches and return cleanly to the hub.
- [ ] Author Commander Sarah, Tutorial, Florence, hub, and boss dialogue as `DialogueSequenceData` resources using the documented typewriter and confirm behavior.
- [ ] Add placeholder portraits/emotions and make dialogue suspend gameplay while leaving UI navigation responsive.
- [ ] Complete Story HUD information, boss bar, enemy overhead bars, checkpoint feedback, level-completion overlay, dust auto-deposit, and loading treatment.
- [ ] Implement full Story difficulty effects on enemies, drops, and rewind without changing Fighter balance.
- [ ] Verify save/resume from every Tutorial/Florence checkpoint and the hub return anchor.

### First production-contract Fighter stage

- [ ] Create a Florence Fighter scene with unique platform geometry, bounds, camera anchors, spawn points, Orb points, and a deterministic era-specific hazard.
- [ ] Complete dynamic midpoint tracking and bounded zoom for the stage.
- [ ] Use the stage as the acceptance template for the nine remaining stage scenes.

### Placeholder presentation

- [ ] Supply complete temporary player/enemy animation libraries, Florence/hub backgrounds, tiles, VFX, dialogue UI treatment, ambient/combat/boss music stems, footsteps, attacks, hazards, and UI sounds.
- [ ] Prove synchronized music transitions, pause/low-health/ultimate ducking, haptics, shake, status feedback, and accessibility settings in the slice.

### Exit criteria

- A new slot can complete Tutorial and Florence at all three difficulties, spend deposited Dust, return to the hub, quit, and resume accurately.
- The same flow survives death, rewind exhaustion, pause, controller reconnect, checkpoint reload, and scene transitions.
- A local human-vs-human or human-vs-CPU Florence match completes from selection through results/rematch.
- The slice meets the documented frame budgets on the agreed baseline machine with placeholders enabled.

## Package 3 - Complete nine-character kits and Resonance behavior

Priority: P1. Use the stable data and presentation contracts from Packages 0-2.

### Cross-mode kit implementation

- [ ] Audit all 36 ability slots against Section 5 of `design-godot.md`; convert every generic projectile/zone approximation into the character's canonical mechanics.
- [ ] Complete Einstein's spacetime/gravity setup, Joan's radiant rushdown/hyper-armor, Leonardo's inventions/turret, Lincoln's rail strikes/shockwaves, Cleopatra's sand/serpents, Tesla's linked coils/electricity, Shakespeare's barriers/spectral actors, Mozart's sonic waves/platforms, and Pocahontas's glide/roots/nature spirits.
- [ ] Complete all nine movement abilities with character-specific rules, animation events, recovery, cooldowns, VFX/SFX hooks, and state interruption.
- [ ] Give persistent constructs stable owner/type/state IDs, deploy limits, pool reset behavior, Story rewind behavior, Fighter snapshot/hash fields, and rollback lifecycle tests.
- [ ] Maintain shared definitions and expected outcomes while keeping Story physics and Fighter fixed-point execution behind explicit adapters.
- [ ] Add placeholder sprite/portrait/animation/VFX/SFX resources for every character and every required state so none remains a colored controller-drawn rectangle.

### Resonance Grid completion

- [ ] Wire all 27 bespoke major perks, three per character, into the relevant Story abilities and systems.
- [ ] Keep perk queries character-scoped and Story-only; add tests proving normalized Fighter loadouts and hashes are unchanged by Story progression.
- [ ] Complete controller/D-pad grid navigation, prerequisites, tooltips, purchase confirmation, disabled-state explanations, unlock animation, and save/autosave behavior.
- [ ] Balance Chronal Dust level budgets, enemy/elite/boss/extractor rewards, and all node costs across a full placeholder campaign progression model.

### Exit criteria

- Every character can complete representative traversal, combat, boss, and puzzle scenarios in Story and a deterministic match in Fighter Mode.
- Every ability and perk has automated outcome coverage, and rollback across every entity lifecycle converges to identical hashes.
- No Story upgrade changes Fighter damage, movement, cooldown, entity limits, or deterministic state.

## Package 4 - Complete enemy and boss roster

Priority: P1. This package may run in parallel with Package 3 after Package 2's encounter contracts are stable.

### Deliverables

- [ ] Create canonical resources for the complete Section 6 standard/elite roster, including all 26+ required era-specific types; do not substitute one resource with visual recolors and duplicate IDs.
- [ ] Finish patrol, chase, attack, stunned, and dead states with difficulty-scaled reaction delays, attack cadence, navigation pacing, and consistent typed-hit behavior.
- [ ] Implement elite secondary abilities, cooldowns, stun resistance, telegraphs, and drop rules.
- [ ] Create resources and encounter controllers for all 15 bosses with weighted attack selection, distance filtering, phase thresholds, telegraphs, invincibility windows, interruption rules, and checkpoint/rewind behavior.
- [ ] Implement Mirror Paradox through the deterministic Fighter CPU/simulation boundary rather than standard boss AI.
- [ ] Pool every mob, elite, boss projectile, summon, hazard, and reward; define per-level warm-up configurations.
- [ ] Add temporary silhouettes, animation libraries, health-bar names, telegraphs, impact VFX, and audio hooks for every roster entry.
- [ ] Add AI, phase-transition, no-valid-attack, death, rewind, and repeated pool-cycle tests.

### Exit criteria

- The manifest contains every designed enemy and boss with valid data, scene, animation, pool, localization, and encounter references.
- Bosses select and execute valid attacks at melee/ranged distances and cannot deadlock between phases.
- Worst-case encounter budgets fit the AI, physics, allocation, sprite, and particle budgets before level production scales.

## Package 5 - Campaign levels 2-15

Priority: P1/P2. Build content in waves after the vertical slice and enemy templates pass.

### Per-level required gate

Every level must have:

- A separate authored scene, final room graph, graybox geometry, camera bounds, spawn/return anchors, and clear completion path.
- Two or three tested checkpoints, pools/warm-up data, encounter budgets, standard/elite placements, a boss arena where designed, drops, difficulty behavior, and safe rewind behavior.
- At least one era-specific traversal, puzzle, hazard, or encounter identity implemented mechanically rather than only described by a label.
- Entrance, critical-path, boss, exit, and hub-return dialogue resources where called for by the design.
- Level completion overlay, earned Dust accounting, auto-deposit, save/autosave, sequential unlock, pause, failure, and reload behavior.
- Placeholder background layers, tile palette, props, enemy/boss presentation, VFX, SFX, and synchronized music stems registered in the asset manifest.
- Content-validation, scene-smoke, completion-path, failure/rewind, localization, pool, and performance passes.

### Wave A - Complete Act I

- [ ] Level 2: Orleans - siege battles, shield towers, and trebuchet identity.
- [ ] Level 3: Chicago - World's Fair logic and energy-routing puzzles.
- [ ] Level 4: Paris - Bastille combat with stealth/searchlight zones.
- [ ] Level 5: Titanic - flooding/sinking systems and Act I finale flow.

### Wave B - Complete Act II

- [ ] Level 6: Pompeii - volcanic hazards and high-speed escape.
- [ ] Level 7: Nassau - ship-to-ship traversal, rope swinging, and pirate encounters.
- [ ] Level 8: Alexandria/Egypt - sand traversal, hieroglyph puzzles, and authored narrative reveal.
- [ ] Level 9: Berlin - snow/urban combat and stealth elements.
- [ ] Level 10: Globe Theatre - staged encounters, trapdoors, and theatrical hazards.
- [ ] Level 11: Gettysburg - linear battlefield assault and encounter pacing.
- [ ] Level 12: Lunar Landing - low-gravity traversal and Act II finale flow.

### Wave C - Complete Act III

- [ ] Level 13: Chronal Void - shifting gravity, transition flow, and Mirror Paradox encounter.
- [ ] Level 14: Neo-Earth - future laboratory assault and Apex Archive escalation.
- [ ] Level 15: Library of Alexandria - restoration sequence, final multi-phase boss, ending, credits, completion save flag, and post-credits return behavior.

### Exit criteria

- One locked character can complete levels 0-15 in order at each difficulty and resume at every checkpoint/hub boundary.
- All nine characters can traverse every required critical path; character abilities may offer alternate solutions but cannot be mandatory unless all nine have an equivalent route.
- Campaign completion, credits, statistics, and completed-slot state survive restart and save migration.

## Package 6 - Ten-stage local Fighter Mode completion

Priority: P1/P2. The Florence stage from Package 2 is the template, not the shared runtime scene for all entries.

### Stage production

- [ ] Create nine additional independently authored stage scenes so all ten catalog entries have distinct scene paths, platform layouts, spawn points, camera bounds, Orb points, and hazard anchors.
- [ ] Replace generic hazard behavior with ten era-specific deterministic implementations, each with Off/Low/Medium/High frequency, warning/active/recovery phases, snapshots, hashes, and rollback tests.
- [ ] Give every stage distinct placeholder parallax layers, tile/prop palette, lighting treatment, preview image, music set, hazard VFX/SFX, and pool configuration.
- [ ] Validate solid side/top boundaries, bottom blast zone, ledges, one-way platforms, spawn safety, camera framing, and worst-case performance for every stage.

### Match and CPU completion

- [ ] Complete respawn-platform presentation, five-second dissolve behavior, input lock, drop, and invulnerability.
- [ ] Complete KO hit-freeze, slow motion, spotlight, KO stamp, winner pose, audio/fanfare, and results transitions without altering deterministic match state.
- [ ] Complete local controller-disconnect forced pause, reconnect assignment, and safe menu-exit behavior.
- [ ] Finish CPU Orb pursuit, hazard avoidance, off-stage recovery, jumps, movement ability, and Special 2 behavior at all difficulty bands.
- [ ] Verify every rule setting persists correctly through rematch and every match can end by HP KO, stock exhaustion, bottom fall, timer decision, or true draw.
- [ ] Pass a rollback-readiness gate across all kits/stages: complete snapshots and hashes, delayed-input convergence, bounded history, and worst-case seven-frame resimulation within the baseline frame budget.

### Exit criteria

- All ten stage choices produce a visibly and mechanically distinct local 1v1 match.
- Human-vs-human and human-vs-CPU flows pass character/stage/rules selection, countdown, pause/disconnect, match, results, rematch, and return paths.
- Local Fighter Mode remains the same deterministic authority later used by online play.
- The rollback-readiness gate passes before production transport integration starts.

## Package 7 - LAN and online rollback completion

Priority: P2 release blocker. Do not begin production transport integration until the Package 6 rollback-readiness gate passes.

### Transport and session protocol

- [ ] Keep protocol-v2 compatibility tests while extending the protocol for handshake, build/protocol gates, peer identity, fighter/stage/rules agreement, deterministic seed, ready/load state, synchronized start, pause/forfeit, snapshot resync, disconnect, and rematch.
- [ ] Add LAN discovery and direct join around the existing UDP transport.
- [ ] Implement bounded full-state transfer and authoritative resync after confirmed desync, including integrity/version checks and diagnostic capture.
- [ ] Integrate Steam Networking Sockets through a transport adapter for encrypted P2P, NAT traversal, and relay fallback; keep simulation code transport-agnostic.
- [ ] Implement private six-digit rooms and a regional public casual 1v1 queue without adding account/profile scope.

### User flow and failure handling

- [ ] Build online lobby character selection, ready state, stage/rules agreement, synchronized loading, match start, results, rematch consensus, and correct queue/menu return behavior.
- [ ] Implement hold-Pause-for-three-seconds online forfeit.
- [ ] Add the Chronal Jitter warning above 150 ms latency or 5 percent loss.
- [ ] Add a 15-second reconnection/timeout presentation and deterministic match resolution for peer departure, relay failure, host loss, and incompatible builds.
- [ ] Ensure statistics are written exactly once and only after an agreed match result; do not count a true draw as a win/loss.

### Verification

- [ ] Expand deterministic transport tests for latency, jitter, reordering, duplication, loss, delayed corrections, disconnect, reconnection, resync, and timeout.
- [ ] Run long LAN/direct, Steam P2P, and relay soak matches across all characters, stages, hazards, Orbs, and entity lifecycles.
- [ ] Measure snapshot size, rollback depth, prediction correction, resimulation time, bandwidth, and frame pacing against the seven-frame rollback budget.

### Exit criteria

- LAN, private online, and public-queue 1v1 matches finish without divergent hashes under the approved network matrix.
- Desync, timeout, incompatible build, forfeit, and relay failure produce recoverable localized UX and unambiguous statistics.
- Gameplay never falls back to delay-based authority.

## Package 8 - Product-wide UI, audio, visuals, controls, and localization

Priority: P2. Build the reusable systems early, then perform this completion pass after campaign, Fighter, and online flows exist.

### UI and narrative presentation

- [ ] Replace remaining controller-built production UI with reusable scenes and shared theme resources.
- [ ] Complete Story/Fighter HUD polish, boss phase notches, conditional enemy bars, cooldown/status indicators, level results, portal and VS loading treatments, network warnings, credits, and campaign-completion screens.
- [ ] Complete dialogue glass treatment, portraits/emotions, text chirps, gameplay suspension, and all initial-scope campaign/hub dialogue resources.
- [ ] Verify controller focus, keyboard focus, back/cancel behavior, modal ownership, pause variants, safe-area layout, HUD opacity, and supported resolutions/aspect ratios on every screen.

### Controls and accessibility

- [ ] Serialize complete InputMap binding overrides in global save data and restore them before gameplay input begins.
- [ ] Implement binding conflict UX and reset-to-default controls.
- [ ] Verify haptics, shake scale, damage-number visibility, HUD opacity, audio levels, screen mode, resolution, and VSync across all Story, Fighter, boss, hazard, rewind, and online flows.

### Audio implementation

- [ ] Finalize the Master/Music/SFX/UI hierarchy and Environmental/Combat/Movement sub-buses.
- [ ] Implement synchronized ambient/combat/boss stems per level/stage, intensity transitions, last-stock transition, KO stinger/silence, pause/low-health/ultimate/rewind/underwater snapshots, and crossfades.
- [ ] Implement surface-specific footsteps, character ability cast/impact hooks, environmental/destruction/hazard sounds, dialogue chirps, reverb zones, and low-pass filtering.
- [ ] Keep all short-lived playback in the existing pool and validate voice-stealing/overflow behavior.

### Visual implementation

- [ ] Implement the shared alpha-outline/glow shader, per-instance parameters, PointLight2D accompaniment, status indicators, hyper-armor shell, hit feedback, and rewind treatment.
- [ ] Add normal-map support, era VFX emitters, Chronal Orb visuals, Chronal Extractor states, environment lighting, and particle emission caps through reusable resources.
- [ ] Make off-screen sprite animation and expensive visual processing suspend safely without changing authoritative gameplay.

### Localization completion

- [ ] Audit every visible string, error path, tooltip, dialogue line, item/enemy/boss/stage name, settings label, and credits entry for translation keys.
- [ ] Complete the English CSV and generated translation resource; add validation for missing/unused keys and raw user-visible English in C# and scenes.

### Exit criteria

- Every initial-release screen and flow is localized, controller-navigable, settings-aware, and functional with placeholder presentation.
- All audio and visual behavior is driven through stable hooks so production assets can replace temporary resources without code changes.
- Deferred accessibility and language options do not appear as broken or disabled release UI.

## Package 9 - QA, performance, balance, and platform completion

Priority: P3. Measurement is continuous; this package is the final content/platform gate.

### Automated and manual coverage

- [ ] Expand FSM tests to complete state/interrupt coverage for players, enemies, bosses, rewind, and dialogue suspension.
- [ ] Add campaign route/completion tests, all-level content validation, pool soak tests, save/resume across all checkpoints, and campaign-completion migration tests.
- [ ] Add long deterministic simulation, network matrix, scene-transition, pause/resume, controller reconnect, save corruption, and repeated rematch soak tests.
- [ ] Maintain regression coverage for the substantially complete save, combat, input, collision, Resonance isolation, match, and localization foundations.

### Performance and allocation gates

- [ ] Profile representative quiet, combat, boss, hazard-heavy, and worst-case rollback scenes against 16.67 ms total, 2.5 ms physics, 3.5 ms AI, and 8 ms rendering budgets.
- [ ] Verify at most roughly 150 draw batches, 150,000 visible vertices, 60 active AnimatedSprite2D nodes, 500 active particles, and 4 GB runtime RAM.
- [ ] Eliminate gameplay-time GC spikes and unpooled high-frequency allocation.
- [ ] Add VisibilityNotifier2D/off-screen animation control, paced AI updates, particle caps, and pool-size tuning where measurements require them.
- [ ] Record per-level/stage budgets in the content manifest and prevent regressions in representative automated benchmarks where practical.

### Balance and platform gates

- [ ] Run structured playtests for all nine characters, all difficulties, all bosses, Dust economy, Resonance progression, drops, hazards, CPU levels, match modes, and network conditions.
- [ ] Tune only canonical resources; keep Story difficulty and Resonance changes out of Fighter normalization.
- [ ] Create and validate Windows, macOS, Linux/SteamOS, and Steam Deck exports, including input devices, aspect modes, save permissions, Steam transport, and suspend/resume where supported.
- [ ] Complete crash/desync diagnostics, legal/third-party notices, credits, release checklists, known issues, save/protocol compatibility policy, and recovery procedures.

### Exit criteria

- The full placeholder campaign and all local/LAN/online 1v1 flows pass release acceptance on target platforms.
- Worst-case representative scenes hold 60 FPS within the documented budgets with no gameplay GC spikes.
- No P0/P1/P2 non-deferred functional gap in `IMPLEMENTATION_STATUS.md` remains open.

## Package 10 - Production asset replacement

Priority: P3. This package can proceed asset family by asset family whenever approved replacements arrive.

### Replacement waves

- [ ] Characters: nine production sprite/animation sets, portraits/emotions, ability VFX, persistent constructs, ultimates, normal maps, and status/hyper-armor presentation.
- [ ] Enemies and bosses: complete sprite/animation/telegraph/VFX sets for the full roster.
- [ ] Environments: final tile palettes, parallax backgrounds, props, lighting, normal maps, hazards, destructibles, extractors, and puzzle art for hub, levels 0-15, and ten stages.
- [ ] UI: final theme, frames, buttons, icons, HUD art, portraits, stage previews, loading treatments, dialogue treatment, credits, and network indicators.
- [ ] Audio: approved level/stage stem sets, character attacks, movement surfaces, bosses, hazards, rewind, UI, dialogue chirps, and cinematics.

### Replacement workflow

- [ ] Replace resources one manifest entry at a time while preserving IDs, animation/event names, dimensions/origins, collision contracts, loop lengths, and audio sync points.
- [ ] Visually inspect every imported asset in its real scene; verify filtering, compression, mipmaps, normal maps, outlines, lights, clipping, layering, and color readability.
- [ ] Re-run gameplay, rollback, localization, pool, memory, draw-call, particle, and platform checks after each asset family.
- [ ] Keep placeholders available as recoverable development fallbacks until the full replacement family passes; remove them from release paths only after verification.

### Exit criteria

- The asset manifest contains no unresolved initial-release placeholder entry.
- Production replacements do not change deterministic hashes, hit timing, collision outcomes, save IDs, or translation keys.
- All target platforms remain within memory, rendering, and frame-time budgets.

## Gap-analysis coverage matrix

Every numbered section in `IMPLEMENTATION_STATUS.md` has an implementation package or a documented deferred disposition.

| Status section | Resolution package(s) |
|---|---|
| 1. Campaign levels | 2, 5, 8, 10 |
| 2. Fighter arenas | 2, 6, 8, 10 |
| 3. Character kits and abilities | 1, 3, 8, 10 |
| 4. Enemies and bosses | 4, 5, 8, 10 |
| 5. Online networking and multiplayer | 7, 9 |
| 6. Temporal Resonance Grid | 3, 8, 9 |
| 7. Save system | Regression and campaign-integration gates in 2, 5, and 9; no crypto rewrite planned |
| 8. Hub world | 2, 5, 8, 10; NPC-gated portal remains deferred |
| 9. Combat systems | 1, 3, 8, 9 |
| 10. Chronal Rewind | 1, 2, 8, 9 |
| 11. UI and menus | 2, 6, 7, 8, 10 |
| 12. Audio and music | 2, 8, 10 |
| 13. Visual art and shaders | 0, 2, 3, 4, 8, 10 |
| 14. Dialogue and narrative | 2, 5, 8, 10; home-era variations remain deferred |
| 15. Puzzles and interaction | 1, 2, 5 |
| 16. Object pooling | 0, 1, 4, 5, 9 |
| 17. Input and controls | 6, 7, 8, 9 |
| 18. Accessibility and settings | 8, 9; future expansion options remain deferred |
| 19. Localization | 8, 9; additional languages remain deferred |
| 20. Testing and QA | Every package, with final gates in 9 |
| 21. CPU Fighter AI | 3, 6, 9 |
| 22. Fighter match flow | 6, 7, 8, 9 |
| 23. Story campaign flow | 2, 5, 8, 9 |
| 24. Items and power-ups | 1, 2, 5, 8 |
| 25. Performance and targets | Baseline in 0, package-level checks throughout, final gates in 9-10 |

## Recommended next implementation batch

Start with this bounded batch before producing levels 2-15:

1. Build the manifest, validators, authored templates, placeholder kit, and performance baseline from Package 0.
2. Complete Story animation callbacks, ledges, one-way platforms, aerial/crouch combat, hyper-armor behavior, and their tests.
3. Implement the puzzle/environment toolkit and finish pooling for Story projectiles, enemies, VFX, and loot.
4. Complete non-player rewind/reset policy, Story pickups, and Timeline Collapse.
5. Promote Tutorial, hub, Florence, and one Florence Fighter stage to the placeholder production contracts.
6. Do not start bulk level, roster, or stage duplication until the vertical-slice exit criteria pass.

After the vertical slice, Packages 3 and 4 can run in parallel. Campaign waves can begin once the relevant enemies, bosses, puzzles, and character interfaces are stable. Package 6 can proceed alongside campaign production. Package 7 starts after the local deterministic match and rollback performance gates pass. Package 8 is continuous and closes after all user flows exist.

## Initial-release completion definition

The remaining-gap program is complete when:

- All non-deferred rows in `IMPLEMENTATION_STATUS.md` are implemented, verified, or replaced by an explicitly approved design change.
- Story Mode supports a locked-character campaign through levels 0-15 with hub progression, puzzles, enemies, bosses, dialogue, rewind, Dust, all nine Resonance grids/perks, save/resume, ending, and credits.
- All nine characters are immediately available and have cross-mode-coherent canonical kits.
- Fighter Mode supports complete 1v1 human/local CPU/LAN/online rollback matches on ten distinct stages with normalized values, hazards/items, results, rematch, errors, and statistics.
- English UI/dialogue is fully key-localized; controls/settings/accessibility behavior works everywhere in initial scope.
- Save security/recovery, deterministic rollback, networking, pools, content manifests, performance, and platform exports pass their automated and manual gates.
- The game is feature- and platform-complete with placeholders; final shipping status is reached after the Package 10 asset manifest is fully replaced and revalidated.
