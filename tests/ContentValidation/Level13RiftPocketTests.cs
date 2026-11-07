using System;
using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Level 13's rift pockets must be crossable by walking, by every hero (no level may require a
/// specific hero). A pocket halves the walker's speed, so the slowest hero (read from the
/// character data, never copied here) needs the longest to cross; the snap delay has to clear
/// that with a margin. <c>PROVISIONAL-L13-RIFT-SNAP-SLOWEST-HERO</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level13RiftPocketTests {
    private const string ScenePath = "res://scenes/campaign/Level_13_ChronalVoid.tscn";
    private const float PixelsPerUnit = 60f;
    private const float RequiredMarginSeconds = 0.4f;

    private static (string id, float speed) SlowestHero() {
        return CharacterRoster.IDs
            .Select(id => (id, speed: AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres").MaxMoveSpeed))
            .OrderBy(pair => pair.speed).First();
    }

    /// <summary>The snap delay clears the slowest hero's walk across the widest pocket, at half speed, by 0.4 s.</summary>
    [TestCase]
    public void TheSnapDelayClearsTheSlowestHeroesWalkAcrossTheWidestPocketWithAMargin() {
        (string slowestID, float speed) = SlowestHero();
        using var fixture = new Fixture(slowestID);
        float halfSpeed = speed * PixelsPerUnit * StatusMovementMultiplierOfAPocket();
        foreach (ChronalRiftZone rift in fixture.Level.RiftPockets) {
            float width = rift.GetChildren().OfType<CollisionShape2D>().Select(shape => ((CircleShape2D)shape.Shape).Radius * 2f).Max()
                + CharacterFactory.HeroBodyBoxWidthPixels;
            float crossing = width / halfSpeed;
            AssertThat(rift.SnapDelay >= crossing + RequiredMarginSeconds)
                .OverrideFailureMessage($"{rift.RiftID}: snap {rift.SnapDelay:0.00}s, {slowestID} needs {crossing:0.00}s + {RequiredMarginSeconds}s.").IsTrue();
            AssertThat(rift.SnapDelay).IsEqual(Level13Controller.RiftSnapDelaySeconds);
        }
    }

    private static float StatusMovementMultiplierOfAPocket() => Math.Max(0.1f, 1f - 0.5f * 1f);

    /// <summary>The slowest hero walks through each pocket in real frames and is never snapped back.</summary>
    [TestCase(Timeout = 120000)]
    public async Task TheSlowestHeroWalksThroughEveryPocketWithoutASnap() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        (string slowestID, _) = SlowestHero();
        bool oldSuppress = EncounterLock.SuppressForTesting;
        EncounterLock.SuppressForTesting = true;
        try {
            using var fixture = new Fixture(slowestID);
            Level13Controller level = fixture.Level;
            PlayerController hero = level.Player;
            var source = new BufferedInputSource();
            try {
                await Settle(tree);
                InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
                foreach (ChronalRiftZone rift in level.RiftPockets) {
                    int snaps = 0;
                    rift.TimeLoopSnapped += (_, _) => snaps++;
                    float startX = rift.GlobalPosition.X - 320f;
                    float endX = rift.GlobalPosition.X + 320f;
                    hero.GlobalPosition = new Vector2(startX, Level13Controller.ShelfY - 2f);
                    hero.Velocity = Vector2.Zero;
                    level.SyncGravityFieldToPlayer();
                    for (int frame = 0; frame < 900 && hero.GlobalPosition.X < endX && snaps == 0; frame++) {
                        source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
                        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                    }
                    AssertThat(snaps).OverrideFailureMessage($"{slowestID} was snapped back in {rift.RiftID} while walking (x {hero.GlobalPosition.X:0}).").IsEqual(0);
                    AssertThat(hero.GlobalPosition.X >= endX)
                        .OverrideFailureMessage($"{slowestID} did not cross {rift.RiftID}: {hero.GlobalPosition}.").IsTrue();
                }
            } finally {
                InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
                PoolManager.Instance?.ReleaseActiveUnder(level);
            }
        } finally {
            EncounterLock.SuppressForTesting = oldSuppress;
        }
    }

    private static async Task Settle(SceneTree tree) {
        for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        DialogueManager dialogue = DialogueManager.Instance;
        for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
    }

    private sealed class Fixture : IDisposable {
        public readonly Level13Controller Level;
        private readonly int _slot;
        private readonly string _character;
        private readonly bool _paused;

        public Fixture(string heroID) {
            var tree = (SceneTree)Engine.GetMainLoop();
            _paused = tree.Paused;
            _slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _character = GameManager.Instance.CurrentSession.SelectedCharacterID;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            GameManager.Instance.CurrentSession.SelectedCharacterID = heroID;
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level13Controller>();
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            if (GodotObject.IsInstanceValid(Level)) {
                PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _slot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _character;
            ((SceneTree)Engine.GetMainLoop()).Paused = _paused;
        }
    }
}
