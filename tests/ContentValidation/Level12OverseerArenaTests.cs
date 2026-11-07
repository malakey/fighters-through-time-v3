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
/// GR-LV4 (rulings 14, 14+; design §6): the Gravity Overseer's arena. Phase 2's marked zones cycle
/// between 0.15 and 2.5 on a 1 s telegraph, with no inversion; Phase 3's vents gust toward the edges.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level12OverseerArenaTests {
    private const string ScenePath = "res://scenes/campaign/Level_12_Lunar.tscn";
    private const float Floor = Level12Controller.SurfaceY - 2f;

    [TestCase(Timeout = 60000)]
    public async Task TheZonesAndVentsAreBuiltOffInsideTheArenaAndTheDampersRule() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GrLevelFixture<Level12Controller>(ScenePath, "einstein");
        Level12Controller level = fixture.Level;
        Rect2 arena = Level12Controller.BossArenaBounds;
        AssertThat(level.OverseerFields.Count).IsEqual(2);
        AssertThat(level.OverseerGusts.Count).IsEqual(4);
        foreach (GravityFieldZone field in level.OverseerFields) {
            AssertThat(field.Enabled).IsFalse();
            AssertThat(field.IsCycling).IsFalse();
            AssertThat(field.RewindPolicy).IsEqual(StoryRewindPolicy.PreserveCurrentState);
            AssertThat(arena.HasPoint(field.GlobalPosition)).IsTrue();
        }
        foreach (StoryCyclicHazard gust in level.OverseerGusts) {
            AssertThat(gust.Enabled).IsFalse();
            AssertThat(gust.IsInGroup("story_hazard")).IsTrue();
            CollisionShape2D strip = gust.GetNode<CollisionShape2D>("CollisionShape2D");
            AssertThat(strip.GlobalPosition.X - 60f >= arena.Position.X && strip.GlobalPosition.X + 60f <= arena.End.X).IsTrue();
            // The node is the strip's inner edge, so all of it pushes one way.
            AssertThat(Mathf.Abs(strip.GlobalPosition.X - gust.GlobalPosition.X)).IsEqual(60f);
        }
        // The stand-in left the kit: the zones are what it was.
        BossData overseer = AuthoredResources.Load<BossData>(Level12Controller.BossResourcePath);
        AssertThat(overseer.BossAbilities.Any(a => a.AbilityID.EndsWith("inverted_gravity_field"))).IsFalse();

        await GrLevelSteps.Settle(tree);
        level.Player.GlobalPosition = new Vector2(10200f, Floor);
        level.Player.Velocity = Vector2.Zero;
        await GrLevelSteps.Frames(tree, 4);
        AssertThat(Mathf.IsEqualApprox(level.Player.EnvironmentGravityScale, Level12Controller.PadGravityScale))
            .OverrideFailureMessage($"An off zone changed gravity: {level.Player.EnvironmentGravityScale}.").IsTrue();
    }

    /// <summary>The real phase event: off the flush, once, and the hero reads exactly 2.5 then 0.15, each shift a second after its telegraph.</summary>
    [TestCase(Timeout = 60000)]
    public async Task PhaseTwoCyclesTheZonesAtTheRuledScalesOnAOneSecondTelegraph() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GrLevelFixture<Level12Controller>(ScenePath, "einstein");
        Level12Controller level = fixture.Level;
        PlayerController hero = level.Player;
        await GrLevelSteps.EnterTheArena(tree, level, 9700f, new Vector2(10200f, Floor));
        BossController overseer = level.OverseerEncounter.Boss;
        await GrLevelSteps.Frames(tree, 3);

        using (PhysicsCallbackGuard.Enter()) {
            overseer.TakeDamage(overseer.CurrentHP - Mathf.FloorToInt(overseer.ScaledMaxHP * overseer.Data.PhaseThresholds[0]) + 1);
            AssertThat(overseer.CurrentPhase).IsEqual(1);
            AssertThat(level.OverseerFieldsCycling)
                .OverrideFailureMessage("The raise must wait out the hit's physics flush.").IsFalse();
        }
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        AssertThat(level.OverseerFieldsCycling).IsTrue();
        GravityFieldZone west = level.OverseerFields[0];
        GravityFieldZone east = level.OverseerFields[1];
        overseer.IsEngaged = false;
        AssertThat(Mathf.IsEqualApprox(west.NextScale * Level12Controller.PadGravityScale, Level12Controller.OverseerHeavyGravityScale)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(east.NextScale * Level12Controller.PadGravityScale, Level12Controller.OverseerLightGravityScale)).IsTrue();

        // The physics_frame signal fires before the step's _PhysicsProcess, so a tick has run after two.
        await GrLevelSteps.Frames(tree, 2);
        AssertThat(west.IsTelegraphing).OverrideFailureMessage("The first shift must telegraph at once.").IsTrue();
        int frames = await FramesUntilShift(tree, west);
        AssertThat(frames >= 55 && frames <= 62).OverrideFailureMessage($"The first shift came after {frames} frames.").IsTrue();
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(Mathf.IsEqualApprox(hero.EnvironmentGravityScale, Level12Controller.OverseerHeavyGravityScale))
            .OverrideFailureMessage($"The hero reads {hero.EnvironmentGravityScale}, not 2.5.").IsTrue();

        // No shift until its telegraph, which stands a whole second ahead of it.
        for (int frame = 0; frame < 150; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(west.IsTelegraphing).IsFalse();
        for (int frame = 0; frame < 90 && !west.IsTelegraphing; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(west.IsTelegraphing).IsTrue();
        frames = await FramesUntilShift(tree, west);
        AssertThat(frames >= 55 && frames <= 62).OverrideFailureMessage($"The telegraph stood {frames} frames.").IsTrue();
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(Mathf.IsEqualApprox(hero.EnvironmentGravityScale, Level12Controller.OverseerLightGravityScale))
            .OverrideFailureMessage($"The hero reads {hero.EnvironmentGravityScale}, not 0.15.").IsTrue();

        // Out of the zone the dampers rule again.
        hero.GlobalPosition = new Vector2(10500f, Floor);
        await GrLevelSteps.Frames(tree, 3);
        AssertThat(Mathf.IsEqualApprox(hero.EnvironmentGravityScale, Level12Controller.PadGravityScale)).IsTrue();
    }

    /// <summary>Phase 3 stands the zones down and opens the vents, which throw a hero toward the edge on its side, and all of it stops when the Overseer falls.</summary>
    [TestCase(Timeout = 60000)]
    public async Task PhaseThreeGustsTowardTheEdgesAndStopsWithTheOverseer() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GrLevelFixture<Level12Controller>(ScenePath, "einstein");
        Level12Controller level = fixture.Level;
        PlayerController hero = level.Player;
        await GrLevelSteps.EnterTheArena(tree, level, 9700f, new Vector2(10200f, Floor));
        BossController overseer = level.OverseerEncounter.Boss;
        await GrLevelSteps.Frames(tree, 3);
        level.RaiseOverseerFields();
        overseer.IsEngaged = false;
        for (int frame = 0; frame < 70; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

        level.OpenOverseerGusts();
        AssertThat(level.OverseerFieldsCycling).IsFalse();
        AssertThat(level.OverseerGustsOpen).IsTrue();
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(Mathf.IsEqualApprox(hero.EnvironmentGravityScale, Level12Controller.PadGravityScale)).IsTrue();
        // A later Phase 2 call changes nothing: the zones are done.
        level.RaiseOverseerFields();
        AssertThat(level.OverseerFieldsCycling).IsFalse();
        // The inner pair telegraphs at once and the outer pair comes later.
        AssertThat(level.OverseerGusts[0].Phase).IsEqual(HazardPhase.Warning);
        AssertThat(level.OverseerGusts[1].Phase).IsEqual(HazardPhase.Cooldown);

        foreach ((int index, float x, float sign) in new[] { (0, 10300f, -1f), (2, 10900f, 1f) }) {
            hero.GlobalPosition = new Vector2(x, Floor);
            hero.Velocity = Vector2.Zero;
            await GrLevelSteps.Frames(tree, 3);
            int health = hero.CurrentHP;
            level.OverseerGusts[index].ForcePhase(HazardPhase.Active, 0.2f);
            await GrLevelSteps.Frames(tree, 30);
            float pushed = (hero.GlobalPosition.X - x) * sign;
            GD.Print($"[gust] vent {index}: pushed {pushed:0} px in 30 frames, hero at {hero.GlobalPosition}, {health - hero.CurrentHP} damage");
            AssertThat(pushed > 80f).OverrideFailureMessage($"Vent {index} pushed the hero only {pushed:0} px toward its edge.").IsTrue();
            AssertThat(health - hero.CurrentHP).IsEqual(Level12Controller.OverseerGustDamage);
        }

        overseer.TakeDamage(overseer.CurrentHP);
        await GrLevelSteps.Frames(tree, 3);
        AssertThat(level.OverseerGustsOpen).IsFalse();
        foreach (StoryCyclicHazard gust in level.OverseerGusts) AssertThat(gust.Enabled).IsFalse();
        level.OpenOverseerGusts();
        AssertThat(level.OverseerGustsOpen).OverrideFailureMessage("A raise after the fall must do nothing.").IsFalse();
    }

    /// <summary>Two real Post-Landing Holds: a zone's cycle stands still through one, and vents opened inside one do not start until it ends.</summary>
    [TestCase(Timeout = 60000)]
    public async Task TheZonesAndVentsAreFrozenWhileTheWorldIsHeld() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GrLevelFixture<Level12Controller>(ScenePath, "einstein");
        Level12Controller level = fixture.Level;
        await GrLevelSteps.EnterTheArena(tree, level, 9700f, new Vector2(10500f, Floor));
        var manager = tree.GetFirstNodeInGroup(ChronalRewindManager.ManagerGroup) as ChronalRewindManager;
        AssertObject(manager).OverrideFailureMessage("The level has no rewind manager.").IsNotNull();

        level.RaiseOverseerFields();
        GravityFieldZone west = level.OverseerFields[0];
        await GrLevelSteps.Frames(tree, 20);
        AssertThat(manager.BeginPlacementHold(StoryRecoveryHoldCause.CollapseResume)).IsTrue();
        int frames = 0;
        for (; frames < 50; frames++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(ChronalRewindManager.IsWorldHeld(tree)).IsTrue();
        AssertThat(west.IsTimeFrozen).IsTrue();
        AssertThat(west.CycleIndex).OverrideFailureMessage("A shift went off inside the hold.").IsEqual(0);
        for (; frames < 120 && ChronalRewindManager.IsWorldHeld(tree); frames++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(ChronalRewindManager.IsWorldHeld(tree)).IsFalse();
        int shift = await FramesUntilShift(tree, west);
        AssertThat(shift >= 25 && shift <= 45).OverrideFailureMessage($"The shift came {shift} frames after the thaw, not the 40 it had left.").IsTrue();

        AssertThat(manager.BeginPlacementHold(StoryRecoveryHoldCause.CollapseResume)).IsTrue();
        await GrLevelSteps.Frames(tree, 2);
        level.OpenOverseerGusts();
        StoryCyclicHazard gust = level.OverseerGusts[0];
        AssertThat(gust.Enabled).IsTrue();
        await GrLevelSteps.Frames(tree, 10);
        AssertThat(gust.Phase).OverrideFailureMessage("A vent started inside a hold.").IsEqual(HazardPhase.Cooldown);
        for (frames = 0; frames < 120 && ChronalRewindManager.IsWorldHeld(tree); frames++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        for (frames = 0; frames < 240 && gust.Phase == HazardPhase.Cooldown; frames++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(gust.Phase).OverrideFailureMessage("The vent did not go on at the thaw.").IsEqual(HazardPhase.Warning);
    }

    private static async Task<int> FramesUntilShift(SceneTree tree, GravityFieldZone field) {
        int index = field.CycleIndex;
        for (int frame = 1; frame <= 400; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            if (field.CycleIndex != index) return frame;
        }
        return -1;
    }
}
