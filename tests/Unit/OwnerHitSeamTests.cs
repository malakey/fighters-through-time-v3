using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The Story seam "the owner's hit landed" (lingering-kit pass, KL-RULES, plan 1.3):
/// <see cref="PlayerController.OwnerHitLanded"/> fires once per landed (D1a) contact of the owner's,
/// carrying the struck hurtbox, the payload and the impact position, and never for a whiff, a
/// drill-invulnerable target or a strike surface. <see cref="OwnerHitLandedInfo.TriggersSyncedStrike"/>
/// states the trigger rule for the constructs. The harness is the StoryHitConfirmTests one.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class OwnerHitSeamTests {
    private const double Step = 1.0 / 60.0;
    private static readonly Vector2 Origin = new(52000f, 0f);

    [TestCase]
    public void ALandedBasicContactRaisesTheSeamOnceWithTheTargetAndThePayload() {
        Run((player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            var seen = new System.Collections.Generic.List<OwnerHitLandedInfo>();
            player.OwnerHitLanded += seen.Add;
            Hitbox box = OpenBox(player);
            Contact(box, mob);
            AssertThat(seen.Count).OverrideFailureMessage("One landed contact, one raise.").IsEqual(1);
            OwnerHitLandedInfo info = seen[0];
            AssertThat(info.Landed).IsTrue();
            AssertThat(info.Payload.ContactLanded).IsTrue();
            AssertThat(info.Payload.HitboxID).IsEqual("combo_1");
            AssertThat(info.Target).IsEqual(mob.GetNode<Hurtbox>("Hurtbox"));
            AssertThat(info.IsBasicHit).IsTrue();
            AssertThat(info.TriggersSyncedStrike).IsTrue();
            AssertThat(info.Position).IsEqual(mob.GetNode<Hurtbox>("Hurtbox").GlobalPosition);
        });
    }

    [TestCase]
    public void AWhiffRaisesNothing() {
        Run((player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            mob.DrillInvulnerable = true;
            int raised = 0;
            player.OwnerHitLanded += _ => raised++;
            Hitbox box = OpenBox(player);
            Contact(box, mob);
            AssertThat(raised)
                .OverrideFailureMessage("A refused contact is not landed and raises nothing.")
                .IsEqual(0);
        });
    }

    [TestCase]
    public void TheTriggerRuleExcludesConstructsTicksThrowsUltimatesStrikesAndTheLorentzPulse() {
        HitPayload basic = Landed("combo_2", HitDelivery.DirectHit, HitOrigin.Basic);
        AssertThat(new OwnerHitLandedInfo(null, in basic, true, Vector2.Zero).TriggersSyncedStrike).IsTrue();
        HitPayload special = Landed("sonic_wave", HitDelivery.DirectHit, HitOrigin.Special);
        AssertThat(new OwnerHitLandedInfo(null, in special, true, Vector2.Zero).TriggersSyncedStrike).IsTrue();

        foreach ((string id, HitDelivery delivery, HitOrigin origin, bool combo, string why) in new[] {
            ("turret_bolt", HitDelivery.Construct, HitOrigin.Basic, false, "a construct hit"),
            ("tick", HitDelivery.Tick, HitOrigin.Special, false, "a tick"),
            ("throw", HitDelivery.DirectHit, HitOrigin.Basic, false, "a throw"),
            ("burst", HitDelivery.DirectHit, HitOrigin.Ultimate, false, "an Ultimate"),
            ("pulse", HitDelivery.DirectHit, HitOrigin.Special, false, "the Lorentz Pulse"),
            ("chain_lightning", HitDelivery.DirectHit, HitOrigin.Special, false, "the chain arc"),
            ("synced", HitDelivery.Construct, HitOrigin.Special, true, "another strike (a Construct delivery)")
        }) {
            HitPayload payload = Landed(id, delivery, origin);
            payload.CountsAsCombo = combo;
            AssertThat(new OwnerHitLandedInfo(null, in payload, true, Vector2.Zero).TriggersSyncedStrike)
                .OverrideFailureMessage($"{why} must never trigger a synced strike.")
                .IsFalse();
        }

        // Review round 1 (decision 18): the CountsAsCombo marker alone excludes nothing (Golden Ratio's ticks).
        HitPayload goldenTick = Landed("spiral_tick", HitDelivery.DirectHit, HitOrigin.Special);
        goldenTick.CountsAsCombo = true;
        AssertThat(new OwnerHitLandedInfo(null, in goldenTick, true, Vector2.Zero).TriggersSyncedStrike).IsTrue();

        HitPayload whiff = Landed("combo_1", HitDelivery.DirectHit, HitOrigin.Basic);
        whiff.ContactLanded = false;
        AssertThat(new OwnerHitLandedInfo(null, in whiff, false, Vector2.Zero).TriggersSyncedStrike).IsFalse();
    }

    [TestCase]
    public void TheMarkerDefaultsOffAndPaysTheMomentumOncePerEvent() {
        AssertThat(default(HitPayload).CountsAsCombo).IsFalse();
        Run((player, host) => {
            player.RestoreMomentumPips(1f);
            player.PayLingeringMomentum(LingeringMomentumEvent.Detonation);
            AssertFloat(player.MomentumPips).IsEqualApprox(1f + LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.Detonation) / (float)BasicComboRules.MomentumUnitsPerPip, 0.0001f);
            player.PayLingeringMomentum(LingeringMomentumEvent.SyncedStrike);
            AssertFloat(player.MomentumPips).IsEqualApprox(1f + (LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.Detonation) + LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.SyncedStrike)) / (float)BasicComboRules.MomentumUnitsPerPip, 0.0001f);
        });
    }

    private static HitPayload Landed(string hitboxID, HitDelivery delivery, HitOrigin origin) => new() {
        AttackerIndex = 0,
        TargetIndex = -1,
        AttackID = "test",
        HitboxID = hitboxID,
        AttackClass = AttackClass.Basic,
        Damage = 5f,
        Delivery = delivery,
        Origin = origin,
        ContactLanded = true
    };

    // === harness (StoryHitConfirmTests) =======================================

    /// <summary>Starts a swing and ticks until its box opens.</summary>
    private static Hitbox OpenBox(PlayerController player, GameplayButtons press = GameplayButtons.BasicAttack, float vertical = 0f) {
        var box = player.GetNode<Hitbox>("MeleeHitbox");
        Tick(player, press, vertical: vertical);
        AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
        for (int tick = 0; tick < 30 && !box.IsActive; tick++) Tick(player, GameplayButtons.None);
        AssertThat(box.IsActive).OverrideFailureMessage("Harness: the swing's box never opened.").IsTrue();
        return box;
    }

    private static void Contact(Hitbox box, Node target) {
        Hurtbox hurtbox = target as Hurtbox ?? target.GetNode<Hurtbox>("Hurtbox");
        box.EmitSignal(Area2D.SignalName.AreaEntered, hurtbox);
    }

    private static EnemyController Mob(Node host, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>("res://resources/Enemies/chrono_slasher.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        host.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        enemy.CurrentHP = 9999;
        return enemy;
    }

    /// <summary>The StoryAbilityLockoutTests harness, with the host exposed for targets.</summary>
    private static void Run(Action<PlayerController, Node2D> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "HitConfirmHost" };
        tree.Root.AddChild(host);
        var pools = new PoolManager { Name = "HitConfirmPools" };
        tree.Root.AddChild(pools);
        host.AddChild(Floor());
        PlayerController player = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        player.Position = Origin + new Vector2(0f, -8f);
        host.AddChild(player);
        try {
            Settle(player);
            // The gauge's regen would blur the exact gain these cases pin: hold it still.
            player.MomentumRegenMultiplier = 0f;
            player.RestoreMomentumPips(BasicComboRules.MomentumStartPips);
            body(player, host);
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            InputManager.Instance?.ClearInputSource(1);
            pools.ClearAllPools();
            tree.Root.RemoveChild(pools);
            pools.Free();
            host.Free();
        }
    }

    private static StaticBody2D Floor() {
        var floor = new StaticBody2D {
            Name = "HitConfirmFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = Origin
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        return floor;
    }

    private static void Settle(PlayerController player) {
        for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
            player.Velocity = new Vector2(player.Velocity.X, 400f);
            Tick(player, GameplayButtons.None);
        }
        for (int frame = 0; frame < 60 && player.CurrentState != CharacterState.Idle; frame++) {
            Tick(player, GameplayButtons.None);
        }
        AssertThat(player.CurrentState)
            .OverrideFailureMessage($"setup: {player.CurrentState} onFloor={player.IsOnFloor()} pos={player.GlobalPosition}")
            .IsEqual(CharacterState.Idle);
        AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(0);
        AssertThat(player.SwingLanded).IsFalse();
    }

    /// <summary>One controller tick; every held button reads as freshly pressed.</summary>
    private static void Tick(PlayerController player, GameplayButtons buttons, float vertical = 0f) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, vertical, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Step);
    }
}
