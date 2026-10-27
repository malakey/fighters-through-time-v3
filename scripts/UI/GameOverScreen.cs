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
            DressGlass(root, layout);
        }

        /// <summary>The cold rule's diamond and rules (the Severed's light, UI_SPEC_GLASS 9.2 agent choice).</summary>
        public static readonly Color ColdRule = UIPalette.UnboundCold;

        /// <summary>
        /// TG-SCREENS (UI_SPEC_GLASS 9.2, Game Over: Smothered): the stronger .82 dim, a 760 px glass slab with the sheen and
        /// the 14 px stack inset, the T04 title, a 1 px cold rule with a cold diamond under it, the body in ink2, the
        /// deposited-dust notice in res2, the two chevrons stacked at their D6 width with Restart focused, and the prompt bar
        /// bottom-centre. An overlay: no screen streak.
        /// </summary>
        private void DressGlass(Control root, Control layout) {
            if (root is ColorRect dim) dim.Color = UIPalette.GameOverDim;
            if (root.GetNodeOrNull<PanelContainer>("Center/Panel") is { } panel) {
                PulpScreenKit.DressPanel(panel, buttonInset: true);
                MenuSafeAreaFit.AttachClearOfPromptBar(panel, root);
                GlassScreenDressing.FadeIn(panel);
            }
            if (layout != null && layout.GetNodeOrNull<Label>("Title") is { } title) {
                var rule = FTT.UI.Pulp.GlassFlourish.Create(FTT.UI.Pulp.GlassFlourish.FlourishShape.RuleDiamondRule, 0f, FTT.UI.Pulp.GlassFlourish.MenuGap);
                rule.Name = "ColdRule";
                rule.RuleColor = ColdRule;
                rule.DiamondTint = ColdRule;
                rule.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                rule.MouseFilter = Control.MouseFilterEnum.Ignore;
                layout.AddChild(rule);
                layout.MoveChild(rule, title.GetIndex() + 1);
            }
            layout?.GetNodeOrNull<Label>("Body")?.AddThemeColorOverride("font_color", UIPalette.Ink2);
            layout?.GetNodeOrNull<Label>("DustNotice")?.AddThemeColorOverride("font_color", UIPalette.Res2);
            var stack = new System.Collections.Generic.List<Button>();
            foreach (Button button in new[] { RestartButton, QuitButton }) {
                if (button == null) continue;
                FTT.UI.Pulp.PulpButtonDecor.Apply(button);
                stack.Add(button);
            }
            // D6 (UI_SPEC_GLASS 3.2): the stack shares its widest required width (prompt reserved), at least 560, inside the slab.
            FTT.UI.Pulp.PulpButtonDecor.ApplyStackWidth(stack, 560f, GlassScreenDressing.ScaledStackCap(680f));
            GlassScreenDressing.AddOverlayPromptBar(root, ("ui_accept", "ui_prompt_select"));
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
