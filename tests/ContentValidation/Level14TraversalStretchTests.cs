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
/// GR-LV5: Level 14's carve and the Breach Catwalk on the real scene. The shared rules are
/// <c>TraversalStretchContentTests</c>; these are the level's own facts. The catwalk has no
/// trench (the deck stands 100 px over the level's floor edge): its low road is the deck,
/// timed through two beams, and its first step stands just clear of a beam's strip.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level14TraversalStretchTests {
    private const string ScenePath = "res://scenes/campaign/Level_14_NeoEarth.tscn";

    /// <summary>A carve, not a shift: the Breach Gallery shrank, par took its +30, and nothing past the catwalk moved.</summary>
    [TestCase]
    public void TheBreachGalleryWasCarvedAndNothingPastTheCatwalkMoved() {
        using var fixture = new Fixture("einstein");
        Level14Controller level = fixture.Level;
        AssertThat(level.BreachGalleryLock.Area.Position.X).IsEqual(320f);
        AssertThat(level.BreachGalleryLock.Area.End.X).IsEqual(2000f);
        AssertThat(level.ContainmentWingLock.Area.Position.X).IsEqual(2920f);

        TraversalStretch catwalk = Children(level).OfType<TraversalStretch>().Single();
        AssertThat(catwalk.MinX).IsEqual(2020f);
        AssertThat(catwalk.MaxX).IsEqual(2900f);
        AssertThat(catwalk.HasTrench).IsFalse();

        // 540 seeded + 30 for the catwalk.
        AssertThat(level.ParSeconds).IsEqual(570f);
        // Unmoved: the PreBoss anchor, the font, the arena, the width and the secret.
        AssertThat(Level14Controller.Checkpoint2Position.X).IsEqual(9220f);
        AssertThat(level.AuthoredRestorationFontPosition?.X).IsEqual(9850f);
        AssertThat(Level14Controller.BossArenaWestX).IsEqual(10200f);
        AssertThat(Level14Controller.LevelWidth).IsEqual(11800f);
        AssertThat(level.AuthoredSecretCachePosition).IsEqual(new Vector2(860f, 1355f));
        // The route pays the moved Resonance Hold node, over the Gantry line's end.
        AssertThat(Level14Controller.ExtractorPlacements[0].Position).IsEqual(new Vector2(2800f, 932f));
        // The catwalk's beam, under its third step.
        StoryCyclicHazard beam = Children(level).OfType<StoryCyclicHazard>()
            .Single(hazard => hazard.HazardID == Level14Controller.BreachBeamID);
        AssertThat(beam.GlobalPosition.X).IsEqual(Level14Controller.BreachBeamX);
    }

    /// <summary>
    /// The catwalk's first step is the line's biggest rise (230 px) and stands over the third
    /// perimeter beam. In real frames Lincoln, the lowest jumper, double-jumps onto it from the
    /// deck, and a firing beam under him does nothing; on the deck the same beam hits him.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task LincolnClimbsOntoTheFirstStepAndTheBeamUnderItMissesHim() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("lincoln");
        Level14Controller level = fixture.Level;
        PlayerController hero = level.Player;
        var source = new BufferedInputSource();
        try {
            await Settle(tree);
            StoryCyclicHazard beam = level.GetNode<StoryCyclicHazard>("PerimeterBeam3");
            foreach (StoryCyclicHazard lane in Children(level).OfType<StoryCyclicHazard>()) lane.Enabled = false;
            InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
            hero.GlobalPosition = new Vector2(2150f, Level14Controller.DeckY - 2f);
            hero.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 6; frame++) await Step(tree, source, GameplayButtons.None);

            // First jump held to its apex, one tick off, the second held until he lands.
            int stage = 0;
            for (int frame = 0; frame < 240; frame++) {
                bool grounded = hero.IsOnFloor();
                if (stage == 3 && grounded) break;
                GameplayButtons held = GameplayButtons.None;
                switch (stage) {
                    case 0: held = GameplayButtons.Jump; stage = 1; break;
                    case 1:
                        held = GameplayButtons.Jump;
                        if (!grounded && hero.Velocity.Y >= 0f) stage = 2;
                        break;
                    case 2: stage = 3; break;
                    default: held = GameplayButtons.Jump; break;
                }
                await Step(tree, source, held);
            }
            float stepTop = Level14Controller.BreachCatwalkFirstTopY;
            AssertThat(hero.IsOnFloor() && Mathf.Abs(hero.GlobalPosition.Y - stepTop) <= 2f)
                .OverrideFailureMessage($"Lincoln did not land on the first step (top {stepTop}): {hero.GlobalPosition}.").IsTrue();

            int hp = hero.CurrentHP;
            beam.Enabled = true;
            beam.ForcePhase(HazardPhase.Active, 1f);
            for (int frame = 0; frame < 20; frame++) await Step(tree, source, GameplayButtons.None);
            AssertThat(hero.CurrentHP).OverrideFailureMessage("The beam under the first step hit Lincoln on it.").IsEqual(hp);

            // The control: on the deck, the same beam hits.
            hero.GlobalPosition = new Vector2(2160f, Level14Controller.DeckY - 2f);
            hero.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await Step(tree, source, GameplayButtons.None);
            beam.ForcePhase(HazardPhase.Active, 1f);
            for (int frame = 0; frame < 10; frame++) await Step(tree, source, GameplayButtons.None);
            AssertThat(hero.CurrentHP < hp).OverrideFailureMessage("The beam never hit Lincoln on the deck either.").IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    /// <summary>The Gantry line's longest hop is real: Lincoln clears it with a double jump, in real frames.</summary>
    [TestCase(Timeout = 60000)]
    public async Task LincolnClearsTheGantryLinesLongestHopInRealFrames() {
        TraversalContentKit.RouteFact route = TraversalContentKit.Levels().Single(level => level.Index == 14).Routes.Single();
        TraversalSurface from = route.BaseLine[^2];
        TraversalSurface to = route.BaseLine[^1];
        AssertThat(TraversalRules.HopGap(from, to)).IsEqual(245f);
        (bool landed, string report) = await TraversalContentKit.Fly("lincoln", from, to, TraversalContentKit.FlightAbility.None);
        GD.Print($"[reach] {report}");
        AssertThat(landed).OverrideFailureMessage(report).IsTrue();
    }

    private static GameplayButtons _previous;

    /// <summary>One physics frame of input; the last frame's buttons give the press and release edges.</summary>
    private static async Task Step(SceneTree tree, BufferedInputSource source, GameplayButtons held) {
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, held, _previous));
        _previous = held;
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }

    private static async Task Settle(SceneTree tree) {
        for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        DialogueManager dialogue = DialogueManager.Instance;
        for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
        AssertThat(tree.Paused).IsFalse();
    }

    private static Node[] Children(Node parent) {
        Godot.Collections.Array<Node> children = parent.GetChildren();
        using var lifetime = children.AsDisposable();
        return children.ToArray();
    }

    /// <summary>The real scene against no save, with the session handed back.</summary>
    private sealed class Fixture : IDisposable {
        public readonly Level14Controller Level;
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
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level14Controller>();
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _slot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _character;
            ((SceneTree)Engine.GetMainLoop()).Paused = _paused;
        }
    }
}
