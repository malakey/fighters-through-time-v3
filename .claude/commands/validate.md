---
description: Run the project validation gate — build, headless import, and the GdUnit4 suite
allowed-tools: Bash, Read, Grep, Glob
---

Run the standard validation gate for this repository, in order, and stop at the first hard failure.

1. **Build**

   ```
   dotnet build FightersThroughTime.csproj --nologo
   ```

   One warning is pre-existing and expected: `CS8632` in `addons/gdunit4/src/dotnet/GdUnit4CSharpApi.cs`. Any other warning or error is yours.

2. **Headless import** — catches broken scenes, resources, and `res://` paths that the C# compiler cannot see.

   ```
   "D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe" --headless --path "D:\Projects\Fighters Through Time - V3" --quit
   ```

   If this reports `.NET: Assemblies not found`, the Godot binary has been separated from `D:\Projects\GodotSharp\` — see CLAUDE.md. Report that and note that step 3's result will be partial.

3. **Tests**

   ```
   dotnet test FightersThroughTime.csproj --settings .runsettings
   ```

   Read the `Total:` count, not just the exit code. The baseline is 270 passing (verified 2026-08-06 on `net10.0`). Two false signals to recognize before reporting:

   - ~21 tests and exit 0 — GdUnit4 could not launch Godot and silently skipped every engine-dependent test. Failed validation, not a pass.
   - `Aborting test run: test run timeout of 300000 milliseconds exceeded` with a partial total — a cold-cache timeout, not a regression. Re-run warm, and confirm the named test class in isolation with `--filter` before reporting a failure.

Then report, plainly: the actual build result, the import result, and the passed/failed/total test counts. If anything failed, quote the relevant output rather than summarizing it away. Do not claim the gate passed unless all three steps genuinely did.
