using Godot;

namespace FTT.Environment {

    public enum ResonanceNodeType { Minor, Major }

    [GlobalClass]
    public partial class ResonanceNodeData : Resource {
        [Export] public string NodeID = "";
        [Export] public string DisplayName = "";
        [Export(PropertyHint.MultilineText)] public string Description = "";
        [Export] public ResonanceNodeType Type = ResonanceNodeType.Minor;
        [Export] public int UnlockCost = 100;
        [Export] public string[] PrerequisiteNodeIDs;
        [Export] public string StatModifierKey = "";
        [Export] public float StatModifierValue;
    }

    [GlobalClass]
    public partial class ResonanceGridData : Resource {
        [Export] public string CharacterID = "";
        [Export] public ResonanceNodeData[] Nodes;
    }
}
