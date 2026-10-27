using System;
using System.Collections.Generic;
using FTT.UI.Motion;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// TG-MENU (Temporal Glass, MENU-A; UI_SPEC_GLASS 3.8, 4.1): the root screen of the main menu. The temporal-glass menu
    /// art under the menu scrim, the three-line title lock-up at (110, 64) (a flourish row, "FIGHTERS" and "TIME" in T01
    /// with the title gradient, "THROUGH" in T02 with its rule), the chevron column anchored bottom-left (left 96, bottom
    /// edge at y 983, rows 68 tall at pitch 79, the D6 width), the version T15 bottom-left, the focusable glass Continue card
    /// bottom-right, the prompt bar and the screen streak. The file keeps its pulp-era name (the old cover lived here).
    ///
    /// <para><b>Title (ruling R2).</b> Line 1 is <c>menu_cover_title_word</c>; lines 2 and 3 are
    /// <c>menu_cover_title_banner</c> split at its first space (<see cref="SplitBanner"/>): a value without a space gives
    /// one middle line, an empty value hides both. No tagline (R3).</para>
    ///
    /// <para><b>UI scale (4.1).</b> Rows, gaps, paddings and fonts scale with the UI scale; the title lock-up is display type
    /// and never grows above its 100 % size (its labels are counter-scaled), and it scales down with the UI scale below
    /// 1.0. When the bottom-anchored column would come within 24 px of the title's bottom the fit sequence runs
    /// (<see cref="ComputeRootFit"/>). The column's width is the D6 rule capped at 700 and so that its focused row stays
    /// 24 px left of the Continue card; the card scales about its bottom-right anchor.</para>
    ///
    /// <para><b>Continue card (R5, AC18).</b> The most recent save (<see cref="MainMenuContinue.PickMostRecent"/>): the hero,
    /// the short place and "difficulty · chapter" (or "difficulty · Campaign Complete"); hidden with no save. It is
    /// focusable, outside the vertical chain: Right from Story Mode reaches it, Left returns; focused it shows the button
    /// focus edge along its hairline, the 14 px glow and the A prompt at its top-right, and it does not slide. Pressing it
    /// resumes through <c>StoryManager.ResumeCampaign</c>.</para>
    /// </summary>
    public partial class MainMenu {

        // ---- test seams ---------------------------------------------------------------------------------------

        /// <summary>
        /// Test seam: when set, the Continue card reads its saves from here instead of
        /// <c>SaveManager.Instance.SaveSlots</c>, so no test depends on the saves on the machine.
        /// </summary>
        internal static FTT.Core.StorySaveData[] SaveSlotsOverrideForTesting;

        /// <summary>Test seam: replaces <c>StoryManager.ResumeCampaign</c> for the card's press.</summary>
        internal static Action<int, FTT.Core.StorySaveData> ResumeOverrideForTesting;

        // ---- MENU-A geometry (UI_SPEC_GLASS 3.8, 4.1), design pixels at UI scale 1.0 --------------------------

        internal const float TitleLeft = 110f, TitleTop = 64f, TitleBlockWidth = 700f;
        /// <summary>The flourish row (11 tall) and the 22 px from it to the first title word's line box.</summary>
        internal const float FlourishRowHeight = 11f, FlourishToTitle = 22f;
        /// <summary>The CSS line heights of T01 (0.94) and T02 (1.5); each line's glyphs are centred on its line box.</summary>
        internal const float TitleLineRatio = 0.94f, MiddleLineRatio = 1.5f;
        /// <summary>"THROUGH" starts 6 px in (the mock's padding) and its rule starts 22 px after the word.</summary>
        internal const float MiddleIndent = 6f, MiddleRuleGap = 22f;
        internal const float ColumnLeft = 96f, ColumnBottom = 983f;
        internal const float RowHeight = 68f, RowGap = 11f, CompactRowHeight = 58f, CompactRowGap = 6f;
        /// <summary>The least gap between the title lock-up's bottom and the column's top.</summary>
        internal const float TitleClearance = 24f;
        internal const float TitleFloorSteps = 0.7f, TitleFloorLast = 0.5f, ColumnScaleFloor = 0.5f;
        internal const float ColumnMinWidth = 560f, ColumnCap = 700f;
        internal const float CardWidth = 600f, CardRightMargin = 70f, CardBottomMargin = 120f, CardClearance = 24f;
        internal const float CardPortrait = 86f, CardRowGap = 18f, CardLineGap = 2f, CardMetaGap = 16f;
        /// <summary>The meta line's separator between the difficulty and the progress (the mock's "·", a glyph, not copy).</summary>
        internal const string MetaSeparator = "·";

        private const string RootLayoutPath = "RootScreen/Center/Panel/Layout/";

        /// <summary>The fit for one UI scale and row count (4.1's sequence). Pure; see <see cref="ComputeRootFit"/>.</summary>
        internal readonly struct RootFit {
            /// <summary>0: nothing to fit; 1 the title down to 0.7; 2 the gap to 6; 3 the rows to 58; 4 the title to 0.5; 5 the column scales.</summary>
            public readonly int Step;
            /// <summary>The title lock-up's scale against its 100 % size.</summary>
            public readonly float TitleScale;
            /// <summary>The row height in design pixels (68, or the compact 58); the button decor multiplies it by the UI scale.</summary>
            public readonly float RowHeight;
            /// <summary>The row height on screen: the design height x s, or the row's own (text) height when that is taller.</summary>
            public readonly float RowPixels;
            /// <summary>The gap between rows in screen pixels.</summary>
            public readonly float Gap;
            /// <summary>The column's own scale about its bottom-left (step 5 only; else 1).</summary>
            public readonly float ColumnScale;
            /// <summary>The column's top and the title's bottom, in screen pixels.</summary>
            public readonly float ColumnTop, TitleBottom;

            public RootFit(int step, float titleScale, float rowHeight, float rowPixels, float gap, float columnScale, float columnTop,
                float titleBottom) {
                Step = step; TitleScale = titleScale; RowHeight = rowHeight; RowPixels = rowPixels; Gap = gap; ColumnScale = columnScale;
                ColumnTop = columnTop; TitleBottom = titleBottom;
            }
        }

        /// <summary>
        /// MENU-A's fit sequence (4.1). The column stands on y 983 and grows upward; the title lock-up hangs from y 64. The
        /// title starts at <c>min(1, uiScale)</c> of its 100 % size (it never grows above it). While the column would come
        /// within 24 px of the title's bottom: (1) the title scales down, pivot top-left, to at most 0.7; (2) the gap drops to
        /// 6 x s; (3) the rows drop to the compact 58 x s; (4) the title may go down to 0.5; (5) the column itself scales down
        /// about its bottom-left. The first step whose configuration fits wins, with the title as large as that step allows.
        /// <paramref name="titleHeight"/> is the lock-up's height at 100 % (300.6 with three lines); <paramref name="extraHeight"/>
        /// is anything else in the column (the save notice and its gap), in screen pixels; <paramref name="minRowPixels"/> is the
        /// rows' own minimum height (a button is never shorter than its text line and paddings: 69 px at 1.0, so a design height
        /// under it does not shorten the row).
        /// </summary>
        internal static RootFit ComputeRootFit(float uiScale, int rows, float titleHeight, float extraHeight = 0f, float minRowPixels = 0f) {
            float s = Mathf.Max(0.1f, uiScale);
            float baseTitle = Mathf.Min(1f, s);
            int n = Mathf.Max(0, rows);
            for (int step = 0; step < FitSteps.Length; step++) {
                (float rowHeight, float gap, float stepFloor) = FitSteps[step];
                float pixels = Mathf.Max(rowHeight * s, minRowPixels);
                float top = ColumnBottom - ColumnHeight(n, pixels, gap * s, extraHeight);
                float needed = titleHeight <= 0f ? baseTitle : (top - TitleClearance - TitleTop) / titleHeight;
                float floor = Mathf.Min(baseTitle, stepFloor);
                if (needed + 0.0001f < floor) continue;
                float title = Mathf.Min(baseTitle, needed);
                return new RootFit(step, title, rowHeight, pixels, gap * s, 1f, top, TitleTop + titleHeight * title);
            }
            float lastTitle = Mathf.Min(baseTitle, TitleFloorLast);
            float lastPixels = Mathf.Max(CompactRowHeight * s, minRowPixels);
            float height = ColumnHeight(n, lastPixels, CompactRowGap * s, extraHeight);
            float room = ColumnBottom - (TitleTop + titleHeight * lastTitle + TitleClearance);
            float scale = Mathf.Clamp(height > 0f ? room / height : 1f, ColumnScaleFloor, 1f);
            return new RootFit(5, lastTitle, CompactRowHeight, lastPixels, CompactRowGap * s, scale, ColumnBottom - height * scale,
                TitleTop + titleHeight * lastTitle);
        }

        // The fit's configurations in order: the row height, the gap (both x s) and the title's floor at that step.
        private static readonly (float Height, float Gap, float Floor)[] FitSteps = {
            (RowHeight, RowGap, 1f),
            (RowHeight, RowGap, TitleFloorSteps),
            (RowHeight, CompactRowGap, TitleFloorSteps),
            (CompactRowHeight, CompactRowGap, TitleFloorSteps),
            (CompactRowHeight, CompactRowGap, TitleFloorLast)
        };

        private static float ColumnHeight(int rows, float rowHeight, float gap, float extra) =>
            rows * rowHeight + Mathf.Max(0, rows - 1) * gap + extra;

        /// <summary>
        /// The column's width cap at UI scale <paramref name="uiScale"/>: 700 x s (3.2's menu cap), and no wider than keeps
        /// the focused row (its 14 x s slide included) 24 px left of the Continue card (4.1: 870 at 1.4).
        /// </summary>
        internal static float ColumnCapAt(float uiScale, float screenWidth) {
            float cardLeft = screenWidth - CardRightMargin - CardWidth * uiScale;
            float beforeCard = cardLeft - CardClearance - PulpMotionRules.FocusTranslateX * uiScale - ColumnLeft;
            return Mathf.Min(ColumnCap * uiScale, beforeCard);
        }

        /// <summary>
        /// Ruling R2: the banner key's value split at its first space into the middle line and the last line. A value
        /// without a space gives one middle line and no last line; an empty (or blank) value gives neither.
        /// </summary>
        internal static (string Middle, string Last) SplitBanner(string value) {
            string text = (value ?? "").Trim();
            if (text.Length == 0) return ("", "");
            int cut = text.IndexOf(' ');
            return cut < 0 ? (text, "") : (text.Substring(0, cut), text.Substring(cut + 1).Trim());
        }

        // ---- nodes --------------------------------------------------------------------------------------------

        private TextureRect _mainArt;
        private TextureRect _subArt;
        private TextureRect _menuScrim;
        private ScreenStreak _streak;
        private Control _titleBlock;
        private Label _titleWord, _titleMiddle, _titleLast;
        private GlassFlourish _titleFlourish, _middleRule;
        private Control _rootColumn;
        private BoxContainer _rootLayout;
        private Button _continueCard;
        private PanelContainer _cardPlate;
        private BoxContainer _cardRow, _cardLines, _cardMeta;
        private Control _cardBadgeRoom;
        private PulpPortraitRoundel _cardPortrait;
        private Label _cardHero, _cardPlace, _cardDifficulty, _cardDot, _cardProgress;
        private SlabFrame _cardFrame;
        private Control _cardBadge;
        private float _cardScale = -1f;
        private bool _layoutQueued;
        private float _rootRowDesign = -1f;

        /// <summary>The Continue card (hidden when no story save exists). Test surface.</summary>
        internal Button ContinueCard => _continueCard;

        /// <summary>The slot the card would resume, or -1. Test surface.</summary>
        internal int ContinueSlot => MainMenuContinue.PickMostRecent(CurrentSaveSlots());

        /// <summary>The fit the root screen last applied. Test surface.</summary>
        internal RootFit LastRootFit { get; private set; }

        private FTT.Core.StorySaveData[] CurrentSaveSlots() =>
            SaveSlotsOverrideForTesting ?? FTT.Core.SaveManager.Instance?.SaveSlots;

        /// <summary>The UI scale every menu size follows (the button decor's clamp of the theme's applied scale).</summary>
        private static float MenuScale => PulpButtonDecor.UiScaleFactor;

        private void BindCover() {
            _mainArt = GetNodeOrNull<TextureRect>("Background/TemporalMainArt");
            _subArt = GetNodeOrNull<TextureRect>("Background/TemporalSubmenuArt");
            // The menu scrim (1.3) sits over both arts and under every screen; the root shows it, the sub-screens do not.
            if (GetNodeOrNull<Control>("Background") is { } background) _menuScrim = PulpScreenKit.AddMenuScrim(background, false, 2);
            _rootColumn = GetNodeOrNull<Control>("RootScreen/Center/Panel");
            _rootLayout = GetNodeOrNull<BoxContainer>("RootScreen/Center/Panel/Layout");

            BindTitleBlock();
            BuildPromptBar();
            BindContinueCard();
            DecorateButtons(this);
            DressSubScreens();
            if (_rootColumn != null) _rootColumn.Resized += PinColumnPivot;
            // The dir-a streak (1.4, AC19) over every menu screen. It draws in tree order, so the confirm modals the menu adds
            // later (overlays, which never show the streak) cover it; its own z 10 would put it over them.
            _streak = PulpScreenKit.AddScreenStreak(this);
            if (_streak != null) _streak.ZIndex = 0;
            QueueMenuLayout();
        }

        public override void _Notification(int what) {
            if (!IsNodeReady()) return;
            if (what == NotificationTranslationChanged) {
                RefreshContinueCard();
                RefreshSubScreenTexts();
                QueueMenuLayout();
            } else if (what == NotificationThemeChanged || what == NotificationResized) {
                // A UI-scale change raises one theme notification per font size; the layout runs once, after them all.
                QueueMenuLayout();
            }
        }

        /// <summary>Queues one deferred layout pass (title, column, card, sub-screen sizes) after a scale, locale or size change.</summary>
        private void QueueMenuLayout() {
            if (_layoutQueued) return;
            _layoutQueued = true;
            Callable.From(ApplyMenuLayout).CallDeferred();
        }

        private void ApplyMenuLayout() {
            _layoutQueued = false;
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            float s = MenuScale;
            float titleHeight = MeasureTitleBlock(s);
            LayoutContinueCard(s);
            RootFit fit = ApplyRootColumn(s, titleHeight);
            PlaceTitleBlock(s, fit.TitleScale);
            ApplySubScreenScale(s);
        }

        // ---- the title lock-up (3.8, R2) ------------------------------------------------------------------------

        private void BindTitleBlock() {
            _titleBlock = GetNodeOrNull<Control>("RootScreen/TitleBlock");
            if (_titleBlock == null) return;
            _titleWord = _titleBlock.GetNodeOrNull<Label>("Title");
            _titleMiddle = _titleBlock.GetNodeOrNull<Label>("Middle");
            _titleLast = _titleBlock.GetNodeOrNull<Label>("Last");
            _titleFlourish = new GlassFlourish {
                Name = "Flourish", Shape = GlassFlourish.FlourishShape.RuleDiamondRule, Gap = GlassFlourish.MenuGap
            };
            _titleBlock.AddChild(_titleFlourish);
            _titleBlock.MoveChild(_titleFlourish, 0);
            _middleRule = new GlassFlourish { Name = "MiddleRule", Shape = GlassFlourish.FlourishShape.Rule, RuleColor = UIPalette.Edge };
            _titleBlock.AddChild(_middleRule);
            // T01's vertical gradient (6.5) on both title words, one material each.
            if (_titleWord != null) TitleGradient.Attach(_titleWord);
            if (_titleLast != null) TitleGradient.Attach(_titleLast);
            RefreshTitleWords();
        }

        private void RefreshTitleWords() {
            (string middle, string last) = SplitBanner(Tr("menu_cover_title_banner"));
            SetTitleWord(_titleMiddle, middle);
            SetTitleWord(_titleLast, last);
        }

        private static void SetTitleWord(Label label, string text) {
            if (label == null) return;
            label.Text = text;
            label.Visible = text.Length > 0;
        }

        /// <summary>
        /// The title lock-up's height at 100 % (3.8: the 11 px flourish row, 22 px, "FIGHTERS" on a 105.3 px line box,
        /// "THROUGH" on a 57 px box, "TIME" on a 105.3 px box; 300.6 with three lines), from the title fonts' sizes at UI scale
        /// <paramref name="s"/> (the theme grows them; the lock-up does not). Refreshes the two banner words first (R2).
        /// </summary>
        private float MeasureTitleBlock(float s) {
            if (_titleBlock == null || _titleWord == null) return 0f;
            RefreshTitleWords();
            float height = FlourishRowHeight + FlourishToTitle + LineBox(_titleWord, TitleLineRatio, s);
            if (_titleMiddle != null && _titleMiddle.Visible) height += LineBox(_titleMiddle, MiddleLineRatio, s);
            if (_titleLast != null && _titleLast.Visible) height += LineBox(_titleLast, TitleLineRatio, s);
            return height;
        }

        /// <summary>A title line's CSS line box in 100 % pixels: the line height ratio x the role's 100 % size.</summary>
        private static float LineBox(Label label, float lineRatio, float s) => lineRatio * label.GetThemeFontSize("font_size") / s;

        /// <summary>
        /// Places the lock-up at <paramref name="titleScale"/> of its 100 % size, pivot top-left: each line's glyphs centred on
        /// its line box, every label drawn at the theme's (UI-scaled) size and scaled by <c>titleScale / s</c>. The flourish
        /// row and THROUGH's rule are laid out in whole screen pixels rather than scaled, so their 1 px rules stay crisp.
        /// </summary>
        private void PlaceTitleBlock(float s, float titleScale) {
            if (_titleBlock == null || _titleWord == null) return;
            float t = titleScale;
            _titleBlock.PivotOffset = Vector2.Zero;
            _titleBlock.Scale = Vector2.One;
            _titleFlourish.Position = Vector2.Zero;
            _titleFlourish.Size = new Vector2(Mathf.Round(TitleBlockWidth * t), FlourishRowHeight);
            float y = (FlourishRowHeight + FlourishToTitle) * t;
            y = PlaceTitleLine(_titleWord, 0f, y, TitleLineRatio, s, t);
            if (_titleMiddle != null && _titleMiddle.Visible) {
                float boxTop = y;
                y = PlaceTitleLine(_titleMiddle, MiddleIndent * t, y, MiddleLineRatio, s, t);
                float ruleX = Mathf.Round((MiddleIndent + LabelTextWidth(_titleMiddle) / s + MiddleRuleGap) * t);
                float right = Mathf.Round(TitleBlockWidth * t);
                _middleRule.Visible = ruleX < right - 8f;
                _middleRule.Position = new Vector2(ruleX, Mathf.Floor(boxTop + (y - boxTop) / 2f));
                _middleRule.Size = new Vector2(Mathf.Max(1f, right - ruleX), 1f);
            } else {
                _middleRule.Visible = false;
            }
            if (_titleLast != null && _titleLast.Visible) y = PlaceTitleLine(_titleLast, 0f, y, TitleLineRatio, s, t);
            _titleBlock.Size = new Vector2(TitleBlockWidth * t, y);
        }

        private static float PlaceTitleLine(Label label, float x, float boxTop, float lineRatio, float s, float t) {
            Font font = label.GetThemeFont("font");
            int size = label.GetThemeFontSize("font_size");
            float fontHeight = font?.GetHeight(size) ?? size;
            float box = lineRatio * size / s * t;
            float scale = t / s;
            label.ResetSize();
            label.PivotOffset = Vector2.Zero;
            label.Scale = new Vector2(scale, scale);
            label.Position = new Vector2(x, boxTop + (box - fontHeight * scale) / 2f);
            return boxTop + box;
        }

        /// <summary>The width a label draws its (resolved) text at, upper-cased when it is.</summary>
        private static float LabelTextWidth(Label label) {
            Font font = label.GetThemeFont("font");
            if (font == null) return 0f;
            string text = label.Uppercase ? PulpButtonDecor.ToDrawnText(label.Text) : label.Text;
            return font.GetStringSize(text, HorizontalAlignment.Left, -1f, label.GetThemeFontSize("font_size")).X;
        }

        // ---- the column (3.2 D6, 4.1) -----------------------------------------------------------------------------

        private RootFit ApplyRootColumn(float s, float titleHeight) {
            if (_rootLayout == null || _rootColumn == null) {
                return LastRootFit = ComputeRootFit(s, 0, titleHeight);
            }
            var buttons = new List<Button>();
            int rows = 0;
            float natural = 0f;
            for (int i = 0, n = _rootLayout.GetChildCount(); i < n; i++) {
                if (_rootLayout.GetChild(i) is not Button button) continue;
                buttons.Add(button);
                if (!button.Visible) continue;
                rows++;
                // A row is never shorter than its own text line and paddings (69 px at 1.0, 88 at 1.4).
                natural = Mathf.Max(natural, button.GetMinimumSize().Y);
            }
            float extra = 0f;
            if (_rootLayout.GetNodeOrNull<Control>("NoticeSlot") is { Visible: true } notice) {
                extra = notice.GetCombinedMinimumSize().Y + RowGap * s;
            }
            RootFit fit = ComputeRootFit(s, rows, titleHeight, extra, natural);
            LastRootFit = fit;
            _rootLayout.AddThemeConstantOverride("separation", Mathf.RoundToInt(fit.Gap));
            // The row height is a design height: the decor multiplies it by the UI scale.
            if (!Mathf.IsEqualApprox(_rootRowDesign, fit.RowHeight)) {
                _rootRowDesign = fit.RowHeight;
                foreach (Button button in buttons) button.CustomMinimumSize = new Vector2(button.CustomMinimumSize.X, fit.RowHeight);
            }
            float screenWidth = Size.X > 0f ? Size.X : 1920f;
            PulpButtonDecor.ApplyStackWidth(buttons, ColumnMinWidth * s, ColumnCapAt(s, screenWidth));
            _rootColumn.Scale = new Vector2(fit.ColumnScale, fit.ColumnScale);
            PinColumnPivot();
            return fit;
        }

        // The column stands on its bottom edge: it grows upward, and the fit's last step scales it about its bottom-left.
        private void PinColumnPivot() {
            if (_rootColumn != null) _rootColumn.PivotOffset = new Vector2(0f, _rootColumn.Size.Y);
        }

        // ---- prompt bar -------------------------------------------------------------------------------------------

        private void BuildPromptBar() {
            var bar = GetNodeOrNull<PulpPromptBar>("PromptBar");
            if (bar == null || bar.PairCount > 0) return;
            bar.AddPrompt("ui_accept", "ui_prompt_select");
            bar.AddPrompt("ui_cancel", "common_back");
        }

        // ---- the Continue card (3.8, R5, AC18) ------------------------------------------------------------------

        private void BindContinueCard() {
            _continueCard = GetNodeOrNull<Button>("RootScreen/ContinueCard");
            if (_continueCard == null) return;
            _cardPlate = _continueCard.GetNode<PanelContainer>("Plate");
            _cardRow = _cardPlate.GetNode<BoxContainer>("Row");
            _cardLines = _cardRow.GetNode<BoxContainer>("Lines");
            _cardMeta = _cardLines.GetNode<BoxContainer>("Meta");
            _cardBadgeRoom = _cardRow.GetNode<Control>("BadgeRoom");
            _cardHero = _cardLines.GetNode<Label>("Hero");
            _cardPlace = _cardLines.GetNode<Label>("Place");
            _cardDifficulty = _cardMeta.GetNode<Label>("Difficulty");
            _cardDot = _cardMeta.GetNode<Label>("Dot");
            _cardProgress = _cardMeta.GetNode<Label>("Progress");
            _cardDot.Text = MetaSeparator;

            _cardPortrait = new PulpPortraitRoundel { Name = "Portrait", Diameter = CardPortrait, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            _cardRow.AddChild(_cardPortrait);
            _cardRow.MoveChild(_cardPortrait, 0);
            // The glass slab's sheen and its tag (3.1; tag "Continue", R5).
            PulpScreenKit.DressPanel(_cardPlate, "boot_continue");

            _cardFrame = new SlabFrame { Name = "FocusFrame", Visible = false };
            _continueCard.AddChild(_cardFrame);
            _cardFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _cardBadge = new Control { Name = "PromptBadge", Visible = false, MouseFilter = MouseFilterEnum.Ignore };
            _continueCard.AddChild(_cardBadge);

            // Outside the vertical chain (Right from Story Mode reaches it, Left returns), never sliding.
            _continueCard.AddToGroup(FocusChainBuilder.SkipGroup);
            _continueCard.FocusMode = FocusModeEnum.All;
            _continueCard.Pressed += OnContinuePressed;
            _continueCard.FocusEntered += OnCardFocusChanged;
            _continueCard.FocusExited += OnCardFocusChanged;
            // Hover is focus (AC8), as on the chevrons.
            _continueCard.MouseEntered += () => {
                if (_continueCard.IsVisibleInTree() && !_continueCard.HasFocus()) _continueCard.GrabFocus();
            };
            _cardPlate.MinimumSizeChanged += QueueMenuLayout;
            if (FTT.Core.InputManager.Instance != null) FTT.Core.InputManager.Instance.DeviceAssigned += OnCardDeviceAssigned;
            TreeExiting += () => {
                if (FTT.Core.InputManager.Instance != null) FTT.Core.InputManager.Instance.DeviceAssigned -= OnCardDeviceAssigned;
            };
            RefreshContinueCard();
        }

        private void OnCardDeviceAssigned(int slot, int device) {
            if (_continueCard != null && _continueCard.HasFocus()) BuildCardBadge();
        }

        /// <summary>Re-reads the most recent save and repaints (or hides) the card; the first show fades it in (MOT-A row 4).</summary>
        internal void RefreshContinueCard() {
            if (_continueCard == null) return;
            FTT.Core.StorySaveData[] slots = CurrentSaveSlots();
            int slot = MainMenuContinue.PickMostRecent(slots);
            bool show = slot >= 0;
            bool wasShown = _continueCard.Visible;
            _continueCard.Visible = show;
            LinkContinueCard(show);
            if (!show) return;
            FTT.Core.StorySaveData save = slots[slot];
            FTT.Characters.CharacterData data = CharacterDataFor(save.SelectedCharacterID);
            _cardPortrait.Portrait = data?.CharacterPortrait;
            _cardHero.Text = CharacterName(save.SelectedCharacterID);
            _cardPlace.Text = MainMenuContinue.ShortPlace(Tr(MainMenuContinue.PlaceKey(save)));
            _cardDifficulty.Text = Tr(MainMenuContinue.DifficultyKey(save.Difficulty));
            _cardProgress.Text = save.IsCompleted
                ? Tr("save_completed")
                : string.Format(Tr("menu_continue_chapter"), MainMenuContinue.ChapterNumber(save));
            if (!wasShown && IsInsideTree() && _rootScreen is { Visible: true }) PulpMotion.PopIn(_cardPlate);
        }

        /// <summary>Right from Story Mode reaches the card and Left returns; the card's other sides hold focus on it.</summary>
        private void LinkContinueCard(bool show) {
            var story = GetNodeOrNull<Button>(RootLayoutPath + "StoryButton");
            if (story == null || _continueCard == null) return;
            if (!show) {
                story.FocusNeighborRight = new NodePath();
                if (_continueCard.HasFocus()) story.GrabFocus();
                return;
            }
            story.FocusNeighborRight = story.GetPathTo(_continueCard);
            NodePath self = _continueCard.GetPathTo(_continueCard);
            NodePath back = _continueCard.GetPathTo(story);
            _continueCard.FocusNeighborLeft = back;
            _continueCard.FocusNeighborRight = self;
            _continueCard.FocusNeighborTop = self;
            _continueCard.FocusNeighborBottom = self;
            _continueCard.FocusPrevious = back;
            _continueCard.FocusNext = back;
        }

        private void OnCardFocusChanged() {
            bool focused = _continueCard.HasFocus();
            _cardFrame.Mode = focused ? SlabFrame.FrameMode.Focus : SlabFrame.FrameMode.None;
            _cardFrame.Visible = focused;
            _cardBadge.Visible = focused;
            if (focused) BuildCardBadge();
        }

        // The focused card's A prompt (3.8): 36 px at its top-right inside the padding, in the focus label colour.
        private void BuildCardBadge() {
            for (int i = _cardBadge.GetChildCount() - 1; i >= 0; i--) {
                Node child = _cardBadge.GetChild(i);
                _cardBadge.RemoveChild(child);
                child.QueueFree();
            }
            float s = MenuScale;
            float size = PulpButtonDecor.BadgeSize * s;
            PromptDevice device = PulpPromptGlyphs.CurrentDevice();
            string text = PulpPromptGlyphs.LabelFor("ui_accept", device);
            Control glyph = device == PromptDevice.Pad
                ? PulpPromptGlyphs.CreatePadGlyph(text, UIPalette.LabelRoles.PromptKey, UIPalette.TextFocus, size, halo: false)
                : PulpPromptGlyphs.CreateKeyCap(text, UIPalette.LabelRoles.PromptKey, UIPalette.TextFocus, size, out _);
            _cardBadge.AddChild(glyph);
            LayoutCardBadge();
        }

        private void LayoutCardBadge() {
            if (_cardBadge == null || _cardBadge.GetChildCount() == 0 || _cardPlate == null) return;
            var glyph = (Control)_cardBadge.GetChild(0);
            Vector2 size = glyph.GetCombinedMinimumSize();
            glyph.Position = Vector2.Zero;
            glyph.Size = size;
            _cardBadge.Size = size;
            StyleBox plate = _cardPlate.GetThemeStylebox("panel");
            float right = plate?.GetContentMargin(Side.Right) ?? 0f;
            float top = plate?.GetContentMargin(Side.Top) ?? 0f;
            _cardBadge.Position = new Vector2(_continueCard.Size.X - right - size.X, top);
        }

        /// <summary>
        /// The card at UI scale <paramref name="s"/> (4.1: it scales about its bottom-right anchor, 600 x 118 at 1.0 and
        /// 840 wide at 1.4): its width, the frame, the gaps and its own copy of the slab's paddings grow with the scale; its
        /// height is its content's.
        /// </summary>
        private void LayoutContinueCard(float s) {
            if (_continueCard == null) return;
            if (!Mathf.IsEqualApprox(s, _cardScale)) {
                _cardScale = s;
                ScalePlatePaddings(_cardPlate, s);
                _cardPortrait.Diameter = CardPortrait * s;
                _cardRow.AddThemeConstantOverride("separation", Mathf.RoundToInt(CardRowGap * s));
                _cardLines.AddThemeConstantOverride("separation", Mathf.RoundToInt(CardLineGap * s));
                _cardMeta.AddThemeConstantOverride("separation", Mathf.RoundToInt(CardMetaGap * s));
                // The badge's room: 36 + 16 at the right, less the row's own gap before it.
                _cardBadgeRoom.CustomMinimumSize = new Vector2((PulpButtonDecor.BadgeReserve - CardRowGap) * s, 0f);
                if (_cardBadge.Visible) BuildCardBadge();
            }
            float height = Mathf.Ceil(_cardPlate.GetCombinedMinimumSize().Y);
            float width = Mathf.Round(CardWidth * s);
            _continueCard.CustomMinimumSize = new Vector2(width, height);
            _continueCard.OffsetRight = -CardRightMargin;
            _continueCard.OffsetBottom = -CardBottomMargin;
            _continueCard.OffsetLeft = -CardRightMargin - width;
            _continueCard.OffsetTop = -CardBottomMargin - height;
            LayoutCardBadge();
        }

        /// <summary>Gives a slab its own copy of its theme plate with the paddings scaled by <paramref name="s"/> (none at 1.0).</summary>
        private static void ScalePlatePaddings(PanelContainer plate, float s) {
            if (plate == null) return;
            plate.RemoveThemeStyleboxOverride("panel");
            if (Mathf.IsEqualApprox(s, 1f) || plate.GetThemeStylebox("panel") is not StyleBox themed) return;
            var copy = (StyleBox)themed.Duplicate();
            foreach (Side side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom }) {
                copy.SetContentMargin(side, Mathf.Round(Mathf.Max(0f, themed.GetContentMargin(side)) * s));
            }
            plate.AddThemeStyleboxOverride("panel", copy);
        }

        private void OnContinuePressed() {
            FTT.Core.StorySaveData[] slots = CurrentSaveSlots();
            int slot = MainMenuContinue.PickMostRecent(slots);
            if (slot < 0) return;
            if (ResumeOverrideForTesting != null) {
                ResumeOverrideForTesting(slot, slots[slot]);
                return;
            }
            FTT.Core.StoryManager.Instance?.ResumeCampaign(slot, slots[slot]);
        }

        // ---- kit decor ----------------------------------------------------------------------------------------------

        /// <summary>
        /// Gives every chevron button the BTN-A decor (the diamond marker, the focus slide, the prompt badge, the upper-case
        /// label at x + 58). Flat buttons are the glass slabs (the Continue card, the slot slabs, the character tiles) and the
        /// licence rows, which draw their own look.
        /// </summary>
        private static void DecorateButtons(Node root) {
            for (int i = 0, n = root.GetChildCount(); i < n; i++) {
                Node child = root.GetChild(i);
                if (child is Button { Flat: false } button) {
                    PulpButtonDecor.Apply(button, compact: button.ThemeTypeVariation == UIPalette.Variations.PulpButtonCompact);
                }
                DecorateButtons(child);
            }
        }

        private void PopInPanel(MainMenuScreen screen) {
            if (screen == MainMenuScreen.Root) return;
            if (ScreenRoot(screen).GetNodeOrNull<Control>("Center/Panel") is { } panel) {
                panel.PivotOffset = panel.Size / 2f;
                PulpMotion.PopIn(panel);
            }
        }
    }
}
