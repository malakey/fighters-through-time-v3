# CM-SIM handoff — the sim foundation (Phase A0) and the Fighter Special phase machine

Combat and movement update 2026-10-16 (`docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md`), workstream **CM-SIM**: decisions 1.2, 1.1(2), 1.3, 1.1(3), 2.3, 10.1 and 8.4 (sim half), the Q1 ruling (§0.2: "brake to a stop, both modes"), and Klotho component **323**.

Branch `worktree-wf_408a1af8-cf8-1`, from the plan commit `735296f`. Tag **`cm-sim-a0`** = `b62bf1af97a8558cfbbd7d0fec935d2301ac0f93` (commit 5).

| # | Commit | Content | `Total:` |
|---|---|---|---|
| 1 | `9ca55ea` | Mechanical split + `partial` systems | 3228 |
| 2 | `d45a637` | The action chokepoint (empty body) at every acceptance site | 3228 |
| 3 | `a4545af` | Component 323, inert; `FighterActionStateTests` 4 | 3232 |
| 4 | `f9be945` | One named sim hurtbox; `FighterHurtboxSourceScanTests` 1 | 3233 |
| 5 | `b62bf1a` | 1.1(3), 2.3, 10.1, the inert cast plumbing (tag `cm-sim-a0`) | 3240 |
| 6 | `aaf6e6d` | `StorySpecialTimingProbeTests` 2 (Story only) | 3242 |
| 7 | `366bfa2` | The Fighter Special phase machine; re-timed suites; +6 in existing suites | 3248 |
| 8 | `c130fa6` | The Q1 cast brake, Story half; `StorySpecialCastMotionTests` 2 | 3250 |
| 9 | `36b1ee7` | `FighterSpecialCastRulesTests` 6, `FighterSpecialTimingTests` 14, `ShieldBreakerTimingTests` 3 | 3273 |
| 10 | (this file) | The handoff | 3273 |

**Nobody has played the build.** Every claim below is a headless suite, a headless smoke run or a measurement probe.

## 1. Shipped

### Phase A0 (commits 1–5)

