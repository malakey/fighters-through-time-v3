# GU-F handoff — the Story engagement director (enemy turn-taking and spacing)

Gameplay update 2026-10-12, request 6 (plan `docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md` §2.6, §4 GU-F). Story only. Branch `worktree-wf_21dd1c67-9be-6`, fast-forwarded to the plan commit `b8f5343` before any edit.

The user's words: "I want the enemies to maintain more separation, especially on normal mode. They shouldn't just make a beeline for the character. They should wait their turn to come and attack the character." Scope ruling: Easy 1 / Normal 2 / Hard 3 attackers at once.

**Nobody has played this build.** Every claim below rests on headless GdUnit cases in real physics frames, a full suite and two single bot runs.

## What a player will see

- **Turn-taking.** When several mobs aggro, only a few walk in and swing. On Normal that is one mob from each side of the hero (two at once). On Easy it is one mob at a time. On Hard it is three at once, at most two on one side.
- **The wait ring.** Every other melee mob stops on a wait ring and faces the hero:
  - On Normal the nearest waiter stands 170 px from the hero, pivot to pivot. That is just outside every hero's hit-1 reach, so the hero has to step in.
  - Each further waiter on the same side stands 64 px farther out, so waiters never share a spot.
  - On Easy the ring starts at 200 px; on Hard at 150 px.
- **Following and backing off.** As the hero moves, waiters follow at 0.8× their own speed and back off at 0.6× when the hero walks toward them. Neither speed ever exceeds 0.9× the hero's run speed, so the hero can always catch a waiter. Waiters stop at ledges and walls in either direction.
- **Rotation.** When an attacker's swing ends and a rested waiter could take its place, the attacker steps back out to the ring and the next mob comes in:
  - Elites come first.
  - Then the waiter that has waited longest, which makes the rotation round-robin.
  - The mob that just attacked rests before it can be handed a turn again: 1.5 s on Easy, 0.75 s on Normal, 1/3 s on Hard.
- **Stale turns.** An attacker that cannot reach or commit for 2 s while someone eligible is waiting gives up its turn. Example: the hero is on a ledge above it.
- **Shooters.** At most 1 / 2 / 3 ranged attacks are in flight at once (Easy / Normal / Hard). The shooter refused longest fires next. A shooter whose widened range would park it off the closer camera's screen now holds no farther than 560 px. Today that is only the infrared border sentry, which used to hold at 689 px.
- **Retaliation is unchanged.** Hitting a waiter still lets it hit back when its stun ends, if it is in reach (V7.4 pressure exit). An elite's Armored Recovery answer still comes at once. A waiter never starts a fight on its own, even with the hero standing next to it.
- **Squeezed sides.** If a wall, ledge or encounter barrier leaves no room for a waiter's place on its side (rank 1 or more), and the other side is less crowded, that waiter crosses to the other side. It walks through the hero with its own pushbox switched off for the crossing. On open floor nobody crosses.
- **The Mirror clone.** No mob or boss ever targets the Level 13 Mirror clone (`IsStoryHostile`) any more.
- **A lone mob** behaves exactly as before, frame for frame.

## 1. Shipped

### `scripts/Combat/StoryEngagementRules.cs` (new; `FTT.Combat`, pure C#, no Godot types, difficulty always a parameter)

Enums:
- `EngagementClass { Free, MeleeSlot, RangedPermit }`
- `EngagementRole { None, Waiting, Slot }`

The difficulty table (Easy / Normal / Hard):

| Member | Easy | Normal | Hard | Status |
|---|---|---|---|---|
| `MeleeSlotsTotal(d)` | 1 | 2 | 3 | **User** |
| `MeleeSlotsPerSide(d)` | 1 | 1 | 2 | Provisional |
| `RangedPermits(d)` | 1 | 2 | 3 | Provisional |
| `WaitRingRankZeroGapPixels(d)` (px) | 200 | 170 | 150 | Provisional |
| `RestFramesAfterHandoff(d)` (own ticks) | 90 | 45 | 20 | Provisional |

Constants (all Provisional):

