using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W5 — Harriet Tubman's The Freedom Line in the deterministic sim
/// (replaces the retired Tidewater Tempest suite). Built on W6's A02
/// framework: the press spends the meter, a straight 6-unit lantern beam is the
/// activation strike, and only its contact starts the cinematic — ultimate zone
/// type 93 on the held victim, seven 8-damage pulses, then the 20-damage final
/// rush as the D15 finale (76). A whiff deals nothing.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TubmanUltimateTests {

    private const int FreedomLineZoneTypeID = (int)FighterCharacterID.Tubman * 10 + 3;
    private const int Total = 7 * 8 + 20;

    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [TestCase]
    public void TheLanternBeamConnectsAndTheTrainLandsSeventySixAsZoneNinetyThree() {
        FighterSimulation simulation = NewSimulation(seed: 93, spawnDistance: 2);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        int tick = UltimateActivationTestKit.CastAndConnect(simulation, 0, out bool connected);
        AssertThat(connected).IsTrue();
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.Influence).IsEqual(FP64.Zero);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual(FreedomLineZoneTypeID);
        AssertThat(zone.TickIntervalFrames).IsEqual(18);

        UltimateActivationTestKit.Idle(simulation, tick, 7 * 18 + 30);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(before.CurrentHP - after.CurrentHP).IsEqual(Total);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent _)).IsFalse();
    }

    [TestCase]
    public void AWhiffedLanternBeamSpendsTheMeterAndDealsNothing() {
        // Twelve units apart: beyond the beam's 6-unit reach.
        FighterSimulation simulation = NewSimulation(seed: 94, spawnDistance: 6);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int tick = UltimateActivationTestKit.CastAndConnect(simulation, 0, out bool connected);
        AssertThat(connected).IsFalse();
        AssertThat(UltimateActivationTestKit.Phase(simulation, 0))
            .IsEqual(FighterUltimateActivationRules.PhaseWhiffRecovery);
        UltimateActivationTestKit.Idle(simulation, tick, 60);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.Influence).IsEqual(FP64.Zero);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.CurrentHP).IsEqual(before.CurrentHP);
        AssertThat(simulation.ZoneCount).IsEqual(0);
    }

    private static FighterSimulation NewSimulation(int seed, int spawnDistance) => new(
        TubmanKitTests.TubmanLoadout(),
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/lincoln_data.tres")),
        seed: seed,
        spawnDistance: spawnDistance,
        rules: FighterMatchRules.Disabled);
}
