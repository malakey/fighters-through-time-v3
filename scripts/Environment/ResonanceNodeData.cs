using Godot;

namespace FTT.Environment {

    /// <summary>
    /// How a node's <see cref="ResonanceNodeData.PrerequisiteNodeIDs"/> list is
    /// satisfied. <see cref="All"/> is the default so every pre-V7.6 authoring
    /// keeps its meaning; <see cref="Any"/> powers Einstein's mesh, Leonardo's
    /// meshing gear rings, Shakespeare's Three Acts and Tubman's Crossing.
    /// An EMPTY prerequisite list is root-eligible under either mode.
    /// </summary>
    public enum PrerequisiteMode { All, Any }

    [GlobalClass]
    public partial class ResonanceNodeData : Resource {
        /// <summary>2 = V7.6 (PrerequisiteMode / AbilityScope / Tier / GatedAbilityID / LayoutPosition).</summary>
        [Export] public int SchemaVersion = 1;
        [Export] public string NodeID = "";
        [Export] public string DisplayName = "";
        [Export] public string DisplayNameKey = "";
        [Export(PropertyHint.MultilineText)] public string Description = "";
        [Export] public string DescriptionKey = "";
        [Export] public ResonanceNodeType Type = ResonanceNodeType.Minor;
        [Export] public int UnlockCost = 100;
        [Export] public string[] PrerequisiteNodeIDs;
        /// <summary>V7.6. All-of (default) or Any-of over <see cref="PrerequisiteNodeIDs"/>.</summary>
        [Export] public PrerequisiteMode PrerequisiteMode = PrerequisiteMode.All;
        [Export] public string StatModifierKey = "";
        /// <summary>
        /// V7.6. The ability this node's stat lane applies to (e.g.
        /// <c>relativity_rift</c>, <c>tesla_coil</c>, <c>finisher_venom</c>).
        /// Empty means the lane is character-wide. Paired with the scoped keys
        /// in <see cref="ResonanceStatKeys"/>.
        /// </summary>
        [Export] public string AbilityScope = "";
        [Export] public float StatModifierValue;
        [Export] public bool StatModifierIsPercent;
        [Export] public string AbilityModifierKey = "";
        /// <summary>
        /// V7.6. Explicit tier (1 / 2 / 3). No longer inferable: V7.6 authors
        /// Tier 1 nodes that carry prerequisites (Joan Vigor, Leonardo Workshop
        /// Vigor, Tesla Conductive Hold, Lincoln Rail Reach) and Tier 2 nodes
        /// that hang straight off a Tier 1 root.
        /// </summary>
        [Export] public int Tier = 1;
        /// <summary>
        /// V7.6 dormant-silhouette rule. The Legacy ability slot
        /// (<c>special1</c> / <c>special2</c> / <c>movement</c> /
        /// <c>ultimate</c>) this node modifies. While that slot is still locked
        /// by the Legacy Unlock Schedule the node renders as an unnamed dormant
        /// star and refuses purchase with
        /// <see cref="ResonanceUnlockResult.AbilityLocked"/>. Empty = ungated.
        /// </summary>
        [Export] public string GatedAbilityID = "";
        /// <summary>
        /// V7.6. Normalized 0-1 constellation position. Nine unique topologies
        /// cannot render without it; the design's own schema snippet omits it.
        /// Also drives nearest-neighbour-in-direction controller navigation.
        /// </summary>
        [Export] public Vector2 LayoutPosition = new(0.5f, 0.5f);
    }
}
