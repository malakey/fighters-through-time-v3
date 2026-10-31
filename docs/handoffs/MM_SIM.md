# MM-SIM handoff (meter and Defy update, Phase B1): the Fighter gauge and the active Defy

Branch `mm/sim` (worktree `D:/Projects/ftt-wt/mm-sim`), cut from `9cba2b9a` (the merged `mm-rules`). Four commits in the plan's binding order:

| # | SHA | What |
|---|---|---|
| 1 | `8897dd93` | the gauge, the reuse delay, regen, landed-swing gains, Sudden Death, stock loss, the not-yet classifier, the 306 and intent cooldown deletions, the Requiem no-op |
| 2 | `d5ee50a7` | the passive Defy deleted |
| 3 | `980365f9` | the active Defy: press, buffer, clock, legality, claim |
| 4 | `5ae7d27b` | the blast and the heal |

Commit 3 holds the three cases of `FighterDefyTests` that need the blast's freeze (the clock timeline, the airborne hold, the fires-once case) out; commit 4 adds them with the blast, so every commit builds and passes its own suites.

## 1. Shipped (exact names)

Gauge (commit 1)
- `FighterVerbComponent.MomentumUnits` (310; was `MomentumRefundedFramesSlotOne`), `FighterVerbComponent.SpareInt0` (was `...SlotTwo`). `DefyHistoryUsed` is still there, dead (never written), for MM-SWEEP.
- `SpawnFighter` writes `FighterMomentumRules.StartUnits` (14 400). `TickCounters(..., bool regenMomentum)` adds `FighterMomentumRules.Regen` (10 a frame) in every branch that ticked the cooldowns, only while `FighterMatchComponent.MatchState == InProgress` (P2, read once per tick in `FighterMovementSystem.Update` as `matchLive`). Hitstop ticks skip it (the cooldowns' own rule).
- `FighterAbilityEntitySystem`: a Special press needs `FighterMomentumRules.CanAfford(verb.MomentumUnits)` and its slot's `SpecialOneReuseFrames` / `SpecialTwoReuseFrames` at 0; acceptance `Spend`s a pip and arms `BasicComboRules.SpecialReuseDelayFrames` (120).
- `OnSwingResolved(..., int momentumGainUnits)` adds `BasicComboRules.MomentumGainUnitsFor(variant, step)` through `FighterMomentumRules.Gain`; the four callers (ledge attack, air strike, directional, chain) pass it. `ApplyMomentumRefund` is gone.
- Sudden Death sets `MomentumUnits = FighterMomentumRules.SuddenDeathUnits` and clears both reuse delays and the movement cooldown (O22). A stock loss touches neither the gauge nor the delays.
- `LockoutRefusalBaseline` gained `MomentumUnits`; its fields are renamed `SpecialOneReuseFrames` / `SpecialTwoReuseFrames`. `FighterLockoutRefusalRules.Evaluate` flashes a press the lockout alone refused (as before) or a press made under one pip, with the slot's reuse clear (P14); the gauge it judges is the one the ability system read (`before + 10` on an unfrozen tick, `before` on a freeze's last tick).
- Deleted: 306 `SpecialOneCooldownFrames` / `SpecialTwoCooldownFrames` and their spawn copy; `AttackIntent.CooldownFrames` and its constructor parameter.
- `FighterKitMotion.ShaveFortissimo(ref FighterRuntimeComponent)` is a documented no-op (signature kept so the call site stays). `KitReachRules.RequiemFortissimoShaveSeconds` / `...Frames` untouched (MM-SWEEP).
- Seams: `FighterSimulation.SeedFighterMomentumForTest(int playerID, int units)`, `SeedFighterSpecialReuseForTest(int, int, int)` (renamed from `SeedFighterSpecialCooldownsForTest`), `tests/Determinism/MomentumTestKit.cs` (`KeepFull`, `KeepFullBoth`, `Units`).
- Driver: `FighterSimulationDriver.TryGetMomentum(int playerID, out int units)` reads `FighterVerbComponent.MomentumUnits` (false with no simulation or an unknown player). The proxy-feed lines (`SpecialOneCooldownTimer` from `SpecialOneReuseFrames` / 60) already read the renamed fields; nothing else needed.

