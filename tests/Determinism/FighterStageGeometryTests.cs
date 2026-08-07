using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using static GdUnit4.Assertions;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.Tests.Determinism;

[TestSuite]
[RequireGodotRuntime]
public class FighterStageGeometryTests {

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

    [TestCase]
    public void UnknownStagesFallBackToTheDefaultGeometry() {
        AssertThat(FighterStageGeometry.ForStage("orleans_vanguard")).IsSame(FighterStageGeometry.Default);
        AssertThat(FighterStageGeometry.ForStage("")).IsSame(FighterStageGeometry.Default);
        AssertThat(FighterStageGeometry.ForStage(null)).IsSame(FighterStageGeometry.Default);
    }

    [TestCase]
    public void FlorenceGeometryDefinesUniquePlatformsBoundsAndAnchors() {
        FighterStageGeometry florence = FighterStageGeometry.ForStage("florence_workshop");
        AssertThat(florence).IsSame(FighterStageGeometry.Florence);
        AssertThat(florence.StageID).IsEqual("florence_workshop");
        AssertThat(florence.LeftWall.RawValue).IsEqual(FP64.FromInt(-9).RawValue);
        AssertThat(florence.RightWall.RawValue).IsEqual(FP64.FromInt(9).RawValue);
        AssertThat(florence.Platforms.Length).IsEqual(2);
        AssertThat(florence.Platforms[0].CenterX.RawValue).IsEqual(FP64.FromInt(-4).RawValue);
        AssertThat(florence.Platforms[0].SurfaceY.RawValue).IsEqual(FP64.FromDouble(2.4).RawValue);
        AssertThat(florence.Platforms[1].CenterX.RawValue).IsEqual(FP64.FromInt(4).RawValue);
        AssertThat(florence.HazardAnchorXs.Length).IsEqual(3);
        AssertThat(florence.OrbAnchors.Length).IsEqual(3);
    }

    [TestCase]
    public void FighterLandsOnAFlorencePlatformAfterAJump() {
        var simulation = new FighterSimulation(seed: 11, stageGeometry: FighterStageGeometry.Florence);
        // Player one spawns at x = -4, directly under the left gear platform.
        simulation.Advance(Frame(0, 0, GameplayButtons.Jump), Frame(0, 0, GameplayButtons.None));

        bool landedOnPlatform = false;
        for (int tick = 1; tick < 120; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
            if (fighter.IsGrounded != 0 && fighter.Position.y.RawValue == FP64.FromDouble(2.4).RawValue) {
                landedOnPlatform = true;
                break;
            }
        }
        AssertThat(landedOnPlatform).IsTrue();
    }

    [TestCase]
    public void DropThroughLeavesThePlatformAndLandsOnTheSolidBaseFloor() {
        var simulation = new FighterSimulation(seed: 12, stageGeometry: FighterStageGeometry.Florence);
        simulation.Advance(Frame(0, 0, GameplayButtons.Jump), Frame(0, 0, GameplayButtons.None));
        int tick = 1;
        for (; tick < 120; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            simulation.TryGetFighter(0, out FighterStateComponent state);
            if (state.IsGrounded != 0 && state.Position.y > FP64.Zero) break;
        }
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
        simulation.Advance(Frame(0, 0, GameplayButtons.Jump), Frame(0, 0, GameplayButtons.None));
        int tick = 1;
        for (; tick < 120; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            simulation.TryGetFighter(0, out FighterStateComponent state);
            if (state.IsGrounded != 0 && state.Position.y > FP64.Zero) break;
        }

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
        if (tick % 97 == 0) buttons |= GameplayButtons.Dash;
        return new PlayerInputFrame {
            Tick = (uint)tick,
            MoveX = axis,
            Held = buttons,
            Pressed = buttons
        };
    }
}
