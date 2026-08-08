using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Integration;

/// <summary>
/// Package 8 B5. The seam between a campaign scene and the A2 stem director:
/// registration on enter, release on exit, and the layer moves at the flow points a
/// level actually reaches.
///
/// <para>These run against the live <c>AudioManager</c> autoload, because the whole
/// point of the director is what it does to that shared state — a test against a
/// private <see cref="StemDirector"/> would prove the model and nothing about the
/// wiring. Every case therefore releases the stage audio in a <c>finally</c>: a
/// leaked registration would leave the next suite's scenes playing this suite's
/// music, which is exactly the production bug the release path exists to prevent.
/// </para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryAudioDirectorTests {
    private const string OrleansSetPath = "res://resources/Audio/level_02_audio.tres";

    [TestCase]
    public void EnteringASceneRegistersItsSetAndStartsOnTheAmbientBed() {
        AudioManager audio = Audio();
        var host = new Node { Name = "AudioDirectorHost" };
        AddToTree(host);
        try {
            StoryAudioDirector director = Attach(host, OrleansSetPath);

            AssertObject(director.ActiveSet).IsNotNull();
            AssertThat(director.ActiveSet.SetID).IsEqual("audio_level_02");
            AssertThat(audio.Stems.IsActive).IsTrue();
            AssertThat(audio.Stems.ActiveSet.SetID).IsEqual("audio_level_02");
            AssertThat(director.PublishedIntensity).IsEqual(StemIntensity.Ambient);
            AssertThat(audio.Stems.CurrentIntensity).IsEqual(StemIntensity.Ambient);
        } finally {
            Cleanup(host, audio);
        }
    }

    /// <summary>
    /// The release is unconditional on the way out. Without it the level the player
    /// just left keeps playing under the hub.
    /// </summary>
    [TestCase]
    public void LeavingTheSceneReleasesTheStageAudio() {
        AudioManager audio = Audio();
        var host = new Node { Name = "AudioDirectorTeardownHost" };
        AddToTree(host);
        try {
            Attach(host, OrleansSetPath);
            AssertThat(audio.Stems.IsActive).IsTrue();

            host.GetChild(0).Free();
            AssertThat(audio.Stems.IsActive).IsFalse();
            AssertObject(audio.Stems.ActiveSet).IsNull();
        } finally {
            Cleanup(host, audio);
        }
    }

    [TestCase]
    public void ABossEncounterRaisesTheClimaxLayerAndItsDefeatFallsBack() {
        AudioManager audio = Audio();
        var host = new Node { Name = "AudioDirectorBossHost" };
        AddToTree(host);
        try {
            StoryAudioDirector director = Attach(host, OrleansSetPath);

            director.SetBossEngaged(true);
            AssertThat(director.PublishedIntensity).IsEqual(StemIntensity.Climax);
            AssertThat(audio.Stems.CurrentIntensity).IsEqual(StemIntensity.Climax);

            // A defeated boss settles onto combat for the hold, not into silence.
            director.SetBossEngaged(false);
            AssertThat(director.PublishedIntensity).IsEqual(StemIntensity.Combat);
        } finally {
            Cleanup(host, audio);
        }
    }

    [TestCase]
    public void LevelCompletionSettlesBackOntoAmbientWithoutUnregisteringTheSet() {
        AudioManager audio = Audio();
        var host = new Node { Name = "AudioDirectorCompletionHost" };
        AddToTree(host);
        try {
            StoryAudioDirector director = Attach(host, OrleansSetPath);
            director.SetBossEngaged(true);
            AssertThat(director.PublishedIntensity).IsEqual(StemIntensity.Climax);

            director.ReleaseToAmbient();
            AssertThat(director.PublishedIntensity).IsEqual(StemIntensity.Ambient);
            AssertThat(director.Intensity.BossEngaged).IsFalse();
            // The results overlay plays over music, not over silence.
            AssertThat(audio.Stems.IsActive).IsTrue();
        } finally {
            Cleanup(host, audio);
        }
    }

    /// <summary>
    /// A scene with no authored set must leave whatever is registered alone rather
    /// than clearing it, so a test fixture or an unmusicked scene cannot stomp the
    /// running game's music.
    /// </summary>
    [TestCase]
    public void ADirectorWithNoSetRegistersNothing() {
        AudioManager audio = Audio();
        var host = new Node { Name = "AudioDirectorSilentHost" };
        AddToTree(host);
        try {
            StoryAudioDirector director = Attach(host, "");

            AssertObject(director.ActiveSet).IsNull();
            AssertThat(audio.Stems.IsActive).IsFalse();
        } finally {
            Cleanup(host, audio);
        }
    }

    /// <summary>
    /// The bootstrapper is what every campaign scene actually calls, so the
    /// "audio set path attaches a director" contract is pinned there too — the
    /// levels never construct one themselves.
    /// </summary>
    [TestCase]
    public void TheBootstrapperAttachesADirectorOnlyWhenAPathIsSupplied() {
        AudioManager audio = Audio();
        var withAudio = new Node { Name = "BootstrapWithAudio" };
        var withoutAudio = new Node { Name = "BootstrapWithoutAudio" };
        AddToTree(withAudio);
        AddToTree(withoutAudio);
        try {
            StorySceneServices silent = StorySceneBootstrapper.Attach(
                withoutAudio, "", includeHUD: false, includeRewind: false);
            AssertObject(silent.Audio).IsNull();

            StorySceneServices musical = StorySceneBootstrapper.Attach(
                withAudio, "", includeHUD: false, includeRewind: false, audioSetPath: AudioSetPaths.Hub);
            AssertObject(musical.Audio).IsNotNull();
            AssertThat(musical.Audio.ActiveSet.SetID).IsEqual("audio_hub");
            AssertThat(audio.Stems.IsActive).IsTrue();
        } finally {
            audio?.ReleaseStageAudio();
            withAudio.QueueFree();
            withoutAudio.QueueFree();
        }
    }

    private static StoryAudioDirector Attach(Node host, string setPath) {
        var director = new StoryAudioDirector {
            Name = "StoryAudioDirector",
            AudioSetPath = setPath
        };
        host.AddChild(director);
        return director;
    }

    private static AudioManager Audio() {
        AudioManager audio = AudioManager.Instance;
        AssertObject(audio)
            .OverrideFailureMessage("the AudioManager autoload is not running")
            .IsNotNull();
        audio.ReleaseStageAudio();
        return audio;
    }

    private static void Cleanup(Node host, AudioManager audio) {
        host?.QueueFree();
        audio?.ReleaseStageAudio();
    }

    private static void AddToTree(Node node) {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(node);
    }
}