- **The split (commit 1).** `FighterUniversalMovementRules` + `FighterAirDodgeRules` (`FighterUniversalMovement.cs`), `FighterLedgeRules`, `FighterBasicAttackRules` and `FighterAbilityLockoutRules` moved out of `FighterSimulationSystems.cs` unedited; `FighterMovementSystem` and `FighterCombatSystem` are `partial`.
- **The chokepoint (commit 2).** `FighterActionRules.OnActionAccepted(ref FighterStateComponent, ref FighterVerbComponent, ref FighterDefenseComponent, ref FighterActionStateComponent, FighterActionKind)`, empty body; `FighterActionKind { None, Swing, Special, Ultimate, Grab, Jump, AirJump, Roll, AirDodge, MovementAbility, EchoStep, LedgeGrab, LedgeGetUp }`; `FighterActionRules.IsOffensive(kind)` (Swing, Special, Ultimate, Grab). `ProcessBasicAttackPhase` now returns whether a swing started. Call sites: the Phase B guide (§9).
- **Component 323 (commit 3).** `FighterActionStateComponent`, nine ints, 36 B per fighter (72 B per snapshot): `SpecialSlot`, `SpecialPhase`, `SpecialPhaseFrames`, `SpecialPhaseElapsed`, `SpecialFlags`, `SpecialCancelBuffer`, `BlockTiming`, `DownTapFrames`, `ActionFlags`; `ActionFlagRespawnProtection = 1`, `ActionFlagJumpReleasedSincePress = 2`, `ActionFlagLedgeStickRearmed = 4`, `ActionFlagRiseFromOwnJump = 8`. Every field, flag and reserved value is documented with its owning workstream in `scripts/FighterSim/FighterSpecialCast.cs`. Added in `SpawnFighter`; held at zero while the respawn platform holds the fighter; zeroed by the F22 Sudden Death setup. Seams: `FighterSimulation.TryGetFighterActionState` (public) and `SeedFighterActionStateForTest` (internal).
- **8.4, sim half (commit 4).** `FighterHurtbox.HalfExtents = new(FP64.FromDouble(0.5), FP64.One)` (`scripts/FighterSim/FighterHurtbox.cs`) and the separately named alias `FighterBodyOverlap.HalfExtents` (orb pickup, hazard overlap). `FighterKitGeometry.FighterHalfExtents` and `FighterCombatSystem.VictimHurtboxHalfWidth` read it; the ten copies are gone. Static readonly fields, not property forwarders, because `in` arguments need a field.
- **1.1(3) (commit 5).** The landing tech calls `FighterUltimateActivationRules.DiscardInput` on its landing tick L (right after a successful `TryLandingTech`) and on every tech-branch tick; `CanReadAbilityPress`, `FighterGrabRules.CanStartGrab` and the Ultimate predicate gained `verb.TechLockoutFrames <= 0`.
- **2.3 (commit 5).** `FighterCombatSystem.UltimatePressPayable` / `UltimatePressAccepted` / `UltimatePressHeldBackByLockout(in FighterStateComponent, in FighterRuntimeComponent, in FighterVerbComponent, in FighterActionStateComponent, in FighterUltimateActivationComponent)` refuse in the block stance (shieldstun included), the tech lock and a cast. Every caller (the combat `Update`, `FighterAbilityEntitySystem`, the refusal classifier) reads the one predicate, so a refused stance Ultimate claims nothing and never flashes. The `FighterLockoutRefusalRules.cs` :80–86 comment is fixed.
- **10.1 (commit 5).** `FighterBasicAttackRules.FlagActiveFacingRight = 256` (component 305 `AttackFlags`) is set or cleared from `FacingRight` on every Startup → Active transition in `ProcessBasicAttackPhase`, chain continuations included; the chain, the air strike and the construct sweep resolve their box through a facing overload of `ResolveStringBox` reading `LatchedFacingRight(runtime)`. The old overload stays for `StringHitboxGeometryTests` and `CpuMeleeBandTests`. The Up-Attack and Down-Air boxes are centred on the pivot (|dx| ≤ 1.2 / 1.0 units), so facing never offsets them and they need no latch.
- **The inert plumbing (commit 5),** live from commit 7: `FighterSpecialCastRules` (phases, flags, `IsBusy`, `Clear`, the interrupt set), the cast region before `TickCounters`, the hitstop-branch busy-caster discard, the cast terms in `CanStartGrab` / `CanReadAbilityPress` / the Ultimate predicate, `UniversalMovementRules.AerialSpecialGravityMultiplier = 0.5f` (beside `ApexHangGravityMultiplier`; Story's `ProcessUsingSpecial` forwards to it in place of its `0.5f`), and `FighterAirPhysics.StepMovement(velocityY, fastFalling, floating, jumpedThisAirtime, FP64 gravityScale)` with `FighterAirPhysics.AerialSpecialGravityScale`.

### The phase machine (commits 7–8)

- **Acceptance** (`FighterAbilityEntitySystem.Update`). Every accepted Special 1/2 press spends at once: `PositiveCooldown(...)`, the Momentum counter reset, `FighterAbilityLockoutRules.ArmAbility`, `FighterBasicAttackRules.CancelString`, `OnActionAccepted(Special)`, then `FighterSpecialCastRules.Begin(ref cast, slot, contract, startedAerial: IsGrounded == 0)`. Special 1 is resolved before Special 2. Melee-execution slots are accepted here too (1.1(2)): an out-of-reach Splitting Strike spends and whiffs, and it takes the tick from the movement ability. Tubman's Foresight starts its 322 stance and records `BeginDelegated`.
- **The clock** (`FighterMovementSystem.AdvanceSpecialCast`, new partial file `FighterMovementSystem.SpecialCast.cs`), after the hitstop gate and after Foresight's `Advance`: the interrupt set clears first; a Delegated cast clears when `FighterForesightRules.IsBusy` is false; otherwise `FighterSpecialCastRules.Advance`, which returns true on the active-entry tick T+S. Divine Piercing's lunge (`FighterKitMotion.StartPiercingLunge(..., contract.ActiveFrames)`) starts there.
- **The fire pass** (`FighterAbilityEntitySystem.FireActiveSpecials`, after the acceptance loop): an Active cast without `FlagEffectFired` re-checks the interrupt set, sets `FlagEffectFired` and fires the slot's existing effect through `FireSpecialEffect` (projectile, construct, zone; the Rift placement, the Vortex anchor and the Tempest lift are read here). A 0-startup Divine Piercing (only a fallback contract) lunges here.
- **Melee contact** (`FighterCombatSystem.BuildIntent(in attacker, in runtime, in FighterActionStateComponent, in FighterAbilityModeComponent, in tuning, in target, contracts)`): tested on every active tick until it resolves one contact; `Update` sets `FlagMeleeResolved` when an intent is built. Divine Piercing (its thrusts) and Foresight resolve elsewhere. `ApplyIntent` arms nothing any more.
- **The cast lock.** While `IsBusy` the caster's input is discarded on every tick: in the cast region (`DiscardInput`, except the grounded recovery Block cancel, which `Clear`s the cast and keeps the tick's input) and in the hitstop branch (its own freeze and thaw ticks). The second gates are `CanStartGrab`, `CanReadAbilityPress` and `UltimatePressPayable`. No ledge capture while busy.
- **Motion (Q1, both modes).** Grounded: `ApplyNormalMovement` runs with the discarded (zero) stick, which is the run-stop ramp (`UniversalMovementRules.RunDecelerationFrames` = 4). Airborne: `CastKeepsAirDrift` skips air control, so the entry drift is kept. Vertical: a cast with `FlagStartedAerial` steps gravity through `AerialSpecialGravityScale` in Startup and Active; recovery and a grounded cast that leaves the floor use the normal step; no fast-fall (Down is discarded). Story: `PlayerController.ApplySpecialCastBrake` (new partial file `PlayerController.SpecialCastMotion.cs`), called from `ProcessUsingSpecial` beside the forwarded gravity line: `DecelerateHorizontal(dt)` while `IsOnFloor()`, except Divine Piercing's Active lunge.
- **Data.** `FighterAbilityHitData.StartupFrames` / `RecoveryFrames` (loadout data, never snapshot state), projected `Math.Max(0, …)` by `FighterLoadoutFactory.HitData`. `FighterMovementSystem` now takes the `FighterHitContractTable`.
- **Fallback.** `FighterSpecialCastRules.IsFallback(contract)` = `ActiveFrames <= 0` (a hand-built `FighterAbilityLoadout.Default` slot): the cast runs 0 / 1 / 0 with `FlagFallbackTiming`, which reproduces the press-tick timing exactly (the seeded hand-built replay hash is unchanged across commit 7).
- **Presentation** (read-only). `FighterSimulationDriver.ResolveAbilityPresentation` poses `special_1` / `special_2` from 323 for the whole clocked cast (Startup → wind-up, Active → strike, Recovery → follow-through) with no trailing hold, through `FighterSimulationDriver.SpecialCastPose(int castPhase)` → `AttackPoseRules.PoseFor`. The thirds hold stays for the fallback and the press-tick Ultimate edge; the movement-ability reel is untouched.
- **Classifier.** `FighterLockoutRefusalRules.Evaluate(..., in FighterActionStateComponent actionState = default)` passes it to `CanReadAbilityPress`.

