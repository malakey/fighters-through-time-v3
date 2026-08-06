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

## Budgets to enforce in later gates

- 16.67 ms total frame time at locked 60 FPS.
- 2.5 ms physics, 3.5 ms AI, 8 ms rendering, and 2.67 ms scripts/events.
- At most approximately 150 draw batches, 150,000 visible vertices, 60 active animated sprites, 500 active particles, and 4 GB runtime RAM.
- No gameplay-time GC spikes from high-frequency entities; pools must cover projectiles, VFX, enemies, loot, damage numbers, audio, and constructs.
