using System.Collections.Generic;
using Godot;

namespace FTT.Core {

    /// <summary>
    /// Stable IDs for every core-mechanic cue the GDD's Section 8 "7. Core
    /// Mechanic Cues" block names (H04; Package 12 W10). The catalog resource is
    /// the one place a stream and its routing live; code refers to cues only by
    /// these IDs.
    ///
    /// <para>The ten Fighter stage-hazard warning/impact pairs are <b>not</b> rows
    /// here: they are per-stage slots on <c>StageAudioSet</c>
    /// (<c>HazardWarningCue</c> / <c>HazardImpactCue</c>), always on the Critical
    /// path. <see cref="StageHazardWarningFallback"/> / <see cref="StageHazardImpactFallback"/>
    /// cover a stage whose set leaves a slot empty.</para>
    ///
    /// <para>Victory fanfares follow the roster-growth rule: one shared
    /// <see cref="VictoryFanfare"/> row plus an optional per-hero override row
    /// named <c>victory_fanfare_&lt;heroID&gt;</c>, resolved by convention —
    /// the cast is never enumerated here.</para>
    /// </summary>
    public static class CoreMechanicCueIDs {
        public const string TimeFreezeActivate = "time_freeze_activate";
        public const string TimeFreezeDrone = "time_freeze_drone";
        public const string TimeFreezeThawWarning = "time_freeze_thaw_warning";
        public const string TimeFreezeThawRelease = "time_freeze_thaw_release";
        /// <summary>R03: reuses the Time Freeze thaw warning at the end of the Post-Landing Hold.</summary>
        public const string PostLandingThawWarning = "post_landing_thaw_warning";
        /// <summary>MM-FX (P29): the Defy burst; replaces the retired toll and seal-shatter cues.</summary>
        public const string DefyBurst = "defy_burst";
        public const string RallyReclaim = "rally_reclaim";
        public const string GrabCatch = "grab_catch";
        public const string GrabWhiff = "grab_whiff";
        public const string ThrowHeave = "throw_heave";
        public const string ThrowImpact = "throw_impact";
        public const string LandingTech = "landing_tech";
        public const string KnockdownThud = "knockdown_thud";
        public const string IntegrityTick = "integrity_tick";
        public const string MatchTimerPip = "match_timer_pip";
        public const string MatchTimerPipFinal = "match_timer_pip_final";
        public const string UltimateReady = "ultimate_ready";
        /// <summary>CM-ULT (decision 2.1): the A02 activation strike's 20-frame wind-up, once on its first frame.</summary>
        public const string UltimateActivationWindup = "ultimate_activation_windup";
        /// <summary>GR-AUDIO (ruling 13): a landed direct hit, the dry thump.</summary>
        public const string HitBasic = "hit_basic";
        /// <summary>GR-AUDIO (ruling 13): an ordinary block or a guard break.</summary>
        public const string Block = "block";
        /// <summary>GR-AUDIO (ruling 13): a perfect block, a brighter ping.</summary>
        public const string PerfectBlock = "perfect_block";
        /// <summary>GR-AUDIO (ruling 13): a swing whose active window closed with no contact.</summary>
        public const string SwingWhiff = "swing_whiff";
        public const string KoStinger = "ko_stinger";
        public const string KoStingerDecisive = "ko_stinger_decisive";
        public const string VictoryFanfare = "victory_fanfare";
        public const string DrawSting = "draw_sting";
        public const string SealCharge = "seal_charge";
        public const string SealLock = "seal_lock";
        public const string StageHazardWarningFallback = "stage_hazard_warning";
        public const string StageHazardImpactFallback = "stage_hazard_impact";

        /// <summary>Per-hero fanfare override row ID; falls back to <see cref="VictoryFanfare"/>.</summary>
        public static string VictoryFanfareFor(string heroID) =>
            string.IsNullOrEmpty(heroID) ? VictoryFanfare : $"{VictoryFanfare}_{heroID}";

