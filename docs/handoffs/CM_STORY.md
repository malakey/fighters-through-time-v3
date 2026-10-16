# CM-STORY — Story combat feel (combat and movement update, 2026-10-16)

**Branch** `worktree-wf_408a1af8-cf8-2`, branched from the plan commit `735296f`. **Worktree** `D:\Projects\Fighters Through Time - V3\.claude\worktrees\wf_408a1af8-cf8-2`.

**Commits**
- `4882d85` — 1.1(1) movement-ability gravity and Q7, 2.4 Ultimate startup armour, 7.1 ability shake, 7.3 Story Echo Step cost.
- `1f166be` — 8.1 standard basic floor and 8.2 dimmed reduced numbers (one commit, as the brief asks; they share the `ResolveIntakeScale` signature).
- `eec3354` — 8.4, the Story half.
- This handoff, committed last.

The first commit holds four decisions because they share `PlayerController.cs` hunks, and this environment has no interactive staging to split them.

**Rulings built against:** Q2 ("startup only") and Q7 ("stop it like the sim"), both from §0.2. No other §0.1 question touches this workstream.

**Nobody played the build.** The evidence is headless only: GdUnit suites, two smoke runs and a load check.

---

## 1. Shipped

### 1.1(1) Story movement abilities get gravity; Leonardo's glide sinks (MOV-13)

**The controller**
- `PlayerController.ProcessUsingMovementAbility` calls `ApplyMovementAbilityGravity(dt)` first. The body is in the new partial `PlayerController.MovementAbilityGravity.cs`.
- That method applies Story gravity unless `BaseSpecial.SuspendsOwnerGravityNow` is true.
- The gravity goes through `ApplyGravity(dt, ignoreShortHop: true)`. The new optional parameter passes `jumpHeld: true` to `StoryAirPhysics.StepVertical`, so an ability's own boost is never cut by the ×2.5 short-hop band.
- After gravity, it clamps `Velocity.Y` to `BaseSpecial.OwnerFallSpeedCapNow`.

**New members on `BaseSpecial`**, both beside `PassesThroughProjectilesNow`:
- `public virtual bool SuspendsOwnerGravityNow => false`
- `public virtual float OwnerFallSpeedCapNow => float.PositiveInfinity`

**Overrides**

