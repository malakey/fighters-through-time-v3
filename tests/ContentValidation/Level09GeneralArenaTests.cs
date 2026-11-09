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
/// GR-LV3 (ruling 14; design §6): the Thunderbomb General's Phase 2 lights fleet lanterns over the
/// flagship: <see cref="SearchlightZone"/>s in delayed-strike mode, the Lantern Watch's beam.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level09GeneralArenaTests {
    private const string ScenePath = "res://scenes/campaign/Level_09_Berlin.tscn";

    /// <summary>Three dark beams, whose whole sweep stays inside the arena, apart from the Lantern Watch's.</summary>
    [TestCase]
    public void TheLanternsAreBuiltDarkAndTheirSweepsStayInsideTheArena() {
        using var fixture = new Fixture("einstein");
        Level09Controller level = fixture.Level;
        AssertThat(level.GeneralLanternsLit).IsFalse();
        AssertThat(level.GeneralLanterns.Count).IsEqual(3);
        Rect2 arena = Level09Controller.BossArenaBounds;
        var periods = new System.Collections.Generic.HashSet<float>();
        foreach (SearchlightZone lantern in level.GeneralLanterns) {
            AssertThat(lantern.Enabled).IsFalse();
            AssertThat(lantern.Monitoring).IsFalse();
            AssertThat(lantern.Visible).IsFalse();
            AssertThat(lantern.Mode).IsEqual(SearchlightMode.DelayedStrike);
            AssertThat(lantern.ExposureGraceSeconds).IsEqual(1.5f);
            AssertThat(lantern.StrikeDamage).IsEqual(Level09Controller.GeneralLanternStrikeDamage);
            AssertThat(lantern.IsInGroup("story_hazard")).IsTrue();
            AssertThat(lantern.RewindPolicy).IsEqual(StoryRewindPolicy.PreserveCurrentState);
            AssertThat(level.Searchlights.Contains(lantern)).IsFalse();
            AssertThat(periods.Add(lantern.SweepPeriodSeconds)).OverrideFailureMessage("Two lanterns sweep in step.").IsTrue();
            Rect2 footprint = Level09Controller.SweptFootprint(lantern.Position, lantern.SweepArcDegrees);
            AssertThat(footprint.Position.X >= arena.Position.X && footprint.End.X <= arena.End.X)
                .OverrideFailureMessage($"{lantern.Name} sweeps {footprint}, outside the arena {arena}.").IsTrue();
            AssertThat(footprint.End.Y >= Level09Controller.GroundY).OverrideFailureMessage("The cone ends over the deck.").IsTrue();
        }
    }

    /// <summary>The real phase event, from the real boss: lit once, off the flush, and a second call changes nothing.</summary>
    [TestCase(Timeout = 60000)]
    public async Task TheGeneralsSecondPhaseLightsTheLanternsOnceOffTheFlush() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level09Controller level = fixture.Level;
        await Settle(tree);
        BossController general = level.BossEncounters[0].Boss;
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

        // Inside a physics flush the lighting hops off it; outside one it is synchronous.
        using (PhysicsCallbackGuard.Enter()) {
            general.TakeDamage(general.CurrentHP - Mathf.FloorToInt(general.ScaledMaxHP * general.Data.PhaseThresholds[0]) + 1);
            AssertThat(general.CurrentPhase).IsEqual(1);
            AssertThat(level.GeneralLanternsLit)
                .OverrideFailureMessage("The lanterns must wait out the hit's physics flush.").IsFalse();
        }
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(level.GeneralLanternsLit).IsTrue();
        foreach (SearchlightZone lantern in level.GeneralLanterns) {
            AssertThat(lantern.Enabled).IsTrue();
            AssertThat(lantern.Monitoring).IsTrue();
            AssertThat(lantern.Visible).IsTrue();
        }

        // Idempotent: a later call disturbs nothing.
        float phase = level.GeneralLanterns[0].SweepPhase;
        level.LightGeneralLanterns();
        AssertThat(level.GeneralLanterns[0].SweepPhase).IsEqual(phase);
    }

    /// <summary>
    /// In real frames: a hero who stays under a lit lantern takes its delayed strike, never before the
    /// grace, a dark lantern ignores him, and the lanterns go out when the General falls.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task ALitLanternStrikesAfterItsGraceAndTheLanternsGoOutWhenHeFalls() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level09Controller level = fixture.Level;
        PlayerController hero = level.Player;
        await Settle(tree);
        BossController general = level.BossEncounters[0].Boss;
        // He stands still for this case: the reveal would otherwise engage him at the hero.
        general.ProcessMode = Node.ProcessModeEnum.Disabled;
        SearchlightZone lantern = level.GeneralLanterns[0];

        // Dark: he stands under it for longer than the grace and nothing lands.
        hero.GlobalPosition = new Vector2(lantern.Position.X, Level09Controller.GroundY - 1f);
        hero.Velocity = Vector2.Zero;
        await Frames(tree, 100);
        AssertThat(lantern.StrikeCount).IsEqual(0);

        // Lit: the first strike comes no sooner than the grace, and costs what the plan says.
        level.LightGeneralLanterns();
        hero.GlobalPosition = new Vector2(lantern.Position.X, Level09Controller.GroundY - 1f);
        hero.Velocity = Vector2.Zero;
        int health = hero.CurrentHP;
        int framesToStrike = 0;
        for (; framesToStrike < 360 && lantern.StrikeCount == 0; framesToStrike++) {
            hero.GlobalPosition = new Vector2(lantern.Position.X, hero.GlobalPosition.Y);
            await Step(tree);
        }
        AssertThat(lantern.StrikeCount).OverrideFailureMessage("A hero under a lit lantern was never struck.").IsGreater(0);
        AssertThat(framesToStrike >= 88).OverrideFailureMessage($"The strike came after {framesToStrike} frames, inside the 90-frame grace.").IsTrue();
        AssertThat(hero.CurrentHP).IsEqual(health - Level09Controller.GeneralLanternStrikeDamage);

        // Out when he falls, and a later call does nothing.
        general.TakeDamage(general.CurrentHP);
        await Frames(tree, 3);
        AssertThat(level.GeneralLanternsLit).IsFalse();
        foreach (SearchlightZone dark in level.GeneralLanterns) {
            AssertThat(dark.Enabled).IsFalse();
            AssertThat(dark.Monitoring).IsFalse();
            AssertThat(dark.Visible).IsFalse();
        }
        level.LightGeneralLanterns();
        AssertThat(level.GeneralLanterns[0].Enabled).OverrideFailureMessage("A lighting after the fall must do nothing.").IsFalse();
    }

    /// <summary>A lantern keeps its sweep while the world is held, and goes on from where it stood.</summary>
    [TestCase(Timeout = 60000)]
    public async Task ALanternIsFrozenWhileTheWorldIsHeld() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Fixture("einstein");
        Level09Controller level = fixture.Level;
        await Settle(tree);
        level.LightGeneralLanterns();
        SearchlightZone lantern = level.GeneralLanterns[1];
        AssertThat(ChronalRewindManager.PostLandingHoldGroups.Contains("story_hazard")).IsTrue();
        await Frames(tree, 6);
        float phase = lantern.SweepPhase;
        AssertThat(phase > 0f).IsTrue();
        lantern.SetTimeFrozen(true);
        await Frames(tree, 6);
        AssertThat(lantern.SweepPhase).IsEqual(phase);
        lantern.SetTimeFrozen(false);
        await Frames(tree, 6);
        AssertThat(lantern.SweepPhase > phase).IsTrue();
    }

    private static async Task Frames(SceneTree tree, int count) {
        for (int frame = 0; frame < count; frame++) await Step(tree);
    }

    /// <summary>One frame, paging any line the reveal started.</summary>
    private static async Task Step(SceneTree tree) {
        DialogueManager dialogue = DialogueManager.Instance;
        if (dialogue != null && dialogue.IsSequenceActive) dialogue.AdvanceLine();
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }

    private static async Task Settle(SceneTree tree) {
        for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        DialogueManager dialogue = DialogueManager.Instance;
        for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
        AssertThat(tree.Paused).IsFalse();
    }

    /// <summary>The real scene against no save on Normal, with the session handed back.</summary>
    private sealed class Fixture : IDisposable {
        public readonly Level09Controller Level;
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
            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level09Controller>();
            Level.Name = "Hakata_GeneralFixture";
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
