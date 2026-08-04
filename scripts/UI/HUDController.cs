using Godot;

namespace FTT.UI {
    public partial class HUDController : CanvasLayer {
        private ProgressBar _p1HPBar;
        private ProgressBar _p2HPBar;
        private ProgressBar _p1UltBar;
        private ProgressBar _p2UltBar;
        private Label _timerLabel;
        private HBoxContainer _p1StockIcons;
        private HBoxContainer _p2StockIcons;

        public override void _Ready() {
            Layer = 10;
            SetupHUD();
            FTT.Core.EventBus.Instance.OnPlayerHPChanged += OnPlayerHPChanged;
            FTT.Core.EventBus.Instance.OnUltimateMeterChanged += OnUltimateMeterChanged;
        }

        public override void _ExitTree() {
            if (FTT.Core.EventBus.Instance != null) {
                FTT.Core.EventBus.Instance.OnPlayerHPChanged -= OnPlayerHPChanged;
                FTT.Core.EventBus.Instance.OnUltimateMeterChanged -= OnUltimateMeterChanged;
            }
        }

        private void SetupHUD() {
            var topBar = new HBoxContainer();
            topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            topBar.CustomMinimumSize = new Vector2(0, 60);
            AddChild(topBar);

            _p1HPBar = new ProgressBar();
            _p1HPBar.CustomMinimumSize = new Vector2(400, 30);
            _p1HPBar.MaxValue = 100;
            _p1HPBar.Value = 100;
            _p1HPBar.ShowPercentage = false;
            topBar.AddChild(_p1HPBar);

            topBar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            _timerLabel = new Label();
            _timerLabel.Text = "8:00";
            _timerLabel.HorizontalAlignment = HorizontalAlignment.Center;
            topBar.AddChild(_timerLabel);

            topBar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            _p2HPBar = new ProgressBar();
            _p2HPBar.CustomMinimumSize = new Vector2(400, 30);
            _p2HPBar.MaxValue = 100;
            _p2HPBar.Value = 100;
            _p2HPBar.ShowPercentage = false;
            _p2HPBar.FillMode = (int)ProgressBar.FillModeEnum.EndToBegin;
            topBar.AddChild(_p2HPBar);

            _p1UltBar = new ProgressBar();
            _p1UltBar.CustomMinimumSize = new Vector2(200, 15);
            _p1UltBar.MaxValue = 100;
            _p1UltBar.Value = 0;
            AddChild(_p1UltBar);

            _p2UltBar = new ProgressBar();
            _p2UltBar.CustomMinimumSize = new Vector2(200, 15);
            _p2UltBar.MaxValue = 100;
            _p2UltBar.Value = 0;
            _p2UltBar.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            _p2UltBar.FillMode = (int)ProgressBar.FillModeEnum.EndToBegin;
            AddChild(_p2UltBar);
        }

        private void OnPlayerHPChanged(FTT.Core.PlayerHPPayload payload) {
            if (payload.PlayerIndex == 0) {
                _p1HPBar.MaxValue = payload.MaxHP;
                _p1HPBar.Value = payload.CurrentHP;
            } else if (payload.PlayerIndex == 1) {
                _p2HPBar.MaxValue = payload.MaxHP;
                _p2HPBar.Value = payload.CurrentHP;
            }
        }

        private void OnUltimateMeterChanged(FTT.Core.UltimateMeterPayload payload) {
            if (payload.PlayerIndex == 0 && _p1UltBar != null) {
                _p1UltBar.Value = payload.CurrentValue;
            } else if (payload.PlayerIndex == 1 && _p2UltBar != null) {
                _p2UltBar.Value = payload.CurrentValue;
            }
        }

        public void UpdateTimer(float seconds) {
            int mins = (int)(seconds / 60f);
            int secs = (int)(seconds % 60f);
            _timerLabel.Text = $"{mins}:{secs:D2}";
        }

        public void UpdateStocks(int playerIndex, int stocks) {
            // Update stock icon display
        }
    }
}
