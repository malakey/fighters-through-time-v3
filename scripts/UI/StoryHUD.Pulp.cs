using FTT.Core;
using FTT.UI.Motion;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// TG-HUDS (Temporal Glass rebuild, 2026-10-27; first written by UR-HUDS for the pulp HUD, the file keeps its legacy
    /// name), made compact the same day (HUD-COMPACT, the user's request: one slab in place of three, everything smaller;
    /// docs/handoffs/TG_HUDS_COMPACT.md). The HUD-S1 corner cluster and the BOSS-A boss bar in glass: the glass
    /// widgets that sit on the authored glyph Labels, the Vitals and value layouts, the thin-ring Integrity dial, the boss
    /// bar's band and its D11 name fit, the checkpoint toast and the boss intro card. Presentation only; every value is
    /// written by <see cref="StoryHUD"/>'s refreshers. The authored glyph Labels are kept with their Text and TooltipText
    /// (tests and accessibility read them) and draw nothing (<c>VisibleCharacters = 0</c>); the widget is their child.
    /// </summary>
    public partial class StoryHUD {

        /// <summary>HP share at or under which the bar reads low (the shipped HUD's rule; UI_SPEC_GLASS 3.4.1 keeps it).</summary>
        public const float LowHpFraction = 0.25f;

        // ---- The boss bar (UI_SPEC_GLASS 3.6, 4.4) ----

        /// <summary>The boss bar's widest (640 at 100 %; 840 until the compact HUD) and, for very narrow canvases only, narrowest width.</summary>
        public const float BossBarMaxWidth = 640f;
        public const float BossBarMinWidth = 320f;
        /// <summary>
        /// The slab's right edge (24 + 484) and the right cluster's reach from the right edge: the widest dust plate the
        /// compact HUD draws (188 px for "1,250 +25", with room for about one more digit) plus its 24 px margin.
        /// </summary>
        public const float LeftClusterRight = 508f, DustPlateReach = 220f;
        /// <summary>The free band keeps 24 px from the slab and from the dust plate: width = min(640, canvas - 776).</summary>
        public const float BossGap = 24f;
        /// <summary>The bar's top (the slab's and the dust plate's top line, y 20); it is centred on the canvas when the band allows.</summary>
        public const float BossTop = 20f;
        /// <summary>The header row (T30 at 26, line height 1), the gap to the tube and the boss tube's height (20; 34 before).</summary>
        public const float BossHeaderHeight = 26f, BossTubeGap = 7f, BossTubeHeight = 20f;
        /// <summary>The name diamond: 4 px of left padding, a 10 px square turned 45 degrees, 12 px to the name, 4 px above the baseline.</summary>
        public const float BossDiamondInset = 4f, BossDiamondSize = 10f, BossNameGap = 12f, BossDiamondLift = 4f;
        /// <summary>The boss tube's lean (10 on its 20 px height) and its phase notches' scale (a 9 px diamond).</summary>
        public const float BossTubeRun = 10f, BossNotchScale = 0.75f;
        /// <summary>The least gap between the name and the phase label on one row (the D11 measurement's 24 px).</summary>
        public const float BossPhaseGap = 24f;
        /// <summary>A dropped phase label: its gap below the name and the gap from it to the tube.</summary>
        public const float BossPhaseDropGap = 4f, BossDroppedTubeGap = 6f;
        /// <summary>The boss name's size at 100 % (T30 at 26) and its D11 floor in pixels, kept at 22.8 px on screen.</summary>
        public const float BossNameSize = 26f, BossNameFloorPixels = 22.8f;
        /// <summary>The boss value's right padding inside the tube and the gap between the numeral and its "/max".</summary>
        public const float BossValueRightPad = 12f, ValueMaxGap = 2f;

        /// <summary>
        /// D11's floor as the name label's scale at UI scale <paramref name="uiScale"/>: 22.8 px on screen, so 0.88 of the
        /// 26 px name at 100 % and 0.63 at 140 % (the HUD canvas is already magnified by the UI scale).
        /// </summary>
        public static float BossNameFloorScale(float uiScale) =>
            Mathf.Min(1f, BossNameFloorPixels / (BossNameSize * Mathf.Max(0.01f, uiScale)));

        /// <summary>
        /// The free band between the left stack and the right cluster, 24 from each, in the HUD's canvas units for a canvas
        /// <paramref name="canvasWidth"/> wide (on screen, times the UI scale): the boss bar's band, the checkpoint toast's
        /// and (TG-CLOSE) the first-use tooltip's.
        /// </summary>
        public static (float Left, float Right) FreeBand(float canvasWidth) =>
            (LeftClusterRight + BossGap, canvasWidth - DustPlateReach - BossGap);

        /// <summary>The boss bar's width in canvas units for a canvas <paramref name="canvasWidth"/> wide: min(640, canvas - 776), floored at 320.</summary>
        public static float BossBarWidthFor(float canvasWidth) =>
            Mathf.Clamp(canvasWidth - LeftClusterRight - DustPlateReach - 2f * BossGap, BossBarMinWidth, BossBarMaxWidth);

        // ---- The Vitals panel and the Integrity dial (UI_SPEC_GLASS 3.4.1, 3.4.3, 3.4.4, 4.4) ----

        /// <summary>The hero name's box (T08, line height 1, 22 px) and its width; a longer name shrinks to its 18 px floor.</summary>
        public const float HeroNameBox = 22f, HeroNameWidth = 380f, HeroNameFloorPixels = 18f;
        /// <summary>The HP value's right padding inside the tube (11 on the compact 22 px tube).</summary>
        public const float HpValueRightPad = 11f;
        /// <summary>The status chips start at the vitals column's left edge (the compact HUD drops the old 4 px inset).</summary>
        public const float ChipInset = 0f;

        // ---- the compact slab (2026-10-27; the mock's .hc-* rules) ----

        /// <summary>The slab's size at 100 % (484 wide, about 182 tall; it grows a few pixels when Act III's anchors show).</summary>
        public const float HudPanelWidth = 484f;
        /// <summary>The portrait frame (68; 112 before).</summary>
        public const float PortraitSize = 68f;
        /// <summary>The HP tube (296 x 22; 380 x 30 before) and its lean (9).</summary>
        public const float HpTubeWidth = 296f, HpTubeHeight = 22f, HpTubeRun = 9f;
        /// <summary>The status badges (30; 44 before), their duration ring (2.5 px, its outer edge 4 px out) and their text gaps.</summary>
        public const float ChipBadgeSize = 30f, ChipRingWidth = 2.5f, ChipRingOutset = 4f, ChipTextGap = 9f;
        public const int ChipColumnGap = 2, ChipGlowSize = 10;
        /// <summary>The status row's height: the 30 px badge beside its two 18 px lines.</summary>
        public const float ChipRowHeight = 38f;
        /// <summary>The ability lenses (48; 74 before), their ring (2.5 px, its outer edge 5 px out) and their centre seconds (18 px).</summary>
        public const float AbilityLensSize = 48f, AbilityRingWidth = 2.5f, AbilityRingOutset = 5f, AbilityCenterSize = 18f;
        /// <summary>The Ultimate lens (64; 92 before), its ring (4.5 px, its outer edge 7 px out: a 78 px box) and its centre percent (21 px).</summary>
        public const float UltimateLensSize = 64f, UltimateRingWidth = 4.5f, UltimateRingOutset = 7f, UltimateCenterSize = 21f;
        /// <summary>The Defy seal's badge on the Ultimate ring (a 28 px dark disc, the seal glyph at 20 px).</summary>
        public const float DefyBadgeSize = 28f, DefyGlyphSize = 20f;
        /// <summary>The time tools' badges (26; 46 before) and their ring (2 px, its outer edge 3 px out).</summary>
        public const float TimeBadgeSize = 26f, TimeBadgeRingWidth = 2f, TimeBadgeRingOutset = 3f;
        /// <summary>The hairline before the time tools: 1.5 px wide, 58 tall, fading at both ends.</summary>
        public const float DividerWidth = 1.5f, DividerHeight = 58f;
        /// <summary>The level title row's gap under the slab and its indent (12 and 8).</summary>
        public const float TitleRowGap = 12f, TitleRowIndent = 8f;
        /// <summary>The objective's flag (18 px; 22 before) and its gap to the text (8).</summary>
        public const float ObjectiveFlagSize = 18f, ObjectiveFlagGap = 8f;
        /// <summary>The dust plate's glyph (26; 38 before).</summary>
        public const float DustGlyphSize = 26f;
        /// <summary>The Integrity dial's diameter (64; 112 before) and its wedge annulus at that size (the G9 shares of the dial).</summary>
        public const float IntegrityDialDiameter = 64f;
        public const float WedgeOuterRadius = IntegrityDialDiameter / IntegrityDialLayer.DialSize * IntegrityDialLayer.WedgeOuterRadius;
        public const float WedgeInnerRadius = WedgeOuterRadius * 0.53f;
        /// <summary>The wedge annulus's drawn alpha (rgba(glow, .85)) and the frozen wedge's (ink2 @0.6).</summary>
        public const float WedgeAlpha = 0.85f, FrozenWedgeAlpha = 0.6f;
        /// <summary>The caption's gap to the dial's left edge, and the sealed note's box (right-aligned under the dust plate).</summary>
        public const float CaptionGap = 12f, SealedNoteWidth = 340f, SealedNoteTop = -6f;
        /// <summary>The checkpoint toast's flag (bold, res2) and its gap to the text (9.2).</summary>
        public const float ToastFlagSize = 22f, ToastFlagGap = 10f;
        /// <summary>The toast holds 2 s after its fade-rise and then fades over 300 ms (9.2, agent choice).</summary>
        public const float ToastHoldSeconds = 2f, ToastFadeSeconds = 0.3f;
        /// <summary>The boss intro card's flourish: two 240 px rules, 16 px gaps, the 12 px boss-red diamond.</summary>
        public const float IntroRuleLength = 240f, IntroFlourishGap = 16f, IntroFlourishLift = 20f;
        /// <summary>The intro card's margin from each side of the screen (its anchors in the scene).</summary>
        public const float IntroCardMargin = 40f;

        private static readonly StringName FontName = "font", FontSizeName = "font_size", FontColorName = "font_color";
        private static readonly StringName NormalStyleName = "normal", PanelStyleName = "panel";

        private PulpBarDecor _hpDecor;
        private StyleBoxFlat _hpFill;
        private Label _hpMax;
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
        private TextureRect _bossDiamond;
        private Label _bossHpMax;
        private int _bossPhase = 1;
        private bool _bossPhaseDropped;
        private bool _hpSeeded;
        private int _hpPresented, _hpPresentedMax;
        private SiphonStreamsLayer _siphonDraw;
        private Label _integrityCaption;
        private Label _sealedNote;
        private Control _clockFace;
        private IntegrityDialLayer _dialFace;
        private IntegrityDialLayer _dialTicks;
        private PanelContainer _hudPanel;
        private HudCapsule _specialsCapsule;
        private HudCapsule _movementCapsule;
        private Control _timeDivider;
        private HudBadge _defyBadge;
        private PulpIcon _toastFlag;
        private GlassFlourish _introFlourish;
        private Label _dustFloatRise;
        private bool _dialShown = true;
        private float _fitScale = float.NaN;

        /// <summary>The one glass slab (<c>GlassPanelHud</c>), or null before the scene resolves. Test seam.</summary>
        public PanelContainer HudPanel => _hudPanel;

        /// <summary>The capsule holding Special 1 and Special 2, and the one holding the Movement ability. Test seam.</summary>
        public HudCapsule SpecialsCapsule => _specialsCapsule;
        public HudCapsule MovementCapsule => _movementCapsule;

        /// <summary>The hairline before the time tools. Test seam.</summary>
        public Control TimeDivider => _timeDivider;

        /// <summary>The Defy seal's badge on the Ultimate ring. Test seam.</summary>
        public HudBadge DefyBadge => _defyBadge;

        /// <summary>The glass lenses (Special 1, Special 2, Movement). Test seam.</summary>
        public System.Collections.Generic.IReadOnlyList<PulpAbilitySlot> SlotWidgets => _slots;

        /// <summary>The Ultimate's lens. Test seam.</summary>
        public PulpAbilitySlot UltimateSlotWidget => _ultSlot;

        /// <summary>The two status chips, damage then control. Test seam.</summary>
        public (PulpStatusChip Damage, PulpStatusChip Control) StatusChips => (_damageChip, _controlChip);

        /// <summary>The HP tube. Test seam.</summary>
        public PulpBarDecor HpDecor => _hpDecor;

        /// <summary>The boss tube. Test seam.</summary>
        public PulpBarDecor BossDecor => _bossDecor;

        /// <summary>The boss name's diamond (the kit's 12 px diamond, boss red). Test seam.</summary>
        public TextureRect BossDiamond => _bossDiamond;

        /// <summary>True while the phase label sits under the boss name (the D11 drop). Test seam.</summary>
        public bool BossPhaseDropped => _bossPhaseDropped;

        /// <summary>The siphon stream layer. Test seam.</summary>
        public SiphonStreamsLayer SiphonDraw => _siphonDraw;

        /// <summary>The dial's face and tick layers. Test seam.</summary>
        public IntegrityDialLayer DialFace => _dialFace;
        public IntegrityDialLayer DialTicks => _dialTicks;

        /// <summary>The sealed-dial note and the dial caption. Test seam.</summary>
        public Label SealedNote => _sealedNote;
        public Label IntegrityCaption => _integrityCaption;

        /// <summary>True while the dial (not the sealed note) shows. Test seam.</summary>
        public bool DialShown => _dialShown;

        /// <summary>The phase the boss label shows (1 on reveal). Test seam.</summary>
        public int BossPhase => _bossPhase;

        /// <summary>The checkpoint toast's flag and the boss intro card's flourish. Test seam.</summary>
        public PulpIcon ToastFlag => _toastFlag;
        public GlassFlourish IntroFlourish => _introFlourish;

        private static void HideGlyph(Label label) {
            if (label != null) label.VisibleCharacters = 0;
        }

        private void BuildPulpWidgets() {
            // The one slab (no panel tags: the compact HUD dropped VITALS and ABILITIES) and the dust plate wear the glass
            // sheen and streaks (3.1): over the slab, under the content.
            _hudPanel = GetNodeOrNull<PanelContainer>(HudPanelPath);
            _specialsCapsule = GetNodeOrNull<HudCapsule>(SpecialsCapsulePath);
            _movementCapsule = GetNodeOrNull<HudCapsule>(MovementCapsulePath);
            PulpScreenKit.DressPanel(_hudPanel);
            PulpScreenKit.DressPanel(GetNodeOrNull<PanelContainer>($"{TopRightPanelPath}/DustPlate"));
            if (_hudPanel != null) _hudPanel.Resized += PlaceTitleRow;

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
            // The flag sits in a left margin of the objective's own text box (18 + 8), so the label keeps its path and its
            // Text for the level controllers and their tests.
            var margin = new StyleBoxEmpty { ContentMarginLeft = ObjectiveFlagSize + ObjectiveFlagGap };
            _objectiveLabel.AddThemeStyleboxOverride(NormalStyleName, margin);
            _objectiveFlag = PulpIcon.Create("flag", UIPalette.Res2, ObjectiveFlagSize);
            _objectiveFlag.Visible = false;
            _objectiveLabel.AddChild(_objectiveFlag);
            _objectiveFlag.AnchorTop = 0.5f;
            _objectiveFlag.AnchorBottom = 0.5f;
            _objectiveFlag.OffsetLeft = 0f;
            _objectiveFlag.OffsetRight = ObjectiveFlagSize;
            _objectiveFlag.OffsetTop = -ObjectiveFlagSize / 2f;
            _objectiveFlag.OffsetBottom = ObjectiveFlagSize / 2f;
        }

        /// <summary>
        /// The level title and objective sit 12 px under the slab, indented 8 (the mock's .hc-loc). The slab does not move
        /// when they hide (a boss fight): they are a separate node placed from the slab's rect whenever the slab resizes.
        /// </summary>
        private void PlaceTitleRow() {
            if (_topLeft == null || _hudPanel == null) return;
            Control stack = _hudPanel.GetParent<Control>();
            Vector2 origin = stack != null ? stack.Position + _hudPanel.Position : _hudPanel.Position;
            var at = new Vector2(origin.X + TitleRowIndent, origin.Y + _hudPanel.Size.Y + TitleRowGap);
            if (_topLeft.Position != at) _topLeft.Position = at;
        }

        private void BuildVitals() {
            Control host = _portrait?.GetParent<Control>();
            if (host != null) {
                _roundel = new PulpPortraitRoundel { Diameter = PortraitSize };
                host.AddChild(_roundel);
                host.MoveChild(_roundel, 0);
            }
            if (_hpBar != null) {
                _hpDecor = PulpBarDecor.Attach(_hpBar);
                _hpDecor.RunOverride = HpTubeRun;
                _hpDecor.SnapLagTo((float)_hpBar.Value);
                // The decor is the bar's last child; the numerals sit above it.
                if (_hpText != null) _hpBar.MoveChild(_hpText, -1);
                if (_hpMax != null) _hpBar.MoveChild(_hpMax, -1);
                if (_hpBar.GetThemeStylebox("fill") is StyleBoxFlat themed) {
                    // The tube reads its fill colour from here (UIPalette.Hp: the hp2 -> hp gradient).
                    _hpFill = (StyleBoxFlat)themed.Duplicate();
                    _hpFill.BgColor = UIPalette.Hp;
                    _hpBar.AddThemeStyleboxOverride("fill", _hpFill);
                }
            }
            if (_echoBand != null) _echoBand.Visible = false;
            _damageChip = AddChip(_damageStatusIndicator);
            _controlChip = AddChip(_controlStatusIndicator);
            SizeChipHosts();
            LayoutHeroName();
        }

        private static PulpStatusChip AddChip(Label host) {
            if (host == null) return null;
            HideGlyph(host);
            // The compact badge: 30 px with a 2.5 px ring 4 px out, the name in sentence case over the time, both 18 px.
            var chip = new PulpStatusChip {
                BadgeSize = ChipBadgeSize, Empty = true, ShowRing = true, RingThickness = ChipRingWidth, RingOutsetPixels = ChipRingOutset,
                TextGapPixels = ChipTextGap, ColumnGapPixels = ChipColumnGap, GlowSize = ChipGlowSize, UppercaseName = false,
                NameRole = UIPalette.LabelRoles.HudStatusName, TimeRole = UIPalette.LabelRoles.HudStatusTime
            };
            host.AddChild(chip);
            // The chip fills its host's 38 px row, so the badge centres in it beside the two lines.
            chip.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            chip.OffsetLeft = ChipInset;
            return chip;
        }

        /// <summary>
        /// Fixed chip positions (HUD_CONTRACT): each host is as wide as its slot's widest status name in the current
        /// language (sentence case, the compact HUD's 18 px name), and the row keeps 16 px between the two hosts.
        /// </summary>
        private void SizeChipHosts() {
            if (_damageStatusIndicator == null || _controlStatusIndicator == null) return;
            Font font = _damageStatusIndicator.GetThemeFont(FontName, UIPalette.LabelRoles.HudStatusName);
            int size = _damageStatusIndicator.GetThemeFontSize(FontSizeName, UIPalette.LabelRoles.HudStatusName);
            if (font == null) return;
            float damage = 0f, control = 0f;
            foreach (StatusType type in System.Enum.GetValues<StatusType>()) {
                if (type == StatusType.None) continue;
                string name = Tr(HudAbilityIndicatorModel.StatusLabelKey(type));
                float width = font.GetStringSize(name, HorizontalAlignment.Left, -1f, size).X;
                if (HudAbilityIndicatorModel.IsDamageStatus(type)) damage = Mathf.Max(damage, width);
                else control = Mathf.Max(control, width);
            }
            float chip = ChipBadgeSize + ChipTextGap;
            _damageStatusIndicator.CustomMinimumSize = new Vector2(Mathf.Ceil(ChipInset + chip + damage), ChipRowHeight);
            _controlStatusIndicator.CustomMinimumSize = new Vector2(Mathf.Ceil(ChipInset + chip + control), ChipRowHeight);
        }

        /// <summary>
        /// The hero name (T08) placed so its glyphs sit where the CSS line box puts them (a 22 px box at the Vitals top), and
        /// shrunk about its baseline, never below 18 px, when a long name would run past the tube's 380 px.
        /// </summary>
        private void LayoutHeroName() {
            if (_heroName == null) return;
            Font font = _heroName.GetThemeFont(FontName);
            int size = _heroName.GetThemeFontSize(FontSizeName);
            if (font == null || size <= 0) return;
            float line = font.GetHeight(size), ascent = font.GetAscent(size);
            _heroName.ClipText = false;
            _heroName.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
            float natural = _heroName.GetCombinedMinimumSize().X;
            float floor = Mathf.Min(1f, HeroNameFloorPixels / size);
            float scale = natural > HeroNameWidth ? Mathf.Max(floor, HeroNameWidth / natural) : 1f;
            bool clip = natural * scale > HeroNameWidth + 0.5f;
            _heroName.ClipText = clip;
            _heroName.TextOverrunBehavior = clip ? TextServer.OverrunBehavior.TrimEllipsis : TextServer.OverrunBehavior.NoTrimming;
            _heroName.Size = new Vector2(clip ? HeroNameWidth / scale : Mathf.Max(natural, 1f), line);
            _heroName.PivotOffset = new Vector2(0f, ascent);
            _heroName.Position = new Vector2(0f, (HeroNameBox - line) / 2f);
            _heroName.Scale = new Vector2(scale, scale);
        }

        private void BuildAbilities() {
            for (int index = 0; index < CooldownSlots.Length; index++) {
                Label icon = _cooldownIcons[index];
                if (icon == null) continue;
                HideGlyph(icon);
                HideGlyph(_cooldownLockOverlays[index]);
                if (_cooldownRadials[index] != null) _cooldownRadials[index].Visible = false;
                // The compact lens: 48 px, no key label (the capsules group them), seconds at 18 px.
                _slots[index] = new PulpAbilitySlot {
                    SlotSize = AbilityLensSize, KeyText = "", RingWidthOverride = AbilityRingWidth, RingOutsetOverride = AbilityRingOutset,
                    CenterTextSize = AbilityCenterSize, EffectScale = AbilityLensSize / 74f
                };
                icon.AddChild(_slots[index]);
                icon.MoveChild(_slots[index], 0);
            }
            HideGlyph(_meterLockOverlay);
            if (_meterHost != null) {
                // The host is the 64 px lens's layout box; the slot's 78 px ring box hangs 7 px outside it. No "ULT" label;
                // the ready state keeps its full gold ring plus the READY pill over the lens's bottom.
                _ultSlot = new PulpAbilitySlot {
                    Ultimate = true, UltimateLensSize = UltimateLensSize, RingWidthOverride = UltimateRingWidth,
                    RingOutsetOverride = UltimateRingOutset, CenterTextSize = UltimateCenterSize,
                    EffectScale = UltimateLensSize / PulpAbilitySlot.UltimateFrame, ReadyPill = true, KeyText = ""
                };
                _meterHost.AddChild(_ultSlot);
                _meterHost.MoveChild(_ultSlot, 0);
                _ultSlot.Position = -_ultSlot.LensOrigin;
            }
            if (_defySeal != null) {
                // The Defy seal as a badge on the Ultimate ring's upper right (it lights at full meter): a 28 px dark glass
                // disc with its 1.5 px edge and the seal glyph at 20 px; its four shape states as before.
                HideGlyph(_defySeal);
                _defyBadge = new HudBadge {
                    Name = "DefyBadge", BadgeSize = DefyBadgeSize, IconShare = DefyGlyphSize / DefyBadgeSize,
                    IconName = "defy_notready", Tint = UIPalette.Ink3
                };
                _defySeal.AddChild(_defyBadge);
                _defyIcon = _defyBadge.Glyph;
            }
        }

        private void BuildTimeTools() {
            if (_rewindIcon != null) {
                HideGlyph(_rewindIcon);
                _rewindBadge = new HudBadge { IconName = "hourglass", Tint = UIPalette.Accent2, BadgeSize = TimeBadgeSize };
                _rewindIcon.AddChild(_rewindBadge);
            }
            if (_freezeIcon != null) {
                HideGlyph(_freezeIcon);
                _freezeBadge = new HudBadge {
                    IconName = "freeze", Tint = UIPalette.Accent2, BadgeSize = TimeBadgeSize,
                    RingThickness = TimeBadgeRingWidth, RingOutsetPixels = TimeBadgeRingOutset
                };
                _freezeIcon.AddChild(_freezeBadge);
            }
            if (_beaconIcon != null) _beaconIcon.CustomMinimumSize = new Vector2(AnchorPip.IconSize, AnchorPip.IconSize);
            // The hairline before the time tools: 1.5 px of the gold glow at .55, fading to nothing at both ends.
            _timeDivider = GetNodeOrNull<Control>($"{AbilitiesPath}/TimeDivider");
            if (_timeDivider != null) {
                var gradient = new Gradient {
                    Offsets = new[] { 0f, 0.25f, 0.75f, 1f },
                    Colors = new[] {
                        new Color(UIPalette.Accent, 0f), new Color(UIPalette.Accent, 0.55f),
                        new Color(UIPalette.Accent, 0.55f), new Color(UIPalette.Accent, 0f)
                    }
                };
                var line = new TextureRect {
                    Name = "Hairline", MouseFilter = Control.MouseFilterEnum.Ignore,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                    Texture = new GradientTexture2D {
                        Gradient = gradient, Width = 1, Height = 64, FillFrom = new Vector2(0f, 0f), FillTo = new Vector2(0f, 1f)
                    },
                    Position = new Vector2((_timeDivider.CustomMinimumSize.X - DividerWidth) / 2f, 0f),
                    Size = new Vector2(DividerWidth, DividerHeight)
                };
                _timeDivider.AddChild(line);
            }
        }

        private void BuildTopRight() {
            Label dustIcon = GetNodeOrNull<Label>($"{CurrencyPath}/ChronalDustIcon");
            if (dustIcon != null) {
                HideGlyph(dustIcon);
                dustIcon.AddChild(PulpIcon.Create("dust", UIPalette.Res, DustGlyphSize));
            }
            BuildDustFloat();
            if (_integrityClock == null) return;
            _clockFace = GetNodeOrNull<Control>($"{TopRightPanelPath}/IntegrityClock/ClockFace");
            if (_clockFace != null) {
                _dialFace = new IntegrityDialLayer(IntegrityDialLayer.DialPart.Face) {
                    CustomMinimumSize = new Vector2(IntegrityDialDiameter, IntegrityDialDiameter)
                };
                _clockFace.AddChild(_dialFace);
                _dialFace.Size = new Vector2(IntegrityDialDiameter, IntegrityDialDiameter);
            }
            if (_clockWedge != null) {
                // The wedge annulus (3.4.4, at the 64 px dial: radius 13.23 to 24.96), clockwise from 12 o'clock; its alpha
                // lives on SelfModulate so FillColor keeps the semantic colour (gold, danger below the tremor threshold, ink2 frozen).
                _clockWedge.Pie = false;
                _clockWedge.Thickness = WedgeOuterRadius - WedgeInnerRadius;
                _clockWedge.Segments = 96;
                _clockWedge.GradientMode = RadialGradientMode.None;
                _clockWedge.TrackColor = new Color(UIPalette.WedgeRemainder, UIPalette.WedgeRemainder.A / WedgeAlpha);
                _clockWedge.FillColor = UIPalette.Res;
                _clockWedge.SelfModulate = new Color(1f, 1f, 1f, WedgeAlpha);
                _dialTicks = new IntegrityDialLayer(IntegrityDialLayer.DialPart.Ticks) {
                    CustomMinimumSize = new Vector2(IntegrityDialDiameter, IntegrityDialDiameter)
                };
                _integrityClock.AddChild(_dialTicks);
                _dialTicks.Size = new Vector2(IntegrityDialDiameter, IntegrityDialDiameter);
                _integrityClock.MoveChild(_dialTicks, _clockWedge.GetIndex() + 1);
            }
            float dialShare = IntegrityDialDiameter / IntegrityDialLayer.DialSize;
            _siphonDraw = new SiphonStreamsLayer {
                Position = new Vector2(IntegrityDialDiameter * 0.62f, -IntegrityDialDiameter * 0.36f),
                Size = new Vector2(SiphonStreamsLayer.BoxSize * dialShare, SiphonStreamsLayer.BoxSize * dialShare)
            };
            _integrityClock.AddChild(_siphonDraw);
            const float captionWidth = 320f, captionHeight = 40f;
            _integrityCaption = new Label {
                Name = "IntegrityCaption", Text = "hud_integrity_caption", Uppercase = true,
                ThemeTypeVariation = UIPalette.LabelRoles.IntegrityCaption, MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Position = new Vector2(-CaptionGap - captionWidth, IntegrityDialDiameter / 2f - captionHeight / 2f),
                Size = new Vector2(captionWidth, captionHeight)
            };
            _integrityClock.AddChild(_integrityCaption);
            float clockWidth = _integrityClock.CustomMinimumSize.X;
            _sealedNote = new Label {
                Name = "SealedNote", Text = "hud_integrity_sealed", Visible = false, Uppercase = true,
                ThemeTypeVariation = UIPalette.LabelRoles.NoteLabel, MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Right, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Position = new Vector2(clockWidth - SealedNoteWidth, SealedNoteTop), Size = new Vector2(SealedNoteWidth, 50f)
            };
            _integrityClock.AddChild(_sealedNote);
        }

        /// <summary>
        /// The "+N" pickup float (T27, MOT-A row 13). The authored label stays in the dust plate's row for the layout, its
        /// path and its text, and draws nothing itself (SelfModulate alpha 0): a child label draws the text so it can rise
        /// 12 px while it fades in (a container would reset the authored label's own position).
        /// </summary>
        private void BuildDustFloat() {
            if (_dustFloat == null) return;
            var margin = new StyleBoxEmpty { ContentMarginLeft = 2f };
            _dustFloat.AddThemeStyleboxOverride(NormalStyleName, margin);
            _dustFloat.SelfModulate = new Color(1f, 1f, 1f, 0f);
            _dustFloatRise = new Label {
                Name = "Rise", ThemeTypeVariation = UIPalette.LabelRoles.DustFloat, MouseFilter = Control.MouseFilterEnum.Ignore,
                VerticalAlignment = VerticalAlignment.Center
            };
            _dustFloatRise.AddThemeStyleboxOverride(NormalStyleName, margin);
            _dustFloat.AddChild(_dustFloatRise);
            _dustFloatRise.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }

        /// <summary>The dial, its streams, caption and numeral, or (sealed at the pre-boss checkpoint) the note alone (3.4.4).</summary>
        private void SetDialShown(bool shown) {
            if (_dialShown == shown) return;
            _dialShown = shown;
            if (_clockFace != null) _clockFace.Visible = shown;
            if (_clockWedge != null) _clockWedge.Visible = shown;
            if (_dialTicks != null) _dialTicks.Visible = shown;
            if (_integrityText != null) _integrityText.Visible = shown;
            if (_integrityCaption != null) _integrityCaption.Visible = shown;
            if (_siphonDraw != null && !shown) _siphonDraw.Visible = false;
            if (_sealedNote != null) _sealedNote.Visible = !shown;
        }

        private void BuildBoss() {
            if (_bossNameLabel != null) _bossNameLabel.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
            if (_bossPanel != null) {
                // The boss-red diamond before the name (3.6): the kit's 12 px diamond with its 12 px glow.
                _bossDiamond = new TextureRect {
                    Name = "BossDiamond", Texture = PulpKit.Texture("glass_diamond_12"), Modulate = UIPalette.Boss,
                    MouseFilter = Control.MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
                    // The kit's 12 px diamond drawn at the compact 10 px (its glow canvas scaled with it).
                    Size = new Vector2(GlassFlourish.DiamondCanvas, GlassFlourish.DiamondCanvas) * (BossDiamondSize / 12f)
                };
                _bossPanel.AddChild(_bossDiamond);
                _bossPanel.MoveChild(_bossDiamond, 0);
            }
            if (_bossBar != null) {
                _bossBar.Modulate = Colors.White;
                _bossDecor = PulpBarDecor.Attach(_bossBar, PulpBarStyle.Boss);
                _bossDecor.RunOverride = BossTubeRun;
                _bossDecor.NotchScale = BossNotchScale;
                if (_bossHpValue != null) _bossBar.MoveChild(_bossHpValue, -1);
                if (_bossHpMax != null) _bossBar.MoveChild(_bossHpMax, -1);
            }
            // The glass tube draws the notches (a white line with a gold diamond); the overlay keeps the thresholds only.
            if (_bossNotches != null) _bossNotches.DrawNotches = false;
            LayoutBossPanel();
        }

        private void BuildToasts() {
            if (_checkpointToast != null) {
                // 9.2: the glass tag plate (G2), top-centre at y 140, the bold res2 flag 10 px before the text (T14 in ink).
                if (_checkpointToast.GetThemeStylebox(PanelStyleName, UIPalette.Variations.PanelTagPlate) is StyleBox tag) {
                    var plate = (StyleBox)tag.Duplicate();
                    float left = Mathf.Max(0f, tag.GetContentMargin(Side.Left));
                    plate.ContentMarginLeft = left + ToastFlagSize + ToastFlagGap;
                    _checkpointToast.AddThemeStyleboxOverride(NormalStyleName, plate);
                    _toastFlag = PulpIcon.Create("flag", UIPalette.Res2, ToastFlagSize);
                    _toastFlag.Name = "ToastFlag";
                    _checkpointToast.AddChild(_toastFlag);
                    _toastFlag.AnchorTop = 0.5f;
                    _toastFlag.AnchorBottom = 0.5f;
                    _toastFlag.OffsetLeft = left;
                    _toastFlag.OffsetRight = left + ToastFlagSize;
                    _toastFlag.OffsetTop = -ToastFlagSize / 2f;
                    _toastFlag.OffsetBottom = ToastFlagSize / 2f;
                }
            }
            if (_bossIntroCard != null) {
                // 9.2: a flourish (two 240 px rules, the boss-red diamond in the middle) above the name in T04, centred.
                _introFlourish = GlassFlourish.Create(GlassFlourish.FlourishShape.RuleDiamondRule, IntroRuleLength, IntroFlourishGap);
                _introFlourish.Name = "IntroFlourish";
                _introFlourish.DiamondSize = 12;
                _introFlourish.DiamondTint = UIPalette.Boss;
                float width = 2f * IntroRuleLength + 2f * IntroFlourishGap + _introFlourish.DiamondSize;
                _introFlourish.Size = new Vector2(width, _introFlourish.DiamondSize);
                _bossIntroCard.AddChild(_introFlourish);
                _bossIntroCard.Rotation = 0f;
            }
        }

        /// <summary>Centres the intro card's flourish above its text and shrinks a name too wide for the screen.</summary>
        private void LayoutIntroCard() {
            if (_bossIntroCard == null || _introFlourish == null) return;
            Font font = _bossIntroCard.GetThemeFont(FontName);
            int size = _bossIntroCard.GetThemeFontSize(FontSizeName);
            if (font == null || size <= 0) return;
            float line = font.GetHeight(size), ascent = font.GetAscent(size);
            // The card's anchored width is the canvas less 40 px a side; a label grows past its anchors to fit its text,
            // so the fit is measured against the anchors, not the label's own (grown) size.
            float available = Mathf.Max(1f, (_root != null && _root.Size.X > 1f ? _root.Size.X : 1920f) - 2f * IntroCardMargin);
            Vector2 box = _bossIntroCard.Size;
            float natural = _bossIntroCard.GetCombinedMinimumSize().X;
            float scale = natural > available ? available / natural : 1f;
            _bossIntroCard.PivotOffset = box / 2f;
            _bossIntroCard.Scale = new Vector2(scale, scale);
            // Cinzel's capitals stand about 0.7 em tall above the baseline: the flourish rides IntroFlourishLift above them.
            float capTop = (box.Y - line) / 2f + ascent - 0.7f * size;
            Vector2 flourish = _introFlourish.Size;
            _introFlourish.Position = new Vector2((box.X - flourish.X) / 2f, capTop - IntroFlourishLift - flourish.Y);
        }

        // === Refresh helpers ==================================================

        private static readonly System.Collections.Generic.Dictionary<(string, string), string> TimeText = new();
        private string _secondsFormat;

        /// <summary>The "2.0s" chip text, formatted once per (format, seconds) pair: chips refresh every frame.</summary>
        private static string SecondsTime(string format, string seconds) {
            if (!TimeText.TryGetValue((format, seconds), out string text)) {
                text = string.Format(format, seconds);
                TimeText[(format, seconds)] = text;
            }
            return text;
        }

        /// <summary>The shown state of one chip, so a steady frame writes (and reads back) nothing.</summary>
        private struct ChipShown {
            public StatusType Type;
            public string Time;
            public float Ring;
        }

        private ChipShown _damageShown = new() { Type = StatusType.None };
        private ChipShown _controlShown = new() { Type = StatusType.None };

        /// <summary>Writes a chip only when its look changed (every setter relayouts); allocation free on a steady frame.</summary>
        private void ApplyChip(PulpStatusChip chip, ref ChipShown shown, StatusType status, float remaining, float ring) {
            if (chip == null || !GodotObject.IsInstanceValid(chip)) return;
            if (status == StatusType.None) {
                if (shown.Type != StatusType.None || !chip.Empty) {
                    chip.Empty = true;
                    chip.NameText = "";
                    chip.TimeText = "";
                    shown = new ChipShown { Type = StatusType.None };
                }
                return;
            }
            _secondsFormat ??= Tr("hud_seconds_short");
            string time = SecondsTime(_secondsFormat, PulpAbilitySlot.FormatSeconds(remaining));
            if (shown.Type != status || chip.Empty) {
                chip.Empty = false;
                chip.Kind = HudAbilityIndicatorModel.StatusChipKind(status);
                chip.GlyphName = HudAbilityIndicatorModel.StatusIconName(status);
                chip.NameText = HudAbilityIndicatorModel.StatusLabelKey(status);
                shown.Type = status;
                shown.Time = null;
            }
            if (!ReferenceEquals(shown.Time, time)) {
                chip.TimeText = time;
                shown.Time = time;
            }
            if (!Mathf.IsEqualApprox(shown.Ring, ring)) {
                chip.Remaining = ring;
                shown.Ring = ring;
            }
        }

        /// <summary>Maps the model state onto a lens, writing only what moved.</summary>
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
                    _bossHpValue.Text = current.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (_bossHpMax != null) _bossHpMax.Text = "/" + maximum.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    LayoutValuePair(_bossHpValue, _bossHpMax, _bossBar, BossValueRightPad);
                }
            }
        }

        /// <summary>
        /// A tube's value (T17 / T32): the numeral then its "/max" (18 px, dimmed), right-aligned <paramref name="rightPad"/>
        /// inside the tube, the numeral centred and the "/max" on the numeral's baseline, 2 px after it.
        /// </summary>
        private static void LayoutValuePair(Label number, Label max, Control bar, float rightPad) {
            if (number == null || bar == null) return;
            float width = bar.Size.X > 0f ? bar.Size.X : bar.CustomMinimumSize.X;
            float height = bar.Size.Y > 0f ? bar.Size.Y : bar.CustomMinimumSize.Y;
            Vector2 numberSize = number.GetCombinedMinimumSize();
            Vector2 maxSize = max != null ? max.GetCombinedMinimumSize() : Vector2.Zero;
            float maxX = width - rightPad - maxSize.X;
            float numberX = maxX - (max != null ? ValueMaxGap : 0f) - numberSize.X;
            float numberY = (height - numberSize.Y) / 2f;
            number.Position = new Vector2(numberX, numberY);
            number.Size = numberSize;
            if (max == null) return;
            Font numberFont = number.GetThemeFont(FontName), maxFont = max.GetThemeFont(FontName);
            float baseline = numberY + (numberFont?.GetAscent(number.GetThemeFontSize(FontSizeName)) ?? numberSize.Y);
            float maxAscent = maxFont?.GetAscent(max.GetThemeFontSize(FontSizeName)) ?? maxSize.Y;
            max.Position = new Vector2(maxX, baseline - maxAscent);
            max.Size = maxSize;
        }

        // === Fit ==============================================================

        private float FitScale => float.IsNaN(_uiScale.AppliedScale) ? UiScaleBinder.CurrentSetting : _uiScale.AppliedScale;

        /// <summary>
        /// BOSS-A (UI_SPEC_GLASS 3.6, 4.4): the bar sits in the free band between the left stack (plus 24) and the dust plate
        /// (minus 24): width = min(840, canvas - 1008) in the HUD's canvas (840 s and 1920 - 1008 s on screen), at the mock's
        /// x 720 when the band has room. The header is a boss-red diamond, the name (T30) and "PHASE N" (T31) on one
        /// baseline; the tube 10 px under the header row. D11: the name shrinks to fit beside the phase label (floor 22.8 px
        /// on screen); if even the floor does not fit, the phase label drops under the name, right-aligned, and the name takes
        /// the full row, ellipsized only if it still does not fit at the floor.
        /// </summary>
        private void LayoutBossPanel() {
            if (_bossPanel == null || _root == null) return;
            float scale = FitScale;
            float space = _root.Size.X > 1f ? _root.Size.X : 1920f / scale;
            (float left, float right) = FreeBand(space);
            float width = BossBarWidthFor(space);
            // Centred on the canvas when the band allows (the mock's (1920 - 640) / 2), else kept inside the band.
            float x = Mathf.Clamp((space - width) / 2f, left, Mathf.Max(left, right - width));
            float nameX = BossDiamondInset + BossDiamondSize + BossNameGap;
            float rowWidth = width - nameX;
            float baseline = BossHeaderHeight * 0.83f;
            float tubeY = BossHeaderHeight + BossTubeGap;
            _bossPhaseDropped = false;
            Vector2 phaseSize = _bossPhaseTab?.GetCombinedMinimumSize() ?? Vector2.Zero;
            if (_bossNameLabel != null) {
                Font font = _bossNameLabel.GetThemeFont(FontName);
                int size = _bossNameLabel.GetThemeFontSize(FontSizeName);
                float ascent = font?.GetAscent(size) ?? BossNameSize;
                float descent = font?.GetDescent(size) ?? 0f;
                float line = font?.GetHeight(size) ?? BossNameSize;
                // The CSS line box is the 26 px header (line height 1): its baseline sits half the leading plus the ascent down.
                baseline = (BossHeaderHeight - line) / 2f + ascent;
                // Measure unclipped: a clipped label reports about no minimum width, which would flip the rule each call.
                _bossNameLabel.ClipText = false;
                _bossNameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
                Vector2 nameSize = _bossNameLabel.GetCombinedMinimumSize();
                float natural = Mathf.Max(1f, nameSize.X);
                float floor = BossNameFloorScale(scale);
                float beside = rowWidth - phaseSize.X - BossPhaseGap;
                float shrink = 1f;
                bool clip = false;
                if (natural > beside) {
                    shrink = beside / natural;
                    if (shrink < floor) {
                        // D11: even at the floor the name and the phase label do not fit side by side.
                        _bossPhaseDropped = true;
                        shrink = Mathf.Min(1f, rowWidth / natural);
                        if (shrink < floor) {
                            shrink = floor;
                            clip = true;
                        }
                    }
                }
                _bossNameLabel.ClipText = clip;
                _bossNameLabel.TextOverrunBehavior = clip ? TextServer.OverrunBehavior.TrimEllipsis : TextServer.OverrunBehavior.NoTrimming;
                _bossNameLabel.Size = new Vector2(clip ? rowWidth / shrink : natural, Mathf.Max(line, nameSize.Y));
                // Shrunk about the baseline's left end, so the baseline (and the diamond on it) stays put.
                _bossNameLabel.PivotOffset = new Vector2(0f, ascent);
                _bossNameLabel.Position = new Vector2(nameX, baseline - ascent);
                _bossNameLabel.Scale = new Vector2(shrink, shrink);
                _bossNameLabel.Rotation = 0f;
                if (_bossPhaseDropped && _bossPhaseTab != null) {
                    float phaseTop = baseline + shrink * descent + BossPhaseDropGap;
                    _bossPhaseTab.Position = new Vector2(width - phaseSize.X, phaseTop);
                    tubeY = Mathf.Max(tubeY, phaseTop + phaseSize.Y + BossDroppedTubeGap);
                }
            }
            if (_bossPhaseTab != null) {
                _bossPhaseTab.Size = phaseSize;
                _bossPhaseTab.Rotation = 0f;
                _bossPhaseTab.Scale = Vector2.One;
                if (!_bossPhaseDropped) {
                    Font phaseFont = _bossPhaseTab.GetThemeFont(FontName);
                    float phaseAscent = phaseFont?.GetAscent(_bossPhaseTab.GetThemeFontSize(FontSizeName)) ?? phaseSize.Y;
                    _bossPhaseTab.Position = new Vector2(width - phaseSize.X, baseline - phaseAscent);
                }
            }
            if (_bossDiamond != null) {
                var centre = new Vector2(BossDiamondInset + BossDiamondSize / 2f, baseline - BossDiamondLift);
                _bossDiamond.Position = centre - _bossDiamond.Size / 2f;
            }
            _bossPanel.Position = new Vector2(x, BossTop);
            _bossPanel.Size = new Vector2(width, tubeY + BossTubeHeight);
            if (_bossBar != null) {
                _bossBar.Position = new Vector2(0f, tubeY);
                _bossBar.CustomMinimumSize = new Vector2(width, BossTubeHeight);
                _bossBar.Size = new Vector2(width, BossTubeHeight);
                LayoutValuePair(_bossHpValue, _bossHpMax, _bossBar, BossValueRightPad);
            }
        }

        /// <summary>
        /// Per-corner fit: the root scale comes from <see cref="UiScaleBinder"/>; here the boss panel is re-laid when the
        /// scale (and so the free band and the name's floor) changes.
        /// </summary>
        private void ApplyFit(bool force = false) {
            float scale = _uiScale.AppliedScale;
            if (!force && Mathf.IsEqualApprox(scale, _fitScale)) return;
            _fitScale = scale;
            LayoutBossPanel();
            LayoutIntroCard();
            if (_checkpointToast != null && _checkpointToast.Visible) PlaceCheckpointToast();
        }

        /// <summary>
        /// UI_SPEC_GLASS 9.2 puts the checkpoint toast at the top centre (y 140). It keeps to the free band between the
        /// left stack and the right cluster (the boss bar's band, 24 px from each) and leaves the centre only when the
        /// centre would put it over a cluster: at UI scale 1.4 the screen's centre falls inside the left stack.
        /// </summary>
        private void PlaceCheckpointToast() {
            if (_checkpointToast == null) return;
            float canvas = _root != null && _root.Size.X > 1f ? _root.Size.X : 1920f;
            float width = _checkpointToast.GetCombinedMinimumSize().X;
            (float bandLeft, float bandRight) = FreeBand(canvas);
            float left = Mathf.Max(bandLeft, Mathf.Min(canvas / 2f - width / 2f, bandRight - width));
            // The toast is anchored at the canvas's centre line (anchors 0.5), so its offsets are measured from it.
            _checkpointToast.OffsetLeft = left - canvas / 2f;
            _checkpointToast.OffsetRight = left - canvas / 2f + width;
        }

        // === Motion =============================================================

        /// <summary>The "+N" float (MOT-A row 13): in over 220 ms rising 12 px, held, out over 300 ms; 150 ms fades under Reduced.</summary>
        private void AnimateDustFloat() {
            if (_dustFloatRise == null) return;
            float elapsed = DustFloatSeconds - _dustFloatTimer;
            float alpha;
            float rise = 0f;
            if (PulpMotion.Reduced) {
                float fade = PulpMotionRules.DustFloatReducedSeconds;
                alpha = Mathf.Min(Mathf.Clamp(elapsed / fade, 0f, 1f), Mathf.Clamp(_dustFloatTimer / fade, 0f, 1f));
            } else {
                float inProgress = PulpMotionRules.CubicOut(Mathf.Clamp(elapsed / PulpMotionRules.DustFloatInSeconds, 0f, 1f));
                float outShare = Mathf.Clamp(_dustFloatTimer / PulpMotionRules.DustFloatOutSeconds, 0f, 1f);
                alpha = Mathf.Min(inProgress, outShare);
                rise = PulpMotionRules.DustFloatRisePixels * (1f - inProgress);
            }
            _dustFloatRise.Modulate = new Color(1f, 1f, 1f, alpha);
            _dustFloatRise.Position = new Vector2(0f, rise);
        }

        /// <summary>The toast's fade-out (the last 300 ms of its life); its fade-rise in is <see cref="PulpMotion.PopIn"/>.</summary>
        private static float ToastAlpha(float remaining) => Mathf.Clamp(remaining / ToastFadeSeconds, 0f, 1f);
    }
}
