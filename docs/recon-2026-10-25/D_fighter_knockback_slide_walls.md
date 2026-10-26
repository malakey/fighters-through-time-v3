# Recon D — Fighter finisher knockback, the hitstun slide, walls (2026-10-25)

Read-only subagent report at `0eba848`. Numbers come from a hand-port of the tick order into Python (`D:\Projects\ftt-scratch\knockback\model.py`, `m2.py`), not checked against a run (marked "model"); code claims are verified.

## 1. Knockback pipeline (verified)
- **Finisher force:** `FighterSimulationSystems.cs:2521-2523` computes `BasicKnockback * profile.FinisherKnockbackTenths / 10` for step 2 (default 45 tenths, Mozart 55; `BasicComboRules.cs:120,285`). The shared `BasicComboRules.KnockbackMultipliers[2]` (4.5, `:368`) is **not read at runtime for the finisher in either mode**: Fighter uses the profile tenths, Story uses `PlayerController.cs:5285-5289` (also profile tenths). `[0]` and `[1]` are read (sim `FighterSimulationSystems.cs:1916`, Story `PlayerController.cs:5287`); the air strike and the ledge attack use `[0]`. `[2]` is referenced only by Story test fixtures (`StoryEnemyCombatTests:1152`, `StoryEnemyCombatReviewTests:379`, `StoryFinisherCarryTests:545`, `StoryWallBounceTests:401`) and a doc comment. Story's D6a carry (`StoryEnemyCombatRules`) and `EnemyController.cs:815` read `profile.FinisherKnockbackTenths`. **So editing the profile tenths or the table moves Story; a Fighter-only change needs a new sim-local constant at `:2521`.**
- **Per-hit force:** `force = ScaleByMissingHP(kb) / (1 + victimWeight)` (`FighterEntitySystems.cs:931`; `ScaleByMissingHP` at `:1105`, 1× at full HP to 2× at 0 HP, after the hit's damage).
- **Launch vector:** `Velocity.x = ±force`, away from the hit origin (`:940`). A launching hit sets `vy = force * verticalScale` (`:937-942`); the finisher passes scale 0, which becomes 1 → a 45° launch with `vy = vx`. Lincoln's hit 2 uses scale 2.0 and launches. Hits 1 and 2 on a grounded victim set `vy = 0` (`:957`). A launch is stashed for DI (`StashPendingLaunch`, `FighterVerbRules.cs:94`) and rotated ±15° at hitstop end (`ResolvePendingLaunch`, `:108`).
- Hitstun table `{30, 40, 24}` (`BasicComboRules.cs:348`). Only the finisher launches by default (`:379`; Lincoln's hit 2 via `Hit2Launches`).
- Same pipeline: the Up-Attack and Down-Air (`ApplyDirectionalSwing`, `:2741`; slam `:2813`), the air strike (`:2677`) and ledge attack (`LedgeAttack.cs:62`) use `[0]`; throws use 0.8 / 3.0 / 2.5 × `BasicKnockback` (`:2324`); Specials use the loadout's `KnockbackMagnitude`.

## 2. Motion during hitstun — the "strange slide" (verified)
- `FighterSimulationSystems.cs:623-628`: in hitstun or daze the branch only does `Velocity.y = StepLaunch(vy)` (flat −30, clamp −20). **`Velocity.x` is never touched** — no friction, drag or decay on the hit victim.
- So knockback is constant-velocity motion for the whole hitstun (hitstun is decremented in `TickCounters` before movement: N frames of hitstun → N−1 constant-velocity ticks). On the tick hitstun reaches 0, `ApplyNormalMovement` (`:1216-1237`) runs the grounded 4-frame stop ramp (step `MoveSpeed/4`, larger than the 1–3 u/s slide speed), so it stops within one or two ticks.
- **What the user sees:** the victim glides away at fixed speed for 0.5 s (hit 1) or 0.67 s (hit 2), then halts abruptly. The finisher is a straight-line 45° flight.
- At a wall, the position is clamped (`:807`) and `Velocity.x` is left alone, so the victim presses into the wall for the rest of the launch.
- The **guard-break daze** shares the frictionless branch: a fixed push of (±2.0, −1.0) for 60 frames (`FighterEntitySystems.cs:766`, `BasicComboRules.GuardBreakPushX`).
- Blockers: a non-shatter blocked hit writes no velocity; the stance decelerates through the normal ramp (`lockHorizontal: blockStance`).
- After hitstun in the air, `ApplyNormalMovement` ramps `vx` to 0 at `MoveSpeed/4 × AirControl` per tick; `StepMovement` applies the fall multiplier 2.2 and the 12 terminal.
- Landing: ground snap at `:856`, then `TryBounce`, `TryLandingTech` or `TryBeginKnockdown` (`:881-888`); tech and knockdown zero velocity. `TumbleAfterHitstun` (310 `Tumble` 2) outlives hitstun while airborne.
- `FighterPushboxSystem` (PostUpdate, `:1757-1760`) zeroes only a velocity component heading into the other fighter.

## 3. Walls, ceiling, pits (verified)
Movement clamps `Position.x` to `[LeftWall, RightWall]` at `:483` (knockdown branch) and `:807`; the ceiling at `:808` zeroes upward `vy`; side-wall velocity is never zeroed or reflected. Blast zone −5 on every stage; ceiling 9; spawn distance 4 (fighters start at ±4) on all ten stages.

| Stage | Wall ± | Floor |
|---|---|---|
| Florence, Orléans, Alexandria, Globe | 9 | sealed |
| Chicago, Berlin, Gettysburg | 10 | sealed |
| Paris | 9 | open: [−9, −2.5] and [2.5, 9] |
| Vesuvius | 8 | open: [−8, 5.5] |
| Nassau | 9 | open: [−9, 6] |

## 4. Numbers (model)
Assumptions: victim weight 1.0, MaxHP 100, speed 5.4, air control 0.6, no DI, no input after the hit; finisher hitstun 24; launch from the ground; landing ends travel; hitstop freezes position.

**Finisher** (`vx = vy = force`; horizontal travel to landing):

| Kit | Base kb | Force 100 % | Airtime | Travel 100 % | Force 50 % | Travel 50 % | Travel 25 % HP |
|---|---|---|---|---|---|---|---|
| Cleopatra | 2.0 | 4.5 | 17 t | 1.27 | 6.75 | 2.84 | 3.52 |
| Leonardo / Tesla | 2.5 | 5.6 | 22 t | 2.06 | 8.4 | 3.87 | 4.69 |
| Einstein / Shakespeare / Tubman | 3.0 | 6.75 | 26 t | 2.84 | 10.1 | 4.85 | 5.87 |
| Mozart (5.5×) | 2.5 | 6.9 | 26 t | 2.90 | 10.3 | 4.96 | 6.00 |
| Joan | 3.5 | 7.9 | 29 t | 3.52 | 11.8 | 5.87 | 7.12 |
| Lincoln | 5.0 | 11.25 | 38 t | 5.52 | 16.9 | 9.26 | 11.4 |

Weight sensitivity (Einstein at 100 % HP): victim weight 0.7 → 3.59, 1.0 → 2.84, 1.6 → 1.73. The finisher lands before its 24-tick hitstun ends for Cleopatra, Leonardo and Tesla.

**Grounded slide** (victim weight 1.0, full HP; constant-velocity slide plus the stop ramp):

| Kit | Hit 1 speed | Hit 1 distance | Hit 2 speed | Hit 2 distance |
|---|---|---|---|---|
| Cleopatra | 1.04 | 0.50 | 1.30 | 0.85 |
| Leonardo / Tesla | 1.30 | 0.63 | 1.64 | 1.07 |
| Einstein / Shakespeare / Tubman | 1.56 | 0.76 | 1.96 | 1.29 |
| Mozart | 1.29 | 0.63 | 1.62 | 1.06 |
| Joan | 1.85 | 0.90 | 2.33 | 1.53 |
| Lincoln | 2.6 | 1.28 | 3.28 | 2.17 |

Lincoln's hit 2 launches instead (force ~3.2, `vy` 2×, travel ~1.3). A typical non-launching Special was not computed (same formula, the loadout's `KnockbackMagnitude`, the same frictionless slide).

**Reaching a wall:** from spawn (±4) the victim is 4–6 units from a wall, minus slides; hits 1–2 already move it 1.3–3.5 units. At full HP the cumulative string travel for Einstein-class kits is ~5 units (≈ a wall at spawn-distance fights on ±9 stages); low-reach kits reach a wall only within 2–3 units of it; at ≤ 50 % HP most kits reach a wall from mid-stage, Lincoln already at 100 % (estimate).

**Pinned separation:** `FighterKnockbackScalingTests.TheFinisherSeparatesEveryKitBeyondMeleeRangeAtFullVictimHP` requires separation at hitstun end ≥ max(2.5, hit-1 reach + victim hurtbox half-width + 0.5); tightest on 2026-10-11 Cleopatra 3.19 vs 2.92 (mirror matchup, 1.6-unit gap). A stronger finisher only raises the margin.

## 5. Wall-bounce hook
- **Detection:** `FighterSimulationSystems.cs:806-807` — capture `x` before the clamp and compare; condition: clamped, `HitstunFrames > 0 || verb.Tumble != 0`, `|vx|` ≥ threshold and pointing into the wall → `vx = −vx × k`. Inside the movement system (Update phase), before the pushbox and combat (PostUpdate); the 323 ref `action` is in scope.
- **"Launched":** `verb.Tumble` (1 in hitstun, 2 after) and `HitstunFrames`. The knockdown branch (`:483`) has its own clamp; rolls are not launches.
- **Once-per-launch flag:** 323 `ActionFlags` bits 4–31 are documented reserved and zero (`FighterSpecialCast.cs:87-89`); nothing writes them (verified). Use bit 4; clear it where a launch is applied — `ResolveFighterHit` already receives `ref targetAction` (`FighterEntitySystems.cs`), beside `StashPendingLaunch` (`:949`). (After reflection the fighter moves away and the halved speed may already fall below the threshold; the flag matters if DI pushes it back into the same wall.)
- **Presentation:** a wrapping bounce counter in 323 (e.g. bits 8–11), mirroring the perfect-block count (`BlockTiming` bits 20–23); `SyncPerfectBlockFlash` (`FighterSimulationDriver.cs:913-922`) is the template (read-only edge detect → `glow.FlashHit`, `CameraShake.Instance.Shake`, haptics). Cues go through `CoreMechanicCuePresenter` (`SyncCoreMechanicCues`, `:998`) and would need a new ID plus a `core_mechanic_cues.tres` row. A VFX burst via `VfxEmitter` is new; the driver already spawns floating damage numbers at `:831`. (A counter-free alternative — detecting a velocity-sign flip at `|x| == wall` — is fragile across rollback.)
- **Determinism:** FP64 only; `k` an `FP64.FromDouble` constant in `scripts/FighterSim/` or Godot-free `BasicComboRules`; never `StoryEnemyCombatRules`.

## 6. Grounded-hitstun friction hook
- Insert in the hitstun branch at `FighterSimulationSystems.cs:623-628`, guarded by `fighter.IsGrounded != 0 && fighter.DazeFrames <= 0 && verb.Tumble == 0` (the same branch carries the guard-break push and launches).
- Sizes: a stop in 6 frames with the speeds above gives ~0.05–0.17 u slides; Story mob friction is 1800 px/s² = 30 u/s² (kills a 1–3 u/s slide in 2–3 ticks).
- Interactions (verified): the hit-2 Block escape and `CanBlockEscapeHitstun` read only hitstun and tumble; the string relies on hitstun covering the connect gaps (30 / 40 / 24), not on sliding (`AVictimWhoDoesNotBlockIsStillInHitstunWhenHitsTwoAndThreeConnect` uses a zero-knockback attacker); today hits 2 and 3 can whiff when the victim slides out of the ~2.2-unit box, so less slide improves reliability; the CPU reads only its own velocity, and the melee band uses distance; less slide near Open-stage pit edges; hazard knockback goes through `ApplyEnvironmentHit` and the same velocity write.

## 7. Tests pinning current behaviour (all `tests/Determinism`; none pins an absolute hash)
`FighterKnockbackScalingTests` (`TheFinisherSeparatesEveryKitBeyondMeleeRangeAtFullVictimHP`, `TheSameHitScalesItsImpulseLinearlyWithTheVictimsMissingHP`, `TheSharedScaleRampsFromOneAtFullHPToTwoAtZero…`); `FighterKnockdownTests` (`NonLaunchingHitsStayGroundedAndReopenTheHitTwoBlockEscape` — needs a non-zero `Velocity.x` after a hit; `LincolnsHitTwoLaunchesAndItsVictimDisAwayAndTechs`; `TheLaunchFlagTableIsTheAuthoredOneAndTheIntentReadsItsContract`); `FighterBasicStringParityTests` (`AVictimWhoDoesNotBlockIsStillInHitstunWhenHitsTwoAndThreeConnect`, `BlockEscapesHitstunOnlyAfterHitTwoConnects`, `AnAirborneVictimHoldingBlockCannotEscapeHitstun`); `FighterTumbleTests` (`APassiveTumbleWhoseHitstunEndsInTheAirKnocksDownAndBlockTechsIt`, `TumbleReadsOneInHitstunAndTwoBetweenTheHitstunEndAndTheLanding`, `ANonLaunchingHitEndsTheTumble…`, `ADepthSevenRollbackReplaysThePostHitstunTumbleAndItsExit`); also `DownAirSlamTests`, `FeelPassSimTests.TheLaunchingFinisherFreezesBothFightersForTheLaunchBonus`, `FighterCombatContractW3Tests`, `FighterHitPipelineTests`, `FighterGrabTests`, `FighterPerfectBlockTests`, `FighterPresentationSyncTests`, `FighterCpuBehaviorTests`, `CpuRecoveryMatrixTests` and the CPU suites (likely sensitive to finisher distance). Leave the Story suites (`StoryFinisherCarryTests`, `StoryWallBounceTests`, the `KnockbackMultipliers[2]` fixtures) alone.

## 8. Story hero (report only, inferred)
`PlayerController.ProcessStunned` (`PlayerController.cs:3776-3860`) calls only `ApplyGravity` and never decelerates `Velocity.X`, so the Story hero also slides at constant velocity for the whole stun (Godot's `MoveAndSlide` zeroes it at walls). Story mobs have friction (S1). Not changed by this package.

## Risks
Every Fighter hash moves (LAN builds desync; LAN is de-scoped). Stronger finishers plus a bounce could produce loops against a wall. The post-hitstun air ramp kills `vx` quickly, so travel past hitstun mostly comes from the launch itself. The model has not been checked against a real run.
