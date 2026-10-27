using System;
using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;
using FTT.UI;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>
    /// The remaining screens' capture targets (UR-SCREENS, re-pointed at the glass screens by TG-SCREENS): results,
    /// loading, Game Over, Credits, the Resonance grid, the drills, and the small panels, at UI scale 1.0 and, for the
    /// surfaces that scale, 1.4 (<c>*_140</c>). Overlays that fade in settle long enough for the fade and the sheen's light
    /// band to finish, so a capture shows the resting state.
    /// </summary>
    public static partial class UiCaptureTargets {

        /// <summary>Frames an overlay needs to finish its fade-in (0.22 s) and the light band (0.6 s) at 60 fps, plus slack.</summary>
        private const int OverlaySettle = 48;

        /// <summary>Builds <paramref name="build"/>'s node at UI scale 1.4 (restored when the node leaves the tree).</summary>
        private static Node At14(Func<Node> build) {
            float saved = UIPalette.AppliedUiScale;
            UIPalette.ApplyUiScale(1.4f);
            Node node = build();
            node.TreeExited += () => UIPalette.ApplyUiScale(saved);
            return node;
        }

        private static LevelResultsPanel BuildLevelResults() {
            LevelResultsPanel panel = LevelResultsPanel.CreateDefault();
            return panel;
        }

        private static void FillLevelResults(Node node) {
            if (node is LevelResultsPanel panel) {
                panel.ShowResults("gettysburg_level_title", 140, 60, 25, 30, 20, 40, 15, 612f, 2);
            }
        }

        /// <summary>Level results with the full six-category dust itemisation.</summary>
        [UiCaptureTarget("results_level", SettleFrames = OverlaySettle)]
        public static Node ResultsLevel(UiCaptureContext context) {
            context.AfterReady = FillLevelResults;
            return BuildLevelResults();
        }

        /// <summary>Level results at the maximum UI scale 1.4 (restored when the node leaves the tree).</summary>
        [UiCaptureTarget("results_level_scale14", SettleFrames = OverlaySettle)]
        public static Node ResultsLevelScale14(UiCaptureContext context) {
            context.AfterReady = FillLevelResults;
            return At14(BuildLevelResults);
        }

        private static Node MatchResultsTarget(UiCaptureContext context, bool draw) {
            GameManager gm = GameManager.Instance;
            if (gm != null) {
                SessionData session = gm.CurrentSession;
                session.SelectedCharacterID = "joan";
                session.OpponentCharacterID = "lincoln";
                session.FighterMatchOrigin = FighterMatchOrigin.LocalVersus;
                gm.CurrentSession = session;
            }
            var results = new MatchResults { Name = "MatchResults" };
            context.AfterReady = node => ((MatchResults)node).ShowResult(draw
                ? new FighterMatchResult(winnerPlayerID: -1, isTrueTie: true, completedTick: 9000, finalHash: 1L)
                : new FighterMatchResult(
                    winnerPlayerID: 1, isTrueTie: false, completedTick: 9000, finalHash: 1L,
                    playerOneStocksLost: 3, playerTwoStocksLost: 1, matchMode: (int)MatchMode.TimeLimit,
                    decidedInSuddenDeath: true));
            return results;
        }

        /// <summary>Match results: player two wins a timed match on stocks lost.</summary>
        [UiCaptureTarget("results_match", SettleFrames = OverlaySettle)]
        public static Node ResultsMatch(UiCaptureContext context) => MatchResultsTarget(context, draw: false);

        /// <summary>Match results: a true tie (no winner's shape and no stamp: one "Draw" line in ink).</summary>
        [UiCaptureTarget("results_match_draw", SettleFrames = OverlaySettle)]
        public static Node ResultsMatchDraw(UiCaptureContext context) => MatchResultsTarget(context, draw: true);

        [UiCaptureTarget("results_match_140", SettleFrames = OverlaySettle)]
        public static Node ResultsMatch140(UiCaptureContext context) => At14(() => MatchResultsTarget(context, draw: false));

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

        [UiCaptureTarget("loading_story_140", SettleFrames = 12)]
        public static Node LoadingStory140(UiCaptureContext context) =>
            At14(() => LoadingTarget(LoadingVariant.StoryPortal, "res://scenes/campaign/Level_02_Orleans.tscn", context));

        [UiCaptureTarget("loading_fighter", SettleFrames = 12)]
        public static Node LoadingFighter(UiCaptureContext context) =>
            LoadingTarget(LoadingVariant.FighterVersus, "res://scenes/arenas/TestArena.tscn", context);

        [UiCaptureTarget("loading_fighter_140", SettleFrames = 12)]
        public static Node LoadingFighter140(UiCaptureContext context) =>
            At14(() => LoadingTarget(LoadingVariant.FighterVersus, "res://scenes/arenas/TestArena.tscn", context));

        [UiCaptureTarget("loading_plain", SettleFrames = 12)]
        public static Node LoadingPlain(UiCaptureContext context) =>
            LoadingTarget(LoadingVariant.Generic, "res://scenes/menus/MainMenu.tscn", context);

        [UiCaptureTarget("game_over", SettleFrames = OverlaySettle)]
        public static Node GameOver(UiCaptureContext context) => GameOverScreen.CreateDefault();

        [UiCaptureTarget("game_over_140", SettleFrames = OverlaySettle)]
        public static Node GameOver140(UiCaptureContext context) => At14(() => GameOverScreen.CreateDefault());

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

        /// <summary>Shakespeare's grid: All-of edges, satisfied (gold) edges, owned, available, locked and dormant chips.</summary>
        [UiCaptureTarget("resonance_grid", SettleFrames = 14)]
        public static Node ResonanceGridAllOf(UiCaptureContext context) =>
            ResonanceGridTarget("shakespeare", context, shakespeare: true);

        /// <summary>Einstein's grid: Any-of (dotted) edges, two owned nodes, dormant stars.</summary>
        [UiCaptureTarget("resonance_grid_anyof", SettleFrames = 14)]
        public static Node ResonanceGridAnyOf(UiCaptureContext context) =>
            ResonanceGridTarget("einstein", context, shakespeare: false);

        /// <summary>Shakespeare's grid at UI scale 1.4 (the chips sized from their text, the panel fitted).</summary>
        [UiCaptureTarget("resonance_grid_140", SettleFrames = 14)]
        public static Node ResonanceGrid140(UiCaptureContext context) =>
            At14(() => ResonanceGridTarget("shakespeare", context, shakespeare: true));

        private static Node SceneTarget(string path) =>
            ResourceLoader.Load<PackedScene>(path).Instantiate();

        [UiCaptureTarget("drill_picker", SettleFrames = 12)]
        public static Node DrillPicker(UiCaptureContext context) => SceneTarget("res://scenes/ui/CalibrationDrillPicker.tscn");

        [UiCaptureTarget("drill_picker_140", SettleFrames = 12)]
        public static Node DrillPicker140(UiCaptureContext context) => At14(() => SceneTarget("res://scenes/ui/CalibrationDrillPicker.tscn"));

        [UiCaptureTarget("drill_list", SettleFrames = 12)]
        public static Node DrillList(UiCaptureContext context) => SceneTarget("res://scenes/ui/CalibrationDrillList.tscn");

        [UiCaptureTarget("drill_list_140", SettleFrames = 12)]
        public static Node DrillList140(UiCaptureContext context) => At14(() => SceneTarget("res://scenes/ui/CalibrationDrillList.tscn"));

        /// <summary>The drill list after a session's results: drill 1 passed, drill 3 failed (forgotten when it closes).</summary>
        [UiCaptureTarget("drill_list_results", SettleFrames = 12)]
        public static Node DrillListResults(UiCaptureContext context) {
            CalibrationDrillList.NoteOutcome(0, CalibrationDrillOutcome.Passed);
            CalibrationDrillList.NoteOutcome(2, CalibrationDrillOutcome.Failed);
            Node list = SceneTarget("res://scenes/ui/CalibrationDrillList.tscn");
            list.TreeExited += CalibrationDrillList.ForgetOutcomesForTest;
            return list;
        }

        /// <summary>The drill sandbox with its HUD panel.</summary>
        [UiCaptureTarget("drill_hud", SettleFrames = 30)]
        public static Node DrillHud(UiCaptureContext context) => SceneTarget("res://scenes/ui/HolodeckDrill.tscn");

        /// <summary>The drill's result card (a pass) over its .70 dim.</summary>
        [UiCaptureTarget("drill_result", SettleFrames = OverlaySettle)]
        public static Node DrillResult(UiCaptureContext context) {
            context.AfterReady = node => ((CalibrationDrillRunner)node).ShowResultCardForPreview(CalibrationDrillOutcome.Passed);
            return SceneTarget("res://scenes/ui/HolodeckDrill.tscn");
        }

        /// <summary>The drill's result card for a fail (the title in ink2, no Next).</summary>
        [UiCaptureTarget("drill_result_fail", SettleFrames = OverlaySettle)]
        public static Node DrillResultFail(UiCaptureContext context) {
            context.AfterReady = node => ((CalibrationDrillRunner)node).ShowResultCardForPreview(CalibrationDrillOutcome.Failed);
            return SceneTarget("res://scenes/ui/HolodeckDrill.tscn");
        }

        /// <summary>The pass card at UI scale 1.4 (the card's fit keeps it clear of the prompt bar).</summary>
        [UiCaptureTarget("drill_result_140", SettleFrames = OverlaySettle)]
        public static Node DrillResult140(UiCaptureContext context) {
            context.AfterReady = node => ((CalibrationDrillRunner)node).ShowResultCardForPreview(CalibrationDrillOutcome.Passed);
            return At14(() => SceneTarget("res://scenes/ui/HolodeckDrill.tscn"));
        }

        /// <summary>The credits roll at UI scale 1.4: the title wraps to two lines inside the 1100 px column (TG-SCREENS ruling 6).</summary>
        [UiCaptureTarget("credits_140", SettleFrames = 12)]
        public static Node Credits140(UiCaptureContext context) {
            context.AfterReady = node => ((CreditsController)node)._Process(6.4);
            return At14(() => CreditsController.CreateDefault());
        }

        [UiCaptureTarget("timeline_restart", SettleFrames = OverlaySettle)]
        public static Node TimelineRestart(UiCaptureContext context) => new TimelineRestartPanel { Name = "TimelineRestartPanel" };

        [UiCaptureTarget("timeline_restart_140", SettleFrames = OverlaySettle)]
        public static Node TimelineRestart140(UiCaptureContext context) => At14(() => new TimelineRestartPanel { Name = "TimelineRestartPanel" });

        [UiCaptureTarget("fighter_play_options", SettleFrames = 12)]
        public static Node FighterPlayOptions(UiCaptureContext context) => SceneTarget("res://scenes/menus/FighterPlayOptions.tscn");

        [UiCaptureTarget("fighter_play_options_140", SettleFrames = 12)]
        public static Node FighterPlayOptions140(UiCaptureContext context) => At14(() => SceneTarget("res://scenes/menus/FighterPlayOptions.tscn"));

        [UiCaptureTarget("network_select", SettleFrames = 12)]
        public static Node NetworkSelect(UiCaptureContext context) => SceneTarget("res://scenes/menus/NetworkSelect.tscn");
    }
}
