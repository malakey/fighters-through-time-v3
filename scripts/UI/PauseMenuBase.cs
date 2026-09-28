using Godot;

namespace FTT.UI {

    /// <summary>
    /// Package 8 A1. The pause contract shared by every pause surface.
    ///
    /// Story's <see cref="PauseMenu"/> and Fighter's <see cref="LocalFighterPause"/>
    /// had independently reimplemented the same four things, which meant the
    /// project's single most dangerous invariant lived in two places at once:
    ///
    /// <b>Whoever sets <see cref="SceneTree.Paused"/> hands it back, including on
    /// teardown.</b> A pause leaked through a scene change freezes whatever loads
    /// next with no way to release it, and under GdUnit4 it stops the runner's
    /// transport node and hangs the entire test session (CLAUDE.md failure
    /// signature 4). That is why <see cref="_ExitTree"/> here is not optional
    /// cleanup — it is the invariant.
    ///
    /// Subclasses supply presentation through <see cref="OnPauseStateChanged"/> and
    /// gate the pause button through <see cref="CanTogglePause"/>; neither one may
    /// take ownership of the flag itself.
    /// </summary>
    public abstract partial class PauseMenuBase : CanvasLayer {

        private bool _isPaused;

        /// <summary>True while this menu holds the scene tree paused.</summary>
        public bool IsPaused => _isPaused;

        /// <summary>
        /// Releases a held pause on teardown. Subclasses that override this must
        /// call <c>base._ExitTree()</c>.
        /// </summary>
        public override void _ExitTree() {
            FTT.Core.PlatformOverlay.OverlayActivated -= OnPlatformOverlayActivated;
            if (!_isPaused) return;
            _isPaused = false;
            SceneTree tree = GetTree();
            if (tree != null) tree.Paused = false;
            // Package 11 A3 (F01): the Integrity clock scope is handed back
            // with the tree pause, on teardown as well as on the normal path.
            FTT.Core.StoryManager.Instance?.SetIntegrityClockPause(
                FTT.Core.IntegrityClockPause.PauseMenu, false);
        }

        /// <summary>Flips the pause state.</summary>
        public void TogglePause() => SetPaused(!_isPaused);

        /// <summary>
        /// Sets the pause state and the tree flag together. Virtual so a subclass
        /// can layer presentation on top, never so it can skip the tree write.
        /// </summary>
        public virtual void SetPaused(bool paused) {
            if (_isPaused == paused) return;
            _isPaused = paused;

            SceneTree tree = GetTree();
            if (tree != null) tree.Paused = paused;
            // Package 11 A3 (F01): reading the map costs no Timeline Integrity.
            FTT.Core.StoryManager.Instance?.SetIntegrityClockPause(
                FTT.Core.IntegrityClockPause.PauseMenu, paused);

            OnPauseStateChanged(paused);
        }

        /// <summary>Presentation hook: show or hide the menu, move focus, close modals.</summary>
        protected virtual void OnPauseStateChanged(bool paused) { }

        /// <summary>
        /// Gate for the pause button. Return false when the surface is holding the
        /// player somewhere they must answer first — an open confirmation, or a
        /// forced controller-disconnect modal.
        /// </summary>
        protected virtual bool CanTogglePause() => true;

        /// <summary>
        /// The pause press is always consumed, including when
        /// <see cref="CanTogglePause"/> refuses it, so it can never fall through to
        /// gameplay underneath a menu that is deliberately holding the player.
        /// </summary>
        public override void _UnhandledInput(InputEvent @event) {
            if (@event == null || !@event.IsActionPressed(FTT.Core.InputManager.Actions.Pause)) return;
            if (CanTogglePause()) TogglePause();
            GetViewport()?.SetInputAsHandled();
        }

        // === Package 12 W6 (G11): auto-pause on window focus loss ===========
        // Design Section 11: "Window focus loss and the Steam overlay also open
        // the normal pause menu in every pausable mode (no modal); nothing
        // resumes until the player chooses Resume." Every pausable surface in
        // the game — Story levels and the hub (PauseMenu), local Fighter, the
        // Holodeck and the Calibration Drills (LocalFighterPause) — derives from
        // this class, so the rule lives here once. Regaining focus deliberately
        // does NOT resume. The Steam overlay reaches the same entry point through
        // FTT.Core.PlatformOverlay, which is a no-op until Steam ships (D7(a)).

        // Subscribed in _EnterTree (which no subclass overrides) and released in
        // _ExitTree (which every subclass must chain to base), so the hook cannot
        // be skipped by a subclass _Ready that forgets to call base.
        public override void _EnterTree() {
            FTT.Core.PlatformOverlay.OverlayActivated += OnPlatformOverlayActivated;
        }

        private void OnPlatformOverlayActivated(bool active) {
            if (active) HandleFocusLost();
        }

        public override void _Notification(int what) {
            if (what == NotificationApplicationFocusOut) HandleFocusLost();
        }

        /// <summary>
        /// Opens the ordinary pause menu because the window lost focus. A no-op when
        /// already paused or when the surface is holding the player elsewhere
        /// (<see cref="CanAutoPause"/>). Public so the rule is exercisable without
        /// a real window. Returns true when it paused.
        /// </summary>
        public bool HandleFocusLost() {
            if (!IsInsideTree() || _isPaused || !CanAutoPause()) return false;
            SetPaused(true);
            return true;
        }

        /// <summary>
        /// Whether a focus loss may open the pause right now. Defaults to the pause
        /// button's own gate; a subclass can narrow it further (a results screen, a
        /// non-pausable online match) but never widen it.
        /// </summary>
        protected virtual bool CanAutoPause() => CanTogglePause();
    }
}
