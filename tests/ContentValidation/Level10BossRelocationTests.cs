using System;
using System.Threading.Tasks;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.Tests.Unit;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// 2026-10-09 (VERIFY-L10-ARENA-RECESS-EXIT, boss half), in the real Globe
/// scene. Bot telemetry caught the Tragedy King dropping into the arena recess
/// through a Phase 2 trap and then casting <c>exit_stage_left</c> (180..360 px
/// past his target), which put him past the recess's east wall at recess height,
/// under the boards, where he fell forever and the level could not be won. His
/// teleport now lands inside the recess, east and west, on its floor; and if he
/// ever leaves the world anyway, the arena fall net puts him back on the stage.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level10BossRelocationTests {
    private const float Step = 1f / 60f;
    private const string ScenePath = "res://scenes/campaign/Level_10_Globe.tscn";
    private const string ExitStageLeftPath = "res://resources/Bosses/Abilities/tragedy_king/exit_stage_left.tres";
    private const int ScratchSlot = 2;
    /// <summary>Boss.tscn's body is 96 px wide.</summary>
    private const float KingHalfWidth = 48f;

    [TestCase]
    public async Task ExitStageLeftFromTheArenaRecessLandsInsideItEastAndWest() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GlobeFixture();
        // This case runs real frames, so go slotless: no snapshot or checkpoint
        // write may reach a real slot file.
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level10Controller level = fixture.Level;
        level.AudienceWatchEnabled = false;
        AssertObject(level.KingEncounter).IsNotNull();
        BossController king = level.KingEncounter.Boss;
        AssertObject(king).IsNotNull();
        // Phase 2's state: the arena traps open, so the recess is open above.
        foreach (TrapdoorPlatform door in level.ArenaTrapdoors) door.Open();
        EnemyAbilityData exit = AuthoredResources.Load<EnemyAbilityData>(ExitStageLeftPath);
        AssertThat(exit.Archetype).IsEqual(EnemyAbilityArchetype.Teleport);

        float floor = Level10Controller.ArenaRecessFloorY;
        float westmost = Level10Controller.ArenaRecessWestX + KingHalfWidth;
        float eastmost = Level10Controller.ArenaRecessEastX - KingHalfWidth;
        var inRecess = new Vector2(9320f, floor);
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

        // East (a hero on the island side of the east trap) and west, several
        // range draws each: every draw would carry him past a wall.
        foreach (Vector2 target in new[] { new Vector2(9500f, floor), new Vector2(9150f, floor) }) {
            for (int cast = 0; cast < 4; cast++) {
                await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
                    king.GlobalPosition = inRecess;
                    king.Velocity = Vector2.Zero;
                    king.BeginAbility(exit, target);
                    for (int frame = 0; frame < 240 && king.AbilityPhase != EnemyAbilityPhase.Idle; frame++) {
                        king.TickAbility(Step);
                    }
                    Vector2 at = king.GlobalPosition;
                    AssertThat(at.X >= westmost - 0.5f && at.X <= eastmost + 0.5f)
                        .OverrideFailureMessage(
                            $"exit_stage_left toward {target} put the King at {at} ({king.LastTeleportLanding}), " +
                            $"outside the recess {Level10Controller.ArenaRecessWestX}..{Level10Controller.ArenaRecessEastX}.")
                        .IsTrue();
                    AssertFloat(at.Y).IsEqualApprox(floor, 0.01f);
                    AssertThat(king.LastTeleportLanding.Outcome).IsEqual(RelocationOutcome.ClippedAtWall);
                });
            }
        }

        // ...and he stands there: real frames, never below the recess floor.
        float lowestFeet = king.GlobalPosition.Y;
        float minX = king.GlobalPosition.X;
        float maxX = king.GlobalPosition.X;
        for (int frame = 0; frame < 45; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            lowestFeet = Mathf.Max(lowestFeet, king.GlobalPosition.Y);
            minX = Mathf.Min(minX, king.GlobalPosition.X);
            maxX = Mathf.Max(maxX, king.GlobalPosition.X);
        }
        AssertThat(lowestFeet <= floor + 0.5f)
            .OverrideFailureMessage($"The King's feet dropped to y {lowestFeet:0.0}, below the recess floor {floor}.")
            .IsTrue();
        AssertThat(minX >= westmost - 0.5f && maxX <= eastmost + 0.5f)
            .OverrideFailureMessage($"The King drifted over x {minX:0.0}..{maxX:0.0}, outside the recess.")
            .IsTrue();
        AssertThat(king.ArenaFallRecoveries).IsEqual(0);
    }

    /// <summary>
    /// The net, in the scene where it was needed: a King set down past the
    /// recess's east wall, under the boards, falls — and the frame his feet pass
    /// the arena's fall net (the level's bottom plus the net's margin) he is back
    /// on his spawn point on the stage, unharmed, once.
    /// </summary>
    [TestCase]
    public async Task AKingWhoFallsOutUnderTheBoardsIsPutBackOnTheStage() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new GlobeFixture();
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level10Controller level = fixture.Level;
        level.AudienceWatchEnabled = false;
        BossController king = level.KingEncounter?.Boss;
        AssertObject(king).IsNotNull();
        Rect2 arena = king.ArenaBounds;
        AssertThat(king.HasArenaLeash).IsTrue();
        AssertFloat(arena.End.Y).IsEqualApprox(Level10Controller.LevelHeight, 0.01f);
        var spawn = new Vector2(Level10Controller.BossSpawnX, Level10Controller.EnemyGroundY);
        int hp = king.CurrentHP;
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

        king.GlobalPosition = new Vector2(10000f, 1300f);
        king.Velocity = Vector2.Zero;
        float deepest = king.GlobalPosition.Y;
        for (int frame = 0; frame < 180 && king.ArenaFallRecoveries == 0; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            deepest = Mathf.Max(deepest, king.GlobalPosition.Y);
        }
        AssertThat(king.ArenaFallRecoveries)
            .OverrideFailureMessage($"The King fell to y {deepest:0} and was never brought back.")
            .IsEqual(1);
        float netLine = BossController.ArenaFallNetLineY(arena);
        AssertThat(deepest <= netLine)
            .OverrideFailureMessage($"The King ended a frame at y {deepest:0}, below the arena's fall net at y {netLine}.")
            .IsTrue();
        AssertThat(king.LastArenaFallAnchor).IsEqual(spawn);
        AssertThat(king.CurrentHP).IsEqual(hp);

        // He lands on the boards under his spawn and stays.
        for (int frame = 0; frame < 30; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertFloat(king.GlobalPosition.X).IsEqualApprox(spawn.X, 1f);
        AssertFloat(king.GlobalPosition.Y).IsEqualApprox(Level10Controller.GroundY, 1f);
        AssertThat(king.ArenaFallRecoveries).IsEqual(1);
    }

    /// <summary>Loads the Globe resumed at its entry checkpoint, on a scratch slot restored on dispose.</summary>
    private sealed class GlobeFixture : IDisposable {
        public readonly Level10Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public GlobeFixture() {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            SaveManager.Instance.SaveSlots[ScratchSlot] = new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.London),
                LastCheckpointID = Level10Controller.Checkpoint0,
                CurrentHP = 100,
                CurrentUltimateMeter = 50f
            };

            PackedScene packed = ResourceLoader.Load<PackedScene>(ScenePath);
            Level = packed.Instantiate<Level10Controller>();
            Level.Name = "Level10BossRelocationFixture";
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }
}
