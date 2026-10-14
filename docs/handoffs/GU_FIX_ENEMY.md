# GU fix round, FIX-ENEMY: enemy behaviour inside engaged locks (Phase C, 2026-10-13)

Phase C fix round of the 2026-10-12 gameplay update (`docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md`). The six items came from the Phase C bot batch (180 runs, tags `guce*` / `gucn*` / `guch*`), where locks a human must also clear stayed shut. Branch `worktree-wf_c57a521f-e47-1`, based on `c4885a9` (main with Phase B merged).

**Nobody has played any of this.** The evidence is:
- the suites below;
- a full suite;
- smoke runs of Levels 4, 5 and 7;
- 19 single bot runs (tags `fxen1`–`fxen6`).

## 1. What changed, per item

### Item 1: flyers out of reach

**Root cause.** A flyer chases toward the hero's feet. Every hold path then zeroes or decays its vertical speed: the ranged band, the D6c band, the wait ring's settled branch, patrol and return. So a holding flyer keeps whatever altitude it ended its chase at. A drone holds its ranged band (Euclidean distance ≤ 0.85 × range: 306 px for the hologram drone, 352 px for the voltaic drone), so it settles directly over the hero at up to that height. Level 14's drone held 292 px up, over a hero whose single jump is 132 px.

**Fix.** One canonical number, `EncounterLockRules.FlyerHoverCapPixels = 160` (Provisional). An engaged flying member never keeps its feet (the bottom of its hurtbox on every roster body) more than 160 px above the hero's feet in Chase, Patrol, Returning or Idle. That covers:
- waiting on the ring;
- holding its ranged band, or waiting for a ranged permit;
- resting after its turn;
- the stand-off band;
- patrol and return inside the lock.

How it works:
- `EncounterLockRules.FlyerHoverLineY(heroFeetY)` gives the line.
- `EncounterLockRules.FlyerSettleVelocityY(feetY, heroFeetY, vy, maxSpeed, dt)`: above the line, the flyer descends at up to its own chase speed and lands on the line exactly. At or below the line, its own vertical speed stands; the cap never lifts a flyer and never slows a faster descent.
- `EnemyController.SettleEncounterFlyer(dt)` (in `EnemyController.Encounter.cs`) applies it with one hook after the AI state switch in `ProcessMobTick`. Attacks and stuns keep their own hover. A dormant member keeps its post.
- `EncounterHero()` gives the hero: the member's target, else its lock's `Hero`. A dead or respawning hero is skipped.
- Flyers outside locks are unchanged (Provisional scope).

