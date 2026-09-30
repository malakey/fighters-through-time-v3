using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7b (LN03) — Lincoln's Splitting Strike in the sim, on his
/// authored data: a 2.2-unit overhead arc that catches grounded and airborne
/// targets alike, SPIKES an airborne target straight down (W1's signed
/// knockback), and gives a grounded one a horizontal shove with no launch.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SplittingStrikeSpikeTests {

    [TestCase]
    public void TheArcSpikesAnAirborneTargetDownward() {
        var simulation = NewMatch();
        // The opponent hops; Lincoln swings while they are rising in front of him.
        simulation.Advance(Frame(0, GameplayButtons.None), Frame(0, GameplayButtons.Jump));
        for (int tick = 1; tick <= 5; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent rising)).IsTrue();
        AssertThat(rising.IsGrounded).IsEqual(0);
        AssertThat(rising.Velocity.y > FP64.Zero).IsTrue();

        simulation.Advance(Frame(6, GameplayButtons.Special2), Frame(6, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent spiked)).IsTrue();
        AssertThat(spiked.CurrentHP).IsEqual(64);
        AssertThat(spiked.Velocity.y < FP64.Zero)
            .OverrideFailureMessage("LN03: an airborne target is spiked straight down.")
            .IsTrue();
    }

    [TestCase]
    public void AGroundedTargetTwoUnitsAwayTakesAHorizontalShoveAndStaysDown() {
        var simulation = NewMatch();
        // Two units apart: past the generic 2-unit melee gate's edge but inside
        // the 2.2-unit arc.
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(64);
        AssertThat(target.IsGrounded).IsEqual(1);
        AssertThat(target.Velocity.y).IsEqual(FP64.Zero);
        AssertThat(target.Velocity.x > FP64.Zero).IsTrue();
    }

    private static FighterSimulation NewMatch() => new(
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/lincoln_data.tres")),
        FighterLoadout.Default(FighterCharacterID.Tesla),
        seed: 99,
        spawnDistance: 1,
        rules: FighterMatchRules.Disabled);

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        Held = pressed,
        Pressed = pressed
    };
}
