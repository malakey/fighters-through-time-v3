# CM-FIXA-STORY handoff: Story kit motion and armour (Phase A fix round)

Combat and movement update 2026-10-16, Phase A fix round. Branch `worktree-wf_a50df6ba-c2b-2`, based on `main` at `5434151` (the merged, green Phase A, 3332/3332).

The findings came from the STORY array of `D:/Projects/genre_review_2026-10-13/fixA_inputs.json`. There were four, and all four are fixed. The verifiers' corrected fixes were used. Nothing in this round changes a user ruling.

Commits:
- `fe4e8db` kit motions travel on every active tick, and the Rail Charge startup holds X
- `3becde4` the Rift Collapse launch respects Story armour
- this handoff

## 1. Findings fixed

### 1.1 Divine Piercing lunged 11 of its 12 active ticks (165 px instead of 180)

**Cause.**
- `PlayerController` moves (`MoveAndSlide`) before its ability nodes process.
- A cast's phase advances inside the kit's own `_PhysicsProcess`, through `base._PhysicsProcess` → `AdvanceToActive` → `OnActive`.
- `JoanDivinePiercing` wrote the lunge velocity only while `CurrentPhase == Active`. So on the first Active tick the controller moved with the velocity left over from startup, which is 0 on the ground under the Q1 brake.
- `OnRecovery` then zeroed the last write.

**Fix.** `OnActive` latches the facing and calls the new private `DriveLunge()`. The per-tick write in `_PhysicsProcess` uses the same helper.
- All 12 active ticks now move 15 px, which is 180 px. That is the sim's `AdvanceSpecialCast` window.
- The thrust cadence (`_activeFramesElapsed`) is unchanged.
- `ApplySpecialCastBrake` is unchanged: it already exempts Divine Piercing's Active phase.

**The Grand Crusade (`JoanGrandCrusade`), for consistency only.** It is a Story Ultimate with no cross-mode distance. It got the same `OnActive` → `DriveCharge()` change, so it charges on all 54 active ticks: 810 px, previously 795.

### 1.2 Tesla, Tubman, Cleopatra and Mozart movement abilities lost their first Active tick

This was CM-STORY §7 open item 1. The cause is the same as 1.1. Each kit now drives its travel from `OnActive`, as Lincoln's Rail Charge already did:

| Kit | `OnActive` now writes | Travel before → after |
|---|---|---|
| Tesla, Lightning Blink | `DriveBlink()`: `_blinkDirection × distance / translation time` (new helper, also used per tick) | 165 → **180** px |
| Tubman, North Star Leap | `_direction × _speed` | 226.67 → **240** px |
| Cleopatra, Desert Mirage | `DriveRush()`: `_mirageDirection × distance / duration` (new helper, also used per tick) | 224 → **240** px |
| Mozart, Sonata Drift | `_glissandoDirection × _glissandoSpeed` | 170 → **180** px |

Consequences:
- **Tubman.** The extended ledge snap in `OnRecovery` now measures from the authored 240 px end. A ledge 55 px past that end is caught (within 14 + 18 + 30 = 62 px). It used to be missed, at 68.3 px.
- **Mozart.** The staff is now placed at the authored end of the glissando.
- **Cleopatra.** Her first rush tick no longer moves with her stale startup velocity, which included startup gravity.

### 1.3 Lincoln's Rail Charge carried run speed through its startup

This was CM-STORY §7 open item 2.
- The sim's `FighterKitMotion.RailCharge` overwrites X from the press. Story's 12-frame startup kept the entry run speed, because the movement-ability state applies no friction.
- **Fix.** `LincolnRailCharge.OnStartup` now writes `Velocity.X = 0`. Y is kept: Story gravity runs through the whole cast.
- **Result.** A running Lincoln (228 px/s) travels **300.0 px** from the press. It used to travel 345.6 px: 45.6 px of startup slide plus the 300 px charge.
- The standing charge, the off-ledge fall and Q7's recovery stop are unchanged (the existing suites stay green).

### 1.4 Einstein's Rift Collapse stunned a caught `PlayerController` through the Ultimate's startup armour

**Cause.**
- The collapse's 0-damage hit returns from `ResolveIncomingHit` before that method reads armour.
- `LaunchCaughtTarget` then wrote the caught hero's velocity and called `ApplyStun` directly.
- `ApplyStun` → `InterruptActiveAbilities` cancelled the paid Ultimate in its armoured startup. That breaks 2.4's acceptance in the Level 13 Einstein Mirror match, in both directions.

