using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using FTT.UI.Motion;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-POLISH2. The last small defects the gallery showed: the Level Complete tag's full title, the shared tag plate's
/// width guard, the Controls table's ragged columns, the Move List / Systems Card footer over the last line, and the
/// boss bar's PHASE tab against a long name at UI scale 1.4.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class UrPolish2Tests {

    private const string LongTag = "AN EXCEEDINGLY LONG LOCALIZED PANEL TITLE THAT RUNS PAST ANY PANEL EDGE";

    private static async Task Settle(int frames = 6) {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int i = 0; i < frames; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static Rect2 Bounds(Control control) => control.GetGlobalTransform() * new Rect2(Vector2.Zero, control.Size);

    [TestCase]
    public async Task TheResultsTagShowsTheShortLevelNameInsideThePanel() {
        var host = new Node { Name = "UrPolish2Results" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            LevelResultsPanel results = LevelResultsPanel.CreateDefault();
            host.AddChild(results);
            results.ShowResults("gettysburg_level_title", 140, 60, 25, 30, 20, 40, 15, 612f, 2);
            await Settle();
            var panel = results.GetNode<PanelContainer>("Shade/Panel");
            var anchor = panel.GetNode<PanelTagAnchor>(PulpScreenKit.TagAnchorName);
            AssertThat(anchor.TagLabel.Text).IsEqual("LEVEL 11: THE GETTYSBURG BATTLEFIELD");
            if (Bounds(anchor.Tag).End.X > Bounds(panel).End.X) {
                AssertThat($"tag ends at {Bounds(anchor.Tag).End.X}, panel at {Bounds(panel).End.X}").IsEqual("");
            }
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    /// <summary>
    /// TG-WIDGETS (Codex finding 2 of REVIEW-UI-PULP-CODEX-3). The tag clamp rule on fresh panels at both scales (an
    /// overlong tag clamps and ellipsizes, a short one is untouched, the glass tag has no tilt), then the same tag hosts
    /// rescaled live across the clip threshold in both directions: a tag that fits at 1.0 must clamp when the scale
    /// grows under it and come back whole when it shrinks again, for all three hosts (<c>PanelTagAnchor</c>,
    /// <c>PulpTaggedPanel</c>, <c>PausePanel</c>).
    /// </summary>
    [TestCase]
    public async Task AnOverlongTagClampsInsideItsPanelAndFollowsALiveUiScaleChangeBothWays() {
        float original = UIPalette.AppliedUiScale;
        Theme hold = UIPalette.LoadTheme();
        var problems = new List<string>();
        try {
            foreach (float scale in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(scale);
                var host = new Control { Name = "UrPolish2Tags" };
                UIPalette.ApplyTheme(host);
                ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
                try {
                    var anchored = new PanelContainer { Name = "Anchored", ThemeTypeVariation = UIPalette.Variations.InkPanel, Position = new Vector2(100, 100), CustomMinimumSize = new Vector2(500, 200) };
                    anchored.AddChild(new Label { Text = "x" });
                    host.AddChild(anchored);
                    PulpScreenKit.AddTag(anchored, "common_back");
                    var anchor = anchored.GetNode<PanelTagAnchor>(PulpScreenKit.TagAnchorName);
                    await Settle();
                    Vector2 shortSize = anchor.Tag.Size, shortPos = anchor.Tag.Position;
                    PulpScreenKit.AddTag(anchored, LongTag);
                    await Settle();
                    if (Bounds(anchor.Tag).End.X > Bounds(anchored).End.X - PulpTagPlate.RightMargin + 2f)
                        problems.Add($"{scale}: anchored tag {Bounds(anchor.Tag)} past panel {Bounds(anchored)}");
                    if (!anchor.TagLabel.ClipText) problems.Add($"{scale}: anchored label not clipped");
                    PulpScreenKit.AddTag(anchored, "common_back");
                    await Settle();
                    if (anchor.Tag.Size != shortSize || anchor.Tag.Position != shortPos) problems.Add($"{scale}: a short tag moved or resized {shortPos} {shortSize} -> {anchor.Tag.Position} {anchor.Tag.Size}");
                    if (!Mathf.IsZeroApprox(anchor.Tag.Rotation)) problems.Add($"{scale}: the anchored glass tag is tilted {anchor.Tag.RotationDegrees} degrees");

                    var pause = new PausePanel { Name = "Pause", Position = new Vector2(100, 400), CustomMinimumSize = new Vector2(500, 200) };
                    pause.AddChild(new Label { Text = "x" });
                    host.AddChild(pause);
                    pause.TagText = LongTag;
                    await Settle();
                    if (Bounds(pause.Tag).End.X > Bounds(pause).End.X - PulpTagPlate.RightMargin + 2f)
                        problems.Add($"{scale}: pause tag {Bounds(pause.Tag)} past panel {Bounds(pause)}");
                    if (!Mathf.IsZeroApprox(pause.Tag.Rotation)) problems.Add($"{scale}: the pause glass tag is tilted {pause.Tag.RotationDegrees} degrees");
                } finally {
                    host.GetParent()?.RemoveChild(host);
                    host.Free();
                }
            }
            await CheckLiveRescale(problems);
        } finally {
            UIPalette.ApplyUiScale(original);
            System.GC.KeepAlive(hold);
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    private const string ThresholdTag = "A MIDDLING PANEL TITLE";

    /// <summary>One tag host of each kind: the panel it measures against, its tag and the tag's label.</summary>
    private sealed class TagHost {
        public string Kind;
        public Control Panel;
        public Control Tag;
        public Label Label;
    }

    private static TagHost BuildTagHost(Control parent, string kind, float width, Vector2 position) {
        switch (kind) {
            case "anchor": {
                var panel = new PanelContainer { Name = "Anchored", ThemeTypeVariation = UIPalette.Variations.InkPanel, Position = position, CustomMinimumSize = new Vector2(width, 120) };
                panel.AddChild(new Label { Text = "x" });
                parent.AddChild(panel);
                Label label = PulpScreenKit.AddTag(panel, ThresholdTag);
                var anchor = panel.GetNode<PanelTagAnchor>(PulpScreenKit.TagAnchorName);
                return new TagHost { Kind = kind, Panel = panel, Tag = anchor.Tag, Label = label };
            }
            case "tagged": {
                var tagged = new PulpTaggedPanel { TagKey = ThresholdTag, Position = position };
                var panel = new PanelContainer { ThemeTypeVariation = UIPalette.Variations.InkPanel, CustomMinimumSize = new Vector2(width, 120) };
                panel.AddChild(new Label { Text = "x" });
                tagged.AddChild(panel);
                parent.AddChild(tagged);
                return new TagHost { Kind = kind, Panel = tagged, Tag = tagged.Tag, Label = tagged.TagLabel };
            }
            default: {
                var pause = new PausePanel { Name = "Pause", Position = position, CustomMinimumSize = new Vector2(width, 120) };
                pause.AddChild(new Label { Text = "x" });
                parent.AddChild(pause);
                pause.TagText = ThresholdTag;
                return new TagHost { Kind = kind, Panel = pause, Tag = pause.Tag, Label = (Label)pause.Tag.GetChild(0) };
            }
        }
    }

    /// <summary>The tag's left edge relative to its panel's left edge (unrotated).</summary>
    private static float TagLeftInPanel(TagHost host) =>
        host.Tag.GetParent() is Control parent && parent != host.Panel ? parent.Position.X + host.Tag.Position.X : host.Tag.Position.X;

    private static void CheckTag(TagHost host, float panelWidth, bool clamped, float natural, string step, List<string> problems) {
        float right = TagLeftInPanel(host) + host.Tag.Size.X;
        float limit = panelWidth - PulpTagPlate.RightMargin;
        string tag = $"{host.Kind} {step}";
        if (clamped) {
            if (!host.Label.ClipText) problems.Add($"{tag}: the label is not clipped although the tag needs {natural} px");
            if (right > limit + 1f) problems.Add($"{tag}: the tag ends at {right}, past the clamp {limit}");
        } else {
            if (host.Label.ClipText) problems.Add($"{tag}: the label is still clipped although the tag fits");
            if (Mathf.Abs(host.Tag.Size.X - natural) > 1f) problems.Add($"{tag}: the tag is {host.Tag.Size.X} wide, not its natural {natural}");
        }
    }

    /// <summary>
    /// Builds one host of each kind at 1.0 (sequence A) and another at 1.4 (sequence B) with a panel width between the
    /// tag's natural width at the two scales, then flips the scale on the same instances: A 1.0 -> 1.4 -> 1.0 and B
    /// 1.4 -> 1.0 -> 1.4. Natural widths and the tag's offset are measured first on fresh hosts in a wide panel.
    /// </summary>
    private static async Task CheckLiveRescale(List<string> problems) {
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        var host = new Control { Name = "UrPolish2LiveTags" };
        UIPalette.ApplyTheme(host);
        root.AddChild(host);
        try {
            string[] kinds = { "anchor", "tagged", "pause" };
            var natural = new Dictionary<(string, float), float>();
            var left = new Dictionary<string, float>();
            foreach (float scale in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(scale);
                await Settle();
                foreach (string kind in kinds) {
                    TagHost wide = BuildTagHost(host, kind, 2000f, new Vector2(0, 0));
                    await Settle();
                    natural[(kind, scale)] = wide.Tag.Size.X;
                    left[kind] = TagLeftInPanel(wide);
                    wide.Panel.GetParent().RemoveChild(wide.Panel);
                    wide.Panel.Free();
                }
            }
            var width = new Dictionary<string, float>();
            foreach (string kind in kinds) {
                float n10 = natural[(kind, 1f)], n14 = natural[(kind, 1.4f)];
                if (n14 - n10 < 20f) problems.Add($"{kind}: harness: the natural widths {n10} / {n14} are too close to straddle");
                width[kind] = Mathf.Round(left[kind] + PulpTagPlate.RightMargin + (n10 + n14) / 2f);
            }

            UIPalette.ApplyUiScale(1f);
            await Settle();
            var a = new Dictionary<string, TagHost>();
            var b = new Dictionary<string, TagHost>();
            float y = 60f;
            foreach (string kind in kinds) { a[kind] = BuildTagHost(host, kind, width[kind], new Vector2(40, y)); y += 170f; }
            await Settle();
            foreach (string kind in kinds) CheckTag(a[kind], width[kind], false, natural[(kind, 1f)], "A built at 1.0", problems);

            UIPalette.ApplyUiScale(1.4f);
            await Settle();
            foreach (string kind in kinds) CheckTag(a[kind], width[kind], true, natural[(kind, 1.4f)], "A rescaled 1.0 -> 1.4", problems);
            foreach (string kind in kinds) { b[kind] = BuildTagHost(host, kind, width[kind], new Vector2(40, y)); y += 170f; }
            await Settle();
            foreach (string kind in kinds) CheckTag(b[kind], width[kind], true, natural[(kind, 1.4f)], "B built at 1.4", problems);

            UIPalette.ApplyUiScale(1f);
            await Settle();
            foreach (string kind in kinds) CheckTag(a[kind], width[kind], false, natural[(kind, 1f)], "A back 1.4 -> 1.0", problems);
            foreach (string kind in kinds) CheckTag(b[kind], width[kind], false, natural[(kind, 1f)], "B rescaled 1.4 -> 1.0", problems);

            UIPalette.ApplyUiScale(1.4f);
            await Settle();
            foreach (string kind in kinds) CheckTag(b[kind], width[kind], true, natural[(kind, 1.4f)], "B back 1.0 -> 1.4", problems);

            // One theme change in one step: an ancestor adopts a theme carrying the 1.4 sizes (UIPalette.ApplyUiScale sends
            // one change per font size, so a later change can hide a placement made with the label's stale size).
            Theme big = (Theme)UIPalette.LoadTheme().Duplicate();
            UIPalette.ApplyUiScale(1f);
            await Settle();
            var swapUp = new Control { Name = "SwapUp", Position = new Vector2(900, 0) };
            var swapDown = new Control { Name = "SwapDown", Position = new Vector2(900, 540), Theme = big };
            host.AddChild(swapUp);
            host.AddChild(swapDown);
            var up = new Dictionary<string, TagHost>();
            var down = new Dictionary<string, TagHost>();
            y = 0f;
            foreach (string kind in kinds) {
                up[kind] = BuildTagHost(swapUp, kind, width[kind], new Vector2(0, y));
                down[kind] = BuildTagHost(swapDown, kind, width[kind], new Vector2(0, y));
                y += 170f;
            }
            await Settle();
            swapUp.Theme = big;
            swapDown.Theme = null;
            await Settle();
            foreach (string kind in kinds) CheckTag(up[kind], width[kind], true, natural[(kind, 1.4f)], "theme swap 1.0 -> 1.4", problems);
            foreach (string kind in kinds) CheckTag(down[kind], width[kind], false, natural[(kind, 1f)], "theme swap 1.4 -> 1.0", problems);
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    [TestCase]
    public async Task TheControlsColumnsLineUpAndShowEveryDefaultWholeAtBothUiScalesEvenWhenTheScaleChangesWhileOpen() {
        float original = UIPalette.AppliedUiScale;
        Theme hold = UIPalette.LoadTheme();
        var problems = new List<string>();
        var host = new Node { Name = "UrPolish2Settings" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            UIPalette.ApplyUiScale(1f);
            var menu = new SettingsMenu { Name = "SettingsMenu" };
            host.AddChild(menu);
            menu.Show();
            var rows = menu.Tabs.GetNode<VBoxContainer>("Controls/Scroll/Content/ActionRows");
            var scroll = menu.Tabs.GetNode<Control>("Controls/Scroll");
            menu.Tabs.CurrentTab = 3;
            foreach (float scale in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(scale);   // the second pass changes the scale while Settings is open
                await Settle();
                Vector2[] reference = null;
                for (int i = 0; i < rows.GetChildCount(); i++) {
                    var row = (HBoxContainer)rows.GetChild(i);
                    var cells = new Vector2[5];
                    for (int c = 0; c < 5; c++) {
                        var cell = (Control)row.GetChild(c);
                        cells[c] = new Vector2(cell.Position.X, cell.Size.X);
                        string text = cell is Label l ? l.Text : ((Button)cell).Text;
                        float needed = cell.GetThemeFont("font").GetStringSize(text, HorizontalAlignment.Left, -1f, cell.GetThemeFontSize("font_size")).X;
                        if (needed > cell.Size.X - 8f) problems.Add($"{scale}: {row.Name} column {c} '{text}' needs {needed} in {cell.Size.X}");
                    }
                    reference ??= cells;
                    for (int c = 0; c < 5; c++) {
                        if (cells[c] != reference[c]) problems.Add($"{scale}: {row.Name} column {c} {cells[c]} vs {reference[c]}");
                    }
                    if (Bounds(row.GetChild<Control>(4)).End.X > Bounds(scroll).End.X) problems.Add($"{scale}: {row.Name} runs past the tab");
                }
            }
        } finally {
            UIPalette.ApplyUiScale(original);
            host.GetParent()?.RemoveChild(host);
            host.Free();
            System.GC.KeepAlive(hold);
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    [TestCase]
    public async Task TheFocusedFooterPoseClearsTheScrollViewportInBothScreens() {
        var host = new Node { Name = "UrPolish2Footer" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var problems = new List<string>();
        try {
            var list = new MoveListScreen { Name = "ML" };
            host.AddChild(list);
            list.Populate("joan", MoveListScreen.MoveListMode.Fighter);
            list.Visible = true;
            var card = new SystemsCardScreen { Name = "SC" };
            host.AddChild(card);
            card.Visible = true;
            await Settle();
            foreach ((Control root, string tag) in new[] { (list.Root, "movelist"), (card.Root, "systems_card") }) {
                if (root == null) { problems.Add($"{tag}: no root"); continue; }
                var scroll = root.GetNode<Control>("Center/Panel/Layout/Scroll");
                var footer = root.GetNode<Control>("Center/Panel/Layout/FooterRow");
                // The gap itself: the tilted, lifted banner pose plus its plate shadow needs room beyond the pose's own box.
                if (Bounds(footer).Position.Y - Bounds(scroll).End.Y < 28f) problems.Add($"{tag}: only {Bounds(footer).Position.Y - Bounds(scroll).End.Y} px between the scroll and the footer");
                for (int i = 0; i < footer.GetChildCount(); i++) {
                    if (footer.GetChild(i) is not Button button) continue;
                    // The pose: scaled about the centre and tilted, plus the plate's expand margin above the button.
                    float expand = (button.GetThemeStylebox("focus") as StyleBoxTexture)?.ExpandMarginTop ?? 0f;
                    Vector2 c = button.Size / 2f;
                    var pose = Transform2D.Identity.Translated(c)
                        * new Transform2D(Mathf.DegToRad(PulpMotionRules.FocusTiltDegrees), new Vector2(PulpMotionRules.FocusScale, PulpMotionRules.FocusScale), 0f, Vector2.Zero)
                        * Transform2D.Identity.Translated(-c);
                    Rect2 posed = pose * new Rect2(Vector2.Zero, button.Size);
                    float posedTop = Bounds(button).Position.Y + posed.Position.Y - expand;
                    if (posedTop < Bounds(scroll).End.Y) problems.Add($"{tag}: {button.Name} posed top {posedTop} above scroll bottom {Bounds(scroll).End.Y}");
                }
            }
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    /// <summary>Every campaign boss's display-name key: the <c>DisplayNameKey</c> of each top-level <c>BossData</c> resource.</summary>
    private static List<string> BossNameKeys() {
        var keys = new List<string>();
        foreach (string file in DirAccess.GetFilesAt("res://resources/Bosses")) {
            if (!file.EndsWith(".tres")) continue;
            using var access = Godot.FileAccess.Open($"res://resources/Bosses/{file}", Godot.FileAccess.ModeFlags.Read);
            var match = System.Text.RegularExpressions.Regex.Match(access.GetAsText(), "DisplayNameKey = \"([^\"]+)\"");
            if (match.Success) keys.Add(match.Groups[1].Value);
        }
        return keys;
    }

    [TestCase]
    public async Task EveryBossNameAndThePhaseTabStayApartAndInsideThePanelAtBothUiScales() {
        SaveManager save = SaveManager.Instance;
        AssertObject(save?.GlobalData).IsNotNull();
        float original = save.GlobalData.UiScale;
        List<string> keys = BossNameKeys();
        AssertThat(keys.Count).IsEqual(15);
        var problems = new List<string>();
        var ellipsized = new List<string>();
        foreach (float scale in new[] { 1.0f, 1.4f }) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            var port = new SubViewport { Size = new Vector2I(1920, 1080), RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
            tree.Root.AddChild(port);
            try {
                save.GlobalData.UiScale = scale;
                StoryHUD hud = StoryHUD.CreateDefault();
                port.AddChild(hud);
                await Settle(4);
                foreach (string key in keys) {
                    hud.ShowBossBar(key, 640, 1000, new[] { 0.5f });
                    EventBus.Instance.RaiseBossPhaseChanged(2);
                    await Settle(2);
                    var panel = hud.GetNode<Control>("SafeArea/BossPanel");
                    var name = hud.GetNode<Label>("SafeArea/BossPanel/BossName");
                    var tab = hud.GetNode<Control>("SafeArea/BossPanel/BossPhaseTab");
                    var bar = hud.GetNode<Control>("SafeArea/BossPanel/BossBar");
                    // The name tilts -1 degree about its top-left, so its right end rises a little above the panel.
                    Rect2 n = Bounds(name), t = Bounds(tab), b = Bounds(bar), p = Bounds(panel).Grow(16f * scale);
                    string tag = $"{scale}/{key}";
                    if (n.Intersects(t)) problems.Add($"{tag}: name {n} overlaps tab {t}");
                    if (n.Intersects(b)) problems.Add($"{tag}: name overlaps the bar");
                    if (t.Intersects(b)) problems.Add($"{tag}: tab {t} overlaps the bar {b}");
                    if (!p.Encloses(n)) problems.Add($"{tag}: name {n} outside panel {p}");
                    if (!p.Encloses(t)) problems.Add($"{tag}: tab {t} outside panel {p}");
                    if (name.Scale.X < 0.5f - 0.001f) problems.Add($"{tag}: name scale {name.Scale.X} below its floor");
                    if (name.ClipText) ellipsized.Add(tag);
                }
                hud.QueueFree();
            } finally {
                save.GlobalData.UiScale = original;
                port.Free();
            }
        }
        // Ellipsis is the last resort: at most the longest name, and only at 1.4.
        foreach (string tag in ellipsized) if (tag != "1.4/boss_iron_chancellor_name") problems.Add($"{tag}: ellipsized");
        GD.Print($"[UrPolish2] boss names needing the ellipsis: {(ellipsized.Count == 0 ? "none" : string.Join(", ", ellipsized))}");
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    /// <summary>The spec's least gap between two buttons in a horizontal row (UI_SPEC_GLASS 9.1 item 4, plan P1).</summary>
    private const float SpecRowGapFloor = 34f;

    /// <summary>A stylebox's expand margins (left, top, right, bottom); zero for a box without them.</summary>
    private static Vector4 ExpandOf(StyleBox box) => box switch {
        StyleBoxTexture t => new Vector4(t.ExpandMarginLeft, t.ExpandMarginTop, t.ExpandMarginRight, t.ExpandMarginBottom),
        StyleBoxFlat f => new Vector4(f.ExpandMarginLeft, f.ExpandMarginTop, f.ExpandMarginRight, f.ExpandMarginBottom),
        _ => Vector4.Zero
    };

    private static Vector4 Max(Vector4 a, Vector4 b) => new(Mathf.Max(a.X, b.X), Mathf.Max(a.Y, b.Y), Mathf.Max(a.Z, b.Z), Mathf.Max(a.W, b.W));

    /// <summary>The expand of every box a button draws while it is not focused (its state boxes).</summary>
    private static Vector4 StateExpand(Button button) {
        Vector4 grow = Vector4.Zero;
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" }) grow = Max(grow, ExpandOf(button.GetThemeStylebox(state)));
        return grow;
    }

    /// <summary>The axis-aligned bounds of a control's local rect grown by <paramref name="grow"/>, through its current global transform.</summary>
    private static Rect2 Drawn(Control control, Vector4 grow) {
        var local = new Rect2(-grow.X, -grow.Y, control.Size.X + grow.X + grow.Z, control.Size.Y + grow.Y + grow.W);
        Transform2D xform = control.GetGlobalTransform();
        var box = new Rect2(xform * local.Position, Vector2.Zero);
        return box.Expand(xform * new Vector2(local.End.X, local.Position.Y)).Expand(xform * local.End)
            .Expand(xform * new Vector2(local.Position.X, local.End.Y));
    }

    /// <summary>
    /// TG-WIDGETS (Codex finding 1 of REVIEW-UI-PULP-CODEX-3). The drawn bounds of each button of a row while it is focused
    /// and pressed, measured on the real control: its focus overlay and state boxes as the theme (or a local override)
    /// gives them, carried by the real <c>FocusPose</c> frame by frame through the slide (so any tween overshoot is seen)
    /// and the press. The overlay shows from the frame focus lands, before the slide. Each must stay clear of every
    /// neighbour's own drawn plate: its normal box at rest and along its own slide (a neighbour that just lost focus is
    /// still easing back). The rows also keep the spec's 34 px floor.
    /// </summary>
    private static async Task CheckRow(Button[] row, string tag, List<string> problems) {
        var tree = (SceneTree)Engine.GetMainLoop();
        int n = row.Length;
        var rest = new Rect2[n];
        var drawn = new Rect2[n];
        var slide = new float[n];
        for (int i = 0; i < n; i++) rest[i] = Bounds(row[i]);
        for (int i = 0; i + 1 < n; i++) {
            float gap = rest[i + 1].Position.X - rest[i].End.X;
            if (gap < SpecRowGapFloor - 0.5f) problems.Add($"{tag}: {row[i].Name} and {row[i + 1].Name} are {gap} px apart, under the spec's {SpecRowGapFloor}");
        }
        for (int i = 0; i < n; i++) {
            Button button = row[i];
            Vector4 grow = Max(StateExpand(button), ExpandOf(button.GetThemeStylebox("focus")));
            Rect2 union = Drawn(button, grow);
            button.GrabFocus();
            for (int frame = 0; frame < 16; frame++) {
                union = union.Merge(Drawn(button, grow));
                slide[i] = Mathf.Max(slide[i], Bounds(button).Position.X - rest[i].Position.X);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
            button.EmitSignal(BaseButton.SignalName.ButtonDown);
            union = union.Merge(Drawn(button, grow));
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            union = union.Merge(Drawn(button, grow));
            button.EmitSignal(BaseButton.SignalName.ButtonUp);
            for (int frame = 0; frame < 12; frame++) {
                union = union.Merge(Drawn(button, grow));
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
            button.ReleaseFocus();
            await Settle(12);
            drawn[i] = union;
        }
        for (int i = 0; i < n; i++) {
            for (int k = 0; k < n; k++) {
                if (k == i) continue;
                Vector4 own = ExpandOf(row[k].GetThemeStylebox("normal"));
                var plate = new Rect2(rest[k].Position - new Vector2(own.X, own.Y), rest[k].Size + new Vector2(own.X + own.Z, own.Y + own.W));
                plate = plate.Merge(new Rect2(plate.Position + new Vector2(slide[k], 0f), plate.Size));
                if (drawn[i].Intersects(plate)) problems.Add($"{tag}: focused {row[i].Name} draws to {drawn[i]}, over {row[k].Name}'s own plate {plate}");
            }
        }
    }

    /// <summary>A pause-row chevron whose focus overlay reaches farther than the shipped one: 12 px left and 23 px right, the margins the review measured on the pulp plate.</summary>
    private static Button WideOverlayButton(string name, float width) {
        var button = new Button {
            Name = name, Text = "common_back", ThemeTypeVariation = UIPalette.Variations.TemporalGlassButton,
            FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(width, 64f)
        };
        var focus = (StyleBox)UIPalette.LoadTheme().GetStylebox("focus", UIPalette.Variations.TemporalGlassButton).Duplicate();
        switch (focus) {
            case StyleBoxTexture t: t.ExpandMarginLeft = 12f; t.ExpandMarginRight = 23f; break;
            case StyleBoxFlat f: f.ExpandMarginLeft = 12f; f.ExpandMarginRight = 23f; break;
        }
        button.AddThemeStyleboxOverride("focus", focus);
        return button;
    }

    [TestCase]
    public async Task AFocusedButtonInAHorizontalRowClearsItsNeighboursDrawnPlatesAtBothUiScales() {
        float original = UIPalette.AppliedUiScale;
        Theme hold = UIPalette.LoadTheme();
        var problems = new List<string>();
        try {
            foreach (float scale in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(scale);
                var host = new Control { Name = "UrPolish2Rows" };
                host.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                UIPalette.ApplyTheme(host);
                ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
                try {
                    ConfirmModal modal = ConfirmModal.Create("pause_quit_confirm");
                    host.AddChild(modal);
                    var list = new MoveListScreen { Name = "ML" };
                    host.AddChild(list);
                    list.Populate("joan", MoveListScreen.MoveListMode.Fighter);
                    list.Visible = true;
                    // A row whose buttons carry a wider focus overlay than the theme's: the gap must follow the real box.
                    var probe = new HBoxContainer { Name = "Probe", Position = new Vector2(80f, 960f) };
                    host.AddChild(probe);
                    Button[] wide = { WideOverlayButton("WideA", 480f), WideOverlayButton("WideB", 360f) };
                    foreach (Button button in wide) probe.AddChild(button);
                    foreach (Button button in wide) PulpButtonDecor.Apply(button);
                    await Settle();
                    modal.Open();
                    await Settle();
                    // Measure from the rest slots: the opened modal focuses Cancel, so release it first.
                    modal.GetViewport().GuiReleaseFocus();
                    await Settle(12);
                    await CheckRow(new[] { modal.CancelButton, modal.ConfirmButton }, $"{scale} modal", problems);
                    var footer = list.Root.GetNode<Control>("Center/Panel/Layout/FooterRow");
                    var buttons = new List<Button>();
                    for (int i = 0; i < footer.GetChildCount(); i++) if (footer.GetChild(i) is Button b) buttons.Add(b);
                    await CheckRow(buttons.ToArray(), $"{scale} movelist footer", problems);
                    await CheckRow(wide, $"{scale} wide-overlay row", problems);
                } finally {
                    host.GetParent()?.RemoveChild(host);
                    host.Free();
                }
            }
        } finally {
            UIPalette.ApplyUiScale(original);
            System.GC.KeepAlive(hold);
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    [TestCase]
    public void TheQuitConfirmationNamesTheRealExitFee() {
        int fee = 100 - SessionExitGuard.CalculateExitRetainedDust(100);
        string csv = "";
        using (var access = Godot.FileAccess.Open("res://localization/en.csv", Godot.FileAccess.ModeFlags.Read)) csv = access.GetAsText();
        string line = "";
        foreach (string candidate in csv.Split((char)10)) if (candidate.StartsWith("pause_quit_confirm,")) line = candidate;
        AssertThat(line.Length > 0).IsTrue();
        AssertThat(line.Contains($"{fee}%")).OverrideFailureMessage($"pause_quit_confirm must state the {fee}% exit fee: {line}").IsTrue();
    }
}
