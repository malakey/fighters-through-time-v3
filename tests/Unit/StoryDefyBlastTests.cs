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
/// MM-SDEFY (meter and Defy update 2026-10-31, plan 2.5 and 2.6, P4 to P10, P22, P23): the Story
/// Defy blast and heal. On frame 6 every hostile character within 150 px takes an assigned push, a
/// 30-frame non-launching stagger and 8 frames of hitstop; the Defier heals its echo pool or 10 %.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryDefyBlastTests {

    private const float Inside = 100f;
    private const float Outside = 260f;

    [TestCase]
    public void TheBlastStaggersMobsInsideTheRadiusAndSparesThoseOutside() {
        Run((hero, tree) => {
            EnemyController near = CreateMob(tree, Inside);
            EnemyController far = CreateMob(tree, Outside);
            try {
                Defy(hero, untilBlast: true);
                AssertThat(near.CurrentState).IsEqual(EnemyState.Stunned);
                AssertThat(far.CurrentState).IsNotEqual(EnemyState.Stunned);
            } finally {
                near.Free();
                far.Free();
            }
        });
    }

    [TestCase]
    public void TheMobIsPushedAwayWithTheAssignedVectorTakesNoDamageAndTheDefierEarnsNothing() {
        Run((hero, tree) => {
            EnemyController right = CreateMob(tree, Inside);
            EnemyController left = CreateMob(tree, -Inside);
            try {
                int hpRight = right.CurrentHP;
                int hpLeft = left.CurrentHP;
                float pips = hero.MomentumPips;
                Defy(hero, untilBlast: true);
                AssertThat(right.Velocity)
                    .OverrideFailureMessage("P7: assigned (+360, -360) px/s away from the Defier.")
                    .IsEqual(new Vector2(360f, -360f));
                AssertThat(left.Velocity).IsEqual(new Vector2(-360f, -360f));
                AssertThat(right.CurrentHP).IsEqual(hpRight);
                AssertThat(left.CurrentHP).IsEqual(hpLeft);
                // Passive regen (10 units a frame) runs through the Defy's few frames; a landed
                // string hit would add a twentieth of a pip, so any gain under 12 frames of regen is no hit.
                AssertThat(hero.MomentumPips - pips)
                    .OverrideFailureMessage("The blast is not a hit: no Momentum beyond passive regen.")
                    .IsLess(12f * BasicComboRules.MomentumRegenUnitsPerFrame / BasicComboRules.MomentumUnitsPerPip + 0.001f);
                AssertThat(hero.CurrentUltimateMeter)
                    .OverrideFailureMessage("The blast earns no Influence: only the 50 cost is gone.")
                    .IsEqualApprox(50f, 0.001f);
            } finally {
                right.Free();
                left.Free();
            }
        });
    }

    [TestCase]
    public void TheStaggerIsThirtyFramesAndEveryoneFreezesEightFramesIncludingTheDefier() {
        Run((hero, tree) => {
            EnemyController mob = CreateMob(tree, Inside);
            try {
                Defy(hero, untilBlast: true);
                AssertThat(mob.HitstopFramesRemaining).IsEqual(BasicComboRules.DefyBlastHitstopFrames);
                AssertThat(hero.IsInHitstop).IsTrue();
                // Run the mob's own clock: the freeze, then the 30-frame stun.
                int frames = 0;
                while (mob.CurrentState == EnemyState.Stunned && frames < 200) {
                    mob._PhysicsProcess(1f / 60f);
                    frames++;
                }
                int expected = BasicComboRules.DefyBlastHitstopFrames + BasicComboRules.DefyBlastStaggerFrames;
                AssertThat(frames >= expected && frames <= expected + 1)
                    .OverrideFailureMessage($"8 frozen ticks plus a 30-frame stagger; the mob stayed stunned {frames} ticks.")
                    .IsTrue();
            } finally {
                mob.Free();
            }
        });
    }

    [TestCase]
    public void TheBlastGoesThroughTheGetupArmourWindow() {
        Run((hero, tree) => {
            EnemyController mob = CreateMob(tree, Inside);
            try {
                mob.ApplyStun(0.1f);
                for (int i = 0; i < 10; i++) mob._PhysicsProcess(1f / 60f);
                AssertThat(mob.IsGetupArmored)
                    .OverrideFailureMessage("setup: a natural stun expiry arms the getup window.")
                    .IsTrue();
                Defy(hero, untilBlast: true);
                AssertThat(mob.CurrentState)
                    .OverrideFailureMessage("P22: the blast ignores the V7.4 armour windows.")
                    .IsEqual(EnemyState.Stunned);
                AssertThat(mob.IsGetupArmored).IsFalse();
            } finally {
                mob.Free();
            }
        });
    }

    [TestCase]
    public void DormantLockMembersAndDrillInvulnerableBodiesAreSkipped() {
        Run((hero, tree) => {
            EnemyController dormant = CreateMob(tree, Inside);
            EnemyController drill = CreateMob(tree, -Inside);
            try {
                dormant.JoinEncounter(7UL, "defy_test_lock", new Rect2(-1000f, -1000f, 2000f, 2000f),
                    dormant.GlobalPosition, dormant: true, facesRight: true);
                AssertThat(dormant.IsEncounterDormant).IsTrue();
                drill.DrillInvulnerable = true;
                Defy(hero, untilBlast: true);
                AssertThat(dormant.CurrentState).IsNotEqual(EnemyState.Stunned);
                AssertThat(drill.CurrentState).IsNotEqual(EnemyState.Stunned);
            } finally {
                dormant.Free();
                drill.Free();
            }
        });
    }

    [TestCase]
    public void ABossTelegraphOrActiveWindowIsCutIntoRecoveryAndAnImmuneBossIsNotMoved() {
        Run((hero, tree) => {
            BossController boss = CreateBoss(tree, Inside, knockbackImmune: true, out EnemyAbilityData melee);
            try {
                boss.BeginAbility(melee, new Vector2(200f, 0f));
                AssertThat(boss.AbilityPhase).IsEqual(EnemyAbilityPhase.Telegraph);
                Defy(hero, untilBlast: true);
                AssertThat(boss.AbilityPhase)
                    .OverrideFailureMessage("P23: the telegraph is cut into the recovery.")
                    .IsEqual(EnemyAbilityPhase.Recovery);
                AssertThat(boss.Velocity.X)
                    .OverrideFailureMessage("A knockback-immune boss keeps its feet.")
                    .IsEqual(0f);
                AssertThat(boss.CurrentHP).IsEqual(boss.ScaledMaxHP);
            } finally {
                boss.Free();
            }
        });
    }

    [TestCase]
    public void ABossThatCanBePushedIsPushedNeverStunnedAndAnOutOfRangeBossIsUntouched() {
        Run((hero, tree) => {
            BossController boss = CreateBoss(tree, Inside, knockbackImmune: false, out EnemyAbilityData melee);
            BossController far = CreateBoss(tree, Outside, knockbackImmune: false, out EnemyAbilityData melee2);
            try {
                boss.BeginAbility(melee, new Vector2(200f, 0f));
                far.BeginAbility(melee2, new Vector2(200f, 0f));
                Defy(hero, untilBlast: true);
                AssertThat(boss.Velocity.X).IsGreater(0f);
                AssertThat(boss.CurrentState).IsNotEqual(BossState.Dead);
                AssertThat(far.Velocity).IsEqual(Vector2.Zero);
                AssertThat(far.AbilityPhase).IsEqual(EnemyAbilityPhase.Telegraph);
            } finally {
                boss.Free();
                far.Free();
            }
        });
    }

    [TestCase]
    public void TheBlastStaggerCannotBeBlockCancelledByAGroundedBlockHoldingVictim() {
        Run((hero, tree) => {
            // The hero stands in for any victim: the blast lands, the player holds Block throughout.
            AssertThat(hero.ApplyDefyBlast(new Vector2(360f, -360f), BasicComboRules.DefyBlastStaggerFrames, 0)).IsTrue();
            for (int frame = 0; frame < BasicComboRules.DefyBlastStaggerFrames - 2; frame++) {
                Step(hero, GameplayButtons.Block);
                AssertThat(hero.CurrentState)
                    .OverrideFailureMessage($"Frame {frame}: the blast stagger is not Block-cancellable (the sim sets the same gate).")
                    .IsEqual(CharacterState.Stunned);
            }
        });
    }

    [TestCase]
    public void ALaunchBufferedBeforeTheBlastNeverReplacesTheAssignedPush() {
        Run((hero, tree) => {
            // An earlier launching hit: a stashed launch waiting out a long freeze, flying left.
            hero.ApplyTutorialScriptedLaunch(new Vector2(-5f, 0f), 1f, 20);
            AssertThat(hero.HasPendingLaunch).IsTrue();
            AssertThat(hero.ApplyDefyBlast(new Vector2(360f, -360f), BasicComboRules.DefyBlastStaggerFrames, BasicComboRules.DefyBlastHitstopFrames)).IsTrue();
            AssertThat(hero.HasPendingLaunch)
                .OverrideFailureMessage("The blast clears the buffered launch the way the sim does.")
                .IsFalse();
            for (int frame = 0; frame < 60 && hero.HitstopFramesRemaining > 0; frame++) Step(hero, GameplayButtons.None);
            Step(hero, GameplayButtons.None);
            AssertThat(hero.Velocity.X)
                .OverrideFailureMessage("After the freeze the hero moves by the blast's push, not the earlier launch.")
                .IsGreater(0f);
        });
    }

    [TestCase]
    public void ABossTakesTheBlastsEightFrameFreezeWithoutBeingStunned() {
        Run((hero, tree) => {
            BossController boss = CreateBoss(tree, Inside, knockbackImmune: true, out _);
            try {
                AssertThat(boss.HitstopFramesRemaining).IsEqual(0);
                Defy(hero, untilBlast: true);
                AssertThat(boss.HitstopFramesRemaining)
                    .OverrideFailureMessage("The 8-frame freeze reaches every victim, a boss included.")
                    .IsEqual(BasicComboRules.DefyBlastHitstopFrames);
                AssertThat(boss.CurrentState).IsNotEqual(BossState.Dead);
            } finally {
                boss.Free();
            }
        });
    }

    [TestCase]
    public void TheMirrorCloneIsStaggeredButAnInvulnerableCloneIsNot() {
        Run((hero, tree) => {
            PlayerController clone = CreateClone(tree, Inside);
            PlayerController protectedClone = CreateClone(tree, -Inside);
            try {
                protectedClone.SeedDefyFrameForTest(1); // its own Defy startup: invulnerable
                int hp = clone.CurrentHP;
                Defy(hero, untilBlast: true);
                AssertThat(clone.CurrentState)
                    .OverrideFailureMessage("R4 / P9: the clone is treated as a fighter.")
                    .IsEqual(CharacterState.Stunned);
                AssertThat(clone.Velocity.X).IsGreater(0f);
                AssertThat(clone.CurrentHP).IsEqual(hp);
                AssertThat(protectedClone.CurrentState)
                    .OverrideFailureMessage("An invulnerable target is untouched.")
                    .IsEqual(CharacterState.Defying);
            } finally {
                protectedClone.SeedDefyFrameForTest(0);
                clone.Free();
                protectedClone.Free();
            }
        });
    }

    [TestCase]
    public void TheDefierHealsItsEchoPoolOrTheTenPercentFloorAndForfeitsTheRest() {
        Run((hero, tree) => {
            // An empty pool still heals the floor (P10).
            hero.ApplyDamage(hero.MaximumHP / 2);
            int missing = hero.MaximumHP - hero.CurrentHP;
            int floor = (hero.MaximumHP + 9) / 10;
            int hp = hero.CurrentHP;
            Defy(hero, untilBlast: true);
            AssertThat(hero.CurrentHP)
                .OverrideFailureMessage("An empty echo pool heals the 10 % floor.")
                .IsEqual(hp + Math.Min(missing, floor));

            // Ride the Defy out so the next hit is not refused by the startup.
            for (int i = 0; i < 60; i++) Step(hero, GameplayButtons.None);
            AssertThat(hero.IsDefying).IsFalse();
            // A big pool heals the pool, up to the missing HP, and is then empty.
            hero.ApplyDamage(10);
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                AttackID = "test.hit", HitboxID = "test", AttackClass = AttackClass.Basic,
                Damage = 30f, HitOrigin = new Vector2(-40f, -30f), AttackerFacingRight = true
            });
            float pool = hero.EchoPool;
            AssertThat(pool).IsGreater(0f);
            int missingNow = hero.MaximumHP - hero.CurrentHP;
            int before = hero.CurrentHP;
            int healed = hero.CashInEchoPool();
            AssertThat(healed)
                .IsEqual(BasicComboRules.DefyHealPoints(missingNow, Mathf.RoundToInt(pool), hero.MaximumHP));
            AssertThat(hero.CurrentHP).IsEqual(before + healed);
            AssertThat(hero.EchoPool).OverrideFailureMessage("The pool is forfeited.").IsEqual(0f);
        });
    }

    // === Harness ===

    private static void Run(Action<PlayerController, SceneTree> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var floor = new StaticBody2D {
            Name = "DefyBlastFloor", CollisionLayer = CollisionLayers.Environment, CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(8000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        tree.Root.AddChild(floor);
        PlayerController hero = CharacterFactory.CreateCharacter(
            "einstein", 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
        tree.Root.AddChild(hero);
        try {
            hero.GlobalPosition = new Vector2(0f, -8f);
            for (int frame = 0; frame < 120 && !hero.IsOnFloor(); frame++) {
                hero.Velocity = new Vector2(hero.Velocity.X, 400f);
                Step(hero, GameplayButtons.None);
            }
            for (int frame = 0; frame < 10; frame++) Step(hero, GameplayButtons.None);
            hero.SetUltimateMeter(100f);
            body(hero, tree);
        } finally {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            hero.Free();
            floor.Free();
        }
    }

    // Presses Defy and runs the hero through its blast tick (frame 6).
    private static void Defy(PlayerController hero, bool untilBlast) {
        Step(hero, GameplayButtons.Defy);
        while (untilBlast && hero.DefyFrame < BasicComboRules.DefyBlastFrame) Step(hero, GameplayButtons.None);
    }

    private static void Step(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }

    private static EnemyController CreateMob(SceneTree tree, float x) {
        EnemyData canonical = FTT.Core.AuthoredResources.Load<EnemyData>("res://resources/Enemies/chrono_slasher.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        tree.Root.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = new Vector2(x, 0f);
        return enemy;
    }

    private static BossController CreateBoss(SceneTree tree, float x, bool knockbackImmune, out EnemyAbilityData melee) {
        melee = new EnemyAbilityData {
            AbilityID = "a.slow", Archetype = EnemyAbilityArchetype.MeleeStrike,
            RangeClass = EnemyAbilityRangeClass.Any, SelectionWeight = 1f,
            TelegraphFrames = 40, ActiveFrames = 4, RecoveryFrames = 10, Damage = 10f,
            HitboxSize = new Vector2(60f, 60f), HitboxOffset = new Vector2(50f, -40f)
        };
        var data = new BossData {
            BossID = "test_boss", DisplayNameKey = "boss_borgia_inquisitor_name", MaxHP = 500,
            MeleeRangeThreshold = 3f, RangedRangeThreshold = 8f, RestCooldown = 1f,
            AttackPattern = BossAttackPattern.DistanceBased, PhaseThresholds = Array.Empty<float>(),
            BossAbilities = new[] { melee }, IsKnockbackImmune = knockbackImmune
        };
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/enemies/Boss.tscn");
        var boss = scene.Instantiate<BossController>();
        boss.SelectionSeed = 1;
        boss.Data = data;
        tree.Root.AddChild(boss);
        boss.ApplyData(data);
        boss.GlobalPosition = new Vector2(x, 0f);
        return boss;
    }

    private static PlayerController CreateClone(SceneTree tree, float x) {
        PlayerController clone = CharacterFactory.CreateCharacter(
            "einstein", 1, applyStoryProgression: false, applyLegacyUnlockLocks: false);
        clone.IsStoryHostile = true;
        tree.Root.AddChild(clone);
        clone.AddToGroup(MirrorParadoxController.MirrorGroup);
        clone.GlobalPosition = new Vector2(x, 0f);
        return clone;
    }
}
