using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A2. The authored bus hierarchy from <c>design-godot.md</c>'s "Audio
/// Engine Integration" graph.
///
/// <para>Before this package the buses were created at runtime by
/// <c>AudioManager._Ready</c> with no layout resource at all, which meant the
/// hierarchy was invisible to the editor, unavailable to any tool that inspects
/// project settings, and carried an orphaned "Ambient" bus nothing routed to.
/// These tests pin the committed layout instead: an engine that boots without it
/// would fall back to a bare Master bus and every sub-bus routing decision in the
/// framework would silently collapse onto it.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AudioBusLayoutTests {
    private const string LayoutPath = "res://resources/Audio/default_bus_layout.tres";

    [TestCase]
    public void TheAuthoredLayoutResourceExistsAndIsRegisteredInProjectSettings() {
        AssertThat(ResourceLoader.Exists(LayoutPath)).IsTrue();
        AssertObject(ResourceLoader.Load<AudioBusLayout>(LayoutPath)).IsNotNull();

        var configured = ProjectSettings.GetSetting("audio/buses/default_bus_layout").AsString();
        AssertThat(configured).IsEqual(LayoutPath);
    }

    [TestCase]
    public void EveryAuthoredBusExistsAtRuntimeAndRoutesToItsDocumentedParent() {
        AssertThat(AudioBuses.All.Length).IsEqual(9);
        foreach (string busName in AudioBuses.All) {
            AssertThat(AudioServer.GetBusIndex(busName) >= 0)
                .OverrideFailureMessage($"bus '{busName}' is missing from the running AudioServer")
                .IsTrue();
        }

        AssertThat(AudioServer.GetBusIndex(AudioBuses.Master)).IsEqual(0);
        AssertThat(AudioServer.GetBusSend(AudioServer.GetBusIndex(AudioBuses.Music))).IsEqual(AudioBuses.Master);
        AssertThat(AudioServer.GetBusSend(AudioServer.GetBusIndex(AudioBuses.SFX))).IsEqual(AudioBuses.Master);
        AssertThat(AudioServer.GetBusSend(AudioServer.GetBusIndex(AudioBuses.UI))).IsEqual(AudioBuses.Master);
        AssertThat(AudioServer.GetBusSend(AudioServer.GetBusIndex(AudioBuses.Combat))).IsEqual(AudioBuses.SFX);
        AssertThat(AudioServer.GetBusSend(AudioServer.GetBusIndex(AudioBuses.Movement))).IsEqual(AudioBuses.SFX);
        AssertThat(AudioServer.GetBusSend(AudioServer.GetBusIndex(AudioBuses.Environmental))).IsEqual(AudioBuses.SFX);
        // C01b (Package 11 A8): the clear path sits UNDER SFX so the player's SFX
        // gain and mute still govern it, and Dialogue sits under UI.
        AssertThat(AudioServer.GetBusSend(AudioServer.GetBusIndex(AudioBuses.CriticalCues)))
            .IsEqual(AudioBuses.SFX);
        AssertThat(AudioServer.GetBusSend(AudioServer.GetBusIndex(AudioBuses.Dialogue)))
            .IsEqual(AudioBuses.UI);
        AssertThat(AudioBuses.HierarchyIsIntact()).IsTrue();
    }

    /// <summary>
    /// Godot mixes buses from the highest index down, so a bus may only send to a
    /// bus that comes before it. Reordering the layout so a sub-bus precedes SFX
    /// would not error — it would just stop being mixed.
    /// </summary>
    [TestCase]
    public void SubBusesComeAfterTheBusTheySendInto() {
        foreach (string busName in AudioBuses.All) {
            string send = AudioBuses.SendTargetOf(busName);
            if (send.Length == 0) continue;
            AssertThat(AudioServer.GetBusIndex(send) < AudioServer.GetBusIndex(busName))
                .OverrideFailureMessage($"bus '{busName}' sends forward into '{send}'")
                .IsTrue();
        }
    }

    /// <summary>
    /// The orphaned runtime "Ambient" bus is gone. The ambient *stem* is music and
    /// plays on the Music bus; a bus named Ambient with nothing routed to it only
    /// invited a future author to route something into a dead end.
    /// </summary>
    [TestCase]
    public void NoOrphanedAmbientBusRemains() {
        AssertThat(AudioServer.GetBusIndex("Ambient")).IsEqual(-1);
    }

    /// <summary>
    /// The mix layer muffles by driving the authored low-pass filters. Without
    /// them the pause/low-health profiles degrade to volume-only, which is a real
    /// but silent loss of behaviour — so the effects are part of the contract.
    ///
    /// <para>Package 11 A8 / C01b moved the SFX-side filter DOWN onto the three
    /// background World SFX children. Leaving it on the shared SFX parent would
    /// have muffled the new Critical Cues path along with everything else, which
    /// is the failure the contract names outright: "never a second filter on a
    /// parent bus carrying protected cues."</para>
    /// </summary>
    [TestCase]
    public void BackgroundBusesCarryTheLowPassAndProtectedBusesCarryNone() {
        AssertThat(HasEffect<AudioEffectLowPassFilter>(AudioBuses.Music)).IsTrue();
        AssertThat(HasEffect<AudioEffectHardLimiter>(AudioBuses.Master)).IsTrue();

        foreach (string background in AudioBuses.BackgroundSfxBuses) {
            AssertThat(HasEffect<AudioEffectLowPassFilter>(background))
                .OverrideFailureMessage($"background bus '{background}' lost its low-pass filter")
                .IsTrue();
        }

        // The SFX parent itself must be clean, or the split buys nothing.
        AssertThat(HasEffect<AudioEffectLowPassFilter>(AudioBuses.SFX)).IsFalse();
        foreach (string protectedBus in AudioBuses.ProtectedBuses) {
            AssertThat(HasEffect<AudioEffectLowPassFilter>(protectedBus))
                .OverrideFailureMessage($"protected bus '{protectedBus}' must carry no background filter")
                .IsFalse();
        }
    }

    /// <summary>
    /// C01b: a selected profile's cutoff reaches the background World SFX buses
    /// and never the protected ones, so a warning stays clear through the most
    /// aggressive muffle the mix can select.
    /// </summary>
    [TestCase]
    public void ASelectedProfileFiltersBackgroundSfxButNotCriticalCues() {
        var mixer = new AudioSnapshotMixer();
        try {
            mixer.ApplySnapshot(AudioSnapshot.Pause);
            mixer.SettleImmediately();
            foreach (string background in AudioBuses.BackgroundSfxBuses) {
                AssertFloat(CutoffOf(background)).IsEqualApprox(1200f, 1f);
            }
            AssertFloat(CutoffOf(AudioBuses.CriticalCues))
                .OverrideFailureMessage("Critical Cues must carry no filter to drive at all.")
                .IsEqual(-1f);
        } finally {
            new AudioSnapshotMixer().SettleImmediately();
            AudioManager.Instance?.ApplySavedVolumes();
        }
    }

    /// <summary>Cutoff of a bus's first low-pass, or -1 when it has none.</summary>
    private static float CutoffOf(string busName) {
        int index = AudioServer.GetBusIndex(busName);
        if (index < 0) return -1f;
        for (int effect = 0; effect < AudioServer.GetBusEffectCount(index); effect++) {
            if (AudioServer.GetBusEffect(index, effect) is AudioEffectLowPassFilter filter) {
                return filter.CutoffHz;
            }
        }
        return -1f;
    }

    /// <summary>
    /// A bus the layout somehow lost must be recreated with its documented send
    /// rather than silently landing on Master.
    /// </summary>
    [TestCase]
    public void ResolveRecreatesAMissingSubBusWithItsDocumentedSend() {
        const string probe = "A2ProbeBus";
        int index = AudioBuses.Resolve(probe);
        try {
            AssertThat(index > 0).IsTrue();
            AssertThat(AudioServer.GetBusName(index)).IsEqual(probe);
            AssertThat(AudioServer.GetBusSend(index)).IsEqual(AudioBuses.Master);
        } finally {
            int cleanup = AudioServer.GetBusIndex(probe);
            if (cleanup > 0) AudioServer.RemoveBus(cleanup);
        }
    }

    private static bool HasEffect<T>(string busName) where T : AudioEffect {
        int index = AudioServer.GetBusIndex(busName);
        if (index < 0) return false;
        for (int effect = 0; effect < AudioServer.GetBusEffectCount(index); effect++) {
            if (AudioServer.GetBusEffect(index, effect) is T) return true;
        }
        return false;
    }
}