        /// <summary>Every row the catalog must carry.</summary>
        public static readonly string[] All = {
            TimeFreezeActivate, TimeFreezeDrone, TimeFreezeThawWarning, TimeFreezeThawRelease,
            PostLandingThawWarning, DefyBurst,
            RallyReclaim,
            GrabCatch, GrabWhiff, ThrowHeave, ThrowImpact, LandingTech, KnockdownThud,
            IntegrityTick, MatchTimerPip, MatchTimerPipFinal, UltimateReady, UltimateActivationWindup,
            KoStinger, KoStingerDecisive, VictoryFanfare, DrawSting, SealCharge, SealLock,
            StageHazardWarningFallback, StageHazardImpactFallback,
            HitBasic, Block, PerfectBlock, SwingWhiff
        };

        /// <summary>
        /// GR-AUDIO (gameplay review ruling 13, 2026-11-03): the combat cues whose placeholder
        /// streams are audible: the eight of the ruling plus <see cref="KoStingerDecisive"/>, the match-ending
        /// variant that shares the KO stinger's asset and replaces it on the final stock. Every other stream stays the digital silence of the 2026-08-10
        /// directive. Only these obey <c>GlobalSaveData.PlaceholderCuesMuted</c>
        /// (<see cref="PlaceholderCueSettings"/>), and the content test pins each one's stream to an
        /// audible asset in <c>audio/sfx/placeholder_cue_manifest.json</c> and every other row to a silent one.
        /// </summary>
        public static readonly string[] AudiblePlaceholders = {
            HitBasic, Block, PerfectBlock, SwingWhiff,
            GrabCatch, KoStinger, KoStingerDecisive, DefyBurst, UltimateActivationWindup
        };

        /// <summary>True for one of <see cref="AudiblePlaceholders"/>.</summary>
        public static bool IsAudiblePlaceholder(string cueID) =>
            System.Array.IndexOf(AudiblePlaceholders, cueID) >= 0;

        /// <summary>
        /// The rows the GDD marks "Critical Cues path": Time Freeze (all of it),
        /// the Post-Landing thaw, Defy, the Integrity tick, the 00:10 pips, the KO
        /// stinger and the stage hazards — plus (CM-ULT, decision 2.1) the A02
        /// activation wind-up, which warns of an unblockable strike.
        /// </summary>
        public static readonly string[] Critical = {
            TimeFreezeActivate, TimeFreezeDrone, TimeFreezeThawWarning, TimeFreezeThawRelease,
            PostLandingThawWarning, DefyBurst,
            IntegrityTick, MatchTimerPip, MatchTimerPipFinal,
            UltimateActivationWindup,
            KoStinger, KoStingerDecisive,
            StageHazardWarningFallback, StageHazardImpactFallback
        };
    }

    /// <summary>
    /// H04's single canonical cue catalog (<c>resources/Audio/core_mechanic_cues.tres</c>).
    /// Loaded through <see cref="AuthoredResources"/> like other immutable tuning
    /// data; the streams it references are placeholder silence.
    /// </summary>
    [GlobalClass]
    public partial class CoreMechanicCueCatalog : Resource {
        public const string DefaultPath = "res://resources/Audio/core_mechanic_cues.tres";

        [Export] public int SchemaVersion = 1;

        [Export] public Godot.Collections.Array<CoreMechanicCueEntry> Entries = new();

        private Dictionary<string, CoreMechanicCueEntry> _byID;

        /// <summary>The row with <paramref name="cueID"/>, or null.</summary>
        public CoreMechanicCueEntry Find(string cueID) {
            if (string.IsNullOrEmpty(cueID)) return null;
            if (_byID == null) {
                _byID = new Dictionary<string, CoreMechanicCueEntry>();
                foreach (CoreMechanicCueEntry entry in Entries) {
                    if (entry != null && !string.IsNullOrEmpty(entry.CueID)) _byID[entry.CueID] = entry;
                }
            }
            return _byID.TryGetValue(cueID, out CoreMechanicCueEntry found) ? found : null;
        }

        /// <summary>The per-hero fanfare row if authored, else the shared one.</summary>
        public CoreMechanicCueEntry VictoryFanfareFor(string heroID) =>
            Find(CoreMechanicCueIDs.VictoryFanfareFor(heroID)) ?? Find(CoreMechanicCueIDs.VictoryFanfare);

        public static CoreMechanicCueCatalog LoadDefault() =>
            ResourceLoader.Exists(DefaultPath)
                ? AuthoredResources.Load<CoreMechanicCueCatalog>(DefaultPath)
                : null;
    }
}
