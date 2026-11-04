using Godot;

namespace FTT.Core {

    /// <summary>
    /// One authored core-mechanic cue (H04, design-godot.md Section 8 "7. Core
    /// Mechanic Cues"; Package 12 W10). A row of
    /// <see cref="CoreMechanicCueCatalog"/>: a stable ID, the bus it plays on,
    /// and whether it is <b>information-bearing</b> — a Critical cue, which
    /// always routes through <c>AudioManager.PlayCriticalCue</c> so it survives
    /// background ducking and filtering (C01b).
    ///
    /// <para>The stream is a silent placeholder under the 2026-08-10 directive,
    /// except the eight combat cues in <see cref="CoreMechanicCueIDs.AudiblePlaceholders"/>,
    /// which GR-AUDIO (gameplay review ruling 13, 2026-11-03) made short, dry,
    /// quiet and separately mutable. Package 10 replaces them by reassignment
    /// with no code change.</para>
    /// </summary>
    [GlobalClass]
    public partial class CoreMechanicCueEntry : Resource {
        /// <summary>Stable identity; one of <see cref="CoreMechanicCueIDs"/>.</summary>
        [Export] public string CueID = "";

        /// <summary>
        /// Bus a non-critical cue plays on (one of <see cref="AudioBuses.All"/>).
        /// A critical cue ignores this and plays on <see cref="AudioBuses.CriticalCues"/>.
        /// </summary>
        [Export] public string Bus = AudioBuses.SFX;

        /// <summary>True for the design's "Critical Cues path" cues.</summary>
        [Export] public bool Critical;

        /// <summary>The placeholder stream (digital silence until Package 10).</summary>
        [Export] public AudioStream Stream;

        /// <summary>
        /// Playback level in dB, added to the voice (GR-AUDIO, additive: a row that
        /// does not author it plays at 0). The audible placeholders are authored at
        /// -12 so they sit under the mix; a stream that is digital silence ignores it.
        /// </summary>
        [Export] public float VolumeDb;

        /// <summary>
        /// Per-cue throttle (GR-AUDIO, additive: 0 = unthrottled). A second request
        /// for this row inside this many seconds of the last one that played is
        /// dropped, so a crowd, a swarm wave or a hitstop cluster cannot machine-gun
        /// the cue. Measured on <see cref="CoreMechanicCuePresenter"/>'s own clock,
        /// which a paused tree holds.
        /// </summary>
        [Export] public float MinIntervalSeconds;

        /// <summary>One line of the GDD's description, for the audio team. Not shown to players.</summary>
        [Export(PropertyHint.MultilineText)] public string DesignNote = "";

        /// <summary>The bus this cue actually plays on.</summary>
        public string ResolvedBus => Critical ? AudioBuses.CriticalCues : Bus;
    }
}
