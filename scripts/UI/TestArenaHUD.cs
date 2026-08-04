using Godot;
using FTT.Characters;
using FTT.FighterSim;

namespace FTT.UI {
    public partial class TestArenaHUD : CanvasLayer {
        private Label _stateLabel;
        private Label _hpLabel;
        private Label _cooldownLabel;
        private Label _controlsLabel;
        private PlayerController _player;
        private FighterSimulationDriver _driver;

        public override void _Ready() {
            _stateLabel = GetNodeOrNull<Label>("StateLabel");
            _hpLabel = GetNodeOrNull<Label>("HPLabel");
            _cooldownLabel = GetNodeOrNull<Label>("CooldownLabel");
            _controlsLabel = GetNodeOrNull<Label>("ControlsLabel");
            if (_controlsLabel != null) _controlsLabel.Text = Tr("test_arena_controls");
        }

        private void FindPlayer() {
            foreach (var node in GetTree().GetNodesInGroup("Players")) {
                if (node is PlayerController pc && pc.PlayerIndex == 0) {
                    _player = pc;
                    foreach (Node simulationNode in GetTree().GetNodesInGroup("FighterSimulation")) {
                        if (simulationNode is FighterSimulationDriver driver) {
                            _driver = driver;
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
                Core.GameManager.Instance?.LoadScene("res://scenes/menus/MainMenu.tscn");
            }
        }

        private string CooldownText(int frames) => frames <= 0
            ? Tr("common_ready")
            : string.Format(Tr("common_seconds_short"), frames / (float)FighterSimulation.TickRate);

        private static string StateKey(string state) => state switch {
            "Knocked Out" => "fighter_state_knocked_out",
            "Respawning" => "fighter_state_respawning",
            "Dazed" => "fighter_state_dazed",
            "Stunned" => "fighter_state_stunned",
            "Blocking" => "fighter_state_blocking",
            "Grounded" => "fighter_state_grounded",
            "Airborne" => "fighter_state_airborne",
            _ => "common_unavailable"
        };
    }
}
