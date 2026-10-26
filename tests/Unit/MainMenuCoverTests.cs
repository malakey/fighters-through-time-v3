using System.Collections.Generic;
using System.IO;
using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-MENU (pulp UI redesign, MENU-B). The cover: the two-key live title, the three inked hero poses, the
/// kit decor on every banner button, the prompt bar and the Continue sticker. The sticker reads its saves
/// through <c>MainMenu.SaveSlotsOverrideForTesting</c> and resumes through
/// <c>MainMenu.ResumeOverrideForTesting</c>, so nothing here depends on (or writes) the saves on this machine.
/// The session is snapshotted and restored (CLAUDE.md failure signature 8).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MainMenuCoverTests {

    private const string ScenePath = "res://scenes/menus/MainMenu.tscn";
    private const string RootLayout = "RootScreen/Center/Panel/Layout/";

    private static StorySaveData Save(string character, string scene, string stamp, bool completed = false) => new() {
        SelectedCharacterID = character,
        CurrentLevelID = scene,
        LastSavedTimestamp = stamp,
        IsCompleted = completed
    };

    [TestCase]
    public void TheStickerIsHiddenWithNoSaveAndOutOfTheFocusChain() {
        using Fixture fixture = Fixture.Open(new StorySaveData[3]);
        MainMenu menu = fixture.Menu;
        AssertThat(menu.ContinueSticker != null).OverrideFailureMessage("The cover has no ContinueSticker").IsTrue();
        AssertThat(menu.ContinueSticker.Visible).IsFalse();
        AssertThat(menu.ContinueSlot).IsEqual(-1);
        AssertThat(FocusChainBuilder.Collect(menu.GetNode<Control>("RootScreen")).Contains(menu.ContinueSticker)).IsFalse();
    }

    [TestCase]
    public void TheStickerShowsTheChapterAndPlaceOfTheMostRecentSaveAndJoinsTheFocusChain() {
        TranslationServer.SetLocale("en");
        var slots = new[] {
            Save("einstein", "res://scenes/campaign/Level_02_Orleans.tscn", "2026-10-10T10:00:00.0000000Z"),
            Save("joan", "res://scenes/campaign/Level_06_Pompeii.tscn", "2026-10-20T10:00:00.0000000Z"),
            null
        };
        using Fixture fixture = Fixture.Open(slots);
        MainMenu menu = fixture.Menu;
        AssertThat(menu.ContinueSticker.Visible).IsTrue();
        AssertThat(menu.ContinueSlot).IsEqual(1);
        string chapter = menu.GetNode<Label>("RootScreen/ContinueSticker/Visual/Line2").Text;
        string place = menu.GetNode<Label>("RootScreen/ContinueSticker/Visual/Line3").Text;
        AssertThat(chapter).IsEqual("Chapter 6");
        // The short place line: the mission name up to its subtitle, never the long one.
        AssertThat(place).IsEqual("Pompeii 79 AD");
        AssertThat(menu.GetNode<Label>("RootScreen/ContinueSticker/Visual/Line1").Text).IsEqual("boot_continue");
        AssertThat(FocusChainBuilder.Collect(menu.GetNode<Control>("RootScreen")).Contains(menu.ContinueSticker)).IsTrue();
        AssertThat(menu.ContinueSticker.FocusMode).IsEqual(Control.FocusModeEnum.All);
    }

    [TestCase]
    public void PressingTheStickerResumesTheMostRecentSaveThroughResumeCampaign() {
        var older = Save("einstein", "res://scenes/campaign/Level_02_Orleans.tscn", "2026-10-10T10:00:00.0000000Z");
        var newer = Save("joan", "res://scenes/campaign/Level_06_Pompeii.tscn", "2026-10-20T10:00:00.0000000Z");
        using Fixture fixture = Fixture.Open(new[] { newer, null, older });
        MainMenu menu = fixture.Menu;
        int resumedSlot = -1;
        StorySaveData resumed = null;
        MainMenu.ResumeOverrideForTesting = (slot, save) => { resumedSlot = slot; resumed = save; };
        menu.ContinueSticker.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(resumedSlot).IsEqual(0);
        AssertThat(ReferenceEquals(resumed, newer)).IsTrue();
    }

    [TestCase]
    public void ACompletedSaveShowsTheCompletionLineAndAnUnknownScenePathReadsAsTheTutorial() {
        TranslationServer.SetLocale("en");
        var slots = new[] { Save("joan", "res://scenes/campaign/Level_15_Alexandria.tscn", "2026-10-20T10:00:00.0000000Z", completed: true), null, null };
        using Fixture fixture = Fixture.Open(slots);
        AssertThat(fixture.Menu.GetNode<Label>("RootScreen/ContinueSticker/Visual/Line2").Text)
            .IsEqual(TranslationServer.Translate("save_completed").ToString());
        AssertThat(MainMenuContinue.ChapterNumber(Save("joan", "res://nowhere.tscn", ""))).IsEqual(0);
    }

    [TestCase]
    public void TheTitleIsTwoLiveKeysAndTheBannerHidesWhenItsKeyIsEmpty() {
        TranslationServer.SetLocale("en");
        using Fixture fixture = Fixture.Open(new StorySaveData[3]);
        MainMenu menu = fixture.Menu;
        AssertThat(menu.GetNode<Label>("RootScreen/TitleBlock/Title").Text).IsEqual("menu_cover_title_word");
        AssertThat(menu.GetNode<Label>("RootScreen/TitleBlock/Banner/BannerLabel").Text).IsEqual("menu_cover_title_banner");
        AssertThat(menu.GetNode<Control>("RootScreen/TitleBlock/Banner").Visible).IsTrue();
        AssertThat(TranslationServer.Translate("menu_cover_title_word").ToString()).IsEqual("Fighters");
        AssertThat(TranslationServer.Translate("menu_cover_title_banner").ToString()).IsEqual("Through Time");
        // A locale may put the whole title in the first key and leave the second empty.
        AssertThat(MainMenu.BannerShows("Through Time")).IsTrue();
        AssertThat(MainMenu.BannerShows("")).IsFalse();
        AssertThat(MainMenu.BannerShows("  ")).IsFalse();
        // Both roles come from the theme: no font-size override on either label.
        foreach (string path in new[] { "RootScreen/TitleBlock/Title", "RootScreen/TitleBlock/Banner/BannerLabel" }) {
            AssertThat(menu.GetNode<Label>(path).HasThemeFontSizeOverride("font_size")).IsFalse();
        }
        AssertThat(menu.GetNode<Label>("RootScreen/TitleBlock/Title").ThemeTypeVariation.ToString()).IsEqual("CoverTitle");
        AssertThat(menu.GetNode<Label>("RootScreen/TitleBlock/Banner/BannerLabel").ThemeTypeVariation.ToString()).IsEqual("CoverBanner");
    }

    [TestCase]
    public void EveryRootButtonIsAPulpBannerWithTheDecorAndALeftAlignedLabel() {
        using Fixture fixture = Fixture.Open(new StorySaveData[3]);
        var issues = new List<string>();
        foreach (string name in new[] {
                     "QuickPlayButton", "StoryButton", "LevelSelectButton", "FighterButton",
                     "CalibrationDrillsButton", "SettingsButton", "ExtrasButton", "QuitButton" }) {
            var button = fixture.Menu.GetNode<Button>(RootLayout + name);
            bool decorated = false;
            foreach (Node child in button.GetChildren()) decorated |= child is PulpButtonDecor;
            if (!decorated) issues.Add(name + " has no PulpButtonDecor");
            if (button.Alignment != HorizontalAlignment.Left) issues.Add(name + " is not left aligned");
            if (button.ThemeTypeVariation.ToString() != "PulpButton") issues.Add(name + " is not a PulpButton");
            if (button.CustomMinimumSize != new Vector2(640f, 80f)) issues.Add(name + " is not 640 x 80");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EverySubScreenBannerButtonCarriesTheDecor() {
        using Fixture fixture = Fixture.Open(new StorySaveData[3]);
        var issues = new List<string>();
        foreach (string screen in new[] {
                     "SlotScreen", "CharacterScreen", "DifficultyScreen", "LevelSelectScreen", "ExtrasScreen", "LicensesScreen" }) {
            foreach (Node node in fixture.Menu.GetNode(screen).FindChildren("*", "Button", true, false)) {
                var button = (Button)node;
                if (button.Flat) continue; // the licence rows are plain list rows, not banners
                bool decorated = false;
                foreach (Node child in button.GetChildren()) decorated |= child is PulpButtonDecor;
                if (!decorated) issues.Add(screen + "/" + button.Name + " has no decor");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void ThePromptBarPairsSelectAndBackAndSitsBottomRight() {
        using Fixture fixture = Fixture.Open(new StorySaveData[3]);
        var bar = fixture.Menu.GetNode<PulpPromptBar>("PromptBar");
        AssertThat(bar.PairCount).IsEqual(2);
        AssertThat(bar.CaptionAt(0).Text).IsEqual("ui_prompt_select");
        AssertThat(bar.CaptionAt(1).Text).IsEqual("common_back");
        AssertThat(bar.AnchorLeft).IsEqual(1f);
        AssertThat(bar.AnchorTop).IsEqual(1f);
    }

    [TestCase]
    public void TheThreeHeroesAreInkedPosesStandingOnTheFloorLine() {
        using Fixture fixture = Fixture.Open(new StorySaveData[3]);
        var issues = new List<string>();
        foreach (string hero in new[] { "Joan", "Einstein", "Cleopatra" }) {
            var rect = fixture.Menu.GetNode<TextureRect>("RootScreen/Heroes/" + hero);
            if (rect.Texture is not ImageTexture) issues.Add(hero + " is not the inked bake");
            // The feet (plus the shadow's pad) end just under y 1020 and well above the bottom of the screen.
            float bottom = rect.Position.Y + rect.Size.Y;
            if (bottom < 1020f || bottom > 1080f) issues.Add(hero + " bottom " + bottom);
            if (rect.Size.X < 200f || rect.Size.Y < 250f) issues.Add(hero + " is too small: " + rect.Size);
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheRootColumnTightensItsPitchSoEightRowsEndAboveThePromptBar() {
        AssertThat(MainMenu.RootRowSeparation(6)).IsEqual(17f);
        AssertThat(MainMenu.RootRowSeparation(7)).IsEqual(17f);
        float eight = MainMenu.RootRowSeparation(8);
        AssertThat(eight).IsGreaterEqual(4f);
        AssertThat(eight).IsLess(17f);
        AssertThat(322f + 8 * 80f + 7 * eight).IsLessEqual(990f);
    }

    [TestCase]
    public void TheSceneCarriesNoPerButtonTextureOrFontSizeOverrideAndNoChromeCyan() {
        string source = File.ReadAllText(ProjectSettings.GlobalizePath(ScenePath));
        AssertThat(source.Contains("GlassBtn")).OverrideFailureMessage("A glass button style survives").IsFalse();
        AssertThat(source.Contains("theme_override_font_sizes")).OverrideFailureMessage("A font-size override survives").IsFalse();
        AssertThat(source.Contains("Color(0, 0.9, 0.9, 1)")).OverrideFailureMessage("A chrome cyan literal survives").IsFalse();
        // The cover banner plate is the theme's CoverBannerPanel variation (closeout), so the scene carries no texture box of its own.
        AssertThat(CountOf(source, "type=\"StyleBoxTexture\"")).IsEqual(0);
        AssertThat(source.Contains("theme_type_variation = &\"CoverBannerPanel\"")).OverrideFailureMessage("The banner lost its theme variation").IsTrue();
    }

    private static int CountOf(string text, string needle) {
        int count = 0;
        int at = 0;
        while ((at = text.IndexOf(needle, at, System.StringComparison.Ordinal)) >= 0) {
            count++;
            at += needle.Length;
        }
        return count;
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

        public static Fixture Open(StorySaveData[] slots) {
            var fixture = new Fixture {
                _session = GameManager.Instance?.CurrentSession,
                _savedSlots = MainMenu.SaveSlotsOverrideForTesting,
                _savedBoot = MainMenu.BootSequenceOverrideForTesting,
                _savedDev = MainMenu.DeveloperLevelSelectOverrideForTesting,
                _savedResume = MainMenu.ResumeOverrideForTesting
            };
            MainMenu.SaveSlotsOverrideForTesting = slots;
            MainMenu.BootSequenceOverrideForTesting = false;
            MainMenu.DeveloperLevelSelectOverrideForTesting = true;
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
                if (_session.HasValue && GameManager.Instance != null) GameManager.Instance.CurrentSession = _session.Value;
            }
        }
    }
}
