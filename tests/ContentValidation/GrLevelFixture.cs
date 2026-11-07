using System;
using System.Threading.Tasks;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GR-LV4: the real level scene against no save, with the session and the pause handed back
/// on dispose. Not a suite. Needs the Godot runtime.
/// </summary>
internal sealed class GrLevelFixture<T> : IDisposable where T : StoryLevelControllerBase {
    public readonly T Level;
    private readonly int _slot;
    private readonly string _character;
    private readonly bool _paused;

    public GrLevelFixture(string scenePath, string heroID) {
        var tree = (SceneTree)Engine.GetMainLoop();
        _paused = tree.Paused;
        _slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        _character = GameManager.Instance.CurrentSession.SelectedCharacterID;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        GameManager.Instance.CurrentSession.SelectedCharacterID = heroID;
        Level = ResourceLoader.Load<PackedScene>(scenePath).Instantiate<T>();
        tree.Root.AddChild(Level);
    }

    public void Dispose() {
        if (GodotObject.IsInstanceValid(Level)) {
            PoolManager.Instance?.ReleaseActiveUnder(Level);
            Level.GetParent()?.RemoveChild(Level);
            Level.Free();
        }
        GameManager.Instance.CurrentSession.ActiveSaveSlot = _slot;
        GameManager.Instance.CurrentSession.SelectedCharacterID = _character;
        ((SceneTree)Engine.GetMainLoop()).Paused = _paused;
    }
}

/// <summary>The steps the GR-LV4 suites share.</summary>
internal static class GrLevelSteps {
    /// <summary>Lets the level's first frames pass and runs out any dialogue that started.</summary>
    public static async Task Settle(SceneTree tree) {
        for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        DialogueManager dialogue = DialogueManager.Instance;
        for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
        AssertThat(tree.Paused).IsFalse();
    }

    /// <summary>
    /// The hero walks into the arena: the real reveal, its intro beat and its dialogue run out, and
    /// then the boss is told to stand still on its anchor, so only the arena acts. The hero first
    /// stands short of the arena (<paramref name="approachX"/>) for a few frames, as a walk would:
    /// a teleport straight into the reveal leaves a zone it crossed still holding the hero.
    /// </summary>
    public static async Task EnterTheArena(SceneTree tree, StoryLevelControllerBase level, float approachX, Vector2 at) {
        await Settle(tree);
        FTT.Enemies.BossEncounterController encounter = level.BossEncounters[0];
        level.Player.GlobalPosition = new Vector2(approachX, at.Y);
        level.Player.Velocity = Vector2.Zero;
        await Frames(tree, 4);
        level.Player.GlobalPosition = at;
        level.Player.Velocity = Vector2.Zero;
        for (int frame = 0; frame < 300 && (!encounter.IsRevealed || encounter.IsIntroBeatRunning); frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(encounter.IsRevealed).IsTrue();
        await Settle(tree);
        encounter.Boss.IsEngaged = false;
        level.Player.GlobalPosition = at;
        level.Player.Velocity = Vector2.Zero;
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }

    public static async Task Frames(SceneTree tree, int count) {
        for (int frame = 0; frame < count; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }

    /// <summary>
    /// A double jump with the stick held, in real frames, until <paramref name="done"/> holds or the
    /// frames run out. A caught ledge is climbed with a fresh Jump press.
    /// </summary>
    public static async Task DoubleJump(SceneTree tree, FTT.Characters.PlayerController hero, BufferedInputSource source,
        float stick, Func<bool> done, int frames = 240) {
        GameplayButtons held = GameplayButtons.None;
        int stage = 0;
        for (int frame = 0; frame < frames && !done(); frame++) {
            GameplayButtons buttons = GameplayButtons.None;
            if (hero.CurrentState == FTT.Characters.CharacterState.LedgeHanging) {
                buttons = frame % 2 == 0 ? GameplayButtons.Jump : GameplayButtons.None;
            } else {
                switch (stage) {
                    // A fresh press every other frame until it takes: one that arrives mid-landing is lost.
                    case 0:
                        buttons = frame % 2 == 0 ? GameplayButtons.Jump : GameplayButtons.None;
                        if (!hero.IsOnFloor()) { stage = 1; buttons = GameplayButtons.Jump; }
                        break;
                    case 1: buttons = GameplayButtons.Jump; if (hero.Velocity.Y >= 0f) stage = 2; break;
                    case 2: stage = 3; break;
                    default: buttons = GameplayButtons.Jump; break;
                }
            }
            source.SetNextFrame(PlayerInputFrame.Create(0, stick, 0f, buttons, held));
            held = buttons;
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
    }
}
