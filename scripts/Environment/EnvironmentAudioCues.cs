using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// The Story world's shared one-shot cues — hazards, pickups, destructible
    /// environment objects (Package 8 B5).
    ///
    /// <para><b>Why this exists.</b> <c>AudioManager</c> already loads the placeholder
    /// hazard and pickup streams, but keeps them private behind the enemy-presentation
    /// resolver; the only public playback entry point that takes an arbitrary world
    /// sound is <c>PlaySFX(stream)</c>, which needs the caller to hold the stream. B5
    /// must not refactor A2's file, so the two streams are resolved once here and the
    /// call sites stay one-liners. If A2's surface later grows a
    /// <c>PlayEnvironmentCue(id)</c>, this class collapses into it without touching a
    /// single call site.</para>
    ///
    /// <para>Streams are loaded through <see cref="ResourceLoader"/>, not
    /// <c>AuthoredResources</c>: audio is streamed content and must stay out of the
    /// authored-data cache. The references are held for the process lifetime, so
    /// there is no reload churn and nothing is ever disposed.</para>
    /// </summary>
    public static class EnvironmentAudioCues {
        public const string HazardCuePath = "res://resources/Audio/placeholder_sfx_hazard.tres";
        public const string PickupCuePath = "res://resources/Audio/placeholder_sfx_pickup.tres";

        /// <summary>A telegraph is quieter than the hit it announces.</summary>
        public const float WarningVolumeDb = -8f;

        /// <summary>A warning reads as a lower, slower version of the same cue.</summary>
        public const float WarningPitch = 0.82f;

        private static AudioStream _hazard;
        private static AudioStream _pickup;
        private static bool _loaded;

        public static AudioStream Hazard { get { EnsureLoaded(); return _hazard; } }

        public static AudioStream Pickup { get { EnsureLoaded(); return _pickup; } }

        /// <summary>A hazard entering its telegraph window.</summary>
        public static void PlayHazardWarning() =>
            AudioManager.Instance?.PlayOneShot(Hazard, AudioBuses.Environmental, WarningPitch, WarningVolumeDb);

        /// <summary>A hazard going live.</summary>
        public static void PlayHazardActive() =>
            AudioManager.Instance?.PlayOneShot(Hazard, AudioBuses.Environmental, 1f, 0f);

        /// <summary>A destructible environment object breaking.</summary>
        public static void PlayDestruction() =>
            AudioManager.Instance?.PlayOneShot(Hazard, AudioBuses.Environmental, 0.7f, 0f);

        /// <summary>Any Story loot or dust the player picks up.</summary>
        public static void PlayPickup(float pitchScale = 1f) =>
            AudioManager.Instance?.PlayOneShot(Pickup, AudioBuses.SFX, pitchScale, 0f);

        private static void EnsureLoaded() {
            if (_loaded) return;
            _loaded = true;
            _hazard = Load(HazardCuePath);
            _pickup = Load(PickupCuePath);
        }

        private static AudioStream Load(string path) =>
            ResourceLoader.Exists(path) ? ResourceLoader.Load<AudioStream>(path) : null;
    }
}
