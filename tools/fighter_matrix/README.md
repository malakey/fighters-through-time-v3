# Fighter CPU matrix tool

Plays CPU-versus-CPU Fighter matches over all 81 hero pairs on one stage and measures win rates, slot
advantage, match length, how matches end, pit KO share and per-hero action counts. It was built for the
2026-11-02 gameplay review (ruling 8b: "keep the matrix as a tool, wait for humans"). It measures; it asserts
no balance and tunes nothing.

| File | Role |
|---|---|
| `tests/Tools/FighterMatrixMeasureTests.cs` | The runner: one GdUnit case, `Measure`. A **no-op unless `FTT_MATRIX` is set**, so it adds one passing case to the suite and cannot hit the 300 s session timeout. |
| `tools/fighter_matrix/config.example.txt` | Example config (stage, difficulty, seeds, pair range, output file). |
| `tools/fighter_matrix/analyse.py` | Turns `matches.csv` into win matrices and tables. |

## Run

1. Copy `config.example.txt` to a scratch folder outside the repository and edit it. `out` is relative to the
   config file's folder; `matches.csv` and `progress.log` appear there. Rows are appended, so a run may be split
   over several calls (`pairfrom` / `pairs`, `seedfrom` / `seeds`).
2. A full stage is 81 pairs times the seeds, tens of minutes. The normal `.runsettings` has a 300 s session
   timeout, so make a copy with a longer one (the case itself allows 1,700 s):

   ```powershell
   (Get-Content .runsettings) -replace '<TestSessionTimeout>\d+</TestSessionTimeout>', '<TestSessionTimeout>1800000</TestSessionTimeout>' |
       Set-Content "$env:TEMP\measure.runsettings"
   ```

3. **Hold the GdUnit lock** (`CLAUDE.md`, failure signature 5): `mkdir D:/Projects/.ftt-gdunit-lock`, run, then
   `rmdir` it, also on failure. Never run it beside another Godot or `dotnet test` launch.
4. Run only this suite:

   ```powershell
   $env:GODOT_BIN = "D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe"
   $env:FTT_MATRIX = "D:\scratch\matrix\config.txt"
   dotnet test FightersThroughTime.csproj --settings "$env:TEMP\measure.runsettings" --filter "FullyQualifiedName~FighterMatrixMeasureTests"
   Remove-Item Env:FTT_MATRIX
   ```

   Read `Total:` (it must be 1) and `progress.log` (matches, ticks and ticks per second per call). A match that
   throws is listed in the failure message; the others are already in the CSV.
5. Analyse:

   ```powershell
   py tools/fighter_matrix/analyse.py D:\scratch\matrix\matches.csv
   ```

   It prints the tables and writes `win_matrix_<stage>_<difficulty>.md` and `analysis_output.txt` beside the CSV
   (or into `--out DIR`).

## Notes

- Seeds are `10000 + pair * 100 + seedfrom + k`, so two calls with disjoint `seedfrom` ranges extend one dataset.
- Both CPUs use the stock band for the difficulty (`CpuBandTuning.For`), with the hero's own recovery profile.
  Since GR-CPU the Hard band keeps its meter for the Ultimate above 70 Influence; a Hard run measures that.
- The stages are the four of the switch in `Measure`. Add a case there for another stage.
- Matches stop at a 70,000-tick cap and are recorded as `none` (excluded from win rates).
- The baseline numbers of 2026-11-02 (Florence, Paris, Nassau, Vesuvius at Hard and Normal) were taken before
  the Hard reserve, the Momentum retune, the running attack and the pit stages, so they are not comparable.
