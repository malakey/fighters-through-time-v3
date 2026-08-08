using Godot;

namespace FTT.UI {

    /// <summary>
    /// Package 8 B1. Live application of the <c>HudOpacity</c> accessibility
    /// setting to a HUD surface.
    ///
    /// Before this, exactly one surface honoured the setting — <c>TestArenaHUD</c>,
    /// once, in <c>_Ready</c> — so a player who moved the slider mid-session saw
    /// nothing change until the scene reloaded, and no Story surface honoured it at
    /// all. Plan §2.10 requires the setting to be live everywhere.
    ///
    /// It polls rather than subscribing because there is no settings-changed event
    /// to subscribe to: <c>SettingsMenu</c> writes straight into
    /// <c>GlobalSaveData</c> and is owned by another workstream this package. A
    /// float compare once per frame against an autoload field is cheaper than the
    /// bus plumbing would be, and it also picks up a change made by any other
    /// writer (a save load, a migration) rather than only by the settings screen.
    /// </summary>
    public sealed class HudOpacityBinder {

        /// <summary>The opacity most recently applied, 1 until the first poll.</summary>
        public float AppliedOpacity { get; private set; } = 1f;

        /// <summary>The saved setting right now, or 1 when no save is loaded.</summary>
        public static float CurrentSetting =>
            FTT.Core.SaveManager.Instance?.GlobalData?.HudOpacity ?? 1f;

        /// <summary>
        /// Applies the current setting to <paramref name="target"/> when it has
        /// changed. Returns true when a write actually happened, so a caller with
        /// extra opacity-derived state can refresh it on the same edge.
        ///
        /// Only the alpha channel is touched: a surface that tints itself (the
        /// boss bar's red, the meter's blue) keeps its colour.
        /// </summary>
        public bool Apply(CanvasItem target, bool force = false) {
            float opacity = CurrentSetting;
            if (!force && Mathf.IsEqualApprox(opacity, AppliedOpacity)) return false;
            AppliedOpacity = opacity;
            if (target == null || !GodotObject.IsInstanceValid(target)) return false;
            Color modulate = target.Modulate;
            target.Modulate = new Color(modulate.R, modulate.G, modulate.B, opacity);
            return true;
        }

        /// <summary>Forgets the applied value so the next <see cref="Apply"/> writes.</summary>
        public void Invalidate() => AppliedOpacity = float.NaN;
    }
}
