# Fighters Through Time: Implementation Gap Analysis

Last audited: 2026-08-08 against `design-godot.md` and the current codebase.

P0 remediation update: Packages 0 and 1 in `IMPLEMENTATION_PLAN.md` are implemented and verified. Shared content contracts, the placeholder pipeline, combat/movement edges, the puzzle/environment toolkit, Story pools/drops, and rewind/timeline-collapse contracts now exist; production content and final presentation remain later packages.

Package 4 update (2026-08-07): Section 4 below is superseded for the enemy and boss roster. All 27 `EnemyData` and 15 `BossData` resources are authored, manifest-registered, localized, pooled, and covered by tests; `EnemyController`, `BossController`, `EnemyAbilityExecutor`, `BossEncounterController`, and `MirrorParadoxController` are implemented. What remains for those systems is production art/animation (Package 8), placement inside campaign levels 2-15 (Package 5), and cinematic boss presentation (Package 8).

Package 5 update (2026-08-08): **Section 1 below is superseded.** All sixteen campaign levels are authored, routed, pool-wired and manifest-flipped; levels 2-15 build on `StoryLevelControllerBase` with room graphs, three checkpoints each, the locked encounter economy, per-level era mechanics from the fourteen new toolkit components, boss arenas, extractors, and dialogue resources. The enemy/boss roster is now placed across the campaign rather than only in Florence, and the ending chain (Temporal Core -> ending dialogue -> credits -> `IsCompleted` -> Main Menu) is complete. What remains for campaign content is production presentation: art to replace graybox geometry, animation, music stems, VFX, and cinematic boss presentation (all Package 8). Percentages in the table below are updated accordingly.

Package 6 update (2026-08-08): **Sections 2, 21 and 22 below are superseded.** All ten Fighter stages are independently authored production-contract scenes with their own fixed-point geometry, era-specific deterministic hazard, placeholder parallax presentation, pool config, audio set and preview plate; the match flow is complete end to end (countdown, respawn platform, KO presentation, pause, disconnect handling, results, rematch, all five end conditions); the CPU is complete at all three difficulty bands; and the rollback-readiness gate passes across all nine kits and all ten stages. What remains for Fighter Mode is production art, music, VFX and cinematic KO presentation (Package 8), rendering-cost measurement on target hardware (Package 9), and the online work (Package 7).

Package 8 update (2026-08-08): **Sections 11, 12, 13, 17, 18 and 19 below are superseded and have been rewritten in place.** Every production UI screen is an authored, themed, focus-authored scene; the audio framework (bus layout, stem director, snapshot mixer, voice pool) is complete with all 27 audio sets authored on placeholder stems; the visual framework (outline/glow shader, glow arbiter, VFX emitter with all 78 roster and ability hooks bound, particle budget, off-screen suspension, `Parallax2D`, era lighting rigs) is complete; input remapping, conflict UX, reset-to-default and display persistence are complete; and localization is gated in both directions by three new permanent tools. **The distinction that matters for everything below: Package 8 delivered frameworks and placeholder content, not assets.** Production art, music and VFX remain Package 10. Nothing in the visual layer has been judged by a human — no headless gate can do it.

This document provides a detailed comparison of what has been implemented versus what is documented in the design specification. It covers every major system, feature, and content area.

---

## High-Level Completion Estimate

| Category | Estimated Completion |
|----------|---------------------|
| Core Architecture & Systems | ~75-80% |
| Fighter Mode Foundation | ~85-90% (ten authored stages, ten era hazards, complete match flow and CPU, rollback gate passed; production art/audio and online remain) |
| Story Mode Flow | ~80-85% (hub, all 16 levels, checkpoints/resume, rewind, completion, credits, and the campaign-complete save state are implemented) |
| Campaign Content (Levels 2-15) | ~75-80% (all 14 levels authored and tested with graybox geometry and placeholder presentation; art/audio/VFX remain) |
| UI, Controls & Localization | ~90-95% (every production screen authored and themed, focus authored, full remapping and settings persistence, localization gated both ways; only the three Package-7 network screens and production UI art remain) |
| Production Art & Animation | ~2-5% (unchanged — Package 8 built the pipelines that consume art, not the art) |
| Audio & Music | ~30-35% (framework complete and fully wired, all 27 sets authored; every sound is a placeholder) |
| Online Networking | ~20-25% |
| Narrative & Dialogue | ~55-60% (all 17 dialogue sets authored and localized; character-specific variants and VO remain deferred) |
| Puzzles & Environmental Interaction | ~85-90% (shared toolkit plus 14 era-mechanic components, all placed across the authored campaign) |
| Enemy/Boss Roster | ~90% (all 27 enemies and 15 bosses authored, wired, and now placed across campaign levels 1-15; production art and cinematic boss presentation remain) |

---

## 1. Campaign Levels (Scenes)

| Level | Design Spec | Implementation Status |
|-------|-------------|----------------------|
| Level 0 - Tutorial | Full 3-part tutorial (Fracture, Calibration, Mobility) | **Implemented (placeholder presentation)** - `Level_00_Tutorial.tscn`; code-generated graybox geometry. Predates `StoryLevelControllerBase` and was not retrofitted |
| Level 1 - Florence | Full 4-room layout with puzzles, enemies, boss | **Implemented (placeholder presentation)** - `Level_01_Florence.tscn`, graybox geometry plus one parallax background asset. Predates `StoryLevelControllerBase`; its 3 budgeted Chronal Extractors are still unauthored (`docs/DUST_ECONOMY.md` §6) |
| Level 2 - Orléans | Full level with siege battles and shield towers | **Implemented (placeholder presentation)** - `Level_02_Orleans.tscn` on `StoryLevelControllerBase`: room graph, 3 checkpoints, locked encounter economy, extractors, 2× shield-generator towers with forcefield gates, mortar hazard lanes; siegemaster_duke |
| Level 3 - Chicago | Full level with logic/routing puzzles | **Implemented (placeholder presentation)** - `Level_03_Chicago.tscn` on `StoryLevelControllerBase`: room graph, 3 checkpoints, locked encounter economy, extractors, scene-authored beam-routing puzzle with dead-end 'crowd tap' mis-routing, vertical climb; chronal_inventor |
| Level 4 - Paris | Full level with stealth/searchlight zones | **Implemented (placeholder presentation)** - `Level_04_Paris.tscn` on `StoryLevelControllerBase`: room graph, 3 checkpoints, locked encounter economy, extractors, searchlight ultimate-drain corridors, 2 rescuable prisoners behind destructible locks; revolutionary_tribunal |
| Level 5 - Titanic | Act I finale with flooding/sinking mechanics | **Implemented (placeholder presentation)** - `Level_05_Titanic.tscn` on `StoryLevelControllerBase`: room graph, 3 checkpoints, locked encounter economy, extractors, two non-overlapping rising-water zones, listing-deck slopes, arena floods in phase 2; tidal_eraser |
| Level 6 - Pompeii | High-speed escape with volcanic hazards | **Implemented (placeholder presentation)** - `Level_06_Pompeii.tscn` on `StoryLevelControllerBase`: room graph, 3 checkpoints, locked encounter economy, extractors, escape-sequence lava front, counterweight puzzle, 2 rescuable civilians, ash geysers; vulcan_decimator |
| Level 7 - Nassau | Ship-to-ship combat and rope swinging | **Implemented (placeholder presentation)** - `Level_07_Nassau.tscn` on `StoryLevelControllerBase`: room graph, 3 checkpoints, locked encounter economy, extractors, pendulum rope swings, path-moving boarding skiffs, mortar hazards; dread_admiral |
| Level 8 - Egypt | Sand dunes and hieroglyph puzzles | **Implemented (placeholder presentation)** - `Level_08_Egypt.tscn` on `StoryLevelControllerBase`: room graph, 3 checkpoints, locked encounter economy, extractors, deep-sand movement dampeners, hieroglyph sequence lock, authored Cleopatra post-boss beat; jackal_priest |
| Level 9 - Berlin | Stealth elements and snowy urban combat | **Implemented (placeholder presentation)** - `Level_09_Berlin.tscn` on `StoryLevelControllerBase`: room graph, 3 checkpoints, locked encounter economy, extractors, searchlight strike-mode stealth corridors, guard-tower climbs; iron_chancellor |
| Level 10 - Globe Theatre | Theatrical stage combat with trapdoors | **Implemented (placeholder presentation)** - `Level_10_Globe.tscn` on `StoryLevelControllerBase`: room graph, 3 checkpoints, locked encounter economy, extractors, trapdoor stage floor, pendulum gallery traverse, idle-punish audience hazard; tragedy_king |
| Level 11 - Gettysburg | Linear battlefield assault | **Implemented (placeholder presentation)** - `Level_11_Gettysburg.tscn` on `StoryLevelControllerBase`: room graph, 3 checkpoints, locked encounter economy, extractors, artillery-line hazards with cover geometry, 2 shield-generator arrays; siege_cannon (widest arena, 12-unit band) |
| Level 12 - Lunar | Low-gravity platforming (Act II finale) | **Implemented (placeholder presentation)** - `Level_12_Lunar.tscn` on `StoryLevelControllerBase`: room graph, 3 checkpoints, locked encounter economy, extractors, level-wide low gravity, vacuum vents, path-moving platform ascent; gravity_overseer (3 phases) |
| Level 13 - Chronal Void | Transitional level with shifting gravity | **Implemented (placeholder presentation)** - `Level_13_ChronalVoid.tscn` on `StoryLevelControllerBase`: room graph, 3 checkpoints, locked encounter economy, extractors, cycling gravity fields (all scales < 1.0), era-mashup motif platforms, drifting shards, rift pockets; Mirror Paradox instead of a scripted boss |
| Level 14 - Neo-Earth | Future laboratory assault | **Implemented (placeholder presentation)** - `Level_14_NeoEarth.tscn` on `StoryLevelControllerBase`: room graph, 3 checkpoints, locked encounter economy, extractors, phase-offset laser grids with a computed survivable walk, 3 disjoint anti-gravity containment pockets, arena grid armed by boss phase; archive_prime (3 phases) |
| Level 15 - Alexandria | Final boss and timeline restoration | **Implemented (placeholder presentation)** - `Level_15_Alexandria.tscn` on `StoryLevelControllerBase`: room graph, 3 checkpoints, locked encounter economy, extractors, burning-library firestorm escape, Temporal Core restoration, ending dialogue, credits, `IsCompleted`, Main Menu; apex_eraser (3 phases, 1200 HP) |

