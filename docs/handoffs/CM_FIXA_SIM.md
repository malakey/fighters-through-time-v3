# CM-FIXA-SIM handoff: the Phase A fix round, Fighter sim half (combat and movement update, 2026-10-16)

Fix round A of `docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md`, workstream **CM-FIXA-SIM**: the nine verified findings under the `SIM` key of `D:/Projects/genre_review_2026-10-13/fixA_inputs.json`, under the orchestrator's four rulings (a)–(d) quoted in §1. Branch `worktree-wf_a50df6ba-c2b-1`, worktree `D:\Projects\Fighters Through Time - V3\.claude\worktrees\wf_a50df6ba-c2b-1`.

**Base.** The worktree was created at `d2e52e0` (the 2026-10-07 baseline), not at merged Phase A. It was clean, so before any edit the branch was moved to `main` = `5434151` ("Merge CM-SIM: the Fighter Special phase machine…", 3332 tests), the commit the task names.

Commits, in order:
- `083f5cc` — `CM-FIXA-SIM: lock the knockdown, the tech's invulnerability, the A02 and Block-cancel ticks; Foresight's aerial rule; the stock-loss tick` (production and tests);
- the commit that adds this handoff (the branch head).

**Fighter hashes move.** No Klotho component, field, snapshot layout, protocol field, save field or `en.csv` key changed; component 323's reserved bits are untouched. No Story file changed.

**Nobody has played it.** Every claim below is a headless suite.

## 1. The rulings applied

- **(a)** The missed-tech knockdown and its get-up lock every action from the landing tick L through the end of the get-up; the get-up direction read stays. A Special cast still running when a knockdown begins is interrupted, so its effect never fires from a downed fighter.
- **(b)** The landing tech's invulnerability lasts exactly as long as its 12-tick action lock (design :1286, Story's one 12-frame window), not 24.
- **(c)** On the thaw tick of a hitstop that froze an Ultimate caster in its wind-up or whiff recovery, no grab, movement ability or other action starts, without discarding a victim's DI read.
- **(d)** The recovery Block-cancel tick keeps only the Block stance: its `PressedButtons` are masked to the Block bit (`HeldButtons` and the stick stay), so every other verb needs a fresh press from recovery frame 1, matching Story.

## 2. Findings fixed

