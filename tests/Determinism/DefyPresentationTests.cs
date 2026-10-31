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
        if (InputManager.Instance == null) return;
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
