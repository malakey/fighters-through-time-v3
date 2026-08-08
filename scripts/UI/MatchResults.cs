using Godot;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;

namespace FTT.UI {

    /// <summary>
    /// Post-match win/loss screen (design-godot.md Section 11 "Post-Match
    /// Results" / "Return and Lobby Flow"). Replaces the panel the Test Arena
    /// HUD used to build in code, and — unlike that panel — rematch resolves the
    /// destination through the stage catalog, so the selected stage survives a
    /// rematch instead of dumping every match back into the Test Arena.
    /// </summary>
    public partial class MatchResults : CanvasLayer {
        private Control _root;
        private Label _stamp;
        private Label _outcome;
        private bool _shown;

        /// <summary>True once a result has been presented. Test seam.</summary>
        public bool IsShowing => _shown;

        /// <summary>Localized outcome line currently displayed. Test seam.</summary>
        public string OutcomeText => _outcome?.Text ?? "";

        public override void _Ready() {
            Layer = 90;
            ProcessMode = ProcessModeEnum.Always;
            BuildUI();
        }

        public void ShowResult(FighterMatchResult result) {
            if (_shown) return;
            _shown = true;
            if (_root != null) _root.Visible = true;
            if (_stamp != null) {
                _stamp.Text = result.IsTrueTie ? Tr("match_draw") : Tr("match_ko");
                _stamp.AddThemeColorOverride(
                    "font_color",
                    result.IsTrueTie ? new Color(0.8f, 0.82f, 0.9f) : new Color(1f, 0.28f, 0.2f));
            }
            if (_outcome != null) {
                _outcome.Text = result.IsTrueTie
                    ? Tr("fighter_results_draw")
                    : string.Format(Tr("fighter_results_winner"), result.WinnerPlayerID + 1);
            }
        }

        /// <summary>
        /// Rematch destination: the scene the selected stage actually lives in.
        /// Falls back to the Test Arena only when no catalog entry resolves.
        /// </summary>
        public static string RematchScenePath() {
            string stageID = GameManager.Instance?.CurrentSession.SelectedStageID ?? "";
            FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
            FighterStageData stage = catalog?.Find(stageID) ?? catalog?.FirstPlayable();
            if (stage != null
                && !string.IsNullOrWhiteSpace(stage.ScenePath)
                && ResourceLoader.Exists(stage.ScenePath)) {
                return stage.ScenePath;
            }
            return "res://scenes/arenas/TestArena.tscn";
        }

        /// <summary>Holodeck practice sessions exit to the Time-Ship hub; everything else to the menu.</summary>
        public static string ExitScenePath() =>
            GameManager.Instance?.CurrentSession.ReturnToHubAfterFighterMatch == true
                ? "res://scenes/campaign/HubWorld.tscn"
                : "res://scenes/menus/MainMenu.tscn";

        private void BuildUI() {
            var shade = new ColorRect {
                Name = "Shade",
                Color = new Color(0.02f, 0.03f, 0.08f, 0.88f),
                MouseFilter = Control.MouseFilterEnum.Stop,
                Visible = false
            };
            shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(shade);
            _root = shade;

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            shade.AddChild(center);

            var panel = new PanelContainer { CustomMinimumSize = new Vector2(560, 360) };
            center.AddChild(panel);

            var layout = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            layout.AddThemeConstantOverride("separation", 16);
            panel.AddChild(layout);

            _stamp = new Label { Name = "Stamp", HorizontalAlignment = HorizontalAlignment.Center };
            _stamp.AddThemeFontSizeOverride("font_size", 56);
            layout.AddChild(_stamp);

            var title = new Label {
                Text = Tr("fighter_results_title"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", 26);
            title.AddThemeColorOverride("font_color", new Color(0f, 0.9f, 0.9f));
            layout.AddChild(title);

            _outcome = new Label { Name = "Outcome", HorizontalAlignment = HorizontalAlignment.Center };
            _outcome.AddThemeFontSizeOverride("font_size", 22);
            layout.AddChild(_outcome);

            var rematch = MakeButton(Tr("fighter_rematch"));
            rematch.Pressed += () => GameManager.Instance?.LoadScene(RematchScenePath());
            layout.AddChild(rematch);

            var fighters = MakeButton(Tr("fighter_change_fighters"));
            fighters.Pressed += () => GameManager.Instance?.LoadScene("res://scenes/menus/CharacterSelect.tscn");
            layout.AddChild(fighters);

            bool holodeck = GameManager.Instance?.CurrentSession.ReturnToHubAfterFighterMatch == true;
            var menu = MakeButton(Tr(holodeck ? "fighter_return_to_ship" : "fighter_main_menu"));
            menu.Pressed += () => GameManager.Instance?.LoadScene(ExitScenePath());
            layout.AddChild(menu);
        }

        private static Button MakeButton(string text) => new() {
            Text = text,
            CustomMinimumSize = new Vector2(340, 46)
        };
    }
}