| Kit | `SuspendsOwnerGravityNow` true during |
|---|---|
| Joan, Ascendant Wings | `IsExecuting`: every phase and the Wing-Dive |
| Tesla, Lightning Blink | Startup, Active |
| Tubman, North Star Leap | Startup, Active, and Recovery while `RecoveryFramesElapsed == 0` (the sim's `LeapEnd`) |
| Einstein, Relativity Warp | Startup, Active |
| Shakespeare, Prospero's Flight | Active |
| Cleopatra, Desert Mirage | Active |
| Mozart, Sonata Drift | Startup (the opening beat is kept), Active |
| Leonardo, Ornithopter Flight | only during the Daedalus dive (`_isDiving`). It also has `OwnerFallSpeedCapNow` = `GlideSinkSpeedPixels` (40) while gliding. New test seam: `LeonardoOrnithopterFlight.GlideSinkSpeed`. |
| Lincoln, Rail Charge | none (gravity throughout) |

**Lincoln's Rail Charge**
- It now writes only its horizontal speed, through the new private `DriveCharge()`. It used to write the whole vector, which zeroed Y every tick. See Deviation 2.
- **Q7:** `OnRecovery` zeroes the horizontal velocity.
- `OnActive` drives the charge on the tick it enters Active, so the 30 active frames move exactly the authored 300 px. See Deviation 2.

### 2.4 Story Ultimates armoured through their startup (Q2: startup only)

- New rule: `StoryCombatRules.UltimateStartupArmorPreventsInterruption(AbilityPhase phase, AttackClass incoming) => phase == Startup && incoming != Ultimate`.
- New partial `PlayerController.UltimateArmor.cs` adds `UltimateHasStartupArmor(AttackClass)`. It is OR'd into `HasActiveHyperArmor` (read against Basic) and into `HasActiveHyperArmorAgainst`.
- The rule is keyed to the Ultimate's own cast clock, so the caster's hitstop and Time Freeze pause it.
- All nine `ultimate.tres` keep `GrantsHyperArmor = false`.

**The StaticCharge leak, closed for every armour window**
- `ResolveIncomingHit` now applies a hit's status through `ApplyHitStatus(in hit, hasHyperArmor)`.
- While the hit is armoured against, that method sets `internal bool ArmorAbsorbsStatusStun`. `StaticChargeStrategy.OnApply` then returns before `ApplyStun`.
- The charge still enters the control slot.
- This covers the Ultimate startup, `GrantsHyperArmor` casts (Rail Charge) and `ApplyStoryHyperArmor` timers.

**The Level 13 clone.** Its post-contact Story Ultimate runs through the same controller, so its startup is armoured too. `MirrorParadoxTests` is green with no changes.

### 7.1 Ability-hit shake scales with damage (CMB-02)

**The rule**
- `StoryEnemyCombatRules.DealtHitShakeIntensity` and `DealtHitShakeDuration` now take a `HitOrigin`. The old four-argument overloads are replaced:
  - `DealtHitShakeIntensity(AttackClass, HitOrigin, float authored, int damage, bool launches)`
  - `DealtHitShakeDuration(AttackClass, HitOrigin, float authored, float authoredDuration, bool launches)`
- New predicate `ScalesAbilityShakeWithDamage(attackClass, origin)` is `attackClass == Special && origin != Ultimate`.
- For such a hit, the intensity is max(authored × 12, the damage curve) and the duration is max(authored, the curve's duration).
- Ultimates keep the authored value. Basics keep the curve.

**The callers**
- `EnemyController.PresentDealtHit` and `BossController.PresentDealtHit` pass `hit.Origin`.
- `PlayerController.ResolveIncomingHit`'s shake block calls `ResolvePlayerDealtShake` (in `PlayerController.StoryFeel.cs`) when `AttackerIndex >= 0`. Enemy hits on the hero keep the old values.
- `CameraShake`'s accessibility scale still applies.

**Results:** the Emancipator (40, launching) shakes 8 px, a 4-damage Special 2.4 px, an Ultimate authored at 0.6 shakes 7.2 px.

### 7.3 Story Echo Step costs 30 × 0.35 (CMB-16, TWO-10)

- `StoryCombatRules.StoryEchoStepMeterCost = BasicComboRules.EchoStepMeterCost * MeterGainScale` = **10.5**. It is a `const float`, derived rather than written as a second 0.35.
- `StoryCombatRules.EchoStepMeterCost(bool storyScaled)` returns that value or 30.
- New `PlayerController.EchoStepMeterCost => StoryCombatRules.EchoStepMeterCost(AppliesStoryMeterGainScale)`. `TryStartEchoStep` checks and drains it.
- Only the campaign hero pays 10.5. The Mirror clone, the drills, the Holodeck, Fighter Mode and the bots' `--story-meter-scale=off` A/B all pay 30.
- `systems_card_echo_body` now reads "Spend meter during the recovery …", with no cost. It stays in place (`en.csv` :959) under a `# CM-STORY` comment. `en.en.translation` was regenerated with `--import`.

### 8.1 Story per-hit floor on basics against standards (CMB-03)

- `StoryEnemyCombatRules.StandardBasicDamageFloor = 6`.
- `StandardBasicPacingScale(int base)` = 1 when base ≤ 0, otherwise max(1.25, 6 / base). It replaces the bare ×1.25 at the S4 site in `EnemyController.ResolveIntakeScale`.
- `ResolveIntakeScale` gained an `int baseDamage` parameter.
- The floor multiplies with the S2/D4 chain scale and the D8 Punish scale under the one `ScaleIntakeDamage` rounding:
  - hit 4 of a chain decays a floored 6 to 5;
  - a punished floored 6 is 7.
- Standard tier and the universal basic set only.

### 8.2 Dim reduced damage numbers (CMB-11)

**The rule**
- `ResolveIntakeScale` gained `out bool prorated`.
- New predicate `StoryEnemyCombatRules.IsProrated(chainScale)` is `chainScale < 1`, meaning the ×0.6 second-Special tax or the ×0.85 decay.
- An untaxed first Special is never dimmed, and neither is any S4 or floor increase.

**The look**
- `PresentDealtHit(hit, dealt, punished, prorated)` floats a prorated number in the new `UIPalette.DamageNumberReduced` = (0.66, 0.68, 0.74), at the new `StoryEnemyCombatRules.ProratedDamageNumberScale` = 0.8.
- A hit that is both punished and prorated shows the Punish treatment (GoldBright at ×1.25).
- `FloatingDamageNumber` needed no code change, only its doc comment.

### 8.4 One named hurtbox value per mode (Story half, no number change)

- `PlayerController.GroundStringBoxAnchorYPixels` was a `-32f` literal. It is now `-StoryCombatRules.HeroHurtboxHeightPixels / 2f`, and `internal` so the test can read it.
- The factory's collision body box gets its own names: `CharacterFactory.HeroBodyBoxWidthPixels` = 40 and `HeroBodyBoxHeightPixels` = 64. It was the literal pair `new Vector2(40, 64)` / `(0, -32)`.

## 2. Files changed

**Owned**
- `scripts/Combat/BaseSpecial.cs`
- The nine `scripts/Characters/Abilities/*Abilities.cs` and `EinsteinRelativityWarp.cs` movement classes (+ `LincolnRailCharge` `OnActive`/`OnRecovery`)
- `scripts/Combat/StoryCombatRules.cs`: the Echo Step section after `MeterGainFactor`, and `UltimateStartupArmorPreventsInterruption` after `IsDropThroughAllowed`. Both are clear of `:8` (CM-MOVE) and the D3 section (CM-CANCEL).
- `scripts/Combat/StoryEnemyCombatRules.cs`
- `scripts/UI/FloatingDamageNumber.cs` (doc only)
- `scripts/UI/UIPalette.cs` (one colour)
- `scripts/Characters/CharacterFactory.cs` (the body-box names)
- `tests/ContentValidation/InfluenceMeterSourceScanTests.cs`
- `localization/en.csv` and `en.en.translation`

**New partials**
- `scripts/Characters/PlayerController.MovementAbilityGravity.cs`
- `PlayerController.UltimateArmor.cs`
- `PlayerController.StoryFeel.cs`

Each has its `.uid`.

**Declared regions of shared files**
- `PlayerController.cs`:
  - `ApplyGravity` (the parameter and its doc);
  - one call line in `ProcessUsingMovementAbility`;
  - `HasActiveHyperArmor` / `HasActiveHyperArmorAgainst`;
  - `ResolveIncomingHit`'s status line (≈ :1799 → `ApplyHitStatus`) and shake block (≈ :1837);
  - `TryStartEchoStep` (two cost reads and the doc);
  - `GroundStringBoxAnchorYPixels`.
- `EnemyController.cs`: `ResolveIntakeScale`, its call in `OnHurtboxHit`, and `PresentDealtHit`.
- `BossController.cs`: `PresentDealtHit` (the arguments and the doc).

**Outside my ownership**
- `scripts/Combat/StatusController.cs`: one guard line in `StaticChargeStrategy.OnApply`. Without it a hit's StaticCharge stuns through any armour. The alternative was editing `PlayerController.ApplyStun`, which is CM-HIT's in Phase B.

**Test files**
- New: `StoryMovementAbilityGravityTests.cs`, `StoryUltimateArmorTests.cs`, `StoryEchoStepCostTests.cs`.
- Cases added: `StoryEnemyCombatRulesTests.cs`, `StoryEnemyCombatTests.cs`.
- In place: `EnemyControllerTests.cs`, `StoryWallBounceTests.cs`, `StoryFirstSpecialTaxTests.cs`, `StoryCombatantGeometryTests.cs`.

## 3. Decisions (Provisional; each becomes a ledger row)

1. **`PROVISIONAL-MOVEMENT-ABILITY-GRAVITY-SCOPE`: the per-kit table in §1.**
   - Tubman's "first recovery frame" is `RecoveryFramesElapsed == 0`, which matches the sim's one `LeapEnd` settle frame.
   - Leonardo's Daedalus dive (a Story-only perk) suspends gravity. It rewrites velocity every tick, the plan's own criterion; with gravity it would dive at about 668 px/s instead of the authored 620.
   - Movement abilities never fast-fall (`fastFalling: false`).
   - Einstein's Warp float window now starts on his single recovery frame, where `ApplyGravity` first runs. It used to start on the cleanup tick, one tick later.
2. **`OwnerFallSpeedCapNow` is a new member.** The cap must sit between gravity and the move for the glide to sink at exactly 40 px/s. The glide's own post-move clamp let each tick fall at 40 + one gravity step, about 87 px/s. See Deviation 1.
3. **2.4, StaticCharge.** It is still applied to the control slot, without its stun, whenever the hit is armoured against, for every armour source, not only the Ultimate. A hitless StaticCharge (`IStatusEffectTarget.ApplyStatusEffect`) is unchanged, because the decision says "from a hit".
4. **2.4, presentation.** `HasActiveHyperArmor` drives `UpdateHyperArmorPresentation`, so the gold hyper-armour shell (and `EventBus.OnHyperArmorChanged`) now shows for the 30-frame Story Ultimate startup. I kept it: the shell should show whenever the body is armoured.
5. **7.1, scope.** The player-side branch applies to `AttackerIndex >= 0` exactly as the plan states. That covers the hero hitting the Level 13 clone and also the clone hitting the hero. A basic on either side now shakes by the curve instead of authored 0.2 × 12. `ResolvePlayerDealtShake` reads `hit.Launches` (the authored flag), as the mobs do, not the controller's `ResolveHitLaunches`.
6. **7.1, duration.** max(authored, curve) only for a non-Ultimate Special with a positive authored duration. A Special that authors 0 duration keeps the curve, as before.
7. **7.3.** 10.5, unrounded, as planned.
8. **8.1.** Floor 6 and the fold order, as planned (`PROVISIONAL-STORY-BASIC-FLOOR-ORDER`).
9. **8.2.** Colour (0.66, 0.68, 0.74) and scale 0.8, as planned.
10. **8.4.** The names are `CharacterFactory.HeroBodyBoxWidthPixels` and `HeroBodyBoxHeightPixels`.

## 4. Deviations from the plan

1. **Leonardo has a member override, but not the planned one.** The plan says "Leonardo has no override", and also asks for a glide "down at exactly 40 px/s".
   - Kits write velocity after the controller's `MoveAndSlide`. With gravity applied before the move, the old clamp (`vel.Y = Min(vel.Y, 40)` after the move) would let him fall at about 87 px/s.
   - So the boost and the glide have **no** `SuspendsOwnerGravityNow` override, as planned, and the glide caps the descent through the new `OwnerFallSpeedCapNow` instead.
   - The dive suspends gravity (Decision 1).
2. **Lincoln's Rail Charge did not write X only.** The plan's premise was that it did. In fact it wrote `_chargeDirection * _chargeSpeed`, the whole vector, which zeroes Y every tick.
   - With gravity before the move, an airborne charge would only sink at about 26 px/s (one gravity step a tick, weight 1.6).
   - It now writes X only (`DriveCharge`), as the sim's `RailCharge` phase does, and a charge off a ledge falls: 127.7 px during the charge, reaching the 720 px/s terminal.
   - For the Q7 test's 300 ± 2: the charge also started moving one tick late, because velocity was first written on its first Active tick, after that tick's move. So with Q7 alone it would travel 290 px.
   - `OnActive` now writes the charge velocity, and it measures exactly 300.0 px on flat ground and off a ledge.
3. **Mozart does not hover "6 px above" his staff.** The staff's 12 px body is centred 6 px below his feet, so its top is flush with them.
   - Before: he hovered on it, airborne, with zero velocity through the 18 recovery frames.
   - Now: he settles onto it (`IsOnFloor`) on his first recovery frame and falls 0 px.
   - Case 3 asserts that landing for Mozart, and a fall of more than 0.5 px for the other three.
4. **Leonardo's acceptance line.** "Leonardo descends from his first glide frame" is met to within 0.07 px, not literally:
   - glide frame 1: vy −4.2 px/s, a 0.07 px rise;
   - glide frame 2: +16.7 px/s;
   - from glide frame 3: capped at 40.
   - Nothing was changed to force it.
5. **The `systems_card_echo_body` row stayed at `en.csv` :959** under a new `# CM-STORY` comment, instead of moving to a block at the end. This keeps it clear of CM-INPUT's end-of-file block.
6. **§9's skeleton says "(declared +20)" for CM-STORY.** That predates Q7; §0.2 and §3.1 say +21, which is what landed.

## 5. Tests

**New: +21**
- `StoryMovementAbilityGravityTests` (6, real physics frames except case 5):
  - `LeonardosBoostPeaksLowAndHisGlideSinksAtExactlyItsSinkSpeed`
  - `JoansWingsRiseIsUnchanged`
  - `TeslaTubmanCleopatraAndMozartFallDuringRecovery`
  - `LincolnsChargeFallsOnceItLeavesTheFloorAndStillTravelsItsAuthoredDistance`
  - `SuspendsOwnerGravityNowAgreesWithTheSimPhaseByPhase`: Tesla, Tubman, Cleopatra, Mozart and Lincoln against `FighterKitMotion.SuspendsGravity`; the Story-only startup holds are checked against the plan's disposition.
  - `LincolnsChargeStopsAtTheStartOfItsRecovery` (Q7)
- `StoryUltimateArmorTests` (5):
  - `EveryRosterHerosUltimateIsArmouredForExactlyItsStartup` (roster from `CharacterRoster.IDs`)
  - `ABasicStunInStartupDealsDamageButTheSameStunInActiveInterrupts`
  - `AnUltimateClassHitStillInterruptsTheStartup`
  - `AStaticChargeBearingHitDoesNotInterruptAnArmouredCast` (also Rail Charge, plus an unarmoured control)
  - `TheArmourNeverReachesTheUltimateResourcesOrTheSimLoadout`
- `StoryEchoStepCostTests` (3):
  - `TheCampaignHeroEchoStepsAtTenAndAHalfAndIsRefusedJustBelowIt` (10.5 accepted, 10.4 refused with nothing spent)
  - `ABuildWithoutTheStoryScaleStillNeedsTheFullThirty`
  - `TheStoryCostIsTheCrossModeCostTimesTheStoryMeterScale`
- `StoryEnemyCombatRulesTests` +2:
  - `ASpecialsShakeScalesWithDamageWhileUltimatesKeepTheirAuthoredValue` (7.1)
  - `TheStandardBasicFloorLiftsSmallHitsToSixBeforeProrationAndPunish` (8.1, plus 8.2's predicate)
- `StoryEnemyCombatTests` +5:
  - `ALaunchingSpecialShakesByTheDamageCurveOnMobsAndBossesUnderTheAccessibilityScale` (7.1)
  - `AThreeDamageStringHitDealsSixToAStandardButThreeToAnEliteOrATurretShot` (8.1)
  - `EveryHeroKillsANormalStandardInSixBasicHitsOrFewer` (8.1; snapshots and restores `GameManager.CurrentSession`)
  - `AProratedHitFloatsADimSmallerNumberWhileUntaxedHitsStayWhite` (8.2)
  - `APunishedHitShowsThePunishTreatmentEvenWhenItIsProrated` (8.2)

**Rewritten in place (no count change)**
- `StoryEnemyCombatRulesTests.AnAbilitysAuthoredShakeWinsAndBasicsKeepTheDamageCurve` → `AnUltimatesAuthoredShakeWinsAndBasicsKeepTheDamageCurve`. Same assertions, the new `HitOrigin` signature, reworded.
- `StoryEnemyCombatTests.AnIdenticalFinisherWritesTheSameVelocityOnAMobAtFullHPAndAtThreeHP` → `…AtSevenHP`. The 1-damage finisher now lands for 6, so the wounded mob starts at 7 and is left on 1.
- `EnemyControllerTests.NaturallyExpiredStunArmsGetupArmorThatDeniesStunAndKnockbackButNeverDamage`: the expected intake reads `StandardBasicPacingScale(3)`, 3 → 6.
- `StoryWallBounceTests.ASlowLaunchAPlainShoveAFloorSeamAndAPhaserNeverWallBounce`: the four 4-damage intakes 5 → 6, derived through `StandardBasicPacingScale(4)`.
- **The fixture audit (HP headroom, no assertion changed).** These were the 1-damage string fixtures the floor now lifts to 6 each. Totals that sat at 39 of a Normal 40 HP, or past an Easy 28, now have headroom:
  - `StoryEnemyCombatTests`: `CurrentHP = 999` in `AStringPlusAMultiHitSpecial…`, `AFrozenOrDrillInvulnerableMob…` (the frozen and drill mobs) and `BackToBackStringsEachComplete…`;
  - `StoryFirstSpecialTaxTests`: `CurrentHP = 999` in its `CreateEnemy` helper (no case there is about a kill);
  - one stale "Easy-scaled 28 HP" comment reworded.
  - `StoryEnemyCombatReviewTests` and `StoryPunishWindowTests` needed nothing (6-damage hits, or the kill is the point).
- `StoryCombatantGeometryTests.ThePlayerPushboxIsTheUniversalZeroPointSixUnits` gains the 8.4 pins: the hurtbox and the body box against their names, the anchor against the hurtbox height, and hit 1's offset.
- `InfluenceMeterSourceScanTests.TheStoryMeterScaleLivesInStoryCombatRulesAndNeverReachesTheSimOrTheSharedRulebook` forbids `StoryEchoStepMeterCost` under `scripts/FighterSim/` and in `InfluenceMeterRules.cs`.
- Unchanged and green, as the plan expected: `AbilityInterruptionTests.UltimateInterruptedMidChargeStopsFiringAndReleasesSteering` (it already interrupts in Active) and the ten movement suites §2.1.1 named as may-change.

**Shared test files touched:** `MirrorParadoxTests` was not touched (green as is). `StoryEnemyCombatTests` and `StoryEnemyCombatRulesTests` are touched by this workstream only in Phase A.

**Runs.** Every run held `D:/Projects/.ftt-gdunit-lock` and drained `testhost` / `Godot*_console` first.

| Run | Result |
|---|---|
| The new gravity suite + the 10 §2.1.1 movement suites + `AbilityInterruption` | 43/45. Both failures were in the new suite's harness: the first awaited frame came from outside a physics frame, and Joan's tolerance ignored the floor's 0.08 px safe margin. |
| Gravity + armour + `AbilityInterruption` + `MirrorParadox` + `StoryCombatRules` + Tesla / StaticCharge / Conductive | 92/93. The one failure was Mozart's case, which assumed the plan's hover premise (Deviation 3). |
| `--import` (after the `en.csv` edit), then 13 suites incl. `StoryEnemyCombat*`, Echo Step, the scan, Systems Card, the localization gates, Mirror | **138/138** |
| `StoryEnemyCombat*`, `StoryFirstSpecialTax`, `StoryPunishWindow`, `StoryWallBounce`, `EnemyController`, `MirrorParadox`, `StoryFinisherCarry`, `StoryHitConfirm`, `FeelPassStory`, `EncounterMemberBehaviour` (§3.3's pre-full set) | first 180/183 (the three in-place breaks above); after the fixes **219/219** |
| The whole §4.1 CM-STORY list (25 filters) | **258/258** |
| **Full suite** | **`Passed! - Failed: 0, Passed: 3249, Skipped: 0, Total: 3249, Duration: 1 m 30 s`**. 3249 = 3228 + 21, exactly the declared delta. The `[TestCase]` count under `tests/` is 3249. |

**Build.** `dotnet build`: 0 errors, and only the three pre-existing warnings (CS8632, `KLSG_ECS004` on `FighterTuningComponent`, CS0649). No `KLOTHO_DET002`.

**Load check and smoke runs**
- `--headless --quit`: exit 0.
- `Level_02_Orleans.tscn --quit-after 300`, four runs: exit 0 and no script error each time. The leak lines were 8 / 4 three times and 6 / 3 once.
- Control `Level_03_Chicago.tscn`, three runs: exit 0. It printed 8 / 4 once and nothing twice.
- The leak lines are the documented nondeterministic noise (CLAUDE.md), not a regression.

## 6. Measurements

**Movement abilities**, real frames, printed `[CM-STORY]` lines from the Godot log:

| Kit | Result |
|---|---|
| Leonardo | Peak **60.9 px** (≤ 70). 93 glide ticks, every airborne tick from glide frame 3 exactly 40/60 px. Before: the boost rose through the whole 3 s glide (about 1,250 px, the plan's figure). |
| Joan | Rises 252 px in 36 ticks, unchanged. |
| Tesla | Recovery falls 37.3 px in 10 ticks. |
| Tubman | Recovery falls 19.2 px in 8 ticks (the settle frame 0 px). |
| Cleopatra | Recovery falls 114.5 px in 18 ticks. |
| Mozart | Lands on his staff in recovery (0 px). |
| Lincoln, flat ground | Travels **300.0 px**, 0.0 px in recovery. Before: by the tick order, about 290 px of charge plus about 180 px of recovery slide. That is derived, not measured. |
| Lincoln, off a ledge | 300.0 px of travel, falls 127.7 px during the charge, vy 720 px/s at its end. |

**Hits to kill a Normal Chrono-Slasher (40 HP), 8.1.** Each string is a fresh chain; the dealt values are in brackets.

| Hero | Hits | Dealt |
|---|---|---|
| Einstein | 6 | 6/6/10/6/6/6 |
| Joan | 5 | 8/8/10/8/6 |
| Leonardo | 6 | 6/6/9/6/6/7 |
| Lincoln | 5 | 6/9/16/6/3 |
| Cleopatra | 6 | 6/6/8/6/6/8 |
| Tesla | 6 | 6/6/9/6/6/7 |
| Shakespeare | 6 | 6/6/10/6/6/6 |
| Mozart | 6 | 6/6/8/6/6/8 |
| Tubman | 6 | 6/8/10/6/8/2 |

The same scheme at the old flat ×1.25 gives Leonardo, Cleopatra, Tesla and Mozart 7 hits, and the others 5–6. That is derived from the same rounding, not measured.

**Shake (7.1):** the Emancipator's numbers (40, launching, authored 0.2 / 0.15 s) shake 8 px at accessibility scale 1 and 4 px at 0.5, on a mob and on a boss.

## 7. Not delivered / open

1. **Other Story kit motions also start one tick late** (derived from the tick order, the same cause as Deviation 2):
   - Each writes its travel velocity in `_PhysicsProcess` before the phase advances, so the first Active tick moves at the startup's velocity.
   - Tesla's blink, Tubman's leap, Cleopatra's rush and Mozart's glissando also have the last Active write overwritten by `OnRecovery`.
   - Tesla's 180 px blink should therefore cover about 165 px in Story.
   - Only Lincoln's was fixed, because Q7's test required it. I suggest a ledger row, `VERIFY-STORY-KIT-MOTION-FIRST-TICK`. Measure it before changing anything.
2. **Lincoln's Rail Charge startup** (12 frames) still carries his entry velocity with no friction; a running Lincoln slides about 45 px before charging. It is horizontal and outside 1.1(1)/Q7. This is the movement-ability twin of the Special slide that Q1 rules on for Specials.
3. **A hitless StaticCharge** (a zone or `ApplyStatusEffect`) can still stun through armour (Decision 3).
4. **Leonardo's first glide frame** rises 0.07 px (Deviation 4).
5. **The Story HUD and Move List show no Echo Step cost**, so there was nothing to change there. The Calibration Drill copy ("costs 30 Influence") stays correct, because the drill student is unscaled.

## 8. For the closeout

**Design master (`design-godot.md` lines at `e2c655a`; none is a `DOC-MASTER-UNBOUND-BINDING` line)**
- **1.1(1):**
  - `:989–991` (Special Jump Abilities; the glide's descent cap, movement abilities under gravity except the kit-owned phases);
  - `:2581` (§5 Leonardo, Ornithopter Flight: the boost decays, the glide sinks at 40 px/s);
  - `:2884` (§5 Lincoln, Rail Charge: 5 units over 30 frames and it stops; no recovery slide).
- **2.4:**
  - `:1117–1121` (Hyper-Armor spec: a Story Ultimate is armoured for its own Startup by a Story rule, not the flag; an Ultimate-class hit still interrupts);
  - `:1275` (the Ultimate block; Story startup armour).
- **7.1:** `:2398–2403` (Camera Shake System: a non-Ultimate Special shakes by the larger of authored and the damage curve; Ultimates authored; basics the curve).
- **7.3:** `:809`, `:822`, `:1271`, `:1345`. The Story line is 30 × 0.35 = 10.5 for the campaign hero; Fighter, the drills and the clone stay at 30.
- **8.1:** new Story content beside Enemy Stagger Discipline, `:1453` ff.
- **8.2:** `:3368–3372` (Floating Damage Numbers: the dimmed, ×0.8 reduced number; Punish wins).

**Contracts (edit both copies, same hunk)**
- `ABILITY_DATA.md` :50, the Ultimates paragraph: Story Ultimates armoured through their startup. This contract is currently identical between the two copies.
- `COMFORT_SETTINGS.md` :116: "a direct Echo Step still requires … 30 meter" becomes "… the Echo Step cost (30; 10.5 for the Story campaign hero)". This file differs between the copies only on the Unbound lines; :116 is not one of them.

**Ledger: write**
- `FIX-STORY-MOVEMENT-ABILITY-GRAVITY` (MOV-13; cite `VERIFY-WING-DIVE-LEAP` as the intended exception)
- `PROVISIONAL-MOVEMENT-ABILITY-GRAVITY-SCOPE` (the table, Decision 1, Decision 2)
- `FIX-STORY-RAIL-CHARGE-RECOVERY-CARRY` (Q7, closed; include the first-tick fix)
- `USER-STORY-ULTIMATE-ARMOR`, `PROVISIONAL-STORY-ULTIMATE-ARMOR` (Q2; Decisions 3–4)
- `USER-STORY-ABILITY-SHAKE` (Decisions 5–6)
- `USER-STORY-ECHO-STEP-COST`
- `USER-STORY-BASIC-FLOOR`, `PROVISIONAL-STORY-BASIC-FLOOR-ORDER`
- `USER-DIM-REDUCED-NUMBERS`
- `USER-HURTBOX-ONE-VALUE` (the Story half)
- suggested: `VERIFY-STORY-KIT-MOTION-FIRST-TICK` (open item 1)

**Ledger: amend**
- `VERIFY-STORY-MIRROR-HOLD`: the clone's post-contact Story Ultimate now has startup armour.
- `F9-DEALT-HIT-FEEDBACK`: 7.1 and 8.2.
- `PROVISIONAL-STORY-CONFIRM-PRORATION`: 8.2.
- `PROVISIONAL-BASICS-PACING`: 8.1.
- `USER-STORY-METER-SCALE`, `PROVISIONAL-STORY-METER-SCALE-RULES` (5), `PROVISIONAL-STORY-METER-GAIN-SCOPE` (2): 7.3.
- `VERIFY-STORY-HURTBOX-UNIVERSAL`: the anchor is derived, and the body box is named separately.

**AGENTS.md**
- "Package 13 kit changes, in brief": movement abilities under gravity, Leonardo's sinking glide, the Rail Charge stopping at 300 px.
- The Combat "Story enemy combat rules" bullet: S4's floor of 6; dimmed prorated numbers; F9's Special shake rule.
- The ability-lockout / Ultimate bullet: Story Ultimate startup armour.
- The Influence-meter bullet: the Story Echo Step cost of 10.5 and the scan.
- The validation section: +21 = 3249 for this branch; it composes with the other workstreams at merge.

**CLAUDE.md:** nothing.
