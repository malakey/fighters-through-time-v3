using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>UR-WIDGETS step 2's capture targets: the GAUGE-B parts, the tagged panel and the toggles.</summary>
    public static partial class UiCaptureTargets {

        private static ProgressBar WidgetBar(Vector2 size, string variation, float value, bool p2 = false) =>
            new() {
                ThemeTypeVariation = variation, CustomMinimumSize = size, Size = size, MinValue = 0, MaxValue = 100,
                Value = value, ShowPercentage = false,
                FillMode = p2 ? (int)ProgressBar.FillModeEnum.EndToBegin : (int)ProgressBar.FillModeEnum.BeginToEnd
            };

        private static void TintBarFill(ProgressBar bar, Color color) {
            Theme theme = UIPalette.LoadTheme();
            if (theme.GetStylebox("fill", "PulpBar").Duplicate() is StyleBoxFlat fill) {
                fill.BgColor = color;
                bar.AddThemeStyleboxOverride("fill", fill);
            }
        }

        /// <summary>Story 380 x 30 and Fighter 510 x 44 bars with tracker, echo, hatch and ticks, a low bar, a P2 bar and a boss bar.</summary>
        [UiCaptureTarget("widgets_gauges")]
        public static Node WidgetsGauges(UiCaptureContext context) {
            Control page = WidgetPage();
            var column = new VBoxContainer { Position = new Vector2(80f, 30f) };
            column.AddThemeConstantOverride("separation", 10);
            page.AddChild(column);
            (string label, Vector2 size, float v, float lag, float echo, bool low, bool p2)[] rows = {
                ("Story 72 / lag 85 / echo 80", new Vector2(380f, 30f), 72f, 85f, 80f, false, false),
                ("Fighter 60 / lag 78 / echo 70", new Vector2(510f, 44f), 60f, 78f, 70f, false, false),
                ("Story low 18", new Vector2(380f, 30f), 18f, 30f, 0f, true, false),
                ("Fighter P2 right-filling 55", new Vector2(510f, 44f), 55f, 70f, 65f, false, true),
                ("Story full", new Vector2(380f, 30f), 100f, 0f, 0f, false, false)
            };
            foreach (var row in rows) {
                column.AddChild(new Label { Text = row.label, ThemeTypeVariation = UIPalette.SmallLabelVariation });
                var holder = new Control { CustomMinimumSize = row.size + new Vector2(30f, 22f) };
                column.AddChild(holder);
                ProgressBar bar = WidgetBar(row.size, UIPalette.Variations.PulpBar, row.v, row.p2);
                TintBarFill(bar, row.low ? UIPalette.Danger : UIPalette.Hp);
                holder.AddChild(bar);
                bar.Position = new Vector2(14f, 6f);
                PulpBarDecor decor = PulpBarDecor.Attach(bar);
                decor.LagValue = row.lag;
                decor.EchoValue = row.echo;
                decor.Low = row.low;
            }
            column.AddChild(new Label { Text = "Boss 64, phase notch at 50", ThemeTypeVariation = UIPalette.SmallLabelVariation });
            var bossHolder = new Control { CustomMinimumSize = new Vector2(880f, 64f) };
            column.AddChild(bossHolder);
            ProgressBar boss = WidgetBar(new Vector2(840f, 44f), UIPalette.Variations.PulpBossBar, 64f);
            bossHolder.AddChild(boss);
            boss.Position = new Vector2(14f, 6f);
            PulpBarDecor bossDecor = PulpBarDecor.Attach(boss, PulpBarStyle.Boss);
            bossDecor.LagValue = 80f;
            bossDecor.PhaseNotches = new[] { 0.5f };
            return page;
        }

        /// <summary>Every slot state at 74 / 54 / 50, and the Ultimate charging, ready, suppressed and dormant.</summary>
        [UiCaptureTarget("widgets_slots")]
        public static Node WidgetsSlots(UiCaptureContext context) {
            Control page = WidgetPage();
            var column = new VBoxContainer { Position = new Vector2(80f, 50f) };
            column.AddThemeConstantOverride("separation", 36);
            page.AddChild(column);
            foreach (float size in new[] { 74f, 54f, 50f }) {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 34);
                column.AddChild(row);
                var states = new (PulpSlotState state, float p, float seconds)[] {
                    (PulpSlotState.Ready, 1f, 0f), (PulpSlotState.Cooling, 0.4f, 2.4f), (PulpSlotState.Cooling, 0.1f, 23f),
                    (PulpSlotState.Dormant, 0f, 0f), (PulpSlotState.Suppressed, 0f, 0f)
                };
                foreach (var s in states) {
                    var slot = new PulpAbilitySlot { SlotSize = size, State = s.state, Progress = s.p, SecondsRemaining = s.seconds };
                    slot.Art = AbilityIconLibrary.Load("einstein", AbilitySlot.Special1);
                    if (size == 74f) slot.KeyText = "hud_slot_special1";
                    row.AddChild(slot);
                }
            }
            var ults = new HBoxContainer();
            ults.AddThemeConstantOverride("separation", 50);
            column.AddChild(ults);
            foreach (var s in new (PulpSlotState state, float p)[] {
                (PulpSlotState.Charging, 0.64f), (PulpSlotState.Ready, 1f), (PulpSlotState.Suppressed, 0.64f), (PulpSlotState.Dormant, 0f) }) {
                var ult = new PulpAbilitySlot { Ultimate = true, State = s.state, Progress = s.p, KeyText = "hud_slot_ultimate" };
                ult.Art = AbilityIconLibrary.Load("einstein", AbilitySlot.Ultimate);
                ults.AddChild(ult);
            }
            return page;
        }

        /// <summary>Status chips: Story and Fighter sizes, each kind, a ring share and an empty reserved slot.</summary>
        [UiCaptureTarget("widgets_chips")]
        public static Node WidgetsChips(UiCaptureContext context) {
            Control page = WidgetPage();
            var panel = new PanelContainer { Position = new Vector2(80f, 60f), ThemeTypeVariation = UIPalette.Variations.InkPanel };
            page.AddChild(panel);
            var rows = new VBoxContainer();
            rows.AddThemeConstantOverride("separation", 24);
            panel.AddChild(rows);
            foreach (float badge in new[] { 44f, 40f }) {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 30);
                rows.AddChild(row);
                row.AddChild(new PulpStatusChip { BadgeSize = badge, Kind = PulpChipKind.Damage, GlyphName = "venom", Remaining = 0.7f, NameText = "Venom", TimeText = "4s" });
                row.AddChild(new PulpStatusChip { BadgeSize = badge, Kind = PulpChipKind.Control, GlyphName = "dilation", Remaining = 0.3f, NameText = "Dilation", TimeText = "1.2s" });
                row.AddChild(new PulpStatusChip { BadgeSize = badge, Kind = PulpChipKind.Suppression, GlyphName = "suppress", Remaining = 1f, NameText = "Suppressed", TimeText = "2s" });
                row.AddChild(new PulpStatusChip { BadgeSize = badge, Empty = true, NameText = "", TimeText = "--" });
                row.AddChild(new PulpStatusChip { BadgeSize = badge, Kind = PulpChipKind.Damage, GlyphName = "burn", Remaining = 0.5f, ShowText = false });
            }
            return page;
        }

        /// <summary>TG-WIDGETS: the G5 portrait frame at 112, 110, 86, 150 and 320 for two heroes (the target keeps the roundel's name).</summary>
        [UiCaptureTarget("widgets_roundel")]
        public static Node WidgetsRoundel(UiCaptureContext context) {
            Control page = WidgetPage();
            var column = new VBoxContainer { Position = new Vector2(80f, 60f) };
            column.AddThemeConstantOverride("separation", 60);
            page.AddChild(column);
            foreach (string who in new[] { "einstein", "joan" }) {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 50);
                column.AddChild(row);
                foreach (float d in new[] { 112f, 110f, 86f, 150f, 320f }) {
                    var roundel = new PulpPortraitRoundel { Diameter = d, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
                    roundel.Portrait = GD.Load<Texture2D>($"res://assets/sprites/characters/portraits/{who}_portrait.png");
                    row.AddChild(roundel);
                }
            }
            return page;
        }

        /// <summary>A tagged dark panel and a tagged paper panel inside a box container.</summary>
        [UiCaptureTarget("widgets_tagged_panel")]
        public static Node WidgetsTaggedPanel(UiCaptureContext context) {
            Control page = WidgetPage();
            var row = new HBoxContainer { Position = new Vector2(100f, 100f) };
            row.AddThemeConstantOverride("separation", 70);
            page.AddChild(row);
            foreach (bool paper in new[] { false, true }) {
                var tagged = new PulpTaggedPanel { TagKey = paper ? "menu_story_mode" : "menu_settings" };
                var panel = new PanelContainer {
                    ThemeTypeVariation = paper ? UIPalette.Variations.PaperPanel : UIPalette.Variations.InkPanel,
                    CustomMinimumSize = new Vector2(420f, 220f)
                };
                panel.AddChild(new Label { Text = "contents", ThemeTypeVariation = "HeadingLabel" });
                tagged.AddChild(panel);
                row.AddChild(tagged);
            }
            return page;
        }

        /// <summary>CheckButton toggles: off, on, focused and disabled.</summary>
        [UiCaptureTarget("widgets_toggles", SettleFrames = 14)]
        public static Node WidgetsToggles(UiCaptureContext context) {
            Control page = WidgetPage();
            var column = new VBoxContainer { Position = new Vector2(80f, 60f) };
            column.AddThemeConstantOverride("separation", 28);
            page.AddChild(column);
            CheckButton focus = null;
            string[] names = { "OFF", "ON", "FOCUSED", "DISABLED OFF", "DISABLED ON" };
            for (int i = 0; i < names.Length; i++) {
                var toggle = new CheckButton {
                    Text = names[i], CustomMinimumSize = new Vector2(520f, 52f), FocusMode = Control.FocusModeEnum.All,
                    ButtonPressed = i == 1 || i == 4, Disabled = i >= 3
                };
                column.AddChild(toggle);
                PulpToggleDecor.Apply(toggle);
                if (i == 2) focus = toggle;
            }
            context.AfterReady = _ => focus?.GrabFocus();
            return page;
        }
    }
}
