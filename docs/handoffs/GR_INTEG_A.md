# GR-INTEG-A: Phase A integration

Worktree `D:/Projects/ftt-wt/gr-integ`, branch `gr/integ`, from `0973f6b9`. Each merge `--no-ff`, in plan order. Every build: 0 errors, `CS8632` the only warning, no `KLOTHO_DET002`.

| # | Branch | Merge SHA | Result line |
|---|---|---|---|
| 1 | gr/mom | 767beeeb | Failed: 0, Passed: 4030, Total: 4030 |
| 2 | gr/tf | 74ae42e2 | Failed: 0, Passed: 4038, Total: 4038 |
| 3 | gr/sched | ed51c75a (+ translation 1d6b1054) | Failed: 0, Passed: 4043, Total: 4043 |
| 4 | gr/crowd | 3625fcf9 (+ translation eafb26aa) | Failed: 0, Passed: 4059, Total: 4059 |
| 5 | gr/rank | 0a4f0bb9 (+ translation faae0175) | Failed: 0, Passed: 4089, Total: 4089 |
| 6 | gr/haz | e390d0ab | Failed: 0, Passed: 4112, Total: 4112 |
| 7 | gr/traits | 4c8b0a36 | Failed: 0, Passed: 4135, Total: 4135 |
| 8 | gr/run | e3fe2d7a (+ translation 7de480a1) | Failed: 0, Passed: 4161, Total: 4161 |

Final head `7de480a1`, re-verified with a final build and full run: `Failed: 0, Passed: 4161, Total: 4161`.

## Conflicts
- Crowd, Rank, Run: `localization/en.csv`, same tail hunk. Resolved as a union (markers removed, every block kept in merge order: GR-SCHED, GR-CROWD, GR-RANK, GR-RUN).
- Run: `scripts/Combat/BasicComboRules.cs`, two comment-only hunks on `MomentumGainUnitsFor` and `MomentumGainUnitsForHitbox`. Mom rewrote the figures as pips (0.2 / 0.05) where Run wrote units (1440 / 360) and added "the running attack". Kept Mom's pip wording and added Run's "running attack". No code differs.
- Tf, Haz, Traits merged without conflict. `HazardMobRules.FodderEnemyID` is `chrono_swarmling`, as Crowd defines it.

## Translation
`en.csv` changed in Sched, Crowd, Rank and Run. After each, `--import` under the lock, committing only `localization/en.en.translation`. The six owed keys are in the csv and the compiled translation. The `.import` and `default_bus_layout.tres` churn was discarded each time.

## GR-INTEG fixes
None needed. No test fixture or assertion was changed, and the case count is exactly the plan's 4161.

## Notes
No failures, no cold-cache reruns, no orphan processes found at start.
