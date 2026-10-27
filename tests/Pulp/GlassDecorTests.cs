using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Combat;
using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>
/// TG-WIDGETS. The glass draw parts: the sheen and streaks scale with the panel (UI_SPEC_GLASS 3.1), the screen streak's
/// bands sit where 1.4 puts them, every title label gets its own gradient material that follows the UI scale (6.5), and a
/// redraw without a change rebuilds no geometry in any step-1 draw widget (standing decision 11).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GlassDecorTests {

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    private static async Task Frames(int count = 3) {
        for (int i = 0; i < count; i++) await Tree.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>The width of a band polygon measured along the gradient direction.</summary>
    private static float Width(Vector2[] polygon, Vector2 direction) {
        float min = float.MaxValue, max = float.MinValue;
        foreach (Vector2 p in polygon) {
            float v = p.Dot(direction);
            min = Mathf.Min(min, v);
            max = Mathf.Max(max, v);
        }
        return max - min;
    }

    private static GlassSheen SheenOn(Control host, Vector2 size) {
        var panel = new Control { Size = size };
        host.AddChild(panel);
        return GlassSheen.Attach(panel);
    }

    [TestCase]
    public async Task TheSheenAndItsStreakBandsScaleWithThePanel() {
        var host = new Control { Name = "SheenHost" };
        Tree.Root.AddChild(host);
        var problems = new List<string>();
        try {
            GlassSheen vitals = SheenOn(host, new Vector2(620f, 214f));
            GlassSheen pause = SheenOn(host, new Vector2(600f, 768.6f));
            await Frames();
            Vector2 d = GlassGeometry.GradientDirection(GlassSheen.Degrees);
            // 3.1: the bands are hard-edged strips of the 112 degree line: 3.3 / 2.6 px on the Vitals panel, 4.2 / 3.4 on the pause slab.
            (GlassSheen sheen, float band1, float band2, string name)[] cases = { (vitals, 3.3f, 2.6f, "vitals"), (pause, 4.2f, 3.4f, "pause") };
            foreach (var c in cases) {
                float w1 = Width(c.sheen.Band1Polygon, d), w2 = Width(c.sheen.Band2Polygon, d);
                if (Mathf.Abs(w1 - c.band1) > 0.1f) problems.Add($"{c.name} streak 1 is {w1} px, not {c.band1}");
                if (Mathf.Abs(w2 - c.band2) > 0.1f) problems.Add($"{c.name} streak 2 is {w2} px, not {c.band2}");
                Rect2 box = c.sheen.FillBox;
                foreach (Vector2 p in c.sheen.SheenPolygon) {
                    if (GlassGeometry.GradientT(p, box, GlassSheen.Degrees) > GlassSheen.SheenEnd + 1e-3f) problems.Add($"{c.name} sheen reaches t {GlassGeometry.GradientT(p, box, GlassSheen.Degrees)}");
                }
                // Clipped to the fill's chamfer: no point inside the cut top-left corner.
                foreach (Vector2[] polygon in new[] { c.sheen.SheenPolygon, c.sheen.Band1Polygon, c.sheen.Band2Polygon }) {
                    if (polygon.Length < 3) problems.Add($"{c.name}: an empty band");
                    foreach (Vector2 p in polygon) {
                        if (p.X + p.Y < 1.5f + 17f + 1.5f - 0.01f) problems.Add($"{c.name}: {p} lies in the cut corner");
                    }
                }
            }
            // A resized panel moves its bands with it.
            int builds = vitals.GeometryBuilds;
            ((Control)vitals.GetParent()).Size = new Vector2(800f, 214f);
            await Frames();
            AssertThat(vitals.GeometryBuilds).IsEqual(builds + 1);
            float length = GlassGeometry.GradientLength(new Vector2(797f, 211f), GlassSheen.Degrees);
            float wide = Width(vitals.Band1Polygon, d);
            if (Mathf.Abs(wide - 0.005f * length) > 0.05f) problems.Add($"the resized streak is {wide} px, not {0.005f * length}");
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    private static (float min, float max) XRangeAt(Vector2[] polygon, float y) {
        // The polygon's horizontal extent on the line y (its edges crossing it).
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < polygon.Length; i++) {
            Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
            if (Mathf.IsEqualApprox(a.Y, y)) { min = Mathf.Min(min, a.X); max = Mathf.Max(max, a.X); }
            if ((a.Y - y) * (b.Y - y) < 0f) {
                float x = a.X + (b.X - a.X) * (y - a.Y) / (b.Y - a.Y);
                min = Mathf.Min(min, x);
                max = Mathf.Max(max, x);
            }
        }
        return (min, max);
    }

    [TestCase]
    public async Task TheScreenStreakBandsSitWhereSpecOnePointFourPutsThem() {
        var streak = new ScreenStreak { Size = new Vector2(1920f, 1080f) };
        Tree.Root.AddChild(streak);
        var problems = new List<string>();
        try {
            await Frames();
            streak.Size = new Vector2(1920f, 1080f);
            (Vector2[] band, float top0, float top1, float bottom0, float bottom1, string name)[] cases = {
                (streak.Band1Polygon, 1721.0f, 1727.3f, 1146.8f, 1153.0f, "band 1"),
                (streak.Band2Polygon, 1833.3f, 1838.3f, 1259.0f, 1264.0f, "band 2")
            };
            foreach (var c in cases) {
                (float t0, float t1) = XRangeAt(c.band, 0f);
                (float b0, float b1) = XRangeAt(c.band, 1080f);
                if (Mathf.Abs(t0 - c.top0) > 0.2f || Mathf.Abs(t1 - c.top1) > 0.2f) problems.Add($"{c.name} at the top spans {t0}-{t1}, not {c.top0}-{c.top1}");
                if (Mathf.Abs(b0 - c.bottom0) > 0.2f || Mathf.Abs(b1 - c.bottom1) > 0.2f) problems.Add($"{c.name} at the bottom spans {b0}-{b1}, not {c.bottom0}-{c.bottom1}");
            }
            AssertThat(streak.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
            AssertThat(ScreenStreak.Band1Color.A).IsEqual(0.04f);
            AssertThat(ScreenStreak.Band2Color.A).IsEqual(0.022f);
            // On a menu root the kit adds it once, last, above the screen's content.
            var root = new Control { Name = "MenuRoot", Size = new Vector2(1920f, 1080f) };
            root.AddChild(new Control { Name = "Content" });
            Tree.Root.AddChild(root);
            try {
                ScreenStreak added = PulpScreenKit.AddScreenStreak(root);
                AssertThat(PulpScreenKit.AddScreenStreak(root)).IsEqual(added);
                AssertThat(root.GetChild(root.GetChildCount() - 1)).IsEqual(added);
                AssertThat(added.ZIndex > 0).IsTrue();
            } finally {
                root.Free();
            }
        } finally {
            streak.Free();
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    [TestCase]
    public async Task EveryTitleLabelGetsItsOwnGradientMaterialThatFollowsTheUiScale() {
        float original = UIPalette.AppliedUiScale;
        Theme hold = UIPalette.LoadTheme();
        var host = new Control { Name = "TitleHost" };
        UIPalette.ApplyTheme(host);
        Tree.Root.AddChild(host);
        try {
            UIPalette.ApplyUiScale(1f);
            var first = new Label { Text = "FIGHTERS", ThemeTypeVariation = UIPalette.LabelRoles.CoverTitle };
            var second = new Label { Text = "TIME", ThemeTypeVariation = UIPalette.LabelRoles.CoverTitle, Position = new Vector2(0f, 200f) };
            host.AddChild(first);
            host.AddChild(second);
            TitleGradient a = TitleGradient.Attach(first);
            TitleGradient b = TitleGradient.Attach(second);
            AssertThat(TitleGradient.Attach(first)).IsEqual(a);
            await Frames();
            AssertObject(a.Material).IsNotNull();
            AssertThat(ReferenceEquals(a.Material, b.Material)).IsFalse();
            AssertThat(ReferenceEquals(first.Material, a.Material)).IsTrue();
            AssertThat(a.Material.Shader.ResourcePath).IsEqual(TitleGradient.ShaderPath);
            // The label draws white under the shader (6.5); the shader multiplies it.
            AssertThat(first.GetThemeColor("font_color")).IsEqual(Colors.White);
            int size = first.GetThemeFontSize("font_size");
            AssertThat(Mathf.IsEqualApprox((float)a.Material.GetShaderParameter("line_height"), TitleGradient.LineHeightRatio * size)).IsTrue();
            // The line box (0.94 of the size) is centred on the font's own line, which a top-aligned label starts at its top padding.
            float fontHeight = first.GetThemeFont("font").GetHeight(size);
            float padding = first.GetThemeStylebox("normal")?.GetContentMargin(Side.Top) ?? 0f;
            float expectedTop = padding + (fontHeight - TitleGradient.LineHeightRatio * size) / 2f;
            AssertThat(first.VerticalAlignment).IsEqual(VerticalAlignment.Top);
            AssertThat(Mathf.Abs((float)a.Material.GetShaderParameter("line_top") - expectedTop) < 0.5f)
                .OverrideFailureMessage($"line_top {a.Material.GetShaderParameter("line_top")}, expected {expectedTop}").IsTrue();

            UIPalette.ApplyUiScale(1.4f);
            await Frames(4);
            int scaled = first.GetThemeFontSize("font_size");
            AssertThat(scaled > size).IsTrue();
            AssertThat(Mathf.IsEqualApprox((float)a.Material.GetShaderParameter("line_height"), TitleGradient.LineHeightRatio * scaled))
                .OverrideFailureMessage($"line_height {a.Material.GetShaderParameter("line_height")} at {scaled} px").IsTrue();
            AssertThat(Mathf.IsEqualApprox((float)b.Material.GetShaderParameter("line_height"), TitleGradient.LineHeightRatio * scaled)).IsTrue();
        } finally {
            UIPalette.ApplyUiScale(original);
            host.GetParent()?.RemoveChild(host);
            host.Free();
            System.GC.KeepAlive(hold);
        }
    }

    /// <summary>
    /// Codex review 1, finding 4: an owned select tile reads the owner colours from the active player-slot palette, so a
    /// palette change recolours its edge (and glow) on the same instance, as a tile built fresh under that palette.
    /// </summary>
    [TestCase]
    public async Task AnOwnedTileTakesTheNewOwnerColoursWhenThePlayerSlotPaletteChanges() {
        GlobalSaveData data = SaveManager.Instance?.GlobalData;
        if (data == null) AssertThat("GlobalData is missing").IsEqual("");
        int saved = data.PlayerSlotPalette;
        var host = new Control { Name = "PaletteHost", Size = new Vector2(800f, 400f) };
        UIPalette.ApplyTheme(host);
        Tree.Root.AddChild(host);
        var problems = new List<string>();
        try {
            data.PlayerSlotPalette = (int)PlayerSlotPalette.Default;
            var tileHost = new Control { Size = new Vector2(164f, 164f) };
            host.AddChild(tileHost);
            GlassTileDecor tile = GlassTileDecor.Attach(tileHost);
            tile.Owners = GlassTileDecor.OwnerP1;
            await Frames(2);
            Color[] before = (Color[])tile.EdgeColors.Clone();
            int other = -1;
            for (int p = 0; p < PlayerSlotPalettes.Count; p++) {
                if (!PlayerSlotPalettes.SlotColor((PlayerSlotPalette)p, 0).IsEqualApprox(PlayerSlotPalettes.SlotColor(PlayerSlotPalette.Default, 0))) {
                    other = p;
                    break;
                }
            }
            AssertThat(other >= 0).OverrideFailureMessage("Harness: no palette gives player 1 another colour").IsTrue();
            data.PlayerSlotPalette = other;
            tile.QueueRedraw();
            await Frames(2);
            var freshHost = new Control { Size = new Vector2(164f, 164f), Position = new Vector2(300f, 0f) };
            host.AddChild(freshHost);
            GlassTileDecor fresh = GlassTileDecor.Attach(freshHost);
            fresh.Owners = GlassTileDecor.OwnerP1;
            await Frames(2);
            Color[] after = tile.EdgeColors, expected = fresh.EdgeColors;
            for (int i = 0; i < after.Length; i++) {
                if (!after[i].IsEqualApprox(expected[i])) problems.Add($"vertex {i} is {after[i]}, a fresh tile draws {expected[i]}");
            }
            bool changed = false;
            for (int i = 0; i < after.Length; i++) changed |= !after[i].IsEqualApprox(before[i]);
            if (!changed) problems.Add("the edge kept the old palette's colours");
        } finally {
            data.PlayerSlotPalette = saved;
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    [TestCase]
    public async Task ARedrawWithoutAChangeRebuildsNoGeometryInAnyStepOneDrawWidget() {
        var host = new Control { Name = "CacheHost", Size = new Vector2(1920f, 1080f) };
        UIPalette.ApplyTheme(host);
        Tree.Root.AddChild(host);
        try {
            GlassSheen sheen = SheenOn(host, new Vector2(620f, 214f));
            var streak = new ScreenStreak { Size = new Vector2(1920f, 1080f) };
            host.AddChild(streak);
            var flourish = new GlassFlourish { Size = new Vector2(700f, 11f) };
            host.AddChild(flourish);
            var tileHost = new Control { Size = new Vector2(164f, 164f) };
            host.AddChild(tileHost);
            GlassTileDecor tile = GlassTileDecor.Attach(tileHost);
            var frame = new PulpPortraitRoundel { Diameter = 112f };
            host.AddChild(frame);
            var ring = new GlassPromptRing { Size = new Vector2(36f, 36f) };
            host.AddChild(ring);
            var toggleButton = new CheckButton { Text = "x", CustomMinimumSize = new Vector2(500f, 52f) };
            host.AddChild(toggleButton);
            PulpToggleDecor toggle = PulpToggleDecor.Apply(toggleButton);
            var button = new Button { Text = "menu_settings", ThemeTypeVariation = UIPalette.Variations.PulpButton, CustomMinimumSize = new Vector2(560f, 68f) };
            host.AddChild(button);
            PulpButtonDecor decor = PulpButtonDecor.Apply(button);
            await Frames(4);

            int[] Counts() => new[] {
                sheen.GeometryBuilds, streak.GeometryBuilds, flourish.GeometryBuilds, tile.GeometryBuilds, frame.GeometryBuilds,
                ring.GeometryBuilds, toggle.GeometryBuilds, decor.LayoutBuilds, ((GlassMarker)decor.Marker).GeometryBuilds
            };
            string[] names = { "sheen", "streak", "flourish", "tile", "portrait", "ring", "toggle", "button label", "marker" };
            int[] before = Counts();
            for (int i = 0; i < before.Length; i++) {
                if (before[i] < 1) AssertThat($"{names[i]} never built its geometry").IsEqual("");
            }
            for (int pass = 0; pass < 3; pass++) {
                foreach (CanvasItem item in new CanvasItem[] { sheen, streak, flourish, tile, frame, ring, toggle.Layer, button, decor.Marker }) item.QueueRedraw();
                decor.Refresh();
                await Frames(2);
            }
            // A state change recolours without rebuilding: an owner on the tile, focus on the button.
            tile.Owners = GlassTileDecor.OwnerP1;
            button.GrabFocus();
            await Frames(3);
            int[] after = Counts();
            // Focus shows the badge, which narrows the label's room once: the label may re-lay out, nothing else may.
            for (int i = 0; i < before.Length; i++) {
                int allowed = names[i] == "button label" ? 1 : 0;
                if (after[i] - before[i] > allowed) AssertThat($"{names[i]} rebuilt {after[i] - before[i]} times without a size change").IsEqual("");
            }
            int focusedLabel = decor.LayoutBuilds;
            for (int pass = 0; pass < 3; pass++) {
                button.QueueRedraw();
                decor.Refresh();
                await Frames(2);
            }
            AssertThat(decor.LayoutBuilds).IsEqual(focusedLabel);
            // A real size change rebuilds exactly once.
            int tileBuilds = tile.GeometryBuilds;
            tileHost.Size = new Vector2(200f, 164f);
            await Frames(2);
            tile.QueueRedraw();
            await Frames(2);
            AssertThat(tile.GeometryBuilds).IsEqual(tileBuilds + 1);
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    /// <summary>
    /// TG-CLOSE (TG-SCREENS kit gap): the title gradient repeats on every line of a wrapped title (it spanned the first
    /// line box only, so a second line drew flat in the gradient's last colour), and the credits title therefore wraps
    /// inside its 1100 px column instead of widening it past the screen at 1.4.
    /// </summary>
    [TestCase]
    public async Task AWrappedTitleRepeatsItsGradientOnEveryLineAndTheCreditsTitleWraps() {
        float original = UIPalette.AppliedUiScale;
        Theme hold = UIPalette.LoadTheme();
        var host = new Control { Name = "WrappedTitleHost" };
        UIPalette.ApplyTheme(host);
        Tree.Root.AddChild(host);
        var creditsHost = new Node { Name = "WrappedCreditsHost" };
        try {
            UIPalette.ApplyUiScale(1f);
            var label = new Label {
                Text = "FIGHTERS THROUGH TIME", ThemeTypeVariation = UIPalette.LabelRoles.PauseTitle,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(620f, 0f)
            };
            host.AddChild(label);
            TitleGradient gradient = TitleGradient.Attach(label);
            await Frames(4);
            AssertObject(gradient.Material).IsNotNull();
            int lines = label.GetLineCount();
            AssertThat(lines >= 2).OverrideFailureMessage($"Harness: the title did not wrap ({lines} line)").IsTrue();
            int size = label.GetThemeFontSize("font_size");
            float pitch = label.GetThemeFont("font").GetHeight(size) + label.GetThemeConstant("line_spacing");
            AssertThat((int)gradient.Material.GetShaderParameter("line_count"))
                .OverrideFailureMessage($"line_count {gradient.Material.GetShaderParameter("line_count")}, the label has {lines} lines").IsEqual(lines);
            AssertThat(Mathf.Abs((float)gradient.Material.GetShaderParameter("line_pitch") - pitch) < 0.01f)
                .OverrideFailureMessage($"line_pitch {gradient.Material.GetShaderParameter("line_pitch")}, the lines are {pitch} apart").IsTrue();

            // The credits roll at 1.4: the title wraps inside the column, and its gradient knows every line.
            UIPalette.ApplyUiScale(1.4f);
            var credits = ResourceLoader.Load<PackedScene>("res://scenes/ui/Credits.tscn").Instantiate<CanvasLayer>();
            Tree.Root.AddChild(creditsHost);
            creditsHost.AddChild(credits);
            await Frames(4);
            var column = credits.GetNode<Control>("Shade/Scroll/Lines");
            var title = column.GetNode<Label>("CreditLine_0");
            AssertThat(title.AutowrapMode != TextServer.AutowrapMode.Off).OverrideFailureMessage("the credits title does not wrap").IsTrue();
            AssertThat(column.Size.X <= 1100.5f).OverrideFailureMessage($"the title widened the 1100 px column to {column.Size.X}").IsTrue();
            var titleGradient = title.GetNodeOrNull<TitleGradient>(TitleGradient.NodeName);
            AssertObject(titleGradient).IsNotNull();
            AssertThat((int)titleGradient.Material.GetShaderParameter("line_count")).IsEqual(title.GetLineCount());
        } finally {
            UIPalette.ApplyUiScale(original);
            host.GetParent()?.RemoveChild(host);
            host.Free();
            creditsHost.GetParent()?.RemoveChild(creditsHost);
            creditsHost.Free();
            System.GC.KeepAlive(hold);
        }
    }
}
