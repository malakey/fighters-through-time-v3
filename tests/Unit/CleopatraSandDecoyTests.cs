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
/// Package 12 W4 (GAP-10c) — Cleopatra's sand decoy (design §5): "Desert Mirage
/// leaves a sand decoy at its origin that enemies and the CPU briefly target
/// (1 s); Royal Aegis's decoy shield uses this decoy", with the decoy's shield a
/// SEPARATE recipient (D02a) — its own capacity, depletion, lifetime and grant
/// identity, never pooled with Cleopatra's. Story only.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CleopatraSandDecoyTests {
    private const double Tick = 1.0 / 60.0;

    [TestCase]
    public void AMirageLeavesAOneSecondDecoyAtItsOriginThatEnemiesAimAt() {
        PlayerController player = CharacterFactory.CreateCharacter("cleopatra");
        Node host = Attach(player);
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            player.GlobalPosition = new Vector2(400f, 300f);
            enemy.GlobalPosition = new Vector2(900f, 300f);
            enemy.SetChaseTargetForTest(player);
            AssertThat(enemy.TargetAimPosition).IsEqual(player.GlobalPosition);

            var mirage = player.GetNode<CleopatraDesertMirage>("MovementAbility");
            player.TransitionTo(CharacterState.Airborne);
            Press(player, GameplayButtons.MovementAbility);
            SandDecoyNode decoy = mirage.Decoy;
            AssertObject(decoy).IsNotNull();
            AssertThat(decoy.IsActive).IsTrue();
            AssertThat(decoy.GlobalPosition).IsEqual(new Vector2(400f, 300f));

            // Cleopatra rushes away; the enemy keeps aiming at the sand.
            player.GlobalPosition = new Vector2(100f, 300f);
            AssertThat(enemy.TargetAimPosition).IsEqual(decoy.GlobalPosition);

            for (int frame = 0; frame < SandDecoyNode.LifetimeFrames; frame++) decoy.Tick();
            AssertThat(decoy.IsActive).IsFalse();
            AssertThat(enemy.TargetAimPosition)
                .OverrideFailureMessage("After one second the lure ends and the enemy aims at her again.")
                .IsEqual(player.GlobalPosition);
        } finally {
            if (GodotObject.IsInstanceValid(enemy)) enemy.Free();
            Teardown(host, player);
        }
    }

    [TestCase]
    public void RoyalAegisGivesTheDecoyItsOwnShieldThatNeverReachesCleopatra() {
        PlayerController player = CharacterFactory.CreateCharacter("cleopatra");
        Node host = Attach(player);
        try {
            player.StoryAbilityPerks.Add(CleopatraDesertMirage.RoyalAegisPerkKey);
            var mirage = player.GetNode<CleopatraDesertMirage>("MovementAbility");
            player.TransitionTo(CharacterState.Airborne);
            Press(player, GameplayButtons.MovementAbility);
            SandDecoyNode decoy = mirage.Decoy;

            float capacity = StoryDefenseRules.GrantedShieldCapacityShare * player.MaximumHP;
            AssertFloat(decoy.ShieldPoints).IsEqualApprox(capacity, 0.001f);
            AssertFloat(player.StoryShieldPoints).IsEqualApprox(capacity, 0.001f);

            // A hit on the sand drains only the decoy's shield.
            int hp = player.CurrentHP;
            float heroShield = player.StoryShieldPoints;
            decoy.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(2f));
            AssertFloat(decoy.ShieldPoints).IsEqualApprox(capacity - 2f, 0.001f);
            AssertThat(player.CurrentHP).IsEqual(hp);
            AssertFloat(player.StoryShieldPoints).IsEqualApprox(heroShield, 0.001f);
            AssertThat(decoy.IsActive).IsTrue();

            // The same grant identity is refused (D02a) — no refill.
            AssertThat(decoy.GrantShield(capacity, StoryDefenseRules.GrantedShieldLifetimeFrames, grantId: 1)).IsFalse();

            // A hit the shield cannot absorb disperses the sand, still never
            // touching Cleopatra.
            decoy.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(capacity + 10f));
            AssertThat(decoy.IsActive).IsFalse();
            AssertThat(player.CurrentHP).IsEqual(hp);
        } finally {
            Teardown(host, player);
        }
    }

    private static HitPayload Hit(float damage) => new() {
        AttackerIndex = -1,
        AttackID = "test.enemy_hit",
        HitboxID = "primary",
        AttackClass = AttackClass.Basic,
        Damage = damage,
        Knockback = Vector2.Zero
    };

    private static EnemyController CreateEnemy(string enemyID) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        return enemy;
    }

    private static void Press(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Tick);
    }

    private static Node Attach(PlayerController player) {
        var host = new Node { Name = "SandDecoyHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        host.AddChild(player);
        return host;
    }

    private static void Teardown(Node host, PlayerController player) {
        InputManager.Instance?.ClearInputSource(player.PlayerIndex);
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
