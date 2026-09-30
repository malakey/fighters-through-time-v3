using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7b — the sim half of the ground-wave primitive
/// (<see cref="FighterGroundWave"/>), exercised on the authored kits that ride
/// it: Joan's Righteous Smite (J02, grounded-only) and Lincoln's Emancipator
/// (LN03, grounded-only). A wave travels along the surface under its caster,
/// strikes only a fighter standing on that surface, and dissipates at a wall or
/// where its surface ends.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GroundWaveTests {

    [TestCase]
    public void AGroundedOnlyWaveStrikesAStandingTargetButPassesUnderAJumper() {
        // Standing: Righteous Smite reaches the opponent two units away.
        var standing = NewMatch("joan", spawnDistance: 1);
        standing.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        AssertThat(standing.TryGetFirstProjectile(out FighterProjectileComponent wave)).IsTrue();
        AssertThat(FighterGroundWave.IsGroundWave(wave.ProjectileTypeID)).IsTrue();
        AssertThat(FighterGroundWave.HitsGroundedOnly(wave.ProjectileTypeID)).IsTrue();
        Run(standing, 1, 20);
        AssertThat(standing.TryGetFighter(1, out FighterStateComponent struck)).IsTrue();
        AssertThat(struck.CurrentHP).IsEqual(72);

        // Jumping: the same wave rolls underneath and is not consumed by the
        // pass — it simply runs out its 2.5 units.
        var jumping = NewMatch("joan", spawnDistance: 1);
        jumping.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.Jump));
        Run(jumping, 1, 20);
        AssertThat(jumping.TryGetFighter(1, out FighterStateComponent cleared)).IsTrue();
        AssertThat(cleared.CurrentHP).IsEqual(100);
        AssertThat(jumping.ProjectileCount).IsEqual(0);
    }

    [TestCase]
    public void AWaveDissipatesAtAWall() {
        var simulation = NewMatch("lincoln", spawnDistance: 8);
        // Turn Lincoln toward the left wall (x = -10), then slam: the wave
        // leaves at -8.5 and meets the wall 1.5 units later — long before its
        // 5-unit, 30-frame travel.
        simulation.Advance(Frame(0, -127, GameplayButtons.None), Frame(0, 0, GameplayButtons.None));
        simulation.Advance(Frame(1, 0, GameplayButtons.Special1), Frame(1, 0, GameplayButtons.None));
        AssertThat(simulation.ProjectileCount).IsEqual(1);
        Run(simulation, 2, 14);
        AssertThat(simulation.ProjectileCount).IsEqual(0);
    }

    [TestCase]
    public void AWaveDissipatesWhereAnOpenStageFloorEnds() {
        var simulation = new FighterSimulation(
            Loadout("lincoln"),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 91,
            rules: FighterMatchRules.Disabled,
            stageGeometry: FighterStageGeometry.Paris);
        // Paris: Lincoln spawns at x = -4 facing the 5.0-wide centre pit, whose
        // edge is at x = -2.5. The wave leaves at -3.5 and ends at the edge.
        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.ProjectileCount).IsEqual(1);
        Run(simulation, 1, 3);
        AssertThat(simulation.ProjectileCount).IsEqual(1);
        Run(simulation, 4, 10);
        AssertThat(simulation.ProjectileCount).IsEqual(0);
    }

    [TestCase]
    public void AnAirborneCastSnapsTheWaveToTheFloorBelow() {
        var simulation = NewMatch("joan", spawnDistance: 6);
        simulation.Advance(Frame(0, 0, GameplayButtons.Jump), Frame(0, 0, GameplayButtons.None));
        Run(simulation, 1, 8);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent joan)).IsTrue();
        AssertThat(joan.IsGrounded).IsEqual(0);
        AssertThat(joan.Position.y > FP64.One).IsTrue();
        simulation.Advance(Frame(9, 0, GameplayButtons.Special1), Frame(9, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstProjectile(out FighterProjectileComponent wave)).IsTrue();
        AssertThat(FighterGroundWave.SurfaceY(in wave)).IsEqual(FP64.Zero);
        AssertThat(wave.Velocity.y).IsEqual(FP64.Zero);
    }

    private static FighterSimulation NewMatch(string characterID, int spawnDistance) => new(
        Loadout(characterID),
        FighterLoadout.Default(FighterCharacterID.Tesla),
        seed: 90,
        spawnDistance: spawnDistance,
        rules: FighterMatchRules.Disabled);

    private static FighterLoadout Loadout(string characterID) => FighterLoadoutFactory.FromCharacterData(
        AuthoredResources.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres"));

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
