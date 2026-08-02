using Godot;
using System.Collections.Generic;

namespace FTT.Combat {
    public partial class MatchManager : Node {
        public static MatchManager Instance { get; private set; }

        [Export] public int StockCount = 3;
        [Export] public float TimeLimit = 480f;

        private Dictionary<int, int> _stocks = new();
        private Dictionary<int, int> _kos = new();
        private float _matchTimer;
        private FTT.Core.MatchState _matchState = FTT.Core.MatchState.PreMatch;

        public override void _Ready() {
            Instance = this;
            var settings = FTT.Core.GameManager.Instance?.CurrentSession.MatchSettings ?? FTT.Core.MatchSettings.GetDefault();
            StockCount = settings.StockCount;
            TimeLimit = settings.TimeLimit;
        }

        public void InitializeMatch(int playerCount) {
            _stocks.Clear();
            _kos.Clear();
            for (int i = 0; i < playerCount; i++) {
                _stocks[i] = StockCount;
                _kos[i] = 0;
            }
            _matchTimer = TimeLimit;
            SetMatchState(FTT.Core.MatchState.Countdown);
        }

        public void StartMatch() {
            SetMatchState(FTT.Core.MatchState.InProgress);
        }

        public override void _PhysicsProcess(double delta) {
            if (_matchState != FTT.Core.MatchState.InProgress) return;

            _matchTimer -= (float)delta;
            if (_matchTimer <= 0) {
                _matchTimer = 0;
                EndMatch();
            }
        }

        public void OnPlayerKO(int playerIndex, int killerIndex) {
            if (!_stocks.ContainsKey(playerIndex)) return;
            _stocks[playerIndex]--;
            if (_kos.ContainsKey(killerIndex)) _kos[killerIndex]++;

            SetMatchState(FTT.Core.MatchState.KOSequence);

            if (_stocks[playerIndex] <= 0) {
                EndMatch();
            } else {
                SetMatchState(FTT.Core.MatchState.InProgress);
            }
        }

        private void EndMatch() {
            SetMatchState(FTT.Core.MatchState.PostMatch);
        }

        private void SetMatchState(FTT.Core.MatchState state) {
            _matchState = state;
            FTT.Core.EventBus.Instance?.RaiseMatchStateChanged(state);
        }

        public int GetStocks(int playerIndex) => _stocks.TryGetValue(playerIndex, out int s) ? s : 0;
        public int GetKOs(int playerIndex) => _kos.TryGetValue(playerIndex, out int k) ? k : 0;
        public float RemainingTime => _matchTimer;
        public FTT.Core.MatchState CurrentState => _matchState;
    }
}
