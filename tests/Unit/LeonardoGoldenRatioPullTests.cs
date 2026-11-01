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
/// The lingering-kit pass's Golden Ratio (KL-SZONE): every tick draws a caught target half the way
/// to the spiral's centre (capped at 1.5 u), and Momentum HoldCast is paid once per cast.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LeonardoGoldenRatioPullTests {
    private const double Step = 1.0 / 60.0;
    private static readonly Vector2 Origin = new(-82000f, 600f);

    [TestCase]
    public void TheStepIsHalfTheRemainingDistanceCappedAtOneAndAHalfUnits() {
        AssertThat(LeonardoGoldenRatio.PullStepPixels(30f)).IsEqualApprox(15f, 0.001f);
        AssertThat(LeonardoGoldenRatio.PullStepPixels(100f)).IsEqualApprox(50f, 0.001f);
        AssertThat(LeonardoGoldenRatio.PullStepPixels(400f)).IsEqualApprox(90f, 0.001f);
    }

    [TestCase]
    public void EachTickDrawsACaughtTargetHalfWayInSoItStaysInForAllThree() {
        Node2D host = CreateHost("GoldenPull");
        try {
            PlayerController leonardo = AddLeonardo(host);
            var spiral = leonardo.GetNode<LeonardoGoldenRatio>("Special1");
            AssertThat(spiral.TryExecute()).IsTrue();
            EnemyController enemy = CreateEnemy(host, Origin + new Vector2(36f, 0f));
            enemy.CurrentHP = 500;
            // Open the spiral (startup frames), then the first tick lands on the next frame.
            for (int frame = 0; frame < 120 && spiral.ActiveSpiralRadiusPixels <= 0f; frame++) spiral._PhysicsProcess(Step);
            AssertThat(spiral.ActiveSpiralRadiusPixels > 0f).IsTrue();
            spiral._PhysicsProcess(Step);
            float afterOne = Mathf.Abs(enemy.GlobalPosition.X - Origin.X);
            AssertThat(afterOne).OverrideFailureMessage($"Tick 1 left it {afterOne} px out of 36.").IsLess(20f);

            for (int frame = 0; frame < 90; frame++) spiral._PhysicsProcess(Step);
            // Three ticks of 10 each landed (the pull kept it inside the growing spiral throughout).
            AssertThat(500 - enemy.CurrentHP).IsGreaterEqual(3);
            AssertThat(Mathf.Abs(enemy.GlobalPosition.X - Origin.X)).IsLess(10f);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void MomentumHoldCastIsPaidOncePerCastNotPerTickOrBody() {
        Node2D host = CreateHost("GoldenMomentum");
        try {
            PlayerController leonardo = AddLeonardo(host);
            var spiral = leonardo.GetNode<LeonardoGoldenRatio>("Special1");
            AssertThat(spiral.TryExecute()).IsTrue();
            EnemyController first = CreateEnemy(host, Origin + new Vector2(30f, 0f));
            EnemyController second = CreateEnemy(host, Origin + new Vector2(-30f, 0f));
            first.CurrentHP = 500;
            second.CurrentHP = 500;
            leonardo.RestoreMomentumPips(1f);
            for (int frame = 0; frame < 200; frame++) spiral._PhysicsProcess(Step);
            AssertThat(first.CurrentHP).IsLess(500);
            AssertThat(second.CurrentHP).IsLess(500);
            AssertThat(leonardo.MomentumPips - 1f)
                .OverrideFailureMessage("HoldCast pays 0.05 pip once for the whole cast.")
                .IsEqualApprox(0.05f, 0.01f);
        } finally {
            host.Free();
        }
    }

    private static PlayerController AddLeonardo(Node2D host) {
        PlayerController leonardo = CharacterFactory.CreateCharacter("leonardo", 0, applyStoryProgression: false);
        host.AddChild(leonardo);
        leonardo.GlobalPosition = Origin;
        return leonardo;
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
