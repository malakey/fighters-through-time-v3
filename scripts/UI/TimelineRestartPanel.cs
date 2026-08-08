using Godot;
using System;

namespace FTT.UI {

    /// <summary>
    /// The Timeline Collapse restart choice, offered in the hub after a campaign
    /// level collapses: resume from the timeline anchor, or restart the level from
    /// the beginning.
    ///
    /// Package 8 B1 themed it, gave it a real focus chain (it previously authored
    /// no focus neighbours, so a controller player could not reliably move between
    /// three buttons on a screen that offers no other input), and put the
    /// full-restart option behind the shared confirmation — that option discards
    /// the timeline anchor the player already reached, and it sits directly below
    /// the option that keeps it.
    /// </summary>
    public partial class TimelineRestartPanel : Control {
        public event Action<bool> RestartChosen;
        public event Action Cancelled;

        private ConfirmModal _fullRestartConfirmation;

        /// <summary>The confirmation guarding the full-restart option.</summary>
        public ConfirmModal FullRestartConfirmation => _fullRestartConfirmation;

        public override void _Ready() {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            UIPalette.ApplyTheme(this);

            var shade = new ColorRect {
                Name = "Shade",
                Color = UIPalette.Shade,
                MouseFilter = MouseFilterEnum.Stop
            };
            shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(shade);

            var panel = new PanelContainer { Name = "Panel", CustomMinimumSize = new Vector2(620f, 300f) };
            panel.SetAnchorsPreset(LayoutPreset.Center);
            panel.Position = new Vector2(-310f, -150f);
            AddChild(panel);

            var layout = new VBoxContainer { Name = "Layout", Alignment = BoxContainer.AlignmentMode.Center };
            layout.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            panel.AddChild(layout);

            var title = new Label {
                Name = "Title",
                Text = Tr("timeline_collapse_title"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", UIPalette.TitleFontSize);
            title.AddThemeColorOverride("font_color", UIPalette.TextAccent);
            layout.AddChild(title);

            var anchor = MakeButton("AnchorButton", Tr("timeline_restart_anchor"));
            anchor.Pressed += () => RestartChosen?.Invoke(true);
            layout.AddChild(anchor);

            var full = MakeButton("FullRestartButton", Tr("timeline_restart_level"));
            full.Pressed += OnFullRestartPressed;
            layout.AddChild(full);

            var cancel = MakeButton("CancelButton", Tr("common_back"));
            cancel.Pressed += () => Cancelled?.Invoke();
            layout.AddChild(cancel);

            _fullRestartConfirmation = ConfirmModal.Create(
                "timeline_restart_level_confirm",
                confirmKey: "timeline_restart_level",
                cancelKey: "common_cancel",
                titleKey: "timeline_collapse_title");
            _fullRestartConfirmation.Confirmed += () => RestartChosen?.Invoke(false);
            AddChild(_fullRestartConfirmation);

            FocusChainBuilder.Apply(layout);
        }

        private void OnFullRestartPressed() => _fullRestartConfirmation?.Open();

        private static Button MakeButton(string name, string text) => new() {
            Name = name,
            Text = text,
            CustomMinimumSize = new Vector2(500f, UIPalette.ButtonMinHeight)
        };
    }
}
