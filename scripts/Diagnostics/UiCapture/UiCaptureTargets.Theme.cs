using System.Collections.Generic;
using FTT.UI;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>
    /// UR-THEME's capture targets, restyled by TG-THEME for the Temporal Glass rebuild: the theme specimen pages (every
    /// Label role, the themed controls, the palette, the glass components) and the main menu as it stands. The specimen
    /// text is diagnostic copy, not shipped UI. A static capture can focus one control only, so the hover, focus,
    /// focused-and-pressed and selected looks are shown by drawing the theme's own boxes over a plain button
    /// (<see cref="Look"/>), never by a hand-made box.
    /// </summary>
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

        /// <summary>A bright "art" swatch: what scene text must survive (the spec bounds every art by pure white).</summary>
        private static readonly Color SceneSwatch = new(0.86f, 0.89f, 0.95f);

        private static PanelContainer Backing(string name, bool scene) {
            var backing = new PanelContainer { Name = name };
            if (scene) {
                backing.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
                    BgColor = SceneSwatch,
                    ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 6, ContentMarginBottom = 6
                });
            } else {
                backing.ThemeTypeVariation = UIPalette.Variations.InkPanel;
            }
            return backing;
        }

        private static Control LabelRolePage(System.Func<(string Name, int Size, string Face, int Weight, float Tracking, string Halo), bool> filter) {
            Control root = SpecimenRoot();
            var flow = new HFlowContainer { Name = "Flow" };
            flow.AddThemeConstantOverride("h_separation", 12);
            flow.AddThemeConstantOverride("v_separation", 10);
            root.AddChild(flow);
            foreach (var role in UIPalette.LabelRoleTable) {
                if (!filter(role)) continue;
                // Scene-text roles (with a halo) sit on a bright art swatch, plate roles on a glass slab.
                PanelContainer backing = Backing(role.Name + "Backing", role.Halo.Length > 0);
                // One line per role, set in the role itself (face, weight, size and halo are in the tooltip and the handoff).
                backing.AddChild(new Label {
                    Text = role.Name, ThemeTypeVariation = role.Name,
                    TooltipText = $"{role.Face} {role.Weight} {role.Size}px {role.Tracking}em {(role.Halo.Length > 0 ? "halo " + role.Halo : "plate")}"
                });
                flow.AddChild(backing);
            }
            return root;
        }

        /// <summary>Every Label role at 30 px and under: plate roles on a glass slab, scene roles on a bright swatch.</summary>
        [UiCaptureTarget("theme_labels_small")]
        public static Node ThemeLabelsSmall(UiCaptureContext context) =>
            LabelRolePage(role => role.Size <= 30);

        /// <summary>The Label roles over 30 px, up to the 112 px menu title.</summary>
        [UiCaptureTarget("theme_labels_large")]
        public static Node ThemeLabelsLarge(UiCaptureContext context) =>
            LabelRolePage(role => role.Size > 30);

        private enum Look { Normal, Hover, Focus, Pressed, FocusPressed, Selected, Disabled }

        /// <summary>
        /// A button that shows <paramref name="look"/> once it is in the tree: hover and selected swap the theme's own hover
        /// or selected box in as its normal box; focus draws the theme's focus box over it as a child panel (the box is an
        /// overlay, so the label stays visible); pressed is a pressed toggle.
        /// </summary>
        private static Button LookButton(string text, string variation, Look look, List<(Button, Look)> deferred, float width = 0f) {
            var button = new Button {
                Text = text, ThemeTypeVariation = variation, FocusMode = Control.FocusModeEnum.None,
                ToggleMode = look is Look.Pressed or Look.FocusPressed,
                ButtonPressed = look is Look.Pressed or Look.FocusPressed,
                Disabled = look == Look.Disabled,
                TooltipText = (variation.Length > 0 ? variation : "Button") + " " + look
            };
            if (width > 0f) button.CustomMinimumSize = new Vector2(width, 0f);
            deferred.Add((button, look));
            return button;
        }

        private static void ApplyLooks(List<(Button Button, Look Look)> deferred) {
            foreach ((Button button, Look look) in deferred) {
                if (!GodotObject.IsInstanceValid(button)) continue;
                switch (look) {
                    case Look.Hover:
                        button.AddThemeStyleboxOverride("normal", button.GetThemeStylebox("hover"));
                        button.AddThemeColorOverride("font_color", button.GetThemeColor("font_hover_color"));
                        break;
                    case Look.Selected:
                        if (button.HasThemeStylebox("selected")) button.AddThemeStyleboxOverride("normal", button.GetThemeStylebox("selected"));
                        button.AddThemeColorOverride("font_color", UIPalette.Ink);
                        break;
                    case Look.Focus:
                    case Look.FocusPressed:
                        var overlay = new Panel { Name = "FocusLook", MouseFilter = Control.MouseFilterEnum.Ignore };
                        overlay.AddThemeStyleboxOverride("panel", button.GetThemeStylebox("focus"));
                        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                        button.AddChild(overlay);
                        button.AddThemeColorOverride("font_color", button.GetThemeColor("font_focus_color"));
                        break;
                }
            }
        }

        private static Label Heading(string text) =>
            new() { Text = text, ThemeTypeVariation = UIPalette.LabelRoles.CaptionLabel };

        /// <summary>Button states, inputs, bars, tabs and the other themed controls.</summary>
        [UiCaptureTarget("theme_controls")]
        public static Node ThemeControls(UiCaptureContext context) {
            Control root = SpecimenRoot();
            var deferred = new List<(Button, Look)>();
            var columns = new HBoxContainer { Name = "Columns" };
            columns.AddThemeConstantOverride("separation", 50);
            root.AddChild(columns);

            var left = new VBoxContainer { Name = "Left", CustomMinimumSize = new Vector2(900, 0) };
            left.AddThemeConstantOverride("separation", 16);
            columns.AddChild(left);
            var right = new VBoxContainer { Name = "Right", CustomMinimumSize = new Vector2(820, 0) };
            right.AddThemeConstantOverride("separation", 14);
            columns.AddChild(right);

            left.AddChild(Heading("Buttons: normal / focus / pressed / disabled"));
            foreach (string variation in new[] { "", UIPalette.Variations.PulpButton, UIPalette.Variations.TemporalGlassButton,
                         UIPalette.Variations.PulpButtonCompact, UIPalette.Variations.GlassTab }) {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 34);
                left.AddChild(row);
                bool wide = variation == UIPalette.Variations.PulpButton || variation == UIPalette.Variations.TemporalGlassButton;
                foreach (Look look in new[] { Look.Normal, Look.Focus, Look.Pressed, Look.Disabled }) {
                    row.AddChild(LookButton("GO", variation, look, deferred, wide ? 150f : 0f));
                }
                row.AddChild(new Label { Text = variation.Length > 0 ? variation : "Button (glass field)", ThemeTypeVariation = UIPalette.SmallLabelVariation });
            }

            left.AddChild(Heading("Panels: glass slabs and presets, the tag, the badges"));
            var panels = new HFlowContainer();
            panels.AddThemeConstantOverride("h_separation", 18);
            panels.AddThemeConstantOverride("v_separation", 18);
            left.AddChild(panels);
            var names = new List<string> { UIPalette.Variations.InkPanel, UIPalette.Variations.DialogueGlassPanel,
                UIPalette.Variations.PanelTagPlate, UIPalette.Variations.ReadyBadge, UIPalette.Variations.ReadyBadgeIdle };
            names.AddRange(new[] { UIPalette.Variations.GlassPanelPause, UIPalette.Variations.GlassPanelHud,
                UIPalette.Variations.GlassPanelDust, UIPalette.Variations.GlassPanelTimer,
                UIPalette.Variations.GlassPanelStats, UIPalette.Variations.GlassPanelContinue });
            foreach (string name in names) {
                var panel = new PanelContainer { ThemeTypeVariation = name };
                var label = new Label { Text = name, ThemeTypeVariation = UIPalette.LabelRoles.PanelTag };
                panel.AddChild(label);
                panels.AddChild(panel);
            }

            right.AddChild(Heading("Fields, slider, separator"));
            var fields = new HBoxContainer();
            fields.AddThemeConstantOverride("separation", 24);
            right.AddChild(fields);
            fields.AddChild(new LineEdit { Text = "Line edit", CustomMinimumSize = new Vector2(300, 0) });
            var option = new OptionButton { CustomMinimumSize = new Vector2(300, 0) };
            option.AddItem("Option one"); option.AddItem("Option two");
            fields.AddChild(option);
            var spin = new SpinBox { Value = 42 };
            fields.AddChild(spin);
            var checks = new HBoxContainer();
            checks.AddThemeConstantOverride("separation", 24);
            right.AddChild(checks);
            checks.AddChild(new CheckBox { Text = "Check box (off)" });
            checks.AddChild(new CheckBox { Text = "Check box (on)", ButtonPressed = true });
            var sliders = new HBoxContainer();
            sliders.AddThemeConstantOverride("separation", 24);
            right.AddChild(sliders);
            sliders.AddChild(new HSlider { Value = 70, CustomMinimumSize = new Vector2(360, 44) });
            sliders.AddChild(new HSlider { Value = 100, CustomMinimumSize = new Vector2(360, 44) });
            right.AddChild(new HSeparator());

            right.AddChild(Heading("Bars: default, then the five tubes"));
            right.AddChild(new ProgressBar { Value = 64, CustomMinimumSize = new Vector2(420, 24), ShowPercentage = false });
            AddTubes(right, 760f);

            right.AddChild(Heading("Tabs (TabContainer) and a scroll bar"));
            var tabs = new TabContainer { CustomMinimumSize = new Vector2(760, 150) };
            tabs.AddChild(new Label { Name = "Audio", Text = "First tab body" });
            tabs.AddChild(new Label { Name = "Display", Text = "Second tab body" });
            tabs.AddChild(new Label { Name = "Controls", Text = "Third tab body" });
            right.AddChild(tabs);
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(760, 110) };
            var tall = new VBoxContainer();
            for (int i = 0; i < 12; i++) tall.AddChild(new Label { Text = $"Scrolled row {i + 1}" });
            scroll.AddChild(tall);
            right.AddChild(scroll);

            context.AfterReady = _ => ApplyLooks(deferred);
            return root;
        }

        /// <summary>Every palette token as a swatch with its name and hex value: PAL-B, the glass literals, then legacy.</summary>
        [UiCaptureTarget("theme_palette")]
        public static Node ThemePalette(UiCaptureContext context) {
            Control root = SpecimenRoot();
            var flow = new HFlowContainer();
            flow.AddThemeConstantOverride("h_separation", 10);
            flow.AddThemeConstantOverride("v_separation", 8);
            root.AddChild(flow);
            void Swatch(string name, Color color) {
                var box = new VBoxContainer { CustomMinimumSize = new Vector2(196, 0) };
                // Translucent tokens sit over white (the spec's worst case) so their real weight shows.
                var under = new ColorRect { Color = new Color(1f, 1f, 1f), CustomMinimumSize = new Vector2(196, 34) };
                under.AddChild(new ColorRect { Color = color, Size = new Vector2(196, 34) });
                box.AddChild(under);
                box.AddChild(new Label { Text = $"{name} #{color.ToHtml(color.A < 1f)}", ThemeTypeVariation = UIPalette.SmallLabelVariation });
                flow.AddChild(box);
            }
            foreach (var (name, value) in UIPalette.GeneratorPalette) Swatch(name, value);
            Swatch("P1", UIPalette.P1); Swatch("P2", UIPalette.P2);
            Swatch("UnboundCold (grammar)", UIPalette.UnboundCold); Swatch("ResonanceGold (grammar)", UIPalette.ResonanceGold);
            Swatch("Cyan (P1 default and the Severed only)", UIPalette.Cyan);
            return root;
        }

        /// <summary>
        /// The glass components at their spec sizes (UI_SPEC_GLASS 3.0-3.3): the 68 / 64 / 58 px chevrons in every state
        /// (the focused-and-pressed row included), glass slabs with a tag, the READY badges, tabs, fields, a slider, the
        /// tubes, and the scene halo on bright art next to plate text.
        /// </summary>
        [UiCaptureTarget("theme_glass_components")]
        public static Node ThemeGlassComponents(UiCaptureContext context) {
            Control root = SpecimenRoot();
            var deferred = new List<(Button, Look)>();
            var columns = new HBoxContainer { Name = "Columns" };
            columns.AddThemeConstantOverride("separation", 40);
            root.AddChild(columns);

            var left = new VBoxContainer { Name = "Chevrons", CustomMinimumSize = new Vector2(600, 0) };
            left.AddThemeConstantOverride("separation", 11);
            columns.AddChild(left);
            left.AddChild(Heading("PulpButton 560 x 68, every state"));
            foreach ((string text, Look look) in new[] { ("STORY MODE", Look.Normal), ("FIGHTER MODE", Look.Hover),
                         ("CALIBRATION DRILLS", Look.Focus), ("SETTINGS", Look.Pressed), ("EXTRAS", Look.FocusPressed),
                         ("NORMAL", Look.Selected), ("QUIT", Look.Disabled) }) {
                Button button = LookButton(text, UIPalette.Variations.PulpButton, look, deferred, 560f);
                button.CustomMinimumSize = new Vector2(560f, 68f);
                button.Alignment = HorizontalAlignment.Left;
                left.AddChild(button);
            }
            left.AddChild(Heading("560 x 64 focused, 290 x 58 compact"));
            Button pause = LookButton("RESUME", UIPalette.Variations.TemporalGlassButton, Look.Focus, deferred, 560f);
            pause.CustomMinimumSize = new Vector2(560f, 64f);
            pause.Alignment = HorizontalAlignment.Left;
            left.AddChild(pause);
            var compact = new HBoxContainer();
            compact.AddThemeConstantOverride("separation", 34);
            left.AddChild(compact);
            foreach ((string text, Look look) in new[] { ("MOVE LIST", Look.Normal), ("BACK", Look.Focus) }) {
                Button button = LookButton(text, UIPalette.Variations.PulpButtonCompact, look, deferred, 290f);
                button.CustomMinimumSize = new Vector2(290f, 58f);
                button.Alignment = HorizontalAlignment.Left;
                compact.AddChild(button);
            }

            var middle = new VBoxContainer { Name = "Slabs", CustomMinimumSize = new Vector2(560, 0) };
            middle.AddThemeConstantOverride("separation", 26);
            columns.AddChild(middle);
            middle.AddChild(Heading("Slab G1 and its tag G2 at (26, -11)"));
            var slab = new PanelContainer { ThemeTypeVariation = UIPalette.Variations.InkPanel, CustomMinimumSize = new Vector2(540, 160) };
            var slabColumn = new VBoxContainer();
            slab.AddChild(slabColumn);
            slabColumn.AddChild(new Label { Text = "JOAN OF ARC", ThemeTypeVariation = UIPalette.LabelRoles.HeroName });
            slabColumn.AddChild(new Label { Text = "Panel copy on glass needs no halo.", ThemeTypeVariation = UIPalette.LabelRoles.BodySmallLabel });
            slabColumn.AddChild(new Label { Text = "SECTION", ThemeTypeVariation = UIPalette.HeadingLabelVariation });
            var tagHolder = new Control { Name = "TagHolder", MouseFilter = Control.MouseFilterEnum.Ignore };
            var tag = new PanelContainer { ThemeTypeVariation = UIPalette.Variations.PanelTagPlate, Position = new Vector2(26f - 26f, -11f - 20f) };
            tag.AddChild(new Label { Text = "VITALS", ThemeTypeVariation = UIPalette.LabelRoles.PanelTag });
            tagHolder.AddChild(tag);
            slab.AddChild(tagHolder);
            middle.AddChild(slab);
            var badges = new HBoxContainer();
            badges.AddThemeConstantOverride("separation", 24);
            middle.AddChild(badges);
            var ready = new PanelContainer { ThemeTypeVariation = UIPalette.Variations.ReadyBadge };
            ready.AddChild(new Label { Text = "READY!", ThemeTypeVariation = UIPalette.LabelRoles.CardStamp });
            badges.AddChild(ready);
            var idle = new PanelContainer { ThemeTypeVariation = UIPalette.Variations.ReadyBadgeIdle };
            var idleText = new Label { Text = "CHOOSING...", ThemeTypeVariation = UIPalette.LabelRoles.CardStamp };
            idleText.AddThemeColorOverride("font_color", UIPalette.Ink2);
            idle.AddChild(idleText);
            badges.AddChild(idle);
            middle.AddChild(Heading("Tabs G14: selected / unselected"));
            var tabRow = new HBoxContainer();
            tabRow.AddThemeConstantOverride("separation", 10);
            middle.AddChild(tabRow);
            tabRow.AddChild(new Button { Text = "AUDIO", ThemeTypeVariation = UIPalette.Variations.GlassTabSelected, FocusMode = Control.FocusModeEnum.None });
            tabRow.AddChild(new Button { Text = "DISPLAY", ThemeTypeVariation = UIPalette.Variations.GlassTab, FocusMode = Control.FocusModeEnum.None });
            tabRow.AddChild(new Button { Text = "CONTROLS", ThemeTypeVariation = UIPalette.Variations.GlassTab, FocusMode = Control.FocusModeEnum.None });
            middle.AddChild(Heading("Glass field: normal / focus"));
            var fieldRow = new HBoxContainer();
            fieldRow.AddThemeConstantOverride("separation", 24);
            middle.AddChild(fieldRow);
            fieldRow.AddChild(LookButton("1920 x 1080", UIPalette.Variations.GlassField, Look.Normal, deferred, 240f));
            fieldRow.AddChild(LookButton("Borderless", UIPalette.Variations.GlassField, Look.Focus, deferred, 240f));
            middle.AddChild(new HSlider { Value = 70, CustomMinimumSize = new Vector2(360, 44) });

            var right = new VBoxContainer { Name = "Tubes", CustomMinimumSize = new Vector2(560, 0) };
            right.AddThemeConstantOverride("separation", 20);
            columns.AddChild(right);
            right.AddChild(Heading("Bars: the glass tubes"));
            AddTubes(right, 540f);
            right.AddChild(Heading("Halos S and L on bright art"));
            PanelContainer art = Backing("HaloArt", scene: true);
            var artColumn = new VBoxContainer();
            art.AddChild(artColumn);
            var numerals = new HBoxContainer();
            numerals.AddChild(new Label { Text = "412", ThemeTypeVariation = UIPalette.LabelRoles.HudNumeralSmall });
            numerals.AddChild(new Label { Text = "/660", ThemeTypeVariation = UIPalette.LabelRoles.HudNumeralMax });
            artColumn.AddChild(numerals);
            artColumn.AddChild(new Label { Text = "POMPEII, 79 AD", ThemeTypeVariation = UIPalette.LabelRoles.LevelTitle });
            artColumn.AddChild(new Label { Text = "BOSS NAME", ThemeTypeVariation = UIPalette.LabelRoles.BossName });
            artColumn.AddChild(new Label { Text = "PLATE TEXT, NO HALO", ThemeTypeVariation = UIPalette.LabelRoles.CaptionLabel });
            right.AddChild(art);

            context.AfterReady = _ => ApplyLooks(deferred);
            return root;
        }

        /// <summary>The main menu root screen as the build stands (the Phase B baseline).</summary>
        [UiCaptureTarget("main_menu_root", SettleFrames = 20)]
        public static Node MainMenuRoot(UiCaptureContext context) =>
            ResourceLoader.Load<PackedScene>("res://scenes/menus/MainMenu.tscn").Instantiate();

        // TG-CLOSE: the five bar variations draw nothing of their own since the pulp purge; the tube decor draws them.
        private static void AddTubes(Control into, float bossWidth) {
            var specs = new (float Value, Vector2 Size, string Variation, FTT.UI.Pulp.PulpBarStyle Style)[] {
                (64, new Vector2(380, 30), UIPalette.Variations.PulpBar, FTT.UI.Pulp.PulpBarStyle.Hp),
                (82, new Vector2(510, 44), UIPalette.Variations.PulpBarH44, FTT.UI.Pulp.PulpBarStyle.Hp),
                (100, new Vector2(390, 16), UIPalette.Variations.PulpBarH16, FTT.UI.Pulp.PulpBarStyle.Meter),
                (55, new Vector2(170, 14), UIPalette.Variations.PulpBarH14, FTT.UI.Pulp.PulpBarStyle.Stat),
                (62, new Vector2(bossWidth, 34), UIPalette.Variations.PulpBossBar, FTT.UI.Pulp.PulpBarStyle.Boss),
            };
            foreach (var spec in specs) {
                var bar = new ProgressBar {
                    Value = spec.Value, CustomMinimumSize = spec.Size, ShowPercentage = false, ThemeTypeVariation = spec.Variation,
                    SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
                };
                into.AddChild(bar);
                FTT.UI.Pulp.PulpBarDecor.Attach(bar, spec.Style);
            }
        }
    }
}
