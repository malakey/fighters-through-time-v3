using FTT.Core;
using FTT.UI;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>
    /// UR-MENU's capture targets: the pulp cover (with and without the Continue sticker, with the debug rows,
    /// at UI scale 1.4) and the six sub-screens. In-memory only: the synthetic saves never reach disk.
    /// </summary>
    public static partial class UiCaptureTargets {

        private static StorySaveData MenuCaptureSave(string character, string levelScene, string stamp, Difficulty difficulty) => new() {
            SelectedCharacterID = character,
            CurrentLevelID = levelScene,
            Difficulty = difficulty,
            LastSavedTimestamp = stamp
        };

        private static MainMenu MenuInstance() {
            MainMenu.BootSequenceOverrideForTesting = false;
            return ResourceLoader.Load<PackedScene>("res://scenes/menus/MainMenu.tscn").Instantiate<MainMenu>();
        }

        private static void MenuPress(Node menu, string path) =>
            menu.GetNode<Button>(path).EmitSignal(BaseButton.SignalName.Pressed);

        private static void MenuSyntheticSlots() {
            if (SaveManager.Instance == null) return;
            SaveManager.Instance.SaveSlots[0] = MenuCaptureSave("joan", "res://scenes/campaign/Level_06_Pompeii.tscn",
                "2026-10-20T10:00:00.0000000Z", Difficulty.Normal);
            SaveManager.Instance.SaveSlots[1] = MenuCaptureSave("einstein", "res://scenes/campaign/Level_02_Orleans.tscn",
                "2026-10-10T10:00:00.0000000Z", Difficulty.Easy);
            SaveManager.Instance.SaveSlots[2] = null;
        }

        /// <summary>The pulp cover as a first boot shows it: no save, so no Continue sticker; no debug rows.</summary>
        [UiCaptureTarget("menu_cover", SettleFrames = 20)]
        public static Node MenuCover(UiCaptureContext context) {
            MainMenu.DeveloperLevelSelectOverrideForTesting = false;
            MainMenu.SaveSlotsOverrideForTesting = new StorySaveData[3];
            return MenuInstance();
        }

        /// <summary>The cover with a recent save (Chapter 6, Pompeii).</summary>
        [UiCaptureTarget("menu_cover_continue", SettleFrames = 20)]
        public static Node MenuCoverContinue(UiCaptureContext context) {
            MainMenu.DeveloperLevelSelectOverrideForTesting = false;
            MainMenu.SaveSlotsOverrideForTesting = new[] {
                MenuCaptureSave("joan", "res://scenes/campaign/Level_06_Pompeii.tscn", "2026-10-20T10:00:00.0000000Z", Difficulty.Normal),
                MenuCaptureSave("einstein", "res://scenes/campaign/Level_02_Orleans.tscn", "2026-10-10T10:00:00.0000000Z", Difficulty.Easy),
                null
            };
            return MenuInstance();
        }

        /// <summary>The cover in a debug build: the Quick Play and Level Select rows too, with the sticker focused.</summary>
        [UiCaptureTarget("menu_cover_debug", SettleFrames = 20)]
        public static Node MenuCoverDebug(UiCaptureContext context) {
            MainMenu.DeveloperLevelSelectOverrideForTesting = true;
            MainMenu.SaveSlotsOverrideForTesting = new[] {
                MenuCaptureSave("joan", "res://scenes/campaign/Level_06_Pompeii.tscn", "2026-10-20T10:00:00.0000000Z", Difficulty.Normal),
                null, null
            };
            MainMenu menu = MenuInstance();
            context.AfterReady = _ => menu.ContinueSticker?.GrabFocus();
            return menu;
        }

        /// <summary>The cover at the maximum UI scale (1.4), pad confirm badge on the focused row.</summary>
        [UiCaptureTarget("menu_cover_scale14", SettleFrames = 24)]
        public static Node MenuCoverScale14(UiCaptureContext context) {
            MainMenu.DeveloperLevelSelectOverrideForTesting = true;
            MainMenu.SaveSlotsOverrideForTesting = new[] {
                MenuCaptureSave("joan", "res://scenes/campaign/Level_06_Pompeii.tscn", "2026-10-20T10:00:00.0000000Z", Difficulty.Normal),
                null, null
            };
            UIPalette.ApplyUiScale(1.4f);
            return MenuInstance();
        }

        private static Node MenuSubScreen(UiCaptureContext context, System.Action<MainMenu> navigate) {
            MainMenu.DeveloperLevelSelectOverrideForTesting = true;
            MainMenu.SaveSlotsOverrideForTesting = new StorySaveData[3];
            MenuSyntheticSlots();
            MainMenu menu = MenuInstance();
            context.AfterReady = _ => navigate(menu);
            return menu;
        }

        [UiCaptureTarget("menu_slots", SettleFrames = 20)]
        public static Node MenuSlots(UiCaptureContext context) =>
            MenuSubScreen(context, menu => MenuPress(menu, "RootScreen/Center/Panel/Layout/StoryButton"));

        [UiCaptureTarget("menu_character", SettleFrames = 20)]
        public static Node MenuCharacter(UiCaptureContext context) =>
            MenuSubScreen(context, menu => {
                MenuPress(menu, "RootScreen/Center/Panel/Layout/StoryButton");
                MenuPress(menu, "SlotScreen/Center/Panel/Layout/SlotRow2/SlotButton");
            });

        [UiCaptureTarget("menu_difficulty", SettleFrames = 20)]
        public static Node MenuDifficulty(UiCaptureContext context) =>
            MenuSubScreen(context, menu => {
                MenuPress(menu, "RootScreen/Center/Panel/Layout/StoryButton");
                MenuPress(menu, "SlotScreen/Center/Panel/Layout/SlotRow2/SlotButton");
                MenuPress(menu, "CharacterScreen/Center/Panel/Layout/Grid/CharacterButton1");
            });

        [UiCaptureTarget("menu_level_select", SettleFrames = 20)]
        public static Node MenuLevelSelect(UiCaptureContext context) =>
            MenuSubScreen(context, menu => {
                MenuPress(menu, "RootScreen/Center/Panel/Layout/LevelSelectButton");
                MenuPress(menu, "CharacterScreen/Center/Panel/Layout/Grid/CharacterButton0");
            });

        [UiCaptureTarget("menu_extras", SettleFrames = 20)]
        public static Node MenuExtras(UiCaptureContext context) =>
            MenuSubScreen(context, menu => MenuPress(menu, "RootScreen/Center/Panel/Layout/ExtrasButton"));

        [UiCaptureTarget("menu_licenses", SettleFrames = 20)]
        public static Node MenuLicenses(UiCaptureContext context) =>
            MenuSubScreen(context, menu => {
                MenuPress(menu, "RootScreen/Center/Panel/Layout/ExtrasButton");
                MenuPress(menu, "ExtrasScreen/Center/Panel/Layout/LicensesButton");
            });
    }
}
