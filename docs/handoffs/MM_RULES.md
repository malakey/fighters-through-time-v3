# MM-RULES handoff (meter and Defy update, Phase A)

Branch `mm/rules` (worktree `D:/Projects/ftt-wt/mm-rules`), cut from `56dbc3e4`. Tag `mm-rules`.

## 1. Shipped (exact names)

Commit 1 `d22dec33`: `scripts/Combat/BasicComboRules.cs`, two regions after the ability-lockout region.
- Momentum: `MomentumUnitsPerPip` 7200, `MomentumMaxPips` 4, `MomentumMaxUnits` 28800, `MomentumStartPips` 2, `MomentumStartUnits` 14400, `MomentumSpecialCostUnits` 7200, `MomentumRegenFramesPerPip` 720, `MomentumRegenUnitsPerFrame` 10, `MomentumStringHitGainUnits` 360, `MomentumDirectionalGainUnits` 360, `MomentumFinisherGainUnits` 1440, `SpecialReuseDelayFrames` 120, `MomentumGainUnitsFor(int variant, int comboStep)`, `MomentumGainUnitsForHitbox(string)`. The ledge attack reaches the sim as `VariantChain` step 0 and pays 360.
- Defy: `DefyInfluenceCost` 50, `DefyInvulnerableFrames` 6, `DefyBlastFrame` 6, `DefyRecoveryFrames` 24, `DefyTotalFrames` 30, `DefyBlastRadiusUnits` 2.5 (double), `DefyBlastStaggerFrames` 30, `DefyBlastHitstopFrames` 8, `DefyBlastPushXUnitsPerSecond` / `...YUnitsPerSecond` 6 / 6, `DefyHealFloorNumerator/Denominator` 1 / 10, `DefyHealPoints(missingHP, echoPoolRounded, maxHP)`.
- `scripts/FighterSim/FighterMomentumRules.cs` (public static, pure ints; the caller owns storage): `UnitsPerPip`, `MaxUnits`, `SpecialCostUnits`, `RegenUnitsPerFrame`, `StartUnits`, `SuddenDeathUnits`, `Clamp(int)`, `CanAfford(int)`, `Spend(int)`, `Gain(int units, int gainUnits)`, `Regen(int)`. They take and return `int` (not `ref verb`) because 310's `MomentumUnits` rename is MM-SIM's.

Commit 2 `c0446b75`: 305 `SpecialOneCooldownFrames/SpecialTwoCooldownFrames` renamed `SpecialOneReuseFrames/SpecialTwoReuseFrames` on `FighterRuntimeComponent` and every reader (152 sites, 39 files). Still armed from the authored cooldown. The 306 tuning fields, the loadout fields, `CpuDecisionObservation` and the `FighterLockoutRefusalRules` snapshot structs keep their old names (MM-SIM deletes 306 and the loadout fields; MM-CPU and MM-SIM own the other two).

Commit 3 `d897e6df`: `InfluenceMeterRules.StockLossRetention*` 1/2 (float view 0.5f); `StoryCombatRules.MeterGainScale` 0.5f; new `StoryCombatRules.FlatEffectScale` (= `MeterGainScale`) and `FlatMeterScale(bool storyScaled)`; deleted `PlayerController.ApplyStockLossMeterRetention`, `UltimateMeter.ApplyStockLossRetention`, `UltimateMeter.StockLossRetention`.

Commit 4 `f06b4f28`: `GameplayButtons.Defy = 1 << 15` (no free wire bit remains); `RollbackInputPacket.ProtocolVersion` 5.

Commit 5 `dc8b40d1`: 323 `DefyFrames` appended (36 to 40 B) with `ActionFlagDefyBlastFired` (32), `ActionFlagDefyBuffered` (64), `DefyCounterShift` 12, `DefyCounterMask` 0xF000; `FighterActionKind.Defy = 13`; `CharacterState.Defying` (appended after `Thrown`); `EventBus.DefyExecutedPayload { PlayerIndex, Position, MeterBefore, TargetsBlasted, Healed, Frame (ulong) }`, `OnDefyExecuted`, `RaiseDefyExecuted`; `PlayerController.Momentum.cs` (`MomentumPips` 2.0, `RestoreMomentumPips`, `IsSpecialReady(AbilitySlot)`, `SpecialReuseRemainingSeconds(AbilitySlot)`, `MomentumRegenMultiplier` 1); `PlayerController.Defy.cs` (`IsDefying` false, `DefyExecutionCount`, `CanDefyNow` false, `partial void PresentDefyStart()`, `partial void PresentDefyBlast(in DefyExecutedPayload)`); `FighterSimulationDriver.Momentum.cs` (`TryGetMomentum(int, out int)` returns `MomentumStartUnits`). The new `.uid` sidecars are in this commit.

