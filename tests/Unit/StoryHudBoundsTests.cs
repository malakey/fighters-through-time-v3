using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-HUDS, re-numbered for Temporal Glass by TG-HUDS. The HUD-S1 clusters at UI scale 0.9, 1.0 and 1.4 in a
/// 1920 x 1080 frame: every cluster stays inside the frame and none overlaps another or the boss bar, which fills
/// the free band between them (UI_SPEC_GLASS 4.4: min(840 s, 1920 - 1008 s), 24 s from each cluster).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryHudBoundsTests {

    private static readonly Rect2 Frame = new(0f, 0f, 1920f, 1080f);

    private static Rect2 Bounds(Control control) =>
        control.GetGlobalTransform() * new Rect2(Vector2.Zero, control.Size);

    private static async Task Frames(int count) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        for (int i = 0; i < count; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public async Task EveryClusterFitsTheFrameAndNoTwoOverlapAtTheThreeUiScales() {
        SaveManager save = SaveManager.Instance;
        AssertObject(save?.GlobalData).IsNotNull();
        float original = save.GlobalData.UiScale;
        var problems = new List<string>();
        foreach (float scale in new[] { 0.9f, 1.0f, 1.4f }) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            var port = new SubViewport { Size = new Vector2I(1920, 1080), RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
            tree.Root.AddChild(port);
            try {
                save.GlobalData.UiScale = scale;
                StoryHUD hud = StoryHUD.CreateDefault();
                port.AddChild(hud);
                hud.SetLevelTitle("campaign_level_orleans");
                hud.SetObjective("florence_objective_reach_boss");
                EventBus bus = EventBus.Instance;
                bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Venom, Duration = 4f, Intensity = 1f });
                bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Suppression, Duration = 2f, Intensity = 1f });
                bus.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 2, Max = 3 });
                bus.RaiseTimelineIntegrityChanged(new IntegrityPayload { Percent = 40f, Tier = IntegrityTier.Fractured, LivingExtractors = 3 });
                await Frames(4);

                // Calm frame: the title row, the left column and the right column.
                Rect2 title = Bounds(hud.GetNode<Control>("SafeArea/TopLeft"));
                Rect2 left = Bounds(hud.GetNode<Control>("SafeArea/TopLeft_Panel"));
                Rect2 dust = Bounds(hud.GetNode<Control>("SafeArea/TopRight_Panel/DustPlate"));
                Rect2 dial = Bounds(hud.GetNode<Control>("SafeArea/TopRight_Panel/IntegrityClock"));
                foreach ((string name, Rect2 rect) in new[] { ("title", title), ("left", left), ("dust", dust), ("dial", dial) }) {
                    // The plates draw 12 px of shadow past their rect; the frame test allows it.
                    if (!Frame.Grow(12f * scale).Encloses(rect)) problems.Add($"{scale}: {name} {rect} outside the frame");
                }
                if (left.Intersects(dust)) problems.Add($"{scale}: left column overlaps the dust plate");
                if (left.Intersects(dial)) problems.Add($"{scale}: left column overlaps the dial");
                if (title.End.X > dust.Position.X && title.End.Y > dust.Position.Y && title.Position.Y < dust.End.Y)
                    problems.Add($"{scale}: title row overlaps the dust plate");

                // UI_SPEC_GLASS 9.2: the checkpoint toast at the top centre (y 140) stays in the free band between the
                // clusters, so it never covers one (at 1.4 the screen's centre falls inside the left stack).
                bus.RaiseCheckpointReached("story_hud_bounds_toast");
                await Frames(2);
                Rect2 toast = Bounds(hud.GetNode<Control>("SafeArea/CheckpointToast"));
                if (!Frame.Encloses(toast)) problems.Add($"{scale}: checkpoint toast {toast} outside the frame");
                if (toast.Intersects(left)) problems.Add($"{scale}: checkpoint toast overlaps the left column ({toast} vs {left})");
                if (toast.Intersects(dust)) problems.Add($"{scale}: checkpoint toast overlaps the dust plate");
                if (toast.Intersects(dial)) problems.Add($"{scale}: checkpoint toast overlaps the dial");

                // Boss frame: the bar sits between the clusters; the title row is hidden meanwhile.
                hud.ShowBossBar("boss_archive_prime_name", 640, 1000, new[] { 0.5f });
                await Frames(3);
                Rect2 boss = Bounds(hud.GetNode<Control>("SafeArea/BossPanel"));
                if (!Frame.Grow(12f * scale).Encloses(boss)) problems.Add($"{scale}: boss {boss} outside the frame");
                if (boss.Intersects(left)) problems.Add($"{scale}: boss overlaps the left column ({boss} vs {left})");
                if (boss.Intersects(dust)) problems.Add($"{scale}: boss overlaps the dust plate");
                if (boss.Intersects(dial)) problems.Add($"{scale}: boss overlaps the dial");
                // UI_SPEC_GLASS 4.4: the bar fills the free band, min(840 s, 1920 - 1008 s), 24 s from the stack and from
                // where the dust plate's 264 px would end.
                float band = Mathf.Min(StoryHUD.BossBarMaxWidth * scale, 1920f - 1008f * scale);
                if (Mathf.Abs(boss.Size.X - band) > 1f) problems.Add($"{scale}: boss {boss.Size.X} wide, not the band's {band}");
                if (boss.Position.X < left.End.X + StoryHUD.BossGap * scale - 1f) problems.Add($"{scale}: boss closer than 24 s to the stack");
                if (boss.End.X > 1920f - (StoryHUD.DustPlateReach + StoryHUD.BossGap) * scale + 1f) problems.Add($"{scale}: boss closer than 24 s to the dust plate");
                var bar = hud.GetNode<Control>("SafeArea/BossPanel/BossBar");
                var phase = hud.GetNode<Control>("SafeArea/BossPanel/BossPhaseTab");
                if (!Bounds(hud.GetNode<Control>("SafeArea/BossPanel")).Grow(8f * scale).Encloses(Bounds(bar)))
                    problems.Add($"{scale}: boss bar outside its panel");
                if (phase.Position.X + phase.Size.X > hud.GetNode<Control>("SafeArea/BossPanel").Size.X + 1f)
                    problems.Add($"{scale}: phase tab past the bar's right edge");
                hud.QueueFree();
            } finally {
                save.GlobalData.UiScale = original;
                port.Free();
            }
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    /// <summary>
    /// TG-CLOSE (the orchestrator's check of the first-use tooltip at y 140 against the clusters): the plate hangs at
    /// 140 s, at most 900 s wide, and keeps to the free band between the left stack and the right cluster like the
    /// checkpoint toast, so no tooltip covers the Vitals panel. Centred at the screen's middle, eight of the nine lines
    /// ran over the Vitals panel at 1.0 and all nine at 1.4.
    /// </summary>
    [TestCase]
    public async Task TheFirstUseTooltipKeepsToTheFreeBandAtTheThreeUiScales() {
        SaveManager save = SaveManager.Instance;
        AssertObject(save?.GlobalData).IsNotNull();
        float original = save.GlobalData.UiScale;
        float applied = UIPalette.AppliedUiScale;
        var problems = new List<string>();
        foreach (float scale in new[] { 0.9f, 1.0f, 1.4f }) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            var port = new SubViewport { Size = new Vector2I(1920, 1080), RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
            tree.Root.AddChild(port);
            try {
                save.GlobalData.UiScale = scale;
                UIPalette.ApplyUiScale(scale);
                StoryHUD hud = StoryHUD.CreateDefault();
                port.AddChild(hud);
                hud.SetLevelTitle("campaign_level_orleans");
                hud.SetObjective("florence_objective_reach_boss");
                var presenter = new FirstUseTooltipPresenter { Name = "FirstUseTooltips" };
                port.AddChild(presenter);
                await Frames(4);
                Rect2 title = Bounds(hud.GetNode<Control>("SafeArea/TopLeft"));
                Rect2 left = Bounds(hud.GetNode<Control>("SafeArea/TopLeft_Panel"));
                Rect2 dust = Bounds(hud.GetNode<Control>("SafeArea/TopRight_Panel/DustPlate"));
                Rect2 dial = Bounds(hud.GetNode<Control>("SafeArea/TopRight_Panel/IntegrityClock"));
                foreach (string id in FirstUseTooltips.All) {
                    presenter.PresentForPreview(FirstUseTooltips.KeyFor(id));
                    await Frames(20);   // past the appear (MOT-A row 4)
                    Rect2 plate = Bounds(presenter.GetNode<Control>("TooltipPanel"));
                    string at = $"{scale} {id}: plate {plate}";
                    if (!Frame.Encloses(plate)) problems.Add($"{at} outside the frame");
                    if (Mathf.Abs(plate.Position.Y - FirstUseTooltipPresenter.PlateTop * scale) > 1f) problems.Add($"{at} not at y {FirstUseTooltipPresenter.PlateTop * scale}");
                    if (plate.Size.X > FirstUseTooltipPresenter.PlateMaxWidth * scale + 1f) problems.Add($"{at} wider than {FirstUseTooltipPresenter.PlateMaxWidth * scale}");
                    if (plate.Intersects(left)) problems.Add($"{at} covers the left stack {left}");
                    if (plate.Intersects(title)) problems.Add($"{at} covers the title row {title}");
                    if (plate.Intersects(dust)) problems.Add($"{at} covers the dust plate {dust}");
                    if (plate.Intersects(dial)) problems.Add($"{at} covers the dial {dial}");
                }
                hud.QueueFree();
            } finally {
                save.GlobalData.UiScale = original;
                UIPalette.ApplyUiScale(applied);
                port.Free();
            }
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }
}
