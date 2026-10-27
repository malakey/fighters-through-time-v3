using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B4. The authored main menu: it instantiates from the scene rather
/// than building itself, every screen's interactive controls are reachable by
/// keyboard/controller focus, <c>ui_cancel</c> walks the screen stack back to the
/// root and stops there, and slot deletion goes through the shared themed
/// <see cref="ConfirmModal"/> instead of a native <c>ConfirmationDialog</c>.
///
/// <para>Save discipline: every test that touches <c>SaveManager.SaveSlots</c>
/// swaps synthetic data in memory and restores the originals in a finally block,
/// following the repository's scratch-slot idiom. The one test that drives the
/// real <c>DeleteStorySlot</c> — which reaches disk — first picks a slot that is
/// empty in memory <b>and</b> has no file of any kind on disk, so the deletion
/// is a no-op against the real <c>user://saves</c> and can never destroy a
/// developer's save or a preserved <c>.corrupt</c> candidate.</para>
///
/// <para>Session discipline (2026-10-11): pressing an empty slot row writes
/// <c>ActiveSaveSlot</c> into <c>GameManager.CurrentSession</c>. <see cref="Open"/>
/// snapshots the session and <see cref="Teardown"/> restores it, because a leaked
/// slot index made every later suite build its heroes behind the real slot's
/// Legacy Unlock locks (ten Story cast cases stuck in Idle/Running/Airborne) and
/// pointed later suites' autosaves at the developer's real slot file.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MainMenuSceneTests {

    private const string ScenePath = "res://scenes/menus/MainMenu.tscn";
    private const string RootLayout = "RootScreen/Center/Panel/Layout/";
    private const string SlotLayout = "SlotScreen/Center/Panel/Layout/";
    // TG-MENU: the character grid sits in Layout/Body beside the hero's side slab.
    private const string CharacterLayout = "CharacterScreen/Center/Panel/Layout/Body/";
    private const string LevelLayout = "LevelSelectScreen/Center/Panel/Layout/";
    private const string DifficultyLayout = "DifficultyScreen/Center/Panel/Layout/";

    [TestCase]
    public void TheAuthoredSceneCarriesEveryScreenAndControlTheScriptBindsByPath() {
        MainMenu menu = Open(out Node host);
        try {
            AssertThat(menu.GetNodeOrNull<Control>("Background") != null).IsTrue();

            // Four authored screens, not four runtime-constructed panels.
            foreach (string screen in ScreenNames()) {
                AssertThat(menu.GetNodeOrNull<Control>(screen) != null)
                    .OverrideFailureMessage($"Missing authored screen {screen}")
                    .IsTrue();
            }

            foreach (string button in new[] {
                         "QuickPlayButton", "StoryButton", "LevelSelectButton", "FighterButton",
                         "CalibrationDrillsButton", "SettingsButton", "ExtrasButton", "QuitButton" }) {
                AssertThat(menu.GetNodeOrNull<Button>(RootLayout + button) != null)
                    .OverrideFailureMessage($"Missing authored root button {button}")
                    .IsTrue();
            }

            // Three authored slot rows: the save-slot count is a fixed contract.
            for (int slot = 0; slot < 3; slot++) {
                AssertThat(menu.GetNodeOrNull<Button>($"{SlotLayout}SlotRow{slot}/SlotButton") != null)
                    .OverrideFailureMessage($"Missing slot button {slot}")
                    .IsTrue();
                AssertThat(menu.GetNodeOrNull<Button>($"{SlotLayout}SlotRow{slot}/DeleteButton") != null)
                    .OverrideFailureMessage($"Missing delete button {slot}")
                    .IsTrue();
            }

            // Package 11 A6b: one authored tile per manifest roster member.
            // Phrased as agreement rather than as the number nine, so adding a
            // character surfaces as "author the tile" instead of as an
            // unrelated menu regression.
            AssertThat(menu.GetNode<GridContainer>(CharacterLayout + "Grid").GetChildCount())
                .IsEqual(FTT.Core.CharacterRoster.Count);

            // Sixteen authored developer level-select tiles, one per routed
            // CampaignLevel (S27 retired Level 4A's seventeenth tile).
            AssertThat(menu.GetNode<GridContainer>(LevelLayout + "Grid").GetChildCount()).IsEqual(16);

            // The A4 save-notice slot must survive the conversion, whether or not a
            // notice is actually pending in this session.
            AssertThat(menu.GetNodeOrNull<Control>(RootLayout + "NoticeSlot") != null).IsTrue();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void StaticCopyIsStoredAsRawTranslationKeysAndResolvesThroughTheCompiledTable() {
        TranslationServer.SetLocale("en");
        MainMenu menu = Open(out Node host);
        try {
            // The A1 convention: the scene stores the key, Godot's automatic
            // control translation resolves it, so a language change needs no rebuild.
            // TG-MENU (ruling R2): the title's first line is the raw key; its middle and last lines are the banner key's
            // value split at its first space, resolved in code.
            AssertThat(menu.GetNode<Label>("RootScreen/TitleBlock/Title").Text).IsEqual("menu_cover_title_word");
            AssertThat(menu.GetNode<Label>("RootScreen/TitleBlock/Middle").Text).IsEqual("Through");
            AssertThat(menu.GetNode<Label>("RootScreen/TitleBlock/Last").Text).IsEqual("Time");
            AssertThat(menu.GetNode<Button>(RootLayout + "StoryButton").Text).IsEqual("menu_story_mode");
            AssertThat(menu.GetNode<Label>(SlotLayout + "Title").Text).IsEqual("save_select_title");

            foreach (string key in new[] {
                         "game_title", "menu_cover_title_word", "menu_cover_title_banner", "menu_quick_play", "menu_story_mode",
                         "menu_fighter_mode", "menu_calibration_drills", "menu_settings",
                         "menu_quit", "save_select_title",
                         "save_delete", "common_back", "story_select_character",
                         "story_select_difficulty", "menu_level_select", "dev_level_select_title" }) {
                AssertThat(TranslationServer.Translate(key).ToString())
                    .OverrideFailureMessage($"{key} does not resolve through the compiled translation")
                    .IsNotEqual(key);
            }
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void EveryRosterTileCarriesALocalizedCharacterNameRatherThanARawKey() {
        TranslationServer.SetLocale("en");
        MainMenu menu = Open(out Node host);
        try {
            var grid = menu.GetNode<GridContainer>(CharacterLayout + "Grid");
            for (int index = 0; index < 9; index++) {
                // CharacterName() resolves through AuthoredResources, not GD.Load —
                // a bare load drops the pinned instance and rebuilds the scripted
                // resource graph on every call (CLAUDE.md failure signature 2).
                string text = grid.GetNode<Button>($"CharacterButton{index}").Text;
                AssertThat(text.Length > 0)
                    .OverrideFailureMessage($"Tile {index} has no name")
                    .IsTrue();
                AssertThat(text)
                    .OverrideFailureMessage($"Tile {index} could not resolve its character")
                    .IsNotEqual(TranslationServer.Translate("common_unknown").ToString());
                AssertThat(text.StartsWith("character_"))
                    .OverrideFailureMessage($"Tile {index} shows a raw key")
                    .IsFalse();
            }
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void EveryInteractiveControlOnEveryScreenJoinsItsFocusChain() {
        StorySaveData[] originals = ClearAllSlots();
        MainMenu menu = Open(out Node host);
        try {
            // Root screen: seven production buttons (Quick Play, Story, Fighter,
            // Calibration Drills, Settings, Extras, Quit) plus the debug-build
            // developer level select (the test host is a debug build), all chained
            // and reachable. LAN was de-scoped to Package 7 (V7.3); Extras is
            // Package 12 W6 (G15e).
            AssertChainCoversScreen(menu, "RootScreen", 8);

            Press(menu, RootLayout + "StoryButton");
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.SlotSelect);
            // Three empty slot buttons plus back; delete buttons are hidden for
            // empty slots and correctly stay out of the chain.
            AssertChainCoversScreen(menu, "SlotScreen", 4);

            Press(menu, SlotLayout + "SlotRow0/SlotButton");
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.CharacterSelect);
            AssertChainCoversScreen(menu, "CharacterScreen", 10);

            Press(menu, CharacterLayout + "Grid/CharacterButton0");
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.DifficultySelect);
            AssertChainCoversScreen(menu, "DifficultyScreen", 4);
        } finally {
            Teardown(host);
            RestoreSlots(originals);
        }
    }

    [TestCase]
    public void CancelWalksTheScreenStackBackAndStopsAtTheRoot() {
        StorySaveData[] originals = ClearAllSlots();
        MainMenu menu = Open(out Node host);
        try {
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.Root);
            AssertThat(menu.ScreenDepth).IsEqual(1);

            Press(menu, RootLayout + "StoryButton");
            Press(menu, SlotLayout + "SlotRow0/SlotButton");
            Press(menu, CharacterLayout + "Grid/CharacterButton0");
            AssertThat(menu.ScreenDepth).IsEqual(4);

            menu.GoBack();
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.CharacterSelect);
            AssertThat(menu.GetNode<Control>("CharacterScreen").Visible).IsTrue();

            menu.GoBack();
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.SlotSelect);

            menu.GoBack();
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.Root);
            AssertThat(menu.GetNode<Control>("RootScreen").Visible).IsTrue();

            // The main menu is the floor of the application: cancel at the root
            // must not quit, unwind past it, or leave every screen hidden.
            menu.GoBack();
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.Root);
            AssertThat(menu.ScreenDepth).IsEqual(1);
            AssertThat(menu.GetNode<Control>("RootScreen").Visible).IsTrue();
        } finally {
            Teardown(host);
            RestoreSlots(originals);
        }
    }

    [TestCase]
    public void ExactlyOneScreenIsVisibleAtATime() {
        StorySaveData[] originals = ClearAllSlots();
        MainMenu menu = Open(out Node host);
        try {
            AssertVisibleScreenCount(menu, 1);
            Press(menu, RootLayout + "StoryButton");
            AssertVisibleScreenCount(menu, 1);
            AssertThat(menu.GetNode<Control>("RootScreen").Visible).IsFalse();
            menu.GoBack();
            AssertVisibleScreenCount(menu, 1);
            AssertThat(menu.GetNode<Control>("RootScreen").Visible).IsTrue();
        } finally {
            Teardown(host);
            RestoreSlots(originals);
        }
    }

    [TestCase]
    public void AFilledSlotShowsItsDeleteButtonAndAnEmptyOneHidesIt() {
        StorySaveData[] originals = ClearAllSlots();
        if (originals == null) return;
        MainMenu menu = Open(out Node host);
        try {
            SaveManager.Instance.SaveSlots[0] = NewSyntheticSave();
            Press(menu, RootLayout + "StoryButton");

            AssertThat(menu.GetNode<Button>(SlotLayout + "SlotRow0/DeleteButton").Visible).IsTrue();
            AssertThat(menu.GetNode<Button>(SlotLayout + "SlotRow1/DeleteButton").Visible).IsFalse();

            // The filled row shows the formatted summary, the empty one the
            // empty-slot line; neither may leak a raw key.
            string filled = menu.GetNode<Button>(SlotLayout + "SlotRow0/SlotButton").Text;
            string empty = menu.GetNode<Button>(SlotLayout + "SlotRow1/SlotButton").Text;
            AssertThat(filled).IsNotEqual("save_slot_summary");
            AssertThat(empty).IsNotEqual("save_slot_empty");
            AssertThat(filled).IsNotEqual(empty);

            // A filled slot adds its delete button to the chain; an empty one does not.
            AssertChainCoversScreen(menu, "SlotScreen", 5);
        } finally {
            Teardown(host);
            RestoreSlots(originals);
        }
    }

    [TestCase]
    public void SlotDeletionOpensTheSharedConfirmModalAndCancellingChangesNothing() {
        StorySaveData[] originals = ClearAllSlots();
        if (originals == null) return;
        MainMenu menu = Open(out Node host);
        try {
            StorySaveData synthetic = NewSyntheticSave();
            SaveManager.Instance.SaveSlots[0] = synthetic;

            Press(menu, RootLayout + "StoryButton");
            Press(menu, SlotLayout + "SlotRow0/DeleteButton");

            // The modal is the shared themed Control, never a native OS window.
            AssertThat(menu.Confirmation != null).IsTrue();
            AssertThat(menu.Confirmation.IsOpen).IsTrue();
            AssertThat(typeof(Window).IsAssignableFrom(menu.Confirmation.GetType())).IsFalse();
            // Cancel exists and is the safe default, so a mashed confirm button
            // cannot delete a campaign.
            AssertThat(menu.Confirmation.CancelButton != null).IsTrue();
            // The prompt is resolved copy carrying the slot number, not a raw key.
            AssertThat(menu.Confirmation.GetNode<Label>("Center/Panel/Layout/Prompt").Text)
                .IsNotEqual("save_confirm_delete");

            menu.Confirmation.Cancel();
            AssertThat(menu.Confirmation.IsOpen).IsFalse();
            AssertThat(ReferenceEquals(SaveManager.Instance.SaveSlots[0], synthetic)).IsTrue();
        } finally {
            Teardown(host);
            RestoreSlots(originals);
        }
    }

    [TestCase]
    public void ConfirmingDeletionClearsTheSlotAndRepaintsTheRow() {
        // DeleteStorySlot reaches disk, so this runs only against a slot the
        // environment has proven empty — a developer's real save is never touched.
        StorySaveData[] originals = ClearAllSlots();
        if (originals == null) return;
        int slot = FirstSlotEmptyInMemoryAndOnDisk(originals);
        if (slot < 0) return;

        MainMenu menu = Open(out Node host);
        try {
            SaveManager.Instance.SaveSlots[slot] = NewSyntheticSave();
            Press(menu, RootLayout + "StoryButton");
            Press(menu, $"{SlotLayout}SlotRow{slot}/DeleteButton");
            menu.Confirmation.Confirm();

            AssertThat(SaveManager.Instance.SaveSlots[slot] == null).IsTrue();
            // The row repaints in place; the old flow freed and rebuilt the panel.
            AssertThat(menu.GetNode<Button>($"{SlotLayout}SlotRow{slot}/DeleteButton").Visible).IsFalse();
            AssertThat(menu.Confirmation.IsOpen).IsFalse();
        } finally {
            Teardown(host);
            RestoreSlots(originals);
        }
    }

    [TestCase]
    public void QuitOpensAConfirmationAndCancelReturnsToTheMenu() {
        // M-30 (audit 2026-08-08; design :2574): Quit Game must confirm before
        // terminating. Only the cancel path is exercisable — confirming would
        // quit the test runner — so the wiring is asserted structurally.
        MainMenu menu = Open(out Node host);
        try {
            Press(menu, RootLayout + "QuitButton");

            AssertThat(menu.QuitConfirmation != null).IsTrue();
            AssertThat(menu.QuitConfirmation.IsOpen).IsTrue();
            // The shared themed modal, never a native OS window.
            AssertThat(typeof(Window).IsAssignableFrom(menu.QuitConfirmation.GetType())).IsFalse();
            AssertThat(menu.QuitConfirmation.CancelButton != null).IsTrue();
            // Raw key stored per the A1 convention; auto-translation resolves it.
            AssertThat(menu.QuitConfirmation.GetNode<Label>("Center/Panel/Layout/Prompt").Text)
                .IsEqual("menu_quit_confirm");

            menu.QuitConfirmation.Cancel();
            AssertThat(menu.QuitConfirmation.IsOpen).IsFalse();
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.Root);

            // GoBack while the quit modal is open cancels it, not the stack.
            Press(menu, RootLayout + "QuitButton");
            menu.GoBack();
            AssertThat(menu.QuitConfirmation.IsOpen).IsFalse();
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.Root);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void AFilledSlotShowsALocalizedLocationRatherThanTheSceneBasename() {
        // Audit Low (UI): the slot row rendered "Level_02_Orleans"-style machine
        // IDs; the scene path now maps onto the *_level_title key family.
        TranslationServer.SetLocale("en");
        StorySaveData[] originals = ClearAllSlots();
        if (originals == null) return;
        MainMenu menu = Open(out Node host);
        try {
            SaveManager.Instance.SaveSlots[0] = NewSyntheticSave();
            Press(menu, RootLayout + "StoryButton");

            string text = menu.GetNode<Button>(SlotLayout + "SlotRow0/SlotButton").Text;
            AssertThat(text.Contains("Level_01_Florence"))
                .OverrideFailureMessage("Slot row still shows the raw scene basename.")
                .IsFalse();
            AssertThat(text.Contains(TranslationServer.Translate("florence_level_title").ToString()))
                .OverrideFailureMessage("Slot row must show the localized level title.")
                .IsTrue();
        } finally {
            Teardown(host);
            RestoreSlots(originals);
        }
    }

    [TestCase]
    public void CancelClosesAnOpenConfirmationRatherThanWalkingTheScreenStack() {
        StorySaveData[] originals = ClearAllSlots();
        if (originals == null) return;
        MainMenu menu = Open(out Node host);
        try {
            SaveManager.Instance.SaveSlots[1] = NewSyntheticSave();
            Press(menu, RootLayout + "StoryButton");
            Press(menu, SlotLayout + "SlotRow1/DeleteButton");
            AssertThat(menu.Confirmation.IsOpen).IsTrue();

            menu.GoBack();
            // The modal absorbs the first cancel; the screen stack does not move.
            AssertThat(menu.Confirmation.IsOpen).IsFalse();
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.SlotSelect);
        } finally {
            Teardown(host);
            RestoreSlots(originals);
        }
    }

    // ---- developer level select (temporary, 2026-08-15) ----------------------

    [TestCase]
    public void DeveloperLevelSelectWalksCharacterThenLevelThenDifficultyAndBacksOutCleanly() {
        StorySaveData[] originals = ClearAllSlots();
        MainMenu menu = Open(out Node host);
        try {
            AssertThat(menu.GetNode<Button>(RootLayout + "LevelSelectButton").Visible).IsTrue();

            Press(menu, RootLayout + "LevelSelectButton");
            AssertThat(menu.IsDeveloperLevelFlow).IsTrue();
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.CharacterSelect);
            AssertVisibleScreenCount(menu, 1);

            // Character first (user-specified order), then the level grid — not the
            // difficulty screen the campaign flow would go to from here.
            Press(menu, CharacterLayout + "Grid/CharacterButton3");
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.LevelSelect);
            // Sixteen tiles plus the Back button.
            AssertChainCoversScreen(menu, "LevelSelectScreen", 17);

            // Every tile carries a numbered, localized mission name, not a raw key.
            var grid = menu.GetNode<GridContainer>(LevelLayout + "Grid");
            for (int index = 0; index < 16; index++) {
                string text = grid.GetNode<Button>($"LevelButton{index}").Text;
                string expectedNumber = $"{index:00}";
                AssertThat(text.StartsWith($"{expectedNumber}  "))
                    .OverrideFailureMessage($"Level tile {index} is not numbered '{expectedNumber}': '{text}'")
                    .IsTrue();
                AssertThat(text.Contains("campaign_level_"))
                    .OverrideFailureMessage($"Level tile {index} shows a raw key: '{text}'")
                    .IsFalse();
            }

            // S27: the retired Level 4A tile is gone.
            AssertObject(grid.GetNodeOrNull<Button>("LevelButton16")).IsNull();

            Press(menu, LevelLayout + "Grid/LevelButton10");
            AssertThat(menu.PendingLevel).IsEqual(CampaignLevel.London);
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.DifficultySelect);
            AssertThat(menu.ScreenDepth).IsEqual(4);

            // Cancel walks back through the borrowed screens in reverse.
            menu.GoBack();
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.LevelSelect);
            menu.GoBack();
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.CharacterSelect);
            menu.GoBack();
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.Root);
            // Returning to the root drops the developer flag, so Story Mode from
            // here is the ordinary slot → character → difficulty campaign path.
            AssertThat(menu.IsDeveloperLevelFlow).IsFalse();
            Press(menu, RootLayout + "StoryButton");
            Press(menu, SlotLayout + "SlotRow0/SlotButton");
            Press(menu, CharacterLayout + "Grid/CharacterButton0");
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.DifficultySelect);
        } finally {
            Teardown(host);
            RestoreSlots(originals);
        }
    }

    /// <summary>
    /// TG-CLOSE polish: a filled slot's summary reads whole at UI scale 1.0 and 1.4 for every campaign level title (all
    /// sixteen <c>*_level_title</c> values, whatever their form) and the widest hero name. A line too long for the slab
    /// first shrinks toward the 18 px floor, then wraps to a second line if the slab holds it, else names the level by its
    /// short title (<see cref="FTT.UI.PulpScreenKit.ShortTitle"/>); menu_slots_140 cut "…THE SIEGE OF ORLÉA…". The slab's
    /// other lines read whole too, and every line stays inside the slab.
    /// </summary>
    [TestCase]
    public async Task EverySlotSummaryReadsWholeForEveryLevelTitleAndTheWidestHeroAtBothUiScales() {
        StorySaveData[] originals = ClearAllSlots();
        if (originals == null) return;
        float scale = UIPalette.AppliedUiScale;
        MainMenu menu = Open(out Node host);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var problems = new List<string>();
        const string Lines = SlotLayout + "SlotRow0/SlotButton/Slab/Row/Lines/";
        try {
            var summary = menu.GetNode<Label>(Lines + "Summary");
            Font font = summary.GetThemeFont("font");
            string hero = "", heroName = "";
            float widest = -1f;
            foreach (string id in CharacterRoster.ToArray()) {
                string name = TranslationServer.Translate($"character_{id}_name").ToString();
                float width = font.GetStringSize(name, HorizontalAlignment.Left, -1f, summary.GetThemeFontSize("font_size")).X;
                if (width > widest) { widest = width; hero = id; heroName = name; }
            }
            foreach (float ui in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(ui);
                for (int level = 0; level < 16; level++) {
                    string path = StoryManager.GetLevelScenePath((CampaignLevel)level);
                    string titleKey = LoadingScreen.LevelTitleKeyForScene(path);
                    string title = TranslationServer.Translate(titleKey).ToString();
                    SaveManager.Instance.SaveSlots[0] = new StorySaveData {
                        SelectedCharacterID = hero, CurrentLevelID = path, Difficulty = Difficulty.Normal,
                        LastSavedTimestamp = "2026-10-27T12:00:00.0000000Z"
                    };
                    Press(menu, RootLayout + "StoryButton");
                    for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                    string where = $"{ui} {titleKey}";
                    if (!summary.Text.Contains(heroName)) problems.Add($"{where}: '{summary.Text}' does not name {heroName}");
                    if (!summary.Text.Contains(title) && !summary.Text.Contains(PulpScreenKit.ShortTitle(title))) {
                        problems.Add($"{where}: '{summary.Text}' names neither the title nor its short form");
                    }
                    Rect2 slab = menu.GetNode<Control>(SlotLayout + "SlotRow0/SlotButton/Slab").GetGlobalRect();
                    foreach (string line in new[] { "Name", "Summary", "Detail", "Lowest" }) {
                        var label = menu.GetNode<Label>(Lines + line);
                        if (!label.Visible) continue;
                        string reason = WholeLabel(label);
                        if (reason != null) problems.Add($"{where}: {line} '{label.Text}' {reason}");
                        Rect2 r = label.GetGlobalRect();
                        if (r.Position.X < slab.Position.X - 1f || r.End.X > slab.End.X + 1f || r.Position.Y < slab.Position.Y - 1f || r.End.Y > slab.End.Y + 1f) {
                            problems.Add($"{where}: {line} leaves the slab ({r} in {slab})");
                        }
                    }
                    menu.GoBack();
                }
            }
        } finally {
            Teardown(host);
            RestoreSlots(originals);
            UIPalette.ApplyUiScale(scale);
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    /// <summary>Null when <paramref name="label"/> shows its whole text at 18 px or more, else why not.</summary>
    private static string WholeLabel(Label label) {
        int size = label.GetThemeFontSize("font_size");
        if (size < UIPalette.MinLabelFontSize) return $"draws at {size} px, under the 18 px floor";
        if (label.AutowrapMode != TextServer.AutowrapMode.Off) {
            return label.GetLineCount() <= label.GetVisibleLineCount() ? null
                : $"wraps to {label.GetLineCount()} lines and shows {label.GetVisibleLineCount()}";
        }
        string text = label.Uppercase ? label.Text.ToUpperInvariant() : label.Text;
        float width = label.GetThemeFont("font").GetStringSize(text, HorizontalAlignment.Left, -1f, size).X;
        return width <= label.Size.X + 0.5f ? null : $"needs {width:0} px of {label.Size.X:0} at {size} px";
    }

    /// <summary>
    /// TG-CLOSE polish: every developer level tile's label reads whole at UI scale 1.0 (today "00 CHRONAL INTEGRATI…" and
    /// "15 THE MERIDIAN FOUND…" were cut): a long name shrinks toward the compact chevron's 18 px floor first, then uses its
    /// short form, and no tile ends in an ellipsis.
    /// </summary>
    [TestCase]
    public async Task EveryDeveloperLevelTileReadsWholeAtUiScaleOne() {
        float scale = UIPalette.AppliedUiScale;
        MainMenu menu = Open(out Node host);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var problems = new List<string>();
        try {
            UIPalette.ApplyUiScale(1f);
            menu.GetNode<Control>("RootScreen").Visible = false;
            menu.GetNode<Control>("LevelSelectScreen").Visible = true;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var grid = menu.GetNode<GridContainer>(LevelLayout + "Grid");
            Rect2 screen = menu.GetViewportRect();
            for (int index = 0; index < grid.GetChildCount(); index++) {
                if (grid.GetChild(index) is not Button tile) continue;
                var decor = tile.GetNodeOrNull<PulpButtonDecor>("PulpButtonDecor");
                if (decor == null) { problems.Add(tile.Name + " has no decor"); continue; }
                if (decor.LabelIsTrimmed) {
                    problems.Add($"'{decor.DrawnText}' is cut at {decor.DrawnFontSize} px (needs {decor.DrawnTextWidth:0} at the role size, has {decor.LabelAvailableWidth:0} in a {tile.Size.X:0} px tile)");
                }
                if (decor.DrawnFontSize < UIPalette.MinLabelFontSize) problems.Add($"'{decor.DrawnText}' draws under the floor");
                Rect2 r = tile.GetGlobalRect();
                if (!screen.Encloses(r)) problems.Add($"{tile.Name} leaves the screen ({r})");
            }
        } finally {
            Teardown(host);
            UIPalette.ApplyUiScale(scale);
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    [TestCase]
    public void DeveloperLevelSelectButtonIsHiddenAndSkippedByTheFocusChainOutsideDebugBuilds() {
        MainMenu.DeveloperLevelSelectOverrideForTesting = false;
        try {
            MainMenu menu = Open(out Node host);
            try {
                AssertThat(menu.GetNode<Button>(RootLayout + "LevelSelectButton").Visible).IsFalse();
                AssertChainCoversScreen(menu, "RootScreen", 7);
            } finally {
                Teardown(host);
            }
        } finally {
            MainMenu.DeveloperLevelSelectOverrideForTesting = null;
        }
    }

    [TestCase]
    public void PreparingADirectLevelSetsTheSessionAndCampaignStateWithoutTouchingASaveSlot() {
        if (StoryManager.Instance == null || GameManager.Instance == null || SaveManager.Instance == null) return;
        StorySaveData[] originals = ClearAllSlots();
        SessionData before = GameManager.Instance.CurrentSession;
        try {
            SessionData primed = before;
            primed.ActiveSaveSlot = 1;
            GameManager.Instance.CurrentSession = primed;

            StoryManager.Instance.PrepareDirectLevel(CampaignLevel.Nassau, "tubman", Difficulty.Hard);

            SessionData session = GameManager.Instance.CurrentSession;
            AssertThat(session.SelectedCharacterID).IsEqual("tubman");
            AssertThat(session.Difficulty).IsEqual(Difficulty.Hard);
            // The dev launch clears the slot so nothing in the level can autosave
            // over a real campaign.
            AssertThat(session.ActiveSaveSlot).IsEqual(-1);
            AssertThat(StoryManager.Instance.CurrentLevel).IsEqual(CampaignLevel.Nassau);
            AssertThat(StoryManager.Instance.GetCurrentLevelPath())
                .IsEqual("res://scenes/campaign/Level_07_Nassau.tscn");
            AssertThat(StoryManager.Instance.ChronalDustCollected).IsEqual(0);
            AssertThat(StoryManager.Instance.ChronalRewindsRemaining).IsEqual(1);
            AssertThat(StoryManager.Instance.TutorialComplete).IsTrue();
            for (int slot = 0; slot < 3; slot++) {
                AssertThat(SaveManager.Instance.SaveSlots[slot] == null)
                    .OverrideFailureMessage($"Direct level launch created a save in slot {slot}")
                    .IsTrue();
            }

            StoryManager.Instance.PrepareDirectLevel(CampaignLevel.Tutorial, "einstein", Difficulty.Easy);
            AssertThat(StoryManager.Instance.TutorialComplete).IsFalse();
            AssertThat(StoryManager.Instance.ChronalRewindsRemaining).IsEqual(5);
        } finally {
            StoryManager.Instance.ResetCampaignState(before.Difficulty);
            GameManager.Instance.CurrentSession = before;
            RestoreSlots(originals);
        }
    }

    [TestCase]
    public void CalibrationDrillsSitsBetweenFighterAndSettingsAndIsAvailableWithNoStorySave() {
        // Package 11 A11 (F18 Option B). The design's root options are Story,
        // Fighter, Calibration Drills, Settings, Quit; the repo's Quick Play and
        // developer Level Select are accepted extras. Position matters because the
        // vertical order is the read order on a controller.
        StorySaveData[] originals = ClearAllSlots();
        MainMenu menu = Open(out Node host);
        try {
            var layout = menu.GetNode<VBoxContainer>("RootScreen/Center/Panel/Layout");
            int fighter = layout.GetNode<Button>("FighterButton").GetIndex();
            int drills = layout.GetNode<Button>("CalibrationDrillsButton").GetIndex();
            int settings = layout.GetNode<Button>("SettingsButton").GetIndex();
            AssertThat(drills > fighter && drills < settings)
                .OverrideFailureMessage("Calibration Drills is not between Fighter Mode and Settings")
                .IsTrue();

            // Available on a first boot: never gated on, or disabled by, a save.
            AssertThat(layout.GetNode<Button>("CalibrationDrillsButton").Disabled).IsFalse();
            AssertThat(layout.GetNode<Button>("CalibrationDrillsButton").Visible).IsTrue();
        } finally {
            Teardown(host);
            RestoreSlots(originals);
        }
    }

    [TestCase]
    public void TheRootScreenCarriesNoLanButton() {
        // V7.3: LAN de-scoped to Package 7. The code-built LanButton is gone;
        // NetworkSelectScreen and scripts/Networking/ remain for the later package.
        MainMenu menu = Open(out Node host);
        try {
            AssertThat(menu.FindChild("LanButton", recursive: true, owned: false) == null)
                .OverrideFailureMessage("The de-scoped LanButton is back on the main menu")
                .IsTrue();
        } finally {
            Teardown(host);
        }
    }

    // ---- helpers ------------------------------------------------------------

    private static string[] ScreenNames() =>
        new[] {
            "RootScreen", "SlotScreen", "CharacterScreen", "DifficultyScreen", "LevelSelectScreen",
            "ExtrasScreen", "LicensesScreen"
        };

    private static void Press(MainMenu menu, string path) =>
        menu.GetNode<Button>(path).EmitSignal(BaseButton.SignalName.Pressed);

    private static void AssertChainCoversScreen(MainMenu menu, string screen, int expected) {
        var root = menu.GetNode<Control>(screen);
        List<Control> chain = FocusChainBuilder.Collect(root);
        AssertThat(chain.Count)
            .OverrideFailureMessage($"{screen} focus chain size")
            .IsEqual(expected);
        foreach (Control control in chain) {
            AssertThat(control.FocusMode)
                .OverrideFailureMessage($"{screen}/{control.Name} is not focusable")
                .IsEqual(Control.FocusModeEnum.All);
            bool linked = !control.FocusNeighborTop.IsEmpty || !control.FocusNeighborBottom.IsEmpty;
            AssertThat(linked)
                .OverrideFailureMessage($"{screen}/{control.Name} has no focus neighbour")
                .IsTrue();
        }
    }

    private static void AssertVisibleScreenCount(MainMenu menu, int expected) {
        int visible = 0;
        foreach (string screen in ScreenNames()) {
            if (menu.GetNode<Control>(screen).Visible) visible++;
        }
        AssertThat(visible).OverrideFailureMessage("visible screen count").IsEqual(expected);
    }

    /// <summary>
    /// Empties the three in-memory slots so navigation is deterministic regardless
    /// of what the developer's <c>user://</c> directory holds. Returns the
    /// originals, or null when there is no SaveManager to work against.
    /// </summary>
    private static StorySaveData[] ClearAllSlots() {
        if (SaveManager.Instance == null) return null;
        var originals = new StorySaveData[3];
        for (int slot = 0; slot < 3; slot++) {
            originals[slot] = SaveManager.Instance.SaveSlots[slot];
            SaveManager.Instance.SaveSlots[slot] = null;
        }
        return originals;
    }

    private static void RestoreSlots(StorySaveData[] originals) {
        if (originals == null || SaveManager.Instance == null) return;
        for (int slot = 0; slot < originals.Length; slot++) {
            SaveManager.Instance.SaveSlots[slot] = originals[slot];
        }
    }

    /// <summary>
    /// A slot whose in-memory save is null and whose primary, backup, temp and
    /// corrupt candidates are all absent on disk, so the real
    /// <c>DeleteStorySlot</c> deletes nothing from <c>user://saves</c>. A slot
    /// that failed to load (null in memory) can still have a preserved
    /// <c>.corrupt</c> file, which a deletion would otherwise destroy.
    /// </summary>
    private static int FirstSlotEmptyInMemoryAndOnDisk(StorySaveData[] originals) {
        for (int slot = 0; slot < originals.Length; slot++) {
            if (originals[slot] != null) continue;
            if (SaveManager.HasAnyStorySlotFileOnDisk(slot)) continue;
            return slot;
        }
        return -1;
    }

    private static StorySaveData NewSyntheticSave() => new() {
        SelectedCharacterID = "einstein",
        CurrentLevelID = "res://scenes/campaign/Level_01_Florence.tscn",
        Difficulty = Difficulty.Normal,
        LastSavedTimestamp = "2026-08-08T00:00:00.0000000Z"
    };

    /// <summary>
    /// The session as it stood when <see cref="Open"/> ran. Every test opens one
    /// menu at a time and tears it down in a finally, so one field is enough.
    /// </summary>
    private static SessionData? _sessionAtOpen;

    /// <summary>The sticker's save source as it stood at <see cref="Open"/>; the menu reads an empty one meanwhile.</summary>
    private static StorySaveData[] _stickerSlotsAtOpen;

    private static MainMenu Open(out Node host) {
        // UR-MENU: the Continue sticker reads the saves; no case here depends on the ones on this machine.
        _stickerSlotsAtOpen = MainMenu.SaveSlotsOverrideForTesting;
        MainMenu.SaveSlotsOverrideForTesting = new StorySaveData[3];
        // The menu writes ActiveSaveSlot (an empty slot row), the calibration
        // route and the pending selection into the shared session; Teardown
        // hands the session back exactly as it was.
        _sessionAtOpen = GameManager.Instance?.CurrentSession;
        host = new Node { Name = "MainMenuHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        var menu = packed.Instantiate<MainMenu>();
        host.AddChild(menu);
        return menu;
    }

    /// <summary>
    /// Frees the host immediately rather than queueing it, so the authored scene's
    /// nodes are not counted as orphans when GdUnit4 tallies at the end of a test,
    /// then restores the session <see cref="Open"/> snapshotted.
    /// </summary>
    private static void Teardown(Node host) {
        try {
            if (GodotObject.IsInstanceValid(host)) {
                host.GetParent()?.RemoveChild(host);
                host.Free();
            }
        } finally {
            MainMenu.SaveSlotsOverrideForTesting = _stickerSlotsAtOpen;
            _stickerSlotsAtOpen = null;
            if (_sessionAtOpen.HasValue && GameManager.Instance != null) {
                GameManager.Instance.CurrentSession = _sessionAtOpen.Value;
            }
            _sessionAtOpen = null;
        }
    }
}
