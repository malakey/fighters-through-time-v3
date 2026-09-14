using Godot;
using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// V7.6 <b>Smothered</b> — the game's only Game Over (Package 11 A3b).
    ///
    /// <para>Reached only from <see cref="StoryManager.EnterSmothered"/>: an Act
    /// III collapse with no anchor charge left. The field finishes the cradle —
    /// the aura gutters out, cold Unbound light closes from the edges, and
    /// Sarah's channel cuts to static. Two options, and no third:</para>
    ///
    /// <list type="bullet">
    /// <item><b>Restart Level</b> — from the level's beginning under the standard
    /// Restart rules: all undeposited level dust cleared, anchor charges refilled,
    /// one new attempt ID. The only door out.</item>
    /// <item><b>Quit to Menu</b> — out of game. The attempt stays Smothered, so
    /// loading returns here, <b>not</b> to the hub and <b>not</b> with refilled
    /// HP or charges, and the failure fee is never charged a second time.</item>
    /// </list>
    ///
    /// <para><b>Deposited dust is always safe</b>, including earnings from
    /// previously completed Act III levels sealed through the Warden Beacon. The
    /// active level's earnings were never deposited and are cleared by the
    /// Restart, exactly as the standing loss rule says.</para>
    ///
    /// <para>The screen never writes <see cref="SceneTree.Paused"/>: it is a full
    /// scene, loaded in place of the level, not an overlay over a live world.</para>
    /// </summary>
    public partial class GameOverScreen : CanvasLayer {

        public const string ScenePath = "res://scenes/ui/GameOver.tscn";

        /// <summary>Raised when the player chooses Restart Level. Test seam.</summary>
        public event System.Action RestartChosen;

        /// <summary>Raised when the player chooses Quit to Menu. Test seam.</summary>
        public event System.Action QuitChosen;

        /// <summary>Set false in a test so the buttons report without changing scene.</summary>
        public bool PerformNavigation { get; set; } = true;

        public Button RestartButton { get; private set; }
        public Button QuitButton { get; private set; }

        /// <summary>The authored focus chain, in reading order. Test seam.</summary>
        public System.Collections.Generic.IReadOnlyList<Control> FocusChain => _focusChain;

        private System.Collections.Generic.List<Control> _focusChain = new();

        public override void _Ready() {
            ProcessMode = ProcessModeEnum.Always;
            var root = GetNodeOrNull<Control>("Shade");
            if (root == null) return;
            UIPalette.ApplyTheme(root);

            RestartButton = root.GetNodeOrNull<Button>("Center/Panel/Layout/RestartButton");
            QuitButton = root.GetNodeOrNull<Button>("Center/Panel/Layout/QuitButton");
            if (RestartButton != null) RestartButton.Pressed += OnRestartPressed;
            if (QuitButton != null) QuitButton.Pressed += OnQuitPressed;

            // Author the chain rather than leaning on Godot's geometric fallback,
            // which is a fallback and not a design.
            Control layout = root.GetNodeOrNull<Control>("Center/Panel/Layout");
            if (layout != null) _focusChain = FocusChainBuilder.Apply(layout);
        }

        public override void _ExitTree() {
            if (RestartButton != null && IsInstanceValid(RestartButton)) RestartButton.Pressed -= OnRestartPressed;
            if (QuitButton != null && IsInstanceValid(QuitButton)) QuitButton.Pressed -= OnQuitPressed;
        }

        private void OnRestartPressed() {
            RestartChosen?.Invoke();
            if (!PerformNavigation) return;
            StoryManager.Instance?.RestartFromGameOver();
        }

        private void OnQuitPressed() {
            QuitChosen?.Invoke();
            if (!PerformNavigation) return;
            // No fee, no state change: the Smothered attempt is already settled
            // and persisted, and quitting a settled failure screen must not
            // charge that same event again (F10).
            GameManager.Instance?.LoadScene(CampaignCompletionSequence.MainMenuScenePath);
        }

        /// <summary>Instantiates the authored scene. Returns null when it is missing.</summary>
        public static GameOverScreen CreateDefault() {
            var packed = ResourceLoader.Load<PackedScene>(ScenePath);
            return packed?.Instantiate() as GameOverScreen;
        }
    }
}
