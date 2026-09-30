using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W7a — the Story half of the burst-on-terrain projectile primitive
/// (<see cref="PlaceholderProjectile.BurstsOnTerrain"/>): a detonating shot that
/// opts in bursts where it strikes solid terrain or a wall and at its maximum
/// range, and a construct bolt that only <see cref="PlaceholderProjectile.StopsOnTerrain"/>
/// vanishes at the wall without bursting.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryProjectileBurstTests {
    private const double Step = 1.0 / 60.0;
    private static readonly Vector2 Origin = new(-42000f, 600f);

    [TestCase]
    public void ABurstingShotBurstsWhereItStrikesAWall() {
        Node2D host = CreateHost("BurstWall", wallX: Origin.X + 200f);
        try {
            PlaceholderProjectile shot = Spawn(host, lifetime: 5f);
            shot.DetonateOnImpact = true;
            shot.BurstsOnTerrain = true;
            Vector2? burstAt = null;
            shot.Impacted += point => burstAt = point;
            for (int frame = 0; frame < 120 && burstAt == null; frame++) shot._PhysicsProcess(Step);
            AssertThat(burstAt.HasValue).OverrideFailureMessage("The shot never burst on the wall.").IsTrue();
            // The wall's near face is at +180 (a 40 px thick wall centred at +200).
            AssertThat(Mathf.Abs(burstAt.Value.X - (Origin.X + 180f)) < 2f).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ABurstingShotBurstsAtItsMaximumRange() {
        Node2D host = CreateHost("BurstRange", wallX: null);
        try {
            PlaceholderProjectile shot = Spawn(host, lifetime: 0.5f);
            shot.DetonateOnImpact = true;
            shot.BurstsOnTerrain = true;
            Vector2? burstAt = null;
            shot.Impacted += point => burstAt = point;
            for (int frame = 0; frame < 60 && burstAt == null; frame++) shot._PhysicsProcess(Step);
            AssertThat(burstAt.HasValue).OverrideFailureMessage("The shot vanished at max range without bursting.").IsTrue();
            // 600 px/s for 0.5 s: ~300 px out.
            AssertThat(Mathf.Abs(burstAt.Value.X - (Origin.X + 300f)) < 12f).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void AConstructBoltStopsOnTerrainWithoutBursting() {
        Node2D host = CreateHost("BoltWall", wallX: Origin.X + 200f);
        try {
            PlaceholderProjectile bolt = Spawn(host, lifetime: 5f);
            bolt.StopsOnTerrain = true;
            bolt.ConfigureConstructHit();
            bool burst = false;
            bolt.Impacted += _ => burst = true;
            for (int frame = 0; frame < 60 && GodotObject.IsInstanceValid(bolt) && !bolt.IsQueuedForDeletion()
                && bolt.GlobalPosition.X < Origin.X + 179f; frame++) {
                bolt._PhysicsProcess(Step);
            }
            AssertThat(burst).IsFalse();
            bool stopped = !GodotObject.IsInstanceValid(bolt) || bolt.IsQueuedForDeletion()
                || !bolt.Visible || bolt.GetParent() != host;
            AssertThat(stopped).OverrideFailureMessage("The bolt flew through the wall.").IsTrue();
        } finally {
            host.Free();
        }
    }

    private static PlaceholderProjectile Spawn(Node2D host, float lifetime) {
        var shot = new PlaceholderProjectile { Name = "Shot" };
        host.AddChild(shot);
        shot.GlobalPosition = Origin;
        shot.Setup(5f, Vector2.Zero, 600f, movingRight: true, ownerIndex: 0,
            Colors.White, new Vector2(16f, 8f), lifetime);
        return shot;
    }

    private static Node2D CreateHost(string name, float? wallX) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        if (wallX.HasValue) {
            var wall = new StaticBody2D {
                Name = "Wall",
                CollisionLayer = CollisionLayers.Environment,
                Position = new Vector2(wallX.Value, Origin.Y)
            };
            wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(40f, 400f) } });
            host.AddChild(wall);
        }
        return host;
    }
}
