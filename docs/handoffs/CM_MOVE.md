# CM-MOVE handoff — movement (combat and movement update, 2026-10-16)

Workstream **CM-MOVE** of `docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md`: decisions **5.1** (sim short hop), **6.1** (apex hang only while Jump is held, both modes), **6.2** (corner forgiveness), **6.3** (both drop-through inputs in both modes) and **5.2** (Fighter ledge get-ups and first-grab intangibility), dossiers §2.5.1, §2.6.1–§2.6.3 and §2.5.2, under the §0.2 rulings **Q4** (keep ×2.5, measure every hero), **Q5** (own jump only, both modes) and **Q6** (add the sim one-way lip pop). Plus the fix-round extra from `CM_FIXA_STORY.md` §5.1 (Cleopatra's Desert Mirage startup).

Branch `worktree-wf_a70ba1d0-06f-3`, worktree `D:\Projects\Fighters Through Time - V3\.claude\worktrees\wf_a70ba1d0-06f-3`, based on `2d44fa9` (the merged Phase A fix round, **3367** tests).

**This workstream was resumed.** A first agent with the same brief worked for about 30 minutes and was stopped by a usage limit before committing; the orchestrator committed its tree as a WIP checkpoint. I mapped that work against the dossiers, kept what was sound (most of the production code), fixed what was broken (the lip-pop band constant, the Story Tempest lift, the lip pop's corner graze), built what was missing (every test except `FighterLipPopTests`, the Move List row, the action-kind seam, the CPU drill, the tooltip) and rewrote the checkpoint's message into a meaningful one with `git commit-tree` (trees unchanged). Commits, in order:

| Commit | Content |
|---|---|
| `4d3c69d` | The production code (the former WIP checkpoint, reworded) with `FighterLipPopTests` (3) and the in-place `FighterLedgeTests` rewrite |
| `54ab809` | The short hop and the held-Jump hang: `FighterShortHopTests` 5, `FighterMovementFeelTests` +3, `StoryAirPhysicsTests` +3, `StoryMovementFeelTests` +1; Story's Tempest lift; the lip-pop band as an exact `double` |
| `bd2002e` | Drop-through: `FighterDropThroughTests` 4, `FeelPassStoryTests` +1 |
| `36919a5` | Corner forgiveness: `StoryCornerForgivenessTests` 4, `FighterStageGeometryTests` +1 (the sim head-clip guard), the lip-pop clearance, the content reach guards' allowance (in place) |
| `51373d0` | Ledge get-ups: `FighterLedgeGetUpTests` 8, `FighterStageGeometryTests` +1, `FighterLedgeRules.GetUpActionKind`, the Move List row, the CPU ledge drill (recorded, §7) |
| `a6300ca` | `PlaytestFlightModelTests` 2; `StoryKitMotionTravelTests` +1 (Cleopatra) |
| `abf9124` | The one-way tooltip names both inputs |
| (this file) | The handoff |

**Fighter hashes move** (every CPU-driven or driver-fed match: component 323's latch bits are hashed state; every first ledge grab; every grounded Down press). No Klotho component, protocol field, save field or new component ID; only the 323 fields and bits §1.5 reserved for CM-MOVE (`DownTapFrames`, `ActionFlags` bits 1–3) and the 305 values (`AttackFlags` 512, universal-movement 8–10).

**Nobody has played the build.** Every claim below is a headless suite, a probe or a bot run.

## 1. Shipped

### 5.1 / 6.1 — the short hop and the held hang (both modes)

- **One constant.** `UniversalMovementRules.ShortHopGravityMultiplier = 2.5f` (decided). `StoryAirPhysics.ShortHopGravityMultiplier` forwards to it.
- **Sim step.** `FighterAirPhysics.StepMovement(vy, fastFalling, floating, jumpedThisAirtime, bool jumpHeld, bool riseFromOwnJump)`. Band order, first match wins: fast-fall; float; **short hop** (`!jumpHeld && riseFromOwnJump && vy > 0`, gravity × 2.5); **apex hang only while `jumpHeld`**; fall; base. The old four-argument overload is `jumpHeld: true, riseFromOwnJump: false` — exactly the pre-change step — so the CPU's arc predictions, `CpuRecoveryProfile`, every harness and the cast-step overload keep planning full hops. `FighterAirPhysics.ShortHopGravityStep` is converted once.
- **The latch** (`FighterMovementSystem.Movement.cs`). `UpdateJumpLatch` writes 323 `ActionFlags` bit 1 `JumpReleasedSincePress` from the tick's raw edges (`Pressed & Jump` clears, `Released & Jump` sets), **above the hitstop gate and every input discard**, so for any `PlayerInputFrame.Create` stream it equals "Jump not held" tick for tick; hand-built frames with no `Released` never set it (full hops). `JumpHeldForGravity(action)` reads it.
- **The Q5 gate.** 323 `ActionFlags` bit 3 `RiseFromOwnJump`: set by `MarkOwnJump` on an accepted ground jump and air jump (`ApplyNormalMovement`) and by the ledge climb jump (`FighterLedgeRules.Process`); cleared by `TickMovementLatches` (after the hitstop gate and the respawn platform's zeroing) when grounded, in hitstun or daze, grab-busy, hanging or climbing a ledge, in a kit motion, in a float window, as an Ultimate caster or captive, and once Shakespeare's Tempest has lifted its caster (`CastLiftedTheCaster`).
- **Story.** `StoryAirPhysics.SelectBand(..., bool riseFromOwnJump = true)` / `StepVertical(..., riseFromOwnJump = true)`: the short hop needs it; the hang needs `jumpHeld`; the launch branch's own short-hop clause is untouched. `PlayerController`'s latch `_riseFromOwnJump` (`PlayerController.CornerForgiveness.cs`; read-only seam `RiseFromOwnJump`) is written with `JumpedThisAirtime` (its setter now writes both: every jump sets it; floor contact, a ledge or rope grab and the Tempest Apex refresh clear it) and cleared by `PrepareMoveAndSlide` in Stunned, Thrown, Dazed, Grabbing, UsingMovementAbility, UsingUltimate, LedgeHanging, Dead, Respawning and while the Tempest lifts. `ApplyGravity` reads `ShortHopReadsOwnJump` (the latch, false while the Tempest's active frames rewrite the rise).
- **Bots.** `PlaytestNav.StepFlight` holds a flight's **final** jump through the hang band (release at `vy ≥ ApexHangSpeedBandPixels`; the release before a planned air jump stays at the apex) and passes `riseFromOwnJump: f.Jumped`; `NavAgent`'s jump hold follows the same rule.

### 6.3 — both drop-through inputs (both modes)

- `UniversalMovementRules.DropThroughDoubleTapFrames = 18` (design value); `StoryCombatRules.DownDoubleTapFrames` forwards to it.
- **Sim.** `ApplyNormalMovement` tries `TryDoubleTapDropThrough` first (a grounded Down press with drop-through allowed: an open window drops — never on the solid base floor — else it opens 323 `DownTapFrames` = 18), then the unchanged Down+Jump (now `BeginDropThrough`: 30 frames, −2 u/s). `TickMovementLatches` decrements the window after the hitstop gate; the respawn platform zeroes it. Taps 17 ticks apart drop; 18 apart reopen the window.
- **Story.** `CheckJumpInput`: a Jump press with Down held on the floor tries `TryDropThrough()` first; on solid ground it stays a jump. Each mode keeps its own `DropThroughFrames`. `tooltip_first_use_one_way` now names both inputs.

### 6.2 — corner forgiveness

- `UniversalMovementRules.HeadClipNudgeUnits = 0.2f` (12 px), `LipPopUnits = 10f/60f` (10 px), `SimOneWayLipPopUnits = 0.2` (a `double`, so `FighterAirPhysics.OneWayLipPopBand` is exactly `FP64.FromDouble(0.2)`).
- **Story** (`PlayerController.CornerForgiveness.cs`, one call `PrepareMoveAndSlide(dt)` before `MoveAndSlide`): in Airborne, an airborne Attacking and the air dodge only, while rising and never under launch gravity — the **head-clip nudge** (a `TestMove` of the tick's rise meets a ceiling, normal Y > 0.7: shifts of 1–12 px away from the contact, the first whose rise is clear) and the **rising lip pop** (a `TestMove` toward the travel meets a wall, |normal X| > 0.7: lifts of 1–10 px, each plus `LipPopClearancePixels` 0.25, the first from which the step is clear). Seams: `HeadClipNudges`, `LipPops`, `HeadClipNudgePixels`, `LipPopPixels`.
- **Sim** (`FighterMovementSystem.TryLipPop`, one call before the ledge capture): on a normal-control airborne tick (no hitstun, daze, Special cast, A02 cast, kit motion or ledge climb; not dropping through) with vertical velocity ≤ 0, feet that came from below a one-way platform's top and are now at most 0.2 u under it, over the platform: placed on the top, vertical velocity zeroed, grounded, jumps refilled (the air dodge and the regrab budget refresh through the ordinary grounded path). Floor segments are never popped. The **head-clip half is a guarded no-op** (`FighterStageGeometryTests.NoSimSurfaceARisingFighterCanMeetFromBelowHasAPartialUnderside`).
- **Bots.** `PlaytestNav.TryNudgeFlight` / `TryPopFlight` model both from the controller's constants.

### 5.2 — Fighter ledge get-ups

- **Constants** (`BasicComboRules`, the ledge anchor beside `LedgeRegrabsPerAirtime`, all *provisional*): `LedgeFirstGrabIntangibleFrames` 30, `LedgeGetUpMinHangFrames` 6, `LedgeNeutralClimbFrames` 20, `LedgeNeutralClimbIntangibleFrames` 12, `LedgeActionClimbFrames` 8, `LedgeAttackStartupFrames` 8 / `ActiveFrames` 4 / `RecoveryFrames` 14, `LedgeAttackDamageMultiplier` 1.0, `LedgeAttackHitstunFrames` 30, `LedgeAttackLaunches` false, `LedgeStandInsetUnits` 0.5.
- **The hang** (`FighterLedgeRules.Process(ref fighter, ref runtime, ref verb, ref defense, ref action, in tuning, in hang, in stand)` → `HangTick`). One action a tick: **Jump** press → the climb jump (unchanged, now `OnActionAccepted(Jump)` and the own-jump bit); **Roll** press without Block or the Echo Step bit → the ledge roll; **BasicAttack** press without Block or the Grab bit → the ledge attack; **Down** held → release (unchanged); a **fresh** toward-stage or Up tilt (`IsClimbTilt`, |stick| > 30) once the hang is 6 frames old and 323 `ActionFlags` bit 2 `LedgeStickRearmed` is set (set on any hang tick whose stick is neutral, away or down; cleared whenever not hanging) → the neutral climb. The old four-argument `Process` (the rulebook suites) sees only presses and never neutral-climbs.
- **The climbs** (305 universal-movement `LedgeClimb` 8, `LedgeRoll` 9, `LedgeAttackClimb` 10, the anchor in `UniversalMovementDirection`). `StartGetUp` clears the hang, calls `OnActionAccepted(GetUpActionKind(phase))` **first** (Swing for the ledge attack, LedgeGetUp otherwise), then max-assigns the climb's intangibility and takes the first climb tick. `AdvanceGetUp` (called from `FighterMovementSystem.AdvanceLedgeGetUp`) moves the fighter linearly from the hang to `FighterStageGeometry.TryGetStandPosition(anchor)` (0.5 u inside the edge, platform or floor segment), then: the neutral climb hands the tick back after 20 ticks; the ledge roll after 8 starts the ordinary `RollStartup` toward the stage; the ledge attack after 8 starts the strike (`FighterBasicAttackRules.StartLedgeAttack`, `FlagLedgeAttack` 512 in `VariantMask`). A hit (hitstun or daze) cancels a climb. `IsCombatLocked` includes 8–10.
- **The strike** (`FighterCombatSystem.ApplyLedgeAttackSwing`, `FighterCombatSystem.LedgeAttack.cs`): string hit 1's box at the latched facing, hit 1's knockback multiplier, one basic, 30-frame block-cancelable hitstun, no launch; a Foresight counter refuses it; the construct sweep treats it as a single strike; Momentum as string hit 1.
- **First-grab intangibility.** `FighterLedgeRules.Grab` max-assigns 30 into `InvulnerabilityFrames` when `LedgeGrabsThisAirtime` was 0.
- **Presentation.** `FighterSimulationDriver.ResolvePresentationAnimation`: the three climbs play `ledge_pull_up`; the ledge roll then plays `roll`, the strike `basic_attack_1`.
- **Move List.** A Fighter-mode `LedgeOptions` row (`movelist_ledge_options`, every number live from `BasicComboRules`); Story mode shows none.

### The fix-round extra

`CleopatraDesertMirage.OnStartup` zeroes the horizontal velocity (Y is left to Story gravity), as Lincoln's Rail Charge startup does: a running Cleopatra now covers 240 px from the press (she slid 69.6 px through the startup before).

## 2. Files changed

Owned: `scripts/Core/UniversalMovementRules.cs` (constants and phases 8–10 at the CM-MOVE anchor), `scripts/Core/StoryAirPhysics.cs`, `scripts/FighterSim/FighterAirPhysics.cs`, `FighterLedgeRules.cs`, `FighterUniversalMovement.cs`, `FighterStageGeometry.cs` (`StandPosition`, `TryGetStandPosition`, the internal `ForTest` seam), new `FighterMovementSystem.Movement.cs`, new `FighterCombatSystem.LedgeAttack.cs`, new `PlayerController.CornerForgiveness.cs`, `scripts/Diagnostics/PlaytestNav.cs`, `scripts/UI/MoveListScreen.cs`, `localization/en.csv` (`# CM-MOVE`) + `en.en.translation`.

Declared call sites: `FighterMovementSystem.Update` (the latch at the top, `TickMovementLatches` after the platform zeroing, the hang and climb calls, the gravity call, the lip-pop call before the ledge capture), `ApplyNormalMovement`'s drop-through and jump lines; `ApplyBasicSwing`'s ledge-attack routing; `FighterBasicAttackRules.cs` (the ledge variant, `VariantMask`, `StartLedgeAttack`); `BasicComboRules.cs` (the ledge section); `StoryCombatRules.DownDoubleTapFrames`; `PlayerController` (the call before `MoveAndSlide`, the Down+Jump branch in `CheckJumpInput`); `FighterSimulationDriver.ResolvePresentationAnimation` (the climb case); the content tests' reach guards.

**Outside the declared lines** (each minimal):
- `PlayerController.cs`: `JumpedThisAirtime` became a property writing the Q5 latch with it (the latch shares its set/clear points); `ApplyGravity`'s `StepVertical` call passes `riseFromOwnJump: ShortHopReadsOwnJump`; two comments (`ProcessCrouching`, `CheckDropThrough`) that described the sim's Down+Jump as separate.
- `scripts/Characters/Abilities/CleopatraAbilities.cs`: one line in `CleopatraDesertMirage.OnStartup` and its doc (the brief's extra).
- `localization/en.csv`: `tooltip_first_use_one_way`'s value (the dossier's "if it names an input").
- Tests: `CpuRecoveryMatrixTests` (the CPU ledge drill, in place), `MoveListScreenTests` (the ledge row, in place), `StoryKitMotionTravelTests` (+1).

## 3. Decisions (provisional unless the user stated the number)

1. **The latch reads the raw edges above the hitstop gate and every discard** (`PROVISIONAL-SIM-SHORT-HOP-LATCH`), so it equals "Jump not held" tick for tick for any `Create` stream, a freeze and a cast included. The plan's alternative sentence ("input-discard paths zero both edges, so the latch freezes there") is the weaker reading; one rule is pinned.
2. **The own-jump bit's clear points** are the list in §1, checked at the top of each normal tick, before the tick's gravity; a kit motion or float window clears it for good (it is not restored when the motion ends).
3. **The sim cast-step overload is unchanged** (plan §2.5.1): an aerial Special keeps the hang after a jump and takes no short hop through its half-gravity startup and active frames. Story's cast reads its `_jumpHeld` as frozen at the cast.
4. **Story's Tempest** counts as a kit lift for Q5 only while its active frames write the rise (`SpecialCastLiftsOwner`); Story zeroes the rise at its recovery anyway.
5. **The lip pop's clearance** (`LipPopClearancePixels` 0.25 px, above the body's 0.08 px safe margin): a whole-pixel lift alone grazes the lip's corner and is refused, so a 10 px lip never popped. With it a 10 px lip pops and 11 px does not.
6. **The sim lip pop's drop-through exemption** is the whole 30-frame `DropThroughFrames` window (the guard every one-way landing already has); by its end the fighter is far below the band. It is also skipped for a fighter in hitstun, daze, a Special or A02 cast, a kit motion or a ledge climb.
7. **The ledge get-up frames** (§1's constants, `PROVISIONAL-LEDGE-GETUP-FRAMES`), the 30-of-127 tilt threshold (the sim's "direction held" everywhere else), the linear climb path from the hang to the stand position over the intangible frames (the neutral climb stands its last 8 vulnerable frames at the stand position), and the ledge attack's Momentum (string hit 1's 20 frames).
8. **The neutral-climb re-arm stays set for the rest of the hang**: once the stick has been neutral, away or down on any hang tick, the first toward-stage or Up tick at 6+ frames climbs (an early tilt waits for frame 6 rather than needing another re-arm).
9. **The Move List row is Fighter-mode only.**

## 4. Deviations from the plan

1. **The CPU does not stay climb-jump only** (§2.5.2: "The CPU's delivered hang decision … always climb-jumps"). The premise is wrong in the code: recovery steer (toward the stage) and neutral-combat presses (BasicAttack) decided before the grab are delivered on hang ticks after the CPU's own hang decision, whose Jump edge waits for the edge pulse; a centred stick from that decision re-arms the climb, and the late steer then climbs. Measured with the CPU unchanged (the in-place drill, 90 drills per band over the three Open stages): Easy **38** get-ups in 170 hangs, Normal **15** in 135, Hard **6** in 173 — nearly all neutral climbs (universal-movement 8), a few ledge attacks (10), no ledge rolls (9). CM-MOVE owns no CPU code, so per the plan this is recorded: the drill's assertion is held behind `CpuRecoveryMatrixTests.CpuClimbJumpOnlyEnforced = false` (it prints the counts and still asserts that every sweep reaches a ledge). **The fix is one line** in `FighterCpuController.Sample`, just before the final `PlayerInputFrame.Create`:
   ```csharp
   if (observation.IsLedgeHanging != 0) { outputMoveX = 0; outputMoveY = 0; held &= ~(GameplayButtons.Roll | GameplayButtons.BasicAttack); }
   ```
   Tried locally and reverted: with it the drills read **0** get-ups on every stage and band (Easy 266 hangs / 258 climb jumps, Normal 158 / 158, Hard 190 / 187), and `CpuRecoveryMatrixTests`, `FighterLedgeTests` and `FighterCpuBehaviorTests` were 68/68 with the flag set to true. The orchestrator decides it with CM-CANCEL (the Phase B CPU owner) and flips the flag in the same change.
2. **`StoryAirPhysicsTests`' "`Band(0, false, true) == Fall`"** (plan §2.6.1) is `Rise` at exactly vy 0 (base gravity, not the hang — the point of the case); the case pins that and `Band(60, false, true) == Fall`.
3. **`FighterLedgeGetUpTests` case 3 cannot see the protection end yet.** The chokepoint body is empty on this base (CM-HIT fills it). The case pins `GetUpActionKind` (the ledge attack hands the chokepoint `Swing`, which `IsOffensive`), the strike, and the climb's own intangibility surviving a shorter grant; the respawn/Defy end rides CM-HIT's chokepoint suites after the merge.
4. **`FighterTumbleTests` and `StoryTumbleTests` were not run**: they are CM-HIT's, a parallel Phase B branch not in this base. The Q5 post-hitstun case uses ordinary hitstun (`FighterShortHopTests` case 5 and `StoryMovementFeelTests`' new case); re-run both tumble suites at the CM-HIT ∪ CM-MOVE merge (`Tumble = 2` must clear the own-jump bit — it does, through `HitstunFrames > 0`, as long as tumble outlives hitstun with the bit already cleared).
5. **The re-run of `CpuRecoveryMatrixTests` cannot show the lip pop**: its drills integrate their own physics and model no ledge or platform pop. The real-simulation CPU ledge drill (deviation 1) is where it shows (§6).
6. **The plan's `StoryCornerForgivenessTests` case 1 bound** ("clips of ≤ 12 px slide past") holds; the measured shift for a 12 px clip is 12.06 px (the slide's own margin recovery), asserted within 0.25 px.

## 5. Tests

**Declared +36, plus the extra +1, delivered +37** (3367 → **3404**):
- `FighterShortHopTests` 5 (new, `[RequireGodotRuntime]`): `TapsOfOneTwoAndThreeTicksRiseMonotonicallyAndStayUnderFiftyFivePercentOfTheHeldApex`, `AHeldJumpIsUnchangedTickForTick`, `TheLaunchStatesNeverTakeTheBand`, `AReleasedAirJumpShortHops`, `APostHitstunRiseAndTheTempestLiftAreNotCutButAReleasedOwnJumpIs`.
- `FighterMovementFeelTests` +3: `TheShortHopLatchEqualsJumpNotHeldForACreateBuiltStream`, `AShortHopTapAndHoldScriptConvergesUnderRollback`, `TheShortHopAndHangBandOrderHoldsAndAHeldJumpIsTheLegacyStep`.
- `StoryAirPhysicsTests` +3: `TheApexHangNeedsJumpHeld`, `TheShortHopCutsOnlyTheHerosOwnJump` (Q5), `AReleasedHopDropsThroughItsPeakWithNoHangAndAHeldJumpKeepsIt`.
- `StoryMovementFeelTests` +1: `AReleasedHopHasNoHangAndARiseAfterAStunIsNotCut`.
- `FighterLipPopTests` 3 (new, pure C#): `AFighterPointNineteenUnderAOneWayTopAtItsApexLandsOnItAndPointTwentyOneDoesNot`, `AFighterInsideTheBandAtANearEdgeApexLandsRatherThanGrabbingTheLedge`, `ADroppingFighterIsNeverPoppedBackAndAPopScriptConvergesUnderRollback`.
- `StoryCornerForgivenessTests` 4 (new): `ACeilingCornerClippingTheHeadByUpToTwelvePixelsIsSlidPast`, `AThirteenPixelClipBonks`, `AWallTopUpToTenPixelsAboveTheFeetIsPoppedOntoAndElevenIsNot`, `ALaunchedHeroGetsNeitherTheNudgeNorThePop`.
- `FighterStageGeometryTests` +2: `NoSimSurfaceARisingFighterCanMeetFromBelowHasAPartialUnderside`, `HeldJumpsEscapeEveryOpenStagePitAndTheNeutralClimbStandsOnTheFloor`.
- `FighterDropThroughTests` 4 (new, pure C#): `ADoubleTapInsideEighteenTicksDropsAndSlowerTapsDoNot`, `TheSolidBaseFloorNeverDropsForEitherInput`, `ShieldstunRefusesTheDropThroughAndOpensNoWindow`, `DownJumpStillDropsAndASnapshotBetweenTapsResumesIdentically`.
- `FeelPassStoryTests` +1: `DownJumpDropsThroughAOneWayPlatformFromEveryGroundedStateAndJumpsOnSolidFloor`.
- `FighterLedgeGetUpTests` 8 (new, pure C#): `EachGetUpEndsOnThePlatformAtItsFrameCount`, `EachGetUpIsIntangibleForItsClimbFrames`, `TheLedgeAttackStrikesATargetAtTheEdgeAndIsAnOffensiveActionWhoseClimbFramesSurvive`, `TheFirstGrabIsIntangibleAndARegrabIsNot`, `TheBlockChordsAndTheLogicalChordBitsStartNothingFromTheHang`, `AHitInTheVulnerableFramesEndsTheGetUp`, `TheNeutralClimbNeedsAFreshTiltAfterSixFramesAndJumpWinsItsTick`, `EveryGetUpConvergesUnderRollback`.
- `PlaytestFlightModelTests` 2 (new): `AHeldFinalJumpKeepsItsHangAndTheModelLandsWithTheController`, `TheModelNudgesAndPopsLikeTheController`.
- The extra, `StoryKitMotionTravelTests` +1: `CleopatrasRunningRushTravelsItsAuthoredDistanceFromThePress`.

**Rewritten or extended in place (no count change):**
- `FighterLedgeTests.HangingSuppressesAttacksSpecialsAndBlockAndIgnoresRollAndMovement` → `HangingSuppressesSpecialsAndBlockAndTheBlockHeldRollAndAttackStartNothing` (the mash holds Block, so Block+Roll and Block+Attack start nothing, and the stick stays neutral; the old alternating stick now re-arms and climbs).
- `CpuRecoveryMatrixTests`: every Open-stage case runs the real-simulation CPU ledge drill (`AssertTheCpuLeavesALedgeOnlyByTheClimbJump`, deviation 1).
- `MoveListScreenTests.TheScreenBuildsForAllNineRosterIDsWithoutError`: the ledge row, from `BasicComboRules`, Fighter mode only.
- The reach guards gain the 10 px lip-pop allowance: `TutorialCalibrationContentTests.TheDoubleJumpGateNeedsTheSecondJumpFromEveryHero` (240 px > 214 × 1.05 + 10), `ArenaGeometryContentTests.ReachPixels`, `Level09ContentTests.TheEastBoundaryWallRunsTheFullHeightAndOutreachesEveryJumpFromTheCastleDeck`, `Level10ContentTests.TheYardWallLeavesNoGapUnderTheBridgeAndTheBridgeIsBeyondEveryJump`. All still pass.
- `FeelPassStoryTests.ADoubleTapOfDownDropsThroughAOneWayPlatformFromStandingAndRunning`: a comment only.
- `FighterMovementFeelTests.AJumpHangsAtItsApexButALedgeDropDoesNot` and `ALedgeClimbJumpHangsAtItsApexAfterAWalkOffCapture` needed no change (their frames set no `Released`).
- The harness audit for Down on consecutive grounded ticks: nothing needed re-timing (the full run is green).

**Shared test files touched** (§3.2 map): `CpuRecoveryMatrixTests`, `FighterLedgeTests` (both in place, as named above).

**Runs** (each holding `D:/Projects/.ftt-gdunit-lock`, `testhost` / `Godot*_console` drained first):
- Filtered, per milestone: the §4.1 CM-MOVE set (`FighterShortHop`, `FighterMovementFeel`, `StoryAirPhysics`, `StoryMovementFeel`, `StoryCornerForgiveness`, `FighterStageGeometry`, `FighterDropThrough`, `FeelPassStory`, `FighterLedge`, `CpuRecoveryMatrix`, `PlaytestFlightModel`, `ArenaGeometryContent`, `TutorialCalibrationContent`, `Level09Content`, `Level10Content`, `MoveListScreen`) plus `FighterLipPop`, `FighterLedgeGetUp`, `StoryKitMotionTravel`, `CleopatraSandDecoy`, `StoryMovementAbilityGravity`, `UnusedTranslationKey`, `ScriptTranslationKey`, `FighterSimulationTests`: all green.
- **Full run: `Passed! - Failed: 0, Passed: 3404, Skipped: 0, Total: 3404, Duration: 1 m 33 s`**, reconciled exactly against 3367 + 37. The `[TestCase]` attribute count under `tests/` is 3404.
- `dotnet build --no-incremental`: 0 errors, the pre-existing CS8632 and `KLSG_ECS004` (`FighterTuningComponent`) only; no `KLOTHO_DET002`, no `KLSG_ECS004` on 323.
- `--headless --import` after each `en.csv` edit (exit 0); `en.en.translation` committed.

## 6. Measurements

**Short-hop heights (Q4, `PROVISIONAL-SIM-SHORT-HOP-HEIGHT`)**, Create-built, Jump held on the press tick and the next 0 / 1 / 2:

| Hero | Held apex | 1-tick tap | 2-tick tap | 3-tick tap |
|---|---|---|---|---|
| einstein | 2.537 u | 1.062 (41.9 %) | 1.179 (46.5 %) | 1.292 (50.9 %) |
| joan | 2.450 u | 1.021 (41.7 %) | 1.133 (46.3 %) | 1.246 (50.9 %) |
| leonardo | 2.337 u | 0.979 (41.9 %) | 1.092 (46.7 %) | 1.200 (51.3 %) |
| lincoln | 2.146 u | 0.900 (41.9 %) | 1.008 (47.0 %) | 1.108 (51.7 %) |
| cleopatra | 2.654 u | 1.104 (41.6 %) | 1.225 (46.2 %) | 1.337 (50.4 %) |
| tesla | 2.337 u | 0.979 (41.9 %) | 1.092 (46.7 %) | 1.200 (51.3 %) |
| shakespeare | 2.450 u | 1.021 (41.7 %) | 1.133 (46.3 %) | 1.246 (50.9 %) |
| mozart | 2.537 u | 1.062 (41.9 %) | 1.179 (46.5 %) | 1.292 (50.9 %) |
| tubman | 2.537 u | 1.062 (41.9 %) | 1.179 (46.5 %) | 1.292 (50.9 %) |

Every 1-tick tap is **below 45 %** (41.6–41.9 %) — ledger it under Q4, not tuned. 2- and 3-tick taps are inside 45–55 %.

**The CPU's ledge exits** (deviation 1), per Open stage and band (drills / hangs / climb jumps / get-ups), CPU unchanged: Paris Easy 36 / 111 / 82 / 27, Normal 36 / 90 / 83 / 7, Hard 36 / 126 / 118 / 6; Vesuvius 18 / 27 / 25 / 2, 18 / 21 / 20 / 1, 18 / 18 / 18 / 0; Nassau 18 / 32 / 23 / 9, 18 / 24 / 17 / 7, 18 / 29 / 29 / 0.

**Captures that became lip-pop landings.** A probe with `TryLipPop` disabled (not committed): `FighterLedgeTests` 24/24, `FighterLedgeGetUpTests` 8/8, `FighterStageGeometryTests` 19/19 and `CpuRecoveryMatrixTests` 11/11 were green either way, so no existing ledge case depends on the pop. In the CPU drills one capture became a landing (Vesuvius Hard 19 → 18 hangs) and one recovery reached a ledge it had missed (Paris Normal 89 → 90); the Open-stage pits are floor segments, which are never popped. `CpuRecoveryMatrixTests`' own drills are unchanged (deviation 5): all nine kits recover as before on all three Open stages.

**Corner forgiveness:** a 12 px clip shifts 12.06 px on its contact tick; the lip pop needs the 0.25 px clearance (decision 5).

**Bot runs** (`play`, Joan, Normal, `--max-seconds=0`, tag `cmmove`, lock held): Level 2 — `left_level:collapse(Death)` at 175 s, 3 of 3 locks cleared, 2 checkpoints, 4 deaths, `siegemaster_duke` 337 of 456 damage taken (the boss fight). Level 7 — `level_complete` in 167 s, 2 of 2 locks, 3 checkpoints, 3 deaths. No stall, no stuck lock, no script error (Level 7 printed the documented "10 resources still in use at exit"). There is no before run in this checkout to compare against; bot batches before and after this package are not comparable (6.1 and 6.2 change the flight model).

## 7. Not delivered / open

- **The CPU leaves ledges by get-ups** (deviation 1): the one-line CPU change and flipping `CpuClimbJumpOnlyEnforced` are the orchestrator's (with CM-CANCEL).
- The protection-ending half of `FighterLedgeGetUpTests` case 3 waits on CM-HIT's chokepoint body (deviation 3); the tumble suites were not run (deviation 4).
- The sim's aerial cast step keeps the hang without the Jump-held gate and has no short hop (decision 3); Story's cast reads its frozen `_jumpHeld`. A small cross-mode difference inside aerial Specials.
- A Story buffered jump that lands with Down held still jumps, not drops (recorded by the plan).
- A grab that seizes a fighter in the neutral climb's vulnerable frames leaves the climb in the 305 slot until the throw's hitstun cancels it; nothing reads it meanwhile (the held branch runs first and the driver shows `hitstun`).
- On the legacy flat arena (no platforms) the ground is the historical drop-through floor, so a double tap of Down drops through it for 30 frames as Down+Jump always did.
- Story's ledge (`LedgeGrabPoint`) has no get-ups (Fighter only, as decided).
- **Nobody has played or looked at any of it**: not a short hop, a released hop's peak, a nudge, a lip pop, a sim pop, a double tap, a ledge get-up or the new Move List row.

## 8. For the closeout

**AGENTS.md:**
- The universal-movement bullet: the sim gains the ×2.5 short hop (`UniversalMovementRules.ShortHopGravityMultiplier`, Story forwards); in both modes the band cuts only a rise from the fighter's own Jump press this airtime (Q5), and the apex hang applies only while Jump is held (6.1); the corner forgiveness (Story nudge 12 px and lip pop 10 px; the sim's 0.2 u one-way lip pop before the ledge capture; no sim head-clip case); both drop-through inputs in both modes (18-tick double tap).
- The ledge sentence: the Fighter get-ups (neutral climb on a fresh tilt, ledge roll, ledge attack) and the first grab's 30 intangible frames; Story's `LedgeGrabPoint` unchanged.
- The cross-mode parity bullet: close the short-hop half of H-7 (`DEFER-H7-STORY-TERMINAL` keeps the launch band) and **M-18** (drop-through).
- The Klotho inventory: 305 `AttackFlags` 512 `FlagLedgeAttack`, universal-movement 8–10 (`LedgeClimb`, `LedgeRoll`, `LedgeAttackClimb`); 323 `DownTapFrames` and `ActionFlags` bits 1–3 now written.
- The validation section: the baseline moves by +37 (the suites in §5).

**CLAUDE.md:** "How the bots move" — a flight's final jump is held through the apex-hang band (the hang needs Jump held), the model applies the controller's head-clip nudge and rising lip pop, and the short hop cuts only the hero's own jump; the comparability note — bot batches from before and after this package are not comparable.

**Design master** (`design-godot.md` at `e2c655a`, per the plan; no `DOC-MASTER-UNBOUND-BINDING` line): short hop :957, :977, :987, :1464–1467; the hang :956–958, :978; corner forgiveness — a clause in Physics Constants (:950–966) and One-Way Platforms (:1006–1027); drop-through — delete :1027, amend :1007, :1012, :1024, :1483–1488, :2279; ledge get-ups :790, :994–1003.

**Contracts:** `CPU_RECOVERY.md` (both copies, identical today): the CPU's ledge answer is the climb jump only, and the get-ups exist (after the CPU line lands). `GAMEPLAY_FEEL_2026-08-10_PLAN.md` §2.11: a supersession note for the get-ups.

**Ledger rows:**
- Add `USER-SIM-SHORT-HOP` (PFC-09), `PROVISIONAL-SIM-SHORT-HOP-LATCH` (decisions 1–2), `USER-SHORT-HOP-OWN-JUMP-ONLY` (Q5, both modes; amends Story's `SelectBand`), `PROVISIONAL-SIM-SHORT-HOP-HEIGHT` (the table above; every 1-tick tap below 45 %).
- Add `USER-APEX-HANG-HELD`, amending `USER-MOVEMENT-FEEL` and `PROVISIONAL-MOVEMENT-FEEL-RULES` (3).
- Add `USER-CORNER-FORGIVENESS` (MOV-05, both modes per Q6), `PROVISIONAL-CORNER-FORGIVENESS-RULES` (12 / 10 px, the 0.25 px clearance, the normal thresholds, the states), `PROVISIONAL-SIM-LIP-POP` (0.2 u, the apex/fall condition, before `TryGrabLedge`, the drop-through exemption, the measured capture change), `VERIFY-SIM-PIT-FLOOR-SNAP` (the any-depth pit-floor snap, unchanged; touches `VERIFY-PARIS-DEEP-PIT-RECOVERY`).
- Add `USER-DROP-THROUGH-BOTH-INPUTS` (MOV-06, TWO-11) and close M-18.
- Add `USER-FIGHTER-LEDGE-GETUPS` (PFC-12) and `PROVISIONAL-LEDGE-GETUP-FRAMES` (decisions 7–8).
- Add a `VERIFY-*` row for the CPU's ledge get-ups (deviation 1, with the counts and the proposed line), closed when the CPU line and the flag land.
- Close the short-hop half of `DEFER-H7-STORY-TERMINAL`.
- Record Cleopatra's held Desert Mirage startup (the CM-FIXA-STORY §5.1 row the user may want), beside `FIX-STORY-RAIL-CHARGE-RECOVERY-CARRY`.
