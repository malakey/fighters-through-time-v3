# Performance baseline

Captured: 2026-08-05 with Godot 4.7.1 .NET on the Windows development machine.

This is the Package 0 comparison baseline, not a release-performance certification. It uses the repeatable headless runner at `res://scenes/diagnostics/PerformanceBaselineRunner.tscn`, samples 120 frames after a 60-frame warm-up, and loads each current gameplay prototype in one process.

## Command

```powershell
& "C:\path\to\Godot_v4.7.1-stable_mono_win64_console.exe" `
  --headless `
  --path "C:\path\to\fighters-through-time-v3" `
  --scene "res://scenes/diagnostics/PerformanceBaselineRunner.tscn"
```

## Measurements

| Scene | Frame delta (ms) | Process monitor (ms) | Physics monitor (ms) | Max managed allocation delta (bytes) | Engine static memory (bytes) | Managed heap (bytes) | Nodes | Active animated sprites | Active particles | Draw calls | Primitives |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Tutorial | 6.9444 | 0.0021 | 0.1331 | 16,400 | 33,207,931 | 9,272,416 | 154 | 0 | 0 | 0 | 0 |
| Florence | 6.9444 | 0.0945 | 0.0046 | 16,400 | 41,861,595 | 13,127,864 | 360 | 0 | 0 | 0 | 0 |
| Test Arena | 6.9444 | 0.1424 | 0.0192 | 16,400 | 33,959,259 | 30,254,368 | 192 | 0 | 0 | 0 | 0 |

## Interpretation and limitations

- The headless runner is uncapped at roughly 144 Hz on this machine, so its 6.9444 ms frame delta is a diagnostic sampling cadence rather than proof of the 60 FPS rendering target.
- The dummy headless renderer reports zero draw calls and primitives. These values are recorded explicitly, but the required rendered GPU baseline must be captured on the target PC and Steam Deck during Package 9.
- Current prototype scenes use `ColorRect` and code-generated presentation rather than active `AnimatedSprite2D` or particle systems, so the zero sprite/particle counts are expected and will change as placeholder presentation is integrated.
- The managed allocation delta includes probe and engine-interop overhead. It is a regression comparison value, not proof of zero gameplay allocations.
- Process/physics monitor values are Godot monitor samples. Content packages must continue reporting frame delta, allocations, memory, draw calls, primitives, sprites, and particles through the same runner so changes remain comparable.

## Rollback readiness (Package 6 A4)

Captured 2026-08-08 on the same Windows development machine, Godot 4.7.1 .NET / .NET 10 Debug build.

Source: `tests/Determinism/RollbackReadinessTests.cs`. Unlike the table above, these numbers do not
come from the Godot scene runner — the deterministic simulation, `OnlineRollbackSession`, and
`InMemoryRollbackTransport` are all engine-free, so the gate runs in the .NET test host. Reproduce
with:

```powershell
dotnet test FightersThroughTime.csproj --settings .runsettings `
  --filter "FullyQualifiedName~RollbackReadinessTests" --logger "console;verbosity=detailed"
```

The timing test prints one `[A4 rollback readiness] ...` line with the measured values; the
`console` logger at `detailed` verbosity is needed to see it on a passing run.

### Worst-case seven-frame resimulation

Method: one `OnlineRollbackSession` is fed a remote input packet for exactly
`CurrentTick - MaximumRollbackFrames` on every frame, so each frame pays a full depth-7 rollback plus
seven replayed ticks. Cost is taken from the session's own `Stopwatch` (the same one that raises
`RollbackBudgetExceeded`), sampled over 300 consecutive corrections after a 1,900-tick warm-up that
puts the match into its heaviest state. JIT warm-up samples are discarded.

Load at measurement time (Florence Workshop geometry, items and hazards at High): 11 concurrent
simulated entities — 5 projectiles, 3 persistent constructs, 2 execution zones, 1 active stage
hazard, 1 Chronal Orb — plus both fighters spamming specials, the movement ability, block, and the
ultimate.

| Metric | Value |
|---|---:|
| Rollback budget (`OnlineRollbackSession.RollbackBudgetMilliseconds`) | 8.000 ms |
| Depth-7 resimulation, median (3 runs) | 0.087 – 0.119 ms |
| Depth-7 resimulation, mean (3 runs) | 0.090 – 0.116 ms |
| Depth-7 resimulation, p95 (3 runs) | 0.100 – 0.141 ms |
| Depth-7 resimulation, max (3 runs) | 0.251 – 0.402 ms |
| Budget breaches over 900 measured corrections | 0 |
| Full-state snapshot (`CaptureFullState().Length`) under that load | 2,482 bytes |
| Peak concurrent simulated entities | 11 |

A worst-case correction therefore costs roughly **1.1 – 1.5 % of the 8 ms budget** and about
**0.6 % of a 16.67 ms frame**, leaving roughly 60–90x headroom. The test fails only if p95 exceeds
twice the budget, so ordinary CI variance cannot flap the gate; the recorded medians are the number
that matters.

### Convergence and history bounds

- Delayed-input convergence (latency 3 transport polls, items and hazards at High) is asserted for
  nine mirror matchups plus six cross-pairs against **every** authored `FighterStageGeometry`. The
  geometries are enumerated by reflection, so stages added later are covered without editing the
  test. Each run asserts zero `InputArrivedTooLate`, more than one correction per tick across the
  two peers, and per-tick recorded-hash equality across the whole retained 120-tick window.
- A long run (2,200 ticks per stage) crosses a full High-frequency Chronal Orb cycle (660 frames)
  and a full stage-hazard cycle (1,800 frames plus its 90-frame warning) and asserts the same
  equality with live hazards and orbs on the field.
- `CorrectRemoteInput` rejects any tick at or beyond `RollbackHistoryTicks` (120), negative ticks,
  the current tick, future ticks, and unknown player slots.
- A depth-8 packet raises `InputArrivedTooLate` and leaves state untouched; a depth-7 packet is
  applied and changes the confirmed hash.
- Corrected-tick de-duplication is a fixed 8-slot ring: over a 3,000-tick session with a correction
  every frame the retained record count never exceeds 8, while a duplicate packet for an
  already-corrected tick inside the window is still ignored. (This replaced an unbounded `HashSet`
  that grew for the whole match.)

### Limitations

- Debug build, single machine, no Steam Deck measurement. Rollback cost on the minimum-spec target
  is unmeasured.
- Stage geometry is not part of any snapshot, so the snapshot size above is stage-independent; the
  per-stage sweep still needs re-running once all ten geometries and their hazard implementations
  exist (Package 6 C1 refreshes this section).
- These are simulation costs only. Presentation, rendering, and the real network transport are not
  included.

## Budgets to enforce in later gates

- 16.67 ms total frame time at locked 60 FPS.
- 2.5 ms physics, 3.5 ms AI, 8 ms rendering, and 2.67 ms scripts/events.
- At most approximately 150 draw batches, 150,000 visible vertices, 60 active animated sprites, 500 active particles, and 4 GB runtime RAM.
- No gameplay-time GC spikes from high-frequency entities; pools must cover projectiles, VFX, enemies, loot, damage numbers, audio, and constructs.
