# GU-A — the single aerial strike (gameplay update 2026-10-12, request 1, both modes)

Branch `worktree-wf_21dd1c67-9be-1`, fast-forwarded to the plan commit `b8f5343` before any edit (the worktree had been created at `d2e52e0`). Plan: `docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md` §2.1 and §4 GU-A. Recon: `aerial.md`, `critic.md` §2.J / §2.K.

User request (verbatim): "For aerial attacks, it should just be a standard attack and no longer chain the combo. It should be a single attack." Scope ruling: **both modes**.

**Nobody has played this.** Every claim below is headless (unit, real-frame and sim tests). No pose, feel or balance judgement was made by a person.

## 1. Shipped

### The rule (`scripts/Combat/BasicComboRules.cs`, both modes)

- **`VariantAirAttack = 3`** (new). `SelectAttackVariant(up, down, airborne)` is now: Up → `VariantUpAttack`; Down and airborne → `VariantDownAir`; **airborne → `VariantAirAttack`**; else `VariantChain`. The three-hit string is grounded only. Grounded Down is still the string.
- **It never chains.** A press during the strike is dropped (no buffer, no queue), holding BasicAttack never repeats it, it never enters the chain hold, never advances the combo index, and exits straight out (Story `Airborne`, sim `PhaseNone`). Landing cancels it with no lag (it carries `FlagAerial` in the sim, `_attackStartedAerial` in Story). Its start arms the ability lockout and clears the D1 landed latch like every swing start. One strike per press; **no per-airtime limit**.
- **No chain hit ever starts in the air** (both modes): a chain continuation — buffered, held at the recovery end, or held in the chain hold — that would begin while the fighter is airborne **ends the string instead**. A fresh BasicAttack **press** in an airborne chain hold is an air press: it starts the air strike (or the Up-Attack / Down-Air by the held direction). Stated once in the `ChainHoldFrames` / `ChainContinues` docs.
- New constants (all *Provisional*):

| Constant | Value | Notes |
|---|---|---|
| `VariantAirAttack` | 3 | |
| `AirAttackStartupFrames` | 5 | template; per character `BasicStringProfile.AirAttackStartupFrames` |
| `AirAttackActiveFrames` | 7 | universal |
| `AirAttackRecoveryFrames` | 13 | universal (template total 25) |
| `AirAttackStringStep` | 0 | the string row whose box and knockback multiplier the strike takes (hit 1) |
| `AirAttackDamageMultiplier` | 1.0 | × `BasicAttackDamage` (like the directional strikes) |
| `AirAttackHitstunFrames` | 30 | hit 1's |
| `AirAttackLaunches` | false | no DI, tumble, D6a carry or D9 bounce |
| `AirAttackHitboxID` | `"air_attack"` | Story payloads; in `IsBasicStringHitbox` and `MomentumRefundFramesForHitbox` (20) |
| `AirAttackBoxCentreYUnits` | −12/60 (Story only, Y down) | the retired aerial hit 3's span |
| `AirAttackBoxHeightUnits` | 104/60 (Story only) | −64 … +40 px about the feet at 100 % |

- `MomentumRefundFramesFor(VariantAirAttack, _)` and `MomentumRefundFramesForHitbox("air_attack")` = `MomentumStringHitRefundFrames` (20). `IsBasicStringHitbox("air_attack")` is true, so the air strike gets the D1 hit-confirm, per-hit Momentum, the freeze-only Special buffer, S4's ×1.25 on standards and the default S5 stun floor (`EnemyBasicStunFloorFrames`, 24). It is not `combo_1`, so it has no hit-1 block-cancel gate (the sim passes `blockCancelableHitstun: true`, as for the directional strikes).
- **Retired, not left unread:** the template arrays `AerialStartupFrames` / `AerialActiveFrames` / `AerialRecoveryFrames`, the Story arrays `AerialStringBoxCentreYUnits` / `AerialStringBoxHeightUnits`, and `BasicStringProfile.AerialStartupFrames` (`int[]`). The profile now carries one `int AirAttackStartupFrames`; its constructor lost the aerial-finisher argument: `BasicStringProfile(gOpen, gFin, airAttackStartup, d1, d2, d3, width, height, …)`. Every profile's air startup is its old aerial opener (Joan 4, template/Cleopatra/Leonardo/Mozart/Tesla/Tubman 5, Einstein/Shakespeare 6, Lincoln 7).