## 2. Files changed outside the brief (cross-edits)
None beyond the brief. The rename touched `UiCaptureTargets.Hudf.cs`, `MirrorParadoxController.cs`, `FighterHUD.cs`, `TestArenaHUD.cs`, the driver and 25 test files for the compile.

## 3. Decisions
- `FighterMomentumRules` takes ints rather than `ref FighterVerbComponent` (MM-SIM passes `verb.MomentumUnits`).
- `IsSpecialReady` stub reads only the reuse timers and is false for non-Special slots.
- `InfluenceMeterSourceScanTests`: added `FlatEffectScale` / `FlatMeterScale` to the sim's forbidden-name list (MM-SWEEP still owns the extra case).
- `StockLossRetention` float view stays in `InfluenceMeterRules` (the sim-view scan pins it as forbidden for the sim).

## 4. Deviations
None. Commit 3's "pins" also changed `FighterSimulationTests` (18.75 to 12.5, test renamed `StockLossRetainsHalfOfInfluence`) and `InfluenceMeterRulesTests` (the sim-view bit-for-bit case now compares against `value * 1 / 2`).

## 5. Tests
- New: `MeterDefyRulesTests` 7 (pure); `PlayerInputFrameTests` +1 (`TheDefyButtonIsTheLastWireBitAndRoundTripsWithoutGrowingTheFrame`). Deleted: `UltimateMeterTests.StockLossRetainsSeventyFivePercent` (-1). Net +7. `[TestCase]` count 3771 to 3778.
- Rewritten in place: `InfluenceMeterRulesTests` (3/4 to 1/2, and the UltimateMeter forwarder line removed), `FighterSimulationTests.StockLossRetainsSeventyFivePercentOfInfluence` to `StockLossRetainsHalfOfInfluence`, `StoryMeterGainScaleTests` (0.35 to 0.5; 0.385 to 0.55), `StoryMeterGainFactorTests` (the stock-loss lines removed), `RosterSwapProtocolTests` (v5, renamed `TheProtocolIsVersionFiveAndRefusesAVersionThreePacket`), `FighterActionStateTests` (ten fields, 40 B pin via `Unsafe.SizeOf`), `InputBindingSchemaTests` (+ the Defy bit pin line), `InfluenceMeterSourceScanTests`, and every 305-name reference. The other Story suites named in the plan read the constants and needed no change.
- Seen failing: one mutation run (regen 11, finisher gain 1000, blast frame 7, heal denominator 9, Defy bit 1<<10, protocol 4, an extra 323 int, stock loss 3/4, scale 0.35) failed 13 cases: all 5 of the five non-trivial `MeterDefyRulesTests` cases, `PlayerInputFrameTests` Defy case, `RosterSwapProtocolTests`, `FighterActionStateTests` (size), `InfluenceMeterRulesTests` x2, `StoryMeterGainScaleTests`, `FighterSimulationTests` stock loss, `InputBindingSchemaTests`. (`SpawnAndSuddenDeath...` case is the one the mutations did not reach; its values are pinned to the constants.) All reverted.
- Filtered: first commit-3 run 146/146; commit-5 run 162/162 (MeterDefyRules, InfluenceMeter, StoryMeterGain, UltimateMeter, PlayerInputFrame, RosterSwapProtocol, RollbackProtocol, RollbackReadiness, FighterSimulation, FighterActionState, MirrorParadox, StoryRally, InputBindingSchema).
- Full run at `dc8b40d1`: `Failed: 1, Passed: 3777, Total: 3778` (2 m 13 s, lock held). The one failure was a missed stock-loss pin, `FighterCombatContractW3Tests.AStockLossRespawnsAtFullHPWithBuffsAndMarksClearedButCooldownsAndDefyKept` (Influence 80 now retains 40, was 60), fixed in the handoff commit (rewritten in place, 0 delta); that suite plus FighterSimulation, RollbackReadiness and RollbackProtocol then passed 57/57. A second full run was not repeated, so the 3778 all-green line is not shown.

## 6. Measurements
Seeded replay (a throwaway probe, not committed: seeds 781 and 1234, 900 ticks of the RollbackProtocolTests script):
- commit 1 (before the rename): 781 `4956633786832517961`, 1234 `-2134670193170679122`
- commit 2 (after the rename): identical to both.
- commit 5: 781 `-487260296604438434`, 1234 `-2046603802659616711` (moved: stock loss 1/2 and the 323 field).

## 7. Not delivered / open
Everything behavioural is MM-SIM and later. Nobody played the build.

## 8. For the docs
Design lines owed are in the plan; this workstream adds: protocol v5 (no free `GameplayButtons` bit, `DEFER-INPUT-WIRE-FULL`), 323 now 40 B, `StockLossRetention` 1/2 (`USER-STOCK-LOSS-RETENTION-HALF`), `MeterGainScale` 0.5 (`USER-STORY-METER-SCALE-HALF`), `FlatEffectScale` (`USER-STORY-FLAT-EFFECTS-SCALED`).