Passive Defy deleted (commit 2)
- `FighterDefenseRules` now holds `GrantedShieldLifetimeTicks`, `TickBarrier(ref defense, bool suspended)`, `ClearBarrier`, `Clear` (Aegis and barrier only). Gone: `DefyProtectionTicks`, `IsDefyProtected`, `BeginDefyProtection`, `Tick`, `EndOnOffence`, `CanDefy`, `IsDefyUnspentAndUnbarred`, `Seal`.
- 312 `FighterDefenseComponent` lost `DefyProtectionAwaitControl`, `DefyProtectionFrames`, `DefyProcIdentity`, `DefyBarred` (36 B to 20 B). The lethal branches in `ApplyFighterHit` and `ApplyUnattributedDamage`, the `defied` echo and meter special cases, the protection gates (`ApplyFighterHit`, `ApplyUnattributedDamage`, `FighterKitRules` x2, `FighterUltimateActivationRules.IsAvoided`, the cinematic's victim test), the Sudden Death bar, `IsDefyActionable` and `SeedFighterDefyProtectionForTest` are gone. `FighterActionRules.OnActionAccepted` keeps its `defense` parameter, unread (22 call sites); the Defy start rides `InvulnerabilityFrames`.

Active Defy (commit 3)
- `FighterDefyRules` (public static): `CostRaw` (50), `RadiusSquaredRaw` (6.25), `PushXRaw`, `PushYRaw`, `IsDefying`, `IsInvulnerable`, `IsStartup`, `BlastFiresThisTick`, `Counter`, `CanBegin(fighter, action, activation, matchLive)`, `Begin(...)` (internal), `Advance`, `Clear`, `IsInterrupted`, `ClearIfInterrupted`.
- `FighterMovementSystem.Defy.cs`: `ReadDefyPress` (called right after `UpdateJumpLatch`, above the hitstop gate), `BeginBufferedDefy` (the thaw tick), `BeginDefy` (releases a grab on either side, calls `FighterActionRules.OnActionAccepted(..., FighterActionKind.Defy)`), `ProcessDefy` (called before the knockdown branch).
- 323 fields used: `DefyFrames`, `ActionFlagDefyBlastFired`, `ActionFlagDefyBuffered`, the counter in bits 12 to 15 (stepped once per Defy in `Begin`, kept when the Defy ends). A captured fighter's Defy is cleared in the captured branch.
- `FighterAbilityLockoutRules.CanReadAbilityPress` refuses while `IsDefying`; the ledge capture refuses while Defying; the hitstop branch discards buttons while Defying.
- Seam `FighterSimulation.SeedFighterDefyForTest(int playerID, int defyFrames)`.

Blast and heal (commit 4)
- `FighterDefyBlastRules` (internal): `InRadius`, `Reaches`, `Heal`, `ApplyToTarget`. `FighterCombatSystem.DefyBlast.cs`: `ResolveDefyBlasts`, called at the top of `FighterCombatSystem.Update`, before `AdvanceGrabs`, `AdvanceUltimate` and every intent (P4).

## 2. Files changed

Production (all owned): `FighterSimulationComponents.cs`, `FighterSimulationSystems.cs`, `FighterEntitySystems.cs`, `FighterSimulation.cs`, `FighterSimulationDriver.Momentum.cs`, `FighterLockoutRefusalRules.cs`, `FighterKitMotion.Reach.cs`, `FighterActionRules.cs`, `FighterKitRules.cs`, `FighterUltimateActivation.cs`, `FighterAbilityLockoutRules.cs`; new `FighterDefyRules.cs`, `FighterDefyBlastRules.cs`, `FighterMovementSystem.Defy.cs`, `FighterCombatSystem.DefyBlast.cs` (each with its `.uid`).

Tests I own: new `FighterMomentumGaugeTests`, `FighterDefyTests`, `FighterDefyBlastTests`, `FighterDefenseLayerTests`, `MomentumTestKit` (not a suite); deleted `DefyProtectedRecoveryTests` (and its `.uid`).

Cross-edits (test files other workstreams may touch; each kept to the smallest change):
- `FighterLockoutRefusalRulesTests`: baseline field renames; `Ready()` carries the start gauge; +1 case.
- `FighterHitConfirmTests`, `FighterAirAttackTests`, `FighterTimeSystemsTests`, `RequiemCooldownShaveTests`, `FighterMatchFlowTests`, `FighterHitPipelineTests`, `FighterVerbLayerTests`, `FighterLedgeGetUpTests`: refund and passive-Defy cases rewritten or removed (section 5).
- Re-timed with `MomentumTestKit.KeepFull` (a second same-slot press, assertions unchanged): `CleopatraKitTests`, `FighterSimulationTests`, `TeslaKitTests`, `TeslaLorentzChainTests`, `TeslaUltimateTests`, `FighterKnockdownTests`. Cooldown-armed assertions rewritten to the 120-frame reuse delay: `DivinePiercingLungeTests`, `TubmanKitTests`, `FighterAbilityLockoutTests`. `RollbackReadinessTests`: see section 3.
- No change to the CPU, the Mirror, `CpuDecisionObservation` or any Story file.

## 3. Decisions

- **The loadout's two cooldown fields stay** (`FighterLoadout.SpecialOneCooldownFrames` / `...TwoCooldownFrames`, the positional constructor arguments, and the factory projection), now dead: deleting them touches the constructor call in ~16 test files that other workstreams also edit. The brief said delete 306 and the intent field only; MM-SWEEP deletes these with the `.tres` cooldowns (P1). The doc comment says so.
- **`matchLive` gates both regen (P2) and the Defy press**: a press outside a live match is refused (the countdown clears input anyway; the guard covers a finished match).
- **The Defier takes the 8-frame freeze even when nobody is in the ring**: the time-break is the Defy's, not the victim's, and it keeps the Defier's frame advantage constant. A reached target takes it too.
- **Stagger is `max(current, 30)`**, never shortened; the push sets `IsGrounded = 0` so the ground snap does not eat the upward 6 u/s; `HitstunBlockCancelBlocked = 1` so a held Block cannot escape it (it would not anyway, airborne).
- **Direction**: away from the Defier by x, a tie pushes right.
- **Buffered Defy** starts inside the hitstop branch when `HitstopFrames` reaches 0, instead of `ResolvePendingLaunch`; the stale buffer bit is cleared on any unfrozen tick.
- **Recovery** discards both buttons and the stick (`DiscardInput`), so a Defier falls straight down and cannot steer; the ledge capture is refused.
- **Interrupt set** for a running Defy is the Special cast's: hitstun, daze, a hold on it, a capture, a knockdown, out of stocks, the platform.
- **RollbackReadiness**: the spam scripts press Specials every cycle, which a two-pip start starves. Both peers and the replay are built with `MomentumTestKit.KeepFullBoth` in `BuildSimulation` (state before any session exists, so rollbacks keep it). A mid-run seed is wiped by the session's depth-7 corrections, so the worst-case timing run instead holds its scripts' Specials until the last 40 warm-up ticks (`ScriptedInputs(..., specialsFromTick)`), starting the window with four pips. Thresholds that counted cooldown-paced casts were lowered: 30 to 12 specials in 2200 ticks, and 20 to 5 before the snapshot (four pips plus about three regenerated per player).

## 4. Deviations from the plan

- **P5 is wrong in one detail.** A Defy started "1 or more ticks later" does NOT catch the earlier one in its recovery: the earlier Defier holds frame 6 and one invulnerable frame through its own 8-frame freeze, so it is invulnerable through press+13. A later Defy catches it only if its blast lands on press+14 or later, that is from a press 9 or more ticks after the earlier one. Simultaneous Defies still blast nothing (both invulnerable on 6). `VERIFY-DEFY-MUTUAL` should say so; `TwoDefiesOnTheSameFrameTouchNeither...` pins both halves (the second half with a press held through the first blast's freeze and begun on the thaw tick).
- Test delta is **+17, not +18**: `FighterVerbLayerTests` also lost `DefyHistorySavesExactlyOneLethalHitAtFullMeter`, a second passive-save case the plan did not list.
- The snapshot is smaller than P33's estimate in 312 and 306 (-16 B and -8 B a fighter) but 323's +4 B is per fighter (40 B), so about -40 B per snapshot in total as planned.

## 5. Tests

New (+36): `FighterMomentumGaugeTests` 11, `FighterDefyTests` 12, `FighterDefyBlastTests` 10, `FighterDefenseLayerTests` 2, `FighterLockoutRefusalRulesTests` +1.
Deleted (-19): `DefyProtectedRecoveryTests` 12 (the suite), `FighterTimeSystemsTests` 2 (`MomentumRefundArithmeticRespectsTheFloorAndTheCap`, `EveryConnectingStringHitRefundsARunningCooldownInMatch`), `RequiemCooldownShaveTests.AHittingExecutionShavesTwoSecondsOffFortissimoExactlyOnce`, `FighterHitPipelineTests.ALethalVenomTickInSuddenDeathRespectsTheDefyBar`, `FighterMatchFlowTests.ADualLethalTradeAtFullMeterFiresBothDefyProcs`, `FighterVerbLayerTests` 2 (`ADefiedHitGeneratesNoRallyEcho`, `DefyHistorySavesExactlyOneLethalHitAtFullMeter`).
Net **+17**: the `[TestCase]` count under `tests/` is **3795** (baseline 3778 at `9cba2b9a`).

Rewritten in place (before to after): `FighterHitConfirmTests` `EveryLandedBasicHitRefundsBothRunningCooldownsOncePerSwing` to `EveryLandedBasicHitAddsItsGaugeGainOncePerSwing`; `MomentumStopsAtOneHundredTwentyFramesPerCycleUntilTheSlotIsReArmed` to `TheGaugeStopsAtFourPipsWhateverALandedSwingAdds`; `BlockedCounteredAndInvulnerable...NeitherConfirmNorRefund...` to `...NeitherConfirmNorPayTheGauge...`; the Sudden Death, construct, Special-own-hit and `ResolveFighterHit` cases (gauge asserts replace the counters; the passive-Defy row is gone). `FighterAirAttackTests` air-strike gain (360 units). `FighterHitPipelineTests.VenomCannotKillThroughAFullDefyMeter` to `AFullMeterNoLongerSavesAFighterFromALethalVenomTick`. `FighterMatchFlowTests` / `FighterTimeSystemsTests` Sudden Death cases (gauge 2 pips, no `DefyBarred`). `FighterLedgeGetUpTests.TheLedgeAttackEndsRespawnAndDefyProtection...` to `...EndsRespawnProtectionAndKeepsOnlyItsOwnClimbFrames`. `TubmanKitTests` Foresight reuse case renamed to `...ArmsTheTwoSecondReuseDelay`. `FighterAbilityLockoutTests.ASpecialLocksTheOtherSlotAndItsOwnRecastForExactlyThirtyTicks` (the recast half clears the 2 s delay by seed so the lockout alone refuses). `RollbackReadinessTests` per section 3.

Seen failing:
- Gauge suite and the hit-confirm gains: one mutation run (no spend, S2 reuse 60, regen outside a live match, Sudden Death gauge 0, gains doubled, classifier ignoring the gauge) failed 9 cases across `FighterMomentumGaugeTests`, `FighterHitConfirmTests` and `FighterLockoutRefusalRulesTests`; all reverted.
- Defy and blast suites: one mutation run (cost halved, the invulnerability test dropped from `Reaches`, the pool not zeroed, `ClearCaster` removed, the blast-fired bit not set, the thaw-tick check removed, `DiscardInput` removed) failed 14 of 22 cases, every group represented; reverted. `ThePresentationCounterStepsOncePerDefy...` caught a real bug while writing it (the counter bits were cleared before being read, so every Defy wrote 1).
- Rewritten cases fail against the pre-change code by construction (they read `MomentumUnits` / the 120-frame delay, which did not exist).

Runs (lock held, `GODOT_BIN` the console exe):
- Commit 1: full run `Failed: 18, Passed: 3758, Total: 3776` before re-timing; filtered 548/548 after.
- Commit 3: filtered 285/285 (`FighterDefy`, `RollbackReadiness`, `RollbackProtocol`, `FighterMomentumGauge`, `FighterAbilityLockout`, `FighterLockoutRefusal`, `FighterHitConfirm`, `FighterSimulation`, `FighterMatchFlow`, `FighterLedge`, `FighterSpecial`, `FighterCpu`).
- Commit 4 (one throwaway probe test included): full `Failed: 0, Passed: 3796, Total: 3796`, 2 m 11 s, twice. The probe is removed from the tree, so the committed total reads **3795**; the working tree matches the last green run apart from that file and this handoff.
- Build: `dotnet build --no-incremental`: 0 errors, one warning (`CS8632`, the vendored gdUnit file). **`KLSG_ECS004` on `FighterTuningComponent` is gone** (306 is under 128 B now, P33); no `KLOTHO_DET002`.

## 6. Measurements

Replay probe (throwaway, not committed; a 900-tick random-input script, seeds 781 and 1234, default kit; not the MM-RULES probe, so not comparable with its hashes):
- commit 1: 781 `-4900148563463106254`, 1234 `2525283783707234172`, snapshot 2647 / 2445 B
- commit 2: 781 `3964631755593588242`, 1234 `-2659787714871770276`, snapshot 2615 / 2413 B (-32 B: the 312 fields, 2 x 16 B)
- commit 3: identical to commit 2 (the script presses no Defy)
- commit 4, with a Defy press added to the script and both meters seeded to 100: 781 `-6139354738954612886`, 1234 `6408404419290496077`, snapshot 2523 / 2514 B

`RollbackReadinessTests` at commit 4: worst case across the 10 stages median 0.079 ms, p95 0.136 ms, 0 breaches, largest snapshot **2988 B** (Chicago). The harness changed (Specials held until the last 40 warm-up ticks), so this is not comparable with the 2026-10-25 figure of 3277 B or 0.090 / 0.137 ms.

Defier's frame advantage: the Defier is free at press + 38 ticks (`TheClockRunsOneToThirty...` pins `DefyFrames` 0 on tick 38); a victim's stun is assigned on tick 5 and starts counting after the 8-tick freeze, so it ends after tick 43 and acts on tick 44: the Defier is **+6**, as P11 says.

## 7. Not delivered / open

- **The CPU is untouched.** Its Special rungs still read `SpecialOneCooldownFrames` / `SpecialTwoCooldownFrames` (now the reuse delay) and never the gauge, so a CPU presses Specials it cannot afford and is refused; `MomentumKnown` / `SelfMomentumUnits`, the Defy bands and the observation fields are MM-CPU's. The CPU suites pass as they are.
- No presentation (MM-FX): the Fighter driver shows nothing for a Defy yet; the 323 counter is there for it. `FighterHUD` / `FighterHudModel` still read `verb.DefyHistoryUsed` (always 0 now) until MM-HUD removes the seal.
- Constructs, projectiles and zones are not touched by the blast (R4). A Fighter pushed over an Open-stage pit can lose a stock to it (P8, `VERIFY-DEFY-BLAST-RINGOUT`).
- No Story or LAN change. Nobody played the build.

## 8. For the docs

Design master / contracts (line numbers at `56031750`, to be re-read): F22 Sudden Death in `FIGHTER_MATCH_RULES.md` (cooldowns ready, Defy disabled) becomes "gauge 2 pips, reuse 0, Defy allowed once 50 is earned"; D04 in `DEFENSIVE_EFFECTS.md` (:237-257) and decision 4.3 superseded by the active Defy; `ROLLBACK_STATE_CONTRACT.md`: 306 loses two ints, 312 four (36 to 20 B), 323 gains `DefyFrames` (40 B), 310's `MomentumRefundedFramesSlotOne` is `MomentumUnits`; the A02 line "hyper-armoured wind-up" gains "cancelled by a Defy blast (R3), and the Defy is refused once the hold has connected"; `ABILITY_DATA.md` :25 the Requiem shave is a no-op.

Ledger rows to write or amend: `USER-MOMENTUM-GAUGE`, `PROVISIONAL-MOMENTUM-RULES` (the unit, P2 implemented as `matchLive`, the classifier's read-time gauge), `USER-MOMENTUM-NOT-YET-FLASH` (P14 in `FighterLockoutRefusalRules`), `USER-DEFY-ACTIVE-BURST`, `PROVISIONAL-DEFY-LEGALITY` (the interrupt set, the stick discard, the ledge capture refusal), `PROVISIONAL-DEFY-BLAST` (max stagger, `IsGrounded = 0`, the always-freeze), `VERIFY-DEFY-MUTUAL` (correct P5 as in section 4), `PROVISIONAL-FIGHTER-SNAPSHOT-SIZE` (P33: the `KLSG_ECS004` warning is gone), `DEFER-REQUIEM-FORTISSIMO-REDESIGN` (the 2.0 s value). Closed as moot: `FIX-SIM-VENOM-DEFY-BARRED`, `VERIFY-DEFY-CANCELS-OWN-ROLL`.

AGENTS.md / CLAUDE.md: the component inventory rows for 306, 310, 312, 323; the "failure signature 8" Defy note about `StoryDefyHistoryUsed` is Story's; CLAUDE.md's `KLSG_ECS004` build note is stale now; the seam names (`SeedFighterMomentumForTest`, `SeedFighterSpecialReuseForTest`, `SeedFighterDefyForTest`, `MomentumTestKit`); the test baseline (3778 to 3795 with this branch alone).

Nobody played the build.
