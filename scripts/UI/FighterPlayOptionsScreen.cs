using FTT.Core;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// Package 12 W5 (H05/G01, adopted D4(a)). The Fighter Mode "Play Options"
    /// menu the main menu's Fighter button opens: <b>Local Versus</b> (the existing
    /// two-human select) and <b>Versus CPU</b> (a P1-only select, then the CPU
    /// configuration panel as a front-end screen, then the match). Steam Remote
    /// Play Together stays a notice (<c>DEFER-STEAM-RPT</c>) until Steam
    /// integration lands. No Story save is read or written on either route.
    ///
    /// <para>Every button writes the session through <see cref="FighterFlowRoutes"/>
    /// and is exposed as a method so a test can assert the session without a
    /// scene change.</para>
    /// </summary>
    public partial class FighterPlayOptionsScreen : Control {

        private const string Layout = "Center/Panel/Layout/";

        public override void _Ready() {
            UIPalette.ApplyTheme(this);
            GetNode<Button>(Layout + "LocalVersusButton").Pressed += () => Go(ApplyLocalVersus());
            GetNode<Button>(Layout + "VersusCpuButton").Pressed += () => Go(ApplyVersusCpu());
            GetNode<Button>(Layout + "BackButton").Pressed += () => Go(FighterFlowRoutes.MainMenuScenePath);
            FocusChainBuilder.Apply(GetNode<Control>(Layout));
            DressGlass();
        }

        /// <summary>
        /// TG-SCREENS (UI_SPEC_GLASS 9.1, 9.2: Fighter play options): a full-screen menu. The sub-screen art, the heading
        /// <c>fighter_play_options_title</c> between flourishes top-centre, the chevron column at its D6 width (min 560,
        /// cap 700, the prompt reserved) with the Remote Play notice on a centred glass slab (the slab stays centred: the
        /// layout sweep and the bound node paths need it; the spec's left column is a question for the orchestrator), the
        /// prompt bar bottom-right and the screen streak.
        /// </summary>
        private void DressGlass() {
            GlassScreenDressing.AddMenuArt(this, index: 1);
            GlassScreenDressing.AddScreenHeading(this, "fighter_play_options_title");
            if (GetNodeOrNull<PanelContainer>("Center/Panel") is { } panel) PulpScreenKit.DressPanel(panel, buttonInset: true);
            System.Collections.Generic.List<Button> column = PulpScreenKit.ButtonsOf(GetNode<Control>(Layout));
            foreach (Button button in column) FTT.UI.Pulp.PulpButtonDecor.Apply(button);
            FTT.UI.Pulp.PulpButtonDecor.ApplyStackWidth(column, 560f, GlassScreenDressing.ScaledStackCap(700f));
            PulpScreenKit.AddPromptBar(this, ("ui_accept", "ui_prompt_select"), ("ui_cancel", "common_back"));
            PulpScreenKit.AddScreenStreak(this);
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (@event == null || !@event.IsActionPressed("ui_cancel")) return;
            GetViewport()?.SetInputAsHandled();
            Go(FighterFlowRoutes.MainMenuScenePath);
        }

        /// <summary>Local Versus: two humans, the full select. Returns the destination.</summary>
        public static string ApplyLocalVersus() {
            if (GameManager.Instance == null) return null;
            GameManager.Instance.CurrentSession = FighterFlowRoutes.EnterLocalVersus(GameManager.Instance.CurrentSession);
            return FighterFlowRoutes.CharacterSelectScenePath;
        }

        /// <summary>Versus CPU: the P1-only select. Returns the destination.</summary>
        public static string ApplyVersusCpu() {
            if (GameManager.Instance == null) return null;
            GameManager.Instance.CurrentSession = FighterFlowRoutes.EnterVersusCpu(GameManager.Instance.CurrentSession);
            return FighterFlowRoutes.CharacterSelectScenePath;
        }

        private static void Go(string scenePath) {
            if (!string.IsNullOrEmpty(scenePath)) GameManager.Instance?.LoadScene(scenePath);
        }
    }
}