**Status (Package 5, 2026-08-08):** all sixteen level scenes exist with room layouts, camera-confined room graphs, puzzles/era mechanics, three checkpoints each, enemy placements at the locked economy counts, boss arenas, extractors, and dialogue. Each has a `tests/ContentValidation/LevelNNContentTests.cs` suite, a `scene_pool_catalog.tres` row, and a `SceneSmokeTests` entry.

**Still missing:** authored tile geometry and environment art (everything is code-built graybox), animation libraries, music stems, VFX, cinematic boss presentation, Florence's 3 extractors, and a human end-to-end playthrough of levels 0-15 at each difficulty.

---

## 2. Fighter Mode Arenas

Package 6 (2026-08-08) closed this section. Plan of record: `docs/PACKAGE6_FIGHTER_PLAN.md`.

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| 10 unique arena scenes | Individually authored with unique platform layouts, backgrounds, and hazard systems | **Implemented** - ten `scenes/fighter/FighterStage_*.tscn`, one per catalog entry, each routed by its own `ScenePath` |
| Per-stage platform layouts | Unique geometry per arena (documented in Section 10 of design doc) | **Implemented** - ten `FighterStageGeometry` entries (walls, ceiling, blast zone, spawn distance, 2-3 one-way platforms over a solid floor, hazard and orb anchors). Every scene marker and collider is pinned to its geometry by the shared `FighterStageConformance` validator |
| Era-themed stage hazards | 10 unique hazard behaviors (steam pipes, trebuchets, volcanic rockfall, etc.) | **Implemented** - ten era-specific implementations with real warning/active/recovery phases, per-type movement/targeting/dwell/idle behaviour, one-shot per-fighter hit masks where specified, and a blocked tick costing one shield charge |
| Stage backgrounds/art | Historical themed parallax backgrounds per arena | **Placeholder** - each stage ships a `ParallaxBackground` with two or more layers at distinct scroll factors over era-palette SVG placeholders, plus a stage-select preview plate. Production art is Package 8. The `ParallaxBackground` -> `Parallax2D` migration is deliberately deferred to that pass |
| Per-stage lighting treatment | `Light2D`/`CanvasModulate` per era | **Not implemented** - flat placeholder colour only; Package 8 |
| Dynamic Fighter camera | Midpoint tracking with zoom based on distance | **Implemented** - `FighterCamera` wired per stage with authored limits and 1.0-1.4 zoom |
| Per-stage pool warm-up | Each stage warms its own gameplay pools | **Implemented** - ten `fighter_stage_*_pool_config.tres` mapped in `scene_pool_catalog.tres`. Florence warmed no pools at all before this package |
| Per-stage music set | Ambient/combat/climax stems per stage | **Placeholder** - ten `StageAudioSet` resources over the shared placeholder stems; real stems are Package 8 |

---

## 3. Character Kits & Abilities

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| 9 Character data resources | Baseline stats per character | **Implemented** - All 9 `.tres` files in `resources/Characters/` |
| 36 ability data resources | 4 abilities × 9 characters | **Implemented** - All 36 `.tres` files in `resources/Abilities/` |
| Ability scripts | Character-specific executable logic | **Partially implemented** - Scripts exist for all 9 characters but use placeholder projectile/zone behavior, not production VFX/behavior |
| AnimatedSprite2D frame callbacks | Frame-perfect hitbox activation from animations | **Not implemented** - No production animation tracks exist; hitboxes are code-generated |
| Production sprite sheets | Full animation sets per character (17+ animations each) | **Not implemented** - All character visuals are code-generated colored rectangles |
| Character-specific persistent objects | Tesla Coils, Clockwork Turret, Serpent Nest, Vine Snare | **Partially implemented** - Deterministic entities exist in FighterSim with generic behavior; not visually authored |
| Hyper-Armor visual effects | Chronal crystalline shell shader overlay | **Not implemented** |
| Movement abilities | Per-character unique mobility (Warp, Wings, Ornithopter, etc.) | **Partially implemented** - Data and deterministic execution exist; visual presentation placeholder |

---

