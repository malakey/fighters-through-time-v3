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

        // CM-PADUI (2026-10-16): how many pause menus in the tree are open right
        // now, so a surface underneath (the dialogue box) can stand down while a
        // pause owns the player's presses. Each menu counts itself at most once
        // and always uncounts in _ExitTree, so a freed menu can never leave it set.
        private static int s_openPauseMenus;
        private bool _countedOpen;

        /// <summary>
        /// CM-PADUI (2026-10-16): true while any pause menu in the tree is open. A
        /// dialogue sequence under the pause must not take the presses the pause
        /// menu is answering (pad A on Resume, pad B backing out).
        /// </summary>
        public static bool IsAnyOpen => s_openPauseMenus > 0;

        /// <summary>
        /// GR-INTEG: true while a screen that is not a pause menu owns the tree pause (the training room's settings panel).
        /// A pause menu then refuses its own button and a focus loss, so there is one pause owner and a second menu cannot
        /// open over the first and unpause gameplay under it.
        /// </summary>
        public static bool ExternalPauseOwnerActive { get; set; }

        private void SetCountedOpen(bool open) {
            if (open == _countedOpen) return;
            _countedOpen = open;
            s_openPauseMenus = System.Math.Max(0, s_openPauseMenus + (open ? 1 : -1));
        }

        /// <summary>
        /// Releases a held pause on teardown. Subclasses that override this must
        /// call <c>base._ExitTree()</c>.
        /// </summary>
        public override void _ExitTree() {
            FTT.Core.PlatformOverlay.OverlayActivated -= OnPlatformOverlayActivated;
            SetCountedOpen(false);
            if (!_isPaused) return;
            _isPaused = false;
            SceneTree tree = GetTree();
            // CM-FIXB-PADUI: handed back to a gameplay-pausing dialogue that still holds it.
            if (tree != null) tree.Paused = DialogueHoldsGameplayPause();
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
            SetCountedOpen(paused && IsInsideTree());

            SceneTree tree = GetTree();
            // CM-FIXB-PADUI (2026-10-16): a pause opened over a gameplay-pausing
            // dialogue (Start, a focus loss) hands the tree back to it on every
            // resume — Resume, pad B, a controller-lost resolution — rather than
            // running the world under the still-open box.
            // GR-INTEG: and under an external owner (the training settings panel), whose close hands the tree back.
            if (tree != null) tree.Paused = paused || DialogueHoldsGameplayPause() || ExternalPauseOwnerActive;
            // Package 11 A3 (F01): reading the map costs no Timeline Integrity.
            FTT.Core.StoryManager.Instance?.SetIntegrityClockPause(
                FTT.Core.IntegrityClockPause.PauseMenu, paused);

            // CM-PADUI (2026-10-16): the press that closed the pause — pad B
            // backing out, a controller-lost face button, a key — is spent. It
            // must not reach the hero or the fighter as a fresh press on the
            // first unpaused tick (B is Interact, A Jump).
            if (!paused) FTT.Core.InputManager.Instance?.ConsumeHeldPresses();

            OnPauseStateChanged(paused);
        }

        /// <summary>
        /// CM-FIXB-PADUI (2026-10-16): closes the pause for a flow that leaves the
        /// scene (Restart Level, Quit to Main Menu). Unlike a resume, the tree is
        /// released outright, even under a gameplay-pausing dialogue the pause
        /// was opened over: the scene load runs on <c>GameManager._Process</c>,
        /// which a paused tree stops, and the dialogue box leaves with the scene
        /// (its own <c>_ExitTree</c> hands back its share).
        /// </summary>
        protected internal void ClosePauseForSceneExit() {
            // GR-INTEG: an open training panel must not keep the tree paused through the load either.
            ExternalPauseOwnerActive = false;
            SetPaused(false);
            SceneTree tree = GetTree();
            if (tree != null) tree.Paused = false;
        }

        /// <summary>
        /// CM-FIXB-PADUI (2026-10-16): whether the scene's dialogue box is in a
        /// gameplay-pausing sequence that took <see cref="SceneTree.Paused"/>,
        /// which then stays set when this pause closes.
        /// </summary>
        private static bool DialogueHoldsGameplayPause() {
            DialogueManager dialogue = DialogueManager.Instance;
            return dialogue != null && IsInstanceValid(dialogue) && dialogue.IsInsideTree() && dialogue.HoldsGameplayPause;
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
            if (@event == null) return;
            if (@event.IsActionPressed(FTT.Core.InputManager.Actions.Pause)) {
                if (!ExternalPauseOwnerActive && CanTogglePause()) TogglePause();
                GetViewport()?.SetInputAsHandled();
                return;
            }
            // CM-PADUI (2026-10-16): ui_cancel (pad B, Escape) backs out of an open
            // pause, as Resume does. An overlay inside the pause (Settings, the
            // Move List, the Dialogue Log, a confirmation) is a child and answers
            // its own cancel first; a pause that is holding the player (a
            // controller-disconnect modal) swallows it. Escape is also the pause
            // key, so it took the branch above and never reaches this one.
            if (!_isPaused || !@event.IsActionPressed("ui_cancel")) return;
            if (CanTogglePause()) SetPaused(false);
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
        protected virtual bool CanAutoPause() => !ExternalPauseOwnerActive && CanTogglePause();
    }
}
