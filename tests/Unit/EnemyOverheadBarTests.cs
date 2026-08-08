using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B1. The authored enemy bodies' overhead HP bar and name label are
/// hidden until the enemy is damaged, and a pooled body never inherits the
/// previous occupant's bar.
///
/// The scene-level half of <c>OverheadBarVisibilityTests</c>: that suite proves
/// the state machine, this one proves the two widgets in
/// <c>StandardEnemy.tscn</c>/<c>EliteEnemy.tscn</c> are actually wired to it, and
/// that <c>OnSpawn</c>/<c>OnDespawn</c> — the two calls a pool makes and the two
/// most likely to be forgotten — reset it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EnemyOverheadBarTests {

    private const string StandardEnemyScenePath = "res://scenes/enemies/StandardEnemy.tscn";
    private const string EliteEnemyScenePath = "res://scenes/enemies/EliteEnemy.tscn";

    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    private static (Node host, EnemyController enemy) CreateEnemy(
        string scenePath = StandardEnemyScenePath,
        string dataPath = "res://resources/Enemies/chrono_slasher.tres") {

        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "EnemyOverheadBarHost" };
        tree.Root.AddChild(host);

        // Scenes are streamed presentation, never AuthoredResources cache entries.
        var scene = ResourceLoader.Load<PackedScene>(scenePath);
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = AuthoredResources.Load<EnemyData>(dataPath);
        host.AddChild(enemy);
        return (host, enemy);
    }

    [TestCase]
    public void AFreshlySpawnedEnemyShowsNoOverheadBar() {
        (Node host, EnemyController enemy) = CreateEnemy();
        try {
            var bar = enemy.GetNodeOrNull<ProgressBar>("HPBar");
            var label = enemy.GetNodeOrNull<Label>("NameLabel");
            AssertObject(bar).IsNotNull();
            AssertObject(label).IsNotNull();

            // A room of six patrolling mobs used to render six always-on bars.
            AssertThat(enemy.BarVisibility.HasBeenDamaged).IsFalse();
            AssertThat(bar.Visible).IsFalse();
            AssertThat(label.Visible).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheFirstHitRevealsTheBarAndTheLabelTogether() {
        (Node host, EnemyController enemy) = CreateEnemy();
        try {
            var bar = enemy.GetNodeOrNull<ProgressBar>("HPBar");
            var label = enemy.GetNodeOrNull<Label>("NameLabel");

            enemy.TakeDamage(5);

            AssertThat(enemy.BarVisibility.HasBeenDamaged).IsTrue();
            AssertThat(bar.Visible).IsTrue();
            AssertThat(label.Visible).IsTrue();
            AssertFloat(bar.Modulate.A).IsEqual(1f);
            AssertThat(bar.Value).IsLess(bar.MaxValue);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void AZeroDamageHitDoesNotRevealTheBar() {
        (Node host, EnemyController enemy) = CreateEnemy();
        try {
            var bar = enemy.GetNodeOrNull<ProgressBar>("HPBar");

            // Nothing landed, so nothing was engaged.
            enemy.TakeDamage(0);

            AssertThat(enemy.BarVisibility.HasBeenDamaged).IsFalse();
            AssertThat(bar.Visible).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void APooledBodyDoesNotInheritThePreviousOccupantsBar() {
        (Node host, EnemyController enemy) = CreateEnemy();
        try {
            var bar = enemy.GetNodeOrNull<ProgressBar>("HPBar");
            var label = enemy.GetNodeOrNull<Label>("NameLabel");

            enemy.TakeDamage(5);
            AssertThat(bar.Visible).IsTrue();

            // The release half of a pool cycle must clear the widgets, not just the
            // flag: a released body can still be visible for a frame.
            enemy.OnDespawn();
            AssertThat(bar.Visible).IsFalse();
            AssertThat(label.Visible).IsFalse();

            enemy.OnSpawn();
            AssertThat(enemy.BarVisibility.HasBeenDamaged).IsFalse();
            AssertThat(bar.Visible).IsFalse();
            AssertThat(label.Visible).IsFalse();
            // And the recycled body is back at full HP, so the bar it eventually
            // shows is its own.
            AssertThat(enemy.CurrentHP).IsEqual(enemy.ScaledMaxHP);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ARewoundEncounterHidesTheBarAgain() {
        (Node host, EnemyController enemy) = CreateEnemy();
        try {
            enemy.RewindPolicy = FTT.Environment.StoryRewindPolicy.ResetToInitialState;
            var bar = enemy.GetNodeOrNull<ProgressBar>("HPBar");

            enemy.TakeDamage(5);
            AssertThat(bar.Visible).IsTrue();

            // In the restored timeline this fight has not happened yet.
            enemy.ApplyStoryRewind();

            AssertThat(enemy.BarVisibility.HasBeenDamaged).IsFalse();
            AssertThat(bar.Visible).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheEliteBodyFollowsTheSameRule() {
        (Node host, EnemyController enemy) = CreateEnemy(
            EliteEnemyScenePath, "res://resources/Enemies/chrono_guard_elite.tres");
        try {
            var bar = enemy.GetNodeOrNull<ProgressBar>("HPBar");
            AssertObject(bar).IsNotNull();
            AssertThat(bar.Visible).IsFalse();

            enemy.TakeDamage(5);
            AssertThat(bar.Visible).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheHudOpacitySettingScalesTheOverheadBarLive() {
        GlobalSaveData data = SaveManager.Instance?.GlobalData;
        AssertObject(data).IsNotNull();
        float original = data.HudOpacity;
        (Node host, EnemyController enemy) = CreateEnemy();
        try {
            var bar = enemy.GetNodeOrNull<ProgressBar>("HPBar");
            enemy.TakeDamage(5);
            AssertFloat(bar.Modulate.A).IsEqual(1f);

            // Read per-write rather than once at spawn, so a slider moved mid-level
            // reaches mobs already patrolling the room.
            data.HudOpacity = 0.5f;
            enemy._PhysicsProcess(1.0 / 60.0);
            AssertFloat(bar.Modulate.A).IsEqualApprox(0.5f, 0.0001f);
        } finally {
            data.HudOpacity = original;
            host.Free();
        }
    }
}
