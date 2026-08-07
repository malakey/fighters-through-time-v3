---
description: Run a Godot scene headlessly for a smoke check (defaults to the main menu)
argument-hint: [res:// scene path] [frames]
allowed-tools: Bash, Read, Grep, Glob
---

Smoke-run a scene in headless Godot and report the engine output.

Scene: `$1` (default `res://scenes/menus/MainMenu.tscn`)
Frames: `$2` (default `300`, i.e. five seconds at the locked 60 Hz)

```
"D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe" --headless --path "D:\Projects\Fighters Through Time - V3" <scene> --quit-after <frames>
```

Build first if C# sources changed since the last build (`dotnet build FightersThroughTime.csproj --nologo`) — headless Godot runs the last compiled assembly, not your edits.

Useful targets:

| Scene | Purpose |
|---|---|
| `res://scenes/menus/MainMenu.tscn` | Main scene and entry flow |
| `res://scenes/arenas/TestArena.tscn` | Fighter simulation sandbox |
| `res://scenes/fighter/FighterStage_Florence.tscn` | First production-contract Fighter stage |
| `res://scenes/campaign/` scenes | Hub, tutorial, Florence story flows |
| `res://scenes/diagnostics/PerformanceBaselineRunner.tscn` | Performance baseline capture |

Scan the output for missing scripts, invalid `res://` paths, node-name mismatches, invalid exported resources, null references, and signal/event lifecycle errors. Report those explicitly — a clean exit code with errors in the log is still a failure. If the run reports `.NET: Assemblies not found`, the Godot binary has been separated from `D:\Projects\GodotSharp\` (see CLAUDE.md); say so instead of retrying.