**Fix.** The direct launch now also requires `!caughtPlayer.HasActiveHyperArmorAgainst(hit.AttackClass)`. That is the same read `ResolveIncomingHit` uses to deny knockback and stun on every other hit.
- It is read at launch, not at the catch, because an Ultimate accepted during the six-frame pull is armoured too.
- It also covers Rail Charge's `GrantsHyperArmor` window and `ApplyStoryHyperArmor` timers.

## 2. Tests

All are new; no existing case was rewritten. Each one failed on the unfixed code. That repro run gave `Failed: 8, Passed: 3, Total: 11`, with the before numbers in the tables above.

**New suite `tests/Unit/StoryKitMotionTravelTests.cs` (7 cases).**
- One `[TestSuite]` in the file.
- Every case runs in real physics frames through the controller, with real input via `PlayerInputFrame.Create`.
- The measurement reads the cast's phase at the start of each frame (the phase the controller's move sees) and the displacement that frame's move made. It asserts:
  - the active-tick count equals the authored count;
  - every active tick, including the first, moves one authored step (±0.05 px);
  - the active total is within ±1 px.

The cases:
1. `DivinePiercingLungesThreeUnitsOnEveryActiveTickFromAStandstillAndFromARun`
   - Standstill: 180 px total, 0 px in startup, 0 px in recovery.
   - From a full run: the lunge is still 12 × 15 px.
2. `TheGrandCrusadeChargesOnEveryActiveTick`: 54 × 15 px.
3. `TeslasBlinkTravelsItsAuthoredDistanceOnEveryTranslationTick`: 12 × 15 px, 180 px from the press.
4. `TubmansLeapTravelsFourUnitsAndCatchesALedgeAtTheEndOfItsExtendedSnap`
   - 18 × 13.33 px, 240 px from the press.
   - A `LedgeGrabPoint` 55 px past the authored end, in open air, ends the cast in `LedgeHanging`.
5. `CleopatrasRushTravelsItsAuthoredDistanceFromAStandstillAndFromARun`: 15 × 16 px from a standstill and from a run.
6. `MozartsGlissandoTravelsItsAuthoredDistanceSidewaysAndStraightUp`: 18 × 10 px held right on the floor, and straight up in open air.
7. `LincolnsRunningRailChargeTravelsThreeHundredFromThePress`: 0 px startup slide and 300 px from the press.

