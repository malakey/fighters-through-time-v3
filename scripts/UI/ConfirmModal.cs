using System;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// Package 8 A1. The shared themed confirmation modal.
    ///
    /// Confirmations were previously done three incompatible ways: a native
    /// <c>ConfirmationDialog</c> (unthemed, mouse-oriented, and it steals the
    /// window), a code-built <c>ColorRect</c> with loose buttons, and an inline
    /// VBox toggled visible inside the owning panel. None of them trapped focus,
    /// so a controller player could tab off the modal onto the menu behind it and
    /// activate the very thing the modal was guarding.
    ///
    /// This one is a plain <see cref="Control"/> so it can be parented under any
    /// themed root and inherit the theme, it runs with
    /// <see cref="Node.ProcessModeEnum.Always"/> so it works from a pause menu
    /// while the tree is paused, and it restores the previously focused control on
    /// close so dismissing a confirmation returns the player exactly where they
    /// were.
    ///
    /// Cancel takes initial focus deliberately: these guard destructive actions
    /// (quit to menu, forfeit a match), and the safe option should be the one a
    /// mashed confirm button hits.
    /// </summary>
    public partial class ConfirmModal : Control {

        /// <summary>Raised when the player accepts. The owner performs the action.</summary>
        public event Action Confirmed;

        /// <summary>Raised when the player backs out, including via ui_cancel.</summary>
        public event Action Cancelled;

        private Label _prompt;
        private Button _confirmButton;
        private Button _cancelButton;
        private Control _previousFocus;

        /// <summary>True while the modal is showing and holding focus.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>The confirm button, exposed so owners can re-key or re-style it.</summary>
        public Button ConfirmButton => _confirmButton;

        /// <summary>The cancel button.</summary>
        public Button CancelButton => _cancelButton;

        /// <summary>
        /// Builds a modal from translation keys. Text is stored as the raw key and
        /// resolved by Godot's automatic control translation, so the modal follows a
        /// language change without being rebuilt.
        /// </summary>
        public static ConfirmModal Create(
            string promptKey,
            string confirmKey = "common_confirm",
            string cancelKey = "common_cancel",
            string titleKey = "") {

            var modal = new ConfirmModal { Name = "ConfirmModal" };
            modal.Build(promptKey, confirmKey, cancelKey, titleKey);
            return modal;
        }

        private void Build(string promptKey, string confirmKey, string cancelKey, string titleKey) {
            Visible = false;
            ProcessMode = ProcessModeEnum.Always;
            MouseFilter = MouseFilterEnum.Stop;
            SetAnchorsPreset(LayoutPreset.FullRect);
            UIPalette.ApplyTheme(this);

            var shade = new ColorRect {
                Name = "Shade",
                Color = UIPalette.Shade,
                MouseFilter = MouseFilterEnum.Stop
            };
            shade.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(shade);

            var center = new CenterContainer { Name = "Center" };
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(center);

            var panel = new PanelContainer { Name = "Panel" };
            panel.CustomMinimumSize = new Vector2(520, 0);
            panel.ThemeTypeVariation = "DialogueGlassPanel";
            center.AddChild(panel);

            var layout = new VBoxContainer { Name = "Layout" };
            layout.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            panel.AddChild(layout);

            if (!string.IsNullOrWhiteSpace(titleKey)) {
                var title = new Label {
                    Name = "Title",
                    Text = titleKey,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                title.ThemeTypeVariation = UIPalette.HeadingLabelVariation;
                title.AddThemeColorOverride("font_color", UIPalette.TextAccent);
                layout.AddChild(title);
            }

            _prompt = new Label {
                Name = "Prompt",
                Text = promptKey,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(440, 0)
            };
            layout.AddChild(_prompt);

            var buttons = new HBoxContainer { Name = "Buttons", Alignment = BoxContainer.AlignmentMode.Center };
            buttons.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            layout.AddChild(buttons);

            _cancelButton = new Button {
                Name = "CancelButton",
                Text = cancelKey,
                CustomMinimumSize = new Vector2(180, UIPalette.ButtonMinHeight),
                ThemeTypeVariation = "TemporalGlassButton"
            };
            _cancelButton.Pressed += Cancel;
            buttons.AddChild(_cancelButton);

            _confirmButton = new Button {
                Name = "ConfirmButton",
                Text = confirmKey,
                CustomMinimumSize = new Vector2(180, UIPalette.ButtonMinHeight),
                ThemeTypeVariation = "TemporalGlassButton"
            };
            _confirmButton.Pressed += Confirm;
            buttons.AddChild(_confirmButton);
        }

        /// <summary>Replaces the prompt copy, for reusing one modal across actions.</summary>
        public void SetPromptKey(string promptKey) {
            if (_prompt != null) _prompt.Text = promptKey ?? "";
        }

        /// <summary>Shows the modal and takes focus, remembering what had it.</summary>
        public void Open() {
            if (IsOpen) return;
            IsOpen = true;
            Visible = true;

            _previousFocus = GetViewport()?.GuiGetFocusOwner();
            FocusChainBuilder.Chain(
                new[] { _cancelButton, _confirmButton },
                FocusChainAxis.Horizontal,
                wrap: true);
            _cancelButton?.GrabFocus();
        }

        /// <summary>Hides the modal and hands focus back to its previous owner.</summary>
        public void Close() {
            if (!IsOpen) return;
            IsOpen = false;
            Visible = false;

            if (_previousFocus != null && IsInstanceValid(_previousFocus) &&
                _previousFocus.IsInsideTree() && _previousFocus.IsVisibleInTree()) {
                _previousFocus.GrabFocus();
            }
            _previousFocus = null;
        }

        /// <summary>Accepts. Public so the path is exercisable without synthetic input.</summary>
        public void Confirm() {
            if (!IsOpen) return;
            Close();
            Confirmed?.Invoke();
        }

        /// <summary>Backs out. Public for the same reason as <see cref="Confirm"/>.</summary>
        public void Cancel() {
            if (!IsOpen) return;
            Close();
            Cancelled?.Invoke();
        }

        /// <summary>
        /// The input half of the focus trap. While open, cancel backs out and every
        /// other bound action is swallowed so the surface underneath — a pause menu
        /// that would happily unpause — cannot act on the same press.
        /// </summary>
        public override void _UnhandledInput(InputEvent @event) {
            if (!IsOpen || @event == null) return;

            if (@event.IsActionPressed("ui_cancel")) {
                Cancel();
                GetViewport()?.SetInputAsHandled();
                return;
            }

            if (@event.IsActionPressed(FTT.Core.InputManager.Actions.Pause)) {
                GetViewport()?.SetInputAsHandled();
            }
        }
    }
}