**Second half (bot evidence).** Level 3's voltaic drone did not hold "in the air". It had settled onto the solid lamp gantry `BuildPlatform(1000, 1100, 260)` (top 1092, 308 px up) directly over the hero standing under it, so no settle could take it through. Two new rules cover this:
- `EncounterLockRules.IsFlyerStalledOutOfReach(flying, inFlight, moved, feetY, heroFeetY)` counts such a flyer as stalled for the watchdog, ranged or not. It is a flyer held above its hover line, moving under 4 px, and not in a knockback flight. Before, a ranged mob holding still was always exempt.
- `EncounterLock` keeps `_watchdogHeroFloorFeetY`, the hero's feet the last time it stood on a floor. So a hero's jump cannot lift the line over the flyer and reset its count.
- After `StallFrames` (600) the watchdog puts the flyer down beside the hero (item 3b's relocation).

### Item 2: flyer containment

**Root cause, found in `L13_joan_play_hard_guch0`.** Phantom 2 (the one spawned at x 2380) took exactly 6 damage, all booked under the bot's `block` action. That is Joan's throw (the bot pressed Attack while blocking: the grab chord), around frame 3975. `ProcessThrownFlight` ends a throw only on floor contact. A flyer has no gravity (`ApplyGravity` returns for flyers), and its throw stun never ticks during the flight. So the thrown phantom kept the forward throw's velocity for good: x was pinned by the area clamp, and it rose at a constant 79 px/s. That figure is exactly `-0.6 × 3.5 / (1 + 0.6) × 60`, the forward throw's vertical speed on a weight-0.6 body. It rose from y 1899 to -3455.

Two other suspects were ruled out:
- the `rift_step` engage blink keeps the vacated y (`EnemyAbilityExecutor.Teleport`);
- the chase heads down toward the hero's feet.

**Fix, at the cause** (`EnemyController.ProcessThrownFlight`, for every flyer, not only lock members): a thrown flyer loses its speed to the existing `StoryEnemyCombatRules.StunnedAirDragPixelsPerSecondSquared` (600 px/s², `Velocity.MoveToward(Vector2.Zero, …)`). Its flight ends when it stops, or on floor contact as before. Its throw stun then runs out in the hover, like any stunned flyer's. The Level 13 throw now stops after about 0.55 s, about 22 px higher.

**Fix, the band.** `ApplyEncounterBounds` (which runs after every move, including thrown flights) now also stops a flyer's body at the top of its lock's camera rect:
- `EncounterLockRules.ClampFlyerFeetY(feetY, viewTopY, headHeight)`;
- the camera rect top is read once at `JoinEncounter` from the owning `EncounterLock.CameraBounds` (`_encounterViewTopY`);
- upward speed is zeroed on a clamp.

The bottom of the band is the existing fall net (240 px under the area), unchanged. The rule is stated once: the frame the lock holds the camera to. The settle (item 1) then brings the flyer down from that edge.

### Item 3: ground members posted on ledges

**(a) The way down.** `EnemyController.Encounter.cs`, hooks in `ProcessChase` and `HoldOnWaitRing`.

`EncounterSeeksWayDown(toTarget)` holds for an engaged, not dormant, ground member whose next attack needs a melee slot, when its target's feet are more than `StoryEnemyCombatRules.LedgeProbeDepthPixels` (64) below its own. The test is `EncounterLockRules.SeeksWayDown(drop)`. While it holds:
- **No hold over the hero.** The member never holds its stand-off band over the hero; `_standOffEngaged` is cleared.
- **Stepping off.** At a ledge, `MayStepOffLedgeInEncounter(direction, toTarget)` lets it walk off when all of these hold:
  - it is on a floor;
  - there is no wall ahead (its own `IsOnWall` / `HasWallAhead`);
  - the step's landing keeps its body inside the area (`EncounterLockRules.StepOffLandsInArea`);
  - the chase's own edge probe (`HasFloorAhead`) finds floor within the relocation rule's `RelocationRules.FloorSearchDepthPixels` (480 px: a drop, never a void). No second probe was added.
- **Choosing the way.** `ResolveEncounterWayDownSign` latches the way (`_encounterWayDown`): toward the target when it stands beyond the overhead hold's band (`ChaseHoldUnderTargetPixels`, 24), else the way the member faces. Without the latch, a member over a hero standing under its own ledge turned back each time it passed over the hero (reproduced: it oscillated at ±25 px).
- **Dead ends.** `TurnAtEncounterDeadEnd` flips the latch at a wall, or at a drop with no floor within 480 px.

Scope:
- Ranged members keep their posts; shooting from one is their job.
- Waiters on the ring step off the same way, toward the hero's floor.
- Outside a lock, P4 is unchanged.

**(b) The watchdog** (`EncounterLock.TickWatchdog` and its new helper `RelocateStalled`).

`EncounterLockRules.RelocatesBesideHero(distanceToAnchor, anchorFeetGapToHero)` decides where a stalled member goes. It is put down beside the hero when either holds:
- it stands within `StallAnchorTolerancePixels` (16, Provisional) of its anchor, i.e. it is stalled on the post itself;
- its anchor's feet stand more than `StallVerticalGapPixels` (200) above the hero's, i.e. the anchor is a post over the hero.

Otherwise it goes back to its anchor, as before. That includes an anchor below a hero standing up on a ledge: the hero can come down to it.

`EnemyController.RelocateBesideEncounterHero(hero, reason)` does the relocation:
- **Where:** the member's own side of the hero first, at the engagement director's rank-0 wait-ring gap for the difficulty (`StoryEngagementRules.WaitGapForRank(0, d)`: 200 / 170 / 150 on Easy / Normal / Hard), inside the area.
- **Validation:** `SafeRelocation.Resolve` sweeps the member's box out from the hero's feet (so no wall or closed barrier is crossed) and needs a floor under a ground member.
- **Refusals:** a side whose landing has no support (`HeldAtOrigin` / `OriginUnsupported`) or ends inside pushbox contact is refused, and the other side is tried.
- **Fallback:** if neither side takes the member, it goes to its anchor when it is not already on it. It is never killed. Each move is logged as `[encounter-lock] … put down beside the hero at (x,y)`.

### Item 4 (investigation): the Level 5 "staircase" stall

**Root cause, confirmed in real frames on the real scene.** Every Level 5 listing ramp was authored by centre, width and angle (12°, 12°, 13°, 13°, 16°), and none met its decks:

| Ramp | Riser under the upper deck's west face | Lip (rotated corner standing proud of a deck) |
|---|---|---|
| 1 (to D1) | 18.7 px | 1.6 px on D1 at 1195.6 |
| 2 (to D2) | 18.7 px | 1.6 px on D2 at 2395.6 |
| 3 (to E1) | 26.9 px | 0.5 px on D2 at 4285.6 |
| 4 (to E2) | 38.5 px | 0.5 px on E1 at 5235.6 |
| 5 (to the boat deck) | 41.3 px | 1.1 px on E2 at 7159.7 |

A riser stops every walker: the hero needs a jump, and a mob never gets up. A lip stops a mob walking across it.

- **The bot's hero** stood "Running" at x 1080: its 40 px body pressed against D1's face, 19 px above the ramp's top.
- **The slot-holding slasher** stood at 1219. Its left edge (1196) was against ramp 1's rotated corner, 1.6 px proud of D1. The chase's P4 step check reads that face as a wall (`IsOnWall` with an opposing normal), so the slasher stopped.

So it was held by the geometry, not by the D6c band or the director:
- the band only holds within about 60–90 px, and the gap was 139 px;
- the director had given it Easy's only slot (`slots=1`).

A real-frames probe on the old geometry (a temporary test, removed) confirmed that a chasing slasher stopped going up every one of the five ramps: at the riser (ramps 1 and 2) or at the low-end lip (ramps 3–5).

**Fix** (`Level05Controller`). The ramps are now authored by their joints, in `public static readonly (Vector2 Low, Vector2 High)[] ListingRampJoints`:
- **Low joint:** where the old slope left the lower deck (800, 2000, 4280, 5230, 7150).
- **High joint:** the upper deck's west top corner. New constants `DeckD1WestX` 1100, `DeckD2WestX` 2300, `DeckE1WestX` 4600, `DeckE2WestX` 5500, and the existing `BoatDeckWestX`. `BuildFloor` and `HullStepDryFromX` now read them, one value each.
- **`BuildListingRamp(Vector2 lowJoint, Vector2 highJoint)`** lays the slab's top exactly through both joints. It runs on `RampLowJointOverlapPixels` (16) past the low joint, buried under the lower deck. The high end stops at the corner, because running on would rise above that deck.
- **Footprints and slopes:** the ramps keep their footprints, so no spawn moved. They steepen to 14.9°, 14.9°, 17.4°, 20.3° and 21.8°, all well inside the 45° floor angle.

Other checks:
- Spawn (4300, 1120) now falls 14 px onto ramp 3. It used to stand 16 px over it.
- `EncounterLockContentTests`, `ArenaGeometryContentTests` and `Level05ContentTests` pass unchanged.

Bot checks (`fxen3`, `fxen6`):

| Run | Before (`guc*`) | Now |
|---|---|---|
| Joan Easy | stuck at the staircase | `level_complete` |
| Joan Normal | stuck at the staircase | `level_complete` |
| Lincoln Hard | stuck at the staircase | all four locks clear; dies at the boss |
| Joan Hard, seed 1 | stuck at the staircase | three locks clear; dies in the boat deck lock |

### Item 5 (investigation): the Level 6 Hard legionnaire that took no damage

**Root cause.** It was not a shield, frontal reduction, dormant ward or arena ward. The vault floor's **basalt boulder** stands between them: the winch's west-pan weight, a `WeightedObject` `RigidBody2D` circle of radius 28 (56 px tall) on the Environment layer at (3900, 872). It is the scene's `RockfallPuzzle/BasaltBoulder`.

In every Hard run:
- The bot stands at x 3852, its right edge (3872) touching the boulder's west face, "Running" with zero velocity.
- The vault's east members (the legionnaire from 5150, the slasher from 4620) stand at x 3955, against the boulder's east face.
- **Joan:** her swings at them are booked `w` with `gmin 0` (the swing log's "in reach but no contact" class). That is the M1 player-melee line-of-sight rule (`PROVISIONAL-PLAYER-MELEE-LINE-OF-SIGHT`), refusing every basic through the boulder. The 62 damage the legionnaire did take came from Specials and `approach` actions before the stall.
- **Lincoln:** he never swings; he presses "approach" for 120 s.
- **The mobs** cannot come round: ground mobs never jump, and P4 stops them at the wall.

A human jumps a 56 px rock (every hero's single jump is at least 132 px), lands beside them and fights. So this is a **bot gap**, not a game bug. The nav graph does not route the `play` bot over a movable weight in its way. No game code was changed for it.

Two Level 6 notes outside this item:
- Easy's vault stall in the same batch (no living member, "0 remaining") is the required winch puzzle, which belongs to FIX-OBJECTIVE / FIX-LEVEL.
- `Level06Controller.cs` was not touched.

### Item 6 (investigation): Level 13's melee attackers over the slot count

**Method.** I ran instrumented L13 `play` runs: a temporary diagnostic tag on every attack entry point, and a per-frame dump of the attackers in each over-slot frame, both reverted afterwards.
- **Easy:** 39 over-slot frames, every one a slot holder (`chase`) plus a `chrono_guard_elite` waiter attacking through the `pressure` path.
- **Normal:** 49 frames: two holders (a Rift Phantom and the guard elite, one per side) plus an `unbound_eraser` waiter on the `pressure` path.
- **Hard:** none.

**Root cause.** The extra attacker is always the **V7.4 pressure exit**: a waiter struck by the hero (Joan's area Specials reach the ring), whose stun ends with the hero inside its reach, answers at once. Plan §2.6 deliberately never gates `TryPressureExitAttack` (retaliation stays V7.4-legal and fires only in reach), so this is the deliberate exception. Level 13 is where it shows because its elites stand close to the slot holder and answer from range:
- the guard elite's pressure exit selects its `blink_rush` (a ChargeDash elite ability), judged against its authored attack range, and dashes in from the ring;
- the Eraser pair is there too. No slot was ever exceeded (`frames_holders_over_slots` 0).

**Changed:** the telemetry now states the exception. Every gated melee commit holds a slot, so a melee-class attacker near the hero that holds none is retaliating.
- `EngagementFrameSample.MeleeRetaliatingNear` (new).
- `MobEngagementTracker.MaxMeleeRetaliatingNear` (new).
- `MobEngagementTracker.FramesGatedMeleeOverSlots`: frames where melee attackers less retaliating ones exceed the slots. This is the turn-taking invariant; it should be 0.
- The report's `mob_engagement` gains `melee_retaliating_near_max` and `frames_gated_melee_over_slots`.
- `PLAYTEST_RESULT`'s `encounters` gains `gated_over_slots_f`.
- `compare_runs.py`'s mob-engagement table gains `gated_over_f`.
- `frames_melee_over_slots` keeps its meaning, so batches stay comparable.
- A real-frames case pins the exception (below).
- `fxen6`: L13 Joan Easy `level_complete` with `over=0 gated_over=0` on that seed.

No game behaviour changed for item 6.

## 2. Files

Changed:
- `scripts/Environment/EncounterLockRules.cs`:
  - new constants `StallAnchorTolerancePixels`, `FlyerHoverCapPixels`;
  - new rules `RelocatesBesideHero`, `IsFlyerStalledOutOfReach`, `FlyerHoverLineY`, `FlyerSettleVelocityY`, `ClampFlyerFeetY`, `SeeksWayDown`, `StepOffLandsInArea`;
  - existing members untouched.
- `scripts/Environment/EncounterLock.cs`, **only `TickWatchdog` and its helpers**: the field `_watchdogHeroFloorFeetY`, the flyer stall sample, and `RelocateStalled`.
- `scripts/Enemies/EnemyController.Encounter.cs`:
  - the view top and the way-down latch (set and reset with membership and dormancy);
  - the flyer top clamp in `ApplyEncounterBounds`;
  - `RelocateBesideEncounterHero`, `SettleEncounterFlyer`, `EncounterHero`, `EncounterSeeksWayDown`, `MayStepOffLedgeInEncounter`, `ResolveEncounterWayDownSign`, `TurnAtEncounterDeadEnd`.
- `scripts/Enemies/EnemyController.cs`, one-line hooks:
  - the settle after the state switch;
  - in `ProcessChase`: the way-down sign, the band override, the step-off and the dead-end turn;
  - the thrown-flyer drag and flight end in `ProcessThrownFlight`.
- `scripts/Enemies/EnemyController.Engagement.cs`: one condition in `HoldOnWaitRing` (the waiter's step-off).
- `scripts/Environment/Level05Controller.cs`: the ramp joints and builder, and the deck west-x constants.
- `scripts/Diagnostics/PlaytestEncounterStats.cs`, `scripts/Diagnostics/PlaytestTelemetry.cs`, `tools/playtest/compare_runs.py`: the item 6 telemetry split. These files are GU-BOT's; the edits are additive.

New:
- `tests/Unit/EncounterMemberRulesTests.cs` (pure C#)
- `tests/Unit/EncounterMemberBehaviourTests.cs`
- `tests/ContentValidation/Level05ListingRampTests.cs`
- this handoff
- the three `.uid` sidecars

Not touched: the ledger, AGENTS.md, CLAUDE.md, `Level06Controller.cs`, `en.csv` (no new visible text), scenes and resources.

## 3. Tests: +17 (3180 → 3197); no existing case rewritten

**`EncounterMemberRulesTests`** (pure C#, no `[RequireGodotRuntime]`), **6**:
- `AFlyerAboveTheHoverLineDescendsToItAndOneAtOrBelowItKeepsItsOwnVerticalSpeed`
- `AFlyersBodyNeverRisesAboveTheTopOfItsLocksCameraRect`
- `AMeleeGroundMemberSeeksAWayDownOnlyToATargetBelowTheChasesLedgeProbe`
- `TheWatchdogNeverSendsAMemberBackToThePostItIsStalledOn`
- `AFlyerHeldAboveTheHoverLineThatCannotMoveIsStalledEvenWhenItShoots`
- `TheCrowdTrackerCountsRetaliationApartFromGatedMeleeAttacks`

**`EncounterMemberBehaviourTests`** (real frames), **9**:
- `EveryHeroReachesAFlyerHeldAtTheHoverCap`. The cap against the shipped data:
  - all nine heroes × the three roster flyers;
  - single jump and full jump budget, integrated through `StoryAirPhysics.MultiJumpRise` from each `CharacterData`;
  - the box from `PlayerController.ResolveAirAttackBoxPixels` with each hero's string profile;
  - the flyer's hurtbox read from its tier scene.

  The box rides the jump from the floor to the apex, so it overlaps the flyer's hurtbox at some moment of the rise exactly when its apex top stands above the hurtbox's bottom. The weakest case is Lincoln's single jump: 132.6 + 71.8 = 204.4 px against a 160 px hurtbox bottom, a 44 px margin.
- `AnEngagedDroneHoldingItsBandOverTheHeroSettlesWithinTheHoverCapAndAFreeOneDoesNot` (Level 14's case, plus a non-member control that keeps 292 px)
- `ADroneRestingOnAGantryOverTheHeroIsPutDownBesideItByTheWatchdog` (Level 3's case)
- `AThrownFlyerLosesItsSpeedAndHoversInsteadOfRisingForever` (Level 13's throw, (320, -79))
- `AFlyingMemberIsHeldBelowTheTopOfItsLocksView` (written to y -3455, then it settles back down)
- `AnEngagedSlasherOnALedgeStepsDownToTheHeroBelowAndAFreeOneHoldsIt` (Level 7's quay platform, plus a non-member control that keeps its ledge)
- `AnEngagedSlasherStraightAboveTheHeroTurnsAtADeadEndAndStepsDownTheOtherWay` (Level 10's gallery tier with a wall at its west end)
- `TheWatchdogPutsAMemberStalledOnItsPostDownBesideTheHeroNotBackOnThePost`
- `AStruckWaiterRetaliatesWithoutASlotWhileTheHolderKeepsTheOnlyOne`. Item 6's premise on Easy: the struck waiter's pressure exit attacks with role `Waiting` and a melee in-flight class, while the director's holder count stays at 1 every frame. It pins existing behaviour, so it passes with or without this change.

**`Level05ListingRampTests`** (the real scene; locks built Cleared through the seam), **2**:
- `TheFloorsTopIsContinuousAcrossEveryListingRamp`. A ray every pixel across each joint: a 1 px step may climb by the slope plus 0.25 px, never more, and never drop.
- `AHeroAndASlasherWalkUpAndDownEveryListingRampWithoutJumping`:
  - the hero walks each ramp up and down on input with Jump never pressed;
  - a chasing slasher crosses each ramp down, starting 120 px east of the high joint (past the old proud corners), then up.

**Revert check.** Every behaviour fix was undone at once:
- the settle;
- the thrown-flyer drag;
- the view clamp;
- the step-off;
- `RelocateStalled` back to anchor-only;
- the old ramps.

Exactly 8 cases then failed, each on its own assertion:
- the drone held at 608 above the 740 line;
- the throw was still in flight after 2 s;
- the body top was at -3527;
- the ledge member stayed at 760;
- the tier member stayed on the tier;
- the watchdog returned the member to its post;
- the ramp floor stepped 18.96 px at x 1100;
- the hero stopped at 1080.

The flyer stall rule was reverted on its own afterwards: the gantry case failed (0 relocations). Every fix was then restored.

The cases that pass either way:
- the six rules cases (pure functions);
- the reach-versus-data pin (constant against data);
- the retaliation case (it pins existing behaviour).

**Runs** (each holding `D:/Projects/.ftt-gdunit-lock`, `GODOT_BIN` set, after a warm-cache `--import`):
- the three new suites: 15/15, then 17/17 after the flyer-stall case was added;
- `EncounterLock*` with the new suites: 74/74;
- 537 cases across 29 affected suite families:
  - lock, director, enemy combat, Levels 3/5/7/10/13/14, arena geometry;
  - relocation, finisher carry, wall bounce, Eraser debut, Captain Smith, post-boss chain;
  - telemetry, drops, baselines, secret caches, fonts, grab, Mirror, kit pulls, fall net.

  536 passed. The one failure was `InputBindingSchemaTests.TheDirectGrabAndEchoStepActionsExistAndAreBindable`, which the `Grab` filter matched. That is CLAUDE.md's known order effect in broad targeted runs; it passes in the full suite.
- **Full suite: `Passed! - Failed: 0, Passed: 3197, Skipped: 0, Total: 3197` (1 m 18 s) = 3180 + 17.** The `[TestCase]` attribute count under `tests/` is 3197.
- `dotnet build`: 0 errors, the three pre-existing warnings only (CS8632, CS0649, KLSG_ECS004), no `KLOTHO_DET002`.
- `--headless --quit`: exit 0.
- `Level_05_Titanic.tscn --quit-after 300`: exit 0, run twice. Both runs printed 6 leaked ObjectDB instances and 3 resources in use, the same as the control smoke runs of Levels 4 and 7 (CLAUDE.md's at-exit noise).

## 4. Bot runs (single runs from this worktree, `play` bot)

Instrumented, then reverted (item 6):

| Tag | Level | Hero | Difficulty | Result |
|---|---|---|---|---|
| `fxen1` | 13 | Joan | Easy | `level_complete`; 39 over-slot frames, all `pressure` |
| `fxen2` | 13 | Joan | Normal | 49 over-slot frames, `pressure` |
| `fxen2` | 13 | Joan | Hard | 0 over-slot frames |

With the fixes:

| Tag | Level | Hero | Difficulty | Seed | End | Notes |
|---|---|---|---|---|---|---|
| `fxen3` | 5 | Joan | Easy | 0 | `level_complete` (100.3 s) | |
| `fxen3` | 5 | Joan | Normal | 0 | `level_complete` (145.7 s) | |
| `fxen3` | 3 | Lincoln | Easy | 0 | `stuck_lock:…midway` | before the flyer stall rule |
| `fxen3` | 14 | Lincoln | Normal | 1 | `collapse(Death)` → GameOver (679 s) | past the fabrication spine |
| `fxen4` | 3 | Lincoln | Easy | 0 | `level_complete` (210.4 s) | the watchdog put the gantry drone, and the Electricity Building's drone, down beside the hero |
| `fxen4` | 3 | Joan | Normal | 0 | `collapse(Death)` at the boss (208.3 s) | all three locks cleared. This run predates the final `RelocatesBesideHero` rule: its midway slasher was put on the gantry beside the hero. Under the final rule (anchor below a hero on a ledge → back to the anchor) that move would go to the anchor. |
| `fxen5` | 13 | Joan | Hard | 0 | `collapse(Death)` → GameOver (107 s) | the Threshold cleared |
| `fxen5` | 7 | Joan | Normal | 0 | `stuck_lock:…quay` | the quay slasher came down and died; the posted deckhand is left (§6) |
| `fxen5` | 10 | Joan | Normal | 0 | `collapse(Death)` at the Tragedy King | all three locks cleared, the galleries included |
| `fxen5` | 10 | Lincoln | Easy | 0 | `timeout` at the boss (600 s) | all three locks cleared |
| `fxen5` | 7 | Lincoln | Easy | 0 | `stuck_lock:…boarding` | the skiff channel and mortars; not this workstream's |
| `fxen6` | 5 | Lincoln | Hard | 0 | `collapse(Death)` at the boss | all four locks cleared |
| `fxen6` | 5 | Joan | Hard | 1 | `collapse(Death)` in the boat deck lock | |
| `fxen6` | 14 | Lincoln | Normal | 0 | `collapse(Death)` → GameOver (642 s) | all locks cleared |
| `fxen6` | 13 | Joan | Easy | 0 | `level_complete` | `gated_over_slots_f` 0 |

## 5. Changes to the GU_E authoring rules

- **A flyer's spawn altitude no longer decides whether it can be reached.** Once engaged it settles to 160 px over the hero's feet, and the watchdog backs that up when it rests on a platform. Still keep flyer spawns out of solid geometry. Do not put a solid platform right over the place the hero fights a flyer from, or the flyer waits 10 s on it.
- **A melee ground member now walks off a ledge toward a hero below it**, when there is floor within 480 px inside its area. A post above the route is no longer a soft-lock for melee members, but the content rule ("no posted-on-a-ledge-only mobs") still stands for ranged members, which keep their posts.
- **The watchdog's last resort is now "beside the hero"** for a member stalled on its anchor, or whose anchor is a post over the hero. The guide's pitfall "if the anchor itself is unreachable, fix the content" still holds for ranged posts.
- **Ramps:** a rotated slab must meet its decks at its joints. A riser of any height stops every mob, and a corner even a pixel proud of a deck stops a mob crossing it. Author ramps by their joints (Level 5's `BuildListingRamp(Vector2, Vector2)` is the pattern), and walk a hero and a mob across them in real frames.

## 6. Not fixed / open

- **Level 7 quay (bot gap).** The ranged `laser_pistol_deckhand` posted on the launch yard (spawn 3450, 642; yard top 672, 228 px up) holds its band there by design. A human climbs to it: deck → the 3200 platform (148 px) → the yard (80 px). The bot only air-strikes from below, through the solid yard. Either the bot climbs, or GU-L4 / FIX-LEVEL moves the marksman to the deck.
- **Level 6 vault, Hard (bot gap, item 5).** The bot never jumps the 56 px basalt boulder. Fix candidate in `PlaytestNav` / `PlaytestBots`: treat a `movable_weight` in the path as a jump edge.
- **Level 7's boss-arena listing ramps** (`Level07Controller.BuildListingRamp`, 10.5°, at 9300 and 10100) were authored the same way. Each leaves a 5.2 px riser under its upper deck's east end (x 9220 and 10020) for anyone walking west uphill. This is outside my ownership and was not changed. It is the boss arena, not a lock.
- **Level 7 boarding lock** (Lincoln Easy): the skiff channel and the mortars. Not investigated.
- **Scope choices left open** (Provisional): flyers outside locks are unchanged; ranged ground members keep posts; a member stalled 10 s is moved (a visible teleport).
- **Seed variance:** the bot runs are single samples. Only the Phase C batch re-run can say how many stuck locks are left.

## 7. Candidate `PROVISIONAL-*` rows for the closeout

- **`PROVISIONAL-ENCOUNTER-FLYER-HOVER-CAP`:**
  - `EncounterLockRules.FlyerHoverCapPixels` = 160;
  - engaged flying members only, in Chase / Patrol / Returning / Idle;
  - descent at the flyer's own chase speed;
  - attacks, stuns and dormancy keep their own hover;
  - flyers outside locks unchanged;
  - weakest margin: Lincoln's single-jump air strike, 44 px.
- **`PROVISIONAL-ENCOUNTER-FLYER-VIEW-TOP`:** a member flyer's body stays under the top of its lock's camera rect; the fall net remains the bottom.
- **`PROVISIONAL-THROWN-FLYER-DRAG`:**
  - a thrown flyer decelerates at `StunnedAirDragPixelsPerSecondSquared` (600 px/s²);
  - its flight ends at rest, then the throw stun runs in the hover;
  - applies to every flyer, member or not.
- **`PROVISIONAL-ENCOUNTER-MEMBER-STEP-OFF`:**
  - engaged melee ground members whose target is more than 64 px below;
  - a step-off needs floor within `RelocationRules.FloorSearchDepthPixels` (480), inside the area, and no wall;
  - the way-down latch (toward a target more than 24 px aside, else facing) and the dead-end turn;
  - waiters on the ring too;
  - ranged members and non-members unchanged;
  - the stand-off band is suspended while seeking a way down.
- **`PROVISIONAL-ENCOUNTER-STALL-DESTINATION`:**
  - `StallAnchorTolerancePixels` = 16;
  - beside the hero (rank-0 ring gap for the difficulty, own side first, the relocation rule validating) when on the anchor or when the anchor is more than 200 px above the hero, else the anchor;
  - fallback to the anchor; never a kill.
- **`PROVISIONAL-ENCOUNTER-FLYER-STALL`:**
  - a flyer above its hover line, still (under 4 px per tick) and not in a knockback flight, counts as stalled whatever its attack;
  - the line is taken from the hero's last floor-standing feet.
- **`PROVISIONAL-L05-RAMP-JOINTS`:**
  - Level 5's ramps authored by joints: low 800 / 2000 / 4280 / 5230 / 7150, high at the deck corners;
  - 16 px of buried overlap at the low end;
  - slopes now 14.9° / 14.9° / 17.4° / 20.3° / 21.8° (were 12° / 12° / 13° / 13° / 16°).
- **For `USER-ENEMY-TURN-TAKING`'s acceptance:** read `frames_gated_melee_over_slots` (the invariant; should be 0). `frames_melee_over_slots` includes the deliberate V7.4 retaliation exception.

## 8. For the closeout

- **AGENTS.md, the Story enemy combat rules bullet:** an engaged lock member's reach rules (hover cap, view top, step-off, watchdog destination), and the thrown-flyer drag.
- **AGENTS.md, Level 5 facts:** the ramps by joints.
- **AGENTS.md / CLAUDE.md, telemetry notes:** `frames_gated_melee_over_slots`, `melee_retaliating_near_max`, `gated_over_slots_f`, `gated_over_f`.
- **Baseline:** +17.
- **Ledger amendments:**
  - `PROVISIONAL-ENCOUNTER-LOCK-RULES`: the watchdog's destination and the flyer stall sample.
  - `PROVISIONAL-ENEMY-CHASE-LEASH`: P4's ledge stop is lifted for engaged melee members seeking a way down.
  - `S1-STUNNED-KNOCKBACK-FRICTION`: the thrown-flyer drag reuses the stunned air drag.
- **Design-master lines owed:** "Enemy Spawn Rules" / the flyer behaviour (flyers in a cleared-area fight hover within jump reach), and the ground mob's no-jump rule (they may step down off ledges inside a lock).
