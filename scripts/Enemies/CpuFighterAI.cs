using Godot;
using FTT.Core;

namespace FTT.Enemies {
    public enum CpuDifficulty { Easy, Normal, Hard }

    public partial class CpuFighterAI : Node {
        [Export] public CpuDifficulty Difficulty = CpuDifficulty.Normal;
        [Export] public NodePath ControlledPlayerPath;

        private FTT.Characters.PlayerController _self;
        private FTT.Characters.PlayerController _target;
        private float _actionTimer;
        private float _reactionDelay;

        public override void _Ready() {
            _self = GetNodeOrNull<FTT.Characters.PlayerController>(ControlledPlayerPath);
            _reactionDelay = Difficulty switch {
                CpuDifficulty.Easy => 0.5f,
                CpuDifficulty.Normal => 0.25f,
                CpuDifficulty.Hard => 0.1f,
                _ => 0.25f
            };
        }

        public override void _PhysicsProcess(double delta) {
            if (_self == null) return;
            _actionTimer -= (float)delta;
            if (_actionTimer > 0) return;

            _target ??= FindOpponent();
            if (_target == null) return;

            _actionTimer = _reactionDelay;
            DecideAction();
        }

        private void DecideAction() {
            if (_target == null || _self == null) return;
            float dist = _self.GlobalPosition.DistanceTo(_target.GlobalPosition);
            // Simplified AI: approach, attack when close, block when being attacked
        }

        private FTT.Characters.PlayerController FindOpponent() {
            Godot.Collections.Array<Node> players = GetTree().GetNodesInGroup("Players");
            using var playersLifetime = players.AsDisposable();
            foreach (var node in players) {
                if (node is FTT.Characters.PlayerController pc && pc != _self) return pc;
            }
            return null;
        }
    }
}