## 2. Files changed

Production (all inside CM-SIM's ownership unless noted):
- New: `scripts/FighterSim/FighterSpecialCast.cs`, `FighterActionRules.cs`, `FighterHurtbox.cs`, `FighterLedgeRules.cs`, `FighterUniversalMovement.cs`, `FighterBasicAttackRules.cs`, `FighterAbilityLockoutRules.cs`, **`FighterMovementSystem.SpecialCast.cs`** (a new partial of the owned movement system: the clock and the motion predicates).
- Edited: `FighterSimulationSystems.cs`, `FighterEntitySystems.cs`, `FighterSimulationComponents.cs`, `FighterLoadoutFactory.cs`, `FighterSimulation.cs`, `FighterKitRules.cs` (the hurtbox forward), `FighterGrabRules.cs` (`CanStartGrab`), `FighterAirPhysics.cs` (the cast-step overload), `FighterUltimateActivation.cs` (the hurtbox line), `FighterLockoutRefusalRules.cs`, `FighterSimulationDriver.cs` (`ResolveAbilityPresentation` and the adjacent static `SpecialCastPose`), `scripts/Core/UniversalMovementRules.cs` (the anchor), `scripts/Characters/PlayerController.cs` (two lines in `ProcessUsingSpecial`: the forwarded constant and the brake call).
- **Outside the declared lines:** `scripts/Characters/PlayerController.SpecialCastMotion.cs`, a new partial file holding only `ApplySpecialCastBrake` / `SpecialCastDrivesOwnMotion`, the helper the owned brake call invokes; it touches no existing `PlayerController` line. `FighterKitMotion.Reach.cs` and `FighterCounter.cs` were **not** edited (deviations 1 and 2).

Tests: every file under §5.

## 3. Decisions (provisional; ledger `PROVISIONAL-SIM-SPECIAL-PHASE-RULES` unless noted)

1. **Tick convention.** Press T is the first startup tick; the effect fires on T+S (the basic-swing convention); the caster is locked through T+S+A+R−1 and free on T+S+A+R, whose input is live. Story fires on T+S−1 (§6); the 1-tick offset goes under `VERIFY-SAME-TICK-VERB-PARITY`.
2. **Fallback.** A contract with `ActiveFrames == 0` runs 0 / 1 / 0 — today's press-tick timing — so the hand-built harnesses keep their meaning.
3. **Interrupt timing.** A hit landed by the combat system (PostUpdate) clears the cast on the next tick's movement pass (the hitstop branch or `AdvanceSpecialCast`), before any system reads it; the fire pass re-checks the set. A hit landing on T+S trades: the fire pass ran first.
4. **One melee contact per execution** (`FlagMeleeResolved`), tested on every active tick.
5. **The hitstop-branch discard** sits after the chain and freeze buffers and after `ClearIfInterrupted` (an interrupted victim keeps its DI read), before the thaw-tick release.
6. **The Q1 motion** as in §1, through the existing run-stop step in both modes (no new constant).
7. **Aerial half gravity** only for a cast that started airborne, only in Startup and Active.
8. **Ledge refusal** while busy (any phase, Delegated included).
9. **Root** keeps cancelling 305's universal-movement slot every tick, so Divine Piercing's lunge dies under Root, as it did on press+1. Reasoned from the rooted branch, not tested.
10. **The Tempest lift** now happens on its active entry (T+8), not on the press.
11. **`FlagEffectFired` is set for every slot the fire pass visits**, melee slots included (it marks the active entry). "Landed" must never be read from it: use `FlagMeleeResolved` or CM-CANCEL's `FlagLandedDirectHit`.
12. **The recovery Block cancel is grounded only** (`IsBlockStance` needs the floor). An aerial cast that lands mid-recovery with Block held cancels on its landing tick.
13. **The grab chord in recovery is the Block cancel**: the cancel tick keeps its live input, so Block+Attack can start a grab on that tick (CM-BLOCK / CM-CANCEL may want to rule on it).
14. **The Splitting Strike + movement-ability same-tick press casts Splitting Strike** (`FIX-SPLITTING-STRIKE-FREE-WHIFF`).

## 4. Deviations from the plan

1. **Divine Piercing's lunge starts in the movement system** (`AdvanceSpecialCast`, on the active-entry tick), not in the fire pass, and `FighterKitMotion.Reach.cs` is untouched (the existing `StartPiercingLunge` takes the active frames). The movement system integrates before the ability system, so a fire-pass start would first move on T+S+1 and run one tick past the active window. With this placement the 12 moving ticks are exactly the 12 active ones (`DivinePiercingLungeTests`: 13 in place, 12 lunge, 20 in place).
2. **`FighterCounter.cs` is untouched.** The Delegated marker is written at acceptance and cleared in `AdvanceSpecialCast` when `FighterForesightRules.IsBusy` is false; no 322 code needed a change.
3. **Two new partial files** (`FighterMovementSystem.SpecialCast.cs`, `PlayerController.SpecialCastMotion.cs`) instead of growing the 4,000-line system file and touching other `PlayerController` lines.
4. **No cast VFX to move.** The plan says "cast VFX fire at `Begin`, impact VFX at the fire tick", but `FighterSimulationDriver` emits no Fighter ability VFX at all today, so there was nothing to retime.
5. **`FighterSpecialTimingTests` case 5 tests the Q1 ruling** (a grounded cast brakes within 4 ticks; an aerial one keeps its drift, the stick and Down doing nothing) rather than the plan text's "holds its entry horizontal velocity, grounded and airborne", which §0.2's Q1 supersedes.
6. **The driver's refusal classifier passes no 323 state.** `FlashLockoutRefusals` is outside `ResolveAbilityPresentation`, so it still calls `Evaluate` without the new argument; the cast discard zeroes `PressedButtons`, so nothing flashes during a cast (`FighterLockoutRefusalRulesTests.APressDuringOnesOwnCastNeverFlashes` covers both paths).
7. **`TeslaBlinkSimTests`' hand-built shooter** gained `StartupFrames = 0` so its shot keeps leaving on tick 5 (the translation-window arithmetic in the fixture's comment stays true), instead of re-timing the blink.
8. **`RollbackReadinessTests.RunConvergence`** gained an overload taking `Func<FighterSimulation>`; the old signature delegates to it and no assertion changed.

