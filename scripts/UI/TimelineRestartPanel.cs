using Godot;
using System;

namespace FTT.UI {

    public partial class TimelineRestartPanel : Control {
        public event Action<bool> RestartChosen;
        public event Action Cancelled;

        public override void _Ready() {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            var shade = new ColorRect {
                Color = new Color(0.01f, 0.03f, 0.08f, 0.86f),
                MouseFilter = MouseFilterEnum.Stop
            };
            shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(shade);

            var panel = new PanelContainer { CustomMinimumSize = new Vector2(620f, 300f) };
            panel.SetAnchorsPreset(LayoutPreset.Center);
            panel.Position = new Vector2(-310f, -150f);
            AddChild(panel);

            var layout = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            layout.AddThemeConstantOverride("separation", 20);
            panel.AddChild(layout);
            var title = new Label {
                Text = Tr("timeline_collapse_title"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", 28);
            layout.AddChild(title);

            var anchor = MakeButton(Tr("timeline_restart_anchor"));
            anchor.Pressed += () => RestartChosen?.Invoke(true);
            layout.AddChild(anchor);
            var full = MakeButton(Tr("timeline_restart_level"));
            full.Pressed += () => RestartChosen?.Invoke(false);
            layout.AddChild(full);
            var cancel = MakeButton(Tr("common_back"));
            cancel.Pressed += () => Cancelled?.Invoke();
            layout.AddChild(cancel);
            anchor.GrabFocus();
        }

        private static Button MakeButton(string text) => new() {
            Text = text,
            CustomMinimumSize = new Vector2(500f, 54f)
        };
    }
}
