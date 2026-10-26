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
    public async Task TheControlsColumnsLineUpWhateverTheBindingTextAtBothUiScales() {
        float original = UIPalette.AppliedUiScale;
        Theme hold = UIPalette.LoadTheme();
        var problems = new List<string>();
        try {
            foreach (float scale in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(scale);
                var host = new Node { Name = "UrPolish2Settings" };
                ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
                try {
                    var menu = new SettingsMenu { Name = "SettingsMenu" };
                    host.AddChild(menu);
                    menu.Show();
                    var rows = menu.Tabs.GetNode<VBoxContainer>("Controls/Scroll/Content/ActionRows");
                    string action = InputManager.Actions.Roll;
                    menu.CaptureForTesting(action, InputDeviceKind.Joypad, new InputBindingEvent(InputBindingKind.JoyButton, 13));
                    menu.CaptureForTesting(InputManager.Actions.Jump, InputDeviceKind.Keyboard, new InputBindingEvent(InputBindingKind.Key, (int)Key.F9));
                    await Settle();
                    Vector2[] reference = null;
                    for (int i = 0; i < rows.GetChildCount(); i++) {
                        var row = (HBoxContainer)rows.GetChild(i);
                        var cells = new Vector2[5];
                        for (int c = 0; c < 5; c++) {
                            var cell = (Control)row.GetChild(c);
                            cells[c] = new Vector2(cell.Position.X, cell.Size.X);
                        }
                        reference ??= cells;
                        for (int c = 0; c < 5; c++) {
                            if (cells[c] != reference[c]) problems.Add($"{scale}: {row.Name} column {c} {cells[c]} vs {reference[c]}");
                        }
                    }
                    var slot = (Button)rows.GetNode<HBoxContainer>($"Row_{action}").GetChild(2);
                    if (slot.TooltipText != slot.Text || slot.Text.Length == 0) problems.Add($"{scale}: tooltip '{slot.TooltipText}' != '{slot.Text}'");
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

    [TestCase]
    public async Task TheBossNameAndThePhaseTabNeverOverlapForTheLongestNameAtBothUiScales() {
        SaveManager save = SaveManager.Instance;
        AssertObject(save?.GlobalData).IsNotNull();
        float original = save.GlobalData.UiScale;
        var problems = new List<string>();
        foreach (float scale in new[] { 1.0f, 1.4f }) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            var port = new SubViewport { Size = new Vector2I(1920, 1080), RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
            tree.Root.AddChild(port);
            try {
                save.GlobalData.UiScale = scale;
                StoryHUD hud = StoryHUD.CreateDefault();
                port.AddChild(hud);
                await Settle(4);
                hud.ShowBossBar("boss_iron_chancellor_name", 640, 1000, new[] { 0.5f });
                EventBus.Instance.RaiseBossPhaseChanged(2);
                await Settle(3);
                var panel = hud.GetNode<Control>("SafeArea/BossPanel");
                var name = hud.GetNode<Control>("SafeArea/BossPanel/BossName");
                var tab = hud.GetNode<Control>("SafeArea/BossPanel/BossPhaseTab");
                Rect2 n = Bounds(name), t = Bounds(tab), p = Bounds(panel).Grow(16f * scale);
                if (n.Intersects(t)) problems.Add($"{scale}: name {n} overlaps tab {t}");
                if (!p.Encloses(n)) problems.Add($"{scale}: name {n} outside panel {p}");
                if (!p.Encloses(t)) problems.Add($"{scale}: tab {t} outside panel {p}");
                if (name.Scale.X < 0.5f - 0.001f) problems.Add($"{scale}: name scale {name.Scale.X} below its floor");
                hud.QueueFree();
            } finally {
                save.GlobalData.UiScale = original;
                port.Free();
            }
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }
}
