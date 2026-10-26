using FTT.Core;
using FTT.UI;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>UR-SELECT's capture targets: the trading-card select in its states, the stage phase, the Holodeck console and the 140 % scale.</summary>
    public static partial class UiCaptureTargets {

        private static CharacterSelectScreen BuildSelect(UiCaptureContext context, System.Action<CharacterSelectScreen> setup) {
            if (GameManager.Instance != null) {
                SessionData session = GameManager.Instance.CurrentSession;
                session.FighterMatchOrigin = FighterMatchOrigin.LocalVersus;
                session.ResumeAtStageSelect = false;
                session.HasPendingMatchSeed = false;
                GameManager.Instance.CurrentSession = session;
            }
            var screen = ResourceLoader.Load<PackedScene>("res://scenes/menus/CharacterSelect.tscn").Instantiate<CharacterSelectScreen>();
            context.AfterReady = _ => {
                // Both slots joined so neither join prompt hides its bubble.
                InputManager.Instance?.SetInputSource(0, new BufferedInputSource());
                InputManager.Instance?.SetInputSource(1, new BufferedInputSource());
                screen.TryMoveCursor(0, 0, 0);
                setup?.Invoke(screen);
            };
            return screen;
        }

        private static void MoveTo(CharacterSelectScreen screen, int player, int target) {
            for (int step = 0; step < 24 && screen.GetCursor(player) % CardGridRules.Columns != target % CardGridRules.Columns; step++) {
                screen.TryMoveCursor(player, 1, 0);
            }
            for (int step = 0; step < 24 && screen.GetCursor(player) != target; step++) screen.TryMoveCursor(player, 0, 1);
        }

        /// <summary>Joan under Player 1's cursor, Tesla under Player 2's: the choosing state with both bubbles.</summary>
        [UiCaptureTarget("select_cards")]
        public static Node SelectCards(UiCaptureContext context) =>
            BuildSelect(context, screen => { MoveTo(screen, 0, 1); MoveTo(screen, 1, 5); });

        /// <summary>Both locked: the READY stamps, the bubbles and the countdown.</summary>
        [UiCaptureTarget("select_ready")]
        public static Node SelectReady(UiCaptureContext context) =>
            BuildSelect(context, screen => {
                MoveTo(screen, 0, 1); MoveTo(screen, 1, 5);
                screen.TryConfirm(0);
                screen.TryConfirm(1);
                screen.AdvanceCountdown(0.4f);
            });

        /// <summary>Both players on one card, Player 1 locked: the stacked frames and both badges.</summary>
        [UiCaptureTarget("select_mirror")]
        public static Node SelectMirror(UiCaptureContext context) =>
            BuildSelect(context, screen => { MoveTo(screen, 0, 1); MoveTo(screen, 1, 1); screen.TryConfirm(0); });

        /// <summary>The stage and rules phase.</summary>
        [UiCaptureTarget("select_stage")]
        public static Node SelectStage(UiCaptureContext context) =>
            BuildSelect(context, screen => {
                MoveTo(screen, 0, 1); MoveTo(screen, 1, 5);
                screen.TryConfirm(0);
                screen.TryConfirm(1);
                screen.AdvanceCountdown(4f);
            });

        /// <summary>The Holodeck console in its hub form.</summary>
        [UiCaptureTarget("select_holodeck")]
        public static Node SelectHolodeck(UiCaptureContext context) => new HolodeckConsolePanel { Name = "HolodeckConsolePanel" };

        /// <summary>The select at the maximum UI scale (140 %).</summary>
        [UiCaptureTarget("select_140")]
        public static Node Select140(UiCaptureContext context) {
            UIPalette.ApplyUiScale(1.4f);
            return BuildSelect(context, screen => { MoveTo(screen, 0, 1); MoveTo(screen, 1, 5); screen.TryConfirm(0); });
        }
    }
}
