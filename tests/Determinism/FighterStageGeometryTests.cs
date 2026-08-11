using System.Collections.Generic;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using static GdUnit4.Assertions;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.Tests.Determinism;

/// <summary>
/// Locks the ten authored production stage geometries. `FighterStageGeometry` is
/// deliberately code-authored (Package 6 plan §2.2): it is deterministic,
/// engine-free, identical on every peer, and never enters a rollback snapshot.
/// These tests are the only thing standing between a careless edit and a silent
/// desync, so every dossier number is pinned here, every platform gets a real
/// jump-landing run through the fixed-point integrator, and every stage gets a
/// two-simulation hash-identity run.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStageGeometryTests {

    /// <summary>The locked §4 dossier: id, walls, platforms, hazard anchors, orb anchors.</summary>
    private sealed record StageDossier(
        string StageID,
        double Wall,
        (double CenterX, double SurfaceY, double HalfWidth)[] Platforms,
        double[] HazardAnchorXs,
        (double X, double Y)[] OrbAnchors);

    private static readonly StageDossier[] Dossiers = {
        new("florence_workshop", 9,
            new[] { (-4.0, 2.4, 1.6), (4.0, 2.4, 1.6) },
            new[] { -6.0, 0.0, 6.0 },
            new[] { (-4.0, 2.9), (0.0, 0.5), (4.0, 2.9) }),
        new("orleans_vanguard", 9,
            new[] { (-3.5, 2.0, 1.4), (3.5, 2.0, 1.4), (0.0, 4.0, 1.4) },
            new[] { -8.0, 8.0 },
            new[] { (-3.5, 2.5), (3.5, 2.5), (0.0, 4.5) }),
        new("chicago_exposition", 10,
            new[] { (-6.0, 3.2, 1.5), (6.0, 3.2, 1.5) },
            new[] { 0.0 },
            new[] { (-6.0, 3.7), (6.0, 3.7), (0.0, 0.5) }),
        new("paris_bastille", 9,
            new[] { (-4.0, 2.6, 2.0), (4.0, 2.6, 2.0) },
            new[] { -6.0, 0.0, 6.0 },
            new[] { (-4.0, 3.1), (4.0, 3.1), (0.0, 0.5) }),
        new("vesuvius_caldera", 8,
            new[] { (-4.5, 2.0, 1.1), (3.0, 3.5, 1.1) },
            new[] { -6.0, -2.0, 2.0, 6.0 },
            new[] { (-4.5, 2.5), (3.0, 4.0), (0.0, 0.5) }),
        new("nassau_flagship", 9,
            new[] { (-4.0, 2.8, 1.8), (4.0, 2.8, 1.8) },
            new[] { -5.0, 0.0, 5.0 },
            new[] { (-4.0, 3.3), (4.0, 3.3), (0.0, 0.5) }),
        new("alexandria_chambers", 9,
            new[] { (-4.0, 1.6, 1.3), (4.0, 1.6, 1.3) },
            new[] { -6.0, 0.0, 6.0 },
            new[] { (-4.0, 2.1), (4.0, 2.1), (0.0, 0.5) }),
        new("berlin_wall", 10,
            new[] { (-6.5, 3.6, 1.3), (6.5, 3.6, 1.3) },
            new[] { -4.0, 0.0, 4.0 },
            new[] { (-6.5, 4.1), (6.5, 4.1), (0.0, 0.5) }),
        // The tiring-house balcony dropped 4.4 → 4.0 with the 2026-08-10 jump
        // retune (feel batch §2.2): the double-jump ceiling is ≈4.21 now.
        new("globe_theatre", 9,
            new[] { (-5.0, 2.4, 1.4), (5.0, 2.4, 1.4), (0.0, 4.0, 1.2) },
            new[] { -5.0, 0.0, 5.0 },
            new[] { (-5.0, 2.9), (5.0, 2.9), (0.0, 4.5) }),
        new("gettysburg_ridge", 10,
            new[] { (-4.5, 1.8, 1.2), (4.5, 1.8, 1.2) },
            new[] { -5.0, 0.0, 5.0 },
            new[] { (-4.5, 2.3), (4.5, 2.3), (0.0, 0.5) })
    };

    [TestCase]
    public void DefaultGeometryMatchesTheLegacyFlatArena() {
        FighterStageGeometry geometry = FighterStageGeometry.Default;
        AssertThat(geometry.LeftWall.RawValue).IsEqual(FP64.FromInt(-10).RawValue);
        AssertThat(geometry.RightWall.RawValue).IsEqual(FP64.FromInt(10).RawValue);
        AssertThat(geometry.Ceiling.RawValue).IsEqual(FP64.FromInt(9).RawValue);
        AssertThat(geometry.BottomBlastZone.RawValue).IsEqual(FP64.FromInt(-5).RawValue);
        AssertThat(geometry.SpawnDistance).IsEqual(4);
        AssertThat(geometry.Platforms.Length).IsEqual(0);
        AssertThat(geometry.HazardAnchorXs.Length).IsEqual(0);
        AssertThat(geometry.OrbAnchors.Length).IsEqual(0);
    }

    /// <summary>
    /// Package 6 A1 replaced the previous assertion (`orleans_vanguard` fell back to
    /// Default). Every catalog stage is authored now, so only a genuinely unknown ID
    /// may fall back — a stage silently reverting to the flat arena is the exact
    /// regression this guards.
    /// </summary>
    [TestCase]
    public void UnknownStagesFallBackToTheDefaultGeometry() {
        AssertThat(FighterStageGeometry.ForStage("")).IsSame(FighterStageGeometry.Default);
        AssertThat(FighterStageGeometry.ForStage(null)).IsSame(FighterStageGeometry.Default);
        AssertThat(FighterStageGeometry.ForStage("not_a_stage")).IsSame(FighterStageGeometry.Default);
        AssertThat(FighterStageGeometry.ForStage("pompeii_caldera")).IsSame(FighterStageGeometry.Default);
        AssertThat(FighterStageGeometry.ForStage("egypt_chambers")).IsSame(FighterStageGeometry.Default);
        AssertThat(FighterStageGeometry.ForStage("Florence_Workshop")).IsSame(FighterStageGeometry.Default);
    }

    [TestCase]
    public void EveryCatalogStageResolvesToItsOwnAuthoredGeometry() {
        AssertThat(FighterStageGeometry.AllAuthored.Length).IsEqual(10);
        var seen = new HashSet<string>();
        foreach (StageDossier dossier in Dossiers) {
            FighterStageGeometry geometry = FighterStageGeometry.ForStage(dossier.StageID);
            AssertThat(geometry).IsNotSame(FighterStageGeometry.Default);
            AssertThat(geometry.StageID).IsEqual(dossier.StageID);
            AssertThat(seen.Add(dossier.StageID)).IsTrue();
        }
        foreach (FighterStageGeometry geometry in FighterStageGeometry.AllAuthored) {
            AssertThat(seen.Contains(geometry.StageID)).IsTrue();
            AssertThat(FighterStageGeometry.ForStage(geometry.StageID)).IsSame(geometry);
        }
    }

    [TestCase]
    public void EveryAuthoredStageMatchesItsLockedDossier() {
        foreach (StageDossier dossier in Dossiers) {
            FighterStageGeometry geometry = FighterStageGeometry.ForStage(dossier.StageID);
            AssertThat(geometry.LeftWall.RawValue).IsEqual(FP64.FromDouble(-dossier.Wall).RawValue);
            AssertThat(geometry.RightWall.RawValue).IsEqual(FP64.FromDouble(dossier.Wall).RawValue);

            AssertThat(geometry.Platforms.Length).IsEqual(dossier.Platforms.Length);
            for (int index = 0; index < dossier.Platforms.Length; index++) {
                (double centerX, double surfaceY, double halfWidth) = dossier.Platforms[index];
                AssertThat(geometry.Platforms[index].CenterX.RawValue)
                    .IsEqual(FP64.FromDouble(centerX).RawValue);
                AssertThat(geometry.Platforms[index].SurfaceY.RawValue)
                    .IsEqual(FP64.FromDouble(surfaceY).RawValue);
                AssertThat(geometry.Platforms[index].HalfWidth.RawValue)
                    .IsEqual(FP64.FromDouble(halfWidth).RawValue);
            }

            AssertThat(geometry.HazardAnchorXs.Length).IsEqual(dossier.HazardAnchorXs.Length);
            for (int index = 0; index < dossier.HazardAnchorXs.Length; index++) {
                AssertThat(geometry.HazardAnchorXs[index].RawValue)
                    .IsEqual(FP64.FromDouble(dossier.HazardAnchorXs[index]).RawValue);
            }

            AssertThat(geometry.OrbAnchors.Length).IsEqual(dossier.OrbAnchors.Length);
            for (int index = 0; index < dossier.OrbAnchors.Length; index++) {
                (double x, double y) = dossier.OrbAnchors[index];
                AssertThat(geometry.OrbAnchors[index].x.RawValue).IsEqual(FP64.FromDouble(x).RawValue);
                AssertThat(geometry.OrbAnchors[index].y.RawValue).IsEqual(FP64.FromDouble(y).RawValue);
            }
        }
    }

    /// <summary>
    /// Shared contract every stage inherits: ceiling 9, blast zone -5, spawn
    /// distance 4, at least two platforms (zero platforms silently reverts the
    /// floor to drop-through), spawns inside the walls, anchors inside the walls.
    /// </summary>
    [TestCase]
    public void EveryAuthoredStageSharesTheCommonBoundsContract() {
        foreach (FighterStageGeometry geometry in FighterStageGeometry.AllAuthored) {
            string id = geometry.StageID;
            AssertThat(geometry.Ceiling.RawValue).IsEqual(FP64.FromInt(9).RawValue);
            AssertThat(geometry.BottomBlastZone.RawValue).IsEqual(FP64.FromInt(-5).RawValue);
            AssertThat(geometry.SpawnDistance).IsEqual(4);
            AssertThat(geometry.LeftWall.RawValue).IsEqual((-geometry.RightWall).RawValue);
            AssertWithMessage(id, geometry.Platforms.Length >= 2);
            AssertWithMessage(id, geometry.HazardAnchorXs.Length >= 1);
            AssertWithMessage(id, geometry.OrbAnchors.Length >= 3);
            AssertWithMessage(id, FP64.FromInt(geometry.SpawnDistance) < geometry.RightWall);

            foreach (FighterStagePlatform platform in geometry.Platforms) {
                AssertWithMessage(id, platform.SurfaceY > FP64.Zero);
                AssertWithMessage(id, platform.SurfaceY < geometry.Ceiling);
                AssertWithMessage(id, platform.HalfWidth > FP64.Zero);
                AssertWithMessage(id, platform.CenterX - platform.HalfWidth > geometry.LeftWall);
                AssertWithMessage(id, platform.CenterX + platform.HalfWidth < geometry.RightWall);
            }
            foreach (FP64 anchorX in geometry.HazardAnchorXs) {
                AssertWithMessage(id, anchorX >= geometry.LeftWall && anchorX <= geometry.RightWall);
            }
            foreach (FPVector2 anchor in geometry.OrbAnchors) {
                AssertWithMessage(id, anchor.x > geometry.LeftWall && anchor.x < geometry.RightWall);
                AssertWithMessage(id, anchor.y > FP64.Zero && anchor.y < geometry.Ceiling);
            }
        }
    }

    /// <summary>
    /// Orb anchors sit exactly 0.5 above their platform surface (or above the
    /// floor for the centre anchor), so an orb is always standable-on rather than
    /// buried in geometry.
    /// </summary>
    [TestCase]
    public void EveryOrbAnchorSitsHalfAUnitAboveItsSupportingSurface() {
        FP64 clearance = FP64.FromDouble(0.5);
        foreach (FighterStageGeometry geometry in FighterStageGeometry.AllAuthored) {
            foreach (FPVector2 anchor in geometry.OrbAnchors) {
                bool supported = anchor.y.RawValue == clearance.RawValue;
                foreach (FighterStagePlatform platform in geometry.Platforms) {
                    if (anchor.y.RawValue == (platform.SurfaceY + clearance).RawValue
                        && platform.Supports(anchor.x)) {
                        supported = true;
                    }
                }
                AssertWithMessage(geometry.StageID, supported);
            }
        }
    }

    /// <summary>
    /// Package 6 plan §2.12: landing compares `Position.y` against `SurfaceY` with
    /// exact fixed-point equality, so every authored surface height must be
    /// reachable and landable by the real integrator. This drives the fixed-point
    /// simulation to walk to each platform, jump (double-jumping when needed), and
    /// asserts an exact landing on the authored surface.
    /// </summary>
    [TestCase]
    public void EveryAuthoredPlatformIsReachableAndLandableByJumping() {
        int seed = 500;
        foreach (FighterStageGeometry geometry in FighterStageGeometry.AllAuthored) {
            for (int index = 0; index < geometry.Platforms.Length; index++) {
                bool landed = JumpAndLandOnPlatform(geometry, index, seed++);
                AssertWithMessage($"{geometry.StageID} platform {index}", landed);
            }
        }
    }

    [TestCase]
    public void EveryAuthoredStageRunsIdenticallyAcrossTwoSimulations() {
        FighterMatchRules rules = new(
            (int)MatchMode.Hybrid,
            itemsEnabled: true,
            itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: true,
            hazardFrequency: (int)HazardTriggerFrequency.High,
            stageHazardTypeID: 1);

        int seed = 900;
        foreach (FighterStageGeometry geometry in FighterStageGeometry.AllAuthored) {
            var first = new FighterSimulation(seed: seed, rules: rules, stageGeometry: geometry);
            var second = new FighterSimulation(seed: seed, rules: rules, stageGeometry: geometry);
            seed++;
            for (int tick = 0; tick < 700; tick++) {
                PlayerInputFrame p1 = InputFor(0, tick);
                PlayerInputFrame p2 = InputFor(1, tick);
                AssertWithMessage(
                    $"{geometry.StageID} tick {tick}",
                    first.Advance(p1, p2) == second.Advance(p1, p2));
            }
        }
    }

    /// <summary>Every authored stage keeps hazards and orbs on its authored anchors.</summary>
    [TestCase]
    public void EveryAuthoredStageSpawnsHazardsAndOrbsOnItsAnchorsOnly() {
        int seed = 1300;
        foreach (FighterStageGeometry geometry in FighterStageGeometry.AllAuthored) {
            FighterMatchRules rules = new(
                (int)MatchMode.Hybrid,
                itemsEnabled: true,
                itemFrequency: (int)ChronalOrbFrequency.High,
                hazardsEnabled: true,
                hazardFrequency: (int)HazardTriggerFrequency.High,
                stageHazardTypeID: 1);
            var simulation = new FighterSimulation(seed: seed++, rules: rules, stageGeometry: geometry);

            // The first orb lands on the High-frequency 660-frame boundary, while
            // both fighters are still idle at their spawns and cannot have walked
            // into it.
            for (int tick = 0; tick < 665; tick++) simulation.Advance(default, default);
            AssertThat(simulation.TryGetFirstOrb(out FighterOrbComponent orb)).IsTrue();
            bool orbOnAnchor = false;
            foreach (FPVector2 anchor in geometry.OrbAnchors) {
                if (orb.Position.x.RawValue == anchor.x.RawValue
                    && orb.Position.y.RawValue == anchor.y.RawValue) orbOnAnchor = true;
            }
            AssertWithMessage(geometry.StageID, orbOnAnchor);

            // The first hazard lands on the 1800-frame boundary and is still
            // telegraphing (warning is 90 frames, 120 on Gettysburg).
            for (int tick = 665; tick < 1805; tick++) simulation.Advance(default, default);
            AssertThat(simulation.TryGetFirstHazard(out FighterHazardComponent hazard)).IsTrue();
            AssertWithMessage(geometry.StageID, hazard.Phase == 0);
            bool hazardOnAnchor = false;
            foreach (FP64 anchorX in geometry.HazardAnchorXs) {
                if (hazard.Position.x.RawValue == anchorX.RawValue) hazardOnAnchor = true;
            }
            AssertWithMessage(geometry.StageID, hazardOnAnchor);
        }
    }

    [TestCase]
    public void FighterLandsOnAFlorencePlatformAfterAJump() {
        var simulation = new FighterSimulation(seed: 11, stageGeometry: FighterStageGeometry.Florence);
        // Player one spawns at x = -4, directly under the left gear platform.
        int landedTick = RiseOntoPlatform(simulation, FP64.FromDouble(2.4), 0);
        AssertThat(landedTick >= 0).OverrideFailureMessage(
            "the fighter never came to rest on the Florence gear platform.").IsTrue();
    }

    [TestCase]
    public void DropThroughLeavesThePlatformAndLandsOnTheSolidBaseFloor() {
        var simulation = new FighterSimulation(seed: 12, stageGeometry: FighterStageGeometry.Florence);
        int tick = RiseOntoPlatform(simulation, FP64.FromDouble(2.4), 0);
        AssertThat(tick >= 0).IsTrue();
        simulation.TryGetFighter(0, out FighterStateComponent onPlatform);
        AssertThat(onPlatform.Position.y.RawValue).IsEqual(FP64.FromDouble(2.4).RawValue);

        // Down + jump drops through the one-way platform.
        tick++;
        simulation.Advance(
            new PlayerInputFrame {
                Tick = (uint)tick,
                Held = GameplayButtons.Down | GameplayButtons.Jump,
                Pressed = GameplayButtons.Jump
            },
            Frame(tick, 0, GameplayButtons.None));

        bool landedOnFloor = false;
        for (tick++; tick < 400; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            simulation.TryGetFighter(0, out FighterStateComponent state);
            if (state.IsGrounded != 0 && state.Position.y.RawValue == FP64.Zero.RawValue) {
                landedOnFloor = true;
                break;
            }
        }
        AssertThat(landedOnFloor).IsTrue();
        // The solid base floor never costs a stock.
        simulation.TryGetFighter(0, out FighterStateComponent grounded);
        AssertThat(grounded.Stocks).IsEqual(3);
    }

    [TestCase]
    public void WalkingOffAPlatformEdgeRemovesGroundSupport() {
        var simulation = new FighterSimulation(seed: 13, stageGeometry: FighterStageGeometry.Florence);
        int tick = RiseOntoPlatform(simulation, FP64.FromDouble(2.4), 0);
        AssertThat(tick >= 0).IsTrue();

        bool becameAirborne = false;
        for (tick++; tick < 400; tick++) {
            simulation.Advance(Frame(tick, 127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            simulation.TryGetFighter(0, out FighterStateComponent state);
            if (state.IsGrounded == 0 && state.Position.y > FP64.Zero) {
                becameAirborne = true;
                break;
            }
        }
        AssertThat(becameAirborne).IsTrue();
    }

    [TestCase]
    public void FlorenceHazardsSpawnOnAuthoredAnchorsOnly() {
        FighterMatchRules rules = new(
            (int)MatchMode.Hybrid,
            itemsEnabled: false,
            itemFrequency: 0,
            hazardsEnabled: true,
            hazardFrequency: (int)HazardTriggerFrequency.High,
            stageHazardTypeID: 1);
        var simulation = new FighterSimulation(seed: 21, rules: rules, stageGeometry: FighterStageGeometry.Florence);

        for (int tick = 0; tick < 1805; tick++) simulation.Advance(default, default);

        AssertThat(simulation.TryGetFirstHazard(out FighterHazardComponent hazard)).IsTrue();
        bool onAnchor = false;
        foreach (FP64 anchorX in FighterStageGeometry.Florence.HazardAnchorXs) {
            if (hazard.Position.x.RawValue == anchorX.RawValue) onAnchor = true;
        }
        AssertThat(onAnchor).IsTrue();
    }

    [TestCase]
    public void FlorenceOrbsSpawnOnAuthoredAnchorsOnly() {
        FighterMatchRules rules = new(
            (int)MatchMode.Hybrid,
            itemsEnabled: true,
            itemFrequency: (int)ChronalOrbFrequency.High,
            hazardsEnabled: false,
            hazardFrequency: 0,
            stageHazardTypeID: 1);
        var simulation = new FighterSimulation(seed: 22, rules: rules, stageGeometry: FighterStageGeometry.Florence);

        for (int tick = 0; tick < 665; tick++) simulation.Advance(default, default);

        AssertThat(simulation.TryGetFirstOrb(out FighterOrbComponent orb)).IsTrue();
        bool onAnchor = false;
        foreach (FPVector2 anchor in FighterStageGeometry.Florence.OrbAnchors) {
            if (orb.Position.x.RawValue == anchor.x.RawValue && orb.Position.y.RawValue == anchor.y.RawValue) {
                onAnchor = true;
            }
        }
        AssertThat(onAnchor).IsTrue();
    }

    [TestCase]
    public void FlorenceMatchesRunIdenticallyAcrossTwoSimulations() {
        var first = new FighterSimulation(seed: 33, stageGeometry: FighterStageGeometry.Florence);
        var second = new FighterSimulation(seed: 33, stageGeometry: FighterStageGeometry.Florence);

        for (int tick = 0; tick < 600; tick++) {
            PlayerInputFrame p1 = InputFor(0, tick);
            PlayerInputFrame p2 = InputFor(1, tick);
            AssertThat(first.Advance(p1, p2)).IsEqual(second.Advance(p1, p2));
        }
    }

    /// <summary>
    /// Climbs player one from the base floor onto the surface directly overhead,
    /// spending the second jump the instant the first stalls, and returns the tick
    /// it came to rest there (−1 if it never did). The 2026-08-10 jump retune
    /// (feel batch §2.2) put the default loadout's single-jump apex at ≈2.11
    /// units, below Florence's 2.4 gear platforms, so these platform cases climb
    /// with the jump chain every kit now carries rather than one hop.
    /// </summary>
    private static int RiseOntoPlatform(FighterSimulation simulation, FP64 surfaceY, int startTick) {
        bool spentAirJump = false;
        for (int tick = startTick; tick < startTick + 200; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
            if (fighter.IsGrounded != 0 && fighter.Position.y.RawValue == surfaceY.RawValue) return tick;

            GameplayButtons buttons = GameplayButtons.None;
            if (fighter.IsGrounded != 0) {
                spentAirJump = false;
                buttons = GameplayButtons.Jump;
            } else if (!spentAirJump
                       && fighter.Velocity.y < FP64.Zero
                       && fighter.RemainingJumps > 0) {
                buttons = GameplayButtons.Jump;
                spentAirJump = true;
            }
            simulation.Advance(Frame(tick, 0, buttons), Frame(tick, 0, GameplayButtons.None));
        }
        return -1;
    }

    /// <summary>
    /// Walks player one to a platform's span and jumps (spending the second jump at
    /// the apex when a single jump cannot reach), then reports whether the fighter
    /// came to rest exactly on the authored surface. Player two is steered to the
    /// far wall so pushbox jostling cannot decide the outcome.
    /// </summary>
    private static bool JumpAndLandOnPlatform(FighterStageGeometry geometry, int platformIndex, int seed) {
        FighterStagePlatform platform = geometry.Platforms[platformIndex];
        var simulation = new FighterSimulation(
            seed: seed, rules: FighterMatchRules.Disabled, stageGeometry: geometry);
        // Player two always walks to the right wall. Sending it toward the target
        // platform instead deadlocks the pair in the middle of the stage: two
        // fighters walking into each other jostle to a standstill.
        const sbyte opponentAxis = 127;
        FP64 stopBand = FP64.FromDouble(0.15);

        for (int tick = 0; tick < 900; tick++) {
            if (!simulation.TryGetFighter(0, out FighterStateComponent fighter)) return false;
            if (fighter.IsGrounded != 0
                && fighter.Position.y.RawValue == platform.SurfaceY.RawValue
                && platform.Supports(fighter.Position.x)) {
                return true;
            }

            FP64 dx = platform.CenterX - fighter.Position.x;
            sbyte moveX = dx > stopBand ? (sbyte)127 : dx < -stopBand ? (sbyte)-127 : (sbyte)0;
            GameplayButtons buttons = GameplayButtons.None;
            bool below = fighter.Position.y < platform.SurfaceY;
            bool underSpan = platform.Supports(fighter.Position.x);
            if (below && underSpan) {
                bool canGroundJump = fighter.IsGrounded != 0;
                bool canAirJump = fighter.IsGrounded == 0
                    && fighter.Velocity.y < FP64.Zero
                    && fighter.RemainingJumps > 0;
                if (canGroundJump || canAirJump) buttons = GameplayButtons.Jump;
            }

            simulation.Advance(
                new PlayerInputFrame {
                    Tick = (uint)tick,
                    MoveX = moveX,
                    Held = buttons,
                    Pressed = buttons
                },
                new PlayerInputFrame { Tick = (uint)tick, MoveX = opponentAxis });
        }
        return false;
    }

    private static void AssertWithMessage(string context, bool condition) {
        if (!condition) AssertThat($"FAILED: {context}").IsEqual("");
    }

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = pressed,
        Pressed = pressed
    };

    private static PlayerInputFrame InputFor(int playerID, int tick) {
        sbyte axis = (sbyte)(((tick + playerID * 17) % 7 - 3) * 40);
        GameplayButtons buttons = GameplayButtons.None;
        if (tick % 47 == 0) buttons |= GameplayButtons.Jump;
        if (tick % 31 == 0) buttons |= GameplayButtons.BasicAttack;
        return new PlayerInputFrame {
            Tick = (uint)tick,
            MoveX = axis,
            Held = buttons,
            Pressed = buttons
        };
    }
}
