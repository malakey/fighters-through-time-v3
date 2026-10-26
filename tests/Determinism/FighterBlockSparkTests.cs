using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// FU-BLOCK (feel and UI update 2026-10-25): the Fighter driver's block-success
/// effect. A block-charge drop shows an ordinary spark, a rise of component
/// 323's perfect-block count a perfect one and a fresh daze (the guard break) a
/// shatter; an ordinary hit shows none. Read-only over the simulation.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterBlockSparkTests {
    private const double Step = 1.0 / 60.0;

    private sealed class Rig {
        public Node Host;
        public FighterSimulationDriver Driver;
        public BufferedInputSource InputOne = new();
        public BufferedInputSource InputTwo = new();
        public readonly List<BlockSparkKind> Seen = new();
        public SessionData SavedSession;
        private int _childrenSeen;

        public void Tick(float oneMove, GameplayButtons oneHeld, GameplayButtons twoHeld,
            GameplayButtons onePrev = GameplayButtons.None, GameplayButtons twoPrev = GameplayButtons.None) {
            uint tick = (uint)Driver.CurrentTick;
            InputOne.SetNextFrame(PlayerInputFrame.Create(tick, oneMove, 0f, oneHeld, onePrev));
            InputTwo.SetNextFrame(PlayerInputFrame.Create(tick, 0f, 0f, twoHeld, twoPrev));
            InputManager.Instance.SetInputSource(0, InputOne);
            InputManager.Instance.SetInputSource(1, InputTwo);
            Driver._PhysicsProcess(Step);
            // Effects are never aged here, so each spawn is a new active child.
            var effects = new List<BlockSparkEffect>();
            foreach (Node child in Host.GetChildren()) {
                if (child is BlockSparkEffect e && e.IsActive) effects.Add(e);
            }
            for (; _childrenSeen < effects.Count; _childrenSeen++) Seen.Add(effects[_childrenSeen].Kind);
        }
    }

    private static Rig NewRig(string name) {
        // GameManager's boot session runs player two as the Normal CPU, which would
        // ignore InputTwo (CLAUDE.md test-authoring notes): make it a local human
        // before the driver is built, and Free restores the session.
        GameManager manager = GameManager.Instance;
        SessionData savedSession = manager.CurrentSession;
        SessionData localVersus = savedSession;
        localVersus.FighterOpponentType = FighterOpponentType.LocalHuman;
        manager.CurrentSession = localVersus;
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        PlayerController one = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        PlayerController two = CharacterFactory.CreateCharacter("joan", 1, applyStoryProgression: false);
        host.AddChild(one);
        host.AddChild(two);
        var driver = new FighterSimulationDriver { Name = "Driver" };
        host.AddChild(driver);
        MatchSettings settings = MatchSettings.GetDefault();
        settings.StageHazardsEnabled = false;
        driver.Initialize(one, two, settings, stageHazardTypeID: 1, stageID: "florence_workshop", matchSeed: 2026);
        var rig = new Rig { Host = host, Driver = driver, SavedSession = savedSession };
        for (int i = 0; i < FighterMatchFlowRules.CountdownFrames + 2; i++) rig.Tick(0f, GameplayButtons.None, GameplayButtons.None);
        return rig;
    }

    private static void Free(Rig rig) {
        InputManager.Instance.ClearInputSource(0);
        InputManager.Instance.ClearInputSource(1);
        rig.Host.Free();
        GameManager.Instance.CurrentSession = rig.SavedSession;
    }

    [TestCase]
    public void AnOrdinaryBlockShowsAnOrdinarySparkAndTheGuardBreakAShatterOne() {
        if (InputManager.Instance == null) return;
        Rig rig = NewRig("FighterBlockSparkOrdinaryHost");
        try {
            // Player two holds Block throughout; player one walks in and swings
            // until the stance breaks.
            GameplayButtons prev = GameplayButtons.None;
            for (int frame = 0; frame < 3000 && !rig.Seen.Contains(BlockSparkKind.Shatter); frame++) {
                GameplayButtons held = frame % 12 < 2 ? GameplayButtons.BasicAttack : GameplayButtons.None;
                rig.Tick(1f, held, GameplayButtons.Block, prev, GameplayButtons.Block);
                prev = held;
            }
            AssertThat(rig.Seen.Contains(BlockSparkKind.Ordinary))
                .OverrideFailureMessage("A blocked swing must show an ordinary spark. Seen: " + string.Join(",", rig.Seen))
                .IsTrue();
            AssertThat(rig.Seen.Contains(BlockSparkKind.Shatter))
                .OverrideFailureMessage("The guard break must show a shatter spark. Seen: " + string.Join(",", rig.Seen))
                .IsTrue();
            AssertThat(rig.Seen.FindAll(k => k == BlockSparkKind.Shatter).Count).IsEqual(1);
            AssertThat(rig.Seen.IndexOf(BlockSparkKind.Ordinary) < rig.Seen.IndexOf(BlockSparkKind.Shatter)).IsTrue();
        } finally {
            Free(rig);
        }
    }

    [TestCase]
    public void ARiseOfThePerfectBlockCountShowsOnePerfectSpark() {
        if (InputManager.Instance == null) return;
        Rig rig = NewRig("FighterBlockSparkPerfectHost");
        try {
            rig.Tick(0f, GameplayButtons.None, GameplayButtons.None);
            AssertThat(rig.Driver.Simulation.TryGetFighterActionState(1, out FighterActionStateComponent action)).IsTrue();
            FighterBlockTimingRules.NotePerfectBlock(ref action);
            AssertThat(rig.Driver.Simulation.SeedFighterActionStateForTest(1, in action)).IsTrue();
            rig.Tick(0f, GameplayButtons.None, GameplayButtons.None);
            rig.Tick(0f, GameplayButtons.None, GameplayButtons.None);
            AssertThat(rig.Seen.Count).IsEqual(1);
            AssertThat(rig.Seen[0]).IsEqual(BlockSparkKind.Perfect);
        } finally {
            Free(rig);
        }
    }

    [TestCase]
    public void AnOrdinaryHitShowsNoSpark() {
        if (InputManager.Instance == null) return;
        Rig rig = NewRig("FighterBlockSparkNoBlockHost");
        try {
            GameplayButtons prev = GameplayButtons.None;
            int hpStart = 0;
            if (rig.Driver.TryGetFighter(1, out FighterStateComponent start)) hpStart = start.CurrentHP;
            bool hit = false;
            for (int frame = 0; frame < 900 && !hit; frame++) {
                GameplayButtons held = frame % 40 < 2 ? GameplayButtons.BasicAttack : GameplayButtons.None;
                rig.Tick(1f, held, GameplayButtons.None, prev);
                prev = held;
                hit = rig.Driver.TryGetFighter(1, out FighterStateComponent now) && now.CurrentHP < hpStart;
            }
            AssertThat(hit).OverrideFailureMessage("Harness: player one never landed a hit.").IsTrue();
            rig.Tick(0f, GameplayButtons.None, GameplayButtons.None);
            AssertThat(rig.Seen.Count).IsEqual(0);
        } finally {
            Free(rig);
        }
    }
}
