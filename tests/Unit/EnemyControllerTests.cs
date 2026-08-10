using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 4 Section 3.2: standard/elite enemy state machine, telegraphed attacks,
/// elite ability cycling, flying/phasing behavior, death, and rewind policy.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EnemyControllerTests {
    private const float Step = 1f / 60f;

    [TestCase]
    public void AuthoredEnemyScenesSatisfyTheirPackageZeroSceneContracts() {
        foreach (string path in new[] {
            "res://scenes/enemies/StandardEnemy.tscn",
            "res://scenes/enemies/EliteEnemy.tscn",
            "res://scenes/enemies/Boss.tscn"
        }) {
            PackedScene scene = ResourceLoader.Load<PackedScene>(path);
            AssertObject(scene).IsNotNull();
            Node instance = scene.Instantiate();
            var marker = instance.GetNodeOrNull<ContentTemplateMarker>("ContentContract");
            AssertObject(marker).IsNotNull();
            AssertThat(marker.ValidateTemplate().Count).IsEqual(0);
            instance.Free();
        }
    }

    [TestCase]
    public void SpawnedEnemyStartsPatrollingAtFullScaledHealth() {
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Patrol);
            AssertThat(enemy.CurrentHP).IsEqual(enemy.ScaledMaxHP);
            AssertThat(enemy.ScaledMaxHP).IsEqual(StoryDifficultyTuning.ScaleEnemyHP(
                enemy.Data.MaxHP, StoryDifficultyTuning.CurrentStoryDifficulty));
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void StunTransitionsToStunnedAndReturnsToPatrolWhenItExpires() {
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            enemy.ApplyStun(0.1f);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            for (int frame = 0; frame < 10; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Patrol);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void StunResistanceShortensIncomingHitstunForElites() {
        EnemyController elite = CreateEnemy("steam_automaton");
        try {
            // steam_automaton authors StunResistance 0.5, so 0.02s of hitstun becomes 0.01s.
            elite.ApplyStun(0.02f);
            AssertThat(elite.CurrentState).IsEqual(EnemyState.Stunned);
            elite._PhysicsProcess(Step);
            AssertThat(elite.CurrentState).IsEqual(EnemyState.Patrol);
        } finally {
            elite.Free();
        }
    }

    [TestCase]
    public void BasicStringHitsFloorPostResistanceStunSoElitesCannotActBetweenChainHits() {
        EnemyController elite = CreateEnemy("steam_automaton");
        try {
            var hurtbox = elite.GetNode<Hurtbox>("Hurtbox");
            // A string opener carries HitstunFrames[0] = 30 frames (0.5s). At
            // StunResistance 0.5 the unfloored result would be 15 frames —
            // enough for the elite to act inside the chain gap. The combo's
            // "combo_N" Basic-class hits floor the post-resistance stun at the
            // shared 24 frames.
            hurtbox.TakeHit(new HitPayload {
                AttackID = "einstein.basic",
                HitboxID = "combo_1",
                AttackClass = AttackClass.Basic,
                Damage = 1f,
                HitstunDuration = BasicComboRules.HitstunFrames[0] / 60f,
                HitOrigin = Vector2.Zero,
                AttackerFacingRight = true
            });
            AssertThat(elite.CurrentState).IsEqual(EnemyState.Stunned);
            for (int frame = 0; frame < 20; frame++) elite._PhysicsProcess(Step);
            AssertThat(elite.CurrentState)
                .OverrideFailureMessage("The floored stun must outlast the unfloored 15 frames.")
                .IsEqual(EnemyState.Stunned);
            for (int frame = 0; frame < 10; frame++) elite._PhysicsProcess(Step);
            AssertThat(elite.CurrentState).IsEqual(EnemyState.Patrol);

            // A non-string Basic-class source (Leonardo's turret, Tesla's coil
            // arcs) keeps its authored short stun: 0.15s at 0.5 resistance
            // recovers within a handful of frames.
            hurtbox.TakeHit(new HitPayload {
                AttackID = "leonardo.turret",
                HitboxID = "turret_shot",
                AttackClass = AttackClass.Basic,
                Damage = 1f,
                HitstunDuration = 0.15f,
                HitOrigin = Vector2.Zero,
                AttackerFacingRight = true
            });
            AssertThat(elite.CurrentState).IsEqual(EnemyState.Stunned);
            for (int frame = 0; frame < 10; frame++) elite._PhysicsProcess(Step);
            AssertThat(elite.CurrentState)
                .OverrideFailureMessage("Non-string Basic sources must not inherit the floor.")
                .IsEqual(EnemyState.Patrol);
        } finally {
            elite.Free();
        }
    }

    [TestCase]
    public void TelegraphRunsBeforeTheHitboxEverGoesLive() {
        EnemyController enemy = CreateEnemy("cyber_guard");
        var hitbox = enemy.GetNode<Hitbox>("Hitbox");
        try {
            EnemyAbilityData primary = enemy.Data.PrimaryAttack;
            AssertObject(primary).IsNotNull();
            enemy.BeginAttack(primary);
            AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Telegraph);

            for (int frame = 0; frame < primary.TelegraphFrames - 1; frame++) {
                enemy._PhysicsProcess(Step);
                AssertThat(hitbox.IsActive).IsFalse();
            }
            enemy._PhysicsProcess(Step);
            AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Active);
            AssertThat(hitbox.IsActive).IsTrue();
            AssertThat(hitbox.AppliedStatus).IsEqual(StatusType.StaticCharge);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void ElitesAlternateStandardAttacksWithSequentiallyCycledEliteAbilities() {
        EnemyController elite = CreateEnemy("tech_enforcer");
        try {
            elite.Data.EliteAbilityCooldown = 0f;
            EnemyAbilityData first = elite.SelectNextAttack();
            AssertThat(elite.LastAttackWasElite).IsTrue();
            AssertThat(first.Archetype).IsEqual(EnemyAbilityArchetype.ShieldBubble);

            EnemyAbilityData second = elite.SelectNextAttack();
            AssertThat(elite.LastAttackWasElite).IsFalse();
            AssertThat(ReferenceEquals(second, elite.Data.PrimaryAttack)).IsTrue();

            EnemyAbilityData third = elite.SelectNextAttack();
            AssertThat(elite.LastAttackWasElite).IsTrue();
            AssertThat(ReferenceEquals(third, first)).IsTrue();
        } finally {
            elite.Free();
        }
    }

    [TestCase]
    public void EliteAbilityCooldownBlocksTheAbilityUntilItElapses() {
        EnemyController elite = CreateEnemy("tech_enforcer");
        try {
            elite.SelectNextAttack();
            AssertThat(elite.LastAttackWasElite).IsTrue();
            elite.SelectNextAttack();
            AssertThat(elite.LastAttackWasElite).IsFalse();
            // The authored 8s cooldown is still running, so the elite ability is denied.
            elite.SelectNextAttack();
            AssertThat(elite.LastAttackWasElite).IsFalse();
        } finally {
            elite.Free();
        }
    }

    [TestCase]
    public void StandardEnemiesWithoutAnAuthoredPrimaryFallBackToTheLegacyScalarMelee() {
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            AssertObject(enemy.Data.PrimaryAttack).IsNull();
            EnemyAbilityData fallback = enemy.SelectNextAttack();
            AssertObject(fallback).IsNotNull();
            AssertThat(fallback.Archetype).IsEqual(EnemyAbilityArchetype.MeleeStrike);
            AssertThat(fallback.Damage).IsEqualApprox(enemy.Data.AttackDamage, 0.001f);
            AssertThat(fallback.TelegraphFrames).IsEqual(enemy.Data.AttackTelegraphFrames);
            AssertThat(fallback.ActiveFrames).IsEqual(enemy.Data.AttackActiveFrames);
            AssertThat(fallback.RecoveryFrames).IsEqual(enemy.Data.AttackRecoveryFrames);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void FlyingEnemiesIgnoreGravityWhilePatrolling() {
        EnemyController drone = CreateEnemy("hologram_drone");
        try {
            AssertThat(drone.Data.Behavior).IsEqual(DefaultBehavior.Flying);
            for (int frame = 0; frame < 20; frame++) drone._PhysicsProcess(Step);
            AssertThat(drone.Velocity.Y).IsEqualApprox(0f, 0.001f);
        } finally {
            drone.Free();
        }
    }

    [TestCase]
    public void PhasingEnemiesDropTheEnvironmentBitOnlyWhilePursuing() {
        uint patrolling = EnemyController.ResolveBodyMask(true, EnemyState.Patrol);
        uint chasing = EnemyController.ResolveBodyMask(true, EnemyState.Chase);
        uint attacking = EnemyController.ResolveBodyMask(true, EnemyState.Attacking);
        uint grounded = EnemyController.ResolveBodyMask(false, EnemyState.Chase);

        AssertThat(patrolling).IsEqual(CollisionLayers.EnemyBodyMask);
        AssertThat(grounded).IsEqual(CollisionLayers.EnemyBodyMask);
        AssertThat((chasing & CollisionLayers.Environment) == 0u).IsTrue();
        AssertThat((attacking & CollisionLayers.Environment) == 0u).IsTrue();
        AssertThat((chasing & CollisionLayers.OneWayPlatform) != 0u).IsTrue();
    }

    [TestCase]
    public void FrontalDamageReductionAppliesOnlyToHitsFromTheFacingSide() {
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            enemy.Data.FrontalDamageReduction = 0.5f;
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            enemy.GlobalPosition = Vector2.Zero;

            // Default facing is right; a hit originating to the right is frontal.
            float frontal = hurtbox.TakeHit(new HitPayload { Damage = 10f, HitOrigin = new Vector2(50f, 0f) });
            float rear = hurtbox.TakeHit(new HitPayload { Damage = 10f, HitOrigin = new Vector2(-50f, 0f) });

            AssertThat(Mathf.RoundToInt(frontal)).IsEqual(5);
            AssertThat(Mathf.RoundToInt(rear)).IsEqual(10);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void DeathRaisesTheKillPayloadImmediatelyAndReleasesAfterTheAnimationWindow() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node { Name = "EnemyDeathParent" };
        tree.Root.AddChild(parent);
        var pools = new PoolManager { Name = "EnemyDeathPools" };
        tree.Root.AddChild(pools);

        EnemyKilledPayload captured = default;
        int killCount = 0;
        void OnKilled(EnemyKilledPayload payload) { captured = payload; killCount++; }
        EventBus.Instance.OnEnemyKilled += OnKilled;
        try {
            EnemyController enemy = EnemyFactory.Spawn("steam_automaton", parent, new Vector2(100f, 0f));
            AssertObject(enemy).IsNotNull();
            enemy.TakeDamage(enemy.ScaledMaxHP);

            AssertThat(killCount).IsEqual(1);
            AssertThat(captured.EnemyID).IsEqual("steam_automaton");
            AssertThat(captured.IsElite).IsTrue();
            AssertThat(captured.ChronalDustDrop).IsEqual(10);
            AssertThat(captured.ItemDropChanceMultiplier).IsEqualApprox(1f, 0.0001f);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Dead);
            AssertThat(pools.GetStats(EnemyFactory.ElitePoolID).Value.Active).IsEqual(1);

            int frames = Mathf.CeilToInt(EnemyController.DeathAnimationSeconds * 60f) + 2;
            for (int frame = 0; frame < frames; frame++) enemy._PhysicsProcess(Step);

            AssertThat(pools.GetStats(EnemyFactory.ElitePoolID).Value.Active).IsEqual(0);
        } finally {
            EventBus.Instance.OnEnemyKilled -= OnKilled;
            pools.ClearAllPools();
            pools.Free();
            parent.Free();
        }
    }

    [TestCase]
    public void RestoreCheckpointRewindPolicyRebuildsTheEncounterState() {
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            enemy.RewindPolicy = StoryRewindPolicy.RestoreCheckpointState;
            enemy.GlobalPosition = new Vector2(500f, 200f);
            enemy.CaptureCheckpointState("checkpoint_a");

            enemy.GlobalPosition = new Vector2(900f, 200f);
            enemy.TakeDamage(10);
            enemy.ApplyStatusEffect(StatusType.Root, 3f);
            AssertThat(enemy.StatusMoveMultiplier).IsEqualApprox(0f, 0.0001f);

            enemy.ApplyStoryRewind();

            AssertThat(enemy.GlobalPosition).IsEqual(new Vector2(500f, 200f));
            AssertThat(enemy.CurrentHP).IsEqual(enemy.ScaledMaxHP);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Patrol);
            AssertThat(enemy.ActiveStatusType).IsEqual(StatusType.None);
            AssertThat(enemy.StatusMoveMultiplier).IsEqualApprox(1f, 0.0001f);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void PreserveCurrentStateIsTheDefaultAndLeavesMobsMidFight() {
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            AssertThat(enemy.RewindPolicy).IsEqual(StoryRewindPolicy.PreserveCurrentState);
            enemy.GlobalPosition = new Vector2(777f, 0f);
            enemy.TakeDamage(15);
            int hpAfterHit = enemy.CurrentHP;

            enemy.ApplyStoryRewind();

            AssertThat(enemy.CurrentHP).IsEqual(hpAfterHit);
            AssertThat(enemy.GlobalPosition.X).IsEqualApprox(777f, 0.001f);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void RewindFreezeStopsTelegraphsAndSimulation() {
        EnemyController enemy = CreateEnemy("cyber_guard");
        try {
            enemy.BeginAttack(enemy.Data.PrimaryAttack);
            AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Telegraph);

            enemy.SetStoryRewindFrozen(true);
            AssertThat(enemy.IsStoryRewindFrozen).IsTrue();
            AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Idle);

            for (int frame = 0; frame < 30; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.Velocity).IsEqual(Vector2.Zero);

            enemy.SetStoryRewindFrozen(false);
            AssertThat(enemy.IsStoryRewindFrozen).IsFalse();
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void ItemDropChanceMultipliesTheDifficultyProfileChance() {
        StoryDropProfile normal = StoryDropTable.LoadDefault().GetProfile(Difficulty.Normal);
        // Normal authors a 0.15 chance; a 2x enemy multiplier lifts it to 0.30.
        AssertThat(normal.ShouldDrop(0.2f, 1f)).IsFalse();
        AssertThat(normal.ShouldDrop(0.2f, 2f)).IsTrue();
        AssertThat(normal.ShouldDrop(0.35f, 2f)).IsFalse();
        AssertThat(normal.ShouldDrop(0.1f, 0f)).IsFalse();
        // The single-argument overload keeps the untouched profile behavior.
        AssertThat(normal.ShouldDrop(0.1f)).IsTrue();
    }

    [TestCase]
    public void EveryPrototypeEnemyKeepsTheNeutralItemDropMultiplier() {
        foreach (string enemyID in new[] {
            "chrono_slasher", "cyber_guard", "hologram_drone", "steam_automaton", "tech_enforcer"
        }) {
            EnemyData data = FTT.Core.AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertThat(data.ItemDropChance).IsEqualApprox(1f, 0.0001f);
            AssertThat(data.PlaceholderTint.A > 0f).IsTrue();
        }
    }

    // === Stand-off band (approach stops at attack range instead of pushbox contact) ===

    [TestCase]
    public void ChasingEnemyHaltsAtItsAttackRangeStandOffWithoutDisplacingThePlayer() {
        StaticBody2D floor = CreateStandOffFloor();
        PlayerController player = CreateTargetPlayer(new Vector2(300f, 0f));
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            // Park the attack after its opener so the run ends in the hold, not mid-swing.
            enemy.Data.AttackCooldown = 999f;
            Vector2 playerBefore = player.GlobalPosition;

            for (int frame = 0; frame < 400; frame++) enemy._PhysicsProcess(Step);

            float dist = enemy.GlobalPosition.DistanceTo(player.GlobalPosition);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Chase);
            AssertThat(enemy.IsStandOffEngaged).IsTrue();
            AssertThat(enemy.Velocity.X).IsEqualApprox(0f, 0.001f);
            // Halted at the engage line (85% of the authored attack range), which
            // must sit outside pushbox contact distance - the boxes never touch.
            AssertThat(dist <= enemy.AttackRangePixels * EnemyController.StandOffEngageFraction).IsTrue();
            var enemyPushbox = enemy.GetNode<CombatantPushbox>("Pushbox");
            var playerPushbox = player.GetNode<CombatantPushbox>("Pushbox");
            AssertThat(dist > (enemyPushbox.BoxSize.X + playerPushbox.BoxSize.X) * 0.5f).IsTrue();
            AssertThat(enemyPushbox.GetHorizontalOverlap(playerPushbox)).IsEqual(0f);
            // The approach never displaced the stationary target.
            AssertThat(player.GlobalPosition).IsEqual(playerBefore);
        } finally {
            enemy.Free();
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void EnemyStillLaunchesAttacksFromTheStandOffBandOnItsCooldownCadence() {
        StaticBody2D floor = CreateStandOffFloor();
        PlayerController player = CreateTargetPlayer(new Vector2(300f, 0f));
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            enemy.Data.AttackCooldown = 0.5f;
            var commitDistances = new System.Collections.Generic.List<float>();
            EnemyState previous = enemy.CurrentState;

            for (int frame = 0; frame < 900; frame++) {
                enemy._PhysicsProcess(Step);
                if (enemy.CurrentState == EnemyState.Attacking && previous != EnemyState.Attacking) {
                    commitDistances.Add(enemy.GlobalPosition.DistanceTo(player.GlobalPosition));
                }
                previous = enemy.CurrentState;
            }

            // The cadence survives the stand-off: repeated attacks keep committing.
            AssertThat(commitDistances.Count >= 3).IsTrue();
            var enemyPushbox = enemy.GetNode<CombatantPushbox>("Pushbox");
            var playerPushbox = player.GetNode<CombatantPushbox>("Pushbox");
            float contact = (enemyPushbox.BoxSize.X + playerPushbox.BoxSize.X) * 0.5f;
            // Every follow-up attack launches from inside striking distance but
            // outside pushbox contact - from the band, not from body overlap.
            for (int index = 1; index < commitDistances.Count; index++) {
                AssertThat(commitDistances[index] <= enemy.AttackRangePixels).IsTrue();
                AssertThat(commitDistances[index] > contact).IsTrue();
            }
            AssertThat(player.GlobalPosition).IsEqual(new Vector2(300f, 0f));
        } finally {
            enemy.Free();
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void SmallTargetShuffleInsideTheBandDoesNotRestartTheApproach() {
        StaticBody2D floor = CreateStandOffFloor();
        PlayerController player = CreateTargetPlayer(new Vector2(300f, 0f));
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            enemy.Data.AttackCooldown = 999f;
            for (int frame = 0; frame < 400; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.IsStandOffEngaged).IsTrue();
            float heldX = enemy.GlobalPosition.X;

            // A 25 px shuffle keeps the target inside the 110% release line
            // (chrono_slasher range 90 px, release 99 px): the enemy must not move.
            player.GlobalPosition += new Vector2(25f, 0f);
            for (int frame = 0; frame < 120; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.IsStandOffEngaged).IsTrue();
            AssertThat(enemy.Velocity.X).IsEqualApprox(0f, 0.001f);
            AssertThat(enemy.GlobalPosition.X).IsEqualApprox(heldX, 0.01f);

            // Beyond the release line the approach resumes.
            player.GlobalPosition += new Vector2(200f, 0f);
            for (int frame = 0; frame < 30; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.IsStandOffEngaged).IsFalse();
            AssertThat(enemy.GlobalPosition.X > heldX + 50f).IsTrue();
        } finally {
            enemy.Free();
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void FlyingChaserHoversAtItsProjectileRangeStandOffInsteadOfPressingIn() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var pools = new PoolManager { Name = "StandOffDronePools" };
        tree.Root.AddChild(pools);
        PlayerController player = CreateTargetPlayer(new Vector2(360f, 0f));
        EnemyController drone = CreateEnemy("hologram_drone");
        try {
            drone.GlobalPosition = new Vector2(0f, -80f);
            drone.Data.AttackCooldown = 999f;
            Vector2 playerBefore = player.GlobalPosition;

            for (int frame = 0; frame < 400; frame++) drone._PhysicsProcess(Step);

            // The projectile primary widens AttackRangePixels to 90% of the aggro
            // radius (360 px), so the drone holds far out instead of at melee reach.
            AssertThat(drone.AttackRangePixels).IsEqualApprox(360f, 0.001f);
            AssertThat(drone.CurrentState).IsEqual(EnemyState.Chase);
            AssertThat(drone.IsStandOffEngaged).IsTrue();
            // Flying hold decelerates both axes: the hover-approach fully stops.
            AssertThat(drone.Velocity.Length()).IsEqualApprox(0f, 0.001f);
            float dist = drone.GlobalPosition.DistanceTo(player.GlobalPosition);
            AssertThat(dist > 250f).IsTrue();
            AssertThat(dist <= drone.AttackRangePixels * EnemyController.StandOffResumeFraction).IsTrue();
            AssertThat(player.GlobalPosition).IsEqual(playerBefore);
        } finally {
            drone.Free();
            player.Free();
            pools.ClearAllPools();
            pools.Free();
        }
    }

    /// <summary>
    /// Instantiates the authored tier scene with a duplicated data resource so a
    /// test may retune fields without leaking into the shared canonical .tres.
    /// </summary>
    private static EnemyController CreateEnemy(string enemyID) {
        EnemyData canonical = FTT.Core.AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        return enemy;
    }

    /// <summary>
    /// A bare PlayerController (no hurtbox, so enemy attacks cannot knock it
    /// around) carrying the factory-shaped pushbox, registered in the "Players"
    /// group as a chase target. It never ticks its own physics in a synchronous
    /// test, so any position change is displacement caused by the enemy.
    /// </summary>
    private static PlayerController CreateTargetPlayer(Vector2 position) {
        var player = new PlayerController { Name = "StandOffTargetPlayer", Position = position };
        var pushbox = new CombatantPushbox {
            Name = "Pushbox",
            BoxSize = new Vector2(30f, 48f),
            Position = new Vector2(0f, -28f),
            CollisionLayer = CollisionLayers.Player,
            CollisionMask = CollisionLayers.Enemy,
            Monitoring = false,
            Monitorable = false
        };
        pushbox.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = pushbox.BoxSize } });
        player.AddChild(pushbox);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        player.AddToGroup("Players");
        return player;
    }

    /// <summary>Environment-layer floor with its top surface at y = 0 so grounded
    /// bodies (feet on the node origin) stand still while a test drives frames.</summary>
    private static StaticBody2D CreateStandOffFloor() {
        var floor = new StaticBody2D {
            Name = "StandOffFloor",
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
