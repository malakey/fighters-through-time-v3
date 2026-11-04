using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GR-LV4 (ruling 14; design §6): the Siege Cannon's Phase 2. The railcar relocates after every
/// volley and the cover fences become destructible.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level11CannonArenaTests {
    private const string ScenePath = "res://scenes/campaign/Level_11_Gettysburg.tscn";

    [TestCase]
    public void TheFencesStandInsideTheArenaAndNothingBreaksThemInPhaseOne() {
        using var fixture = new GrLevelFixture<Level11Controller>(ScenePath, "einstein");
        Level11Controller level = fixture.Level;
        AssertThat(level.RailcutFences.Count).IsEqual(2);
        Rect2 arena = Level11Controller.BossArenaBounds;
        foreach (RailcutFence fence in level.RailcutFences) {
            AssertThat(arena.HasPoint(fence.GlobalPosition)).IsTrue();
            AssertThat(fence.CollisionLayer).IsEqual(CollisionLayers.Environment);
            AssertThat(fence.Breakable).IsFalse();
            AssertThat(fence.IsInGroup("puzzle_object")).OverrideFailureMessage("A bot would go and break it.").IsFalse();
            AssertThat(fence.RewindPolicy).IsEqual(StoryRewindPolicy.PreserveCurrentState);
            AssertThat(fence.TakeEnvironmentDamage(999f)).IsEqual(0);
            AssertThat(fence.IsDestroyed).IsFalse();
        }
        // Neither fence stands on the boss's spawn, and each stands clear of the font.
        AssertThat(Mathf.Abs(level.RailcutFences[0].GlobalPosition.X - Level11Controller.BossSpawnX) > 300f).IsTrue();
        AssertThat(level.RailcutSecondActRaised).IsFalse();
    }

    /// <summary>The real phase event, from the real boss: raised once, off the physics flush, and three strikes then break a fence.</summary>
    [TestCase(Timeout = 60000)]
    public async Task ThePhaseChangeOpensTheRulesOnceAndThreeStrikesBreakAFence() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GrLevelFixture<Level11Controller>(ScenePath, "einstein");
        Level11Controller level = fixture.Level;
        await GrLevelSteps.Settle(tree);
        BossController cannon = level.CannonEncounter.Boss;
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertObject(cannon.AbilityAfterVolley).IsNull();

        using (PhysicsCallbackGuard.Enter()) {
            cannon.TakeDamage(cannon.CurrentHP - Mathf.FloorToInt(cannon.ScaledMaxHP * cannon.Data.PhaseThresholds[0]) + 1);
            AssertThat(cannon.CurrentPhase).IsEqual(1);
            AssertThat(level.RailcutSecondActRaised)
                .OverrideFailureMessage("The raise must wait out the hit's physics flush.").IsFalse();
        }
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(level.RailcutSecondActRaised).IsTrue();
        AssertThat(cannon.AbilityAfterVolley?.AbilityID).IsEqual(Level11Controller.RailWarpAbilityID);

        RailcutFence fence = level.RailcutFences[0];
        fence.TakeEnvironmentDamage(1f);
        fence.TakeEnvironmentDamage(1f);
        AssertThat(fence.IsDestroyed).IsFalse();
        // Idempotent: a later call neither restores the fence nor rewires the boss.
        level.RaiseRailcutSecondAct();
        AssertThat(fence.HitCount).IsEqual(2);
        fence.TakeEnvironmentDamage(1f);
        AssertThat(fence.IsDestroyed).IsTrue();
        await GrLevelSteps.Frames(tree, 2);
        AssertThat(fence.GetNode<CollisionShape2D>("CollisionShape2D").Disabled).IsTrue();
    }

    /// <summary>In real frames: a volley hands straight to the warp, and a volley fired across a fence spends a strike of it.</summary>
    [TestCase(Timeout = 120000)]
    public async Task EveryVolleyIsFollowedByTheWarpAndWearsTheFenceItCrosses() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GrLevelFixture<Level11Controller>(ScenePath, "einstein");
        Level11Controller level = fixture.Level;
        PlayerController hero = level.Player;
        try {
            hero.ScriptedHPFloor = 1;
            await GrLevelSteps.EnterTheArena(tree, level, 9900f, new Vector2(10500f, Level11Controller.EnemyGroundY));
            BossController cannon = level.CannonEncounter.Boss;
            cannon.IsEngaged = true;
            level.RaiseRailcutSecondAct();

            int volleys = 0;
            cannon.VolleyEnded += _ => volleys++;
            for (int frame = 0; frame < 3000 && volleys == 0; frame++) {
                if (frame % 30 == 0) hero.GlobalPosition = new Vector2(10500f, Level11Controller.EnemyGroundY);
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(volleys).OverrideFailureMessage("The Cannon never fired a volley.").IsEqual(1);
            AssertThat(cannon.SelectedAbility.AbilityID).IsEqual(Level11Controller.RailWarpAbilityID);
            AssertThat(cannon.CurrentState).IsEqual(BossState.Attacking);
            Vector2 vacated = cannon.GlobalPosition;
            for (int frame = 0; frame < 120 && cannon.CurrentState == BossState.Attacking; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(cannon.GlobalPosition.DistanceTo(vacated) > 1f)
                .OverrideFailureMessage($"The warp did not move the railcar from {vacated}.").IsTrue();
            AssertThat(Level11Controller.BossArenaBounds.HasPoint(cannon.GlobalPosition)).IsTrue();

            // The hero takes cover behind the west fence: the next volley spends a strike of it.
            RailcutFence fence = level.RailcutFences[0];
            for (int frame = 0; frame < 3000 && fence.HitCount == 0; frame++) {
                if (frame % 30 == 0) hero.GlobalPosition = new Vector2(10040f, Level11Controller.EnemyGroundY);
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(fence.HitCount >= 1)
                .OverrideFailureMessage($"{volleys} volleys never wore the fence; the Cannon stands at {cannon.GlobalPosition}.").IsTrue();
        } finally {
            hero.ScriptedHPFloor = 0;
        }
    }

    /// <summary>A warp stops short of a standing fence and passes once it falls, and the rules end when the Cannon does.</summary>
    [TestCase(Timeout = 60000)]
    public async Task AWarpStopsAtAStandingFenceAndTheRulesEndWithTheCannon() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GrLevelFixture<Level11Controller>(ScenePath, "einstein");
        Level11Controller level = fixture.Level;
        await GrLevelSteps.Settle(tree);
        BossController cannon = level.CannonEncounter.Boss;
        await GrLevelSteps.Frames(tree, 2);
        level.RaiseRailcutSecondAct();
        EnemyAbilityData warp = cannon.AbilityAfterVolley;
        RailcutFence fence = level.RailcutFences[0];
        float fenceEast = fence.GlobalPosition.X + 100f;
        var target = new Vector2(10040f, Level11Controller.EnemyGroundY);

        cannon.GlobalPosition = new Vector2(10700f, Level11Controller.EnemyGroundY);
        await GrLevelSteps.Frames(tree, 2);
        await RunWarp(cannon, warp, target);
        AssertThat(cannon.GlobalPosition.X >= fenceEast)
            .OverrideFailureMessage($"The warp passed a standing fence: {cannon.GlobalPosition.X:0}.").IsTrue();

        for (int strike = 0; strike < Level11Controller.RailcutFenceHitsToBreak; strike++) fence.TakeEnvironmentDamage(1f);
        await GrLevelSteps.Frames(tree, 3);
        await RunWarp(cannon, warp, target);
        AssertThat(cannon.GlobalPosition.X < fence.GlobalPosition.X)
            .OverrideFailureMessage($"The warp did not pass the fallen fence: {cannon.GlobalPosition.X:0}.").IsTrue();

        cannon.TakeDamage(cannon.CurrentHP);
        await GrLevelSteps.Frames(tree, 3);
        AssertThat(level.RailcutSecondActRaised).IsFalse();
        AssertObject(cannon.AbilityAfterVolley).IsNull();
        AssertThat(level.RailcutFences[1].Breakable).IsFalse();
        level.RaiseRailcutSecondAct();
        AssertThat(level.RailcutSecondActRaised).OverrideFailureMessage("A raise after the fall must do nothing.").IsFalse();
    }

    private static async Task RunWarp(BossController cannon, EnemyAbilityData warp, Vector2 target) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        cannon.IsEngaged = false;
        AssertThat(cannon.BeginAbility(warp, target)).IsTrue();
        for (int frame = 0; frame < 120 && cannon.AbilityPhase != EnemyAbilityPhase.Idle; frame++) {
            cannon.TickAbility(1f / 60f);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
    }
}
