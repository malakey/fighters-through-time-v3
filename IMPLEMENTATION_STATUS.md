# Fighters Through Time: Implementation Gap Analysis

Last audited: 2026-08-08 against `design-godot.md` and the current codebase.

P0 remediation update: Packages 0 and 1 in `IMPLEMENTATION_PLAN.md` are implemented and verified. Shared content contracts, the placeholder pipeline, combat/movement edges, the puzzle/environment toolkit, Story pools/drops, and rewind/timeline-collapse contracts now exist; production content and final presentation remain later packages.

Package 4 update (2026-08-07): Section 4 below is superseded for the enemy and boss roster. All 27 `EnemyData` and 15 `BossData` resources are authored, manifest-registered, localized, pooled, and covered by tests; `EnemyController`, `BossController`, `EnemyAbilityExecutor`, `BossEncounterController`, and `MirrorParadoxController` are implemented. What remains for those systems is production art/animation (Package 8), placement inside campaign levels 2-15 (Package 5), and cinematic boss presentation (Package 8).

Package 5 update (2026-08-08): **Section 1 below is superseded.** All sixteen campaign levels are authored, routed, pool-wired and manifest-flipped; levels 2-15 build on `StoryLevelControllerBase` with room graphs, three checkpoints each, the locked encounter economy, per-level era mechanics from the fourteen new toolkit components, boss arenas, extractors, and dialogue resources. The enemy/boss roster is now placed across the campaign rather than only in Florence, and the ending chain (Temporal Core -> ending dialogue -> credits -> `IsCompleted` -> Main Menu) is complete. What remains for campaign content is production presentation: art to replace graybox geometry, animation, music stems, VFX, and cinematic boss presentation (all Package 8). Percentages in the table below are updated accordingly.

Package 6 update (2026-08-08): **Sections 2, 21 and 22 below are superseded.** All ten Fighter stages are independently authored production-contract scenes with their own fixed-point geometry, era-specific deterministic hazard, placeholder parallax presentation, pool config, audio set and preview plate; the match flow is complete end to end (countdown, respawn platform, KO presentation, pause, disconnect handling, results, rematch, all five end conditions); the CPU is complete at all three difficulty bands; and the rollback-readiness gate passes across all nine kits and all ten stages. What remains for Fighter Mode is production art, music, VFX and cinematic KO presentation (Package 8), rendering-cost measurement on target hardware (Package 9), and the online work (Package 7).

This document provides a detailed comparison of what has been implemented versus what is documented in the design specification. It covers every major system, feature, and content area.

---

## High-Level Completion Estimate

