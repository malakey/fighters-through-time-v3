using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The 2026-10-12 gameplay update's movement cut, Fighter-sim half (workstream
/// GU-C; the user: "reduce the movement speed by about 20%, and the jump
/// distance by the same. The jump height should remain the same.", both modes
/// by the user's scope ruling). The loadouts read the cut <c>MaxMoveSpeed</c>
/// straight from the nine resources, a jump's height and airtime do not read
/// it, and the roll and the knockdown roll get-up keep their pre-cut travel
/// through <see cref="UniversalMovementRules.RollSpeedMultiplier"/> (1.5 → 1.875).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterMovementSpeedCutTests {
    /// <summary>The user's cut: "about 20%", applied as exactly × 0.8.</summary>
    private const double SpeedCut = 0.8;

    /// <summary>The roll multiplier before the cut, the distance the roll keeps.</summary>
    private const double PreCutRollSpeedMultiplier = 1.5;

    /// <summary>
    /// A jump from full run speed with the stick held forward: the rise and the
    /// airtime match the same loadout's standing jump (no horizontal term reaches
    /// the vertical step), and the distance is the airtime at the run speed, so
    /// the cut takes a fifth off the distance and nothing off the height.
    /// </summary>
    [TestCase]
    public void AJumpKeepsItsHeightAndAirtimeAndCoversAirtimeTimesTheRunSpeed() {
        foreach (string id in new[] { "lincoln", "joan" }) {
            FighterLoadout loadout = LoadoutFor(id);
            foreach (int jumps in new[] { 1, 2 }) {
                (FP64 runRise, FP64 runDistance, int runAirtime) = MeasureJump(loadout, jumps, moveX: 127);
                (FP64 standRise, FP64 standDistance, int standAirtime) = MeasureJump(loadout, jumps, moveX: 0);
                AssertThat(runAirtime)
                    .OverrideFailureMessage($"{id} x{jumps}: the running jump stayed up {runAirtime} ticks, the standing one {standAirtime}.")
                    .IsEqual(standAirtime);
                AssertThat(runRise.RawValue)
                    .OverrideFailureMessage($"{id} x{jumps}: the running jump rose {runRise}, the standing one {standRise}.")
                    .IsEqual(standRise.RawValue);
                AssertThat(standDistance.RawValue).IsEqual(FP64.Zero.RawValue);
                double expected = runAirtime * loadout.MoveSpeed.ToFloat() / 60.0;
                AssertThat(System.Math.Abs(runDistance.ToFloat() - expected) < 0.01)
                    .OverrideFailureMessage($"{id} x{jumps}: covered {runDistance} u, airtime x run is {expected:0.####}.")
                    .IsTrue();
            }
        }
    }

    /// <summary>
    /// Lincoln's and Joan's roll travel and roll get-up cover what they did
    /// before the cut (pre-cut speed × 1.5 over the moving ticks), and Lincoln,
    /// rolling from pushbox contact, still crosses up the opponent.
    /// </summary>
    [TestCase]
    public void TheRollAndTheRollGetUpKeepTheirPreCutTravelAndLincolnStillCrossesUp() {
        foreach (string id in new[] { "lincoln", "joan" }) {
            FighterLoadout loadout = LoadoutFor(id);
            double preCutRollSpeed = loadout.MoveSpeed.ToFloat() / SpeedCut * PreCutRollSpeedMultiplier;

            var simulation = new FighterSimulation(loadout, FighterLoadout.Default(FighterCharacterID.Joan),
                spawnDistance: 18, rules: FighterMatchRules.Disabled);
            int tick = 0;
            for (; tick < 10; tick++) simulation.Advance(Frame(tick, 0), Frame(tick, 0));
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
            int movingTicks = 0;
            FP64 previousX = before.Position.x;
            // The press tick (stick forward, Roll) and the rest of the roll, from a standstill.
            for (int frame = 0; frame < UniversalMovementRules.RollTotalFrames + 10; frame++, tick++) {
                simulation.Advance(
                    frame == 0 ? Frame(tick, 127, GameplayButtons.Roll) : Frame(tick, 0),
                    Frame(tick, 0));
                AssertThat(simulation.TryGetFighter(0, out FighterStateComponent rolling)).IsTrue();
                if (rolling.Position.x > previousX) movingTicks++;
                previousX = rolling.Position.x;
            }
            AssertThat(movingTicks > 0).OverrideFailureMessage($"{id}: the roll never moved.").IsTrue();
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent after)).IsTrue();
            double travel = (after.Position.x - before.Position.x).ToFloat();
            AssertThat(System.Math.Abs(travel - preCutRollSpeed * movingTicks / 60.0) < 0.005)
                .OverrideFailureMessage($"{id}: the roll covered {travel:0.####} u in {movingTicks} ticks, "
                    + $"not the pre-cut {preCutRollSpeed * movingTicks / 60.0:0.####}.")
                .IsTrue();

            double getUp = MeasureRollGetUp(loadout);
            AssertThat(System.Math.Abs(getUp - preCutRollSpeed * BasicComboRules.RollGetUpFrames / 60.0) < 0.005)
                .OverrideFailureMessage($"{id}: the roll get-up covered {getUp:0.####} u, "
                    + $"not the pre-cut {preCutRollSpeed * BasicComboRules.RollGetUpFrames / 60.0:0.####}.")
                .IsTrue();
        }

        // Cross-up: walk Lincoln into pushbox contact with the default opponent, then roll through it.
        var crossUp = new FighterSimulation(LoadoutFor("lincoln"), FighterLoadout.Default(FighterCharacterID.Joan),
            spawnDistance: 2, rules: FighterMatchRules.Disabled);
        int at = 0;
        for (; at < 60; at++) crossUp.Advance(Frame(at, 127), Frame(at, -127));
        AssertThat(crossUp.TryGetFighter(0, out FighterStateComponent roller)).IsTrue();
        AssertThat(crossUp.TryGetFighter(1, out FighterStateComponent blocker)).IsTrue();
        AssertThat(roller.Position.x < blocker.Position.x).IsTrue();
        AssertThat(blocker.Position.x - roller.Position.x
                <= FighterPushboxSystem.MinimumHorizontalDistance + FP64.FromDouble(0.05))
            .OverrideFailureMessage("the pair never reached pushbox contact.")
            .IsTrue();
        for (int frame = 0; frame < UniversalMovementRules.RollTotalFrames + 2; frame++, at++) {
            crossUp.Advance(Frame(at, frame == 0 ? (sbyte)127 : (sbyte)0, frame == 0 ? GameplayButtons.Roll : GameplayButtons.None),
                Frame(at, 0));
        }
        AssertThat(crossUp.TryGetFighter(0, out FighterStateComponent crossed)).IsTrue();
        AssertThat(crossUp.TryGetFighter(1, out FighterStateComponent passed)).IsTrue();
        AssertThat(crossed.Position.x > passed.Position.x)
            .OverrideFailureMessage($"Lincoln ended at {crossed.Position.x}, short of the opponent at {passed.Position.x}.")
            .IsTrue();
    }

    // === Harness ===

    private static (FP64 rise, FP64 distance, int airtime) MeasureJump(FighterLoadout loadout, int jumps, sbyte moveX) {
        var simulation = new FighterSimulation(loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            spawnDistance: 18, rules: FighterMatchRules.Disabled);
        int tick = 0;
        for (; tick < 30; tick++) simulation.Advance(Frame(tick, moveX), Frame(tick, 0));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent start)).IsTrue();
        FP64 highest = start.Position.y;
        simulation.Advance(Frame(tick, moveX, GameplayButtons.Jump), Frame(tick, 0));
        tick++;
        int airtime = 1;
        bool secondPressed = jumps < 2;
        bool airborne = false;
        for (int frame = 0; frame < 600; frame++, tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent now)).IsTrue();
            GameplayButtons buttons = GameplayButtons.None;
            if (!secondPressed && now.Velocity.y <= FP64.Zero) {
                buttons = GameplayButtons.Jump;
                secondPressed = true;
            }
            simulation.Advance(Frame(tick, moveX, buttons), Frame(tick, 0));
            airtime++;
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent flying)).IsTrue();
            if (flying.Position.y > highest) highest = flying.Position.y;
            if (flying.IsGrounded == 0) airborne = true;
            if (airborne && flying.IsGrounded != 0) {
                return (highest - start.Position.y, flying.Position.x - start.Position.x, airtime);
            }
        }
        AssertThat(false).OverrideFailureMessage("the jump never landed.").IsTrue();
        return (FP64.Zero, FP64.Zero, -1);
    }

    /// <summary>
    /// Launches the loadout's fighter (player two) with a launching Special,
    /// lets it land without a tech into the knockdown, holds right on the
    /// knockdown's last frame and returns how far the 14-frame roll get-up moved it.
    /// </summary>
    private static double MeasureRollGetUp(FighterLoadout victim) {
        var simulation = new FighterSimulation(Launcher(), victim,
            seed: 52, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0));
        int tick = 1;
        bool down = false;
        for (; tick < 160 && !down; tick++) {
            simulation.Advance(Frame(tick, 0), Frame(tick, 0));
            simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent knockdown);
            down = FighterKnockdownRules.IsDown(in knockdown);
        }
        AssertThat(down).OverrideFailureMessage("the launch never landed into a knockdown.").IsTrue();
        for (int frame = 1; frame <= BasicComboRules.KnockdownFrames; frame++, tick++) {
            sbyte held = frame == BasicComboRules.KnockdownFrames ? (sbyte)127 : (sbyte)0;
            simulation.Advance(Frame(tick, 0), Frame(tick, held));
        }
        AssertThat(simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent chosen)).IsTrue();
        AssertThat(chosen.GetUpKind).IsEqual(BasicComboRules.GetUpRoll);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent start)).IsTrue();
        for (int frame = 1; frame <= BasicComboRules.RollGetUpFrames; frame++, tick++) {
            simulation.Advance(Frame(tick, 0), Frame(tick, 0));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent end)).IsTrue();
        return (end.Position.x - start.Position.x).ToFloat();
    }

    /// <summary>The Default kit's numbers with a launching melee Special 1 (the FighterKnockdownTests launcher).</summary>
    private static FighterLoadout Launcher() => new(
        (int)FighterCharacterID.Einstein,
        100, 3, 2,
        10, 14, 12, 20,
        (int)StatusType.None, 0,
        (int)StatusType.None, 0,
        (int)StatusType.None, 0,
        FP64.One,
        FP64.FromInt(7),
        FP64.FromDouble(11.5),
        FP64.FromInt(3),
        FP64.FromInt(4),
        FP64.FromInt(4),
        FP64.FromInt(5),
        FP64.One, FP64.One, FP64.One,
        FighterAbilityLoadout.Default with {
            SpecialOneExecutionType = 0,
            SpecialOneHit = new FighterAbilityHitData {
                HitstunFrames = 40,
                BlockClass = (int)BlockClass.Special,
                Launches = true,
                Delivery = (int)HitDelivery.DirectHit,
                Origin = (int)HitOrigin.Special
            }
        });

    private static FighterLoadout LoadoutFor(string characterID) {
        CharacterData data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres");
        return FighterLoadoutFactory.FromCharacterData(data);
    }

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons buttons = GameplayButtons.None) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = buttons,
        Pressed = buttons
    };
}
