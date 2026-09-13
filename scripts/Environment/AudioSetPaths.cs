namespace FTT.Environment {

    /// <summary>
    /// The one place that turns a scene's identity into its <see cref="StageAudioSet"/>
    /// path (Package 8 B5).
    ///
    /// <para>Neither the campaign nor the Fighter stage catalog carries an audio-set
    /// field, and adding one to <c>FighterStageData</c> would mean editing ten
    /// authored catalog rows plus the ten stage scenes that B7 owns. The authored
    /// file names are already perfectly regular against the manifest's reserved
    /// paths, so the mapping is derived here instead of duplicated seventeen (plus
    /// ten) times across controllers. A wrong derivation is silent — the scene simply
    /// plays no music — which is why
    /// <c>tests/ContentValidation/StoryAudioSetContentTests.cs</c> resolves every
    /// campaign id through this class and asserts the file exists.</para>
    ///
    /// <para>Nothing here loads a resource; these are strings only.</para>
    /// </summary>
    public static class AudioSetPaths {
        public const string Directory = "res://resources/Audio/";

        /// <summary>The Time-Ship hub's set (manifest row <c>audio_hub</c>).</summary>
        public const string Hub = Directory + "hub_audio.tres";

        /// <summary>Level 0's set is named for the tutorial, not for its index.</summary>
        public const string Tutorial = Directory + "tutorial_audio.tres";

        /// <summary>
        /// Level 4A's set (Package 11 A12), shared by all nine Legacy variants —
        /// P01 Option A reuses suitable era themes rather than commissioning nine
        /// tracks, and a variant that needs its own overrides <c>AudioSetPath</c>.
        /// Named separately rather than derived: <see cref="ForStoryLevel"/> reads a
        /// two-digit slot and 4A has none, so leaving that derivation untouched keeps
        /// every existing campaign mapping and its negative cases intact.
        /// </summary>
        public const string Legacy = Directory + "level_04a_audio.tres";

        /// <summary>Identity written into the 4A set's <c>StageID</c>.</summary>
        public const string LegacyStageID = "level_04a_legacy";

        /// <summary>Identity written into the hub set's <c>StageID</c>.</summary>
        public const string HubStageID = "hub_ship";

        /// <summary>
        /// Resolves a campaign level id (<c>level_02_orleans</c>) to its authored set.
        /// Level 0 maps to <see cref="Tutorial"/>; every other level maps to
        /// <c>level_NN_audio.tres</c> using the id's own two-digit slot, so a level
        /// whose era token is later renamed keeps its music. Returns "" for anything
        /// that is not a <c>level_NN_*</c> id, and callers treat "" as "no music set".
        /// </summary>
        public static string ForStoryLevel(string levelID) {
            if (string.IsNullOrWhiteSpace(levelID)) return "";
            string[] parts = levelID.Split('_');
            if (parts.Length < 2 || parts[0] != "level") return "";
            if (parts[1].Length != 2 || !int.TryParse(parts[1], out int index)) return "";
            if (index < 0 || index > 15) return "";
            return index == 0 ? Tutorial : $"{Directory}level_{parts[1]}_audio.tres";
        }

        /// <summary>
        /// Resolves a Fighter catalog <c>StageID</c> (<c>orleans_vanguard</c>) to its
        /// authored set (<c>stage_orleans_audio.tres</c>). The era token is the first
        /// segment of the stage id, which is how all ten Package 6 stages are named.
        /// Returns "" for a blank id.
        /// </summary>
        public static string ForFighterStage(string stageID) {
            if (string.IsNullOrWhiteSpace(stageID)) return "";
            int separator = stageID.IndexOf('_');
            string era = separator > 0 ? stageID.Substring(0, separator) : stageID;
            return era.Length == 0 ? "" : $"{Directory}stage_{era}_audio.tres";
        }
    }
}
