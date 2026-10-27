using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using FTT.UI.Motion;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// TG-SELECT (Temporal Glass, CARD-A; UI_SPEC_GLASS 3.7, 4.2; the pulp cards were UR-SELECT's). The glass tile grid, the
    /// side heads with their READY badges, the glass stat panels with their tubes and lenses, the hero sprites, and the chrome of
    /// both phases. Tiles stay cursor-driven <see cref="Button"/>s outside the focus chain; their look is a
    /// <see cref="GlassTileDecor"/> and child labels, so nothing transforms the buttons themselves.
    /// </summary>
    public partial class CharacterSelectScreen {

        /// <summary>The tile name's box (3.7: centred in 156 px, its bottom 11 px above the tile's bottom).</summary>
        public const float TileNameBox = 156f, TileNameBottomInset = 11f;
        /// <summary>The side columns (4.2, AC4): 460 wide, 40 from the screen edges; the heads from y 132, the stat panels ending at y 1011.8.</summary>
        public const float SideWidth = 460f, SideMargin = 40f, HeadTop = 132f, PanelBottom = 1011.8f, HeadWidth = 440f;
        /// <summary>The hero sprites' pivot x (4.2: 260 for P1, 1660 for P2).</summary>
        public const float HeroPivotP1 = 260f, HeroPivotP2 = 1660f;
        /// <summary>The stat panel internals (3.7, 4.2): rows 28.8 tall 14 apart, the label column, the 170 x 14 tube, the value column; three 130 px ability cells 9 apart with a 56 px lens and the name 8 px under it.</summary>
        public const float StatRowHeight = 28.8f, StatRowGap = 14f, StatLabelWidth = 104f, StatValueWidth = 56f;
        public static readonly Vector2 StatTubeSize = new(170f, 14f);
        public const float AbilityCellWidth = 130f, AbilityCellGap = 9f, AbilityLensSize = 56f, AbilityNameGap = 8f, AbilityRowGap = 18f;
        /// <summary>The ability lenses' slots (P9: three, as the mockup draws them).</summary>
        public static readonly AbilitySlot[] SidePanelAbilities = { AbilitySlot.Special1, AbilitySlot.Special2, AbilitySlot.MovementAbility };

        private sealed class TileParts {
            public Button Button;
            public GlassTileDecor Decor;
            public Label Name;
            public Label Mark;   // the Random tile's "?"
        }

        private sealed class SidePanel {
            public Control Head, NameBox, Gap;
            public Label PlayerLabel, Name, Thinking, Stats, CpuNote;
            public PulpIcon Shape;
            public PanelContainer Card;
            public Control Rows, Lenses;
            public readonly ProgressBar[] Bars = new ProgressBar[3];
            public readonly Label[] Values = new Label[3];
            public readonly PulpAbilitySlot[] Slots = new PulpAbilitySlot[3];
            public readonly Label[] AbilityNames = new Label[3];
            public SelectHeroSprite Hero;
            public int ShownIndex = int.MinValue;
            public bool ReadyShown;
            public Tween ReadyFade;
        }

        private TileParts[] _tiles;
        private readonly SidePanel[] _sides = new SidePanel[2];
        private ColorRect _stageScrim;
        private CardGridRules.GridLayout _gridLayout;
        private CardStatMaxima? _maxima;
        private Control _playersRow;
        private GridContainer _grid;
        private Label _title;
        private Control _buttonRow;
        private Label _hint;
        private Label _remoteNotice;
        private PulpPromptBar _promptBar;
        private StyleBoxFlat _readyGlowBox;
        private readonly List<Button> _stageTiles = new();

        /// <summary>The stat tubes' full scale: the largest HP, speed and weight in the roster, read from its data.</summary>
        public CardStatMaxima CardMaxima => _maxima ??= ComputeMaxima();

        /// <summary>The grid's layout for this roster (<see cref="CardGridRules.LayoutFor"/>).</summary>
        public CardGridRules.GridLayout GridLayout => _gridLayout;

        /// <summary>Test seams: a player's stat tube (row 0 HP, 1 Speed, 2 Weight), side panel, head name, idle badge, lenses and hero sprite.</summary>
        internal ProgressBar StatBar(int token, int row) => _sides[token]?.Bars[row];
        internal PanelContainer StatPanel(int token) => _sides[token]?.Card;
        internal Label HeadName(int token) => _sides[token]?.Name;
        internal Label IdleBadge(int token) => _sides[token]?.Thinking;
        internal PulpAbilitySlot AbilityLens(int token, int index) => _sides[token]?.Slots[index];
        internal Label AbilityNameLabel(int token, int index) => _sides[token]?.AbilityNames[index];
        internal SelectHeroSprite HeroSprite(int token) => _sides[token]?.Hero;
        internal IReadOnlyList<Button> StageTiles => _stageTiles;

        /// <summary>The style tag a player's side panel shows (empty when hidden).</summary>
        internal string StatPanelTag(int token) =>
            _sides[token]?.Card?.GetNodeOrNull<PanelTagAnchor>(PulpScreenKit.TagAnchorName)?.TagLabel?.Text ?? "";

        private CardStatMaxima ComputeMaxima() {
            var values = new List<(float, float, float)>();
            foreach (string id in _characterIDs) {
                CharacterData data = DataFor(id);
                if (data != null) values.Add((data.MaxHP, data.MaxMoveSpeed, data.Weight));
            }
            return CardStatMaxima.FromValues(values);
        }

        private static CharacterData DataFor(string id) =>
            AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");

        // === The tile grid (3.7 G4, 4.2 element 5) ===============================

        private void BindCharacterGrid() {
            _grid = GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
            _gridLayout = CardGridRules.LayoutFor(_characterIDs.Length);
            _grid.Columns = GridColumns;
            int gap = Mathf.RoundToInt(_gridLayout.Gap);
            _grid.AddThemeConstantOverride("h_separation", gap);
            _grid.AddThemeConstantOverride("v_separation", gap);
            _characterButtons = new Button[TileCount];
            _tileBadges = new Label[TileCount];
            _tiles = new TileParts[TileCount];
            // One tile per manifest member plus the Random tile: the roster is never counted here.
            for (int index = 0; index < TileCount; index++) {
                int captured = index;
                bool random = index == RandomTileIndex;
                var button = new Button {
                    Name = random ? "RandomTile" : $"CharacterButton{index}",
                    Text = string.Empty,
                    Flat = true,
                    FocusMode = FocusModeEnum.None,
                    CustomMinimumSize = random
                        ? new Vector2(_gridLayout.Tile, CardGridRules.RandomHeight)
                        : new Vector2(_gridLayout.Tile, _gridLayout.Tile)
                };
                foreach (string box in TileBoxSlots) button.AddThemeStyleboxOverride(box, TileBox);
                _grid.AddChild(button);
                _tiles[index] = random ? BuildRandomTile(button) : BuildTile(button, index);
                _tileBadges[index] = new Label { Name = "TokenBadge", Visible = false, MouseFilter = MouseFilterEnum.Ignore };
                button.AddChild(_tileBadges[index]);
                // Cursor-driven, not focus-driven: see FocusChain.
                button.AddToGroup(FocusChainBuilder.SkipGroup);
                button.Pressed += () => OnTilePressed(captured);
                _characterButtons[index] = button;
            }
            // The Random tile spans the grid on a row of its own, below the roster's rows (4.2 element 5.rnd).
            _grid.SortChildren += PlaceRandomTile;
        }

        private static readonly string[] TileBoxSlots = { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" };
        private static readonly StyleBoxEmpty TileBox = new();

        private TileParts BuildTile(Button button, int index) {
            var parts = new TileParts { Button = button };
            parts.Decor = GlassTileDecor.Attach(button, GlassTileDecor.SelectChamfer);
            parts.Decor.Portrait = DataFor(_characterIDs[index])?.CharacterPortrait;
            parts.Name = new Label {
                Name = "CharacterName", Text = TileName(index), Uppercase = true,
                ThemeTypeVariation = UIPalette.LabelRoles.CardName, MouseFilter = MouseFilterEnum.Ignore,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            button.AddChild(parts.Name);
            button.MoveChild(parts.Name, 1);
            return parts;
        }

        /// <summary>The Random tile (3.7): the same glass, a centred row of "?" (T38) and the random word (T57), 22 px apart.</summary>
        private TileParts BuildRandomTile(Button button) {
            var parts = new TileParts { Button = button };
            parts.Decor = GlassTileDecor.Attach(button, GlassTileDecor.SelectChamfer);
            parts.Mark = new Label {
                Name = "RandomMark", Text = "?", ThemeTypeVariation = UIPalette.LabelRoles.CardQuestion,
                MouseFilter = MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                VerticalAlignment = VerticalAlignment.Center
            };
            parts.Name = new Label {
                Name = "CharacterName", Text = Tr("fighter_random_character"), Uppercase = true,
                ThemeTypeVariation = UIPalette.LabelRoles.RandomWord, MouseFilter = MouseFilterEnum.Ignore,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled, VerticalAlignment = VerticalAlignment.Center
            };
            button.AddChild(parts.Mark);
            button.AddChild(parts.Name);
            button.MoveChild(parts.Mark, 1);
            button.MoveChild(parts.Name, 2);
            return parts;
        }

        /// <summary>Puts the Random tile on its own full-width row and lays out every tile's text (after the grid sorts).</summary>
        private void PlaceRandomTile() {
            if (_tiles == null || _grid == null) return;
            TileParts random = _tiles[RandomTileIndex];
            random.Button.Position = new Vector2(0f, _gridLayout.RandomTop);
            random.Button.Size = new Vector2(_gridLayout.Width, CardGridRules.RandomHeight);
            LayoutTileText();
        }

        private void LayoutTileText() {
            if (_tiles == null) return;
            foreach (TileParts tile in _tiles) {
                if (tile == null || !IsInstanceValid(tile.Button)) continue;
                Vector2 size = tile.Button.Size;
                if (tile.Mark != null) {
                    // The "?" and the word as one centred row, 22 px apart.
                    float mark = tile.Mark.GetMinimumSize().X, word = CardTextFit.TextWidth(tile.Name);
                    float left = (size.X - mark - 22f - word) / 2f;
                    tile.Mark.Size = new Vector2(mark, size.Y);
                    tile.Mark.Position = new Vector2(left, 0f);
                    tile.Name.Size = new Vector2(word + 2f, size.Y);
                    tile.Name.Position = new Vector2(left + mark + 22f, 0f);
                    continue;
                }
                float height = tile.Name.GetMinimumSize().Y;
                float box = Mathf.Max(1f, size.X - (CardGridRules.TileSize - TileNameBox));
                tile.Name.Size = new Vector2(tile.Name.Size.X, height);
                tile.Name.Position = new Vector2(tile.Name.Position.X, size.Y - TileNameBottomInset - (height + 18f) / 2f);
                CardTextFit.Fit(tile.Name, (size.X - box) / 2f, box, centred: true);
            }
        }

        private string TileName(int index) {
            if (index == RandomTileIndex) return Tr("fighter_random_character");
            string id = _characterIDs[index];
            foreach (string key in new[] { $"character_{id}_card_name", $"hero_name_{id}" }) {
                string text = Tr(key);
                if (text != key) return text;
            }
            return GetCharacterName(index);
        }

        private string TileFullName(int index) =>
            index == RandomTileIndex ? Tr("fighter_random_character") : GetCharacterName(index);

        /// <summary>The badge glyphs on a tile: ▲ for P1, ● for P2, both when shared. Test seam.</summary>
        public string TileBadgeText(int index) =>
            _tileBadges != null && index >= 0 && index < _tileBadges.Length && _tileBadges[index] != null
                ? _tileBadges[index].Text
                : "";

        /// <summary>
        /// Paints one tile from the token states (3.7): the owners' edge, glow and shape badges on the hovered or locked tile
        /// (both on a tile both players hold, AC25), the lock mark of the player who locked it, and a white name on a picked
        /// tile. The colours come from the active player-slot palette inside <see cref="GlassTileDecor"/>.
        /// </summary>
        private void StyleTile(int index) {
            TileParts tile = _tiles?[index];
            if (tile == null) return;
            bool p1Here = _tokens[0].LockedIndex == index || (!_tokens[0].Ready && _tokens[0].Cursor == index);
            bool p2Here = OpponentCursorActive
                && (_tokens[1].LockedIndex == index || (!_tokens[1].Ready && _tokens[1].Cursor == index));
            tile.Decor.Owners = (p1Here ? GlassTileDecor.OwnerP1 : 0) | (p2Here ? GlassTileDecor.OwnerP2 : 0);
            tile.Decor.LockedBy = _tokens[0].LockedIndex == index ? 0
                : OpponentCursorActive && _tokens[1].LockedIndex == index ? 1 : -1;
            if (p1Here || p2Here) tile.Name.AddThemeColorOverride("font_color", UIPalette.TextFocus);
            else tile.Name.RemoveThemeColorOverride("font_color");

            Label badge = _tileBadges[index];
            if (badge != null) {
                // The glyph text survives as screen-reader / test state; the decor draws the shapes.
                badge.Text = (p1Here ? PlayerSlotPalettes.PlayerOneGlyph : "")
                    + (p2Here ? PlayerSlotPalettes.PlayerTwoGlyph : "");
            }
        }

        private bool OpponentCursorActive => !IsVersusCpuFlow && (IsPlayerTwoJoined || _tokens[1].Ready);

        // === The side heads and stat panels (3.7, 4.2 elements 6-7) ================

        private void BuildSides() {
            for (int t = 0; t < 2; t++) {
                bool right = t == 1;
                var column = GetNode<VBoxContainer>(SelectRoot + $"PlayersRow/P{t + 1}Panel");
                Label ready = t == 0 ? _p1Ready : _p2Ready;
                Label join = t == 0 ? _p1JoinPrompt : _p2JoinPrompt;
                var side = new SidePanel();
                SizeFlags along = right ? SizeFlags.ShrinkEnd : SizeFlags.ShrinkBegin;

                column.AddChild(Spacer("TopMargin", HeadTop));
                column.MoveChild(column.GetChild(column.GetChildCount() - 1), 0);

                // The head row (12 px gap): the slot shape (a 22 px glyph in a 30 px box) and "PLAYER n" (T14 on the scene).
                var head = new HBoxContainer { Name = "Head", MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = along };
                head.AddThemeConstantOverride("separation", 12);
                var shapeBox = new Control { Name = "Shape", CustomMinimumSize = new Vector2(30f, 30f), MouseFilter = MouseFilterEnum.Ignore };
                side.Shape = PulpIcon.Create(right ? "slot_p2" : "slot_p1", UIPalette.OwnerOnDark(t), 22f);
                side.Shape.Name = "SlotShape";
                side.Shape.Position = new Vector2(4f, 4f);
                shapeBox.AddChild(side.Shape);
                side.PlayerLabel = new Label {
                    Name = "PlayerLabel", Text = right ? "loading_player_two" : "loading_player_one", Uppercase = true,
                    ThemeTypeVariation = UIPalette.LabelRoles.SceneCaption, VerticalAlignment = VerticalAlignment.Center,
                    MouseFilter = MouseFilterEnum.Ignore
                };
                if (right) { head.AddChild(side.PlayerLabel); head.AddChild(shapeBox); }
                else { head.AddChild(shapeBox); head.AddChild(side.PlayerLabel); }
                column.AddChild(head);
                column.MoveChild(head, 1);
                side.Head = head;

                // The hero name (T40) in a plain box so its D10 scale sticks (a container would reset it).
                side.NameBox = new Control { Name = "NameBox", MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(HeadWidth, 52f), SizeFlagsHorizontal = along };
                side.Name = new Label {
                    Name = $"P{t + 1}Name", ThemeTypeVariation = UIPalette.LabelRoles.SideCardName, Uppercase = true,
                    AutoTranslateMode = AutoTranslateModeEnum.Disabled, MouseFilter = MouseFilterEnum.Ignore,
                    VerticalAlignment = VerticalAlignment.Center
                };
                side.NameBox.AddChild(side.Name);
                column.AddChild(side.NameBox);
                column.MoveChild(side.NameBox, 2);
                side.Gap = Spacer("BadgeGap", 12f);
                column.AddChild(side.Gap);
                column.MoveChild(side.Gap, 3);

                // The READY badge (G12, T37) and its idle twin, "HMM..." (R4), are labels on the theme's badge plates.
                StyleReadyBadge(ready, idle: false);
                ready.SizeFlagsHorizontal = along;
                column.MoveChild(ready, 4);
                side.Thinking = new Label {
                    Name = $"P{t + 1}Thinking", Text = "fighter_select_choosing", Uppercase = true,
                    ThemeTypeVariation = UIPalette.LabelRoles.CardStamp, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = along, MouseFilter = MouseFilterEnum.Ignore
                };
                StyleReadyBadge(side.Thinking, idle: true);
                column.AddChild(side.Thinking);
                column.MoveChild(side.Thinking, 5);
                join.SizeFlagsHorizontal = along;
                column.MoveChild(join, 6);

                column.AddChild(new Control { Name = "Spacer", SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
                BuildStatPanel(t, side);
                column.AddChild(side.Card);
                column.AddChild(Spacer("BottomMargin", 1080f - PanelBottom));

                side.Hero = new SelectHeroSprite { Name = $"P{t + 1}Hero", Mirrored = right };
                side.Hero.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                GetNode<Control>("SelectPhase/HeroLayer").AddChild(side.Hero);
                _sides[t] = side;
            }
            _p1Name = _sides[0].Name;
            _p1Stats = _sides[0].Stats;
            _p2Name = _sides[1].Name;
            _p2Stats = _sides[1].Stats;
        }

        private static Control Spacer(string name, float height) =>
            new() { Name = name, CustomMinimumSize = new Vector2(0f, height), MouseFilter = MouseFilterEnum.Ignore };

        /// <summary>
        /// A READY badge (3.7 G12, 6.2) on a label: the theme's <c>ReadyBadge</c> / <c>ReadyBadgeIdle</c> plate without its
        /// shadow, so the translucent badge does not fill with its own glow (TG-THEME deviation 7), and the ready badge's 18 px
        /// glow drawn by a ring behind it (<see cref="ReadyGlow"/>). The text is T37 (<c>CardStamp</c>): <c>accent</c> ready,
        /// <c>ink2</c> idle.
        /// </summary>
        private void StyleReadyBadge(Label badge, bool idle) {
            if (badge == null) return;
            Theme theme = UIPalette.LoadTheme();
            string variation = idle ? UIPalette.Variations.ReadyBadgeIdle : UIPalette.Variations.ReadyBadge;
            if (theme?.GetStylebox("panel", variation) is StyleBoxFlat plate) {
                var own = (StyleBoxFlat)plate.Duplicate();
                if (!idle) {
                    _readyGlowBox ??= new StyleBoxFlat {
                        DrawCenter = false, BgColor = new Color(0, 0, 0, 0), ShadowColor = plate.ShadowColor,
                        ShadowSize = plate.ShadowSize, ShadowOffset = plate.ShadowOffset
                    };
                    badge.AddChild(new ReadyGlow(_readyGlowBox) { Name = "Glow" });
                }
                own.ShadowSize = 0;
                badge.AddThemeStyleboxOverride("normal", own);
            }
            if (idle) badge.AddThemeColorOverride("font_color", UIPalette.Ink2);
        }

        /// <summary>The READY badge's glow alone (6.3: 18 px, rgba(glow,.30)), drawn behind its label as a ring with no centre.</summary>
        private partial class ReadyGlow : Control {
            private readonly StyleBoxFlat _box;
            public ReadyGlow(StyleBoxFlat box) {
                _box = box;
                MouseFilter = MouseFilterEnum.Ignore;
                ShowBehindParent = true;
                SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            }
            public override void _Draw() => DrawStyleBox(_box, new Rect2(Vector2.Zero, Size));
        }

        private void BuildStatPanel(int t, SidePanel side) {
            side.Card = new PanelContainer {
                Name = $"P{t + 1}Card", ThemeTypeVariation = UIPalette.Variations.GlassPanelStats,
                CustomMinimumSize = new Vector2(SideWidth, 0f), MouseFilter = MouseFilterEnum.Ignore
            };
            PulpScreenKit.DressPanel(side.Card, null, sheen: true);
            var col = new VBoxContainer { Name = "Col", MouseFilter = MouseFilterEnum.Ignore };
            col.AddThemeConstantOverride("separation", 0);
            side.Card.AddChild(col);
            side.Stats = new Label { Name = $"P{t + 1}Stats", Visible = false, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
            col.AddChild(side.Stats);
            side.CpuNote = new Label {
                Name = "CpuNote", Text = "fighter_cpu_configured_next", ThemeTypeVariation = UIPalette.LabelRoles.BodySmallLabel,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false, MouseFilter = MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(SideWidth - 52f, 0f)
            };
            col.AddChild(side.CpuNote);

            var rows = new VBoxContainer { Name = "Rows", MouseFilter = MouseFilterEnum.Ignore };
            rows.AddThemeConstantOverride("separation", (int)StatRowGap);
            col.AddChild(rows);
            side.Rows = rows;
            string[] labelKeys = { "fighter_stat_max_hp", "fighter_stat_speed", "fighter_stat_weight" };
            for (int row = 0; row < 3; row++) {
                // Label, tube and value spread across the panel's 408 px (3.7: "spread by space-between").
                var line = new HBoxContainer { Name = $"Row{row}", CustomMinimumSize = new Vector2(0f, StatRowHeight), MouseFilter = MouseFilterEnum.Ignore };
                line.AddThemeConstantOverride("separation", 0);
                rows.AddChild(line);
                line.AddChild(new Label {
                    Name = "Label", Text = labelKeys[row], Uppercase = true, ThemeTypeVariation = UIPalette.LabelRoles.StatLabel,
                    CustomMinimumSize = new Vector2(StatLabelWidth, 0f), VerticalAlignment = VerticalAlignment.Center,
                    MouseFilter = MouseFilterEnum.Ignore
                });
                line.AddChild(new Control { Name = "GapA", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
                ProgressBar bar = MakeStatTube();
                line.AddChild(bar);
                side.Bars[row] = bar;
                line.AddChild(new Control { Name = "GapB", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
                side.Values[row] = new Label {
                    Name = "Value", ThemeTypeVariation = UIPalette.LabelRoles.StatValue, CustomMinimumSize = new Vector2(StatValueWidth, 0f),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                    AutoTranslateMode = AutoTranslateModeEnum.Disabled, MouseFilter = MouseFilterEnum.Ignore
                };
                line.AddChild(side.Values[row]);
            }

            // Three 130 px cells, 9 apart, 18 px under the rows: a 56 px lens (ready, no key) and the ability's name (T56).
            var lenses = new HBoxContainer { Name = "Lenses", MouseFilter = MouseFilterEnum.Ignore };
            lenses.AddThemeConstantOverride("separation", (int)AbilityCellGap);
            col.AddChild(Spacer("LensGap", AbilityRowGap));
            col.AddChild(lenses);
            side.Lenses = lenses;
            for (int i = 0; i < SidePanelAbilities.Length; i++) {
                var cell = new VBoxContainer { Name = $"Cell{i}", CustomMinimumSize = new Vector2(AbilityCellWidth, 0f), MouseFilter = MouseFilterEnum.Ignore };
                cell.AddThemeConstantOverride("separation", (int)AbilityNameGap);
                lenses.AddChild(cell);
                var slot = new PulpAbilitySlot { Name = "Lens", SlotSize = AbilityLensSize, State = PulpSlotState.Ready, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
                cell.AddChild(slot);
                side.Slots[i] = slot;
                // The name (T56, two lines at most) in a plain box so its fit scale sticks: a word never breaks (a large UI
                // scale shrinks the name instead, CardTextFit.FitWrappedName).
                var nameBox = new Control { Name = "NameBox", CustomMinimumSize = new Vector2(AbilityCellWidth, 0f), MouseFilter = MouseFilterEnum.Ignore };
                cell.AddChild(nameBox);
                var name = new Label {
                    Name = "AbilityName", ThemeTypeVariation = UIPalette.LabelRoles.AbilityName, Uppercase = true,
                    HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    MaxLinesVisible = AbilityNameMaxLines, MouseFilter = MouseFilterEnum.Ignore
                };
                nameBox.AddChild(name);
                side.AbilityNames[i] = name;
            }
        }

        /// <summary>An ability name's line limit under its lens (3.7: two).</summary>
        public const int AbilityNameMaxLines = 2;

        /// <summary>
        /// Fits a side's three ability names to their cells (after a text or UI-scale change), all three at the smallest fit
        /// among them, so the row reads at one size.
        /// </summary>
        private static void FitAbilityNames(SidePanel side) {
            if (side == null) return;
            float shared = 1f;
            foreach (Label name in side.AbilityNames) {
                if (name == null || !IsInstanceValid(name) || !name.IsInsideTree()) return;
                CardTextFit.FitWrappedName(name, AbilityCellWidth, AbilityNameMaxLines);
                shared = Mathf.Min(shared, name.Scale.X);
            }
            foreach (Label name in side.AbilityNames) {
                float height = CardTextFit.FitWrappedName(name, AbilityCellWidth, AbilityNameMaxLines, shared);
                var box = (Control)name.GetParent();
                if (!Mathf.IsEqualApprox(box.CustomMinimumSize.Y, height)) box.CustomMinimumSize = new Vector2(AbilityCellWidth, height);
            }
        }

        private static ProgressBar MakeStatTube() {
            var bar = new ProgressBar {
                Name = "Bar", ThemeTypeVariation = UIPalette.Variations.PulpBarH14, CustomMinimumSize = StatTubeSize,
                SizeFlagsVertical = SizeFlags.ShrinkCenter, ShowPercentage = false, MinValue = 0, MaxValue = 1, Step = 0.001,
                MouseFilter = MouseFilterEnum.Ignore
            };
            PulpBarDecor decor = PulpBarDecor.Attach(bar, PulpBarStyle.Stat);
            decor.LagValue = 0f;
            decor.EchoValue = 0f;
            return bar;
        }

        private static string StyleKey(CombatStyle style) => style switch {
            CombatStyle.Melee => "movelist_style_melee",
            CombatStyle.Ranged => "movelist_style_ranged",
            _ => "movelist_style_hybrid"
        };

        private string StatsText(string characterID) {
            CharacterData data = DataFor(characterID);
            if (data == null) return Tr("fighter_stats_unavailable");
            return string.Format(Tr("fighter_stats_card"), data.MaxHP, data.MaxMoveSpeed, data.Weight, Tr(StyleKey(data.Style)));
        }

        private string TileStats(int index) =>
            index == RandomTileIndex ? Tr("fighter_random_character_hint") : StatsText(_characterIDs[index]);

        /// <summary>The D10 fit of a head's hero name (shrink to 36 / 48 on one line, then two lines), its box following the height.</summary>
        private static void FitHeadName(SidePanel side, bool right) {
            if (side?.Name == null || !IsInstanceValid(side.Name) || !side.Name.IsInsideTree()) return;
            CardTextFit.FitHeroName(side.Name, HeadWidth, right);
            // The box is T40's line box (4.2 6a: 51.8 = 48 x 1.08 for one line), not Godot's taller font height: the role's
            // negative line spacing is the difference, and the label hangs half of it above and below the box, so the
            // glyphs sit where the mock draws them and the badge follows 12 px under the box (at y 225.8).
            Label name = side.Name;
            float spacing = name.GetThemeConstant("line_spacing");
            float scale = name.Scale.Y, height = name.Size.Y;
            float box = Mathf.Max(1f, (height + spacing) * scale);
            // One line is scaled about its middle, two lines about their top edge (CardTextFit.FitHeroName).
            bool wrapped = name.AutowrapMode != TextServer.AutowrapMode.Off;
            name.Position = new Vector2(name.Position.X, wrapped ? spacing * scale / 2f : (box - height) / 2f);
            side.NameBox.CustomMinimumSize = new Vector2(HeadWidth, box);
        }

        private void FillSide(SidePanel side, int t, int index) {
            bool real = index != RandomTileIndex && index >= 0 && index < _characterIDs.Length;
            if (side.ShownIndex != index) {
                side.ShownIndex = index;
                side.Name.Text = TileFullName(index);
                FitHeadName(side, t == 1);
                side.Stats.Text = TileStats(index);
                side.Rows.Visible = real;
                side.Lenses.Visible = real;
                side.CpuNote.Visible = false;
                CharacterData data = real ? DataFor(_characterIDs[index]) : null;
                PulpScreenKit.AddTag(side.Card, data == null ? "" : StyleKey(data.Style));
                if (data != null) {
                    CardStatMaxima max = CardMaxima;
                    (double value, double cap, string text)[] stats = {
                        (data.MaxHP, max.Hp, data.MaxHP.ToString("0")),
                        (data.MaxMoveSpeed, max.Speed, data.MaxMoveSpeed.ToString("0.##")),
                        (data.Weight, max.Weight, data.Weight.ToString("0.##"))
                    };
                    for (int row = 0; row < 3; row++) {
                        side.Bars[row].MaxValue = stats[row].cap > 0 ? stats[row].cap : 1;
                        side.Bars[row].Value = stats[row].value;
                        side.Values[row].Text = stats[row].text;
                    }
                    AbilityData[] abilities = { data.SpecialAttackOne, data.SpecialAttackTwo, data.MovementAbility };
                    for (int i = 0; i < SidePanelAbilities.Length; i++) {
                        side.Slots[i].Art = AbilityIconLibrary.Load(_characterIDs[index], SidePanelAbilities[i]);
                        side.AbilityNames[i].Text = abilities[i]?.DisplayNameKey ?? "";
                    }
                    FitAbilityNames(side);
                } else {
                    side.Rows.Visible = false;
                    side.Lenses.Visible = false;
                }
            }
            // The Random tile shows no hero; a real one stands beside its player's side.
            side.Hero.ShowHero(real ? _characterIDs[index] : null);
        }

        private void RefreshPanels() {
            if (_sides[0] == null) return;
            SidePanel p1 = _sides[0];
            FillSide(p1, 0, EffectiveIndex(_tokens[0]));
            // CM-PADUI (2026-10-16): Local Versus pre-claims no slot (CM-FIXA-INPUT), so until a device presses, Player 1's
            // head says how to join, in place of the name and the badge (4.2).
            // A mouse click acts as Player 1 before any device has joined, so a lock still shows its name and READY.
            bool joinPrompt = IsPlayerOneJoinPromptShown;
            SetJoinState(p1, _p1JoinPrompt, joinPrompt, _tokens[0].Ready);
            ShowBadge(p1, _p1Ready, _tokens[0].Ready, !joinPrompt && !_tokens[0].Ready);

            SidePanel p2 = _sides[1];
            if (IsVersusCpuFlow) {
                // The CPU is configured on the next screen, not picked here.
                p2.ShownIndex = int.MinValue;
                p2.Name.Text = Tr("fighter_versus_cpu_title");
                FitHeadName(p2, true);
                p2.Stats.Text = Tr("fighter_cpu_configured_next");
                PulpScreenKit.AddTag(p2.Card, "");
                p2.Rows.Visible = false;
                p2.Lenses.Visible = false;
                p2.CpuNote.Visible = true;
                p2.Card.Visible = true;
                p2.Hero.ShowHero(null);
                SetJoinState(p2, _p2JoinPrompt, false);
                ShowBadge(p2, _p2Ready, false, false);
                return;
            }
            // 9.1: an unjoined Player 2 shows only the press-to-join prompt: no name, badge, panel, hero or token.
            bool joined = OpponentCursorActive;
            SetJoinState(p2, _p2JoinPrompt, !joined);
            p2.Card.Visible = joined;
            ShowBadge(p2, _p2Ready, joined && _tokens[1].Ready, joined && !_tokens[1].Ready);
            if (!joined) {
                p2.Hero.ShowHero(null);
                return;
            }
            FillSide(p2, 1, EffectiveIndex(_tokens[1]));
        }

        private static void SetJoinState(SidePanel side, Label join, bool prompt, bool locked = false) {
            if (join != null) join.Visible = prompt;
            side.NameBox.Visible = !prompt || locked;
            side.Gap.Visible = !prompt || locked;
        }

        /// <summary>Shows the READY badge (fading in over 120 ms, MOT-A row 15; at once under Reduced Temporal Effects) or the idle badge.</summary>
        private void ShowBadge(SidePanel side, Label ready, bool readyShown, bool idleShown) {
            side.Thinking.Visible = idleShown;
            if (ready == null) return;
            bool was = side.ReadyShown;
            side.ReadyShown = readyShown;
            ready.Visible = readyShown;
            if (!readyShown || was) return;
            side.ReadyFade?.Kill();
            if (PulpMotion.Reduced || !IsInsideTree()) {
                ready.Modulate = Colors.White;
                return;
            }
            ready.Modulate = new Color(1f, 1f, 1f, 0f);
            side.ReadyFade = PulpMotion.NewTween(ready);
            side.ReadyFade.TweenProperty(ready, "modulate:a", 1f, PulpMotionRules.CrossFadeSeconds);
        }

        // === Layout (4.2's anchor model) =============================================

        /// <summary>
        /// Places what no container places (4.2): the grid's top at the layout's line (the row centres it vertically, so its
        /// height reserves the space below it), the title's row centre at y 68, the hero sprites' feet, and the bottom stack
        /// (footer, hint, prompt bar, Remote Play notice) from the screen's bottom up at the current UI scale, with the
        /// countdown between the grid and the footer. Runs after a resize or a theme change, deferred once.
        /// </summary>
        private void LayoutSelect() {
            _layoutQueued = false;
            if (!IsInsideTree() || _playersRow == null) return;
            LayoutTileText();
            for (int t = 0; t < _sides.Length; t++) {
                if (_sides[t] == null) continue;
                FitHeadName(_sides[t], t == 1);
                FitAbilityNames(_sides[t]);
            }
            float screenH = _playersRow.Size.Y > 0f ? _playersRow.Size.Y : 1080f;
            float gridH = Mathf.Max(_gridLayout.BlockHeight, screenH - 2f * _gridLayout.Top);
            if (!Mathf.IsEqualApprox(_grid.CustomMinimumSize.Y, gridH) || !Mathf.IsEqualApprox(_grid.CustomMinimumSize.X, _gridLayout.Width)) {
                _grid.CustomMinimumSize = new Vector2(_gridLayout.Width, gridH);
            }

            float titleH = Mathf.Max(56f, _title.GetMinimumSize().Y);
            _title.OffsetTop = TitleCentreY - titleH / 2f;
            _title.OffsetBottom = TitleCentreY + titleH / 2f;

            foreach (SidePanel side in _sides) if (side != null) side.Hero.FeetPoint = new Vector2(side == _sides[0] ? HeroPivotP1 : HeroPivotP2, SelectHeroSprite.SelectFeetY);

            float scale = Mathf.Max(1f, UIPalette.AppliedUiScale);
            float bottom = screenH - NoticeBottomMargin;
            // The notice is focusable (a pad player reaches it to read it): it is as wide as its sentence, so its focus
            // frame hugs the text, and wraps only past the screen's side margins.
            float screenW = _playersRow.Size.X > 0f ? _playersRow.Size.X : 1920f;
            float noticeW = Mathf.Min(NoticeTextWidth() + 2f * NoticePadding, screenW - 2f * SideMargin);
            if (!Mathf.IsEqualApprox(_remoteNotice.OffsetRight - _remoteNotice.OffsetLeft, noticeW)) {
                _remoteNotice.OffsetLeft = -noticeW / 2f;
                _remoteNotice.OffsetRight = noticeW / 2f;
            }
            float noticeH = _remoteNotice.GetMinimumSize().Y;
            Place(_remoteNotice, bottom - noticeH, noticeH);
            float promptH = Mathf.Max(PulpPromptBar.GlyphHeight, _promptBar.GetCombinedMinimumSize().Y);
            float promptTop = bottom - noticeH - NoticeGap - promptH;
            Place(_promptBar, promptTop, promptH);
            _hint.Size = new Vector2(_hint.Size.X, 0f);
            float hintH = _hint.GetMinimumSize().Y;
            float footerH = Mathf.Max(58f * scale, _buttonRow.GetCombinedMinimumSize().Y);
            // The spec's gaps at 100 % (hint 20 under the footer, the prompt bar 20 under the hint), tightened toward 6 px only
            // when a large UI scale would push the footer into the grid.
            float gap = StackGap;
            float footerTop = promptTop - gap - hintH - gap - footerH;
            float floorTop = _gridLayout.Bottom + MinFooterClearance;
            if (footerTop < floorTop) {
                gap = Mathf.Max(MinStackGap, StackGap - (floorTop - footerTop) / 2f);
                footerTop = promptTop - gap - hintH - gap - footerH;
            }
            Place(_hint, promptTop - gap - hintH, hintH);
            Place(_buttonRow, footerTop, footerH);

            // The countdown (T59) centred between the Random tile and the footer, or under the title when they leave no room.
            float countH = _countdownLabel.GetMinimumSize().Y;
            float room = footerTop - _gridLayout.Bottom;
            float countCentre = room >= countH + 8f ? (_gridLayout.Bottom + footerTop) / 2f
                : (TitleCentreY + titleH / 2f + _gridLayout.Top) / 2f;
            Place(_countdownLabel, countCentre - countH / 2f, countH);
        }

        /// <summary>The title row's centre (4.2: the 56 px title box from y 40; the flourish diamonds at y 68).</summary>
        public const float TitleCentreY = 68f;
        public const float StackGap = 20f, MinStackGap = 6f, NoticeGap = 8f, NoticeBottomMargin = 8f, MinFooterClearance = 4f;
        /// <summary>The Remote Play notice's room either side of its sentence (its focus frame's inset).</summary>
        public const float NoticePadding = 12f;

        private float NoticeTextWidth() {
            Font font = _remoteNotice.GetThemeFont("font");
            int size = _remoteNotice.GetThemeFontSize("font_size");
            return font == null ? 0f : font.GetStringSize(Tr(_remoteNotice.Text), HorizontalAlignment.Left, -1f, size).X;
        }

        // Every placed control hangs from the screen's top edge: its offsets are screen y.
        private static void Place(Control control, float top, float height) {
            if (control.AnchorTop != 0f || control.AnchorBottom != 0f) {
                control.AnchorTop = 0f;
                control.AnchorBottom = 0f;
            }
            control.OffsetTop = top;
            control.OffsetBottom = top + height;
        }

        private bool _layoutQueued;

        private void QueueLayout() {
            if (_layoutQueued) return;
            _layoutQueued = true;
            Callable.From(LayoutSelect).CallDeferred();
        }

        public override void _Notification(int what) {
            // Text sizes follow the UI scale. The change reaches this root before its labels (their font sizes are cached
            // until they are notified), so the tile names, the head names and the stack are laid out once, deferred.
            if (what == NotificationThemeChanged && IsInsideTree()) QueueLayout();
        }

        // === Chrome: the title, footer decor, prompt bar, the streak, both phases ===================

        private void StyleChrome() {
            _stageScrim = GetNode<ColorRect>("StageScrim");
            _playersRow = GetNode<Control>(SelectRoot + "PlayersRow");
            _title = GetNode<Label>(SelectRoot + "Title");
            _buttonRow = GetNode<Control>(SelectRoot + "ButtonRow");
            _hint = GetNode<Label>(SelectRoot + "HintLabel");
            _remoteNotice = GetNode<Label>(SelectRoot + "RemotePlayNotice");
            BuildSides();

            // The title between its flourishes (3.7), placed round the centred text so the label keeps its path.
            TitleFlourishes.Attach(_title);
            TitleFlourishes.Attach(GetNode<Label>(StageRoot + "StageTitle"));
            // The Remote Play notice is a sentence on the scene: the small body copy with the scene halo (2.4).
            PulpScreenKit.ApplySceneHalo(_remoteNotice, large: false);

            // The footer (P1): three compact chevrons 280 wide, 34 px apart, labels shrinking to fit (the compact floor is
            // 4.2's 18 px at every scale), no prompt badge (D6). They wear the compact footer chevron, whose label runs 30 px
            // short of the right edge rather than the compact plate's 40, as the mock's does (TG-CLOSE folded the local box).
            var footer = new List<Button>();
            foreach (string name in new[] { "MoveListButton", "SystemsCardButton", "BackButton" }) {
                Button button = GetNode<Button>(SelectRoot + "ButtonRow/" + name);
                button.ThemeTypeVariation = UIPalette.Variations.PulpButtonCompactFooter;
                PulpButtonDecor decor = PulpButtonDecor.Apply(button, compact: true);
                decor.ShowPrompt = false;
                footer.Add(button);
            }
            PulpButtonDecor.ApplyStackWidth(footer, FooterButtonWidth, FooterButtonWidth, reservePrompt: false);
            PulpScreenKit.SeparateFocusRow((BoxContainer)_buttonRow);

            _promptBar = new PulpPromptBar { Name = "PromptBar" };
            _promptBar.AddPrompt("ui_accept", "common_confirm");
            _promptBar.AddPrompt("ui_cancel", "common_cancel");
            GetNode<Control>(SelectRoot.TrimEnd('/')).AddChild(_promptBar);
            _promptBar.AnchorLeft = 0.5f;
            _promptBar.AnchorRight = 0.5f;
            _promptBar.GrowHorizontal = GrowDirection.Both;

            StyleStagePhase();
            PulpScreenKit.AddScreenStreak(this);

            _playersRow.Resized += QueueLayout;
            _hint.Resized += QueueLayout;
            _remoteNotice.Resized += QueueLayout;
            _promptBar.Resized += QueueLayout;
            LayoutTileText();
            QueueLayout();
        }

        /// <summary>The footer buttons' width (plan 0.4 P1: 280 so three fit 34 px apart between the side panels).</summary>
        public const float FooterButtonWidth = 280f;

        // === The stage phase (9.2) ========================================================

        /// <summary>A stage preview tile (9.2: 260 x 180, chamfer 16).</summary>
        public static readonly Vector2 StageTileSize = new(260f, 180f);

        private void StyleStagePhase() {
            foreach (string name in new[] { "StageBackButton", "FightButton" }) {
                PulpButtonDecor decor = PulpButtonDecor.Apply(GetNode<Button>(StageRoot + "StageButtonRow/" + name), compact: true);
                decor.ShowPrompt = false;
            }
            PulpScreenKit.SeparateFocusRow(GetNode<BoxContainer>(StageRoot + "StageButtonRow"));
            PulpToggleDecor.Apply(_meterPickups);
            PulpToggleDecor.Apply(_stageHazards);
            PulpScreenKit.AddPromptBar(_stagePhase, ("ui_accept", "ui_prompt_select"), ("ui_cancel", "common_back"));
            BuildStageTiles();
        }

        /// <summary>
        /// The stage tiles (9.2): one glass tile per stage in the list (the Random stage last), the preview plate in the
        /// window, the name (T35) and the Open / Sealed tag with its <c>stage_layout_*</c> tooltip. They mirror the stage
        /// field: the selected stage's tile is lit, and a click selects it. Like the character tiles they stay out of the focus
        /// chain; the field is the keyboard and pad route.
        /// </summary>
        private void BuildStageTiles() {
            var grid = GetNode<GridContainer>(StageRoot + "StageGrid");
            foreach (Button old in _stageTiles) if (IsInstanceValid(old)) old.QueueFree();
            _stageTiles.Clear();
            for (int item = 0; item < _stageSelect.ItemCount; item++) {
                int itemIndex = item;
                int id = _stageSelect.GetItemId(item);
                bool random = id == RandomStageItemID;
                FighterStageData stage = random || id < 0 || id >= _stageIDs.Count ? null : _stageCatalog?.Find(_stageIDs[id]);
                var tile = new Button {
                    Name = random ? "RandomStage" : $"Stage{id}", Flat = true, FocusMode = FocusModeEnum.None,
                    CustomMinimumSize = StageTileSize, TooltipText = _stageSelect.GetItemTooltip(item)
                };
                foreach (string box in TileBoxSlots) tile.AddThemeStyleboxOverride(box, TileBox);
                grid.AddChild(tile);
                GlassTileDecor.Attach(tile, GlassTileDecor.SelectChamfer);
                var window = new Control {
                    Name = "Window", Position = new Vector2(8f, 8f), Size = StageTileSize - new Vector2(16f, 50f),
                    ClipContents = true, MouseFilter = MouseFilterEnum.Ignore
                };
                tile.AddChild(window);
                if (stage != null && !string.IsNullOrWhiteSpace(stage.PreviewTexturePath) && ResourceLoader.Exists(stage.PreviewTexturePath)) {
                    window.AddChild(new TextureRect {
                        Name = "Preview", Texture = ResourceLoader.Load<Texture2D>(stage.PreviewTexturePath),
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                        Size = window.Size, MouseFilter = MouseFilterEnum.Ignore
                    });
                } else if (random) {
                    var mark = new Label {
                        Name = "RandomMark", Text = "?", ThemeTypeVariation = UIPalette.LabelRoles.CardQuestion,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                        AutoTranslateMode = AutoTranslateModeEnum.Disabled, Size = window.Size, MouseFilter = MouseFilterEnum.Ignore
                    };
                    window.AddChild(mark);
                }
                var name = new Label {
                    Name = "StageName", Text = random ? Tr("fighter_random_stage") : stage != null ? Tr(stage.DisplayNameKey) : "",
                    Uppercase = true, ThemeTypeVariation = UIPalette.LabelRoles.CardName, AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    MouseFilter = MouseFilterEnum.Ignore
                };
                tile.AddChild(name);
                void FitName() {
                    if (!IsInstanceValid(name)) return;
                    float height = name.GetMinimumSize().Y;
                    name.Size = new Vector2(name.Size.X, height);
                    name.Position = new Vector2(name.Position.X, StageTileSize.Y - TileNameBottomInset - (height + 18f) / 2f);
                    CardTextFit.Fit(name, 8f, StageTileSize.X - 16f, centred: true, CardTextFit.StageNameShrinkFloor);
                }
                name.ThemeChanged += () => Callable.From(FitName).CallDeferred();
                Callable.From(FitName).CallDeferred();
                if (stage != null) {
                    PanelContainer tag = PulpTagPlate.Create(out Label tagLabel);
                    tag.Name = "LayoutTag";
                    tagLabel.Text = FighterStageLayoutBadge.LabelKey(stage);
                    tag.TooltipText = Tr(FighterStageLayoutBadge.TooltipKey(stage));
                    tag.Visible = true;
                    tile.AddChild(tag);
                    Callable.From(() => { if (IsInstanceValid(tag)) PulpTagPlate.Place(tag, PulpTagPlate.PanelCornerOffset - new Vector2(2f, 0f), StageTileSize.X - PulpTagPlate.RightMargin); }).CallDeferred();
                }
                tile.AddToGroup(FocusChainBuilder.SkipGroup);
                tile.Pressed += () => {
                    _stageSelect.Select(itemIndex);
                    UpdateStagePreview();
                    PaintStageTiles();
                };
                _stageTiles.Add(tile);
            }
            PaintStageTiles();
        }

        /// <summary>Lights the tile of the stage the field shows; the rest stay dim.</summary>
        private void PaintStageTiles() {
            int selected = _stageSelect?.Selected ?? -1;
            for (int i = 0; i < _stageTiles.Count; i++) {
                if (!IsInstanceValid(_stageTiles[i])) continue;
                GlassTileDecor decor = _stageTiles[i].GetNodeOrNull<GlassTileDecor>(GlassTileDecor.NodeName);
                if (decor != null) decor.Edge = i == selected ? GlassTileDecor.TileEdge.Lit : GlassTileDecor.TileEdge.Dim;
                if (_stageTiles[i].GetNodeOrNull<Label>("StageName") is { } label) {
                    if (i == selected) label.AddThemeColorOverride("font_color", UIPalette.TextFocus);
                    else label.RemoveThemeColorOverride("font_color");
                }
            }
        }
    }
}
