# ADR 0006: GdUnit4-first automated test strategy

Status: Accepted; dependency installation pending

## Context

The project has no automated tests. It needs both pure rule/determinism coverage and Godot scene/integration coverage. Godot 4.7.1 compatibility is required.

## Decision

- Pin GdUnit4 6.2.x for Godot 4.7.1 scene and C# integration tests.
- Use the GdUnit4Net/VSTest integration for C# tests when its package versions have been verified by restore/build in this repository.
- Keep deterministic simulation and rule tests headless and free of scene dependencies even when executed by the same test runner.
- Organize suites under `tests/Unit`, `tests/Integration`, `tests/Determinism`, and `tests/ContentValidation`.
- CI emits machine-readable JUnit/TRX results and treats missing-resource/scene-load errors as failures.

Primary references:

- https://github.com/godot-gdunit-labs/gdUnit4
- https://godot-gdunit-labs.github.io/gdUnit4/latest/csharp_project_setup/csharp-setup/

The upstream compatibility table lists GdUnit4 6.2.x as compatible with Godot 4.7 and 4.7.1. Installation remains pending until the repository's unavailable private NuGet source is isolated from the reproducible restore path.

## Initial suites

- Project/scene smoke loading.
- Character and ability resource completeness/unique IDs.
- FSM transition and interrupt rules.
- Damage, knockback, block, meter, and status rules.
- Rewind safe-frame search and difficulty refresh.
- Save envelope round trip, corruption, tampering, backup, and migration.
- Deterministic input, snapshots, hashes, rollback, and resimulation.

