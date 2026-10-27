using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Core;
using FTT.FighterSim;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// TG-SCREENS (UI_SPEC_GLASS 9.1 item 1, AC19, plan 0.4 P3). The remaining screens come in two types. A full-screen menu
/// (the Fighter play options, the network select, the Calibration picker and list, the Resonance grid, the credits and
/// the three loading screens) carries its art (the sub-screen art, or the <c>bg0</c> field of loading and credits) covering
/// the screen and the <c>dir-a</c> screen streak. An overlay over gameplay (the level and match results, Game Over, the
/// timeline restart choice, the drill result card) carries the build dim (rgba(3,5,14,.70); Game Over .82) covering the
/// screen and never the streak, because a static line over gameplay reads as a screen fault.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ScreenTypeDressingTests {

    private static readonly Rect2 Screen = new(0f, 0f, 1920f, 1080f);
    private const string SubmenuArt = "res://assets/backgrounds/menu/temporal_glass_submenu_background.png";

    private enum Field { SubmenuArt, Backdrop }

    private sealed class Surface {
        public string Name;
        public Func<Node> Build;
        public Action<Node> Open;
        public Field Art;
        public Color Dim;
    }

    [TestCase]
    public async Task EveryFullScreenMenuCarriesItsArtAndTheStreakAndEveryOverlayTheDimAndNoStreak() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GameManager gm = GameManager.Instance;
        SessionData saved = gm != null ? gm.CurrentSession : default;
        bool wasPaused = tree.Paused;
        var problems = new List<string>();
        try {
            if (gm != null) {
                SessionData session = gm.CurrentSession;
                session.SelectedCharacterID = "joan";
                session.OpponentCharacterID = "lincoln";
                session.ActiveSaveSlot = -1;
                session.FighterMatchOrigin = FighterMatchOrigin.LocalVersus;
                gm.CurrentSession = session;
            }
            foreach (Surface surface in FullScreenMenus()) {
                await Inspect(tree, surface, node => {
                    if (!HasField(node, surface.Art)) problems.Add($"{surface.Name}: no {surface.Art} covering the screen");
                    if (!HasStreak(node)) problems.Add($"{surface.Name}: no screen streak");
                });
            }
            foreach (Surface surface in Overlays()) {
                await Inspect(tree, surface, node => {
                    if (!HasDim(node, surface.Dim)) problems.Add($"{surface.Name}: no {surface.Dim} dim covering the screen");
                    if (HasStreak(node)) problems.Add($"{surface.Name}: an overlay carries the screen streak");
                });
            }
            // The drill result card is an overlay inside the drill sandbox: its dim is authored beside the card.
            Node drill = ResourceLoader.Load<PackedScene>("res://scenes/ui/HolodeckDrill.tscn").Instantiate();
            try {
                var dim = drill.GetNodeOrNull<ColorRect>("DrillLayer/Overlay/ResultDim");
                if (dim == null || !Near(dim.Color, UIPalette.PauseDim)) problems.Add("HolodeckDrill: the result card has no .70 dim");
                if (dim != null && dim.GetIndex() > drill.GetNode("DrillLayer/Overlay/ResultCard").GetIndex()) {
                    problems.Add("HolodeckDrill: the result dim sits over its card");
                }
                if (HasStreak(drill)) problems.Add("HolodeckDrill: the drill carries the screen streak");
            } finally {
                drill.Free();
            }
        } finally {
            tree.Paused = wasPaused;
            if (gm != null) gm.CurrentSession = saved;
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    private static IEnumerable<Surface> FullScreenMenus() {
        yield return Scene("FighterPlayOptions", "res://scenes/menus/FighterPlayOptions.tscn", Field.SubmenuArt);
        yield return Scene("NetworkSelect", "res://scenes/menus/NetworkSelect.tscn", Field.SubmenuArt);
        yield return Scene("CalibrationDrillPicker", "res://scenes/ui/CalibrationDrillPicker.tscn", Field.SubmenuArt);
        yield return Scene("CalibrationDrillList", "res://scenes/ui/CalibrationDrillList.tscn", Field.SubmenuArt);
        yield return new Surface { Name = "ResonanceGrid", Build = () => new ResonanceGridPanel { Name = "ResonanceGrid" }, Art = Field.SubmenuArt };
        yield return new Surface { Name = "Credits", Build = () => CreditsController.CreateDefault(), Art = Field.Backdrop };
        foreach (LoadingVariant variant in new[] { LoadingVariant.Generic, LoadingVariant.StoryPortal, LoadingVariant.FighterVersus }) {
            yield return new Surface { Name = "Loading" + variant, Build = () => LoadingScreen.Create(variant), Art = Field.Backdrop };
        }
    }

    private static IEnumerable<Surface> Overlays() {
        yield return new Surface {
            Name = "LevelResults", Build = () => LevelResultsPanel.CreateDefault(), Dim = UIPalette.PauseDim,
            Open = node => ((LevelResultsPanel)node).ShowResults("orleans_level_title", 120, 45f, 1)
        };
        yield return new Surface {
            Name = "MatchResults", Build = () => new MatchResults { Name = "MatchResults" }, Dim = UIPalette.PauseDim,
            Open = node => ((MatchResults)node).ShowResult(new FighterMatchResult(winnerPlayerID: 0, isTrueTie: false, completedTick: 600, finalHash: 3L))
        };
        yield return new Surface { Name = "GameOver", Build = () => GameOverScreen.CreateDefault(), Dim = UIPalette.GameOverDim };
        yield return new Surface { Name = "TimelineRestart", Build = () => new TimelineRestartPanel { Name = "TimelineRestartPanel" }, Dim = UIPalette.PauseDim };
    }

    private static Surface Scene(string name, string path, Field art) => new() {
        Name = name, Art = art, Build = () => ResourceLoader.Load<PackedScene>(path).Instantiate()
    };

    private static async Task Inspect(SceneTree tree, Surface surface, Action<Node> check) {
        var host = new CanvasLayer { Name = "ScreenTypeHost" };
        tree.Root.AddChild(host);
        try {
            Node node = surface.Build();
            host.AddChild(node);
            surface.Open?.Invoke(node);
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            check(node);
        } finally {
            if (GodotObject.IsInstanceValid(host)) {
                host.GetParent()?.RemoveChild(host);
                host.Free();
            }
        }
    }

    private static bool Near(Color a, Color b) =>
        Mathf.Abs(a.R - b.R) < 0.004f && Mathf.Abs(a.G - b.G) < 0.004f && Mathf.Abs(a.B - b.B) < 0.004f && Mathf.Abs(a.A - b.A) < 0.004f;

    private static bool Covers(Control control) =>
        control.IsVisibleInTree() && control.GetGlobalRect().Grow(1f).Encloses(Screen);

    private static bool HasField(Node node, Field field) {
        if (node is TextureRect art && field == Field.SubmenuArt && art.Texture?.ResourcePath == SubmenuArt && Covers(art)) return true;
        if (node is ColorRect rect && field == Field.Backdrop && Near(rect.Color, UIPalette.Backdrop) && Covers(rect)) return true;
        for (int i = 0, n = node.GetChildCount(); i < n; i++) {
            if (HasField(node.GetChild(i), field)) return true;
        }
        return false;
    }

    private static bool HasDim(Node node, Color dim) {
        if (node is ColorRect rect && Near(rect.Color, dim) && Covers(rect)) return true;
        for (int i = 0, n = node.GetChildCount(); i < n; i++) {
            if (HasDim(node.GetChild(i), dim)) return true;
        }
        return false;
    }

    private static bool HasStreak(Node node) {
        if (node is ScreenStreak streak && (!streak.IsInsideTree() || streak.IsVisibleInTree())) return true;
        for (int i = 0, n = node.GetChildCount(); i < n; i++) {
            if (HasStreak(node.GetChild(i))) return true;
        }
        return false;
    }
}
