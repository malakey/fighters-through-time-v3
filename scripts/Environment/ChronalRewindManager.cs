using Godot;

namespace FTT.Environment {

    public partial class ChronalRewindManager : Node {
        [Export] public int MaxRewinds = 3;
        public int RemainingRewinds { get; private set; }

        private FTT.Core.Difficulty _difficulty;

        public override void _Ready() {
            _difficulty = FTT.Core.GameManager.Instance?.CurrentSession.Difficulty ?? FTT.Core.Difficulty.Normal;
            RemainingRewinds = GetRewindCount(_difficulty);
            FTT.Core.EventBus.Instance.OnPlayerDied += OnPlayerDied;
            FTT.Core.EventBus.Instance.OnCheckpointReached += OnCheckpointReached;
        }

        public override void _ExitTree() {
            if (FTT.Core.EventBus.Instance != null) {
                FTT.Core.EventBus.Instance.OnPlayerDied -= OnPlayerDied;
                FTT.Core.EventBus.Instance.OnCheckpointReached -= OnCheckpointReached;
            }
        }

        private int GetRewindCount(FTT.Core.Difficulty diff) {
            return diff switch {
                FTT.Core.Difficulty.Easy => 5,
                FTT.Core.Difficulty.Normal => 3,
                FTT.Core.Difficulty.Hard => 1,
                _ => 3
            };
        }

        private float GetHPRestorePercent(FTT.Core.Difficulty diff) {
            return diff switch {
                FTT.Core.Difficulty.Easy => 1.0f,
                FTT.Core.Difficulty.Normal => 0.5f,
                FTT.Core.Difficulty.Hard => 0.3f,
                _ => 0.5f
            };
        }

        private void OnPlayerDied(int playerIndex) {
            if (RemainingRewinds > 0) {
                RemainingRewinds--;
                var levelManager = GetTree().CurrentScene.GetNodeOrNull<LevelManager>("LevelManager");
                var respawnPos = levelManager?.GetRespawnPosition() ?? Vector2.Zero;
                FTT.Core.EventBus.Instance?.RaiseRewindTriggered(respawnPos);
            }
        }

        private void OnCheckpointReached(string checkpointID) {
            RemainingRewinds = GetRewindCount(_difficulty);
        }
    }
}
