using System;
using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GR-LV3 (ruling 14; design §6): the Dread Admiral's Phase 2 lists the deck and sets loose
/// cannonballs rolling down it. No tilt physics: two static slabs and two scripted balls.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level07AdmiralArenaTests {
    private const string ScenePath = "res://scenes/campaign/Level_07_Nassau.tscn";

    /// <summary>Everything is built dark, the slope obeys the 8° limit, and the balls' roll stays inside the arena.</summary>
    [TestCase]
    public void TheListIsBuiltDisabledWithASlopeUnderEightDegreesAndTheBallsRollInsideTheArena() {
        using var fixture = new Fixture("einstein");
        Level07Controller level = fixture.Level;
        AssertThat(level.AdmiralDeckListed).IsFalse();
        AssertThat(level.ListedDeckSlab.Visible).IsFalse();
        AssertThat(level.ListedDeckShape.Disabled).IsTrue();
        float tilt = Mathf.Abs(level.ListedDeckSlab.RotationDegrees);
        AssertThat(tilt <= 8f).OverrideFailureMessage($"The deck tilts {tilt:0.0}°, over the 8° limit.").IsTrue();
        AssertThat(tilt > 1f).IsTrue();
        AssertThat(level.Cannonballs.Count).IsEqual(2);
        Rect2 arena = Level07Controller.BossArenaBounds;
        foreach (RollingCannonball ball in level.Cannonballs) {
            AssertThat(ball.Enabled).IsFalse();
            AssertThat(ball.Phase).IsEqual(HazardPhase.Cooldown);
            AssertThat(ball.IsInGroup("story_hazard")).IsTrue();
            AssertThat(ball.RewindPolicy).IsEqual(StoryRewindPolicy.PreserveCurrentState);
            AssertThat(ball.StartX - ball.Radius >= arena.Position.X).IsTrue();
            AssertThat(ball.EndX + ball.Radius <= arena.End.X).IsTrue();
        }
        // The list keeps the deck's own ends: no riser at the west corner or at the stern.
        AssertThat(Level07Controller.ListedDeckTopAt(Level07Controller.FlagshipWestDeckEndX)).IsEqual(Level07Controller.ListDeckWestY);
        AssertThat(Level07Controller.ListedDeckTopAt(Level07Controller.LevelWidth - 20f)).IsEqual(Level07Controller.ListDeckEastY);
    }

    /// <summary>The real phase event, from the real boss: listed once, off the flush, the first ball on its warning.</summary>
    [TestCase(Timeout = 60000)]
    public async Task TheAdmiralsSecondPhaseListsTheDeckOnceOffTheFlush() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level07Controller level = fixture.Level;
        await Settle(tree);
        BossController admiral = level.BossEncounters[0].Boss;
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

        // Inside a physics flush the list hops off it; outside one it is synchronous.
        using (PhysicsCallbackGuard.Enter()) {
            admiral.TakeDamage(admiral.CurrentHP - Mathf.FloorToInt(admiral.ScaledMaxHP * admiral.Data.PhaseThresholds[0]) + 1);
            AssertThat(admiral.CurrentPhase).IsEqual(1);
            AssertThat(level.AdmiralDeckListed)
                .OverrideFailureMessage("The list must wait out the hit's physics flush.").IsFalse();
        }
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(level.AdmiralDeckListed).IsTrue();
        AssertThat(level.ListedDeckSlab.Visible).IsTrue();
        AssertThat(level.ListedDeckShape.Disabled).IsFalse();
        AssertThat(level.Cannonballs[0].Enabled).IsTrue();
        AssertThat(level.Cannonballs[0].Phase).IsEqual(HazardPhase.Warning);
        AssertThat(level.Cannonballs[1].Enabled).IsTrue();
        AssertThat(level.Cannonballs[1].Phase).IsEqual(HazardPhase.Cooldown);

        // Idempotent: a later call does not restart the telegraph.
        level.Cannonballs[0].ForcePhase(HazardPhase.Active, 1f);
        level.ListAdmiralDeck();
        AssertThat(level.Cannonballs[0].Phase).IsEqual(HazardPhase.Active);
    }

    /// <summary>
    /// In real frames: a hero caught where the deck rises is lifted on top of it, the slowest hero
    /// walks the listed deck both ways, a rolling ball hurts him once a roll, and the balls stop
    /// when the Admiral falls while the list stays.
    /// </summary>
    [TestCase(Timeout = 90000)]
    public async Task TheListedDeckIsWalkedBothWaysABallHurtsOnceAndTheBallsStopWhenHeFalls() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("lincoln");
        Level07Controller level = fixture.Level;
        PlayerController hero = level.Player;
        var source = new BufferedInputSource();
        try {
            await Settle(tree);
            BossController admiral = level.BossEncounters[0].Boss;
            // He stands still for this case: the reveal would otherwise engage him at the hero.
            admiral.ProcessMode = Node.ProcessModeEnum.Disabled;
            // The flagship's own fires are not what this case measures.
            foreach (string fire in new[] { "FlagshipFireA", "FlagshipFireB", "FlagshipFireC" }) {
                level.GetNode<StoryCyclicHazard>(fire).Enabled = false;
            }
            InputManager.Instance.SetInputSource(hero.PlayerIndex, source);

            // Standing on the old deck where the slab rises most (29 px): on top, not inside.
            hero.GlobalPosition = new Vector2(9410f, Level07Controller.ListDeckMidY - 1f);
            hero.Velocity = Vector2.Zero;
            await Frames(tree, 3);
            level.ListAdmiralDeck();
            foreach (RollingCannonball ball in level.Cannonballs) ball.Enabled = false;
            await Frames(tree, 4);
            AssertThat(hero.IsOnFloor()).OverrideFailureMessage($"The hero is not standing: {hero.GlobalPosition}.").IsTrue();
            AssertThat(Mathf.Abs(hero.GlobalPosition.Y - Level07Controller.ListedDeckTopAt(hero.GlobalPosition.X)) <= 3f)
                .OverrideFailureMessage($"The hero is not on the listed deck: {hero.GlobalPosition}.").IsTrue();

            // Walk down to the stern and back up to the west deck, no stall. (The reveal holds him for a beat on the way.)
            source.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None, GameplayButtons.None));
            for (int frame = 0; frame < 900 && hero.GlobalPosition.X < 10540f; frame++) await Step(tree);
            AssertThat(hero.GlobalPosition.X >= 10540f)
                .OverrideFailureMessage($"Lincoln stalled walking down the deck at {hero.GlobalPosition}.").IsTrue();
            source.SetNextFrame(PlayerInputFrame.Create(0, -1f, 0f, GameplayButtons.None, GameplayButtons.None));
            for (int frame = 0; frame < 900 && hero.GlobalPosition.X > 9200f; frame++) await Step(tree);
            AssertThat(hero.GlobalPosition.X <= 9200f && hero.GlobalPosition.Y <= Level07Controller.ListDeckWestY + 1f)
                .OverrideFailureMessage($"Lincoln stalled walking up the deck at {hero.GlobalPosition}.").IsTrue();
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None, GameplayButtons.None));

            // A ball rolling through a hero standing mid-deck hurts him once, and rides the deck.
            RollingCannonball rolling = level.Cannonballs[0];
            hero.GlobalPosition = new Vector2(9600f, Level07Controller.ListedDeckTopAt(9600f) - 1f);
            hero.Velocity = Vector2.Zero;
            await Frames(tree, 3);
            int health = hero.CurrentHP;
            rolling.Enabled = true;
            rolling.ForcePhase(HazardPhase.Active, rolling.ActiveDuration);
            int hits = 0;
            int lastHealth = health;
            for (int frame = 0; frame < 240 && rolling.Phase == HazardPhase.Active; frame++) {
                await Step(tree);
                if (hero.CurrentHP < lastHealth) hits++;
                lastHealth = hero.CurrentHP;
                if (rolling.Phase == HazardPhase.Active && rolling.GlobalPosition.X > rolling.StartX + 40f) {
                    float centre = Level07Controller.ListedDeckTopAt(rolling.GlobalPosition.X) - rolling.Radius;
                    AssertThat(Mathf.Abs(rolling.GlobalPosition.Y - centre) <= 0.5f)
                        .OverrideFailureMessage($"The ball left the deck at {rolling.GlobalPosition}.").IsTrue();
                }
            }
            AssertThat(hits).OverrideFailureMessage($"A roll should hurt once, not {hits} times.").IsEqual(1);
            AssertThat(health - hero.CurrentHP).IsEqual(rolling.Damage);

            // The balls stop when he falls. The list stays, and a later call does nothing.
            level.Cannonballs[1].Enabled = true;
            admiral.TakeDamage(admiral.CurrentHP);
            await Frames(tree, 3);
            foreach (RollingCannonball ball in level.Cannonballs) {
                AssertThat(ball.Enabled).IsFalse();
                AssertThat(ball.Phase).IsEqual(HazardPhase.Cooldown);
            }
            AssertThat(level.ListedDeckShape.Disabled).IsFalse();
            level.ListAdmiralDeck();
            AssertThat(level.Cannonballs[0].Enabled).OverrideFailureMessage("A list after the fall must do nothing.").IsFalse();
        } finally {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
        }
    }

    /// <summary>A ball keeps its phase, timer and place while the world is held, and goes on from where it stood.</summary>
    [TestCase(Timeout = 60000)]
    public async Task ABallIsFrozenWhileTheWorldIsHeld() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level07Controller level = fixture.Level;
        await Settle(tree);
        level.ListAdmiralDeck();
        RollingCannonball ball = level.Cannonballs[0];
        AssertThat(ChronalRewindManager.PostLandingHoldGroups.Contains("story_hazard")).IsTrue();

        ball.ForcePhase(HazardPhase.Active, ball.ActiveDuration);
        await Frames(tree, 10);
        float x = ball.GlobalPosition.X;
        AssertThat(x > ball.StartX).IsTrue();
        ball.SetTimeFrozen(true);
        await Frames(tree, 6);
        AssertThat(ball.GlobalPosition.X).IsEqual(x);
        AssertThat(ball.Phase).IsEqual(HazardPhase.Active);
        ball.SetTimeFrozen(false);
        await Frames(tree, 6);
        AssertThat(ball.GlobalPosition.X > x).IsTrue();
    }

    private static async Task Frames(SceneTree tree, int count) {
        for (int frame = 0; frame < count; frame++) await Step(tree);
    }

    /// <summary>One frame, paging any line the reveal started, so a hero walking into the arena keeps walking.</summary>
    private static async Task Step(SceneTree tree) {
        DialogueManager dialogue = DialogueManager.Instance;
        if (dialogue != null && dialogue.IsSequenceActive) dialogue.AdvanceLine();
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }
    private static int CountPickups(SceneTree tree) {
        Godot.Collections.Array<Node> pickups = tree.GetNodesInGroup("chronal_dust");
        using var lifetime = pickups.AsDisposable();
        return pickups.Count;
    }

    private static async Task Settle(SceneTree tree) {
        for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        DialogueManager dialogue = DialogueManager.Instance;
        for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
        AssertThat(tree.Paused).IsFalse();
    }

    /// <summary>The real scene against no save on Normal, with the session handed back.</summary>
    private sealed class Fixture : IDisposable {
        public readonly Level07Controller Level;
        private readonly SessionData _session;
        private readonly bool _paused;

        public Fixture(string heroID) {
            var tree = (SceneTree)Engine.GetMainLoop();
            _paused = tree.Paused;
            _session = GameManager.Instance.CurrentSession;
            SessionData session = _session;
            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = heroID;
            session.Difficulty = Difficulty.Normal;
            GameManager.Instance.CurrentSession = session;
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level07Controller>();
            Level.Name = "Nassau_AdmiralFixture";
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            PoolManager.Instance?.ReleaseActiveUnder(Level);
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            GameManager.Instance.CurrentSession = _session;
            ((SceneTree)Engine.GetMainLoop()).Paused = _paused;
        }
    }
}
