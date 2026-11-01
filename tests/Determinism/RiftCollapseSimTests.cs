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
        // Decision 1.2 (2026-10-16): the rift opens on the cast's active entry.
        int riftOpen = SpecialCastTestKit.StepUntilFired(simulation, 0, 1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent rift)).IsTrue();
        AssertThat(rift.Position.x).IsEqual(FP64.FromInt(2));
        AssertThat(rift.HalfExtents.x).IsEqual(FP64.FromDouble(KitMotionRules.RelativityRiftRadiusUnits));

        // E=mc² once the rift's ability lockout and cast have run out (the rift
        // lives 180 ticks, so it is still up and still over the opponent).
        int castTick = IdleUntilAbilityReady(simulation, riftOpen);
        simulation.Advance(Frame(castTick, GameplayButtons.Special1), Frame(castTick, GameplayButtons.None));
        int fired = SpecialCastTestKit.StepUntilFired(simulation, 0, castTick + 1);
        AssertThat(simulation.ZoneCount).IsEqual(1);
        int burstTick = -1;
        FP64 meterAtBurst = FP64.Zero;
        for (int tick = fired; tick < fired + 38 && burstTick < 0; tick++) {
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
        // Lingering-kit pass (decision 27): the collapse hits for 8 (it was a 0-damage launch),
        // launched upward from (near) the rift centre.
        AssertThat(hpAfterBurst - launched.CurrentHP).IsEqual(LingeringKitRules.RiftCollapseDamage);
        AssertThat(launched.Velocity.y > FP64.Zero || launched.Position.y > FP64.Zero).IsTrue();
        AssertThat(FP64.Abs(launched.Position.x - FP64.FromInt(2)) < FP64.FromDouble(0.5)).IsTrue();
        AssertThat(launched.HitstunFrames > 0).IsTrue();
        // The damaging collapse now credits the caster's meter like any damage dealt.
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.Influence >= meterAtBurst).IsTrue();
    }

    [TestCase]
    public void ABlockerIsNeverPulledButTheRiftStillEnds() {
        var simulation = new FighterSimulation(
            ProjectileBurstSimTests.Authored("einstein"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 7202, spawnDistance: 3, rules: FighterMatchRules.Disabled);
        // The opponent faces Einstein and holds the stance throughout.
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None, -127));
        // E=mc² once the rift's ability lockout and (decision 1.2, 2026-10-16)
        // its cast have run out.
        int castTick = IdleUntilAbilityReady(simulation, 1, victimHeld: GameplayButtons.Block);
        simulation.Advance(Frame(castTick, GameplayButtons.Special1), Frame(castTick, GameplayButtons.Block));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent cast)).IsTrue();
        AssertThat(cast.SpecialOneReuseFrames > 0).OverrideFailureMessage("E=mc² was not cast.").IsTrue();
        int fired = SpecialCastTestKit.StepUntilFired(simulation, 0, castTick + 1, t => (
            SpecialCastTestKit.Neutral(t), Frame(t, GameplayButtons.Block)));
        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        for (int tick = fired; tick < fired + 37; tick++) {
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
        int riftOpen = SpecialCastTestKit.StepUntilFired(simulation, 0, 1);
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent inside)).IsTrue();
        AssertThat(inside.StatusType).IsEqual((int)StatusType.TimeDilation);

        // Walk out to the right; find the first tick fully outside the rift box.
        int exitTick = -1;
        FP64 edge = FP64.FromInt(2) + FP64.FromDouble(KitMotionRules.RelativityRiftRadiusUnits) + FP64.FromDouble(0.5);
        int tick = riftOpen;
        for (int end = riftOpen + 199; tick < end && exitTick < 0; tick++) {
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

    /// <summary>
    /// Idles Einstein (the opponent sends <paramref name="victimHeld"/>) until
    /// his ability lockout has run out and (decision 1.2, 2026-10-16) his cast
    /// has ended; returns the next free input tick.
    /// </summary>
    private static int IdleUntilAbilityReady(
        FighterSimulation simulation, int startTick, GameplayButtons victimHeld = GameplayButtons.None) {
        int tick = startTick;
        for (int limit = startTick + 90; tick < limit; tick++) {
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            if (runtime.AbilityLockoutFrames == 0 && !SpecialCastTestKit.IsCasting(simulation, 0)) break;
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, victimHeld));
        }
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent ready)).IsTrue();
        AssertThat(ready.AbilityLockoutFrames).IsEqual(0);
        return tick;
    }

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed, sbyte moveX = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = pressed,
        Pressed = pressed
    };
}
