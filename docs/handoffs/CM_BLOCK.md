# CM-BLOCK handoff: perfect block, damage-scaled shieldstun, Story hazard blocking (combat and movement update, 2026-10-16)

Phase B workstream **CM-BLOCK** of `docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md`: decisions **3.4** (perfect block, both modes, Q3 "still shatters"), **4.1 / 11.2** (shieldstun scales with the blocked hit's damage, both modes) and **7.4** (a raised block absorbs Story hazard ticks). Dossiers §2.3.2, §2.4.1, §2.7.4. Branch `worktree-wf_a70ba1d0-06f-2`, worktree `D:\Projects\Fighters Through Time - V3\.claude\worktrees\wf_a70ba1d0-06f-2`, base `2d44fa9` (Phase A + review fixes, **3367** tests).

**Resumed work.** A first agent with this brief was stopped by a usage limit; the orchestrator committed its uncommitted tree as a WIP checkpoint (`0be0b69`). This agent audited it (built it, mapped it against every dossier item, ran it), kept the sound work, fixed two broken test harnesses, built what was missing (the Story perfect-block, environmental, block-model, parity and drill cases; the block-fixture audit and its re-times; the Guard-Crush constant consolidation), then `git reset --soft 2d44fa9` and recommitted in logical pieces. The WIP commit is not on the branch.

Commits, in order:
- `498a17b` — `CM-BLOCK: damage-scaled shieldstun and the perfect block, both modes (3.4, 4.1 / 11.2)`;
- `a6e4e29` — `CM-BLOCK: a raised block absorbs Story hazard ticks (7.4)`;
- `b80ad9e` — `CM-BLOCK: the Systems Card's block copy states the scaled shieldstun and the perfect block`;
- the commit that adds this handoff (the branch head).

**Fighter hashes move** (component 323's `BlockTiming` is written whenever a fighter blocks, and every shieldstun length changed). No component, protocol field, save field or snapshot layout changed; only CM-BLOCK's own `BlockTiming` int of component 323 is used.

**Nobody has played the build.** Every claim below is a headless suite or an instrumented run.

## 1. Shipped

### Shieldstun (4.1 / 11.2), both modes

- `BasicComboRules.ShieldstunFramesFor(int blockedDamage)` = `clamp(ShieldstunBaseFrames 6 + blockedDamage / ShieldstunDamageDivisor 4, 6, ShieldstunMaxFrames)`, integer division; `ShieldstunMaxFrames = ShatterFreezeFrames` (16). Basics 4–7 → 7, Lincoln's finisher 13 → 9, Smite 28 → 13, the Emancipator 40 → 16, a zero-damage cast (Siphon Snare) → 6.
- **Deleted:** `BasicComboRules.ShieldstunFrames` (8) and `BlockSystem.ShieldStunSeconds`. Nothing reads a flat shieldstun any more.
- Sim: `FighterDamageRules.ResolveFighterHit`'s non-shatter branch writes `ShieldstunFramesFor(damage)` — the post-barrier remainder, before Radiant Burn.
- Story: `BlockSystem.ResolveHit` arms `ShieldstunFramesFor(round(hit.Damage)) / 60f` — the delivered damage after `ResolveIncomingHit`'s partial absorption (so the difficulty multiplier the mob's damage already carries counts).

### Perfect block (3.4, Q3), both modes

Shared, in `BasicComboRules`' block section: `PerfectBlockWindowFrames` **5** (contact ages 0–4 from the first live stance tick), `PerfectBlockRearmFrames` **20**, `GuardCrushBlockChargeCost` **2**, and two predicates both modes call:
- `PerfectBlockArms(bool stanceRose, bool blockPressed, int loweredFrames)` — the stance came up this tick, Block is pressed this tick, and the lowered age is 0 ("never raised") or ≥ 20;
- `PerfectBlockApplies(bool windowOpen, bool shieldBreaker, int authoredBlockChargeCost)` — the window is open, the hit is not a Shield-Breaker (Q3: it still shatters) and its authored charge cost is below the Guard-Crush cost (a Guard-Crush keeps its effect).

Effect, every contact the ordinary block layer would absorb inside the window: **no charge, no shieldstun, no regen re-arm**; the 2-frame blocked hitstop stays; the outcome stays `Blocked` (no D1a confirm, no Momentum). The D01/D02b order is unchanged (D04 → projectile immunity → Aegis → barrier → perfect-or-ordinary block → HP). The Ultimate activation never reaches the block. Hazard ticks can be perfect-blocked.

**Sim** (`scripts/FighterSim/FighterMovementSystem.Block.cs`, static class `FighterBlockTimingRules`, over component 323's `BlockTiming`):
- bits 0–7 window frames left (`WindowMask`), 8–15 lowered age (`LoweredShift`, `LoweredMask`, saturating at `LoweredMaxFrames` 255; 0 = never raised this stock), 16 `StanceUpBit`; and (§3) 17 `ObservedBit`, 18 `EscapeTickBit`, 20–23 the perfect-block count (`PerfectCountShift`, `PerfectCountMask`, cycling 1..15).
- Reads: `WindowFrames`, `LoweredFrames`, `IsPerfectBlockOpen`, `PerfectBlockCount`. Writes: `NotePerfectBlock` (the block branch), `BeginTick(ref action, in fighter)` and `Observe(ref action, bool stanceUp, bool blockPressed)`.
- Call sites in `FighterMovementSystem.Update`: `BeginTick` once per unfrozen tick, right after `AdvanceSpecialCast` (before any branch can end the tick); `Observe` once on the normal-control path, right after `blockStance` is computed, with `(PressedButtons & Block) != 0`. A fighter who never blocks keeps the whole field at 0; 323 is zeroed on the respawn platform (a fresh stock is eligible at once) and by the F22 setup.
- `FighterDamageRules.ApplyFighterHit`, `ResolveFighterHit` and `ApplyEnvironmentHit` gained overloads whose first parameter is `ref FighterActionStateComponent targetAction`; the block branch reads `IsPerfectBlockOpen` there. The old overloads remain for hand-built callers and resolve an ordinary block (a scratch zeroed 323). Every blockable production site passes the victim's 323: `ApplyIntent` (Special/Ultimate intents), `ApplyBasicSwing` → `ApplyAirAttackSwing` / `ApplyDirectionalSwing` / `ApplyDownAirSlam` / the string, `ResolveForesightAnswer`, the reach-kit hits (Piercing's thrusts), projectiles (both the entity system and `FighterKitRules`' projectile pass), persistent constructs, the coil fence, the four hazard sites, zone pulses, the spiral expiry launch and `FighterLorentzChainRules.FireArcs` (new `ref FighterActionStateComponent targetAction` parameter). Not threaded, because none can meet a block: the throw, the Rift Collapse launch (it returns on a raised stance), Ultimate-class hits and the A02 activation strike.
- Presentation: `FighterSimulationDriver.SyncPerfectBlockFlash(playerID, glow)`, one call line at the end of `SyncPresentationFeedback`, read-only over 323's count: on each change it plays `glow.FlashHit(BlockSystem.PerfectBlockFlashColor, BlockSystem.PerfectBlockFlashSeconds)`, a 3 / 0.08 camera shake and the Guard Impact haptic. Cosmetic cache `_presentedPerfectBlockCount`.

**Story** (`scripts/Characters/PlayerController.PerfectBlock.cs`):
- State `_perfectBlockFrames`, `_blockLoweredFrames` (0 = never raised, this controller's lifetime), `_blockStanceSeen`, `_perfectBlockArmPending`; seams `IsPerfectBlockWindowOpen`, `PerfectBlockFramesRemaining`, `PerfectBlockCount`.
- `CheckBlockInput` → `NoteBlockStanceRaised()` after entering Blocking: the window is **owed** when Block is pressed that tick and the rearm guard allows it. `ProcessBlocking` → `OpenOwedPerfectBlockWindow()` right after `BlockSystem.StartBlock`: the first live stance tick opens it (Story's stance is live one tick after the press), so contact ages 0–4 match the sim. The hit-2 escape enters Blocking from `ProcessStunned`, never through `CheckBlockInput`, so it never arms.
- `TickPerfectBlock()`, one call inside `_PhysicsProcess`'s `if (!TimeFrozen)` counter block: hitstop's early return and Time Freeze pause the window and the lowered age.
- `BlockSystem.ResolveHit` gained the perfect branch (before the charge spend) and `LastBlockWasPerfect`; it calls `PlayerController.NotePerfectBlock()` (the count and a `FlashHit` on the hero's glow), still grants Henry's Bastion and raises `OnBlockAbsorbed`. `ResolveIncomingHit` and `TryAbsorbNonDamagingCast` skip Shield of Orléans when `LastBlockWasPerfect` (design :2561 needs a consumed charge). A perfect block opens the D8 Punish window (it is `Blocked`), including a perfect-blocked Siphon Snare.
- `BlockSystem.PerfectBlockFlashColor` = (0.85, 1, 1), `PerfectBlockFlashSeconds` = 0.12, shared by both modes' flash.

**Calibration Drill 1.** `CalibrationDrillSample.StudentPerfectBlockCount` (filled by `CalibrationDrillRunner.Sample` from 323); `ObserveBlockString` counts any change of the count other than to 0 as one block, beside the existing shieldstun rising edge.

**One Guard-Crush value.** `HitClassification.BlockChargeCostFor`, `EnemyAbilityExecutor` (the attack hitbox stamp) and `EnemyProjectile` now read `BasicComboRules.GuardCrushBlockChargeCost` instead of three literal 2s.

### Story hazard blocking (7.4)

`scripts/Characters/PlayerController.EnvironmentalBlock.cs`:
- `PlayerController.ApplyEnvironmentalHit(int damage, Vector2 hitOrigin, bool blockable, bool appliesHitstop = false)` and an overload with `out int damageApplied`, returning `EnvironmentalHitResult { Applied, Blocked, Protected }`.
- Order: D04 (`IsDefyProtected` → `Protected`) → Aegis / barrier (`ResolveProtectionLayers`; fully absorbed → `Protected`) → for a blockable source with the hero in `Blocking`, the front-facing test on the origin's X and a charge left, `BlockSystem.ResolveHit` with a Hazard-class payload (`AttackerIndex` −1, `ExemptFromHitstop`, cost 1, shieldstun scaled by the tick's damage, the perfect block applying) → `Blocked` (a shatter included) → otherwise the existing pipeline, now the private `ApplyEnvironmentalRemainder` (HP, Defy flag, Rally echo, victim meter, optional hitstop) that `ApplyEnvironmentalDamage` also calls. A block deals no HP and gives no echo or meter; Henry's Bastion grants, Shield of Orléans does not.
- Blockable sources (each passes its `GlobalPosition` and skips its knockback on `Blocked`): `StoryCyclicHazard.ApplyToPlayer`; `ChronalExtractor.ApplyDischargeToPlayer` (its −20 meter drain still applies, D03c); `SearchlightZone.TriggerStrike`; the spikes (`StoryLevelControllerBase.ApplySpikeDamage(player, damage, Vector2? hitOrigin = null)`, `Level01Controller.BuildHazardSpikes`); `FallingDebris.ApplyTo`, which moves onto the entry point (Hazard class, cost 1 — Basic before) and so no longer meets the block ahead of D04 and Aegis.
- Unblockable (penalties, unchanged on `ApplyEnvironmentalDamage`): `RisingWaterZone` drowning, `Level07Controller.HaulOutOfTheWater`, `ChronalRiftZone.TriggerTimeLoopSnap`, the `EscapeSequenceController` catch, Level 0's scripted kill; `StageHazard` (in no scene) stays unblockable.

### Systems Card

`systems_card_block_body` (under `# CM-BLOCK` in `localization/en.csv`; `en.en.translation` regenerated with `--import` and re-checked unchanged): "Three charges absorb Basic hits; the harder the blocked hit, the longer its shieldstun (6 frames plus 1 per 4 damage, up to 16). Raise Block within 5 frames of a hit for a perfect block: no charge spent and no shieldstun. A special costs two charges, and a Shield-Breaker special takes them all, even from a perfect block. …"

## 2. Files changed

Owned / declared:
- New: `scripts/FighterSim/FighterMovementSystem.Block.cs`, `scripts/Characters/PlayerController.PerfectBlock.cs`, `scripts/Characters/PlayerController.EnvironmentalBlock.cs` (with `.uid` sidecars).
- `scripts/Combat/BlockSystem.cs`, `scripts/Combat/CalibrationDrillDefinition.cs`, `scripts/Environment/StoryCyclicHazard.cs`, `ChronalExtractor.cs`, `SearchlightZone.cs`, `FallingDebris.cs`, `localization/en.csv` + `en.en.translation`.
- Declared regions: `BasicComboRules.cs` (the block section beside the old `ShieldstunFrames`); `FighterEntitySystems.cs` (`ResolveFighterHit`'s block branch, `ApplyEnvironmentHit`); `FighterMovementSystem.Update` (the `Observe` call after `blockStance`, plus `BeginTick` — §4); `FighterSimulationDriver.cs` (`SyncPerfectBlockFlash` + one call line); `PlayerController.cs` (`CheckBlockInput`, `ProcessBlocking`, `ResolveIncomingHit`'s block branch, `ApplyEnvironmentalDamage`, `TryAbsorbNonDamagingCast`, the `!TimeFrozen` tick line); `StoryLevelControllerBase.cs` / `Level01Controller.cs` (the spike methods); `CalibrationDrillRunner.Sample` (the block line).

**Outside my ownership** (each minimal):
- **The sim hit-site callers** in `FighterEntitySystems.cs` (14 sites), `FighterSimulationSystems.cs` (`ApplyIntent`, `ApplyBasicSwing`, `ApplyAirAttackSwing`, `ApplyDirectionalSwing`, `ApplyDownAirSlam`, `ResolveForesightAnswer` signatures and their calls in `FighterCombatSystem.Update`) and `FighterKitRules.cs` (the projectile pass, `FighterLorentzChainRules.FireArcs`): each gained one `ref …FighterActionStateComponent` argument. `ResolveFighterHit` has no `Frame`, so the victim's 323 must come from the caller. **CM-CANCEL edits the same hit-site callers (2.11.1); expect textual merge conflicts there** — each is one added argument line.
- `scripts/FighterSim/FighterSpecialCast.cs`: the `BlockTiming` doc comment only (names the extra bits).
- `scripts/Combat/HitboxSystem.cs` (`HitClassification.BlockChargeCostFor`), `scripts/Enemies/EnemyAbilityExecutor.cs` (≈ :489, outside its declared ≈ :375–379 region, which needed no change) and `scripts/Enemies/EnemyProjectile.cs` (≈ :114): the literal Guard-Crush 2 → `BasicComboRules.GuardCrushBlockChargeCost`, so the perfect block's Guard-Crush exclusion reads the one statement of the value. No behaviour change.
- Test files outside the §3.2 map, re-timed by the audit (§5): `FighterHazardBehaviorTests`, `StoryBlockClassificationTests`, `StorySpecialRecoveryCancelTests` (a CM-CANCEL suite: one fixture re-timed in place, no case rewritten), `StoryHitConfirmTests`.

## 3. Decisions (provisional; ledger rows)

Built against the plan's defaults and the §0.2 rulings (Q3: a Shield-Breaker inside the window still shatters).
- `PROVISIONAL-PERFECT-BLOCK-RULES`: window 5, rearm 20 (the plan's numbers); arming needs the down → up stance edge **and** a Block press on that tick **and** the lowered age 0 or ≥ 20. Hand-built `Pressed = Held` streams arm once.
- **The Guard-Crush exclusion is keyed on an authored charge cost ≥ `GuardCrushBlockChargeCost`.** The Siphon Snare's non-damaging cast (cost 1) is perfect-blockable. In the sim no production player ability authors a Guard-Crush cost, so the term is inert there.
- **Bookkeeping bits 17 and 18** (inside CM-BLOCK's own `BlockTiming` int). A tick the normal-control path never observes (hitstun, daze, the knockdown, the tech lock, a grab, an A02 capture, a ledge hang) is finalized by the next `BeginTick` as a lowered tick, so a stance broken by a hit counts as dropped. The hit-2 escape tick is detected as "hitstun > 1 at the start of the unfrozen tick" (so it outlives this tick's decrement and only the escape can raise the stance); a natural stun expiry with a press on its last tick may arm.
- **The perfect-block count, bits 20–23** (cycling 1..15, 0 = none this stock): read by the driver's flash and the Calibration Drill only, never by a rule. Plan §1.5 reserved bits 0–16; the count was needed because a perfect block changes no other observable (no charge, no shieldstun) for presentation or the drill to see.
- A stance raised during the fighter's own hitstop (a frozen sim tick) never arms: the press edge lands on a frozen tick, where neither `BeginTick` nor `Observe` runs. Story behaves the same (the hitstop early return).
- Story's lowered age lives for the controller's lifetime (Story has no stocks); a death rewind does not reset it. A fresh controller is eligible at once.
- `PROVISIONAL-SHIELDSTUN-CURVE`: the plan's `6 + damage / 4`, clamped 6..16. Story converts frames to its seconds timer (`frames / 60f`); one tick of float slack is tested (`FighterBasicStringParityTests`).
- The perfect-block flash (both modes): `(0.85, 1, 1)` for 0.12 s on the hit-flash channel (the glow arbiter has no outline-flash entry point), plus the ordinary block's 3 / 0.08 shake and the Guard Impact haptic. No cue row (the catalog ties rows to the GDD).
- `PROVISIONAL-STORY-HAZARD-BLOCK-SOURCES`: the source list above, exactly the plan's; the environmental block payload is `AttackID "environment"`, `HitboxID "hazard"`, `Delivery Hazard`, `Origin Environment`.
- The Calibration Drill counts a perfect block on any change of the 323 count except to 0 (a stock reset).

## 4. Deviations from the plan

1. **The window decrement is not in `TickCounters`.** `FighterBlockTimingRules.BeginTick` is called once per unfrozen tick right after the Special cast clock in `FighterMovementSystem.Update`. `TickCounters` runs from five different branches (captured, knockdown, tech, grab, normal) and takes no 323; one call before them all counts exactly the unfrozen ticks, pauses in hitstop as specified, and also finalizes unobserved ticks. Still two call lines in `Update`.
2. **Component 323 is threaded to every blockable hit site** (§2), beyond the declared block-branch region; the block branch cannot reach the victim's 323 otherwise. The hit-site edits are one argument each and change nothing else.
3. **Extra `BlockTiming` bits** (17, 18, 20–23), within CM-BLOCK's own field (§3).
4. `ApplyEnvironmentalHit` also has an `out int damageApplied` overload, for the sources that report or act on the HP lost (the searchlight's signal, debris's knockback gate, the spikes' return value).
5. The Guard-Crush literal consolidation (three one-token edits outside ownership, §2).
6. `StoryBlockClassificationTests.EnemyBasicClassFireCostsOneChargeAndUnblockablesPierce` never asserted the one charge its name promises; the re-time added that assertion (it now fails on a perfect block).

## 5. Tests

**Declared +22, delivered +22** (3367 → 3389).

New:
- `ShieldstunCurveTests` 2 (new, pure C#): `TheShieldstunTableScalesWithTheBlockedDamage` (0, 3, 4, 8, 12, 20, 40, 100 → 6, 6, 7, 8, 9, 11, 16, 16), `TheCapIsTheShatterFreezeAndNoDamageGoesBelowTheBase`.
- `FighterPerfectBlockTests` 7 (new, pure C#): `ARaiseZeroToFourTicksBeforeContactSpendsNothingAndFiveIsOrdinary`, `AHeldRaiseAnEscapeRaisedStanceAndAPressInShieldstunNeverArm`, `AReRaiseInsideTwentyFramesIsOrdinary` (19 vs 20), `APressedEqualsHeldStreamArmsOnceAndAFreshStockIsEligibleAtOnce` (hand-built and `PlayerInputFrame.Create`-built), `AShieldBreakerInsideTheWindowStillShattersButAnOrdinarySpecialIsPerfect` (+ a Guard-Crush cost keeps its effect), `AHazardTickInsideTheWindowIsPerfectBlocked`, `ASnapshotInsideTheWindowReplaysThePerfectBlock`.
- `StoryPerfectBlockTests` 4 (new): `ARaiseZeroToFourLiveTicksBeforeAMobHitIsPerfectAndTheFifthIsOrdinary`, `AHeldRaiseAnEscapeRaisedStanceAndAToggleReRaiseInsideTwentyFramesNeverArm` (Toggle through the real `BlockToggleLatch`; 19 vs 20 lowered ticks), `APerfectBlockOpensThePunishWindowAndGrantsHenrysBastionButShieldOfOrleansDoesNotPay` (with an ordinary-block Orléans control), `AGuardCrushInsideTheWindowKeepsItsTwoChargeCostAndASiphonSnareIsPerfectBlocked`. All drive the hero's `_PhysicsProcess` with `Create`-built edges.
- `StoryEnvironmentalDamageTests` +6: `AFrontFacingBlockAbsorbsACyclicHazardTickForOneChargeWithNoHPKnockbackEchoOrMeter`, `AHazardTickFromBehindTheStanceLandsInFull`, `TemporalAegisAbsorbsAHazardTickBeforeTheBlockSpendsACharge`, `DefyProtectionRejectsAHazardTickBeforeTheBlock`, `DrowningIsAPenaltyNotAHitAndIgnoresARaisedBlock`, `ABlockedDischargeStillDrainsMeterAndDebrisDuringDefySpendsNothing`.
- `FighterBasicStringParityTests` +1: `ShieldstunScalesWithTheBlockedDamageAndBothModesLockEqualFramesForEqualDamage` (4 / 13 / 28 / 40 → 7 / 9 / 13 / 16 in the sim, the same ±1 float tick in Story).
- `StoryBlockModelTests` +1: `AMobHitsShieldstunScalesWithTheDamageTheBlockTookAfterAbsorption` (a 20-damage mob hit gives 11; a barrier's partial absorption leaves the remainder as the basis).
- `CalibrationDrillScriptTests` +1: `HoldTheLineCountsAPerfectBlockAsABlock`.

Rewritten in place (no count change):
- 4.1 (the deleted constant): `StoryBlockModelTests.ABlockedHitLocksTheBlockerInShieldstunForTheSharedWindow` (`ShieldstunFrames` → `ShieldstunFramesFor(10)`); `FighterBasicStringParityTests.ABlockedHitLocksTheBlockerIntoTheStanceForShieldstun` (the window the opener's measured damage earns, helper `OpenerDamageOnAnOpenTarget`) and `BlockChargesRegenerateOnTheSharedIntervalAfterTheStanceDrops` (bounded by `ShieldstunMaxFrames`); `SpecialBlockChargeCostTests.AnOrdinarySpecialAtThreeChargesLeavesOneWithShieldstunAndNoDaze`; `DefenceOrderingTests.APartialAbsorptionPassesOnlyTheRemainderAtTheOriginalClass` and `APartialAbsorptionOfAnOrdinarySpecialSpendsTwoChargesOfTheRemainder` (`ShieldstunFramesFor(20)`, the remainder); `BlockShatterLockoutTests.AStockLossClearsTheLockoutShieldstunAndCancelGate` (seeds `ShieldstunMaxFrames`).
- 7.4: `CollapseTremorTests.DebrisIsBlockableAsABasicClassHitAndSpendsOneShieldCharge` → `DebrisIsBlockableAsAHazardClassHitAndSpendsOneShieldCharge` (Hazard class, and the hero must be in the real Blocking state).
- **The audit (3.4).** I instrumented `PerfectBlockApplies` to throw on every perfect block and ran the full suite once (3376 cases), plus a Godot-log grep for the physics-callback path. Fixtures that reached a perfect block, and what I did:
  - `FighterHazardBehaviorTests.BlockingAbsorbsAHazardTickForOneShieldCharge` (sim): Block was first held on the hazard's first active tick → perfect, no charge (the case's `blockedCharges < 3` would fail). Re-timed: `RunSteamPipeStandoff` raises the guard when the fighter reaches its hold line in the telegraph and asserts it was up more than the window before the hazard went active.
  - `StoryBlockClassificationTests` (Story; its `EnterBlockStance` helper serves both cases): six fixed ticks left one perfect frame. Re-timed: settle until the stance is live, then hold the window out and assert it is shut; `EnemyBasicClassFireCostsOneChargeAndUnblockablesPierce` gained its one-charge assertion.
  - `StorySpecialRecoveryCancelTests.ABarrierAbsorptionArmsTheCancelButABlockOrAnInvulnerableTargetDoesNot` and `StoryHitConfirmTests.AShieldAbsorbedHitConfirmsAndRefundsButABlockedOrInvulnerableOneDoesNot` (Story; "a block does not land"): both struck a fresh raise. They passed either way (a perfect block is no landing either); re-timed so they test an ordinary block, with an `IsPerfectBlockWindowOpen` guard.
  - `RollbackReadinessTests.DelayedInputsConvergeForEveryKitOnEveryAuthoredStage` (cases 1 and 5): their scripted random inputs perfect-block during the convergence runs. Left as is — convergence now covers the new state.
  - The suites the plan names — `FighterBasicStringParityTests`, `FighterHitPipelineTests`, `StoryDefenceOrderTests`, `CpuVerbPolicyTests`, `FighterCpuBehaviorTests` — reach no perfect block (`ABlockedHitLocksTheBlockerIntoTheStanceForShieldstun` presses Block on the attacker's press tick, more than five ticks before contact). Story suites that raise the stance with `StartBlock` + `TransitionTo(Blocking)` (Punish window, Siphon Snare, Story Defence Order, …) never arm by construction.
- Fixed from the WIP (never run before): `FighterPerfectBlockTests`' escape harness (it pressed the attacker every 4 ticks, so the finisher landed on the escape tick; now the zero-knockback opener-then-chain-hold pattern of `FighterBasicStringParityTests`) and two enum assertions GdUnit's dynamic `AssertThat` cannot bind on the internal `FighterHitOutcome` (now compared as `int`).

Plan-listed in-place suites that needed no change (they read no flat 8 any more and pass): `StorySpecialBlockChargeCostTests`, `FighterGrabTests`, `SiphonSnareTests`, `StoryDefenceOrderTests`, `ShakespeareKitTests`, `FighterLockoutRefusalRulesTests`, `CpuVerbPolicyTests`, `EraMechanicToolkitTests`, `PuzzleEnvironmentToolkitTests`.

Shared test files touched (§3.2 map): `FighterBasicStringParityTests` (+1, two in place), `CalibrationDrillScriptTests` (+1), `StoryDefenceOrderTests` (none). No other workstream's case was rewritten except the audit re-times above.

Runs (each holding `D:/Projects/.ftt-gdunit-lock`, leftovers drained):
- WIP as found, CM-BLOCK filter (22 suites): `Failed: 2, Passed: 204, Total: 206` (the two harness failures above).
- Audit run (instrumented, full): `Failed: 9, Total: 3376` — the instrumentation's expected failures, listed above.
- After the fixes, the CM-BLOCK filter plus the new and re-timed suites (27 suites): `Failed: 3, Passed: 260, Total: 263` → fixed (a hero-hitstop pause in my new Story shieldstun case; the classification helper's settle); re-run `Passed: 8, Total: 8`.
- `--headless --import`: exit 0, `en.en.translation` unchanged.
- **Full run** (at `b80ad9e`): **`Passed! - Failed: 0, Passed: 3389, Skipped: 0, Total: 3389, Duration: 1 m 31 s`**, reconciled exactly against 3367 + 22. The `[TestCase]` attribute count under `tests/` is 3389. No `FATAL` in `godot.log`.
- `dotnet build`: 0 errors; only the pre-existing CS8632 and `KLSG_ECS004` on `FighterTuningComponent` (an incremental build); no `KLOTHO_DET002`.

## 6. Measurements

- Shieldstun by damage (both modes): 0–3 → 6, 4–7 → 7, 8–11 → 8, 12–15 → 9, 16–19 → 10, 20–23 → 11, 28 → 13, 36–39 → 15, ≥ 40 → 16.
- The sim window, contact ages from the first live stance tick: 0–4 perfect, 5 ordinary (`FighterPerfectBlockTests` case 1 sweeps leads 0–6). Story the same, counted from the first live stance tick (the tick after the press).
- Rearm: a re-raise after 19 lowered ticks is ordinary, after 20 arms, in both modes.

## 7. Not delivered / open

- **Nobody has seen the flash or felt the window.** §5 row 11 of the plan (learnability, the anti-mash rearm, how often a Hard CPU perfect-blocks) and row 12 (who acts first after a block, by damage) are untested by hand. The CPU is unchanged (9.3): a reactive Hard CPU (pulsed Block, 4–8-frame reaction) will perfect-block often; CM-CANCEL's smoke records how often.
- The bots brace with Block (`brace_hazard`, `brace_discharge`), so hazard damage in bot reports (`damage_by_source`) moves; bot batches before and after this change are not comparable.
- A perfect block during a rollback resimulation that is later corrected can flash twice (the driver's cache is cosmetic).
- A Story stance raised on the press tick is still not live on that tick (pre-existing); a hit in that tick's flush lands.

## 8. For the closeout

**Ledger** (new rows, owner `Unassigned`):
- `USER-PERFECT-BLOCK` (PFC-07, FGS-06, CMB-13; decision 3.4), amending the V7.3 block-model closure (audit H-5): a perfect block spends no charge and sets no shieldstun.
- `PROVISIONAL-PERFECT-BLOCK-RULES` — §3 (window 5, rearm 20, the arming rule, Guard-Crush and Shield-Breaker exclusions, the Punish window, Henry's yes / Orléans no, the bookkeeping and count bits, the flash, the drill).
- `USER-SCALED-SHIELDSTUN` (PFC-06, FGS-07; 4.1 / 11.2) and `PROVISIONAL-SHIELDSTUN-CURVE` (6 + damage / 4, 6..16; the two damage bases).
- **Close `VERIFY-STORY-HAZARD-BLOCK`** with the ruled source list; add `PROVISIONAL-STORY-HAZARD-BLOCK-SOURCES` (the blockable and unblockable lists of §1, `StageHazard` unblockable; debris now Hazard class after D04 and Aegis).
- Amend D01/D02b (perfect block is a variant of the ordinary block layer, order unchanged) and D03c (Story scope).

**Design master + mirror** (`design-godot.md` at `e2c655a`; none is a `DOC-MASTER-UNBOUND-BINDING` line):
- :1423–1450, the block section: add the perfect block (5-frame window, the arming rule, no charge / no shieldstun, Shield-Breakers still shatter, a Story Guard-Crush keeps its cost, no reflect or attacker stun).
- :1434 (F09 "8 frames") and :1432 (A01's "ordinary 8-frame shieldstun"): the scaled shieldstun.
- :1089 (F09 arcade note): shieldstun now scales with damage; still no landing lag.
- :337 (Collapse Tremor debris "blockable as a Basic-class hit"): Hazard class, through the environmental entry point after D04 and Aegis.
- :3937 (D03c): the Story scope and the ruled source list.

**Contracts** (edit both copies with the same hunk):
- `DEFENSIVE_EFFECTS.md` D01/D02b (the perfect block as a variant of the ordinary-block layer) and D03c (Story scope). The two copies are identical today and must stay so.
- `COMBAT_VALIDATION.md` :5 (the shieldstun) and a perfect-block row in the on-block table — by hand in both copies (they already differ at :37–38).

**AGENTS.md:**
- The Combat block bullet ("Shieldstun… 8 frames", "a non-shatter blocked hit locks the blocker into the stance for `ShieldstunFrames` (8 …)") → the curve, the perfect block, the deleted constant.
- The V7.3 environmental-damage bullet and the Collapse Tremor bullet ("blockable as a Basic-class hit") → `ApplyEnvironmentalHit`, the source list, Hazard-class debris.
- "Still held for a user ruling": remove `VERIFY-STORY-HAZARD-BLOCK`.
- The validation baseline (3389 at this branch, before the other Phase B merges).
- CLAUDE.md: nothing.
