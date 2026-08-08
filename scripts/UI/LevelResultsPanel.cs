using Godot;

namespace FTT.UI {

    /// <summary>
    /// Story level completion overlay: localized banner, dust earned this level,
    /// completion time, rewinds spent, and the documented "Return to Time-Ship"
    /// prompt. The owning controller connects <see cref="ReturnRequested"/> to the
    /// hub-return flow.
    /// </summary>
    public partial class LevelResultsPanel : CanvasLayer {
        public const string SceneResourcePath = "res://scenes/ui/LevelResults.tscn";

        [Signal] public delegate void ReturnRequestedEventHandler();

        private Control _shade;
        private Label _titleLabel;
        private Label _dustLabel;
        private Label _timeLabel;
        private Label _rewindsLabel;
        private Button _returnButton;

        /// <summary>
        /// Instantiates the authored results scene. Package 8 B1 removed the
        /// code-built duplicate of this layout: two definitions of the same panel
        /// meant a stat added to one silently never appeared in the other, which is
        /// precisely the failure this change was fixing. A missing scene is now a
        /// reported packaging error rather than a quietly different screen.
        /// </summary>
        public static LevelResultsPanel CreateDefault() {
            if (ResourceLoader.Exists(SceneResourcePath)) {
                var packed = ResourceLoader.Load<PackedScene>(SceneResourcePath);
                if (packed?.Instantiate() is LevelResultsPanel authored) return authored;
            }
            GD.PushError($"LevelResults scene missing at {SceneResourcePath}; results will render nothing.");
            return new LevelResultsPanel { Name = "LevelResults" };
        }

        public override void _Ready() {
            Layer = 95;
            ProcessMode = ProcessModeEnum.Always;
            ResolveUI();
            if (_returnButton == null) return;
            _returnButton.Pressed += () => EmitSignal(SignalName.ReturnRequested);
            FocusChainBuilder.Apply(_returnButton.GetParent());
        }

        /// <summary>
        /// Shows the results using the run statistics <c>StoryManager</c> froze when
        /// the level reported complete.
        /// </summary>
        public void ShowResults(string levelTitleKey, int dustEarned) {
            FTT.Core.StoryManager story = FTT.Core.StoryManager.Instance;
            ShowResults(
                levelTitleKey,
                dustEarned,
                story?.LastLevelCompletionSeconds ?? 0f,
                story?.LastLevelRewindsUsed ?? 0);
        }

        /// <summary>
        /// Explicit-statistics overload. Present so the panel is provable without
        /// standing up the StoryManager autoload and playing a level through.
        /// </summary>
        public void ShowResults(string levelTitleKey, int dustEarned, float completionSeconds, int rewindsUsed) {
            if (_titleLabel != null) _titleLabel.Text = $"{Tr("results_level_complete")}\n{Tr(levelTitleKey)}";
            if (_dustLabel != null) _dustLabel.Text = string.Format(Tr("results_dust_earned"), dustEarned);
            if (_timeLabel != null) {
                _timeLabel.Text = string.Format(
                    Tr("results_completion_time"), FTT.Core.StoryManager.FormatDuration(completionSeconds));
            }
            if (_rewindsLabel != null) {
                _rewindsLabel.Text = string.Format(Tr("results_rewinds_used"), Mathf.Max(0, rewindsUsed));
            }
        }

        private void ResolveUI() {
            _shade = GetNodeOrNull<Control>("Shade");
            if (_shade != null) UIPalette.ApplyTheme(_shade);
            _titleLabel = GetNodeOrNull<Label>("Shade/Panel/Layout/Title");
            _dustLabel = GetNodeOrNull<Label>("Shade/Panel/Layout/DustEarned");
            _timeLabel = GetNodeOrNull<Label>("Shade/Panel/Layout/CompletionTime");
            _rewindsLabel = GetNodeOrNull<Label>("Shade/Panel/Layout/RewindsUsed");
            _returnButton = GetNodeOrNull<Button>("Shade/Panel/Layout/ReturnButton");
        }
    }
}
