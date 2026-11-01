using System.Threading.Tasks;
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
/// The lingering-kit pass's Sandstorm Vortex (KL-SZONE): the 2.5 u radius, a 7 u/s pull that beats a
/// hero's run, the roll / air-dodge / boss / dormant-member exemptions, no slow while inside (the
/// 1.2 / 1.75 s slow lands once on leaving or when the Vortex ends), and the final launch paying
/// Momentum Detonation once.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CleopatraVortexLingeringTests {
    private const double Step = 1.0 / 60.0;
    private static readonly Vector2 Origin = new(-72000f, 600f);

    [TestCase]
    public void TheVortexIsTwoAndAHalfUnitsWide() {
        Node2D host = CreateHost("VortexRadius");
        try {
            CleopatraSandstormVortex vortex = Open(AddCleopatra(host));
            AssertThat(vortex.ActiveVortexRadiusPixels).IsEqual(150f);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ThePullBeatsAHeroRunningAwayAtThreeSeventyTwoPixelsASecond() {
        Node2D host = CreateHost("VortexPull");
        try {
            CleopatraSandstormVortex vortex = Open(AddCleopatra(host));
            EnemyController enemy = CreateEnemy(host, vortex.ActiveVortexCenter + new Vector2(100f, 0f));
            float start = enemy.GlobalPosition.X;
            for (int frame = 0; frame < 10; frame++) vortex._PhysicsProcess(Step);
            float pulled = start - enemy.GlobalPosition.X;
            // Joan's run is 6.2 px a frame; the pull is 7.
            AssertThat(pulled >= 65f)
                .OverrideFailureMessage($"Ten frames pulled {pulled} px; the pull must beat a 6.2 px/frame run.")
                .IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ARollingTargetABossAndADormantMemberAreNotPulled() {
        Node2D host = CreateHost("VortexExemptions");
        BossController boss = null;
        try {
            AddCleopatra(host);
            // A roller: the hero on the opposing slot, mid-roll.
            var floor = new StaticBody2D { Name = "RollFloor", CollisionLayer = CollisionLayers.Environment, CollisionMask = 0 };
            floor.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) }, Position = new Vector2(0f, 20f)
            });
            floor.Position = Origin + new Vector2(0f, 400f);
            host.AddChild(floor);
            PlayerController roller = CharacterFactory.CreateCharacter("joan", 1, applyStoryProgression: false);
            host.AddChild(roller);
            roller.GlobalPosition = floor.Position + new Vector2(0f, -8f);
            for (int frame = 0; frame < 60; frame++) {
                var idle = new BufferedInputSource();
                idle.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
                InputManager.Instance.SetInputSource(roller.PlayerIndex, idle);
                roller._PhysicsProcess(Step);
            }
            AssertThat(CleopatraSandstormVortex.IsPullable(roller)).OverrideFailureMessage("Control: standing.").IsTrue();
            for (int frame = 0; frame < 30 && !roller.IsRollTravelOrAirDodge; frame++) {
                var source = new BufferedInputSource();
                source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, frame == 0 ? GameplayButtons.Roll : GameplayButtons.None));
                InputManager.Instance.SetInputSource(roller.PlayerIndex, source);
                roller._PhysicsProcess(Step);
            }
            AssertThat(roller.IsRollTravelOrAirDodge).OverrideFailureMessage("Harness: the roll never reached its travel.").IsTrue();
            AssertThat(CleopatraSandstormVortex.IsPullable(roller)).IsFalse();
            InputManager.Instance.ClearInputSource(roller.PlayerIndex);

            boss = Boss(knockbackImmune: true);
            host.AddChild(boss);
            AssertThat(CleopatraSandstormVortex.IsPullable(boss)).IsFalse();
            boss.Data.IsKnockbackImmune = false;
            AssertThat(CleopatraSandstormVortex.IsPullable(boss)).OverrideFailureMessage("Control: a boss that can be pushed.").IsTrue();

            EnemyController member = CreateEnemy(host, Origin);
            AssertThat(CleopatraSandstormVortex.IsPullable(member)).IsTrue();
            member.JoinEncounter(7UL, "vortex_test_lock", new Rect2(-100000f, -100000f, 200000f, 200000f),
                Origin, dormant: true, facesRight: true);
            AssertThat(CleopatraSandstormVortex.IsPullable(member)).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public async Task NoSlowWhileInsideTheSlowLandsOnLeavingAndAnyoneStillInsideGetsItWhenTheVortexEnds() {
        Node2D host = CreateHost("VortexSlow");
        try {
            CleopatraSandstormVortex vortex = Open(AddCleopatra(host));
            Vector2 center = vortex.ActiveVortexCenter;
            EnemyController leaver = CreateEnemy(host, center + new Vector2(100f, 0f));
            EnemyController stayer = CreateEnemy(host, center + new Vector2(-20f, 0f));

            // Through a damage tick (0.4 s) inside: damage lands, no slow.
            for (int frame = 0; frame < 30; frame++) vortex._PhysicsProcess(Step);
            AssertThat(leaver.ActiveStatuses.Control.IsActive).OverrideFailureMessage("No slow while inside.").IsFalse();
            AssertThat(stayer.ActiveStatuses.Control.IsActive).IsFalse();

            leaver.GlobalPosition = center + new Vector2(0f, -3000f);
            // The physics server takes the moved hurtbox's place on its next frame.
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            vortex._PhysicsProcess(Step);
            StatusEffectData slow = leaver.ActiveStatuses.Control;
            AssertThat(slow.Type).IsEqual(StatusType.TimeDilation);
            AssertThat(slow.Intensity).IsEqualApprox(1.2f, 0.001f);
            AssertThat(slow.RemainingSeconds).IsEqualApprox(1.75f, 0.1f);
            AssertThat(stayer.ActiveStatuses.Control.IsActive).IsFalse();

            for (int frame = 0; frame < 200 && vortex.VortexActive; frame++) vortex._PhysicsProcess(Step);
            AssertThat(vortex.VortexActive).IsFalse();
            AssertThat(stayer.ActiveStatuses.Control.Type).IsEqual(StatusType.TimeDilation);
            AssertThat(stayer.ActiveStatuses.Control.RemainingSeconds).IsEqualApprox(1.75f, 0.1f);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheFinalLaunchPaysMomentumDetonationOnceForTheWholeVortex() {
        Node2D host = CreateHost("VortexMomentum");
        try {
            PlayerController cleopatra = AddCleopatra(host);
            CleopatraSandstormVortex vortex = Open(cleopatra);
            Vector2 center = vortex.ActiveVortexCenter;
            EnemyController first = CreateEnemy(host, center + new Vector2(20f, 0f));
            EnemyController second = CreateEnemy(host, center + new Vector2(-20f, 0f));
            first.CurrentHP = 500;
            second.CurrentHP = 500;
            cleopatra.RestoreMomentumPips(1f);

            for (int frame = 0; frame < 200 && vortex.VortexActive; frame++) vortex._PhysicsProcess(Step);
            AssertThat(vortex.VortexActive).IsFalse();
            AssertThat(first.CurrentHP).IsLess(500);
            AssertThat(second.CurrentHP).IsLess(500);
            AssertThat(cleopatra.MomentumPips - 1f)
                .OverrideFailureMessage("Detonation pays 0.2 pip once, on the final launch, never per body or per churn tick.")
                .IsEqualApprox(0.2f, 0.01f);
        } finally {
            host.Free();
        }
    }

    private static CleopatraSandstormVortex Open(PlayerController cleopatra) {
        var vortex = cleopatra.GetNode<CleopatraSandstormVortex>("Special2");
        AssertThat(vortex.TryExecute()).IsTrue();
        for (int frame = 0; frame < 120 && !vortex.VortexActive; frame++) vortex._PhysicsProcess(Step);
        AssertThat(vortex.VortexActive).IsTrue();
        return vortex;
    }

    private static PlayerController AddCleopatra(Node2D host) {
        PlayerController cleopatra = CharacterFactory.CreateCharacter("cleopatra", 0, applyStoryProgression: false);
        host.AddChild(cleopatra);
        cleopatra.GlobalPosition = Origin;
        return cleopatra;
    }

    private static BossController Boss(bool knockbackImmune) {
        var data = new BossData {
            BossID = "vortex_scratch_boss",
            DisplayNameKey = "boss_borgia_inquisitor_name",
            MaxHP = 500,
            MoveSpeed = 4f,
            MeleeRangeThreshold = 3f,
            RangedRangeThreshold = 8f,
            RestCooldown = 1f,
            AttackPattern = BossAttackPattern.DistanceBased,
            PhaseThresholds = System.Array.Empty<float>(),
            BossAbilities = System.Array.Empty<EnemyAbilityData>(),
            IsKnockbackImmune = knockbackImmune
        };
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/enemies/Boss.tscn");
        var boss = scene.Instantiate<BossController>();
        boss.SelectionSeed = 4243;
        boss.Data = data;
        return boss;
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
