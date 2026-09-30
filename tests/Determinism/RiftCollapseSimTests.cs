using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7a — Einstein's Relativity Rift in the sim, on the AUTHORED
/// loadout: E03 placement (thrown 5 units ahead, 1.5-unit radius) and the 0.5 s
/// Time Dilation linger, and E01 Rift Collapse — an E=mc² burst inside his own
/// active rift ends it, pulls the opponent to its centre over 6 frames and
/// launches them upward with a 0-damage hit that earns no meter; a blocker is
/// never pulled.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RiftCollapseSimTests {

    [TestCase]
    public void AnEmcSquaredBurstInsideTheRiftCollapsesItPullsAndLaunches() {
        // Einstein at -3, the opponent at +3: the rift lands at +2, over them.
        var simulation = new FighterSimulation(
            ProjectileBurstSimTests.Authored("einstein"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 7201, spawnDistance: 3, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent rift)).IsTrue();
        AssertThat(rift.Position.x).IsEqual(FP64.FromInt(2));
        AssertThat(rift.HalfExtents.x).IsEqual(FP64.FromDouble(KitMotionRules.RelativityRiftRadiusUnits));

        simulation.Advance(Frame(1, GameplayButtons.Special1), Frame(1, GameplayButtons.None));
        int burstTick = -1;
        FP64 meterAtBurst = FP64.Zero;
        for (int tick = 2; tick < 40 && burstTick < 0; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            simulation.TryGetFirstZone(out FighterZoneComponent live);
            if (live.CollapseFramesRemaining > 0) {
                burstTick = tick;
                simulation.TryGetFighter(0, out FighterStateComponent atBurst);
                meterAtBurst = atBurst.Influence;
            }
        }
        AssertThat(burstTick >= 0).OverrideFailureMessage("The burst never collapsed the rift.").IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent hitByBurst)).IsTrue();
        int hpAfterBurst = hitByBurst.CurrentHP;

        // The pull runs out over the remaining collapse frames; then the rift is gone.
        for (int tick = burstTick + 1; tick <= burstTick + KitMotionRules.RiftCollapsePullFrames; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent launched)).IsTrue();
        // 0 damage, launched upward from (near) the rift centre.
        AssertThat(launched.CurrentHP).IsEqual(hpAfterBurst);
        AssertThat(launched.Velocity.y > FP64.Zero || launched.Position.y > FP64.Zero).IsTrue();
        AssertThat(FP64.Abs(launched.Position.x - FP64.FromInt(2)) < FP64.FromDouble(0.5)).IsTrue();
        AssertThat(launched.HitstunFrames > 0).IsTrue();
        // The collapse launch earns the caster no meter of its own.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.Influence).IsEqual(meterAtBurst);
    }

    [TestCase]
    public void ABlockerIsNeverPulledButTheRiftStillEnds() {
        var simulation = new FighterSimulation(
            ProjectileBurstSimTests.Authored("einstein"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 7202, spawnDistance: 3, rules: FighterMatchRules.Disabled);
        // The opponent faces Einstein and holds the stance throughout.
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None, -127));
        simulation.Advance(Frame(1, GameplayButtons.None), Frame(1, GameplayButtons.Block));
        simulation.Advance(Frame(2, GameplayButtons.Special1), Frame(2, GameplayButtons.Block));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        for (int tick = 3; tick < 40; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.Block));
        }
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        // Not dragged to the centre (+2) and never launched.
        AssertThat(after.Position.x > FP64.FromDouble(2.5)).IsTrue();
        AssertThat(after.Position.y < FP64.FromDouble(0.5)).IsTrue();
    }

    [TestCase]
    public void TimeDilationLingersHalfASecondAfterLeavingTheRift() {
        var simulation = new FighterSimulation(
            ProjectileBurstSimTests.Authored("einstein"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 7203, spawnDistance: 3, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent inside)).IsTrue();
        AssertThat(inside.StatusType).IsEqual((int)StatusType.TimeDilation);

        // Walk out to the right; find the first tick fully outside the rift box.
        int exitTick = -1;
        FP64 edge = FP64.FromInt(2) + FP64.FromDouble(KitMotionRules.RelativityRiftRadiusUnits) + FP64.FromDouble(0.5);
        int tick = 1;
        for (; tick < 200 && exitTick < 0; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None, 127));
            simulation.TryGetFighter(1, out FighterStateComponent walker);
            if (walker.Position.x > edge) exitTick = tick;
        }
        AssertThat(exitTick >= 0).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent justLeft)).IsTrue();
        AssertThat(justLeft.StatusType).IsEqual((int)StatusType.TimeDilation);
        AssertThat(justLeft.StatusFrames <= KitMotionRules.RelativityRiftLingerFrames).IsTrue();
        AssertThat(justLeft.StatusFrames >= KitMotionRules.RelativityRiftLingerFrames - 2).IsTrue();
        // Stand still outside: the slow is gone within the linger.
        for (int end = tick + KitMotionRules.RelativityRiftLingerFrames + 1; tick < end; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent later)).IsTrue();
        AssertThat(later.StatusType).IsEqual((int)StatusType.None);
    }

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed, sbyte moveX = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = pressed,
        Pressed = pressed
    };
}
