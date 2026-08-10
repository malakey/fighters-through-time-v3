using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Audit M-7: <c>FighterSimulationDriver.Initialize</c> used to construct the
/// simulation on the constructor-default seed, so every match and rematch
/// replayed the identical orb/hazard schedule and the CPU stream (derived from
/// <c>WorldSeed</c>) never varied either. The driver now rolls a fresh seed per
/// match at the Godot presentation boundary and passes it through; harnesses
/// pin one with the <c>matchSeed</c> parameter. A future Package 7 online
/// handshake must agree on this value at match start (design ~3134) instead of
/// each peer rolling its own.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterMatchSeedTests {

    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [TestCase]
    public void AnUnseededMatchRollsAFreshWorldSeedPerInitialize() {
        (FighterSimulationDriver first, Node hostA) = CreateDriver("MatchSeedHostA", null);
        (FighterSimulationDriver second, Node hostB) = CreateDriver("MatchSeedHostB", null);
        try {
            int seedA = first.Simulation.GetMatchState().WorldSeed;
            int seedB = second.Simulation.GetMatchState().WorldSeed;
            // Two independent draws from Godot's randomized global stream; a
            // collision means the fixed-schedule bug is back.
            AssertThat(seedA).IsNotEqual(seedB);
            // Nor may either land on the old hardcoded constructor default.
            AssertThat(seedA != 2026 || seedB != 2026).IsTrue();
        } finally {
            hostA.Free();
            hostB.Free();
        }
    }

    [TestCase]
    public void AnExplicitMatchSeedIsHonoredForReproducibleHarnesses() {
        (FighterSimulationDriver first, Node hostA) = CreateDriver("PinnedSeedHostA", 4321);
        (FighterSimulationDriver second, Node hostB) = CreateDriver("PinnedSeedHostB", 4321);
        try {
            AssertThat(first.Simulation.GetMatchState().WorldSeed).IsEqual(4321);
            AssertThat(second.Simulation.GetMatchState().WorldSeed).IsEqual(4321);
            // Same seed, same loadouts, same rules: the schedules stay identical.
            AssertThat(first.CurrentHash).IsEqual(second.CurrentHash);
        } finally {
            hostA.Free();
            hostB.Free();
        }
    }

    private static (FighterSimulationDriver driver, Node host) CreateDriver(string name, int? matchSeed) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = name };
        tree.Root.AddChild(host);

        PlayerController one = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        PlayerController two = CharacterFactory.CreateCharacter("joan", 1, applyStoryProgression: false);
        host.AddChild(one);
        host.AddChild(two);

        var driver = new FighterSimulationDriver { Name = "Driver" };
        host.AddChild(driver);
        driver.Initialize(
            one, two, MatchSettings.GetDefault(),
            stageHazardTypeID: 1, stageID: "florence_workshop", matchSeed: matchSeed);
        return (driver, host);
    }
}