| # | Finding | Fix |
|---|---|---|
| 0 (high) | The knockdown leak is wider than the recorded L-tick case: a hitstop thaw tick during the knockdown (the downed fighter's own landed or blocked shot freezes it) reads live presses, and whatever leaks plays out under the knockdown's invulnerability | Ruling (a), below |
| 6 (medium) | A Special accepted on L keeps casting through the knockdown and fires from the floor | Ruling (a): nothing is accepted on L; the knockdown also joins the cast interrupt set |
| 1 (high) | The sim tech stays invulnerable ~24 ticks, 11 of them actionable after the lock | Ruling (b), below |
| 7 (medium) | A02 thaw-tick leak (the caster's own shot is enough; the whiff recovery leaks Specials too); the obvious hitstop-branch fix would erase DI | Ruling (c), below |
| 2 (low) | The recovery Block-cancel tick keeps all live input (roll / jump / drop-through / grab on recovery frame 0) | Ruling (d): `runtime.PressedButtons &= BlockButton` after `Clear` |
| 3 (low) | Divine Piercing's Block cancel is unavailable on recovery frame 0 (the lunge's 305 state lingers one tick) | `AdvanceSpecialCast` ends the spent lunge on the Active → Recovery tick |
| 4 (low) | An aerial Foresight (Delegated cast) never gets the aerial-Special half gravity, and isn't planted as Story's is | `BeginDelegated(startedAerial)`; Delegated half gravity in startup/window; X = 0 while 322 is busy |
| 5 (low) | Component 323 is reset one tick late after a stock loss: a press on the KO tick is accepted, and a melee Special resolves contact from the respawn anchor | `CanReadAbilityPress` refuses on the platform; `BuildIntent` resolves nothing from it |
| 8 (low) | The BasicAttack row of the tech landing-tick test cannot fail | The row is skipped in the landing-tick case, with the reason |

### How

**Ruling (a), the knockdown** (`FighterSimulationSystems.cs`, `FighterAbilityLockoutRules.cs`, `FighterGrabRules.cs`, `FighterSpecialCast.cs`, `FighterEntitySystems.cs`):
- **Tick L:** `else if (FighterKnockdownRules.TryBeginKnockdown(...)) FighterUltimateActivationRules.DiscardButtons(ref runtime);` — the knockdown branch's own rule (buttons only; the stick, which chooses the get-up on L+30, survives).
- **Hitstop ticks:** the movement system now reads components 320 and 321 above the hitstop gate, and the hitstop branch ends, after `ResolvePendingLaunch` and the freeze-buffer release, with `if (FighterKnockdownRules.IsActive(knockdown) || FighterUltimateActivationRules.IsCasterBusy(ultimateActivation)) DiscardButtons(ref runtime);`. That covers the thaw tick's presses **and** the block stance on every frozen tick (a held Block used to raise `IsBlockStance` there, where the projectile and combat hit paths read it). Buttons only, never the stick: a hit during the get-up freezes its victim with the knockdown still marked (it is cleared on the next normal tick), and that freeze's DI is read from `MoveX`/`MoveY` on the thaw tick.
- **Second gates:** `CanReadAbilityPress`, `FighterGrabRules.CanStartGrab` and `FighterCombatSystem.UltimatePressPayable` refuse while `FighterKnockdownRules.IsActive`. They take component 320 (`in FighterKnockdownComponent`) as a new parameter; `UltimatePressAccepted` / `UltimatePressHeldBackByLockout` pass it through.
- **The interrupt set:** `FighterSpecialCastRules.IsInterrupted` / `ClearIfInterrupted` take `bool knockedDown`; all three call sites (the hitstop branch, `AdvanceSpecialCast`, the fire pass) pass `FighterKnockdownRules.IsActive`.

**Ruling (b), the tech:** `FighterVerbRules.TryLandingTech` max-assigns `LandingTechRecoveryFrames + 1` (the knockdown's +1: the landing tick's own combat pass), and the tech branch decrements `InvulnerabilityFrames` beside `TechLockoutFrames`. Combat sees 13 on L and 12 … 1 on L+1 … L+12; the counter is 0 on L+13, the first tick whose presses are live. Both counters freeze together under hitstop. Before, the grant read 12 and sat frozen through the lock (the normal-path decrement never runs during it), so it ran out on ≈ L+24.

**Ruling (c), A02:** the hitstop-branch `DiscardButtons` above (for `IsCasterBusy`), plus `!FighterUltimateActivationRules.IsCasterBusy` in `CanReadAbilityPress` (Specials and the movement ability) and `CanStartGrab`, which take component 321 as a new parameter. `UltimatePressPayable` already refused a busy caster. A caster punished out of its whiff recovery stays the activation's owner through the punish's freeze (`AdvanceUltimate` waits out the hitstop), so the stick-preserving discard is what keeps its DI.

**Ruling (d), the Block cancel:** one line after `FighterSpecialCastRules.Clear(ref action)` in the cast region. Roll, Jump, drop-through (Jump + held Down), the grab chord and the direct grab, Echo Step, the movement ability, the Specials and the Ultimate are dropped that tick and act on a fresh press from the next (from the stance). This also closes CM-SIM decision 13 (the grab chord on the cancel tick).

**Finding 3, Piercing:** in `AdvanceSpecialCast`, on the tick `Advance` moves the cast out of Active, a `PiercingLunge` 305 slot with `UniversalMovementFramesRemaining <= 0` is cancelled and X zeroed — exactly what `ProcessReach`'s end branch did later on the same tick, before anything else read the slot, so motion and hashes on non-cancel paths are unchanged. The final thrust already resolved on the last active tick (`ResolveReachKitHits` on T+S+A−1). The `<= 0` guard keeps a 0-startup fallback lunge (started in the fire pass, frames remaining) untouched. The verifiers' warning holds: cancelling inside `ProcessReach` on the last move's tick would drop the final thrust.

**Finding 4, Foresight:** `BeginDelegated(ref cast, slot, bool startedAerial)` records `FlagStartedAerial` (the acceptance passes `fighter.IsGrounded == 0`); `CastHalvesGravity(in action, in FighterCounterComponent counter)` adds a Delegated cast whose 322 phase is Startup or Window (Story's Startup and Active; the sidestep and the whiff recovery fall on the normal step). The movement block plants the stance — `if (!movementHandled && FighterForesightRules.IsBusy(counter)) Velocity.x = 0; else if (… !CastKeepsAirDrift) ApplyNormalMovement(…)` — grounded and airborne, because Story's `TubmanForesight` writes X = 0 on every executing tick, a kit writing its own velocity, which the Q1 ruling lets override the brake. So a grounded Foresight now stops at once instead of over the 4-frame brake (the verifiers' optional step 3, taken for cross-mode parity).

**Finding 5, the stock-loss tick:** `CanReadAbilityPress` gains `fighter.RespawnFramesRemaining <= 0` (as `UltimatePressPayable` and `CanStartGrab` already had), which refuses a Special or movement-ability press on the KO tick and on a platform thaw tick; `BuildIntent`'s early-outs gain `FighterMatchFlowRules.IsOnRespawnPlatform(attacker)`, so a melee Special whose caster was KO'd earlier in the tick (the blast zone, a lethal Venom tick) resolves no contact from the anchor. The optional KO-site `DiscardInput` + `action = default` was not added: every same-tick reader is now gated, and the platform hold still zeroes 323 on the next movement pass.

**The D10 classifier:** `FighterLockoutRefusalRules.Evaluate` gains an optional `in FighterKnockdownComponent knockdown = default` and passes 320 and 321 to `CanReadAbilityPress`. The driver's call still passes neither 323 nor 320 (CM-SIM deviation 6); that stays safe because every one of these locks now discards its buttons on hitstop ticks too.

## 3. Tests

**Declared +10, delivered +10** (3332 → 3342).

New cases:
- `FighterKnockdownTests` +3 (pure C#):
  - `NoVerbStartsOnAMissedTechsLandingTickAndNoCastRunsThroughTheKnockdown` — Special 1 (a clocked 8 / 2 / 6 area cast), Special 2, the movement ability, the direct grab and a paid Ultimate, each pressed on L: nothing starts, and no effect fires and no cast runs through the 40-tick knockdown and get-up (findings 0 and 6).
  - `NoVerbStartsOnAHitstopThawTickInsideTheKnockdownOrItsGetUp` — the downed fighter's own slow shot (fired before the launch) freezes it while down; on the thaw tick Special 2, the movement ability, the direct grab, the grab chord, a paid Ultimate and held Block (the stance) start nothing (finding 0's second path). The fire tick is searched.
  - `AHitDuringTheGetUpKeepsTheVictimsDiRead` — a launcher on get-up frame 1; holding Up through its freeze bends the launch, angle only (the DI caveat).
- `FighterVerbLayerTests` +1: `AHitOnTheFirstTickAfterTheTechLockLandsAndOneInsideItIsRefused` — a copy probe through `FighterDamageRules.ApplyFighterHit` is refused on L … L+12 and lands on L+13 (finding 1).
- `FighterSpecialTimingTests` +4:
  - `TheRecoveryBlockCancelTickRaisesOnlyTheStanceAndEachVerbNeedsAFreshPress` — a whiffed Righteous Smite; on recovery frame 0 Block held with Roll / Jump / Jump + Down / the grab chord / the direct grab raises only the stance; the same fresh press acts on the next tick (finding 2).
  - `DivinePiercingsRecoveryBlockCancelOpensOnItsRecoveryFrameZero` — whiffed (T+25 = T+S+A) and connecting (the three thrusts' hitstops push it back); the stance rises on the reference run's recovery-frame-0 tick (finding 3).
  - `ForesightFallsAtHalfGravityThroughItsStartupAndWindowAndItsStanceIsPlanted` — airborne: 23 half-gravity ticks (3 startup + 20 window), normal after, X = 0 throughout; grounded: X = 0 from T+1 (finding 4).
  - `AStockLossTickResolvesNoMeleeContactAndTakesNoPressFromTheRespawnPlatform` — a generic 2 / 60 / 0 melee Special cast while falling through the blast zone strikes nothing from the anchor on the KO tick (the opponent stands 1–1.5 units in front of it); Special 2 and the movement ability pressed on the KO tick spend nothing (finding 5).
- `UltimateActivationTests` +2:
  - `NoVerbStartsOnAnOwnShotsThawTickInTheWindupOrTheWhiffRecovery` — the caster's own slow shot freezes it in the wind-up and, in a second search, the whiff recovery; on each thaw tick Special 2, the movement ability, the direct grab, the grab chord and held Block start nothing, and the Ultimate runs on (finding 7).
  - `ACasterLaunchedOutOfItsWhiffRecoveryKeepsItsDiRead` — the punish's freeze keeps the caster's DI (the caveat in finding 7).

Rewritten or extended in place (no count change):
- `FighterVerbLayerTests.LandingTechEndsTumbleWithAnInvulnerableRecovery` — asserted `InvulnerabilityFrames <= 12` on the tech tick; now asserts the grant is 13 on L, counts down 12 … 1 with the lock, and is 0 on L+13 (finding 1's "check when the grant expires").
- `FighterVerbLayerTests.NoVerbStartsOnTheTechsOwnLandingTick` — skips the BasicAttack row, with the reason in its doc (finding 8). The row stays in `TheLandingTechLocksEveryVerbForItsTwelveFramesAndThePressAfterIsAccepted`.
- `ShieldBreakerTimingTests.AfterABlockedShatterTheAttackerIsLockedThroughItsWholeRecovery` — per Shield-Breaker, Block+Roll and Block+Jump on recovery frame 0 raise only the stance (finding 2). The Emancipator's wave shatters after its caster is in recovery, so its frame 0 precedes the shatter; the probe finds frame 0 from the press either way.
- `FighterSpecialCastRulesTests.EveryInterruptClearsTheCastAndLeavesTheOtherFieldsAlone` gains a "knocked down" row; it and `ADelegatedCastIsBusyButNotClocked` pass the new `knockedDown` / `startedAerial` arguments.
- `FighterLockoutRefusalRulesTests` (its `LockedIdle` helper) passes the two new `CanReadAbilityPress` components.

**Failing first.** Against the unchanged code (`5434151`), the filtered run of the five suites read `Failed: 11, Passed: 52, Total: 63`: every new case except the two DI guards failed on its own assertion (the leaks reproduced: Special 1 accepted on L, Special 2 accepted on a knockdown thaw tick, a roll on the Block-cancel tick, Piercing's stance on T+26, no aerial flag on Foresight, a melee contact on the KO tick, the tech grant 12 and a refused hit on L+13), plus the rewritten tech case and the Piercing row of the ShieldBreaker extension. Two failures there were harness bugs of mine in the new A02 cases (a default `AbilityData` is clocked 12 / 6 / 12, and the thaw tick can legitimately end the wind-up), fixed before the after-run. The two DI cases are guards: they pass before and after, and fail if the hitstop-branch discard ever zeroes the stick.

**Runs** (each holding `D:/Projects/.ftt-gdunit-lock`, leftovers drained, 0 found):
- After the fix, the touched suites plus the CM-SIM regression filter (`FighterKnockdown`, `FighterVerbLayer`, `FighterSpecial*`, `ShieldBreaker`, `UltimateActivation`, `FighterLockoutRefusal`, `RollbackReadiness`, `FighterSimulation`, `RollbackProtocol`, `FighterActionState`, `FighterAbilityLockout`, `FighterHitConfirm`, `FighterGrab`, `*KitTests`, `FeelPassSim`, `FighterPresentationSync`, `FighterCpu`, `CpuMeleeBand`, `CpuRecoveryMatrix`, `FighterLedge`, `FighterMatchFlow`, `MirrorParadox`, `SplittingStrike`, `DivinePiercing`, `TubmanUltimate`, `FighterStockLoss`, `CpuVerbPolicy`, `FighterCombatContract`): `Failed: 1, Passed: 475, Total: 476` — the one failure the ShieldBreaker extension's Emancipator harness (above), fixed; the suite then read `Passed: 3, Total: 3`.
- **Full run** (commit `083f5cc`): **`Passed! - Failed: 0, Passed: 3342, Skipped: 0, Total: 3342, Duration: 1 m 28 s`**, reconciled exactly against 3332 + 10. The `[TestCase]` attribute count under `tests/` is 3342.
- `dotnet build`: 0 errors, only the pre-existing warnings (CS8632, `KLSG_ECS004` on `FighterTuningComponent`); no `KLOTHO_DET002`.

## 4. Fighter hash moves

Yes, but only where one of the fixed situations occurs: a missed tech with a press on L or a hitstop during the knockdown; a landing tech (its invulnerability counter now differs on every tech tick); an A02 caster frozen in its Ultimate; a recovery Block cancel with any other button pressed (the masked `PressedButtons` is hashed state); an airborne or moving Foresight; a stock-loss tick with a press or a live melee cast. The Piercing change is hash-neutral on every path that never Block-cancels. A match with none of these keeps its hash. No suite pins an absolute hash value.

## 5. Not fixed, and why

- **The grabber's throw-animation invulnerability** (a side note in finding 1's verifier notes, flagged there as outside that finding): the 12-frame grant `AdvanceGrabPhase` gives the grabber at the throw animation sits frozen through the grab-busy branch (which `continue`s past the normal decrement) and `ResolveThrow` zeroes only the victim's, so the grabber leaves every throw with about 12 free invulnerable ticks (the thrown victim cannot act in them, but in-flight projectiles, constructs and hazards are refused). Same root cause as finding 1. Not one of the nine findings and unruled, so recorded, not changed. The likely fix is the tech's: decrement `InvulnerabilityFrames` in the grab-busy branch (or zero the grabber's grant in `ResolveThrow`, though that would also erase an unrelated larger grant).
- **The CPU's grab-legality mirror** (`FighterCpuController.CanStartGrab` over `CpuDecisionObservation`) has no knockdown or A02 term. Input-side only: the sim now refuses such a press. `FighterCpuController` is not CM-SIM's to edit; CM-CANCEL (or whoever owns the CPU in Phase B) may add the terms.
- **No paired Story case** for the Block-cancel tick (a verifier suggestion): Story already enters Blocking and returns on that tick, and this workstream is the Fighter sim.
- **The driver's classifier call** still passes neither 323 nor 320 (CM-SIM deviation 6), safe for the reason in §2.

## 6. For Phase B

- **CM-CANCEL (D3).** D3 resolves in the cast region **before** the new mask, and never on a tick the Block cancel won (Story's Block branch returns before `TryFireSpecialCancel`); it must not re-feed a press the mask dropped. A landed Divine Piercing's frame-0 roll now works: the lunge's 305 slot is `None` on recovery frame 0 (finding 3), so `TryStartUniversalMovement` no longer refuses it. The hitstop branch now ends with the knockdown / A02 `DiscardButtons`; D3's feed call (after the two buffers, before `ClearIfInterrupted`) runs before it, and a knockdown or busy-Ultimate fighter never casts anyway.
- **CM-BLOCK (perfect block).** The mask keeps the Block press edge (`PressedButtons & BlockButton`) and `HeldButtons`, so a stance raised by the Block cancel is a normal Block-pressed tick for the perfect-block clock. The new hitstop-branch discard clears `HeldButtons` for a downed fighter or a busy Ultimate caster on frozen ticks; neither has a stance to time.
- **Signatures.** `CanReadAbilityPress`, `CanStartGrab` and `UltimatePressPayable` / `Accepted` / `HeldBackByLockout` take components 320 (and the first two 321); `IsInterrupted` / `ClearIfInterrupted` take `bool knockedDown`; `BeginDelegated` takes `bool startedAerial`.

## 7. What the closeout owes (docs and ledger; not edited here)

- **Ledger.** The knockdown landing-tick leak and the A02 thaw-tick leak (CM-SIM §6/§8 asked for new `VERIFY-*` rows): record them as found **and fixed** by this round, naming both knockdown entry paths (tick L, and a hitstop thaw tick during the knockdown or get-up, e.g. the downed fighter's own shot) and the A02 paths (the caster's own shot in the wind-up or the whiff recovery). `FIX-SIM-TECH-LOCK-ACTIONS`: the tech's invulnerability now ends with its lock (13 ticks counting L). `PROVISIONAL-SIM-SPECIAL-PHASE-RULES`: decision 13 (the grab chord on the Block-cancel tick) is superseded by ruling (d); decision 7 extends to Foresight (Delegated half gravity in startup and window) with the planted stance; the interrupt set (decision 3) gains the knockdown. `VERIFY-SAME-TICK-VERB-PARITY`: the Block-cancel tick and the tech's invulnerability now match Story. New row for the grabber's throw-animation invulnerability (§5).
- **AGENTS.md.** The landing-tech and M05 bullets (Combat; the cross-mode parity bullet): the tech is invulnerable and locked for the same 12 ticks after its landing tick; the knockdown locks every action from its landing tick, hitstop ticks included. The cast-lock description: the Block-cancel tick raises only the stance. The A02 bullet: the action lock holds on the caster's hitstop ticks.
- **`docs/handoffs/CM_SIM.md`** is historical: its §6 "the knockdown branch's own discard covers L+1 on" (it did not cover a hitstop thaw tick) and its §9 "The two checks" (proposed fixes) are superseded by this handoff.
- **Design master:** nothing; every change implements text it already states (:820, :1097, :1286, :1287).
