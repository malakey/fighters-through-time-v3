using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 11 A9b — the F19 validation sweep <c>CPU_RECOVERY.md</c> demands:
/// "Exercise all nine characters on every authored Open stage at all three CPU
/// difficulties… Also check Closed stages for false recovery triggers."
/// </summary>
/// <remarks>
/// <para>
/// Nine cases: one per (Open stage x difficulty) pair, each sweeping all nine
/// kits through a pit-escape drill against the <b>real</b>
/// <see cref="FighterStageGeometry"/> — Paris's 5.0-wide central courtyard pit,
/// Vesuvius's 2.5-wide gap against the right wall, and Nassau's 3.0-wide stern
/// gap. Every case also runs the Sealed-stage false-trigger check, because the
/// contract asks for it and the cheapest place to prove it is beside its
/// counter-example.
/// </para>
/// <para>
/// The drill harness integrates with the simulation's own tick length and gravity
/// (<c>FighterMovementSystem.FixedDeltaSeconds</c> /
/// <c>.GravityPerSecondSquared</c>) and applies the movement ability exactly the
/// way <c>FighterAbilitySystem.ApplyMovement</c> does per authored
/// <c>MovementType</c>, reading every distance and speed from the character's own
/// <see cref="CpuRecoveryProfile"/>. There is no CPU-only jump reset, cooldown
/// refund, invulnerability or teleport reach anywhere in it.
/// </para>
/// <para>
/// <b>What is asserted, and what deliberately is not.</b> "An unreachable route
/// may still end in a KO; no tier promises successful or optimal recovery", so
/// the policy invariants are asserted for every drill on every band — never steer
/// away from the return target, never cast a Special, respect the per-episode
/// activation cap — while <i>successful return</i> is asserted only for Medium
/// and Hard from a shallow knock, which is the depth the contract's "basic route"
/// language actually covers.
/// </para>
/// </remarks>
[TestSuite]
[RequireGodotRuntime]
public class CpuRecoveryMatrixTests {
    private const int DrillTicks = 600;

