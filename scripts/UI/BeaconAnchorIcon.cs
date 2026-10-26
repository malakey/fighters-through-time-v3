using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// The Warden Beacon icon at the head of the Story HUD's Act III anchor readout (M15, Package 12 W10),
    /// authored as <c>TemporalRow/BeaconAnchors/BeaconIcon</c> immediately right of the rewind hourglass.
    ///
    /// <para>With charges remaining it is the kit's solid <c>anchor</c> icon in gold. At <b>zero</b> it
    /// becomes the <c>anchor_hollow</c> outline with a steady last-stand tint, no pulse, because the next
    /// collapse is Smothered. The hollow shape carries the meaning, so it reads without colour. Since the
    /// pulp redesign (UR-HUDS) it is drawn by a child <see cref="PulpIcon"/> at the 46 px badge size.</para>
    /// </summary>
    public partial class BeaconAnchorIcon : Control {
        /// <summary>The steady last-stand tint at zero charges.</summary>
        public static readonly Color LastStandTint = UIPalette.Danger;

        private bool _lastStand;
        private readonly PulpIcon _icon;

        public BeaconAnchorIcon() {
            MouseFilter = MouseFilterEnum.Ignore;
            _icon = new PulpIcon { IconName = "anchor", Tint = UIPalette.Res2 };
            _icon.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_icon);
        }

        /// <summary>True while the icon shows the hollow last-stand state.</summary>
        public bool LastStand => _lastStand;

        /// <summary>The colour currently drawn.</summary>
        public Color CurrentColor => _lastStand ? LastStandTint : UIPalette.Res2;

        public void SetLastStand(bool lastStand) {
            if (_lastStand == lastStand) return;
            _lastStand = lastStand;
            _icon.IconName = lastStand ? "anchor_hollow" : "anchor";
            _icon.Tint = CurrentColor;
        }
    }
}