## 4. Enemies & Bosses

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| 26+ enemy types | 2 standard + 2 elite per era × 10+ eras | **Implemented** (Package 4, 2026-08-07) - 27 `EnemyData` resources with unique IDs, distinct placeholder tints, and manifest rows; production art/animation remain |
| Era-specific altered mobs | Unique enemies per historical period (Cyber-Guard, Laser Archer, Voltaic Shock Drone, etc.) | **Implemented** - Every era in the plan's Section 4.1 mapping has an authored standard and elite; Titanic and Act III reuse the cultist roster by design |
| Enemy AI states | Patrol → Chase → Attack → Stunned → Dead with reaction delays | **Implemented** - `EnemyController` runs the full state set with seeded, difficulty-scaled reaction delays, telegraph windows, flying and wall-phasing behavior, frontal damage reduction, a death animation window, and checkpoint/rewind capture |
| Elite mob abilities | Secondary abilities with cooldowns and stun resistance | **Implemented** - `EnemyData.EliteAbilities : EnemyAbilityData[]` cycles standard ↔ elite against `EliteAbilityCooldown`; every elite carries `StunResistance > 0` |
| 15 boss encounters | Multi-phase bosses with attack patterns, weighted random selection, phase transitions | **Implemented** - 14 scripted `BossData` kits (49 abilities) plus Mirror Paradox; seeded weighted/distance selection, `AbilityMinPhase` gating, phase invincibility, interruption rules, and `BossEncounterController` wiring proven on Florence. Cinematic presentation is Package 8 |
| Boss attack animations/phases | Scripted phase transitions, telegraphed attacks, invincibility windows | **Implemented with placeholder presentation** - `EnemyAbilityExecutor` runs telegraph → active → recovery for every archetype and raises `EventBus.OnEnemyPresentation`; real VFX/SFX bind in Package 8 |
| Mirror Paradox boss (Level 13) | Uses CPU Fighter AI instead of standard boss logic | **Implemented** - `MirrorParadoxController` drives the real `FighterCpuController` Hard decision table through a Story-side `CpuDecisionObservation` adapter; the clone uses normalized character data with no Resonance perks. `Level_13_ChronalVoid.tscn` builds the encounter in `OnLevelReady` and passes the session's locked character, and its content test asserts the level wires no `BossController`/`BossEncounterController` at all |
| Distance-based attack selection | Melee vs ranged ability filtering by player distance | **Implemented** - `BossController` filters `BossAbilities` by `RangeClass` against `MeleeRangeThreshold`/`RangedRangeThreshold` and falls back to the full set rather than deadlocking; every scripted boss authors both bands |
| Per-level enemy pool warm-up | Warm every mob/elite/projectile/reward pool before a level loads | **Implemented** - `resources/Pools/level_pool_configs/level_02..15_pool_config.tres`, all fourteen now wired into `scene_pool_catalog.tres` with per-scene `ConfigID` assertions (Package 5, 2026-08-08) |

---

