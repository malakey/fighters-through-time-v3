using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>
    /// TG-MENU's capture targets (Temporal Glass, MENU-A): the root screen (a first boot, with the Continue card, the debug
    /// rows with the card focused, the rec_01 comparison state with pad prompts, UI scale 1.4) and the six sub-screens (the
    /// slots and the character screen also at 1.4). The target names are UR-MENU's, so the pulp gallery pairs 1:1;
    /// <c>menu_cover_rec01</c>, <c>menu_slots_140</c> and <c>menu_character_140</c> are new. In-memory only: the synthetic
    /// saves never reach disk, and every seam a target sets is cleared when its menu leaves the tree.
    /// </summary>
    public static partial class UiCaptureTargets {

        private static StorySaveData MenuCaptureSave(string character, string levelScene, string stamp, Difficulty difficulty) => new() {
            SelectedCharacterID = character,
            CurrentLevelID = levelScene,
            Difficulty = difficulty,
            LastSavedTimestamp = stamp,
            LowestDifficultyUsed = difficulty
        };

        private static StorySaveData[] MenuCaptureSaves(string character = "joan") => new[] {
            MenuCaptureSave(character, "res://scenes/campaign/Level_06_Pompeii.tscn", "2026-10-20T10:00:00.0000000Z", Difficulty.Normal),
            MenuCaptureSave("einstein", "res://scenes/campaign/Level_02_Orleans.tscn", "2026-10-10T10:00:00.0000000Z", Difficulty.Easy),
            null
        };

        private static MainMenu MenuInstance(PromptDevice device = PromptDevice.Keyboard) {
            MainMenu.BootSequenceOverrideForTesting = false;
            PulpPromptGlyphs.DeviceOverrideForTesting = device;
            MainMenu menu = ResourceLoader.Load<PackedScene>("res://scenes/menus/MainMenu.tscn").Instantiate<MainMenu>();
            // The seams are static: hand them back when the capture frees the menu, so no later target inherits them.
            menu.TreeExiting += () => {
                PulpPromptGlyphs.DeviceOverrideForTesting = null;
                MainMenu.SaveSlotsOverrideForTesting = null;
                MainMenu.DeveloperLevelSelectOverrideForTesting = null;
                MainMenu.BootSequenceOverrideForTesting = null;
            };
            return menu;
        }

        private static void MenuPress(Node menu, string path) =>
            menu.GetNode<Button>(path).EmitSignal(BaseButton.SignalName.Pressed);

        private static void MenuSyntheticSlots() {
            if (SaveManager.Instance == null) return;
            StorySaveData[] saves = MenuCaptureSaves();
            for (int slot = 0; slot < 3; slot++) SaveManager.Instance.SaveSlots[slot] = saves[slot];
        }

        /// <summary>The root as a first boot shows it: no save, so no Continue card; the release rows (Quick Play, R1).</summary>
        [UiCaptureTarget("menu_cover", SettleFrames = 20)]
        public static Node MenuCover(UiCaptureContext context) {
            MainMenu.DeveloperLevelSelectOverrideForTesting = false;
            MainMenu.SaveSlotsOverrideForTesting = new StorySaveData[3];
            return MenuInstance();
        }

        /// <summary>The root with a recent save: the Continue card (Joan, Pompeii 79 AD, Normal, Chapter 6).</summary>
        [UiCaptureTarget("menu_cover_continue", SettleFrames = 20)]
        public static Node MenuCoverContinue(UiCaptureContext context) {
            MainMenu.DeveloperLevelSelectOverrideForTesting = false;
            MainMenu.SaveSlotsOverrideForTesting = MenuCaptureSaves();
            return MenuInstance();
        }

        /// <summary>The root in a debug build (the Quick Play and Level Select rows) with the Continue card focused.</summary>
        [UiCaptureTarget("menu_cover_debug", SettleFrames = 20)]
        public static Node MenuCoverDebug(UiCaptureContext context) {
            MainMenu.DeveloperLevelSelectOverrideForTesting = true;
            MainMenu.SaveSlotsOverrideForTesting = MenuCaptureSaves();
            MainMenu menu = MenuInstance();
            context.AfterReady = _ => menu.ContinueCard?.GrabFocus();
            return menu;
        }

        /// <summary>The state UI_SPEC_GLASS's rec_01 draws, for compare_capture.py: Einstein's save, the release rows, pad prompts.</summary>
        [UiCaptureTarget("menu_cover_rec01", SettleFrames = 20)]
        public static Node MenuCoverRec01(UiCaptureContext context) {
            MainMenu.DeveloperLevelSelectOverrideForTesting = false;
            MainMenu.SaveSlotsOverrideForTesting = MenuCaptureSaves("einstein");
            return MenuInstance(PromptDevice.Pad);
        }

        /// <summary>The root at the maximum UI scale (1.4) with the debug rows and the card: the fit sequence at step 4.</summary>
        [UiCaptureTarget("menu_cover_scale14", SettleFrames = 24)]
        public static Node MenuCoverScale14(UiCaptureContext context) {
            MainMenu.DeveloperLevelSelectOverrideForTesting = true;
            MainMenu.SaveSlotsOverrideForTesting = MenuCaptureSaves();
            UIPalette.ApplyUiScale(1.4f);
            return MenuInstance();
        }

        private static Node MenuSubScreen(UiCaptureContext context, System.Action<MainMenu> navigate, float uiScale = 1f) {
            MainMenu.DeveloperLevelSelectOverrideForTesting = true;
            MainMenu.SaveSlotsOverrideForTesting = new StorySaveData[3];
            MenuSyntheticSlots();
            if (!Mathf.IsEqualApprox(uiScale, 1f)) UIPalette.ApplyUiScale(uiScale);
            MainMenu menu = MenuInstance();
            context.AfterReady = _ => navigate(menu);
            return menu;
        }

        private const string MenuRootLayout = "RootScreen/Center/Panel/Layout/";

        private static void OpenSlots(MainMenu menu) => MenuPress(menu, MenuRootLayout + "StoryButton");

        [UiCaptureTarget("menu_slots", SettleFrames = 20)]
        public static Node MenuSlots(UiCaptureContext context) => MenuSubScreen(context, OpenSlots);

        [UiCaptureTarget("menu_slots_140", SettleFrames = 24)]
        public static Node MenuSlots140(UiCaptureContext context) => MenuSubScreen(context, OpenSlots, 1.4f);

        private static void OpenCharacters(MainMenu menu) {
            OpenSlots(menu);
            MenuPress(menu, "SlotScreen/Center/Panel/Layout/SlotRow2/SlotButton");
            // A tile other than the first, so the side slab shows a longer name (Shakespeare's).
            menu.GetNodeOrNull<Button>("CharacterScreen/Center/Panel/Layout/Body/Grid/CharacterButton6")?.GrabFocus();
        }

        [UiCaptureTarget("menu_character", SettleFrames = 20)]
        public static Node MenuCharacter(UiCaptureContext context) => MenuSubScreen(context, OpenCharacters);

        [UiCaptureTarget("menu_character_140", SettleFrames = 24)]
        public static Node MenuCharacter140(UiCaptureContext context) => MenuSubScreen(context, OpenCharacters, 1.4f);

        [UiCaptureTarget("menu_difficulty", SettleFrames = 20)]
        public static Node MenuDifficulty(UiCaptureContext context) =>
            MenuSubScreen(context, menu => {
                OpenSlots(menu);
                MenuPress(menu, "SlotScreen/Center/Panel/Layout/SlotRow2/SlotButton");
                MenuPress(menu, "CharacterScreen/Center/Panel/Layout/Body/Grid/CharacterButton1");
            });

        [UiCaptureTarget("menu_level_select", SettleFrames = 20)]
        public static Node MenuLevelSelect(UiCaptureContext context) =>
            MenuSubScreen(context, menu => {
                MenuPress(menu, MenuRootLayout + "LevelSelectButton");
                MenuPress(menu, "CharacterScreen/Center/Panel/Layout/Body/Grid/CharacterButton0");
            });

        [UiCaptureTarget("menu_extras", SettleFrames = 20)]
        public static Node MenuExtras(UiCaptureContext context) =>
            MenuSubScreen(context, menu => MenuPress(menu, MenuRootLayout + "ExtrasButton"));

        [UiCaptureTarget("menu_licenses", SettleFrames = 20)]
        public static Node MenuLicenses(UiCaptureContext context) =>
            MenuSubScreen(context, menu => {
                MenuPress(menu, MenuRootLayout + "ExtrasButton");
                MenuPress(menu, "ExtrasScreen/Center/Panel/Layout/LicensesButton");
            });
    }
}
