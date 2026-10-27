using System.Collections.Generic;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// TG-MENU (Temporal Glass; UI_SPEC_GLASS 9.1, 9.2 "Main menu sub-screens"): the six sub-screens as full-screen menus
    /// over the sub-screen art and the screen streak, each a centred glass slab (its sheen, its tag, the 14 px stack inset,
    /// <see cref="MenuSafeAreaFit"/>) with its panel title between flourishes, chevron rows and a compact Back.
    /// <list type="bullet">
    /// <item><b>Slots</b>: three focusable glass slabs 1000 x 140 (a portrait frame 86, the hero's name T45, the summary
    /// lines T46 / T47; an empty slot shows <c>save_slot_empty</c> in ink2 on a quiet slab with a dashed edge), each with a
    /// compact Delete. The slot buttons keep their full summary as their (hidden) <c>Text</c>.</item>
    /// <item><b>Character</b>: the 3 x 3 glass tiles (3.7: the portrait window, the card name T35, the focus edge) and a side
    /// slab for the focused hero (a portrait frame 112, the name T40 with the D10 fit, the style as its tag).</item>
    /// <item><b>Difficulty</b>: three chevron rows and the focused row's description under the column.</item>
    /// <item><b>Level select</b> (debug builds): a 4 x 4 grid of compact chevrons, "NN  place".</item>
    /// <item><b>Extras</b>: a chevron column. <b>Licences</b>: one slab with the scrolling list.</item>
    /// </list>
    /// Every node path the screen scripts and tests bind (<c>&lt;Screen&gt;/Center/Panel/Layout/...</c>,
    /// <c>SlotRow{0-2}/SlotButton|DeleteButton</c>) is kept; the character grid moved into <c>Layout/Body</c> beside the
    /// hero slab.
    /// </summary>
    public partial class MainMenu {

        /// <summary>Each sub-screen's slab and the key its tag shows (the keys the pulp tags used).</summary>
        private static readonly (string Screen, string TagKey)[] SubScreenTags = {
            ("SlotScreen", "menu_story_mode"),
            ("CharacterScreen", "menu_story_mode"),
            ("DifficultyScreen", "menu_story_mode"),
            ("LevelSelectScreen", "menu_level_select"),
            ("ExtrasScreen", "game_title"),
            ("LicensesScreen", "game_title")
        };

        // ---- sizes (UI_SPEC_GLASS 3.2, 3.7, 9.2), design pixels at UI scale 1.0 ---------------------------------
        internal const float SlotSlabWidth = 1000f, SlotSlabHeight = 140f;
        internal const float SlotPortrait = 86f, SlotRowGap = 18f;
        internal const float CompactMinWidth = 290f;
        internal const float TileSize = 164f, TileNameBox = 156f, TileNameBottom = 11f;
        internal const float HeroSlabWidth = 440f, HeroPortrait = 112f;
        /// <summary>D10: a long hero name shrinks on one line down to 36 / 48 of its size before it wraps.</summary>
        internal const float HeroNameShrinkFloor = 0.75f;
        internal const int LevelColumns = 4;

        private readonly PulpPortraitRoundel[] _slotPortraits = new PulpPortraitRoundel[StorySlotCount];
        private readonly PanelContainer[] _slotSlabs = new PanelContainer[StorySlotCount];
        private readonly SlabFrame[] _slotFrames = new SlabFrame[StorySlotCount];
        private readonly bool[] _slotEmpty = new bool[StorySlotCount];
        private float _subScale = -1f;

        private PanelContainer _heroSlab;
        private PulpPortraitRoundel _heroPortrait;
        private Control _heroNameBox;
        private Label _heroName;
        private string _heroID = "";
        private Label _difficultyDescription;
        private string _difficultyDescriptionKey = "difficulty_easy_description";

        /// <summary>The hero the character screen's side slab shows. Test surface.</summary>
        internal string ShownHeroID => _heroID;

        private void DressSubScreens() {
            foreach ((string screen, string tag) in SubScreenTags) {
                if (GetNodeOrNull<PanelContainer>($"{screen}/Center/Panel") is not { } panel) continue;
                // The glass slab's sheen, its tag and the 14 px right inset that keeps a focused row's slide inside it (3.2).
                PulpScreenKit.DressPanel(panel, tag, sheen: true, buttonInset: true);
                // A tall panel at the maximum UI scale shrinks onto the screen instead of clipping.
                MenuSafeAreaFit.Attach(panel);
                if (panel.GetNodeOrNull<Label>("Layout/Title") is { } title) TitleFlourishes.Attach(title);
                if (panel.GetNodeOrNull<Button>("Layout/BackButton") is { } back) SetShowsPrompt(back, false);
            }
            // The level tiles are a grid of compact chevrons: no prompt badge, so each keeps its whole width for its label.
            if (GetNodeOrNull<Node>("LevelSelectScreen/Center/Panel/Layout/Grid") is { } levels) {
                for (int i = 0, n = levels.GetChildCount(); i < n; i++) if (levels.GetChild(i) is Button tile) SetShowsPrompt(tile, false);
            }
        }

        private static void SetShowsPrompt(Button button, bool show) {
            for (int i = 0, n = button.GetChildCount(); i < n; i++) {
                if (button.GetChild(i) is PulpButtonDecor decor) decor.ShowPrompt = show;
            }
        }

        // ---- sizes at the UI scale --------------------------------------------------------------------------------

        /// <summary>
        /// Applies the UI scale to the sub-screens: every chevron stack's D6 width (3.2: menu rows 560 to 700, compact 290,
        /// all x s), the slot slabs (140 x s tall, their frame, gaps and paddings x s; the width stays 1000), the hero slab's
        /// name box, the difficulty description's two-line height and the level tiles' width cap (four columns on screen).
        /// </summary>
        private void ApplySubScreenScale(float s) {
            var deletes = new List<Button>();
            for (int slot = 0; slot < StorySlotCount; slot++) if (_deleteButtons[slot] != null) deletes.Add(_deleteButtons[slot]);
            PulpButtonDecor.ApplyStackWidth(deletes, CompactMinWidth * s);
            foreach ((string screen, _) in SubScreenTags) {
                if (GetNodeOrNull<Button>($"{screen}/Center/Panel/Layout/BackButton") is { } back) {
                    PulpButtonDecor.ApplyStackWidth(new[] { back }, CompactMinWidth * s, reservePrompt: false);
                }
            }
            StackRows("DifficultyScreen/Center/Panel/Layout", s);
            StackRows("ExtrasScreen/Center/Panel/Layout", s);
            ApplyLevelTileWidth(s);

            if (Mathf.IsEqualApprox(s, _subScale)) return;
            _subScale = s;
            for (int slot = 0; slot < StorySlotCount; slot++) {
                Button button = _slotButtons[slot];
                if (button == null || _slotSlabs[slot] == null) continue;
                button.CustomMinimumSize = new Vector2(SlotSlabWidth, Mathf.Round(SlotSlabHeight * s));
                _slotPortraits[slot].Diameter = SlotPortrait * s;
                _slotSlabs[slot].GetNode<BoxContainer>("Row").AddThemeConstantOverride("separation", Mathf.RoundToInt(SlotRowGap * s));
                PaintSlotFill(slot);
            }
            if (_heroPortrait != null) _heroPortrait.Diameter = HeroPortrait * s;
            if (_heroNameBox != null && _heroName != null) FitHeroName();
            if (_difficultyDescription != null) {
                Font font = _difficultyDescription.GetThemeFont("font");
                int size = _difficultyDescription.GetThemeFontSize("font_size");
                float line = font?.GetHeight(size) ?? size;
                float twoLines = 2f * line + _difficultyDescription.GetThemeConstant("line_spacing");
                _difficultyDescription.CustomMinimumSize = new Vector2(_difficultyDescription.CustomMinimumSize.X, Mathf.Ceil(twoLines));
            }
        }

        /// <summary>A sub-screen's chevron rows (its non-compact buttons) share the D6 width: 560 to 700, x s (3.2).</summary>
        private void StackRows(string layoutPath, float s) {
            if (GetNodeOrNull<Node>(layoutPath) is not { } layout) return;
            var rows = new List<Button>();
            for (int i = 0, n = layout.GetChildCount(); i < n; i++) {
                if (layout.GetChild(i) is Button button && button.ThemeTypeVariation != UIPalette.Variations.PulpButtonCompact) rows.Add(button);
            }
            PulpButtonDecor.ApplyStackWidth(rows, ColumnMinWidth * s, ColumnCap * s);
        }

        /// <summary>
        /// The level tiles share one width: their widest label (no prompt), at least 290, at most what keeps four columns and
        /// their 34 px gaps inside the screen's safe area and the slab's paddings; a label that is still too long shrinks,
        /// then ellipsizes in its decor.
        /// </summary>
        private void ApplyLevelTileWidth(float s) {
            if (GetNodeOrNull<GridContainer>("LevelSelectScreen/Center/Panel/Layout/Grid") is not { } grid) return;
            var tiles = new List<Button>();
            for (int i = 0, n = grid.GetChildCount(); i < n; i++) if (grid.GetChild(i) is Button tile) tiles.Add(tile);
            if (tiles.Count == 0) return;
            var panel = GetNodeOrNull<PanelContainer>("LevelSelectScreen/Center/Panel");
            StyleBox plate = panel?.GetThemeStylebox("panel");
            float chrome = (plate?.GetContentMargin(Side.Left) ?? 0f) + (plate?.GetContentMargin(Side.Right) ?? 0f);
            float screen = Size.X > 0f ? Size.X : 1920f;
            int columns = Mathf.Max(1, grid.Columns);
            float gaps = (columns - 1) * grid.GetThemeConstant("h_separation");
            float cap = (screen - 2f * MenuSafeAreaFit.MarginPixels - chrome - gaps) / columns;
            PulpButtonDecor.ApplyStackWidth(tiles, Mathf.Min(CompactMinWidth * s, cap), cap, reservePrompt: false);
        }

        // ---- slots (9.2) ------------------------------------------------------------------------------------------

        /// <summary>Builds slot <paramref name="slot"/>'s glass slab: the portrait frame, the sheen, the focus / dashed frame.</summary>
        private void BuildSlotSlab(int slot) {
            Button button = _slotButtons[slot];
            var slab = button.GetNodeOrNull<PanelContainer>("Slab");
            if (slab == null) return;
            _slotSlabs[slot] = slab;
            var row = slab.GetNode<BoxContainer>("Row");
            var portrait = new PulpPortraitRoundel { Name = "Portrait", Diameter = SlotPortrait, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            row.AddChild(portrait);
            row.MoveChild(portrait, 0);
            _slotPortraits[slot] = portrait;
            GlassSheen.Attach(slab);
            var frame = new SlabFrame { Name = "SlabFrame" };
            button.AddChild(frame);
            frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _slotFrames[slot] = frame;
            button.FocusEntered += () => PaintSlotFrame(slot);
            button.FocusExited += () => PaintSlotFrame(slot);
            // Hover is focus (AC8).
            button.MouseEntered += () => {
                if (button.IsVisibleInTree() && !button.HasFocus()) button.GrabFocus();
            };
        }

        /// <summary>Paints slot <paramref name="slot"/>'s slab from its save (null: the empty slab).</summary>
        private void PaintSlotSlab(int slot, FTT.Core.StorySaveData save, string summary) {
            PanelContainer slab = _slotSlabs[slot];
            if (slab == null) return;
            var lines = slab.GetNode<Node>("Row/Lines");
            var name = lines.GetNode<Label>("Name");
            var first = lines.GetNode<Label>("Summary");
            var detail = lines.GetNode<Label>("Detail");
            var lowest = lines.GetNode<Label>("Lowest");
            _slotEmpty[slot] = save == null;
            if (save == null) {
                _slotPortraits[slot].Visible = false;
                name.Visible = detail.Visible = lowest.Visible = false;
                first.Text = summary;
                first.AddThemeColorOverride("font_color", UIPalette.Ink2);
            } else {
                _slotPortraits[slot].Visible = true;
                _slotPortraits[slot].Portrait = CharacterDataFor(save.SelectedCharacterID)?.CharacterPortrait;
                name.Visible = true;
                name.Text = CharacterName(save.SelectedCharacterID);
                first.RemoveThemeColorOverride("font_color");
                // The summary's lines (save_slot_summary, then the G14 lowest-difficulty line): T46 for the first, T47 below.
                string[] parts = summary.Split('\n');
                first.Text = parts[0];
                detail.Text = parts.Length > 2 ? string.Join("  ", parts, 1, parts.Length - 2) : "";
                lowest.Text = parts.Length > 1 ? parts[^1] : "";
                detail.Visible = detail.Text.Length > 0;
                lowest.Visible = lowest.Text.Length > 0;
            }
            PaintSlotFill(slot);
            PaintSlotFrame(slot);
        }

        // A filled slot is the glass slab (the theme's continue-card plate); an empty one the theme's quiet empty slab
        // (GlassPanelEmpty, folded by TG-CLOSE) with the frame's dashed edge; either with its paddings x s.
        private void PaintSlotFill(int slot) {
            PanelContainer slab = _slotSlabs[slot];
            if (slab == null) return;
            slab.ThemeTypeVariation = _slotEmpty[slot] ? UIPalette.Variations.GlassPanelEmpty : UIPalette.Variations.GlassPanelContinue;
            ScalePlatePaddings(slab, MenuScale);
            if (slab.GetNodeOrNull<GlassSheen>(GlassSheen.NodeName) is { } sheen) sheen.Visible = !_slotEmpty[slot];
        }

        private void PaintSlotFrame(int slot) {
            SlabFrame frame = _slotFrames[slot];
            if (frame == null) return;
            frame.Mode = _slotButtons[slot].HasFocus() ? SlabFrame.FrameMode.Focus
                : _slotEmpty[slot] ? SlabFrame.FrameMode.Dashed : SlabFrame.FrameMode.None;
        }

        // ---- character (3.7, 9.2) -------------------------------------------------------------------------------

        /// <summary>A character tile: the glass tile (chamfer 16, the portrait window, the focus edge), the card name T35.</summary>
        private void DressCharacterTile(Button button, string characterID) {
            GlassTileDecor tile = GlassTileDecor.Attach(button);
            tile.Portrait = CharacterDataFor(characterID)?.CharacterPortrait;
            button.FocusEntered += () => OnCharacterTileFocused(button, characterID);
            button.FocusExited += () => PaintTileName(button);
            PaintTileName(button);
        }

        private void PaintTileName(Button button) {
            if (button.GetNodeOrNull<Label>("CardName") is not { } name) return;
            // A focused tile's name is white (3.7: a picked tile); otherwise the role's ink2.
            if (button.HasFocus()) name.AddThemeColorOverride("font_color", UIPalette.TextFocus);
            else name.RemoveThemeColorOverride("font_color");
        }

        /// <summary>Writes every tile's card name (the short name, <c>character_&lt;id&gt;_card_name</c>) and fits it in 156 px.</summary>
        private void PaintTileNames() {
            if (GetNodeOrNull<GridContainer>("CharacterScreen/Center/Panel/Layout/Body/Grid") is not { } grid) return;
            for (int index = 0; index < RosterIDs.Length; index++) {
                if (grid.GetNodeOrNull<Button>($"CharacterButton{index}") is not { } button) continue;
                if (button.GetNodeOrNull<Label>("CardName") is not { } name) continue;
                name.Text = Tr($"character_{RosterIDs[index]}_card_name");
                name.ResetSize();
                float height = name.GetCombinedMinimumSize().Y;
                name.Position = new Vector2(0f, TileSize - TileNameBottom - height);
                name.Size = new Vector2(TileNameBox, height);
                CardTextFit.Fit(name, (TileSize - TileNameBox) / 2f, TileNameBox, centred: true);
            }
        }

        private void BindHeroSlab() {
            _heroSlab = GetNodeOrNull<PanelContainer>("CharacterScreen/Center/Panel/Layout/Body/HeroSlab");
            if (_heroSlab == null) return;
            var layout = _heroSlab.GetNode<BoxContainer>("HeroLayout");
            _heroPortrait = new PulpPortraitRoundel { Name = "HeroPortrait", Diameter = HeroPortrait, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
            layout.AddChild(_heroPortrait);
            layout.MoveChild(_heroPortrait, 0);
            _heroNameBox = layout.GetNode<Control>("NameBox");
            _heroName = _heroNameBox.GetNode<Label>("HeroName");
            PulpScreenKit.DressPanel(_heroSlab, "movelist_style_hybrid");
        }

        private void OnCharacterTileFocused(Button button, string characterID) {
            PaintTileName(button);
            ShowHero(characterID);
        }

        /// <summary>The side slab shows <paramref name="characterID"/>: its portrait, its name (D10 fit) and its style as the tag.</summary>
        private void ShowHero(string characterID) {
            if (_heroSlab == null) return;
            _heroID = characterID ?? "";
            FTT.Characters.CharacterData data = CharacterDataFor(characterID);
            _heroPortrait.Portrait = data?.CharacterPortrait;
            _heroName.Text = CharacterName(characterID);
            PulpScreenKit.AddTag(_heroSlab, StyleKey(data?.Style ?? FTT.Characters.CombatStyle.Hybrid));
            FitHeroName();
        }

        private static string StyleKey(FTT.Characters.CombatStyle style) => style switch {
            FTT.Characters.CombatStyle.Melee => "movelist_style_melee",
            FTT.Characters.CombatStyle.Ranged => "movelist_style_ranged",
            _ => "movelist_style_hybrid"
        };

        /// <summary>
        /// D10 (UI_SPEC_GLASS 3.7, AC5) in the side slab's name box: one line at T40's size when it fits; else shrink on one
        /// line down to 36 / 48; else two lines at full size, shrinking further only when a word or a third line still does
        /// not fit. The label is scaled as a node (never a font-size override); the box keeps two full lines of height.
        /// </summary>
        private void FitHeroName() {
            if (_heroName == null || _heroNameBox == null) return;
            float s = MenuScale;
            float box = Mathf.Max(1f, _heroNameBox.CustomMinimumSize.X);
            Font font = _heroName.GetThemeFont("font");
            int size = _heroName.GetThemeFontSize("font_size");
            if (font == null) return;
            float line = font.GetHeight(size);
            float spacing = _heroName.GetThemeConstant("line_spacing");
            float boxHeight = Mathf.Ceil(2f * line + spacing);
            _heroNameBox.CustomMinimumSize = new Vector2(HeroSlabTextWidth(s), boxHeight);
            box = _heroNameBox.CustomMinimumSize.X;
            string drawn = PulpButtonDecor.ToDrawnText(_heroName.Text);
            float one = font.GetStringSize(drawn, HorizontalAlignment.Left, -1f, size).X;
            float scale = 1f;
            int lines = 1;
            if (one > box) {
                if (one * HeroNameShrinkFloor <= box) {
                    scale = box / one;
                } else {
                    lines = 2;
                    for (; scale > 0.5f; scale -= 0.02f) {
                        if (FitsInLines(drawn, font, size, box / scale, 2)) break;
                    }
                }
            }
            _heroName.AutowrapMode = lines > 1 ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
            float width = box / scale;
            float height = lines > 1 ? 2f * line + spacing : line;
            _heroName.Size = new Vector2(width, height);
            _heroName.PivotOffset = Vector2.Zero;
            _heroName.Scale = new Vector2(scale, scale);
            _heroName.Position = new Vector2(0f, (boxHeight - height * scale) / 2f);
        }

        /// <summary>The side slab's content width: 440 less its paddings, at the UI scale's font growth (the slab grows with it).</summary>
        private float HeroSlabTextWidth(float s) {
            StyleBox plate = _heroSlab?.GetThemeStylebox("panel");
            float chrome = (plate?.GetContentMargin(Side.Left) ?? 0f) + (plate?.GetContentMargin(Side.Right) ?? 0f);
            return Mathf.Round(HeroSlabWidth * s - chrome);
        }

        private static bool FitsInLines(string text, Font font, int size, float width, int maxLines) {
            var paragraph = new TextParagraph { Width = width, BreakFlags = TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound };
            paragraph.AddString(text, font, size);
            if (paragraph.GetLineCount() > maxLines) return false;
            for (int i = 0; i < paragraph.GetLineCount(); i++) {
                if (paragraph.GetLineWidth(i) > width + 0.5f) return false;
            }
            return true;
        }

        // ---- difficulty (9.2) -------------------------------------------------------------------------------------

        private void ShowDifficultyDescription(string descriptionKey) {
            _difficultyDescriptionKey = descriptionKey;
            if (_difficultyDescription != null) _difficultyDescription.Text = Tr(descriptionKey);
        }

        // ---- locale changes -------------------------------------------------------------------------------------------

        /// <summary>Repaints the texts the sub-screens resolve in code (names, descriptions, level tiles, slots).</summary>
        private void RefreshSubScreenTexts() {
            PaintTileNames();
            if (_heroID.Length > 0) ShowHero(_heroID);
            ShowDifficultyDescription(_difficultyDescriptionKey);
            PaintLevelTiles();
            if (_slotScreen != null && _slotScreen.Visible) RefreshSlotRows();
        }
    }
}
