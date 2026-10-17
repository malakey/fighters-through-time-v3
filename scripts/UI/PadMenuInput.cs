using Godot;

namespace FTT.UI {

    /// <summary>
    /// CM-PADUI (2026-10-16): the one rule a screen needs once a pad's A is
    /// <c>ui_accept</c> (project.godot). Godot's buttons act on <c>ui_accept</c>
    /// on <b>release</b> and never mark the press handled, so the same press still
    /// reaches every <c>_UnhandledInput</c> handler and every polled reader. A
    /// screen that also acts on <c>ui_accept</c> (the dialogue box, the Resonance
    /// grid) asks this first and stands down, and <c>InputManager._Input</c> asks
    /// it to keep the press from reaching the game as a fresh Jump.
    /// </summary>
    public static class PadMenuInput {

        /// <summary>The confirm action. Built in, extended with pad A in project.godot.</summary>
        public const string AcceptAction = "ui_accept";

        /// <summary>The back-out action. Built in, extended with pad B in project.godot.</summary>
        public const string CancelAction = "ui_cancel";

        /// <summary>
        /// True when <paramref name="inputEvent"/> is a <c>ui_accept</c> press and
        /// the viewport's focus owner is a visible, enabled, processing button —
        /// a control the GUI is about to act on with this very press.
        /// </summary>
        public static bool FocusedButtonTakesAccept(Viewport viewport, InputEvent inputEvent) {
            if (viewport == null || inputEvent == null || !inputEvent.IsActionPressed(AcceptAction)) return false;
            return viewport.GuiGetFocusOwner() is BaseButton button
                && button.IsVisibleInTree()
                && !button.Disabled
                && button.CanProcess();
        }
    }
}
