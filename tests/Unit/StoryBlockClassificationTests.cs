using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins V7.2's enemy-attack classification against the player's block
/// ("blocking is never a trap"): every mob attack is Basic-class (1 charge,
/// never an instant shatter), Guard-Crush attacks cost 2 charges, boss-only
/// unblockables pierce the stance, and the companion ruling — player
/// Ultimate-class damage ignores enemy damage-reduction defenses.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryBlockClassificationTests {

    [TestCase]
    public void GuardCrushSpendsTwoChargesAndNeverInstantlyShatters() {
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        try {
            EnterBlockStance(player);
            int hpBefore = player.CurrentHP;

            // A Guard-Crush hit (orange telegraph): 2 charges, absorbed.
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(EnemyHit(damage: 12f, chargeCost: 2));
            AssertThat(player.CurrentHP).IsEqual(hpBefore);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("A 3-charge shield absorbs one Guard-Crush without breaking.")
                .IsEqual(CharacterState.Blocking);

            // The second Guard-Crush is the 2 + 2 shatter path: guard broken.
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(EnemyHit(damage: 12f, chargeCost: 2));
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("2 + 2 Guard-Crush pressure is the authored PvE shatter.")
                .IsEqual(CharacterState.Dazed);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void EnemyBasicClassFireCostsOneChargeAndUnblockablesPierce() {
        StaticBody2D floor = CreateFloor();
        PlayerController player = CreatePlayer();
        try {
            EnterBlockStance(player);
            int hpBefore = player.CurrentHP;

            // Basic-class mob fire (the V7.2 standard rule): 1 charge, chip-priced.
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(EnemyHit(damage: 9f, chargeCost: 0));
            AssertThat(player.CurrentHP).IsEqual(hpBefore);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Blocking);

            // A red-telegraph unblockable pierces the stance outright.
            HitPayload unblockable = EnemyHit(damage: 15f, chargeCost: 0);
            unblockable.Unblockable = true;
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(unblockable);
            AssertThat(player.CurrentHP < hpBefore)
                .OverrideFailureMessage("An unblockable attack must land through the stance.")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void PlayerUltimateClassDamageIgnoresEnemyFrontalReduction() {
        EnemyData canonical = AuthoredResources.Load<EnemyData>(
            "res://resources/Enemies/shock_shield_legionnaire.tres");
        var data = (EnemyData)canonical.Duplicate();
        data.FrontalDamageReduction = 0.8f;
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        try {
            int hpStart = enemy.CurrentHP;
            // A frontal Basic-class hit is shaved by the shield wall.
            Vector2 front = enemy.GlobalPosition
                + new Vector2(enemy.IsFacingRight ? 40f : -40f, 0f);
            enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(PlayerHit(AttackClass.Basic, 20f, front));
            int basicApplied = hpStart - enemy.CurrentHP;
            AssertThat(basicApplied < 20)
                .OverrideFailureMessage("The frontal reduction must shave Basic-class damage.")
                .IsTrue();

            // The same hit at Ultimate class ignores the reduction entirely.
            int hpBeforeUltimate = enemy.CurrentHP;
            enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(PlayerHit(AttackClass.Ultimate, 20f, front));
            AssertThat(hpBeforeUltimate - enemy.CurrentHP)
                .OverrideFailureMessage("Ultimate-class damage is the authored answer to a shelled target.")
                .IsEqual(20);
        } finally {
            enemy.Free();
        }
    }

    // ---- Harness -------------------------------------------------------------

    private static void EnterBlockStance(PlayerController player) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Block));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        for (int frame = 0; frame < 6; frame++) player._PhysicsProcess(1.0 / 60.0);
        AssertThat(player.CurrentState).IsEqual(CharacterState.Blocking);
    }

    /// <summary>An enemy hit as the V7.2 executor now builds them: Basic-class,
    /// with the classification carried on the payload fields.</summary>
    private static HitPayload EnemyHit(float damage, int chargeCost) => new() {
        AttackerIndex = -1,
        TargetIndex = 0,
        AttackID = "test.enemy_hit",
        HitboxID = "primary",
        AttackClass = AttackClass.Basic,
        Damage = damage,
        Knockback = new Vector2(3f, -1f),
        HitstunDuration = 0.2f,
        HitOrigin = new Vector2(40f, 0f),
        AttackerFacingRight = false,
        BlockChargeCost = chargeCost
    };

    private static HitPayload PlayerHit(AttackClass attackClass, float damage, Vector2 origin) => new() {
        AttackerIndex = 0,
        TargetIndex = -1,
        AttackID = "test.player_hit",
        HitboxID = attackClass == AttackClass.Ultimate ? "ultimate" : "combo_1",
        AttackClass = attackClass,
        Damage = damage,
        Knockback = Vector2.Zero,
        HitstunDuration = 0f,
        HitOrigin = origin,
        AttackerFacingRight = true
    };

    private static PlayerController CreatePlayer() {
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        player.Position = new Vector2(0f, 0f);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        return player;
    }

    private static StaticBody2D CreateFloor() {
        var floor = new StaticBody2D {
            Name = "BlockTestFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }
}