### Story (`scripts/Characters/PlayerController.cs`)

- `StartComboHit` routes `VariantAirAttack` to the new **`StartAirAttack()`** before `StartDirectionalAttack()`, which now only ever receives the Up-Attack and the Down-Air. `StartAirAttack` writes: damage `BasicAttackDamage × StoryBasicDamageMultiplier × AirAttackDamageMultiplier`; `HitboxID = "air_attack"`; `AttackClass.Basic`; `Launches = false`; `HitDelivery.DirectHit`; `HitOrigin.Basic`; `HitstunDuration = 30/60`; `KnockbackForce = (baseKB × KnockbackMultipliers[0], −1)` (hit 1's); no status; **no combo mark**; pose `basic_attack_1`.
- **`ResolveAirAttackBoxPixels(profile, reachMultiplier, out size, out offset)`** replaces `ResolveAerialStringBoxPixels(step, …)`: string hit 1's x-span from `ResolveGroundStringBoxPixels(AirAttackStringStep, …)` (reach %, the AttackRange minor, front-only), the air strike's vertical span, facing read at active start (`OnAttackActiveStarted`).
- Timelines: `_aerialComboTimelines` (3) → one `_airAttackTimeline` (profile air startup / 7 / 13); `GetActiveComboTimeline()` selects it for the variant.
- **`AerialComboCounter` is deleted** (it could only ever be 0). `GroundComboCounter` is the one string counter; `GetActiveComboIndex()`, `AdvanceActiveCombo()`, `ResetActiveCombo()`, `ResetComboChain()`, `StartBasicSwing()` and `TransitionTo(Airborne / Idle)` were simplified accordingly.
- Walk-off: new private **`EndStringInTheAir()`** (reset the counter and buffers, leave the hold, `TransitionTo(Airborne)`). Called from `CompleteCurrentComboHit` (a continuation `ChainContinues` asks for while `!IsOnFloor()`) and from `ProcessRecoveryHold` (a held continuation while airborne). An airborne **press** in the hold calls `StartBasicSwing(upHeld, downHeld)`. `ChainSwingStartsThisTick()` mirrors both (an airborne held or last-frame start is no swing start; an airborne press in the hold still is). The hold's aerial landing cancel is deleted: only a grounded chain swing reaches the hold now.
- `NextChainStepForPress` returns 0 for the variant (unchanged code, doc updated). It does not read the ground — see §7.
- Joan's Wing-Dive cancel (`ProcessUsingMovementAbility` → `CheckAttackInput`) now yields the air strike by construction. **Wings Refresh and Zealous Vigor still key on `combo_3`, so they trigger on the grounded finisher only** (comments updated in `PlayerController.cs` and `JoanAbilities.cs`).
- **Found and fixed:** `StartDirectionalAttack` never cleared `ComboMark` / `ComboMarkFrames`, so an Up-Attack or Down-Air swung after Tesla's finisher applied his Conductive mark (the sim's directional strikes never do). It now clears both, like the air strike.

### Fighter sim (`scripts/FighterSim/FighterSimulationSystems.cs`, basic-attack rules only)

