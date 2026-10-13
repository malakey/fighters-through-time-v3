# GU-D — static Story knockback (request 5) and the carry-only D6a flight

Branch `worktree-wf_21dd1c67-9be-4` (from `main` at `b8f5343`, the gameplay-update plan commit). Plan `docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md` §2.5; recon `absolum_research/update_2026-10-12/recon/knockback.md` and `critic.md` §2.H / §2.I. **Story only.** No save field, schema, Klotho component, protocol, `.tres`, scene or translation changed. Nothing under `scripts/FighterSim/` changed except two comments, so no Fighter hash can move.

**Nobody has played it.** Every number below comes from headless GdUnit runs in real physics frames (Normal, `--fixed-fps 60`). Nobody has looked at a finisher, a wall bounce or a throw since the change.

The user's words (request 5, 2026-10-12): "In story mode, the knockback of enemies should be static. It should not be dependent on enemy health. So any abilities that knock the enemy back and cause the added wall bounce should be the same no matter what their health is. The health effects of the knockback should only apply to fighting mode."

## 1. Shipped

### Static Story knockback

- **`DamageCalculator.CalculateKnockback(Vector2 baseKnockback, float targetWeight, float targetCurrentHP, float targetMaxHP)` is deleted** (the victim-HP overload, `scripts/Combat/DamageCalculator.cs` :17–35 at `b8f5343`). The one remaining overload, `CalculateKnockback(Vector2 baseKnockback, float targetWeight, bool attackerFacingRight)` = `baseKnockback / (1 + max(0, weight))` with its x pushed the attacker's way, is now documented as Story's single, static knockback rule.
- Every Story victim now takes that overload:
  - **the hero and the Level 13 Mirror clone** — `PlayerController.ResolveIncomingHit` (the `if (hit.Knockback != Vector2.Zero)` block, ~:1712). The hero's DI stash (`_pendingLaunch = Velocity`) and slam-bounce speed inherit the static velocity with no code change;
  - **the Story throw launch** — `PlayerController.ResolveStoryThrow` (~:3579): `CalculateKnockback(impulse, victim.Data?.Weight ?? 1f, IsFacingRight) * 60f`. It used the post-throw HP before;
  - **mobs and elites** — `EnemyController.TryApplyKnockback` (~:1996), which every knockback-writing hit (`OnHurtboxHit`), crowd bowling (`ProcessThrownFlight`'s `other.TryApplyKnockback(new Vector2(2.5f, -1.5f), …)`) and the public test seam `ApplyKnockback` go through. D6a's `launchVx` and D9's bounce check read the static speed with no code change.
- **Bosses: verified unchanged.** `BossController.ApplyKnockback` (:1803) already used the 3-argument overload with a hard-coded weight of 2 and `Velocity +=`. It is untouched.
- What stays: the weight term; the attacker-side Story knockback lane (`StoryKnockbackMultiplier`, applied to the payload before it reaches the calculator); every knockback vector, multiplier and the 4.5× finisher; the M05 grounded slide; DI; S1's friction for every non-finisher launch.
- **`BasicComboRules.LowHealthKnockbackScale` stays, re-documented as Fighter Mode only.** It is the float statement of `FighterDamageRules.ScaleByMissingHP` (FP64, inside `ApplyFighterHit`). The sim has never called the float version. Only comments changed in `FighterEntitySystems.cs` (:734 and the `ScaleByMissingHP` summary), plus the `KnockbackMultipliers` doc.

### D6a: the finisher flight decelerates at its carry deceleration alone

- `EnemyController.ApplyStunnedKnockbackFriction` (~:1661), which used to read `decel = Mathf.Max(decel, _finisherCarryDeceleration);`, now reads **`if (_finisherFlight && _finisherCarryDeceleration > 0f) decel = _finisherCarryDeceleration;`**. A finisher flight brakes at `StoryEnemyCombatRules.FinisherCarryDeceleration` (`v²/(2(carry + v/120))`) in place of S1's 1800 px/s² ground friction and 600 px/s² air drag, in the air and on the ground, until it ends: at touchdown, at stun expiry while grounded or flying, or on any knockback write.
- This is required by the static launch. With static knockback but the old `max(S1, carry)`, Cleopatra's 300 px/s finisher landed **104.1 px, 34 px inside her own 138.2 px hit-1 reach**. The others landed 0–17 px short of their targets, because the first free frame starts grounded at S1's 1800 px/s² (table in §6, "intermediate"). With the carry alone, a carry that runs out in the air stops on the target by construction and never past it. One that touches down first (a slow launch, or an elite's short airtime) lands short.
- Comments were updated in four places: the S1 summary on `ApplyStunnedKnockbackFriction`, the `FinisherCarryDeceleration` summary in `StoryEnemyCombatRules` (:655–668), the `TryApplyKnockback` comment, and the `OnHurtboxHit` `launchVx` comment (~:2639, "static: weight, no HP term"). The D9 bounce keeps "the current deceleration (the finisher carry's, else S1)" (:823), which is still accurate.

## 2. Files changed

| File | Change |
|---|---|
| `scripts/Combat/DamageCalculator.cs` | Victim-HP overload deleted; the 3-argument overload documented as the static rule |
| `scripts/Characters/PlayerController.cs` | Knockback lines only: `ResolveIncomingHit` and `ResolveStoryThrow` call the 3-argument overload |
| `scripts/Enemies/EnemyController.cs` | `TryApplyKnockback` calls the 3-argument overload; the finisher-flight deceleration line; four comments |
| `scripts/Combat/StoryEnemyCombatRules.cs` | Comment only (`FinisherCarryDeceleration`) |
| `scripts/Combat/BasicComboRules.cs` | Comments only (`KnockbackMultipliers`, `LowHealthKnockbackScale` marked Fighter-only) |
| `scripts/FighterSim/FighterEntitySystems.cs` | Comments only (`ApplyFighterHit`'s knockback block, the `ScaleByMissingHP` summary) |
| `tests/Unit/StoryCombatRulesTests.cs` | One case rewritten, one added (§5); `using System.IO; using System.Linq;` |
| `tests/Unit/StoryEnemyCombatTests.cs` | One case added |
| `tests/Unit/StoryFinisherCarryTests.cs` | One case added; the landing-precision gate re-premised; two measurement columns in the printed table |
| `tests/Determinism/FighterKnockbackScalingTests.cs` | Comments only: the suite summary and the helper pin's stale "Story multiplies its knockback vector by it" |

Nothing outside my ownership was changed. The temporary throw-measurement suite (`GuDMeasureThrowTests`) was deleted before the commit.

## 3. Decisions (*Provisional* unless stated; each line is a candidate ledger item)

1. **Every Story victim is static, including the hero and the Mirror clone** (plan §2.5 and critic Q7's default: "Health effects only in fighting mode"). A hero at 25 % HP used to take ×1.75 knockback; now ×1.0.
2. **The carry deceleration also replaces S1's ground friction** during a finisher flight, not only the air drag (the plan's line, applied as written). A flight that touches down while still stunned keeps braking at the carry rate until the stun ends or the flight resolves.
3. **`LowHealthKnockbackScale` is kept** (not deleted with its pin), as the documented float statement of the Fighter rule, so `FighterKnockbackScalingTests.TheSharedScaleRampsFromOneAtFullHPToTwoAtZeroAndIgnoresDegenerateInputs` stands and the count does not change.
4. **Source-scan scope.** It covers every `.cs` under `scripts/` except `scripts/FighterSim/` and `BasicComboRules.cs`, skips comment lines (`//`, `///`), and leaves out `tests/`, where `FighterKnockbackScalingTests` pins the helper. "DamageCalculator has no HP parameter" is checked by reflection: exactly one `CalculateKnockback` with parameters `(Vector2 baseKnockback, Single targetWeight, Boolean attackerFacingRight)`. That is stronger than a text match.
5. **`StoryFinisherCarryTests` landing-precision gate re-premised.** The old gate, `CarryDeceleration > StunnedGroundFrictionPixelsPerSecondSquared` ("the carry rule binds"), encoded the retired `max(S1, carry)`. It is now `CarryStoppedInAir`: the flight's horizontal speed reached 0 before touchdown, which is when the carry-alone rule guarantees a landing within ±6 px of the target. In the after run, seven of the nine slasher rows and both C2 rows meet it. Leonardo touches down on the frame he stops, at exactly the target, so the gate leaves him out. Cleopatra and both elite rows are airtime-limited and stay bounded only by the existing `> R1 + 10` and `≤ target + 2` bars.
6. **Measurement aids kept in `StoryFinisherCarryTests`' printed table.** `stopped in air` and `D9 bounce r` are new. The bounce radius is the farthest the body travelled from where the finisher struck it while `IsWallBounceArmed` held and the sampled pre-move `|vx|` was above 300 px/s. A wall face within that distance of the leading edge bounces the mob. This matches spec D §1.4's definition and reproduces its 52–81 px table exactly on the before tree.

## 4. Deviations from the plan

- **+1 test beyond the plan's list:** `StoryFinisherCarryTests.AFinisherFlightBrakesAtItsCarryDecelerationAloneEvenWhereS1IsStronger`, a direct pin of the D6a rule change. It fails under the old `max` (frame 0: vx 495.00 against 500.40), as does the re-premised landing gate (Einstein 151.2 against 158.6). I checked this by temporarily restoring the `max` line and reverting it.
- The plan cites line numbers `EnemyController` ~:1656, ~:1990, ~:2634 and `StoryEnemyCombatRules` ~:655–663. I re-verified all four; they sit at :1656, :1990–1999, :2634 and :655–668 at `b8f5343`.

## 5. Tests

**New cases (+3):**
- `StoryCombatRulesTests.NoStoryScriptReadsTheFighterOnlyLowHealthKnockbackScale` (the source scan plus the reflection check on `DamageCalculator`). The suite goes 19 → 20.
- `StoryEnemyCombatTests.AnIdenticalFinisherWritesTheSameVelocityOnAMobAtFullHPAndAtThreeHP`: two Normal `chrono_slashers`, one at 40/40 and one at 3 HP, take the same Joan-shape finisher. The test asserts equal velocities, both equal to the static `CalculateKnockback(…) × 60`. Before the change the 3 HP mob flew about ×1.9 as fast. The suite goes 21 → 22.
- `StoryFinisherCarryTests.AFinisherFlightBrakesAtItsCarryDecelerationAloneEvenWhereS1IsStronger`: the first 12 free frames of a hand-built finisher flight (carry 1476 px/s² < S1 ground 1800), the first one grounded, each brake by exactly `carry × dt`. The suite goes 6 → 7.

**Rewritten in place (no count change):**
- `StoryCombatRulesTests.StoryKnockbackScalesWithTheVictimsMissingHPAfterTheHit` → **`StoryKnockbackOnTheHeroIsStaticAtEveryHP`**. The same hit lands at full HP, after a hit, and at a fifth of max HP. Each `Velocity.X` equals `CalculateKnockback(hit.Knockback, weight, true).X × 60`, and all three velocities are equal. This non-launcher on a grounded hero is an M05 slide with `vy = 0`, so the vertical is checked as equal across the three hits rather than against the raw vector.
- `StoryFinisherCarryTests.AssertLandsOneStepOut` (used by `EveryHerosFinisherLandsAStandardAboutOneStepOutsideItsHit1ReachAndAHeavyFinisherLandsFarthest`, `ATipRangeStringAndARangedMobAlsoLandOneStepOut` and `AnElitesFinisherAlsoLandsOneStepOutsideItsReachWithoutSteeringBack`): the ±6 px landing check is gated on `CarryStoppedInAir` instead of `CarryDeceleration > 1800` (decision 5). The harness also records `BounceRadius` / `CarryStoppedInAir`; that is measurement only.
- Comments only: `FighterKnockbackScalingTests` (summary item 1, the helper pin's comment).

**Runs (all holding `D:/Projects/.ftt-gdunit-lock`, `--no-build` after `dotnet build`, 0 errors and the 3 pre-existing warnings, no `KLOTHO_DET002`):**

| Run | Suites | Result |
|---|---|---|
| Before (game code untouched; harness columns and temp throw suite only) | `StoryFinisherCarryTests`, `StoryWallBounceTests`, temp `GuDMeasureThrowTests` | 13 / 13 |
| After, 1st | `StoryCombatRulesTests`, `StoryEnemyCombatTests`, `StoryFinisherCarryTests`, `StoryWallBounceTests`, `StoryEnemyCombatReviewTests`, `StoryDownAirSlamTests`, `StoryKnockdownTests`, `FeelPassStoryTests`, `StoryGrabTests`, `EnemyControllerTests`, `MirrorParadoxTests`, `FighterKnockbackScalingTests`, `DamageCalculatorTests`, `StoryEnemyCombatRulesTests`, temp throw suite | 201 / 202. The 1 failure was my new hero pin asserting `vy` against the raw vector on a grounded M05 slide; fixed as above |
| After, with the sim determinism suites | The 15 above + `RollbackReadinessTests` 22, `FighterCombatContractW3Tests` 6, `FighterHitPipelineTests` 6, `FighterHitConfirmTests` 18, `FighterSimulationTests` 23, `StoryCombatContractW3Tests` 4, `AbilityInterruptionTests` 5, `BossControllerTests` 23 | **309 / 309** (202 + 107, reconciled per suite) |
| Proof that the pins discriminate (old `max` line temporarily restored) | `StoryFinisherCarryTests` | 3 of 7 failed as intended; reverted |
| After, final targeted | The first 15 (with the new carry pin) | **203 / 203** |
| **Full suite** | everything | **`Passed! - Failed: 0, Passed: 3024, Skipped: 0, Total: 3024`**, 1 m 8 s |

**Reconciliation:** 3021 + 3 = **3024** exactly. The `[TestCase]` count under `tests/` is 3024.

**Fighter proof.** `git diff scripts/FighterSim` contains only comment lines. No sim or Networking file calls `DamageCalculator` (grep). `FighterKnockbackScalingTests` (all three cases, including the per-MaxHP raw FP64 impulse pins and the per-kit finisher separation) and the rollback, hit-pipeline, hit-confirm, contract and simulation suites above are green. No Fighter hash can have moved.

## 6. Measurements (for the ledger)

All on Normal, from `StoryFinisherCarryTests`' printed table (real strings in real physics frames, full-HP aggroed mob at pushbox contact, pivot-to-pivot), **before (`b8f5343`) → after**. `R1` = the hero's own hit-1 reach (far edge + the mob's hurtbox half-width). `g0` = gap at the finisher. `v0` = launch speed. Touchdown is counted in frames after the finisher connects.

### D6a / D9 on `chrono_slasher` (Standard, weight 0.8)

| Hero | R1 | g0 | v0 (px/s) | carry decel (px/s²) | lands (±R1) | touchdown f | D9 bounce radius |
|---|---|---|---|---|---|---|---|
| Einstein | 128.6 | 55.5 → 48.9 | 686 → **450** | 2163 → 892 | 158.6 (+30.0) → **158.6 (+30.0)** | 66 → 46 | 81.3 → **61.4** |
| Joan | 109.4 | 67.2 → 53.5 | 866 → **525** | 4726 → 1526 | 139.4 (+30.0) → **139.4 (+30.0)** | 70 → 46 | 64.3 → **54.7** |
| Leonardo | 133.4 | 49.2 → 44.5 | 553 → **375** | 1287 → 576 | 159.9 (+26.5) → **163.4 (+30.0)** | 63 → 45 | 76.2 → **39.3** |
| Lincoln | 104.6 | 102.3 → 74.2 | 1331 → **750** | 9628 → 2441 | 183.5 (+78.9) → **183.3 (+78.7)** | 76 → 47 | 77.0 → **92.7** |
| Cleopatra | 138.2 | 43.4 → 41.0 | 428 → **300** | 712 → 347 | 157.8 (+19.6) → **159.6 (+21.4)** | 61 → 45 | 52.5 → **0** (cannot bounce: exactly 300, the floor is strict) |
| Tesla | 119.0 | 49.2 → 44.5 | 553 → **375** | 1464 → 654 | 147.0 (+28.0) → **149.1 (+30.1)** | 63 → 45 | 68.9 → **33.7** |
| Shakespeare | 123.8 | 55.5 → 48.9 | 686 → **450** | 2263 → 932 | 153.8 (+30.0) → **153.8 (+30.0)** | 66 → 46 | 79.8 → **55.9** |
| Mozart | 123.8 | 48.6 → 44.5 | 653 → **458** | 1927 → 929 | 153.9 (+30.1) → **153.9 (+30.1)** | 72 → 53 | 79.4 → **62.2** |
| Tubman | 119.0 | 57.0 → 48.9 | 709 → **450** | 2565 → 975 | 149.1 (+30.1) → **149.0 (+30.0)** | 67 → 46 | 74.3 → **55.3** |

- The static horizontal launch is `F × 60 / 1.8`, where `F` = `BasicAttackKnockback` × the finisher tenths / 10. The vertical is 300 px/s up for every hero except Mozart (367). The factor removed was ×1.43–1.78.
- Every hero now lands **21.4–78.7 px** outside its own hit-1 reach (before 19.6–78.9). Lincoln still lands farthest (183.3; next Leonardo 163.4). No mob steers back after its stun.
- Touchdown moved from **61–76 → 45–53** frames after the connect. Stun end (31–33) and getup-armor end (67–69) are unchanged.
- `g0` fell for every hero, because hits 1 and 2 were HP-scaled too. Lincoln's launching hit 2 now leaves the mob at 74 px instead of 102.
- **Intermediate (static knockback, old `max(S1, carry)`), for the record of why the D6a fix ships with it:** Einstein 151.2 (+22.6), Joan 137.9 (+28.5), Leonardo 146.7 (+13.3), Lincoln 183.3 (+78.7), **Cleopatra 104.1 (−34.1, inside her own reach)**, Tesla 138.5 (+19.5), Shakespeare 147.1 (+23.3), Mozart 146.9 (+23.1), Tubman 142.9 (+23.9). The elite rows were Joan 125.6 (+10.2) and Lincoln 173.4 (+62.8), and `laser_rifle_infantry` 149.8 (+21.2).

### Other D6a rows

| Case | v0 | lands (±R1) | touchdown f | D9 bounce radius |
|---|---|---|---|---|
| Einstein, tip-range string, passive slasher | 686 → 450 | 158.7 (+30.1) → 158.7 (+30.1) | 66 → 46 | 15.8 → 12.4 |
| Einstein, close string, `laser_rifle_infantry` (w 0.9) | 613 → 426 | 158.0 (+29.4) → 158.6 (+30.0) | 60 → 44 | 81.1 → 54.1 |
| Joan, `chrono_guard_elite` (w 1.8, resistance zeroed) | 368 → 338 | 134.5 (+19.1) → 137.1 (+21.7) | 34 → 32 | 21.4 → 15.9 |
| Lincoln, `chrono_guard_elite` | 537 → 482 | 182.8 (+72.2) → 180.1 (+69.5) | 36 → 33 | 82.1 → 78.2 |

### Can a finisher bounce a mob at all (launch > 300 px/s)?

The condition is `F × 60 / (1 + w) > 300`, i.e. `w < F/5 − 1`. HP plays no part any more. Before, a hurt mob could always be bounced.

| Hero | F | Bounces mobs with weight below | In practice |
|---|---|---|---|
| Cleopatra | 9.0 | 0.8 | `holo_page`, `hologram_drone`, `voltaic_shock_drone` (0.7) and `laser_archer` (0.75) only; never `chrono_slasher` (0.8) or heavier |
| Leonardo, Tesla | 11.25 | 1.25 | Every standard; elites `unbound_eraser` (1.0) and `chrono_warden` (1.2) |
| Einstein, Shakespeare, Tubman | 13.5 | 1.7 | Every standard; every elite below 1.7 (not `cyber_cavalry_commander` 1.7, nor the 1.8s) |
| Mozart | 13.75 | 1.75 | As above, plus 1.7 |
| Joan | 15.75 | 2.15 | Everything |
| Lincoln | 22.5 | 3.5 | Everything |

Whether a wall actually fires the bounce then depends on the radius above. `rift_phantom` phases and never bounces.

### Forward throw on `chrono_slasher` (Normal, full HP, a real grab and throw; temporary harness, not committed)

The thrown flight has no horizontal drag, so its bounce radius is its whole flight.

| Hero | baseKB | Throw damage | vx (px/s) | Bounces? | Flight (frames / px) |
|---|---|---|---|---|---|
| Einstein | 3.0 | 5 | 337.5 → **300.0** | yes → **no** | 8 / 45.0 → 7 / 35.0 |
| Joan | 3.5 | 6 | 402.5 → **350.0** | yes → yes | 9 / 60.4 → 8 / 46.7 |
| Leonardo | 2.5 | 4 | 275.0 → 250.0 | no → no | 6 / 27.5 → 6 / 25.0 |
| Lincoln | 5.0 | 8 | 600.0 → **500.0** | yes → yes | 14 / 140.0 → 12 / 100.0 |
| Cleopatra | 2.0 | 4 | 220.0 → 200.0 | no → no | 5 / 18.3 → 4 / 13.4 |
| Tesla | 2.5 | 4 | 275.0 → 250.0 | no → no | 6 / 27.5 → 6 / 25.0 |
| Shakespeare | 3.0 | 5 | 337.5 → **300.0** | yes → **no** | 8 / 45.0 → 7 / 35.0 |
| Mozart | 2.5 | 4 | 275.0 → 250.0 | no → no | 6 / 27.5 → 6 / 25.0 |
| Tubman | 3.0 | 6 | 345.0 → **300.0** | yes → **no** | 8 / 46.0 → 7 / 35.0 |

- A forward throw now bounces a standard of weight `w` only when `180 × baseKB / (1 + w) > 300`:
  - Lincoln: every standard;
  - Joan: every standard but `shock_shield_legionnaire` (1.2);
  - Einstein, Shakespeare, Tubman: only `holo_page`, `laser_archer` and the two 0.7 drones (if grabbable);
  - Leonardo, Tesla, Mozart, Cleopatra: never.
- Before, every hero's throw bounced a sufficiently hurt standard: the ramp reached ×2 as the post-throw HP approached 0, so Cleopatra needed the mob under half HP after the throw, and Leonardo, Tesla and Mozart under 80 %.

### Difficulty

Story knockback no longer depends on difficulty. Easy's ×0.7 HP and Hard's ×1.5 used to change the post-hit HP fraction, so the same full string launched an Easy mob about ×1.93 and a Hard one about ×1.43 (recon, est.). The after table now holds on every difficulty.

## 7. Not delivered / open

- **Nobody has played it.** The shorter, earlier-landing finisher (45–53 frames instead of 61–76) is untested by hand. The 2026-10-11 bot finding — the first swing after a finisher whiffs 30 px short, because the bots re-swing about 47 frames after the connect while the mob landed 60–75 frames after it — may shrink now that touchdown is at 45–53. A bot batch would tell; none was run (that is GU-BOT / Phase C).
- **Bot batches before and after this change are not comparable** on finisher carry, landing gap, re-approach frames or wall-bounce frequency.
- `BossController.ApplyKnockback` still adds velocity and uses a hard-coded weight of 2. The HP half of that "open inconsistency" (`AGENTS.md:280`, `docs/GAMEPLAY_FEEL_2026-08-10_PLAN.md` §9 item 3) is moot now that all of Story is static; the additive / weight-2 half remains.
- Kit direct velocity writes (Rift Collapse on a caught hero, Lincoln's spike and bounce, the Tempest push) and Story environmental pushes were already static and are untouched. `design-godot.md:1282`'s claim that hazards are HP-scaled was already false for Story.
- Story Rally and Desperation Resonance still scale with missing HP. That is not knockback, and it is untouched.

## 8. For the closeout

**AGENTS.md:**
- :280 (the knockback bullet): Story is `baseKnockback / (1 + Weight)`, **static** (no HP term, any difficulty) since 2026-10-12. The missing-HP ×(1 + missingHPFraction) ramp is **Fighter-only** (`FighterDamageRules.ApplyFighterHit` / `ScaleByMissingHP`; `BasicComboRules.LowHealthKnockbackScale` is its float statement). Delete "Story `DamageCalculator` overload". The boss's HP inconsistency is moot; the additive / weight-2 note remains.
- :296 (Story enemy combat rules, the D6a and D9 sentences): replace "decelerates at `max(S1, v²/(2(carry + v/120)))`" with "decelerates at `v²/(2(carry + v/120))` alone, in place of S1, for the whole flight". The measured line becomes "every hero now lands an aggroed `chrono_slasher` 21–79 px outside its own hit-1 reach (21.4–78.7)", with touchdown 45–53 frames after the connect.
- :280-adjacent text and the "Known prototype gaps" cross-mode parity bullet: add "Story knockback is static; Fighter keeps the HP ramp".
- Validation section: the baseline moves 3021 → 3024 with GU-D's +3 (named in §5).

**CLAUDE.md:**
- :310: the `GAMEPLAY_FEEL_2026-08-10_PLAN.md` row's "HP-scaled knockback" needs "(Fighter only since 2026-10-12)".
- The baseline count.
- The bot comparability note: finisher carry, landing gap and wall-bounce frequency are not comparable across this change.

**`docs/GAMEPLAY_FEEL_2026-08-10_PLAN.md`:** supersession notes at §2.5 (:87–95, "HP scaling (all hits, both modes)" → Fighter only; the Story half is retired) and §9 item 3 (:456–462).

**Design-master lines owed** (`design-godot.md` mirror at `b8f5343`; edit the master, not the mirror):
- :97, "Knockback still scales with damage taken": now Fighter Mode only;
- :98, the "you fly farther" clause of Desperation Resonance: Fighter only;
- :1238, the `HitPayload.Knockback` comment "before weight / low-HP scaling": low-HP is Fighter only;
- :1282, "Low-HP Scaling … Applies to every hit in both modes … the `DamageCalculator` victim-HP overload (Story)": Fighter only; Story static;
- :1303, the knockback formula: split by mode;
- :1530, Forward Throw "KO-capable at low HP via the universal low-HP knockback scale": Fighter only;
- :1534, "Throw impulses obey … the low-HP knockback scale": Fighter only; Story throws are static.

**Ledger** (`docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md`):
- New **`USER-STORY-STATIC-KNOCKBACK`**:
  - the user's request 5 verbatim, with the scope ruling "Story only";
  - the build: the overload deleted; the hero, Mirror clone, throw, mobs, elites and bowling static; bosses already static;
  - `LowHealthKnockbackScale` documented Fighter-only;
  - the three pins;
  - the master lines above;
  - acceptance: a hands-on pass.
- Its **`PROVISIONAL-*`** row (or fold into `PROVISIONAL-SPACING-WALLS-RULES`):
  - decision 1 (the hero and the Mirror clone too);
  - decision 2 (the carry also replaces S1's ground friction);
  - decision 3 (the helper kept);
  - decisions 4–6 (test-side).
- Amend **`USER-FINISHER-CARRY`** (:321): the measured before → after table becomes the §6 table (2026-10-12 static numbers), and "applied as `max(S1, it)`" becomes "applied alone, in place of S1".
- Amend **`PROVISIONAL-SPACING-WALLS-RULES`** item 1(c) (:324): "realised as the deceleration `max(S1, v²/…)` … Cleopatra lands at +20" → the carry alone (2026-10-12). Cleopatra lands +21.4 (airtime-limited); the other close-string slashers land +30.0 ± 0.1, Lincoln +78.7.
- Amend **`S1-STUNNED-KNOCKBACK-FRICTION`** (:137): "Since 2026-10-11 a … finisher flight decelerates at `max(S1, the D6a carry deceleration)`" → "at the D6a carry deceleration alone (2026-10-12)".
- Amend **`USER-STORY-WALL-BOUNCE`** (:323):
  - the slasher's string-finisher bounce radii are now Cleopatra 0 (cannot bounce, 300 px/s), Tesla 33.7, Leonardo 39.3, Joan 54.7, Tubman 55.3, Shakespeare 55.9, Einstein 61.4, Mozart 62.2, Lincoln 92.7 (were 52–81);
  - the elites: Joan 15.9, Lincoln 78.2;
  - the forward-throw bounce list above;
  - "whether a hit bounces now depends on the hero and the mob, never on its HP".

## Review fixes (2026-10-13)

One confirmed Phase A review finding (low, a test defect), fixed by the GU-F review-fix workstream on branch `worktree-wf_0734101c-91c-2` from `199e260`. No game code changed for it; the build's hero knockback was already static.

**"Hero static-knockback pin never reaches its 'fifth of max HP' case (`Mathf.Max` instead of `Min`)."** `StoryCombatRulesTests.StoryKnockbackOnTheHeroIsStaticAtEveryHP` set the third hit's HP with `Mathf.Max(hpAfterSecond − 1, MaximumHP / 5)`, which for the harness Einstein (100 HP, 10-damage hits) is 79, so §5's "and at a fifth of max HP" never ran. Nothing reset `Velocity` between the three direct `TakeHit` calls either, so hits 2 and 3 would have passed on hit 1's leftover velocity had they written none.

- **Rewritten in place (no count change):** the same test. `Velocity` is zeroed before hits 2 and 3, so each proves it wrote its own impulse. The third hit lands at exactly `MaximumHP / 5` (20 → 10 HP), with a harness check that this is below the second hit's HP and above one hit's damage, and an assertion that the hero ends at 20 % HP or less. §5's description is now true as written.
- **Checked by temporary revert:** with a low-HP-only change in `PlayerController.ResolveIncomingHit` (the knockback written only at 50 % HP or more), the rewritten test failed ("at a fifth of max HP: vx 0.000, static 120.000"), where the old test would have passed at 79 HP. Restored.
- **Not done (optional in the finding):** a low-HP pin for `ResolveStoryThrow`'s static launch. Its HP independence is still pinned only by inspection.
- Test delta for this finding: 0 new, 1 rewritten. The review-fix branch's full suite read `Passed: 3114, Total: 3114` (3109 + 5; `GU_F.md`'s review-fix section has the runs).
