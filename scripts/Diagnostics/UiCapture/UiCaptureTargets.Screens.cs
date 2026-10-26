using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;
using FTT.UI;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>UR-SCREENS' capture targets: results, loading, Game Over, Credits, the Resonance grid, the drills, and the small panels.</summary>
    public static partial class UiCaptureTargets {

        private static LevelResultsPanel BuildLevelResults() {
            LevelResultsPanel panel = LevelResultsPanel.CreateDefault();
            return panel;
        }

        private static void FillLevelResults(Node node) {
            if (node is LevelResultsPanel panel) {
                panel.ShowResults("campaign_level_02", 140, 60, 25, 30, 20, 40, 15, 612f, 2);
            }
        }

        /// <summary>Level results with the full six-category dust itemisation.</summary>
        [UiCaptureTarget("results_level", SettleFrames = 12)]
        public static Node ResultsLevel(UiCaptureContext context) {
            context.AfterReady = FillLevelResults;
            return BuildLevelResults();
        }

        /// <summary>Level results at the maximum UI scale 1.4 (restored when the node leaves the tree).</summary>
        [UiCaptureTarget("results_level_scale14", SettleFrames = 12)]
        public static Node ResultsLevelScale14(UiCaptureContext context) {
            float saved = UIPalette.AppliedUiScale;
            UIPalette.ApplyUiScale(1.4f);
            LevelResultsPanel panel = BuildLevelResults();
            panel.TreeExited += () => UIPalette.ApplyUiScale(saved);
            context.AfterReady = FillLevelResults;
            return panel;
        }

        /// <summary>Match results: player two wins a timed match on stocks lost.</summary>
        [UiCaptureTarget("results_match", SettleFrames = 12)]
        public static Node ResultsMatch(UiCaptureContext context) {
            GameManager gm = GameManager.Instance;
            if (gm != null) {
                SessionData session = gm.CurrentSession;
                session.SelectedCharacterID = "joan";
                session.OpponentCharacterID = "lincoln";
                session.FighterMatchOrigin = FighterMatchOrigin.LocalVersus;
                gm.CurrentSession = session;
            }
            var results = new MatchResults { Name = "MatchResults" };
            context.AfterReady = node => ((MatchResults)node).ShowResult(new FighterMatchResult(
                winnerPlayerID: 1, isTrueTie: false, completedTick: 9000, finalHash: 1L,
                playerOneStocksLost: 3, playerTwoStocksLost: 1, matchMode: (int)MatchMode.TimeLimit,
                decidedInSuddenDeath: true));
            return results;
        }

        private static Node LoadingTarget(LoadingVariant variant, string destination, UiCaptureContext context) {
            LoadingScreen screen = LoadingScreen.Create(variant);
            context.AfterReady = node => {
                SessionData session = GameManager.Instance?.CurrentSession ?? default;
                session.SelectedCharacterID = "einstein";
                session.OpponentCharacterID = "cleopatra";
                session.SelectedStageID = "";
                var stages = FighterStageCatalog.LoadDefault();
                if (stages?.FirstPlayable() is { } stage) session.SelectedStageID = stage.StageID;
                ((LoadingScreen)node).Configure(destination, session);
            };
            return screen;
        }

        [UiCaptureTarget("loading_story", SettleFrames = 12)]
        public static Node LoadingStory(UiCaptureContext context) =>
            LoadingTarget(LoadingVariant.StoryPortal, "res://scenes/campaign/Level_02_Orleans.tscn", context);

        [UiCaptureTarget("loading_fighter", SettleFrames = 12)]
        public static Node LoadingFighter(UiCaptureContext context) =>
            LoadingTarget(LoadingVariant.FighterVersus, "res://scenes/arenas/TestArena.tscn", context);

        [UiCaptureTarget("loading_plain", SettleFrames = 12)]
        public static Node LoadingPlain(UiCaptureContext context) =>
            LoadingTarget(LoadingVariant.Generic, "res://scenes/menus/MainMenu.tscn", context);

        [UiCaptureTarget("game_over", SettleFrames = 12)]
        public static Node GameOver(UiCaptureContext context) => GameOverScreen.CreateDefault();

        /// <summary>Credits scrolled 700 px in.</summary>
        [UiCaptureTarget("credits", SettleFrames = 12)]
        public static Node Credits(UiCaptureContext context) {
            CreditsController credits = CreditsController.CreateDefault();
            context.AfterReady = node => ((CreditsController)node)._Process(6.4);
            return credits;
        }

        private static Node ResonanceGridTarget(string characterID, UiCaptureContext context, bool shakespeare) {
            SaveManager save = SaveManager.Instance;
            GameManager gm = GameManager.Instance;
            if (save == null || gm == null) return new Control();
            var story = new StorySaveData { SelectedCharacterID = characterID };
            story.DepositedChronalDust[characterID] = 140;
            if (shakespeare) {
                story.UnlockedLegacyAbilities[characterID] = new List<string> { "special1" };
                ResonanceGridData grid = ResonanceProgression.LoadGrid(characterID);
                List<string> owned = ResonanceProgression.GetUnlockedNodes(story, characterID);
                owned.Add("shakespeare_quill_reach");
                owned.Add("shakespeare_wit");
                owned.Add("shakespeare_chronicle");
            } else {
                ResonanceProgression.GetUnlockedNodes(story, characterID).Add("einstein_momentum");
                ResonanceProgression.GetUnlockedNodes(story, characterID).Add("einstein_mass");
            }
            save.SaveSlots[0] = story;
            SessionData session = gm.CurrentSession;
            session.ActiveSaveSlot = 0;
            session.SelectedCharacterID = characterID;
            gm.CurrentSession = session;
            return new ResonanceGridPanel { Name = "ResonanceGrid" };
        }

        /// <summary>Shakespeare's grid: All-of edges, satisfied edges, owned (paper), available, locked and dormant chips.</summary>
        [UiCaptureTarget("resonance_grid", SettleFrames = 14)]
        public static Node ResonanceGridAllOf(UiCaptureContext context) =>
            ResonanceGridTarget("shakespeare", context, shakespeare: true);

        /// <summary>Einstein's grid: Any-of (dotted) edges, two owned nodes, dormant stars.</summary>
        [UiCaptureTarget("resonance_grid_anyof", SettleFrames = 14)]
        public static Node ResonanceGridAnyOf(UiCaptureContext context) =>
            ResonanceGridTarget("einstein", context, shakespeare: false);

        private static Node SceneTarget(string path) =>
            ResourceLoader.Load<PackedScene>(path).Instantiate();

        [UiCaptureTarget("drill_picker", SettleFrames = 12)]
        public static Node DrillPicker(UiCaptureContext context) => SceneTarget("res://scenes/ui/CalibrationDrillPicker.tscn");

        [UiCaptureTarget("drill_list", SettleFrames = 12)]
        public static Node DrillList(UiCaptureContext context) => SceneTarget("res://scenes/ui/CalibrationDrillList.tscn");

        /// <summary>The drill sandbox with its HUD panel; the result card is forced visible by a second target.</summary>
        [UiCaptureTarget("drill_hud", SettleFrames = 30)]
        public static Node DrillHud(UiCaptureContext context) => SceneTarget("res://scenes/ui/HolodeckDrill.tscn");

        [UiCaptureTarget("timeline_restart", SettleFrames = 12)]
        public static Node TimelineRestart(UiCaptureContext context) => new TimelineRestartPanel { Name = "TimelineRestartPanel" };

        [UiCaptureTarget("fighter_play_options", SettleFrames = 12)]
        public static Node FighterPlayOptions(UiCaptureContext context) => SceneTarget("res://scenes/menus/FighterPlayOptions.tscn");

        [UiCaptureTarget("network_select", SettleFrames = 12)]
        public static Node NetworkSelect(UiCaptureContext context) => SceneTarget("res://scenes/menus/NetworkSelect.tscn");
    }
}
