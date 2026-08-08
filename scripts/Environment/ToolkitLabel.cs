using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Resolves the placeholder signage a shared toolkit template carries.
    ///
    /// The era-mechanic templates under <c>scenes/templates/</c> are graybox scenes,
    /// and their <c>Label</c> children were originally authored with literal English
    /// ("DEEP SAND", "GRAVITY FIELD", ...). Every component that owns one now exposes
    /// a <c>LabelKey</c> export instead and resolves it here in <c>_Ready</c>.
    ///
    /// Two things this buys, both learned in Wave B:
    /// <list type="bullet">
    /// <item>the copy is localized like every other visible string, through
    ///       <c>localization/en.csv</c>;</item>
    /// <item>a level can retarget the sign from its own <c>.tscn</c> by setting one
    ///       property on the instanced <b>root</b>, instead of overriding a child of
    ///       an instanced scene — which needs the fragile <c>index=</c> block the
    ///       Level 2 deviation flagged, and which is why Level 8 was setting the text
    ///       from level code.</item>
    /// </list>
    /// </summary>
    public static class ToolkitLabel {
        public const string DefaultLabelPath = "Label";

        /// <summary>
        /// Sets <paramref name="owner"/>'s label to the translation of
        /// <paramref name="translationKey"/>. A blank key blanks the label, which is
        /// how a level turns the placeholder signage off. Returns the label it wrote
        /// to, or null when the template has none.
        /// </summary>
        public static Label Apply(Node owner, NodePath labelPath, string translationKey) {
            if (owner == null || !GodotObject.IsInstanceValid(owner)) return null;
            NodePath path = labelPath == null || labelPath.IsEmpty ? DefaultLabelPath : labelPath;
            if (owner.GetNodeOrNull<Label>(path) is not Label label) return null;
            label.Text = string.IsNullOrWhiteSpace(translationKey) ? "" : owner.Tr(translationKey);
            return label;
        }
    }
}
