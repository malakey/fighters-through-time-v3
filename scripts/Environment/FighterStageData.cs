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
        [Export] public bool IsPlayable = true;
        [Export] public bool ProductionReady;
        [Export] public Color BackgroundColor = new(0.06f, 0.06f, 0.12f);
        [Export] public Color GroundColor = new(0.2f, 0.18f, 0.12f);
        [Export] public Color AccentColor = new(0.4f, 0.8f, 0.3f);
    }
}
