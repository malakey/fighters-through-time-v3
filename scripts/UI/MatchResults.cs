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
        private Control _panel;
        private Label _stamp;
        private Label _outcome;
        private Button _rematchButton;
        private bool _shown;
        // V7 "Rematch requires both": in local-human matches the Rematch press
        // arms a vote and each player confirms with their own Attack press.
        private bool _rematchPending;
        private readonly bool[] _rematchReady = new bool[2];

        /// <summary>True while the both-players Rematch vote is armed. Test seam.</summary>
        public bool RematchVotePending => _rematchPending;

        /// <summary>True once a result has been presented. Test seam.</summary>
        public bool IsShowing => _shown;

        /// <summary>Localized outcome line currently displayed. Test seam.</summary>
        public string OutcomeText => _outcome?.Text ?? "";

        /// <summary>Stamp colour currently in force. Test seam.</summary>
        public Color StampColor => _stamp != null ? _stamp.GetThemeColor("font_color") : default;

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
                    result.IsTrueTie ? UIPalette.Slate : UIPalette.BossRed);
            }
            if (_outcome != null) {
                _outcome.Text = result.IsTrueTie
                    ? Tr("fighter_results_draw")
                    : string.Format(Tr("fighter_results_winner"), result.WinnerPlayerID + 1);
            }
            // Focus is authored only once the panel is on screen: a chain built
            // against a hidden subtree collects nothing (FocusChainBuilder skips
            // invisible branches), and a controller player would land on nothing.
            FocusChainBuilder.Apply(_panel);
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
                Color = UIPalette.Shade,
                MouseFilter = Control.MouseFilterEnum.Stop,
                Visible = false
            };
            shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            UIPalette.ApplyTheme(shade);
            AddChild(shade);
            _root = shade;

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            shade.AddChild(center);

            var panel = new PanelContainer { Name = "Panel", CustomMinimumSize = new Vector2(560, 360) };
            center.AddChild(panel);
            _panel = panel;

            var layout = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            layout.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            panel.AddChild(layout);

            _stamp = new Label { Name = "Stamp", HorizontalAlignment = HorizontalAlignment.Center };
            _stamp.AddThemeFontSizeOverride("font_size", 56);
            _stamp.AddThemeColorOverride("font_outline_color", UIPalette.NavyDeep);
            _stamp.AddThemeConstantOverride("outline_size", 6);
            layout.AddChild(_stamp);

            var title = new Label {
                Text = Tr("fighter_results_title"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", UIPalette.TitleFontSize);
            title.AddThemeColorOverride("font_color", UIPalette.TextAccent);
            layout.AddChild(title);

            _outcome = new Label { Name = "Outcome", HorizontalAlignment = HorizontalAlignment.Center };
            _outcome.AddThemeFontSizeOverride("font_size", UIPalette.HeadingFontSize);
            layout.AddChild(_outcome);

            var rematch = MakeButton(Tr("fighter_rematch"));
            rematch.Pressed += OnRematchPressed;
            layout.AddChild(rematch);
            _rematchButton = rematch;

            // V7 "New Stage": same characters, back to stage select only. Any
            // player can trigger it (like Change Fighters); only Rematch votes.
            var newStage = MakeButton(Tr("fighter_new_stage"));
            newStage.Pressed += () => {
                GameManager manager = GameManager.Instance;
                if (manager == null) return;
                SessionData session = manager.CurrentSession;
                session.ResumeAtStageSelect = true;
                manager.CurrentSession = session;
                manager.LoadScene("res://scenes/menus/CharacterSelect.tscn");
            };
            layout.AddChild(newStage);

            var fighters = MakeButton(Tr("fighter_change_fighters"));
            fighters.Pressed += () => GameManager.Instance?.LoadScene("res://scenes/menus/CharacterSelect.tscn");
            layout.AddChild(fighters);

            bool holodeck = GameManager.Instance?.CurrentSession.ReturnToHubAfterFighterMatch == true;
            var menu = MakeButton(Tr(holodeck ? "fighter_return_to_ship" : "fighter_main_menu"));
            menu.Pressed += () => GameManager.Instance?.LoadScene(ExitScenePath());
            layout.AddChild(menu);
        }

        private void OnRematchPressed() {
            bool localHuman = GameManager.Instance?.CurrentSession.FighterOpponentType
                == FighterOpponentType.LocalHuman;
            if (!localHuman) {
                GameManager.Instance?.LoadScene(RematchScenePath());
                return;
            }
            // Arm the vote (design Section 11: "Rematch requires both"); each
            // player confirms on their own device, Interact cancels the vote.
            _rematchPending = true;
            _rematchReady[0] = false;
            _rematchReady[1] = false;
            if (_rematchButton != null) _rematchButton.Text = Tr("fighter_rematch_waiting");
        }

        public override void _Process(double delta) {
            if (!_rematchPending || InputManager.Instance == null) return;
            for (int player = 0; player < 2; player++) {
                if (InputManager.Instance.IsActionJustPressed("gameplay_basic_attack", player)) {
                    _rematchReady[player] = true;
                }
                if (InputManager.Instance.IsActionJustPressed("gameplay_interact", player)) {
                    _rematchPending = false;
                    if (_rematchButton != null) _rematchButton.Text = Tr("fighter_rematch");
                    return;
                }
            }
            if (_rematchReady[0] && _rematchReady[1]) {
                _rematchPending = false;
                GameManager.Instance?.LoadScene(RematchScenePath());
            }
        }

        private static Button MakeButton(string text) => new() {
            Text = text,
            CustomMinimumSize = new Vector2(UIPalette.ButtonMinWidth, UIPalette.ButtonMinHeight)
        };
    }
}
