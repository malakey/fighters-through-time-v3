using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// FU-KNOCK R2, the presentation half: the driver edge-detects component 323's
/// wrapping bounce counter and plays an impact burst at the wall. It is a
/// one-way street - reading the counter writes nothing into the simulation.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterWallBouncePresentationTests {
    private const double Step = 1.0 / 60.0;

    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [TestCase]
    public void ARisingBounceCounterSpawnsOneImpactBurstAndAnUnchangedOneSpawnsNone() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        // A local human player two (the boot session's CPU would act on its own).
        GameManager manager = GameManager.Instance;
        SessionData savedSession = manager.CurrentSession;
        SessionData localVersus = savedSession;
        localVersus.FighterOpponentType = FighterOpponentType.LocalHuman;
        manager.CurrentSession = localVersus;
        var host = new Node2D { Name = "WallBouncePresentationHost" };
        tree.Root.AddChild(host);
        try {
            PlayerController one = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
            PlayerController two = CharacterFactory.CreateCharacter("joan", 1, applyStoryProgression: false);
            host.AddChild(one);
            host.AddChild(two);
            var driver = new FighterSimulationDriver { Name = "Driver" };
            host.AddChild(driver);
            driver.Initialize(
                one, two, MatchSettings.GetDefault(),
                stageHazardTypeID: 1, stageID: "florence_workshop", matchSeed: 2026);

            // The first read only records the counter (a rematch or restored snapshot never replays a bounce).
            for (int frame = 0; frame < FighterMatchFlowRules.CountdownFrames + 2; frame++) driver._PhysicsProcess(Step);
            int settled = host.GetChildCount();
            driver._PhysicsProcess(Step);
            AssertThat(host.GetChildCount()).OverrideFailureMessage("no bounce, no burst").IsEqual(settled);

            AssertThat(driver.Simulation.TryGetFighterActionState(0, out FighterActionStateComponent action)).IsTrue();
            action.ActionFlags = FighterActionStateComponent.ActionFlagWallBounced | (1 << 8);
            AssertThat(driver.Simulation.SeedFighterActionStateForTest(0, action)).IsTrue();
            driver._PhysicsProcess(Step);
            int afterBounce = host.GetChildCount();
            AssertThat(afterBounce).OverrideFailureMessage("a rising counter plays one impact burst").IsEqual(settled + 1);

            driver._PhysicsProcess(Step);
            AssertThat(host.GetChildCount()).OverrideFailureMessage("the same counter plays nothing more").IsEqual(afterBounce);
        } finally {
            host.Free();
            manager.CurrentSession = savedSession;
        }
    }
}
