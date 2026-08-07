using Godot;

namespace FTT.UI {

    /// <summary>
    /// Story level completion overlay: localized banner, dust earned this level,
    /// and the documented "Return to Time-Ship" prompt. The owning controller
    /// connects <see cref="ReturnRequested"/> to the hub-return flow.
    /// </summary>
    public partial class LevelResultsPanel : CanvasLayer {
        public const string SceneResourcePath = "res://scenes/ui/LevelResults.tscn";

        [Signal] public delegate void ReturnRequestedEventHandler();

        private Label _titleLabel;
        private Label _dustLabel;
        private Button _returnButton;

        public static LevelResultsPanel CreateDefault() {
            if (ResourceLoader.Exists(SceneResourcePath)) {
                var packed = ResourceLoader.Load<PackedScene>(SceneResourcePath);
                if (packed?.Instantiate() is LevelResultsPanel authored) return authored;
            }
            return new LevelResultsPanel { Name = "LevelResults" };
        }

        public override void _Ready() {
            Layer = 95;
            ProcessMode = ProcessModeEnum.Always;
            ResolveOrBuildUI();
            _returnButton.Pressed += () => EmitSignal(SignalName.ReturnRequested);
            _returnButton.GrabFocus();
        }

        public void ShowResults(string levelTitleKey, int dustEarned) {
            if (_titleLabel != null) _titleLabel.Text = $"{Tr("results_level_complete")}\n{Tr(levelTitleKey)}";
            if (_dustLabel != null) _dustLabel.Text = string.Format(Tr("results_dust_earned"), dustEarned);
        }

        private void ResolveOrBuildUI() {
            _titleLabel = GetNodeOrNull<Label>("Shade/Panel/Layout/Title");
            _dustLabel = GetNodeOrNull<Label>("Shade/Panel/Layout/DustEarned");
            _returnButton = GetNodeOrNull<Button>("Shade/Panel/Layout/ReturnButton");
            if (_titleLabel != null && _dustLabel != null && _returnButton != null) return;
            BuildFallbackUI();
        }

        private void BuildFallbackUI() {
            var shade = new ColorRect { Name = "Shade", Color = new Color(0.02f, 0.03f, 0.08f, 0.85f) };
            shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(shade);

            var panel = new PanelContainer { Name = "Panel" };
            panel.SetAnchorsPreset(Control.LayoutPreset.Center);
            panel.CustomMinimumSize = new Vector2(520, 260);
            shade.AddChild(panel);

            var layout = new VBoxContainer { Name = "Layout" };
            layout.AddThemeConstantOverride("separation", 18);
            panel.AddChild(layout);

            _titleLabel = new Label { Name = "Title", HorizontalAlignment = HorizontalAlignment.Center };
            _titleLabel.AddThemeFontSizeOverride("font_size", 24);
            _titleLabel.AddThemeColorOverride("font_color", new Color(0f, 0.9f, 0.9f));
            layout.AddChild(_titleLabel);

            _dustLabel = new Label { Name = "DustEarned", HorizontalAlignment = HorizontalAlignment.Center };
            _dustLabel.AddThemeFontSizeOverride("font_size", 16);
            _dustLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.8f, 0.3f));
            layout.AddChild(_dustLabel);

            _returnButton = new Button { Name = "ReturnButton", Text = Tr("results_return_hub") };
            layout.AddChild(_returnButton);
        }
    }
}
