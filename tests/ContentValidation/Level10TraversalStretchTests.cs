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
/// GR-LV4: Level 10's carve, its stretch and its swarm on the real scene. The shared rules are
/// <c>TraversalStretchContentTests</c>; these are this level's own facts.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level10TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_10_Globe.tscn";

    /// <summary>A carve, not a shift: both locks shrank, par took its +30, and the traps, the ropes and the arena did not move.</summary>
    [TestCase]
    public void TheYardAndTheStageWereCarvedAndNothingElseMoved() {
        using var fixture = new GrLevelFixture<Level10Controller>(ScenePath, "einstein");
        Level10Controller level = fixture.Level;
        AssertThat(level.YardLock.Area.End.X).IsEqual(2000f);
        AssertThat(level.StageLock.Area.Position.X).IsEqual(2900f);
        AssertThat(level.StageLock.Area.End.X).IsEqual(5740f);
        AssertThat(level.GalleriesLock.Area.Position.X).IsEqual(6080f);

        TraversalStretch stageFront = level.GetChildren().OfType<TraversalStretch>().Single();
        AssertThat(stageFront.MinX).IsEqual(2020f);
        AssertThat(stageFront.MaxX).IsEqual(2880f);
        AssertThat(stageFront.HasTrench).IsTrue();
        AssertThat(level.ParSeconds).IsEqual(390f);
        AssertThat(level.AuthoredSecretCachePosition).IsEqual(new Vector2(2140f, 505f));

        // Unmoved: every trap, every rope, both later anchors, the font, the arena and its recess.
        AssertThat(string.Join(",", Level10Controller.TrapdoorCentresX)).IsEqual("3160,3900,4640,9080,9560");
        AssertThat(level.Rigging.Count).IsEqual(4);
        AssertThat(level.Rigging[0].GlobalPosition.X).IsEqual(6340f);
        AssertThat(Level10Controller.MiddleCheckpointX).IsEqual(6040f);
        AssertThat(Level10Controller.PreBossCheckpointX).IsEqual(8460f);
        AssertThat(level.AuthoredRestorationFontPosition?.X).IsEqual(8760f);
        AssertThat(Level10Controller.BossArenaWestX).IsEqual(9000f);
        AssertThat(Level10Controller.ArenaRecessWestX).IsEqual(8970f);
        AssertThat(Level10Controller.ArenaRecessEastX).IsEqual(9670f);

        // The cellar's west wall followed the lock: no way out of the fight runs under the boards.
        AssertThat(Level10Controller.CellarWestWallX).IsEqual(level.StageLock.EntryBarrier.Footprint.Position.X);
    }

    /// <summary>
    /// A hero in the trench is never stuck: Lincoln walks down the west ramp, along the floor to
    /// the east ramp and onto the boards, and back out by the west ramp, in real frames.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSlowestHeroWalksInAndOutOfTheTrench() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GrLevelFixture<Level10Controller>(ScenePath, "lincoln");
        Level10Controller level = fixture.Level;
        PlayerController hero = level.Player;
        var source = new BufferedInputSource();
        try {
            await GrLevelSteps.Settle(tree);
            level.AudienceWatchEnabled = false;
            foreach (StoryCyclicHazard lane in level.GetChildren().OfType<StoryCyclicHazard>()) lane.Enabled = false;
            InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
            hero.GlobalPosition = new Vector2(2030f, Level10Controller.GroundY - 10f);
            hero.Velocity = Vector2.Zero;

            source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
            float deepest = 0f;
            for (int frame = 0; frame < 600 && hero.GlobalPosition.X < 2860f; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                deepest = Mathf.Max(deepest, hero.GlobalPosition.Y);
            }
            AssertThat(hero.GlobalPosition.X >= 2860f && hero.GlobalPosition.Y <= Level10Controller.GroundY + 1f)
                .OverrideFailureMessage($"Lincoln stalled in the trench at {hero.GlobalPosition}.").IsTrue();
            AssertThat(deepest >= 1238f).OverrideFailureMessage($"He never reached the trench floor: deepest y {deepest:0}.").IsTrue();

            source.SetNextFrame(PlayerInputFrame.Create(0, -1f, 0f, GameplayButtons.None, GameplayButtons.None));
            for (int frame = 0; frame < 600 && hero.GlobalPosition.X > 2045f; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(hero.GlobalPosition.X <= 2045f && hero.GlobalPosition.Y <= Level10Controller.GroundY + 1f)
                .OverrideFailureMessage($"Lincoln did not walk back out by the west ramp: {hero.GlobalPosition}.").IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
        }
    }

    /// <summary>
    /// The swarm row on the real scene: on the Galleries' floor, far from the Yard, once at most two
    /// of the lock's actors are left. The audience's stillness clock stands still through the
    /// lock, and a fodder kill pays nothing (F05).
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheGalleriesSwarmComesAtTwoAliveWithTheAudienceHeldAndPaysNothing() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GrLevelFixture<Level10Controller>(ScenePath, "einstein");
        Level10Controller level = fixture.Level;
        PlayerController hero = level.Player;
        await GrLevelSteps.Settle(tree);
        level.YardLock.RestoreCleared();
        level.StageLock.RestoreCleared();
        hero.GlobalPosition = new Vector2(6300f, Level10Controller.EnemyGroundY);
        hero.Velocity = Vector2.Zero;
        await GrLevelSteps.Frames(tree, 4);
        EncounterLock galleries = level.GalleriesLock;
        AssertThat(galleries.State).IsEqual(EncounterLockState.Engaged);
        AssertThat(level.AudienceHeldByEncounterLock).IsTrue();
        int actors = galleries.LivingMemberCount;
        AssertThat(actors).IsEqual(Level10Controller.Room3Spawns.Length);

        galleries.LivingMembers.First().TakeDamage(99999);
        await GrLevelSteps.Frames(tree, 3);
        AssertThat(galleries.LivingMemberCount).OverrideFailureMessage("Three left must not bring the row.").IsEqual(actors - 1);
        galleries.LivingMembers.First().TakeDamage(99999);
        await GrLevelSteps.Frames(tree, 6);

        int row = Level10Controller.SwarmSpawnXs.Length;
        AssertThat(row).IsEqual(SwarmWaveRules.RowMobsForLockWidth(galleries.Area.Size.X));
        AssertThat(galleries.LivingMemberCount).IsEqual(actors - 2 + row);
        foreach (float x in Level10Controller.SwarmSpawnXs) {
            AssertThat(x >= galleries.Area.Position.X && x < galleries.Area.End.X).IsTrue();
            AssertThat(x > level.YardLock.Area.End.X).IsTrue();
        }

        // The clock has not moved in a lock with the hero standing still, for longer than it takes to arm.
        for (int frame = 0; frame < 200; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            if (galleries.State != EncounterLockState.Engaged) break;
        }
        AssertThat(level.AudienceIdleSeconds).IsEqual(0f);
        AssertThat(level.AudienceArmed).IsFalse();

        int wallet = StoryManager.Instance.ChronalDustCollected;
        EnemyController fodder = galleries.LivingMembers.First(member => member.Data.DustWeightZero);
        fodder.TakeDamage(99999);
        await GrLevelSteps.Frames(tree, 6);
        AssertThat(StoryManager.Instance.ChronalDustCollected).IsEqual(wallet);
    }
}
