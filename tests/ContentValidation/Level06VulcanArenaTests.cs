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
/// GR-LV2 (ruling 14; design §6): the Vulcan Decimator's Phase 2 cracks the floor into lava
/// vents on a visible cadence and rains the eruption's debris at double rate. Level 2's
/// <c>Level02DukeArenaTests</c> is the model.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level06VulcanArenaTests {
    private const string ScenePath = "res://scenes/campaign/Level_06_Pompeii.tscn";

    [TestCase]
    public void TheVentsAreBuiltDisabledInsideTheArenaWithSafeFloorBetweenThem() {
        using var fixture = new Fixture();
        Level06Controller level = fixture.Level;
        AssertThat(level.VulcanVents.Count >= 4 && level.VulcanVents.Count <= 5)
            .OverrideFailureMessage($"{level.VulcanVents.Count} vents, not 4-5.").IsTrue();
        Rect2 arena = Level06Controller.BossArenaBounds;
        float previousEast = float.MinValue;
        foreach (StoryCyclicHazard vent in level.VulcanVents) {
            AssertThat(vent.Enabled).IsFalse();
            AssertThat(vent.Phase).IsEqual(HazardPhase.Cooldown);
            AssertThat(vent.IsInGroup("story_hazard")).IsTrue();
            AssertThat(vent.RewindPolicy).IsEqual(StoryRewindPolicy.PreserveCurrentState);
            // Its 120 px strip stays inside the rect, and so inside the boss's leash.
            AssertThat(vent.GlobalPosition.X - 60f >= arena.Position.X).IsTrue();
            AssertThat(vent.GlobalPosition.X + 60f <= arena.End.X).IsTrue();
            AssertThat(vent.GlobalPosition.X - 60f - previousEast >= 100f || previousEast == float.MinValue)
                .OverrideFailureMessage($"No safe floor west of the vent at {vent.GlobalPosition.X}.").IsTrue();
            previousEast = vent.GlobalPosition.X + 60f;
        }
        AssertThat(level.VulcanEruptionRaised).IsFalse();
        AssertThat(Level06Controller.EruptionDebrisIntervalSeconds).IsEqual(CollapseTremorRules.Stage1DebrisIntervalSeconds / 2f);
        // Never on the PreBoss anchor's safe beat, never outside the arena.
        AssertThat(Level06Controller.EruptionTargetX(0f) >= Level06Controller.PreBossCheckpointX + CollapseTremorRules.SafeZoneRadiusPixels).IsTrue();
        AssertThat(Level06Controller.EruptionTargetX(99999f)).IsEqual(arena.End.X);
    }

    /// <summary>The real phase event, from the real boss: raised once, off the physics flush, each vent later than the last; a second call changes nothing.</summary>
    [TestCase(Timeout = 60000)]
    public async Task TheSecondPhaseRaisesTheVentsOnceOnStaggeredWarnings() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture();
        Level06Controller level = fixture.Level;
        await EnterTheArena(tree, level);
        BossController vulcan = level.BossEncounters[0].Boss;

        using (PhysicsCallbackGuard.Enter()) {
            vulcan.TakeDamage(vulcan.CurrentHP - Mathf.FloorToInt(vulcan.ScaledMaxHP * vulcan.Data.PhaseThresholds[0]) + 1);
            AssertThat(vulcan.CurrentPhase).IsEqual(1);
            AssertThat(level.VulcanEruptionRaised)
                .OverrideFailureMessage("The raise must wait out the hit's physics flush.").IsFalse();
        }
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(level.VulcanEruptionRaised).IsTrue();
        foreach (StoryCyclicHazard vent in level.VulcanVents) {
            AssertThat(vent.Enabled).IsTrue();
            AssertThat(vent.Phase).IsEqual(HazardPhase.Warning);
        }

        // Staggered: the first goes active while the last is still on its warning.
        double firstActive = await FramesUntilActive(tree, level.VulcanVents[0], 120);
        AssertThat(firstActive > 0).IsTrue();
        AssertThat(level.VulcanVents[^1].Phase).IsEqual(HazardPhase.Warning);

        // Idempotent: a later call does not restart the telegraph.
        level.VulcanVents[0].ForcePhase(HazardPhase.Active, 1f);
        level.RaiseVulcanEruption();
        AssertThat(level.VulcanVents[0].Phase).IsEqual(HazardPhase.Active);
    }

    /// <summary>
    /// In real frames a vent hurts a hero on it and not one between two, debris rains at its
    /// cadence with at most two in the air, a launched mob takes the vent (GR-HAZ) while a walking
    /// one does not, and all of it stops when the Vulcan falls.
    /// </summary>
    [TestCase(Timeout = 90000)]
    public async Task TheVentsHurtOnlyWhereTheyStandHitLaunchedMobsAndStopWhenTheVulcanFalls() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture();
        Level06Controller level = fixture.Level;
        PlayerController hero = level.Player;
        await EnterTheArena(tree, level);
        level.RaiseVulcanEruption();
        StoryCyclicHazard vent = level.VulcanVents[0];

        // Between two vents is safe through a whole active window.
        float between = (Level06Controller.VulcanVentXs[0] + Level06Controller.VulcanVentXs[1]) / 2f;
        hero.GlobalPosition = new Vector2(between, 890f);
        hero.Velocity = Vector2.Zero;
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        int health = hero.CurrentHP;
        vent.ForcePhase(HazardPhase.Active, 0.3f);
        level.VulcanVents[1].ForcePhase(HazardPhase.Active, 0.3f);
        for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        int damageBetween = health - hero.CurrentHP;

        hero.GlobalPosition = new Vector2(vent.GlobalPosition.X, 890f);
        hero.Velocity = Vector2.Zero;
        vent.ForcePhase(HazardPhase.Active, 0.3f);
        int onVent = hero.CurrentHP;
        for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(onVent - hero.CurrentHP).OverrideFailureMessage("A vent must hurt the hero on it.").IsGreater(0);
        AssertThat(damageBetween).OverrideFailureMessage($"A vent reached the floor between two: {damageBetween}.").IsEqual(0);

        // The rain: a piece every interval, never more than two in the air.
        hero.GlobalPosition = new Vector2(9700f, 890f);
        int before = level.EruptionDebrisLaunched;
        int seconds = 5;
        int airborneMost = 0;
        for (int frame = 0; frame < seconds * 60; frame++) {
            hero.CurrentHP = hero.MaximumHP;
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            airborneMost = Math.Max(airborneMost, level.EruptionDebrisAirborne.Count);
        }
        AssertThat(level.EruptionDebrisLaunched - before >= 2)
            .OverrideFailureMessage($"{level.EruptionDebrisLaunched - before} pieces in {seconds} s.").IsTrue();
        AssertThat(airborneMost <= CollapseTremorRules.MaxAirborneDebris).IsTrue();

        // A launched mob takes the vent; a walking one does not.
        EnemyController walker = EnemyFactory.Spawn("chrono_slasher", level, new Vector2(vent.GlobalPosition.X, 850f));
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        int walkerHP = walker.CurrentHP;
        vent.ForcePhase(HazardPhase.Active, 0.3f);
        for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(walker.CurrentHP).IsEqual(walkerHP);
        walker.GlobalPosition = new Vector2(vent.GlobalPosition.X, 850f);
        walker.LaunchThrown(new Vector2(0f, -40f), 0);
        vent.ForcePhase(HazardPhase.Active, 0.3f);
        for (int frame = 0; frame < 6; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(walker.CurrentHP < walkerHP)
            .OverrideFailureMessage("A thrown mob took nothing from the vent.").IsTrue();

        BossController vulcan = level.BossEncounters[0].Boss;
        vulcan.TakeDamage(vulcan.CurrentHP);
        for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(level.VulcanEruptionRaised).IsFalse();
        AssertThat(vent.Enabled).IsFalse();
        int launched = level.EruptionDebrisLaunched;
        for (int frame = 0; frame < (int)(Level06Controller.EruptionDebrisIntervalSeconds * 60f) + 10; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(level.EruptionDebrisLaunched).IsEqual(launched);
        level.RaiseVulcanEruption();
        AssertThat(vent.Enabled).OverrideFailureMessage("A raise after the fall must do nothing.").IsFalse();
    }

    /// <summary>
    /// His fall retires the pieces already overhead: one in the air (here over the hero's head, in its
    /// fall) never lands, whatever the hero's health, and a second defeat call changes nothing.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task APieceInTheAirWhenTheVulcanFallsNeverHitsTheHero() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture();
        Level06Controller level = fixture.Level;
        await EnterTheArena(tree, level);
        PlayerController hero = level.Player;
        level.RaiseVulcanEruption();
        hero.GlobalPosition = new Vector2(9700f, 890f);
        hero.Velocity = Vector2.Zero;
        for (int frame = 0; frame < 400 && level.EruptionDebrisAirborne.Count == 0; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(level.EruptionDebrisAirborne.Count).IsGreater(0);
        FallingDebris piece = level.EruptionDebrisAirborne[0];
        // Put it straight over the hero, falling, about to land.
        piece.Launch(hero.GlobalPosition, 120f);
        piece.Step(10f);
        AssertThat(piece.Phase).IsEqual(FallingDebrisPhase.Falling);

        int health = hero.CurrentHP;
        BossController vulcan = level.BossEncounters[0].Boss;
        vulcan.TakeDamage(vulcan.CurrentHP);
        for (int frame = 0; frame < 30; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(hero.CurrentHP).OverrideFailureMessage("Debris landed after the Vulcan fell.").IsEqual(health);
        AssertThat(level.EruptionDebrisAirborne.Count).IsEqual(0);
        AssertThat(piece.Phase).IsEqual(FallingDebrisPhase.Spent);
        piece.Retire();
        AssertThat(hero.CurrentHP).IsEqual(health);
    }

    /// <summary>
    /// Two real Post-Landing Holds, through the call a Collapse resume makes. Through the first
    /// the vents keep their phase and the rain's clock stands still; through the second a piece
    /// in the air waits. Everything goes on at the thaw.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task TheVentsAndTheDebrisAreFrozenWhileTheWorldIsHeld() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture();
        Level06Controller level = fixture.Level;
        await EnterTheArena(tree, level);
        var manager = tree.GetFirstNodeInGroup(ChronalRewindManager.ManagerGroup) as ChronalRewindManager;
        AssertObject(manager).OverrideFailureMessage("The level has no rewind manager.").IsNotNull();
        AssertThat(ChronalRewindManager.PostLandingHoldGroups.Contains("story_hazard")).IsTrue();

        level.RaiseVulcanEruption();
        StoryCyclicHazard vent = level.VulcanVents[0];
        vent.ForcePhase(HazardPhase.Warning, 2f / 60f);
        AssertThat(manager.BeginPlacementHold(StoryRecoveryHoldCause.CollapseResume)).IsTrue();
        int frames = 0;
        for (; frames < 30; frames++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(ChronalRewindManager.IsWorldHeld(tree)).IsTrue();
        AssertThat(vent.IsTimeFrozen).IsTrue();
        AssertThat(vent.Phase).IsEqual(HazardPhase.Warning);
        for (; frames < 120 && ChronalRewindManager.IsWorldHeld(tree); frames++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(ChronalRewindManager.IsWorldHeld(tree)).IsFalse();
        int thawed = frames;
        for (; frames < thawed + 6; frames++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(vent.Phase).OverrideFailureMessage("The vent did not go on at the thaw.").IsEqual(HazardPhase.Active);

        // The rain's clock stood still through the hold: its first piece comes a whole interval after the thaw.
        for (; frames < 400 && level.EruptionDebrisLaunched == 0; frames++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(level.EruptionDebrisLaunched).IsEqual(1);
        int interval = Mathf.RoundToInt(Level06Controller.EruptionDebrisIntervalSeconds * 60f);
        AssertThat(frames >= thawed + interval - 3)
            .OverrideFailureMessage($"The first piece came {frames - thawed} frames after the thaw, not {interval}.").IsTrue();

        // A fresh piece telegraphs for 45 frames: a 50-frame wait would see it fall, were it not held.
        FallingDebris piece = level.EruptionDebrisAirborne[0];
        AssertThat(piece.Phase).IsEqual(FallingDebrisPhase.Telegraph);
        Vector2 at = piece.GlobalPosition;
        AssertThat(manager.BeginPlacementHold(StoryRecoveryHoldCause.CollapseResume)).IsTrue();
        for (int frame = 0; frame < 50; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(ChronalRewindManager.IsWorldHeld(tree)).IsTrue();
        AssertThat(piece.Phase).OverrideFailureMessage("A piece went on inside the hold.").IsEqual(FallingDebrisPhase.Telegraph);
        AssertThat(piece.GlobalPosition).IsEqual(at);
        for (int frame = 0; frame < 120 && piece.Phase == FallingDebrisPhase.Telegraph; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(piece.Phase).OverrideFailureMessage("A piece never went on after the thaw.").IsNotEqual(FallingDebrisPhase.Telegraph);
    }

    private static async Task<int> FramesUntilActive(SceneTree tree, StoryCyclicHazard vent, int limit) {
        for (int frame = 1; frame <= limit; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            if (vent.Phase == HazardPhase.Active) return frame;
        }
        return -1;
    }

    /// <summary>
    /// The hero walks in between two vents: the real reveal, its intro beat and its dialogue run
    /// out, and then the Vulcan is told to stand still on his anchor, so only the arena acts.
    /// </summary>
    private static async Task EnterTheArena(SceneTree tree, Level06Controller level) {
        await Settle(tree);
        PlayerController hero = level.Player;
        BossEncounterController encounter = level.BossEncounters[0];
        hero.GlobalPosition = new Vector2(9730f, 890f);
        hero.Velocity = Vector2.Zero;
        for (int frame = 0; frame < 300 && (!encounter.IsRevealed || encounter.IsIntroBeatRunning); frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(encounter.IsRevealed).IsTrue();
        await Settle(tree);
        encounter.Boss.IsEngaged = false;
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }

    private static async Task Settle(SceneTree tree) {
        for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        DialogueManager dialogue = DialogueManager.Instance;
        for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
        AssertThat(tree.Paused).IsFalse();
    }

    private sealed class Fixture : IDisposable {
        public readonly Level06Controller Level;
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
            // A level the runner instantiates directly has no warmed pools; the game warms this one from the level's config.
            if (!PoolManager.Instance.IsRegistered(FallingDebris.PoolID)) {
                PoolManager.Instance.RegisterPool(FallingDebris.PoolID, ResourceLoader.Load<PackedScene>(CollapseTremorController.DebrisScenePath),
                    2, 8, PoolOverflowPolicy.Reject);
            }
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level06Controller>();
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
