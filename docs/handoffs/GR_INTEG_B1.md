# GR-INTEG-B1: Phase B1 integration

Worktree `D:/Projects/ftt-wt/gr-integ`, branch `gr/integ`, from `946db4ab`. Each merge `--no-ff`, in plan order. Every build: 0 errors, `CS8632` the only warning, no `KLOTHO_DET002`. Every full run exit 0, no orphan processes, lock held and released.

| # | Branch | Merge SHA | Translation commit | Result line |
|---|---|---|---|---|
| 1 | gr/cpu | a395dd08 | none (no en.csv change) | Failed: 0, Passed: 4171, Total: 4171 |
| 2 | gr/stage | b88cdfbb | 96406553 | Failed: 0, Passed: 4196, Total: 4196 |
| 3 | gr/audio | 3177cd9c | fef24b2c | Failed: 0, Passed: 4212, Total: 4212 |
| 4 | gr/train | c557a2b3 | 3768236a | Failed: 0, Passed: 4226, Total: 4226 |
| 5 | gr/cap | cb80a142 | 6d699556 | Failed: 0, Passed: 4240, Total: 4240 |
| 6 | gr/pilot | fbf65142 | 9d411044 | Failed: 0, Passed: 4269, Total: 4269 |

Audio landed +16 (declared +10) and pilot +29 (declared +27), as the brief expected; every total equals the table.

## Conflicts
- Train, Cap, Pilot: `localization/en.csv`, the same tail hunk each time. Resolved as a union (markers removed, every block kept in merge order: GR-RUN, GR-AUDIO, GR-TRAIN, GR-CAP, GR-PILOT). Nothing dropped.
- Cpu, Stage, Audio merged without conflict (Stage changed two layout rows in place, Audio appended `settings_mute_placeholder_cues`).
- No conflict in `BossPhaseRuleMatrixTests`, `PlayerController.cs`, `SaveManager.cs` or `EnemyController.Hazard.cs`.

## Translation
`en.csv` changed in Stage, Audio, Train, Cap and Pilot. After each, `touch localization/en.csv`, `--import` under the lock, committing only `localization/en.en.translation`. Owed keys present: `stage_berlin_layout`, `stage_globe_layout`, `settings_mute_placeholder_cues`, the `# GR-TRAIN` block, the `# GR-CAP` block, `orleans_room_rampart`. `.import` and `default_bus_layout.tres` churn discarded each time.

## GR-INTEG fixes
None needed. No fixture or assertion changed; no count change beyond the merged branches' own.

## Notes
The matrix tool's case in `tests/Tools/` (`FighterMatrixMeasureTests`) ran as a no-op with `FTT_MATRIX` unset. Run time grew 2 m 41 s to 3 m 29 s across the six merges, in line with +98 cases.
