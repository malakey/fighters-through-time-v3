using System;
using FTT.UI.Motion;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-MENU (pulp UI redesign, MENU-B): the cover half of the main menu. The hero poses with their ink
    /// treatment, the two-part live title, the Continue sticker, the prompt bar, the root column's fit and the
    /// kit decor on every banner button.
    /// </summary>
    public partial class MainMenu {

        // ---- test seams ---------------------------------------------------------

        /// <summary>
        /// Test seam: when set, the Continue sticker reads its saves from here instead of
        /// <c>SaveManager.Instance.SaveSlots</c>, so no test depends on the saves on the machine.
        /// </summary>
        internal static FTT.Core.StorySaveData[] SaveSlotsOverrideForTesting;

        /// <summary>Test seam: replaces <c>StoryManager.ResumeCampaign</c> for the sticker's press.</summary>
        internal static Action<int, FTT.Core.StorySaveData> ResumeOverrideForTesting;

        // ---- cover constants (UI_SPEC 3.7) ---------------------------------------

        private const float CoverFloorY = 1020f;
        private const int HeroOutlinePixels = 3;
        private static readonly Vector2I HeroShadowPixels = new(4, 5);
        private const float RootColumnTop = 322f;
        private const float RootColumnBottom = 990f;
        private const float RootColumnRoomBottom = 996f;
        private const float StickerTextMaxWidth = 200f;

        /// <summary>The three baked victory poses: node, scale, feet-pivot x, feet distance from the image bottom.</summary>
        private static readonly (string Node, float Scale, float PivotX, float FeetPixels)[] CoverHeroes = {
            ("Joan", 1.62f, 250f, 15f),
            ("Einstein", 1.55f, 540f, 14f),
            ("Cleopatra", 1.50f, 800f, 14f)
        };

        private Control _coverRays;
        private Control _coverBurst;
        private Control _subScrim;
        private Button _continueSticker;
        private Control _stickerVisual;
        private Label _stickerLine2;
        private Label _stickerLine3;
        private Control _rootColumn;

        /// <summary>The Continue sticker (hidden when no story save exists). Test surface.</summary>
        internal Button ContinueSticker => _continueSticker;

        /// <summary>The slot the sticker would resume, or -1. Test surface.</summary>
        internal int ContinueSlot => MainMenuContinue.PickMostRecent(CurrentSaveSlots());

        private FTT.Core.StorySaveData[] CurrentSaveSlots() =>
            SaveSlotsOverrideForTesting ?? FTT.Core.SaveManager.Instance?.SaveSlots;

        private void BindCover() {
            _coverRays = GetNodeOrNull<Control>("Background/CoverRays");
            _coverBurst = GetNodeOrNull<Control>("Background/CoverBurst");
            _subScrim = GetNodeOrNull<Control>("Background/SubScrim");
            _rootColumn = GetNodeOrNull<Control>("RootScreen/Center/Panel");

            BuildHeroes();
            ApplyCoverUiScale();
            RefreshCoverTitle();
            BuildPromptBar();
            BindContinueSticker();
            DecorateButtons(this);
            FitRootColumn();
            if (_rootColumn != null) {
                _rootColumn.MinimumSizeChanged += FitRootColumn;
                _rootColumn.Resized += FitRootColumn;
            }
            foreach (string screen in new[] {
                         "SlotScreen", "CharacterScreen", "DifficultyScreen", "LevelSelectScreen",
                         "ExtrasScreen", "LicensesScreen" }) {
                // A tall panel at the maximum UI scale shrinks onto the screen instead of clipping.
                if (GetNodeOrNull<Control>($"{screen}/Center/Panel") is { } panel) MenuSafeAreaFit.Attach(panel);
            }
        }

        public override void _Notification(int what) {
            if (!IsNodeReady()) return;
            if (what == NotificationTranslationChanged) {
                RefreshCoverTitle();
                RefreshContinueSticker();
            } else if (what == NotificationThemeChanged) {
                ApplyCoverUiScale();
                RefreshContinueSticker();
                FitRootColumn();
            }
        }

        // ---- title ----------------------------------------------------------------

        /// <summary>The title is two live keys; a locale may put the whole title in the first and empty the second.</summary>
        private void RefreshCoverTitle() {
            var banner = GetNodeOrNull<Control>("RootScreen/TitleBlock/Banner");
            if (banner != null) banner.Visible = BannerShows(Tr("menu_cover_title_banner"));
        }

        /// <summary>The banner plate shows only when its key has text, so a locale can put the whole title in the first key.</summary>
        internal static bool BannerShows(string translated) => !string.IsNullOrWhiteSpace(translated);

        /// <summary>
        /// The title block and the sticker are print, not interface text: the accessibility UI scale grows their
        /// theme font sizes in place, so the blocks counter-scale to keep the cover's composition.
        /// </summary>
        private float CoverTextScale => 1f / Mathf.Max(0.5f, UIPalette.AppliedUiScale);

        /// <summary>
        /// The title block, as authored at UI scale 1.0 (block space): the word and its seven extrusion copies at
        /// (0, 0) to (7, 7), the banner at (18, 179) and 710 x 98. At a larger UI scale each part keeps its authored
        /// place and look: the text is counter-scaled and the banner plate is laid out ui times larger, then scaled back.
        /// </summary>
        private void ApplyCoverUiScale() {
            float ui = Mathf.Max(0.5f, UIPalette.AppliedUiScale);
            var block = GetNodeOrNull<Control>("RootScreen/TitleBlock");
            if (block == null) return;
            foreach (Node child in block.GetChildren()) {
                if (child is not Control control) continue;
                Vector2 authored;
                if (control.Name == "Banner") {
                    authored = new Vector2(18f, 179f);
                    control.Size = new Vector2(710f, 98f) * ui;
                } else if (control.Name.ToString().StartsWith("Extrude")) {
                    float step = control.Name.ToString().Substring("Extrude".Length).ToInt();
                    authored = new Vector2(step, step);
                } else {
                    authored = Vector2.Zero;
                }
                control.Position = authored;
                if (control is Label label) {
                    label.PivotOffset = Vector2.Zero;
                    label.Scale = new Vector2(CoverTextScale, CoverTextScale);
                } else {
                    control.PivotOffset = Vector2.Zero;
                    control.Scale = new Vector2(CoverTextScale, CoverTextScale);
                }
            }
        }

        // ---- heroes -----------------------------------------------------------------

        private void BuildHeroes() {
            foreach ((string name, float scale, float pivotX, float feet) in CoverHeroes) {
                var rect = GetNodeOrNull<TextureRect>($"RootScreen/Heroes/{name}");
                if (rect?.Texture == null) continue;
                Texture2D source = rect.Texture;
                ImageTexture inked = MainMenuCoverInk.Build(source, HeroOutlinePixels, HeroShadowPixels, out int pad);
                float width = source.GetWidth();
                float height = source.GetHeight();
                rect.Texture = inked;
                rect.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
                rect.Position = new Vector2(
                    pivotX - width / 2f * scale - pad * scale,
                    CoverFloorY + feet * scale - height * scale - pad * scale);
                rect.Size = new Vector2((width + 2 * pad) * scale, (height + 2 * pad) * scale);
            }
        }

        // ---- prompt bar -------------------------------------------------------------

        private void BuildPromptBar() {
            var bar = GetNodeOrNull<PulpPromptBar>("PromptBar");
            if (bar == null || bar.PairCount > 0) return;
            bar.AddPrompt("ui_accept", "ui_prompt_select");
            bar.AddPrompt("ui_cancel", "common_back");
        }

        // ---- the Continue sticker ----------------------------------------------------

        private void BindContinueSticker() {
            _continueSticker = GetNodeOrNull<Button>("RootScreen/ContinueSticker");
            if (_continueSticker == null) return;
            _stickerVisual = _continueSticker.GetNodeOrNull<Control>("Visual");
            _stickerLine2 = _continueSticker.GetNodeOrNull<Label>("Visual/Line2");
            _stickerLine3 = _continueSticker.GetNodeOrNull<Label>("Visual/Line3");
            _continueSticker.Pressed += OnContinuePressed;
            _continueSticker.FocusEntered += () => PoseSticker(true);
            _continueSticker.MouseEntered += () => PoseSticker(true);
            _continueSticker.FocusExited += () => PoseSticker(_continueSticker.HasFocus());
            _continueSticker.MouseExited += () => PoseSticker(_continueSticker.HasFocus());
            RefreshContinueSticker();
        }

        /// <summary>
        /// The sticker is print, not a pose: it keeps its 8 degree tilt and swells a little on focus (a stable
        /// swap, no tween, so Reduced Temporal Effects changes nothing).
        /// </summary>
        private void PoseSticker(bool active) {
            if (_stickerVisual == null) return;
            _stickerVisual.Scale = active ? new Vector2(1.07f, 1.07f) : Vector2.One;
            _stickerVisual.Rotation = Mathf.DegToRad(active ? 5f : 8f);
        }

        /// <summary>Re-reads the most recent save and repaints (or hides) the sticker.</summary>
        internal void RefreshContinueSticker() {
            if (_continueSticker == null) return;
            FTT.Core.StorySaveData[] slots = CurrentSaveSlots();
            int slot = MainMenuContinue.PickMostRecent(slots);
            _continueSticker.Visible = slot >= 0;
            if (slot < 0) return;
            FTT.Core.StorySaveData save = slots[slot];
            if (_stickerLine2 != null) {
                _stickerLine2.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
                _stickerLine2.Text = save.IsCompleted
                    ? Tr("save_completed")
                    : string.Format(Tr("menu_continue_chapter"), MainMenuContinue.ChapterNumber(save));
            }
            if (_stickerLine3 != null) {
                _stickerLine3.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
                _stickerLine3.Text = MainMenuContinue.ShortPlace(Tr(MainMenuContinue.PlaceKey(save)));
            }
            FitStickerLine(GetNodeOrNull<Label>("RootScreen/ContinueSticker/Visual/Line1"));
            FitStickerLine(_stickerLine2);
            FitStickerLine(_stickerLine3);
            PoseSticker(_continueSticker.HasFocus());
        }

        /// <summary>Shrinks a sticker line that is wider than the star's chord, by scale (never a font override).</summary>
        private void FitStickerLine(Label label) {
            if (label == null) return;
            float baseScale = CoverTextScale;
            // Each line is centred on its authored box centre and scaled about its own middle.
            Vector2 centre = StickerLineCentre(label.Name);
            Vector2 min = label.GetCombinedMinimumSize();
            label.Size = min;
            label.PivotOffset = min / 2f;
            label.Position = centre - min / 2f;
            float width = min.X * baseScale;
            float scale = width > StickerTextMaxWidth ? baseScale * StickerTextMaxWidth / width : baseScale;
            label.Scale = new Vector2(scale, scale);
        }

        private static Vector2 StickerLineCentre(StringName line) => line.ToString() switch {
            "Line1" => new Vector2(135f, 94f),
            "Line2" => new Vector2(135f, 142f),
            _ => new Vector2(135f, 177f)
        };

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

        // ---- the root column ------------------------------------------------------------

        /// <summary>
        /// Row pitch for the root column: the spec's 97 px (80 + 17) for a short list, tightened so a debug
        /// build's eight rows end above the prompt bar (UI_SPEC 4.1, ruling 8.2).
        /// </summary>
        internal static float RootRowSeparation(int visibleRows) {
            if (visibleRows <= 1) return 17f;
            float fit = Mathf.Floor((RootColumnBottom - RootColumnTop - 80f * visibleRows) / (visibleRows - 1));
            return Mathf.Clamp(fit, 4f, 17f);
        }

        private void ApplyRootRowSeparation(Control layout) {
            int rows = 0;
            foreach (Node child in layout.GetChildren()) {
                if (child is Button { Visible: true }) rows++;
            }
            if (layout is BoxContainer box) box.AddThemeConstantOverride("separation", (int)RootRowSeparation(rows));
        }

        /// <summary>At a large UI scale the buttons grow; the column shrinks about its top-left so it stays on screen.</summary>
        private void FitRootColumn() {
            if (_rootColumn == null || !IsInsideTree()) return;
            Vector2 min = _rootColumn.GetCombinedMinimumSize();
            float height = Mathf.Max(RootColumnBottom - RootColumnTop, min.Y);
            float width = Mathf.Max(640f, min.X);
            float scale = Mathf.Min(1f, Mathf.Min((RootColumnRoomBottom - RootColumnTop) / height, (1896f - 1180f) / width));
            _rootColumn.PivotOffset = Vector2.Zero;
            _rootColumn.Scale = new Vector2(scale, scale);
        }

        // ---- kit decor ---------------------------------------------------------------------

        /// <summary>
        /// Gives every banner button the pulp marker, focus pose and prompt badge, and left-aligns its label.
        /// Flat buttons (the Continue sticker, the licence rows) are not banners.
        /// </summary>
        private static void DecorateButtons(Node root) {
            foreach (Node child in root.GetChildren()) {
                if (child is Button { Flat: false } button) {
                    button.Alignment = HorizontalAlignment.Left;
                    ClearMarker(button);
                    PulpButtonDecor.Apply(button, compact: button.ThemeTypeVariation == UIPalette.Variations.PulpButtonCompact);
                }
                DecorateButtons(child);
            }
        }

        /// <summary>
        /// The decor's marker sits at x + 26 and the kit label margin is 64 (UI_SPEC 3.2). A variation whose own
        /// content margin is smaller (the legacy 26 px plate) would run its text under the marker, so it gets a
        /// transparent spacer icon that pushes the label out to x + 64 without touching the theme.
        /// </summary>
        private static void ClearMarker(Button button) {
            if (button.ThemeTypeVariation == UIPalette.Variations.PulpButton
                || button.ThemeTypeVariation == UIPalette.Variations.PulpButtonCompact) return;
            float margin = button.GetThemeStylebox("normal").GetMargin(Side.Left);
            float want = 64f - margin - button.GetThemeConstant("h_separation");
            if (want < 1f) return;
            var blank = Image.CreateEmpty(Mathf.RoundToInt(want), 1, false, Image.Format.Rgba8);
            button.Icon = ImageTexture.CreateFromImage(blank);
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
