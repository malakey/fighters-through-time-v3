using System;
using System.Collections.Generic;
using FTT.Core;
using Godot;

namespace FTT.UI {

    /// <summary>The steps of the Package 12 W6 boot sequence, in order.</summary>
    public enum BootStep {
        PhotosensitivityNotice = 0,
        FirstRunSetup = 1,
        CrashPrompt = 2,
        Done = 3
    }

    /// <summary>
    /// Pure plan for the boot sequence (design Section 7 "Game Boot Sequence",
    /// steps 4–5, plus the G09 crash prompt). No engine dependency.
    /// </summary>
    public static class BootSequencePlan {
        /// <summary>The notice's display time (design: "about 3 s").</summary>
        public const float NoticeSeconds = 3f;

        /// <summary>
        /// The ordered steps for this launch. The notice plays on <b>every</b>
        /// launch; the setup cards only on a first run; the crash prompt only when
        /// the Ask setting found a previous crash.
        /// </summary>
        public static List<BootStep> Steps(bool firstRunSetupCompleted, bool crashPromptPending) {
            var steps = new List<BootStep> { BootStep.PhotosensitivityNotice };
            if (!firstRunSetupCompleted) steps.Add(BootStep.FirstRunSetup);
            if (crashPromptPending) steps.Add(BootStep.CrashPrompt);
            steps.Add(BootStep.Done);
            return steps;
        }

        /// <summary>The notice is skippable only once it has been seen at least once before.</summary>
        public static bool NoticeSkippable(bool noticeSeenBefore) => noticeSeenBefore;
    }

    /// <summary>
    /// Package 12 W6 (G06 + G09). The boot overlay the main menu raises once per
    /// process: the photosensitivity notice (every launch, skippable after the
    /// first), the first-launch setup cards (first run only), and the local
    /// crash-report prompt (Ask only). Code-built over the shared theme, at
    /// <see cref="Node.ProcessModeEnum.Always"/>; it never touches
    /// <see cref="SceneTree.Paused"/>.
    ///
    /// <para><b>Reduced Temporal Effects is already live before this runs.</b>
    /// <c>SaveManager._Ready</c> pushes the saved preset before any scene loads
    /// (C01a), and the setup card applies a change through
    /// <see cref="ComfortSettings.Apply"/> the moment it is toggled.</para>
    /// </summary>
    public partial class BootSequenceOverlay : CanvasLayer {

        /// <summary>Raised once, after the last step, just before the overlay frees itself.</summary>
        public event Action Finished;

        private List<BootStep> _steps = new();
        private int _stepIndex;
        private float _noticeElapsed;
        private bool _noticeSkippable;

        private Control _root;
        private VBoxContainer _content;
        private Button _continueButton;
        private ProgressBar _noticeProgress;

        // First-run card state.
        private CheckButton _rteToggle;
        private HSlider _uiScaleSlider;
        private ColorRect _previewSwatch;
        private Label _controllerLabel;
        private bool _listeningForControllerTest;
        private float _previewPhase;

        public BootStep CurrentStep =>
            _stepIndex >= 0 && _stepIndex < _steps.Count ? _steps[_stepIndex] : BootStep.Done;

        public bool NoticeSkippable => _noticeSkippable;

        /// <summary>The primary button of the current step. Test surface.</summary>
        internal Button ContinueButton => _continueButton;

        internal CheckButton ReducedEffectsToggle => _rteToggle;
        internal HSlider UiScaleSlider => _uiScaleSlider;
        internal Label ControllerLabel => _controllerLabel;

        public override void _Ready() {
            Layer = 110;
            ProcessMode = ProcessModeEnum.Always;
            GlobalSaveData data = SaveManager.Instance?.GlobalData;
            _noticeSkippable = BootSequencePlan.NoticeSkippable(data?.PhotosensitivityNoticeSeen == true);
            _steps = BootSequencePlan.Steps(
                data?.FirstRunSetupCompleted ?? true,
                CrashReportService.PromptPending);
            BuildShell();
            _stepIndex = 0;
            ShowStep();
            if (InputManager.Instance == null) return;
            Input.JoyConnectionChanged += OnJoyConnectionChanged;
        }

        public override void _ExitTree() {
            Input.JoyConnectionChanged -= OnJoyConnectionChanged;
        }

        private void BuildShell() {
            _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Stop };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            UIPalette.ApplyTheme(_root);
            AddChild(_root);

            var backdrop = new ColorRect { Name = "Backdrop", Color = UIPalette.NavyDeep, MouseFilter = Control.MouseFilterEnum.Ignore };
            backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(backdrop);

