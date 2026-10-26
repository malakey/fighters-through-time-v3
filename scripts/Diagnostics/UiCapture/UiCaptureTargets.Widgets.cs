using FTT.UI;
using FTT.UI.Pulp;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>UR-WIDGETS' capture targets: the pulp button decor, the icon kit, the prompt bars and the halftone corner.</summary>
    public static partial class UiCaptureTargets {

        private static readonly Color WidgetBackdrop = new(0.13f, 0.10f, 0.19f);

        private static Control WidgetPage() {
            var page = new Control { Name = "WidgetPage" };
            UIPalette.ApplyTheme(page);
            var back = new ColorRect { Color = WidgetBackdrop };
            back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            page.AddChild(back);
            return page;
        }

        private static Button WidgetButton(string text, string variation, Vector2 size) {
            var button = new Button {
                Text = text, ThemeTypeVariation = variation, FocusMode = Control.FocusModeEnum.All,
                CustomMinimumSize = size, Size = size
            };
            return button;
        }

        private static Node WidgetButtons(UiCaptureContext context, PromptDevice device) {
            PulpPromptGlyphs.DeviceOverrideForTesting = device;
            Control page = WidgetPage();
            var column = new VBoxContainer { Position = new Vector2(80f, 40f) };
            column.AddThemeConstantOverride("separation", 24);
            page.AddChild(column);

            Button focus = null;
            string[] states = { "NORMAL", "FOCUS", "SELECTED", "PRESSED (toggle)", "DISABLED" };
            for (int i = 0; i < states.Length; i++) {
                Button button = WidgetButton(states[i], UIPalette.Variations.PulpButton, new Vector2(640f, 80f));
                if (i == 2) { button.ToggleMode = true; button.ButtonPressed = true; }
                if (i == 3) { button.ToggleMode = true; }
                if (i == 4) button.Disabled = true;
                column.AddChild(button);
                PulpButtonDecor.Apply(button);
                if (i == 1) focus = button;
            }
            var compact = new HBoxContainer();
            compact.AddThemeConstantOverride("separation", 24);
            column.AddChild(compact);
            string[] compactStates = { "COMPACT", "FOCUS", "OFF" };
            for (int i = 0; i < compactStates.Length; i++) {
                Button button = WidgetButton(compactStates[i], UIPalette.Variations.PulpButtonCompact, new Vector2(300f, 58f));
                if (i == 2) button.Disabled = true;
                compact.AddChild(button);
                PulpButtonDecor.Apply(button, compact: true);
                if (i == 1) focus ??= button;
            }
            var glass = WidgetButton("GLASS BUTTON", UIPalette.Variations.TemporalGlassButton, new Vector2(640f, 64f));
            column.AddChild(glass);
            PulpButtonDecor.Apply(glass);

            context.AfterReady = _ => focus?.GrabFocus();
            return page;
        }

        /// <summary>PulpButtons in every state, with the pad confirm badge on the focused row.</summary>
        [UiCaptureTarget("widgets_buttons", SettleFrames = 14)]
        public static Node WidgetsButtons(UiCaptureContext context) => WidgetButtons(context, PromptDevice.Pad);

        /// <summary>The same stack with the keyboard key-cap badge.</summary>
        [UiCaptureTarget("widgets_buttons_kb", SettleFrames = 14)]
        public static Node WidgetsButtonsKeyboard(UiCaptureContext context) => WidgetButtons(context, PromptDevice.Keyboard);

        /// <summary>All 26 kit icons at 28 and 46 px over a dark panel, the flat variants of dormant / cross beside them.</summary>
        [UiCaptureTarget("widgets_icons")]
        public static Node WidgetsIcons(UiCaptureContext context) {
            Control page = WidgetPage();
            var panel = new PanelContainer { Position = new Vector2(60f, 40f), ThemeTypeVariation = UIPalette.Variations.InkPanel };
            page.AddChild(panel);
            var rows = new VBoxContainer();
            rows.AddThemeConstantOverride("separation", 18);
            panel.AddChild(rows);
            foreach (int size in new[] { 28, 46 }) {
                var flow = new HFlowContainer { CustomMinimumSize = new Vector2(1500f, 0f) };
                flow.AddThemeConstantOverride("h_separation", 20);
                flow.AddThemeConstantOverride("v_separation", 12);
                rows.AddChild(flow);
                foreach (string name in PulpIcon.KitNames) {
                    var cell = new VBoxContainer();
                    cell.AddChild(PulpIcon.Create(name, name == "marker" ? UIPalette.Ink3 : UIPalette.Accent, size));
                    var caption = new Label { Text = name, ThemeTypeVariation = UIPalette.SmallLabelVariation };
                    cell.AddChild(caption);
                    flow.AddChild(cell);
                }
            }
            var flats = new HBoxContainer();
            flats.AddThemeConstantOverride("separation", 20);
            rows.AddChild(flats);
            flats.AddChild(new Label { Text = "flat:", ThemeTypeVariation = UIPalette.SmallLabelVariation });
            foreach (string name in new[] { "dormant", "cross" }) {
                PulpIcon flat = PulpIcon.Create(name, UIPalette.Ink2, 46f);
                flat.Flat = true;
                flats.AddChild(flat);
            }
            return page;
        }

        private static Node WidgetPrompts(PromptDevice device) {
            PulpPromptGlyphs.DeviceOverrideForTesting = device;
            Control page = WidgetPage();
            var column = new VBoxContainer { Position = new Vector2(80f, 40f) };
            column.AddThemeConstantOverride("separation", 36);
            page.AddChild(column);

            var bar = new PulpPromptBar();
            bar.AddPrompt("ui_accept", "ui_prompt_select");
            bar.AddPrompt("ui_cancel", "common_back");
            column.AddChild(bar);

            var panels = new HBoxContainer();
            panels.AddThemeConstantOverride("separation", 60);
            column.AddChild(panels);
            foreach (bool paper in new[] { false, true }) {
                var panel = new PanelContainer {
                    ThemeTypeVariation = paper ? UIPalette.Variations.PaperPanel : UIPalette.Variations.InkPanel,
                    CustomMinimumSize = new Vector2(520f, 300f)
                };
                HalftoneCorner.Attach(panel, paper);
                var label = new Label { Text = paper ? "PAPER PANEL" : "DARK PANEL", ThemeTypeVariation = "HeadingLabel" };
                panel.AddChild(label);
                panels.AddChild(panel);
            }
            return page;
        }

        /// <summary>A pad prompt bar and a dark and a paper panel with the halftone corner.</summary>
        [UiCaptureTarget("widgets_prompts_pad")]
        public static Node WidgetsPromptsPad(UiCaptureContext context) => WidgetPrompts(PromptDevice.Pad);

        /// <summary>A keyboard prompt bar and the two panels.</summary>
        [UiCaptureTarget("widgets_prompts_kb")]
        public static Node WidgetsPromptsKeyboard(UiCaptureContext context) => WidgetPrompts(PromptDevice.Keyboard);
    }
}
