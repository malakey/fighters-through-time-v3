using Godot;

namespace FTT.Environment {

    [GlobalClass]
    public partial class FighterStageData : Resource {
        [Export] public int SchemaVersion = 1;
        [Export] public string StageID = "";
        [Export] public string DisplayNameKey = "";
        [Export] public string LayoutDescriptionKey = "";
        [Export] public string HazardNameKey = "";
        [Export] public string HazardDescriptionKey = "";
        [Export(PropertyHint.File, "*.tscn")] public string ScenePath = "res://scenes/arenas/TestArena.tscn";
        /// <summary>
        /// Stage-select preview image. Empty means "no preview authored yet"; the
        /// select screen falls back to the era colours. Populated per stage in the
        /// Package 6 closeout once every stage ships a placeholder preview.
        /// </summary>
        [Export(PropertyHint.File, "*.svg,*.png")] public string PreviewTexturePath = "";
        [Export(PropertyHint.Range, "1,10,1")] public int HazardTypeID = 1;
        /// <summary>
        /// True for an <b>Open</b> stage: one whose main floor is authored as
        /// segments with real pits between them, so the bottom blast zone is
        /// reachable (V7 "Floor Segments, Pits &amp; Ledges"; Package 11 A9).
        /// False is <b>Sealed</b> — an unbroken floor wall to wall. Stage select
        /// shows the label, and <c>FighterStageCatalogTests</c> asserts this flag
        /// matches <c>FighterStageGeometry.ForStage(StageID).IsOpenStage</c>: a
        /// drift here would be a silent lie to the player about whether they can
        /// be knocked out downward.
        /// </summary>
        [Export] public bool IsOpenStage;
        [Export] public bool IsPlayable = true;
        [Export] public bool ProductionReady;
        [Export] public Color BackgroundColor = new(0.06f, 0.06f, 0.12f);
        [Export] public Color GroundColor = new(0.2f, 0.18f, 0.12f);
        [Export] public Color AccentColor = new(0.4f, 0.8f, 0.3f);
    }

    /// <summary>
    /// The one place the Open/Sealed layout badge's translation keys are chosen
    /// (Package 11 A9). Both stage pickers — the Fighter character-select stage
    /// phase and the hub Holodeck console — read it, so the wording can never
    /// disagree between them.
    /// </summary>
    public static class FighterStageLayoutBadge {
        public const string OpenLabelKey = "stage_layout_open";
        public const string SealedLabelKey = "stage_layout_sealed";
        public const string OpenTooltipKey = "stage_layout_open_tooltip";
        public const string SealedTooltipKey = "stage_layout_sealed_tooltip";

        public static string LabelKey(FighterStageData stage) =>
            stage != null && stage.IsOpenStage ? OpenLabelKey : SealedLabelKey;

        public static string TooltipKey(FighterStageData stage) =>
            stage != null && stage.IsOpenStage ? OpenTooltipKey : SealedTooltipKey;
    }
}
