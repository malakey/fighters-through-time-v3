using FTT.Core;
using FTT.UI;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>
    /// TG-SELECT's capture targets (UR-SELECT's names kept, re-pointed at the glass select, CARD-A): the select in its states
    /// (both choosing, Player 1 ready as <c>rec_02</c> draws it, both ready with the countdown, a mirror pick, the join
    /// prompt, the Versus CPU flow), the stage phase, the Holodeck console in both forms, the 140 % scale, and every
    /// player-slot palette.
    /// </summary>
    public static partial class UiCaptureTargets {

        private static CharacterSelectScreen BuildSelect(UiCaptureContext context, System.Action<CharacterSelectScreen> setup,
            FighterMatchOrigin origin = FighterMatchOrigin.LocalVersus, bool joinPlayerTwo = true) {
            if (GameManager.Instance != null) {
                SessionData session = GameManager.Instance.CurrentSession;
                session.FighterMatchOrigin = origin;
                session.ResumeAtStageSelect = false;
                session.HasPendingMatchSeed = false;
                GameManager.Instance.CurrentSession = session;
            }
            var screen = ResourceLoader.Load<PackedScene>("res://scenes/menus/CharacterSelect.tscn").Instantiate<CharacterSelectScreen>();
            context.AfterReady = _ => {
                // Slots driven by input sources count as joined, so neither join prompt replaces a head.
                InputManager.Instance?.SetInputSource(0, new BufferedInputSource());
                if (joinPlayerTwo) InputManager.Instance?.SetInputSource(1, new BufferedInputSource());
                else InputManager.Instance?.ClearInputSource(1);
                screen.TryMoveCursor(0, 0, 0);
                setup?.Invoke(screen);
            };
            return screen;
        }

        private static void MoveTo(CharacterSelectScreen screen, int player, int target) {
            for (int step = 0; step < 24 && screen.GetCursor(player) % CardGridRules.Columns != target % CardGridRules.Columns
                 && screen.GetCursor(player) != target; step++) {
                screen.TryMoveCursor(player, 1, 0);
            }
            for (int step = 0; step < 24 && screen.GetCursor(player) != target; step++) screen.TryMoveCursor(player, 0, 1);
        }

        private static int IndexOf(string id) => System.Math.Max(0, System.Array.IndexOf(CharacterRoster.ToArray(), id));

        /// <summary>Joan under Player 1's cursor, Tesla under Player 2's: both choosing.</summary>
        [UiCaptureTarget("select_cards", SettleFrames = 12)]
        public static Node SelectCards(UiCaptureContext context) =>
            BuildSelect(context, screen => { MoveTo(screen, 0, IndexOf("joan")); MoveTo(screen, 1, IndexOf("tesla")); });

        /// <summary>Player 1 ready on Joan, Player 2 choosing Tesla: the state <c>rec_02</c> draws.</summary>
        [UiCaptureTarget("select_p1_ready", SettleFrames = 12)]
        public static Node SelectP1Ready(UiCaptureContext context) =>
            BuildSelect(context, screen => {
                MoveTo(screen, 0, IndexOf("joan")); MoveTo(screen, 1, IndexOf("tesla"));
                screen.TryConfirm(0);
            });

        /// <summary>Both locked: both READY badges, the lock marks and the countdown.</summary>
        [UiCaptureTarget("select_ready", SettleFrames = 12)]
        public static Node SelectReady(UiCaptureContext context) =>
            BuildSelect(context, screen => {
                MoveTo(screen, 0, IndexOf("joan")); MoveTo(screen, 1, IndexOf("tesla"));
                screen.TryConfirm(0);
                screen.TryConfirm(1);
                screen.AdvanceCountdown(0.4f);
            });

        /// <summary>Both players on one tile, Player 1 locked: both badges and the white-to-P1-to-P2 edge.</summary>
        [UiCaptureTarget("select_mirror", SettleFrames = 12)]
        public static Node SelectMirror(UiCaptureContext context) =>
            BuildSelect(context, screen => { MoveTo(screen, 0, IndexOf("joan")); MoveTo(screen, 1, IndexOf("joan")); screen.TryConfirm(0); });

        /// <summary>Player 2 not joined yet: its head shows the press-to-join prompt and nothing else.</summary>
        [UiCaptureTarget("select_join", SettleFrames = 12)]
        public static Node SelectJoin(UiCaptureContext context) =>
            BuildSelect(context, screen => MoveTo(screen, 0, IndexOf("lincoln")), joinPlayerTwo: false);

        /// <summary>The Versus CPU select: Player 1 alone, Player 2's side names the CPU configured next.</summary>
        [UiCaptureTarget("select_versus_cpu", SettleFrames = 12)]
        public static Node SelectVersusCpu(UiCaptureContext context) =>
            BuildSelect(context, screen => MoveTo(screen, 0, IndexOf("shakespeare")), FighterMatchOrigin.VersusCpu, joinPlayerTwo: false);

        /// <summary>The stage and rules phase.</summary>
        [UiCaptureTarget("select_stage", SettleFrames = 12)]
        public static Node SelectStage(UiCaptureContext context) =>
            BuildSelect(context, screen => {
                MoveTo(screen, 0, IndexOf("joan")); MoveTo(screen, 1, IndexOf("tesla"));
                screen.TryConfirm(0);
                screen.TryConfirm(1);
                screen.AdvanceCountdown(4f);
            });

        /// <summary>The stage phase at the maximum UI scale (140 %).</summary>
        [UiCaptureTarget("select_stage_140", SettleFrames = 12)]
        public static Node SelectStage140(UiCaptureContext context) {
            UIPalette.ApplyUiScale(1.4f);
            return SelectStage(context);
        }

        /// <summary>The Holodeck console in its hub form.</summary>
        [UiCaptureTarget("select_holodeck", SettleFrames = 16)]
        public static Node SelectHolodeck(UiCaptureContext context) => new HolodeckConsolePanel { Name = "HolodeckConsolePanel" };

        /// <summary>The Holodeck console as the Versus CPU configuration screen (no drills entry).</summary>
        [UiCaptureTarget("select_holodeck_frontend", SettleFrames = 16)]
        public static Node SelectHolodeckFrontEnd(UiCaptureContext context) =>
            new HolodeckConsolePanel { Name = "HolodeckConsolePanel", FrontEnd = true };

        /// <summary>The Holodeck console at the maximum UI scale (140 %).</summary>
        [UiCaptureTarget("select_holodeck_140", SettleFrames = 16)]
        public static Node SelectHolodeck140(UiCaptureContext context) {
            UIPalette.ApplyUiScale(1.4f);
            return new HolodeckConsolePanel { Name = "HolodeckConsolePanel" };
        }

        /// <summary>The select at the maximum UI scale (140 %): Player 1 ready on Joan, Player 2 on Tesla.</summary>
        [UiCaptureTarget("select_140", SettleFrames = 12)]
        public static Node Select140(UiCaptureContext context) {
            UIPalette.ApplyUiScale(1.4f);
            return SelectP1Ready(context);
        }

        /// <summary>The countdown at the maximum UI scale (where the footer leaves the countdown no room under the grid).</summary>
        [UiCaptureTarget("select_ready_140", SettleFrames = 12)]
        public static Node SelectReady140(UiCaptureContext context) {
            UIPalette.ApplyUiScale(1.4f);
            return SelectReady(context);
        }

        /// <summary>Both ready on a mirror pick in the Blue / Orange player-slot palette.</summary>
        [UiCaptureTarget("select_palette_blue_orange", SettleFrames = 12)]
        public static Node SelectPaletteBlueOrange(UiCaptureContext context) => SelectInPalette(context, FTT.Combat.PlayerSlotPalette.BlueOrange);

        /// <summary>Both ready on a mirror pick in the High-Contrast player-slot palette (its black player 2 on dark chrome).</summary>
        [UiCaptureTarget("select_palette_high_contrast", SettleFrames = 12)]
        public static Node SelectPaletteHighContrast(UiCaptureContext context) => SelectInPalette(context, FTT.Combat.PlayerSlotPalette.HighContrast);

        /// <summary>Both ready on a mirror pick in the Default player-slot palette (the reference for the two above).</summary>
        [UiCaptureTarget("select_palette_default", SettleFrames = 12)]
        public static Node SelectPaletteDefault(UiCaptureContext context) => SelectInPalette(context, FTT.Combat.PlayerSlotPalette.Default);

        // The palette is a saved setting: set it for the capture and put the player's back when the runner frees the screen.
        private static Node SelectInPalette(UiCaptureContext context, FTT.Combat.PlayerSlotPalette palette) {
            GlobalSaveData data = SaveManager.Instance?.GlobalData;
            int saved = data?.PlayerSlotPalette ?? 0;
            if (data != null) data.PlayerSlotPalette = (int)palette;
            CharacterSelectScreen screen = BuildSelect(context, s => {
                MoveTo(s, 0, IndexOf("cleopatra")); MoveTo(s, 1, IndexOf("tesla"));
                s.TryConfirm(0);
                MoveTo(s, 1, IndexOf("cleopatra"));
                s.TryConfirm(1);
                s.AdvanceCountdown(0.2f);
            });
            screen.TreeExiting += () => { if (data != null) data.PlayerSlotPalette = saved; };
            return screen;
        }
    }
}