| Constant | Value |
|---|---|
| `WaitRingRankSpacingPixels` | 64 |
| `WaitRingTolerancePixels` | 12 |
| `StaleSlotFrames` | 120 |
| `RangedHoldMaxPixels` | 560 (1920 / 1.5 / 2 − 80) |
| `WaiterApproachSpeedFraction` | 0.8 |
| `WaiterBackOffSpeedFraction` | 0.6 |
| `WaiterHeroSpeedCapFraction` | 0.9 |
| `PriorityGapBucketPixels` | 32 |
| `CrossingCooldownFrames` | 60 |

Functions:
- `ClassOf(EnemyAbilityArchetype, bool isEngageBlink)`:
  - MeleeStrike, ChargeDash, AreaPulse and the engage-blink Teleport → `MeleeSlot`.
  - Projectile, Shockwave, PersistentFieldAtTarget and SiphonTether → `RangedPermit`.
  - ShieldBubble, SummonMinions and the reactive Teleport → `Free`.
- `WaitGapForRank(rank, d)` = rank-0 gap + max(0, rank) × 64.
- `WaiterSpeedPixels(mobSpeed, heroRunSpeed, towardTarget)` = `min(mobSpeed × 0.8 or 0.6, 0.9 × heroRunSpeed)`.
- `CappedHoldEngagePixels(engage)` = `min(engage, 560)`.
- `IsStale(frames)`.
- `ShouldCrossToOtherSide(rank, ownPositionCutOff, ownPresence, otherPresence)` = rank ≥ 1 && cut off && other < own.
- `RangedWaiterOutranks(otherRefused, otherId, ownRefused, ownId)`.
- `SideHasFreeSlot(sideHolders, totalHolders, d)` and `CanTakeFreedSlot(waiterSide, holderSide, holdersLeft, holdersRight, d)`.
- `readonly struct Candidate(InstanceId, IsElite, Rested, Side, WaitedFrames, GapPixels)`.
- `ComparePriority(in a, in b, holdersLeft, holdersRight)`. The order is: elite → rested → the side with fewer holders → more waited frames → the 32 px gap bucket → instance id.
- `SelectGrants(List<Candidate>, holdersLeft, holdersRight, d, List<ulong> granted)`. It is an allocation-free in-place insertion sort, then grants under the total and per-side caps.

### `scripts/Enemies/StoryEngagementDirector.cs` (new; `FTT.Enemies`, static)

- **Storage:** `Dictionary<ulong targetId, List<ulong>>` of member instance ids, in join order.
- **Pruning** runs on every access through `CollectMembers`. A member is dropped when it is any of:
  - invalid, queued for deletion, or out of the tree;
  - not physics-processing;
  - `!CanProcess()` while the tree is not paused (pool-parked);
  - role `None`;
  - in Dead, Patrol, Returning or Idle;
  - chasing another target.

  A dropped member's state is cleared (`ClearEngagementState`), except on a target mismatch, where the other group owns its flags.