- **`FighterBasicAttackRules.FlagAirAttack = 128`**, the next spare bit of the existing `AttackFlags` int on component 305; `VariantMask = FlagUpAttack | FlagDownAir | FlagAirAttack`. **No component, snapshot layout or protocol change.**
- `StartSwing` maps `VariantAirAttack` → `FlagAirAttack` (with `FlagAerial`). `StartupFramesFor` / `ActiveFramesFor` / `RecoveryFramesFor` read the air strike's frames for the flag; the chain reads the ground table only (`FlagAerial` no longer selects frames, since no chain swing carries it).
- `ApplyBasicSwing` routes the flag to the new **`ApplyAirAttackSwing`** before `ApplyDirectionalSwing` (which now only receives the Up-Attack and the Down-Air): `ResolveStringBox(AirAttackStringStep)`, `ScaleDamage(BasicDamage, AirAttackDamageMultiplier)`, knockback `BasicKnockback × BasicKnockbackMultipliers[0]` on the scalar path, `AirAttackHitstunFrames`, no status, `verticalKnockbackScale = 0`, `blockCancelableHitstun: true`, `launches: false`, Foresight-counterable; `OnSwingResolved` with `MomentumRefundFramesFor(VariantAirAttack, 0)` (20). `ApplyConstructSwing` already deals one basic through step 0 for any variant swing.
- **`ProcessBasicAttackPhase`** (now `internal` so a suite can drive it on a hand-built fighter): a held continuation in the chain hold and a buffered/held continuation at the recovery end both `CancelString` when `fighter.IsGrounded == 0`; a press in the hold re-selects as before, which now yields the air strike when airborne.
- `NextChainStep` returns 0 for the flag via `IsVariantSwing` (no code change). The CPU (`FighterCpuController`) and the Mirror (`MirrorParadoxController`) needed **no change**: a press during the air strike reads the opener's band, which is the air strike's own box.
- Presentation: `FighterSimulationDriver` needed no change (`ComboIndex 0` → `basic_attack_1`).
- `FighterSimulationComponents.cs`: the `AttackFlags` doc lists every bit (1 airborne start, 2, 4, 8, 16, 32, 64, 128 air strike).

### Move List and localization

- `MoveListScreen`: the `AerialStartups` row is replaced by an **`AirAttack`** row, `Tr("movelist_air_attack")` = "Air attack: {0}/{1}/{2} frames — {3:0.#} damage, one strike that never chains" (profile air startup, 7, 13, `BasicAttackDamage × 1.0`).
- `localization/en.csv`: `movelist_aerial_startups` deleted; `movelist_air_attack` added under a `# GU-A` block at the end of the file. `--headless --import` run; `localization/en.en.translation` regenerated and committed.

## 2. Files changed

Production: `scripts/Combat/BasicComboRules.cs`; `scripts/Characters/PlayerController.cs`; `scripts/FighterSim/FighterSimulationSystems.cs`; `scripts/FighterSim/FighterSimulationComponents.cs` (doc); `scripts/UI/MoveListScreen.cs`; `localization/en.csv`, `localization/en.en.translation`.

Comment-only: `scripts/Characters/Abilities/JoanAbilities.cs`, `scripts/FighterSim/FighterKitMotion.Reach.cs`, `scripts/FighterSim/FighterEntitySystems.cs` (one comment, line ~1064), `scripts/Diagnostics/PlaytestCombatStats.cs`, `scripts/Diagnostics/PlaytestTelemetry.cs` (a note that finisher records no longer arise in the air).

Tests: new `tests/Unit/StoryAirAttackTests.cs`, `tests/Determinism/FighterAirAttackTests.cs` (+ `.uid`); edited `StoryAerialStringReachTests`, `FeelPassStoryTests`, `StoryCombatRulesTests`, `StorySpecialRecoveryCancelTests`, `MirrorParadoxTests`, `BasicStringProfileTests`, `DirectionalAttackTests`, `CpuMeleeBandTests`, `AbilityLockoutRulesTests`, `StoryEnemyCombatRulesTests`, `WingDiveTests`.

**Outside my ownership:** `tests/Unit/StoryEnemyCombatRulesTests.cs` (three `air_attack` rows added to existing loops/asserts in `BasicsPacingCoversOnlyTheUniversalBasicSet`, `TheHitIndexedStunFloorSpansEveryBufferedStringGap` and the elite-budget loop — additive, far from any knockback case); `FighterEntitySystems.cs` (one comment, as the plan listed). `ProcessBasicAttackPhase`'s visibility went `private` → `internal` (inside the basic-attack rules I own). Not edited although stale (GU-D owns that file's comments): `StoryEnemyCombatRules.StandardBasicDamageScale`'s doc ("the three string hits and the two directional strikes") and `BasicStringStunFloorFrames`' doc ("The finisher and the two directional strikes end the exchange") now also cover the air strike through `IsBasicStringHitbox`.

## 3. Decisions (Provisional — ledger rows)

