using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// TG-MENU (Temporal Glass, MENU-A; UI_SPEC_GLASS 3.8, 4.1, 9.2), rewritten from UR-MENU's pulp-cover suite. The root
/// screen: the temporal-glass art under the menu scrim with the screen streak, the three-line title from two keys with the
/// gradient on both title words (ruling R2), the chevron column standing on y 983 with the D6 width and the fit sequence,
/// the prompt bar, and the focusable Continue card (R5, AC18); and the glass sub-screens. The card reads its saves through
/// <c>MainMenu.SaveSlotsOverrideForTesting</c> and resumes through <c>MainMenu.ResumeOverrideForTesting</c>, so nothing here
/// depends on (or writes) the saves on this machine. The session, the UI scale and every seam are restored (CLAUDE.md
/// failure signature 8).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MainMenuCoverTests {

    private const string ScenePath = "res://scenes/menus/MainMenu.tscn";
    private const string RootLayout = "RootScreen/Center/Panel/Layout/";
    private const string Card = "RootScreen/ContinueCard/";
    private const string CardLines = Card + "Plate/Row/Lines/";

    private static readonly string[] RootButtons = {
        "QuickPlayButton", "StoryButton", "LevelSelectButton", "FighterButton",
        "CalibrationDrillsButton", "SettingsButton", "ExtrasButton", "QuitButton"
    };

    private static readonly string[] SubScreens = {
        "SlotScreen", "CharacterScreen", "DifficultyScreen", "LevelSelectScreen", "ExtrasScreen", "LicensesScreen"
    };

    private static StorySaveData Save(string character, string scene, string stamp, bool completed = false,
        Difficulty difficulty = Difficulty.Normal) => new() {
        SelectedCharacterID = character,
        CurrentLevelID = scene,
        LastSavedTimestamp = stamp,
        IsCompleted = completed,
        Difficulty = difficulty
    };

    private static StorySaveData[] TwoSaves() => new[] {
        Save("einstein", "res://scenes/campaign/Level_02_Orleans.tscn", "2026-10-10T10:00:00.0000000Z"),
        Save("joan", "res://scenes/campaign/Level_06_Pompeii.tscn", "2026-10-20T10:00:00.0000000Z"),
        null
    };

    // ---- the Continue card (R5, AC18) ---------------------------------------------------------------------------

    [TestCase]
    public void TheContinueCardIsHiddenWithNoSaveAndOutOfTheFocusChain() {
        using Fixture fixture = Fixture.Open(new StorySaveData[3]);
        MainMenu menu = fixture.Menu;
        AssertThat(menu.ContinueCard != null).OverrideFailureMessage("The root has no ContinueCard").IsTrue();
        AssertThat(menu.ContinueCard.Visible).IsFalse();
        AssertThat(menu.ContinueSlot).IsEqual(-1);
        AssertThat(FocusChainBuilder.Collect(menu.GetNode<Control>("RootScreen")).Contains(menu.ContinueCard)).IsFalse();
        Control right = menu.GetNode<Button>(RootLayout + "StoryButton").FindValidFocusNeighbor(Side.Right);
        AssertThat(right == menu.ContinueCard).OverrideFailureMessage("Right from Story Mode reaches a hidden card").IsFalse();
    }

    [TestCase]
    public void TheContinueCardShowsTheHeroPlaceAndMetaOfTheMostRecentSave() {
        TranslationServer.SetLocale("en");
        using Fixture fixture = Fixture.Open(TwoSaves());
        MainMenu menu = fixture.Menu;
        AssertThat(menu.ContinueCard.Visible).IsTrue();
        AssertThat(menu.ContinueSlot).IsEqual(1);
        var hero = menu.GetNode<Label>(CardLines + "Hero");
        AssertThat(hero.Text).IsEqual("Joan of Arc");
        AssertThat(hero.Uppercase).IsTrue();
        AssertThat(hero.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.StickerLine1);
        // The short place line: the mission name up to its subtitle, never the long one.
        AssertThat(menu.GetNode<Label>(CardLines + "Place").Text).IsEqual("Pompeii 79 AD");
        AssertThat(menu.GetNode<Label>(CardLines + "Meta/Difficulty").Text).IsEqual("Normal");
        AssertThat(menu.GetNode<Label>(CardLines + "Meta/Dot").Text).IsEqual(MainMenu.MetaSeparator);
        AssertThat(menu.GetNode<Label>(CardLines + "Meta/Progress").Text).IsEqual("Chapter 6");
        // The 86 px portrait frame holds the save's hero, and the glass slab is tagged "Continue".
        var portrait = menu.GetNode<PulpPortraitRoundel>(Card + "Plate/Row/Portrait");
        var joan = AuthoredResources.Load<FTT.Characters.CharacterData>("res://resources/Characters/joan_data.tres");
        AssertThat(portrait.Diameter).IsEqual(86f);
        AssertThat(ReferenceEquals(portrait.Portrait, joan.CharacterPortrait)).OverrideFailureMessage("The frame does not show Joan").IsTrue();
        var plate = menu.GetNode<PanelContainer>(Card + "Plate");
        AssertThat(plate.ThemeTypeVariation.ToString()).IsEqual(UIPalette.Variations.GlassPanelContinue);
        AssertThat(plate.GetNode<PanelTagAnchor>(PulpScreenKit.TagAnchorName).TagLabel.Text).IsEqual("boot_continue");
        AssertThat(plate.GetNodeOrNull<GlassSheen>(GlassSheen.NodeName) != null).OverrideFailureMessage("The card's slab has no sheen").IsTrue();
        // Focusable, but outside the vertical chain: the card is reached sideways.
        AssertThat(menu.ContinueCard.FocusMode).IsEqual(Control.FocusModeEnum.All);
        AssertThat(FocusChainBuilder.Collect(menu.GetNode<Control>("RootScreen")).Contains(menu.ContinueCard)).IsFalse();
    }

    [TestCase]
    public void PressingTheContinueCardResumesTheMostRecentSaveThroughResumeCampaign() {
        var older = Save("einstein", "res://scenes/campaign/Level_02_Orleans.tscn", "2026-10-10T10:00:00.0000000Z");
        var newer = Save("joan", "res://scenes/campaign/Level_06_Pompeii.tscn", "2026-10-20T10:00:00.0000000Z");
        using Fixture fixture = Fixture.Open(new[] { newer, null, older });
        MainMenu menu = fixture.Menu;
        int resumedSlot = -1;
        StorySaveData resumed = null;
        MainMenu.ResumeOverrideForTesting = (slot, save) => { resumedSlot = slot; resumed = save; };
        menu.ContinueCard.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(resumedSlot).IsEqual(0);
        AssertThat(ReferenceEquals(resumed, newer)).IsTrue();
    }

    [TestCase]
    public void ACompletedSaveShowsCampaignCompleteAndAnUnknownScenePathReadsAsTheTutorial() {
        TranslationServer.SetLocale("en");
        var slots = new[] {
            Save("joan", "res://scenes/campaign/Level_15_Alexandria.tscn", "2026-10-20T10:00:00.0000000Z", completed: true, Difficulty.Hard),
            null, null
        };
        using Fixture fixture = Fixture.Open(slots);
        AssertThat(fixture.Menu.GetNode<Label>(CardLines + "Meta/Progress").Text)
            .IsEqual(TranslationServer.Translate("save_completed").ToString());
        AssertThat(fixture.Menu.GetNode<Label>(CardLines + "Meta/Difficulty").Text).IsEqual("Hard");
        AssertThat(MainMenuContinue.ChapterNumber(Save("joan", "res://nowhere.tscn", ""))).IsEqual(0);
        AssertThat(MainMenuContinue.DifficultyKey(Difficulty.Easy)).IsEqual("difficulty_easy");
        AssertThat(MainMenuContinue.DifficultyKey(Difficulty.Normal)).IsEqual("difficulty_normal");
        AssertThat(MainMenuContinue.DifficultyKey(Difficulty.Hard)).IsEqual("difficulty_hard");
    }

    [TestCase]
    public void TheContinueCardIsReachedWithRightFromStoryModeAndLeftReturns() {
        using Fixture fixture = Fixture.Open(TwoSaves());
        MainMenu menu = fixture.Menu;
        Button story = menu.GetNode<Button>(RootLayout + "StoryButton");
        Button card = menu.ContinueCard;
        AssertThat(story.FindValidFocusNeighbor(Side.Right) == card).OverrideFailureMessage("Right from Story Mode misses the card").IsTrue();
        AssertThat(card.FindValidFocusNeighbor(Side.Left) == story).OverrideFailureMessage("Left from the card misses Story Mode").IsTrue();
        foreach (Side side in new[] { Side.Top, Side.Bottom, Side.Right }) {
            AssertThat(card.FindValidFocusNeighbor(side) == card).OverrideFailureMessage("The card lets focus leave by " + side).IsTrue();
        }
        // Focused: the focus edge along its hairline with the glow, and the A prompt; it never slides (no focus pose).
        var frame = card.GetNode<SlabFrame>("FocusFrame");
        var badge = card.GetNode<Control>("PromptBadge");
        AssertThat(frame.Visible).IsFalse();
        card.GrabFocus();
        AssertThat(frame.Visible).IsTrue();
        AssertThat(frame.Mode == SlabFrame.FrameMode.Focus).OverrideFailureMessage("The focused card's frame is " + frame.Mode).IsTrue();
        AssertThat(badge.Visible).IsTrue();
        AssertThat(badge.GetChildCount()).IsEqual(1);
        bool posed = false;
        foreach (Node child in card.GetChildren()) posed |= child is FTT.UI.Motion.FocusPose;
        AssertThat(posed).OverrideFailureMessage("The card carries a focus pose (it must not slide)").IsFalse();
        // The focus edge runs from the opaque focus gold at the left to rgba(glow,.55) at the right (100 degrees).
        Color[] edge = frame.OutlineColors;
        AssertThat(edge[4].A).IsGreater(edge[2].A);
        story.GrabFocus();
        AssertThat(frame.Visible).IsFalse();
        AssertThat(badge.Visible).IsFalse();
    }

    // ---- the title lock-up (3.8, R2, R3) --------------------------------------------------------------------------

    [TestCase]
    public async Task TheTitleIsThreeLinesFromTwoKeysWithTheGradientOnBothTitleWords() {
        TranslationServer.SetLocale("en");
        using Fixture fixture = Fixture.Open(new StorySaveData[3]);
        await Frames(4);
        MainMenu menu = fixture.Menu;
        var title = menu.GetNode<Label>("RootScreen/TitleBlock/Title");
        var middle = menu.GetNode<Label>("RootScreen/TitleBlock/Middle");
        var last = menu.GetNode<Label>("RootScreen/TitleBlock/Last");
        AssertThat(title.Text).IsEqual("menu_cover_title_word");
        AssertThat(middle.Text).IsEqual("Through");
        AssertThat(last.Text).IsEqual("Time");
        AssertThat(middle.Visible && last.Visible).IsTrue();
        // R2: the banner value splits at its first space; no space gives one middle line; empty hides both.
        AssertThat(Split("Through Time")).IsEqual("Through|Time");
        AssertThat(Split("  Durch  die Zeit ")).IsEqual("Durch|die Zeit");
        AssertThat(Split("Zeitreise")).IsEqual("Zeitreise|");
        AssertThat(Split("   ")).IsEqual("|");
        AssertThat(Split(null)).IsEqual("|");
        // T01 on both title words with one gradient material each; T02 on the middle word, without one.
        AssertThat(title.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.CoverTitle);
        AssertThat(last.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.CoverTitle);
        AssertThat(middle.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.CoverBanner);
        var first = title.GetNodeOrNull<TitleGradient>(TitleGradient.NodeName);
        var third = last.GetNodeOrNull<TitleGradient>(TitleGradient.NodeName);
        AssertThat(first != null && third != null).OverrideFailureMessage("A title word has no gradient").IsTrue();
        AssertThat(first.Material != null && third.Material != null && !ReferenceEquals(first.Material, third.Material))
            .OverrideFailureMessage("The two title words must each carry their own gradient material").IsTrue();
        AssertThat(middle.GetNodeOrNull<TitleGradient>(TitleGradient.NodeName) == null).IsTrue();
        foreach (Label label in new[] { title, middle, last }) {
            AssertThat(label.HasThemeFontSizeOverride("font_size")).IsFalse();
        }
        // The lines sit on their line boxes at the fit's title scale t (3.8 at 100 %: FIGHTERS 105.3 from y 33 of the block,
        // THROUGH 57 from 138.3, TIME from 195.3). The debug build's eight rows shrink the title a little (step 1).
        float t = menu.LastRootFit.TitleScale;
        AssertThat(t).IsLessEqual(1f);
        AssertThat(Mathf.Abs(LineCentre(title) / t - (33f + 52.64f))).IsLess(1.5f);
        AssertThat(Mathf.Abs(LineCentre(middle) / t - (138.28f + 28.5f))).IsLess(1.5f);
        AssertThat(Mathf.Abs(LineCentre(last) / t - (195.28f + 52.64f))).IsLess(1.5f);
        // The flourish row above, and THROUGH's rule from 22 px after the word to the block's right edge, on whole pixels.
        var flourish = menu.GetNode<GlassFlourish>("RootScreen/TitleBlock/Flourish");
        AssertThat(flourish.Shape).IsEqual(GlassFlourish.FlourishShape.RuleDiamondRule);
        AssertThat(flourish.Size).IsEqual(new Vector2(Mathf.Round(700f * t), 11f));
        var rule = menu.GetNode<GlassFlourish>("RootScreen/TitleBlock/MiddleRule");
        float wordEnd = middle.Position.X + middle.Size.X * middle.Scale.X;
        AssertThat(rule.Visible).IsTrue();
        AssertThat(Mathf.Abs(rule.Position.X - (wordEnd + 22f * t))).IsLess(2f);
        AssertThat(Mathf.Abs(rule.Position.X + rule.Size.X - Mathf.Round(700f * t))).IsLess(0.5f);
        AssertThat(rule.Position.Y).IsEqual(Mathf.Floor(rule.Position.Y));
        // R3: no tagline under the title: the block's only labels are the three title lines.
        var labels = new List<string>();
        foreach (Node child in menu.GetNode<Control>("RootScreen/TitleBlock").GetChildren()) if (child is Label label) labels.Add(label.Name);
        AssertThat(string.Join(",", labels)).IsEqual("Title,Middle,Last");
    }

    private static float LineCentre(Label label) => label.Position.Y + label.Size.Y * label.Scale.Y / 2f;

    private static string Split(string value) {
        (string middle, string last) = MainMenu.SplitBanner(value);
        return middle + "|" + last;
    }

    // ---- the column, the prompt bar, the art ------------------------------------------------------------------

    [TestCase]
    public async Task EveryRootButtonIsAGlassChevronWithTheDecorSharingTheD6Width() {
        using Fixture fixture = Fixture.Open(new StorySaveData[3]);
        await Frames(4);
        var issues = new List<string>();
        float width = -1f;
        foreach (string name in RootButtons) {
            var button = fixture.Menu.GetNode<Button>(RootLayout + name);
            bool decorated = false;
            foreach (Node child in button.GetChildren()) decorated |= child is PulpButtonDecor;
            if (!decorated) issues.Add(name + " has no PulpButtonDecor");
            if (button.Alignment != HorizontalAlignment.Left) issues.Add(name + " is not left aligned");
            if (button.ThemeTypeVariation.ToString() != UIPalette.Variations.PulpButton) issues.Add(name + " is not a PulpButton");
            if (!Mathf.IsEqualApprox(button.CustomMinimumSize.Y, MainMenu.RowHeight)) issues.Add(name + " is " + button.CustomMinimumSize.Y + " tall, not 68");
            if (width < 0f) width = button.CustomMinimumSize.X;
            else if (!Mathf.IsEqualApprox(width, button.CustomMinimumSize.X)) issues.Add(name + " is " + button.CustomMinimumSize.X + " wide, not the stack's " + width);
        }
        // D6 (3.2): the stack shares its widest required width, at least 560 and capped at 700 (Quick Play's label caps it, R1).
        if (width < MainMenu.ColumnMinWidth || width > MainMenu.ColumnCap + 0.5f) issues.Add("the stack is " + width + " wide");
        var layout = fixture.Menu.GetNode<BoxContainer>(RootLayout.TrimEnd('/'));
        if (layout.GetThemeConstant("separation") != Mathf.RoundToInt(MainMenu.RowGap)) issues.Add("the gap is " + layout.GetThemeConstant("separation"));
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void ThePromptBarPairsSelectAndBackAndSitsBottomRightAtSeventyAndFortyFour() {
        using Fixture fixture = Fixture.Open(new StorySaveData[3]);
        var bar = fixture.Menu.GetNode<PulpPromptBar>("PromptBar");
        AssertThat(bar.PairCount).IsEqual(2);
        AssertThat(bar.CaptionAt(0).Text).IsEqual("ui_prompt_select");
        AssertThat(bar.CaptionAt(1).Text).IsEqual("common_back");
        AssertThat(bar.AnchorLeft).IsEqual(1f);
        AssertThat(bar.AnchorTop).IsEqual(1f);
        AssertThat(bar.OffsetRight).IsEqual(-PulpScreenKit.PromptRightMargin);
        AssertThat(bar.OffsetBottom).IsEqual(-PulpScreenKit.PromptCornerBottomMargin);
        var version = fixture.Menu.GetNode<Label>("VersionLabel");
        AssertThat(version.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.VersionCaption);
        AssertThat(version.OffsetLeft).IsEqual(MainMenu.TitleLeft);
        AssertThat(version.OffsetBottom).IsEqual(-38f);
    }

    [TestCase]
    public void TheMenuArtCarriesTheScrimAndTheScreenStreak() {
        using Fixture fixture = Fixture.Open(new StorySaveData[3]);
        MainMenu menu = fixture.Menu;
        var main = menu.GetNode<TextureRect>("Background/TemporalMainArt");
        var sub = menu.GetNode<TextureRect>("Background/TemporalSubmenuArt");
        var scrim = menu.GetNode<TextureRect>("Background/" + PulpScreenKit.MenuScrimName);
        AssertThat(main.Texture.ResourcePath).IsEqual("res://assets/backgrounds/menu/temporal_glass_main_menu_background.png");
        AssertThat(sub.Texture.ResourcePath).IsEqual("res://assets/backgrounds/menu/temporal_glass_submenu_background.png");
        AssertThat(main.StretchMode).IsEqual(TextureRect.StretchModeEnum.KeepAspectCovered);
        AssertThat(ReferenceEquals(scrim.Texture, PulpKit.Texture("glass_menu_scrim"))).OverrideFailureMessage("The scrim is not the kit's menu scrim").IsTrue();
        AssertThat(scrim.GetIndex()).IsGreater(sub.GetIndex());
        var streak = menu.GetNodeOrNull<ScreenStreak>(PulpScreenKit.ScreenStreakName);
        AssertThat(streak != null).OverrideFailureMessage("The menu has no screen streak").IsTrue();
        AssertThat(streak.GetIndex()).IsGreater(menu.GetNode<Control>("PromptBar").GetIndex());
        AssertThat(main.Visible && !sub.Visible && scrim.Visible && streak.Visible).OverrideFailureMessage("The root's art is wrong").IsTrue();
        // A sub-screen is a full-screen menu over the sub-screen art, no scrim (its text sits on glass), the streak kept.
        menu.GetNode<Button>(RootLayout + "StoryButton").EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.SlotSelect);
        AssertThat(!main.Visible && sub.Visible && !scrim.Visible && streak.Visible).OverrideFailureMessage("The sub-screen's art is wrong").IsTrue();
        menu.GoBack();
        AssertThat(main.Visible && !sub.Visible && scrim.Visible).IsTrue();
    }

    // ---- the fit (4.1) -------------------------------------------------------------------------------------------

    [TestCase]
    public async Task TheBottomAnchoredColumnKeepsTwentyFourPixelsUnderTheTitleWithSixSevenAndEightRows() {
        const float Title = 300.56f;
        var issues = new List<string>();
        foreach (float scale in new[] { 1f, 1.4f }) {
            foreach (int rows in new[] { 6, 7, 8 }) {
                MainMenu.RootFit fit = MainMenu.ComputeRootFit(scale, rows, Title);
                string at = rows + " rows at " + scale + ": ";
                if (fit.ColumnTop - fit.TitleBottom < MainMenu.TitleClearance - 0.01f) {
                    issues.Add(at + "the column top " + fit.ColumnTop + " is within 24 px of the title's bottom " + fit.TitleBottom);
                }
                if (fit.TitleScale > Mathf.Min(1f, scale) + 0.0001f) issues.Add(at + "the title grows to " + fit.TitleScale);
                float height = rows * fit.RowPixels + (rows - 1) * fit.Gap;
                if (Mathf.Abs(MainMenu.ColumnBottom - fit.ColumnTop - height * fit.ColumnScale) > 0.01f) issues.Add(at + "the column does not stand on y 983");
            }
        }
        // 4.1's arithmetic: six rows at 1.0 need nothing; at 1.4 step 1 suffices (here 0.82: no tagline, R3); eight at 1.4 reach step 4.
        AssertThat(MainMenu.ComputeRootFit(1f, 6, Title).Step).IsEqual(0);
        AssertThat(MainMenu.ComputeRootFit(1.4f, 6, Title).Step).IsEqual(1);
        AssertThat(Mathf.Abs(MainMenu.ComputeRootFit(1.4f, 6, Title).ColumnTop - 334.8f)).IsLess(0.01f);
        AssertThat(MainMenu.ComputeRootFit(1.4f, 8, Title).Step).IsEqual(4);
        // A row is never shorter than its own text line: a taller natural row height pushes the fit one step further.
        MainMenu.RootFit natural = MainMenu.ComputeRootFit(1.4f, 8, Title, 0f, 88f);
        AssertThat(natural.RowPixels).IsEqual(88f);
        AssertThat(natural.Step).IsEqual(5);
        AssertThat(natural.ColumnTop - natural.TitleBottom).IsGreaterEqual(MainMenu.TitleClearance - 0.01f);
        // The column's width cap keeps its focused row 24 px left of the Continue card (870 at 1.4), and 700 at 1.0.
        AssertThat(MainMenu.ColumnCapAt(1f, 1920f)).IsEqual(700f);
        AssertThat(Mathf.Abs(MainMenu.ColumnCapAt(1.4f, 1920f) - 870.4f)).IsLess(0.01f);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

        // Live, with the debug build's eight rows and a save, at 1.0 and 1.4.
        float saved = UIPalette.AppliedUiScale;
        try {
            foreach (float scale in new[] { 1f, 1.4f }) {
                using Fixture fixture = Fixture.Open(TwoSaves(), scale);
                await Frames(6);
                MainMenu menu = fixture.Menu;
                Rect2 column = menu.GetNode<Control>("RootScreen/Center/Panel").GetGlobalRect();
                var block = menu.GetNode<Control>("RootScreen/TitleBlock");
                float titleBottom = block.GlobalPosition.Y + block.Size.Y * block.Scale.Y;
                Rect2 card = menu.ContinueCard.GetGlobalRect();
                string at = "live at " + scale + ": ";
                if (Mathf.Abs(column.End.Y - MainMenu.ColumnBottom) > 1.5f) issues.Add(at + "the column ends at " + column.End.Y);
                if (Mathf.Abs(column.Position.X - MainMenu.ColumnLeft) > 0.5f) issues.Add(at + "the column starts at x " + column.Position.X);
                if (column.Position.Y - titleBottom < MainMenu.TitleClearance - 1f) issues.Add(at + "the column top " + column.Position.Y + " is within 24 px of the title " + titleBottom);
                if (column.End.X + 14f * scale + MainMenu.CardClearance > card.Position.X + 1f) issues.Add(at + "the column " + column.End.X + " crowds the card " + card.Position.X);
                if (Mathf.Abs(card.End.X - (1920f - MainMenu.CardRightMargin)) > 1f || Mathf.Abs(card.End.Y - (1080f - MainMenu.CardBottomMargin)) > 1f) {
                    issues.Add(at + "the card ends at " + card.End + ", not 70 / 120 from the corner");
                }
                if (Mathf.Abs(card.Size.X - Mathf.Round(MainMenu.CardWidth * scale)) > 1f) issues.Add(at + "the card is " + card.Size.X + " wide");
            }
        } finally {
            UIPalette.ApplyUiScale(saved);
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    // ---- the sub-screens (9.2) ----------------------------------------------------------------------------------

    [TestCase]
    public async Task EverySubScreenIsAGlassSlabWithItsTagTitleFlourishesAndDecoratedChevrons() {
        TranslationServer.SetLocale("en");
        using Fixture fixture = Fixture.Open(new StorySaveData[3]);
        await Frames(4);
        MainMenu menu = fixture.Menu;
        var issues = new List<string>();
        foreach (string screen in SubScreens) {
            var panel = menu.GetNode<PanelContainer>(screen + "/Center/Panel");
            if (panel.GetNodeOrNull<GlassSheen>(GlassSheen.NodeName) == null) issues.Add(screen + " has no sheen");
            if (panel.GetNodeOrNull<PanelTagAnchor>(PulpScreenKit.TagAnchorName) is not { } tag || tag.TagLabel.Text.Length == 0) issues.Add(screen + " has no tag");
            if (panel.GetNodeOrNull<Label>("Layout/Title")?.GetNodeOrNull<TitleFlourishes>(TitleFlourishes.NodeName) == null) {
                issues.Add(screen + "'s title has no flourishes");
            }
            foreach (Node node in menu.GetNode(screen).FindChildren("*", "Button", true, false)) {
                var button = (Button)node;
                if (button.Flat) continue; // the glass slabs, the tiles and the licence rows draw their own look
                PulpButtonDecor decor = null;
                foreach (Node child in button.GetChildren()) if (child is PulpButtonDecor found) decor = found;
                if (decor == null) issues.Add(screen + "/" + button.Name + " has no decor");
                else if (button.Name == "BackButton" && decor.ShowPrompt) issues.Add(screen + "'s Back shows a prompt (a compact footer, D6)");
            }
        }
        // Open the slot screen (Story Mode). Its rows show SaveManager's slots (the screen reads them for display only; no
        // slot row is pressed, so nothing loads or writes): TG-CLOSE folded the quiet empty slab into the theme, so an empty
        // slot's slab wears GlassPanelEmpty and a filled one GlassPanelContinue, with no local panel box at 1.0.
        menu.GetNode<Button>("RootScreen/Center/Panel/Layout/StoryButton").EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        for (int slot = 0; slot < 3; slot++) {
            var button = menu.GetNode<Button>($"SlotScreen/Center/Panel/Layout/SlotRow{slot}/SlotButton");
            var slab = button.GetNodeOrNull<PanelContainer>("Slab");
            bool empty = SaveManager.Instance?.SaveSlots[slot] == null;
            string slabVariation = empty ? UIPalette.Variations.GlassPanelEmpty : UIPalette.Variations.GlassPanelContinue;
            if (slab == null || slab.ThemeTypeVariation.ToString() != slabVariation) issues.Add("slot " + slot + " (" + (empty ? "empty" : "filled") + ") wears " + slab?.ThemeTypeVariation + ", not " + slabVariation);
            else if (slab.HasThemeStyleboxOverride("panel")) issues.Add("slot " + slot + " paints its own slab");
            if (button.GetNodeOrNull<SlabFrame>("SlabFrame") == null) issues.Add("slot " + slot + " has no frame");
            if (button.CustomMinimumSize != new Vector2(MainMenu.SlotSlabWidth, MainMenu.SlotSlabHeight)) issues.Add("slot " + slot + " is " + button.CustomMinimumSize);
        }
        var grid = menu.GetNode<GridContainer>("CharacterScreen/Center/Panel/Layout/Body/Grid");
        if (grid.Columns != 3) issues.Add("the character grid has " + grid.Columns + " columns");
        for (int i = 0; i < grid.GetChildCount(); i++) {
            var tile = (Button)grid.GetChild(i);
            if (tile.GetNodeOrNull<GlassTileDecor>(GlassTileDecor.NodeName) == null) issues.Add(tile.Name + " is not a glass tile");
            if (tile.CustomMinimumSize != new Vector2(MainMenu.TileSize, MainMenu.TileSize)) issues.Add(tile.Name + " is " + tile.CustomMinimumSize);
        }
        var hero = menu.GetNode<PanelContainer>("CharacterScreen/Center/Panel/Layout/Body/HeroSlab");
        if (hero.ThemeTypeVariation.ToString() != UIPalette.Variations.GlassPanelStats) issues.Add("the hero slab is " + hero.ThemeTypeVariation);
        if (menu.GetNode<GridContainer>("LevelSelectScreen/Center/Panel/Layout/Grid").Columns != MainMenu.LevelColumns) issues.Add("the level grid does not have MainMenu.LevelColumns columns");
        // The focused character tile drives the side slab: its name and its style as the tag. (The screen is shown directly:
        // pressing a slot row would read this machine's saves.)
        menu.GetNode<Control>("RootScreen").Visible = false;
        menu.GetNode<Control>("CharacterScreen").Visible = true;
        ((Button)grid.GetChild(1)).GrabFocus();
        string expected = FTT.Core.CharacterRoster.ToArray()[1];
        if (menu.ShownHeroID != expected) issues.Add("the side slab shows '" + menu.ShownHeroID + "', not " + expected);
        if (hero.GetNode<PanelTagAnchor>(PulpScreenKit.TagAnchorName).TagLabel.Text.StartsWith("movelist_style_") == false) issues.Add("the side slab's tag is not the style");
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheSceneAndScriptsCarryNoPulpArtNoFontSizeOverrideAndNoChromeCyan() {
        string source = File.ReadAllText(ProjectSettings.GlobalizePath(ScenePath));
        foreach (string retiredPath in FTT.Tests.ContentValidation.UiLegacyKitReferenceScanTests.RetiredPaths) {
            AssertThat(source.Contains(retiredPath)).OverrideFailureMessage("The scene still references pulp art").IsFalse();
        }
        AssertThat(source.Contains("theme_override_font_sizes")).OverrideFailureMessage("A font-size override survives").IsFalse();
        AssertThat(source.Contains("Color(0, 0.9, 0.9, 1)")).OverrideFailureMessage("A chrome cyan literal survives").IsFalse();
        AssertThat(source.Contains("type=\"StyleBoxTexture\"")).OverrideFailureMessage("The scene carries its own texture plate").IsFalse();
        AssertThat(source.Contains("temporal_glass_main_menu_background.png")).OverrideFailureMessage("The root lost its glass art").IsTrue();
        AssertThat(source.Contains("temporal_glass_submenu_background.png")).OverrideFailureMessage("The sub-screens lost their art").IsTrue();
        // No retired pulp variation, role or kit path anywhere in the menu's scene or scripts (TG-CLOSE keeps the one retired
        // list in UiLegacyKitReferenceScanTests, which also scans the whole repository).
        var retired = new List<string>(FTT.Tests.ContentValidation.UiLegacyKitReferenceScanTests.RetiredVariations);
        retired.AddRange(FTT.Tests.ContentValidation.UiLegacyKitReferenceScanTests.RetiredRoles);
        retired.AddRange(FTT.Tests.ContentValidation.UiLegacyKitReferenceScanTests.RetiredPaths);
        foreach (string file in new[] { ScenePath, "res://scripts/UI/MainMenu.cs", "res://scripts/UI/MainMenu.Cover.cs",
                     "res://scripts/UI/MainMenu.SubScreens.cs" }) {
            string text = File.ReadAllText(ProjectSettings.GlobalizePath(file));
            foreach (string name in retired) {
                bool named = name.Contains('/') ? text.Contains(name) : Regex.IsMatch(text, @"\b" + name + @"\b");
                AssertThat(named).OverrideFailureMessage(file + " names the retired " + name).IsFalse();
            }
        }
    }

    private static async Task Frames(int count) {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int i = 0; i < count; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>One open menu with the seams set; disposing frees it and restores every seam and the session.</summary>
    private sealed class Fixture : System.IDisposable {
        public MainMenu Menu;
        private Node _host;
        private SessionData? _session;
        private StorySaveData[] _savedSlots;
        private bool? _savedBoot;
        private bool? _savedDev;
        private System.Action<int, StorySaveData> _savedResume;
        private float _scale = 1f;

        public static Fixture Open(StorySaveData[] slots, float uiScale = 1f) {
            var fixture = new Fixture {
                _scale = UIPalette.AppliedUiScale,
                _session = GameManager.Instance?.CurrentSession,
                _savedSlots = MainMenu.SaveSlotsOverrideForTesting,
                _savedBoot = MainMenu.BootSequenceOverrideForTesting,
                _savedDev = MainMenu.DeveloperLevelSelectOverrideForTesting,
                _savedResume = MainMenu.ResumeOverrideForTesting
            };
            MainMenu.SaveSlotsOverrideForTesting = slots;
            MainMenu.BootSequenceOverrideForTesting = false;
            MainMenu.DeveloperLevelSelectOverrideForTesting = true;
            UIPalette.ApplyUiScale(uiScale);
            fixture._host = new Node { Name = "MainMenuCoverHost" };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(fixture._host);
            fixture.Menu = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<MainMenu>();
            fixture._host.AddChild(fixture.Menu);
            return fixture;
        }

        public void Dispose() {
            try {
                if (GodotObject.IsInstanceValid(_host)) {
                    _host.GetParent()?.RemoveChild(_host);
                    _host.Free();
                }
            } finally {
                MainMenu.SaveSlotsOverrideForTesting = _savedSlots;
                MainMenu.BootSequenceOverrideForTesting = _savedBoot;
                MainMenu.DeveloperLevelSelectOverrideForTesting = _savedDev;
                MainMenu.ResumeOverrideForTesting = _savedResume;
                UIPalette.ApplyUiScale(_scale);
                if (_session.HasValue && GameManager.Instance != null) GameManager.Instance.CurrentSession = _session.Value;
            }
        }
    }
}
