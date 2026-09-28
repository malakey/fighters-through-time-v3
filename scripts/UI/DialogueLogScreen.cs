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

        /// <summary>The row container. Test surface.</summary>
        internal VBoxContainer Rows => _rows;

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

        private void Build() {
            _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Stop };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            UIPalette.ApplyTheme(_root);
            AddChild(_root);

            var shade = new ColorRect { Name = "Shade", Color = UIPalette.Shade, MouseFilter = Control.MouseFilterEnum.Ignore };
            shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(shade);

            var margin = new MarginContainer { Name = "Margin" };
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 240);
            margin.AddThemeConstantOverride("margin_right", 240);
            margin.AddThemeConstantOverride("margin_top", 90);
            margin.AddThemeConstantOverride("margin_bottom", 90);
            _root.AddChild(margin);

            var panel = new PanelContainer { Name = "Panel", ThemeTypeVariation = DialogueTheme.GlassPanelVariation };
            margin.AddChild(panel);

            var body = new VBoxContainer { Name = "Body" };
            body.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            panel.AddChild(body);

            var title = new Label {
                Name = "Title",
                Text = "dialogue_log_title",
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = UIPalette.TitleLabelVariation
            };
            title.AddThemeColorOverride("font_color", UIPalette.TextAccent);
            body.AddChild(title);

            _emptyLabel = new Label {
                Name = "EmptyLabel",
                Text = "dialogue_log_empty",
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = UIPalette.SmallLabelVariation
            };
            body.AddChild(_emptyLabel);

            _scroll = new ScrollContainer {
                Name = "Scroll",
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
            };
            body.AddChild(_scroll);

            _rows = new VBoxContainer { Name = "Rows", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _rows.AddThemeConstantOverride("separation", 10);
            _scroll.AddChild(_rows);

            var footer = new HBoxContainer { Name = "Footer", Alignment = BoxContainer.AlignmentMode.End };
            body.AddChild(footer);
            _closeButton = new Button {
                Name = "CloseButton",
                Text = "common_back",
                CustomMinimumSize = new Vector2(UIPalette.ButtonMinWidth, UIPalette.ButtonMinHeight),
                ThemeTypeVariation = "TemporalGlassButton"
            };
            _closeButton.Pressed += Close;
            footer.AddChild(_closeButton);
        }

        /// <summary>Repaints from the log and shows the overlay with focus on Close.</summary>
        public void Open() {
            Populate();
            Visible = true;
            FocusChainBuilder.Apply(_root);
            _closeButton?.GrabFocus();
            ScrollToNewest();
        }

        public void Close() {
            if (!Visible) return;
            Visible = false;
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
            foreach (DialogueLog.Entry entry in entries) {
                _rows.AddChild(BuildRow(entry, index++));
            }
        }

        private static Control BuildRow(DialogueLog.Entry entry, int index) {
            var row = new HBoxContainer { Name = $"Entry{index}" };
            row.AddThemeConstantOverride("separation", 12);

            var portrait = new TextureRect {
                Name = "Portrait",
                CustomMinimumSize = new Vector2(56, 56),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            if (!string.IsNullOrWhiteSpace(entry.PortraitPath) && ResourceLoader.Exists(entry.PortraitPath)) {
                portrait.Texture = ResourceLoader.Load<Texture2D>(entry.PortraitPath);
            }
            row.AddChild(portrait);

            var column = new VBoxContainer { Name = "Column", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(column);

            // Already-resolved strings: control auto-translation must leave them alone.
            var speaker = new Label {
                Name = "Speaker",
                Text = entry.Speaker ?? "",
                AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
                ThemeTypeVariation = UIPalette.HeadingLabelVariation
            };
            speaker.AddThemeColorOverride("font_color", UIPalette.TextAccent);
            column.AddChild(speaker);

            var text = new Label {
                Name = "Text",
                Text = entry.Text ?? "",
                AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
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
