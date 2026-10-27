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
        private PanelContainer _shade;
        private PausePanel _panel;
        private bool _usesHostDim;

        /// <summary>UI_SPEC_GLASS 9.2 (agent choice): the modal is a 760 px glass slab; its message is at most 680 wide.</summary>
        public const float ModalPanelWidth = 760f;
        public const float MessageWidth = 680f;

        /// <summary>The two compact chevrons: at least 260 wide each (9.2), sharing the row; 58 tall (T11).</summary>
        public const float ModalButtonMinWidth = 260f;
        public const float ModalButtonHeight = 58f;

        /// <summary>The slab's column gap: the title, the message and the button row.</summary>
        public const int LayoutSeparation = 22;

        /// <summary>
        /// One dim (UI_SPEC_GLASS 9.2, the pause's child screens): a modal opened over a host that already dims the game (a
        /// pause menu) draws none of its own and sits on the host's. Default false: the modal brings its 0.70 dim.
        /// </summary>
        public bool UsesHostDim {
            get => _usesHostDim;
            set {
                _usesHostDim = value;
                if (_shade != null) _shade.Visible = !value;
            }
        }

        /// <summary>The modal's glass slab. Test surface.</summary>
        internal PausePanel Panel => _panel;

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

            _shade = PulpScreenKit.MakeScrim();
            AddChild(_shade);

            var center = new CenterContainer { Name = "Center" };
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(center);

            // TG-PAUSE (UI_SPEC_GLASS 9.2): a 760 px glass slab (the default slab padding, untagged) over the 0.70 dim, the
            // panel title, the message in body copy and a row of two compact chevrons.
            _panel = new PausePanel {
                Name = "Panel",
                ThemeTypeVariation = UIPalette.Variations.InkPanel,
                CustomMinimumSize = new Vector2(ModalPanelWidth, 0)
            };
            center.AddChild(_panel);
            PulpScreenKit.DressPanel(_panel, sheen: true, buttonInset: false);

            var layout = new VBoxContainer { Name = "Layout" };
            layout.AddThemeConstantOverride("separation", LayoutSeparation);
            _panel.AddChild(layout);

            if (!string.IsNullOrWhiteSpace(titleKey)) {
                // 9.1 item 2: a panel title inside a slab (Cinzel 900 36, upper case, ink, centred).
                var title = new Label {
                    Name = "Title",
                    Text = titleKey,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Uppercase = true,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    CustomMinimumSize = new Vector2(MessageWidth, 0)
                };
                title.ThemeTypeVariation = UIPalette.TitleLabelVariation;
                layout.AddChild(title);
            }

            // 9.2: the message in Outfit 400 26 / 1.35, ink, centred, at most 680 wide.
            _prompt = new Label {
                Name = "Prompt",
                Text = promptKey,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(MessageWidth, 0),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
                ThemeTypeVariation = UIPalette.LabelRoles.BodyLabel
            };
            layout.AddChild(_prompt);

            var buttons = new HBoxContainer { Name = "Buttons", Alignment = BoxContainer.AlignmentMode.Center };
            layout.AddChild(buttons);

            _cancelButton = ModalButton("CancelButton", cancelKey);
            _cancelButton.Pressed += Cancel;
            buttons.AddChild(_cancelButton);

            _confirmButton = ModalButton("ConfirmButton", confirmKey);
            _confirmButton.Pressed += Confirm;
            buttons.AddChild(_confirmButton);
            PulpScreenKit.SeparateFocusRow(buttons);

            // 9.1 item 1: an overlay's prompt bar sits bottom-centre; B presses the cancel button, so it carries its words.
            PulpScreenKit.AddBottomPromptBar(this, cancelKey);
        }

        /// <summary>
        /// A compact chevron of the modal's row: at least 260 wide and sharing the row with its neighbour, its label never
        /// trimmed (the UR-POLISH rule: a label too long for the line wraps and the button grows to hold it).
        /// </summary>
        private static Button ModalButton(string name, string textKey) {
            var button = new Button {
                Name = name,
                Text = textKey,
                CustomMinimumSize = new Vector2(ModalButtonMinWidth, ModalButtonHeight),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ThemeTypeVariation = UIPalette.Variations.PulpButtonCompact
            };
            // The decor re-measures the longest word whenever its badge changes width (TG-CLOSE folded the workaround that
            // toggled GrowToFit here), so the focused label never ends under the keyboard key-cap ("CALIBRATIO").
            Pulp.PulpButtonDecor.Apply(button, compact: true).GrowToFit = true;
            return button;
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
            // MOT-A row 4: the slab fades and rises (its own dim fading in with it unless it sits on a host's).
            if (_shade != null) _shade.Visible = !_usesHostDim;
            _panel?.PlayAppear(_usesHostDim ? null : _shade);
        }

        /// <summary>Hides the modal and hands focus back to its previous owner.</summary>
        public void Close() {
            if (!IsOpen) return;
            IsOpen = false;
            Visible = false;
            _panel?.StopAppear(_usesHostDim ? null : _shade);

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
