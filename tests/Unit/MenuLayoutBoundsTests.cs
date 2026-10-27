using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FTT.Core;
using FTT.FighterSim;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// FU-MENU (2026-10-25): every menu surface sits inside the 1920 x 1080 screen and,
/// where it is centred by design, on its centre, at UI scale 1.0 and at the
/// maximum 1.4. Four code-built panels used SetAnchorsPreset(Center) with no
/// offsets, which parks the control's top-left corner on the screen centre; the
/// Holodeck console's bottom edge then sat at y 1169. The fix is a full-rect
/// CenterContainer parent plus MenuSafeAreaFit (a uniform shrink about the panel
/// centre when its minimum size exceeds the viewport minus a margin), so a tall
/// panel at 1.4 shrinks instead of clipping.
///
/// The sweep measures every visible PanelContainer / Panel outside a
/// ScrollContainer against the screen, through the control's global transform so
/// a MenuSafeAreaFit scale counts. The largest such panel of a surface must also
/// be centred within 2 px unless the surface is on the explicit allow-list of
/// deliberate layouts below.
///
/// TG-CLOSE: where a surface shows the kit's prompt bar (PulpScreenKit.PromptBarName),
/// no panel but the bar's own ancestors comes within MenuSafeAreaFit.PromptGapPixels
/// (10 px) of the bar's real rect, at both scales; and every overlay that clears a bar
/// reserves exactly the clearance measured from that bar's top (one rule, where TG-SCREENS
/// kept a scaled 56 px reserve and TG-PAUSE an unscaled one).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MenuLayoutBoundsTests {

    private const float Tolerance = 1.5f;
    private const int SettleFrames = 20;
    private const float CentreTolerance = 2f;
    private static readonly Rect2 Screen = new(0f, 0f, 1920f, 1080f);

    /// <summary>Deliberate non-centred layouts (surface name or "Surface/state"). Keep this short.</summary>
    private static readonly HashSet<string> LeftAligned = new() {
        "MainMenu/RootScreen",
        "FirstUseTooltip",
    };

    /// <summary>The surfaces whose safe-area fit reserves the room the prompt bar takes (TG-CLOSE).</summary>
    private static readonly HashSet<string> ClearsPromptBar = new() {
        "Settings", "MoveList", "SystemsCard", "ResonanceGrid", "GameOver", "LevelResults", "LocalFighterPause",
        "StoryPause", "HolodeckConsole", "HolodeckConsoleFrontEnd", "TimelineRestart", "DialogueLog",
    };

    private sealed class Surface {
        public string Name;
        public Func<Node> Build;
        public Action<Node> Open;
        public Func<Node, Node> Target;
        public string[] States = { "" };
        public Action<Node, string> SetState;
        public Action Cleanup;
    }

    [TestCase]
    public async Task EveryMenuSurfaceStaysOnScreenAndCentredAtBothUiScales() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var issues = new List<string>();
        GameManager gm = GameManager.Instance;
        SessionData savedSession = gm != null ? gm.CurrentSession : default;
        bool wasPaused = tree.Paused;
        bool? savedBoot = MainMenu.BootSequenceOverrideForTesting;
        bool? savedDev = MainMenu.DeveloperLevelSelectOverrideForTesting;
        float savedScale = UIPalette.AppliedUiScale;
        MainMenu.BootSequenceOverrideForTesting = false;
        try {
            if (gm != null) {
                SessionData session = gm.CurrentSession;
                session.SelectedCharacterID = "einstein";
                session.OpponentCharacterID = "joan";
                gm.CurrentSession = session;
            }
            foreach (float scale in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(scale);
                foreach (Surface surface in Surfaces()) {
                    foreach (string state in surface.States) {
                        var host = new CanvasLayer { Name = "MenuLayoutHost" };
                        tree.Root.AddChild(host);
                        try {
                            Node node = surface.Build();
                            host.AddChild(node);
                            surface.Open?.Invoke(node);
                            surface.SetState?.Invoke(node, state);
                            // Past the panel appear (MOT-A: 0.22 s at the fixed 60 fps), so a panel is measured on its slot,
                            // not mid-rise (TG-CLOSE: the prompt-bar checks read the panel's bottom edge).
                            for (int frame = 0; frame < SettleFrames; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                            string label = state.Length == 0 ? surface.Name : surface.Name + "/" + state;
                            Node target = surface.Target != null ? surface.Target(node) : host;
                            if (target == null) {
                                issues.Add(label + "@" + scale + ": nothing to measure");
                            } else {
                                Measure(target, label, scale, LeftAligned.Contains(label), issues);
                                if (ClearsPromptBar.Contains(label)) MeasurePromptReserve(target, label, scale, issues);
                            }
                        } finally {
                            tree.Paused = wasPaused;
                            if (GodotObject.IsInstanceValid(host)) {
                                host.GetParent()?.RemoveChild(host);
                                host.Free();
                            }
                            surface.Cleanup?.Invoke();
                        }
                    }
                }
            }
        } finally {
            UIPalette.ApplyUiScale(savedScale);
            MainMenu.BootSequenceOverrideForTesting = savedBoot;
            MainMenu.DeveloperLevelSelectOverrideForTesting = savedDev;
            tree.Paused = wasPaused;
            if (gm != null) gm.CurrentSession = savedSession;
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void ThePureFitRuleShrinksOnlyWhatDoesNotFit() {
        var view = new Vector2(1920f, 1080f);
        AssertThat(MenuSafeAreaFit.ScaleFor(new Vector2(640f, 629f), view)).IsEqual(1f);
        float tall = MenuSafeAreaFit.ScaleFor(new Vector2(640f, 1500f), view);
        AssertThat(tall).IsLess(1f);
        AssertThat(1500f * tall).IsLessEqual(1080f - 2f * MenuSafeAreaFit.MarginPixels + 0.01f);
        AssertThat(MenuSafeAreaFit.ScaleFor(new Vector2(640f, 99999f), view)).IsEqual(MenuSafeAreaFit.MinScale);
        AssertThat(MenuSafeAreaFit.ScaleFor(Vector2.Zero, view)).IsEqual(1f);
    }

    [TestCase]
    public void NoCodeBuiltPanelCentresItsAnchorsWithoutOffsetsOrACenterContainer() {
        string root = ProjectSettings.GlobalizePath("res://scripts");
        var offenders = new List<string>();
        var call = new Regex(@"SetAnchorsPreset\(\s*(Control\.)?LayoutPreset\.Center\b");
        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)) {
            string[] lines = File.ReadAllLines(file);
            for (int index = 0; index < lines.Length; index++) {
                if (!call.IsMatch(lines[index])) continue;
                bool fixedUp = false;
                for (int next = index; next < Math.Min(lines.Length, index + 7); next++) {
                    if (lines[next].Contains("Offset") || lines[next].Contains("CenterContainer")) fixedUp = true;
                }
                if (!fixedUp) offenders.Add(Path.GetFileName(file) + ":" + (index + 1));
            }
        }
        if (offenders.Count > 0) AssertThat(string.Join(" | ", offenders)).IsEqual("");
    }

    [TestCase]
    public async Task APanelTallerThanTheScreenIsShrunkToFitAndStaysCentred() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new CanvasLayer { Name = "FitHost" };
        tree.Root.AddChild(host);
        try {
            var center = new CenterContainer { Name = "Center" };
            center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            host.AddChild(center);
            var panel = new PanelContainer { Name = "Panel", CustomMinimumSize = new Vector2(640f, 1500f) };
            center.AddChild(panel);
            MenuSafeAreaFit.Attach(panel);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var issues = new List<string>();
            Measure(host, "FitProbe", 0f, false, issues);
            if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
            AssertThat(center.Scale.X).IsLess(1f);
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    // ---- measurement ----------------------------------------------------------

    private static void Measure(Node target, string label, float scale, bool leftAligned, List<string> issues) {
        var panels = new List<(Control Panel, Rect2 Rect)>();
        Collect(target, panels);
        if (panels.Count == 0) {
            issues.Add(label + "@" + scale + ": no visible panel found");
            return;
        }
        (Control Panel, Rect2 Rect) largest = panels[0];
        foreach (var entry in panels) {
            Rect2 r = entry.Rect;
            if (r.Position.X < Screen.Position.X - Tolerance || r.Position.Y < Screen.Position.Y - Tolerance
                || r.End.X > Screen.End.X + Tolerance || r.End.Y > Screen.End.Y + Tolerance) {
                issues.Add(label + "@" + scale + ": " + entry.Panel.Name + " " + Fmt(r) + " leaves the screen via " + Chain(entry.Panel));
            }
            if (r.Size.X * r.Size.Y > largest.Rect.Size.X * largest.Rect.Size.Y) largest = entry;
        }
        if (FindPromptBar(target) is { } bar && bar.Size.Y > 0f) {
            Rect2 barRect = bar.GetGlobalTransform() * new Rect2(Vector2.Zero, bar.Size);
            foreach (var entry in panels) {
                // The bar itself, what holds it or sits in it, and a full-screen dim or backdrop are not a slab.
                if (entry.Panel == bar || entry.Panel.IsAncestorOf(bar) || bar.IsAncestorOf(entry.Panel)) continue;
                if (entry.Rect.Size.X >= Screen.Size.X - Tolerance && entry.Rect.Size.Y >= Screen.Size.Y - Tolerance) continue;
                if (entry.Rect.Intersects(barRect.Grow(MenuSafeAreaFit.PromptGapPixels - Tolerance))) {
                    issues.Add(label + "@" + scale + ": " + entry.Panel.Name + " " + Fmt(entry.Rect) + " runs within "
                        + MenuSafeAreaFit.PromptGapPixels + " px of the prompt bar " + Fmt(barRect));
                }
            }
        }
        if (leftAligned) return;
        Vector2 centre = largest.Rect.GetCenter();
        if (Mathf.Abs(centre.X - Screen.GetCenter().X) > CentreTolerance
            || Mathf.Abs(centre.Y - Screen.GetCenter().Y) > CentreTolerance) {
            issues.Add(label + "@" + scale + ": " + largest.Panel.Name + " " + Fmt(largest.Rect) + " is off the screen centre");
        }
    }

    // The fit of the surface's open panel reserves exactly what its prompt bar's real top needs.
    private static void MeasurePromptReserve(Node target, string label, float scale, List<string> issues) {
        Control bar = FindPromptBar(target);
        if (bar == null || bar.Size.Y <= 0f) {
            issues.Add(label + "@" + scale + ": no prompt bar to clear");
            return;
        }
        float barTop = (bar.GetGlobalTransform() * new Rect2(Vector2.Zero, bar.Size)).Position.Y;
        float wanted = MenuSafeAreaFit.PromptClearance(Screen.Size.Y, barTop);
        var fits = new List<MenuSafeAreaFit>();
        CollectFits(target, fits);
        if (fits.Count == 0) issues.Add(label + "@" + scale + ": no safe-area fit on the open panel");
        foreach (MenuSafeAreaFit fit in fits) {
            if (Mathf.Abs(fit.ExtraVertical - wanted) > 0.5f) {
                issues.Add(label + "@" + scale + ": " + fit.Target.Name + " reserves " + fit.ExtraVertical.ToString("0.#")
                    + " px, the prompt bar's top at " + barTop.ToString("0.#") + " needs " + wanted.ToString("0.#"));
            }
        }
    }

    private static void CollectFits(Node node, List<MenuSafeAreaFit> found) {
        if (node is MenuSafeAreaFit fit && fit.Target != null && GodotObject.IsInstanceValid(fit.Target) && fit.Target.IsVisibleInTree()) {
            found.Add(fit);
        }
        for (int index = 0; index < node.GetChildCount(); index++) CollectFits(node.GetChild(index), found);
    }

    private static Control FindPromptBar(Node node) {
        if (node is CanvasLayer layer && !layer.Visible) return null;
        if (node is Control control) {
            if (!control.Visible) return null;
            if (control.Name == PulpScreenKit.PromptBarName && control.IsVisibleInTree()) return control;
        }
        for (int index = 0; index < node.GetChildCount(); index++) {
            if (FindPromptBar(node.GetChild(index)) is { } bar) return bar;
        }
        return null;
    }

    private static void Collect(Node node, List<(Control, Rect2)> found) {
        if (node is ScrollContainer) return;
        if (node is CanvasLayer layer && !layer.Visible) return;
        if (node is Control control) {
            if (!control.Visible) return;
            if (control.IsVisibleInTree() && control.Size.X * control.Size.Y >= 2500f && IsPanelLike(control)) {
                found.Add((control, control.GetGlobalTransform() * new Rect2(Vector2.Zero, control.Size)));
            }
        }
        for (int index = 0; index < node.GetChildCount(); index++) Collect(node.GetChild(index), found);
    }

    /// <summary>A framed panel, or a box/grid content block (for surfaces authored without a panel).</summary>
    private static bool IsPanelLike(Control control) =>
        control is PanelContainer || control is Panel
        || control is VBoxContainer || control is HBoxContainer || control is GridContainer;

    private static string Chain(Control control) {
        var parts = new List<string>();
        Node node = control.GetParent();
        for (int depth = 0; depth < 4 && node != null; depth++, node = node.GetParent()) {
            parts.Add(node is Control c ? c.Name + " " + Fmt(new Rect2(c.GlobalPosition, c.Size)) : node.Name + " (not a Control)");
        }
        return string.Join(" < ", parts);
    }

    private static string Fmt(Rect2 r) =>
        "(" + r.Position.X.ToString("0") + "," + r.Position.Y.ToString("0") + " "
        + r.Size.X.ToString("0") + "x" + r.Size.Y.ToString("0") + ")";

    // ---- surfaces -------------------------------------------------------------

    private static IEnumerable<Surface> Surfaces() {
        string[] mainMenuScreens = { "RootScreen", "SlotScreen", "CharacterScreen", "DifficultyScreen",
            "LevelSelectScreen", "ExtrasScreen", "LicensesScreen" };
        yield return new Surface {
            Name = "MainMenu",
            Build = () => ResourceLoader.Load<PackedScene>("res://scenes/menus/MainMenu.tscn").Instantiate(),
            States = mainMenuScreens,
            SetState = (node, state) => {
                foreach (string screen in mainMenuScreens) {
                    if (node.GetNodeOrNull<Control>(screen) is { } c) c.Visible = screen == state;
                }
            }
        };
        yield return Scene("CharacterSelect", "res://scenes/menus/CharacterSelect.tscn");
        yield return Scene("FighterPlayOptions", "res://scenes/menus/FighterPlayOptions.tscn");
        yield return Scene("NetworkSelect", "res://scenes/menus/NetworkSelect.tscn");
        yield return Scene("CalibrationDrillPicker", "res://scenes/ui/CalibrationDrillPicker.tscn");
        yield return Scene("CalibrationDrillList", "res://scenes/ui/CalibrationDrillList.tscn");
        yield return new Surface {
            Name = "Settings",
            Build = () => new SettingsMenu { Name = "SettingsMenu" },
            Open = node => ((SettingsMenu)node).Show()
        };
        yield return new Surface {
            Name = "MoveList",
            Build = () => new MoveListScreen { Name = "MoveList" },
            Open = node => ((MoveListScreen)node).Open("einstein")
        };
        yield return new Surface {
            Name = "SystemsCard",
            Build = () => new SystemsCardScreen { Name = "SystemsCard" },
            Open = node => ((SystemsCardScreen)node).Open()
        };
        yield return new Surface {
            Name = "ResonanceGrid",
            Build = () => new ResonanceGridPanel { Name = "ResonanceGrid" }
        };
        yield return new Surface {
            Name = "GameOver",
            Build = () => GameOverScreen.CreateDefault()
        };
        yield return new Surface {
            Name = "LevelResults",
            Build = () => LevelResultsPanel.CreateDefault(),
            Open = node => ((LevelResultsPanel)node).ShowResults("campaign_level_01", 120, 45f, 1)
        };
        yield return new Surface {
            Name = "MatchResults",
            Build = () => new MatchResults { Name = "MatchResults" },
            Open = node => ((MatchResults)node).ShowResult(
                new FighterMatchResult(winnerPlayerID: 1, isTrueTie: false, completedTick: 900, finalHash: 7L))
        };
        yield return new Surface {
            Name = "LocalFighterPause",
            Build = () => new LocalFighterPause { Name = "LocalFighterPause" },
            Open = node => ((LocalFighterPause)node).SetPaused(true)
        };
        yield return new Surface {
            Name = "StoryPause",
            Build = () => PauseMenu.CreateDefault(),
            Open = node => ((PauseMenu)node).SetPaused(true)
        };
        yield return new Surface {
            Name = "HolodeckConsole",
            Build = () => new HolodeckConsolePanel { Name = "HolodeckConsolePanel" }
        };
        yield return new Surface {
            Name = "HolodeckConsoleFrontEnd",
            Build = () => new HolodeckConsolePanel { Name = "HolodeckConsolePanel", FrontEnd = true }
        };
        yield return new Surface {
            Name = "TimelineRestart",
            Build = () => new TimelineRestartPanel { Name = "TimelineRestartPanel" }
        };
        yield return new Surface {
            Name = "ConfirmModal",
            Build = () => ConfirmModal.Create("pause_quit_confirm"),
            Open = node => ((ConfirmModal)node).Open()
        };
        yield return new Surface {
            Name = "DialogueLog",
            Build = () => new DialogueLogScreen { Name = "DialogueLog" },
            Open = node => ((DialogueLogScreen)node).Open()
        };
        yield return new Surface {
            Name = "BootSequence",
            Build = () => {
                MainMenu.BootSequenceOverrideForTesting = true;
                return ResourceLoader.Load<PackedScene>("res://scenes/menus/MainMenu.tscn").Instantiate();
            },
            Target = node => (node as MainMenu)?.BootOverlay,
            Cleanup = () => MainMenu.BootSequenceOverrideForTesting = false
        };
        yield return new Surface {
            Name = "FirstUseTooltip",
            Build = () => new FirstUseTooltipPresenter { Name = "FirstUseTooltips" },
            Open = node => {
                var panel = node.GetNodeOrNull<Control>("TooltipPanel");
                if (panel == null) return;
                panel.AddChild(new Label { Text = "Tooltip text" });
                panel.Visible = true;
            }
        };
    }

    private static Surface Scene(string name, string path) => new() {
        Name = name,
        Build = () => ResourceLoader.Load<PackedScene>(path).Instantiate()
    };
}
