# MM-SWEEP (Phase C) - retirements, data, scans

Branch `mm/sweep`, cut from main at `632c393b`. Plan section 3.2 "MM-SWEEP", 1.9, 1.8, 4.3, P1, P17-P19.

## Deleted (every one after a repo-wide `rg`)
- `BasicComboRules` D2 region: `MomentumRefundFramesFor`, `MomentumRefundFramesForHitbox`, `MomentumGrant`, `MomentumStringHitRefundFrames`, `MomentumDirectionalRefundFrames`, `MomentumFinisherRefundFrames`, `MomentumRefundCapFramesPerCycle`. Its only test (`AbilityLockoutRulesTests.MomentumRefundsTwentyPerHit...`) became `TheAirStrikeIsABasicStringHitbox`, keeping the one surviving assertion (no count change).
- `scripts/UI/DefySealModel.cs`, `tests/Unit/DefySealTests.cs` (-5) and their `.uid`; `EventBus.DefySealState`, `DefySealPayload`, `OnDefySealChanged`, `RaiseDefySealChanged`.
- `PlayerController.StoryDefyHistoryUsed` (dead getter) and the assertions on it in `StoryKillBoundaryTests`, `SuppressionTests`, `TutorialCalibrationV76Tests`.
- `FighterHudModel.DefyIconName` / `DefyIconTint`; their uses in `GlassKitContentTests` and `FighterHudLayoutRulesTests` (test renamed `EveryStatusHasADistinctKitIconAndTheModeCaptionHasAKey`).
- `KitReachRules.RequiemFortissimoShaveSeconds` (**value 2.0, for the DEFER ledger row**) and `RequiemFortissimoShaveFrames` (120).
- 310 `DefyHistoryUsed` renamed `SpareInt1` (never written; 310 still 128 B). `FighterCombatContractW3Tests` lost its two lines on it.
- `StoryAttemptState` legacy recovery no longer sets `DefyHistoryUsed = true`. The payload fields `StorySaveData.StoryDefyHistoryUsed` and `StoryAttemptState.DefyHistoryUsed` stay deserializable, never written. `SaveEnvelopeTests` (parked v5 payload) and `StoryAttemptStateTests` now expect false; the round-trip case that sets the dead fields true is unchanged.
- Theme: `DefyLabel` role and `DefyReadyGlow` palette entry removed from `tools/ui_theme/build_ftt_theme.py` and `UIPalette.cs`; `ftt_theme.tres` and `UIPalette.Roles.cs` regenerated (-4 lines each); `UIThemeTests` palette parity row dropped. The generator reports 72 roles (the plan's "70 -> 69" was stale: other workstreams added roles; no test pins a count).
- Icons: the four `defy_*` icons and bold twins (png, `.import`, `icons_src` and generated svg) deleted; `glass_svg.py` / `build_contact_sheet.py` entries removed; kit rebuilt with `build_all.ps1` (30 icons now; `glass_kit.json` -124 lines, `tools/asset_src/jobs.json` -72); `PulpIconTests` count 34 -> 30, `GlassKitContentTests` name floor 10 -> 6; the seeds in `UiCaptureTargets.Widgets.cs`.
- Keys `hud_defy_not_ready`, `hud_defy_ready`, `hud_defy_spent`, `hud_defy_unavailable`; `--import` run and `localization/en.en.translation` committed. `UnusedTranslationKeyTests` green (orphan ceiling unchanged).
- 18 Special `.tres`: `CooldownDuration = 0.0` explicit (P1). Per-hero content pins (`Cleopatra`, `Einstein`, `Joan`, `Leonardo`, `Lincoln`, `Mozart`, `Shakespeare`, `Tesla`, `Tubman` ContentTests, `KitAlignmentContractTests`) now expect 0f. Synthetic `new AbilityData { CooldownDuration = ... }` fixtures in the kit suites left alone (nothing reads them for Specials).
- `FighterLoadout.SpecialOneCooldownFrames` / `SpecialTwoCooldownFrames`, the two constructor args and the factory projection (ten test loadout builders and `FighterKnockdownTests` lost the arguments; the knockdown helper's unused `specialOneCooldownFrames` parameter went too).
- `CpuDecisionObservation.SpecialOneCooldownFrames` / `SpecialTwoCooldownFrames` renamed `SpecialOneReuseFrames` / `SpecialTwoReuseFrames` (mechanical, scripts and tests).
- **Kept:** the scoped `CooldownReduction` resolver branch: `BaseSpecial` still reads `StoryScoped(CooldownReduction, Data.AbilityID)` for the movement ability.

## Provided
- `FighterSimulation.SeedFighterRespawnForTest(int playerID, int frames)` (internal test seam, cross-edit to an MM-SIM file) and `FighterSimulationDriver.SyncDefyFeedback` made `internal` (cross-edit to an MM-FX file; was private).
- New tests: `DefySealRetirementScanTests` (1; pure; banned names assembled from halves, whole-word from the start of the name, C# comments stripped with a string-aware stripper; it skips its own file; test names are covered because `tests/` is scanned), `SpecialCooldownDataTests` (1; pure; 18 Special files each author `CooldownDuration` exactly once and 0), `InfluenceMeterSourceScanTests.NothingUnderTheFighterSimNamesAStoryOnlyMomentumOrScaleIdentifier` (+1; whole-word, so the shared `BasicComboRules.MomentumRegenUnitsPerFrame` in `FighterMomentumRules` does not trip `MomentumRegen`); `TouchedStoryFiles` lost `DefySealModel.cs`.

## Nit rework (orchestrator round 3), `DefyPresentationTests`
- `AStockResetClears...` (called `ResetDefyPresentation` directly) was replaced by two cases through the driver's sync path with a real `FighterSimulation`, for **both** players: `AStockLossDuringADefysRecoveryClearsBothPlayersMemorySoTheNextGenuineDefyPlays` (Defy presented, recovery state seeded, respawn seam, one sim tick zeroes 323, sync, release, a new Defy at the old counter value must ring) and `SuddenDeathEntryClearsBothPlayersMemory...` (1 s time-limit match advanced into Sudden Death through the countdown; both players re-baseline and ring again). Net +1.
- The five cases that returned silently when `InputManager.Instance` was null now `AssertThat(...).IsNotNull()` through `RequireInputManager()` (the first case and `RunDefyCounterCase`'s four callers).

## Test delta and evidence
- Plan: -2 (DefySealTests -5, +1 +1 +1). Mine: -5 +3 +1 (nit) = **-1**. Full run: `Failed: 0, Passed: 3873, Total: 3873` (2 m 15 s, lock held, orphans drained) = 3872 + 1 as briefed.
- Fail evidence: one mutation run with four mutations applied, then reverted: Joan Special 1 at `CooldownDuration = 3.0` (`SpecialCooldownDataTests` failed), a probe `.cs` naming a retired identifier (`DefySealRetirementScanTests` failed), `StoryCombatRules.MeterGainScale` named in a `scripts/FighterSim` file (`NothingUnderTheFighterSimNames...` and the older D5 scan failed), and the driver's three `ResetDefyPresentation` calls removed (both new `DefyPresentationTests` cases failed, with their own messages).

## Open item for the orchestrator (not mine to decide)
`FighterSpecialRecoveryCancelTests.NoDirectHitProjectileOrZoneOutlivesItsSlotsEarliestRecast` asserted that no DirectHit shot or zone outlives its slot's earliest re-cast (cooldown less the momentum cap). With a 120-frame reuse delay four slots can: **einstein S1 (314 frames), leonardo S1 (312), tesla S2 (314), mozart S1 (209)**. An earlier cast's hit landing during the next cast's recovery could arm D3's landed-hit flag for the wrong cast (Fighter sim; Story keeps `SourceExecutionSerial`). I did not change behaviour: the case is now `OnlyTheKnownDirectHitSlotsCanOutliveTheReuseDelay`, a tripwire pinning exactly those four. Suggest a ledger row (`VERIFY-DIRECT-HIT-ATTRIBUTION-OVERLAP`) and a ruling on whether to stamp the cast serial in the sim.

## Commands
`dotnet build FightersThroughTime.csproj --nologo` (0 errors, only CS8632; no KLSG_ECS004), `--headless --quit` exit 0 and `--import` exit 0 with no error or missing line, `build_all.ps1` (it holds the lock itself), one full `dotnet test` under the lock.

## Reference scan (final)
`rg "DefySeal|MomentumRefund|MomentumGrant|DefyIconName|DefyIconTint|RequiemFortissimoShave|DefyLabel|DefyReadyGlow|hud_defy_|defy_(ready|notready|spent|barred)|SetStoryDefyHistoryUsed|ClearDefyProtection|DefyProtectionFrames"` over `scripts scenes resources tests tools` (excluding the scan's own file): no matches. Remaining by design: `StoryDefyHistoryUsed` / `DefyHistoryUsed` as dead save data (`SaveManager`, `SaveEnvelope`, `StoryAttemptState`, their tests), `tutorial_step_defy_spent` (a live Level 0 key).

## For the docs
- `STORY_PERSISTENCE`: the two Defy fields are dead data, never written, including by the legacy recovery.
- Ledger: the Requiem -> Fortissimo shave value 2.0 s (DEFER row); the open item above; `FighterLoadout` no longer carries Special cooldowns; the Special `.tres` author 0 explicitly and the class default is 10 s (P1).
- AGENTS.md: the glass kit is 30 icons; the Defy kit icons, `DefyLabel` and `DefyReadyGlow` are gone; 310's third int is `SpareInt1`.

## Nobody played the build
Headless suites and one windowless kit rebuild only.
