# ADR 0005: Stable resource IDs and canonical tuning ownership

Status: Accepted

## Context

Character resources exist, but factories currently create ability/enemy data and repeat tuning numbers in code. Save data, rollback snapshots, content validators, and localization all need stable identifiers that survive file moves and display-name changes.

## Decision

- Every character, ability, movement ability, status, enemy, boss, persistent object, item, stage, level, dialogue sequence, and Resonance node has a unique lowercase snake-case string ID.
- IDs are serialized and networked; display names are localization keys and may change independently.
- Canonical base tuning lives in versioned `.tres` resources. Runtime controllers contain mutable state only.
- Resources may reference scenes/VFX/audio, but runtime scenes do not override canonical numeric values without an explicit mode-specific modifier layer.
- A validator checks uniqueness, required references, compatible schema versions, and complete roster slots.

## Consequences

- Renaming an ID is a data migration, not a cosmetic edit.
- Factory switch statements should select definitions/scene types, not embed move numbers.
- Story upgrades remain separate modifiers keyed to base definitions.

