using FTT.UI.Pulp;
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
        private PanelContainer _panel;
        private Label _titleLabel;
        private Label _dustLabel;
        private Label _dustMobsLabel;
        private Label _dustExtractorsLabel;
        private Label _dustBossLabel;
        private Label _timeLabel;
        private Label _rewindsLabel;
        private Label _integrityLabel;
        private Label _secretsLabel;
        /// <summary>Package 11 A5: the V7.5 "Resonance Restored" unlock beat.</summary>
        private Label _resonanceRestoredLabel;
        private Button _returnButton;

        /// <summary>The Resonance Restored line, for the A5 pin. Test seam.</summary>
        internal Label ResonanceRestoredLine => _resonanceRestoredLabel;
        // Package 11 A8 / F05: the itemisation widened from three categories to
        // six — required enemies, boss, Extractors, optional/secret allocations,
        // losses, and the Integrity tier bonus. A10 supplies the values from its
        // ledger; this surface owns the lines and their copy.
        private Label _dustOptionalLabel;
        private Label _dustLossesLabel;
        private Label _dustTierBonusLabel;
        /// <summary>
        /// D12 review fix-up: the dust the attempt had earned before this level
        /// load (a Collapse resume, an Anchor Snap, a quit-and-resume), which
        /// the scene-local category lines never saw. Shown only when non-zero.
        /// </summary>
        private Label _dustCarriedLabel;

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
        /// Package 11 A8 / F05: the full six-category itemisation — required
        /// enemies, boss, Extractors, optional and secret allocations, losses, and
        /// the Integrity tier bonus.
        ///
        /// <para><paramref name="lossDust"/> is subtracted from the displayed
        /// total and rendered as its own negative line, because a player who paid a
        /// Collapse fee should be able to see the number that left rather than
        /// inferring it from a total that quietly does not add up. The tier bonus
        /// is added on top for the same reason.</para>
        ///
        /// <para>The values come from the economy ledger, not from this panel: the
        /// overlay must never disagree with what the wallet was actually paid.</para>
        /// </summary>
        public void ShowResults(
            string levelTitleKey, int mobDust, int extractorDust, int bossDust,
            int optionalDust, int lossDust, int tierBonusDust,
            float completionSeconds, int rewindsUsed) =>
            ShowResults(
                levelTitleKey, mobDust, extractorDust, bossDust, optionalDust,
                carriedDust: 0, lossDust, tierBonusDust, completionSeconds, rewindsUsed);

        /// <summary>
        /// The six-category itemisation plus the dust carried into this level
        /// load (D12 review fix-up, <c>RESULTS-DUST-LINES-AFTER-RESET</c>). The
        /// category lines are scene-local tallies, so after a Collapse resume,
        /// an Anchor Snap or a quit-and-resume the dust the attempt already held
        /// appears on none of them; <paramref name="carriedDust"/> is that held
        /// dust plus the losses already taken from it
        /// (<c>StoryManager.LastLevelDustCarriedIn</c>), so
        /// <c>categories + carried + bonus − losses</c> is exactly what the
        /// completion banked. Its line is hidden when it is zero, which is every
        /// run that was never reloaded.
        /// </summary>
        public void ShowResults(
            string levelTitleKey, int mobDust, int extractorDust, int bossDust,
            int optionalDust, int carriedDust, int lossDust, int tierBonusDust,
            float completionSeconds, int rewindsUsed) {
            mobDust = Mathf.Max(0, mobDust);
            extractorDust = Mathf.Max(0, extractorDust);
            bossDust = Mathf.Max(0, bossDust);
            optionalDust = Mathf.Max(0, optionalDust);
            carriedDust = Mathf.Max(0, carriedDust);
            lossDust = Mathf.Max(0, lossDust);
            tierBonusDust = Mathf.Max(0, tierBonusDust);
            int total = mobDust + extractorDust + bossDust + optionalDust + carriedDust
                + tierBonusDust - lossDust;
            ShowResults(levelTitleKey, Mathf.Max(0, total), completionSeconds, rewindsUsed);
            SetItemizedLine(_dustMobsLabel, "results_dust_mobs", mobDust);
            SetItemizedLine(_dustExtractorsLabel, "results_dust_extractors", extractorDust);
            SetItemizedLine(_dustBossLabel, "results_dust_boss", bossDust);
            SetItemizedLine(_dustOptionalLabel, "results_dust_optional", optionalDust);
            if (carriedDust > 0) SetItemizedLine(_dustCarriedLabel, "results_dust_carried", carriedDust);
            SetItemizedLine(_dustLossesLabel, "results_dust_losses", lossDust);
            SetItemizedLine(_dustTierBonusLabel, "results_dust_tier_bonus", tierBonusDust);
        }

        /// <summary>
        /// Explicit-statistics overload. Present so the panel is provable without
        /// standing up the StoryManager autoload and playing a level through.
        /// </summary>
        public void ShowResults(string levelTitleKey, int dustEarned, float completionSeconds, int rewindsUsed) {
            // The level's short name rides the panel tag (UR-POLISH2's rule); the title is the T03 heading.
            if (_titleLabel != null) {
                _titleLabel.Text = Tr("results_level_complete");
                _titleFlourishes?.Refresh();
            }
            if (_panel != null) PulpScreenKit.AddTag(_panel, PulpScreenKit.ShortTitle(Tr(levelTitleKey)));
            SetRow(_dustLabel, "results_dust_earned", dustEarned);
            SetLineVisible(_dustMobsLabel, false);
            SetLineVisible(_dustExtractorsLabel, false);
            SetLineVisible(_dustBossLabel, false);
            SetLineVisible(_dustOptionalLabel, false);
            SetLineVisible(_dustCarriedLabel, false);
            SetLineVisible(_dustLossesLabel, false);
            SetLineVisible(_dustTierBonusLabel, false);
            SetRow(_timeLabel, "results_completion_time", FTT.Core.StoryManager.FormatDuration(completionSeconds));
            SetRow(_rewindsLabel, "results_rewinds_used", Mathf.Max(0, rewindsUsed));
            ShowTimelineLines();
        }

        /// <summary>Writes a results line through its row: the line's own text is the whole formatted string.</summary>
        private void SetRow(Label line, string translationKey, params object[] args) {
            if (line == null) return;
            ResultRow row = ResultRow.Attach(line);
            if (row != null) row.SetText(Tr(translationKey), args);
            else line.Text = string.Format(Tr(translationKey), args);
        }

        /// <summary>
        /// V7/V7.1 result lines: the Timeline Integrity percentage with its tier,
        /// and the secrets counter — read from the statistics StoryManager froze at
        /// completion. Hidden when no story run produced them (Fighter-adjacent
        /// tests, bare panels).
        ///
        /// <para>Package 11 A8, ruling 2.A: the <b>Chronal Rating</b> stamp is
        /// gone. V7.6 retires the rating outright, and the Integrity tier it was
        /// derived from is the thing the player is actually being told about — two
        /// gradings of the same run on adjacent lines only invited the question of
        /// which one counted.</para>
        ///
        /// <para>F01/H-31: the Integrity value shown is the one frozen at PreBoss
        /// activation, not a live reading — the gauge stops there by contract, so
        /// the boss fight cannot change the grade the player earned on the
        /// approach.</para>
        /// </summary>
        private void ShowTimelineLines() {
            // Package 11 A5: every ShowResults overload funnels through here, so
            // the unlock beat is wired once.
            ShowResonanceRestoredBeat();
            FTT.Core.StoryManager story = FTT.Core.StoryManager.Instance;
            if (story == null) {
                SetLineVisible(_integrityLabel, false);
                SetLineVisible(_secretsLabel, false);
                return;
            }
            float integrity = story.LastLevelIntegrityPercent;
            if (_integrityLabel != null) {
                SetRow(_integrityLabel, "results_integrity",
                    Mathf.FloorToInt(integrity), Tr(FTT.Core.TimelineIntegrityRules.TierKey(integrity)));
                _integrityLabel.Visible = true;
            }
            if (_secretsLabel == null) return;
            SetRow(_secretsLabel, "results_secrets", story.LastLevelSecretsFound, 1);
            _secretsLabel.Visible = true;
        }

        private void SetItemizedLine(Label label, string translationKey, int amount) {
            if (label == null) return;
            SetRow(label, translationKey, amount);
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
            _panel = GetNodeOrNull<PanelContainer>("Shade/Panel");
            // V7/V7.1 lines are code-built so the authored scene stays untouched:
            // inserted above the return button, hidden until populated.
            if (GetNodeOrNull<Container>("Shade/Panel/Layout") is Container layout) {
                // F05's three extra dust categories sit with the existing three,
                // above the run statistics.
                _dustOptionalLabel = AppendResultLine(layout, "DustOptional");
                _dustCarriedLabel = AppendResultLine(layout, "DustCarried");
                _dustLossesLabel = AppendResultLine(layout, "DustLosses");
                _dustTierBonusLabel = AppendResultLine(layout, "DustTierBonus");
                _integrityLabel = AppendResultLine(layout, "IntegrityLine");
                _secretsLabel = AppendResultLine(layout, "SecretsLine");
                _resonanceRestoredLabel = AppendResultLine(layout, "ResonanceRestoredLine");
                // The beat is a state line in Resonance bright (T25's face, res2), centred: not a row.
                _resonanceRestoredLabel.ThemeTypeVariation = UIPalette.LabelRoles.StateLabel;
                _resonanceRestoredLabel.HorizontalAlignment = HorizontalAlignment.Center;
                _resonanceRestoredLabel.Uppercase = true;
                _resonanceRestoredLabel.AddThemeColorOverride("font_color", UIPalette.Res2);
                // No RatingLine: V7.6 ruling 2.A retires the Chronal Rating.
                // UI_SPEC_GLASS 9.2: the dust categories, a 1 px edge-dim rule, the dust total in res, then the run
                // statistics and the restored-Resonance beat above the one chevron.
                _totalRule = new HSeparator { Name = "TotalRule", MouseFilter = Control.MouseFilterEnum.Ignore };
                layout.AddChild(_totalRule);
                var statsGap = new Control { Name = "StatsGap", CustomMinimumSize = new Vector2(0f, 6f), MouseFilter = Control.MouseFilterEnum.Ignore };
                layout.AddChild(statsGap);
                Order(layout, _titleLabel, _dustMobsLabel, _dustExtractorsLabel, _dustBossLabel, _dustOptionalLabel,
                    _dustCarriedLabel, _dustTierBonusLabel, _dustLossesLabel, _totalRule, _dustLabel, statsGap,
                    _integrityLabel, _secretsLabel, _timeLabel, _rewindsLabel, _resonanceRestoredLabel, _returnButton);
            }
            DressGlass();
        }

        private HSeparator _totalRule;
        private TitleFlourishes _titleFlourishes;

        /// <summary>Moves <paramref name="nodes"/> (skipping the missing) to consecutive slots of <paramref name="layout"/>, in order.</summary>
        private static void Order(Container layout, params Node[] nodes) {
            int at = 0;
            foreach (Node node in nodes) {
                if (node == null || node.GetParent() != layout) continue;
                layout.MoveChild(node, at++);
            }
        }

        /// <summary>
        /// TG-SCREENS (UI_SPEC_GLASS 9.2, level results): an overlay over the finished level. The .70 dim, one 880 px glass
        /// slab with the sheen, the 14 px button inset and the short-title tag; the T03 title between flourishes inside the
        /// slab; every statistic a results row (caption left, tabular value right) over its line, whose own text stays the
        /// one formatted string the results tests read; the dust total's value in res; one focused chevron with the D6 width
        /// (prompt reserved) that wraps rather than trims (UR-POLISH); the prompt bar bottom-centre; no streak (plan 0.4 P3).
        /// </summary>
        private void DressGlass() {
            if (_shade is ColorRect dim) dim.Color = UIPalette.PauseDim;
            if (_panel != null) {
                PulpScreenKit.DressPanel(_panel, buttonInset: true);
                MenuSafeAreaFit.AttachClearOfPromptBar(_panel, _shade);
            }
            if (_titleLabel != null) _titleFlourishes = TitleFlourishes.Attach(_titleLabel);
            foreach (Label line in new[] { _dustMobsLabel, _dustExtractorsLabel, _dustBossLabel, _dustOptionalLabel,
                         _dustCarriedLabel, _dustTierBonusLabel, _dustLossesLabel, _dustLabel, _integrityLabel,
                         _secretsLabel, _timeLabel, _rewindsLabel }) {
                ResultRow.Attach(line);
            }
            ResultRow.Attach(_dustLabel)?.SetValueColor(UIPalette.Res);
            if (_returnButton != null) {
                PulpButtonDecor.Apply(_returnButton).GrowToFit = true;
                PulpButtonDecor.ApplyStackWidth(new[] { _returnButton }, 560f, PulpScreenKit.ScaledStackCap(700f));
                if (_shade != null) PulpScreenKit.AddOverlayPromptBar(_shade, ("ui_accept", "ui_prompt_select"));
            }
            if (_panel != null) PulpScreenKit.FadeIn(_panel);
        }

        // === Package 11 A5: the V7.5 "Resonance Restored" beat ==============

        /// <summary>
        /// Names the ability this level's completion restored — Levels 0–3 alike
        /// (Level 0 restores Special 1; since the 2026-11-03 schedule shift the
        /// Ultimate is granted by Level 3). Level 4 is the first full-kit level.
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
            // Results rows take the value's role (TG-SCREENS: the row draws a caption and a value over it).
            var label = new Label { Name = name, Visible = false, ThemeTypeVariation = UIPalette.LabelRoles.SettingsRowValue };
            layout.AddChild(label);
            if (_returnButton != null && _returnButton.GetParent() == layout) {
                layout.MoveChild(label, _returnButton.GetIndex());
            }
            return label;
        }
    }
}
