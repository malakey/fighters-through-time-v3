using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// TG-SELECT (Temporal Glass, CARD-A; UI_SPEC_GLASS 3.7, 4.2, 9.2; first written by UR-SELECT for the pulp cards). The glass
/// select: the 3-column tile grid with the Random tile on its own row, the tile names, the side heads with the D10 name fit
/// and the READY / idle badges, the glass stat panels (three tubes against the roster's real maxima, three ability lenses with
/// their names), the hero sprites from TG-ART's baked idle frames, the player-slot palette, the 1.4 text fit, the stage tiles
/// and the Holodeck console's slab.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CharacterSelectCardTests {

    private const string ScenePath = "res://scenes/menus/CharacterSelect.tscn";
    private const string SelectRoot = "SelectPhase/Root/";
    private const string StageRoot = "StagePhase/Root/";

    private static SessionData? _saved;

    [TestCase]
    public async Task TheGridIsThreeColumnsOfGlassTilesWithTheRandomTileAloneOnAFullWidthRow() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            await Frames(screen, 3);
            var grid = screen.GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
            int roster = CharacterRoster.Count;
            CardGridRules.GridLayout layout = CardGridRules.LayoutFor(roster);
            AssertThat(screen.GridLayout).IsEqual(layout);
            AssertThat(grid.Columns).IsEqual(CardGridRules.Columns);
            AssertThat(grid.GetChildCount()).IsEqual(roster + 1);
            AssertThat(grid.GetChild(grid.GetChildCount() - 1).Name.ToString()).IsEqual("RandomTile");
            var problems = new List<string>();
            for (int index = 0; index < roster; index++) {
                var tile = grid.GetNode<Button>($"CharacterButton{index}");
                var decor = tile.GetNodeOrNull<GlassTileDecor>(GlassTileDecor.NodeName);
                CharacterData data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{CharacterRoster.At(index)}_data.tres");
                if (!tile.Size.IsEqualApprox(new Vector2(layout.Tile, layout.Tile))) problems.Add($"tile {index} is {tile.Size}");
                if (decor == null) problems.Add($"tile {index} has no glass tile decor");
                else if (decor.Portrait == null || decor.Portrait != data?.CharacterPortrait) problems.Add($"tile {index} does not show its portrait");
                if (tile.FocusMode != Control.FocusModeEnum.None) problems.Add($"tile {index} is focusable");
            }
            // The Random tile spans the grid's width on a row of its own, under the roster's last row.
            var random = grid.GetNode<Button>("RandomTile");
            if (!random.Position.IsEqualApprox(new Vector2(0f, layout.RandomTop))) problems.Add($"Random tile at {random.Position}, not (0, {layout.RandomTop})");
            if (!random.Size.IsEqualApprox(new Vector2(layout.Width, CardGridRules.RandomHeight))) problems.Add($"Random tile is {random.Size}");
            if (random.GetNodeOrNull<GlassTileDecor>(GlassTileDecor.NodeName)?.Portrait != null) problems.Add("the Random tile shows a portrait");
            // The grid hangs where 4.2 draws it: top-centre, from the layout's top line.
            if (Mathf.Abs(grid.GlobalPosition.Y - layout.Top) > 1f) problems.Add($"the grid starts at y {grid.GlobalPosition.Y}, not {layout.Top}");
            if (Mathf.Abs(grid.GlobalPosition.X + layout.Width / 2f - 960f) > 1f) problems.Add($"the grid is not centred (x {grid.GlobalPosition.X})");
            if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public async Task EveryTileShowsItsCardNameAndTheRandomTileAQuestionMarkAndTheRandomWord() {
        TranslationServer.SetLocale("en");
        CharacterSelectScreen screen = Open(out Node host);
        try {
            await Frames(screen, 3);
            var grid = screen.GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
            var problems = new List<string>();
            for (int index = 0; index < CharacterRoster.Count; index++) {
                string id = CharacterRoster.At(index);
                Button tile = grid.GetNode<Button>($"CharacterButton{index}");
                var name = tile.GetNodeOrNull<Label>("CharacterName");
                string expected = TranslationServer.Translate($"character_{id}_card_name");
                if (name == null) { problems.Add($"{id}: no name"); continue; }
                if (name.Text != expected || expected == $"character_{id}_card_name") problems.Add($"{id}: name '{name.Text}', not the card name '{expected}'");
                if (name.ThemeTypeVariation.ToString() != UIPalette.LabelRoles.CardName || !name.Uppercase) problems.Add($"{id}: name role {name.ThemeTypeVariation}");
                // 3.7: centred in 156 px, its box 11 px above the tile's bottom.
                float centre = name.Position.Y + name.Size.Y / 2f;
                float expectedCentre = tile.Size.Y - CharacterSelectScreen.TileNameBottomInset - 9f;
                if (Mathf.Abs(centre - expectedCentre) > 1.5f) problems.Add($"{id}: name centred at y {centre}, not {expectedCentre}");
                if (tile.FindChild("CharacterDates", true, false) != null) problems.Add($"{id}: still shows life dates");
            }
            Button random = grid.GetNode<Button>("RandomTile");
            var mark = random.GetNodeOrNull<Label>("RandomMark");
            var word = random.GetNodeOrNull<Label>("CharacterName");
            if (mark == null || mark.Text != "?" || mark.ThemeTypeVariation.ToString() != UIPalette.LabelRoles.CardQuestion) problems.Add("Random: no '?' in the CardQuestion role");
            if (word == null || word.Text != TranslationServer.Translate("fighter_random_character").ToString()
                || word.ThemeTypeVariation.ToString() != UIPalette.LabelRoles.RandomWord) problems.Add("Random: no random word in the RandomWord role");
            else if (mark != null && !(mark.Position.X + mark.Size.X <= word.Position.X && word.Position.X + word.Size.X <= random.Size.X)) problems.Add("Random: the '?' and the word are not one row inside the tile");
            if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public async Task TheStatTubesFillAgainstTheRostersRealMaximaInsideTheGlassStatPanel() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            // The maxima, computed here independently of CardStatMaxima.
            float hp = 0f, speed = 0f, weight = 0f;
            for (int index = 0; index < CharacterRoster.Count; index++) {
                var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{CharacterRoster.At(index)}_data.tres");
                hp = Mathf.Max(hp, data.MaxHP); speed = Mathf.Max(speed, data.MaxMoveSpeed); weight = Mathf.Max(weight, data.Weight);
            }
            AssertThat(screen.CardMaxima).IsEqual(new CardStatMaxima(hp, speed, weight));
            var joan = AuthoredResources.Load<CharacterData>("res://resources/Characters/joan_data.tres");
            screen.TryMoveCursor(0, 1, 0);
            AssertThat(CharacterRoster.At(screen.SelectedIndex)).IsEqual("joan");
            await Frames(screen, 3);
            double[] caps = { hp, speed, weight };
            double[] values = { joan.MaxHP, joan.MaxMoveSpeed, joan.Weight };
            for (int row = 0; row < 3; row++) {
                ProgressBar bar = screen.StatBar(0, row);
                // The bar's step is 0.001: compare at that precision (a float weight of 1.1 reads 1.100000023841858 as a double).
                AssertThat(System.Math.Abs(bar.MaxValue - caps[row])).IsLess(0.001);
                AssertThat(System.Math.Abs(bar.Value - values[row])).IsLess(0.001);
                AssertThat(bar.Size).IsEqual(CharacterSelectScreen.StatTubeSize);
                PulpBarDecor tube = PulpBarDecor.Of(bar);
                AssertObject(tube).IsNotNull();
                AssertThat(tube.Style).IsEqual(PulpBarStyle.Stat);
            }
            PanelContainer panel = screen.StatPanel(0);
            AssertThat(panel.ThemeTypeVariation.ToString()).IsEqual(UIPalette.Variations.GlassPanelStats);
            AssertThat(panel.Size.X).IsEqual(CharacterSelectScreen.SideWidth);
            AssertThat(screen.StatPanelTag(0)).IsEqual("movelist_style_melee");
            // The side panels end on 4.2's line (y 1011.8), 40 px in from the screen edges.
            AssertThat(Mathf.Abs(panel.GlobalPosition.Y + panel.Size.Y - CharacterSelectScreen.PanelBottom)).IsLess(1f);
            AssertThat(Mathf.Abs(panel.GlobalPosition.X - CharacterSelectScreen.SideMargin)).IsLess(1f);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public async Task ATileBothPlayersHoldShowsBothBadgesTheP1ToP2EdgeAndBothReadyBadges() {
        CharacterSelectScreen screen = Open(out Node host, joinBoth: true);
        try {
            var grid = screen.GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
            GlassTileDecor shared = grid.GetNode<Button>("CharacterButton0").GetNode<GlassTileDecor>(GlassTileDecor.NodeName);
            GlassTileDecor other = grid.GetNode<Button>("CharacterButton3").GetNode<GlassTileDecor>(GlassTileDecor.NodeName);
            AssertThat(shared.Owners).IsEqual(GlassTileDecor.OwnerP1);
            AssertThat(other.Owners).IsEqual(0);
            Color p1 = UIPalette.OwnerOnDark(0), p2 = UIPalette.OwnerOnDark(1);
            AssertThat(HasColourNear(shared.EdgeColors, p2)).IsFalse();

            AssertThat(screen.TryMoveCursor(1, -1, 0)).IsTrue();
            AssertThat(screen.TryConfirm(0)).IsTrue();
            AssertThat(screen.TryConfirm(1)).IsTrue();
            await Frames(screen, 2);
            // Both owners: both badges, the edge running from white through P1 to P2 (AC25), and P1's lock mark.
            AssertThat(shared.Owners).IsEqual(GlassTileDecor.OwnerP1 | GlassTileDecor.OwnerP2);
            AssertThat(shared.LockedBy).IsEqual(0);
            AssertThat(HasColourNear(shared.EdgeColors, p2)).IsTrue();
            AssertThat(HasColourNear(shared.EdgeColors, Colors.White)).IsTrue();
            AssertThat(other.Owners).IsEqual(0);
            AssertThat(other.LockedBy).IsEqual(-1);
            // A picked tile's name is white.
            AssertThat(grid.GetNode<Label>("CharacterButton0/CharacterName").GetThemeColor("font_color")).IsEqual(UIPalette.TextFocus);
            // Both heads show READY on the glass badge plate (accent edge, no fill-flooding shadow, its glow apart).
            foreach (string path in new[] { "PlayersRow/P1Panel/P1Ready", "PlayersRow/P2Panel/P2Ready" }) {
                var ready = screen.GetNode<Label>(SelectRoot + path);
                AssertThat(ready.Visible).IsTrue();
                AssertThat(ready.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.CardStamp);
                var plate = ready.GetThemeStylebox("normal") as StyleBoxFlat;
                AssertObject(plate).IsNotNull();
                AssertThat(plate.BorderColor.IsEqualApprox(UIPalette.Accent)).IsTrue();
                AssertThat(plate.ShadowSize).IsEqual(0);
                AssertThat(ready.GetNodeOrNull("Glow") != null).IsTrue();
            }
            AssertThat(screen.IdleBadge(0).Visible).IsFalse();
            AssertThat(screen.IdleBadge(1).Visible).IsFalse();
        } finally {
            Teardown(host);
        }
    }

    /// <summary>
    /// Every ownership colour on the select reads the active player-slot palette (Settings &gt; Gameplay), in all three
    /// palettes: the heads' slot shapes, the owned tile's edge (a mix of white and the slot colour), and UIPalette.P1 / P2
    /// themselves; and nothing painted on the dark chrome is too dark to read there.
    /// </summary>
    [TestCase]
    public void TheTilesAndHeadsWearTheActiveSlotPaletteInEveryPalette() {
        GlobalSaveData data = SaveManager.Instance?.GlobalData;
        // A test that can pass without asserting anything is not a test: fail loudly when the settings are missing.
        if (data == null) AssertThat("GlobalData is missing").IsEqual("");
        int saved = data.PlayerSlotPalette;
        var problems = new List<string>();
        try {
            for (int palette = 0; palette < PlayerSlotPalettes.Count; palette++) {
                data.PlayerSlotPalette = palette;
                var kind = (PlayerSlotPalette)palette;
                if (!UIPalette.P1.IsEqualApprox(PlayerSlotPalettes.SlotColor(kind, 0))) problems.Add($"{kind}: UIPalette.P1");
                if (!UIPalette.P2.IsEqualApprox(PlayerSlotPalettes.SlotColor(kind, 1))) problems.Add($"{kind}: UIPalette.P2");
                CharacterSelectScreen screen = Open(out Node host);
                try {
                    for (int player = 0; player < 2; player++) {
                        var shape = screen.GetNode<PulpIcon>(SelectRoot + $"PlayersRow/P{player + 1}Panel/Head/Shape/SlotShape");
                        if (!shape.Tint.IsEqualApprox(UIPalette.OwnerOnDark(player))) problems.Add($"{kind}: P{player + 1} head shape");
                        if (shape.IconName != (player == 0 ? "slot_p1" : "slot_p2")) problems.Add($"{kind}: P{player + 1} head shape is {shape.IconName}");
                        Color c = UIPalette.OwnerOnDark(player);
                        float luminance = 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
                        if (luminance < UIPalette.OwnerDarkLuminance) problems.Add($"{kind}: P{player + 1} too dark on chrome");
                    }
                    // P1's cursor tile: every edge vertex is white, the slot colour or a mix of the two.
                    var grid = screen.GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
                    GlassTileDecor decor = grid.GetNode<Button>("CharacterButton0").GetNode<GlassTileDecor>(GlassTileDecor.NodeName);
                    Color owner = UIPalette.OwnerOnDark(0);
                    foreach (Color vertex in decor.EdgeColors) {
                        if (!IsMixOfWhiteAnd(vertex, owner)) problems.Add($"{kind}: P1 tile edge {vertex} is not white mixed with {owner}");
                    }
                } finally {
                    Teardown(host);
                }
            }
        } finally {
            data.PlayerSlotPalette = saved;
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    [TestCase]
    public async Task TileTextShrinksToItsBoxAtLargeUiScaleButNeverBelowItsSizeAtScaleOne() {
        float saved = UIPalette.AppliedUiScale;
        CharacterSelectScreen screen = null;
        Node host = null;
        try {
            UIPalette.ApplyUiScale(1.4f);
            screen = Open(out host);
            await Frames(screen, 3);
            var problems = new List<string>();
            var grid = screen.GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
            float floor = 1f / 1.4f - 0.001f;
            for (int index = 0; index < CharacterRoster.Count; index++) {
                var label = grid.GetNode<Label>($"CharacterButton{index}/CharacterName");
                float scale = label.Scale.X;
                float drawn = CardTextFit.TextWidth(label) * scale;
                if (scale > 1.001f || scale < floor) problems.Add($"tile {index}: scale {scale}");
                else if (drawn > CharacterSelectScreen.TileNameBox + 1f && scale > floor + 0.002f) problems.Add($"tile {index}: {drawn} px is neither fitted to 156 nor at the floor");
            }
            // The grid itself does not grow with the UI scale (4.2).
            CardGridRules.GridLayout layout = CardGridRules.LayoutFor(CharacterRoster.Count);
            if (!grid.GetNode<Button>("CharacterButton0").Size.IsEqualApprox(new Vector2(layout.Tile, layout.Tile))) problems.Add("the tiles grew with the UI scale");
            // The footer's buttons stay between the side panels, below the grid and on the screen.
            foreach (string name in new[] { "MoveListButton", "SystemsCardButton", "BackButton" }) {
                // TG-CLOSE: the footer wears the compact footer variation (no local box), and its labels shrink to 4.2's
                // 18 px floor at any scale (a 20 px floor scaled to 28 at 1.4): never below it. Since the fix step the row
                // shares its width by need at 1.4, so every label, SYSTEMS CARD's too, reads whole there.
                var footerButton = screen.GetNode<Button>(SelectRoot + "ButtonRow/" + name);
                if (footerButton.ThemeTypeVariation.ToString() != UIPalette.Variations.PulpButtonCompactFooter) problems.Add($"{name} is {footerButton.ThemeTypeVariation}");
                if (footerButton.HasThemeStyleboxOverride("normal")) problems.Add($"{name} carries its own normal box");
                foreach (Node child in footerButton.GetChildren()) {
                    if (child is not PulpButtonDecor footerDecor) continue;
                    if (footerDecor.DrawnFontSize < UIPalette.MinLabelFontSize) problems.Add($"{name}'s label draws at {footerDecor.DrawnFontSize} px");
                    if (footerDecor.LabelIsTrimmed && footerDecor.DrawnFontSize > UIPalette.MinLabelFontSize) problems.Add($"{name}'s label is trimmed at {footerDecor.DrawnFontSize} px, above the 18 px floor");
                    if (footerDecor.LabelIsTrimmed) problems.Add($"{name}'s label is trimmed at 1.4");
                }
                Rect2 button = screen.GetNode<Control>(SelectRoot + "ButtonRow/" + name).GetGlobalRect();
                if (button.Position.X < CharacterSelectScreen.SideMargin + CharacterSelectScreen.SideWidth
                    || button.End.X > 1920f - CharacterSelectScreen.SideMargin - CharacterSelectScreen.SideWidth) problems.Add($"{name} {button} meets a side panel");
                if (button.Position.Y < layout.Bottom || button.End.Y > 1080f) problems.Add($"{name} {button} meets the grid or leaves the screen");
            }
            if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
        } finally {
            UIPalette.ApplyUiScale(saved);
            if (host != null) Teardown(host);
        }
    }

    /// <summary>
    /// TG-CLOSE fix step (the orchestrator's ruling on C1's question 2): every footer label reads whole at UI scale 1.0 and
    /// 1.4, never under the 18 px compact floor, its button inside the row between the two side panels, the buttons the
    /// focus-row separation apart. At 1.4 the three 280 px buttons cut "SYSTEMS CARD" (about 178 px at 18 px against the
    /// 165 a 280 px button keeps); the row now shares its width by the labels' needs there.
    /// </summary>
    [TestCase]
    public async Task EveryFooterLabelReadsWholeBetweenTheSidePanelsAtBothUiScales() {
        float saved = UIPalette.AppliedUiScale;
        var problems = new List<string>();
        try {
            foreach (float scale in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(scale);
                CharacterSelectScreen screen = Open(out Node host);
                try {
                    await Frames(screen, 4);
                    Rect2 left = screen.GetNode<Control>(SelectRoot + "PlayersRow/P1Panel").GetGlobalRect();
                    Rect2 right = screen.GetNode<Control>(SelectRoot + "PlayersRow/P2Panel").GetGlobalRect();
                    Rect2? previous = null;
                    foreach (string name in new[] { "MoveListButton", "SystemsCardButton", "BackButton" }) {
                        var button = screen.GetNode<Button>(SelectRoot + "ButtonRow/" + name);
                        PulpButtonDecor decor = null;
                        foreach (Node child in button.GetChildren()) if (child is PulpButtonDecor found) decor = found;
                        if (decor == null) { problems.Add($"{scale}: {name} has no decor"); continue; }
                        if (decor.LabelIsTrimmed) problems.Add($"{scale}: {name}'s label is trimmed at {decor.DrawnFontSize} px");
                        if (decor.DrawnFontSize < UIPalette.MinLabelFontSize) problems.Add($"{scale}: {name}'s label draws at {decor.DrawnFontSize} px");
                        Rect2 rect = button.GetGlobalRect();
                        if (rect.Position.X < left.End.X || rect.End.X > right.Position.X) {
                            problems.Add($"{scale}: {name} {rect} leaves the row between the side panels ({left.End.X} to {right.Position.X})");
                        }
                        if (previous is Rect2 before && rect.Position.X - before.End.X < PulpScreenKit.FocusRowGap - 0.5f) {
                            problems.Add($"{scale}: {name} is {rect.Position.X - before.End.X} px from its neighbour, under the focus row's {PulpScreenKit.FocusRowGap}");
                        }
                        previous = rect;
                    }
                } finally {
                    Teardown(host);
                }
            }
        } finally {
            UIPalette.ApplyUiScale(saved);
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    [TestCase]
    public async Task TheStagePhaseShowsAGlassTilePerStageAndFocusesFightAndTheConsoleIsAGlassSlabWithSettingsRows() {
        float savedScale = UIPalette.AppliedUiScale;
        CharacterSelectScreen screen = Open(out Node host);
        try {
            foreach (string name in new[] { "MeterPickups", "StageHazards" }) {
                var toggle = screen.GetNode<CheckButton>(StageRoot + "RulesRow/" + name);
                AssertThat(toggle.GetNodeOrNull<PulpToggleDecor>("PulpToggleDecor") != null)
                    .OverrideFailureMessage($"{name} is not skinned")
                    .IsTrue();
                bool before = toggle.ButtonPressed;
                toggle.ButtonPressed = !before;
                AssertThat(toggle.ButtonPressed).IsEqual(!before);
            }
            // One glass tile per stage in the list, the Random stage last; the field's stage is lit, a click selects.
            var stages = screen.GetNode<OptionButton>(StageRoot + "StageRow/StageSelect");
            IReadOnlyList<Button> tiles = screen.StageTiles;
            AssertThat(tiles.Count).IsEqual(stages.ItemCount);
            AssertThat(tiles[tiles.Count - 1].Name.ToString()).IsEqual("RandomStage");
            var problems = new List<string>();
            for (int i = 0; i < tiles.Count; i++) {
                Button tile = tiles[i];
                if (tile.FocusMode != Control.FocusModeEnum.None) problems.Add($"stage tile {i} is focusable");
                if (!tile.CustomMinimumSize.IsEqualApprox(CharacterSelectScreen.StageTileSize)) problems.Add($"stage tile {i} is {tile.CustomMinimumSize}");
                if (tile.GetNodeOrNull<GlassTileDecor>(GlassTileDecor.NodeName) == null) problems.Add($"stage tile {i} has no glass");
                if (i == tiles.Count - 1) continue;
                var tag = tile.GetNodeOrNull<PanelContainer>("LayoutTag");
                string key = tag?.GetChild<Label>(0)?.Text ?? "";
                if (key != "stage_layout_open" && key != "stage_layout_sealed") problems.Add($"stage tile {i} has no Open / Sealed tag ({key})");
                else if (string.IsNullOrEmpty(tag.TooltipText)) problems.Add($"stage tile {i}'s tag has no tooltip");
                if (tile.GetNodeOrNull<TextureRect>("Window/Preview")?.Texture == null) problems.Add($"stage tile {i} shows no preview plate");
            }
            // Every stage name stays on its tile: the longest ("ALEXANDRIA CHAMBERS") shrinks to fit even at UI scale 1.0.
            await Frames(screen, 2);
            foreach (Button tile in tiles) {
                var label = tile.GetNode<Label>("StageName");
                Transform2D xform = label.GetTransform();
                float left = (xform * Vector2.Zero).X, right = (xform * new Vector2(label.Size.X, 0f)).X;
                if (left < -1f || right > CharacterSelectScreen.StageTileSize.X + 1f) problems.Add($"{tile.Name}'s name runs over x {left:0}-{right:0} of its {CharacterSelectScreen.StageTileSize.X} px tile");
            }
            if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
            AssertThat(tiles[stages.Selected].GetNode<GlassTileDecor>(GlassTileDecor.NodeName).Edge).IsEqual(GlassTileDecor.TileEdge.Lit);
            tiles[1].EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(stages.Selected).IsEqual(1);
            AssertThat(tiles[1].GetNode<GlassTileDecor>(GlassTileDecor.NodeName).Edge).IsEqual(GlassTileDecor.TileEdge.Lit);
            AssertThat(tiles[0].GetNode<GlassTileDecor>(GlassTileDecor.NodeName).Edge).IsEqual(GlassTileDecor.TileEdge.Dim);

            // Entering the stage phase focuses Fight, its primary action (9.2).
            AssertThat(screen.TryConfirm(0)).IsTrue();
            AssertThat(screen.TryConfirm(1)).IsTrue();
            screen.AdvanceCountdown(3.5f);
            AssertThat(screen.GetNode<Button>(StageRoot + "StageButtonRow/FightButton").HasFocus()).IsTrue();

            // The Holodeck console: one glass slab, its title inside (TitleLabel, no tag), settings rows, glass toggles.
            var console = new HolodeckConsolePanel { Name = "TgSelectConsole" };
            host.AddChild(console);
            AssertThat(console.MeterPickupsToggle.GetNodeOrNull<PulpToggleDecor>("PulpToggleDecor") != null).IsTrue();
            AssertThat(console.StageHazardsToggle.GetNodeOrNull<PulpToggleDecor>("PulpToggleDecor") != null).IsTrue();
            AssertThat(console.GetNode<PulpTaggedPanel>("Center/Tagged").TagKey).IsEqual("");
            var panel = console.GetNode<PanelContainer>("Center/Tagged/Panel");
            AssertThat(panel.GetNodeOrNull<GlassSheen>(GlassSheen.NodeName) != null).IsTrue();
            var title = console.GetNode<Label>("Center/Tagged/Panel/Layout/Title");
            AssertThat(title.Text).IsEqual("holodeck_console_title");
            AssertThat(title.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.TitleLabel);
            var rowLabel = console.GetNode<Label>("Center/Tagged/Panel/Layout/CpuDifficultyRow/CpuDifficultyLabel");
            AssertThat(rowLabel.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.SettingsRowLabel);
            AssertThat(rowLabel.CustomMinimumSize.X).IsEqual(HolodeckConsolePanel.RowLabelWidth);
            var launch = console.GetNode<Button>("Center/Tagged/Panel/Layout/LaunchButton");
            AssertThat(launch.ThemeTypeVariation.ToString()).IsEqual(UIPalette.Variations.TemporalGlassButton);
            AssertThat(launch.GetNodeOrNull<PulpButtonDecor>("PulpButtonDecor") != null).IsTrue();

            // At UI scale 1.0 and 1.4 the fields share one width (the longest stage name widens them all together), and
            // the slab clears the overlay prompt bar at the foot of the screen.
            string[] fields = {
                "CpuDifficultyRow/CpuDifficulty", "CpuCharacterRow/CpuCharacter", "StageSelectRow/StageSelect", "MatchModeRow/MatchMode",
                "RulesRow/StockCount", "TimerRow/TimeLimit", "ItemFrequencyRow/ItemFrequency"
            };
            foreach (float uiScale in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(uiScale);
                await Frames(console, 3);
                float width = console.GetNode<Control>("Center/Tagged/Panel/Layout/" + fields[0]).Size.X;
                foreach (string field in fields) {
                    float w = console.GetNode<Control>("Center/Tagged/Panel/Layout/" + field).Size.X;
                    if (Mathf.Abs(w - width) > 0.5f) problems.Add($"@{uiScale}: {field} is {w:0} wide, {fields[0]} {width:0}");
                }
                if (width < HolodeckConsolePanel.RowControlWidth - 0.5f) problems.Add($"@{uiScale}: the fields are {width:0} wide");
                Rect2 slab = panel.GetGlobalRect();
                Rect2 bar = console.GetNode<Control>(PulpScreenKit.PromptBarName).GetGlobalRect();
                if (slab.End.Y > bar.Position.Y) problems.Add($"@{uiScale}: the slab ends at y {slab.End.Y:0}, under the prompt bar from {bar.Position.Y:0}");
            }
            UIPalette.ApplyUiScale(savedScale);
            if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
        } finally {
            UIPalette.ApplyUiScale(savedScale);
            Teardown(host);
        }
    }

    [TestCase]
    public async Task TheSideHeadsFitLongNamesAndShowTheReadyOrIdleBadge() {
        TranslationServer.SetLocale("en");
        float savedScale = UIPalette.AppliedUiScale;
        CharacterSelectScreen screen = Open(out Node host, joinBoth: true);
        try {
            await Frames(screen, 2);
            var problems = new List<string>();
            bool sawWrap = false;
            // Both heads (P2's is right-aligned), at UI scale 1.0 and at 1.4, where the names are 1.4 times wider.
            foreach (float uiScale in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(uiScale);
                await Frames(screen, 2);
                for (int index = 0; index < CharacterRoster.Count; index++) {
                    for (int t = 0; t < 2; t++) MoveCursorTo(screen, t, index);
                    await Frames(screen, 2);
                    for (int t = 0; t < 2; t++) {
                        Label name = screen.HeadName(t);
                        string id = $"{CharacterRoster.At(index)} P{t + 1} @{uiScale}";
                        // D10 / AC5: one line if it fits 440, else shrunk to no less than 36 / 48, else two lines at the full size.
                        (float scale, bool wrap) = CardTextFit.HeroNameFit(CardTextFit.TextWidth(name), CharacterSelectScreen.HeadWidth);
                        if (wrap != (name.AutowrapMode != TextServer.AutowrapMode.Off)) problems.Add($"{id}: wrap {name.AutowrapMode}, the rule says {wrap}");
                        if (!wrap && !Mathf.IsEqualApprox(name.Scale.X, scale)) problems.Add($"{id}: scale {name.Scale.X}, not {scale}");
                        if (name.Scale.X < CardTextFit.HeroNameShrinkFloorAt(uiScale) - 0.001f || name.Scale.X > 1.0001f) problems.Add($"{id}: scaled to {name.Scale.X}");
                        // Every word shows: two lines at most, every line drawn (never trimmed), inside the 440 px head box
                        // and on the screen.
                        if (name.GetLineCount() > 2) problems.Add($"{id}: {name.GetLineCount()} lines");
                        if (name.GetVisibleLineCount() < name.GetLineCount()) problems.Add($"{id}: {name.GetVisibleLineCount()} of {name.GetLineCount()} lines show");
                        Rect2 drawn = name.GetGlobalRect(), box = name.GetParent<Control>().GetGlobalRect();
                        if (drawn.Position.X < box.Position.X - 1f || drawn.End.X > box.End.X + 1f || drawn.Position.X < 0f || drawn.End.X > 1920f) {
                            problems.Add($"{id}: drawn over x {drawn.Position.X:0}-{drawn.End.X:0}, past its head {box.Position.X:0}-{box.End.X:0}");
                        }
                        sawWrap |= wrap;
                    }
                }
            }
            // 4.2 6a at UI scale 1.0: a one-line name's box is T40's line box (162 to 213.8), and the badge hangs 12 px under it.
            UIPalette.ApplyUiScale(1f);
            MoveCursorTo(screen, 0, CharacterRoster.IndexOf("joan"));
            await Frames(screen, 2);
            Control headBox = screen.HeadName(0).GetParent<Control>();
            if (Mathf.Abs(headBox.GlobalPosition.Y - 162f) > 1f || Mathf.Abs(headBox.Size.Y - 51.8f) > 1f) problems.Add($"Joan's name box is y {headBox.GlobalPosition.Y:0.0} + {headBox.Size.Y:0.0}, not 162 + 51.8");
            if (Mathf.Abs(screen.IdleBadge(0).GlobalPosition.Y - 225.8f) > 1f) problems.Add($"the idle badge's top is y {screen.IdleBadge(0).GlobalPosition.Y:0.0}, not 225.8");
            UIPalette.ApplyUiScale(savedScale);
            await Frames(screen, 2);
            // "WILLIAM SHAKESPEARE" is 672 px at 48 px (UI_SPEC_GLASS D10): it wraps.
            if (!sawWrap) problems.Add("no roster name needed the two-line rule");

            // Choosing: the idle badge (R4: "HMM...", ink2, the idle plate); locked: READY in accent.
            Label idle = screen.IdleBadge(0);
            var ready = screen.GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1Ready");
            if (!idle.Visible || ready.Visible) problems.Add("choosing does not show the idle badge alone");
            if (idle.Text != "fighter_select_choosing" || !idle.GetThemeColor("font_color").IsEqualApprox(UIPalette.Ink2)) problems.Add("the idle badge's text or colour");
            if (idle.GetThemeStylebox("normal") is not StyleBoxFlat idlePlate || !idlePlate.BorderColor.IsEqualApprox(UIPalette.EdgeDim)) problems.Add("the idle badge's plate");
            AssertThat(screen.TryConfirm(0)).IsTrue();
            if (idle.Visible || !ready.Visible) problems.Add("locked does not show READY alone");
            if (!ready.GetThemeColor("font_color").IsEqualApprox(UIPalette.Accent)) problems.Add("READY is not accent");
            if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
        } finally {
            UIPalette.ApplyUiScale(savedScale);
            Teardown(host);
        }
    }

    [TestCase]
    public async Task TheSidePanelsShowThreeStatTubesAndThreeAbilityLensesWithNames() {
        TranslationServer.SetLocale("en");
        CharacterSelectScreen screen = Open(out Node host);
        try {
            screen.TryMoveCursor(0, 1, 0);
            await Frames(screen, 3);
            string id = CharacterRoster.At(screen.SelectedIndex);
            CharacterData data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
            AbilityData[] abilities = { data.SpecialAttackOne, data.SpecialAttackTwo, data.MovementAbility };
            PanelContainer panel = screen.StatPanel(0);
            var problems = new List<string>();
            if (panel.GetNodeOrNull<GlassSheen>(GlassSheen.NodeName) == null) problems.Add("the stat panel has no sheen");
            for (int row = 0; row < 3; row++) {
                if (PulpBarDecor.Of(screen.StatBar(0, row))?.Style != PulpBarStyle.Stat) problems.Add($"row {row} is not a stat tube");
            }
            float[] centres = { 91f, 230f, 369f };   // 4.2 element 7.in, box-relative
            for (int i = 0; i < 3; i++) {
                PulpAbilitySlot lens = screen.AbilityLens(0, i);
                Label name = screen.AbilityNameLabel(0, i);
                if (lens.SlotSize != CharacterSelectScreen.AbilityLensSize || lens.State != PulpSlotState.Ready) problems.Add($"lens {i}: {lens.SlotSize} {lens.State}");
                if (lens.Art == null || lens.Art != AbilityIconLibrary.Load(id, CharacterSelectScreen.SidePanelAbilities[i])) problems.Add($"lens {i} shows the wrong art");
                if (!string.IsNullOrEmpty(lens.KeyText)) problems.Add($"lens {i} shows a key label");
                float centre = lens.GlobalPosition.X + lens.LensSize / 2f - panel.GlobalPosition.X;
                if (Mathf.Abs(centre - centres[i]) > 2f) problems.Add($"lens {i} centred at {centre}, not {centres[i]}");
                if (name.Text != abilities[i].DisplayNameKey) problems.Add($"name {i} '{name.Text}', not {abilities[i].DisplayNameKey}");
                if (name.ThemeTypeVariation.ToString() != UIPalette.LabelRoles.AbilityName || !name.Uppercase) problems.Add($"name {i}'s role");
                if (name.GetLineCount() > 2 || name.MaxLinesVisible != 2) problems.Add($"name {i} runs to {name.GetLineCount()} lines");
                if (name.GlobalPosition.Y < lens.GlobalPosition.Y + lens.LensSize + CharacterSelectScreen.AbilityNameGap - 1f) problems.Add($"name {i} sits on its lens");
            }
            // The hidden stat line stays for screen readers.
            if (string.IsNullOrEmpty(screen.GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1Card/Col/P1Stats").Text)) problems.Add("no hidden stat line");
            // Every hero's names, at UI scale 1.0 and 1.4: two lines at most, every line drawn, no word broken (the widest
            // word fits the 130 px cell on screen, the name shrinking with the scale, never below its 1.0 size), inside the
            // cell, the three at one size.
            float savedScale = UIPalette.AppliedUiScale;
            try {
                foreach (float uiScale in new[] { 1f, 1.4f }) {
                    UIPalette.ApplyUiScale(uiScale);
                    float floor = 1f / uiScale;
                    for (int index = 0; index < CharacterRoster.Count; index++) {
                        MoveCursorTo(screen, 0, index);
                        await Frames(screen, 2);
                        for (int i = 0; i < 3; i++) {
                            Label name = screen.AbilityNameLabel(0, i);
                            string at = $"{CharacterRoster.At(index)} name {i} @{uiScale}";
                            if (name.GetLineCount() > CharacterSelectScreen.AbilityNameMaxLines) problems.Add($"{at}: {name.GetLineCount()} lines");
                            if (name.GetVisibleLineCount() < name.GetLineCount()) problems.Add($"{at}: {name.GetVisibleLineCount()} of {name.GetLineCount()} lines show");
                            float word = CardTextFit.WidestWordWidth(name) * name.Scale.X;
                            if (word > CharacterSelectScreen.AbilityCellWidth + 1f && name.Scale.X > floor + 0.001f) problems.Add($"{at}: a {word:0} px word in a {CharacterSelectScreen.AbilityCellWidth} px cell breaks");
                            Rect2 drawn = name.GetGlobalRect(), cell = name.GetParent<Control>().GetGlobalRect();
                            if (drawn.Position.X < cell.Position.X - 1f || drawn.End.X > cell.End.X + 1f) problems.Add($"{at}: drawn over x {drawn.Position.X:0}-{drawn.End.X:0}, past its cell {cell.Position.X:0}-{cell.End.X:0}");
                            // The row reads at one size.
                            if (!Mathf.IsEqualApprox(name.Scale.X, screen.AbilityNameLabel(0, 0).Scale.X)) problems.Add($"{at}: scale {name.Scale.X}, the first name's {screen.AbilityNameLabel(0, 0).Scale.X}");
                        }
                    }
                }
            } finally {
                UIPalette.ApplyUiScale(savedScale);
            }
            await Frames(screen, 2);
            // Random: no stats, no lenses, no style tag.
            while (screen.SelectedIndex != screen.RandomTileIndex) screen.TryMoveCursor(0, 0, 1);
            await Frames(screen, 2);
            if (screen.StatBar(0, 0).IsVisibleInTree() || screen.AbilityLens(0, 0).IsVisibleInTree()) problems.Add("Random shows stats or lenses");
            if (screen.StatPanelTag(0) != "") problems.Add("Random shows a style tag");
            // An unjoined Player 2 shows no panel.
            if (!screen.IsPlayerTwoJoined && screen.StatPanel(1).Visible) problems.Add("an unjoined Player 2 shows a panel");
            if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public async Task TheHeroSpritesStandOnTheFeetLineFromTheBakedIdleFrames() {
        CharacterSelectScreen screen = Open(out Node host, joinBoth: true);
        try {
            screen.TryMoveCursor(0, 1, 0);
            screen.TryMoveCursor(1, 1, 0);
            await Frames(screen, 3);
            var problems = new List<string>();
            for (int player = 0; player < 2; player++) {
                SelectHeroSprite hero = screen.HeroSprite(player);
                string id = CharacterRoster.At(player == 0 ? screen.SelectedIndex : screen.OpponentIndex);
                if (hero.HeroID != id) problems.Add($"P{player + 1} shows {hero.HeroID}, not {id}");
                string path = hero.FrameTexture?.ResourcePath ?? "";
                if (!path.Contains($"select_heroes/{id}_idle_")) problems.Add($"P{player + 1} draws '{path}', not a baked select frame");
                var feet = new Vector2(player == 0 ? CharacterSelectScreen.HeroPivotP1 : CharacterSelectScreen.HeroPivotP2, SelectHeroSprite.SelectFeetY);
                if (!hero.FeetPoint.IsEqualApprox(feet)) problems.Add($"P{player + 1} feet at {hero.FeetPoint}, not {feet}");
                if (hero.Mirrored != (player == 1)) problems.Add($"P{player + 1} mirrored {hero.Mirrored}");
                Rect2 drawn = hero.DrawnRect;
                if (!(drawn.Position.Y < feet.Y && drawn.End.Y >= feet.Y && drawn.Position.X < feet.X && drawn.End.X > feet.X)) problems.Add($"P{player + 1} drawn at {drawn}, not over its feet {feet}");
                if (Mathf.Abs(drawn.Size.Y - hero.FrameTexture.GetHeight() * SelectHeroSprite.SelectScale) > 0.5f) problems.Add($"P{player + 1} not drawn at 2.05");
            }
            // The idle plays at the hero's own rate.
            SelectHeroSprite p1 = screen.HeroSprite(0);
            int frame = p1.FrameIndex;
            p1._Process(1.0 / p1.FramesPerSecond + 0.01);
            int poses = FTT.Tests.ContentValidation.CharacterPresentationTests.IdlePoseCount(p1.HeroID);
            if (p1.FrameIndex == frame || p1.FrameCount != poses || p1.LoopLength != 2 * (poses - 1)) problems.Add($"the idle did not step (pose {p1.FrameIndex} of {p1.FrameCount}, loop of {p1.LoopLength}; expected {poses} poses)");
            // The Random tile shows no hero.
            while (screen.SelectedIndex != screen.RandomTileIndex) screen.TryMoveCursor(0, 0, 1);
            if (p1.HeroID != null || p1.FrameTexture != null) problems.Add("Random shows a hero");
            if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
        } finally {
            Teardown(host);
        }
    }

    private static bool HasColourNear(Color[] colours, Color target) {
        foreach (Color c in colours) {
            if (Mathf.Abs(c.R - target.R) < 0.15f && Mathf.Abs(c.G - target.G) < 0.15f && Mathf.Abs(c.B - target.B) < 0.15f) return true;
        }
        return false;
    }

    /// <summary>Whether <paramref name="c"/>'s colour lies on the line from white to <paramref name="owner"/> (any alpha).</summary>
    private static bool IsMixOfWhiteAnd(Color c, Color owner) {
        float[] cc = { c.R, c.G, c.B }, oo = { owner.R, owner.G, owner.B };
        int pick = 0;
        for (int i = 1; i < 3; i++) if (Mathf.Abs(1f - oo[i]) > Mathf.Abs(1f - oo[pick])) pick = i;
        float span = 1f - oo[pick];
        float s = span < 0.0001f ? 0f : Mathf.Clamp((1f - cc[pick]) / span, 0f, 1f);
        for (int i = 0; i < 3; i++) if (Mathf.Abs(cc[i] - (1f - s + oo[i] * s)) > 0.02f) return false;
        return true;
    }

    /// <summary>Walks player <paramref name="token"/>'s cursor along the rows (and off the Random tile) to <paramref name="index"/>.</summary>
    private static void MoveCursorTo(CharacterSelectScreen screen, int token, int index) {
        for (int step = 0; step < 4 * (screen.RandomTileIndex + 1) && screen.GetCursor(token) != index; step++) {
            int cursor = screen.GetCursor(token);
            if (cursor == screen.RandomTileIndex) screen.TryMoveCursor(token, 0, 1);
            else if (cursor % CardGridRules.Columns == CardGridRules.Columns - 1 || cursor == screen.RandomTileIndex - 1) screen.TryMoveCursor(token, 1, 1);
            else screen.TryMoveCursor(token, 1, 0);
        }
        AssertThat(screen.GetCursor(token)).OverrideFailureMessage($"P{token + 1}'s cursor never reached tile {index}").IsEqual(index);
    }

    private static async Task Frames(Node node, int count) {
        SceneTree tree = node.GetTree();
        for (int i = 0; i < count; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static CharacterSelectScreen Open(out Node host, bool joinBoth = false) {
        if (GameManager.Instance != null) {
            _saved ??= GameManager.Instance.CurrentSession;
            SessionData session = GameManager.Instance.CurrentSession;
            session.FighterMatchOrigin = FighterMatchOrigin.LocalVersus;
            session.ResumeAtStageSelect = false;
            session.HasPendingMatchSeed = false;
            GameManager.Instance.CurrentSession = session;
        }
        host = new Node { Name = "CharacterSelectCardHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var screen = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<CharacterSelectScreen>();
        host.AddChild(screen);
        if (joinBoth && InputManager.Instance != null) {
            // Both slots driven by input sources count as joined (the capture tool's way).
            InputManager.Instance.SetInputSource(0, new BufferedInputSource());
            InputManager.Instance.SetInputSource(1, new BufferedInputSource());
            screen.TryMoveCursor(0, 0, 0);
        }
        return screen;
    }

    private static void Teardown(Node host) {
        if (GameManager.Instance != null && _saved.HasValue) {
            GameManager.Instance.CurrentSession = _saved.Value;
            _saved = null;
        }
        if (InputManager.Instance != null) {
            InputManager.Instance.ClearInputSource(0);
            InputManager.Instance.ClearInputSource(1);
            InputManager.Instance.EndLocalJoin();
        }
        if (host == null || !GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }

    /// <summary>TG-CLOSE Codex review 3: labels too long even at the floor never push the footer past its budget.</summary>
    [TestCase]
    public void TheFooterWidthsStayInsideTheBudgetWhenTheLabelsCannotFit() {
        float[] paddings = { 40f, 40f, 40f };
        float[] huge = CharacterSelectScreen.FooterWidths(280f, paddings, (i, s) => 400f, 24, 18);
        float sum = 0f;
        foreach (float w in huge) sum += w;
        AssertThat(sum <= 280f * 3f).IsTrue();
        foreach (float w in huge) AssertThat(w > 0f).IsTrue();
        // A row that fits whole in the shared width is unchanged.
        float[] fits = CharacterSelectScreen.FooterWidths(280f, paddings, (i, s) => 100f, 24, 18);
        AssertThat(fits[0]).IsEqual(280f);
        AssertThat(fits[2]).IsEqual(280f);
    }

    /// <summary>TG-CLOSE Codex review 3 (follow-up): asymmetric oversized labels never squeeze a button under its minimum.</summary>
    [TestCase]
    public void TheFooterWidthsKeepEveryButtonItsMinimumWhenOneLabelIsOversized() {
        float[] paddings = { 82f, 82f, 82f };
        float[] labels = { 600f, 600f, 40f };
        float[] widths = CharacterSelectScreen.FooterWidths(280f, paddings, (i, s) => labels[i], 24, 18);
        float sum = 0f;
        for (int i = 0; i < widths.Length; i++) {
            sum += widths[i];
            if (widths[i] < paddings[i] + CharacterSelectScreen.FooterSlack) AssertThat(widths[i]).OverrideFailureMessage($"button {i} is {widths[i]}, under its minimum").IsGreaterEqual(paddings[i] + CharacterSelectScreen.FooterSlack);
        }
        AssertThat(sum <= 840f).OverrideFailureMessage($"sum {sum} over the 840 budget").IsTrue();
    }

    /// <summary>The real select with long, asymmetric footer labels: the row stays between the side panels, apart.</summary>
    [TestCase]
    public async Task OversizedAsymmetricFooterLabelsStayBetweenTheSidePanelsAtBothUiScales() {
        float saved = UIPalette.AppliedUiScale;
        var problems = new List<string>();
        try {
            foreach (float scale in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(scale);
                CharacterSelectScreen screen = Open(out Node host);
                try {
                    await Frames(screen, 4);
                    string[] names = { "MoveListButton", "SystemsCardButton", "BackButton" };
                    string[] longText = { "A VERY LONG MOVE LIST LABEL THAT CANNOT FIT AT ALL", "ANOTHER EXTREMELY LONG SYSTEMS CARD LABEL HERE", "OK" };
                    for (int i = 0; i < 3; i++) screen.GetNode<Button>(SelectRoot + "ButtonRow/" + names[i]).Text = longText[i];
                    // A label change does not queue a layout by itself (a locale change does), so ask for the one a resize runs.
                    screen.GetNode<Control>(SelectRoot + "PlayersRow").EmitSignal(Control.SignalName.Resized);
                    await Frames(screen, 6);
                    Rect2 left = screen.GetNode<Control>(SelectRoot + "PlayersRow/P1Panel").GetGlobalRect();
                    Rect2 right = screen.GetNode<Control>(SelectRoot + "PlayersRow/P2Panel").GetGlobalRect();
                    Rect2? previous = null;
                    foreach (string name in names) {
                        Rect2 rect = screen.GetNode<Button>(SelectRoot + "ButtonRow/" + name).GetGlobalRect();
                        if (rect.Position.X < left.End.X || rect.End.X > right.Position.X) problems.Add($"{scale}: {name} {rect} leaves the row ({left.End.X} to {right.Position.X})");
                        if (previous is Rect2 before) {
                            if (rect.Position.X < before.End.X) problems.Add($"{scale}: {name} overlaps its neighbour");
                            else if (rect.Position.X - before.End.X < PulpScreenKit.FocusRowGap - 0.5f) problems.Add($"{scale}: {name} is {rect.Position.X - before.End.X} px from its neighbour, under {PulpScreenKit.FocusRowGap}");
                        }
                        previous = rect;
                    }
                } finally {
                    Teardown(host);
                }
            }
        } finally {
            UIPalette.ApplyUiScale(saved);
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }
}
