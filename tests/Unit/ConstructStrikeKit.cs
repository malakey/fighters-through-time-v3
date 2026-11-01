using System;
using System.Collections.Generic;
using System.Reflection;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using Godot;

namespace FTT.Tests.Unit;

/// <summary>
/// A non-suite helper for the KL-SCON construct suites (lingering-kit pass): a host with an owner, mobs
/// and constructs, and the hand-built "the owner's hit landed" event the constructs subscribe to. Not a
/// <c>[TestSuite]</c>, so it adds no cases.
/// </summary>
internal static class ConstructStrikeKit {
    public const double Step = 1.0 / 60.0;

    public static Node2D NewHost(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }

    public static PlayerController Owner(Node2D host, string characterID, Vector2 position) {
        PlayerController owner = CharacterFactory.CreateCharacter(characterID, 0, applyStoryProgression: false);
        host.AddChild(owner);
        owner.GlobalPosition = position;
        // The gauge's regen would blur the exact Momentum these cases pin.
        owner.MomentumRegenMultiplier = 0f;
        owner.RestoreMomentumPips(1f);
        return owner;
    }

    public static EnemyController Mob(Node host, Vector2 position, string enemyID = "chrono_slasher") {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
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

    public static TeslaCoilNode Coil(Node2D host, PlayerController owner, Vector2 position, bool track = true) {
        AbilityData data = AuthoredResources.Load<AbilityData>("res://resources/Abilities/tesla/special_1.tres");
        var coil = ResourceLoader.Load<PackedScene>("res://scenes/constructs/TeslaCoil.tscn").Instantiate<TeslaCoilNode>();
        host.AddChild(coil);
        coil.GlobalPosition = position;
        coil.Initialize(data, owner, resonantOverdrive: false);
        if (track) owner.ActivePersistentObjects.Add(coil);
        return coil;
    }

    public static LeonardoTurretNode Turret(Node2D host, PlayerController owner, Vector2 position, bool overdrive = false) {
        AbilityData data = AuthoredResources.Load<AbilityData>("res://resources/Abilities/leonardo/special_2.tres");
        var turret = ResourceLoader.Load<PackedScene>("res://scenes/constructs/ClockworkTurret.tscn")
            .Instantiate<LeonardoTurretNode>();
        host.AddChild(turret);
        turret.GlobalPosition = position;
        turret.Initialize(data, owner, overdrive);
        owner.ActivePersistentObjects.Add(turret);
        return turret;
    }

    public static SerpentNestNode Nest(Node2D host, PlayerController owner, Vector2 position) {
        AbilityData data = AuthoredResources.Load<AbilityData>("res://resources/Abilities/cleopatra/special_1.tres");
        var nest = ResourceLoader.Load<PackedScene>("res://scenes/constructs/SerpentNest.tscn")
            .Instantiate<SerpentNestNode>();
        host.AddChild(nest);
        nest.GlobalPosition = position;
        nest.Initialize(data, owner, aspsBite: false);
        owner.ActivePersistentObjects.Add(nest);
        return nest;
    }

    public static Hurtbox HurtboxOf(Node body) => body.GetNode<Hurtbox>("Hurtbox");

    /// <summary>A landed (D1a) basic or direct-Special contact of the owner's, as the seam carries it.</summary>
    public static HitPayload OwnerHit(string hitboxID = "combo_1", HitDelivery delivery = HitDelivery.DirectHit,
        HitOrigin origin = HitOrigin.Basic, bool landed = true, float damage = 5f) => new() {
        AttackerIndex = 0,
        TargetIndex = -1,
        AttackID = "test_hit",
        HitboxID = hitboxID,
        AttackClass = AttackClass.Basic,
        Damage = damage,
        Delivery = delivery,
        Origin = origin,
        ContactLanded = landed
    };

    /// <summary>Raises the owner's seam for a hit on <paramref name="target"/>; returns nothing, the constructs react.</summary>
    public static void OwnerLands(PlayerController owner, Node target, HitPayload? payload = null) {
        HitPayload hit = payload ?? OwnerHit();
        Hurtbox hurtbox = target as Hurtbox ?? HurtboxOf(target);
        owner.RaiseOwnerHitLanded(hurtbox, in hit, hurtbox.GlobalPosition);
    }

    /// <summary>The mob's running stun in seconds (private field read through reflection).</summary>
    public static float StunSeconds(EnemyController enemy) =>
        (float)typeof(EnemyController).GetField("_stunTimer", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(enemy)!;

    public static void SetStun(EnemyController enemy, float seconds) =>
        typeof(EnemyController).GetField("_stunTimer", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(enemy, seconds);

    /// <summary>Runs <paramref name="body"/> in a fresh host, always freeing it.</summary>
    public static void InHost(string name, Action<Node2D> body) {
        Node2D host = NewHost(name);
        try {
            body(host);
        } finally {
            host.Free();
        }
    }
}
