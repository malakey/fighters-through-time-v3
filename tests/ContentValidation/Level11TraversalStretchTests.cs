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
/// GR-LV4: Level 11's carve, its stretch and its two swarm rows on the real scene. The shared
/// rules are <c>TraversalStretchContentTests</c>; these are this level's own facts.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level11TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_11_Gettysburg.tscn";

    /// <summary>A carve, not a shift: the ridge shrank, par took its +30, every gap keeps cover, and nothing past the stretch moved.</summary>
    [TestCase]
    public void TheRidgeWasCarvedAndTheCoverGapsAndEverythingPastTheStretchHeld() {
        using var fixture = new GrLevelFixture<Level11Controller>(ScenePath, "einstein");
        Level11Controller level = fixture.Level;
        AssertThat(level.SeminaryRidgeLock.Area.End.X).IsEqual(2000f);
        AssertThat(level.WheatfieldLock.Area.Position.X).IsEqual(3220f);

        TraversalStretch road = level.GetChildren().OfType<TraversalStretch>().Single();
        AssertThat(road.MinX).IsEqual(2020f);
        AssertThat(road.MaxX).IsEqual(3200f);
        AssertThat(road.HasTrench).IsTrue();
        AssertThat(level.ParSeconds).IsEqual(390f);

        // The sandbags moved inside the same gap between two columns, clear of every column.
        AssertThat(Level11Controller.UncoveredArtilleryGaps().Count).IsEqual(0);
        foreach ((float x, Level11Controller.CoverKind _) in Level11Controller.CoverPositions) {
            AssertThat(Level11Controller.IsInsideBlastColumn(x)).IsFalse();
        }

        // Unmoved: both later anchors, the font, the arena, the arrays, the level's width and the secret.
        AssertThat(Level11Controller.MiddleCheckpointX).IsEqual(6340f);
        AssertThat(Level11Controller.PreBossCheckpointX).IsEqual(9080f);
        AssertThat(level.AuthoredRestorationFontPosition?.X).IsEqual(9400f);
        AssertThat(Level11Controller.BossArenaWestX).IsEqual(10000f);
        AssertThat(Level11Controller.ArrayAlphaX).IsEqual(5400f);
        AssertThat(Level11Controller.LevelWidth).IsEqual(11520f);
        AssertThat(level.AuthoredSecretCachePosition).IsEqual(new Vector2(5150f, 465f));
    }

    /// <summary>
    /// Lincoln walks down the west ramp to the trench and back out, and a double jump from the east
    /// ramp clears the wall into the deck's pocket by the Wheatfield, in real frames (GR-LV1's
    /// lesson: a wall step needs clear air over the pocket beside it).
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSlowestHeroWalksTheTrenchAndClearsTheWall() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GrLevelFixture<Level11Controller>(ScenePath, "lincoln");
        Level11Controller level = fixture.Level;
        PlayerController hero = level.Player;
        var source = new BufferedInputSource();
        try {
            await GrLevelSteps.Settle(tree);
            foreach (StoryCyclicHazard lane in level.GetChildren().OfType<StoryCyclicHazard>()) lane.Enabled = false;
            InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
            hero.GlobalPosition = new Vector2(2040f, Level11Controller.GroundY - 10f);
            hero.Velocity = Vector2.Zero;

            source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
            float deepest = 0f;
            for (int frame = 0; frame < 480 && hero.GlobalPosition.X < 2840f; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                deepest = Mathf.Max(deepest, hero.GlobalPosition.Y);
            }
            AssertThat(deepest >= 1018f).OverrideFailureMessage($"He never reached the trench floor: deepest y {deepest:0}.").IsTrue();

            source.SetNextFrame(PlayerInputFrame.Create(0, -1f, 0f, GameplayButtons.None, GameplayButtons.None));
            for (int frame = 0; frame < 480 && hero.GlobalPosition.X > 2045f; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(hero.GlobalPosition.X <= 2045f && hero.GlobalPosition.Y <= Level11Controller.GroundY + 1f)
                .OverrideFailureMessage($"Lincoln did not walk back out by the west ramp: {hero.GlobalPosition}.").IsTrue();

            hero.GlobalPosition = new Vector2(3120f, 900f);
            hero.Velocity = Vector2.Zero;
            source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
            await GrLevelSteps.Frames(tree, 20);
            await GrLevelSteps.DoubleJump(tree, hero, source, 1f, () => hero.GlobalPosition.X > 3190f && hero.IsOnFloor());
            AssertThat(hero.GlobalPosition.X > 3190f && hero.GlobalPosition.Y <= Level11Controller.GroundY + 1f)
                .OverrideFailureMessage($"Lincoln did not clear the wall: {hero.GlobalPosition}.").IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
        }
    }

    /// <summary>
    /// The Wheatfield's swarm on the real scene: it comes at two alive, and an artillery column leaves
    /// a walking swarmling alone and kills a launched one (ruling 7d), which pays nothing (F05).
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheWheatfieldSwarmComesAtTwoAliveAndALaunchedSwarmlingDiesInAColumn() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GrLevelFixture<Level11Controller>(ScenePath, "einstein");
        Level11Controller level = fixture.Level;
        PlayerController hero = level.Player;
        await GrLevelSteps.Settle(tree);
        level.SeminaryRidgeLock.RestoreCleared();
        hero.GlobalPosition = new Vector2(3300f, Level11Controller.EnemyGroundY);
        hero.Velocity = Vector2.Zero;
        await GrLevelSteps.Frames(tree, 4);
        EncounterLock wheatfield = level.WheatfieldLock;
        AssertThat(wheatfield.State).IsEqual(EncounterLockState.Engaged);
        int riflemen = wheatfield.LivingMemberCount;
        AssertThat(riflemen).IsEqual(Level11Controller.Room2Spawns.Length);

        wheatfield.LivingMembers.First().TakeDamage(99999);
        await GrLevelSteps.Frames(tree, 3);
        AssertThat(wheatfield.LivingMemberCount).OverrideFailureMessage("Three left must not bring the row.").IsEqual(riflemen - 1);
        wheatfield.LivingMembers.First().TakeDamage(99999);
        await GrLevelSteps.Frames(tree, 6);
        int row = Level11Controller.WheatfieldSwarmSpawnXs.Length;
        AssertThat(row).IsEqual(SwarmWaveRules.RowMobsForLockWidth(wheatfield.Area.Size.X));
        AssertThat(wheatfield.LivingMemberCount).IsEqual(riflemen - 2 + row);

        var column = level.GetNode<StoryCyclicHazard>("ArtilleryWheatB");
        EnemyController fodder = wheatfield.LivingMembers.First(member => member.Data.DustWeightZero);
        Vector2 inColumn = new(column.GlobalPosition.X, Level11Controller.EnemyGroundY);
        fodder.GlobalPosition = inColumn;
        fodder.Velocity = Vector2.Zero;
        column.ForcePhase(HazardPhase.Active, 0.5f);
        await GrLevelSteps.Frames(tree, 4);
        AssertThat(wheatfield.LivingMembers.Contains(fodder))
            .OverrideFailureMessage("A column killed a swarmling that was only walking.").IsTrue();

        int wallet = StoryManager.Instance.ChronalDustCollected;
        fodder.GlobalPosition = inColumn;
        fodder.LaunchThrown(new Vector2(0f, -60f), 0);
        column.ForcePhase(HazardPhase.Active, 0.5f);
        await GrLevelSteps.Frames(tree, 6);
        AssertThat(wheatfield.LivingMembers.Contains(fodder))
            .OverrideFailureMessage("A column left a launched swarmling alive.").IsFalse();
        await GrLevelSteps.Frames(tree, 6);
        AssertThat(StoryManager.Instance.ChronalDustCollected).IsEqual(wallet);
    }
}
