using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7b (M01) — Mozart's Fortissimo Wave in the sim: the retired lob
/// is a slow wall of sound on the ground-wave primitive, 1.5 units tall,
/// travelling 4 units/s for 6 units. Unlike Joan's and Lincoln's waves it is
/// not grounded-only: its height reaches a fighter who has not jumped clear.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FortissimoWallTests {

    [TestCase]
    public void TheWallIsOneAndAHalfUnitsTallAndTravelsFourUnitsPerSecondForSixUnits() {
        var simulation = NewMatch(spawnDistance: 8);
        simulation.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstProjectile(out FighterProjectileComponent wall)).IsTrue();
        AssertThat(FighterGroundWave.IsGroundWave(wall.ProjectileTypeID)).IsTrue();
        AssertThat(FighterGroundWave.HitsGroundedOnly(wall.ProjectileTypeID)).IsFalse();
        AssertThat(wall.HalfExtents.y * FP64.FromInt(2)).IsEqual(FP64.FromDouble(1.5));
        AssertThat(wall.Velocity.x).IsEqual(FP64.FromInt(4));
        AssertThat(wall.Velocity.y).IsEqual(FP64.Zero);
        AssertThat(wall.GravityPerSecond).IsEqual(FP64.Zero);
        // 6 units at 4 units/s is 1.5 s = 90 frames of travel; the projectile
        // system has already advanced it one frame on the cast tick.
        AssertThat(wall.LifetimeFrames + 1).IsEqual(90);
        AssertThat(FighterGroundWave.SurfaceY(in wall)).IsEqual(FP64.Zero);
        Run(simulation, 1, 91);
        AssertThat(simulation.ProjectileCount).IsEqual(0);
    }

    [TestCase]
    public void TheWallStrikesAnAirborneFighterBelowItsHeight() {
        // Mozart at -3, the opponent at +3: the wall arrives about 70 frames
        // after the cast. The opponent hops just before it — still low enough
        // for the 1.5-unit wall to catch, which a grounded-only wave never would.
        var simulation = NewMatch(spawnDistance: 3);
        simulation.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));
        bool struckAirborne = false;
        for (int tick = 1; tick <= 100; tick++) {
            GameplayButtons opponent = tick == 64 ? GameplayButtons.Jump : GameplayButtons.None;
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, opponent));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent probe)).IsTrue();
            if (probe.CurrentHP < 100) {
                struckAirborne = probe.Position.y > FP64.Zero;
                break;
            }
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(76);
        AssertThat(struckAirborne)
            .OverrideFailureMessage("The wall of sound reaches a fighter who has not jumped clear of its 1.5 units.")
            .IsTrue();
    }

    private static FighterSimulation NewMatch(int spawnDistance) => new(
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/mozart_data.tres")),
        FighterLoadout.Default(FighterCharacterID.Tesla),
        seed: 92,
        spawnDistance: spawnDistance,
        rules: FighterMatchRules.Disabled);

    private static void Run(FighterSimulation simulation, int fromTick, int toTick) {
        for (int tick = fromTick; tick <= toTick; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
    }

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };
}
