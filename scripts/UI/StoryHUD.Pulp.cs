using FTT.Core;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-HUDS (pulp UI redesign, 2026-10-26). The HUD-S1 widgets and layout of the Story HUD: the pulp
    /// parts that sit on the authored glyph Labels, the boss nameplate layout, the dial's sealed state and
    /// the per-corner fit. Presentation only; every value is written by <see cref="StoryHUD"/>'s refreshers.
    /// The authored glyph Labels are kept with their Text and TooltipText (tests and accessibility read
    /// them) and draw nothing (<c>VisibleCharacters = 0</c>); the widget is their child.
    /// </summary>
    public partial class StoryHUD {

        /// <summary>HP share at or under which the bar reads low (UI_SPEC 3.3.1 leaves the threshold to data; provisional).</summary>
        public const float LowHpFraction = 0.25f;

        /// <summary>Boss bar width limits (UI_SPEC 3.5 and ruling 8: top centre, a maximum width).</summary>
        public const float BossBarMaxWidth = 840f;
        public const float BossBarMinWidth = 320f;
        private const float LeftClusterRight = 652f;
        private const float RightClusterWidth = 356f;
        private const float BossGap = 24f;

        private PulpBarDecor _hpDecor;
        private StyleBoxFlat _hpFill;
        private PulpPortraitRoundel _roundel;
        private PulpStatusChip _damageChip;
        private PulpStatusChip _controlChip;
        private readonly PulpAbilitySlot[] _slots = new PulpAbilitySlot[3];
        private PulpAbilitySlot _ultSlot;
        private PulpIcon _defyIcon;
        private HudBadge _rewindBadge;
        private HudBadge _freezeBadge;
        private PulpIcon _objectiveFlag;
        private PulpBarDecor _bossDecor;
        private int _bossPhase = 1;
        private bool _hpSeeded;
        private SiphonStreamsLayer _siphonDraw;
        private Label _integrityCaption;
        private Label _sealedNote;
        private IntegrityDialLayer _dialBack;
        private IntegrityDialLayer _dialTicks;
        private PulpTaggedPanel _vitalsPanel;
        private PulpTaggedPanel _abilitiesPanel;
        private bool _dialShown = true;
        private float _fitScale = float.NaN;

        /// <summary>The Vitals panel's tag, or null before the scene resolves. Test seam.</summary>
        public PulpTaggedPanel VitalsPanel => _vitalsPanel;

        /// <summary>The Abilities panel's tag, or null before the scene resolves. Test seam.</summary>
        public PulpTaggedPanel AbilitiesPanel => _abilitiesPanel;

        /// <summary>The pulp slot widgets (Special 1, Special 2, Movement). Test seam.</summary>
        public System.Collections.Generic.IReadOnlyList<PulpAbilitySlot> SlotWidgets => _slots;

        /// <summary>The Ultimate's slot widget. Test seam.</summary>
        public PulpAbilitySlot UltimateSlotWidget => _ultSlot;

        /// <summary>The two status chips, damage then control. Test seam.</summary>
        public (PulpStatusChip Damage, PulpStatusChip Control) StatusChips => (_damageChip, _controlChip);

        /// <summary>The HP bar's decor. Test seam.</summary>
        public PulpBarDecor HpDecor => _hpDecor;

        /// <summary>The boss bar's decor. Test seam.</summary>
        public PulpBarDecor BossDecor => _bossDecor;

        /// <summary>The siphon stream layer. Test seam.</summary>
        public SiphonStreamsLayer SiphonDraw => _siphonDraw;

        /// <summary>True while the dial (not the sealed note) shows. Test seam.</summary>
        public bool DialShown => _dialShown;

        /// <summary>The phase the boss tab shows (1 on reveal). Test seam.</summary>
        public int BossPhase => _bossPhase;

        private static void HideGlyph(Label label) {
            if (label != null) label.VisibleCharacters = 0;
        }

        private void BuildPulpWidgets() {
            _vitalsPanel = GetNodeOrNull<PulpTaggedPanel>(VitalsPanelPath);
            if (_vitalsPanel != null) _vitalsPanel.TagKey = "hud_vitals";
            _abilitiesPanel = GetNodeOrNull<PulpTaggedPanel>(AbilitiesPanelPath);
            if (_abilitiesPanel != null) _abilitiesPanel.TagKey = "hud_abilities";

            BuildTitleRow();
            BuildVitals();
            BuildAbilities();
            BuildTimeTools();
            BuildTopRight();
            BuildBoss();
            BuildToasts();
            ApplyFit(force: true);
        }

        private void BuildTitleRow() {
            if (_objectiveLabel == null) return;
            // The flag icon sits in a left margin of the objective's own text box, so the label keeps its
            // path and its Text for the level controllers and their tests.
            var margin = new StyleBoxEmpty { ContentMarginLeft = 32f };
            _objectiveLabel.AddThemeStyleboxOverride("normal", margin);
            _objectiveFlag = PulpIcon.Create("flag", UIPalette.Res2, 22f);
            _objectiveFlag.Position = new Vector2(0f, 4f);
            _objectiveFlag.Visible = false;
            _objectiveLabel.AddChild(_objectiveFlag);
        }

        private void BuildVitals() {
            Control host = _portrait?.GetParent<Control>();
            if (host != null) {
                _roundel = new PulpPortraitRoundel { Diameter = 112f };
                host.AddChild(_roundel);
                host.MoveChild(_roundel, 0);
            }
            if (_hpBar != null) {
                _hpDecor = PulpBarDecor.Attach(_hpBar);
                _hpDecor.LagValue = (float)_hpBar.Value;
                // The decor is the bar's last child; the numeral sits above it.
                if (_hpText != null) _hpBar.MoveChild(_hpText, -1);
                if (_hpBar.GetThemeStylebox("fill") is StyleBoxFlat themed) {
                    _hpFill = (StyleBoxFlat)themed.Duplicate();
                    _hpFill.BgColor = UIPalette.Hp;
                    _hpBar.AddThemeStyleboxOverride("fill", _hpFill);
                }
            }
            if (_echoBand != null) _echoBand.Visible = false;
            _damageChip = AddChip(_damageStatusIndicator);
            _controlChip = AddChip(_controlStatusIndicator);
        }

        private static PulpStatusChip AddChip(Label host) {
            if (host == null) return null;
            HideGlyph(host);
            var chip = new PulpStatusChip { BadgeSize = 44f, Empty = true, ShowRing = true };
            host.AddChild(chip);
            return chip;
        }

        private void BuildAbilities() {
            for (int index = 0; index < CooldownSlots.Length; index++) {
                Label icon = _cooldownIcons[index];
                if (icon == null) continue;
                HideGlyph(icon);
                HideGlyph(_cooldownLockOverlays[index]);
                if (_cooldownRadials[index] != null) _cooldownRadials[index].Visible = false;
                _slots[index] = new PulpAbilitySlot {
                    SlotSize = 74f, KeyText = HudAbilityIndicatorModel.SlotLabelKey(CooldownSlots[index])
                };
                icon.AddChild(_slots[index]);
                icon.MoveChild(_slots[index], 0);
            }
            HideGlyph(_meterLockOverlay);
            if (_meterHost != null) {
                _ultSlot = new PulpAbilitySlot { Ultimate = true, KeyText = "hud_slot_ultimate" };
                _meterHost.AddChild(_ultSlot);
                _meterHost.MoveChild(_ultSlot, 0);
            }
            if (_defySeal != null) {
                HideGlyph(_defySeal);
                _defyIcon = PulpIcon.Create("defy_notready", UIPalette.Ink3, 34f);
                _defySeal.AddChild(_defyIcon);
            }
        }

        private void BuildTimeTools() {
            if (_rewindIcon != null) {
                HideGlyph(_rewindIcon);
                _rewindBadge = new HudBadge { IconName = "hourglass", Tint = UIPalette.Accent2 };
                _rewindIcon.AddChild(_rewindBadge);
            }
            if (_freezeIcon != null) {
                HideGlyph(_freezeIcon);
                _freezeBadge = new HudBadge { IconName = "freeze", Tint = UIPalette.Accent2 };
                _freezeIcon.AddChild(_freezeBadge);
            }
            // The Beacon's pips take the pulp pip art and sizes; see AnchorPip.
            if (_beaconIcon != null) _beaconIcon.CustomMinimumSize = new Vector2(AnchorPip.IconSize, AnchorPip.IconSize);
        }

        private void BuildTopRight() {
            Label dustIcon = GetNodeOrNull<Label>($"{CurrencyPath}/ChronalDustIcon");
            if (dustIcon != null) {
                HideGlyph(dustIcon);
                dustIcon.AddChild(PulpIcon.Create("dust", UIPalette.Res, 38f));
            }
            if (_integrityClock == null) return;
            var face = GetNodeOrNull<Control>($"{TopRightPanelPath}/IntegrityClock/ClockFace");
            if (face != null) {
                _dialBack = new IntegrityDialLayer("dial_back");
                face.AddChild(_dialBack);
            }
            if (_clockWedge != null) {
                _clockWedge.Pie = true;
                _clockWedge.TrackColor = UIPalette.PromptCream;
                _clockWedge.FillColor = UIPalette.Res;
                _dialTicks = new IntegrityDialLayer("dial_ticks");
                _integrityClock.AddChild(_dialTicks);
                _integrityClock.MoveChild(_dialTicks, _clockWedge.GetIndex() + 1);
            }
            _siphonDraw = new SiphonStreamsLayer {
                Position = new Vector2(112f * 0.62f, -112f * 0.36f), Size = new Vector2(87f, 87f), Visible = false
            };
            _integrityClock.AddChild(_siphonDraw);
            _integrityCaption = new Label {
                Name = "IntegrityCaption", Text = "hud_integrity_caption", Uppercase = true,
                ThemeTypeVariation = UIPalette.LabelRoles.SceneCaption, MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Position = new Vector2(-300f, 40f), Size = new Vector2(286f, 32f)
            };
            _integrityClock.AddChild(_integrityCaption);
            _sealedNote = new Label {
                Name = "SealedNote", Text = "hud_integrity_sealed", Visible = false,
                ThemeTypeVariation = UIPalette.LabelRoles.NoteLabel, MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Right, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Position = new Vector2(-228f, 0f), Size = new Vector2(340f, 49f)
            };
            _integrityClock.AddChild(_sealedNote);
        }

        /// <summary>The dial, its streams and its numeral, or (sealed at the pre-boss checkpoint) the note alone.</summary>
        private void SetDialShown(bool shown) {
            if (_dialShown == shown) return;
            _dialShown = shown;
            var face = GetNodeOrNull<Control>($"{TopRightPanelPath}/IntegrityClock/ClockFace");
            if (face != null) face.Visible = shown;
            if (_clockWedge != null) _clockWedge.Visible = shown;
            if (_dialTicks != null) _dialTicks.Visible = shown;
            if (_integrityText != null) _integrityText.Visible = shown;
            if (_integrityCaption != null) _integrityCaption.Visible = shown;
            if (_sealedNote != null) _sealedNote.Visible = !shown;
        }

        private void BuildBoss() {
            if (_bossNameLabel != null) {
                StyleBoxTexture plate = PulpKit.Plate("boss_nameplate");
                if (plate != null) {
                    plate.ContentMarginLeft = 30f;
                    plate.ContentMarginRight = 30f;
                    _bossNameLabel.AddThemeStyleboxOverride("normal", plate);
                }
                _bossNameLabel.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
            }
            if (_bossPhaseTab != null) {
                StyleBoxTexture tab = PulpKit.Plate("boss_phase_tab");
                if (tab != null) {
                    tab.ContentMarginLeft = 14f;
                    tab.ContentMarginRight = 14f;
                    _bossPhaseTab.AddThemeStyleboxOverride("normal", tab);
                }
                _bossPhaseTab.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
            }
            if (_bossBar != null) {
                _bossBar.Modulate = Colors.White;
                _bossDecor = PulpBarDecor.Attach(_bossBar, PulpBarStyle.Boss);
                if (_bossHpValue != null) _bossBar.MoveChild(_bossHpValue, -1);
                if (_bossBar.GetThemeStylebox("fill") is StyleBoxFlat themed) {
                    var fill = (StyleBoxFlat)themed.Duplicate();
                    fill.BgColor = UIPalette.Boss;
                    _bossBar.AddThemeStyleboxOverride("fill", fill);
                }
            }
            if (_bossNotches != null) _bossNotches.DrawNotches = false;
            LayoutBossPanel();
        }

        private void BuildToasts() {
            if (_checkpointToast != null) {
                StyleBoxTexture plate = PulpKit.Plate("tag_panel");
                if (plate != null) {
                    plate.ContentMarginLeft = 22f;
                    plate.ContentMarginRight = 22f;
                    plate.ContentMarginTop = 8f;
                    plate.ContentMarginBottom = 8f;
                    _checkpointToast.AddThemeStyleboxOverride("normal", plate);
                }
                _checkpointToast.AddThemeColorOverride("font_color", UIPalette.LabelDark);
            }
            if (_bossIntroCard != null) {
                StyleBoxTexture plate = PulpKit.Plate("boss_nameplate");
                if (plate != null) {
                    plate.ContentMarginLeft = 40f;
                    plate.ContentMarginRight = 40f;
                    _bossIntroCard.AddThemeStyleboxOverride("normal", plate);
                }
                _bossIntroCard.Rotation = Mathf.DegToRad(-1f);
            }
        }

        // === Refresh helpers ==================================================

        private static readonly System.Collections.Generic.Dictionary<(string, string), string> TimeText = new();

        /// <summary>The "2.0s" chip text, formatted once per (format, seconds) pair: chips refresh every frame.</summary>
        private static string SecondsTime(string format, string seconds) {
            if (!TimeText.TryGetValue((format, seconds), out string text)) {
                text = string.Format(format, seconds);
                TimeText[(format, seconds)] = text;
            }
            return text;
        }

        /// <summary>Writes a chip only when its look changed (every setter relayouts).</summary>
        private static void ApplyChip(PulpStatusChip chip, StatusType status, float remaining, float ring) {
            if (chip == null || !GodotObject.IsInstanceValid(chip)) return;
            if (status == StatusType.None) {
                if (!chip.Empty) {
                    chip.Empty = true;
                    chip.NameText = "";
                    chip.TimeText = "";
                }
                return;
            }
            string time = SecondsTime(chip.Tr("hud_seconds_short"), PulpAbilitySlot.FormatSeconds(remaining));
            string nameKey = HudAbilityIndicatorModel.StatusLabelKey(status);
            if (chip.Empty || chip.GlyphName != HudAbilityIndicatorModel.StatusIconName(status)) {
                chip.Empty = false;
                chip.Kind = HudAbilityIndicatorModel.StatusChipKind(status);
                chip.GlyphName = HudAbilityIndicatorModel.StatusIconName(status);
            }
            if (chip.NameText != nameKey) chip.NameText = nameKey;
            if (chip.TimeText != time) chip.TimeText = time;
            chip.Remaining = ring;
        }

        /// <summary>Maps the model state onto a slot widget, writing only what moved.</summary>
        private static void ApplySlot(PulpAbilitySlot slot, AbilitySlotLockState lockState, bool ready, float readiness, float seconds) {
            if (slot == null || !GodotObject.IsInstanceValid(slot)) return;
            PulpSlotState state = lockState == AbilitySlotLockState.Dormant ? PulpSlotState.Dormant
                : lockState == AbilitySlotLockState.Suppressed ? PulpSlotState.Suppressed
                : ready ? PulpSlotState.Ready
                : slot.Ultimate ? PulpSlotState.Charging : PulpSlotState.Cooling;
            if (slot.State != state) slot.State = state;
            if (!Mathf.IsEqualApprox(slot.Progress, readiness)) slot.Progress = readiness;
            // Whole tenths only: the centre number changes at most ten times a second.
            float tenths = Mathf.Ceil(seconds * 10f) / 10f;
            if (!Mathf.IsEqualApprox(slot.SecondsRemaining, tenths)) slot.SecondsRemaining = tenths;
        }

        private int _shownBossPhase = -1, _shownBossHp = int.MinValue, _shownBossMax = int.MinValue;

        private void RefreshBossTexts() {
            if (_bossPhaseTab != null && _shownBossPhase != _bossPhase) {
                _shownBossPhase = _bossPhase;
                _bossPhaseTab.Text = string.Format(Tr("hud_boss_phase"), _bossPhase);
            }
            if (_bossHpValue != null && _bossBar != null) {
                // Format only when a number moved: this runs on every boss HP update.
                int current = Mathf.RoundToInt((float)_bossBar.Value), maximum = Mathf.RoundToInt((float)_bossBar.MaxValue);
                if (current != _shownBossHp || maximum != _shownBossMax) {
                    _shownBossHp = current; _shownBossMax = maximum;
                    _bossHpValue.Text = $"{current}/{maximum}";
                }
            }
        }

        // === Fit ==============================================================

        /// <summary>
        /// Lays the boss panel out inside the free band between the left cluster and the right one, at most
        /// <see cref="BossBarMaxWidth"/> wide, so it overlaps neither at any UI scale (ruling 8).
        /// </summary>
        private void LayoutBossPanel() {
            if (_bossPanel == null || _root == null) return;
            float space = _root.Size.X > 1f ? _root.Size.X : 1920f;
            float left = LeftClusterRight + BossGap;
            float right = space - RightClusterWidth - BossGap + BossGap;
            float width = Mathf.Clamp(right - left, BossBarMinWidth, BossBarMaxWidth);
            float x = left + Mathf.Max(0f, right - left - width) / 2f;
            const float PlateHeight = 62f;
            const float TabHeight = 35f;
            const float BarHeight = 44f;
            const float TabWidth = 128f;
            _bossPanel.Position = new Vector2(x, 22f);
            _bossPanel.Size = new Vector2(width, PlateHeight + 10f + BarHeight);
            if (_bossBar != null) {
                _bossBar.Position = new Vector2(0f, PlateHeight + 10f);
                _bossBar.Size = new Vector2(width, BarHeight);
                _bossBar.CustomMinimumSize = new Vector2(width, BarHeight);
            }
            float tabWidth = width < 520f ? 104f : TabWidth;
            // The tab never narrower than its own text, so it cannot poke past the bar's right edge.
            if (_bossPhaseTab != null) tabWidth = Mathf.Max(tabWidth, _bossPhaseTab.GetCombinedMinimumSize().X);
            if (_bossPhaseTab != null) {
                _bossPhaseTab.Size = new Vector2(tabWidth, TabHeight);
                _bossPhaseTab.Position = new Vector2(width - tabWidth, PlateHeight - TabHeight + 2f);
                _bossPhaseTab.PivotOffset = _bossPhaseTab.Size / 2f;
                _bossPhaseTab.Rotation = Mathf.DegToRad(2f);
            }
            if (_bossNameLabel != null) {
                float available = Mathf.Min(687f, width - tabWidth - 12f);
                float natural = Mathf.Max(available, _bossNameLabel.GetCombinedMinimumSize().X);
                float shrink = Mathf.Clamp(available / natural, 0.5f, 1f);
                _bossNameLabel.Size = new Vector2(natural, PlateHeight);
                _bossNameLabel.PivotOffset = Vector2.Zero;
                _bossNameLabel.Position = new Vector2(0f, 0f);
                _bossNameLabel.Scale = new Vector2(shrink, shrink);
                _bossNameLabel.Rotation = Mathf.DegToRad(-1f);
            }
        }

        /// <summary>
        /// Per-corner fit: the root scale comes from <see cref="UiScaleBinder"/>; here the boss panel is
        /// re-laid when the scale (and so the free band) changes.
        /// </summary>
        private void ApplyFit(bool force = false) {
            float scale = _uiScale.AppliedScale;
            if (!force && Mathf.IsEqualApprox(scale, _fitScale)) return;
            _fitScale = scale;
            LayoutBossPanel();
        }
    }
}