            var center = new CenterContainer { Name = "Center" };
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(center);

            var panel = new PanelContainer {
                Name = "Panel",
                CustomMinimumSize = new Vector2(820, 0),
                ThemeTypeVariation = DialogueTheme.GlassPanelVariation
            };
            center.AddChild(panel);

            _content = new VBoxContainer { Name = "Content" };
            _content.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            panel.AddChild(_content);
        }

        private void ClearContent() {
            Godot.Collections.Array<Node> existing = _content.GetChildren();
            using (existing.AsDisposable()) {
                foreach (Node child in existing) {
                    _content.RemoveChild(child);
                    child.Free();
                }
            }
            _continueButton = null;
            _noticeProgress = null;
            _rteToggle = null;
            _uiScaleSlider = null;
            _previewSwatch = null;
            _controllerLabel = null;
            _listeningForControllerTest = false;
        }

        private void ShowStep() {
            ClearContent();
            switch (CurrentStep) {
                case BootStep.PhotosensitivityNotice: BuildNotice(); break;
                case BootStep.FirstRunSetup: BuildFirstRunSetup(); break;
                case BootStep.CrashPrompt: BuildCrashPrompt(); break;
                default: Finish(); return;
            }
            FocusChainBuilder.Apply(_content);
        }

        // ---- Step 1: photosensitivity notice (G06) ---------------------------

        private void BuildNotice() {
            _noticeElapsed = 0f;
            _content.AddChild(MakeLabel("NoticeTitle", "boot_photosensitivity_title", UIPalette.TitleLabelVariation, accent: true));
            _content.AddChild(MakeLabel("NoticeBody", "boot_photosensitivity_body", null, wrap: true));
            _noticeProgress = new ProgressBar {
                Name = "NoticeProgress", MinValue = 0, MaxValue = 1, Value = 0, ShowPercentage = false,
                CustomMinimumSize = new Vector2(0, 8)
            };
            _content.AddChild(_noticeProgress);
            _continueButton = MakeButton("ContinueButton", "boot_continue", AdvanceFromNotice);
            // Skippable only after the first viewing (design Section 7 step 4).
            _continueButton.Disabled = !_noticeSkippable;
            _content.AddChild(_continueButton);
        }

        /// <summary>Pumps the notice timer. Public so a test can step it deterministically.</summary>
        public void AdvanceTime(float seconds) {
            if (CurrentStep == BootStep.PhotosensitivityNotice) {
                _noticeElapsed += Mathf.Max(0f, seconds);
                if (_noticeProgress != null) _noticeProgress.Value = Mathf.Clamp(_noticeElapsed / BootSequencePlan.NoticeSeconds, 0f, 1f);
                if (_noticeElapsed >= BootSequencePlan.NoticeSeconds) AdvanceFromNotice(force: true);
                return;
            }
            if (CurrentStep == BootStep.FirstRunSetup) AnimatePreview(seconds);
        }

        public override void _Process(double delta) => AdvanceTime((float)delta);

        private void AdvanceFromNotice() => AdvanceFromNotice(force: false);

        /// <summary>Leaves the notice: on the timer always, early only when skippable.</summary>
        public bool AdvanceFromNotice(bool force) {
            if (CurrentStep != BootStep.PhotosensitivityNotice) return false;
            if (!force && !_noticeSkippable) return false;
            GlobalSaveData data = SaveManager.Instance?.GlobalData;
            if (data != null && !data.PhotosensitivityNoticeSeen) {
                data.PhotosensitivityNoticeSeen = true;
                SaveManager.Instance.SaveGlobalData();
            }
            NextStep();
            return true;
        }

        // ---- Step 2: first-launch setup (G06) --------------------------------

