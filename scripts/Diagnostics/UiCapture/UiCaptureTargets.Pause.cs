using FTT.Core;
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

        /// <summary>
        /// TG-PAUSE: frames to wait before and after a target opens its surface. The pause dim's 0.16 s ramp and the panel's
        /// 0.22 s fade-rise both end well inside it, so a capture shows the resting layout, not the motion.
        /// </summary>
        private const int PauseSettle = 30;

        /// <summary>Builds <paramref name="build"/> with the shared theme at UI scale 1.4 (restored when the capture frees it).</summary>
        private static Node PauseAtUi14(UiCaptureContext context, System.Func<UiCaptureContext, Node> build) {
            float saved = UIPalette.AppliedUiScale;
            UIPalette.ApplyUiScale(1.4f);
            // The saved setting too, as in the game (the first-use tooltip reads the HUD's scale for its free band); the
            // runner puts the setting back around every target (TG-CLOSE).
            FTT.Core.GlobalSaveData global = FTT.Core.SaveManager.Instance?.GlobalData;
            float savedSetting = global?.UiScale ?? 1f;
            if (global != null) global.UiScale = 1.4f;
            Node node = build(context);
            node.TreeExited += () => {
                UIPalette.ApplyUiScale(saved);
                if (global != null) global.UiScale = savedSetting;
            };
            return node;
        }

        /// <summary>The Story pause from the authored scene, open.</summary>
        [UiCaptureTarget("pause_story", SettleFrames = PauseSettle)]
        public static Node PauseStory(UiCaptureContext context) {
            Node stage = PauseStage(PauseMenu.CreateDefault());
            context.AfterReady = node => PauseFindIn<PauseMenu>(node)?.SetPaused(true);
            return stage;
        }

        /// <summary>The code-built fallback, which must read the same.</summary>
        [UiCaptureTarget("pause_story_fallback", SettleFrames = PauseSettle)]
        public static Node PauseStoryFallback(UiCaptureContext context) {
            Node stage = PauseStage(new PauseMenu { Name = "FallbackPause" });
            context.AfterReady = node => PauseFindIn<PauseMenu>(node)?.SetPaused(true);
            return stage;
        }

        /// <summary>The Story pause with the shared theme at UI scale 1.4.</summary>
        [UiCaptureTarget("pause_story_ui14", SettleFrames = PauseSettle)]
        public static Node PauseStoryUiScale(UiCaptureContext context) => PauseAtUi14(context, PauseStory);

        [UiCaptureTarget("pause_fighter", SettleFrames = PauseSettle)]
        public static Node PauseFighter(UiCaptureContext context) {
            Node stage = PauseStage(new LocalFighterPause { Name = "LocalFighterPause" });
            context.AfterReady = node => PauseFindIn<LocalFighterPause>(node)?.SetPaused(true);
            return stage;
        }

        [UiCaptureTarget("pause_fighter_ui14", SettleFrames = PauseSettle)]
        public static Node PauseFighterUi14(UiCaptureContext context) => PauseAtUi14(context, PauseFighter);

        [UiCaptureTarget("disconnect_modal", SettleFrames = PauseSettle)]
        public static Node PauseDisconnectModal(UiCaptureContext context) {
            Node stage = PauseStage(new LocalFighterPause { Name = "LocalFighterPause" });
            context.AfterReady = node => PauseFindIn<LocalFighterPause>(node)?.ForceDisconnectPause(1);
            return stage;
        }

        [UiCaptureTarget("disconnect_modal_ui14", SettleFrames = PauseSettle)]
        public static Node PauseDisconnectModalUi14(UiCaptureContext context) => PauseAtUi14(context, PauseDisconnectModal);

        private static Node PauseSettingsTab(UiCaptureContext context, int tab) {
            var settings = new SettingsMenu { Name = "SettingsMenu" };
            context.AfterReady = node => {
                var menu = PauseFindIn<SettingsMenu>(node);
                menu?.Show();
                if (menu?.Tabs != null) menu.Tabs.CurrentTab = tab;
            };
            return PauseStage(settings);
        }

        [UiCaptureTarget("settings_audio", SettleFrames = PauseSettle)]
        public static Node PauseSettingsAudio(UiCaptureContext context) => PauseSettingsTab(context, 0);

        [UiCaptureTarget("settings_display", SettleFrames = PauseSettle)]
        public static Node PauseSettingsDisplay(UiCaptureContext context) => PauseSettingsTab(context, 1);

        [UiCaptureTarget("settings_gameplay", SettleFrames = PauseSettle)]
        public static Node PauseSettingsGameplay(UiCaptureContext context) => PauseSettingsTab(context, 2);

        [UiCaptureTarget("settings_controls", SettleFrames = PauseSettle)]
        public static Node PauseSettingsControls(UiCaptureContext context) => PauseSettingsTab(context, 3);

        [UiCaptureTarget("settings_audio_ui14", SettleFrames = PauseSettle)]
        public static Node PauseSettingsAudioUi14(UiCaptureContext context) => PauseAtUi14(context, PauseSettingsAudio);

        [UiCaptureTarget("settings_display_ui14", SettleFrames = PauseSettle)]
        public static Node PauseSettingsDisplayUi14(UiCaptureContext context) => PauseAtUi14(context, PauseSettingsDisplay);

        [UiCaptureTarget("settings_gameplay_ui14", SettleFrames = PauseSettle)]
        public static Node PauseSettingsGameplayUi14(UiCaptureContext context) => PauseAtUi14(context, PauseSettingsGameplay);

        /// <summary>The Controls tab at UI scale 1.4.</summary>
        [UiCaptureTarget("settings_controls_ui14", SettleFrames = PauseSettle)]
        public static Node PauseSettingsControlsUi14(UiCaptureContext context) => PauseAtUi14(context, PauseSettingsControls);

        [UiCaptureTarget("movelist", SettleFrames = PauseSettle)]
        public static Node PauseMoveList(UiCaptureContext context) {
            var screen = new MoveListScreen { Name = "MoveList" };
            context.AfterReady = node => PauseFindIn<MoveListScreen>(node)?.Open("joan");
            return PauseStage(screen);
        }

        [UiCaptureTarget("movelist_ui14", SettleFrames = PauseSettle)]
        public static Node PauseMoveListUi14(UiCaptureContext context) => PauseAtUi14(context, PauseMoveList);

        [UiCaptureTarget("systems_card", SettleFrames = PauseSettle)]
        public static Node PauseSystemsCard(UiCaptureContext context) {
            var screen = new SystemsCardScreen { Name = "SystemsCard" };
            context.AfterReady = node => PauseFindIn<SystemsCardScreen>(node)?.Open();
            return PauseStage(screen);
        }

        [UiCaptureTarget("systems_card_ui14", SettleFrames = PauseSettle)]
        public static Node PauseSystemsCardUi14(UiCaptureContext context) => PauseAtUi14(context, PauseSystemsCard);

        [UiCaptureTarget("confirm_modal", SettleFrames = PauseSettle)]
        public static Node PauseConfirmModalTarget(UiCaptureContext context) {
            ConfirmModal modal = ConfirmModal.Create("pause_quit_confirm", titleKey: "pause_quit_to_menu");
            context.AfterReady = node => PauseFindIn<ConfirmModal>(node)?.Open();
            return PauseStage(modal);
        }

        [UiCaptureTarget("confirm_modal_ui14", SettleFrames = PauseSettle)]
        public static Node PauseConfirmModalUi14(UiCaptureContext context) => PauseAtUi14(context, PauseConfirmModalTarget);

        [UiCaptureTarget("two_option_prompt", SettleFrames = PauseSettle)]
        public static Node PauseTwoOptionPromptTarget(UiCaptureContext context) {
            TwoOptionPrompt prompt = TwoOptionPrompt.Create(
                "dlg_l00_wren_offer_1", "level00_choice_full", "level00_choice_skip", "speaker_wren");
            context.AfterReady = node => PauseFindIn<TwoOptionPrompt>(node)?.Open();
            return PauseStage(prompt);
        }

        [UiCaptureTarget("two_option_prompt_ui14", SettleFrames = PauseSettle)]
        public static Node PauseTwoOptionPromptUi14(UiCaptureContext context) => PauseAtUi14(context, PauseTwoOptionPromptTarget);

        [UiCaptureTarget("dialogue_box", SettleFrames = PauseSettle)]
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

        [UiCaptureTarget("dialogue_box_ui14", SettleFrames = PauseSettle)]
        public static Node PauseDialogueBoxUi14(UiCaptureContext context) => PauseAtUi14(context, PauseDialogueBoxTarget);

        [UiCaptureTarget("dialogue_log", SettleFrames = PauseSettle)]
        public static Node PauseDialogueLogTarget(UiCaptureContext context) {
            DialogueLog.Clear();
            DialogueLog.Append(new DialogueLog.Entry("demo", "Joan of Arc", "Hold the line. They will break on us, not through us.", PausePortrait, false));
            DialogueLog.Append(new DialogueLog.Entry("demo", "Commander Sarah", "The Meridian is thin here. Stay close to the anchor.", "", false));
            var log = new DialogueLogScreen { Name = "DialogueLog" };
            context.AfterReady = node => PauseFindIn<DialogueLogScreen>(node)?.Open();
            log.TreeExited += DialogueLog.Clear;
            return PauseStage(log);
        }

        [UiCaptureTarget("dialogue_log_ui14", SettleFrames = PauseSettle)]
        public static Node PauseDialogueLogUi14(UiCaptureContext context) => PauseAtUi14(context, PauseDialogueLogTarget);

        [UiCaptureTarget("first_use_tooltip", SettleFrames = PauseSettle)]
        public static Node PauseFirstUseTooltip(UiCaptureContext context) {
            var presenter = new FirstUseTooltipPresenter { Name = "FirstUseTooltips" };
            // The real fit and appear path (TG-PAUSE): a real tooltip key, so the line is the shipped copy.
            context.AfterReady = node => PauseFindIn<FirstUseTooltipPresenter>(node)?.PresentForPreview(FirstUseTooltips.KeyFor(FirstUseTooltips.Grab));
            return PauseStage(presenter);
        }

        [UiCaptureTarget("first_use_tooltip_ui14", SettleFrames = PauseSettle)]
        public static Node PauseFirstUseTooltipUi14(UiCaptureContext context) => PauseAtUi14(context, PauseFirstUseTooltip);

        [UiCaptureTarget("boot_overlay", SettleFrames = PauseSettle)]
        public static Node PauseBootOverlay(UiCaptureContext context) => new BootSequenceOverlay { Name = "BootOverlay" };

        [UiCaptureTarget("boot_overlay_ui14", SettleFrames = PauseSettle)]
        public static Node PauseBootOverlayUi14(UiCaptureContext context) => PauseAtUi14(context, PauseBootOverlay);

        /// <summary>The first-run cards (shown on a fresh install only), through the overlay's preview seam.</summary>
        [UiCaptureTarget("boot_first_run", SettleFrames = PauseSettle)]
        public static Node PauseBootFirstRun(UiCaptureContext context) {
            var overlay = new BootSequenceOverlay { Name = "BootOverlay" };
            context.AfterReady = node => (node as BootSequenceOverlay)?.ShowStepForPreview(BootStep.FirstRunSetup);
            return overlay;
        }

        [UiCaptureTarget("boot_first_run_ui14", SettleFrames = PauseSettle)]
        public static Node PauseBootFirstRunUi14(UiCaptureContext context) => PauseAtUi14(context, PauseBootFirstRun);

        /// <summary>The local crash-report prompt (shown only after a crash), through the overlay's preview seam.</summary>
        [UiCaptureTarget("boot_crash_prompt", SettleFrames = PauseSettle)]
        public static Node PauseBootCrashPrompt(UiCaptureContext context) {
            var overlay = new BootSequenceOverlay { Name = "BootOverlay" };
            context.AfterReady = node => (node as BootSequenceOverlay)?.ShowStepForPreview(BootStep.CrashPrompt);
            return overlay;
        }
    }
}
