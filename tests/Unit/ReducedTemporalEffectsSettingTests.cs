using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A8 / C01a. The <b>Reduced Temporal Effects</b> comfort preset: its
/// persistence, its legacy default, its live application, and the invariant that
/// makes it safe — it is local presentation state and cannot reach gameplay.
///
/// <para>The legacy-default case is the one worth the words. C01a says a missing
/// value "defaults Off without overwriting an existing explicit value or inferring
/// it from Screen Shake". Inferring it would have been the tempting shortcut — a
/// player who turned shake off probably wants calmer visuals — and it is wrong:
/// shake and distortion are different complaints, and silently deciding for
/// someone is exactly what an accessibility setting must not do.</para>
/// </summary>
[TestSuite]
public class ReducedTemporalEffectsSettingTests {

    [TestCase]
    public void AFreshPayloadDefaultsOffAndNeverInfersFromScreenShake() {
        var data = new GlobalSaveData { ScreenShakeScale = 0f };
        data.Normalize();
        AssertThat(data.ReducedTemporalEffects)
            .OverrideFailureMessage("Zero screen shake must not imply the reduced preset.")
            .IsFalse();

        // An explicit choice survives normalization untouched in both directions.
        data.ReducedTemporalEffects = true;
        data.Normalize();
        AssertThat(data.ReducedTemporalEffects).IsTrue();

        data.ScreenShakeScale = 1f;
        data.Normalize();
        AssertThat(data.ReducedTemporalEffects)
            .OverrideFailureMessage("Restoring screen shake must not clear an explicit preset.")
            .IsTrue();
    }

    [TestCase]
    public void ApplyingThePresetPushesItToTheRuntimeReadPointAndRaisesOnce() {
        bool original = ComfortSettings.ReducedTemporalEffects;
        int raised = 0;
        void Count(bool _) => raised++;
        try {
            ComfortSettings.Apply(false);
            ComfortSettings.Changed += Count;

            ComfortSettings.Apply(true);
            AssertThat(ComfortSettings.ReducedTemporalEffects).IsTrue();
            AssertThat(raised).IsEqual(1);

            // Re-applying the same value changes nothing and raises nothing, so a
            // per-frame poll cannot spam consumers into clearing effects forever.
            ComfortSettings.Apply(true);
            AssertThat(raised).IsEqual(1);

            ComfortSettings.Apply(false);
            AssertThat(ComfortSettings.ReducedTemporalEffects).IsFalse();
            AssertThat(raised).IsEqual(2);
        } finally {
            ComfortSettings.Changed -= Count;
            ComfortSettings.Apply(original);
        }
    }

    /// <summary>
    /// The reduced treatment table, as the four shapes the repo's presentation
    /// code actually branches on. Every one is off while the preset is on, and
    /// every one returns when it is off — "turning Off resumes only the current
    /// event's remaining authored presentation".
    /// </summary>
    [TestCase]
    public void TheReducedTreatmentSuppressesDistortionTrailsFlashesAndPulses() {
        bool original = ComfortSettings.ReducedTemporalEffects;
        try {
            ComfortSettings.Apply(true);
            AssertThat(ComfortSettings.DistortionAllowed).IsFalse();
            AssertThat(ComfortSettings.GhostTrailsAllowed).IsFalse();
            AssertThat(ComfortSettings.FullScreenFlashAllowed).IsFalse();
            AssertThat(ComfortSettings.ScreenTintPulseAllowed).IsFalse();
            // Armor/status/spawn/Defy feedback becomes a STEADY glow: the authored
            // pulse collapses, the cue itself does not.
            AssertFloat(ComfortSettings.ResolvePulseSpeed(3f)).IsEqual(0f);

            ComfortSettings.Apply(false);
            AssertThat(ComfortSettings.DistortionAllowed).IsTrue();
            AssertThat(ComfortSettings.GhostTrailsAllowed).IsTrue();
            AssertThat(ComfortSettings.FullScreenFlashAllowed).IsTrue();
            AssertThat(ComfortSettings.ScreenTintPulseAllowed).IsTrue();
            AssertFloat(ComfortSettings.ResolvePulseSpeed(3f)).IsEqual(3f);
        } finally {
            ComfortSettings.Apply(original);
        }
    }

    /// <summary>
    /// C01a: "Store and apply the preference locally, outside match
    /// snapshots/hashes. Under S01 it cannot influence hit eligibility, target
    /// selection, random draws or simulation results."
    ///
    /// <para>Pinned structurally rather than by running two matches: nothing under
    /// <c>scripts/FighterSim/</c> may reference <see cref="ComfortSettings"/> at
    /// all. A hash comparison would pass today and say nothing about the first
    /// deterministic system that reads the preset tomorrow; this fails the moment
    /// one does.</para>
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void NoDeterministicSimulationCodeReadsThePreset() {
        string simRoot = Godot.ProjectSettings.GlobalizePath("res://scripts/FighterSim");
        var offenders = new System.Collections.Generic.List<string>();
        foreach (string file in System.IO.Directory.GetFiles(simRoot, "*.cs", System.IO.SearchOption.AllDirectories)) {
            string text = System.IO.File.ReadAllText(file);
            if (text.Contains("ComfortSettings") || text.Contains("ReducedTemporalEffects")) {
                offenders.Add(System.IO.Path.GetFileName(file));
            }
        }
        if (offenders.Count > 0) {
            AssertThat(
                "scripts/FighterSim must never read the comfort preset (C01a / S01): "
                + string.Join(", ", offenders)).IsEqual("");
        }
    }

    /// <summary>
    /// The preset must be live before the first loading/portal effect. SaveManager
    /// is where the global payload lands, so that is the boundary — and the read
    /// point answers correctly even with no save loaded at all, which is the state
    /// a first boot is in.
    /// </summary>
    [TestCase]
    public void ThePresetIsReadableBeforeAnySaveIsLoaded() {
        bool original = ComfortSettings.ReducedTemporalEffects;
        try {
            ComfortSettings.Apply(true);
            var data = new GlobalSaveData();
            data.ApplyComfortSettings();
            AssertThat(ComfortSettings.ReducedTemporalEffects)
                .OverrideFailureMessage("A defaulted payload must push Off, not leave a stale value.")
                .IsFalse();

            data.ReducedTemporalEffects = true;
            data.ApplyComfortSettings();
            AssertThat(ComfortSettings.ReducedTemporalEffects).IsTrue();
        } finally {
            ComfortSettings.Apply(original);
        }
    }
}
