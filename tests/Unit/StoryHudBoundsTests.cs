using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-HUDS. The HUD-S1 clusters at UI scale 0.9, 1.0 and 1.4 in a 1920 x 1080 frame: every cluster stays
/// inside the frame and none overlaps another or the boss bar (ruling 8: per-corner fit, the boss bar top
/// centre with a maximum width, 320 units minimum).
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

                // Boss frame: the bar sits between the clusters; the title row is hidden meanwhile.
                hud.ShowBossBar("boss_archive_prime_name", 640, 1000, new[] { 0.5f });
                await Frames(3);
                Rect2 boss = Bounds(hud.GetNode<Control>("SafeArea/BossPanel"));
                if (!Frame.Grow(12f * scale).Encloses(boss)) problems.Add($"{scale}: boss {boss} outside the frame");
                if (boss.Intersects(left)) problems.Add($"{scale}: boss overlaps the left column ({boss} vs {left})");
                if (boss.Intersects(dust)) problems.Add($"{scale}: boss overlaps the dust plate");
                if (boss.Intersects(dial)) problems.Add($"{scale}: boss overlaps the dial");
                if (boss.Size.X < StoryHUD.BossBarMinWidth * scale - 1f) problems.Add($"{scale}: boss narrower than its minimum");
                if (boss.Size.X > StoryHUD.BossBarMaxWidth * scale + 1f) problems.Add($"{scale}: boss wider than its maximum");
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
}
