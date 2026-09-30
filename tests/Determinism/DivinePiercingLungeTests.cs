using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7b (J01/J03) — Divine Piercing in the sim, on Joan's authored
/// data: a ~3-unit forward lunge across the 12 active frames (her ground
/// gap-closer), three 8-damage thrusts along the way that each resolve exactly
/// once however long a hitstop holds the lunge, and the 11 s cooldown.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DivinePiercingLungeTests {

    [TestCase]
    public void TheLungeCarriesJoanAboutThreeUnitsForwardWhetherOrNotAnyoneIsThere() {
        var simulation = NewMatch(spawnDistance: 8);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent lunging)).IsTrue();
        AssertThat(lunging.UniversalMovementState).IsEqual(FighterKitMotion.PiercingLunge);
        // J03: the 11 s cooldown is armed on the press.
        AssertThat(lunging.SpecialTwoCooldownFrames).IsEqual(660);
        for (int tick = 1; tick <= 14; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent after)).IsTrue();
        FP64 travelled = after.Position.x - before.Position.x;
        AssertThat(travelled > FP64.FromDouble(2.95)).IsTrue();
        AssertThat(travelled < FP64.FromDouble(3.05)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent done)).IsTrue();
        AssertThat(done.UniversalMovementState).IsEqual(0);
    }

    [TestCase]
    public void EachOfTheThreeThrustsResolvesExactlyOnceThroughHitstop() {
        var simulation = NewMatch(spawnDistance: 1);
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        int hits = 0;
        int previousHP = 100;
        for (int tick = 1; tick <= 90; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent probe)).IsTrue();
            if (probe.CurrentHP < previousHP) {
                AssertThat(previousHP - probe.CurrentHP).IsEqual(8);
                hits++;
            }
            previousHP = probe.CurrentHP;
        }
        AssertThat(hits).IsEqual(3);
        AssertThat(previousHP).IsEqual(76);
    }

    private static FighterSimulation NewMatch(int spawnDistance) => new(
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/joan_data.tres")),
        FighterLoadout.Default(FighterCharacterID.Tesla),
        seed: 93,
        spawnDistance: spawnDistance,
        rules: FighterMatchRules.Disabled);

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        Held = pressed,
        Pressed = pressed
    };
}
