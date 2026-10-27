using Godot;

namespace FTT.UI {

    /// <summary>
    /// The Warden Beacon icon at the head of the Story HUD's Act III anchor readout (M15, Package 12 W10),
    /// authored as <c>TemporalRow/BeaconAnchors/BeaconIcon</c> immediately right of the rewind hourglass.
    ///
    /// <para>Since the Temporal Glass rebuild (TG-HUDS, UI_SPEC_GLASS 3.4.4) it is a 46 px glass badge
    /// (<see cref="HudBadge"/>) holding the kit's <c>beacon_anchor</c> glyph in <c>accent2</c>. At <b>zero</b> it becomes the
    /// <c>beacon_anchor_hollow</c> outline with a steady last-stand tint, no pulse, because the next collapse is Smothered
    /// (plan 0.4 P4). The hollow shape carries the meaning, so it reads without colour.</para>
    /// </summary>
    public partial class BeaconAnchorIcon : Control {
        /// <summary>The steady last-stand tint at zero charges.</summary>
        public static readonly Color LastStandTint = UIPalette.Danger;

        /// <summary>The glyph's tint while charges remain (UI_SPEC_GLASS 3.4.4: <c>accent2</c>).</summary>
        public static readonly Color ChargedTint = UIPalette.Accent2;

        public const string GlyphName = "beacon_anchor";
        public const string HollowGlyphName = "beacon_anchor_hollow";

        private bool _lastStand;
        private readonly HudBadge _badge;

        public BeaconAnchorIcon() {
            MouseFilter = MouseFilterEnum.Ignore;
            _badge = new HudBadge { Name = "Badge", IconName = GlyphName, Tint = ChargedTint, BadgeSize = AnchorPip.IconSize };
            AddChild(_badge);
        }

        /// <summary>True while the icon shows the hollow last-stand state.</summary>
        public bool LastStand => _lastStand;

        /// <summary>The colour currently drawn.</summary>
        public Color CurrentColor => _lastStand ? LastStandTint : ChargedTint;

        /// <summary>The glass badge that draws the icon.</summary>
        public HudBadge Badge => _badge;

        /// <summary>The glyph name currently drawn.</summary>
        public string CurrentGlyph => _badge.IconName;

        public void SetLastStand(bool lastStand) {
            if (_lastStand == lastStand) return;
            _lastStand = lastStand;
            _badge.IconName = lastStand ? HollowGlyphName : GlyphName;
            _badge.Tint = CurrentColor;
        }
    }
}
