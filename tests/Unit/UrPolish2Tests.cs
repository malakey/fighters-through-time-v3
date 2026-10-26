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

    [TestCase]
    public async Task AnOverlongTagClampsInsideItsPanelAtBothUiScalesAndAShortOneIsUntouched() {
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

                    var pause = new PausePanel { Name = "Pause", Position = new Vector2(100, 400), CustomMinimumSize = new Vector2(500, 200) };
                    pause.AddChild(new Label { Text = "x" });
                    host.AddChild(pause);
                    pause.TagText = LongTag;
                    await Settle();
                    if (Bounds(pause.Tag).End.X > Bounds(pause).End.X - PulpTagPlate.RightMargin + 2f)
                        problems.Add($"{scale}: pause tag {Bounds(pause.Tag)} past panel {Bounds(pause)}");
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
}
