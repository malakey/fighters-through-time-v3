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
this section originally carried were Florence-only. The gate still passed when re-measured on
2026-10-16, after the combat and movement update: 0.084 ms median / 0.168 ms p95, zero breaches, a
largest snapshot of 4,473 bytes (the re-measure section below). After Echo Step's removal (2026-10-25) it read 0.090 ms median / 0.137 ms p95 and 3,277 bytes (the last section below).

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

### Re-measure, 2026-10-16 (the combat and movement update closeout)

**Gate verdict: PASS across all ten stages, zero breaches.** Re-measured on 2026-10-16 at the
closeout of the combat and movement update (`docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md` §4.3,
`main` at `14374d7`, the GdUnit lock held), with the same `RollbackReadinessTests` method and command
as above: Tesla vs Mozart, the 1,900-tick warm-up, 300 depth-7 corrections per stage, the 8.0 ms
budget. This is the first re-measure since 2026-08-08, so it carries Packages 11–13 and the update
together: the update added Klotho component **323** (`FighterActionStateComponent`, nine ints,
36 B per fighter, 72 B per snapshot by its layout) and runs every Special through its authored
startup, active and recovery, so each tick now clocks casts, the perfect-block window, the
short-hop latch and the other new rules.

| Stage | Median | Mean | p95 | Max | Snapshot | Peak entities |
|---|---:|---:|---:|---:|---:|---:|
| `florence_workshop` | 0.075 ms | 0.084 ms | 0.123 ms | 0.280 ms | 4,300 B | 8 |
| `orleans_vanguard` | 0.080 ms | 0.092 ms | 0.135 ms | 0.704 ms | 4,237 B | 8 |
| `chicago_exposition` | 0.082 ms | 0.099 ms | **0.168 ms** | 0.542 ms | 4,353 B | 10 |
| `paris_bastille` | 0.074 ms | 0.079 ms | 0.096 ms | 0.319 ms | 4,300 B | 8 |
| `vesuvius_caldera` | 0.071 ms | 0.080 ms | 0.144 ms | 0.243 ms | 4,344 B | 8 |
| `nassau_flagship` | 0.077 ms | 0.078 ms | 0.090 ms | 0.190 ms | 4,374 B | 10 |
| `alexandria_chambers` | **0.084 ms** | 0.087 ms | 0.109 ms | 0.187 ms | 4,180 B | 8 |
| `berlin_wall` | 0.079 ms | 0.080 ms | 0.092 ms | 0.148 ms | 4,193 B | 10 |
| `globe_theatre` | 0.078 ms | 0.084 ms | 0.120 ms | 0.259 ms | **4,473 B** | 10 |
| `gettysburg_ridge` | 0.078 ms | 0.090 ms | 0.140 ms | 0.313 ms | 4,245 B | 9 |

| Metric | 2026-08-08 | 2026-10-16 |
|---|---:|---:|
| Worst median across ten stages | 0.058 ms (`globe_theatre`) | 0.084 ms (`alexandria_chambers`) |
| Worst p95 across ten stages | 0.104 ms (`orleans_vanguard`) | 0.168 ms (`chicago_exposition`) |
| Worst max across ten stages | 0.357 ms (`nassau_flagship`) | 0.704 ms (`orleans_vanguard`) |
| Budget breaches over 3,000 measured corrections | 0 | 0 |
| Largest full-state snapshot | 2,699 bytes (`globe_theatre`) | 4,473 bytes (`globe_theatre`) |
| Peak concurrent simulated entities | 12 (`globe_theatre`) | 10 (four stages) |

A worst-case correction still costs about **1 % of the 8 ms budget** and about **0.5 % of a
16.67 ms frame**. The largest snapshot grew 1,774 bytes since 2026-08-08; component 323 accounts for
72 of them by its layout, and the rest accrued over Packages 11–13 (which added components 312–322
and grew 303, 309 and 320) and through any change in the entity mix. No snapshot was measured between
the two dates, so the growth cannot be split further, and the change in peak entities was not
investigated. Raw log:
`D:/Projects/genre_review_2026-10-13/closeout/rollback_readiness.log`.

**A shared machine can fail the gate on one correction.** The case fails on any budget breach, and the
depth-7 cost is wall-clock: during the update two workstream full runs on a machine shared with other
checkouts' builds failed it on isolated over-budget corrections beside normal medians (two on
`alexandria_chambers`, max 10.942 ms, in CM-BOT's run; one of 17.4 ms on `paris_bastille` in
CM-SIM's), and both passed when re-run alone under the lock.
Re-run the class alone before reading a breach as a cost.

### Re-measure, 2026-10-25 (the feel and UI update: Echo Step removed)

**Gate verdict: PASS, zero breaches.** Echo Step's removal (`docs/FEEL_UI_UPDATE_2026-10-25_PLAN.md`
§2.5) retired Klotho components 313–317 (the 31-sample position ring, 524 B per fighter) and four
`FighterVerbComponent` fields, about 1,096 B off every full snapshot. FU-ECHO measured the rollback
readiness class on its own branch (`fu/echo`), before FU-KNOCK merged; FU-KNOCK added no snapshot
state (it uses only reserved bits of component 323), so the snapshot figure carries to the merged
tree, while the resimulation timings were not re-run on it (the `dotnet test` logs of the integrated
runs print no timing lines). Same machine, Godot 4.7.1 .NET / .NET 10 Debug build, the same
10-stage worst-case depth-7 harness as above.

| Metric | 2026-10-16 | 2026-10-25 (`fu/echo`) |
|---|---:|---:|
| Worst median across ten stages | 0.084 ms (`alexandria_chambers`) | 0.090 ms |
| Worst p95 across ten stages | 0.168 ms (`chicago_exposition`) | 0.137 ms |
| Budget breaches | 0 | 0 |
| Largest full-state snapshot | 4,473 bytes (`globe_theatre`) | 3,277 bytes |

The median is a few microseconds higher while the p95 and the snapshot are lower; the medians
differ by less than the run-to-run spread seen on this shared machine, so no cost change is claimed.
The per-stage table was not recorded for this re-measure.

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
