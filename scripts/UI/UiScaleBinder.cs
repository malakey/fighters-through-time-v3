using Godot;

namespace FTT.UI {

    /// <summary>
    /// Live application of the accessibility <c>UiScale</c> setting (design
    /// "Committed Accessibility Additions": 90%–140%) to a HUD layout.
    ///
    /// The HUDs are authored in screen pixels under a full-rect root Control, so
    /// scaling works by magnifying that root while shrinking its anchor box by
    /// the inverse factor: the root lays its children out on a
    /// <c>viewport / scale</c> canvas that the render transform blows back up to
    /// exactly the viewport. Corner-anchored elements therefore stay pinned to
    /// their corners at every factor instead of sliding off-screen.
    ///
    /// Polls like <see cref="HudOpacityBinder"/> and for the same reason: the
    /// settings screen writes straight into <c>GlobalSaveData</c> with no
    /// settings-changed event to subscribe to.
    /// </summary>
    public sealed class UiScaleBinder {

        /// <summary>The scale most recently applied; NaN until the first poll.</summary>
        public float AppliedScale { get; private set; } = float.NaN;

        /// <summary>The saved setting right now, clamped; 1 when no save is loaded.</summary>
        public static float CurrentSetting => UIPalette.CurrentUiScale;

        /// <summary>
        /// Applies the current setting to <paramref name="root"/> when it has
        /// changed. Returns true when a write actually happened. The root must
        /// be a full-rect (anchors 0..1) Control: its right/bottom anchors are
        /// rewritten to <c>1 / scale</c> as the compensation half of the trick.
        /// </summary>
        public bool Apply(Control root, bool force = false) {
            float scale = CurrentSetting;
            if (!force && Mathf.IsEqualApprox(scale, AppliedScale)) return false;
            AppliedScale = scale;
            if (root == null || !GodotObject.IsInstanceValid(root)) return false;
            root.Scale = new Vector2(scale, scale);
            root.AnchorLeft = 0f;
            root.AnchorTop = 0f;
            root.AnchorRight = 1f / scale;
            root.AnchorBottom = 1f / scale;
            return true;
        }

        /// <summary>Forgets the applied value so the next <see cref="Apply"/> writes.</summary>
        public void Invalidate() => AppliedScale = float.NaN;
    }
}
