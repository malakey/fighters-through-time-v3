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
        private Label _integrityLabel;
        private Label _secretsLabel;
        private Label _ratingLabel;
        /// <summary>Package 11 A5: the V7.5 "Resonance Restored" unlock beat.</summary>
        private Label _resonanceRestoredLabel;
        private Button _returnButton;

        /// <summary>The Resonance Restored line, for the A5 pin. Test seam.</summary>
        internal Label ResonanceRestoredLine => _resonanceRestoredLabel;

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
            ShowTimelineLines();
        }

        /// <summary>
        /// V7/V7.1 result lines: the Timeline Integrity percentage and tier,
        /// the secrets counter, and the Chronal Rating stamp — read from the
        /// statistics StoryManager froze at completion. Hidden when no story
        /// run produced them (Fighter-adjacent tests, bare panels).
        /// </summary>
        private void ShowTimelineLines() {
            // Package 11 A5: every ShowResults overload funnels through here, so
            // the unlock beat is wired once.
            ShowResonanceRestoredBeat();
            FTT.Core.StoryManager story = FTT.Core.StoryManager.Instance;
            if (story == null) {
                SetLineVisible(_integrityLabel, false);
                SetLineVisible(_secretsLabel, false);
                SetLineVisible(_ratingLabel, false);
                return;
            }
            float integrity = story.LastLevelIntegrityPercent;
            if (_integrityLabel != null) {
                _integrityLabel.Text = string.Format(
                    Tr("results_integrity"),
                    Mathf.FloorToInt(integrity),
                    Tr(FTT.Core.TimelineIntegrityRules.TierKey(integrity)));
                _integrityLabel.Visible = true;
            }
            if (_secretsLabel != null) {
                _secretsLabel.Text = string.Format(Tr("results_secrets"), story.LastLevelSecretsFound, 1);
                _secretsLabel.Visible = true;
            }
            if (_ratingLabel != null && !string.IsNullOrEmpty(story.LastLevelChronalRating)) {
                _ratingLabel.Text = string.Format(Tr("results_rating"), story.LastLevelChronalRating);
                _ratingLabel.Visible = true;
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
            // V7/V7.1 lines are code-built so the authored scene stays untouched:
            // inserted above the return button, hidden until populated.
            if (GetNodeOrNull<Container>("Shade/Panel/Layout") is Container layout) {
                _integrityLabel = AppendResultLine(layout, "IntegrityLine");
                _secretsLabel = AppendResultLine(layout, "SecretsLine");
                _ratingLabel = AppendResultLine(layout, "RatingLine");
                _resonanceRestoredLabel = AppendResultLine(layout, "ResonanceRestoredLine");
                _resonanceRestoredLabel.AddThemeColorOverride("font_color", UIPalette.TextAccent);
            }
        }

        // === Package 11 A5: the V7.5 "Resonance Restored" beat ==============

        /// <summary>
        /// Names the ability this level's completion restored. Level 4's grant
        /// is deliberately silent here — the Ultimate's beat plays on Level 4A's
        /// entry instead (StoryManager.TryConsumeDeferredResonanceRestored), so
        /// the first full-kit level opens on it.
        /// </summary>
        private void ShowResonanceRestoredBeat() {
            if (_resonanceRestoredLabel == null) return;
            FTT.Core.StoryManager story = FTT.Core.StoryManager.Instance;
            if (story == null || !story.ShouldPlayResonanceRestoredOnResults) {
                _resonanceRestoredLabel.Visible = false;
                return;
            }
            _resonanceRestoredLabel.Text = string.Format(
                Tr("results_resonance_restored"), ResolveRestoredAbilityName(story.LastLegacyUnlockSlot.Value));
            _resonanceRestoredLabel.Visible = true;
        }

        /// <summary>
        /// The restored ability's authored display name — the resource owns it,
        /// so the beat can never disagree with the Move List. Falls back to the
        /// slot label when the character data is unavailable.
        /// </summary>
        private static string ResolveRestoredAbilityName(FTT.Core.AbilitySlot slot) {
            string slotKey = slot switch {
                FTT.Core.AbilitySlot.Special1 => "movelist_slot_special1",
                FTT.Core.AbilitySlot.Special2 => "movelist_slot_special2",
                FTT.Core.AbilitySlot.MovementAbility => "movelist_slot_movement",
                _ => "movelist_slot_ultimate"
            };
            string characterID = FTT.Core.GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "";
            if (!FTT.Characters.CharacterFactory.IsKnownCharacter(characterID)) {
                return TranslationServer.Translate(slotKey).ToString();
            }
            var data = FTT.Core.AuthoredResources.Load<FTT.Characters.CharacterData>(
                $"res://resources/Characters/{characterID}_data.tres");
            FTT.Combat.AbilityData ability = slot switch {
                FTT.Core.AbilitySlot.Special1 => data?.SpecialAttackOne,
                FTT.Core.AbilitySlot.Special2 => data?.SpecialAttackTwo,
                FTT.Core.AbilitySlot.MovementAbility => data?.MovementAbility,
                _ => data?.UltimateAttack
            };
            if (ability == null || string.IsNullOrWhiteSpace(ability.DisplayNameKey)) {
                return TranslationServer.Translate(slotKey).ToString();
            }
            return TranslationServer.Translate(ability.DisplayNameKey).ToString();
        }

        private Label AppendResultLine(Container layout, string name) {
            var label = new Label { Name = name, Visible = false };
            layout.AddChild(label);
            if (_returnButton != null && _returnButton.GetParent() == layout) {
                layout.MoveChild(label, _returnButton.GetIndex());
            }
            return label;
        }
    }
}
