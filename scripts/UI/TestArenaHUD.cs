using Godot;
using FTT.Characters;
using FTT.Combat;

namespace FTT.UI {
    public partial class TestArenaHUD : CanvasLayer {
        private Label _stateLabel;
        private Label _hpLabel;
        private Label _cooldownLabel;
        private PlayerController _player;
        private UltimateMeter _meter;
        private BlockSystem _blockSys;

        public override void _Ready() {
            _stateLabel = GetNodeOrNull<Label>("StateLabel");
            _hpLabel = GetNodeOrNull<Label>("HPLabel");
            _cooldownLabel = GetNodeOrNull<Label>("CooldownLabel");
        }

        private void FindPlayer() {
            foreach (var node in GetTree().GetNodesInGroup("Players")) {
                if (node is PlayerController pc && pc.PlayerIndex == 0) {
                    _player = pc;
                    _meter = pc.GetNodeOrNull<UltimateMeter>("UltimateMeter");
                    _blockSys = pc.GetNodeOrNull<BlockSystem>("BlockSystem");
                    return;
                }
            }
        }

        public override void _Process(double delta) {
            if (_player == null || !IsInstanceValid(_player)) {
                FindPlayer();
                if (_player == null) return;
            }

            if (_stateLabel != null) {
                string charName = _player.Data?.DisplayName ?? "Unknown";
                _stateLabel.Text = $"{charName} | State: {_player.CurrentState} | Facing: {(_player.IsFacingRight ? "Right" : "Left")} | Jumps: {_player.RemainingJumps}";
            }

            if (_hpLabel != null) {
                int maxHP = _player.Data?.MaxHP ?? 100;
                float ultPct = _meter?.CurrentValue ?? _player.CurrentUltimateMeter;
                int blocks = _blockSys?.CurrentCharges ?? _player.CurrentBlockCharges;
                int maxBlocks = _blockSys?.MaxCharges ?? (_player.Data?.MaxBlockCharges ?? 3);
                _hpLabel.Text = $"HP: {_player.CurrentHP}/{maxHP} | Ult: {ultPct:F0}/100 | Blocks: {blocks}/{maxBlocks} | Combo: {_player.ComboCounter}";
            }

            if (_cooldownLabel != null) {
                string cd1 = _player.SpecialOneCooldownTimer > 0 ? $"{_player.SpecialOneCooldownTimer:F1}s" : "Ready";
                string cd2 = _player.SpecialTwoCooldownTimer > 0 ? $"{_player.SpecialTwoCooldownTimer:F1}s" : "Ready";
                string cdM = _player.MovementAbilityCooldownTimer > 0 ? $"{_player.MovementAbilityCooldownTimer:F1}s" : "Ready";
                _cooldownLabel.Text = $"[K] Special1: {cd1} | [L] Special2: {cd2} | [Shift] Move: {cdM}";
            }

            if (Input.IsActionJustPressed("ui_cancel")) {
                Core.GameManager.Instance?.LoadScene("res://scenes/menus/MainMenu.tscn");
            }
        }
    }
}
