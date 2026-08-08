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
            if (!_isPaused) return;
            _isPaused = false;
            SceneTree tree = GetTree();
            if (tree != null) tree.Paused = false;
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
    }
}
