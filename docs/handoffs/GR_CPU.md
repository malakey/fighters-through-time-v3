# GR-CPU handoff (gameplay review 2026-11-03; rulings 9d, 9d+, 8b)

Branch `gr/cpu`. **Nobody has played any of it**; every claim is a headless suite or a two-match smoke of the tool.

## What shipped

**The Hard CPU meter reserve (9d).** In `scripts/FighterSim/FighterCpuController.cs`:
- `CpuBandTuning.DefyReserveInfluence` (Hard **70**, every other band 0 = off) and `CpuBandTuning.DefyNearDeathHPPercent` (Hard **25**). Code-owned in the band, never a resource.
- `FighterCpuController.DefyAllowedByReserve(in CpuDecisionObservation)`, beside `DefyLegalForSelf`. Reserve off, or Influence under it: passes. At or above it: passes only when the opponent is in an A02 wind-up within 3.0 u (`TargetUltimateWindupInDefyRange`, same range test as the neutral trigger, frames left not judged) or own HP is at or below the near-death percent (`SelfCurrentHP * 100 <= pct * SelfMaxHP`, false when `SelfMaxHP` is 0).
- Called in `UpdateDefyReflex` after `DefyLegalForSelf` and **before** the RNG gate (Hard takes no roll anyway, so no stream shift; a test pins that a reserved string equals a no-Defy Hard frame for frame) and in the corner branch of `TryDecideNeutralDefy`. The wind-up branch of `TryDecideNeutralDefy` never consults it, so an A02 wind-up always passes.

**The Mirror exemption (9d+).** New band `CpuBandTuning.HardBoss = Hard with { DefyReserveInfluence = 0, DefyNearDeathHPPercent = 0 }`; `BossOverride(Hard)` returns it instead of `Hard` (Easy and Normal unchanged). The Level 13 clone therefore behaves exactly as before. `HardBoss` equals `Hard` apart from the two reserve fields (pinned).

**The matrix tool (8b).** `tests/Tools/FighterMatrixMeasureTests.cs` (namespace `FTT.Tests.Tools`, one case `Measure`), from the scratch `ZzFighterMatrixMeasureTests`. It **returns at once unless env var `FTT_MATRIX` names a config file**, so it is one passing case in a normal run and cannot touch the 300 s session timeout. Scratch paths are gone: the config file is the env var's value, `out` is relative to the config file's folder and `progress.log` is written beside the CSV; `#` comments and blank lines are accepted; a set variable naming a missing file fails loudly. `tools/fighter_matrix/` holds `config.example.txt`, `analyse.py` (now `analyse.py <matches.csv> [--out DIR]`, no hard-coded folder, the dead combined-matrix stub dropped) and `README.md` (run command, longer-timeout runsettings recipe, lock rule). Smoke: a 2-pair, 1-seed Florence Hard call through `FTT_MATRIX` wrote 2 rows and `analyse.py` read them.

## Tests (declared +6: policy 5, tool 1; actual +6)
- New in `CpuDefyPolicyTests` (8 → 13): `HardKeepsASeventyInfluenceReserveWithA25PercentNearDeathExceptionAndNoOtherBandDoes`, `HardBanksItsMeterAtTheReserveInsteadOfDefyingAStringAndSpendsItJustBelow`, `HardDefiesAtTheReserveOnlyWhenOwnHpIsAtOrBelowTwentyFivePercent`, `TheReserveBlocksTheCornerDefyAtSeventyAndNeverTheUltimateWindupOrAnUnreservedBand`, `TheMirrorCloneOnHardKeepsDefyingStringsAtAFullMeterBecauseItsBandHasNoReserve`. Seen failing with Hard's reserve temporarily set to 0 (5 failed, 8 passed), then green.
- New suite `FighterMatrixMeasureTests` (1 case).
- In place, no count change: `CpuDefyPolicyTests` Hard string, corner, daze and 3-unit cases run at a new `BelowReserve` = 60 Influence (wind-up cases stay at 100, which proves the wind-up passes); `FighterCpuHitstunDefenseTests.TheDefyReflexWinsItsTickOverTheBlockHoldAndTheHoldResumesAfterIt` (a Hard pin outside the declared list) moved from 100 to 60 Influence.
- Filtered run: `CpuDefyPolicyTests`, `FighterMatrixMeasureTests`, `FighterCpuHitstunDefenseTests`, `MirrorParadoxTests`, `FighterCpuSpecialTimingSmokeTests`, `CpuVerbPolicyTests`, `FighterCpuBehaviorTests`, `CpuRecoveryMatrixTests`, `CpuSelfActionStateTests`, `CpuMeleeBandTests`, `CpuRecoveryProfileTests`, `FighterAbilityLockoutTests`, `FighterCpuControllerTests`, `FighterHitConfirmTests`, `FighterLedgeTests`: `Passed! Failed: 0, Passed: 197, Total: 197`.

## Effects
Hard practice CPU matches change (it keeps 70+ meter instead of Defying strings and corners); every seeded Hard-CPU match hash moves. No sim component, snapshot field, protocol, save or translation change; no `KLOTHO_DET002`. Build: 0 errors, `CS8632` only.

## Provisional numbers
70 reserve and 25 % near-death are the dossier's recommendation (ruling 9d said "about 70" and "near death"), `VERIFY-CPU-DEFY-ULTIMATE-STARVE` stays open: Hard banks to 100 and may still not convert (the dossier suggested a time cap; not built, no ruling).

## Deviations
None from the plan. Note the plan and dossier assumed the reserve gate in `UpdateDefyReflex` "before the RNG gate" for Hard; Hard takes no roll, so the placement matters only for Medium-style bands, which have no reserve.

## What did not ship
No balance change, no re-run of the matrix (rule 8b: the tool is kept, humans first), no CPU Ultimate-conversion help.

## What the docs owe (GR-DOCS)
- AGENTS.md: the CPU Defy paragraph ("Hard no roll ... otherwise banks its meter") gains the 70 / 25 % reserve, the A02 exception and the `HardBoss` band; the Mirror paragraph says the clone's Hard band has no reserve.
- CLAUDE.md: test baseline arithmetic (+6), the verified-commands section gets the matrix tool (`FTT_MATRIX`, `tools/fighter_matrix/README.md`) and a note that bot/CPU matrix numbers from before and after are not comparable for Hard.
- Ledger: new `USER-HARD-CPU-METER-RESERVE` (9d) and `USER-MIRROR-EXEMPT-FROM-CPU-RESERVE` (9d+), `PROVISIONAL-CPU-DEFY-RESERVE` (70, 25 %), keep `VERIFY-CPU-DEFY-ULTIMATE-STARVE` open; `VERIFY-FIGHTER-FIRST-SLOT-WINRATE` cites the tool.
