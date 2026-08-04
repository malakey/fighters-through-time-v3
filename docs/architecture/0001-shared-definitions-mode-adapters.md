# ADR 0001: Shared definitions with mode-specific execution adapters

Status: Accepted

## Context

Story Mode uses Godot `CharacterBody2D` physics and includes Story-only progression. Fighter Mode requires deterministic fixed-point rollback. Copying complete character kits into independent implementations would create tuning drift, while forcing Story Mode onto the rollback runtime would add unnecessary constraints to campaign interactions.

## Decision

- `CharacterData`, `AbilityData`, movement-ability data, status definitions, hit definitions, and persistent-object definitions are shared, versioned Godot resources.
- Story execution uses Godot nodes/physics through a Story combat adapter.
- Fighter execution uses the deterministic simulation through a Fighter combat adapter.
- Shared conformance tests feed equivalent definitions and input scenarios to both adapters and compare rule-level results such as damage, cooldowns, status, spawn intent, and meter changes.
- Temporal Resonance Grid modifiers are applied only by the Story adapter and never mutate the shared base definition.

## Consequences

- Physics trajectories may be implemented differently, but move identity, timing, damage, cooldown, status, and spawn rules remain one source of truth.
- Ability resources cannot contain mutable per-session state.
- Factories may instantiate configured scenes but must not author canonical tuning values.

