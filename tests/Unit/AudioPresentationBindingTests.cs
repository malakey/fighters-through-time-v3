using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A2. The <c>OnEnemyPresentation</c> → SFX binder.
///
/// <para>The 76 authored <c>EnemyAbilityData</c> kits already emit
/// <c>PresentationEventID</c> strings (<c>boss.jackal_priest.khopesh_sweep</c> and
/// friends) from three raise sites, and until now nothing anywhere subscribed to
/// them. The binder gives those strings a destination without requiring 42 bespoke
/// sound maps: a phase-suffixed key wins, a bare event ID matches every phase, and
/// otherwise a generic per-phase placeholder plays. Package 8 B6 registers the real
/// content against the same keys.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AudioPresentationBindingTests {
    private const string EventID = "boss.jackal_priest.khopesh_sweep";
    private const string OverridePath = "res://resources/Audio/placeholder_sfx_pickup.tres";

    [TestCase]
    public void ThePhaseSuffixConventionCoversEveryPhase() {
        AssertThat(AudioManager.PhaseSuffix(EnemyPresentationPhase.Telegraph)).IsEqual(".telegraph");
        AssertThat(AudioManager.PhaseSuffix(EnemyPresentationPhase.Active)).IsEqual(".active");
        AssertThat(AudioManager.PhaseSuffix(EnemyPresentationPhase.Recovery)).IsEqual(".recovery");
        AssertThat(AudioManager.PhaseSuffix(EnemyPresentationPhase.Death)).IsEqual(".death");
    }

    /// <summary>
    /// Impact beats belong on Combat and anticipation beats on Environmental, so a
    /// later level-specific duck can lower one without flattening the other.
    /// </summary>
    [TestCase]
    public void ImpactPhasesRouteToCombatAndAnticipationPhasesToEnvironmental() {
        AssertThat(AudioManager.PhaseBus(EnemyPresentationPhase.Active)).IsEqual(AudioBuses.Combat);
        AssertThat(AudioManager.PhaseBus(EnemyPresentationPhase.Death)).IsEqual(AudioBuses.Combat);
        AssertThat(AudioManager.PhaseBus(EnemyPresentationPhase.Telegraph)).IsEqual(AudioBuses.Environmental);
        AssertThat(AudioManager.PhaseBus(EnemyPresentationPhase.Recovery)).IsEqual(AudioBuses.Environmental);
    }

    [TestCase]
    public void AnUnregisteredEventStillGetsAGenericPlaceholderForItsPhase() {
        AudioManager audio = Audio();
        try {
            AssertThat(audio.ResolvePresentationCue(
                Payload(EnemyPresentationPhase.Active), out AudioStream active, out string activeBus, out _)).IsTrue();
            AssertObject(active).IsNotNull();
            AssertThat(activeBus).IsEqual(AudioBuses.Combat);

            AssertThat(audio.ResolvePresentationCue(
                Payload(EnemyPresentationPhase.Telegraph), out AudioStream telegraph, out string telegraphBus, out _)).IsTrue();
            AssertObject(telegraph).IsNotNull();
            AssertThat(telegraphBus).IsEqual(AudioBuses.Environmental);
            // Telegraph and impact must not be the same sound, or the wind-up
            // carries no information.
            AssertObject(telegraph).IsNotSame(active);
        } finally {
            audio.ClearPresentationSounds();
        }
    }

    /// <summary>
    /// Recovery fires on every ability of every enemy several times a second. A
    /// generic sound there would be pure noise, so it stays silent unless an author
    /// deliberately registers one.
    /// </summary>
    [TestCase]
    public void RecoveryHasNoGenericSoundButAcceptsAnExplicitOne() {
        AudioManager audio = Audio();
        try {
            AssertThat(audio.ResolvePresentationCue(
                Payload(EnemyPresentationPhase.Recovery), out AudioStream none, out _, out _)).IsFalse();
            AssertObject(none).IsNull();

            AudioStream custom = Override();
            audio.RegisterPresentationSound(EventID + ".recovery", custom);
            AssertThat(audio.ResolvePresentationCue(
                Payload(EnemyPresentationPhase.Recovery), out AudioStream registered, out _, out _)).IsTrue();
            AssertObject(registered).IsSame(custom);
        } finally {
            audio.ClearPresentationSounds();
        }
    }

    [TestCase]
    public void APhaseSuffixedRegistrationBeatsABareEventRegistration() {
        AudioManager audio = Audio();
        try {
            AudioStream bare = ResourceLoader.Load<AudioStream>("res://resources/Audio/placeholder_sfx_ui.tres");
            AudioStream suffixed = Override();
            audio.RegisterPresentationSound(EventID, bare);
            audio.RegisterPresentationSound(EventID + ".active", suffixed);

            audio.ResolvePresentationCue(Payload(EnemyPresentationPhase.Active), out AudioStream resolved, out _, out _);
            AssertObject(resolved).IsSame(suffixed);

            // The bare registration still covers the phases nothing specific was
            // registered for.
            audio.ResolvePresentationCue(Payload(EnemyPresentationPhase.Telegraph), out AudioStream fallback, out _, out _);
            AssertObject(fallback).IsSame(bare);
        } finally {
            audio.ClearPresentationSounds();
        }
    }

    /// <summary>
    /// <c>EnemyController</c> builds its death event as <c>{enemyID}.death</c>, which
    /// already carries the phase suffix. Appending it again would produce
    /// <c>x.death.death</c> and miss every registration.
    /// </summary>
    [TestCase]
    public void AnEventIDThatAlreadyCarriesItsPhaseSuffixIsNotDoubleSuffixed() {
        AudioManager audio = Audio();
        try {
            AudioStream custom = Override();
            audio.RegisterPresentationSound("archive_drone.death", custom);

            var payload = new EnemyPresentationPayload {
                SourceID = "archive_drone",
                PresentationEventID = "archive_drone.death",
                Phase = EnemyPresentationPhase.Death
            };
            AssertThat(audio.ResolvePresentationCue(payload, out AudioStream resolved, out string bus, out float pitch)).IsTrue();
            AssertObject(resolved).IsSame(custom);
            AssertThat(bus).IsEqual(AudioBuses.Combat);
            // Deaths are pitched down so they read as heavier than a normal hit.
            AssertThat(pitch < 1f).IsTrue();
        } finally {
            audio.ClearPresentationSounds();
        }
    }

    [TestCase]
    public void AnEmptyEventIDStillResolvesToThePhasePlaceholder() {
        AudioManager audio = Audio();
        try {
            var payload = new EnemyPresentationPayload {
                SourceID = "",
                PresentationEventID = "",
                Phase = EnemyPresentationPhase.Active
            };
            AssertThat(audio.ResolvePresentationCue(payload, out AudioStream stream, out _, out _)).IsTrue();
            AssertObject(stream).IsNotNull();
        } finally {
            audio.ClearPresentationSounds();
        }
    }

    /// <summary>The binder is live: a raised event must actually consume a voice.</summary>
    [TestCase]
    public void ARaisedPresentationEventReachesTheVoicePool() {
        AudioManager audio = Audio();
        try {
            audio.ReleaseAllVoices();
            int before = audio.IdleVoiceCount;

            EventBus.Instance?.RaiseEnemyPresentation(Payload(EnemyPresentationPhase.Active));

            AssertThat(audio.IdleVoiceCount).IsEqual(before - 1);
            AssertThat(audio.NewestActiveVoice.Bus).IsEqual(AudioBuses.Combat);
        } finally {
            audio.ClearPresentationSounds();
            audio.ReleaseAllVoices();
        }
    }

    private static AudioManager Audio() {
        AudioManager audio = AudioManager.Instance;
        AssertObject(audio)
            .OverrideFailureMessage("the AudioManager autoload is not running")
            .IsNotNull();
        audio.ClearPresentationSounds();
        return audio;
    }

    private static AudioStream Override() => ResourceLoader.Load<AudioStream>(OverridePath);

    private static EnemyPresentationPayload Payload(EnemyPresentationPhase phase) => new() {
        SourceID = "jackal_priest",
        AbilityID = "khopesh_sweep",
        PresentationEventID = EventID,
        Phase = phase
    };
}
