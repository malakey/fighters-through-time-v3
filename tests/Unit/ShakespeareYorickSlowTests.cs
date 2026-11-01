using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The lingering-kit pass's Yorick's Lament (KL-SZONE): the sonic wave's slow is the lingering 1.2
/// for 1.75 s from the authored resource.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShakespeareYorickSlowTests {
    private static readonly Vector2 Origin = new(-92000f, 600f);

    [TestCase]
    public void TheWaveSlowsACaughtMobByOnePointTwoForOnePointSevenFiveSeconds() {
        Node2D host = CreateHost("YorickSlow");
        try {
            PlayerController shakespeare = CharacterFactory.CreateCharacter("shakespeare", 0, applyStoryProgression: false);
            host.AddChild(shakespeare);
            shakespeare.GlobalPosition = Origin;
            var yorick = shakespeare.GetNode<ShakespeareYoricksLament>("Special1");
            EnemyController enemy = CreateEnemy(host, Origin + new Vector2(60f, 0f));
            yorick.EmitSonicWaveForTest(Origin + new Vector2(60f, 0f));
            StatusEffectData slow = enemy.ActiveStatuses.Control;
            AssertThat(slow.Type).IsEqual(StatusType.TimeDilation);
            AssertThat(slow.Intensity).IsEqualApprox(1.2f, 0.001f);
            AssertThat(slow.RemainingSeconds).IsEqualApprox(1.75f, 0.01f);
        } finally {
            host.Free();
        }
    }

    private static EnemyController CreateEnemy(Node host, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>("res://resources/Enemies/chrono_slasher.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        host.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        return enemy;
    }

    private static Node2D CreateHost(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }
}