**`StoryRiftCollapseTests` +1 (3 → 4).** `ARiftCollapseNeitherLaunchesNorInterruptsAHeroInItsUltimateStartup`.
- An Einstein on slot 1 (the clone's slot) collapses its rift on a slot-0 Joan who is in her Ultimate's startup.
- Expected: the Ultimate keeps executing, the state stays `UsingUltimate`, and the hero is not launched (vy ≥ 0).
- Positive control: the same collapse on an unarmoured hero gives `Stunned` with vy < 0.

**Delta: +8 exactly.**

## 3. Validation

**Build.** `dotnet build`: 0 errors. There are 2 warnings, both pre-existing: CS8632 in the vendored GdUnit API, and `KLSG_ECS004` on `FighterTuningComponent`. CLAUDE.md's third warning, CS0649, does not appear on this tree. No `KLOTHO_DET002`.

**Targeted run.** The new suite plus 26 related suites, 212/212:
- StoryRiftCollapse, StoryMovementAbilityGravity, StoryUltimateArmor
- TeslaLightningBlinkStory, TubmanKitStory, CleopatraSandDecoy, MozartSonataDriftCharge, ResonanceTraversal, StoryReachKit
- StorySpecialCastMotion, MirrorParadox, AbilityInterruption, StorySpecialTimingProbe, ShapeQueryPropDelivery, StorySpecialRecoveryCancel, StoryKitPullWall
- Einstein/Joan/Lincoln/Tubman content
- StoryHitConfirm, FeelPassStory, StoryCombatRules, StoryAbilityDamageRouting, KitAlignmentContract, StoryAbilityLockout

**Full suite.** `Passed! - Failed: 0, Passed: 3340, Skipped: 0, Total: 3340, Duration: 1 m 31 s`. 3340 = 3332 + 8.
- One run, holding `D:/Projects/.ftt-gdunit-lock`, with `testhost` and `Godot*_console` drained first.
- `--headless --import` ran once at worktree setup, under the lock.

**Fighter hash moves: none.** No file under `scripts/FighterSim/` (or anything the sim reads) was touched. Every change is a Story kit script.

## 4. Measurements

These are real frames, from the `[CM-FIXA-STORY]` lines in the Godot log. Each line gives startup / active / recovery / total, in px along the travel axis.

| Cast | Startup | Active | Recovery | Total |
|---|---|---|---|---|
| Divine Piercing, standstill | 0 | 180 (12 ticks) | 0 | 180 |
| Divine Piercing, from a run | 15.5 (the Q1 brake ramp) | 180 (12) | 0 | 195.5 |
| Grand Crusade | 0 | 810 (54) | 0 | 810 |
| Lightning Blink | 0 | 180 (12) | 0 | 180 |
| North Star Leap | 0 | 240 (18) | 0 | 240 |
| Desert Mirage, standstill | 0 | 240 (15) | 0 | 240 |
| Desert Mirage, from a run | 69.6 (see §5.1) | 240 (15) | 0 | 309.6 |
| Sonata Drift, sideways | 0 | 180 (18) | 0 | 180 |
| Sonata Drift, straight up | 0 | 180 up (18) | 0 (lands on the staff) | 180 up |
| Rail Charge, from a run | 0 | 300 (30) | 0 | 300 |

## 5. Not fixed, and why

1. **A running Cleopatra still slides through her Desert Mirage startup.** She moves 12 × 5.8 = 69.6 px before the 240 px rush. The sim's `SandRush` starts at the press, so it covers 240 px from the press.
   - This is the same class of defect as the Rail Charge startup in §1.3.
   - It was not in this round's findings. CM-STORY's disposition says "the startup carries her entry velocity under gravity" (`PROVISIONAL-MOVEMENT-ABILITY-GRAVITY-SCOPE`).
   - So I did not widen the scope. If wanted, the fix is one line, the same as Lincoln's: zero X in `OnStartup`. A running-start case could then assert the total.
2. **Divine Piercing from a run** slides 15.5 px in its startup. That is the Q1 brake's 4-frame ramp, the ruled behaviour in both modes, not a defect.
3. **The Rift Collapse still pulls an armoured body.** Only the launch (knockback and stun) is gated. No other kit pull reads armour either (the Lorentz pull, the Cataclysm drag, `PullThroughCollision`), so the pull was left as it is. The verifiers marked a catch-time skip as optional.
4. **The reverse direction is not tested separately.** That case is the hero's collapse on the clone's armoured post-contact Ultimate startup. It runs the identical code path.
5. **The sim's `FighterRiftRules.TryCollapse` armour behaviour was not examined.** This was a Story ruling only.
6. **`StoryReachKitTests.TheWingDiveIsSteepAndForwardAndThePiercingLungeCoversThreeUnits` keeps its arithmetic.** It still pins the speed derivation, and the real-frame travel is now in the new suite. It was left unchanged so this round's delta stays additive.

## 6. What the closeout owes (docs and ledger; not edited here)

**Ledger rows:**
- `VERIFY-STORY-KIT-MOTION-FIRST-TICK`, suggested by CM-STORY §7 item 1: record it as **closed by CM-FIXA-STORY**, with the before → after table in §1.2, plus Divine Piercing (165 → 180) and the Grand Crusade (795 → 810). Cite Q7's "travels the authored distance" intent.
- `FIX-STORY-RAIL-CHARGE-RECOVERY-CARRY` (Q7): add the startup hold. A running Lincoln now travels 300 px from the press. This closes CM-STORY §7 item 2.
- `PROVISIONAL-STORY-ULTIMATE-ARMOR` / `USER-STORY-ULTIMATE-ARMOR`: add the Rift Collapse launch as a second armour read site. The direct launch is gated, and an armoured body is still pulled.
- If the user wants it: a row for §5.1, Cleopatra's startup carry against the sim's press-start `SandRush`.

**`AGENTS.md` validation section:** the baseline moves 3332 → 3340. The +8 is the new `StoryKitMotionTravelTests` (7) and `StoryRiftCollapseTests` 3 → 4.

**`docs/handoffs/CM_STORY.md` §6 / §7:** the "Tesla's blink should cover about 165 px" note and items 1 and 2 are superseded by this round.

**Design master:** no change. Divine Piercing's "about 3 units" now holds exactly in Story, and the movement abilities travel their A15 distances.
