using System;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// G1 (playtest bots, 2026-10-04): Level 6 was unwinnable. The winch puzzle is the
/// only way past the rockfall, and two defects stopped it in real play while the
/// content test (which registers loads by hand) stayed green:
/// <list type="number">
/// <item>The pressure-plate template still masked the props' pre-Package-12
/// PersistentObject layer. W8 (M02) moved the movable weights to Environment, so
/// no prop ever entered a plate by physics; only the west pan's authored load was
/// registered, by hand, in <c>Level06Controller.OnLevelReady</c>.</item>
/// <item>Godot Physics 2D does not wake a sleeping rigid body when the static shape
/// under it is disabled, so the pumice block stayed asleep in mid-air after its
/// wedge broke.</item>
/// </list>
/// This suite plays the puzzle on the real scene with real physics frames: break
/// the wedge, the block falls onto the east pan, the hero stands on the pan, the
/// pans balance and the road opens.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level06RockfallPhysicsTests {
    private const string ScenePath = "res://scenes/campaign/Level_06_Pompeii.tscn";
    private const string LevelID = "level_06_pompeii";
    private const int ScratchSlot = 2;
    /// <summary>A 550 px fall plus settling, with room to spare.</summary>
    private const int SettleFrameCap = 300;

    [TestCase]
    public void ThePlateTemplateSeesThePlayerAndTheMovableWeights() {
        var plate = ResourceLoader.Load<PackedScene>("res://scenes/templates/PressurePlateTemplate.tscn")
            .Instantiate<PressurePlate>();
        var weight = ResourceLoader.Load<PackedScene>("res://scenes/templates/MovableWeightTemplate.tscn")
            .Instantiate<WeightedObject>();
        try {
            AssertThat(plate.CollisionMask & weight.CollisionLayer)
                .OverrideFailureMessage("A pressure plate cannot see the layer the movable weights live on.")
                .IsNotEqual(0u);
            AssertThat(plate.CollisionMask & CollisionLayers.BodyLayerForFighterSlot(0))
                .OverrideFailureMessage("A pressure plate cannot see the Story player's body.")
                .IsNotEqual(0u);
        } finally {
            plate.Free();
            weight.Free();
        }
    }

    [TestCase]
    public async Task BreakingTheWedgeDropsThePumiceOntoTheEastPanAndTheHeroBalancesTheWinch() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new PompeiiFixture();
        Level06Controller level = fixture.Level;
        WeightedObject pumice = level.PumiceBoulder;
        AssertObject(pumice).IsNotNull();
        AssertObject(level.WedgeLock).IsNotNull();
        AssertObject(level.RightPan).IsNotNull();

        // Let the space register the authored bodies, the plates report their
        // overlaps, and the held block settle into sleep on its wedge — the state a
        // player always finds it in, and the one the engine never wakes it from.
        for (int frame = 0; frame < 240 && !(frame >= 10 && pumice.Sleeping); frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(pumice.Sleeping)
            .OverrideFailureMessage("The held pumice block never went to sleep on its wedge.")
            .IsTrue();
        AssertThat(level.LeftPan.IsCounting(level.BasaltBoulder)).IsTrue();
        AssertFloat(level.RightPan.CurrentWeight).IsEqualApprox(0f, 0.001f);
        AssertFloat(pumice.GlobalPosition.Y)
            .OverrideFailureMessage($"The pumice block should rest on its wedge, but is at {pumice.GlobalPosition}.")
            .IsLess(level.WedgeLock.GlobalPosition.Y);

        // Break the wedge the way a player's hit does: inside a physics callback,
        // so both the shape write and the wake are deferred off the flush.
        using (PhysicsCallbackGuard.Enter()) {
            for (int hit = 0; hit < level.WedgeLock.HitsToBreak; hit++) level.WedgeLock.TakeEnvironmentDamage(999f);
        }
        AssertThat(level.WedgeLock.IsDestroyed).IsTrue();

        bool landed = false;
        for (int frame = 0; frame < SettleFrameCap && !landed; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            landed = level.RightPan.IsCounting(pumice) && pumice.LinearVelocity.Length() < 1f;
        }
        AssertThat(landed)
            .OverrideFailureMessage(
                $"The pumice never settled on the east pan: it is at {pumice.GlobalPosition} " +
                $"(sleeping {pumice.Sleeping}), the pan reads {level.RightPan.CurrentWeight}.")
            .IsTrue();
        AssertFloat(level.RightPan.CurrentWeight).IsEqualApprox(2f, 0.001f);
        AssertThat(level.RockfallCleared)
            .OverrideFailureMessage("Two units on the east pan against three on the west must not balance.")
            .IsFalse();

        // The missing unit is the hero, standing on the pan beside the block.
        PlayerController hero = level.Player;
        hero.GlobalPosition = new Vector2(level.RightPan.GlobalPosition.X + 52f, 898f);
        hero.Velocity = Vector2.Zero;
        for (int frame = 0; frame < 90 && !level.RockfallCleared; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(level.RightPan.IsCounting(hero))
            .OverrideFailureMessage($"The hero at {hero.GlobalPosition} is not counted on the east pan.")
            .IsTrue();
        AssertFloat(level.RightPan.CurrentWeight).IsEqualApprox(3f, 0.001f);
        AssertThat(level.RockfallPuzzle.IsCompleted).IsTrue();
        AssertThat(level.RockfallCleared).IsTrue();
        var rockfall = level.GetNodeOrNull<StaticBody2D>("Rockfall");
        AssertThat(rockfall == null || rockfall.IsQueuedForDeletion())
            .OverrideFailureMessage("The rockfall is still solid after the winch balanced.").IsTrue();
    }

    /// <summary>
    /// The real Pompeii scene against a scratch save, resumed at the entry anchor
    /// (a fresh entry would defer the entrance dialogue, which pauses the tree);
    /// every shared singleton and the pause flag are handed back on dispose.
    /// </summary>
    private sealed class PompeiiFixture : IDisposable {
        public readonly Level06Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public PompeiiFixture() {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            SaveManager.Instance.SaveSlots[ScratchSlot] = new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Pompeii),
                LastCheckpointID = $"{LevelID}_checkpoint_0",
                CurrentHP = 100,
                CurrentUltimateMeter = 50f
            };

            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level06Controller>();
            Level.Name = "Level06RockfallPhysicsFixture";
            tree.Root.AddChild(Level);
            // The resume has been read. This case runs real frames, so go slotless:
            // no per-second snapshot or checkpoint write may reach a real slot file.
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
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
