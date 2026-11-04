using System;
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
/// GR-PILOT (ruling 14; design §6): the Siegemaster Duke's Phase 2 ruptures the boiler, so both
/// arena edges become steam and the fight is pushed to the centre. The worked example for the
/// five arena changes that follow.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level02DukeArenaTests {
    private const string ScenePath = "res://scenes/campaign/Level_02_Orleans.tscn";

    [TestCase]
    public void TheSteamIsBuiltDisabledInsideTheArenaAtItsTwoEdges() {
        using var fixture = new Fixture();
        Level02Controller level = fixture.Level;
        foreach (StoryCyclicHazard steam in new[] { level.DukeSteamWest, level.DukeSteamEast }) {
            AssertObject(steam).IsNotNull();
            AssertThat(steam.Enabled).IsFalse();
            AssertThat(steam.Phase).IsEqual(HazardPhase.Cooldown);
            AssertThat(steam.IsInGroup("story_hazard")).IsTrue();
            AssertThat(steam.RewindPolicy).IsEqual(StoryRewindPolicy.PreserveCurrentState);
            // Its 120 px strip stays inside the rect, and so inside the boss's leash.
            Rect2 arena = Level02Controller.BossArenaBounds;
            AssertThat(steam.GlobalPosition.X - 60f >= arena.Position.X - 0.5f).IsTrue();
            AssertThat(steam.GlobalPosition.X + 60f <= arena.End.X + 0.5f).IsTrue();
        }
        AssertThat(level.DukeSteamWest.GlobalPosition.X < 9400f).IsTrue();
        AssertThat(level.DukeSteamEast.GlobalPosition.X > 10400f).IsTrue();
        AssertThat(level.DukeSteamRaised).IsFalse();
    }

    /// <summary>The real phase event, from the real boss: raised once, on its warning, and a second call changes nothing.</summary>
    [TestCase(Timeout = 60000)]
    public async Task TheDukesSecondPhaseRaisesTheSteamOnceOnItsWarning() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture();
        Level02Controller level = fixture.Level;
        await Settle(tree);
        BossEncounterController encounter = level.BossEncounters[0];
        encounter.RevealForTesting();
        BossController duke = encounter.Boss;
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

        // Inside a physics flush the raise hops off it; outside one it is synchronous.
        using (PhysicsCallbackGuard.Enter()) {
            duke.TakeDamage(duke.CurrentHP - Mathf.FloorToInt(duke.MaxHP * duke.Data.PhaseThresholds[0]) + 1);
            AssertThat(duke.CurrentPhase).IsEqual(1);
            AssertThat(level.DukeSteamRaised)
                .OverrideFailureMessage("The raise must wait out the hit's physics flush.").IsFalse();
        }
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(level.DukeSteamRaised).IsTrue();
        AssertThat(level.DukeSteamWest.Enabled).IsTrue();
        AssertThat(level.DukeSteamEast.Enabled).IsTrue();
        AssertThat(level.DukeSteamWest.Phase).IsEqual(HazardPhase.Warning);

        // Idempotent: a later call does not restart the telegraph.
        level.DukeSteamWest.ForcePhase(HazardPhase.Active, 1f);
        level.RaiseDukeSteam();
        AssertThat(level.DukeSteamWest.Phase).IsEqual(HazardPhase.Active);
    }

    /// <summary>In real frames the steam hurts a hero at the edge and not one at the centre, and stops when the Duke falls.</summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSteamHurtsTheEdgesNotTheCentreAndStopsWhenTheDukeFalls() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture();
        Level02Controller level = fixture.Level;
        PlayerController hero = level.Player;
        await Settle(tree);
        BossEncounterController encounter = level.BossEncounters[0];
        encounter.RevealForTesting();
        level.RaiseDukeSteam();

        // The centre is safe through a whole active window. (The boss is not engaged at a hero
        // this test holds still: a dormant boss stands on its anchor.)
        hero.GlobalPosition = new Vector2(9920f, 890f);
        hero.Velocity = Vector2.Zero;
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        int health = hero.CurrentHP;
        level.DukeSteamWest.ForcePhase(HazardPhase.Active, 0.5f);
        level.DukeSteamEast.ForcePhase(HazardPhase.Active, 0.5f);
        for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        int damageAtCentre = health - hero.CurrentHP;

        hero.GlobalPosition = new Vector2(Level02Controller.DukeSteamWestX, 890f);
        hero.Velocity = Vector2.Zero;
        level.DukeSteamWest.ForcePhase(HazardPhase.Active, 0.5f);
        int atEdge = hero.CurrentHP;
        for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(level.DukeSteamWest.Damage).IsEqual(12);
        AssertThat(atEdge - hero.CurrentHP).OverrideFailureMessage("Steam at the edge must hurt.").IsGreater(0);
        AssertThat(damageAtCentre).OverrideFailureMessage($"Steam reached the centre: {damageAtCentre}.").IsEqual(0);

        encounter.Boss.TakeDamage(encounter.Boss.CurrentHP);
        for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(level.DukeSteamRaised).IsFalse();
        AssertThat(level.DukeSteamWest.Enabled).IsFalse();
        AssertThat(level.DukeSteamEast.Enabled).IsFalse();
        level.RaiseDukeSteam();
        AssertThat(level.DukeSteamWest.Enabled).OverrideFailureMessage("A raise after the fall must do nothing.").IsFalse();
    }

    /// <summary>The steam keeps its phase and timer while the world is held, and goes on from where it stood.</summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSteamIsFrozenWhileTheWorldIsHeld() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture();
        Level02Controller level = fixture.Level;
        await Settle(tree);
        StoryCyclicHazard steam = level.DukeSteamWest;
        level.RaiseDukeSteam();
        AssertThat(ChronalRewindManager.PostLandingHoldGroupsForTesting).Contains("story_hazard");
        steam.ForcePhase(HazardPhase.Warning, 2f / 60f);
        steam.SetTimeFrozen(true);
        for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(steam.Phase).IsEqual(HazardPhase.Warning);
        steam.SetTimeFrozen(false);
        for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(steam.Phase).IsEqual(HazardPhase.Active);
    }

    private static async Task Settle(SceneTree tree) {
        for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        DialogueManager dialogue = DialogueManager.Instance;
        for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
        AssertThat(tree.Paused).IsFalse();
    }

    private sealed class Fixture : IDisposable {
        public readonly Level02Controller Level;
        private readonly int _slot;
        private readonly string _character;
        private readonly bool _paused;

        public Fixture() {
            var tree = (SceneTree)Engine.GetMainLoop();
            _paused = tree.Paused;
            _slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _character = GameManager.Instance.CurrentSession.SelectedCharacterID;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level02Controller>();
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
}
