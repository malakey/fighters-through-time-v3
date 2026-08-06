# Implementation status

Last audited: 2026-08-05 against `design-godot.md` and `IMPLEMENTATION_PLAN.md`.

This is the concise execution ledger for the gap-closure roadmap. `AGENTS.md` is the durable architecture/context file, while `IMPLEMENTATION_PLAN.md` remains the ordered delivery plan.

## Milestone ledger

| Milestone | Status | Implemented boundary |
|---|---|---|
| 0 - Production contracts and validators | Complete | Authoritative manifest, content contracts, authored templates, replaceable placeholder kit, validators, per-scene pool budgets, and captured performance baselines are implemented. Tracked legacy `.godot/` files still require separately authorized index cleanup. |
| 1 - Data, collision, input | Complete for the current architecture | Nine character resources, 36 ability resources, nine authored Resonance grids, enemy/boss schemas, a ten-stage Fighter catalog, canonical statuses, collision settings, fixed aspect/60 Hz, and per-player serializable inputs. |
| 2 - Shared combat/movement | P0 complete; production presentation open | Typed hits, combat formulas, authored placeholder event tracks, independent aerial/crouched combos, ledges, one-way drop-through, block/meter/status rules, hyper-armor interruption parity, soft Story pushboxes, accelerated run, dash, and roll are implemented and tested. Final character animation/art remains production work. |
| 3 - Deterministic Fighter foundation | Substantially complete | Klotho fixed-point 1v1, full snapshots/hashes, prediction/correction rollback, deterministic jostling and universal dash/roll phases, projectiles, persistent constructs, hazards, Chronal Orbs, movement abilities, and pooled Godot presentation proxies are implemented and tested. Performance/platform proof remains. |
| 4 - Production vertical slice | Functional prototype; production pass open | Slot creation, difficulty, tutorial/hub/Florence routing, reusable puzzle/environment toolkit, representative authored placeholder puzzle/hazards, safe-frame rewind, checkpoint resume, pooled drops, localized HUD/menu flow, CPU match completion, and representative Florence art work. Full authored layouts, production animation/audio/VFX, and complete tutorial/Florence content gates remain. |
| 5 - Story meta-game and secure saves | P0 foundation complete; breadth open | Schema-v3 AES/HMAC envelopes, per-install keys, atomic recovery, migration/tamper rejection, puzzle completion persistence, three-slot flow, checkpoint autosave, nine grids, deposit/unlock UI, Story-only modifiers, world-aware rewind, and Timeline Collapse restart choices are implemented. Bespoke perks and complete hub/content breadth remain. |
| 6 - Local Fighter Mode | Functional core; content/polish open | Human-vs-CPU and human-vs-human selection, CPU difficulty bands, two-sided character and ten-stage selection, Stock/Time/Hybrid rules, stocks/timer/items/hazards, deterministic per-stage hazard identities/orbs, results/rematch/menu, and win/loss persistence. The stages share the Test Arena template; production-authored arenas, controller-disconnect flow, match presentation polish, and broader kit-specific execution remain. |
| 7 - LAN/online rollback | Foundation only | Protocol-v2 compact input/hash packets include Roll/Dash commands, acknowledgements, confirmed hashes, direct-IP UDP LAN transport, deterministic in-memory latency transport, rollback/resimulation, desync/budget diagnostics, and cryptographic room codes. Steam transport/relay, discovery, negotiation, online UI, state resync, public queue, and failure UX remain. |
| 8 - Full content/assets | Contracts complete; production pending | The asset/content manifest and placeholder replacement contracts exist. Levels 2-15, the full enemy/boss roster, nine production character presentations, ten authored arenas, campaign dialogue, and final assets remain. |
| 9 - UI/audio/accessibility | In progress | Settings are reachable and persisted; audio, resolution, fullscreen, VSync, haptics, damage numbers, HUD opacity, and shake apply to implemented flows. SFX uses a fixed pool and key screens are localized. Production UI components, input remapping persistence, full copy cleanup, stems, and final visual/audio assets remain. |
| 10 - QA/release | Foundation only | The suite has 118 passing tests plus successful Godot import and clean Tutorial, Florence, Hub, Main Menu, and Test Arena smoke runs. Profiling, soak testing, balancing, exports, platform/Steam Deck validation, and release checklists remain. |

## Implemented systems

