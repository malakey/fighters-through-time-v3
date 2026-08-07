# Building and validating Fighters Through Time

## Required tools

- Godot 4.7.1 .NET/Mono build.
- .NET SDK 10.0.302, pinned by `global.json`.
- A restored NuGet cache for `Godot.NET.Sdk`, Klotho's runtime dependencies, GdUnit4's test dependencies, and `K4os.Compression.LZ4`.

The game targets `net10.0`, built with the pinned SDK. Verified on 2026-08-06: Godot 4.7.1 .NET loads the `net10.0` assembly, headless import and scene runs are clean, and all 270 GdUnit4 tests pass.

## Build

From the repository root:

```powershell
dotnet build FightersThroughTime.csproj --nologo
```

The Godot SDK writes generated build output beneath `.godot/mono/temp/`. That directory is ignored and must not be treated as source.

## Manual smoke validation

Open the repository root in Godot 4.7.1 .NET and verify:

1. `res://scenes/menus/MainMenu.tscn` loads and runs.
2. Story selection can load Tutorial, Hub, and Florence.
3. Fighter selection can load `res://scenes/arenas/TestArena.tscn`.
4. The Godot output contains no missing script, missing resource, or invalid node-path errors.

## Automated tests

Set `GODOT_BIN` to the Godot .NET console executable, then run the GdUnit4 C# test project from the repository root:

```powershell
$env:GODOT_BIN = "C:\path\to\Godot_v4.7.1-stable_mono_win64_console.exe"
dotnet test FightersThroughTime.csproj --settings .runsettings
```

The shared `.runsettings` file forces serial, headless execution and provides sufficient compile and engine-connect timeouts for a cold Godot import.
The audited baseline is 270 passing tests (verified 2026-08-06). See `docs/IMPLEMENTATION_STATUS.md` for the current coverage and remaining gaps.

Two known false signals:

- If the run reports roughly 21 tests and still exits 0, GdUnit4 could not launch Godot — check that `GodotSharp\` sits beside the Godot executable. Read the `Total:` count, not the exit code.
- After `.godot/mono/temp/` is cleared, the first run can exceed the 300 s `TestSessionTimeout` while Godot re-imports and abort mid-test, reporting a spurious failure and a partial total. Re-run with a warm cache before treating it as a regression.

## Known environment warning

Some developer machines have an unavailable private NuGet source in their user-level `NuGet.Config`. A build can still succeed from cached packages while reporting `NU1801`/`NU1900`. Do not commit credentials or user-specific package feeds to this repository. CI and documented restore commands should use an explicit, approved package-source configuration.
