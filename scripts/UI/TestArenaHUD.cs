using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;

namespace FTT.UI {
    public partial class TestArenaHUD : CanvasLayer {
        private Label _stateLabel;
        private Label _hpLabel;
        private Label _cooldownLabel;
        private Label _controlsLabel;
        private PlayerController _player;
        private FighterSimulationDriver _driver;
        private bool _resultShown;

        public override void _Ready() {
            float hudOpacity = FTT.Core.SaveManager.Instance?.GlobalData?.HudOpacity ?? 1f;
            Godot.Collections.Array<Node> children = GetChildren();
            using var childrenLifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is CanvasItem item) item.Modulate = new Color(1f, 1f, 1f, hudOpacity);
            }
            _stateLabel = GetNodeOrNull<Label>("StateLabel");
            _hpLabel = GetNodeOrNull<Label>("HPLabel");
            _cooldownLabel = GetNodeOrNull<Label>("CooldownLabel");
            _controlsLabel = GetNodeOrNull<Label>("ControlsLabel");
            if (_controlsLabel != null) _controlsLabel.Text = Tr("test_arena_controls");
        }

        private void FindPlayer() {
            Godot.Collections.Array<Node> players = GetTree().GetNodesInGroup("Players");
            using var playersLifetime = players.AsDisposable();
            foreach (var node in players) {
                if (node is PlayerController pc && pc.PlayerIndex == 0) {
                    _player = pc;
                    Godot.Collections.Array<Node> simulations = GetTree().GetNodesInGroup("FighterSimulation");
                    using var simulationsLifetime = simulations.AsDisposable();
                    foreach (Node simulationNode in simulations) {
                        if (simulationNode is FighterSimulationDriver driver) {
                            _driver = driver;
                            _driver.MatchCompleted += ShowResults;
                            break;
                        }
                    }
                    return;
                }
            }
        }

        public override void _Process(double delta) {
            if (_player == null || !IsInstanceValid(_player) || _driver == null || !IsInstanceValid(_driver)) {
                FindPlayer();
                if (_player == null || _driver == null) return;
            }

            if (!_driver.TryGetFighter(0, out FighterStateComponent playerOne)
                || !_driver.TryGetFighter(1, out FighterStateComponent playerTwo)
                || !_driver.TryGetRuntime(0, out FighterRuntimeComponent runtime)) return;

            if (_stateLabel != null) {
                string characterNameKey = _player.Data?.DisplayNameKey ?? "";
                string characterName = string.IsNullOrWhiteSpace(characterNameKey)
                    ? Tr("common_unknown")
                    : Tr(characterNameKey);
                _stateLabel.Text = string.Format(
                    Tr("test_arena_state"),
                    characterName,
                    Tr(StateKey(_driver.GetStateLabel(0))),
                    Tr(playerOne.FacingRight != 0 ? "direction_right" : "direction_left"),
                    playerOne.RemainingJumps,
                    _driver.CurrentTick,
                    _driver.CurrentHash);
            }

            if (_hpLabel != null) {
                _hpLabel.Text = string.Format(
                    Tr("test_arena_vitals"),
                    playerOne.CurrentHP,
                    playerOne.MaxHP,
                    playerOne.Stocks,
                    playerOne.Influence.ToFloat(),
                    playerOne.BlockCharges,
                    playerTwo.CurrentHP,
                    playerTwo.MaxHP,
                    playerTwo.Stocks,
                    playerTwo.Influence.ToFloat(),
                    playerTwo.BlockCharges);
            }

            if (_cooldownLabel != null) {
                _cooldownLabel.Text = string.Format(
                    Tr("test_arena_cooldowns"),
                    CooldownText(runtime.SpecialOneCooldownFrames),
                    CooldownText(runtime.SpecialTwoCooldownFrames),
                    Tr(((FTT.Core.StatusType)runtime.StatusType) == FTT.Core.StatusType.None
                        ? "status_none"
                        : $"status_{((FTT.Core.StatusType)runtime.StatusType).ToString().ToLowerInvariant()}"));
            }

            if (Input.IsActionJustPressed("ui_cancel")) {
                Core.GameManager.Instance?.LoadScene(ExitScenePath());
            }
        }

        public override void _ExitTree() {
            if (_driver != null && IsInstanceValid(_driver)) _driver.MatchCompleted -= ShowResults;
        }

        private void ShowResults(FighterMatchResult result) {
            if (_resultShown) return;
            _resultShown = true;
            var shade = new ColorRect {
                Color = new Color(0.02f, 0.03f, 0.08f, 0.88f),
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(shade);

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            shade.AddChild(center);
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(520, 300) };
            center.AddChild(panel);
            var layout = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            layout.AddThemeConstantOverride("separation", 18);
            panel.AddChild(layout);
            var title = new Label {
                Text = Tr("fighter_results_title"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", 30);
            title.AddThemeColorOverride("font_color", new Color(0f, 0.9f, 0.9f));
            layout.AddChild(title);
            var outcome = new Label {
                Text = result.IsTrueTie
                    ? Tr("fighter_results_draw")
                    : string.Format(Tr("fighter_results_winner"), result.WinnerPlayerID + 1),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            outcome.AddThemeFontSizeOverride("font_size", 24);
            layout.AddChild(outcome);
            var rematch = MakeResultButton(Tr("fighter_rematch"));
            rematch.Pressed += () => Core.GameManager.Instance?.LoadScene("res://scenes/arenas/TestArena.tscn");
            layout.AddChild(rematch);
            var fighters = MakeResultButton(Tr("fighter_change_fighters"));
            fighters.Pressed += () => Core.GameManager.Instance?.LoadScene("res://scenes/menus/CharacterSelect.tscn");
            layout.AddChild(fighters);
            bool holodeck = Core.GameManager.Instance?.CurrentSession.ReturnToHubAfterFighterMatch == true;
            var menu = MakeResultButton(Tr(holodeck ? "fighter_return_to_ship" : "fighter_main_menu"));
            menu.Pressed += () => Core.GameManager.Instance?.LoadScene(ExitScenePath());
            layout.AddChild(menu);
        }

        /// <summary>Holodeck practice sessions exit back to the Time-Ship hub; everything else exits to the menu.</summary>
        private static string ExitScenePath() =>
            Core.GameManager.Instance?.CurrentSession.ReturnToHubAfterFighterMatch == true
                ? "res://scenes/campaign/HubWorld.tscn"
                : "res://scenes/menus/MainMenu.tscn";

        private static Button MakeResultButton(string text) => new() {
            Text = text,
            CustomMinimumSize = new Vector2(320, 46)
        };

        private string CooldownText(int frames) => frames <= 0
            ? Tr("common_ready")
            : string.Format(Tr("common_seconds_short"), frames / (float)FighterSimulation.TickRate);

        private static string StateKey(string state) => state switch {
            "Knocked Out" => "fighter_state_knocked_out",
            "Respawning" => "fighter_state_respawning",
            "Dazed" => "fighter_state_dazed",
            "Stunned" => "fighter_state_stunned",
            "Blocking" => "fighter_state_blocking",
            "Dashing" => "fighter_state_dashing",
            "Rolling" => "fighter_state_rolling",
            "Grounded" => "fighter_state_grounded",
            "Airborne" => "fighter_state_airborne",
            _ => "common_unavailable"
        };
    }
}
