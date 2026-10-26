using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// Closeout (2026-10-26). The one place a panel tag is built and placed (UI_SPEC 3.1: a <c>PanelTagPlate</c>
    /// holding an upper-case <c>PanelTag</c> label, rotated -1.2 degrees about its bottom-left corner). Four
    /// panels wear a tag and three of them (<see cref="PulpTaggedPanel"/>, <c>PanelTagAnchor</c>, <c>PausePanel</c>)
    /// used to build it by hand; they differ only in where the tag hangs, because their node paths differ and
    /// tests pin those paths. The main menu's authored <c>TagHolder/Tag</c> nodes stay scene-authored for the
    /// same reason and use the same variations and offsets.
    /// </summary>
    public static class PulpTagPlate {

        /// <summary>Builds the tag plate (hidden until it has text). <paramref name="label"/> is its <c>PanelTag</c> label.</summary>
        public static PanelContainer Create(out Label label) {
            label = new Label {
                Name = "TagLabel", Uppercase = true, ThemeTypeVariation = UIPalette.LabelRoles.PanelTag,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            var tag = new PanelContainer {
                Name = "Tag", ThemeTypeVariation = UIPalette.Variations.PanelTagPlate,
                MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 1, Visible = false
            };
            tag.AddChild(label);
            return tag;
        }

        /// <summary>Shrinks the tag to its text, puts its top-left at <paramref name="position"/> and tilts it.</summary>
        public static void Place(Control tag, Vector2 position) {
            tag.ResetSize();
            tag.Position = position;
            tag.PivotOffset = new Vector2(0f, tag.Size.Y);
            tag.Rotation = Mathf.DegToRad(PulpTaggedPanel.TagDegrees);
        }
    }
}