        private void BuildFirstRunSetup() {
            GlobalSaveData data = SaveManager.Instance?.GlobalData;
            _content.AddChild(MakeLabel("SetupTitle", "first_run_title", UIPalette.TitleLabelVariation, accent: true));
            _content.AddChild(MakeLabel("SetupIntro", "first_run_intro", UIPalette.SmallLabelVariation, wrap: true));

            // Card 1: Reduced Temporal Effects, with a live preview.
            _content.AddChild(MakeLabel("ComfortHeading", "first_run_card_comfort", UIPalette.HeadingLabelVariation));
            var comfortRow = new HBoxContainer { Name = "ComfortRow" };
            comfortRow.AddThemeConstantOverride("separation", 16);
            _rteToggle = new CheckButton {
                Name = "ReducedEffectsToggle",
                Text = "settings_reduced_temporal_effects",
                ButtonPressed = ComfortSettings.ReducedTemporalEffects
            };
            _rteToggle.Toggled += on => ComfortSettings.Apply(on);
            comfortRow.AddChild(_rteToggle);
            _previewSwatch = new ColorRect {
                Name = "PreviewSwatch",
                CustomMinimumSize = new Vector2(160, 48),
                Color = UIPalette.TextAccent,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            comfortRow.AddChild(_previewSwatch);
            _content.AddChild(comfortRow);
            _content.AddChild(MakeLabel("ComfortHelp", "first_run_comfort_preview", UIPalette.SmallLabelVariation, wrap: true));

            // Card 2: UI Scale, applied live to the shared theme.
            _content.AddChild(MakeLabel("ScaleHeading", "first_run_card_ui_scale", UIPalette.HeadingLabelVariation));
            _uiScaleSlider = new HSlider {
                Name = "UiScaleSlider",
                MinValue = GlobalSaveData.MinUiScale,
                MaxValue = GlobalSaveData.MaxUiScale,
                Step = 0.05,
                Value = data?.UiScale ?? 1f,
                CustomMinimumSize = new Vector2(360, 20)
            };
            _uiScaleSlider.ValueChanged += value => UIPalette.ApplyUiScale((float)value);
            _content.AddChild(_uiScaleSlider);

            // Card 3: controller detection.
            _content.AddChild(MakeLabel("ControllerHeading", "first_run_card_controller", UIPalette.HeadingLabelVariation));
            _controllerLabel = MakeLabel("ControllerLabel", "", null, wrap: true);
            _controllerLabel.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            _content.AddChild(_controllerLabel);
            RefreshDetectedController();
            _content.AddChild(MakeButton("ControllerTestButton", "first_run_controller_test", BeginControllerTest));

            var footer = new HBoxContainer { Name = "Footer", Alignment = BoxContainer.AlignmentMode.End };
            footer.AddThemeConstantOverride("separation", 12);
            footer.AddChild(MakeButton("SkipButton", "first_run_skip", SkipFirstRunSetup));
            _continueButton = MakeButton("ContinueButton", "boot_continue", ConfirmFirstRunSetup);
            footer.AddChild(_continueButton);
            _content.AddChild(footer);
        }

        /// <summary>
        /// The "live preview of the rewind and freeze treatment": a placeholder
        /// swatch that pulses and shifts hue with the full treatment and holds a
        /// steady, smooth colour with Reduced Temporal Effects on — the same
        /// distinction C01a draws. Package 10 replaces it with the real overlay.
        /// </summary>
        private void AnimatePreview(float seconds) {
            if (_previewSwatch == null) return;
            _previewPhase += seconds;
            if (ComfortSettings.ReducedTemporalEffects) {
                _previewSwatch.Color = UIPalette.TextAccent;
                return;
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(_previewPhase * 9f);
            _previewSwatch.Color = UIPalette.TextAccent.Lerp(UIPalette.Gold, pulse);
        }

        private void RefreshDetectedController() {
            if (_controllerLabel == null) return;
            Godot.Collections.Array<int> pads = Input.GetConnectedJoypads();
            string device;
            using (pads.AsDisposable()) {
                device = pads.Count > 0 ? Input.GetJoyName(pads[0]) : Tr("first_run_controller_keyboard");
            }
            _controllerLabel.Text = string.Format(Tr("first_run_controller_detected"), device);
        }

        private void OnJoyConnectionChanged(long device, bool connected) => RefreshDetectedController();

        private void BeginControllerTest() {
            _listeningForControllerTest = true;
            if (_controllerLabel != null) _controllerLabel.Text = Tr("first_run_controller_press");
            Godot.Collections.Array<int> pads = Input.GetConnectedJoypads();
            using (pads.AsDisposable()) {
                // A short, gentle rumble through the existing haptic settings path.
                if (pads.Count > 0 && SaveManager.Instance?.GlobalData?.HapticsEnabled != false) {
                    Input.StartJoyVibration(pads[0], 0.3f, 0.3f, 0.25f);
                }
            }
        }

        public override void _Input(InputEvent @event) {
            if (!_listeningForControllerTest || @event == null || !@event.IsPressed() || @event.IsEcho()) return;
            string heard = @event switch {
                InputEventJoypadButton button => $"{Input.GetJoyName(button.Device)} — {button.ButtonIndex}",
                InputEventKey key => OS.GetKeycodeString(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode),
                _ => ""
            };
            if (heard.Length == 0) return;
            _listeningForControllerTest = false;
            if (_controllerLabel != null) _controllerLabel.Text = string.Format(Tr("first_run_controller_heard"), heard);
            GetViewport()?.SetInputAsHandled();
        }

        /// <summary>Keeps the chosen values (each is changeable later in Settings).</summary>
        public void ConfirmFirstRunSetup() {
            GlobalSaveData data = SaveManager.Instance?.GlobalData;
            if (data != null) {
                data.ReducedTemporalEffects = _rteToggle?.ButtonPressed ?? data.ReducedTemporalEffects;
                data.UiScale = (float)(_uiScaleSlider?.Value ?? data.UiScale);
                data.ApplyComfortSettings();
                UIPalette.ApplyUiScale(data.UiScale);
            }
            CompleteFirstRun();
        }

        /// <summary>Design: "Skipping keeps defaults."</summary>
        public void SkipFirstRunSetup() {
            GlobalSaveData data = SaveManager.Instance?.GlobalData;
            var defaults = new GlobalSaveData();
            if (data != null) {
                data.ReducedTemporalEffects = defaults.ReducedTemporalEffects;
                data.UiScale = defaults.UiScale;
                data.ApplyComfortSettings();
            } else {
                ComfortSettings.Apply(defaults.ReducedTemporalEffects);
            }
            UIPalette.ApplyUiScale(defaults.UiScale);
            CompleteFirstRun();
        }

        private void CompleteFirstRun() {
            if (CurrentStep != BootStep.FirstRunSetup) return;
            GlobalSaveData data = SaveManager.Instance?.GlobalData;
            if (data != null) {
                data.FirstRunSetupCompleted = true;
                SaveManager.Instance.SaveGlobalData();
            }
            NextStep();
        }

        // ---- Step 3: the local crash-report prompt (G09, D9(a)) --------------

        private void BuildCrashPrompt() {
            _content.AddChild(MakeLabel("CrashTitle", "crash_prompt_title", UIPalette.TitleLabelVariation, accent: true));
            _content.AddChild(MakeLabel("CrashBody", "crash_prompt_body", null, wrap: true));
            var row = new HBoxContainer { Name = "Buttons", Alignment = BoxContainer.AlignmentMode.End };
            row.AddThemeConstantOverride("separation", 12);
            row.AddChild(MakeButton("NeverButton", "crash_prompt_never", () => AnswerCrashPrompt(openFolder: false, never: true)));
            row.AddChild(MakeButton("DismissButton", "crash_prompt_dismiss", () => AnswerCrashPrompt(openFolder: false, never: false)));
            _continueButton = MakeButton("OpenLogsButton", "crash_prompt_open_logs", () => AnswerCrashPrompt(openFolder: true, never: false));
            row.AddChild(_continueButton);
            _content.AddChild(row);
        }

        /// <summary>
        /// Answers the prompt. Opening the folder is the only action — there is no
        /// send (D9(a)). "Never ask" switches the setting to Never.
        /// </summary>
        public void AnswerCrashPrompt(bool openFolder, bool never) {
            if (CurrentStep != BootStep.CrashPrompt) return;
            if (openFolder) CrashReportService.OpenLogsFolder();
            if (never && SaveManager.Instance?.GlobalData != null) {
                SaveManager.Instance.GlobalData.CrashReports = CrashReportMode.Never;
                SaveManager.Instance.SaveGlobalData();
            }
            CrashReportService.ConsumePrompt();
            NextStep();
        }

        // ---- Flow ------------------------------------------------------------

        private void NextStep() {
            _stepIndex++;
            ShowStep();
        }

        private void Finish() {
            _stepIndex = _steps.Count;
            Finished?.Invoke();
            QueueFree();
        }

        public override void _UnhandledInput(InputEvent @event) {
            // The overlay owns the screen while it is up: nothing leaks to the menu.
            if (@event != null && (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed(InputManager.Actions.Pause))) {
                GetViewport()?.SetInputAsHandled();
            }
        }

        // ---- Widgets ---------------------------------------------------------

        private static Label MakeLabel(string name, string key, string variation, bool wrap = false, bool accent = false) {
            var label = new Label {
                Name = name,
                Text = key,
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(760, 0)
            };
            if (!string.IsNullOrEmpty(variation)) label.ThemeTypeVariation = variation;
            if (wrap) label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (accent) label.AddThemeColorOverride("font_color", UIPalette.TextAccent);
            return label;
        }

        private static Button MakeButton(string name, string key, Action handler) {
            var button = new Button {
                Name = name,
                Text = key,
                CustomMinimumSize = new Vector2(UIPalette.ButtonMinWidth, UIPalette.ButtonMinHeight),
                ThemeTypeVariation = "TemporalGlassButton"
            };
            button.Pressed += handler;
            return button;
        }
    }
}
