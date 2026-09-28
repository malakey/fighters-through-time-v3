using System.Collections.Generic;
using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// Package 12 W5 (G15b). CPU difficulty is labelled <b>Easy / Medium / Hard</b>
    /// through CPU-specific keys — the Story difficulty's "Normal" label is not the
    /// CPU's middle band. The <see cref="CpuDifficulty"/> ordinals (and so every
    /// saved session value and the CPU band tables) are unchanged.
    /// </summary>
    public static class CpuDifficultyLabels {
        public static string Key(CpuDifficulty difficulty) => difficulty switch {
            CpuDifficulty.Easy => "cpu_difficulty_easy",
            CpuDifficulty.Hard => "cpu_difficulty_hard",
            _ => "cpu_difficulty_medium"
        };

        /// <summary>Fills a selector with the three bands, ids = enum ordinals.</summary>
        public static void Fill(Godot.OptionButton select, CpuDifficulty selected) {
            select.Clear();
            foreach (CpuDifficulty difficulty in new[] { CpuDifficulty.Easy, CpuDifficulty.Normal, CpuDifficulty.Hard }) {
                select.AddItem(Godot.TranslationServer.Translate(Key(difficulty)), (int)difficulty);
            }
            select.Select(System.Math.Clamp((int)selected, 0, 2));
        }
    }

    /// <summary>The post-match choices a results screen can offer (M26).</summary>
    public enum FighterResultsAction {
        Rematch,
        NewStage,
        ChangeFighters,
        /// <summary>Holodeck only: reopen the console panel in the hub.</summary>
        Reconfigure,
        /// <summary>Leave the flow: Main Menu, the Fighter menu, or the ship, by origin.</summary>
        Exit
    }

    /// <summary>
    /// Package 12 W5 (H05/G01, M26). The single statement of where every Fighter
    /// exit goes, keyed on <see cref="SessionData.FighterMatchOrigin"/>. The pause
    /// menu, the results screen, the select screen and the Fighter menu all route
    /// through here, so an origin's destinations cannot drift apart between
    /// surfaces. Every method takes and returns a <see cref="SessionData"/> value
    /// rather than touching <c>GameManager</c>, which is what makes the rules
    /// testable without a scene change.
    ///
    /// <list type="table">
    /// <item><term>Local Versus</term><description>pause Exit → character select;
    /// results Rematch / New Stage / Change Fighters / Main Menu (unchanged).</description></item>
    /// <item><term>Versus CPU</term><description>pause Exit and the results exit →
    /// the Fighter menu; Change Fighters → the P1-only select; New Stage → the CPU
    /// configuration panel.</description></item>
    /// <item><term>Holodeck</term><description>pause Exit and Return to Ship → the
    /// hub, in front of the console; Reconfigure reopens the console panel and
    /// replaces New Stage / Change Fighters; Rematch unchanged.</description></item>
    /// <item><term>Drill</term><description>owned by <c>CalibrationRoute</c>; never a
    /// Fighter lobby.</description></item>
    /// </list>
    /// </summary>
    public static class FighterFlowRoutes {

        public const string MainMenuScenePath = "res://scenes/menus/MainMenu.tscn";
        /// <summary>The Fighter "Play Options" menu (Local Versus / Versus CPU).</summary>
        public const string FighterMenuScenePath = "res://scenes/menus/FighterPlayOptions.tscn";
        public const string CharacterSelectScenePath = "res://scenes/menus/CharacterSelect.tscn";
        public const string HubScenePath = "res://scenes/campaign/HubWorld.tscn";

        private static readonly FighterResultsAction[] LocalActions = {
            FighterResultsAction.Rematch, FighterResultsAction.NewStage,
            FighterResultsAction.ChangeFighters, FighterResultsAction.Exit
        };

        private static readonly FighterResultsAction[] HolodeckActions = {
            FighterResultsAction.Rematch, FighterResultsAction.Reconfigure, FighterResultsAction.Exit
        };

        /// <summary>The results buttons for an origin, in display order.</summary>
        public static IReadOnlyList<FighterResultsAction> ResultsActions(FighterMatchOrigin origin) =>
            origin == FighterMatchOrigin.Holodeck ? HolodeckActions : LocalActions;

        /// <summary>Label key for a results button under an origin.</summary>
        public static string ResultsLabelKey(FighterResultsAction action, FighterMatchOrigin origin) => action switch {
            FighterResultsAction.Rematch => "fighter_rematch",
            FighterResultsAction.NewStage => "fighter_new_stage",
            FighterResultsAction.ChangeFighters => "fighter_change_fighters",
            FighterResultsAction.Reconfigure => "fighter_reconfigure",
            _ => origin switch {
                FighterMatchOrigin.Holodeck => "fighter_return_to_ship",
                FighterMatchOrigin.VersusCpu => "fighter_return_to_fighter_menu",
                _ => "fighter_main_menu"
            }
        };

        /// <summary>The Fighter menu's Local Versus entry: a two-human select.</summary>
        public static SessionData EnterLocalVersus(SessionData session) {
            session.FighterMatchOrigin = FighterMatchOrigin.LocalVersus;
            // Entering a local flow clears any lingering LAN opponent type.
            session.FighterOpponentType = FighterOpponentType.LocalHuman;
            session.ResumeAtStageSelect = false;
            session.HasPendingMatchSeed = false;
            return session;
        }

        /// <summary>The Fighter menu's Versus CPU entry: the P1-only select.</summary>
        public static SessionData EnterVersusCpu(SessionData session) {
            session.FighterMatchOrigin = FighterMatchOrigin.VersusCpu;
            session.FighterOpponentType = FighterOpponentType.Cpu;
            session.ResumeAtStageSelect = false;
            session.HasPendingMatchSeed = false;
            return session;
        }

        /// <summary>Where the pause menu's Exit lands (Drill is handled by the calibration route).</summary>
        public static string PauseExit(ref SessionData session) {
            switch (session.FighterMatchOrigin) {
                case FighterMatchOrigin.Holodeck:
                    session.ArriveAtHolodeckConsole = true;
                    return HubScenePath;
                case FighterMatchOrigin.VersusCpu:
                    return FighterMenuScenePath;
                default:
                    return CharacterSelectScenePath;
            }
        }

        /// <summary>
        /// Resolves a results button to its destination, updating the session the
        /// destination reads. Rematch resolves through the stage catalog and is not
        /// handled here.
        /// </summary>
        public static string ResultsDestination(FighterResultsAction action, ref SessionData session) {
            FighterMatchOrigin origin = session.FighterMatchOrigin;
            switch (action) {
                case FighterResultsAction.NewStage:
                    // Same characters, back to the stage phase — which for Versus
                    // CPU *is* the CPU configuration panel.
                    session.ResumeAtStageSelect = true;
                    return CharacterSelectScenePath;
                case FighterResultsAction.ChangeFighters:
                    session.ResumeAtStageSelect = false;
                    return CharacterSelectScenePath;
                case FighterResultsAction.Reconfigure:
                    session.ArriveAtHolodeckConsole = true;
                    session.ReopenHolodeckConsole = true;
                    return HubScenePath;
                default:
                    return origin switch {
                        FighterMatchOrigin.Holodeck => ReturnToShip(ref session),
                        FighterMatchOrigin.VersusCpu => FighterMenuScenePath,
                        _ => MainMenuScenePath
                    };
            }
        }

        private static string ReturnToShip(ref SessionData session) {
            session.ArriveAtHolodeckConsole = true;
            return HubScenePath;
        }

        /// <summary>
        /// The character select's Back: the hub for a Holodeck session, the Fighter
        /// menu for both front-end flows (the screen they were entered from).
        /// </summary>
        public static string SelectBack(FighterMatchOrigin origin) =>
            origin == FighterMatchOrigin.Holodeck ? HubScenePath : FighterMenuScenePath;
    }
}
