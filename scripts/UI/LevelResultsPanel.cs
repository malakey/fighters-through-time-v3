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
        private Label _dustMobsLabel;
        private Label _dustExtractorsLabel;
        private Label _dustBossLabel;
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
        /// the level reported complete. Single-total form: the itemized dust lines
        /// stay hidden (pre-M-1 callers and tests).
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
        /// Itemized results (audit M-1; design's "itemized: mob kills, Chronal
        /// Extractors, boss reward"). The displayed total is the sum of the three
        /// lines, which the level controllers derive from the actual wallet awards,
        /// so the overlay can never disagree with what the player was paid.
        /// </summary>
        public void ShowResults(string levelTitleKey, int mobDust, int extractorDust, int bossDust) {
            FTT.Core.StoryManager story = FTT.Core.StoryManager.Instance;
            ShowResults(
                levelTitleKey,
                mobDust,
                extractorDust,
                bossDust,
                story?.LastLevelCompletionSeconds ?? 0f,
                story?.LastLevelRewindsUsed ?? 0);
        }

        /// <summary>Explicit-statistics itemized overload (provable without the autoloads).</summary>
        public void ShowResults(
            string levelTitleKey, int mobDust, int extractorDust, int bossDust,
            float completionSeconds, int rewindsUsed) {
            mobDust = Mathf.Max(0, mobDust);
            extractorDust = Mathf.Max(0, extractorDust);
            bossDust = Mathf.Max(0, bossDust);
            ShowResults(levelTitleKey, mobDust + extractorDust + bossDust, completionSeconds, rewindsUsed);
            SetItemizedLine(_dustMobsLabel, "results_dust_mobs", mobDust);
            SetItemizedLine(_dustExtractorsLabel, "results_dust_extractors", extractorDust);
            SetItemizedLine(_dustBossLabel, "results_dust_boss", bossDust);
        }

        /// <summary>
        /// Explicit-statistics overload. Present so the panel is provable without
        /// standing up the StoryManager autoload and playing a level through.
        /// </summary>
        public void ShowResults(string levelTitleKey, int dustEarned, float completionSeconds, int rewindsUsed) {
            if (_titleLabel != null) _titleLabel.Text = $"{Tr("results_level_complete")}\n{Tr(levelTitleKey)}";
            if (_dustLabel != null) _dustLabel.Text = string.Format(Tr("results_dust_earned"), dustEarned);
            SetLineVisible(_dustMobsLabel, false);
            SetLineVisible(_dustExtractorsLabel, false);
            SetLineVisible(_dustBossLabel, false);
            if (_timeLabel != null) {
                _timeLabel.Text = string.Format(
                    Tr("results_completion_time"), FTT.Core.StoryManager.FormatDuration(completionSeconds));
            }
            if (_rewindsLabel != null) {
                _rewindsLabel.Text = string.Format(Tr("results_rewinds_used"), Mathf.Max(0, rewindsUsed));
            }
        }

        private void SetItemizedLine(Label label, string translationKey, int amount) {
            if (label == null) return;
            label.Text = string.Format(Tr(translationKey), amount);
            label.Visible = true;
        }

        private static void SetLineVisible(Label label, bool visible) {
            if (label != null) label.Visible = visible;
        }

        private void ResolveUI() {
            _shade = GetNodeOrNull<Control>("Shade");
            if (_shade != null) UIPalette.ApplyTheme(_shade);
            _titleLabel = GetNodeOrNull<Label>("Shade/Panel/Layout/Title");
            _dustLabel = GetNodeOrNull<Label>("Shade/Panel/Layout/DustEarned");
            _dustMobsLabel = GetNodeOrNull<Label>("Shade/Panel/Layout/DustMobs");
            _dustExtractorsLabel = GetNodeOrNull<Label>("Shade/Panel/Layout/DustExtractors");
            _dustBossLabel = GetNodeOrNull<Label>("Shade/Panel/Layout/DustBoss");
            _timeLabel = GetNodeOrNull<Label>("Shade/Panel/Layout/CompletionTime");
            _rewindsLabel = GetNodeOrNull<Label>("Shade/Panel/Layout/RewindsUsed");
            _returnButton = GetNodeOrNull<Button>("Shade/Panel/Layout/ReturnButton");
        }
    }
}