| Category | Estimated Completion |
|----------|---------------------|
| Core Architecture & Systems | ~75-80% |
| Fighter Mode Foundation | ~85-90% (ten authored stages, ten era hazards, complete match flow and CPU, rollback gate passed; production art/audio and online remain) |
| Story Mode Flow | ~80-85% (hub, all 16 levels, checkpoints/resume, rewind, completion, credits, and the campaign-complete save state are implemented) |
| Campaign Content (Levels 2-15) | ~75-80% (all 14 levels authored and tested with graybox geometry and placeholder presentation; art/audio/VFX remain) |
| Production Art & Animation | ~2-5% |
| Audio & Music | ~5-10% |
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
| Direct-IP UDP LAN transport | Binary input/hash packets with acknowledgements | **Implemented** - Protocol v2 with Roll/Dash bits |
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
| Universal dash | 12-frame, 1.35× speed, no invulnerability, 4-frame commitment | **Implemented** |
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
| Main Menu | Story Mode, Fighter Mode, Settings, Quit with confirmation | **Implemented** |
| Character Select Screen | Grid portraits, ready toggle, 3s countdown, duplicate prevention | **Implemented** |
| Stage Select Screen | Carousel/grid of 10 arenas with preview | **Implemented** - Ten-stage catalog with localized names |
| Settings Menu | Audio, Display, Controls, Gameplay sections | **Implemented** - Persisted to global save |
| Pause Menu | Story and Fighter variants with correct options | **Implemented** |
| Story Mode HUD | HP, meter, rewinds, block charges, cooldowns, status, currency | **Partially implemented** - `HUDController` exists; visual polish incomplete |
| Fighter Mode HUD | HP bars depleting to center, stocks, timer, block, status, meter | **Partially implemented** - `TestArenaHUD` exists |
| Dialogue box system | Typewriter reveal at 30 chars/sec, portraits, speaker names | **Partially implemented** - `DialogueManager` script exists; no authored dialogue resources |
| Resonance Grid UI | Constellation navigation with purchase flow | **Implemented** - `ResonanceGridPanel` exists |
| Save Select Screen | Slot summaries, new/load/delete with confirmation | **Implemented** |
| Difficulty Select | Easy/Normal/Hard with description tooltips | **Implemented** |
| Loading screen overlay | Story portal effect / Fighter VS matchup cards | **Partially implemented** - Loading overlay with minimum display time; not the full portal/VS-card VFX |
| Post-match results flow | KO freeze/slow-mo/spotlight, results screen, rematch | **Implemented** - Results/rematch/return flow functional |
| Quit confirmation modal | "Are you sure?" with Confirm/Cancel | **Implemented** |
| Boss health bar | Top-center 50% width bar with name and phase notches | **Not implemented** |
| Enemy overhead health bars | Floating bars above damaged/aggroed enemies | **Not implemented** |
| Level completion overlay | Dust earned, time, auto-deposit, return prompt | **Not implemented** |

---

## 12. Audio & Music

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| AudioServer bus hierarchy | Master → Music/SFX/UI with sub-buses (Environmental/Combat/Movement) | **Partially implemented** - `AudioManager` controls buses |
| Dynamic music stems (3 layers) | Ambient, Combat, Boss stems per level with crossfade | **Not implemented** - No music/stem assets exist |
| Horizontal transitions | Last-stock tempo change, KO stinger, silence | **Not implemented** |
| Surface-specific footstep SFX | Wood, stone, sand, metal, snow variants | **Not implemented** |
| Character-specific attack SFX | Per-ability cast and impact sounds | **Not implemented** - No SFX assets |
| Dialogue text chirps | Character-pitched blip sounds (lower for Lincoln, electric for Tesla) | **Not implemented** |
| Audio snapshots | Low health filter, pause duck, ultimate duck, normal gameplay | **Not implemented** |
| SFX pool | Fixed-size reusable players | **Implemented** - 24 prewarmed SFX players |
| Reverb zones | Per-area AudioEffectReverb (caves, cathedrals) | **Not implemented** |
| Low-pass filtering | Pause menus, low health, underwater | **Not implemented** |

---

## 13. Visual Art & Shaders

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| Hand-drawn 2D sprite sheets | Full animation sets per character (17+ sheets each) | **Not implemented** - All characters use code-generated colored rectangles |
| Outline/glow shader system | Alpha-based edge detection with configurable parameters | **Not implemented** |
| Status effect visual indicators | Per-status ShaderMaterial + GPUParticles2D | **Not implemented** |
| PointLight2D glow accompaniment | Ambient environmental light cast matching outline color | **Not implemented** |
| Normal-mapped sprites | Light2D interaction for 2D lighting | **Not implemented** |
| Parallax backgrounds per level | Multi-layer scrolling historical backgrounds | **Partially implemented** - One Florence background asset exists |
| Tile palettes per era | Historical-themed tilesets with physics layers | **Not implemented** |
| Environment VFX | Steam, lava, sand, electricity GPUParticles2D | **Not implemented** |
| Character portraits (512×512) | For character select and HUD panels | **Not implemented** |
| Enemy/boss sprite sheets | Per-type idle/patrol/attack/hit/death animations | **Not implemented** |
| Chronal Orb sprites | 4 types with distinct colored glow effects | **Not implemented** |
| Chronal Extractor props | Idle/damaged/destroyed animation states | **Not implemented** |

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
| InputMap actions (12 defined) | Move, Dash, Jump, Down, BasicAttack, Special1/2, MovementAbility, Block, Roll, Ultimate, Interact, Pause | **Implemented** |
| Per-player device assignment | Local 1v1 controller separation via player index | **Implemented** |
| Dash input detection | Digital double-tap (15 frames) / analog flick (85% magnitude) | **Implemented** |
| Roll dedicated input | `O` keyboard / Right Trigger controller | **Implemented** |
| Serializable PlayerInputFrame | Deterministic per-player input capture for replay/network | **Implemented** |
| Full input remapping | Serialized keybinding overrides via InputMap API | **Partially implemented** - Settings UI exists; full persistence of remapped bindings incomplete |
| Controller haptics | Vibration on hit/block/KO/heavy-landing/hazard events | **Implemented** - `HapticFeedbackManager` with intensity settings |
| Reset-to-default controls | Restore all bindings to factory defaults | **Not fully implemented** |