1. **Air strike numbers** as tabled in §1: the profile's aerial-opener frames, 1.0× damage, hit 1's knockback multiplier (and Story's hit-1 vertical −1), 30-frame hitstun, no launch, hitbox ID `air_attack`, Momentum 20, pose `basic_attack_1`. Reasoning: the plan's table; 1.0× matches the other two single strikes (critic §1.8's "hit 1's damage shape" reading — Joan 1.0, Lincoln 0.7, others 0.8 — is the alternative).
2. **Story box vertical span −64 … +40 px** (the retired aerial hit 3's), x-span = hit 1's: so a jump-in reaches a grounded foe (the hit-1 span, −78 … −14, sat above the feet; critic §2.K). The sim keeps hit 1's grounded box (its aerial string always used it).
3. **No per-airtime limit**: one strike per press, a press during it dropped.
4. **No chain hit starts in the air** (both modes); an airborne **press** in the chain hold is an air press (air strike / Up-Attack / Down-Air by direction), while an airborne **held** or **buffered** continuation ends the string and the press is dropped. Reasoning: mirrors the sim's existing re-selection in the hold, so both modes behave identically.
5. **Retired the aerial frame and box data outright** (the plan allowed either): the profile constructor stayed simple (one argument fewer), and nothing reads hit-2/hit-3 aerial numbers any more. `AerialComboCounter` deleted for the same reason.
6. **Momentum mapping**: `VariantAirAttack` → `MomentumStringHitRefundFrames` (the air strike is the standard attack), not the directional constant; both are 20 today.
7. **`StartDirectionalAttack` now clears the combo mark** (bug fix, §1).

## 4. Deviations from the plan

- The plan asks for "the walk-off-a-ledge string end (both modes)". **Story** has a real ledge case. **In the sim a walk-off cannot reach the rule**: every authored edge (platform ends and Open-stage pit ends) is a grabbable ledge, and `FighterLedgeRules.CanGrab` does not check the attack phase, so a fighter walking off mid-string is caught by the ledge grab on its first airborne tick, which cancels the string (`FighterLedgeRules.Grab` → `CancelString`). The only real airborne continuation left is a one-way drop-through (Down + Jump) during the swing's startup or active frames, and every platform is too low for the fall to outlast hit 1 (a 2.4–4.0-unit drop lands in 15–24 ticks against hit 1's 26–29), except the legacy flat arena's drop-through floor (into the blast zone, ~2 ticks of margin). So the sim rule is pinned on `ProcessBasicAttackPhase` itself with a hand-built airborne fighter (`FighterAirAttackTests.AChainContinuationThatWouldStartAirborneEndsTheStringInsteadOfChaining`), which is why the method became `internal`.
- The worktree was created at `d2e52e0`, not at the plan commit; I fast-forwarded it to `b8f5343` (no local changes existed) before starting.

## 5. Tests

**New cases: +7** (two new suites, one `[TestSuite]` per file):

- `tests/Unit/StoryAirAttackTests.cs` (`[RequireGodotRuntime]`, real frames, 4):
  - `AnAirPressIsOneStrikeThatNeverBuffersHoldsOrContinuesAndLeavesToAirborne` — mashing and holding through Tesla's 5/7/13 strike: one box, live exactly on its active frames, `ComboCounter` 0, `NextChainStepForPress` 0, lockout armed, `basic_attack_1` wind-up pose, exits to `Airborne`, a hold never repeats it.
  - `TheAirStrikeIsOneBasicAtHitOnesKnockbackWithNoLaunchAndNoLeftoverRider` — Tesla and Lincoln payloads (ID, class, delivery, origin, 1.0× damage, no launch, 30/60 hitstun, `(baseKB × 1.0, −1)` knockback, pose); a leftover finisher status and Conductive mark are cleared by the air strike and by the Up-Attack (the fixed leak).
  - `ALandedAirStrikeConfirmsAndRefundsTwentyLikeStringHitOne` — on a `chrono_slasher`: damage applied, `SwingLanded`, both cooldowns −20 frames, Momentum counters 20, attacker hitstop.
  - `AGroundedStringThatWalksOffALedgeEndsInsteadOfChainingInTheAir` — a real floor edge, Tesla walking right through hit 1: control on unbroken floor continues to hit 2; held → string ends (`Airborne`, no second box in 30 more held ticks); a press buffered on hit 1's first active frame → same; no input → airborne chain hold, then a press → the air strike.
- `tests/Determinism/FighterAirAttackTests.cs` (pure C#, no Godot runtime, 3):
  - `AnAirPressLatchesTheAirStrikeWithItsProfilesFramesAndNeverChains` — Joan 4 / Tesla 5 / Einstein 6 / Lincoln 7: `FlagAirAttack | FlagAerial`, `ComboIndex` 0, `NextChainStep` 0, 7 active / 13 recovery, never `PhaseChainHold`, never buffered while mashed, `PhaseNone` exactly startup + 20 ticks later, still airborne.
  - `TheAirStrikeLandsOneBasicAtHitOnesKnockbackWithoutLaunching` — Joan (hit 1 authored at 1.0×) vs a standing Tesla: the air strike and her grounded hit 1 deal the same damage, 30 hitstun and the **raw-identical** victim `Velocity.x`; the victim stays grounded with `Velocity.y = 0`; `SwingLanded` 1, Momentum 20 / 20.
  - `AChainContinuationThatWouldStartAirborneEndsTheStringInsteadOfChaining` — `ProcessBasicAttackPhase` on a hand-built Tesla at hit 1: held at the recovery end, buffered at the recovery end, held in the hold → hit 2 grounded, `PhaseNone` airborne; pressed in the hold → hit 2 grounded, the air strike airborne.

**Rewritten in place (no count change):**

- `StoryAerialStringReachTests.AnAerialStringHitSharesTheGroundedXSpanInFrontAndKeepsItsVerticalSpan` → `TheAirStrikeTakesHitOnesXSpanInFrontAndReachesBelowTheFeet` (one box per held air press; hit 1's x-span; −64 … +40 scaled; bottom below the feet; equals `ResolveAirAttackBoxPixels`). The suite keeps its name; its turned-in-startup case is unchanged.
- `StoryCombatRulesTests.AerialComboUsesIndependentStateAndLandingResetDoesNotLeakIntoGroundCombo` → `AnAirPressIsOneStrikeThatNeverAdvancesTheStringAndLandingLeavesItFresh`; `DirectionalAttacksRouteToTheirOwnSingleStrikeOutsideTheChain` (air press → `air_attack`; `AerialComboCounter` → `ComboCounter`).
- `FeelPassStoryTests`: `TheOpenerIsPosedFromItsFrameWindowsAndTheChainHoldReadsAsLocomotion`, `APressDuringTheActiveFramesBuffersTheNextHit`, `APressDuringHitstopBuffersAndTimeFreezeDiscardsIt`, `HoldingAttackContinuesTheChainThroughTheFinisher`, `ThreePressesOnATwelveFrameRhythmLandThreeSwings` moved to a grounded harness (ground frames; the hold reads `idle`, not `fall`); the helper `TicksUntilTheOpenerAndItsHoldEnd` → `TicksUntilTheAirStrikeEnds` (`HitstopExtendsTemplateAndAuthoredProfileSwingsByExactlyTheFreeze` now pins the air strike's clock).
- `StorySpecialRecoveryCancelTests.NoSpecialOrEchoStepComesOutOfALandedRecovery` (positive control → `air_attack`).
- `MirrorParadoxTests.TheStoryAdapterProjectsYUpwardAndDeclaresTheFighterOnlyBlocksAbsent` (the floor-less clone's press is the air strike → `SelfNextStringStep` 0).
- `BasicStringProfileTests` (`EveryProfileStaysInsideTheAuthoredBands`: air startup 4–7, aerial-finisher band removed; `TheChassisStaysUniversal`: aerial hit-2 pin removed; `UnknownAndNullCharacterIDsResolveToTheTemplate`: template air startup).
- Rows added: `DirectionalAttackTests.TheSharedSelectionRuleIsTheOneSourceForBothModes` (airborne neutral → `VariantAirAttack`); `CpuMeleeBandTests.TheFinisherAndADirectionalSwingCannotContinueSoAPressDuringThemReadsTheOpenerBand` (two air-strike rows → 0); `AbilityLockoutRulesTests.MomentumRefundsTwentyPerHitSixtyOnTheFinisherAndCapsEachCycleAtOneTwenty` (variant and hitbox → 20, `IsBasicStringHitbox`); `StoryEnemyCombatRulesTests` (S4 pacing, S5 floor, elite budget rows); `WingDiveTests.AttackDuringTheDiveStartsTheAerialString` (asserts `FlagAirAttack`). Comment only: `DirectionalAttackTests.ACpuStyleZeroVerticalStreamNeverProducesAVariant`.

**Runs** (all with the shared lock held, `GODOT_BIN` set, after a warm-cache `--import`):

- Targeted 1 — the 16 directly affected suites: `Passed: 151, Total: 151` (equals their `[TestCase]` count).
- Targeted 2 — `FighterCpuBehaviorTests`, `CpuVerbPolicyTests`, `RollbackReadinessTests`, `FighterBasicStringParityTests`, `FighterHitConfirmTests`, `CpuRecoveryMatrixTests`, `StoryHitConfirmTests`, `StoryFsmM04TransitionTests`, `StoryMovementFeelTests`, `StringHitboxGeometryTests`, `PlaytestCombatStatsTests`, `PlaytestBotStrikeRangeTests`, `StoryAbilityLockoutTests`, `FighterAbilityLockoutTests`, `FeelPassSimTests`, `FeelPassRulesTests`, `JoanKitTests`, `FighterSimulationTests`, `UltimateActivationTests` (+ `StoryUltimateActivationTests` by substring), `FighterLockoutRefusalRulesTests`, `StoryLockoutRefusalEventTests`, `StoryFinisherCarryTests`, `StoryWallBounceTests`, `FighterKnockbackScalingTests`, `RollbackProtocolTests`: `Passed: 252, Total: 252` (249 counted + 3 `StoryUltimateActivationTests`).
- **Full suite: `Passed! - Failed: 0, Passed: 3028, Skipped: 0, Total: 3028`, 1 m 7 s.** Reconciliation: 3021 + 7 = 3028, and the `[TestCase]` attribute count under `tests/` is 3028.
- `dotnet build`: 0 errors, the three pre-existing warnings only (CS8632, CS0649, KLSG_ECS004); no `KLOTHO_DET002`.
- Every Fighter hash moves whenever a fighter attacks in the air (CPU matches included); no suite pins an absolute hash.

## 6. Measurements

Air strike frames (startup / active / recovery = total): Joan 4/7/13 = 24; Cleopatra, Leonardo, Mozart, Tesla, Tubman 5/7/13 = 25; Einstein, Shakespeare 6/7/13 = 26; Lincoln 7/7/13 = 27. (The retired aerial string ran 25 + 28 + 40 = 93 template frames for three hits.)

Damage: 1.0× `BasicAttackDamage` per strike (the old aerial string dealt the profile's 3.3× over three hits; its opener 0.8× template, 1.0× Joan, 0.7× Lincoln).

Story box, AttackRange minor 1.0 (px in front of the pivot; Y down from the feet): near / far edge = 24w / 96w for width factor w — template 24 / 96; Joan 21.6 / 86.4; Cleopatra 28.8 / 115.2; Leonardo 27.6 / 110.4; Lincoln 20.4 / 81.6; Einstein 26.4 / 105.6; Mozart and Shakespeare 25.2 / 100.8. Vertical: −64 … +40 at 100 % height; Joan (110 %) −69.2 … +45.2; Lincoln (115 %) −71.8 … +47.8. The old aerial hit 1 was −78 … −14 (entirely above the feet).

Sim box: string hit 1's grounded box (`ResolveStringBox(0)`), unchanged from the aerial string's opener.

## 7. Not delivered / open

- **Not played or seen.** Whether a single strike with no limit per jump feels right, and whether the −64 … +40 box is too generous on a jump-in, is a hands-on question.
- `NextChainStep` / `NextChainStepForPress` do not read the ground: during a grounded string that is airborne mid-swing (Story walk-off, sim drop-through), they still report the next chain hit, although that press now ends the string or (in the hold) becomes the air strike. The CPU band and the Mirror projection are off by one step only in that rare edge; fixing it needs a ground parameter on the sim's `NextChainStep` (the CPU's `Observe`) — left as is to keep CPU hashes stable.
- Story still does not re-select the variant on a **grounded** press in the chain hold (Up held there continues the string in Story, starts an Up-Attack in the sim) — the pre-existing difference the recon noted; unchanged.
- No Story test drives Joan's actual Wing-Dive into the air strike (the sim's `WingDiveTests` does); the Story path is `CheckAttackInput` → `StartBasicSwing`, so it is the air strike by construction.
- Telemetry and bots (GU-BOT, Phase B): `PlaytestTelemetry` finisher records no longer arise in the air, so finisher numbers before and after this change are not comparable; `compare_runs.py` will see a new `air_attack` hit ID.
- `ApplyConstructSwing` deals `BasicDamage` to constructs for every single strike rather than reading `AirAttackDamageMultiplier` / `DirectionalAttackDamageMultiplier` (both 1.0 today) — pre-existing for the directional strikes.

## 8. For the closeout

**Ledger:** a new `USER-AERIAL-SINGLE-STRIKE` (quote the request and the both-modes ruling) and `PROVISIONAL-AERIAL-SINGLE-STRIKE-RULES` (§3 items 1–6). Amend: `USER-STORY-AERIAL-REACH` (row ~320: the per-hit D6b boxes are gone; the single air strike keeps D6b's x-span rule with hit 3's old vertical span), `PROVISIONAL-SPACING-WALLS-RULES` item (2) (~324), `PROVISIONAL-REACH-MINOR-SHAPE` (b) (~246, "Story's aerial chain boxes" → the air strike box), `VERIFY-STORY-AERIAL-STRING-BOXES` (~249, closed; history note that the string itself is retired), `VERIFY-SIM-STRING-FACING-PER-TICK` (~250, mentions the aerial box), `VERIFY-SAME-TICK-VERB-PARITY` item (3) (~177, "on the tick an aerial string lands"), `PROVISIONAL-CPU-MELEE-BAND` (2) (~261, "an aerial string landed"); record the Tesla combo-mark leak fix (§1) and the `NextChainStep` ground edge (§7).

**AGENTS.md:** the Combat bullets "The basic combo has three hits …" (add the air strike as the third single strike; the string is grounded only), "Basic-combo timing and chain rules live in `FTT.Combat.BasicComboRules`" (drop "aerial 25/28/40"; replace the D6b aerial-box sentence with the air strike box; "landing cancels an aerial string" → the air strike; add "no chain hit starts in the air"); the universal-movement bullet (air dodge "legal from `Airborne` and an aerial swing's recovery/chain window"); the kit-motion bullet ("25–26 Ascendant Wings' rise and held Wing-Dive (Attack cancels into the aerial string)" → the air strike); the Klotho inventory row for 305 (`AttackFlags` bit 128 = `FlagAirAttack`); the cross-mode parity known-gap bullet (the `VERIFY-STORY-AERIAL-STRING-BOXES` parenthesis); the D6d telemetry line (a chain hit, the air strike, the up-attack or the down-air); and the baseline (3028 from this workstream alone).

**design-godot.md lines owed** (mirror line numbers; edit the master `design-godot-v7.md` first): 773 (FSM row "(aerial attack)"), 777 and 829 (air dodge "from an aerial attack's recovery or chain window"), 1043 ("aerial totals 25/28/40"), 1046 ("landing cancels an aerial string"), 1050–1062 (profile table: "Opener startup (gnd/air)" → the air column is the air strike's startup; "Finisher startup (gnd/air)" loses its air half), 1081–1086 (**the core rewrite**: "Aerial Basic Attacks … perform an aerial 3-hit combo string" and "Separation of Combo Chains" with its independent aerial counter), 1087–1089 (landing cancel: keep, "aerial attack" wording fine), 1095 ("Both never chain" → all three single strikes), 1099–1101 (Dynamic Hitbox Shifting, already superseded by D6b), 1297 ("Crouch & Aerial Attack Rule … aerial string uses its own … 25/28/40"), 2443 and 2524 (Joan's Wing-Dive "cancels into her aerial string"), 3335 (Move List content: add the air strike row), 3774 and 4851 (H03 "the aerial 3-hit string plays basic_attack_1–3" → the air strike plays `basic_attack_1`), 3789 (animation table "Aerial Attack (3-Hit Combo)"), 3912 (the aerial landing cancel — still valid). Contracts: `COMBAT_VALIDATION.md` :26 ("Aerial string | Each hit"), :32 ("vs aerial strings"), :45 ("String and down-air"); `ABILITY_DATA.md` :39 (Wing-Dive "cancels it into her aerial string").
