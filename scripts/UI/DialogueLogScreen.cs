using System;
using System.Collections.Generic;
using FTT.Core;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// Package 12 W6 (G10). The scrollable Dialogue Log overlay: the session's last
    /// 100 lines (<see cref="DialogueLog"/>), newest at the bottom, each with its
    /// speaker, portrait and text.
    ///
    /// <para>Follows the Move List overlay pattern: a CanvasLayer at
    /// <see cref="Node.ProcessModeEnum.Always"/> that <b>never touches
    /// <see cref="SceneTree.Paused"/></b> — the Story pause menu (or the dialogue
    /// box) that opened it keeps whatever pause it owns — and raises
    /// <see cref="Closed"/> so the opener can restore focus.</para>
    /// </summary>
    public partial class DialogueLogScreen : CanvasLayer {

        /// <summary>Raised after the log hides.</summary>
        public event Action Closed;

        private Control _root;
        private ScrollContainer _scroll;
        private VBoxContainer _rows;
        private Label _emptyLabel;
        private Button _closeButton;
        private PanelContainer _shade;
        private PausePanel _panel;

        /// <summary>UI_SPEC_GLASS 9.2: the log is a 1200 x 860 overlay slab.</summary>
        public static readonly Vector2 PanelSize = new(1200f, 860f);

        /// <summary>The space between two entries (9.2: 14 px).</summary>
        public const int EntryGap = 14;

        /// <summary>The portrait frame beside an entry (G5, kept from the built log).</summary>
        public const float EntryPortraitSize = 64f;

        /// <summary>The row container. Test surface.</summary>
        internal VBoxContainer Rows => _rows;

        /// <summary>The log's glass slab. Test surface.</summary>
        internal PausePanel Panel => _panel;

        /// <summary>
        /// One dim (UI_SPEC_GLASS 9.2): opened from a pause menu the log sits on the pause's dim and draws none of its own;
        /// opened from the dialogue box it brings its 0.70 dim. Default false.
        /// </summary>
        public bool UsesHostDim { get; set; }

        public bool IsOpen => Visible;

        public override void _Ready() {
            Layer = 101;
            ProcessMode = ProcessModeEnum.Always;
            Visible = false;
            Build();
            DialogueLog.Changed += OnLogChanged;
        }

        public override void _ExitTree() {
            DialogueLog.Changed -= OnLogChanged;
            // Deliberately does not touch SceneTree.Paused: this screen never set it.
        }

        /// <summary>
        /// TG-PAUSE (UI_SPEC_GLASS 9.2): the 0.70 dim, a centred 1200 x 860 glass slab (kept on screen at UI scale 1.4 by
        /// <see cref="MenuSafeAreaFit"/>), the 36 px title, a scroll list of entries (a speaker in gold capitals over the
        /// line, 14 px between entries, a hairline rule between two speakers), a compact Back and the bottom prompt bar.
        /// </summary>
        private void Build() {
            _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Stop };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            UIPalette.ApplyTheme(_root);
            AddChild(_root);

            _shade = PulpScreenKit.MakeScrim();
            _root.AddChild(_shade);

            var center = new CenterContainer { Name = "Center", MouseFilter = Control.MouseFilterEnum.Ignore };
            center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(center);

            _panel = new PausePanel {
                Name = "Panel", ThemeTypeVariation = UIPalette.Variations.InkPanel, CustomMinimumSize = PanelSize
            };
            center.AddChild(_panel);
            PulpScreenKit.DressPanel(_panel, sheen: true, buttonInset: true);
            MenuSafeAreaFit.AttachClearOfPromptBar(_panel, _root);

            var body = new VBoxContainer { Name = "Body" };
            body.AddThemeConstantOverride("separation", 16);
            _panel.AddChild(body);

            var title = new Label {
                Name = "Title",
                Text = "dialogue_log_title",
                HorizontalAlignment = HorizontalAlignment.Center,
                Uppercase = true,
                ThemeTypeVariation = UIPalette.TitleLabelVariation
            };
            body.AddChild(title);

            _emptyLabel = new Label {
                Name = "EmptyLabel",
                Text = "dialogue_log_empty",
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = UIPalette.LabelRoles.BodySmallDimLabel
            };
            body.AddChild(_emptyLabel);

            _scroll = new ScrollContainer {
                Name = "Scroll",
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
            };
            body.AddChild(_scroll);

            _rows = new VBoxContainer { Name = "Rows", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _rows.AddThemeConstantOverride("separation", EntryGap);
            _scroll.AddChild(_rows);

            var footer = new HBoxContainer { Name = "Footer", Alignment = BoxContainer.AlignmentMode.Center };
            body.AddChild(footer);
            _closeButton = new Button {
                Name = "CloseButton",
                Text = "common_back",
                ThemeTypeVariation = UIPalette.Variations.PulpButtonCompact,
                CustomMinimumSize = new Vector2(MoveListScreen.CompactFooterMinWidth, ConfirmModal.ModalButtonHeight)
            };
            _closeButton.Pressed += Close;
            footer.AddChild(_closeButton);
            Pulp.PulpButtonDecor.Apply(_closeButton, compact: true).ShowPrompt = false;
            Pulp.PulpButtonDecor.ApplyStackWidth(new[] { _closeButton }, MoveListScreen.CompactFooterMinWidth, reservePrompt: false);

            PulpScreenKit.AddBottomPromptBar(_root, "common_back");
        }

        /// <summary>Repaints from the log and shows the overlay with focus on Close.</summary>
        public void Open() {
            Populate();
            Visible = true;
            if (_shade != null) _shade.Visible = !UsesHostDim;
            FocusChainBuilder.Apply(_root);
            _closeButton?.GrabFocus();
            ScrollToNewest();
            // MOT-A row 4: the slab fades and rises (its own dim fading in with it unless it sits on a host's).
            _panel?.PlayAppear(UsesHostDim ? null : _shade);
        }

        public void Close() {
            if (!Visible) return;
            Visible = false;
            _panel?.StopAppear(UsesHostDim ? null : _shade);
            Closed?.Invoke();
        }

        /// <summary>Rebuilds the rows from <see cref="DialogueLog"/>. Public so tests can repaint without opening.</summary>
        public void Populate() {
            if (_rows == null) return;
            Godot.Collections.Array<Node> existing = _rows.GetChildren();
            using (existing.AsDisposable()) {
                foreach (Node child in existing) {
                    _rows.RemoveChild(child);
                    child.Free();
                }
            }
            IReadOnlyCollection<DialogueLog.Entry> entries = DialogueLog.Entries;
            if (_emptyLabel != null) _emptyLabel.Visible = entries.Count == 0;
            int index = 0;
            string previousSpeaker = null;
            foreach (DialogueLog.Entry entry in entries) {
                // 9.2: a 1 px edge-dim rule between two speakers (the theme's separator line).
                string speaker = entry.Speaker ?? "";
                if (previousSpeaker != null && previousSpeaker != speaker) {
                    _rows.AddChild(new HSeparator { Name = $"SpeakerRule{index}", MouseFilter = Control.MouseFilterEnum.Ignore });
                }
                previousSpeaker = speaker;
                _rows.AddChild(BuildRow(entry, index++));
            }
        }

        private static Control BuildRow(DialogueLog.Entry entry, int index) {
            var row = new HBoxContainer { Name = $"Entry{index}" };
            row.AddThemeConstantOverride("separation", 18);

            // The speaker's G5 portrait frame at log size.
            var portrait = new Pulp.PulpPortraitRoundel { Name = "Portrait", Diameter = EntryPortraitSize };
            if (!string.IsNullOrWhiteSpace(entry.PortraitPath) && ResourceLoader.Exists(entry.PortraitPath)) {
                portrait.Portrait = ResourceLoader.Load<Texture2D>(entry.PortraitPath);
            }
            portrait.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            row.AddChild(portrait);

            var column = new VBoxContainer { Name = "Column", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            column.AddThemeConstantOverride("separation", 4);
            row.AddChild(column);

            // Already-resolved strings: control auto-translation must leave them alone. 9.2: the speaker in Cinzel 700 22,
            // accent (the nearest role, HeroName, recoloured: a kit gap), over the line in body copy (ink).
            var speakerLabel = new Label {
                Name = "Speaker",
                Text = entry.Speaker ?? "",
                AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
                Uppercase = true,
                ThemeTypeVariation = UIPalette.LabelRoles.LogSpeaker
            };
            column.AddChild(speakerLabel);

            var text = new Label {
                Name = "Text",
                Text = entry.Text ?? "",
                AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ThemeTypeVariation = UIPalette.LabelRoles.BodyCompactLabel
            };
            column.AddChild(text);
            return row;
        }

        private void ScrollToNewest() {
            if (_scroll == null) return;
            // Deferred: the new rows have no size until the container lays them out.
            Callable.From(() => {
                if (IsInstanceValid(_scroll)) _scroll.ScrollVertical = (int)_scroll.GetVScrollBar().MaxValue;
            }).CallDeferred();
        }

        private void OnLogChanged() {
            if (Visible) Populate();
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (!Visible || @event == null) return;
            if (@event.IsActionPressed("ui_cancel")
                || @event.IsActionPressed(InputManager.Actions.Pause)
                || @event.IsActionPressed(InputManager.Actions.DialogueLog)) {
                Close();
                GetViewport()?.SetInputAsHandled();
            }
        }
    }
}
