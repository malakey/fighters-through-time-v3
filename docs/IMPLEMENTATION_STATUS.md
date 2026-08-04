# Implementation status

Last audited: 2026-08-03 against `design-godot.md` and `IMPLEMENTATION_PLAN.md`.

This is the concise execution ledger for the gap-closure roadmap. `AGENTS.md` remains the durable architecture/context file, while `IMPLEMENTATION_PLAN.md` remains the ordered delivery plan.

## Completed foundations

- Reproducible .NET/Godot setup: pinned SDK, local NuGet configuration, build instructions, `.gitignore`, third-party notices, and six architecture decisions.
- GdUnit4 6.2.0 is installed and integrated with headless C# tests. The current suite has 38 passing tests covering resource manifests, scene loading, input serialization/isolation, collision layers, combat rules, statuses, meters, pools, and deterministic Fighter state.
- Canonical content schemas/resources exist for all nine characters and their 36 initial ability slots, plus enemy and boss data. `CharacterFactory` and `EnemyFactory` consume resources rather than defining duplicate tuning.
- Gameplay input is represented by a serializable, quantized `PlayerInputFrame`. `InputManager` owns player/device assignment and supports injected sources for CPU, rollback, replay, and tests.
- Project configuration uses 60 Hz physics, fixed 1920×1080 `keep` aspect behavior, black presentation bars, and named collision layers 1–11 with centralized masks.
- Shared combat includes typed attack payloads, 0.8×/1.0×/1.5× combo tuning, weight-adjusted knockback, directional block rules, special shield shatter, ultimate bypass, guard-break daze, Influence gain/use/stock retention, and exact-frame timelines.
- Canonical statuses are `TimeDilation`, `Venom`, `StaticCharge`, `RadiantBurn`, and `Root`. New effects replace old effects and their mutable runtime state is snapshot-safe.
- Pool lifecycle now validates capacity, safely recycles/reset nodes, ignores duplicate release, and cleans pool containers. Player and training-dummy damage numbers use a warmed 64-object recycle pool rather than instantiate/tween/free churn.

## Deterministic Fighter foundation

- Klotho v0.6.1 is pinned under `addons/klotho/`. It is experimental and must not yet be described as production-ready netcode.
- `scripts/FighterSim/` owns fixed-point 1v1 fighter/match/runtime/tuning state and stable component/entity IDs.
- Normalized `CharacterData` and `AbilityData` resources are converted once at the match boundary into immutable deterministic loadouts. Story-only Resonance modifiers are not accepted by that boundary.
- The simulation covers ground/air movement, character jump counts, bottom-zone stock loss, solid side/top bounds, basics, both specials, generic ultimates, directional blocking, HP, Influence, cooldowns, canonical statuses, timer resolution, and stock/HP-percentage tie breaking.
- Full-state snapshots, deterministic hashes, a 120-tick history, remote-input prediction, corrected-input rollback, and resimulation are implemented.
- The local Test Arena runs the fixed-point simulation as authority. Godot `PlayerController` nodes are input-free/collision-free presentation objects driven from verified simulation state. The diagnostic HUD displays tick/hash and both players' competitive state.

## Validation record

- `dotnet build FightersThroughTime.csproj --no-restore --nologo -p:DebugType=None -p:DebugSymbols=false`: passed. One nullable-context warning remains in vendored GdUnit4 source; there are no project or Klotho determinism warnings.
- Godot 4.7.1 headless editor import: passed with no missing resource/script errors.
- GdUnit4/.NET headless suite: 38 passed, 0 failed, 0 skipped.
- Direct headless run of `res://scenes/arenas/TestArena.tscn`: passed with no runtime diagnostics.

## Next implementation packages

1. Finish Milestone 2 pooling and authored combat execution: replace runtime allocation of projectiles, zones, damage labels, SFX, enemies, pickups, and persistent constructs; add reset/overflow tests and animation-frame hit callbacks.
2. Finish Milestone 3 simulation breadth: deterministic projectile/persistent-object/hazard systems, character-specific movement/construct behavior, rollback tests across their lifecycles, state-size/performance budgets, and removal or explicit archival of the legacy float snapshot queue.
3. Build Milestone 4 as the production vertical slice: Tutorial → Hub → Florence → Hub plus one authored Fighter arena, full rewind/dust/checkpoint loop, production HUD/dialogue, representative assets/audio, CPU match completion, and accessibility validation.
4. Implement the versioned/atomic/encrypted/HMAC save envelope and migrations before expanding campaign progression.
5. Add LAN/Steam transport and matchmaking only after the deterministic simulation meets rollback convergence and performance gates. Steam Networking Sockets is not installed.

## Material gaps that remain

- Campaign routing and authored content exist only for the tutorial and Florence prototypes; levels 2–15 and production arenas are absent.
- Character-specific Fighter execution is still a generic deterministic adapter for core attacks. Persistent constructs, projectiles, hazards, items, and full historical kits are represented in schema but not fully simulated.
- The Story rewind buffer, safe-frame selection, difficulty-specific refill/restore, complete Resonance Grid, and progression loop are incomplete.
- Saves remain prototype Base64/plain JSON without schema migration, atomic recovery, encryption, or integrity verification.
- Pool coverage is incomplete and code-generated placeholder geometry/UI remains widespread.
- CPU Fighter behavior, results/rematch, complete settings flow, production localization cleanup, music stems, art/animation/VFX, performance profiling, platform exports, LAN, Steam transport, relay, and matchmaking remain pending.
- `.gitignore` now excludes `.godot/`, but generated files already tracked by the repository still appear in status until an explicitly authorized Git index cleanup is performed.