    private static readonly string[] Roster = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra",
        "tesla", "shakespeare", "mozart", "tubman"
    };

    // === Paris Bastille: a 5.0-wide pit at stage centre, ledges at ∓2.5 ===
    // The drop is 0.5 units past the left ledge — the contract's shallow
    // knockback. Dead centre of a 5.0-wide pit is NOT a basic route: Lincoln's
    // horizontal-only Rail Charge and lowest-in-roster jump cannot cover 2.5
    // units of climb from there, and "an unreachable route may still end in a
    // KO; no tier promises successful or optimal recovery".

    [TestCase]
    public void EasyRecoversTheWholeRosterFromTheParisCourtyardPit() =>
        SweepRoster("paris_bastille", CpuDifficulty.Easy, dropX: -2.0);

    [TestCase]
    public void MediumRecoversTheWholeRosterFromTheParisCourtyardPit() =>
        SweepRoster("paris_bastille", CpuDifficulty.Normal, dropX: -2.0);

    [TestCase]
    public void HardRecoversTheWholeRosterFromTheParisCourtyardPit() =>
        SweepRoster("paris_bastille", CpuDifficulty.Hard, dropX: -2.0);

    // === Vesuvius Caldera: a 2.5-wide gap against the right wall, ledge at +5.5 ===

    [TestCase]
    public void EasyRecoversTheWholeRosterFromTheVesuviusCaldera() =>
        SweepRoster("vesuvius_caldera", CpuDifficulty.Easy, dropX: 6.5);

    [TestCase]
    public void MediumRecoversTheWholeRosterFromTheVesuviusCaldera() =>
        SweepRoster("vesuvius_caldera", CpuDifficulty.Normal, dropX: 6.5);

    [TestCase]
    public void HardRecoversTheWholeRosterFromTheVesuviusCaldera() =>
        SweepRoster("vesuvius_caldera", CpuDifficulty.Hard, dropX: 6.5);

    // === Nassau Flagship: a 3.0-wide stern gap, ledge at +6 ===

    [TestCase]
    public void EasyRecoversTheWholeRosterFromTheNassauStern() =>
        SweepRoster("nassau_flagship", CpuDifficulty.Easy, dropX: 7.0);

    [TestCase]
    public void MediumRecoversTheWholeRosterFromTheNassauStern() =>
        SweepRoster("nassau_flagship", CpuDifficulty.Normal, dropX: 7.0);

    [TestCase]
    public void HardRecoversTheWholeRosterFromTheNassauStern() =>
        SweepRoster("nassau_flagship", CpuDifficulty.Hard, dropX: 7.0);

    // === Package 13 W5: Tubman's North Star Leap (the roster swap) ===
    // Pocahontas's approved Spirit Strike and its two drills left with her; no
    // character approves a mobility Special now. Tubman's plan is the leap.

    /// <summary>
    /// Both Paris edges and a deep drop, on Hard: every drill lands, no Special
    /// is ever cast to recover (CPU_RECOVERY: Conductor's Call and Foresight are
    /// not recovery tools), and she never steers away from the return.
    /// </summary>
    [TestCase]
    public void HardTubmanRecoversFromBothParisEdgesAndFromDepthWithTheLeap() {
        FighterStageGeometry paris = FighterStageGeometry.ForStage("paris_bastille");
        CpuRecoveryProfile tubman = ProfileFor("tubman");
        AssertThat(tubman.MobilitySpecial).IsEqual(CpuMobilitySpecialSlot.None);
        foreach ((double dropX, double startY) in new[] { (-2.0, -0.25), (2.0, -0.25), (-1.5, -2.0), (1.5, -2.0) }) {
            DrillResult result = RunDrill(
                paris, CpuDifficulty.Hard, tubman, dropX, seed: 20260927,
                startY: startY, targetX: dropX < 0 ? -5.0 : 5.0);
            string drill = $"x={dropX}, y={startY}";
            AssertThat(result.SpecialEdges + result.ApprovedSpecialEdges)
                .OverrideFailureMessage($"Tubman cast a Special to recover ({drill}).").IsEqual(0);
            AssertThat(result.SteeredAwayFromReturn)
                .OverrideFailureMessage($"Tubman steered away from the return ({drill}).").IsFalse();
            AssertThat(result.Landed)
                .OverrideFailureMessage(
                    $"Tubman failed the Paris drill {drill} (ended at x={result.FinalX}, y={result.FinalY}).")
                .IsTrue();
        }
    }

    /// <summary>
    /// With her jumps spent the aimed 8-way leap alone brings her back from a
    /// shallow Paris knock on both sides, on Medium and Hard; Easy still never
    /// casts a Special with nothing else in hand.
    /// </summary>
    [TestCase]
    public void WithJumpsSpentTubmansAimedLeapStillBringsHerBack() {
        FighterStageGeometry paris = FighterStageGeometry.ForStage("paris_bastille");
        CpuRecoveryProfile tubman = ProfileFor("tubman");
        foreach (CpuDifficulty band in new[] { CpuDifficulty.Normal, CpuDifficulty.Hard }) {
            foreach (double dropX in new[] { -2.0, 2.0 }) {
                DrillResult result = RunDrill(
                    paris, band, tubman, dropX, seed: 20260928,
                    startY: 0.0, startJumps: 0, targetX: dropX < 0 ? -5.0 : 5.0);
                AssertThat(result.LeapsCast > 0)
                    .OverrideFailureMessage($"{band} never leapt from x={dropX} with no jumps left.")
                    .IsTrue();
                AssertThat(result.Landed)
                    .OverrideFailureMessage(
                        $"The leap did not bring her back from x={dropX} on {band} (ended at x={result.FinalX}, y={result.FinalY}).")
                    .IsTrue();
            }
        }
        DrillResult easy = RunDrill(
            paris, CpuDifficulty.Easy, tubman, -2.0, seed: 20260928,
            startY: 0.0, startJumps: 0, targetX: -5.0);
        AssertThat(easy.ApprovedSpecialEdges + easy.SpecialEdges).IsEqual(0);
    }

    /// <summary>
    /// Runs the nine kits through one Open stage at one band, then proves the
    /// same band never enters recovery during ordinary supported traversal on a
    /// Sealed stage.
    /// </summary>
    private static void SweepRoster(string stageID, CpuDifficulty band, double dropX) {
        FighterStageGeometry geometry = FighterStageGeometry.ForStage(stageID);
        AssertThat(geometry.IsOpenStage)
            .OverrideFailureMessage($"{stageID} must be an Open stage for this drill.")
            .IsTrue();

        foreach (string characterID in Roster) {
            CpuRecoveryProfile profile = ProfileFor(characterID);
            DrillResult result = RunDrill(geometry, band, profile, dropX, seed: 20260913);

            AssertThat(result.SpecialEdges)
                .OverrideFailureMessage(
                    $"{characterID} on {band} at {stageID} cast an unapproved Special to recover.")
                .IsEqual(0);
            // "no Specials anywhere" on Easy, approved or not.
            if (band == CpuDifficulty.Easy) {
                AssertThat(result.ApprovedSpecialEdges)
                    .OverrideFailureMessage($"{characterID} on Easy at {stageID} cast a Special.")
                    .IsEqual(0);
            }
            AssertThat(result.SteeredAwayFromReturn)
                .OverrideFailureMessage(
                    $"{characterID} on {band} at {stageID} steered away from the return target.")
                .IsFalse();
            if (band != CpuDifficulty.Hard) {
                AssertThat(result.MovementActivations <= 1)
                    .OverrideFailureMessage(
                        $"{characterID} on {band} spent {result.MovementActivations} movement "
                        + $"activations in one offstage episode at {stageID}.")
                    .IsTrue();
            }
            // "Every action must make progress toward returning, not create
            // indefinite offstage cooldown/ledge stalls."
            AssertThat(result.Resolved)
                .OverrideFailureMessage(
                    $"{characterID} on {band} at {stageID} stalled offstage for the whole drill.")
                .IsTrue();
            if (band == CpuDifficulty.Easy) continue;
            AssertThat(result.Landed)
                .OverrideFailureMessage(
                    $"{characterID} on {band} failed the shallow {stageID} return "
                    + $"(ended at x={result.FinalX}, y={result.FinalY}).")
                .IsTrue();
        }

        AssertNoFalseTriggerOnASealedStage(band);
    }

    /// <summary>
    /// "Closed stages and ordinary supported traversal must not trigger emergency
    /// casts." Alexandria's floor is unbroken wall to wall, so a fighter airborne
    /// above it — a routine jump — must keep fighting rather than recovering, on
    /// every band.
    /// </summary>
    private static void AssertNoFalseTriggerOnASealedStage(CpuDifficulty band) {
        FighterStageGeometry sealedStage = FighterStageGeometry.ForStage("alexandria_chambers");
        AssertThat(sealedStage.IsOpenStage)
            .OverrideFailureMessage("Alexandria must stay Sealed; the false-trigger check depends on it.")
            .IsFalse();

        var cpu = new FighterCpuController(band, 5150, null, null, null, ProfileFor("einstein"));
        CpuDecisionObservation hop = StageObservation(sealedStage, x: FP64.FromInt(-3), y: FP64.FromInt(2));
        hop.SelfVelocityYRaw = FP64.FromInt(-4).RawValue;
        hop.TargetPositionXRaw = FP64.FromDouble(-2.5).RawValue;
        hop.RemainingJumps = 1;

        PlayerInputFrame previous = default;
        int attackEdges = 0;
        int specialEdges = 0;
        for (uint tick = 0; tick < 300; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in hop, in previous);
            previous = frame;
            if (frame.IsPressed(GameplayButtons.BasicAttack)) attackEdges++;
            if (frame.IsPressed(GameplayButtons.Special1)
                || frame.IsPressed(GameplayButtons.Special2)) specialEdges++;
        }
        // The recovery branch emits no attack of any kind, so a single basic
        // edge proves the CPU is in ordinary combat rather than emergency return.
        AssertThat(attackEdges > 0)
            .OverrideFailureMessage($"{band} entered recovery during supported traversal on a Sealed stage.")
            .IsTrue();
        if (band == CpuDifficulty.Easy) AssertThat(specialEdges).IsEqual(0);
    }

    private static CpuRecoveryProfile ProfileFor(string characterID) {
        CharacterData data = AuthoredResources.Load<CharacterData>(
            $"res://resources/Characters/{characterID}_data.tres");
        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(data);
        return CpuRecoveryProfile.FromLoadout(in loadout);
    }

    /// <summary>
    /// One pit-escape drill. The fighter starts just past the gap edge at
    /// <paramref name="dropX"/>, airborne and falling, with a full air-jump
    /// budget and its movement ability ready — the shallow knock the contract's
    /// "basic route" language covers.
    /// </summary>
    private static DrillResult RunDrill(
        FighterStageGeometry geometry,
        CpuDifficulty band,
        CpuRecoveryProfile profile,
        double dropX,
        int seed,
        double startY = -0.25,
        int startMovementCooldown = 0,
        int startJumps = -1,
        double? targetX = null) {
        var cpu = new FighterCpuController(band, seed, null, null, null, profile);
        FP64 step = FighterMovementSystem.FixedDeltaSeconds;
        FP64 gravity = FighterMovementSystem.GravityPerSecondSquared;

        FP64 x = FP64.FromDouble(dropX);
        FP64 y = FP64.FromDouble(startY);
        FP64 velocityX = FP64.Zero;
        FP64 velocityY = FP64.FromInt(-3);
        int remainingJumps = startJumps >= 0 ? startJumps : profile.MaxJumpCount;
        int movementCooldown = startMovementCooldown;
        int specialCooldown = 0;
        // Package 12 W4 / 13 W5: the blink and North Star Leap are multi-frame
        // kit phases in the sim (FighterKitMotion), mirrored here frame for frame.
        int kitPhase = KitNone;
        int kitFrames = 0;
        int kitDirectionX = 0;
        int kitDirectionY = 0;
        int floatFrames = 0;
        int facing = x >= FP64.Zero ? -1 : 1;
        int unsupportedFrames = 0;
        cpu.GetReactionDelayBounds(out int reactionWindow);
        reactionWindow += 4;
        var result = new DrillResult();
        PlayerInputFrame previous = default;

        for (uint tick = 0; tick < DrillTicks; tick++) {
            CpuDecisionObservation observation = StageObservation(geometry, x, y);
            if (targetX.HasValue) observation.TargetPositionXRaw = FP64.FromDouble(targetX.Value).RawValue;
            observation.SelfVelocityXRaw = velocityX.RawValue;
            observation.SelfVelocityYRaw = velocityY.RawValue;
            observation.RemainingJumps = remainingJumps;
            observation.MovementCooldownFrames = movementCooldown;
            observation.SpecialOneCooldownFrames = specialCooldown;
            observation.LaunchTrajectoryCrossesGap =
                velocityY <= FP64.Zero && !geometry.HasFloorSupport(x) ? 1 : 0;

            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            bool unsupported = !geometry.HasFloorSupport(x);
            unsupportedFrames = unsupported ? unsupportedFrames + 1 : 0;
            // A decision formed while the fighter was still over floor is
            // delivered a reaction window later, by design — "Commit a recovery
            // plan after the tier's delayed recognition of the situation". So a
            // neutral-combat button that arrives just after the fighter crosses
            // the edge is pipeline latency, not a recovery cast. Only inputs the
            // CPU could have decided on while already unsupported count.
            bool decidedWhileUnsupported = unsupportedFrames > reactionWindow;
            // Only a Special pressed while genuinely unsupported is a RECOVERY
            // cast. Once the fighter has drifted back over solid floor the
            // episode is effectively over and ordinary neutral combat — which
            // does use Specials on Medium and Hard — is the correct behaviour.
            if (decidedWhileUnsupported) {
                bool one = frame.IsPressed(GameplayButtons.Special1);
                bool two = frame.IsPressed(GameplayButtons.Special2);
                bool approvedOne = profile.MobilitySpecial == CpuMobilitySpecialSlot.SpecialOne;
                bool approvedTwo = profile.MobilitySpecial == CpuMobilitySpecialSlot.SpecialTwo;
                if ((one && !approvedOne) || (two && !approvedTwo)) result.SpecialEdges++;
                if ((one && approvedOne) || (two && approvedTwo)) result.ApprovedSpecialEdges++;
            }
            // The policy metric is the controller's own per-episode counter, not
            // every movement-ability press: a neutral gap-closer over solid floor
            // is not a recovery activation.
            if (cpu.EpisodeMovementActivations > result.MovementActivations) {
                result.MovementActivations = cpu.EpisodeMovementActivations;
            }
            // The return target is a floor edge on every drill; steering the
            // other way is the failure the contract calls out.
            long targetRaw = NearestFloorEdgeRaw(geometry, x);
            if (frame.MoveX != 0) {
                bool towardTarget = targetRaw >= x.RawValue ? frame.MoveX > 0 : frame.MoveX < 0;
                if (!towardTarget && decidedWhileUnsupported) result.SteeredAwayFromReturn = true;
                facing = frame.MoveX > 0 ? 1 : -1;
            }

            // A kit phase is an action: no jump, ability or drift inputs land
            // while it runs, exactly as the sim's combat lock and its
            // universal-movement slot behave.
            bool inKitPhase = kitPhase != KitNone;
            if (!inKitPhase && frame.IsPressed(GameplayButtons.Jump) && remainingJumps > 0) {
                velocityY = profile.JumpSpeed;
                remainingJumps--;
                floatFrames = 0;
            }
            if (!inKitPhase && frame.IsPressed(GameplayButtons.MovementAbility) && movementCooldown <= 0) {
                movementCooldown = profile.MovementCooldownFrames;
                if (profile.Character == FighterCharacterID.Tubman) {
                    StartLeap(frame, facing, profile, ref kitPhase, ref kitFrames, ref kitDirectionX, ref kitDirectionY);
                    result.LeapsCast++;
                } else if (profile.MovementKind == CpuRecoveryProfile.MovementKindBlink) {
                    StartBlink(frame, facing, ref kitPhase, ref kitFrames, ref kitDirectionX, ref kitDirectionY);
                } else if (profile.MovementKind == CpuRecoveryProfile.MovementKindGust) {
                    // A08 (Package 13 W7a): the gust burst is a kit phase along facing.
                    kitPhase = KitGust;
                    kitFrames = KitMotionRules.ProsperoGustFrames;
                    kitDirectionX = facing;
                    kitDirectionY = 0;
                } else {
                    ApplyMovementAbility(
                        profile, frame, facing, ref x, ref y, ref velocityX, ref velocityY, ref floatFrames);
                }
                if (profile.MovementResetsJump) remainingJumps = profile.MaxJumpCount;
            }
            if (movementCooldown > 0) movementCooldown--;
            if (specialCooldown > 0) specialCooldown--;

            bool gravityApplies = AdvanceKitPhase(
                profile, ref kitPhase, ref kitFrames, kitDirectionX, kitDirectionY,
                ref velocityX, ref velocityY, out bool kitOwnsVelocity);
            if (!kitOwnsVelocity) {
                // Air drift toward the held direction at the loadout's own speed and
                // air control, then integration with the simulation's own constants.
                FP64 targetSpeed = FP64.FromInt(frame.MoveX) / FP64.FromInt(127)
                    * profile.MoveSpeed * profile.AirControl;
                FP64 accelerationStep = profile.MoveSpeed / FP64.FromInt(4);
                velocityX = velocityX < targetSpeed
                    ? FP64.Min(velocityX + accelerationStep, targetSpeed)
                    : FP64.Max(velocityX - accelerationStep, targetSpeed);
            }
            FP64 appliedGravity = floatFrames > 0 ? gravity / FP64.FromInt(3) : gravity;
            if (floatFrames > 0) floatFrames--;
            if (gravityApplies) velocityY += appliedGravity * step;
            FP64 previousY = y;
            x = FP64.Clamp(x + velocityX * step, geometry.LeftWall, geometry.RightWall);
            y += velocityY * step;

            result.FinalX = x;
            result.FinalY = y;
            if (LandedOnAPlatform(geometry, previousY, y, x)) {
                result.Landed = true;
                result.Resolved = true;
                return result;
            }
            if (y <= FP64.Zero && geometry.HasFloorSupport(x)) {
                result.Landed = true;
                result.Resolved = true;
                return result;
            }
            if (y < geometry.BottomBlastZone) {
                result.Resolved = true;
                return result;
            }
        }
        return result;
    }

    private const int KitNone = 0;
    private const int KitBlinkStartup = 1;
    private const int KitBlinkTravel = 2;
    private const int KitBlinkRecovery = 3;
    private const int KitLeapTravel = 4;
    private const int KitLeapEnd = 5;
    private const int KitGust = 6;

    private static void StartBlink(
        PlayerInputFrame frame, int facing,
        ref int kitPhase, ref int kitFrames, ref int directionX, ref int directionY) {
        directionX = frame.MoveX > 30 ? 1 : frame.MoveX < -30 ? -1 : 0;
        directionY = frame.MoveY < -30 ? 1 : frame.MoveY > 30 ? -1 : 0;
        if (directionX == 0 && directionY == 0) directionX = facing;
        kitPhase = KitBlinkStartup;
        kitFrames = KitMotionRules.LightningBlinkStartupFrames;
    }

    /// <summary>Mirrors <c>FighterKitMotion.StartNorthStarLeap</c>: 8-way from the stick, neutral = facing.</summary>
    private static void StartLeap(
        PlayerInputFrame frame, int facing, CpuRecoveryProfile profile,
        ref int kitPhase, ref int kitFrames, ref int directionX, ref int directionY) {
        directionX = frame.MoveX > 30 ? 1 : frame.MoveX < -30 ? -1 : 0;
        directionY = frame.MoveY < -30 ? 1 : frame.MoveY > 30 ? -1 : 0;
        if (directionX == 0 && directionY == 0) directionX = facing;
        kitPhase = KitLeapTravel;
        kitFrames = BlinkTravelFrames(profile);
    }

    /// <summary>
    /// Mirrors <c>FighterKitMotion.Process</c>: the blink hovers through its
    /// startup, translates its authored distance over the translation frames,
    /// then recovers under gravity; North Star Leap travels its authored
    /// distance over its authored frames, then holds one weightless settle
    /// frame. Returns whether gravity applies this tick.
    /// </summary>
    private static bool AdvanceKitPhase(
        CpuRecoveryProfile profile,
        ref int kitPhase, ref int kitFrames, int directionX, int directionY,
        ref FP64 velocityX, ref FP64 velocityY, out bool kitOwnsVelocity) {
        kitOwnsVelocity = kitPhase != KitNone;
        switch (kitPhase) {
            case KitBlinkStartup:
                if (kitFrames <= 0) {
                    kitPhase = KitBlinkTravel;
                    kitFrames = BlinkTravelFrames(profile);
                    goto case KitBlinkTravel;
                }
                velocityX = FP64.Zero;
                velocityY = FP64.Zero;
                kitFrames--;
                return false;
            case KitBlinkTravel:
                if (kitFrames <= 0) {
                    kitPhase = KitBlinkRecovery;
                    kitFrames = KitMotionRules.LightningBlinkRecoveryFrames;
                    velocityX = FP64.Zero;
                    velocityY = FP64.Zero;
                    goto case KitBlinkRecovery;
                }
                FP64 speed = profile.MovementDistance * FP64.FromInt(60) / FP64.FromInt(BlinkTravelFrames(profile));
                if (directionX != 0 && directionY != 0) speed *= FP64.FromDouble(0.70710678118654752);
                velocityX = speed * FP64.FromInt(directionX);
                velocityY = speed * FP64.FromInt(directionY);
                kitFrames--;
                return false;
            case KitBlinkRecovery:
                if (kitFrames <= 0) {
                    kitPhase = KitNone;
                    kitOwnsVelocity = false;
                    return true;
                }
                kitFrames--;
                return true;
            case KitLeapTravel:
                if (kitFrames <= 0) {
                    kitPhase = KitLeapEnd;
                    kitFrames = 1;
                    goto case KitLeapEnd;
                }
                FP64 leapSpeed = profile.MovementDistance * FP64.FromInt(60) / FP64.FromInt(BlinkTravelFrames(profile));
                if (directionX != 0 && directionY != 0) leapSpeed *= FP64.FromDouble(0.70710678118654752);
                velocityX = leapSpeed * FP64.FromInt(directionX);
                velocityY = leapSpeed * FP64.FromInt(directionY);
                kitFrames--;
                return false;
            case KitLeapEnd:
                if (kitFrames <= 0) {
                    kitPhase = KitNone;
                    kitOwnsVelocity = false;
                    return true;
                }
                velocityX = FP64.Zero;
                velocityY = FP64.Zero;
                kitFrames--;
                return false;
            case KitGust:
                // Mirrors FighterKitMotion's GustBurst: the authored forward
                // distance and the rulebook rise over 20 frames, then a normal
                // fall with the momentum spent.
                if (kitFrames <= 0) {
                    velocityX = FP64.Zero;
                    velocityY = FP64.Zero;
                    kitPhase = KitNone;
                    kitOwnsVelocity = false;
                    return true;
                }
                velocityX = profile.MovementDistance * FP64.FromInt(60)
                    / FP64.FromInt(KitMotionRules.ProsperoGustFrames) * FP64.FromInt(directionX);
                velocityY = FP64.FromDouble(KitMotionRules.ProsperoGustRiseUnits * 60 / KitMotionRules.ProsperoGustFrames);
                kitFrames--;
                return false;
            default:
                return true;
        }
    }

    private static int BlinkTravelFrames(CpuRecoveryProfile profile) =>
        profile.MovementDurationFrames > 0 ? profile.MovementDurationFrames : KitMotionRules.LightningBlinkTravelFrames;

    /// <summary>
    /// Mirrors <c>FighterAbilitySystem.ApplyMovement</c> per authored
    /// <c>MovementType</c>, using only the profile's normalized numbers.
    /// </summary>
    private static void ApplyMovementAbility(
        CpuRecoveryProfile profile,
        PlayerInputFrame frame,
        int facing,
        ref FP64 x,
        ref FP64 y,
        ref FP64 velocityX,
        ref FP64 velocityY,
        ref int floatFrames) {
        switch (profile.MovementKind) {
            case CpuRecoveryProfile.MovementKindGlide:
                velocityX = profile.MovementSpeed * FP64.FromInt(facing);
                velocityY = profile.MovementSpeed / FP64.FromInt(2);
                floatFrames = profile.MovementDurationFrames;
                break;
            case CpuRecoveryProfile.MovementKindFloat:
                velocityY = profile.MovementSpeed / FP64.FromInt(2);
                break;
            case CpuRecoveryProfile.MovementKindDash:
                velocityX = profile.MovementSpeed * FP64.FromInt(facing);
                break;
            default:
                int directionX = frame.MoveX > 30 ? 1 : frame.MoveX < -30 ? -1 : 0;
                int directionY = frame.MoveY < -30 ? 1 : frame.MoveY > 30 ? -1 : 0;
                if (directionX == 0 && directionY == 0) directionX = facing;
                x += profile.MovementDistance * FP64.FromInt(directionX);
                if (directionY != 0) {
                    y += profile.MovementDistance * FP64.FromInt(directionY);
                    if (y < FP64.Zero) y = FP64.Zero;
                }
                if (profile.MovementKind == CpuRecoveryProfile.MovementKindWarp) floatFrames = 60;
                break;
        }
    }

    private static bool LandedOnAPlatform(
        FighterStageGeometry geometry, FP64 previousY, FP64 y, FP64 x) {
        for (int index = 0; index < geometry.Platforms.Length; index++) {
            FighterStagePlatform platform = geometry.Platforms[index];
            if (previousY < platform.SurfaceY || y > platform.SurfaceY) continue;
            if (platform.Supports(x)) return true;
        }
        return false;
    }

    private static long NearestFloorEdgeRaw(FighterStageGeometry geometry, FP64 x) {
        bool hasLeft = geometry.TryGetNearestFloorEdge(x, -1, out FP64 left);
        bool hasRight = geometry.TryGetNearestFloorEdge(x, 1, out FP64 right);
        if (hasLeft && hasRight) {
            return FP64.Abs(x - left) < FP64.Abs(right - x) ? left.RawValue : right.RawValue;
        }
        if (hasLeft) return left.RawValue;
        if (hasRight) return right.RawValue;
        return ((geometry.LeftWall + geometry.RightWall) / FP64.FromInt(2)).RawValue;
    }

    /// <summary>
    /// Builds the observation from the <b>real</b> geometry, the same way
    /// <c>FighterCpuController.FillFloorTopology</c> does — support under the
    /// fighter's X, the two nearest pit-facing edges, and the current gap width.
    /// </summary>
    private static CpuDecisionObservation StageObservation(
        FighterStageGeometry geometry, FP64 x, FP64 y) {
        var observation = new CpuDecisionObservation {
            SelfPositionXRaw = x.RawValue,
            SelfPositionYRaw = y.RawValue,
            TargetPositionXRaw = FP64.Zero.RawValue,
            Stocks = 3,
            IsGrounded = 0,
            RemainingJumps = 2,
            SelfCurrentHP = 100,
            SelfMaxHP = 100,
            TargetCurrentHP = 100,
            TargetMaxHP = 100,
            HasStageBounds = 1,
            LeftWallRaw = geometry.LeftWall.RawValue,
            RightWallRaw = geometry.RightWall.RawValue,
            CeilingRaw = geometry.Ceiling.RawValue,
            BottomBlastZoneRaw = geometry.BottomBlastZone.RawValue,
            PlatformCount = geometry.Platforms.Length,
            HasFloorSupportUnderSelf = geometry.HasFloorSupport(x) ? 1 : 0
        };
        if (geometry.Platforms.Length > 0) {
            int nearest = 0;
            FP64 best = FP64.Abs(geometry.Platforms[0].CenterX - x);
            for (int index = 1; index < geometry.Platforms.Length; index++) {
                FP64 distance = FP64.Abs(geometry.Platforms[index].CenterX - x);
                if (distance >= best) continue;
                best = distance;
                nearest = index;
            }
            observation.NearestPlatformCenterXRaw = geometry.Platforms[nearest].CenterX.RawValue;
            observation.NearestPlatformSurfaceYRaw = geometry.Platforms[nearest].SurfaceY.RawValue;
            observation.NearestPlatformHalfWidthRaw = geometry.Platforms[nearest].HalfWidth.RawValue;
        }
        if (!geometry.IsOpenStage) return observation;
        observation.HasFloorSegments = 1;
        if (geometry.TryGetNearestFloorEdge(x, -1, out FP64 leftEdge)) {
            observation.HasFloorEdgeLeft = 1;
            observation.NearestFloorEdgeLeftXRaw = leftEdge.RawValue;
        }
        if (geometry.TryGetNearestFloorEdge(x, 1, out FP64 rightEdge)) {
            observation.HasFloorEdgeRight = 1;
            observation.NearestFloorEdgeRightXRaw = rightEdge.RawValue;
        }
        if (observation.HasFloorSupportUnderSelf == 0
            && observation.HasFloorEdgeLeft != 0 && observation.HasFloorEdgeRight != 0) {
            observation.CurrentGapWidthRaw = (rightEdge - leftEdge).RawValue;
        }
        return observation;
    }

    private struct DrillResult {
        /// <summary>Specials pressed while unsupported that the profile does NOT approve.</summary>
        public int SpecialEdges;
        /// <summary>Presses of the profile's approved mobility Special while unsupported.</summary>
        public int ApprovedSpecialEdges;
        /// <summary>North Star Leaps the harness actually executed.</summary>
        public int LeapsCast;
        public int MovementActivations;
        public bool SteeredAwayFromReturn;
        public bool Landed;
        /// <summary>The drill ended in a landing or a KO rather than an endless hover.</summary>
        public bool Resolved;
        public FP64 FinalX;
        public FP64 FinalY;
    }
}