- The deterministic Fighter authority now includes eight-frame run acceleration, universal non-invulnerable dash, phased evasive roll, fixed-point pushbox jostling, attacks, block, meter, statuses, cooldowns, match modes, projectiles, hazards, Chronal Orbs, persistent constructs, bottom-zone KOs, snapshots, hashes, prediction, corrected-input rollback, and resimulation.
- Story combatants use lower-torso soft pushboxes rather than solid hurtboxes. Normal movement and dash body-block; roll travel crosses ordinary enemies, while opt-in immovable bosses block it.
- The Test Arena uses `FighterSimulationDriver`; Godot player nodes and 112 prewarmed entity proxies are presentation only. Story-only Resonance values are excluded at the Fighter loadout boundary.
- Local Fighter setup supports a human or deterministic CPU opponent, two character choices, CPU Easy/Normal/Hard reaction bands, Stock/Time/Hybrid rules, stocks, timer, items, and hazards. Match completion supports results, rematch, change fighters, menu return, and global/character statistics.
- Story rewind records 300 frames at 60 Hz, searches backward for a safe grounded state, retains a grounded fallback, replays backward at 4x, uses the physical checkpoint as a final fallback, and applies the Easy/Normal/Hard restore/refill contracts.
- Rewind now freezes enemies and persistent simulation participants, clears enemy projectiles, restores/reset constructs according to explicit policies, grants 120 invulnerable frames, and emits typed visual/audio presentation phases. Timeline Collapse applies the 20 percent Dust penalty and offers anchor/full-level restart choices in the Hub.
- Story projectiles/zones, current enemy instances, placeholder VFX, persistent constructs, Chronal Dust, and helper items use stable-ID pools with configured Grow/Recycle/Reject policies. Difficulty-driven drops implement 50/25/10 healing and +50%/15s, +25%/10s, or no buff drops.
- The reusable Story toolkit now includes stable puzzle state, levers/rotating platforms, weights/plates/counterweights, power routing, extractors, destructibles/chests, room transitions, crumble/cyclic hazards, and Chronal Rifts. Florence contains representative placeholder instances.
- Story saves use schema version 3, AES-256-CBC encryption, encrypt-then-HMAC-SHA-256, independent derived keys, a random per-install key file, atomic verified writes, backup recovery, corruption preservation, legacy migration, puzzle completion persistence, and future-schema rejection.
- Story slot flow supports three slots, locked-character creation, difficulty selection, resume, autosave at checkpoints/level completion, and restoration of Florence position, HP, Influence, dust, and rewinds.
- All nine characters have complete baseline 3x3 Resonance data resources with prerequisites/costs plus repository deposit/unlock UI. Generic resolved stats and character-scoped modifier queries apply only when Story characters are built; character-specific perk behavior remains to be connected to bespoke abilities.
- Fighter stage select loads a localized ten-stage catalog. Each entry currently uses the shared Test Arena scene with distinct presentation colors and a snapshot/hash-visible deterministic hazard identity; this is functional prototype breadth, not ten production arena scenes.
- LAN groundwork uses exact binary input packets and UDP, while a deterministic in-memory transport exercises latency and rollback convergence. Steam/public matchmaking deliberately reports unavailable until its SDK is integrated.
- Settings persist audio buses, display mode/resolution/VSync, haptics, damage numbers, HUD opacity, and screen-shake scale. SFX playback uses 24 prewarmed reusable players.
- Florence now includes an original generated parallax background asset at `assets/environments/florence/florence_far_background.png` while most character/environment presentation remains prototype art.

## Validation record

- `dotnet build FightersThroughTime.csproj --no-restore --nologo`: passed. The only warning is the existing nullable-context warning in vendored GdUnit4 source; there are no project or Klotho warnings.
- Godot 4.7.1 headless editor import: passed with no missing resource or script errors.
- GdUnit4/.NET suite: 118 passed, 0 failed, 0 skipped.
- Direct headless smoke runs: `MainMenu.tscn`, `TestArena.tscn`, `HubWorld.tscn`, `Level_00_Tutorial.tscn`, and `Level_01_Florence.tscn` all passed without runtime diagnostics. Standalone Godot had to run outside the filesystem sandbox because the sandboxed native process crashed before project initialization.

## Remaining implementation packages

1. Finish the production vertical slice: authored Tutorial/Florence layouts, final encounters and puzzle gates, full HUD detail, dialogue presentation, representative music/VFX, and measured frame budgets.
2. Finish Story meta-game breadth: execute each character's bespoke Resonance modifiers, complete data-driven level content/routing validation, and build the remaining hub stations/progression gates on the completed P0 contracts.
3. Finish local Fighter content/polish: replace the ten shared-template stage variants with independently authored arena scenes/art, add kit-specific deterministic behaviors for all nine characters, controller disconnect handling, countdown/KO presentation, and rematch-setting persistence review.
4. Finish online scope: Steam Networking Sockets/relay, LAN discovery, handshake and rules agreement, synchronized load/start, state resync, packet-loss/reorder/timeout tests, public/private lobby UI, forfeit/error flows, and protocol/build gates.
5. Produce release content and presentation: levels 2-15, bosses/enemies, character sprites/animations/portraits, arenas, dialogue, audio stems, VFX/shaders, reusable UI scenes, and complete localization cleanup.
6. Complete release QA: gameplay-allocation audit, rollback/network soak, performance profiling, balancing, export presets, Windows/macOS/Linux/SteamOS/Steam Deck validation, and release documentation.

## Scope warning

The architecture and functional foundations are substantially farther along, but the initial-release game is not content-complete or production-ready. Do not describe LAN as production online play, authored Resonance data as complete bespoke perk behavior, or the prototype Florence/shared-template Fighter presentation as final assets.