---

## 18. Accessibility & Settings

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| Master/Music/SFX volume sliders | Mapped to AudioServer buses (default 100%/80%/100%) | **Implemented** |
| Resolution dropdown | Populated from DisplayServer | **Implemented** |
| Screen mode toggle | Fullscreen / Borderless / Windowed | **Implemented** |
| VSync toggle | Enable/disable vertical sync | **Implemented** |
| Damage number toggle | Show/hide floating combat text | **Implemented** |
| HUD opacity slider | Alpha control for on-screen bars | **Implemented** |
| Screen shake slider | 0.0-1.0 intensity multiplier | **Implemented** |
| Haptics toggle + intensity | On/off master + 0-100% slider | **Implemented** |
| Input remapping persistence | Serialized to JSON in GlobalSaveData | **Not fully implemented** - UI exists but complete persistence/reset incomplete |
| Colorblind modes | Protanopia/Deuteranopia/Tritanopia shader presets | **Not implemented** (deferred in design doc) |
| Screen reader support | Text-to-speech for menus/HUD/dialogue | **Not implemented** (deferred in design doc) |
| Alternative input layouts | Tap-to-hold, toggle sprint, deadzone sliders | **Not implemented** (deferred in design doc) |

---

## 19. Localization

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| Translation key architecture | `Tr("key")` / `TranslationServer.Translate()` for all visible text | **Implemented** - Registered in `project.godot` |
| English translation file | `localization/en.csv` with key-value pairs | **Implemented** |
| Key screens localized | Menus, HUD, select screens, settings | **Mostly implemented** - Key flows use translation keys |
| Hardcoded copy audit | All prototype UI cleaned of raw English strings | **In progress** - Some less-traveled paths may still have raw text |
| Multi-language support | ES, FR, DE, JA, etc. with `.translation` resources | **Not implemented** (deferred in design doc) |
| Language selection UI | Dropdown in settings | **Not implemented** (deferred in design doc) |
| CJK font fallback | SystemFont or imported TTF/OTF with fallback chain | **Not implemented** (deferred in design doc) |

---

## 20. Testing & QA

| Feature | Design Spec | Implementation Status |
|---------|-------------|----------------------|
| GdUnit4 test framework | Automated headless C# tests | **Implemented** - 140 passing tests |
| Content validation tests | Character/ability/enemy/stage manifest completeness | **Implemented** |
| Combat formula tests | Damage calc, block rules, meter build/use | **Implemented** |
| Determinism/rollback tests | Hash consistency, prediction, correction convergence | **Implemented** |
| Save system tests | Encryption, migration, corruption, tamper rejection, atomic backup | **Implemented** |
| Input/collision tests | Roll bindings, dash gesture, pushbox geometry | **Implemented** |
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
- **Input architecture** - Per-player serializable input frames with device isolation and dash gesture detection
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
