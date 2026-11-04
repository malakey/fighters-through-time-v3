using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 12 W10 / H04. The single core-mechanic cue catalog
/// (<c>resources/Audio/core_mechanic_cues.tres</c>) against design-godot.md
/// Section 8 "7. Core Mechanic Cues": every cue present once, its bus and its
/// Critical flag, silent placeholder streams, the ten per-stage hazard slots, and
/// critical routing that survives the W6 category mutes.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CoreMechanicCueCatalogTests {
    private static readonly string[] FighterStageSets = {
        "florence", "orleans", "chicago", "paris", "vesuvius",
        "nassau", "alexandria", "berlin", "globe", "gettysburg"
    };

    private static CoreMechanicCueCatalog Catalog() {
        CoreMechanicCueCatalog catalog = CoreMechanicCueCatalog.LoadDefault();
        AssertObject(catalog).IsNotNull();
        return catalog;
    }

    [TestCase]
    public void TheCatalogCarriesEveryDesignedCueOnceWithItsBusAndCriticalFlag() {
        CoreMechanicCueCatalog catalog = Catalog();
        var issues = new List<string>();
        var seen = new HashSet<string>();
        var critical = new HashSet<string>(CoreMechanicCueIDs.Critical);
        var buses = new HashSet<string>(AudioBuses.All);
        foreach (CoreMechanicCueEntry entry in catalog.Entries) {
            if (entry == null) { issues.Add("null row"); continue; }
            if (!seen.Add(entry.CueID)) issues.Add($"duplicate {entry.CueID}");
            if (entry.Stream == null) issues.Add($"{entry.CueID} has no stream");
            if (!buses.Contains(entry.Bus)) issues.Add($"{entry.CueID} names unknown bus '{entry.Bus}'");
            if (entry.Critical != critical.Contains(entry.CueID)) {
                issues.Add($"{entry.CueID} Critical={entry.Critical} disagrees with the GDD");
            }
            if (entry.Critical && entry.ResolvedBus != AudioBuses.CriticalCues) {
                issues.Add($"{entry.CueID} is critical but resolves to {entry.ResolvedBus}");
            }
        }
        foreach (string id in CoreMechanicCueIDs.All) {
            if (!seen.Contains(id)) issues.Add($"missing {id}");
        }
        AssertThat(catalog.Entries.Count).IsEqual(CoreMechanicCueIDs.All.Length);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

        // Roster growth: per-hero fanfares resolve by convention, falling back to
        // the shared row — the cast is never enumerated in the catalog.
        AssertThat(catalog.VictoryFanfareFor("einstein").CueID).IsEqual(CoreMechanicCueIDs.VictoryFanfare);
        AssertThat(catalog.VictoryFanfareFor(null).CueID).IsEqual(CoreMechanicCueIDs.VictoryFanfare);
    }

    [TestCase]
    public void EveryFighterStageAuthorsAHazardWarningAndImpactSlotAndEveryPlaceholderIsSilentExceptTheAudibleCombatCues() {
        var issues = new List<string>();
        foreach (string stage in FighterStageSets) {
            string path = $"res://resources/Audio/stage_{stage}_audio.tres";
            var set = AuthoredResources.Load<StageAudioSet>(path);
            if (set == null) { issues.Add($"{path} did not load"); continue; }
            if (set.HazardWarningCue == null) issues.Add($"{stage} has no hazard warning cue");
            if (set.HazardImpactCue == null) issues.Add($"{stage} has no hazard impact cue");
            CheckSilent(set.HazardWarningCue, $"{stage} warning", issues);
            CheckSilent(set.HazardImpactCue, $"{stage} impact", issues);
        }
        foreach (CoreMechanicCueEntry entry in Catalog().Entries) {
            CheckSilent(entry.Stream, entry.CueID, issues);
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// The 2026-08-10 directive: placeholder audio is digital silence, amended on 2026-11-03
    /// (GR-AUDIO) for <see cref="CoreMechanicCueIDs.AudiblePlaceholders"/> only. A WAV row must be
    /// all zero bytes; an .ogg row must be a generator asset under <c>res://audio/sfx/</c> whose
    /// manifest record is audible exactly when the row is one of the audible placeholders.
    /// (A stage hazard slot's label is a stage name, never an audible row ID.)
    /// </summary>
    private static void CheckSilent(AudioStream stream, string label, List<string> issues) {
        if (stream == null) return;
        if (stream is AudioStreamWav wav) {
            foreach (byte sample in wav.Data) {
                if (sample != 0) { issues.Add($"{label} is not silent"); return; }
            }
            return;
        }
        if (!stream.ResourcePath.StartsWith("res://audio/sfx/")) {
            issues.Add($"{label} uses an unrecognized stream {stream.ResourcePath}");
            return;
        }
        bool audibleRow = CoreMechanicCueIDs.IsAudiblePlaceholder(label);
        if (!PlaceholderCueManifest.Load().TryGetValue(PlaceholderCueManifest.KeyFor(stream), out PlaceholderCueManifest.Asset asset)) {
            issues.Add($"{label} names an asset the manifest does not describe: {stream.ResourcePath}");
        } else if (asset.Audible != audibleRow) {
            issues.Add($"{label} audible={audibleRow} but its asset {stream.ResourcePath} audible={asset.Audible}");
        }
    }

    // === GR-AUDIO (gameplay review ruling 13, 2026-11-03) ====================

    [TestCase]
    public void TheFourNewRowsAndTheAudibleSetAreAuthoredAtMinusTwelveDecibels() {
        CoreMechanicCueCatalog catalog = Catalog();
        var issues = new List<string>();
        foreach (string id in new[] {
                     CoreMechanicCueIDs.HitBasic, CoreMechanicCueIDs.Block,
                     CoreMechanicCueIDs.PerfectBlock, CoreMechanicCueIDs.SwingWhiff }) {
            CoreMechanicCueEntry entry = catalog.Find(id);
            if (entry == null) { issues.Add($"missing {id}"); continue; }
            if (entry.Bus != AudioBuses.Combat) issues.Add($"{id} bus {entry.Bus}");
            if (entry.Critical) issues.Add($"{id} must not be critical");
            if (!(entry.MinIntervalSeconds > 0f)) issues.Add($"{id} has no throttle");
            if (System.Array.IndexOf(CoreMechanicCueIDs.All, id) < 0) issues.Add($"{id} not in All");
        }
        // The eight combat cues of the ruling plus the decisive KO variant that shares the stinger's asset.
        AssertThat(CoreMechanicCueIDs.AudiblePlaceholders.Length).IsEqual(9);
        foreach (string id in CoreMechanicCueIDs.AudiblePlaceholders) {
            CoreMechanicCueEntry entry = catalog.Find(id);
            if (entry == null) { issues.Add($"audible row missing {id}"); continue; }
            if (!Mathf.IsEqualApprox(entry.VolumeDb, -12f)) issues.Add($"{id} VolumeDb {entry.VolumeDb}");
        }
        // A row that does not author the additive fields plays at the old level, unthrottled.
        AssertThat(catalog.Find(CoreMechanicCueIDs.RallyReclaim).VolumeDb).IsEqual(0f);
        AssertThat(catalog.Find(CoreMechanicCueIDs.RallyReclaim).MinIntervalSeconds).IsEqual(0f);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void ARowsVolumeReachesTheVoiceOnBothTheOrdinaryAndTheCriticalPath() {
        AudioManager audio = AudioManager.Instance;
        CoreMechanicCuePresenter cues = audio.CoreCues;
        try {
            cues.ResetTransient();
            AssertThat(cues.Play(CoreMechanicCueIDs.HitBasic)).IsTrue();
            AssertThat(audio.NewestActiveVoice.VolumeDb).IsEqual(-12f);
            AssertThat(cues.LastVolumeDb).IsEqual(-12f);
            AssertThat(cues.Play(CoreMechanicCueIDs.UltimateActivationWindup)).IsTrue();
            AssertThat(cues.LastRoutedBus).IsEqual(AudioBuses.CriticalCues);
            AssertThat(audio.NewestActiveVoice.VolumeDb).IsEqual(-12f);
            // A silent, unauthored row stays at 0 dB.
            AssertThat(cues.Play(CoreMechanicCueIDs.UltimateReady)).IsTrue();
            AssertThat(audio.NewestActiveVoice.VolumeDb).IsEqual(0f);
        } finally {
            cues.ResetTransient();
            audio.ReleaseAllVoices();
        }
    }

    /// <summary>A swarm wave: dozens of hit requests inside one frame play once, then the window reopens.</summary>
    [TestCase]
    public void ASwarmOfHitRequestsInsideTheWindowPlaysOnceAndThenTheWindowReopens() {
        AudioManager audio = AudioManager.Instance;
        CoreMechanicCuePresenter cues = audio.CoreCues;
        try {
            cues.ResetTransient();
            int playedBefore = cues.CuesPlayed;
            int throttledBefore = cues.CuesThrottled;
            for (int hit = 0; hit < 40; hit++) AssertThat(cues.Play(CoreMechanicCueIDs.HitBasic)).IsTrue();
            AssertThat(cues.CuesPlayed - playedBefore).IsEqual(1);
            AssertThat(cues.CuesThrottled - throttledBefore).IsEqual(39);

            // A hit every two frames for a second is thinned to the row's window.
            cues.ResetTransient();
            playedBefore = cues.CuesPlayed;
            for (int frame = 0; frame < 60; frame++) {
                if (frame % 2 == 0) cues.Play(CoreMechanicCueIDs.HitBasic);
                cues.Advance(1f / 60f);
            }
            int played = cues.CuesPlayed - playedBefore;
            AssertThat(played >= 10 && played <= 12)
                .OverrideFailureMessage($"expected about 1 / 0.09 s, got {played}").IsTrue();

            // One row's throttle never starves another row.
            cues.ResetTransient();
            cues.Play(CoreMechanicCueIDs.HitBasic);
            playedBefore = cues.CuesPlayed;
            cues.Play(CoreMechanicCueIDs.Block);
            AssertThat(cues.CuesPlayed - playedBefore).IsEqual(1);

            // A reopened window plays again.
            cues.Advance(0.1f);
            playedBefore = cues.CuesPlayed;
            cues.Play(CoreMechanicCueIDs.HitBasic);
            AssertThat(cues.CuesPlayed - playedBefore).IsEqual(1);
        } finally {
            cues.ResetTransient();
            audio.ReleaseAllVoices();
        }
    }

    [TestCase]
    public void MutingPlaceholderCuesDropsTheAudibleRowsOnlyAndUnmutingRestoresThem() {
        AudioManager audio = AudioManager.Instance;
        CoreMechanicCuePresenter cues = audio.CoreCues;
        try {
            cues.ResetTransient();
            PlaceholderCueSettings.Apply(true);
            int played = cues.CuesPlayed;
            int muted = cues.CuesMuted;
            foreach (string id in CoreMechanicCueIDs.AudiblePlaceholders) {
                AssertThat(cues.Play(id)).OverrideFailureMessage(id).IsTrue();
            }
            AssertThat(cues.CuesPlayed).IsEqual(played);
            AssertThat(cues.CuesMuted - muted).IsEqual(CoreMechanicCueIDs.AudiblePlaceholders.Length);

            // Silent rows are untouched by the switch.
            cues.Play(CoreMechanicCueIDs.UltimateReady);
            AssertThat(cues.CuesPlayed - played).IsEqual(1);

            PlaceholderCueSettings.Apply(false);
            cues.Play(CoreMechanicCueIDs.DefyBurst);
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.DefyBurst);
            AssertThat(cues.LastRoutedBus).IsEqual(AudioBuses.CriticalCues);
        } finally {
            PlaceholderCueSettings.Apply(false);
            cues.ResetTransient();
            audio.ReleaseAllVoices();
        }
    }

    [TestCase]
    public void CriticalCuesRouteThroughTheClearPathEvenWithEveryCategoryMuted() {
        AudioManager audio = AudioManager.Instance;
        AssertObject(audio).IsNotNull();
        CoreMechanicCuePresenter cues = audio.CoreCues;
        AssertObject(cues).IsNotNull();
        int criticalBus = AudioServer.GetBusIndex(AudioBuses.CriticalCues);
        try {
            foreach (string category in AudioManager.MuteCategoryBuses) audio.SetCategoryMuted(category, true);
            AssertThat(AudioServer.IsBusMute(criticalBus)).IsFalse();

            var issues = new List<string>();
            foreach (string id in CoreMechanicCueIDs.All) {
                AssertThat(cues.Play(id)).IsTrue();
                CoreMechanicCueEntry entry = cues.Catalog.Find(id);
                string expected = entry.Critical ? AudioBuses.CriticalCues : entry.Bus;
                if (cues.LastRoutedBus != expected) issues.Add($"{id} routed to {cues.LastRoutedBus}");
                AudioStreamPlayer voice = audio.NewestActiveVoice;
                if (voice == null || voice.Bus.ToString() != expected) {
                    issues.Add($"{id} voice on {voice?.Bus.ToString() ?? "none"}");
                }
            }
            if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
            // The clear path still sends into SFX, so the player's SFX mute applies
            // through the send (C01b) — the category mute never reroutes it.
            AssertThat(AudioServer.GetBusSend(criticalBus).ToString()).IsEqual(AudioBuses.SFX);

            // A Fighter stage's hazard slots are always critical, set or fallback.
            cues.PlayStageHazard(warning: true);
            AssertThat(cues.LastRoutedBus).IsEqual(AudioBuses.CriticalCues);
            cues.PlayStageHazard(warning: false);
            AssertThat(cues.LastRoutedBus).IsEqual(AudioBuses.CriticalCues);
        } finally {
            audio.ApplySavedMutes();
            audio.ReleaseAllVoices();
        }
    }

    [TestCase]
    public void StoryEventsDriveTheirCuesFromTheExistingBus() {
        AudioManager audio = AudioManager.Instance;
        CoreMechanicCuePresenter cues = audio.CoreCues;
        EventBus bus = EventBus.Instance;
        try {
            cues.ResetTransient();

            // Time Freeze: activation (+ drone), the thaw warning in the final
            // 0.5 s, and the release at thaw.
            bus.RaiseTimeFreezeStateChanged(new TimeFreezePayload { State = TimeFreezeState.Active, SecondsRemaining = 5f });
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.TimeFreezeDrone);
            bus.RaiseTimeFreezeStateChanged(new TimeFreezePayload { State = TimeFreezeState.Active, SecondsRemaining = 1f });
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.TimeFreezeDrone);
            cues.Advance(0.49f);
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.TimeFreezeDrone);
            cues.Advance(0.02f);
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.TimeFreezeThawWarning);
            bus.RaiseTimeFreezeStateChanged(new TimeFreezePayload { State = TimeFreezeState.Cooldown, SecondsRemaining = 45f });
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.TimeFreezeThawRelease);

            // Defy (MM-FX): a burst plays the Critical defy_burst cue on the clear path.
            bus.RaiseDefyExecuted(new DefyExecutedPayload { PlayerIndex = 0, Position = Vector2.Zero });
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.DefyBurst);
            AssertThat(cues.LastRoutedBus).IsEqual(AudioBuses.CriticalCues);

            // The Ultimate-ready chord plays once per fill.
            int before = cues.CuesPlayed;
            bus.RaiseUltimateMeterChanged(new UltimateMeterPayload { PlayerIndex = 0, IsFull = true, NormalizedValue = 1f });
            bus.RaiseUltimateMeterChanged(new UltimateMeterPayload { PlayerIndex = 0, IsFull = true, NormalizedValue = 1f });
            AssertThat(cues.CuesPlayed - before).IsEqual(1);
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.UltimateReady);

            // The Integrity tick rides the Collapse Tremor: 1 s below 20 %, 0.5 s below 10 %.
            bus.RaiseCollapseTremorChanged(new TremorPayload { Level = 1 });
            AssertThat(cues.IntegrityTickInterval).IsEqual(CoreMechanicCuePresenter.IntegrityTickSecondsLevelOne);
            cues.Advance(1.01f);
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.IntegrityTick);
            bus.RaiseCollapseTremorChanged(new TremorPayload { Level = 2 });
            AssertThat(cues.IntegrityTickInterval).IsEqual(CoreMechanicCuePresenter.IntegrityTickSecondsLevelTwo);
            bus.RaiseCollapseTremorChanged(new TremorPayload { Level = 0 });
            before = cues.CuesPlayed;
            cues.Advance(3f);
            AssertThat(cues.CuesPlayed).IsEqual(before);

            // Rally: pitched up as more of the pool returns.
            AssertThat(CoreMechanicCuePresenter.RallyReclaimPitch(0.1f) > CoreMechanicCuePresenter.RallyReclaimPitch(0.9f)).IsTrue();

            // R03: W1's Post-Landing thaw warning plays the catalog's stream.
            AssertObject(ChronalRewindManager.ThawCueStream).IsNotNull();
        } finally {
            cues.ResetTransient();
            audio.ReleaseAllVoices();
        }
    }

    /// <summary>
    /// MM-FX (P29): the Defy burst is a Critical row and the two retired seal cues are gone
    /// from the catalog and from the ID lists.
    /// </summary>
    [TestCase]
    public void TheDefyBurstIsCriticalAndTheRetiredSealCuesAreGone() {
        CoreMechanicCueCatalog catalog = Catalog();
        CoreMechanicCueEntry burst = catalog.Find(CoreMechanicCueIDs.DefyBurst);
        AssertObject(burst).IsNotNull();
        AssertThat(CoreMechanicCueIDs.DefyBurst).IsEqual("defy_burst");
        AssertThat(burst.Critical).IsTrue();
        AssertThat(burst.ResolvedBus).IsEqual(AudioBuses.CriticalCues);
        AssertThat(new List<string>(CoreMechanicCueIDs.Critical).Contains(CoreMechanicCueIDs.DefyBurst)).IsTrue();
        AssertObject(catalog.Find("defy_toll")).IsNull();
        AssertObject(catalog.Find("defy_seal_shatter")).IsNull();
        AssertThat(new List<string>(CoreMechanicCueIDs.All).Contains("defy_toll")).IsFalse();
    }

    /// <summary>
    /// Package 12 Phase C seam: W10 authored the N01 seal charge/lock rows before
    /// W8 built the sealing anchor. The anchor now publishes its two edges on
    /// <see cref="EventBus.OnSealingAnchorChanged"/>; arming plays the hum (there
    /// is no hold to charge through) and the accepted seal plays the lock, each
    /// exactly once however often the anchor is poked.
    /// </summary>
    [TestCase]
    public void TheSealingAnchorPlaysTheChargeHumWhenArmedAndTheLockWhenSealed() {
        AudioManager audio = AudioManager.Instance;
        CoreMechanicCuePresenter cues = audio.CoreCues;
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        TemporalCoreAnchor anchor = TemporalCoreAnchor.CreateSealingAnchor("phase_c.sealing_anchor", Vector2.Zero);
        try {
            cues.ResetTransient();
            tree.Root.AddChild(anchor);
            int before = cues.CuesPlayed;

            // Dormant: a premature seal is refused and plays nothing.
            AssertThat(anchor.InsertCore()).IsFalse();
            AssertThat(cues.CuesPlayed).IsEqual(before);

            AssertThat(anchor.Arm()).IsTrue();
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.SealCharge);
            AssertThat(anchor.Arm()).IsFalse();
            AssertThat(cues.CuesPlayed - before).IsEqual(1);

            AssertThat(anchor.InsertCore()).IsTrue();
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.SealLock);
            AssertThat(anchor.InsertCore()).IsFalse();
            AssertThat(cues.CuesPlayed - before).IsEqual(2);
        } finally {
            if (IsInstanceValid(anchor)) anchor.Free();
            cues.ResetTransient();
            audio.ReleaseAllVoices();
        }
    }

    /// <summary>
    /// CM-ULT (2026-10-16, decision 2.1): the A02 activation wind-up cue is a
    /// Critical row — it warns of an unblockable strike — with an audible
    /// placeholder stream, listed in both ID tables, and it plays on the
    /// CriticalCues path.
    /// </summary>
    [TestCase]
    public void TheUltimateActivationWindupRowIsACriticalAudiblePlaceholderCue() {
        CoreMechanicCueEntry entry = Catalog().Find(CoreMechanicCueIDs.UltimateActivationWindup);
        AssertObject(entry).IsNotNull();
        AssertThat(entry.Critical).IsTrue();
        AssertThat(entry.ResolvedBus).IsEqual(AudioBuses.CriticalCues);
        AssertObject(entry.Stream).IsNotNull();
        var issues = new List<string>();
        CheckSilent(entry.Stream, entry.CueID, issues);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(System.Array.IndexOf(CoreMechanicCueIDs.All, CoreMechanicCueIDs.UltimateActivationWindup) >= 0).IsTrue();
        AssertThat(System.Array.IndexOf(CoreMechanicCueIDs.Critical, CoreMechanicCueIDs.UltimateActivationWindup) >= 0).IsTrue();

        AudioManager audio = AudioManager.Instance;
        AssertObject(audio).IsNotNull();
        CoreMechanicCuePresenter cues = audio.CoreCues;
        try {
            AssertThat(cues.Play(CoreMechanicCueIDs.UltimateActivationWindup)).IsTrue();
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.UltimateActivationWindup);
            AssertThat(cues.LastRoutedBus).IsEqual(AudioBuses.CriticalCues);
        } finally {
            audio.ReleaseAllVoices();
        }
    }

    private static bool IsInstanceValid(GodotObject node) => GodotObject.IsInstanceValid(node);
}
