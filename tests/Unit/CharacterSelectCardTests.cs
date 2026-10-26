using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>UR-SELECT. The CARD-B trading-card select: the grid's growth rule, the life dates, the stat maxima from the roster, the pick states and the toggles.</summary>
[TestSuite]
[RequireGodotRuntime]
public class CharacterSelectCardTests {

    private const string ScenePath = "res://scenes/menus/CharacterSelect.tscn";
    private const string SelectRoot = "SelectPhase/Root/";

    private static SessionData? _saved;

    [TestCase]
    public void TheGridIsFiveColumnsWithOneCardPerRosterMemberPlusRandomAndNoCompactScaleYet() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            var grid = screen.GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
            AssertThat(grid.Columns).IsEqual(CardGridRules.Columns);
            AssertThat(grid.GetChildCount()).IsEqual(CharacterRoster.Count + 1);
            AssertThat(grid.GetChild(grid.GetChildCount() - 1).Name.ToString()).IsEqual("RandomTile");
            AssertThat(screen.GridScale).IsEqual(CardGridRules.ScaleFor(CharacterRoster.Count + 1));
            var first = grid.GetNode<Button>("CharacterButton0");
            AssertThat(first.CustomMinimumSize)
                .IsEqual(new Vector2(CardGridRules.CardWidth, CardGridRules.CardHeight) * screen.GridScale);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void EveryRosterCardShowsItsLifeDatesAndTheRandomCardAMysteryLine() {
        TranslationServer.SetLocale("en");
        CharacterSelectScreen screen = Open(out Node host);
        try {
            var grid = screen.GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
            var problems = new List<string>();
            for (int index = 0; index < CharacterRoster.Count; index++) {
                string id = CharacterRoster.At(index);
                var dates = grid.GetNode<Button>($"CharacterButton{index}").FindChild("CharacterDates", true, false) as Label;
                string key = $"character_{id}_dates";
                if (dates == null || dates.Text.Length == 0 || dates.Text == key) problems.Add($"{id}: no life dates");
                else if (!dates.Text.Contains('–')) problems.Add($"{id}: dates {dates.Text} lack the en dash");
            }
            var random = grid.GetNode<Button>("RandomTile").FindChild("CharacterDates", true, false) as Label;
            if (random == null || random.Text == "fighter_random_mystery" || random.Text.Length == 0) problems.Add("Random: no mystery line");
            if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheStatBarsFillAgainstTheRostersRealMaximaAndTheCardNamesTheStyle() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            var values = new List<(float, float, float)>();
            for (int index = 0; index < CharacterRoster.Count; index++) {
                var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{CharacterRoster.At(index)}_data.tres");
                values.Add((data.MaxHP, data.MaxMoveSpeed, data.Weight));
            }
            CardStatMaxima expected = CardStatMaxima.FromValues(values);
            AssertThat(screen.CardMaxima).IsEqual(expected);
            var joan = AuthoredResources.Load<CharacterData>("res://resources/Characters/joan_data.tres");
            screen.TryMoveCursor(0, 1, 0);
            AssertThat(CharacterRoster.At(screen.SelectedIndex)).IsEqual("joan");
            AssertThat(screen.StatBar(0, 0).MaxValue).IsEqual((double)expected.Hp);
            AssertThat(screen.StatBar(0, 1).MaxValue).IsEqual((double)expected.Speed);
            AssertThat(screen.StatBar(0, 2).MaxValue).IsEqual((double)expected.Weight);
            AssertThat(screen.StatBar(0, 0).Value).IsEqual((double)joan.MaxHP);
            AssertThat(screen.StatBar(0, 1).Value).IsEqual((double)joan.MaxMoveSpeed);
            AssertThat(screen.StatCard(0).TagKey).IsEqual("movelist_style_melee");
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void ACardBothPlayersHoldShowsBothFramesBothBadgesAndTheReadyStamp() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            var grid = screen.GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
            var card = grid.GetNode<Button>("CharacterButton0");
            var other = grid.GetNode<Button>("CharacterButton3");
            AssertThat(((Control)card.FindChild("Frame", true, false)).ThemeTypeVariation.ToString())
                .IsEqual(UIPalette.Variations.CardPlatePick);
            AssertThat(((Control)other.FindChild("Frame", true, false)).ThemeTypeVariation.ToString())
                .IsEqual(UIPalette.Variations.CardPlate);
            AssertThat(((Control)card.FindChild("Stamp", true, false)).Visible).IsFalse();

            AssertThat(screen.TryMoveCursor(1, -1, 0)).IsTrue();
            AssertThat(screen.TryConfirm(0)).IsTrue();
            AssertThat(screen.TryConfirm(1)).IsTrue();
            AssertThat(((Control)card.FindChild("FrameBack", true, false)).Visible).IsTrue();
            AssertThat(((Control)card.FindChild("Badge1", true, false)).Visible).IsTrue();
            AssertThat(((Control)card.FindChild("Badge2", true, false)).Visible).IsTrue();
            AssertThat(((Control)card.FindChild("Stamp", true, false)).Visible).IsTrue();
            AssertThat(((Control)other.FindChild("Stamp", true, false)).Visible).IsFalse();
        } finally {
            Teardown(host);
        }
    }

