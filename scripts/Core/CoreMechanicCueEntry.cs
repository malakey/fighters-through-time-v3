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
    /// <para>The stream is a silent placeholder under the 2026-08-10 directive;
    /// Package 10 replaces it by reassignment with no code change.</para>
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

        /// <summary>One line of the GDD's description, for the audio team. Not shown to players.</summary>
        [Export(PropertyHint.MultilineText)] public string DesignNote = "";

        /// <summary>The bus this cue actually plays on.</summary>
        public string ResolvedBus => Critical ? AudioBuses.CriticalCues : Bus;
    }
}
