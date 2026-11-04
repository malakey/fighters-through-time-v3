using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// GR-AUDIO (gameplay review ruling 13, 2026-11-03): the Fighter half of the small combat sound set,
/// raised by <c>FighterSimulationDriver</c> from copied sim state. A hit (HP falling, outside an active
/// hazard) plays <c>hit_basic</c>; a blocked swing plays <c>block</c> and never <c>hit_basic</c>; a rise of
/// component 323's perfect-block count plays <c>perfect_block</c> and a lowered count (a rollback
/// correction) plays nothing; a swing whose active window ends with nothing resolved plays
/// <c>swing_whiff</c>. Nothing is written into the simulation. The cues are read from the presenter's
/// <c>CuesPlayed</c> / <c>LastCueID</c> seams, sampled after every driver tick.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterCombatCueTests {
    private const double Step = 1.0 / 60.0;

    private sealed class Rig {
        public Node Host;
        public FighterSimulationDriver Driver;
        public CoreMechanicCuePresenter Cues;
        public BufferedInputSource InputOne = new();
        public BufferedInputSource InputTwo = new();
        public readonly List<string> Seen = new();
        public SessionData SavedSession;

        public void Tick(float oneMove, GameplayButtons oneHeld, GameplayButtons twoHeld,
            GameplayButtons onePrev = GameplayButtons.None, GameplayButtons twoPrev = GameplayButtons.None) {
            uint tick = (uint)Driver.CurrentTick;
            InputOne.SetNextFrame(PlayerInputFrame.Create(tick, oneMove, 0f, oneHeld, onePrev));
            InputTwo.SetNextFrame(PlayerInputFrame.Create(tick, 0f, 0f, twoHeld, twoPrev));
            InputManager.Instance.SetInputSource(0, InputOne);
            InputManager.Instance.SetInputSource(1, InputTwo);
            int before = Cues.CuesPlayed;
            Driver._PhysicsProcess(Step);
            if (Cues.CuesPlayed != before) Seen.Add(Cues.LastCueID);
            Cues.Advance((float)Step);
        }

        public int Count(string cueID) => Seen.FindAll(id => id == cueID).Count;
    }

    private static Rig NewRig(string name) {
        // GameManager's boot session runs player two as the Normal CPU, which would ignore InputTwo
        // (CLAUDE.md test-authoring notes): make it a local human before the driver is built.
        GameManager manager = GameManager.Instance;
        SessionData savedSession = manager.CurrentSession;
        SessionData localVersus = savedSession;
        localVersus.FighterOpponentType = FighterOpponentType.LocalHuman;
        manager.CurrentSession = localVersus;
        CoreMechanicCuePresenter cues = AudioManager.Instance.CoreCues;
        cues.ResetTransient();
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
        var rig = new Rig { Host = host, Driver = driver, Cues = cues, SavedSession = savedSession };
        for (int i = 0; i < FighterMatchFlowRules.CountdownFrames + 2; i++) rig.Tick(0f, GameplayButtons.None, GameplayButtons.None);
        rig.Seen.Clear();
        return rig;
    }

    private static void Free(Rig rig) {
        InputManager.Instance.ClearInputSource(0);
        InputManager.Instance.ClearInputSource(1);
        rig.Host.Free();
        GameManager.Instance.CurrentSession = rig.SavedSession;
        rig.Cues.ResetTransient();
        AudioManager.Instance.ReleaseAllVoices();
    }

    [TestCase]
    public void ALandedHitPlaysTheHitThumpAndNeitherBlockCue() {
        if (InputManager.Instance == null) return;
        Rig rig = NewRig("FighterCueHitHost");
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
            AssertThat(rig.Count(CoreMechanicCueIDs.HitBasic)).OverrideFailureMessage("seen: " + string.Join(",", rig.Seen)).IsEqual(1);
            AssertThat(rig.Count(CoreMechanicCueIDs.Block)).IsEqual(0);
            AssertThat(rig.Count(CoreMechanicCueIDs.PerfectBlock)).IsEqual(0);
        } finally {
            Free(rig);
        }
    }

    [TestCase]
    public void ABlockedSwingPlaysTheBlockPingAndNoHitThump() {
        if (InputManager.Instance == null) return;
        Rig rig = NewRig("FighterCueBlockHost");
        try {
            // Player two holds Block throughout; player one walks in and swings until a block lands.
            GameplayButtons prev = GameplayButtons.None;
            for (int frame = 0; frame < 3000 && rig.Count(CoreMechanicCueIDs.Block) == 0; frame++) {
                GameplayButtons held = frame % 12 < 2 ? GameplayButtons.BasicAttack : GameplayButtons.None;
                rig.Tick(1f, held, GameplayButtons.Block, prev, GameplayButtons.Block);
                prev = held;
            }
            AssertThat(rig.Count(CoreMechanicCueIDs.Block) >= 1)
                .OverrideFailureMessage("A blocked swing must play the block ping. Seen: " + string.Join(",", rig.Seen))
                .IsTrue();
            AssertThat(rig.Count(CoreMechanicCueIDs.HitBasic))
                .OverrideFailureMessage("A block moves no HP, so it must play no hit thump. Seen: " + string.Join(",", rig.Seen))
                .IsEqual(0);
        } finally {
            Free(rig);
        }
    }

    [TestCase]
    public void ARiseOfThePerfectBlockCountPlaysTheBrightPingOnceAndALoweredCountPlaysNothing() {
        if (InputManager.Instance == null) return;
        Rig rig = NewRig("FighterCuePerfectHost");
        try {
            rig.Tick(0f, GameplayButtons.None, GameplayButtons.None);
            AssertThat(rig.Driver.Simulation.TryGetFighterActionState(1, out FighterActionStateComponent action)).IsTrue();
            FighterBlockTimingRules.NotePerfectBlock(ref action);
            AssertThat(rig.Driver.Simulation.SeedFighterActionStateForTest(1, in action)).IsTrue();
            rig.Tick(0f, GameplayButtons.None, GameplayButtons.None);
            rig.Tick(0f, GameplayButtons.None, GameplayButtons.None);
            AssertThat(rig.Count(CoreMechanicCueIDs.PerfectBlock)).OverrideFailureMessage("seen: " + string.Join(",", rig.Seen)).IsEqual(1);

            // Count 1 -> 3 plays once more (the row's throttle window has passed); 3 -> 2 is a correction.
            for (int i = 0; i < 12; i++) rig.Tick(0f, GameplayButtons.None, GameplayButtons.None);
            AssertThat(rig.Driver.Simulation.TryGetFighterActionState(1, out FighterActionStateComponent after)).IsTrue();
            FighterActionStateComponent countTwo = after;
            FighterBlockTimingRules.NotePerfectBlock(ref countTwo);
            FighterActionStateComponent countThree = countTwo;
            FighterBlockTimingRules.NotePerfectBlock(ref countThree);
            AssertThat(rig.Driver.Simulation.SeedFighterActionStateForTest(1, in countThree)).IsTrue();
            rig.Tick(0f, GameplayButtons.None, GameplayButtons.None);
            AssertThat(rig.Count(CoreMechanicCueIDs.PerfectBlock)).IsEqual(2);
            for (int i = 0; i < 12; i++) rig.Tick(0f, GameplayButtons.None, GameplayButtons.None);
            AssertThat(rig.Driver.Simulation.SeedFighterActionStateForTest(1, in countTwo)).IsTrue();
            rig.Tick(0f, GameplayButtons.None, GameplayButtons.None);
            AssertThat(rig.Count(CoreMechanicCueIDs.PerfectBlock))
                .OverrideFailureMessage("a lowered count is a correction, not a perfect block").IsEqual(2);
        } finally {
            Free(rig);
        }
    }

    [TestCase]
    public void ASwingAimedAtNothingPlaysTheWhiffOnceAndNoHitThump() {
        if (InputManager.Instance == null) return;
        Rig rig = NewRig("FighterCueWhiffHost");
        try {
            // Player one steers away from player two (facing follows the stick) and swings at empty air.
            GameplayButtons prev = GameplayButtons.None;
            for (int frame = 0; frame < 20; frame++) {
                GameplayButtons held = frame < 2 ? GameplayButtons.BasicAttack : GameplayButtons.None;
                rig.Tick(-1f, held, GameplayButtons.None, prev);
                prev = held;
            }
            AssertThat(rig.Count(CoreMechanicCueIDs.SwingWhiff)).OverrideFailureMessage("seen: " + string.Join(",", rig.Seen)).IsEqual(1);
            AssertThat(rig.Count(CoreMechanicCueIDs.HitBasic)).IsEqual(0);
        } finally {
            Free(rig);
        }
    }

    /// <summary>The pure edge rule: only Active -> Recovery or ChainHold with nothing resolved is a whiff.</summary>
    [TestCase]
    public void TheWhiffEdgeNeedsAnActiveWindowThatEndsIntoRecoveryWithNothingResolved() {
        const int none = FighterBasicAttackRules.PhaseNone;
        const int active = FighterBasicAttackRules.PhaseActive;
        const int recovery = FighterBasicAttackRules.PhaseRecovery;
        const int hold = FighterBasicAttackRules.PhaseChainHold;
        int resolved = FighterBasicAttackRules.FlagHitResolved;
        AssertThat(FighterSimulationDriver.SwingWhiffStarts(active, recovery, 0)).IsTrue();
        AssertThat(FighterSimulationDriver.SwingWhiffStarts(active, hold, FighterBasicAttackRules.FlagAerial)).IsTrue();
        // A resolved contact (landed, blocked or absorbed) is no whiff.
        AssertThat(FighterSimulationDriver.SwingWhiffStarts(active, recovery, resolved)).IsFalse();
        // A swing cancelled out of its active frames reads phase None.
        AssertThat(FighterSimulationDriver.SwingWhiffStarts(active, none, 0)).IsFalse();
        // Only the forward edge counts: startup -> active, recovery -> none, and a correction back to active.
        AssertThat(FighterSimulationDriver.SwingWhiffStarts(FighterBasicAttackRules.PhaseStartup, active, 0)).IsFalse();
        AssertThat(FighterSimulationDriver.SwingWhiffStarts(recovery, none, 0)).IsFalse();
        AssertThat(FighterSimulationDriver.SwingWhiffStarts(recovery, active, 0)).IsFalse();
        AssertThat(FighterSimulationDriver.SwingWhiffStarts(recovery, recovery, 0)).IsFalse();
    }
}
