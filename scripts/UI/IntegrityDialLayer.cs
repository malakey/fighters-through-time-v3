using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-HUDS. One of the two baked layers of the Integrity stopwatch (UR-ART): the back (crown, ring,
    /// shadow, paper face) under the wedge, or the ticks and the 3 px wedge ring over it. Both are 2x bakes
    /// listed in 1x units, drawn at half size with the 112 px face's box origin at this control's origin.
    /// </summary>
    public partial class IntegrityDialLayer : Control {

        private readonly string _stem;

        public IntegrityDialLayer() : this("dial_back") { }

        public IntegrityDialLayer(string stem) {
            _stem = stem;
            Name = stem == "dial_back" ? "DialBack" : "DialTicks";
            MouseFilter = MouseFilterEnum.Ignore;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        }

        public override void _Draw() {
            Texture2D texture = PulpKit.Texture(_stem);
            if (texture == null) return;
            int[] expand = PulpKit.Expand(_stem);
            DrawTextureRect(texture, new Rect2(new Vector2(-expand[0], -expand[1]), texture.GetSize() / 2f), false);
        }
    }
}
