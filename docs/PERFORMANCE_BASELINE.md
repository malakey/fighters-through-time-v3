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

## Rollback readiness (Package 6 A4, refreshed at the C1 closeout)

**Gate verdict: PASS across all ten authored stages.** This is Package 7's entry criterion, and it
is met — worst-case depth-7 resimulation costs **0.058 ms median / 0.104 ms p95** against an 8.000 ms
budget, with zero budget breaches over 3,000 measured corrections and a largest full-state snapshot
of 2,699 bytes. Recorded 2026-08-08 by C1 across the ten production stage geometries; the A4 numbers
this section originally carried were Florence-only.

Captured 2026-08-08 on the same Windows development machine, Godot 4.7.1 .NET / .NET 10 Debug build.

Source: `tests/Determinism/RollbackReadinessTests.cs`. Unlike the table above, these numbers do not
come from the Godot scene runner — the deterministic simulation, `OnlineRollbackSession`, and
`InMemoryRollbackTransport` are all engine-free, so the gate runs in the .NET test host. Reproduce
with:

```powershell
dotnet test FightersThroughTime.csproj --settings .runsettings `
  --filter "FullyQualifiedName~RollbackReadinessTests" --logger "console;verbosity=detailed"
```

The timing test prints one `[A4 rollback readiness] ...` line **per stage** plus a worst-case summary
line; the `console` logger at `detailed` verbosity is needed to see them on a passing run.

### Worst-case seven-frame resimulation, per stage

Method: one `OnlineRollbackSession` is fed a remote input packet for exactly
`CurrentTick - MaximumRollbackFrames` on every frame, so each frame pays a full depth-7 rollback plus
seven replayed ticks. Cost is taken from the session's own `Stopwatch` (the same one that raises
`RollbackBudgetExceeded`), sampled over 300 consecutive corrections after a 1,900-tick warm-up that
puts the match into its heaviest state — past the first High-frequency hazard spawn at frame 1,800
and its 90-frame warning, so the measured window always runs with a live hazard. JIT warm-up samples
are discarded. Both fighters are Tesla vs Mozart (persistent coils + an execution zone against two
projectile specials + a Float platform), spamming specials, the movement ability, block, and the
ultimate, with items and hazards at High.

The sweep enumerates `FighterStageGeometry.AllAuthored`, so a stage added later is measured with no
edit to the test. Stage geometry is not part of any snapshot; the snapshot sizes differ between rows
only because the entity mix each hazard identity produces differs.

| Stage | Median | Mean | p95 | Max | Snapshot | Peak entities |
|---|---:|---:|---:|---:|---:|---:|
| `florence_workshop` | 0.048 ms | 0.050 ms | 0.061 ms | 0.319 ms | 2,511 B | 10 |
| `orleans_vanguard` | 0.048 ms | 0.054 ms | 0.104 ms | 0.201 ms | 2,405 B | 10 |
| `chicago_exposition` | 0.052 ms | 0.055 ms | 0.066 ms | 0.267 ms | 2,394 B | 11 |
| `paris_bastille` | 0.050 ms | 0.056 ms | 0.095 ms | 0.265 ms | 2,530 B | 10 |
| `vesuvius_caldera` | 0.048 ms | 0.049 ms | 0.054 ms | 0.198 ms | 2,502 B | 11 |
| `nassau_flagship` | 0.048 ms | 0.050 ms | 0.053 ms | 0.357 ms | 2,610 B | 11 |
| `alexandria_chambers` | 0.054 ms | 0.056 ms | 0.063 ms | 0.282 ms | 2,530 B | 11 |
| `berlin_wall` | 0.046 ms | 0.047 ms | 0.053 ms | 0.197 ms | 2,574 B | 11 |
| `globe_theatre` | **0.058 ms** | 0.062 ms | 0.076 ms | 0.273 ms | **2,699 B** | **12** |
| `gettysburg_ridge` | 0.052 ms | 0.052 ms | 0.061 ms | 0.166 ms | 2,413 B | 9 |

| Metric | Value |
|---|---:|
| Rollback budget (`OnlineRollbackSession.RollbackBudgetMilliseconds`) | 8.000 ms |
| Worst median across ten stages | 0.058 ms (`globe_theatre`) |
| Worst p95 across ten stages | 0.104 ms (`orleans_vanguard`) |
| Worst max across ten stages | 0.357 ms (`nassau_flagship`) |
| Budget breaches over 3,000 measured corrections (10 stages x 300) | 0 |
| Largest full-state snapshot (`CaptureFullState().Length`) | 2,699 bytes (`globe_theatre`) |
| Peak concurrent simulated entities | 12 (`globe_theatre`) |

A worst-case correction therefore costs roughly **0.7 % of the 8 ms budget** and about **0.35 % of a
16.67 ms frame**, leaving roughly 140x headroom on the worst stage. The test fails only if a stage's
p95 exceeds twice the budget or that stage records any budget breach, so ordinary CI variance cannot
flap the gate; the recorded medians are the number that matters. The spread across stages is under
0.012 ms of median — hazard identity is not a meaningful cost driver at this entity scale.

### Convergence and history bounds

- Delayed-input convergence (latency 3 transport polls, items and hazards at High) is asserted for
  nine mirror matchups plus six cross-pairs against **every** authored `FighterStageGeometry` — as of
  the C1 closeout that is eleven geometries (the legacy flat arena plus the ten production stages),
  i.e. 165 (matchup, stage) convergence runs. The geometries are enumerated by reflection, so stages
  added later are covered without editing the test. Each run asserts zero `InputArrivedTooLate`, more
  than one correction per tick across the two peers, and per-tick recorded-hash equality across the
  whole retained 120-tick window.
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
- The full gate (23 cases, including the 165-run convergence matrix and the ten-stage timing sweep)
  completes in about 13 s, well inside the 300 s `TestSessionTimeout` in `.runsettings`.
- Stage geometry is not part of any snapshot. The per-stage snapshot sizes above differ only through
  the entity mix each hazard identity produces, not through the geometry itself.
- These are simulation costs only. Presentation, rendering, and the real network transport are not
  included.

## Budgets to enforce in later gates

- 16.67 ms total frame time at locked 60 FPS.
- 2.5 ms physics, 3.5 ms AI, 8 ms rendering, and 2.67 ms scripts/events.
- At most approximately 150 draw batches, 150,000 visible vertices, 60 active animated sprites, 500 active particles, and 4 GB runtime RAM.
- No gameplay-time GC spikes from high-frequency entities; pools must cover projectiles, VFX, enemies, loot, damage numbers, audio, and constructs.