    /// <summary>
    /// Review fix (closeout, 2026-10-26): every ownership colour on the cards reads the active player-slot palette
    /// (Settings &gt; Gameplay), in all three palettes: the picked frame's ring, the pick badge glyph, the star, and
    /// UIPalette.P1 / P2 themselves. The old baked cyan / red frames and the fixed GlowPalette read ignored it.
    /// </summary>
    [TestCase]
    public void TheCardsWearTheActiveSlotPaletteInEveryPalette() {
        GlobalSaveData data = SaveManager.Instance?.GlobalData;
        if (data == null) return;
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
                    var grid = screen.GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
                    var card = grid.GetNode<Button>("CharacterButton0");
                    var frame = (Control)card.FindChild("Frame", true, false);
                    if (!frame.SelfModulate.IsEqualApprox(UIPalette.OwnerOnDark(0))) problems.Add($"{kind}: P1 frame ring");
                    var shape = card.FindChild("Shape", true, false) as PulpIcon;
                    if (shape == null || !shape.Tint.IsEqualApprox(UIPalette.OwnerOnDark(0))) problems.Add($"{kind}: P1 badge glyph");
                    var star = screen.GetNode<Control>(SelectRoot + "PlayersRow/P1Panel/P1Star/Fill");
                    if (!star.Modulate.IsEqualApprox(UIPalette.OwnerOnDark(0))) problems.Add($"{kind}: P1 star");
                    var star2 = screen.GetNode<Control>(SelectRoot + "PlayersRow/P2Panel/P2Star/Fill");
                    if (!star2.Modulate.IsEqualApprox(UIPalette.OwnerOnDark(1))) problems.Add($"{kind}: P2 star");
                    // Whatever the palette, nothing painted on the dark chrome may be too dark to read there.
                    for (int player = 0; player < 2; player++) {
                        Color c = UIPalette.OwnerOnDark(player);
                        float luminance = 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
                        if (luminance < UIPalette.OwnerDarkLuminance) problems.Add($"{kind}: P{player + 1} too dark on chrome");
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
    public void CardTextShrinksToItsBoxAtLargeUiScaleButNeverBelowItsSizeAtScaleOne() {
        float saved = UIPalette.AppliedUiScale;
        CharacterSelectScreen screen = null;
        Node host = null;
        try {
            UIPalette.ApplyUiScale(1.4f);
            screen = Open(out host);
            var problems = new List<string>();
            var grid = screen.GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
            float floor = 1f / 1.4f - 0.001f;
            foreach (Node tile in grid.GetChildren()) {
                foreach (string part in new[] { "CharacterName", "CharacterDates" }) {
                    var label = tile.FindChild(part, true, false) as Label;
                    float scale = label.Scale.X;
                    float drawn = CardTextFit.TextWidth(label) * scale;
                    if (scale > 1.001f || scale < floor) problems.Add($"{tile.Name}/{part}: scale {scale}");
                    else if (drawn > CardGridRules.CardWidth + 1f && scale > floor + 0.002f) problems.Add($"{tile.Name}/{part}: {drawn} px is neither fitted nor at the floor");
                }
            }
            if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
        } finally {
            UIPalette.ApplyUiScale(saved);
            if (host != null) Teardown(host);
        }
    }

    [TestCase]
    public void TheStagePhaseAndTheHolodeckConsoleSkinTheirTogglesAndKeepTheirValues() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            foreach (string name in new[] { "MeterPickups", "StageHazards" }) {
                var toggle = screen.GetNode<CheckButton>("StagePhase/Root/RulesRow/" + name);
                AssertThat(toggle.GetNodeOrNull<PulpToggleDecor>("PulpToggleDecor") != null)
                    .OverrideFailureMessage($"{name} is not skinned")
                    .IsTrue();
                bool before = toggle.ButtonPressed;
                toggle.ButtonPressed = !before;
                AssertThat(toggle.ButtonPressed).IsEqual(!before);
            }
            var console = new HolodeckConsolePanel { Name = "UrSelectConsole" };
            host.AddChild(console);
            AssertThat(console.MeterPickupsToggle.GetNodeOrNull<PulpToggleDecor>("PulpToggleDecor") != null).IsTrue();
            AssertThat(console.StageHazardsToggle.GetNodeOrNull<PulpToggleDecor>("PulpToggleDecor") != null).IsTrue();
            var tagged = console.GetNode<PulpTaggedPanel>("Center/Tagged");
            AssertThat(tagged.TagKey).IsEqual("holodeck_console_title");
        } finally {
            Teardown(host);
        }
    }

    private static CharacterSelectScreen Open(out Node host) {
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
        return screen;
    }

    private static void Teardown(Node host) {
        if (GameManager.Instance != null && _saved.HasValue) {
            GameManager.Instance.CurrentSession = _saved.Value;
            _saved = null;
        }
        InputManager.Instance?.EndLocalJoin();
        if (host == null || !GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
