using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A2. The pooled one-shot voice allocator — the only voice-stealing in
/// the project — and the playback entry points Package 8 B3/B5 wire consumers to.
///
/// <para>The pool has always been there and has never been tested. Its steal-oldest
/// policy is a deliberate choice: with a hard cap of
/// <see cref="AudioManager.SfxPoolCapacity"/> voices, dropping the *newest* request
/// would mute exactly the hit the player just landed, so the oldest sound — the one
/// already furthest through its envelope — is the one that goes.</para>
///
/// <para>These tests run against the live autoload because the pool is its state.
/// They allocate synchronously within one frame, so the deferred <c>Finished</c>
/// signals cannot recycle a voice mid-assertion.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AudioVoicePoolTests {
    private const string HitStreamPath = "res://resources/Audio/placeholder_sfx_hit.tres";

    [TestCase]
    public void APristinePoolOffersEveryVoice() {
        AudioManager audio = Audio();
        try {
            AssertThat(AudioManager.SfxPoolCapacity).IsEqual(24);
            AssertThat(audio.IdleVoiceCount + audio.ActiveVoiceCount).IsEqual(AudioManager.SfxPoolCapacity);
        } finally {
            Reset(audio);
        }
    }

    [TestCase]
    public void AllocatingUpToCapacityConsumesTheIdleVoicesExactlyOnce() {
        AudioManager audio = Audio();
        try {
            AudioStream stream = Stream();
            int available = audio.IdleVoiceCount;
            for (int index = 0; index < available; index++) {
                audio.PlayOneShot(stream, AudioBuses.SFX);
            }
            AssertThat(audio.IdleVoiceCount).IsEqual(0);
            AssertThat(audio.ActiveVoiceCount).IsEqual(AudioManager.SfxPoolCapacity);
        } finally {
            Reset(audio);
        }
    }

    [TestCase]
    public void ExhaustingThePoolStealsTheOldestVoiceRatherThanGrowingOrDropping() {
        AudioManager audio = Audio();
        try {
            AudioStream stream = Stream();
            for (int index = 0; index < AudioManager.SfxPoolCapacity; index++) {
                audio.PlayOneShot(stream, AudioBuses.SFX);
            }
            AudioStreamPlayer victim = audio.OldestActiveVoice;
            AssertObject(victim).IsNotNull();

            audio.PlayOneShot(stream, AudioBuses.Combat);

            // The cap holds, the pool does not grow, and the stolen voice is the
            // one that was reused — now carrying the new request's bus.
            AssertThat(audio.ActiveVoiceCount).IsEqual(AudioManager.SfxPoolCapacity);
            AssertThat(audio.IdleVoiceCount).IsEqual(0);
            AssertThat(victim.Bus).IsEqual(AudioBuses.Combat);
            AssertObject(audio.OldestActiveVoice).IsNotSame(victim);
        } finally {
            Reset(audio);
        }
    }

    [TestCase]
    public void OldestActiveVoiceIsNullWhileAnyVoiceIsStillIdle() {
        AudioManager audio = Audio();
        try {
            audio.PlayOneShot(Stream(), AudioBuses.SFX);
            // Nothing would be stolen yet, so there is no victim to report.
            AssertObject(audio.OldestActiveVoice).IsNull();
        } finally {
            Reset(audio);
        }
    }

    [TestCase]
    public void ANullStreamNeverConsumesAVoice() {
        AudioManager audio = Audio();
        try {
            int before = audio.IdleVoiceCount;
            audio.PlayOneShot(null, AudioBuses.SFX);
            audio.PlaySFX(null);
            AssertThat(audio.IdleVoiceCount).IsEqual(before);
            AssertThat(audio.ActiveVoiceCount).IsEqual(AudioManager.SfxPoolCapacity - before);
        } finally {
            Reset(audio);
        }
    }

    /// <summary>
    /// The entry points exist so B3/B5 never have to know a bus name. Each must land
    /// on its documented sub-bus — a footstep on Combat would make the combat
    /// snapshot duck the player's own movement.
    /// </summary>
    [TestCase]
    public void EachPlaybackEntryPointRoutesToItsDocumentedBus() {
        AudioManager audio = Audio();
        try {
            audio.PlayChirp(1.2f);
            AudioStreamPlayer chirp = LastAllocated(audio);
            AssertObject(chirp).IsNotNull();
            AssertThat(chirp.Bus).IsEqual(AudioBuses.UI);
            AssertThat(chirp.PitchScale).IsEqualApprox(1.2f, 0.001f);

            audio.PlayFootstep("stone");
            AudioStreamPlayer footstep = LastAllocated(audio);
            AssertThat(footstep.Bus).IsEqual(AudioBuses.Movement);
            AssertThat(footstep.PitchScale).IsEqualApprox(AudioManager.SurfacePitch("stone"), 0.001f);

            audio.PlayUISound();
            AssertThat(LastAllocated(audio).Bus).IsEqual(AudioBuses.UI);

            audio.PlaySFX(Stream());
            AssertThat(LastAllocated(audio).Bus).IsEqual(AudioBuses.SFX);

            audio.PlayCountdownBlip(1.5f);
            AudioStreamPlayer blip = LastAllocated(audio);
            AssertThat(blip.Bus).IsEqual(AudioBuses.UI);
            AssertThat(blip.PitchScale).IsEqualApprox(1.5f, 0.001f);
        } finally {
            Reset(audio);
        }
    }

    /// <summary>
    /// Placeholder surfaces are told apart by pitch until B5 registers real streams;
    /// an unknown surface must still make a sound rather than falling silent.
    /// </summary>
    [TestCase]
    public void UnknownSurfacesFallBackToTheGenericFootstep() {
        AudioManager audio = Audio();
        try {
            AssertThat(AudioManager.SurfacePitch("wood")).IsEqual(1.0f);
            AssertThat(AudioManager.SurfacePitch("not_a_surface")).IsEqual(1.0f);
            AssertThat(AudioManager.SurfacePitch("snow") < 1.0f).IsTrue();
            AssertThat(AudioManager.SurfacePitch("metal") > 1.0f).IsTrue();

            int before = audio.IdleVoiceCount;
            audio.PlayFootstep("not_a_surface");
            AssertThat(audio.IdleVoiceCount).IsEqual(before - 1);
        } finally {
            Reset(audio);
        }
    }

    [TestCase]
    public void ARegisteredFootstepStreamOverridesThePlaceholder() {
        AudioManager audio = Audio();
        try {
            AudioStream custom = Stream();
            audio.RegisterFootstepSound("gantry", custom);
            audio.PlayFootstep("gantry");
            AssertObject(LastAllocated(audio).Stream).IsSame(custom);
        } finally {
            Reset(audio);
        }
    }

    [TestCase]
    public void PitchIsClampedToASaneRange() {
        AudioManager audio = Audio();
        try {
            audio.PlayChirp(99f);
            AssertThat(LastAllocated(audio).PitchScale <= 4f).IsTrue();
            audio.PlayChirp(0f);
            AssertThat(LastAllocated(audio).PitchScale >= 0.25f).IsTrue();
        } finally {
            Reset(audio);
        }
    }

    private static AudioManager Audio() {
        AudioManager audio = AudioManager.Instance;
        AssertObject(audio)
            .OverrideFailureMessage("the AudioManager autoload is not running")
            .IsNotNull();
        Reset(audio);
        return audio;
    }

    /// <summary>Returns every voice to the pool so tests cannot leak allocations into each other.</summary>
    private static void Reset(AudioManager audio) => audio?.ReleaseAllVoices();

    /// <summary>The most recently allocated voice — the one the last call took.</summary>
    private static AudioStreamPlayer LastAllocated(AudioManager audio) => audio.NewestActiveVoice;

    private static AudioStream Stream() => ResourceLoader.Load<AudioStream>(HitStreamPath);
}
