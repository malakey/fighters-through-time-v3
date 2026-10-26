using FTT.UI;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>
    /// UR-PAUSE's capture targets: the Story and Fighter pause, the disconnect modal, Settings (four tabs), the Move
    /// List, the Systems Card, the confirm modal and two-option prompt, the dialogue box and log, the first-use
    /// tooltip and the boot overlay, plus the Story pause at UI scale 1.4. A pause target pauses the capture tree
    /// while it is shown; freeing the shown node hands the pause back (the base class's <c>_ExitTree</c>).
    /// </summary>
    public static partial class UiCaptureTargets {

        private const string PausePortrait = "res://assets/sprites/characters/portraits/joan_portrait.png";

        /// <summary>A warm "gameplay" backdrop the pause dims, standing in for the level behind it.</summary>
        private static Control PauseStage(Node overlay) {
            var stage = new Control { Name = "PauseStage" };
            var sky = new ColorRect { Color = new Color(0.56f, 0.30f, 0.16f) };
            sky.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            stage.AddChild(sky);
            var ground = new ColorRect { Color = new Color(0.20f, 0.12f, 0.10f) };
            ground.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
            ground.OffsetTop = -260f;
            stage.AddChild(ground);
            stage.AddChild(overlay);
            return stage;
        }

        private static T PauseFindIn<T>(Node root) where T : Node {
            if (root is T match) return match;
            for (int index = 0; index < root.GetChildCount(); index++) {
                T found = PauseFindIn<T>(root.GetChild(index));
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>The Story pause from the authored scene, open.</summary>
        [UiCaptureTarget("pause_story", SettleFrames = 12)]
        public static Node PauseStory(UiCaptureContext context) {
            Node stage = PauseStage(PauseMenu.CreateDefault());
            context.AfterReady = node => PauseFindIn<PauseMenu>(node)?.SetPaused(true);
            return stage;
        }

        /// <summary>The code-built fallback, which must read the same.</summary>
        [UiCaptureTarget("pause_story_fallback", SettleFrames = 12)]
        public static Node PauseStoryFallback(UiCaptureContext context) {
            Node stage = PauseStage(new PauseMenu { Name = "FallbackPause" });
            context.AfterReady = node => PauseFindIn<PauseMenu>(node)?.SetPaused(true);
            return stage;
        }

        /// <summary>The Story pause with the shared theme at UI scale 1.4 (restored when the capture frees it).</summary>
        [UiCaptureTarget("pause_story_ui14", SettleFrames = 12)]
        public static Node PauseStoryUiScale(UiCaptureContext context) {
            float saved = UIPalette.AppliedUiScale;
            UIPalette.ApplyUiScale(1.4f);
            Node stage = PauseStage(PauseMenu.CreateDefault());
            stage.TreeExited += () => UIPalette.ApplyUiScale(saved);
            context.AfterReady = node => PauseFindIn<PauseMenu>(node)?.SetPaused(true);
            return stage;
        }

        [UiCaptureTarget("pause_fighter", SettleFrames = 12)]
        public static Node PauseFighter(UiCaptureContext context) {
            Node stage = PauseStage(new LocalFighterPause { Name = "LocalFighterPause" });
            context.AfterReady = node => PauseFindIn<LocalFighterPause>(node)?.SetPaused(true);
            return stage;
        }

        [UiCaptureTarget("disconnect_modal", SettleFrames = 12)]
        public static Node PauseDisconnectModal(UiCaptureContext context) {
            Node stage = PauseStage(new LocalFighterPause { Name = "LocalFighterPause" });
            context.AfterReady = node => PauseFindIn<LocalFighterPause>(node)?.ForceDisconnectPause(1);
            return stage;
        }

        private static Node PauseSettingsTab(UiCaptureContext context, int tab) {
            var settings = new SettingsMenu { Name = "SettingsMenu" };
            context.AfterReady = node => {
                var menu = PauseFindIn<SettingsMenu>(node);
                menu?.Show();
                if (menu?.Tabs != null) menu.Tabs.CurrentTab = tab;
            };
            return settings;
        }

        [UiCaptureTarget("settings_audio", SettleFrames = 12)]
        public static Node PauseSettingsAudio(UiCaptureContext context) => PauseSettingsTab(context, 0);

        [UiCaptureTarget("settings_display", SettleFrames = 12)]
        public static Node PauseSettingsDisplay(UiCaptureContext context) => PauseSettingsTab(context, 1);

        [UiCaptureTarget("settings_gameplay", SettleFrames = 12)]
        public static Node PauseSettingsGameplay(UiCaptureContext context) => PauseSettingsTab(context, 2);

        [UiCaptureTarget("settings_controls", SettleFrames = 12)]
        public static Node PauseSettingsControls(UiCaptureContext context) => PauseSettingsTab(context, 3);

        /// <summary>The Controls tab at UI scale 1.4 (restored when the capture frees it).</summary>
        [UiCaptureTarget("settings_controls_ui14", SettleFrames = 12)]
        public static Node PauseSettingsControlsUi14(UiCaptureContext context) {
            float saved = UIPalette.AppliedUiScale;
            UIPalette.ApplyUiScale(1.4f);
            Node menu = PauseSettingsTab(context, 3);
            menu.TreeExited += () => UIPalette.ApplyUiScale(saved);
            return menu;
        }

        [UiCaptureTarget("movelist", SettleFrames = 12)]
        public static Node PauseMoveList(UiCaptureContext context) {
            var screen = new MoveListScreen { Name = "MoveList" };
            context.AfterReady = node => PauseFindIn<MoveListScreen>(node)?.Open("joan");
            return screen;
        }

        [UiCaptureTarget("systems_card", SettleFrames = 12)]
        public static Node PauseSystemsCard(UiCaptureContext context) {
            var screen = new SystemsCardScreen { Name = "SystemsCard" };
            context.AfterReady = node => PauseFindIn<SystemsCardScreen>(node)?.Open();
            return screen;
        }

        [UiCaptureTarget("confirm_modal", SettleFrames = 12)]
        public static Node PauseConfirmModalTarget(UiCaptureContext context) {
            ConfirmModal modal = ConfirmModal.Create("pause_quit_confirm", titleKey: "pause_quit_to_menu");
            context.AfterReady = node => PauseFindIn<ConfirmModal>(node)?.Open();
            return PauseStage(modal);
        }

        [UiCaptureTarget("two_option_prompt", SettleFrames = 12)]
        public static Node PauseTwoOptionPromptTarget(UiCaptureContext context) {
            TwoOptionPrompt prompt = TwoOptionPrompt.Create(
                "dlg_l00_wren_offer_1", "level00_choice_full", "level00_choice_skip", "speaker_wren");
            context.AfterReady = node => PauseFindIn<TwoOptionPrompt>(node)?.Open();
            return PauseStage(prompt);
        }

        [UiCaptureTarget("dialogue_box", SettleFrames = 12)]
        public static Node PauseDialogueBoxTarget(UiCaptureContext context) {
            DialogueManager dialogue = DialogueManager.CreateDefault();
            context.AfterReady = node => {
                var box = PauseFindIn<DialogueManager>(node);
                if (box == null || !box.RegisterSetFromPath("res://resources/Dialogue/level_08_dialogue.tres")) return;
                box.StartSequence("level_08.postboss");
                for (int step = 0; step < 300 && !box.Reveal.IsComplete; step++) box._Process(0.1);
                for (int step = 0; step < 10; step++) box._Process(0.1);
            };
            return PauseStage(dialogue);
        }

        [UiCaptureTarget("dialogue_log", SettleFrames = 12)]
        public static Node PauseDialogueLogTarget(UiCaptureContext context) {
            DialogueLog.Clear();
            DialogueLog.Append(new DialogueLog.Entry("demo", "Joan of Arc", "Hold the line. They will break on us, not through us.", PausePortrait, false));
            DialogueLog.Append(new DialogueLog.Entry("demo", "Commander Sarah", "The Meridian is thin here. Stay close to the anchor.", "", false));
            var log = new DialogueLogScreen { Name = "DialogueLog" };
            context.AfterReady = node => PauseFindIn<DialogueLogScreen>(node)?.Open();
            log.TreeExited += DialogueLog.Clear;
            return PauseStage(log);
        }

        [UiCaptureTarget("first_use_tooltip", SettleFrames = 12)]
        public static Node PauseFirstUseTooltip(UiCaptureContext context) {
            var presenter = new FirstUseTooltipPresenter { Name = "FirstUseTooltips" };
            context.AfterReady = node => {
                var panel = PauseFindIn<FirstUseTooltipPresenter>(node)?.GetNodeOrNull<Control>("TooltipPanel");
                if (panel == null) return;
                if (panel.GetChildCount() > 0 && panel.GetChild(0) is Label label) {
                    label.Text = "Hold Block, then press Attack, to grab a guarding foe.";
                }
                panel.Visible = true;
            };
            return PauseStage(presenter);
        }

        [UiCaptureTarget("boot_overlay", SettleFrames = 12)]
        public static Node PauseBootOverlay(UiCaptureContext context) => new BootSequenceOverlay { Name = "BootOverlay" };
    }
}
