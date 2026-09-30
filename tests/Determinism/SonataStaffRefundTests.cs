using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7b (M04, D14 both modes) — Mozart's Sonata Drift in the sim, on
/// his authored data: the glissando rises about 3 units along the held
/// direction, the 2.0-unit staff is placed under his feet where it ends and is
/// a walkable one-way surface, and landing on his own staff halves the
/// remaining movement cooldown AT MOST ONCE PER AIRTIME (so he gets at most two
/// drifts before touching down); real ground re-arms the refund.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SonataStaffRefundTests {

    [TestCase]
    public void TheGlissandoRisesThreeUnitsAndPlacesTheStaffUnderItsEnd() {
        var simulation = NewMatch();
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
        simulation.Advance(Frame(0, GameplayButtons.MovementAbility), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstPersistentObject(out FighterPersistentObjectComponent staff)).IsTrue();
        AssertThat(staff.ObjectTypeID).IsEqual(FighterReachKitRules.StaffPlatformTypeID);
        AssertThat(staff.Position.y).IsEqual(before.Position.y + FP64.FromInt(3));
        AssertThat(staff.HalfExtents.x).IsEqual(FP64.One);
        Run(simulation, 1, 18);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent risen)).IsTrue();
        AssertThat(risen.Position.y > FP64.FromDouble(2.95)).IsTrue();
        AssertThat(risen.Position.x).IsEqual(before.Position.x);
    }

    [TestCase]
    public void LandingOnHisOwnStaffRefundsHalfTheCooldownOnce() {
        var simulation = NewMatch();
        simulation.Advance(Frame(0, GameplayButtons.MovementAbility), Frame(0, GameplayButtons.None));
        int landedTick = RunUntilGroundedOnStaff(simulation, 1, 40, out int cooldownBeforeLanding);
        AssertThat(landedTick > 0).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.MovementCooldownFrames).IsEqual((cooldownBeforeLanding - 1) / 2);
        AssertThat(simulation.TryGetFighterKnockdown(0, out FighterKnockdownComponent knockdown)).IsTrue();
        AssertThat(knockdown.StaffRefundUsed).IsEqual(1);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent standing)).IsTrue();
        AssertThat(standing.Position.y).IsEqual(FP64.FromInt(3));
    }

    [TestCase]
    public void ASecondStaffInTheSameAirtimeRefundsNothingAndRealGroundReArmsIt() {
        var simulation = NewMatch();
        simulation.Advance(Frame(0, GameplayButtons.MovementAbility), Frame(0, GameplayButtons.None));
        int tick = RunUntilGroundedOnStaff(simulation, 1, 40, out _);
        AssertThat(tick > 0).IsTrue();

        // Wait out the halved cooldown on the staff, then drift again.
        int next = tick + 1;
        for (; next < tick + 200; next++) {
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent waiting)).IsTrue();
            if (waiting.MovementCooldownFrames <= 0) break;
            simulation.Advance(Frame(next, GameplayButtons.None), Frame(next, GameplayButtons.None));
        }
        simulation.Advance(Frame(next, GameplayButtons.MovementAbility), Frame(next, GameplayButtons.None));
        int landedAgain = RunUntilGroundedOnStaff(simulation, next + 1, next + 40, out int cooldownBefore);
        AssertThat(landedAgain > 0).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent second)).IsTrue();
        AssertThat(second.MovementCooldownFrames)
            .OverrideFailureMessage("M04: the refund is once per airtime — a second staff refunds nothing.")
            .IsEqual(cooldownBefore - 1);

        // Both staffs expire; he falls to the floor, which re-arms the refund.
        for (int later = landedAgain + 1; later < landedAgain + 400; later++) {
            simulation.Advance(Frame(later, GameplayButtons.None), Frame(later, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent probe)).IsTrue();
            if (probe.IsGrounded != 0 && probe.Position.y == FP64.Zero) break;
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent floored)).IsTrue();
        AssertThat(floored.Position.y).IsEqual(FP64.Zero);
        AssertThat(simulation.TryGetFighterKnockdown(0, out FighterKnockdownComponent rearmed)).IsTrue();
        AssertThat(rearmed.StaffRefundUsed).IsEqual(0);
    }

    /// <summary>Advances until Mozart is grounded above the floor; returns that tick (or -1).</summary>
    private static int RunUntilGroundedOnStaff(
        FighterSimulation simulation, int fromTick, int toTick, out int cooldownBeforeLanding) {
        cooldownBeforeLanding = 0;
        for (int tick = fromTick; tick <= toTick; tick++) {
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent before)).IsTrue();
            cooldownBeforeLanding = before.MovementCooldownFrames;
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent probe)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            if (probe.IsGrounded != 0 && probe.Position.y > FP64.Zero && runtime.UniversalMovementState == 0) {
                return tick;
            }
        }
        return -1;
    }

    private static FighterSimulation NewMatch() => new(
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/mozart_data.tres")),
        FighterLoadout.Default(FighterCharacterID.Tesla),
        seed: 96,
        spawnDistance: 6,
        rules: FighterMatchRules.Disabled);

    private static void Run(FighterSimulation simulation, int fromTick, int toTick) {
        for (int tick = fromTick; tick <= toTick; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
    }

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        Held = pressed,
        Pressed = pressed
    };
}
