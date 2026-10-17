# CM-CANCEL handoff: D3 in Fighter, and the CPU and the Mirror adapted to Special timing

Combat and movement update 2026-10-16 (`docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md`), Phase B workstream **CM-CANCEL**: decisions **11.1** (D3 as one rule in both modes) and **9.3** (adapt the CPU and the Level 13 Mirror adapter only as far as the new Special timing requires), dossiers 2.11.1 and 2.9.3, plus the fix round's open CPU item (CM_FIXA_SIM §5: the CPU's grab mirror had no knockdown or A02 term). Decisions file: `D:/Projects/genre_review_2026-10-13/DECISIONS_combat_movement.md`.

**Branch** `worktree-wf_a70ba1d0-06f-4`, worktree `D:\Projects\Fighters Through Time - V3\.claude\worktrees\wf_a70ba1d0-06f-4`. **Base** `2d44fa9` (Phase A plus its fix round merged; **3367** tests, not the plan's 3332).

A first agent with this brief was stopped by a usage limit; its uncommitted work was checkpointed as `955450c` ("WIP checkpoint"). I reviewed and kept it, fixed what was broken, finished the rest, then `git reset --soft 2d44fa9` and recommitted in logical pieces. The WIP commit is not on the branch, and neither are its two scratch files.

| Commit | Content |
|---|---|
| `dec3c21` | One D3 table in `BasicComboRules`; Story forwards to it; `SpecialRecoveryCancelTableTests` (1) |
| `e5c3fc6` | D3 in Fighter; `FighterSpecialRecoveryCancelTests` (13); `TubmanKitTests` extended in place |
| `7133cea` | The CPU and the Mirror read their own cast; FighterCpuBehaviorTests +2, MirrorParadoxTests +2, `CpuSelfActionStateTests` (3), `FighterCpuSpecialTimingSmokeTests` (1) |
| (this file) | The handoff |

**Fighter hashes move.** That covers every match where a Special lands (the new 323 buffer and flag are hashed state, and a cancel changes the timeline) and every CPU-driven match (the CPU's early-outs move where its RNG draws fall). No Klotho component, protocol field, save field or `en.csv` key changed. Only component 323's reserved CM-CANCEL fields are written: `SpecialFlags` bit 8 and `SpecialCancelBuffer`.

**Nobody played the build.** Every claim below comes from a headless suite, a headless smoke run, or the recorded CPU smoke.

## 1. Shipped

### The D3 table (both modes)

`BasicComboRules` has a new D3 section, after the ability-lockout region (after `IsSpecialLocked`):

| Member | Value |
|---|---|
| `SpecialRecoveryEvadeCancelFrame` | 0 |
| `SpecialRecoveryActionCancelFrame` | 6 |
| `SpecialCancelBufferFrames` | 6 |
| `SpecialRecoveryAllowsEvade(landed, frame)` | |
| `SpecialRecoveryAllowsAction(landed, frame)` | |
| `SpecialCancelBufferHolds(age)` | |

`StoryCombatRules`' five D3 names now forward to these: the three constants by assignment (`= BasicComboRules.X`), the three predicates by call. Story's D3 behaviour is unchanged (`StorySpecialRecoveryCancelTests` 11/11 green, unedited).

### D3 in the Fighter sim (`FighterSpecialCancelRules`, new `scripts/FighterSim/FighterSpecialCancel.cs`)

**State.** All of it lives on component 323:
- `FighterSpecialCastRules.FlagLandedDirectHit` (8) in `SpecialFlags`.
- `SpecialCancelBuffer`: bits 0–2 are the verb (`VerbNone` 0, `VerbRoll` 1, `VerbJump` 2, `VerbBasic` 3, `VerbMovement` 4). Bits 3–6 are the age in unfrozen ticks. `UpHeldBit` (bit 7) and `DownHeldBit` (bit 8) record the stick at the press.

**Arming: `NoteLanded(ref cast, hitSlot, FighterHitOutcome, damage, in FighterAbilityHitData contract)`.** It arms only when all of these hold:
- the outcome is `Landed` or `Absorbed` (D1a);
- damage > 0;
- the cast is busy;
- `cast.SpecialSlot == hitSlot`;
- the slot's projected `Delivery` is `DirectHit`.

These hit sites switched from `ApplyFighterHit` to `ResolveFighterHit` and call `NoteLanded`:
- Divine Piercing's thrusts (`FighterEntitySystems`).
- The Special projectile and ground-wave contact. Construct bolts and Ultimate-origin shots are skipped, and the slot is `ProjectileTypeID % 10`.
- The zone pulse. Ultimate zones are skipped, and the slot is `ZoneTypeID % 10`.
- `FighterProjectileBurstRules.Burst` (`FighterKitRules`).
- `FighterLorentzChainRules.FireArcs`. It gained the attacker's 323 and the Lorentz contract, and `TryResolveLorentzChain` passes them through.
- The Foresight answer (`ResolveForesightAnswer` gained Tubman's 323).
- The melee `ApplyIntent`. It gained the `FighterHitContractTable` parameter, and Kind 2 / 4 map to slot 1 / 2.

What arms in the shipped roster:
- These arm D3: Golden Ratio, the Lorentz Pulse and the Requiem burst (zone pulses that Story authors as direct hits).
- These never do: the Rift and the Vortex (Tick), the Tempest (Tick), constructs and Cleopatra's two Specials.

**The buffer: `Feed(ref cast, in fighter, in runtime, hitstopTick)`.** Story's `BufferSpecialCancelPress`, in the same order.
- It takes the first verb pressed, in the order roll, jump, basic, movement.
- The latest press replaces the one held. The held press ages first on unfrozen ticks and drops past age 6.
- A hitstop tick feeds the buffer but never ages it.
- These presses are never buffered: the Echo Step chord's Roll (the EchoStep bit, or Block held with chords on); the grab chord's BasicAttack (the Grab bit, or Block held on the ground with chords on); a Movement press on an Ultimate-pressed tick. An Ultimate press also drops a buffered Movement press.
- When no cast is busy, the buffer is zeroed.

**The cast region: `FighterMovementSystem.ResolveSpecialCastRegion`** (new partial `scripts/FighterSim/FighterMovementSystem.SpecialCancel.cs`). It replaces CM-SIM's inline block in `Update`, with one call line at the same site. The order:
1. It feeds the buffer.
2. A busy cast in its recovery with the grounded Block stance up is Block-cancelled. This covers a clocked Special's recovery, and also Foresight's whiff recovery and sidestep. The cast ends, the buffer is emptied, and `PressedButtons &= BlockButton` (fix round A ruling (d)). D3 never fires on a tick the Block cancel won, and a press the mask dropped is never re-fed.
3. Otherwise `TryFireSpecialCancel` fires a landed cast's buffered verb, if its frame gate is open and `CancelVerbCanStart` says the verb's ordinary path would start it now. `RewriteInput` makes that verb's bit the tick's only press; the stick and the held buttons stay. For a basic it sets `MoveY` to ±127 or 0, and the held Down, from the press-time direction. For a jump it drops the held Down, so a grounded cancel is never a drop-through. Then `EndCastForCancel` runs.
4. Otherwise the busy caster's input is discarded (the cast lock).

The verb then starts through its normal path (`TryStartUniversalMovement`, `ApplyNormalMovement`, `ProcessBasicAttackPhase` with `SelectAttackVariant`, the ability system's movement branch), each passing `OnActionAccepted`. A verb that cannot start yet stays buffered until it ages out. Examples: no jump left, the air dodge spent, Root, a cooldown.

**The hitstop branch.** One `Feed(..., hitstopTick: true)` call sits after `ClearIfInterrupted` and before the busy-caster discard. The knockdown / A02 `DiscardButtons` runs after both.

**Foresight: `FighterForesightRules.CancelStance(ref counter, ref fighter)`** (`FighterCounter.cs`).
- A Block or D3 cancel ends the 322 stance.
- From the sidestep it zeroes `InvulnerabilityFrames` only when they are ≤ `PhaseFrames + 1`, which is exactly the sidestep's own grant. A larger grant (a respawn, a tech) stays whole.
- `TryGetRecoveryFrame` gives Foresight's recovery frame:
  - whiff recovery: `ForesightWhiffRecoveryFrames − PhaseFrames`;
  - sidestep: `ForesightSidestepFrames − PhaseFrames`. Its first tick is frame 1, Story's earliest Foresight cancel.

### The CPU and the Mirror (decision 9.3)

- **`CpuDecisionObservation`** gains:
  - `SelfSpecialCastPhase`, on the mode-neutral scale `CpuCastPhase` (`None` 0, `Startup` 1, `Active` 2, `Recovery` 3; the sim's own phase values);
  - `SelfSpecialCastPhaseFrames`;
  - `SelfTechLockoutFrames`, `SelfKnockedDown` and `SelfUltimateCasterBusy`.
- **`CpuVerbState`** gains the matching five fields. `FighterSimulationWorldObserver.TryGetVerbState` fills them from:
  - 323, or for a Delegated cast from 322's stance, through `CpuCastPhase.FromSim`: startup → Startup, window → Active, whiff recovery and sidestep → Recovery;
  - 310 `TechLockoutFrames`, 320 (`FighterKnockdownRules.IsActive`) and 321 (`IsCasterBusy`).
- **`FighterCpuController`:**
  - `AbilityLockoutClearAtDelivery` and `SpecialLockoutClearAtDelivery` also require `CastIdleAtDelivery`: no cast, or a recovery with no more frames left than the band's minimum reaction delay (Hard 4).
  - New: at delivery, a Special 1, Special 2 or Ultimate edge is dropped while `CastHoldsThisTick`. That is any busy phase except the recovery's last frame, which the sim's movement pass ends before it reads the press.
  - `CanStartGrab` gains the cast, the tech lock, the knockdown and the A02 terms; `CanStartEchoStep` gains the cast term. Both are read at decision time and again at delivery (`VerbStillLegalForSelf`).
  - Input-side only: no snapshot state, no RNG draw.
- **The Mirror adapter:**
  - `MirrorParadoxDecisionAdapter.Observe` fills the cast fields through `ProjectSpecialCastPhase(BaseSpecial, out frames)`. Its mapping: `Startup` / `Active` / `Recovery` with `PhaseFramesRemaining`, `Cleanup` → Recovery with 0 frames, none → None.
  - It reads the clone's `PlayerController.ExecutingSpecialCast` (new read-only partial `scripts/Characters/PlayerController.CastObservation.cs`). That is the Special 1 or 2 node while the body is `UsingSpecial` and the node `IsExecuting`.
  - The adapter leaves the verb layer absent, as before.

## 2. Files changed

Owned (§3.2):
- New: `scripts/FighterSim/FighterSpecialCancel.cs`, `scripts/FighterSim/FighterMovementSystem.SpecialCancel.cs`, `scripts/Characters/PlayerController.CastObservation.cs` (each with its `.uid`).
- Edited:
  - `FighterSpecialCast.cs` (the `SpecialCancelBuffer` doc only);
  - `FighterCounter.cs` (`CancelStance`);
  - `FighterCpuController.cs`;
  - `CpuDecisionObservation.cs`;
  - `MirrorParadoxController.cs` (the adapter's `Observe` and `ProjectSpecialCastPhase`);
  - `StoryCombatRules.cs` (the D3 forwarders).

Declared sites in shared files:
- `BasicComboRules.cs`: the D3 section after the ability-lockout region.
- `FighterSimulationSystems.cs`:
  - the cast-region call line, which replaces CM-SIM's inline Block-cancel/discard block; its body moved into the new partial, ruling (d) included;
  - the hitstop-branch feed;
  - `ApplyIntent`'s resolution.
- `FighterEntitySystems.cs`: the Piercing thrust, the projectile contact, the zone pulse and the Lorentz-chain call.
- `FighterKitRules.cs`: the burst and `FireArcs`.

**Outside the declared lines (minimal):**
- `FighterSimulationSystems.ResolveForesightAnswer` (the Foresight answer site, which the plan lists among the hit sites; it lives in this file). It gained a `ref FighterActionStateComponent` parameter.
- `ApplyIntent`'s and `TryResolveLorentzChain`'s parameter lists, and their two call lines.

## 3. Decisions (provisional; ledger `PROVISIONAL-FIGHTER-SPECIAL-RECOVERY-CANCEL` unless noted)

1. **The buffer's verb order and exclusions** are Story's `BufferSpecialCancelPress` exactly: roll, jump, basic, movement; the latest press wins; the Echo Step chord, the grab chord and the Movement half of an Ultimate tick never buffer. The sim reads the Echo Step and grab chords from Block held with chords on (no `DirectOrigin`), as its own resolver does.
2. **A verb that cannot start yet stays buffered** (Story's `if (!started) return false`). `CancelVerbCanStart` mirrors the gates of each verb's path, so a cancel never ends the cast for a verb that then does nothing.
3. **The hitstop feed sits after `ClearIfInterrupted`.** CM_SIM's guide put it before. Both are before the discard, which is the plan's requirement. This way an interrupted cast's buffer is emptied at once instead of fed.
4. **The buffer opens empty without touching `Begin`.** Every feed that finds no cast busy zeroes it, and such a feed runs on the movement pass of every tick a new cast can be accepted on, the freeze-buffer thaw tick included. I tried zeroing in `Begin` first, but that breaks CM-SIM's `FighterSpecialCastRulesTests.EveryInterruptClearsTheCastAndLeavesTheOtherFieldsAlone`, so it was reverted.
5. **The Foresight sidestep cancel cuts `InvulnerabilityFrames` only when ≤ `PhaseFrames + 1`.** The sidestep's grant is max-assigned at the trigger and counts down with the stance. A larger grant belongs to another source.
6. **The slot attribution relies on a lifetime invariant**, not on growing components 302/309: no DirectHit Special projectile, zone or Requiem burst outlives its slot's earliest re-cast (the cooldown minus the 120-frame Momentum cap, minus every Requiem shave for Fortissimo). `FighterSpecialRecoveryCancelTests` case 12 pins it over all nine production kits (9 slots checked).
7. **`CastIdleAtDelivery` uses the band's minimum reaction delay**, as the lockout predicates do. It adds no draw. A cast stretched by hitstop can still refuse the press, which costs nothing.
8. **The delivery-time drop (`CastHoldsThisTick`) is my addition** (ledger `PROVISIONAL-CPU-SPECIAL-TIMING`, with `USER-CPU-SPECIAL-TIMING-ONLY`). Without it the CPU kept pressing into its own cast whenever it decided a second Special or the Ultimate before the first press's cast was visible: the smoke counted 39 such presses across the ten stages, 0–3 per fighter. With it the count is 1.
   - The remaining one is a press on a cast's last recovery frame while frozen. The observation carries no self hitstop, so the CPU treats that frame as free; the sim discards the press.
9. **The Mirror's one-tick `Cleanup`** reads Recovery with 0 frames. A Delegated stance that has ended but is not yet cleared reads Recovery.

## 4. Deviations from the plan

1. **New files instead of growing `FighterSpecialCast.cs`.**
   - The rules live in `FighterSpecialCancel.cs`, and the cast region's body in `FighterMovementSystem.SpecialCancel.cs`.
   - CM-SIM's inline block in `Update` became one call line.
   - `FighterSpecialCast.cs` only had its field doc updated.
2. **Lincoln's Splitting Strike is not reach-checked before the CPU presses it.**
   - My brief summarised 9.3 as including "Lincoln's Splitting Strike checked before pressing". The plan (§2.9.3) lists kit awareness ("CPU Lincoln's Splitting Strike whiffs and spends") as **not in scope**, and the user's ruling is "Leave for now — only adapt it to the new Special timing".
   - I followed the plan and the ruling. The Special 2 rung already fires only inside the 2.0-unit close range, against a 2.2-unit arc.
   - The smoke records Lincoln's Splitting casts and whiffs (§6) for the pad session's row 25. If the orchestrator wants a reach gate, it is a one-line rung condition, but it is kit-specific and needs a ruling.
3. **Extras beyond the plan.** All are input-side or tests:
   - the delivery-time drop (Decision 8);
   - the CPU grab mirror's knockdown / tech / A02 terms (the fix round's open item);
   - case 13 of the D3 suite (`PlayerInputFrame.Create`-built input, §1.8);
   - the new `CpuSelfActionStateTests` (3).
4. **"The admission counters do not advance"** is asserted on `GrabOpportunityID`. The Echo Step admission roll is keyed to a private execution ID, so that half asserts that no chord comes during the cast and one comes after it.
5. **`TubmanKitTests` were extended, not renamed.**
   - `AWhiffedForesightLocksHerThroughTheStanceAndArmsTheTenSecondCooldown` gains a second run: Block held from T+1 raises the stance on the whiff recovery's first tick (T+24 = startup 4 + window 20) and ends the stance.
   - `ForesightNullifiesAStringHitAndAnswersWithALaunchingCounterStrike` asserts that the landed answer arms D3 in the sidestep.

## 5. Tests

**Declared +18, delivered +22** (+18 as planned, +4 extra). Running total 3367 → **3389**.
- `FighterSpecialRecoveryCancelTests` **13** (new; the plan's 12 + 1):
  1. `EachVerbFiresOnlyFromItsOwnRecoveryFrameAfterALandedHit`
  2. `AWhiffOrABlockKeepsItsWholeRecoveryAndBlockIsTheOnlyExit`
  3. `TicksConstructsZonesAndZeroDamageNeverArmItAndAnAegisAbsorbedContactDoes`
  4. `AnEarlierCastsProjectileNeverArmsTheRunningCast`
  5. `NoSpecialUltimateOrEchoStepComesOutOfALandedRecovery`
  6. `TheBasicCancelSwingsTheVariantOfTheDirectionHeldAtThePress`
  7. `TheUltimateChordNeverTakesTheMovementCancel`
  8. `ACancelPressIsBufferedForSixUnfrozenTicksAndHitstopNeverAgesIt`
  9. `ForesightsSidestepCancelEndsTheStanceAndCutsOnlyItsOwnInvulnerability`
  10. `TheFreezeBufferAndTheCancelBufferNeverFireOnePressTwice`
  11. `ARestoredSnapshotReplaysTheCancelTickForTick`
  12. `NoDirectHitProjectileOrZoneOutlivesItsSlotsEarliestRecast`
  13. **extra:** `WithRealInputAHeldButtonIsOnePressAndOnlyAFreshTapCancels`
- `SpecialRecoveryCancelTableTests` **1** (new, pure C#): `TheD3TableLivesOnceInBasicComboRulesStoryForwardsToItAndTheSimNamesNoStoryRulebook`.
- `FighterCpuBehaviorTests` **+2**: `NoGrabOrEchoStepIsScheduledDuringTheCpusOwnCastAndNoAdmissionIsSpent`, `AFullMeterHardUltimateWaitsOutItsOwnCastAndArrivesOnlyAfterIt`.
- `MirrorParadoxTests` **+2**: `TheStoryAdapterProjectsTheClonesSpecialCastThroughItsPhasesAndNoneAtRest`, `TheClonesCastProjectionIsLosslessOnTheSimsScale`.
- `FighterCpuSpecialTimingSmokeTests` **1** (new): `HardCpusCastSpecialsThroughTheirPhasesOnEveryStageAndTheMatchReplays`.
- **extra:** `CpuSelfActionStateTests` **3** (new, pure C#):
  - `TheGrabMirrorRefusesTheKnockdownTheTechLockTheCpusOwnUltimateAndItsCast`
  - `TheWorldObserverReadsTheCastTheKnockdownTheTechLockAndTheUltimateFromTheSim`
  - `ASpecialPressDecidedBeforeTheCastWasVisibleIsDroppedAtDelivery`

**Rewritten or extended in place (no count change):** the two `TubmanKitTests` Foresight cases (§4 item 5). No other existing case was edited.

**Shared test files touched** (§3.2 map): `FighterCpuBehaviorTests` and `MirrorParadoxTests`. My cases are separate methods in a `CM-CANCEL` section, with their own private helpers (the `Cast*`-prefixed builders in the first, `StepCloneAndCast` in the second).

**Harness notes for later readers:**
- `spawnDistance` is the half-gap: `1` puts the fighters 2 units apart.
- A test that drives a Story clone's `_PhysicsProcess` by hand must also tick the casting `BaseSpecial` node, whose own clock the engine would advance after its owner's.

**Runs** (each holding `D:/Projects/.ftt-gdunit-lock`, with leftover testhost and Godot console processes drained first):
- The §4.1 CM-CANCEL filter (`FighterSpecialRecoveryCancel`, `SpecialRecoveryCancelTable`, `StorySpecialRecoveryCancel`, `TubmanKit`, `FighterCpu*`, `CpuVerbPolicy`, `CpuRecoveryMatrix`, `CpuMeleeBand`, `MirrorParadox`, `CpuSelfActionState`):
  - first run, against the WIP as found: 3 harness failures out of 142;
  - after the fixes: 150 / 151, then the last Mirror case fixed, 2 / 2.
  - **`CpuRecoveryMatrixTests`** (11) and **`CpuMeleeBandTests`** (8) are green, unedited.
- The broad sim regression filter (`FighterSpecial*`, `ShieldBreaker`, `SplittingStrike`, `DivinePiercing`, `RollbackReadiness`, `FighterSimulation`, `RollbackProtocol`, `*KitTests`, `FighterHitConfirm`, `FighterAbilityLockout`, `FighterKnockdown`, `UltimateActivation`, `ProjectileBurst`, `GroundWave`, `RequiemCooldownShave`, `TeslaLorentzChain`, `RiftCollapse`, `FeelPassSim`, `FighterLockoutRefusal`, `FighterVerbLayer`, `FighterGrab`, `FighterPresentationSync`, `FighterLedge`, `FighterMatchFlow`, `FortissimoWall`, `FighterStockLoss`, `FighterCombatContract`, `StoryAbilityLockout`, `StorySpecial*`, `StoryCombatRules`): 468 / 469.
  - The one failure was my `Begin` reset (Decision 4). Once it was reverted, `FighterSpecial*`, `TubmanKit`, `ShieldBreaker` and the smoke read 64 / 64.
- **Full run (at `7133cea`): `Passed! - Failed: 0, Passed: 3389, Skipped: 0, Total: 3389, Duration: 1 m 27 s`.** That reconciles exactly against 3367 + 22, and the `[TestCase]` attribute count under `tests/` is 3389.
- `dotnet build --no-incremental`: 0 errors. The only warnings are CS8632 (vendored GdUnit) and `KLSG_ECS004` on `FighterTuningComponent`. There is no `KLOTHO_DET002` and no `KLSG_ECS004` on 323. The CS0649 the plan expects did not appear.
- Headless `--quit` load check: exit 0, clean.
- **Mirror smoke** `Level_13_ChronalVoid.tscn --quit-after 600`: exit 0, no script error, no exception. It printed "6 ObjectDB instances leaked / 3 resources still in use at exit" on both of its runs.
  - The base `2d44fa9` printed exactly the same on 1 of its 3 runs (worktree detached at the base, rebuilt, then switched back).
  - So this is the documented nondeterministic `AuthoredResources` noise, not this change.
  - Controls `Level_12_Lunar` and `Level_14_NeoEarth` printed nothing.
  - The hero does not reach the Mirror's arena in 600 frames, so the smoke loads the adapter but does not exercise it. The two `MirrorParadoxTests` cases exercise it.

## 6. Measurements: the CPU smoke's recorded counts (plan §5 rows 9, 11 and 25)

`FighterCpuSpecialTimingSmokeTests`, from the final full run's `godot.log` (`[CM-CANCEL smoke]` lines):
- **Setup:** Hard vs Hard, production loadouts, items and hazards on, 1,800 ticks per stage. Every stage ran the full 1,800 ticks, and each replay was identical tick for tick.
- **Columns:**
  - **Casts / Active**: every Special execution, and those that reached their Active window. Foresight's window counts as Active.
  - **Ans. blk / evd**: Specials the opponent answered during their startup, by raising the block stance or by starting a roll or air dodge.
  - **Blocks / Perfect**: block contacts on that fighter, and those that spent no charge and set no shieldstun. **Perfect is 0 everywhere because CM-BLOCK's perfect block is not on this branch.** The closeout re-reads it after the merge.
  - **Into cast**: Special or Ultimate presses into the CPU's own cast.

| Stage | P1 (casts / active, ans. blk / evd, blocks / perfect, into cast) | P2 |
|---|---|---|
| florence_workshop | Einstein 5/5, 0/0, 5/0, 0 | Cleopatra 6/6, 0/0, 1/0, 0 |
| orleans_vanguard | Joan 6/4, 0/1, 2/0, 0 | Tesla 6/5, 0/0, 3/0, 0 |
| chicago_exposition | Leonardo 8/8, 1/0, 0/0, 0 | Shakespeare 4/3, 0/0, 5/0, 0 |
| paris_bastille | Lincoln 4/4, 0/0, 2/0, 0; **Splitting 2, whiffs 0, interrupted 1** | Mozart 6/6, 0/0, 1/0, 0 |
| vesuvius_caldera | Cleopatra 5/4, 0/0, 3/0, 0 | Tubman 6/6, 0/0, 3/0, 0 |
| nassau_flagship | Tesla 6/4, 0/0, 0/0, 0 | Einstein 4/3, 0/0, 6/0, **1** |
| alexandria_chambers | Shakespeare 6/5, 1/0, 2/0, 0 | Joan 5/4, 0/0, 5/0, 0 |
| berlin_wall | Mozart 5/4, 0/0, 4/0, 0 | Leonardo 7/7, 1/0, 0/0, 0 |
| globe_theatre | Tubman 6/6, 1/0, 1/0, 0 | Lincoln 4/4, 0/0, 2/0, 0; **Splitting 2, whiffs 1, interrupted 0** |
| gettysburg_ridge | Einstein 5/5, 0/0, 1/0, 0 | Cleopatra 5/5, 0/0, 3/0, 0 |

**Totals:** 109 casts, 98 reached Active; the other 11 ended in their startup through the interrupt set (a hit, a grab or a KO). Specials answered in startup: 4 by block, 1 by evade. Lincoln's Splitting Strike: 4 casts, 1 whiff, 1 interrupted. Presses into the CPU's own cast: 1. Before the delivery drop, the same smoke counted 39.

## 7. Not delivered / open

- **Perfect-block counts** are not measurable on this branch. The smoke's proxy reads them generically, so it will count CM-BLOCK's perfect blocks after the merge without naming CM-BLOCK code.
- **One frozen-tick press can still reach the CPU's own cast.** The CPU's observation carries no self hitstop (Decision 8), so on the last recovery frame of a frozen cast the delivery drop lets a press through; the sim discards it.
- **Kit awareness** (Lincoln's reach check, deliberate D3, ledge get-ups or perfect blocks by the CPU) is out of scope by ruling 9.3 (§4 item 2).
- **D3 in Fighter is not presented.** There is no new pose, flash or HUD cue; the cancel verb's own presentation plays.
- **Nobody has played or looked at any of it.**
  - Plan §5 row 10: landed ranged Specials are now safe in Fighter, including the Emancipator at 5 u, and Special → basic → Special routes exist with no scaling.
  - Plan §5 row 25: the CPU's play against the new timing.

## 8. For the closeout

**AGENTS.md:**
- **Combat, the D3 bullet** ("Since 2026-10-11 a Special that landed a direct hit cancels its own recovery (D3, Story only …)"). It becomes **both modes**:
  - the table is `BasicComboRules`' D3 section, which `StoryCombatRules` forwards;
  - the sim's state is component 323 `SpecialFlags` 8 (`FlagLandedDirectHit`) and `SpecialCancelBuffer`;
  - the arming sites and the DirectHit-slot rule are as in §1;
  - Foresight's whiff recovery and sidestep gain the Block cancel;
  - drop "The Level 13 Mirror clone inherits it; the sim waits on `DEFER-SIM-SPECIAL-PHASES`".
- **The CPU bullets** (the ability lockout bullet's CPU sentence, `PROVISIONAL-HIT-CONFIRM-RULES` (7)):
  - the CPU reads its own Special cast (`SelfSpecialCastPhase`);
  - its Special and Ultimate presses wait out the cast and are dropped at delivery while the cast holds the tick;
  - its grab and Echo Step mirrors refuse during a cast, and the grab mirror also during the tech lock, the knockdown and its own Ultimate;
  - the Mirror projects the clone's executing Special.
- **The Klotho inventory, row 323:** the CM-CANCEL fields are live.
- **The validation section:** 3389, the five new or extended suites above, and the in-place Tubman extensions.

**Design master** (`design-godot.md` at `e2c655a`; no `DOC-MASTER-UNBOUND-BINDING` line is involved):
- the FSM rows :783–786 (`UsingSpecial` exits: D3 in both modes);
- :1500 ("Cancel into Block", plus the D3 cancel);
- the 2026-10-11 index's owed D3 lines (`USER-SPECIAL-RECOVERY-CANCEL`), now both modes.

**Contracts:** `CPU_COMBAT_POLICY.md` in both copies (repo and docs-3, which are identical today): cast legality is self-state; the CPU never schedules a grab or Echo Step during its own cast; Special and Ultimate presses are revalidated against the cast at delivery.

**Ledger:**
- Add `USER-FIGHTER-SPECIAL-RECOVERY-CANCEL` (11.1, quoted). It amends `USER-SPECIAL-RECOVERY-CANCEL` and `PROVISIONAL-SPECIAL-RECOVERY-CANCEL-RULES` (Story only → both modes, one table).
- Add `PROVISIONAL-FIGHTER-SPECIAL-RECOVERY-CANCEL` (§3 Decisions 1–6).
- Add `USER-CPU-SPECIAL-TIMING-ONLY` (9.3, KIT-04/05/06), with `PROVISIONAL-CPU-SPECIAL-TIMING` (§3 Decisions 7–9; the residual frozen-tick press).
- Amend `PROVISIONAL-HIT-CONFIRM-RULES` (7) and `PROVISIONAL-ABILITY-LOCKOUT-RULES` (6): the CPU reads its own cast.
- Close the fix round's open CPU item (CM_FIXA_SIM §5: the CPU grab mirror's knockdown and A02 terms).
- Record under `VERIFY-*` / plan §5 row 25 the smoke's counts (§6). Record the Lincoln reach-check question (§4 item 2) as open for a user ruling if wanted.
