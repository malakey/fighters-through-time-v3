using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>TG-WIDGETS step 2's capture targets (first written by UR-WIDGETS): the glass tubes, lenses and chips, the not-yet circle, and step 1's frames, tagged panel and toggles.</summary>
    public static partial class UiCaptureTargets {

        private static ProgressBar WidgetBar(Vector2 size, string variation, float value, bool p2 = false) =>
            new() {
                ThemeTypeVariation = variation, CustomMinimumSize = size, Size = size, MinValue = 0, MaxValue = 100,
                Value = value, ShowPercentage = false,
                FillMode = p2 ? (int)ProgressBar.FillModeEnum.EndToBegin : (int)ProgressBar.FillModeEnum.BeginToEnd
            };

        /// <summary>Every tube style in both directions: Story and Fighter HP with the tracker and the echo, low HP, P2 mirrored, the Ultimate meter (charging, full, P2), the stat bar and the boss tube with a notch.</summary>
        [UiCaptureTarget("widgets_gauges")]
        public static Node WidgetsGauges(UiCaptureContext context) {
            Control page = WidgetPage();
            var column = new VBoxContainer { Position = new Vector2(80f, 24f) };
            column.AddThemeConstantOverride("separation", 6);
            page.AddChild(column);
            (string label, Vector2 size, string variation, PulpBarStyle style, float v, float lag, float echo, bool low, bool p2)[] rows = {
                ("Story HP 72 / lag 85 / echo 80 (380 x 30)", new Vector2(380f, 30f), UIPalette.Variations.PulpBar, PulpBarStyle.Hp, 72f, 85f, 80f, false, false),
                ("Fighter HP 60 / lag 78 / echo 70 (510 x 44)", new Vector2(510f, 44f), UIPalette.Variations.PulpBarH44, PulpBarStyle.Hp, 60f, 78f, 70f, false, false),
                ("Story low 18 (the 7 px danger pulse)", new Vector2(380f, 30f), UIPalette.Variations.PulpBar, PulpBarStyle.Hp, 18f, 30f, 0f, true, false),
                ("Fighter P2 mirrored 55 / lag 70 / echo 65", new Vector2(510f, 44f), UIPalette.Variations.PulpBarH44, PulpBarStyle.Hp, 55f, 70f, 65f, false, true),
                ("Fighter Ultimate meter 64 (390 x 16)", new Vector2(390f, 16f), UIPalette.Variations.PulpBarH16, PulpBarStyle.Meter, 64f, 0f, 0f, false, false),
                ("Fighter Ultimate meter full", new Vector2(390f, 16f), UIPalette.Variations.PulpBarH16, PulpBarStyle.Meter, 100f, 0f, 0f, false, false),
                ("Fighter P2 Ultimate meter 40, mirrored", new Vector2(390f, 16f), UIPalette.Variations.PulpBarH16, PulpBarStyle.Meter, 40f, 0f, 0f, false, true),
                ("Select stat 60 (170 x 14)", new Vector2(170f, 14f), UIPalette.Variations.PulpBarH14, PulpBarStyle.Stat, 60f, 0f, 0f, false, false),
                ("Story full", new Vector2(380f, 30f), UIPalette.Variations.PulpBar, PulpBarStyle.Hp, 100f, 0f, 0f, false, false)
            };
            foreach (var row in rows) {
                column.AddChild(new Label { Text = row.label, ThemeTypeVariation = UIPalette.SmallLabelVariation });
                var holder = new Control { CustomMinimumSize = row.size + new Vector2(40f, 28f) };
                column.AddChild(holder);
                ProgressBar bar = WidgetBar(row.size, row.variation, row.v, row.p2);
                holder.AddChild(bar);
                bar.Position = new Vector2(18f, 12f);
                PulpBarDecor decor = PulpBarDecor.Attach(bar, row.style);
                decor.LagValue = row.lag;
                decor.EchoValue = row.echo;
                decor.Low = row.low;
            }
            column.AddChild(new Label { Text = "Boss 64 / lag 80, phase notch at 50 (840 x 34)", ThemeTypeVariation = UIPalette.SmallLabelVariation });
            var bossHolder = new Control { CustomMinimumSize = new Vector2(880f, 72f) };
            column.AddChild(bossHolder);
            ProgressBar boss = WidgetBar(new Vector2(840f, 34f), UIPalette.Variations.PulpBossBar, 64f);
            bossHolder.AddChild(boss);
            boss.Position = new Vector2(18f, 12f);
            PulpBarDecor bossDecor = PulpBarDecor.Attach(boss, PulpBarStyle.Boss);
            bossDecor.LagValue = 80f;
            bossDecor.PhaseNotches = new[] { 0.5f };
            return page;
        }

        private static PulpAbilitySlot Lens(float size, PulpSlotState state, float p, float seconds, string key = "") {
            var slot = new PulpAbilitySlot { SlotSize = size, State = state, Progress = p, SecondsRemaining = seconds, KeyText = key };
            slot.Art = AbilityIconLibrary.Load("einstein", AbilitySlot.Special1);
            return slot;
        }

        /// <summary>The lens in every state at 74 (Story), 54 (Fighter) and 56 (select), and the 92 Ultimate charging, ready, suppressed and dormant.</summary>
        [UiCaptureTarget("widgets_slots")]
        public static Node WidgetsSlots(UiCaptureContext context) {
            Control page = WidgetPage();
            var column = new VBoxContainer { Position = new Vector2(90f, 60f) };
            column.AddThemeConstantOverride("separation", 58);
            page.AddChild(column);
            foreach (float size in new[] { 74f, 54f, 56f }) {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 46);
                column.AddChild(row);
                var states = new (PulpSlotState state, float p, float seconds)[] {
                    (PulpSlotState.Ready, 1f, 0f), (PulpSlotState.Cooling, 0.4f, 2.4f), (PulpSlotState.Cooling, 0.1f, 23f),
                    (PulpSlotState.Dormant, 0f, 0f), (PulpSlotState.Suppressed, 0.3f, 0f)
                };
                foreach (var s in states) row.AddChild(Lens(size, s.state, s.p, s.seconds, size == 74f ? "hud_slot_special1" : ""));
            }
            var ults = new HBoxContainer();
            ults.AddThemeConstantOverride("separation", 60);
            column.AddChild(ults);
            foreach (var s in new (PulpSlotState state, float p)[] {
                (PulpSlotState.Charging, 0.64f), (PulpSlotState.Ready, 1f), (PulpSlotState.Suppressed, 0.64f), (PulpSlotState.Dormant, 0f) }) {
                var ult = new PulpAbilitySlot { Ultimate = true, State = s.state, Progress = s.p, KeyText = "hud_slot_ultimate" };
                ult.Art = AbilityIconLibrary.Load("einstein", AbilitySlot.Ultimate);
                ults.AddChild(ult);
            }
            return page;
        }

        /// <summary>The not-yet flash 6 frames in: the glass circle round a 74 lens's ring and round the Ultimate, beside the frame on a 54 lens.</summary>
        [UiCaptureTarget("widgets_notyet", SettleFrames = 6)]
        public static Node WidgetsNotYet(UiCaptureContext context) {
            Control page = WidgetPage();
            var row = new HBoxContainer { Position = new Vector2(120f, 120f) };
            row.AddThemeConstantOverride("separation", 90);
            page.AddChild(row);
            PulpAbilitySlot story = Lens(74f, PulpSlotState.Ready, 1f, 0f, "hud_slot_special1");
            var ult = new PulpAbilitySlot { Ultimate = true, State = PulpSlotState.Charging, Progress = 0.4f, KeyText = "hud_slot_ultimate" };
            ult.Art = AbilityIconLibrary.Load("einstein", AbilitySlot.Ultimate);
            PulpAbilitySlot framed = Lens(54f, PulpSlotState.Ready, 1f, 0f);
            foreach (PulpAbilitySlot slot in new[] { story, ult, framed }) {
                slot.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
                row.AddChild(slot);
            }
            context.AfterReady = _ => {
                NotYetFlash.Play(story.Ring, story, NotYetShape.Circle);
                NotYetFlash.Play(ult.Ring, ult, NotYetShape.Circle);
                NotYetFlash.Play(framed, framed);
            };
            return page;
        }

        /// <summary>Status chips (G7): Story and Fighter sizes, each kind, a ring share, an empty G8 slot and a chip without text.</summary>
        [UiCaptureTarget("widgets_chips")]
        public static Node WidgetsChips(UiCaptureContext context) {
            Control page = WidgetPage();
            var panel = new PanelContainer { Position = new Vector2(80f, 60f), ThemeTypeVariation = UIPalette.Variations.InkPanel };
            page.AddChild(panel);
            var rows = new VBoxContainer();
            rows.AddThemeConstantOverride("separation", 28);
            panel.AddChild(rows);
            foreach (float badge in new[] { 44f, 40f }) {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", badge == 44f ? 30 : 18);
                rows.AddChild(row);
                row.AddChild(new PulpStatusChip { BadgeSize = badge, Kind = PulpChipKind.Damage, GlyphName = "venom", Remaining = 0.7f, NameText = "status_venom", TimeText = "4s" });
                row.AddChild(new PulpStatusChip { BadgeSize = badge, Kind = PulpChipKind.Control, GlyphName = "dilation", Remaining = 0.3f, NameText = "status_timedilation", TimeText = "1.2s" });
                row.AddChild(new PulpStatusChip { BadgeSize = badge, Kind = PulpChipKind.Suppression, GlyphName = "suppress", Remaining = 1f, NameText = "status_suppression", TimeText = "2s" });
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
