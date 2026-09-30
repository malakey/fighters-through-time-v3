using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7a (E04) — Relativity Warp as a spacetime fold in the sim, on the
/// AUTHORED loadout: a 10-frame startup in place (a kit phase of component 305,
/// no new component), then an instant relocation of up to 4 units along the held
/// direction, shortened to the farthest point inside the stage walls; a hit in
/// the startup cancels it with the cooldown spent.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RelativityWarpFoldSimTests {

    [TestCase]
    public void TheFoldHoldsTenFramesThenRelocatesFourUnitsInstantly() {
        var simulation = new FighterSimulation(
            ProjectileBurstSimTests.Authored("einstein"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 7301, spawnDistance: 4, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.MovementAbility, -127), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.UniversalMovementState).IsEqual(FighterKitMotion.WarpFoldStartup);
        // Where he stands when the fold begins (the press tick's own step moved him).
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent start)).IsTrue();

        for (int tick = 1; tick <= KitMotionRules.RelativityWarpStartupFrames; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            simulation.TryGetFighter(0, out FighterStateComponent holding);
            AssertThat(holding.Position.x)
                .OverrideFailureMessage($"The fold moved during its startup (tick {tick}).")
                .IsEqual(start.Position.x);
        }
        // The startup has run its 10 frames; the next tick relocates him —
        // exactly 4 units, with no travel frames in between.
        simulation.Advance(
            Frame(KitMotionRules.RelativityWarpStartupFrames + 1, GameplayButtons.None),
            Frame(KitMotionRules.RelativityWarpStartupFrames + 1, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent folded)).IsTrue();
        AssertThat(folded.Position.x).IsEqual(start.Position.x - FP64.FromInt(4));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent after)).IsTrue();
        AssertThat(after.UniversalMovementState).IsEqual((int)UniversalMovementPhase.None);
    }

    [TestCase]
    public void TheFoldShortensToTheFarthestPointInsideTheWall() {
        // Einstein spawns at -8 on the ±10 legacy arena: a left fold has only 2 units.
        var simulation = new FighterSimulation(
            ProjectileBurstSimTests.Authored("einstein"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 7302, spawnDistance: 8, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.MovementAbility, -127), Frame(0, GameplayButtons.None));
        for (int tick = 1; tick <= KitMotionRules.RelativityWarpStartupFrames + 1; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent folded)).IsTrue();
        AssertThat(folded.Position.x).IsEqual(FighterStageGeometry.Default.LeftWall);

        // The pure destination rule the movement system reads.
        FPVector2 origin = new(FP64.FromInt(-8), FP64.Zero);
        FPVector2 upLeft = FighterKitMotion.WarpFoldDestination(
            FighterStageGeometry.Default, in origin, (-1 + 1) + 3 * (1 + 1), FP64.FromInt(4));
        AssertThat(upLeft.x).IsEqual(FighterStageGeometry.Default.LeftWall);
        AssertThat(upLeft.y > FP64.Zero && upLeft.y < FP64.FromInt(4)).IsTrue();
    }

    [TestCase]
    public void AHitDuringTheStartupCancelsTheFoldWithTheCooldownSpent() {
        // Player two is also the authored Einstein and fires E=mc² first; the shot
        // arrives inside player one's 10-frame fold startup.
        var simulation = new FighterSimulation(
            ProjectileBurstSimTests.Authored("einstein"), ProjectileBurstSimTests.Authored("einstein"),
            seed: 7303, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.None), Frame(0, GameplayButtons.Special1));
        simulation.Advance(Frame(1, GameplayButtons.None), Frame(1, GameplayButtons.None));
        simulation.Advance(Frame(2, GameplayButtons.MovementAbility, 127), Frame(2, GameplayButtons.None));
        FP64 furthestRight = FP64.FromInt(-100);
        bool everHit = false;
        for (int tick = 3; tick < 40; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            simulation.TryGetFighter(0, out FighterStateComponent einstein);
            if (einstein.Position.x > furthestRight) furthestRight = einstein.Position.x;
            if (einstein.CurrentHP < 100) everHit = true;
        }
        AssertThat(everHit).IsTrue();
        // A completed fold would have put him 4 units right of -2, at +2.
        AssertThat(furthestRight < FP64.FromInt(0)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.MovementCooldownFrames > 0).IsTrue();
        AssertThat(runtime.UniversalMovementState).IsEqual((int)UniversalMovementPhase.None);
    }

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed, sbyte moveX = 0, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };
}