## 5. Online Networking & Multiplayer

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| GGPO-style rollback netcode | Frame-perfect deterministic rollback with 7-frame budget | **Foundation only** - Klotho FP64 simulation with rollback/resimulation works locally and via in-memory transport |
| Direct-IP UDP LAN transport | Binary input/hash packets with acknowledgements | **Implemented** - Protocol v2 with the Roll bit (the Dash bit is reserved wire padding since the universal dash's 2026-08-09 removal) |
| Steam Networking Sockets | NAT traversal, relay, encrypted P2P | **Not implemented** |
| LAN discovery | Auto-discover peers on local network | **Not implemented** |
| Connection handshake/negotiation | Rules agreement, version check, synchronized start | **Not implemented** |
| Full state resync | Desync recovery via authoritative snapshot transfer | **Not implemented** |
| Public matchmaking queue | Regional casual queue with latency-based pairing | **Not implemented** |
| Private lobbies with room codes | 6-digit room invite codes for direct join | **Foundation only** - Cryptographic room code generation exists |
| Online Character Select lobby flow | Both players select/ready in shared CSS | **Not implemented** |
| Forfeit mechanism | Hold Pause for 3 seconds during online play | **Not implemented** |
| Controller disconnect handling | Forced pause + reconnect prompt for local play | **Not implemented** |
| Desync hash protocol | Pause + authoritative resync on mismatch | **Partially implemented** - Desync detection/events exist, full resync flow does not |
| Latency/jitter HUD warning | "Chronal Jitter" amber icon at >150ms or >5% loss | **Not implemented** |
| Reconnection window | 15-second countdown with timeout/forfeit | **Not implemented** |

---

## 6. Temporal Resonance Grid (Story Progression)

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| 9 character-specific 3×3 grids | 9 nodes per character with prerequisites and costs | **Implemented** - All 9 `.tres` resources authored |
| Generic stat modifier resolution | Stat bonuses applied to Story characters at runtime | **Implemented** |
| Deposit/Unlock UI | Interactive constellation grid panel for purchasing nodes | **Implemented** |
| Bespoke major perk execution | 27 unique perk behaviors (3 per character) | **Not implemented** - Grid data exists but bespoke behaviors are not wired to abilities |
| Chronal Dust economy | Drop rates per mob/elite/boss/extractor, level budgets | **Partially implemented** - Guaranteed pooled Dust plus Easy/Normal/Hard helper-item tables are implemented; full campaign economy balancing remains |
| Chronal Dust visual tiers | Small/Medium/Large sprites based on quantity | **Implemented with replaceable placeholders** - 1-5, 6-24, and 25+ tiers use resource-driven textures |
| Fighter Mode isolation | Resonance excluded from Fighter stats | **Implemented** - `FighterLoadoutFactory` uses normalized base resources |
| Grid UI constellation navigation | D-pad navigation, tooltips, purchase confirmation, animations | **Partially implemented** - Basic panel exists |

---

## 7. Save System

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| AES-256-CBC encryption | Secure save files with CBC mode | **Implemented** |
| HMAC-SHA256 tamper protection | Integrity verification with separate auth key | **Implemented** |
| Per-install key generation | Random key at `user://saves/.savekey` | **Implemented** |
| Atomic write + backup | Temp-swap and `.bak` recovery | **Implemented** |
| Three story slots | Create, resume, delete with confirmation | **Implemented** |
| Corruption preservation | `.corrupt` extension preservation for recovery | **Implemented** |
| Legacy migration | Forward migration from older schemas | **Implemented** |
| Future schema rejection | Reject newer saves without rewriting | **Implemented** |
| Autosave at checkpoints/level completion | Silent background saves | **Implemented** |
| Slot summaries in save select | Character portrait, level, playtime, timestamp display | **Implemented** |
| Global statistics persistence | Wins, losses, per-character stats, playtime | **Implemented** |

**Status: Substantially complete.**

---

## 8. Hub World (Archive Time-Ship)

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| Hub scene | Navigable 2D side-scrolling environment | **Partially implemented** - `HubWorld.tscn` exists with prototype geometry |
| Chronal Repository terminal | Interact to open Resonance Grid UI | **Partially implemented** - Interaction trigger exists |
| Holodeck Arena Console | Configure and start CPU practice matches from hub | **Not implemented** - Fighter matches start from TestArena flow, not hub |
| NPC interactions & dialogue | Resistance crew with dialogue trees and portraits | **Not implemented** |
| Temporal Portal | Sequential level activation and travel | **Partially implemented** - Portal auto-activates (interim behavior per design doc) |
| NPC-gated portal activation | Talk to key NPCs to charge the portal | **Not implemented** (explicitly deferred in design doc) |
| Hub world spatial layout | Room dimensions, area connections, walking paths | **Not implemented** - Uses placeholder geometry |
| Calibration Bay spawn anchor | Defined spawn point on hub return | **Partially implemented** |

---

## 9. Combat Systems

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| 3-hit basic combo | Frame timings, damage multipliers (0.8×/1.0×/1.5×) | **Implemented** |
| Block system (3 charges) | Basic consumes 1, special shatters all 3, 5s cooldown on break | **Implemented** |
| Block charge regeneration | 1 charge per 2.0s in all states except Blocking and Dead | **Implemented** |
| Ultimate meter (0-100) | Build rates (1.0 per dealt, 0.25 per taken), death carryover (75%) | **Implemented** |
| Status effects (5 types) | TimeDilation, Venom, StaticCharge, RadiantBurn, Root | **Implemented** - Strategy pattern with newest-overwrite |
| Knockback formula | `baseKnockback / (1 + weight)` | **Implemented** |
| Cooldown system | 10s flat for specials, 5s for movement ability | **Implemented** |
| Damage numbers | Floating pooled damage text with shimmer | **Implemented** - Pooled with configurable visibility |
| Combatant pushboxes | Soft horizontal jostling, non-solid hurtboxes | **Implemented** - Both Story and Fighter modes |
| Universal dash | 12-frame, 1.35× speed, no invulnerability, 4-frame commitment | **Removed 2026-08-09 by user decision** (supersedes the design spec; roll and character movement abilities are the mobility tools) |
| Universal evasive roll | 4 startup / 12 travel / 10 recovery, first 8 invulnerable | **Implemented** |
| Ledge hanging | 5-second hang limit, pull up/drop/jump off, single occupancy | **Implemented with placeholder geometry** - Reusable ledge points, occupancy, response, and representative Tutorial placements exist |
| One-way platform drop-through | Double-tap down, 0.25s collision disable, state restrictions | **Implemented with placeholder geometry** - Authored template and representative Tutorial platform exist |
| Aerial combat | Independent air combo counter, landing cancel, no lockout | **Implemented** - Independent counters, authored placeholder frame tracks, landing cancel, and interruption tests exist |
| Hyper-armor mechanics | Damage taken but no hitstun/knockback during flagged abilities | **Implemented mechanically** - Story and Fighter interruption rules are tested; final shell shader remains presentation work |
| Cancel rules | Special cancels basic; block cancels recovery only | **Implemented** |
| Crouch attacks | Same combo string with -30% hurtbox height | **Implemented** |

---

## 10. Chronal Rewind System (Story Mode)

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| 300-frame circular buffer | 60 Hz recording of position/grounded/facing/animation | **Implemented** |
| Safe grounded frame search | Reverse scan with dynamic expansion beyond 300 frames | **Implemented** |
| Last-known grounded fallback | Retained beyond normal buffer capacity | **Implemented** |
| Difficulty-based pools (5/3/1) | HP restore 100%/50%/30% | **Implemented** |
| Checkpoint refill rules | Easy: full reset, Normal: +1 capped, Hard: none | **Implemented** |
| 4× reverse replay animation | Backward interpolated movement at high speed | **Implemented** |
| Enemy freeze during rewind | All enemies frozen (`timeScale = 0`) | **Implemented** - Active enemy and construct simulation participants freeze, then resume without changing enemy positions |
| Projectile clearing on rewind | All active enemy projectiles instantly despawned | **Implemented** - Grouped pooled and fallback projectiles are cleared at rewind start |
| Post-rewind invincibility | 2.0 seconds of spawn invincibility | **Implemented** - Explicit 120-frame immunity covers regular and persistent damage |
| Timeline Collapse flow | Hub respawn + 20% dust penalty + level restart option | **Implemented with placeholder UI** - Hub portal offers Timeline Anchor or full-level restart and restores HP/rewinds |
| Rewind visual effects | 50% opacity blue-tint ghost trail, chromatic aberration, scanlines | **Presentation contract implemented** - Typed phase events expose ghost/tint/scanline hooks; final effects remain Package 8 |
| Rewind audio | BGM duck -12dB, reverse tape sweep, ticking clock | **Presentation contract implemented** - Typed events expose -12 dB duck, sweep, and ticking hooks; final audio remains Package 8 |

---

## 11. UI & Menus

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
Package 8 (2026-08-08) converted every production screen from a code-built shell to an authored,
themed, focus-authored scene against one shared `Theme` (`resources/UI/ftt_theme.tres`) and one
palette (`scripts/UI/UIPalette.cs`), pinned to each other by test. Presentation is placeholder
**art**, not placeholder structure.

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| Shared UI theme | One Theme resource driving every screen | **Implemented** - `ftt_theme.tres` + `UIPalette`; no font resource by design (`assets/fonts/` is empty) |
| Controller/keyboard focus | Authored focus order and initial focus on every screen | **Implemented** - `FocusChainBuilder`, including `SpinBox` internal editors |
| Main Menu | Story Mode, Fighter Mode, Settings, Quit with confirmation | **Implemented** - Authored four-screen stack (root/slot/character/difficulty) with one back/cancel rule |
| Character Select Screen | Grid portraits, ready toggle, 3s countdown, duplicate prevention | **Implemented** - Nine real focusable Button tiles (previously mouse-only PanelContainers) |
| Stage Select Screen | Carousel/grid of 10 arenas with preview | **Implemented** - Ten-stage catalog with localized names and preview plate |
| Settings Menu | Audio, Display, Controls, Gameplay sections | **Implemented** - Authored `Settings.tscn`, four tabs, embedded by all three openers; never writes `SceneTree.Paused` |
| Pause Menu | Story and Fighter variants with correct options | **Implemented** - Shared `PauseMenuBase` owns the pause and hands it back in `_ExitTree`; Story variant adds an exit confirmation |
| Story Mode HUD | HP, meter, rewinds, block charges, cooldowns, status, currency | **Implemented** - Authored `StoryHUD.tscn`, cooldown + status indicators, live `HudOpacity` |
| Fighter Mode HUD | HP bars depleting to center, stocks, timer, block, status, meter | **Implemented** - Authored `FighterHUD.tscn` (bars, stock pips, portraits, block charges, cooldown slots, clock, status); debug text moved behind an F3 toggle |
| Dialogue box system | Typewriter reveal at 30 chars/sec, portraits, speaker names | **Implemented** - Glass panel treatment, emotion-driven portrait framing, punctuation pacing, per-character chirps |
| Resonance Grid UI | Constellation navigation with purchase flow | **Implemented** - Authored `ResonanceGrid.tscn` shell; node buttons stay code-built from the character's authored grid |
| Save Select Screen | Slot summaries, new/load/delete with confirmation | **Implemented** - Authored rows; deletion confirmed through the shared modal |
| Difficulty Select | Easy/Normal/Hard with description tooltips | **Implemented** |
| Loading screen overlay | Story portal effect / Fighter VS matchup cards | **Implemented** - Themed per-transition scene chosen from the destination path; portal and VS variants; 2 s minimum retained. Production VFX for the portal is Package 10 |
| Post-match results flow | KO freeze/slow-mo/spotlight, results screen, rematch | **Implemented** - Themed banners and results; cinematic KO art remains Package 10 |
| Quit confirmation modal | "Are you sure?" with Confirm/Cancel | **Implemented** - Shared `ConfirmModal` replaced three ad-hoc patterns and both native `ConfirmationDialog` windows |
| Boss health bar | Top-center 50% width bar with name and phase notches | **Implemented** - Notches are the authored `PhaseThresholds` themselves, not a derived split |
| Enemy overhead health bars | Floating bars above damaged/aggroed enemies | **Implemented** - Hidden until first damage, fade after four quiet seconds, reset on both halves of the pool cycle and on rewind |
| Level completion overlay | Dust earned, time, auto-deposit, return prompt | **Implemented** - Dust, completion time and rewinds used (both statistics are new on `StoryManager`) |
| Network select / online pause / network error | Online lobby, pause and error surfaces | **Not implemented** - deferred with Package 7; manifest rows stay `Planned` |

---

## 12. Audio & Music

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
Package 8 built the whole audio framework and filled it with placeholder content. Every row below
that says "Implemented" means the *system and its wiring* are real and tested; the audio itself is
three shared placeholder stems and a handful of synthetic `.ogg` cues generated by a committed
script (`tools/generate_placeholder_audio.py`). Production audio replaces the files in place with
no code change — that is the exit criterion this package was measured against, not fidelity.

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| AudioServer bus hierarchy | Master → Music/SFX/UI with sub-buses (Environmental/Combat/Movement) | **Implemented** - Authored `resources/Audio/default_bus_layout.tres`, registered in project settings; the orphaned runtime `Ambient` bus removed |
| Dynamic music stems (3 layers) | Ambient, Combat, Boss stems per level with crossfade | **Implemented (placeholder audio)** - `StemDirector` plays all 27 authored `StageAudioSet`s synchronized; the mix is **additive** (ambient always audible) per the design's vertical layering |
| Intensity transitions | Ambient ↔ combat ↔ climax with authored crossfade | **Implemented** - Story: engaged enemies within 1000 px at 4 Hz, four-second fall-back hold; Fighter: last-stock climax, one-way, mode-keyed |
| Horizontal transitions | Last-stock tempo change, KO stinger, silence | **Implemented (placeholder audio)** - KO stinger, victory fanfare and countdown blips now resolve; they had been silently no-oping since Package 6 because the asset paths did not exist |
| Surface-specific footstep SFX | Wood, stone, sand, metal, snow variants | **Partially implemented** - Story footsteps are distance-based with an immediate first step and a surface parameter plumbed through, on one placeholder sound. **Fighter Mode has no footsteps at all** — the driver disables the presentation bodies' `ProcessMode`; deferred to Package 10 |
| Character-specific attack SFX | Per-ability cast and impact sounds | **Partially implemented** - The `PresentationEventID` → SFX binding covers telegraph/active/death for all 42 roster entries on generic cues; per-ability character audio is Package 10 |
| Dialogue text chirps | Character-pitched blip sounds (lower for Lincoln, electric for Tesla) | **Implemented (placeholder audio)** - Every third non-whitespace character, silent during punctuation holds; nine authored `DialogueChirpPitch` values 0.72–1.34 pitch-shifting one sample. Per-character *waveforms* are Package 10 |
| Audio snapshots | Low health filter, pause duck, ultimate duck, normal gameplay | **Implemented** - `AudioSnapshotMixer` blends volume offsets and low-pass cutoffs (cutoffs stack by minimum, not sum); the rewind duck migrated onto it, fixing a bug where a volume change during a rewind was reverted when it ended |
| SFX pool | Fixed-size reusable players | **Implemented** - 24 prewarmed players with oldest-steal, now tested; `ReleaseAllVoices()` added for scene changes |
| Boot-time volume application | Saved sliders applied before first audio | **Implemented** - `AudioManager._Ready` reads `GlobalData` (autoload order permits it) |
| Reverb zones | Per-area AudioEffectReverb (caves, cathedrals) | **Not implemented** - no content declares one; the bus layout can carry them |
| Low-pass filtering | Pause menus, low health, underwater | **Partially implemented** - Music and SFX carry filters at a transparent 20500 Hz cutoff so a snapshot can lower them without an audible effect-toggle click; pause and low-health drive them. Underwater is not authored |

---

## 13. Visual Art & Shaders

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
Package 8 built the visual *framework*. Nothing in this section has been looked at by a human:
headless gates prove the shader compiles, the arbiter resolves the right state, the parallax
migrated, and every ambient tone clears a readability floor — none of them can judge how it looks.

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| Hand-drawn 2D sprite sheets | Full animation sets per character (17+ sheets each) | **Not implemented** - per-character placeholder `SpriteFrames` wired through `CharacterData`; production art is a resource reassignment (Package 10) |
| Outline/glow shader system | Alpha-based edge detection with configurable parameters | **Implemented** - `assets/shaders/outline_glow.gdshader` with the design's four uniforms. Two `gl_compatibility` limits found and test-guarded: no `instance uniform`s (the base material is duplicated per entity by the arbiter) and `TEXTURE` cannot be passed as a `sampler2D` argument (all ring sampling inlined). A `CanvasItem` shader cannot draw outside its quad, so production art must budget a few texels of transparent padding |
| Glow/tint arbitration | Priority: hyper-armor/spawn-invuln > status > slot colour | **Implemented** - `GlowPresentationController` is the single owner of sprite material/tint state, with three independent channels (base tint, tint override, arbitrated outline stack) so a fighter tint survives a status ending |
| Status effect visual indicators | Per-status ShaderMaterial + GPUParticles2D | **Implemented** - driven by `OnStatusEffectApplied`/the new `OnStatusEffectCleared` falling edge (the bus previously had a rising edge only, so a status outline could be set but never cleared); Venom's authored two-colour gradient is a per-frame lerp |
| Hyper-armor shell / hit feedback | Gold shell, hit flash, camera shake, haptics | **Implemented** - the gold shell replaced the `ChronalArmorOverlay` ColorRect; Fighter-mode shake and haptics wired from driver HP deltas and KOs |
| PointLight2D glow accompaniment | Ambient environmental light cast matching outline color | **Implemented** - textured; the repository's one pre-existing light had been emitting nothing at all for want of a texture |
| Normal-mapped sprites | Light2D interaction for 2D lighting | **Not implemented** - nothing authored consumes a normal map; Package 10 with production art |
| Shadow casting | `LightOccluder2D` geometry | **Not implemented** - no occluder geometry is authored anywhere |
| Parallax backgrounds per level | Multi-layer scrolling historical backgrounds | **Implemented (placeholder art)** - nine Fighter stages carry authored `Parallax2D` layers (migrated from `ParallaxBackground` so they sit in canvas layer 0 and are lit; they now zoom with the camera, which is a real visual change nobody has reviewed). Florence dresses distance with a static sprite by design. Campaign levels have no parallax |
| Environment lighting | `CanvasModulate` ambient tone + per-era key/fill/rim lights | **Implemented (thirteen scenes)** - `StageLightingRig` on all ten Fighter stages plus the hub and levels 2 and 6; ambient channels held above 0.55 because the placeholder character silhouettes have no rim art to survive a crush. The other thirteen campaign scenes are unlit — the rig is the pattern |
| Tile palettes per era | Historical-themed tilesets with physics layers | **Not implemented** |
| Environment VFX | Steam, lava, sand, electricity GPUParticles2D | **Implemented (placeholder art)** - six reusable family scenes (`Burst/Slash/Beam/Shockwave/Summon/Impact`) carry all 78 hooks: 36 ability cast/impact assignments derived from `ExecutionType`, and 42 roster `PresentationEventID` mappings resolved by verb token. Recovery deliberately draws nothing |
| Particle budget | 500 active particles | **Implemented** - shared registry with steal-oldest eviction; a request larger than the whole budget is refused outright |
| Off-screen suspension | Suspend animation/VFX outside the view | **Implemented** - `PresentationVisibilitySuspender`, presentation nodes only |
| Character portraits (512×512) | For character select and HUD panels | **Partially implemented** - placeholder portraits wired through `CharacterData` and consumed by the Fighter HUD and dialogue box |
| Enemy/boss sprite sheets | Per-type idle/patrol/attack/hit/death animations | **Not implemented** - authored scenes with `PlaceholderTint`/`SpriteFramesResource` |
| Chronal Orb sprites | 4 types with distinct colored glow effects | **Implemented (placeholder art)** - `ChronalOrbData.Icon` finally has a reader; `ChronalOrbItem.EffectColor` is now the single colour source shared by the Story item and the Fighter proxy. The template is deliberately not in any pool config |
| Chronal Extractor props | Idle/damaged/destroyed animation states | **Implemented (placeholder art)** - damaged/destroyed dressing in the template |
| Rewind treatment | Ghost trail, reverse sweep, clock tick, tint, music duck | **Implemented (placeholder art)** - the three dead payload fields are consumed at last; `RewindCueState` owns the edges, since the manager republishes `Playback` every frame |

---

## 14. Dialogue & Narrative

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| Dialogue data schema | `DialogueSequenceData` + `DialogueEntry` resources | **Partially implemented** - `DialogueManager` script exists |
| Level 0 calibration dialogue | Full scripted tutorial conversation with Commander Sarah | **Not implemented** - No authored dialogue resources |
| Level 1 Florence entrance/exit dialogue | Pre/post-level narrative exchanges | **Not implemented** |
| Act II/III narrative dialogue | Mid-game revelations and ending scripts | **Not implemented** |
| NPC dialogue trees | Hub Resistance crew conversations | **Not implemented** |
| Character-specific narrative variations | Per-character lines in their "home" era | **Not implemented** (deferred in design doc) |
| Glassmorphism dialogue box styling | Dark cyan/violet container with brass/gold trim | **Not implemented** |
| Portrait emotion variations | Neutral, Determined, Shocked, Injured per character | **Not implemented** |
| Gameplay suspension during dialogue | Physics/combat paused, UI navigation active | **Partially implemented** - Logic exists in DialogueManager |

---

## 15. Puzzles & Environmental Interaction

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| PuzzleManager system | Event-driven puzzle completion triggers with signals | **Implemented** - Stable IDs, prerequisites, completion/reset events, save/checkpoint state, and rewind policies exist |
| Gear rotation puzzles (Florence) | Hit lever to rotate platforms 90° | **Implemented with placeholders** - Florence contains a lever-driven two-platform gear puzzle |
| Weight/physics puzzles | Pressure plates, counterweights, apples | **Implemented as reusable toolkit** |
| Logic routing puzzles (Chicago) | Energy beam bouncing through conductive coils | **Implemented as reusable toolkit** - Emitters, rotatable coils, routing nodes, and receivers are ready for level authoring |
| Chronal Extractors | 100 HP destructible with hazard emissions and 25 dust reward | **Implemented as reusable template** |
| Destructible blocks | Breakable by attacks, 1-3 hits to destroy | **Implemented as reusable template** |
| Interactive objects (levers, chests) | `IInteractable` interface with floating prompt | **Implemented** - Common contract, interaction area/prompt, lever, and chest adapters exist |
| Room transitions | Camera confiner boundary updates on room change | **Implemented as reusable template** |
| Crumbling platforms | 0.8s shake + 1.2s collapse, 5s respawn | **Implemented** - Florence contains representative placeholder instances |
| Cyclic hazards | Swinging pendulums, intermittent geysers, rhythmic flames | **Implemented as configurable toolkit** - Florence includes a representative steam geyser |
| Temporal/Chronal Rift zones | Time Dilation field with "Time-Loop Snap" reset | **Implemented as reusable template** |

---

## 16. Object Pooling

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| PoolManager autoload | Registry with warm-up, overflow policies, and release | **Implemented** |
| IPoolable interface | OnSpawn/OnDespawn lifecycle with reset contracts | **Implemented** |
| Damage number pool | 50 warm / 100 max / Grow | **Implemented** |
| SFX pool | 24 reusable AudioStreamPlayers | **Implemented** |
| Fighter presentation proxies | Entity render nodes | **Implemented** - 112 prewarmed |
| Projectile pools | 20 per character / 50 max / Grow | **Implemented for current Story/Fighter paths** - Story projectile and zone templates use stable pool IDs and reset contracts |
| VFX/particle pools | 30 per character / 100 max / RecycleOldest | **Implemented as replaceable placeholder pools** |
| Enemy mob pools | 10 per type / 25 max / Grow | **Implemented** - Shared `standard_enemy`/`elite_enemy`/`enemy_projectile` pools over authored scenes; `EnemyFactory` is generic over any roster ID and reuses reset controllers |
| Loot/currency drop pools | 30 warm / 50 max / Reject | **Implemented** - Separate Dust and helper-item pools use Reject overflow |
| ScenePoolConfig per level | Data-driven warm-up definitions per scene | **Implemented** - Tutorial, Florence, Test Arena, and all fourteen campaign levels 2-15 are cataloged and budget-validated; `ScenePoolConfigTests` asserts every campaign scene resolves to its own `ConfigID` |

---

## 17. Input & Controls

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| InputMap actions (12 defined) | Move, Jump, Down, BasicAttack, Special1/2, MovementAbility, Block, Roll, Ultimate, Interact, Pause | **Implemented** (Dash retired with the universal dash, 2026-08-09) |
| Per-player device assignment | Local 1v1 controller separation via player index | **Implemented** |
| Dash input detection | Digital double-tap (15 frames) / analog flick (85% magnitude) | **Removed 2026-08-09 by user decision** (supersedes the design spec; roll and character movement abilities are the mobility tools) |
| Roll dedicated input | `O` keyboard / Right Trigger controller | **Implemented** |
| Serializable PlayerInputFrame | Deterministic per-player input capture for replay/network | **Implemented** |
| Full input remapping | Serialized keybinding overrides via InputMap API | **Implemented** - Controls tab with listen-for-input capture; typed multi-event `InputBindingSet` (physical keycodes, so a remap survives a keyboard-layout change) in the global payload at schema v4; only actions that differ from `project.godot` are persisted, so a later default change still reaches players who never touched that action. Restored into `InputMap` by `SaveManager` after global load, before gameplay |
| Binding conflict UX | Explain and prevent a duplicate binding | **Implemented** - Conflicts **block** rather than swap (a swap would move a binding the player never asked to change) and name the owning action; each row carries one slot per device kind, so a key remap never clears the joypad binding |
| Non-remappable inputs | Inputs that a per-event row cannot express | **Implemented as read-only** - the `gameplay_ultimate` LB+RB chord is evaluated as a conjunction of the action's joypad events and is stated in the UI rather than hidden (the derived Dash gesture was removed with the universal dash, 2026-08-09) |
| Controller haptics | Vibration on hit/block/KO/heavy-landing/hazard events | **Implemented** - `HapticFeedbackManager` with intensity settings. Two device bugs fixed in Package 8: damage vibrated hardcoded device 0 (so in local 1v1 player two's damage buzzed player one's pad) and guard-break passed a *player index* as a device id; both now route through `InputManager.GetDeviceForPlayer` |
| Reset-to-default controls | Restore all bindings to factory defaults | **Implemented** - per-action and global, via `InputMap.LoadFromProjectSettings()` plus clearing the saved overrides |

---

## 18. Accessibility & Settings

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| Master/Music/SFX volume sliders | Mapped to AudioServer buses (default 100%/80%/100%) | **Implemented** |
| Boot-time application | Saved volumes and display settings applied before first frame/audio | **Implemented** - volumes in `AudioManager._Ready`, display in `ViewportEnforcer` (the last autoload, so the global payload is already loaded) |
| Resolution dropdown | Populated from DisplayServer | **Implemented** - persisted as two ints and snapped onto `GlobalSaveData.SupportedResolutions`, so a corrupt payload cannot ask the engine for a 0×0 window |
| Screen mode toggle | Fullscreen / Borderless / Windowed | **Implemented** - three-value `WindowMode` enum replacing the old boolean fullscreen flag; DisplayServer calls skipped under `headless` |
| VSync toggle | Enable/disable vertical sync | **Implemented** - persisted |
| Damage number toggle | Show/hide floating combat text | **Implemented** |
| HUD opacity slider | Alpha control for on-screen bars | **Implemented** - and now honoured **live** on both HUDs. It is polled once per frame rather than evented, deliberately: there is no settings-changed signal in the project, and polling also picks up a change made by a save load or migration |
| Screen shake slider | 0.0-1.0 intensity multiplier | **Implemented** |
| Haptics toggle + intensity | On/off master + 0-100% slider | **Implemented** |
| Input remapping persistence | Serialized in GlobalSaveData | **Implemented** - see §17; global payload schema v3 → v4 with a migration branch that drops the never-written string map rather than inventing player intent |
| Save recovery notices | Tell the player when a save was recovered or migrated | **Implemented** - the eight raw-English notices became localized `save_notice_*` key + args pairs and are surfaced on the main menu |
| Dead settings removed | No disabled or non-functional options shown | **Implemented** - the global default-difficulty dropdown had no consumer (campaign difficulty is per-slot and locked at creation) and was removed rather than wired up |
| Colorblind modes | Protanopia/Deuteranopia/Tritanopia shader presets | **Not implemented** (deferred in design doc) |
| Screen reader support | Text-to-speech for menus/HUD/dialogue | **Not implemented** (deferred in design doc) |
| Alternative input layouts | Tap-to-hold, toggle sprint, deadzone sliders | **Not implemented** (deferred in design doc) |

---

## 19. Localization

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| Translation key architecture | `Tr("key")` / `TranslationServer.Translate()` for all visible text | **Implemented** - Registered in `project.godot` |
| English translation file | `localization/en.csv` with key-value pairs | **Implemented** |
| Key screens localized | Menus, HUD, select screens, settings | **Implemented** - every production screen; authored `.tscn`s store the raw key as the control's `text` and let Godot's automatic control translation resolve it, so a language change follows without rebuilding a surface |
| Hardcoded copy audit | All prototype UI cleaned of raw English strings | **Implemented** - zero raw-English visible strings remain in `scripts/` or `scenes/`; the last offenders (settings tab titles, save-load notices, a `Player.tscn` debug label, an unreferenced `TestScene.tscn`) were fixed or deleted in Package 8 |
| Missing-key validation | Fail the build on a key that does not resolve | **Implemented** - `ScriptTranslationKeyTests` sweeps every `Tr("...")` and `TranslationServer.Translate("...")` literal under `scripts/`; `SceneVisibleTextTests` sweeps every visible `text` in an authored `.tscn`. Both resolve through the **compiled** `en.en.translation`, because `--headless --quit` does not regenerate it and a CSV ahead of the compiled resource renders raw keys while looking perfect in review |
| Unused-key validation | Report keys nothing references | **Implemented** - `UnusedTranslationKeyTests` fails on a *new* orphan and lets a recorded one be retired freely; twelve pre-Package-8 orphans are on the recorded roster, and two Package-8-caused ones were deleted outright |
| Multi-language support | ES, FR, DE, JA, etc. with `.translation` resources | **Not implemented** (deferred in design doc) |
| Language selection UI | Dropdown in settings | **Not implemented** (deferred in design doc) |
| CJK font fallback | SystemFont or imported TTF/OTF with fallback chain | **Not implemented** (deferred in design doc) |

---

## 20. Testing & QA

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| GdUnit4 test framework | Automated headless C# tests | **Implemented** - 1293 passing tests (Package 8 close, 2026-08-08) |
| Content validation tests | Character/ability/enemy/stage manifest completeness | **Implemented** |
| Combat formula tests | Damage calc, block rules, meter build/use | **Implemented** |
| Determinism/rollback tests | Hash consistency, prediction, correction convergence | **Implemented** |
| Save system tests | Encryption, migration, corruption, tamper rejection, atomic backup | **Implemented** |
| Input/collision tests | Roll bindings, pushbox geometry (dash-gesture tests removed with the mechanic, 2026-08-09) | **Implemented** |
| Resonance grid tests | All nine grids, prerequisites, isolation from Fighter | **Implemented** |
| Localization tests | Key registration validation | **Implemented** |
| FSM state validation | Simulated inputs + state assertions | **Partially implemented** |
| Performance profiling | Frame budget audit against 16.67ms target | **Not implemented** |
| Platform validation | Windows/macOS/Linux/Steam Deck exports | **Not implemented** |
| Soak/balancing testing | Extended gameplay sessions, AI tuning | **Not implemented** |
| Network simulation tests | Latency, jitter, reorder, loss, disconnect | **Not implemented** |

---

## 21. CPU Fighter AI

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| Utility AI decision engine | Score-based action evaluation every 3 frames | **Implemented** - `FighterCpuController` exists |
| Easy difficulty (30-45 frame reaction) | Basic attacks only, no specials/ultimate, no recovery | **Implemented** |
| Medium difficulty (15-20 frame reaction) | Uses specials, basic recovery, 40% block rate | **Implemented** |
| Hard difficulty (4-8 frame reaction) | Frame-perfect combos, movement ability evasion, 80% block | **Implemented** |
| Chronal Orb pickup behavior | Difficulty-scaled chance to path toward orbs | **Implemented** (Package 6) - the controller sees the nearest live orb through `ICpuWorldObserver` and pursues it when safe, at a per-band rate |
| Hazard avoidance | Difficulty-scaled reaction to warning/active zones | **Implemented** (Package 6) - per `design-godot.md` Section 10 rather than a uniform scaling: Hard vacates during the 90-frame telegraph, Normal reacts only once the hazard is damaging, and Easy deliberately walks into both phases |
| Recovery behavior | Off-stage return using jumps + movement ability + Special 2 | **Implemented** (Package 6) - steers toward centre, spends jumps, and aims the movement ability upward (`MoveY` was hardcoded to 0 before). All three bands issue the commands; only Normal and Hard are asserted to make it back, because Easy's 30-45 frame reflex window is longer than the fall to the blast zone |
| Button edges | Attacks, jumps and dashes read from the `Pressed` edge | **Implemented** (Package 6) - CPU buttons are now pulsed (press, hold, release, gap) rather than latched. Latched buttons meant a CPU that kept choosing "attack" produced exactly one attack per match and could never double jump |
| Per-band tuning | Design difficulty matrices | **Implemented** (Package 6) - lifted out of inline literals into a `CpuBandTuning` table; Special 1 and the defensive block/roll branch used to be Hard-only regardless of band |
| Seed | Reproducible per match | **Implemented** (Package 6) - derived from the match seed and player slot instead of a literal `2026` |

---

## 22. Fighter Mode Match Flow

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| Match modes | Stock / Time Limit / Hybrid | **Implemented** |
| Stock configuration | 1-5 stocks (default 3) | **Implemented** |
| Timer resolution | Stocks → HP percentage → Draw | **Implemented** |
| Draw/Tie behavior | Not logged as win/loss, "DRAW" text stamp | **Implemented** |
| Chronal Orb spawning | Off/Low/Medium/High frequency with deterministic collection | **Implemented** |
| Stage hazards | Off/Low/Medium/High frequency with warning/active phases | **Implemented** |
| Pre-match countdown | 3-2-1-GO before inputs are live | **Implemented** (Package 6) - deterministic `MatchState = 0` for 180 frames plus a 30-frame GO window; the state ticks and enters every snapshot and hash, but gameplay input is discarded until the match goes live |
| Respawn platform | 5s dissolve timer, all inputs disabled, invulnerability on drop | **Implemented** (Package 6) - deterministic simulation state: 300 frames at (0, +3.0) with all inputs locked, a 30-frame grace window, then 180 frames of invulnerability counted from the drop. Roll i-frames stay distinct. This replaced an instant ground teleport plus a flat 120 i-frames |
| Bottom-fall stock loss | Falling through the bottom blast zone costs a stock | **Implemented** (Package 6) - this was **unreachable** before: on any stage whose floor spans the full width the ground snap ran before the blast-zone check and teleported the fighter back up. The end condition was nominal until the movement system's tail was reordered |
| KO sequence | Hit-freeze, slow-motion, spotlight, KO stamp, victory pose | **Implemented, placeholder art** (Package 6) - driver-side hit-freeze, slow motion (paced `Advance` calls, never `Engine.TimeScale`), camera focus, localized KO/DRAW stamp, winner-pose hold, then the results transition, published as `EventBus` phases so Package 8 can restyle without touching flow. A test advances an identical simulation without the driver and compares hashes, proving deterministic state is untouched. Cinematic art and the fanfare stems are Package 8 |
| Pause and controller disconnect | Forced pause naming the disconnected player, reconnect assignment, safe exit | **Implemented** (Package 6) - authored `LocalFighterPause.tscn` with `SceneTree.Paused` released in `_ExitTree`; `InputManager` no longer reshuffles both fighters' pads when any device is plugged in mid-match |
| Results screen | Per-end-condition treatment | **Implemented** (Package 6) - authored `MatchResults.tscn` replacing the code-built panel; the raw `ui_cancel` instant-exit is gone |
| Rematch flow | Rematch / Change Fighters / Return to Menu | **Implemented** - rematch now resolves through the catalog `ScenePath` for the selected stage rather than the hardcoded Test Arena |
| Win/loss statistics | Global and per-character tracking | **Implemented** - `CharacterWins`/`CharacterLosses` alongside the player-one-centric totals; a true tie writes none of them |
| Match settings persistence | Stock count, timer, items, hazards configurable | **Implemented, session-scoped** - Off/Low/Medium/High item and hazard granularity is exposed in select (it was collapsed to a binary), and every field survives select -> match -> rematch -> match through `GameManager.CurrentSession`. Deliberately **not** written to the save schema in this package (plan Section 2.6) |
| Rollback readiness | Snapshots, hashes, delayed-input convergence, bounded history, depth-7 resimulation in budget | **Implemented and passing** (Package 6) - see `docs/PERFORMANCE_BASELINE.md` for the per-stage table; this is Package 7's entry criterion |

---

## 23. Story Mode Campaign Flow

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| Character lock per slot | Selected character locked for entire campaign | **Implemented** |
| Difficulty selection | Easy/Normal/Hard with scaling | **Implemented** |
| Linear sequential progression | Level 0 → 1 → 2 → ... → 15 | **Implemented** in routing; only 0-1 have scenes |
| Checkpoint system | 2-3 per level, attack to activate, visual state change | **Partially implemented** - Checkpoint logic exists; authored placement only in Tutorial/Florence |
| Level completion flow | Boss defeat → dialogue → results → auto-deposit → hub return | **Not implemented** - Flow logic partially exists but no complete content |
| Campaign completion | Level 15 defeat → ending → credits → save marked complete | **Not implemented** |
| Hub return spawn | Calibration Bay anchor in front of Repository | **Partially implemented** |
| Chronal Dust deposit | Auto-deposit on hub return, available for grid spending | **Implemented** |

---

## 24. Items & Power-Up System

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| Fighter Mode Chronal Orbs | 4 types: Restoration, Haste, Uplift, Aegis | **Implemented** - Deterministic spawning and effects in FighterSim |
| Story Mode enemy drops | Guaranteed Chronal Dust + random healing/buff items | **Partially implemented** - Dust drops exist; healing/buff items not implemented |
| Healing pickup (difficulty-scaled) | Easy 50 HP / Normal 25 HP / Hard 10 HP | **Not implemented** |
| Temporary buff pickups | Damage/Speed boosts with difficulty-scaled magnitude/duration | **Not implemented** |
| Auto-collect radius | 2.5 units with magnetize interpolation at 15 units/sec | **Partially implemented** |
| Drop visual tiers | Small/Medium/Large sprites based on quantity | **Not implemented** |

---

## 25. Performance & Technical Targets

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| Locked 60 FPS | 16.67ms frame budget | **Configured** - 60 Hz physics tick set; not profiled against budget |
| Fixed 16:9 viewport | 1920×1080 with letterbox/pillarbox | **Implemented** - `ViewportEnforcer` with `keep` aspect |
| Physics budget (2.5ms) | CharacterBody2D + shape queries | **Not measured** |
| AI budget (3.5ms) | Paced pathfinding (every 5-10 frames) | **Not measured** |
| Rendering budget (8ms) | ≤150 draw batches, ≤150K vertices | **Not measured** |
| RAM budget (4GB max) | No GC spikes during gameplay | **Not measured** |
| Active sprite limit (60) | Off-screen animation disabled via VisibilityNotifier2D | **Not implemented** |
| Active particle limit (500) | Emission caps per emitter | **Not implemented** |
| Object pooling coverage | All high-frequency objects pooled | **Partially implemented** - Core, enemy, elite, enemy-projectile, VFX, story-projectile, and loot pools all exist with reset contracts; boss/hazard scenes added during level production must still be brought into them |

---

## Top Priority Gaps (Blockers for a Playable Build)

1. **No production character art** - All characters are colored rectangles with no animation tracks
2. **No campaign levels beyond Florence** - 14 levels with unique environments, enemies, and puzzles remain
3. **No independently authored Fighter arenas** - 10 stages share one template scene
4. **No production audio/music** - No BGM stems, no character SFX, no environmental audio
5. **No bespoke Resonance perk behavior** - 27 major perks are data-only; not wired to gameplay
6. **No dialogue content authored** - Scripts documented in design but no resources in project
7. **No puzzle system** - Core Story Mode gameplay mechanic doesn't exist
8. **No enemy or boss production art** - All 42 roster entries render as tinted placeholder silhouettes; the data, AI, abilities, and encounter flow are complete (Package 4)
9. ~~**No roster placement in campaign content**~~ - **Closed (Package 5, 2026-08-08).** All 27 enemies and 15 bosses are placed across campaign levels 1-15 at the locked encounter economy counts
10. **No online multiplayer UI or infrastructure** - Transport layer exists but no user-facing flow

---

## What IS Working Well

The project has strong architectural foundations that will support scaling:

- **Deterministic Fighter simulation** - Klotho FP64 with full snapshots, hashes, prediction, rollback, and resimulation
- **Secure save system** - AES-256-CBC, HMAC-SHA256, atomic writes, backup recovery, migration, corruption handling
- **Input architecture** - Per-player serializable input frames with device isolation (dash gesture detection removed with the universal dash, 2026-08-09)
- **Combat formulas** - Typed hits, canonical damage/knockback/block/meter/status rules tested
- **Object pooling** - PoolManager with IPoolable lifecycle, overflow policies, and warm-up
- **Event-driven architecture** - EventBus decouples combat, HUD, audio, and save systems
- **Collision system** - Named layers 1-11, centralized constants, validated matrix
- **Autoload lifecycle** - 11 persistent managers with proper Instance patterns
- **Test coverage** - 74 automated tests covering combat, saves, determinism, content validation, and localization
- **Localization readiness** - Translation key architecture with English CSV and registered `.translation` resource

---

## Codebase Statistics

| Directory | File Type | Count |
|-----------|-----------|-------|
| `scripts/` | `.cs` | 84 |
| `scenes/` | `.tscn` | 9 |
| `resources/` | `.tres` | 61 |
| `tests/` | `.cs` | 24 |
| **Total** | | **178** |

### Scripts by Area

| Area | Files | Key Scripts |
|------|-------|-------------|
| Core | 16 | GameManager, SaveManager, AudioManager, InputManager, EventBus, PoolManager, etc. |
| Characters | 4 | CharacterData, CharacterFactory, PlayerController, TrainingDummy |
| Characters/Abilities | 12 | Per-character ability implementations |
| Combat | 14 | AbilityData, BaseSpecial, BlockSystem, HitboxSystem, StatusController, etc. |
| Enemies | 11 | EnemyController, BossController, BossEncounterController, EnemyAbilityExecutor, EnemyFactory, EnemyProjectile, MirrorParadoxController/EncounterController, plus the EnemyData/BossData/EnemyAbilityData resources (the dead `CpuFighterAI` stub and its duplicate `CpuDifficulty` enum were deleted in Package 6) |
| Environment | 14 | ChronalRewindManager, HubWorldController, LevelManager, FighterStageCatalog, etc. |
| FighterSim | 8 | FighterSimulation, FighterSimulationDriver, FighterCpuController, etc. |
| Networking | 2 | NetworkManager, RollbackProtocol |
| UI | 9 | MainMenu, CharacterSelectScreen, DialogueManager, SettingsMenu, etc. |

### Scenes

| Category | Scenes |
|----------|--------|
| Menus | MainMenu, CharacterSelect |
| Campaign | HubWorld, Level_00_Tutorial, Level_01_Florence |
| Arenas | TestArena |
| Characters | Player |
| UI | FloatingDamageNumber |
| Test | TestScene |

---

## Conclusion

The project is approximately **30-40% complete** toward a shippable initial release. The architecture, systems, and data foundations are substantially ahead of the content and presentation work. The highest-leverage next steps are:

1. Complete the production vertical slice (authored Tutorial + Florence + one Fighter arena)
2. Produce character sprite sheets and animation tracks to replace placeholder geometry
3. Wire bespoke Resonance perk behaviors into ability execution
4. Begin level 2+ production using validated templates
5. Integrate Steam Networking Sockets for online play
6. Produce audio stems and SFX assets

The existing `IMPLEMENTATION_PLAN.md` provides the sequenced milestone roadmap for closing these gaps. The `docs/IMPLEMENTATION_STATUS.md` file tracks the execution ledger against that plan.
