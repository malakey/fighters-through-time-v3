using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// MM-FX (meter and Defy update 2026-10-31, plan section 2.11): the Defy's presentation in
/// both modes. Story raises <c>EventBus.OnDefyExecuted</c>; Fighter steps component 323's
/// wrapping Defy count. Neither touches simulation state.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DefyPresentationTests {

    private static int CountActiveRings(Node host) {
        int n = 0;
        foreach (Node child in host.GetChildren()) if (child is DefyBurstEffect e && e.IsActive) n++;
        return n;
    }

    private static string FindDefiedLabel(Node host) {
        foreach (Node child in host.GetChildren()) {
            if (child is FloatingDamageNumber number && number.DisplayedText.Length > 0
                && !int.TryParse(number.DisplayedText, out _)) return number.DisplayedText;
        }
        return null;
    }

    [TestCase]
    public void OneStoryDefyEventShowsTheRingAndDefiedEvenWithDamageNumbersOffAndRequestsTheCueAndSnapshot() {
        AudioManager audio = AudioManager.Instance;
        EventBus bus = EventBus.Instance;
        SaveManager save = SaveManager.Instance;
        if (audio?.CoreCues == null || bus == null || save?.GlobalData == null) return;
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "DefyStoryHost" };
        tree.Root.AddChild(host);
        Node previousScene = tree.CurrentScene;
        bool numbersWere = save.GlobalData.DamageNumbersVisible;
        bool wasReduced = ComfortSettings.ReducedTemporalEffects;
        CoreMechanicCuePresenter cues = audio.CoreCues;
        try {
            tree.CurrentScene = host;
            ComfortSettings.Apply(false);
            save.GlobalData.DamageNumbersVisible = false;
            cues.ResetTransient();
            int before = cues.CuesPlayed;

            bus.RaiseDefyExecuted(new DefyExecutedPayload { PlayerIndex = 0, Position = new Vector2(100f, 50f) });

            AssertThat(CountActiveRings(host)).IsEqual(1);
            string label = FindDefiedLabel(host);
            AssertThat(label != null).OverrideFailureMessage("DEFIED must show with damage numbers off.").IsTrue();
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.DefyBurst);
            AssertThat(cues.CuesPlayed - before).IsEqual(1);
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.DefyOrScripted)).IsTrue();

            // The snapshot is held for the freeze window and then released.
            cues.Advance(CoreMechanicCuePresenter.DefySnapshotSeconds + 0.01f);
            AssertThat(audio.IsSnapshotActive(AudioSnapshot.DefyOrScripted)).IsFalse();
        } finally {
            save.GlobalData.DamageNumbersVisible = numbersWere;
            ComfortSettings.Apply(wasReduced);
            tree.CurrentScene = previousScene;
            cues.ResetTransient();
            host.Free();
            audio.ReleaseAllVoices();
        }
    }

    [TestCase]
    public void TheScreenPulseIsSkippedUnderReducedTemporalEffectsButTheRingStillPlays() {
        var host = new Node2D { Name = "DefyPulseHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        bool wasReduced = ComfortSettings.ReducedTemporalEffects;
        try {
            ComfortSettings.Apply(false);
            AssertThat(DefyScreenPulse.Show(host) != null).IsTrue();

            ComfortSettings.Apply(true);
            AssertThat(DefyScreenPulse.Show(host) == null).IsTrue();
            DefyBurstEffect ring = DefyPresentation.Play(Vector2.Zero, host, DefyBurstEffect.StoryRadiusPixels);
            AssertThat(ring != null).IsTrue();
            int pulses = 0;
            foreach (Node child in host.GetChildren()) if (child is DefyScreenPulse) pulses++;
            // Only the one pulse made while effects were allowed exists.
            AssertThat(pulses).IsEqual(1);
        } finally {
            ComfortSettings.Apply(wasReduced);
            AudioManager.Instance?.CoreCues?.ResetTransient();
            host.Free();
        }
    }

    [TestCase]
    public void AFighterDefyCounterForwardStepSpawnsOneRingAndAnUnchangedOrLoweredCounterNone() {
        RequireInputManager();
        GameManager manager = GameManager.Instance;
        SessionData saved = manager.CurrentSession;
        var host = new Node2D { Name = "DefyFighterHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            PlayerController one = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
            PlayerController two = CharacterFactory.CreateCharacter("joan", 1, applyStoryProgression: false);
            host.AddChild(one);
            host.AddChild(two);
            var driver = new FighterSimulationDriver { Name = "Driver" };
            host.AddChild(driver);
            MatchSettings settings = MatchSettings.GetDefault();
            settings.StageHazardsEnabled = false;
            driver.Initialize(one, two, settings, stageHazardTypeID: 1, stageID: "florence_workshop", matchSeed: 2026);

            FighterActionStateComponent At(int count, int defyFrames) => new() {
                ActionFlags = count << FighterActionStateComponent.DefyCounterShift,
                DefyFrames = defyFrames
            };

            // The first read only records the baseline.
            AssertThat(driver.PresentDefyCounter(one, 0, At(0, 0))).IsFalse();
            AssertThat(CountActiveRings(host)).IsEqual(0);
            // A forward step while a Defy runs: one ring.
            AssertThat(driver.PresentDefyCounter(one, 0, At(1, 1))).IsTrue();
            AssertThat(CountActiveRings(host)).IsEqual(1);
            // The same count on later frames of the Defy: nothing more.
            AssertThat(driver.PresentDefyCounter(one, 0, At(1, 2))).IsFalse();
            AssertThat(driver.PresentDefyCounter(one, 0, At(1, 7))).IsFalse();
            // A rollback correction that lowers the count: nothing.
            AssertThat(driver.PresentDefyCounter(one, 0, At(0, 5))).IsFalse();
            // The ring wraps (15 to 0 with a Defy running is a forward step).
            driver.PresentDefyCounter(one, 0, At(15, 0));
            AssertThat(driver.PresentDefyCounter(one, 0, At(0, 1))).IsTrue();
            AssertThat(CountActiveRings(host)).IsEqual(2);
            // A reset to zero with no Defy running is not a Defy.
            driver.PresentDefyCounter(one, 0, At(15, 0));
            AssertThat(driver.PresentDefyCounter(one, 0, At(0, 0))).IsFalse();
            AssertThat(CountActiveRings(host)).IsEqual(2);
        } finally {
            AudioManager.Instance?.CoreCues?.ResetTransient();
            host.Free();
            manager.CurrentSession = saved;
        }
    }

    [TestCase]
    public void AnInterruptedDefyFollowedByAnotherWithinThirtyTicksPlaysTwoBursts() {
        RunDefyCounterCase((driver, one, host, at) => {
            driver.PresentDefyCounter(one, 0, at(0, 0));
            AssertThat(driver.PresentDefyCounter(one, 0, at(1, 6))).IsTrue();
            // Hit in the vulnerable recovery: the action ends, the counter stays; Defy again out of the hitstun.
            driver.PresentDefyCounter(one, 0, at(1, 0));
            AssertThat(driver.PresentDefyCounter(one, 0, at(2, 6)))
                .OverrideFailureMessage("A second Defy with no correction between is a genuine new Defy.")
                .IsTrue();
            AssertThat(CountActiveRings(host)).IsEqual(2);
        });
    }

    [TestCase]
    public void ARollbackCorrectionReplayOfOneDefyPlaysOneBurst() {
        RunDefyCounterCase((driver, one, host, at) => {
            driver.PresentDefyCounter(one, 0, at(0, 0));
            AssertThat(driver.PresentDefyCounter(one, 0, at(1, 6))).IsTrue();
            driver.PresentDefyCounter(one, 0, at(0, 0));
            AssertThat(driver.PresentDefyCounter(one, 0, at(1, 6)))
                .OverrideFailureMessage("0, 1, 0, 1 is the same Defy replayed.")
                .IsFalse();
            AssertThat(CountActiveRings(host)).IsEqual(1);
        });
    }

    [TestCase]
    public void ACorrectionFollowedByAGenuinelyNewDefyPlaysTwoBursts() {
        RunDefyCounterCase((driver, one, host, at) => {
            driver.PresentDefyCounter(one, 0, at(0, 0));
            AssertThat(driver.PresentDefyCounter(one, 0, at(1, 6))).IsTrue();
            driver.PresentDefyCounter(one, 0, at(0, 0));
            AssertThat(driver.PresentDefyCounter(one, 0, at(1, 6))).IsFalse();
            AssertThat(driver.PresentDefyCounter(one, 0, at(2, 6))).IsTrue();
            AssertThat(CountActiveRings(host)).IsEqual(2);
        });
    }

    private static FighterActionStateComponent DefyAt(int count, int defyFrames) => new() {
        ActionFlags = count << FighterActionStateComponent.DefyCounterShift,
        DefyFrames = defyFrames
    };

    private static PlayerInputFrame Neutral(int tick) => new() { Tick = (uint)tick };

    /// <summary>
    /// The stock-reset memory, driven through the driver's own sync path for both players: a Defy is
    /// presented, the stock is lost during its recovery (the sim's respawn-platform hold zeroes 323), and the
    /// next genuine Defy of the new stock plays even at the old counter value. Without the reset the
    /// lowered-then-raised counter reads as a rollback replay of the first Defy and stays silent.
    /// </summary>
    [TestCase]
    public void AStockLossDuringADefysRecoveryClearsBothPlayersMemorySoTheNextGenuineDefyPlays() {
        RunDrivenCase(new MatchSettings { Mode = MatchMode.Stock, StockCount = 3, TimeLimit = 480f }, (driver, players, host) => {
            for (int id = 0; id < 2; id++) {
                PlayerController player = players[id];
                int ringsBefore = CountActiveRings(host);
                driver.SyncDefyFeedback(player, id);
                AssertThat(driver.Simulation.SeedFighterActionStateForTest(id, DefyAt(1, 6))).IsTrue();
                driver.SyncDefyFeedback(player, id);
                AssertThat(CountActiveRings(host))
                    .OverrideFailureMessage($"Player {id}: the first Defy plays one ring through the sync path.")
                    .IsEqual(ringsBefore + 1);

                // The stock is lost with the Defy in its recovery: the platform hold zeroes 323.
                AssertThat(driver.Simulation.SeedFighterActionStateForTest(id, DefyAt(1, 12))).IsTrue();
                AssertThat(driver.Simulation.SeedFighterRespawnForTest(id, 120)).IsTrue();
                driver.Simulation.Advance(Neutral(1), Neutral(1));
                AssertThat(driver.Simulation.TryGetFighterActionState(id, out FighterActionStateComponent held)).IsTrue();
                AssertThat(held.ActionFlags & FighterActionStateComponent.DefyCounterMask)
                    .OverrideFailureMessage($"Player {id}: harness: the respawn hold must have zeroed the Defy counter.")
                    .IsEqual(0);
                driver.SyncDefyFeedback(player, id);

                // Back in the match, a genuine new Defy lands on the old counter value.
                AssertThat(driver.Simulation.SeedFighterRespawnForTest(id, 0)).IsTrue();
                driver.SyncDefyFeedback(player, id);
                AssertThat(driver.Simulation.SeedFighterActionStateForTest(id, DefyAt(1, 6))).IsTrue();
                driver.SyncDefyFeedback(player, id);
                AssertThat(CountActiveRings(host))
                    .OverrideFailureMessage($"Player {id}: after the stock reset the first Defy of the new stock plays even at the old counter value.")
                    .IsEqual(ringsBefore + 2);
            }
        });
    }

    /// <summary>The same, across Sudden Death entry (the round reset zeroes 323 for both fighters, one sync call resets both).</summary>
    [TestCase]
    public void SuddenDeathEntryClearsBothPlayersMemorySoTheNextGenuineDefyPlays() {
        RunDrivenCase(new MatchSettings { Mode = MatchMode.TimeLimit, StockCount = 1, TimeLimit = 1f }, (driver, players, host) => {
            for (int id = 0; id < 2; id++) {
                driver.SyncDefyFeedback(players[id], id);
                AssertThat(driver.Simulation.SeedFighterActionStateForTest(id, DefyAt(1, 6))).IsTrue();
                driver.SyncDefyFeedback(players[id], id);
            }
            AssertThat(CountActiveRings(host)).OverrideFailureMessage("Harness: one ring per player before the round ends.").IsEqual(2);

            for (int tick = 1; tick <= 600 && driver.Simulation.GetMatchState().SuddenDeathActive != 1; tick++) {
                driver.Simulation.Advance(Neutral(tick), Neutral(tick));
            }
            AssertThat(driver.Simulation.GetMatchState().SuddenDeathActive)
                .OverrideFailureMessage("Harness: the one-second timer must have ended in Sudden Death.")
                .IsEqual(1);

            for (int id = 0; id < 2; id++) {
                AssertThat(driver.Simulation.TryGetFighterActionState(id, out FighterActionStateComponent setup)).IsTrue();
                AssertThat(setup.ActionFlags & FighterActionStateComponent.DefyCounterMask)
                    .OverrideFailureMessage($"Player {id}: harness: the Sudden Death setup must have zeroed the Defy counter.")
                    .IsEqual(0);
                driver.SyncDefyFeedback(players[id], id);
                driver.SyncDefyFeedback(players[id], id);
                AssertThat(driver.Simulation.SeedFighterActionStateForTest(id, DefyAt(1, 6))).IsTrue();
                driver.SyncDefyFeedback(players[id], id);
            }
            AssertThat(CountActiveRings(host))
                .OverrideFailureMessage("Both players' first Defy of the new phase plays even at the old counter value.")
                .IsEqual(4);
        });
    }

    private static void RequireInputManager() {
        AssertThat(InputManager.Instance)
            .OverrideFailureMessage("Harness: the InputManager autoload must exist; without it this case would pass vacuously.")
            .IsNotNull();
    }

    private static void RunDrivenCase(
        MatchSettings settings,
        System.Action<FighterSimulationDriver, PlayerController[], Node2D> body) {
        RequireInputManager();
        GameManager manager = GameManager.Instance;
        SessionData saved = manager.CurrentSession;
        var host = new Node2D { Name = "DefyDrivenHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            PlayerController one = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
            PlayerController two = CharacterFactory.CreateCharacter("joan", 1, applyStoryProgression: false);
            host.AddChild(one);
            host.AddChild(two);
            var driver = new FighterSimulationDriver { Name = "Driver" };
            host.AddChild(driver);
            settings.StageHazardsEnabled = false;
            driver.Initialize(one, two, settings, stageHazardTypeID: 1, stageID: "florence_workshop", matchSeed: 2028);
            body(driver, new[] { one, two }, host);
        } finally {
            AudioManager.Instance?.CoreCues?.ResetTransient();
            host.Free();
            manager.CurrentSession = saved;
        }
    }

    private static void RunDefyCounterCase(
        System.Action<FighterSimulationDriver, PlayerController, Node2D, System.Func<int, int, FighterActionStateComponent>> body) {
        RequireInputManager();
        GameManager manager = GameManager.Instance;
        SessionData saved = manager.CurrentSession;
        var host = new Node2D { Name = "DefyReplayHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            PlayerController one = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
            PlayerController two = CharacterFactory.CreateCharacter("joan", 1, applyStoryProgression: false);
            host.AddChild(one);
            host.AddChild(two);
            var driver = new FighterSimulationDriver { Name = "Driver" };
            host.AddChild(driver);
            MatchSettings settings = MatchSettings.GetDefault();
            settings.StageHazardsEnabled = false;
            driver.Initialize(one, two, settings, stageHazardTypeID: 1, stageID: "florence_workshop", matchSeed: 2027);
            body(driver, one, host, (count, defyFrames) => new FighterActionStateComponent {
                ActionFlags = count << FighterActionStateComponent.DefyCounterShift,
                DefyFrames = defyFrames
            });
        } finally {
            AudioManager.Instance?.CoreCues?.ResetTransient();
            host.Free();
            manager.CurrentSession = saved;
        }
    }

    [TestCase]
    public void TheDefyInvulnerableFramesAreOneToSixAndTheWorldLabelResolves() {
        var inv = new List<int>();
        for (int f = 0; f <= BasicComboRules.DefyTotalFrames; f++) {
            if (FighterSimulationDriver.IsDefyInvulnerable(new FighterActionStateComponent { DefyFrames = f })) inv.Add(f);
        }
        AssertThat(inv.Count).IsEqual(BasicComboRules.DefyInvulnerableFrames);
        AssertThat(inv[0]).IsEqual(1);
        AssertThat(inv[inv.Count - 1]).IsEqual(6);
        AssertThat(DefyPresentation.LabelKey).IsEqual("defy_world_label");
        AssertThat(TranslationServer.Translate(DefyPresentation.LabelKey)).IsEqual("DEFIED");
        AssertThat(DefyPresentation.LabelScale).IsEqual(1.25f);
    }
}
