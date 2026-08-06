using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
[RequireGodotRuntime]
public class CombatantPushboxTests {
    [TestCase]
    public void StoryPushboxesResolveHorizontallyWithoutMakingHurtboxesSolid() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var player = new CharacterBody2D { Position = Vector2.Zero };
        var enemy = new CharacterBody2D { Position = new Vector2(20f, 0f) };
        CombatantPushbox playerPushbox = AddPushbox(
            player, CollisionLayers.Player, CollisionLayers.Enemy, new Vector2(30f, 48f));
        AddPushbox(enemy, CollisionLayers.Enemy, CollisionLayers.Player, new Vector2(30f, 48f));
        tree.Root.AddChild(player);
        tree.Root.AddChild(enemy);

        bool resolved = playerPushbox.ResolveStoryOverlaps();

        AssertThat(resolved).IsTrue();
        AssertThat(Mathf.Abs(enemy.GlobalPosition.X - player.GlobalPosition.X)).IsEqualApprox(30f, 0.01f);
        player.Free();
        enemy.Free();
    }

    [TestCase]
    public void VerticallySeparatedPushboxesDoNotJostle() {
        var first = new CombatantPushbox { BoxSize = new Vector2(30f, 48f), Position = Vector2.Zero };
        var second = new CombatantPushbox { BoxSize = new Vector2(30f, 48f), Position = new Vector2(10f, 60f) };

        AssertThat(first.GetHorizontalOverlap(second)).IsEqual(0f);
        first.Free();
        second.Free();
    }

    private static CombatantPushbox AddPushbox(
        CharacterBody2D body,
        uint layer,
        uint mask,
        Vector2 size) {
        var pushbox = new CombatantPushbox {
            Name = "Pushbox",
            BoxSize = size,
            CollisionLayer = layer,
            CollisionMask = mask,
            Monitoring = false,
            Monitorable = false
        };
        pushbox.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size } });
        body.AddChild(pushbox);
        return pushbox;
    }
}
