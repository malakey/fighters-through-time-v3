using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-SELECT (pulp UI redesign, CARD-B): the trading-card grid, the paper stat cards, the player stars and
    /// speech bubbles, and the pulp chrome of both phases. Tiles stay cursor-driven <see cref="Button"/>s; every
    /// visual sits on inner children (a tilt on the button itself would be reset by the grid's sort).
    /// </summary>
    public partial class CharacterSelectScreen {

        private const string PulpDir = "res://assets/sprites/ui/pulp/";
        private const float CardW = CardGridRules.CardWidth;
        private const float CardH = CardGridRules.CardHeight;

        /// <summary>The photo window's portrait layer: UI_SPEC 3.6, art at 132 % of the 188 x 204 window.</summary>
        public static readonly Vector2 CardPortraitSize = new(248f, 248f);

        private sealed class CardParts {
            public Control Scaler, Tilt, Stamp, Badge1, Badge2;
            public PanelContainer Frame, FrameBack;
            public Label Name, Dates;
        }

        private sealed class PlayerPanel {
            public Control Star;
            public Label Thinking;
            public PulpTaggedPanel Card;
            public Label Name, Stats;
            public Control Rows, SlotsRow;
            public readonly ProgressBar[] Bars = new ProgressBar[3];
            public readonly Label[] Values = new Label[3];
            public readonly PulpAbilitySlot[] Slots = new PulpAbilitySlot[4];
        }

        private CardParts[] _cards;
        private readonly PlayerPanel[] _players = new PlayerPanel[2];
        private ColorRect _stageScrim;
        private float _gridScale = 1f;
        private CardStatMaxima? _maxima;

        /// <summary>The stat bars' full scale: the largest HP, speed and weight in the roster, read from its data.</summary>
        public CardStatMaxima CardMaxima => _maxima ??= ComputeMaxima();

        /// <summary>The compact scale the card grid currently runs at (1 while it fits two rows).</summary>
        public float GridScale => _gridScale;

        /// <summary>Test seam: the stat bar of a player's paper card (row 0 HP, 1 Speed, 2 Weight).</summary>
        internal ProgressBar StatBar(int token, int row) => _players[token]?.Bars[row];

        /// <summary>Test seam: a player's paper card.</summary>
        internal PulpTaggedPanel StatCard(int token) => _players[token]?.Card;

        private CardStatMaxima ComputeMaxima() {
            var values = new List<(float, float, float)>();
            foreach (string id in _characterIDs) {
                var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
                if (data != null) values.Add((data.MaxHP, data.MaxMoveSpeed, data.Weight));
            }
            return CardStatMaxima.FromValues(values);
        }

        // === The card grid =====================================================

        private void BindCharacterGrid() {
            var grid = GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
            grid.Columns = GridColumns;
            _gridScale = CardGridRules.ScaleFor(TileCount);
            grid.AddThemeConstantOverride("h_separation", Mathf.RoundToInt(CardGridRules.ColumnGap * _gridScale));
            grid.AddThemeConstantOverride("v_separation", Mathf.RoundToInt(CardGridRules.RowGap * _gridScale));
            _characterButtons = new Button[TileCount];
            _tileBadges = new Label[TileCount];
            _cards = new CardParts[TileCount];
            // The grid grows with the roster: one card per manifest member plus the Random card, never a fixed ten.
            for (int index = 0; index < TileCount; index++) {
                int captured = index;
                bool random = index == RandomTileIndex;
                var button = new Button {
                    Name = random ? "RandomTile" : $"CharacterButton{index}",
                    Text = string.Empty,
                    CustomMinimumSize = new Vector2(CardW, CardH) * _gridScale,
                    FocusMode = FocusModeEnum.None
                };
                foreach (string box in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" }) {
                    button.AddThemeStyleboxOverride(box, new StyleBoxEmpty());
                }
                grid.AddChild(button);
                _cards[index] = BuildCard(button, index, random);
                _tileBadges[index] = new Label { Name = "TokenBadge", Visible = false, MouseFilter = MouseFilterEnum.Ignore };
                button.AddChild(_tileBadges[index]);
                // Cursor-driven, not focus-driven: see FocusChain.
                button.AddToGroup(FocusChainBuilder.SkipGroup);
                button.Pressed += () => OnTilePressed(captured);
                _characterButtons[index] = button;
            }
        }

        private CardParts BuildCard(Button button, int index, bool random) {
            var parts = new CardParts();
            parts.Scaler = new Control {
                Name = "Scaler", Size = new Vector2(CardW, CardH), Scale = new Vector2(_gridScale, _gridScale),
                MouseFilter = MouseFilterEnum.Ignore
            };
            button.AddChild(parts.Scaler);
            parts.Tilt = new Control {
                Name = "Tilt", Size = new Vector2(CardW, CardH), PivotOffset = new Vector2(CardW / 2f, CardH / 2f),
                MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps
            };
            parts.Scaler.AddChild(parts.Tilt);

            parts.FrameBack = MakeFrame("FrameBack", UIPalette.Variations.CardPlateP2);
            parts.FrameBack.Position = new Vector2(16f, 12f);
            parts.FrameBack.Rotation = Mathf.DegToRad(4f);
            parts.FrameBack.Visible = false;
            parts.Tilt.AddChild(parts.FrameBack);
            parts.Frame = MakeFrame("Frame", UIPalette.Variations.CardPlate);
            parts.Tilt.AddChild(parts.Frame);

            var photo = new Control {
                Name = "Photo", Position = new Vector2(4f, 4f), Size = new Vector2(188f, 204f),
                ClipContents = true, MouseFilter = MouseFilterEnum.Ignore
            };
            parts.Tilt.AddChild(photo);
            Texture2D portrait = random ? null : GetCharacterPortrait(index);
            if (random) {
                var silhouette = new ColorRect {
                    Name = "Silhouette", Color = new Color(0.106f, 0.09f, 0.141f), Size = photo.Size,
                    MouseFilter = MouseFilterEnum.Ignore
                };
                photo.AddChild(silhouette);
                silhouette.AddChild(new TextureRect {
                    Name = "Hatch", Texture = GD.Load<Texture2D>(PulpDir + "hatch_random.png"),
                    StretchMode = TextureRect.StretchModeEnum.Tile, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    Size = photo.Size, MouseFilter = MouseFilterEnum.Ignore
                });
                silhouette.AddChild(new Label {
                    Name = "RandomMark", Text = "?", ThemeTypeVariation = UIPalette.LabelRoles.CardQuestion,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    Size = photo.Size, MouseFilter = MouseFilterEnum.Ignore
                });
            } else {
                photo.AddChild(PhotoLayer("Field", "card_field.png", photo.Size));
                photo.AddChild(PhotoLayer("Halftone", "card_halftone.png", photo.Size));
            }
            photo.AddChild(new TextureRect {
                Name = "Portrait", Texture = portrait, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore,
                Position = new Vector2(-30f, -8f), Size = CardPortraitSize
            });

            parts.Name = new Label {
                Name = "CharacterName", Text = TileName(index), Uppercase = true,
                ThemeTypeVariation = UIPalette.LabelRoles.CardName, MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Position = new Vector2(0f, 210f), Size = new Vector2(196f, 32f), ClipText = true
            };
            parts.Tilt.AddChild(parts.Name);
            parts.Dates = new Label {
                Name = "CharacterDates", Text = TileDates(index), ThemeTypeVariation = UIPalette.LabelRoles.CardDates,
                MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, Position = new Vector2(0f, 240f), Size = new Vector2(196f, 28f), ClipText = true
            };
            parts.Tilt.AddChild(parts.Dates);

            parts.Stamp = new PanelContainer {
                Name = "Stamp", ThemeTypeVariation = UIPalette.Variations.StampPlate, MouseFilter = MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(180f, 64f), Size = new Vector2(180f, 64f),
                Position = new Vector2((CardW - 180f) / 2f, 132f), PivotOffset = new Vector2(90f, 32f),
                Rotation = Mathf.DegToRad(-10f), Visible = false
            };
            parts.Stamp.AddChild(new Label {
                Name = "StampText", Text = "fighter_ready_banner", Uppercase = true,
                ThemeTypeVariation = UIPalette.LabelRoles.CardStamp, MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            });
            parts.Tilt.AddChild(parts.Stamp);

            parts.Badge1 = BuildTokenBadge(0);
            parts.Badge2 = BuildTokenBadge(1);
            parts.Tilt.AddChild(parts.Badge1);
            parts.Tilt.AddChild(parts.Badge2);
            return parts;
        }

        private static PanelContainer MakeFrame(string name, string variation) {
            var frame = new PanelContainer {
                Name = name, ThemeTypeVariation = variation, Size = new Vector2(CardW, CardH),
                PivotOffset = new Vector2(CardW / 2f, CardH / 2f), MouseFilter = MouseFilterEnum.Ignore
            };
            return frame;
        }

        private static TextureRect PhotoLayer(string name, string file, Vector2 size) => new() {
            Name = name, Texture = GD.Load<Texture2D>(PulpDir + file), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, Size = size, MouseFilter = MouseFilterEnum.Ignore
        };

        /// <summary>The 50 px pick badge (UI_SPEC 3.6): an ink disc with a paper ring holding the slot shape, so a pick never reads by colour alone.</summary>
        private static Control BuildTokenBadge(int player) {
            var root = new Control {
                Name = $"Badge{player + 1}", Size = new Vector2(50f, 50f), Position = new Vector2(CardW - 30f, -20f),
                MouseFilter = MouseFilterEnum.Ignore, Visible = false, ZIndex = 2
            };
            var disc = new Panel { Name = "Disc", Size = root.Size, MouseFilter = MouseFilterEnum.Ignore };
            var box = new StyleBoxFlat { BgColor = UIPalette.LabelDark, BorderColor = UIPalette.Paper };
            box.SetBorderWidthAll(3);
            box.SetCornerRadiusAll(25);
            disc.AddThemeStyleboxOverride("panel", box);
            root.AddChild(disc);
            PulpIcon icon = PulpIcon.Create(player == 0 ? "slot_p1" : "slot_p2", player == 0 ? UIPalette.P1 : UIPalette.P2, 28f);
            icon.Name = "Shape";
            icon.ShadowEnabled = false;
            icon.Position = new Vector2(11f, 11f);
            root.AddChild(icon);
            return root;
        }

        private string TileDates(int index) {
            if (index == RandomTileIndex) return Tr("fighter_random_mystery");
            string key = $"character_{_characterIDs[index]}_dates";
            string text = Tr(key);
            return text == key ? string.Empty : text;
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
        /// Paints one card from the token states: the P1 or P2 frame with its tilt and lift on the hovered or locked
        /// card (a card both players hold shows both frames, the P2 one peeking out behind), the pick badges, and the
        /// READY stamp on a locked card.
        /// </summary>
        private void StyleTile(int index) {
            Button button = _characterButtons[index];
            CardParts card = _cards?[index];
            if (button == null || card == null) return;
            bool p1Here = _tokens[0].LockedIndex == index || (!_tokens[0].Ready && _tokens[0].Cursor == index);
            bool p2Here = OpponentCursorActive
                && (_tokens[1].LockedIndex == index || (!_tokens[1].Ready && _tokens[1].Cursor == index));
            bool locked = _tokens[0].LockedIndex == index || (OpponentCursorActive && _tokens[1].LockedIndex == index);

            card.Frame.ThemeTypeVariation = p1Here ? UIPalette.Variations.CardPlateP1
                : p2Here ? UIPalette.Variations.CardPlateP2 : UIPalette.Variations.CardPlate;
            card.FrameBack.Visible = p1Here && p2Here;
            if (p1Here) {
                card.Tilt.Position = new Vector2(0f, -10f);
                card.Tilt.Rotation = Mathf.DegToRad(-1.6f);
            } else if (p2Here) {
                card.Tilt.Position = Vector2.Zero;
                card.Tilt.Rotation = Mathf.DegToRad(1.6f);
            } else {
                card.Tilt.Position = Vector2.Zero;
                card.Tilt.Rotation = 0f;
            }
            card.Stamp.Visible = locked;
            card.Badge1.Visible = p1Here;
            card.Badge2.Visible = p2Here;
            card.Badge2.Position = new Vector2(CardW - 30f - (p1Here ? 56f : 0f), -20f);
            button.ZIndex = p1Here || p2Here ? 1 : 0;

            Label badge = _tileBadges[index];
            if (badge != null) {
                // The glyph text survives as screen-reader / test state; the drawn badges carry the look.
                badge.Text = (p1Here ? PlayerSlotPalettes.PlayerOneGlyph : "")
                    + (p2Here ? PlayerSlotPalettes.PlayerTwoGlyph : "");
            }
        }

        private bool OpponentCursorActive => !IsVersusCpuFlow && (IsPlayerTwoJoined || _tokens[1].Ready);

        // === The player panels: star, bubbles, paper stat card ===================

        private void BuildPlayerPanels() {
            for (int t = 0; t < 2; t++) {
                var panel = GetNode<VBoxContainer>(SelectRoot + $"PlayersRow/P{t + 1}Panel");
                var pp = new PlayerPanel();
                pp.Star = BuildStar(t);
                pp.Star.SizeFlagsHorizontal = t == 0 ? SizeFlags.ShrinkBegin : SizeFlags.ShrinkEnd;
                panel.AddChild(pp.Star);
                panel.MoveChild(pp.Star, 0);

                pp.Thinking = new Label {
                    Name = $"P{t + 1}Thinking", Text = "fighter_select_choosing", ThemeTypeVariation = UIPalette.LabelRoles.BubbleText,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    SizeFlagsHorizontal = t == 0 ? SizeFlags.ShrinkBegin : SizeFlags.ShrinkEnd, Uppercase = true
                };
                StyleBubble(pp.Thinking, new Vector2(214f, 74f), idle: true);
                panel.AddChild(pp.Thinking);
                panel.MoveChild(pp.Thinking, 1);
                StyleBubble(t == 0 ? _p1Ready : _p2Ready, new Vector2(237f, 74f), idle: false);
                (t == 0 ? _p1Ready : _p2Ready).Uppercase = true;

                panel.AddChild(new Control { Name = "Spacer", SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
                BuildStatCard(t, pp);
                panel.AddChild(pp.Card);
                _players[t] = pp;
            }
            _p1Name = _players[0].Name;
            _p1Stats = _players[0].Stats;
            _p2Name = _players[1].Name;
            _p2Stats = _players[1].Stats;
        }

        private static Control BuildStar(int player) {
            var star = new Control {
                Name = $"P{player + 1}Star", CustomMinimumSize = new Vector2(118f, 118f), MouseFilter = MouseFilterEnum.Ignore,
                TextureFilter = TextureFilterEnum.LinearWithMipmaps
            };
            foreach ((string layer, string file) in new[] { ("Shadow", "star_badge_sh.png"), ("Fill", "star_badge_fill.png"), ("Ink", "star_badge_ink.png") }) {
                var rect = new TextureRect {
                    Name = layer, Texture = GD.Load<Texture2D>(PulpDir + file), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale, Position = new Vector2(-4f, -4f), Size = new Vector2(131f, 132f),
                    MouseFilter = MouseFilterEnum.Ignore
                };
                if (layer == "Fill") rect.Modulate = player == 0 ? UIPalette.P1 : UIPalette.P2;
                star.AddChild(rect);
            }
            var text = new Label {
                Name = "StarText", ThemeTypeVariation = UIPalette.LabelRoles.StarBadgeText,
                Text = string.Format(TranslationServer.Translate("fighter_hud_player"), player + 1),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            text.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            star.AddChild(text);
            return star;
        }

        /// <summary>A speech bubble: the nine-slice bubble plate as the label's own box, the tail hanging from its bottom edge.</summary>
        private static void StyleBubble(Label bubble, Vector2 size, bool idle) {
            if (bubble == null) return;
            StyleBoxTexture plate = PulpKit.Plate("bubble");
            // The plate's nine-slice margins would otherwise double as content margins and fatten the bubble.
            plate.ContentMarginLeft = 16f; plate.ContentMarginRight = 16f; plate.ContentMarginTop = 0f; plate.ContentMarginBottom = 0f;
            bubble.AddThemeStyleboxOverride("normal", plate);
            bubble.CustomMinimumSize = size;
            bubble.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
            if (idle) bubble.Modulate = new Color("D8CDB2");
            var tail = new TextureRect {
                Name = "Tail", Texture = GD.Load<Texture2D>(PulpDir + "bubble_tail.png"), MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                AnchorTop = 1f, AnchorBottom = 1f, OffsetLeft = 34f, OffsetRight = 74f, OffsetTop = -5f, OffsetBottom = 23f
            };
            bubble.AddChild(tail);
        }

        private void BuildStatCard(int t, PlayerPanel pp) {
            pp.Card = new PulpTaggedPanel { Name = $"P{t + 1}Card" };
            var paper = new PanelContainer {
                Name = "Paper", ThemeTypeVariation = UIPalette.Variations.PaperPanel, CustomMinimumSize = new Vector2(360f, 260f)
            };
            pp.Card.AddChild(paper);
            var inset = new MarginContainer { Name = "Inset" };
            foreach (string side in new[] { "left", "top", "right", "bottom" }) inset.AddThemeConstantOverride("margin_" + side, 14);
            paper.AddChild(inset);
            var col = new VBoxContainer { Name = "Col" };
            col.AddThemeConstantOverride("separation", 8);
            inset.AddChild(col);

            pp.Name = new Label {
                Name = $"P{t + 1}Name", ThemeTypeVariation = UIPalette.LabelRoles.SideCardName, Uppercase = true,
                ClipText = true, CustomMinimumSize = new Vector2(0f, 34f), VerticalAlignment = VerticalAlignment.Center
            };
            col.AddChild(pp.Name);
            pp.Stats = new Label { Name = $"P{t + 1}Stats", Visible = false };
            col.AddChild(pp.Stats);

            var rows = new VBoxContainer { Name = "Rows" };
            rows.AddThemeConstantOverride("separation", 14);
            col.AddChild(rows);
            pp.Rows = rows;
            string[] labelKeys = { "fighter_stat_max_hp", "fighter_stat_speed", "fighter_stat_weight" };
            for (int row = 0; row < 3; row++) {
                var line = new HBoxContainer { Name = $"Row{row}" };
                line.AddThemeConstantOverride("separation", 16);
                rows.AddChild(line);
                line.AddChild(new Label {
                    Name = "Label", Text = labelKeys[row], Uppercase = true, ThemeTypeVariation = UIPalette.LabelRoles.StatLabel,
                    CustomMinimumSize = new Vector2(104f, 0f), VerticalAlignment = VerticalAlignment.Center
                });
                ProgressBar bar = MakeStatBar();
                line.AddChild(bar);
                pp.Bars[row] = bar;
                pp.Values[row] = new Label {
                    Name = "Value", ThemeTypeVariation = UIPalette.LabelRoles.StatValue, CustomMinimumSize = new Vector2(56f, 0f),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center
                };
                line.AddChild(pp.Values[row]);
            }

            var slots = new HBoxContainer { Name = "Slots", Alignment = BoxContainer.AlignmentMode.Center };
            slots.AddThemeConstantOverride("separation", 26);
            col.AddChild(slots);
            pp.SlotsRow = slots;
            for (int i = 0; i < 4; i++) {
                var slot = new PulpAbilitySlot { SlotSize = 50f, State = PulpSlotState.Ready };
                slots.AddChild(slot);
                pp.Slots[i] = slot;
            }
        }

        private static ProgressBar MakeStatBar() {
            var bar = new ProgressBar {
                Name = "Bar", ThemeTypeVariation = UIPalette.Variations.PulpBar, CustomMinimumSize = new Vector2(118f, 14f),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, ShowPercentage = false, MinValue = 0, MaxValue = 1, Step = 0.001
            };
            Theme theme = UIPalette.LoadTheme();
            // The track plate is the 14 px kit texture on the theme's own content margins; the fill stays a flat box.
            if (theme.GetStylebox("background", UIPalette.Variations.PulpBar) is StyleBoxTexture themed
                && PulpKit.Plate("bar_track_h14") is { } plate && themed.Duplicate() is StyleBoxTexture track) {
                track.Texture = plate.Texture;
                track.TextureMarginLeft = plate.TextureMarginLeft; track.TextureMarginTop = plate.TextureMarginTop;
                track.TextureMarginRight = plate.TextureMarginRight; track.TextureMarginBottom = plate.TextureMarginBottom;
                track.ExpandMarginLeft = plate.ExpandMarginLeft; track.ExpandMarginTop = plate.ExpandMarginTop;
                track.ExpandMarginRight = plate.ExpandMarginRight; track.ExpandMarginBottom = plate.ExpandMarginBottom;
                bar.AddThemeStyleboxOverride("background", track);
            }
            if (theme.GetStylebox("fill", UIPalette.Variations.PulpBar) is StyleBoxFlat fillThemed && fillThemed.Duplicate() is StyleBoxFlat fill) {
                fill.BgColor = UIPalette.Accent;
                bar.AddThemeStyleboxOverride("fill", fill);
            }
            PulpBarDecor decor = PulpBarDecor.Attach(bar);
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
            var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres");
            if (data == null) return Tr("fighter_stats_unavailable");
            return string.Format(Tr("fighter_stats_card"), data.MaxHP, data.MaxMoveSpeed, data.Weight, Tr(StyleKey(data.Style)));
        }

        private string TileStats(int index) =>
            index == RandomTileIndex ? Tr("fighter_random_character_hint") : StatsText(_characterIDs[index]);

        private void FillCard(PlayerPanel pp, int index) {
            pp.Name.Text = TileFullName(index);
            pp.Stats.Text = TileStats(index);
            bool real = index != RandomTileIndex && index >= 0 && index < _characterIDs.Length;
            pp.Rows.Visible = real;
            pp.SlotsRow.Visible = real;
            pp.Card.TagKey = "";
            if (!real) return;
            string id = _characterIDs[index];
            var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
            if (data == null) { pp.Rows.Visible = false; pp.SlotsRow.Visible = false; return; }
            pp.Card.TagKey = StyleKey(data.Style);
            CardStatMaxima max = CardMaxima;
            (double value, double cap, string text)[] stats = {
                (data.MaxHP, max.Hp, data.MaxHP.ToString("0")),
                (data.MaxMoveSpeed, max.Speed, data.MaxMoveSpeed.ToString("0.##")),
                (data.Weight, max.Weight, data.Weight.ToString("0.##"))
            };
            for (int row = 0; row < 3; row++) {
                pp.Bars[row].MaxValue = stats[row].cap > 0 ? stats[row].cap : 1;
                pp.Bars[row].Value = stats[row].value;
                pp.Values[row].Text = stats[row].text;
            }
            AbilitySlot[] order = { AbilitySlot.Special1, AbilitySlot.Special2, AbilitySlot.MovementAbility, AbilitySlot.Ultimate };
            for (int i = 0; i < 4; i++) pp.Slots[i].Art = AbilityIconLibrary.Load(id, order[i]);
        }

        private void RefreshPanels() {
            if (_players[0] == null) return;
            PlayerPanel p1 = _players[0];
            FillCard(p1, EffectiveIndex(_tokens[0]));
            bool joinPrompt = IsPlayerOneJoinPromptShown;
            _p1Ready.Visible = _tokens[0].Ready;
            p1.Thinking.Visible = !_tokens[0].Ready && !joinPrompt;
            // CM-PADUI (2026-10-16): Local Versus pre-claims no slot (CM-FIXA-INPUT),
            // so until a device presses, Player 1's panel says how to join too.
            if (_p1JoinPrompt != null) _p1JoinPrompt.Visible = joinPrompt;

            PlayerPanel p2 = _players[1];
            if (IsVersusCpuFlow) {
                // The CPU is configured on the next screen, not picked here.
                p2.Name.Text = Tr("fighter_versus_cpu_title");
                p2.Stats.Text = Tr("fighter_cpu_configured_next");
                p2.Card.TagKey = "";
                p2.Rows.Visible = false;
                p2.SlotsRow.Visible = false;
                p2.Card.Visible = true;
                _p2Ready.Visible = false;
                p2.Thinking.Visible = false;
                if (_p2JoinPrompt != null) _p2JoinPrompt.Visible = false;
                return;
            }
            // 9.1: an unjoined Player 2 shows only the press-to-join prompt.
            bool joined = OpponentCursorActive;
            if (_p2JoinPrompt != null) _p2JoinPrompt.Visible = !joined;
            p2.Card.Visible = joined;
            _p2Ready.Visible = joined && _tokens[1].Ready;
            p2.Thinking.Visible = joined && !_tokens[1].Ready;
            if (!joined) return;
            FillCard(p2, EffectiveIndex(_tokens[1]));
        }

        // === Chrome: banners, footer decor, prompt bar, stage phase ================

        private void StyleChrome() {
            _stageScrim = GetNode<ColorRect>("StageScrim");
            BuildPlayerPanels();
            StyleBanner(GetNode<Label>(SelectRoot + "Title"));
            StyleBanner(GetNode<Label>(StageRoot + "StageTitle"));

            foreach (string name in new[] { "MoveListButton", "SystemsCardButton", "BackButton" }) {
                PulpButtonDecor.Apply(GetNode<Button>(SelectRoot + "ButtonRow/" + name), compact: true);
            }
            var bar = new PulpPromptBar { Name = "PromptBar", SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
            bar.AddPrompt("ui_accept", "common_confirm");
            bar.AddPrompt("ui_cancel", "common_cancel");
            GetNode<Control>(SelectRoot.TrimEnd('/')).AddChild(bar);

            PulpButtonDecor.Apply(GetNode<Button>(StageRoot + "StageButtonRow/StageBackButton"));
            PulpButtonDecor.Apply(GetNode<Button>(StageRoot + "StageButtonRow/FightButton"));
            PulpToggleDecor.Apply(_meterPickups);
            PulpToggleDecor.Apply(_stageHazards);
        }

        /// <summary>The gold select banner behind a heading: the kit plate as the label's box, tilted a degree (re-applied after every container sort).</summary>
        private static void StyleBanner(Label title) {
            title.AddThemeStyleboxOverride("normal", PulpKit.Plate("banner_select"));
            title.CustomMinimumSize = new Vector2(844f, 90f);
            title.Uppercase = true;
            title.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
            if (title.GetParent() is Container parent) {
                parent.SortChildren += () => {
                    title.PivotOffset = title.Size / 2f;
                    title.Rotation = Mathf.DegToRad(-1f);
                };
            }
        }
    }
}
