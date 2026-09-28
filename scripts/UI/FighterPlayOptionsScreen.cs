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
