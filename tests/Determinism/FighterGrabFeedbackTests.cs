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
/// Grab pass 2026-10-31: the Fighter driver's grab presentation. One grip spark
/// at the catch (never again during the hold or throw), a lifted and leaning
/// HELD victim, a leaning THROWN victim, and a reset to upright afterwards.
/// Read-only over the simulation.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterGrabFeedbackTests {
    private const double Step = 1.0 / 60.0;

    private sealed class Rig {
        public Node Host;
        public FighterSimulationDriver Driver;
        public PlayerController One;
        public PlayerController Two;
        public BufferedInputSource InputOne = new();
        public BufferedInputSource InputTwo = new();
        public SessionData SavedSession;
        public int SparksSeen;
        private readonly HashSet<ulong> _seen = new();

        public void Tick(float oneMove, GameplayButtons oneHeld, GameplayButtons onePrev = GameplayButtons.None) {
            uint tick = (uint)Driver.CurrentTick;
            InputOne.SetNextFrame(PlayerInputFrame.Create(tick, oneMove, 0f, oneHeld, onePrev));
            InputTwo.SetNextFrame(PlayerInputFrame.Create(tick, 0f, 0f, GameplayButtons.None, GameplayButtons.None));
            InputManager.Instance.SetInputSource(0, InputOne);
            InputManager.Instance.SetInputSource(1, InputTwo);
            Driver._PhysicsProcess(Step);
            foreach (Node child in Host.GetChildren()) {
                if (child is GrabSparkEffect e && e.IsActive && _seen.Add(e.GetInstanceId())) SparksSeen++;
            }
        }
    }

    private static Rig NewRig(string name) {
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
        var rig = new Rig { Host = host, Driver = driver, One = one, Two = two, SavedSession = savedSession };
        for (int i = 0; i < FighterMatchFlowRules.CountdownFrames + 2; i++) rig.Tick(0f, GameplayButtons.None);
        return rig;
    }

    private static void Free(Rig rig) {
        InputManager.Instance.ClearInputSource(0);
        InputManager.Instance.ClearInputSource(1);
        rig.Host.Free();
        GameManager.Instance.CurrentSession = rig.SavedSession;
    }

    private const int Hold = FighterGrabRules.PhaseHolding;
    private const int Throw = FighterGrabRules.PhaseThrowAnimation;

    [TestCase]
    public void ACatchIdentityIsPresentedOnceEvenAfterARollbackAndAnyDelay() {
        int last = 0;
        AssertThat(FighterSimulationDriver.ShouldPresentCatch(Hold, 101, last)).IsTrue();
        last = 101;
        // Held, then thrown: the same catch, no second spark.
        AssertThat(FighterSimulationDriver.ShouldPresentCatch(Hold, 101, last)).IsFalse();
        AssertThat(FighterSimulationDriver.ShouldPresentCatch(Throw, 101, last)).IsFalse();
        // A correction rolls the state back before the catch (no identity), then
        // re-adds the same catch later: still the same identity, still no spark.
        AssertThat(FighterSimulationDriver.ShouldPresentCatch(FighterGrabRules.PhaseActive, 0, last)).IsFalse();
        AssertThat(FighterSimulationDriver.ShouldPresentCatch(Hold, 101, last)).IsFalse();
    }

    [TestCase]
    public void ACatchIntroducedByACorrectionSparksOnce() {
        int last = 0;
        // Predicted whiff: recovery with no identity presents nothing.
        AssertThat(FighterSimulationDriver.ShouldPresentCatch(FighterGrabRules.PhaseNone, 0, last)).IsFalse();
        AssertThat(FighterSimulationDriver.ShouldPresentCatch(3, 0, last)).IsFalse();
        // The correction makes it a catch: Recovery -> Holding with a fresh identity.
        AssertThat(FighterSimulationDriver.ShouldPresentCatch(Hold, 240, last)).IsTrue();
        last = 240;
        AssertThat(FighterSimulationDriver.ShouldPresentCatch(Hold, 240, last)).IsFalse();
    }

    [TestCase]
    public void ALaterCatchWithANewIdentitySparksAgain() {
        AssertThat(FighterSimulationDriver.ShouldPresentCatch(Hold, 900, 101)).IsTrue();
    }

    [TestCase]
    public void TheHeldLeanPointsAtTheGrabberForFrontAndRearCatches() {
        // Grabber to the victim's right: lean toward +x, whichever way the victim faces.
        AssertThat(FighterSimulationDriver.TowardGrabberSign(0f, 1.2f)).IsEqual(1f);
        // Grabber to the victim's left (a rear catch of a victim facing right).
        AssertThat(FighterSimulationDriver.TowardGrabberSign(0f, -1.2f)).IsEqual(-1f);
        AssertThat(FighterSimulationDriver.TowardGrabberSign(3f, 3f)).IsEqual(0f);
    }

    [TestCase]
    public void ThrownLeanFollowsTheTravelAndStaysClamped() {
        AssertThat(FighterSimulationDriver.ThrownLeanRadians(0f, -600f)).IsEqual(0f);
        AssertThat(FighterSimulationDriver.ThrownLeanRadians(400f, -100f) > 0.5f).IsTrue();
        AssertThat(FighterSimulationDriver.ThrownLeanRadians(-400f, -100f) < -0.5f).IsTrue();
        AssertThat(FighterSimulationDriver.ThrownLeanRadians(100000f, 0f)).IsEqual(1f);
    }

    [TestCase]
    public void OneSparkAtTheCatchAHeldPoseThenAThrownPoseThenUpright() {
        if (InputManager.Instance == null) return;
        Rig rig = NewRig("FighterGrabFeedbackHost");
        try {
            // Walk right until the pair is in grab reach.
            for (int i = 0; i < 400; i++) {
                rig.Driver.Simulation.TryGetFighter(0, out var a);
                rig.Driver.Simulation.TryGetFighter(1, out var b);
                if ((b.Position.x - a.Position.x).ToFloat() <= BasicComboRules.GrabReachUnits) break;
                rig.Tick(1f, GameplayButtons.None);
            }
            rig.Tick(0f, GameplayButtons.Block | GameplayButtons.BasicAttack, GameplayButtons.None);

            bool sawHeld = false, sawThrown = false;
            for (int i = 0; i < 120; i++) {
                rig.Tick(0f, GameplayButtons.Block);
                rig.Driver.Simulation.TryGetFighterVerb(1, out FighterVerbComponent victim);
                if (victim.BeingHeld != 0) {
                    sawHeld = true;
                    rig.Driver.Simulation.TryGetFighter(0, out var g);
                    rig.Driver.Simulation.TryGetFighter(1, out var v);
                    rig.Driver.Simulation.TryGetFighterVerb(0, out FighterVerbComponent grabberVerb);
                    AssertThat(grabberVerb.GrabConnectFrame > 0).OverrideFailureMessage("The catch carries its identity.").IsTrue();
                    float expected = FighterSimulationDriver.TowardGrabberSign(v.Position.x.ToFloat(), g.Position.x.ToFloat());
                    AssertThat(System.MathF.Sign(rig.Two.Rotation)).OverrideFailureMessage("A held victim leans toward the grabber.").IsEqual((int)expected);
                    AssertThat(rig.Two.ActiveAnimationFrame).IsEqual(FighterSimulationDriver.HeldPoseFrame);
                } else if (sawHeld && rig.Two.Rotation != 0f) {
                    sawThrown = true;
                    AssertThat(rig.Two.ActiveAnimationFrame).IsEqual(FighterSimulationDriver.ThrownPoseFrame);
                }
            }
            AssertThat(sawHeld).OverrideFailureMessage("setup: the victim was never held.").IsTrue();
            AssertThat(sawThrown).OverrideFailureMessage("The thrown victim never got its pose.").IsTrue();
            rig.Driver.Simulation.TryGetFighterVerb(0, out FighterVerbComponent after);
            AssertThat(after.GrabConnectFrame).OverrideFailureMessage("The identity is cleared once the throw ends.").IsEqual(0);
            AssertThat(rig.SparksSeen)
                .OverrideFailureMessage("Exactly one grip spark per catch.")
                .IsEqual(1);
            AssertThat(rig.Two.Rotation)
                .OverrideFailureMessage("After the launch ends the victim stands upright again.")
                .IsEqual(0f);
        } finally {
            Free(rig);
        }
    }

    [TestCase]
    public void AWhiffedGrabNeverCarriesACatchIdentity() {
        if (InputManager.Instance == null) return;
        Rig rig = NewRig("FighterGrabWhiffHost");
        try {
            rig.Tick(0f, GameplayButtons.Block | GameplayButtons.BasicAttack, GameplayButtons.None);
            for (int i = 0; i < 90; i++) {
                rig.Tick(0f, GameplayButtons.None);
                rig.Driver.Simulation.TryGetFighterVerb(0, out FighterVerbComponent verb);
                AssertThat(verb.GrabConnectFrame).OverrideFailureMessage("A whiff has no catch identity.").IsEqual(0);
            }
            AssertThat(rig.SparksSeen).IsEqual(0);
        } finally {
            Free(rig);
        }
    }
}
