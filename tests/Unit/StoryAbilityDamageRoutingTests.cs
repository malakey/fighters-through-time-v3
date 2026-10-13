using System.Collections.Generic;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// 2026-10-11, ledger FIX-STORY-DAMAGE-BUFF-KIT-ROUTING. The Story item-drop
/// damage buff (Easy x1.5 for 15 s, Normal x1.25 for 10 s) reached only
/// Hitbox-built payloads and throws: every Special and Ultimate a kit resolves
/// itself (a shape query, a ground wave, a burst, a zone tick) multiplied only
/// the SpecialDamage lane, so most of the kit ignored the pickup. The three
/// Story attacker multipliers now compose in one place
/// (<see cref="PlayerController.StoryAbilityDamage"/>); a Hitbox keeps adding the
/// contact-time half itself, so a value it carries takes only the build-time
/// half (<see cref="PlayerController.StoryAbilityHitboxDamage"/>). These cases
/// pin that a buffed hero's kit-delivered Special and Ultimate deal the buffed
/// damage exactly once, and that a Hitbox-delivered ability is not buffed twice.
/// The source half of the rule (no kit reads the inputs directly) is
/// <c>KitAlignmentContractTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryAbilityDamageRoutingTests {
    private static readonly Vector2 Origin = new(-61000f, 600f);

    [TestCase]
    public void TheCompositionAppliesEachStoryFactorOnceAndAHitboxAddsOnlyTheContactHalf() {
        var hero = new PlayerController { StorySpecialDamageMultiplier = 2f };
        var hitbox = new Hitbox { AttackID = "joan_righteous_smite", SourcePlayer = hero };
        try {
            hero.StoryScopedStats = new ScopedStoryStats(new Dictionary<ScopedStatKey, float> {
                [new ScopedStatKey(ResonanceStatKeys.AbilityDamage, "joan_righteous_smite")] = 1.1f
            });
            hero.ApplyStoryDamageBuff(1.5f, 15f);

            // Build-time half: the SpecialDamage lane alone.
            AssertFloat(hero.StoryAbilityHitboxDamage(10f)).IsEqualApprox(20f, 0.0001f);
            // Contact-time half: the buff and the ability's lane; a basic swing's
            // ID carries no lane, so it takes the buff alone.
            AssertFloat(hero.StoryHitboxContactDamageScale("joan_righteous_smite")).IsEqualApprox(1.65f, 0.0001f);
            AssertFloat(hero.StoryHitboxContactDamageScale("combo_1")).IsEqualApprox(1.5f, 0.0001f);
            // The whole composition, each factor once: 10 x 2 x 1.1 x 1.5.
            AssertFloat(hero.StoryAbilityDamage(10f, "joan_righteous_smite")).IsEqualApprox(33f, 0.0001f);
            AssertFloat(hero.StoryAbilityDamageScale("joan_righteous_smite")).IsEqualApprox(3.3f, 0.0001f);
            // Another ability's lane is not this one's.
            AssertFloat(hero.StoryAbilityDamage(10f, "joan_divine_piercing")).IsEqualApprox(30f, 0.0001f);

            // A hitbox carrying the build-time half lands the same composition.
            hitbox.Damage = hero.StoryAbilityHitboxDamage(10f);
            AssertFloat(hitbox.CreatePayload(-1).Damage).IsEqualApprox(33f, 0.0001f);

            // The buff expiring returns every path to neutral for it.
            hero.ApplyStoryDamageBuff(1.5f, 0f);
            AssertFloat(hero.StoryAbilityDamage(10f, "joan_righteous_smite")).IsEqualApprox(22f, 0.0001f);
        } finally {
            hitbox.Free();
            hero.Free();
        }
    }

    [TestCase]
    public void ABuffedHerosKitDeliveredSpecialsDealTheBuffedDamageOnce() {
        StoryDropProfile easy = StoryDropTable.LoadDefault().GetProfile(Difficulty.Easy);
        Node2D host = CreateHost("DamageRoutingSpecials");
        var pools = new PoolManager { Name = "DamageRoutingSpecialsPools" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(pools);
        try {
            // Joan's Righteous Smite: a ground wave resolved through a direct TakeHit.
            PlayerController joan = AddCharacter(host, "joan", Origin);
            var smite = joan.GetNode<JoanRighteousSmite>("Special1");
            EnemyController plain = CreateEnemy(host, Origin + new Vector2(80f, 0f));
            EnemyController buffed = CreateEnemy(host, Origin + new Vector2(80f, 0f));

            smite.StrikeWithWave(plain.GetNode<Hurtbox>("Hurtbox"), plain.GlobalPosition);
            int unbuffedDealt = 9999 - plain.CurrentHP;
            AssertThat(unbuffedDealt).IsEqual(Mathf.RoundToInt(smite.Data.BaseDamage));

            joan.ApplyStoryDamageBuff(easy.BuffMultiplier, easy.BuffDurationSeconds);
            smite.StrikeWithWave(buffed.GetNode<Hurtbox>("Hurtbox"), buffed.GlobalPosition);
            AssertThat(9999 - buffed.CurrentHP)
                .OverrideFailureMessage("A buffed Righteous Smite must deal its damage x the buff, once.")
                .IsEqual(Mathf.RoundToInt(smite.Data.BaseDamage * easy.BuffMultiplier));

            // Einstein's E=mc² burst: a shape query resolved one step after the shot.
            PlayerController einstein = AddCharacter(host, "einstein", Origin + new Vector2(0f, -4000f));
            var emc = einstein.GetNode<EinsteinEmc2Blast>("Special1");
            Vector2 burstAt = Origin + new Vector2(200f, -4000f);
            EnemyController burstTarget = CreateEnemy(host, burstAt + new Vector2(20f, 0f));
            einstein.ApplyStoryDamageBuff(easy.BuffMultiplier, easy.BuffDurationSeconds);
            AssertThat(emc.TryExecute()).IsTrue();
            emc.DetonateForTest(burstAt);
            AssertThat(9999 - burstTarget.CurrentHP)
                .OverrideFailureMessage("A buffed E=mc² burst must deal its damage x the buff, once.")
                .IsEqual(Mathf.RoundToInt(emc.Data.BaseDamage * easy.BuffMultiplier));
        } finally {
            pools.ClearAllPools();
            pools.Free();
            host.Free();
        }
    }

    [TestCase]
    public void ABuffedHerosKitDeliveredUltimateDealsTheBuffedTotalOnce() {
        StoryDropProfile easy = StoryDropTable.LoadDefault().GetProfile(Difficulty.Easy);
        Node2D host = CreateHost("DamageRoutingUltimate");
        var pools = new PoolManager { Name = "DamageRoutingUltimatePools" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(pools);
        try {
            int plainTotal = CastEinsteinUltimate(host, Origin, buff: null);
            int buffedTotal = CastEinsteinUltimate(host, Origin + new Vector2(0f, -4000f), easy);
            AbilityData ultimate = AuthoredResources.Load<AbilityData>("res://resources/Abilities/einstein/ultimate.tres");

            AssertThat(plainTotal).IsEqual(Mathf.RoundToInt(ultimate.UltimateImpactTotal));
            // 6 x 10 + 18 = 78 unbuffed; every hit x1.5 is whole, so 117 exactly
            // (a second application would be 175).
            AssertThat(buffedTotal)
                .OverrideFailureMessage("A buffed Cosmological Constant must deal its total x the buff, once.")
                .IsEqual(Mathf.RoundToInt(ultimate.UltimateImpactTotal * easy.BuffMultiplier));
        } finally {
            pools.ClearAllPools();
            pools.Free();
            host.Free();
        }
    }

    [TestCase]
    public void ABuffedHerosZoneTicksCarryTheBuffOnce() {
        StoryDropProfile easy = StoryDropTable.LoadDefault().GetProfile(Difficulty.Easy);
        Node2D host = CreateHost("DamageRoutingZone");
        try {
            PlayerController hero = AddCharacter(host, "einstein", Origin);
            EnemyController enemy = CreateEnemy(host, Origin + new Vector2(60f, 0f));
            var zone = new PlaceholderZone { Name = "DamageRoutingZoneProbe" };
            host.AddChild(zone);
            zone.Setup(10f, 5f, 1f, hero.PlayerIndex, Colors.White, 80f, ownerPlayer: hero);

            hero.ApplyStoryDamageBuff(easy.BuffMultiplier, easy.BuffDurationSeconds);
            zone.ApplyTickTo(enemy);
            AssertThat(9999 - enemy.CurrentHP)
                .OverrideFailureMessage("A buffed hero's zone tick must deal its damage x the buff, once.")
                .IsEqual(PlaceholderZone.ComputeTickDamage(10f, easy.BuffMultiplier));
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void AHitboxDeliveredAbilityIsNotBuffedTwice() {
        StoryDropProfile normal = StoryDropTable.LoadDefault().GetProfile(Difficulty.Normal);
        Node2D host = CreateHost("DamageRoutingHitbox");
        var pools = new PoolManager { Name = "DamageRoutingHitboxPools" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(pools);
        try {
            // A factory-built kit hitbox: Lincoln's Splitting Strike overhead.
            PlayerController lincoln = AddCharacter(host, "lincoln", Origin);
            lincoln.ApplyStoryDamageBuff(normal.BuffMultiplier, normal.BuffDurationSeconds);
            var split = lincoln.GetNode<LincolnSplittingStrike>("Special2");
            AssertThat(split.TryExecute()).IsTrue();
            split.AdvanceToActive();
            Hitbox overhead = split.GetNode<Hitbox>("OverheadHitbox");
            AssertFloat(overhead.Damage)
                .OverrideFailureMessage("A hitbox carries only the build-time half; the buff is added when it lands.")
                .IsEqualApprox(split.Data.BaseDamage, 0.0001f);
            AssertFloat(overhead.CreatePayload(-1).Damage)
                .OverrideFailureMessage("The overhead must land x the buff once (x1.25, never x1.5625).")
                .IsEqualApprox(split.Data.BaseDamage * normal.BuffMultiplier, 0.0001f);

            // A pooled projectile's contact: E=mc²'s minor contact stage, landed
            // through the real Hitbox signal path.
            PlayerController einstein = AddCharacter(host, "einstein", Origin + new Vector2(0f, -4000f));
            einstein.ApplyStoryDamageBuff(normal.BuffMultiplier, normal.BuffDurationSeconds);
            var emc = einstein.GetNode<EinsteinEmc2Blast>("Special1");
            AssertThat(emc.TryExecute()).IsTrue();
            emc.AdvanceToActive();
            PlaceholderProjectile shot = FirstChildOf<PlaceholderProjectile>(host);
            AssertThat(shot).OverrideFailureMessage("E=mc² fired no shot.").IsNotNull();
            Hitbox contact = shot.GetNode<Hitbox>("Hitbox");
            float authoredContact = emc.Data.ProjectileContactDamage;
            AssertFloat(contact.Damage).IsEqualApprox(authoredContact, 0.0001f);
            AssertFloat(contact.CreatePayload(-1).Damage).IsEqualApprox(authoredContact * normal.BuffMultiplier, 0.0001f);

            EnemyController enemy = CreateEnemy(host, shot.GlobalPosition + new Vector2(10f, 0f));
            contact.EmitSignal(Area2D.SignalName.AreaEntered, enemy.GetNode<Hurtbox>("Hurtbox"));
            // 7 x 1.25 = 8.75 lands as 9; a second application (10.94) would land 11.
            AssertThat(9999 - enemy.CurrentHP).IsEqual(Mathf.RoundToInt(authoredContact * normal.BuffMultiplier));
        } finally {
            pools.ClearAllPools();
            pools.Free();
            host.Free();
        }
    }

    /// <summary>
    /// Casts Einstein's Cosmological Constant from <paramref name="at"/> at one
    /// fresh enemy inside the singularity and returns the HP it lost. Recovery
    /// lands every hit the active window has not, ending with the finale.
    /// </summary>
    private static int CastEinsteinUltimate(Node2D host, Vector2 at, StoryDropProfile buff) {
        PlayerController einstein = AddCharacter(host, "einstein", at);
        EnemyController enemy = CreateEnemy(host, at + new Vector2(150f, 0f));
        if (buff != null) einstein.ApplyStoryDamageBuff(buff.BuffMultiplier, buff.BuffDurationSeconds);
        einstein.GetNode<UltimateMeter>("UltimateMeter").SetValue(UltimateMeter.MaxValue);
        var ultimate = einstein.GetNode<EinsteinUltimate>("Ultimate");
        AssertThat(ultimate.TryExecute()).IsTrue();
        ultimate.AdvanceToActive();
        ultimate.AdvanceToRecovery();
        return 9999 - enemy.CurrentHP;
    }

    private static Node2D CreateHost(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }

    private static PlayerController AddCharacter(Node2D host, string characterID, Vector2 position) {
        PlayerController player = CharacterFactory.CreateCharacter(characterID, 0, applyStoryProgression: false);
        player.Position = position;
        host.AddChild(player);
        player.GlobalPosition = position;
        return player;
    }

    private static EnemyController CreateEnemy(Node host, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>("res://resources/Enemies/chrono_slasher.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        // Positioned before entering the tree, so the physics server registers
        // its hurtbox where the shape queries will look.
        enemy.Position = position;
        host.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        enemy.CurrentHP = 9999;
        // A passive target: no frames run here, and none may move it.
        enemy.SetPhysicsProcess(false);
        return enemy;
    }

    private static T FirstChildOf<T>(Node parent) where T : Node {
        Godot.Collections.Array<Node> children = parent.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is T match) return match;
        }
        return null;
    }
}
