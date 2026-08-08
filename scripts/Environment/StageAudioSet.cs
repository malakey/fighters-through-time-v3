using Godot;

namespace FTT.Environment {

    /// <summary>
    /// The per-stage Fighter Mode music set: the three synchronized stems a stage
    /// layers during a match, plus the crossfade metadata <c>AudioManager</c> needs
    /// to move between them without a seam.
    ///
    /// <para>Stems are designed to be played together and mixed rather than
    /// restarted, so all three must be loopable and share one loop length — that is
    /// what keeps ambient/combat/climax phase-aligned when the mix changes
    /// mid-match. <c>StageAudioSetTests</c> pins the contract; the ten authored sets
    /// under <c>resources/Audio/</c> are the manifest's <c>audio_stage_*</c> rows.</para>
    ///
    /// <para>This is placeholder-grade content by design: production stems replace
    /// the assigned <see cref="AudioStream"/>s by reassignment, with no code or
    /// schema change.</para>
    /// </summary>
    [GlobalClass]
    public partial class StageAudioSet : Resource {
        [Export] public int SchemaVersion = 1;

        /// <summary>Stable identity; must match the manifest AudioSet row id.</summary>
        [Export] public string SetID = "";

        /// <summary>Stage this set belongs to; must match a catalog <c>StageID</c>.</summary>
        [Export] public string StageID = "";

        /// <summary>Neutral-play bed; always audible while the match is live.</summary>
        [ExportGroup("Stems")]
        [Export] public AudioStream AmbientStem;

        /// <summary>Layered in while the fighters are engaged.</summary>
        [Export] public AudioStream CombatStem;

        /// <summary>Layered in for the last-stock / final-seconds climax.</summary>
        [Export] public AudioStream ClimaxStem;

        /// <summary>Seconds to fade a stem in or out. Must be greater than zero.</summary>
        [ExportGroup("Crossfade")]
        [Export(PropertyHint.Range, "0.05,10.0,0.05")] public float CrossfadeSeconds = 2.0f;

        /// <summary>
        /// True when the stems are bar-aligned and must be started together and
        /// only mixed, never restarted. The authored placeholder kit satisfies this.
        /// </summary>
        [Export] public bool StemsAreSynchronized = true;

        /// <summary>Shared loop length in seconds; used to schedule mix changes on a boundary.</summary>
        [Export(PropertyHint.Range, "0.1,600.0,0.1")] public float LoopSeconds = 1.0f;

        /// <summary>The three stems in mix order (ambient, combat, climax).</summary>
        public AudioStream[] Stems() => new[] { AmbientStem, CombatStem, ClimaxStem };
    }
}
