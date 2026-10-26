using FTT.UI;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>UR-THEME's capture targets: the theme specimen pages and the main menu as it stands.</summary>
    public static partial class UiCaptureTargets {

        private static Control SpecimenRoot() {
            var root = new MarginContainer { Name = "Specimen" };
            root.AddThemeConstantOverride("margin_left", 40);
            root.AddThemeConstantOverride("margin_top", 30);
            root.AddThemeConstantOverride("margin_right", 40);
            root.AddThemeConstantOverride("margin_bottom", 30);
            UIPalette.ApplyTheme(root);
            return root;
        }

        private static bool IsBright(Color c) => (c.R * 0.2126f + c.G * 0.7152f + c.B * 0.0722f) > 0.45f;

        private static Control LabelRolePage(System.Func<(string Name, int Size, string Face, int Weight), bool> filter) {
            Control root = SpecimenRoot();
            var flow = new HFlowContainer { Name = "Flow" };
            flow.AddThemeConstantOverride("h_separation", 16);
            flow.AddThemeConstantOverride("v_separation", 10);
            root.AddChild(flow);
            Theme theme = UIPalette.LoadTheme();
            foreach (var role in UIPalette.LabelRoleTable) {
                if (!filter(role)) continue;
                Color color = theme.HasColor("font_color", role.Name) ? theme.GetColor("font_color", role.Name) : UIPalette.Ink;
                var backing = new PanelContainer { Name = role.Name + "Backing" };
                backing.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
                    BgColor = IsBright(color) ? UIPalette.Track : UIPalette.Accent,
                    ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 4, ContentMarginBottom = 4
                });
                var column = new VBoxContainer();
                backing.AddChild(column);
                var sample = new Label { Text = role.Name, ThemeTypeVariation = role.Name };
                column.AddChild(sample);
                var caption = new Label {
                    Text = $"{role.Face} {role.Weight}  {role.Size}px",
                    ThemeTypeVariation = UIPalette.SmallLabelVariation
                };
                caption.AddThemeColorOverride("font_color", IsBright(color) ? UIPalette.Ink2 : UIPalette.LabelDark);
                column.AddChild(caption);
                flow.AddChild(backing);
            }
            return root;
        }

        /// <summary>Every Label role at 40 px and under, on a backing swatch.</summary>
        [UiCaptureTarget("theme_labels_small")]
        public static Node ThemeLabelsSmall(UiCaptureContext context) =>
            LabelRolePage(role => role.Size <= 40);

        /// <summary>The display-size Label roles.</summary>
        [UiCaptureTarget("theme_labels_large")]
        public static Node ThemeLabelsLarge(UiCaptureContext context) =>
            LabelRolePage(role => role.Size > 40);

        /// <summary>Button states, inputs, bars, tabs and the other themed controls.</summary>
        [UiCaptureTarget("theme_controls")]
        public static Node ThemeControls(UiCaptureContext context) {
            Control root = SpecimenRoot();
            var columns = new HBoxContainer { Name = "Columns" };
            columns.AddThemeConstantOverride("separation", 50);
            root.AddChild(columns);

            var left = new VBoxContainer { Name = "Left", CustomMinimumSize = new Vector2(700, 0) };
            left.AddThemeConstantOverride("separation", 18);
            columns.AddChild(left);
            var right = new VBoxContainer { Name = "Right", CustomMinimumSize = new Vector2(720, 0) };
            right.AddThemeConstantOverride("separation", 16);
            columns.AddChild(right);

            left.AddChild(Heading("Buttons: normal / focus (also hover) / pressed / disabled"));
            Button focused = null;
            foreach (string variation in new[] { "", UIPalette.Variations.PulpButton, UIPalette.Variations.PulpButtonCompact, UIPalette.Variations.TemporalGlassButton }) {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 26);
                left.AddChild(row);
                string tag = variation == "" ? "Button" : variation;
                for (int state = 0; state < 4; state++) {
                    var button = new Button {
                        Text = state switch { 0 => "NORMAL", 1 => "FOCUS", 2 => "PRESSED", _ => "OFF" },
                        ThemeTypeVariation = variation,
                        ToggleMode = state == 2,
                        ButtonPressed = state == 2,
                        Disabled = state == 3,
                        FocusMode = Control.FocusModeEnum.All,
                        TooltipText = tag
                    };
                    if (variation == UIPalette.Variations.PulpButton || variation == UIPalette.Variations.TemporalGlassButton) {
                        button.CustomMinimumSize = new Vector2(150, 0);
                        button.Text = state switch { 0 => "Go", 1 => "Go", 2 => "Go", _ => "Go" };
                    }
                    if (state == 1 && focused == null) focused = button;
                    row.AddChild(button);
                }
                row.AddChild(new Label { Text = tag, ThemeTypeVariation = UIPalette.SmallLabelVariation });
            }

            left.AddChild(Heading("Panels"));
            var panels = new HFlowContainer();
            panels.AddThemeConstantOverride("h_separation", 28);
            panels.AddThemeConstantOverride("v_separation", 22);
            left.AddChild(panels);
            foreach (string name in new[] {
                UIPalette.Variations.InkPanel, UIPalette.Variations.PaperPanel, UIPalette.Variations.BannerPanel,
                UIPalette.Variations.PanelTagPlate, UIPalette.Variations.BossNameplate, UIPalette.Variations.BossPhaseTab,
                UIPalette.Variations.SlotPlate, UIPalette.Variations.SlotPlateReady, UIPalette.Variations.KeyTagPlate,
                UIPalette.Variations.CardPlate, UIPalette.Variations.CardPlateP1, UIPalette.Variations.CardPlateP2,
                UIPalette.Variations.BubblePlate, UIPalette.Variations.StampPlate, UIPalette.Variations.HudPlatePanel,
                UIPalette.Variations.DialogueGlassPanel }) {
                var panel = new PanelContainer { ThemeTypeVariation = name };
                bool dark = name is nameof(UIPalette.Variations.BannerPanel) or nameof(UIPalette.Variations.PaperPanel)
                    or nameof(UIPalette.Variations.PanelTagPlate) or nameof(UIPalette.Variations.BossPhaseTab)
                    or nameof(UIPalette.Variations.KeyTagPlate) or nameof(UIPalette.Variations.BubblePlate);
                var label = new Label { Text = name };
                label.AddThemeFontSizeOverride("font_size", 18);
                if (dark) label.AddThemeColorOverride("font_color", UIPalette.LabelDark);
                panel.AddChild(label);
                panels.AddChild(panel);
            }

            right.AddChild(Heading("Inputs"));
            right.AddChild(new LineEdit { Text = "Line edit", CustomMinimumSize = new Vector2(380, 0) });
            var option = new OptionButton();
            option.AddItem("Option one"); option.AddItem("Option two");
            right.AddChild(option);
            right.AddChild(new CheckBox { Text = "Check box (off)" });
            right.AddChild(new CheckBox { Text = "Check box (on)", ButtonPressed = true });
            var spin = new SpinBox { Value = 42 };
            right.AddChild(spin);
            right.AddChild(new HSlider { Value = 60, CustomMinimumSize = new Vector2(420, 24) });
            right.AddChild(new HSeparator());

            right.AddChild(Heading("Bars"));
            right.AddChild(new ProgressBar { Value = 64, CustomMinimumSize = new Vector2(420, 28) });
            right.AddChild(new ProgressBar { Value = 64, CustomMinimumSize = new Vector2(420, 34), ThemeTypeVariation = UIPalette.Variations.PulpBar });
            right.AddChild(new ProgressBar { Value = 40, CustomMinimumSize = new Vector2(520, 40), ThemeTypeVariation = UIPalette.Variations.PulpBossBar });

            right.AddChild(Heading("Tabs"));
            var tabs = new TabContainer { CustomMinimumSize = new Vector2(620, 150) };
            var first = new Label { Name = "Audio", Text = "First tab body" };
            var second = new Label { Name = "Display", Text = "Second tab body" };
            tabs.AddChild(first); tabs.AddChild(second);
            right.AddChild(tabs);

            context.AfterReady = _ => focused?.GrabFocus();
            return root;
        }

        private static Label Heading(string text) {
            var label = new Label { Text = text, ThemeTypeVariation = UIPalette.LabelRoles.CaptionLabel };
            return label;
        }

        /// <summary>Every PAL-B token as a swatch with its name and hex value.</summary>
        [UiCaptureTarget("theme_palette")]
        public static Node ThemePalette(UiCaptureContext context) {
            Control root = SpecimenRoot();
            var flow = new HFlowContainer();
            flow.AddThemeConstantOverride("h_separation", 14);
            flow.AddThemeConstantOverride("v_separation", 14);
            root.AddChild(flow);
            void Swatch(string name, Color color) {
                var box = new VBoxContainer { CustomMinimumSize = new Vector2(220, 0) };
                box.AddChild(new ColorRect { Color = color, CustomMinimumSize = new Vector2(220, 56) });
                box.AddChild(new Label { Text = $"{name}  #{color.ToHtml(color.A < 1f)}", ThemeTypeVariation = UIPalette.SmallLabelVariation });
                flow.AddChild(box);
            }
            Swatch("PlateFill", UIPalette.PlateFill); Swatch("PlateRaised", UIPalette.PlateRaised);
            Swatch("Backdrop", UIPalette.Backdrop); Swatch("Ink", UIPalette.Ink); Swatch("Ink2", UIPalette.Ink2);
            Swatch("Ink3", UIPalette.Ink3); Swatch("Accent", UIPalette.Accent); Swatch("Accent2", UIPalette.Accent2);
            Swatch("Res", UIPalette.Res); Swatch("Res2", UIPalette.Res2); Swatch("Meter", UIPalette.Meter);
            Swatch("Meter2", UIPalette.Meter2); Swatch("Hp", UIPalette.Hp); Swatch("HpLag", UIPalette.HpLag);
            Swatch("Echo", UIPalette.Echo); Swatch("Shield", UIPalette.Shield); Swatch("Danger", UIPalette.Danger);
            Swatch("Boss", UIPalette.Boss); Swatch("Paper", UIPalette.Paper); Swatch("P1", UIPalette.P1);
            Swatch("P2", UIPalette.P2); Swatch("InkBlack", UIPalette.InkBlack); Swatch("LabelDark", UIPalette.LabelDark);
            Swatch("PaperInk", UIPalette.PaperInk); Swatch("PaperInk2", UIPalette.PaperInk2);
            Swatch("PaperInk3", UIPalette.PaperInk3); Swatch("Track", UIPalette.Track);
            Swatch("SlotFrame", UIPalette.SlotFrame); Swatch("PromptCream", UIPalette.PromptCream);
            Swatch("StatusDamage", UIPalette.StatusDamage); Swatch("StatusControl", UIPalette.StatusControl);
            Swatch("DisabledFill", UIPalette.DisabledFill); Swatch("DisabledLabel", UIPalette.DisabledLabel);
            Swatch("UnboundCold (grammar)", UIPalette.UnboundCold); Swatch("ResonanceGold (grammar)", UIPalette.ResonanceGold);
            Swatch("Cyan (legacy)", UIPalette.Cyan);
            return root;
        }

        /// <summary>The main menu root screen as the build stands (the Phase B baseline).</summary>
        [UiCaptureTarget("main_menu_root", SettleFrames = 20)]
        public static Node MainMenuRoot(UiCaptureContext context) =>
            ResourceLoader.Load<PackedScene>("res://scenes/menus/MainMenu.tscn").Instantiate();
    }
}
