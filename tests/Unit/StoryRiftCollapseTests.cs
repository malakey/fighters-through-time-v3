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
/// Package 13 W7a — Einstein's Relativity Rift in Story: E03 placement (thrown
/// 5 units = 300 px ahead, stopping short of a wall, 1.5 units = 90 px radius)
/// and E01 Rift Collapse (an E=mc² burst inside his own open rift ends it, pulls
/// the caught enemy to its centre over 6 frames and launches it upward with a
/// 0-damage hit).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryRiftCollapseTests {
    private const double Step = 1.0 / 60.0;
    private static readonly Vector2 Origin = new(-46000f, 600f);

    [TestCase]
    public void TheRiftIsThrownFiveUnitsAheadAndStopsShortOfAWall() {
        Node2D host = CreateHost("RiftPlacement");
        PlayerController einstein = null;
        try {
            einstein = AddEinstein(host);
            EinsteinRelativityRift rift = OpenRift(einstein);
            AssertThat(rift.ActiveRiftRadiusPixels).IsEqual(150f);
            AssertThat(rift.ActiveRiftCenter.X - einstein.GlobalPosition.X).IsEqual(300f);
        } finally {
            host.Free();
        }

        Node2D walled = CreateHost("RiftPlacementWall");
        try {
            var wall = new StaticBody2D {
                Name = "Wall",
                CollisionLayer = CollisionLayers.Environment,
                Position = Origin + new Vector2(200f, 0f)
            };
            wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(40f, 400f) } });
            walled.AddChild(wall);
            PlayerController blocked = AddEinstein(walled);
            EinsteinRelativityRift rift = OpenRift(blocked);
            // The wall's near face is 180 px out; the rift lands just short of it.
            float thrown = rift.ActiveRiftCenter.X - blocked.GlobalPosition.X;
            AssertThat(thrown > 170f && thrown < 180f).IsTrue();
        } finally {
            walled.Free();
        }
    }

    [TestCase]
    public void AnEmcSquaredBurstInsideTheRiftPullsTheEnemyToTheCentreAndLaunchesIt() {
        Node2D host = CreateHost("RiftCollapse");
        EnemyController enemy = null;
        try {
            PlayerController einstein = AddEinstein(host);
            EinsteinRelativityRift rift = OpenRift(einstein);
            Vector2 center = rift.ActiveRiftCenter;
            enemy = CreateEnemy("chrono_slasher", host, center + new Vector2(40f, 0f));

            var emc = einstein.GetNode<EinsteinEmc2Blast>("Special1");
            AssertThat(emc.BurstRadius).IsEqual(72f);
            emc.DetonateForTest(center + new Vector2(20f, 0f));
            AssertThat(rift.RiftActive).OverrideFailureMessage("The collapse must end the rift at once.").IsFalse();
            AssertThat(rift.IsCollapsing).IsTrue();

            for (int frame = 0; frame < KitMotionRules.RiftCollapsePullFrames; frame++) rift._PhysicsProcess(Step);

            AssertThat(rift.IsCollapsing).IsFalse();
            AssertThat(enemy.GlobalPosition.DistanceTo(center) < 1f)
                .OverrideFailureMessage("The caught enemy was not pulled to the rift centre.")
                .IsTrue();
            AssertThat(enemy.Velocity.Y < 0f)
                .OverrideFailureMessage("The collapse must launch the enemy upward.")
                .IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheCollapseRidesItsEmcSquaredExecutionsFirstSpecialSlot() {
        // D4 (2026-10-11) with design E01: the collapse is "a 0-damage,
        // Special-class hit of the same E=mc² execution", so it shares the
        // burst's first-Special slot on the mob's stun chain and charges standard
        // poise x1.0 — never as a second, taxed Special.
        Node2D host = CreateHost("RiftCollapseChain");
        try {
            PlayerController einstein = AddEinstein(host);
            EinsteinRelativityRift rift = OpenRift(einstein);
            Vector2 center = rift.ActiveRiftCenter;
            EnemyController enemy = CreateEnemy("chrono_slasher", host, center + new Vector2(40f, 0f));
            AssertThat(enemy.Data.Tier).IsEqual(EnemyTier.Standard);
            AssertThat(enemy.Data.StunResistance).IsEqual(0f);

            var emc = einstein.GetNode<EinsteinEmc2Blast>("Special1");
            string emcID = emc.Data.AbilityID;
            AssertThat(emcID).IsEqual("einstein_mass_energy_conversion");
            emc.DetonateForTest(center + new Vector2(20f, 0f));
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            AssertThat(enemy.ChainFirstSpecialID).IsEqual(emcID);
            AssertThat(rift.IsCollapsing).IsTrue();

            for (int frame = 0; frame < KitMotionRules.RiftCollapsePullFrames; frame++) rift._PhysicsProcess(Step);
            AssertThat(rift.IsCollapsing).IsFalse();

            // The burst (12 frames, the first hit, x1.0) plus the collapse
            // (30 frames, V7.4-diminished to 15, still E=mc²'s: x1.0).
            AssertThat(enemy.ChainFirstSpecialID).IsEqual(emcID);
            float burstFrames = emc.Data.HitstunFrames;
            float collapseFrames = EinsteinRelativityRift.CollapseLaunchHitstunFrames
                * FTT.Combat.EnemyStaggerRules.SpecialStunDiminishFactor;
            AssertThat(burstFrames).IsEqual(12f);
            AssertThat(enemy.StaggerBudgetSeconds).IsEqualApprox((burstFrames + collapseFrames) / 60f, 0.001f);
            AssertThat(enemy.IsArmoredRecovery).IsFalse();
        } finally {
            host.Free();
        }
    }

    /// <summary>
    /// CM-FIXA-STORY (decision 2.4, Q2 "startup only"): the collapse's launch on a
    /// caught <see cref="PlayerController"/> is that hit's knockback and stun, so
    /// it reads the same armour <c>ResolveIncomingHit</c> reads. The Level 13
    /// shape: an Einstein clone on the opposing slot collapses its rift on a hero
    /// in its paid Ultimate's startup — the Ultimate must keep executing and the
    /// hero must not be launched. An unarmoured hero is still launched and stunned.
    /// </summary>
    [TestCase]
    public void ARiftCollapseNeitherLaunchesNorInterruptsAHeroInItsUltimateStartup() {
        Node2D host = CreateHost("RiftCollapseArmour");
        PlayerController hero = null;
        try {
            EinsteinRelativityRift rift = OpenRift(AddOpposingEinstein(host));
            hero = AddHeroInRift(host, "joan", rift.ActiveRiftCenter);
            BaseSpecial ultimate = StartUltimate(hero);
            AssertThat(ultimate).OverrideFailureMessage("Harness: the hero's Ultimate did not start.").IsNotNull();
            AssertThat(ultimate.CurrentPhase).IsEqual(AbilityPhase.Startup);
            AssertThat(hero.HasActiveHyperArmorAgainst(AttackClass.Special)).IsTrue();

            AssertThat(rift.TryCollapse(rift.ActiveRiftCenter)).IsTrue();
            for (int frame = 0; frame < KitMotionRules.RiftCollapsePullFrames; frame++) rift._PhysicsProcess(Step);
            AssertThat(rift.IsCollapsing).IsFalse();

            AssertThat(ultimate.IsExecuting)
                .OverrideFailureMessage("A Rift Collapse must not interrupt an armoured Ultimate startup.")
                .IsTrue();
            AssertThat(hero.CurrentState).IsEqual(CharacterState.UsingUltimate);
            AssertThat(hero.Velocity.Y >= 0f)
                .OverrideFailureMessage($"The armour denies the collapse's launch; vy {hero.Velocity.Y:0.0}.")
                .IsTrue();
        } finally {
            if (hero != null && GodotObject.IsInstanceValid(hero)) InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            host.Free();
        }

        // Positive control: the same collapse on an unarmoured hero launches and stuns it.
        Node2D control = CreateHost("RiftCollapseUnarmoured");
        try {
            EinsteinRelativityRift rift = OpenRift(AddOpposingEinstein(control));
            PlayerController idle = AddHeroInRift(control, "joan", rift.ActiveRiftCenter);
            AssertThat(idle.HasActiveHyperArmorAgainst(AttackClass.Special)).IsFalse();
            AssertThat(rift.TryCollapse(rift.ActiveRiftCenter)).IsTrue();
            for (int frame = 0; frame < KitMotionRules.RiftCollapsePullFrames; frame++) rift._PhysicsProcess(Step);
            AssertThat(idle.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(idle.Velocity.Y < 0f).OverrideFailureMessage("The collapse must launch an unarmoured hero upward.").IsTrue();
        } finally {
            control.Free();
        }
    }

    /// <summary>An Einstein on the opposing slot (the Level 13 clone's), whose rift catches player 0.</summary>
    private static PlayerController AddOpposingEinstein(Node2D host) {
        PlayerController clone = CharacterFactory.CreateCharacter("einstein", 1, applyStoryProgression: false);
        host.AddChild(clone);
        clone.GlobalPosition = Origin;
        return clone;
    }

    /// <summary>The campaign-slot hero, airborne inside the rift. Positioned before it enters the tree.</summary>
    private static PlayerController AddHeroInRift(Node2D host, string heroID, Vector2 center) {
        PlayerController hero = CharacterFactory.CreateCharacter(heroID, 0, applyStoryProgression: false);
        hero.Position = center + new Vector2(40f, 0f);
        host.AddChild(hero);
        hero.TransitionTo(CharacterState.Airborne);
        return hero;
    }

    /// <summary>Fills the meter and presses the Ultimate (the StoryUltimateArmorTests harness); null when refused.</summary>
    private static BaseSpecial StartUltimate(PlayerController hero) {
        hero.SetUltimateMeter(InfluenceMeterRules.MaxValue);
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Ultimate));
        InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
        hero._PhysicsProcess(Step);
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None, GameplayButtons.Ultimate));
        var ultimate = hero.GetNode<BaseSpecial>("Ultimate");
        return hero.CurrentState == CharacterState.UsingUltimate && ultimate.IsExecuting ? ultimate : null;
    }

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

    private static EnemyController CreateEnemy(string enemyID, Node host, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
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
        return enemy;
    }

    private static Node2D CreateHost(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }
}
