using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// GR-TRAIN (9c). The training room's settings menu: a centred glass slab, built like the Holodeck console it is entered
    /// from (a full-rect <c>CenterContainer</c>, <see cref="PulpScreenKit.DressPanel"/>, the pause panel's chevron rows,
    /// <see cref="MenuSafeAreaFit"/> clear of the prompt bar). It edits the three session-only dummy settings, resets the
    /// sandbox, and leaves for the ship.
    ///
    /// <para><b>Pause.</b> A sandbox with a live dummy cannot share its controls with a menu, so the panel pauses the tree
    /// while it is open (it runs at <see cref="Node.ProcessModeEnum.Always"/>) and hands the pause back when it closes and in
    /// <c>_ExitTree</c>, writing it only when it was the one that raised it. It never writes a setting anywhere but its own
    /// <see cref="Settings"/>; the room reads that.</para>
    /// </summary>
    public partial class TrainingRoomPanel : Control {

        /// <summary>Raised when the panel closes to resume training (Resume, Back, the pause button).</summary>
        public event Action Resumed;
        /// <summary>Raised by the Reset row; the room rebuilds its sandbox.</summary>
        public event Action ResetRequested;
        /// <summary>Raised by the Return to Time-Ship row.</summary>
        public event Action ExitRequested;
        /// <summary>Raised whenever a setting changes.</summary>
        public event Action<TrainingDummySettings> SettingsChanged;

        /// <summary>The dummy settings the panel shows. Set before the panel enters the tree to start from other values.</summary>
        public TrainingDummySettings Settings { get; set; }

        /// <summary>True while this panel holds the tree pause it raised.</summary>
        public bool HoldsPause => _pausedByPanel;

        private bool _pausedByPanel;
        private OptionButton _block;
        private OptionButton _jump;
        private OptionButton _hitstun;
        private Button _resume;
        private Button _reset;
        private Button _exit;
        private Control _tagged;

        // Test seams.
        internal OptionButton BlockSelect => _block;
        internal OptionButton JumpSelect => _jump;
        internal OptionButton HitstunSelect => _hitstun;
        internal Button ResumeButton => _resume;
        internal Button ResetButton => _reset;
        internal Button ExitButton => _exit;

        public override void _Ready() {
            ProcessMode = ProcessModeEnum.Always;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            UIPalette.ApplyTheme(this);

            var shade = new ColorRect { Name = "Shade", Color = UIPalette.PauseDim, MouseFilter = MouseFilterEnum.Stop };
            shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(shade);

            // FU-MENU: a full-rect CenterContainer keeps the slab on the screen centre however tall it grows.
            var center = new CenterContainer { Name = "Center", MouseFilter = MouseFilterEnum.Ignore };
            center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(center);

            var tagged = new PulpTaggedPanel { Name = "Tagged", TagKey = "" };
            center.AddChild(tagged);
            _tagged = tagged;
            var panel = new PanelContainer { Name = "Panel", ThemeTypeVariation = UIPalette.Variations.InkPanel };
            tagged.AddChild(panel);
            PulpScreenKit.DressPanel(panel, null, sheen: true, buttonInset: true);
            MenuSafeAreaFit.AttachClearOfPromptBar(tagged, this);

            var layout = new VBoxContainer { Name = "Layout" };
            layout.AddThemeConstantOverride("separation", (int)(HolodeckConsolePanel.RowPitch - HolodeckConsolePanel.RowControlHeight));
            panel.AddChild(layout);
            layout.AddChild(new Label {
                Name = "Title", Text = "training_room_title", Uppercase = true,
                ThemeTypeVariation = UIPalette.LabelRoles.TitleLabel, HorizontalAlignment = HorizontalAlignment.Center
            });

            TrainingDummySettings settings = Settings;
            _block = HolodeckConsolePanel.AddOptionRow(layout, "Block", "training_setting_block");
            _block.AddItem(Tr("training_option_never"), (int)TrainingBlockMode.Never);
            _block.AddItem(Tr("training_option_always"), (int)TrainingBlockMode.Always);
            _block.AddItem(Tr("training_block_after_hit"), (int)TrainingBlockMode.AfterHit);
            _block.Select((int)settings.Block);
            _block.ItemSelected += _ => OnSettingChanged();

            _jump = HolodeckConsolePanel.AddOptionRow(layout, "Jump", "training_setting_jump");
            _jump.AddItem(Tr("training_option_never"), (int)TrainingJumpMode.Never);
            _jump.AddItem(Tr("training_option_always"), (int)TrainingJumpMode.Always);
            _jump.AddItem(Tr("training_jump_timed"), (int)TrainingJumpMode.Timed);
            _jump.Select((int)settings.Jump);
            _jump.ItemSelected += _ => OnSettingChanged();

            _hitstun = HolodeckConsolePanel.AddOptionRow(layout, "Hitstun", "training_setting_hitstun");
            _hitstun.AddItem(Tr("training_hitstun_none"), (int)TrainingHitstunResponse.None);
            _hitstun.AddItem(Tr("training_hitstun_hold_block"), (int)TrainingHitstunResponse.HoldBlock);
            _hitstun.AddItem(Tr("training_hitstun_di"), (int)TrainingHitstunResponse.DirectionalInfluence);
            _hitstun.Select((int)settings.Hitstun);
            _hitstun.ItemSelected += _ => OnSettingChanged();

            // The actions: the pause panel's chevron rows (64 px, D6 width, the focused row's prompt), Resume first.
            var actions = new List<Button>();
            _resume = AddAction(layout, actions, "ResumeButton", "training_resume", Close);
            _reset = AddAction(layout, actions, "ResetButton", "training_reset", () => ResetRequested?.Invoke());
            _exit = AddAction(layout, actions, "ExitButton", "fighter_return_to_ship", () => ExitRequested?.Invoke());
            PulpButtonDecor.ApplyStackWidth(actions, HolodeckConsolePanel.ActionRowMinWidth, HolodeckConsolePanel.ActionRowCap);

            PulpScreenKit.AddOverlayPromptBar(this, ("ui_accept", "ui_prompt_select"), ("ui_cancel", "common_back"));
            PulpScreenKit.FadeIn(tagged);

            FocusChainBuilder.Apply(layout);
            _resume.GrabFocus();
        }

        private static Button AddAction(VBoxContainer layout, List<Button> actions, string name, string textKey, Action pressed) {
            var button = new Button { Name = name, Text = TranslationServer.Translate(textKey) };
            PulpScreenKit.StylePauseButton(button, HolodeckConsolePanel.ActionRowMinWidth);
            button.Pressed += () => pressed();
            layout.AddChild(button);
            actions.Add(button);
            return button;
        }

        private void OnSettingChanged() {
            Settings = new TrainingDummySettings {
                Block = (TrainingBlockMode)_block.GetSelectedId(),
                Jump = (TrainingJumpMode)_jump.GetSelectedId(),
                Hitstun = (TrainingHitstunResponse)_hitstun.GetSelectedId()
            };
            SettingsChanged?.Invoke(Settings);
        }

        /// <summary>
        /// Shows the panel and pauses the tree (only if nothing else already has), then takes focus on Resume. The settings
        /// rows are refreshed from <see cref="Settings"/>, so a room that changed them elsewhere shows the truth.
        /// </summary>
        public void Open() {
            if (_block != null) {
                _block.Select((int)Settings.Block);
                _jump.Select((int)Settings.Jump);
                _hitstun.Select((int)Settings.Hitstun);
            }
            Visible = true;
            SceneTree tree = GetTree();
            if (tree != null && !tree.Paused) {
                tree.Paused = true;
                _pausedByPanel = true;
            }
            _resume?.GrabFocus();
        }

        /// <summary>Hides the panel, hands the pause back and spends the closing press so it cannot reach the sandbox.</summary>
        public void Close() {
            bool wasOpen = Visible;
            Visible = false;
            ReleasePause();
            if (wasOpen) InputManager.Instance?.ConsumeHeldPresses();
            Resumed?.Invoke();
        }

        private void ReleasePause() {
            if (!_pausedByPanel) return;
            _pausedByPanel = false;
            SceneTree tree = GetTree();
            if (tree != null) tree.Paused = false;
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (@event == null || !Visible) return;
            if (!@event.IsActionPressed("ui_cancel") && !@event.IsActionPressed(InputManager.Actions.Pause)) return;
            GetViewport()?.SetInputAsHandled();
            Close();
        }

        public override void _ExitTree() {
            ReleasePause();
        }
    }
}
