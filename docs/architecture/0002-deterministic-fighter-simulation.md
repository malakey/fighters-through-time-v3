# ADR 0002: Deterministic Klotho Fighter simulation

Status: Accepted; integration spike required before production migration

## Context

The design requires GGPO-style rollback and rejects delay-based gameplay. Godot's native floating-point physics and scene-tree execution are not an acceptable authoritative online simulation. The current `NetworkManager` snapshot queue is float-based scaffolding with no prediction, rollback, transport, or resimulation.

## Decision

- Fighter Mode, including offline local matches, will use a fixed-step deterministic Klotho simulation.
- Godot nodes own input capture and presentation only; they cannot feed interpolated transforms or animation state back into authoritative gameplay.
- Authoritative state includes fighters, cooldowns, statuses, hitboxes, projectiles, persistent objects, hazards/items, stocks, match timer, and seeded RNG state.
- Network peers exchange input frames and hashes. Snapshots support rollback/resimulation and diagnostic capture.
- Story Mode keeps native Godot physics behind the adapter selected in ADR 0001.

## Candidate dependency

Klotho 0.6.1 is listed in the Godot Asset Library for Godot 4.4+ .NET 8 and provides FP64 math, seeded RNG, ECS, snapshots, rollback, and deterministic physics. Install through `res://addons/klotho/` and import its props only after the spike verifies Godot 4.7.1 compatibility and representative platform-fighter collision behavior.

Primary references:

- https://godotengine.org/asset-library/asset/5234
- https://github.com/xpTURN/Klotho

## Spike exit criteria

- Builds under Godot 4.7.1/.NET 10 target on the supported toolchain.
- Runs a headless two-body movement/collision scenario with identical hashes across repeated runs.
- Saves/loads a snapshot and resumes the same hash sequence.
- Rolls back across a collision and projectile spawn.
- Produces no per-tick managed allocations in the representative loop.

## Consequences

- The current float snapshot queue must be replaced, not incrementally promoted.
- Gameplay randomness must be seeded deterministic state; `System.Random` is prohibited in Fighter authority.
- Klotho integration is an early release gate, not a late networking task.