## 5. Tests

**Declared +45, delivered +45** (3228 → 3273):
- A0 +12: `FighterActionStateTests` 4 (new, pure C#), `FighterHurtboxSourceScanTests` 1 (new, pure C#), `FighterVerbLayerTests` +3 (`TheLandingTechLocksEveryVerbForItsTwelveFramesAndThePressAfterIsAccepted`, `NoVerbStartsOnTheTechsOwnLandingTick`, `NoNotYetRefusalIsClassifiedInsideTheTechLock`), `UltimateActivationTests` +2 (`AnUltimatePressedFromTheBlockStanceIsRefusedKeepsTheMeterAndClaimsNothing`, `AnUltimatePressedInShieldstunIsRefusedAndKeepsTheMeter`), `StoryAbilityLockoutTests` +1 (`BlockingWithAFullMeterStaysInBlocking`), `FighterBasicStringParityTests` +1 (the box-facing latch).
- Phase machine +33: `StorySpecialTimingProbeTests` 2, `StorySpecialCastMotionTests` 2, `FighterSpecialCastRulesTests` 6 (pure C#), `FighterSpecialTimingTests` 14, `ShieldBreakerTimingTests` 3, `SplittingStrikeSpikeTests` +2 (`AnOutOfReachPressSpendsItsCostAndPlaysItsWholeWhiff`, `ASameTickSplittingStrikeAndMovementPressCastsSplittingStrike`), `DivinePiercingLungeTests` +1 (`JoanHoldsThirteenTicksLungesTwelveAndHoldsTwentyAndAStartupHitLeavesNoLunge`), `FeelPassSimTests` +1 (`TheWholeSpecialCastIsPosedFromItsPhases`), `FighterLockoutRefusalRulesTests` +1 (`APressDuringOnesOwnCastNeverFlashes`), `RollbackReadinessTests` +1 (`ProductionLoadoutsConvergeWithSpecialsCastThroughTheirFullPhases`, method-level `[RequireGodotRuntime]`).

**Rewritten in place:**
- `FighterCombatContractW3Tests.ASpecialLeavesNoRecoveryLockSoTheStanceRisesOnTheNextTick` → `BlockCancelsASpecialOnlyFromItsRecoveryFrameZero` (production Joan, Block held from T+1, the stance rises on T+18 = T+S+A).
- `FighterPresentationSyncTests.AcceptedFighterSpecialHoldsTheAuthoredAbilityPose`: the pose now follows the cast phases (wind-up through startup, strike on the active entry, follow-through in recovery) instead of thirds of the hold.
- `StorySpecialTimingProbeTests`' motion case → `StorysCastMotionBrakesGroundedAndKeepsTheAirborneDrift` (commit 8, after the brake landed; the pre-change slide is in §6).

**Re-timed (assertions unchanged, fixtures step through the startup with the new non-suite helper `tests/Determinism/SpecialCastTestKit.cs`: `StepUntilFired`, `StepUntilCastEnds`, `FiresOnNextAdvance`):** `FighterSimulationTests` (5 cases), `FighterVerbLayerTests.LandLauncher` (its two callers), `CleopatraKitTests` (3), `DivinePiercingLungeTests` (1), `FighterConstructAttackTests` (2), `FighterMatchFlowTests.SuddenDeathRemovesEveryRegulationProjectileZoneAndConstruct`, `FighterStockLossDespawnTests` (3), `FighterTimeSystemsTests.EveryConnectingStringHitRefundsARunningCooldownInMatch`, `FortissimoWallTests` (2), `GroundWaveTests` (4), `JoanKitTests` (1), `LeonardoKitTests` (2), `LeonardoTurretSimTests` (2), `LincolnKitTests` (1), `ProjectileBurstSimTests` (3), `RelativityWarpFoldSimTests` (1), `RequiemCooldownShaveTests` (3), `RiftCollapseSimTests` (3), `ShakespeareKitTests` (3), `SplittingStrikeSpikeTests` (2), `TeslaBlinkSimTests` (1, fixture), `TeslaKitTests` (3), `TubmanKitTests` (2). No CPU suite needed a change: all CPU, Mirror and recovery suites are green unmodified.

**Shared test files touched** (§3 maps): `FighterVerbLayerTests`, `FighterBasicStringParityTests`, `FighterPresentationSyncTests`, `FighterMatchFlowTests`, `UltimateActivationTests`, `FighterLockoutRefusalRulesTests`, `RollbackReadinessTests`, `FeelPassSimTests`, `SplittingStrikeSpikeTests`, `DivinePiercingLungeTests`, `StoryAbilityLockoutTests`, every `*KitTests` listed above.

**Runs** (each holding `D:/Projects/.ftt-gdunit-lock`, orphans drained):
- The §4.1 CM-SIM filter (`RollbackReadiness`, `FighterSimulation`, `RollbackProtocol`, `FighterSpecial`, `FighterActionState`, `ShieldBreaker`, `FighterAbilityLockout`, `FighterHitConfirm`, `FighterLockoutRefusal`, `FighterVerbLayer`, `FighterGrab`, `UltimateActivation`, `*KitTests`, `FeelPassSim`, `FighterPresentationSync`, `FighterCpu`, `CpuMeleeBand`, `CpuRecoveryMatrix`, `FighterLedge`, `FighterMatchFlow`, `StorySpecialTimingProbe`, `StoryAbilityLockout`, plus `MirrorParadox`, `SplittingStrike`, `DivinePiercing`, `StorySpecialCastMotion`): **`Passed! - Failed: 0, Passed: 449, Total: 449`** (29 s).
- After commit 3, the rollback trio: green (54/54).
- Commit 5's full run read `Failed: 2, Passed: 3239, Total: 3241` (3240 + the uncommitted hash probe): a harness bug in the new `NoNotYetRefusalIsClassifiedInsideTheTechLock` (the victim's own swing walked out of range; fixed before tagging) and one `RollbackReadinessTests.WorstCaseDepthSevenResimulationFitsTheRollbackBudget` breach (17.4 ms on `paris_bastille`, machine contention: green on an isolated rerun and in every later run).
- **Final full run at commit 9: `Passed! - Failed: 0, Passed: 3273, Skipped: 0, Total: 3273, Duration: 1 m 31 s`**, reconciled exactly against 3228 + 45. The `[TestCase]` attribute count under `tests/` is 3273.
- `dotnet build --no-incremental`: 0 errors, only the three pre-existing warnings (CS8632, `KLSG_ECS004` on `FighterTuningComponent`, CS0649); no `KLOTHO_DET002`, no `KLSG_ECS004` on 323.
- Headless `--quit` load check: clean.

## 6. Measurements

**Seeded replay hashes per commit** (a temporary probe, never committed: 18 production pairs with the rollback suite's scripted inputs on `florence_workshop` and `paris_bastille`, one hand-built default pair, five Hard-CPU production matches; 24 final hashes per commit):
- Commits 1 and 2: **identical** to the plan commit (all 24).
- Commit 3: all 24 moved (the registered 323 storage is hashed); two runs identical.
- Commit 4: **identical** to commit 3.
- Commit 5: 10 of 24 moved (four scripted pairs, the hand-built pair and all five CPU matches).
- Commit 7: 23 of 24 moved; the **hand-built default pair did not**, which confirms the 0 / 1 / 0 fallback reproduces press-tick timing exactly. Example, `default scripted`: `-2481685572144015716` (plan commit) → `-6151741511669466926` (commits 5 and 7).

**Story's Special timing** (`StorySpecialTimingProbeTests`, commit 6): Einstein's E=mc² (14 / 6 / 20) is Active and its projectile exists on **T+13 = T+S−1**, one tick before the sim's T+S (the controller reads the press on T and the child ability node clocks after it on the same tick). Story's pre-change cast motion: a running grounded caster slid at **324 px/s on every cast tick** (`ProcessUsingSpecial` wrote only Y), and an airborne caster kept its drift. After commit 8 the grounded caster stops on exactly the 4th tick.

**The shatter → Ultimate gap** (production attacker vs a default-kit blocker holding Block; the attacker mashes the Ultimate with a full meter):

| Shield-Breaker | Spawn gap | Shatter | Attacker's freeze | First Ultimate | Gap | Blocker's first action |
|---|---|---|---|---|---|---|
| Divine Piercing (13 / 12 / 20) | 4 u | T+24 | 16 | T+61 | **37** | shatter + 76 |
| The Emancipator (16 / 6 / 24) | 4 u | T+31 | 16 | T+62 | **31** | shatter + 76 |
| Splitting Strike (14 / 6 / 22) | 2 u | T+14 | 16 | T+58 | **44** | shatter + 76 |

The blocker acts at shatter + 76 (the 16-frame shatter freeze + the 60-frame daze), so the attacker's first Ultimate press comes 39 / 45 / 32 frames before it, longer than the 20-frame activation wind-up: a blocked Shield-Breaker is still an Ultimate punish on the blocker.

**Rollback** (`RollbackReadinessTests`, detailed logger, this worktree): worst-case depth-7 median **0.115 ms** (`florence_workshop`), p95 **0.240 ms**, **0 breaches**, largest snapshot **4429 B** (`paris_bastille`), budget 8 ms.

**Smoke runs** (`--headless`, lock held): `TestArena.tscn --quit-after 1800` exit 0, no error lines. `Level_02_Orleans.tscn --quit-after 300` exit 0 with "8 ObjectDB instances leaked / 4 resources still in use at exit"; the untouched control `Level_03_Chicago.tscn` prints exactly the same, so it is the documented `AuthoredResources` noise.

**The knockdown landing-tick check (leaks; recorded, not fixed, §8).** A temporary probe launched a tumbling victim with a launcher Special and had it miss the tech (no Block) while pressing one verb on every tick. On the landing tick L the knockdown begins (`KnockdownFrames` 30 post-tick) but L's presses are live: **Special 1, Special 2, the movement ability, the direct grab and a paid Ultimate all start on L.** A BasicAttack and a Roll do not (the movement system resolves them before the landing); a Jump cannot (the hitstun branch). The knockdown branch's own discard covers L+1 on.

**The A02 thaw-tick check (leaks; recorded, not fixed).** The Ultimate caster takes **no hitstop from its own activation strike or cinematic hits** in any of the nine production kits (Wind-up → Active → Cinematic with zero frozen ticks; one hitstop tick after the cinematic ends, when the caster is free anyway). But a **third-party hit in the armored wind-up** (a generic caster at 2 units, a Leonardo-reach opponent swinging into it) freezes the caster 4 ticks, and on that freeze's thaw tick, still in the wind-up, **a direct-grab press starts a grab** (`GrabPhase` ≠ 0) and **a movement-ability press starts the movement ability** (its cooldown armed; the wind-up then ran 5 ticks longer). Special 1, Special 2 and BasicAttack presses start nothing there. Cause: A02's discard sits in the normal branch after the hitstop branch, and the hitstop branch discards only for a busy Special caster.

**CPU counts:** none recorded; CM-SIM ships no CPU smoke (CM-CANCEL owns `FighterCpuSpecialTimingSmokeTests`).

## 7. Not delivered / open

- The knockdown landing-tick leak and the A02 thaw-tick leak (§6), recorded per §8 for the closeout's `VERIFY-*` rows.
- The Block-release tick (2.3): on the tick Block is released the sim reads Held without Block and can accept an Ultimate, while Story's `ProcessBlocking` returns to Idle and drops the press. Recorded per the plan, not changed or tested.
- Root vs the Piercing lunge (decision 9) is reasoned, not tested.
- The driver's "not yet" classifier call does not pass the 323 state (deviation 6).
- CPU behaviour against the new timing (the Hard CPU's Lincoln now whiffs and spends an unchecked Splitting Strike; its Special rungs do not yet read the cast) is CM-CANCEL's; every CPU suite is green on re-timing alone, and none needed it.
- No Fighter cast or impact VFX exist to place (deviation 4).
- **Nobody has played or looked at any of it.** No pad session has judged a startup, a whiff punish, the brake or the half-gravity float.

## 8. For the closeout

**AGENTS.md:**
- Combat, the Special-recovery bullet: "The sim has no special recovery lock, so its stance simply rises on the next tick" → the sim honours every Special's authored startup / active / recovery (component 323), and Block cancels only from recovery frame 0; "the sim waits on `DEFER-SIM-SPECIAL-PHASES`" (the D3 bullet) → Phase B's CM-CANCEL.
- Package 13 "not delivered", Combat: "**Sim Specials still fire on press** (`DEFER-SIM-SPECIAL-PHASES`, plan D9)" → closed.
- The F1 feel bullet: "on-press sim Specials from thirds of the 18-frame cosmetic hold (`SIM-SPECIAL-POSE-THIRDS`)" → posed from the cast phases; the thirds stay for the fallback and the Ultimate edge.
- The ability-lockout bullet: hit 1's 30-frame hitstun still covers every Special startup (≤ 16), so hit 1 → a reaching Special remains a true confirm in PvP; a Special press during one's own cast is discarded, never refused.
- The Klotho inventory: add **323** `FighterActionStateComponent` (nine ints, 36 B, MaxCount 2; the fields and owners above); highest ID **323**; 305's row gains `AttackFlags` 256 `FlagActiveFacingRight` (and reserves 512 for CM-MOVE, universal-movement values 8–10 for CM-MOVE); 310's row reserves `Tumble` = 2 for CM-HIT.
- The cross-mode parity bullet: the box facing is latched at active start (closes `VERIFY-SIM-STRING-FACING-PER-TICK`); the sim landing tech locks every action from its landing tick; no Ultimate from the block stance or shieldstun.
- The 8.4 sim hurtbox: `FighterHurtbox.HalfExtents` (and `FighterBodyOverlap.HalfExtents`) is the one statement of the 1.0 × 2.0 sim hurtbox.
- The "Fighter networking and determinism" system list: Special cast phases (323), the fallback 0 / 1 / 0.
- The validation section: the new baseline and the suites above.

**CLAUDE.md:** the "Character ability" row (drop "Sim Specials still fire on press (`DEFER-SIM-SPECIAL-PHASES`), so a new startup number is Story-only until that closes") and the "Fighter simulation / netcode" row (highest ID **323**).

**Design master** (`design-godot.md` at `e2c655a`, per the plan): the FSM rows :783–786 (Fighter Specials have startup / active / recovery), :1097 (aerial Specials' half gravity, "both modes"). No `DOC-MASTER-UNBOUND-BINDING` line is involved.

**Contracts** (both copies, repo and docs-3): `ABILITY_DATA.md` (startup and recovery are honoured in both modes); `ROLLBACK_STATE_CONTRACT.md` :37 (component 323, by hand in both copies).

**Ledger rows:**
- Add `USER-SIM-SPECIAL-TIMING` (1.2) and `PROVISIONAL-SIM-SPECIAL-PHASE-RULES` (§3 items 1–14).
- Add `FIX-SPLITTING-STRIKE-FREE-WHIFF` (1.1(2)), `FIX-SIM-TECH-LOCK-ACTIONS` (1.1(3)), `USER-NO-ULT-FROM-BLOCK` (2.3), and the 8.4 sim half.
- Close `DEFER-SIM-SPECIAL-PHASES`, `SIM-SPECIAL-POSE-THIRDS`, `VERIFY-SIM-STRING-FACING-PER-TICK`, `VERIFY-SAME-TICK-VERB-PARITY` (1) and (5).
- Record under `VERIFY-SAME-TICK-VERB-PARITY`: Story's Special effect on T+S−1 against the sim's T+S (a 1-tick offset).
- New `VERIFY-*` rows: the knockdown landing-tick leak and the A02 thaw-tick leak (§6).
- Amend: `USER-COMBAT-FLOW-NOT-NOW`; `PROVISIONAL-ABILITY-LOCKOUT-RULES` (1); `PROVISIONAL-MOVEMENT-FEEL-RULES` (5) (aerial Specials have a sim counterpart through one constant); `VERIFY-TUNING-SPECIAL-TOTAL-FOLD` (only Splitting Strike reaches the folded generic total); `USER-HIT-CONFIRM-CANCEL`'s Fighter sentence; delete the out-of-reach melee flash note and the block-stance miss from `PROVISIONAL-HIT-CONFIRM-RULES` (6).

## 9. Phase B guide

### Component 323 — where each field is read and written

| Field / bit | Owner | Written | Read |
|---|---|---|---|
| `SpecialSlot`, `SpecialPhase`, `SpecialPhaseFrames`, `SpecialPhaseElapsed` | CM-SIM | `FighterSpecialCastRules.Begin` / `BeginDelegated` (acceptance, `FighterEntitySystems.cs` ≈ :1140–1155), `Advance` and `Clear` (`AdvanceSpecialCast`, `FighterMovementSystem.SpecialCast.cs`; the cast-region Block cancel ≈ :477–491; `ClearIfInterrupted` in the hitstop branch ≈ :343 and the fire pass ≈ :1221) | `IsBusy` / `IsCasting` / `IsStartup` / `IsActive` / `IsRecovery` / `IsDelegated` / `RecoveryFrame`: the hitstop branch, the cast region, `CastKeepsAirDrift` (≈ :654), `CastHalvesGravity` (≈ :715), the ledge gate (≈ :832), `CanReadAbilityPress`, `CanStartGrab`, `UltimatePressPayable`, `BuildIntent` (≈ :3047), the fire pass, the driver's pose, `FighterLockoutRefusalRules.Evaluate` (optional) |
| `SpecialFlags` 1 `FlagStartedAerial` | CM-SIM | `Begin` | `CastHalvesGravity` |
| `SpecialFlags` 2 `FlagEffectFired` | CM-SIM | the fire pass; `AdvanceSpecialCast` (the Piercing lunge) | the fire pass. **Set for melee slots too**: never read it as "landed". |
| `SpecialFlags` 4 `FlagMeleeResolved` | CM-SIM | `FighterCombatSystem.Update` when `BuildIntent` returns an intent | `BuildIntent` |
| `SpecialFlags` 8 `FlagLandedDirectHit` | **CM-CANCEL** | nobody yet | nobody yet |
| `SpecialFlags` 16 `FlagFallbackTiming` | CM-SIM | `Begin` | the driver (keeps the thirds hold) |
| `SpecialCancelBuffer` | **CM-CANCEL** | nobody yet (zeroed by spawn, the platform hold, Sudden Death; `Clear` leaves it alone) | nobody yet |
| `BlockTiming` | **CM-BLOCK** | as above | nobody yet |
| `DownTapFrames` | **CM-MOVE** | as above | nobody yet |
| `ActionFlags` bit 0 `ActionFlagRespawnProtection` | **CM-HIT** | nobody yet (set it at the platform drop, after the hold at ≈ :372) | nobody yet |
| `ActionFlags` bits 1–3 | **CM-MOVE** | nobody yet | nobody yet |
| 305 `AttackFlags` 256 `FlagActiveFacingRight` | CM-SIM | `ProcessBasicAttackPhase` (≈ :1282) | `LatchedFacingRight` (≈ :1805): the construct sweep, the chain and the air strike (≈ :2299, :2381, :2523) |
| 305 `AttackFlags` 512, universal-movement values 8–10; 310 `Tumble` = 2 | CM-MOVE / CM-HIT | unused | unused |

Resets: `SpawnFighter` adds 323 zeroed (≈ :198); the movement system holds it at `default` while `FighterMatchFlowRules.IsOnRespawnPlatform` (≈ :372); the F22 setup zeroes it (≈ :3547).

### The chokepoint

`FighterActionRules.OnActionAccepted(ref FighterStateComponent fighter, ref FighterVerbComponent verb, ref FighterDefenseComponent defense, ref FighterActionStateComponent action, FighterActionKind kind)`, empty body, called once after each verb is committed. Call sites (`FighterSimulationSystems.cs` unless noted): the swing start (≈ :606, `ProcessBasicAttackPhase`'s return), the Special acceptance (`FighterEntitySystems.cs` ≈ :1157), the movement ability (`FighterEntitySystems.cs` ≈ :1183), the ledge grab (≈ :836), the air dodge and the roll (`TryStartUniversalMovement`, ≈ :1022 / :1034), the ground and air jump (`ApplyNormalMovement`, ≈ :1161 / :1165), Echo Step (`TryStartEchoStep`, ≈ :1451), the grab start (`AdvanceGrabs`, ≈ :2012 / :2019), the Ultimate (`TryCharacterUltimate`, ≈ :2706). `LedgeGetUp` has no site yet (CM-MOVE). The ledge climb jump inside `FighterLedgeRules.Process` has no call.

### The cast-region exits and the hitstop-branch discard (CM-CANCEL)

- **The hitstop branch** (`FighterMovementSystem.Update`, ≈ :327–358): `BufferChainPress`, `BufferFreezePress`, **then** `FighterSpecialCastRules.ClearIfInterrupted(...)`, **then** `if (IsBusy) DiscardInput`, then the decrement and the thaw-tick release. CM-CANCEL's D3 feed call goes after the two buffers and before the `ClearIfInterrupted` / discard pair.
- **The cast region** (≈ :477–491, after the grab branch, before `TickCounters`): `if (IsBusy) { if (IsRecovery && IsBlockStance(live)) Clear; else DiscardInput; }`. D3 fires here, after the Block cancel. `RecoveryFrame(cast)` is the 0-based recovery index: the tick `AdvanceSpecialCast` moves Active → Recovery is recovery frame 0, and the cast region sees it that same tick.
- **Foresight's discard** (≈ :502–507) and A02's (≈ :499–501) follow the cast region.
- **The fire pass** is `FighterAbilityEntitySystem.FireActiveSpecials` (`FighterEntitySystems.cs` ≈ :1208); **melee contact** is `FighterCombatSystem.BuildIntent` (≈ :3047) with `FlagMeleeResolved` set in `Update`; a hit that lands on a Special's contact should set `FlagLandedDirectHit` at the hit sites 2.11.1 lists, not in `ResolveFighterHit`'s body.
- Test helpers: `tests/Determinism/SpecialCastTestKit.cs` (`StepUntilFired`, `StepUntilCastEnds`, `FiresOnNextAdvance`, `IsCasting`, `HasFired`).

### The two checks

The knockdown landing tick and the A02 thaw tick both leak (§6). A fix for the first would discard L's input right after `TryBeginKnockdown` succeeds (the tech's pattern); for the second, a hitstop-branch discard for `FighterUltimateActivationRules.IsCasterBusy`. Neither is made here (§8 of the plan).
