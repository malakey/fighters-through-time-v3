# GR-MOM handoff (gameplay review package, 2026-11-03)

Branch `gr/mom`. Rulings 10a and 10a+ (Momentum regen 9 s per pip, both modes); plan section 1.4. **Nobody has played or looked at any of it**; every claim is a headless suite.

## What shipped
- `BasicComboRules` Momentum region: `MomentumUnitsPerPip` 7200 -> **5400**, `MomentumRegenFramesPerPip` 720 -> **540**, `MomentumRegenUnitsPerFrame` 10 (unchanged: the unit changed, not the rate, because 7200/540 = 13.33). The gains now derive from the unit: `MomentumStringHitGainUnits` and `MomentumDirectionalGainUnits` = `MomentumUnitsPerPip / 20` (270), `MomentumFinisherGainUnits` = `MomentumUnitsPerPip / 5` (1080). Max 21,600 and start 10,800 were already derived (`MomentumMaxPips` x unit, `MomentumStartPips` x unit).
- Everything else already read the constants: `FighterMomentumRules` (`StartUnits`, `MaxUnits`, `SpecialCostUnits`, `RegenUnitsPerFrame`), Story `PlayerController.RegenMomentum` (`60 / MomentumRegenFramesPerPip` pips a second: 0.111, no code change), `LingeringKitRules.MomentumUnitsFor` (now 1080 / 270 / 270), `FighterHudModel.MomentumPipFill`, the CPU's `MomentumSpecialCostUnits`, the Mirror adapter, `PlaytestWorld.MomentumRegenPerTick`.
- Comments with the old numbers updated in `PlayerController.Momentum`, `LingeringKitRules`, `CpuDecisionObservation`, `FighterSimulationComponents`, `FighterSimulationSystems`, `FighterMomentumRules`, `FighterHudModel`, `PlaytestWorld`, `PlaytestCombatStats`.
- `UiCaptureTargets.Hudf.cs`: the two literal capture fills (18720 and 2880 units, 2.6 and 0.4 pip) became `MomentumUnitsPerPip * 26 / 10` and `* 4 / 10`, so the capture keeps its pip levels.

## Tests
- New (+2, both in `MeterDefyRulesTests`): `AGainReviewPipIsFiftyFourHundredUnitsAndRegeneratesInNineSeconds` (5400, 540, 10, 9 s, max 21,600, start 10,800, sim start) and `TheGainsStayFiveHundredthsAndAFifthOfAPip` (270 / 270 / 1080 and x20 / x5 = a pip). These are the only absolute pins of the new numbers.
- In place, literals replaced by constants (no count change): `MeterDefyRulesTests` (rest), `FighterMomentumGaugeTests` (comment), `StoryMomentumGaugeTests` (`1/720`, `60/720`, the 12 s test renamed `...EveryNineSeconds...`), `LingeringKitRulesTests`, `FighterLingeringKitRulesTests`, `OwnerHitSeamTests`, `FighterHitConfirmTests` and `FighterAirAttackTests` (comments), `StoryHitConfirmTests`, `StoryAirAttackTests`, `StoryDefyBlastTests` (comment), `FighterHudSceneTests` (`using FTT.Combat`, pip-based fills), `CpuVerbPolicyTests` (comment), `PlaytestCombatStatsTests`, `RollbackReadinessTests` (comment only).
- Declared +2, actual +2. Filtered runs: 15 suites, `Passed! - Failed: 0, Passed: 141, Total: 141` (141 = the exact `[TestCase]` count of those files); broader (`RollbackReadiness`, `Cpu`, `Defy`, `Special`, `Playtest`, `Momentum`, `KitTests`, `Lingering`, `Ultimate`): `Failed: 0, Passed: 757, Total: 757`. Build 0 errors, only the known `CS8632`.
- `RollbackReadinessTests`' spam-script thresholds needed no change (green as is); the brief's "more pips may need re-tuning" did not bite.

## Effects
- Every Fighter hash moves (regen is 33 % faster per pip, gains are smaller in units). No suite pins an absolute hash. No snapshot size, component, save (Story saves pips, `AbilityCooldowns["momentum"]`) or protocol change. Old and new builds would desync on LAN (de-scoped).
- Bots: Specials a minute rise (the bots' 4.93 a minute figure is stale); bot batches from before and after are not comparable for Momentum, `momentum` block and Special counts.

## Deviations
None. Gains were written as `unit / 20` and `unit / 5` instead of the literals 270 / 1080 so they derive from the one unit; the values are identical and pinned.

## Provisional numbers
None new: 9 s is the user's ruling (10a+); 5400 is the plan's unit choice.

## What the docs owe (GR-DOCS)
"1 pip per 12 s" becomes 9 s in AGENTS.md (Combat, Momentum bullet; Klotho 310 row: 0-28,800 / 7200 units, start 14,400), CLAUDE.md (Momentum mentions), the ledger (`USER-MOMENTUM-REGEN-9S`, amends `USER-MOMENTUM-GAUGE` and `PROVISIONAL-MOMENTUM-RULES`: unit 5400, regen 540 frames, gains 270 / 270 / 1080), contract A15, the design master and mirror. `systems_card_momentum_body` states no seconds, so no copy change. Baseline after this workstream: 4030.