- **Groups:** a group whose target is gone (invalid, out of tree, or `IsStoryHostile`) is dropped. Dead groups are swept whenever a new group opens.
- **Counts:** slot and permit counts are recomputed from the members' own flags on every query. The director has no clock of its own.
- **API:**
  - `CurrentDifficulty` reads `StoryDifficultyTuning.CurrentStoryDifficulty`.
  - Internal: `Join(mob, target)`, `Leave(mob)`, `IsGrantedMeleeSlot(mob)`, `HasEligibleWaiterFor(holder)`, `IsRangedPermitFree(mob)`, `HasLongerRangedWaiter(mob)`, `WaitRank(mob)`, `RingingWaitersOn(mob, side)`, `CountSides(mob, out holdersL/R, out presenceL/R)`.
  - Test seams: `MemberCountFor(target)`, `HolderCountFor(target)`, `ResetForTest()` (clears every member's flags too) and `BypassForTest`, an internal static property that answers every gate as if no director existed.

### `scripts/Enemies/EnemyController.Engagement.cs` (new partial)

- **State fields:** role, target id, waited / rest / stale counters, ring side, crossing flag and cooldown, blocked-away flag, pushbox-suppressed flag, ranged refused count and asked flag.
- **Public seams:** `EngagementRole`, `WaitRingRank` (−1 when not ringing), `WaitRingGapPixels`, `EngagementRestFramesRemaining`, `IsEngagementCrossing`.
- **The director's view:**
  - `EngagementSide`: −1 / +1 relative to `TargetAimPosition`; facing breaks a tie.
  - `EngagementRingSide`, `EngagementGapPixels`.
  - `EngagementWantsMeleeSlot`: the class of `PreviewNextAttack()`.
  - `IsRingingWaiter`, `IsMeleeSlotCandidate`.
  - `EngagementInFlightClass`: an `Attacking` member's active ability, or its previewed next attack during the reaction delay.
- **Hero run speed for the cap:** `Data.MaxMoveSpeed × StoryMoveSpeedMultiplier × StoryTemporarySpeedMultiplier × 60`, read from the target at runtime. GU-C's ×0.8 therefore flows in by itself.

### Hooks in `scripts/Enemies/EnemyController.cs` (one line each unless noted)

- **`ProcessChase`:**
  - `if (EngagementPermitsEngageBlink() && TryEngageBlink()) return;`
  - `if (!answerOwed && EngagementHoldsOnWaitRing(next, aim, dt)) return;` sits before the commit. It runs the holder's stale count and stale hand-off, the waiter's slot take, and otherwise `HoldOnWaitRing`.
  - `if (commits && !answerOwed && !EngagementPermitsCommit(next)) commits = false;` is the ranged permit gate.
  - The non-capped band lines now call `StandOffBandFor` (three lines).
- **`StandOffBandFor`:** the non-capped branch applies `CappedHoldEngagePixels`. A capped line releases at `engage × 1.10 / 0.85`; an uncapped one keeps `AttackRange × 1.10` exactly.
- **`ProcessMobTick`:** `TickEngagementCounters()` after `TickStaggerDiscipline`. That is after the hitstop, held and thrown returns and after the frozen return in `_PhysicsProcess`.
- **`ProcessAttacking`'s end:** `EngagementOnAttackEnded(engageBlink: _secondaryBlinkInFlight)`.
- **`ResolveFinisherTouchdown`:** `EngagementOnFinisherTouchdown()` before the pressure exit.
- **`LeaveEngagement()`** in `StartReturning`, `Die` (after the pushbox stands down), `OnSpawn`, `OnDespawn`, the non-preserve branch of `ApplyStoryRewind`, and `_ExitTree`.
- **`FindNearestPlayer`** skips `IsStoryHostile`. The same line is in `BossController.FindNearestPlayer`.

Never gated: `BeginAttack`, `CommitArmoredAnswer` (an owed answer bypasses the ring and walks in armored), `TryPressureExitAttack` and `TryReactivePhaseSkip`.

## 2. Files changed

- New:
  - `scripts/Combat/StoryEngagementRules.cs` (+ `.uid`)
  - `scripts/Enemies/StoryEngagementDirector.cs` (+ `.uid`)
  - `scripts/Enemies/EnemyController.Engagement.cs` (+ `.uid`)
  - `tests/Unit/StoryEngagementRulesTests.cs` (+ `.uid`)
  - `tests/Unit/StoryEngagementDirectorTests.cs` (+ `.uid`)
  - `docs/handoffs/GU_F.md`
- Edited:
  - `scripts/Enemies/EnemyController.cs`: the hooks above.
  - `scripts/Enemies/BossController.cs`: `FindNearestPlayer` only.
- **Edited outside the plan's §3.1 hook list**, all inside `EnemyController.cs` and minimal:
  - `ProcessMobTick`: the counter tick. The plan requires counters "ticked in unfrozen ticks after the hitstop/frozen returns".
  - `ResolveFinisherTouchdown`: the plan's "handed off at finisher touchdown only if a waiter is eligible".
  - `StandOffBandFor` and the non-capped band lines of `ProcessChase`: the ranged hold cap. GU-D's `ApplyStunnedKnockbackFriction` line and `TryApplyKnockback` are untouched, and so is everything GU-E owns.
- Not edited: `localization/en.csv` (no visible strings), scenes, resources, anything under `scripts/FighterSim/`.

## 3. Decisions (Provisional — ledger `PROVISIONAL-ENGAGEMENT-DIRECTOR-RULES`)

1. **The numbers** are the plan's (§2.6): per side E1/N1/H2, ranged permits E1/N2/H3, rank-0 gap 200/170/150, rest 90/45/20, spacing 64, tolerance ±12, stale 120, ranged hold 560, waiter speeds 0.8 / 0.6 capped at 0.9× the hero. Added: the 32 px priority gap bucket, as specified, and `CrossingCooldownFrames` = 60 (decision 7).
2. **Rest semantics.** A resting waiter is never *handed* a slot: hand-off eligibility requires its rest to have elapsed. It can still take a *free* slot when no rested waiter wants it, which is priority key 2. So when the only rested waiter dies, a resting mob re-engages at once instead of idling out its rest.
3. **Stale** means 120 consecutive own Chase ticks holding a slot without committing while a waiter could take it. It resets whenever the holder leaves Chase (an attack, a stun). A holder on a long cooldown therefore hands off after 2 s if someone is waiting.
4. **Hand-off eligibility.** A rested waiter in Chase, not crossing, whose next attack needs a melee slot and whose own side has room once the holder's slot frees (`CanTakeFreedSlot`).
5. **Ranks** are counted per side, by current gap, ties by instance id, with crossing waiters queued last on the side they cross to. A hand-off re-ranks the ring: the old holder, now nearest, becomes rank 0, and the others step out 64 px.
6. **Ranged fairness (added).** Among shooters whose ready ranged commit is refused for want of a permit, the one refused longest takes the next free permit (ties: lower instance id). The permit count itself stays stateless, as the plan says. Without the rule, tree order decides; `TheLongestRefusedShooterTakesTheNextRangedPermitWhateverTheTreeOrder` shows the difference.
7. **Side rebalancing, my reading of "crowded or blocked by a wall or barrier":**
   - A waiter crosses only when all of these hold:
     - it is rank 1 or more;
     - it has to move outward;
     - its place on its own side is cut off: its last step away was blocked, or an Environment wall lies between it and the position, or there is no floor there;
     - the other side holds fewer engaged melee mobs (holders and ringing waiters);
     - its ring position on the other side has floor and no wall in between;
     - its 60-frame crossing cooldown has run out.
   - During the crossing its pushbox is disabled. It walks through the hero at the approach speed. The crossing ends on the far side once it clears pushbox contact (own half-width + 18 + 4 px), so re-enabling the pushbox never shoves the hero.
   - On open floor nobody crosses. That is why three slashers on one side commit one at a time on Normal, as the plan's own test expects. A head-count rule would have had a waiter walk through the hero every time.
8. **Ranged hold cap scope.** It applies to every `AttackRange`-based stand-off band, lone mobs included (it is positional, not turn-taking). It binds only `infrared_border_sentry` today: 0.85 × 810 = 688.5 → 560. The next highest in the roster are `void_enforcer` and `chrono_warden` at 489.6.
9. **Hero speed for the cap** leaves out status and terrain multipliers, so a rooted or wading hero does not freeze the ring.
10. **The ring centre** is the mob's `TargetAimPosition`, so a live Cleopatra sand decoy moves the ring. The director's side and gap bookkeeping use the same aim.

## 4. Deviations from the plan

- Side rebalancing reads "crowded" as "out of room" (decision 7), not as a head-count imbalance.
- The ranged-fairness counter is an addition (decision 6).
- Hooks in `ProcessMobTick`, `ResolveFinisherTouchdown` and `StandOffBandFor` sit outside the §3.1 hook list; the plan's own text needs them (§2 above).
- The boss half of "no mob targets the Mirror clone" is a one-line code change with no test. The director suite pins the `EnemyController` half.
- Mob↔mob pushbox jostling is deferred, as the plan says.

## 5. Tests

- **New: +24 cases.**
  - `StoryEngagementRulesTests`, 9 cases, pure C# with no `[RequireGodotRuntime]`:
    - the slot table;
    - the wait ring against the Phase Skip radius, the 77 px commit and `WidestHit1PivotReachPixels(29)` = 144.2;
    - every archetype's class;
    - the priority order;
    - grants under the caps, including resting versus rested;
    - the hand-off test;
    - the waiter speed cap;
    - the ranged hold cap;
    - the stale, crossing and fairness rules.
  - `StoryEngagementDirectorTests`, 15 cases, `[RequireGodotRuntime]`. Real frames through `OutOfBandPhysicsStep.RunInPhysicsFrameAsync`; fixed reaction delays; `CurrentSession` and the registry restored in `finally`:
    - three slashers on one side commit one at a time while the rest hold settled ranks 64 px apart;
    - round-robin rotation over 1500 frames;
    - Normal both sides: one attacker per side, two at once;
    - Easy/Normal/Hard hold 1/2/3 slots (per side 1/1/2), with the director's count equal to the flags every frame;
    - death, `OnDespawn`, `Free()` and a resetting rewind each free the slot, and a stun keeps it;
    - a lone mob is frame-identical to a `BypassForTest` control for 500 frames and holds `Slot` from its first Chase tick;
    - an elite takes the next slot before a longer-waiting standard;
    - ranged permits cap concurrent shots at 1 (Easy) and 2 (Normal), and every archer fires;
    - the longest-refused shooter wins whatever the tree order;
    - the sentry holds at ≤ 560 px;
    - no mob targets or groups on the Mirror clone;
    - Time Freeze leaves roles, counters and positions untouched;
    - two identical runs are identical;
    - a waiter cut off by a wall crosses with its pushbox down, takes the far slot, and does not shove the hero;
    - a source scan: nothing under `scripts/FighterSim/` names `StoryEngagement`.
- **Rewritten: none.**
- **Mutation checks (reverted):**
  - With `Join` forced off (no director), 11 of the then 14 director cases failed. The ranged-cap, determinism and source-scan cases rightly passed.
  - With the fairness rule disabled, the fairness case failed.
- **Runs:**

  | Run | Result |
  |---|---|
  | Before writing tests, the 16 enemy suites the brief lists (EnemyControllerTests, StoryEnemyCombatTests, StoryEnemyCombatRulesTests, StoryEnemyCombatReviewTests, StoryPunishWindowTests, StoryFinisherCarryTests, StoryFirstSpecialTaxTests, RiftPhantomTeleportTests, CleopatraSandDecoyTests, ArenaEnemyAttackGeometryTests, PoolManagerTests, BossControllerTests, MirrorParadoxTests, EnemyRosterContentTests, TimeFreezeTests, PostLandingHoldTests) | **244/244**, equal to their `[TestCase]` count; no existing test changed |
  | The two new suites | 24/24 |
  | **Full suite** | `Passed! - Failed: 0, Passed: 3045, Skipped: 0, Total: 3045` (1 m 12 s) |

  3045 = 3021 + 24 exactly. The `[TestCase]` count under `tests/` is 3045.
- `dotnet build`: 0 errors and only the three pre-existing warnings (CS8632 in GdUnit, KLSG_ECS004, CS0649 on `InputManager`). No KLOTHO_DET002.

## 6. Measurements

- **Ranged hold cap.** `infrared_border_sentry` stand-off engage 688.5 → 560 px and release 891 → 724.7 px. No other roster mob changes.
- **Waiter speeds against GU-C's slowest hero** (Lincoln, 228 px/s):
  - `chrono_slasher` (300 px/s): approach 205.2 and back-off 180 px/s.
  - `chrono_chariot_raider` (390 px/s): both 205.2 px/s.
- **Bot smoke runs** (one run each, Level 2, Joan, `play`, no before batch, so not a comparison):

  | Difficulty | End | Time | Deaths | Damage taken | Mob melee swings |
  |---|---|---|---|---|---|
  | Normal | `left_level:collapse(Death)` at the boss | — | 4 | 411, of which `siegemaster_duke` 314 and `chrono_slasher` 10 | 2 |
  | Easy | `level_complete` | 132.9 s | 1 | 196 (boss 127, no mob damage) | 2 |

  - No script errors or exceptions in either log.
  - The boss is outside the director.
  - Level 2's mob fights are too short to measure turn-taking. The `mob_engagement` telemetry and the before/after batch are GU-BOT's and the closeout's.

## 7. Not delivered / open

- **Crossing presentation.** A crossing waiter visibly passes through the hero's body. The game is 2D and the pushboxes would otherwise stop it. Nobody has looked at it. Fallback if it reads badly: turn crossing off (waiters then stand at the wall).
- **Waiters never strike first** while waiting, even with the hero adjacent; they retaliate only after being hit. A ruling may want an "in reach and the hero is facing away" exception.
- **Mob↔mob overlap.** Waiters walking to their ranks pass through each other and through holders, because mobs never collide. Jostling is deferred, as the plan says.
- **Aggro through walls.** A mob that aggroes from another room can hold a slot until the 2 s stale rule hands it off. GU-E's locks largely remove the case.
- **Close-range ranged waiters.** A ranged-class waiter with a short `AttackRange` (an elite whose next attack is a projectile signature, such as `cyber_centurion` from 132 px) holds in its band close to the hero without firing while no permit is free.
- **Flyers** ring horizontally only. They track the hero's height while moving and stop vertically when they settle.
- **The boss half of the Mirror rule is untested** (inspection only).
- **Optional plan items not done:** the `PoolManagerTests` assertion that a recycled body has `EngagementRole == None`; capping AreaPulse commits at `PulseRadius + 20`. AreaPulse is melee-class here, so a waiter with an AreaPulse next holds on the ring until it gets a slot, then still commits from its authored range.

## 8. For the closeout

### AGENTS.md

- **The Story enemy combat rules bullet:** add the engagement director paragraph (what a player sees, the table, gate-decisions-not-actions, the ranged hold cap, the Mirror skip, the suites). Amend the D6c sentence: a melee waiter holds on the ring instead of walking in.
- **The scene-scoped systems table:** add a row for `StoryEngagementDirector` (static per-target registry, `FTT.Enemies`) + `EnemyController.Engagement.cs` + `StoryEngagementRules` (`FTT.Combat`, pure).
- **The Mirror Paradox bullet:** "no mob or boss targets the clone (`FindNearestPlayer` skips `IsStoryHostile`)".
- **Known gaps:** crossing through the hero, no mob↔mob jostle, waiters never strike first.
- **The coding conventions:** "nothing in `scripts/FighterSim/` may consume … `StoryEngagementRules`" (guarded by `StoryEngagementDirectorTests.NothingInTheFighterSimNamesTheEngagementDirector`).
- **The baseline:** +24 (`StoryEngagementRulesTests` 9, `StoryEngagementDirectorTests` 15).

### Ledger rows

- **`USER-ENEMY-TURN-TAKING`:** request 6 verbatim plus the E1/N2/H3 ruling. Acceptance: a bot batch with `mob_engagement` (max and mean concurrent attackers within 250 px ≤ the slot totals on each difficulty) and a hands-on pass.
- **`PROVISIONAL-ENGAGEMENT-DIRECTOR-RULES`:** every number and decision in §3, including the crossing, the fairness rule and the rest semantics.
- **Amend `USER-MOB-MELEE-RANGE`:** the ring overrides the walk-in for waiters; the 560 px ranged hold cap; the sentry's new line.
- **Amend `PROVISIONAL-ENEMY-CHASE-LEASH`:** waiters still leash, and ledge and wall stops apply in either direction on the ring.
- **Amend `PROVISIONAL-SPACING-WALLS-RULES` / `USER-FINISHER-CARRY`:** a carried mob keeps its slot until touchdown and hands off there only to an eligible waiter.
- **New `VERIFY-ENGAGEMENT-CROSSING-PRESENTATION`:** a waiter walks through the hero when its side is cut off.
- **Keep `DEFER` for mob↔mob jostling.**

### Design-master lines owed (owner User; line numbers are the repository mirror's at `b8f5343`)

- `design-godot.md:2996` (Aggro/Chase: "moves directly toward the player").
- `:2997` (Attack: "immediately transitions to `Attacking`").
- `:3004` (the Stand-Off Band; add the turn-taking ring and the 560 px ranged hold).

The recon cited `:3005` against the pre-plan tree.
