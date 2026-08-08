using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A2. The vertical-layer music director: synchronized stem starts,
/// intensity switching, and the linear crossfade from
/// <c>design-godot.md</c>'s "Vertical Layering (Combat Intensity)".
///
/// <para>Tests drive their own <see cref="StemDirector"/> instance rather than the
/// autoload's, so a failure cannot leave the running game's music in a half-faded
/// state. Time is stepped through <see cref="StemDirector.AdvanceFades"/> instead
/// of waiting on engine frames — that is exactly why the fade is a pumped
/// interpolation rather than a <c>Tween</c> node.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StemDirectorTests {
    private const string AmbientStemPath = "res://resources/Audio/placeholder_stem_ambient.tres";
    private const string CombatStemPath = "res://resources/Audio/placeholder_stem_combat.tres";
    private const string ClimaxStemPath = "res://resources/Audio/placeholder_stem_boss.tres";

    [TestCase]
    public void TheMixIsAdditiveAcrossIntensities() {
        // Ambient is the bed and never drops out; each higher intensity layers on top.
        float[] ambient = StemDirector.TargetMix(StemIntensity.Ambient);
        float[] combat = StemDirector.TargetMix(StemIntensity.Combat);
        float[] climax = StemDirector.TargetMix(StemIntensity.Climax);

        AssertThat(ambient[0]).IsEqual(1f);
        AssertThat(ambient[1]).IsEqual(0f);
        AssertThat(ambient[2]).IsEqual(0f);

        AssertThat(combat[0]).IsEqual(1f);
        AssertThat(combat[1]).IsEqual(1f);
        AssertThat(combat[2]).IsEqual(0f);

        AssertThat(climax[0]).IsEqual(1f);
        AssertThat(climax[1]).IsEqual(1f);
        AssertThat(climax[2]).IsEqual(1f);
    }

    [TestCase]
    public void RegisteringASetStartsAllThreeStemsWithOnlyAmbientAudible() {
        StemDirector director = BuildDirector(out Node host);
        try {
            director.RegisterStageAudio(BuildSet(crossfadeSeconds: 1f));

            AssertThat(director.IsActive).IsTrue();
            AssertThat(director.CurrentIntensity).IsEqual(StemIntensity.Ambient);

            // Synchronized stems must all be running, or a later crossfade would
            // drop a layer in out of phase.
            AssertThat(director.IsStemPlaying(StemIntensity.Ambient)).IsTrue();
            AssertThat(director.IsStemPlaying(StemIntensity.Combat)).IsTrue();
            AssertThat(director.IsStemPlaying(StemIntensity.Climax)).IsTrue();

            AssertThat(director.GetStemLevel(StemIntensity.Ambient)).IsEqualApprox(1f, 0.001f);
            AssertThat(director.GetStemLevel(StemIntensity.Combat)).IsEqualApprox(0f, 0.001f);
            AssertThat(director.GetStemVolumeDb(StemIntensity.Combat)).IsEqualApprox(StemDirector.SilentDb, 0.001f);
        } finally {
            Teardown(director, host);
        }
    }

    [TestCase]
    public void SwitchingIntensityCrossfadesOverTheAuthoredDurationRatherThanSnapping() {
        StemDirector director = BuildDirector(out Node host);
        try {
            director.RegisterStageAudio(BuildSet(crossfadeSeconds: 2f));
            director.SetIntensity(StemIntensity.Combat);

            // The target moves immediately; the audible level does not.
            AssertThat(director.GetTargetStemLevel(StemIntensity.Combat)).IsEqualApprox(1f, 0.001f);
            AssertThat(director.GetStemLevel(StemIntensity.Combat)).IsEqualApprox(0f, 0.001f);
            AssertThat(director.IsCrossfadeComplete()).IsFalse();

            director.AdvanceFades(1.0);
            AssertThat(director.GetStemLevel(StemIntensity.Combat)).IsEqualApprox(0.5f, 0.01f);
            AssertThat(director.IsCrossfadeComplete()).IsFalse();

            director.AdvanceFades(1.0);
            AssertThat(director.GetStemLevel(StemIntensity.Combat)).IsEqualApprox(1f, 0.001f);
            AssertThat(director.GetStemLevel(StemIntensity.Ambient)).IsEqualApprox(1f, 0.001f);
            AssertThat(director.IsCrossfadeComplete()).IsTrue();
            AssertThat(director.GetStemVolumeDb(StemIntensity.Combat)).IsEqualApprox(0f, 0.01f);
        } finally {
            Teardown(director, host);
        }
    }

    [TestCase]
    public void ClimaxAddsOnTopAndFallingBackToAmbientDropsBothUpperLayers() {
        StemDirector director = BuildDirector(out Node host);
        try {
            director.RegisterStageAudio(BuildSet(crossfadeSeconds: 0.5f));

            director.SetIntensity(StemIntensity.Climax);
            director.AdvanceFades(0.5);
            AssertThat(director.GetStemLevel(StemIntensity.Combat)).IsEqualApprox(1f, 0.001f);
            AssertThat(director.GetStemLevel(StemIntensity.Climax)).IsEqualApprox(1f, 0.001f);

            director.SetIntensity(StemIntensity.Ambient);
            director.AdvanceFades(0.5);
            AssertThat(director.GetStemLevel(StemIntensity.Combat)).IsEqualApprox(0f, 0.001f);
            AssertThat(director.GetStemLevel(StemIntensity.Climax)).IsEqualApprox(0f, 0.001f);
            AssertThat(director.GetStemLevel(StemIntensity.Ambient)).IsEqualApprox(1f, 0.001f);
            // Stems keep running through a fade to silence — that is what keeps them
            // phase-aligned for the next transition.
            AssertThat(director.IsStemPlaying(StemIntensity.Climax)).IsTrue();
        } finally {
            Teardown(director, host);
        }
    }

    [TestCase]
    public void ReleasingStopsEveryStemAndUnbindsTheSet() {
        StemDirector director = BuildDirector(out Node host);
        try {
            director.RegisterStageAudio(BuildSet(crossfadeSeconds: 1f));
            director.SetIntensity(StemIntensity.Combat);

            director.ReleaseStageAudio();

            AssertThat(director.IsActive).IsFalse();
            AssertObject(director.ActiveSet).IsNull();
            AssertThat(director.CurrentIntensity).IsEqual(StemIntensity.Ambient);
            foreach (StemIntensity stem in new[] { StemIntensity.Ambient, StemIntensity.Combat, StemIntensity.Climax }) {
                AssertThat(director.IsStemPlaying(stem)).IsFalse();
                AssertObject(director.GetStemStream(stem)).IsNull();
            }
            // Releasing twice is a no-op, not a crash: scenes release on unload and
            // a level that also releases on completion must not double-fault.
            director.ReleaseStageAudio();
            AssertThat(director.IsActive).IsFalse();
        } finally {
            Teardown(director, host);
        }
    }

    [TestCase]
    public void RegisteringASecondSetReplacesTheFirst() {
        StemDirector director = BuildDirector(out Node host);
        try {
            StageAudioSet first = BuildSet(crossfadeSeconds: 1f);
            StageAudioSet second = BuildSet(crossfadeSeconds: 3f);
            director.RegisterStageAudio(first);
            director.SetIntensity(StemIntensity.Climax);

            director.RegisterStageAudio(second);

            AssertObject(director.ActiveSet).IsSame(second);
            // A new scene starts at its ambient bed, never inheriting the last
            // scene's boss layer.
            AssertThat(director.CurrentIntensity).IsEqual(StemIntensity.Ambient);
            AssertThat(director.GetStemLevel(StemIntensity.Climax)).IsEqualApprox(0f, 0.001f);
        } finally {
            Teardown(director, host);
        }
    }

    /// <summary>
    /// Stems that are not bar-matched cannot be mixed, so the director hard-switches
    /// instead. Authored placeholder sets are all synchronized, but the flag exists
    /// on the resource and must mean something.
    /// </summary>
    [TestCase]
    public void UnsynchronizedStemsHardSwitchInsteadOfCrossfading() {
        StemDirector director = BuildDirector(out Node host);
        try {
            StageAudioSet set = BuildSet(crossfadeSeconds: 2f);
            set.StemsAreSynchronized = false;
            director.RegisterStageAudio(set);

            AssertThat(director.IsStemPlaying(StemIntensity.Combat)).IsFalse();

            director.SetIntensity(StemIntensity.Combat);
            AssertThat(director.GetStemLevel(StemIntensity.Combat)).IsEqualApprox(1f, 0.001f);
            AssertThat(director.IsStemPlaying(StemIntensity.Combat)).IsTrue();
            AssertThat(director.IsCrossfadeComplete()).IsTrue();
        } finally {
            Teardown(director, host);
        }
    }

    [TestCase]
    public void SettingIntensityWithNoRegisteredSetIsRememberedNotLost() {
        StemDirector director = BuildDirector(out Node host);
        try {
            director.SetIntensity(StemIntensity.Combat);
            AssertThat(director.CurrentIntensity).IsEqual(StemIntensity.Combat);
            AssertThat(director.IsActive).IsFalse();
            // Advancing with nothing registered must not throw.
            director.AdvanceFades(0.5);
        } finally {
            Teardown(director, host);
        }
    }

    private static StemDirector BuildDirector(out Node host) {
        host = new Node { Name = "StemDirectorHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var director = new StemDirector { Name = "StemDirector" };
        host.AddChild(director);
        return director;
    }

    private static void Teardown(StemDirector director, Node host) {
        director?.ReleaseStageAudio();
        host?.QueueFree();
    }

    private static StageAudioSet BuildSet(float crossfadeSeconds) => new() {
        SetID = "audio_a2_probe",
        StageID = "florence_workshop",
        CrossfadeSeconds = crossfadeSeconds,
        AmbientStem = ResourceLoader.Load<AudioStreamWav>(AmbientStemPath),
        CombatStem = ResourceLoader.Load<AudioStreamWav>(CombatStemPath),
        ClimaxStem = ResourceLoader.Load<AudioStreamWav>(ClimaxStemPath)
    };
}
