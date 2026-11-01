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
/// The lingering-kit pass's Rift (KL-SZONE): the 2.5 u radius, the 1.2 / 1.75 s slow that refreshes
/// while a target is inside and lingers after it leaves, the collapse on the owner's landed string
/// finisher (<c>combo_3</c>) as well as on E=mc2, the collapse's 8 damage, Momentum Detonation once
/// per collapse, and D3 arming inside the E=mc2 cast.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EinsteinRiftLingeringTests {
    private const double Step = 1.0 / 60.0;
    private static readonly Vector2 Origin = new(-62000f, 600f);

    [TestCase]
    public void TheRiftIsTwoAndAHalfUnitsWide() {
        Node2D host = CreateHost("RiftLingerRadius");
        try {
            EinsteinRelativityRift rift = OpenRift(AddEinstein(host));
            AssertThat(rift.ActiveRiftRadiusPixels).IsEqual(150f);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ASlowOfOnePointTwoRefreshesInsideAndLingersOnePointSevenFiveSecondsAfterLeaving() {
        Node2D host = CreateHost("RiftLingerSlow");
        try {
            PlayerController einstein = AddEinstein(host);
            EinsteinRelativityRift rift = OpenRift(einstein);
            EnemyController enemy = CreateEnemy(host, rift.ActiveRiftCenter + new Vector2(30f, 0f));
            rift._PhysicsProcess(Step);
            StatusEffectData slow = enemy.ActiveStatuses.Control;
            AssertThat(slow.Type).IsEqual(StatusType.TimeDilation);
            AssertThat(slow.Intensity).IsEqualApprox(1.2f, 0.001f);
            AssertThat(slow.RemainingSeconds).IsEqualApprox(1.75f, 0.01f);

            // Inside, it refreshes: tick the mob half a second, the rift refreshes it to the full linger.
            for (int frame = 0; frame < 30; frame++) enemy._PhysicsProcess(Step);
            rift._PhysicsProcess(Step);
            AssertThat(enemy.ActiveStatuses.Control.RemainingSeconds).IsEqualApprox(1.75f, 0.05f);

            // Out of the rift it lingers 1.75 s (105 frames), then ends.
            enemy.GlobalPosition = rift.ActiveRiftCenter + new Vector2(0f, -2000f);
            for (int frame = 0; frame < 95; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.ActiveStatuses.Control.IsActive).IsTrue();
            for (int frame = 0; frame < 20; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.ActiveStatuses.Control.IsActive).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheOwnersLandedFinisherInsideTheRiftCollapsesItAndOtherBasicsDoNot() {
        Node2D host = CreateHost("RiftFinisherCollapse");
        try {
            PlayerController einstein = AddEinstein(host);
            EinsteinRelativityRift rift = OpenRift(einstein);
            Vector2 center = rift.ActiveRiftCenter;
            EnemyController enemy = CreateEnemy(host, center + new Vector2(40f, 0f));
            Hurtbox hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");

            einstein.RaiseOwnerHitLanded(hurtbox, Landed("combo_1"), hurtbox.GlobalPosition);
            einstein.RaiseOwnerHitLanded(hurtbox, Landed("combo_2"), hurtbox.GlobalPosition);
            AssertThat(rift.RiftActive).OverrideFailureMessage("Hits one and two do not collapse the rift.").IsTrue();

            EnemyController outside = CreateEnemy(host, center + new Vector2(400f, 0f));
            Hurtbox far = outside.GetNode<Hurtbox>("Hurtbox");
            einstein.RaiseOwnerHitLanded(far, Landed("combo_3"), far.GlobalPosition);
            AssertThat(rift.RiftActive).OverrideFailureMessage("A finisher outside the rift does not collapse it.").IsTrue();

            einstein.RaiseOwnerHitLanded(hurtbox, Landed("combo_3"), hurtbox.GlobalPosition);
            AssertThat(rift.RiftActive).IsFalse();
            AssertThat(rift.IsCollapsing).IsTrue();
            for (int frame = 0; frame < KitMotionRules.RiftCollapsePullFrames; frame++) rift._PhysicsProcess(Step);
            AssertThat(enemy.GlobalPosition.DistanceTo(center) < 1f).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheCollapseDealsEightToEachBodyAndPaysMomentumOncePerCollapse() {
        Node2D host = CreateHost("RiftCollapseDamage");
        try {
            PlayerController einstein = AddEinstein(host);
            EinsteinRelativityRift rift = OpenRift(einstein);
            Vector2 center = rift.ActiveRiftCenter;
            EnemyController first = CreateEnemy(host, center + new Vector2(40f, 0f));
            EnemyController second = CreateEnemy(host, center + new Vector2(-40f, 0f));
            first.CurrentHP = 500;
            second.CurrentHP = 500;
            einstein.RestoreMomentumPips(1f);

            Hurtbox hurtbox = first.GetNode<Hurtbox>("Hurtbox");
            einstein.RaiseOwnerHitLanded(hurtbox, Landed("combo_3"), hurtbox.GlobalPosition);
            AssertThat(rift.IsCollapsing).IsTrue();
            for (int frame = 0; frame < KitMotionRules.RiftCollapsePullFrames; frame++) rift._PhysicsProcess(Step);

            AssertThat(500 - first.CurrentHP).IsEqual(LingeringKitRules.RiftCollapseDamage);
            AssertThat(500 - second.CurrentHP).IsEqual(LingeringKitRules.RiftCollapseDamage);
            AssertThat(einstein.MomentumPips - 1f)
                .OverrideFailureMessage("Detonation pays 0.2 pip once for the collapse, not once per body.")
                .IsEqualApprox(0.2f, 0.01f);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ACollapseLandingInsideTheEmcSquaredCastArmsItsRecoveryCancel() {
        Node2D host = CreateHost("RiftCollapseD3");
        try {
            PlayerController einstein = AddEinstein(host);
            EinsteinRelativityRift rift = OpenRift(einstein);
            Vector2 center = rift.ActiveRiftCenter;
            // Inside the rift but beyond the burst's 72 px, so only the collapse can land.
            EnemyController enemy = CreateEnemy(host, center + new Vector2(120f, 0f));
            enemy.CurrentHP = 500;
            einstein.RestoreMomentumPips(2f);

            var emc = einstein.GetNode<EinsteinEmc2Blast>("Special1");
            AssertThat(emc.TryExecute()).IsTrue();
            AssertThat(emc.IsExecuting).IsTrue();
            emc.DetonateForTest(center + new Vector2(-20f, 0f));
            AssertThat(rift.IsCollapsing).IsTrue();
            AssertThat(emc.LandedDirectHitThisExecution)
                .OverrideFailureMessage("Harness: the burst alone landed nothing.")
                .IsFalse();

            for (int frame = 0; frame < KitMotionRules.RiftCollapsePullFrames; frame++) rift._PhysicsProcess(Step);
            AssertThat(500 - enemy.CurrentHP).IsGreater(0);
            AssertThat(emc.LandedDirectHitThisExecution)
                .OverrideFailureMessage("The collapse landing inside the cast arms D3.")
                .IsTrue();
        } finally {
            host.Free();
        }
    }

    private static HitPayload Landed(string hitboxID) => new() {
        AttackerIndex = 0,
        AttackID = "einstein_basic",
        HitboxID = hitboxID,
        AttackClass = AttackClass.Basic,
        Origin = HitOrigin.Basic,
        Delivery = HitDelivery.DirectHit,
        Damage = 8f,
        ContactLanded = true
    };

    private static EinsteinRelativityRift OpenRift(PlayerController einstein) {
        var rift = einstein.GetNode<EinsteinRelativityRift>("Special2");
        AssertThat(rift.TryExecute()).IsTrue();
        for (int frame = 0; frame < 120 && !rift.RiftActive; frame++) rift._PhysicsProcess(Step);
        AssertThat(rift.RiftActive).IsTrue();
        return rift;
    }

    private static PlayerController AddEinstein(Node2D host) {
        PlayerController einstein = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        host.AddChild(einstein);
        einstein.GlobalPosition = Origin;
        return einstein;
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
