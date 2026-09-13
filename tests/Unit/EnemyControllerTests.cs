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

            // V7.4: the naturally-expired stun armed the getup armor window,
            // which would deny the follow-up stun below — wait it out first
            // (its own contract is pinned in the stagger-discipline tests).
            AssertThat(elite.IsGetupArmored).IsTrue();
            for (int frame = 0; frame < EnemyStaggerRules.GetupArmorFrames + 2; frame++) {
                elite._PhysicsProcess(Step);
            }
            AssertThat(elite.IsGetupArmored).IsFalse();

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
            // V7.3 dual-channel telegraph: a legacy mob melee is Basic-class,
            // so its tint sits in the shared white/yellow family — the old
            // orange (1, 0.6, 0.3) read as Guard-Crush and broke the promise.
            AssertThat(fallback.TelegraphTint).IsEqual(EnemyAbilityExecutor.BasicTelegraph);
            AssertThat(fallback.TelegraphTint.R).IsEqualApprox(1f, 0.001f);
            AssertThat(fallback.TelegraphTint.G >= 0.9f)
                .OverrideFailureMessage("Basic telegraphs are white/yellow: green must stay high.")
                .IsTrue();
            AssertThat(fallback.TelegraphTint.B >= 0.5f)
                .OverrideFailureMessage("Basic telegraphs are white/yellow, not orange.")
                .IsTrue();
        } finally {
            enemy.Free();
        }
    }

    // === V7.3 Chrono-Warden rework: reactive Phase Skip ===

    [TestCase]
    public void ClosingInsidePhaseSkipRangeFiresTheTeleportOffCycleAndMarksTheEliteCooldown() {
        StaticBody2D floor = CreateStandOffFloor();
        EnemyController warden = CreateEnemy("chrono_warden");
        PlayerController player = CreateTargetPlayer(new Vector2(100f, 0f));
        try {
            warden.GlobalPosition = Vector2.Zero;
            AssertThat(player.GlobalPosition.DistanceTo(warden.GlobalPosition)
                    <= EnemyController.ReactiveTeleportRangePixels).IsTrue();

            // Frame 1 aggros into Chase; frame 2's chase step fires the
            // reactive teleport immediately — no reaction delay, no cycle.
            for (int frame = 0; frame < 5; frame++) {
                warden._PhysicsProcess(Step);
                if (warden.AbilityPhase != EnemyAbilityPhase.Idle) break;
            }
            AssertThat(warden.CurrentState).IsEqual(EnemyState.Attacking);
            AssertThat(warden.AbilityPhase).IsEqual(EnemyAbilityPhase.Telegraph);
            AssertObject(warden.ActiveAbility).IsNotNull();
            AssertThat(warden.ActiveAbility.Archetype).IsEqual(EnemyAbilityArchetype.Teleport);
            AssertThat(warden.LastAttackWasElite).IsTrue();

            // The elite cooldown was marked: a second reactive fire is refused.
            AssertThat(warden.TryReactivePhaseSkip()).IsFalse();
        } finally {
            warden.Free();
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void TheSequentialEliteCycleNeverSelectsATeleportAbility() {
        EnemyController warden = CreateEnemy("chrono_warden");
        try {
            warden.Data.EliteAbilityCooldown = 0f;
            for (int pick = 0; pick < 8; pick++) {
                EnemyAbilityData attack = warden.SelectNextAttack();
                AssertObject(attack).IsNotNull();
                AssertThat(attack.Archetype != EnemyAbilityArchetype.Teleport)
                    .OverrideFailureMessage(
                        "Phase Skip is reactive-only (V7.3): the sequential cycle must never select it.")
                    .IsTrue();
            }

            // Beyond reactive range nothing fires off-cycle either.
            AssertThat(warden.TryReactivePhaseSkip()).IsFalse();
        } finally {
            warden.Free();
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

            // Approach until the band engages, then let the halt settle: per-step
            // displacement rides the engine's out-of-band MoveAndSlide process
            // delta, so a fixed frame count is not a fixed travel distance.
            int approachFrames = 0;
            while (!enemy.IsStandOffEngaged && approachFrames++ < 2000) enemy._PhysicsProcess(Step);
            for (int frame = 0; frame < 30; frame++) enemy._PhysicsProcess(Step);

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
            // Approach until the band engages and the halt settles: per-step
            // displacement rides the engine's out-of-band MoveAndSlide process
            // delta, so a fixed frame count is not a fixed travel distance.
            int approachFrames = 0;
            while (!enemy.IsStandOffEngaged && approachFrames++ < 2000) enemy._PhysicsProcess(Step);
            AssertThat(enemy.IsStandOffEngaged).IsTrue();
            for (int frame = 0; frame < 30; frame++) enemy._PhysicsProcess(Step);
            float heldX = enemy.GlobalPosition.X;

            // Shuffle the target while staying safely inside the 110% release
            // line (chrono_slasher range 90 px, release 99 px): the enemy must
            // not move. The shuffle is computed from the actual settled gap —
            // the halt position rides the engine's process delta, so a fixed
            // 25 px sat within noise of the release line and flaked.
            float settledGap = player.GlobalPosition.X - enemy.GlobalPosition.X;
            float shuffle = Mathf.Max(5f, enemy.AttackRangePixels * 1.1f - settledGap - 6f);
            player.GlobalPosition += new Vector2(shuffle, 0f);
            for (int frame = 0; frame < 120; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.IsStandOffEngaged).IsTrue();
            AssertThat(enemy.Velocity.X).IsEqualApprox(0f, 0.001f);
            AssertThat(enemy.GlobalPosition.X).IsEqualApprox(heldX, 0.01f);

            // Beyond the release line the approach resumes: the next stepped frame
            // already carries chase velocity, and the walk clearly leaves the hold.
            player.GlobalPosition += new Vector2(200f, 0f);
            enemy._PhysicsProcess(Step);
            AssertThat(enemy.IsStandOffEngaged).IsFalse();
            AssertThat(enemy.Velocity.X > 0f).IsTrue();
            int resumeFrames = 0;
            while (enemy.GlobalPosition.X <= heldX + 50f && resumeFrames++ < 600) {
                enemy._PhysicsProcess(Step);
            }
            AssertThat(enemy.GlobalPosition.X > heldX + 50f).IsTrue();
        } finally {
            enemy.Free();
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void FlyingChaserHoversAtItsProjectileRangeStandOffInsteadOfPressingIn() {
        // NOTE: never instantiate a second PoolManager here. Its _Ready hijacks
        // the static Instance from the autoload, and freeing it nulls Instance
        // for the remainder of the session — every later suite's pooled spawn
        // (dust pickups, projectiles, story mobs) then silently degrades. The
        // autoload is running and serves any projectile the drone might pool.
        PlayerController player = CreateTargetPlayer(new Vector2(360f, 0f));
        EnemyController drone = CreateEnemy("hologram_drone");
        try {
            drone.GlobalPosition = new Vector2(0f, -80f);
            drone.Data.AttackCooldown = 999f;
            Vector2 playerBefore = player.GlobalPosition;

            // Hover-approach until the band engages, then let the deceleration
            // settle (the velocity ramp is stepped-dt deterministic; per-step
            // displacement rides the out-of-band MoveAndSlide process delta).
            int approachFrames = 0;
            while (!drone.IsStandOffEngaged && approachFrames++ < 2000) drone._PhysicsProcess(Step);
            for (int frame = 0; frame < 30; frame++) drone._PhysicsProcess(Step);

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
        }
    }

    [TestCase]
    public void CommittedAttacksLockFacingWhileTheReactionDelayStillAims() {
        // Audit M-19: the sprite re-aimed every telegraph frame while the executor
        // resolved the hitbox on the facing captured at commit, so a roll-through
        // made the telegraph lie. Facing may track during the pre-commit reaction
        // delay only; from the telegraph on it is locked.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "FacingLockHost" };
        tree.Root.AddChild(host);
        EnemyController phantom = null;
        try {
            FTT.Characters.PlayerController player =
                FTT.Characters.CharacterFactory.CreateCharacter("einstein", 0);
            host.AddChild(player);
            player.GlobalPosition = new Vector2(80f, 0f);

            // Flying, so a floorless test tree adds no gravity drift.
            phantom = CreateEnemy("rift_phantom");
            phantom.GlobalPosition = Vector2.Zero;

            // Patrol -> Chase -> Attacking (reaction delay aims) -> committed telegraph.
            int guard = 0;
            while (phantom.AbilityPhase != EnemyAbilityPhase.Telegraph && guard++ < 240) {
                phantom._PhysicsProcess(Step);
            }
            AssertThat(phantom.AbilityPhase).IsEqual(EnemyAbilityPhase.Telegraph);
            AssertThat(phantom.IsFacingRight).IsTrue();

            // The player crosses over mid-telegraph; the sprite must not flip.
            player.GlobalPosition = new Vector2(-80f, phantom.GlobalPosition.Y);
            for (int frame = 0; frame < 5; frame++) {
                phantom._PhysicsProcess(Step);
                AssertThat(phantom.IsFacingRight).IsTrue();
            }

            // And the hit resolves on the committed side.
            var hitbox = phantom.GetNode<Hitbox>("Hitbox");
            guard = 0;
            while (phantom.AbilityPhase != EnemyAbilityPhase.Active && guard++ < 60) {
                phantom._PhysicsProcess(Step);
            }
            AssertThat(phantom.AbilityPhase).IsEqual(EnemyAbilityPhase.Active);
            AssertThat(phantom.IsFacingRight).IsTrue();
            AssertThat(hitbox.IsActive).IsTrue();
            AssertThat(hitbox.Position.X > 0f).IsTrue();
        } finally {
            phantom?.Free();
            host.Free();
        }
    }

    [TestCase]
    public void TheDesignTableStandGuardRowsAuthorStandGuard() {
        // design-godot.md:1157-1161: Tech-Enforcer, Steam Automaton v2, and the
        // Neural-Linked Knight are the concrete table's three StandGuard rows.
        foreach (string enemyID in new[] { "tech_enforcer", "steam_automaton", "neural_linked_knight" }) {
            EnemyData data = FTT.Core.AuthoredResources.Load<EnemyData>(
                $"res://resources/Enemies/{enemyID}.tres");
            AssertThat(data.Behavior).IsEqual(DefaultBehavior.StandGuard);
        }
    }

    [TestCase]
    public void StandGuardEnemiesHoldTheirPostInsteadOfPacing() {
        EnemyController guard = CreateEnemy("tech_enforcer");
        try {
            AssertThat(guard.Data.Behavior).IsEqual(DefaultBehavior.StandGuard);
            float postX = guard.GlobalPosition.X;
            for (int frame = 0; frame < 120; frame++) guard._PhysicsProcess(Step);
            AssertThat(guard.CurrentState).IsEqual(EnemyState.Patrol);
            AssertThat(guard.GlobalPosition.X).IsEqualApprox(postX, 1f);

            // Control on the duplicated data: the same body with the design's
            // Patrol behavior paces its authored waypoints immediately — the very
            // next stepped frame carries pacing velocity, and the walk clearly
            // leaves the post. (Displacement per manual step rides the engine's
            // out-of-band MoveAndSlide delta, so distance is walked to, not
            // assumed from a fixed frame count.)
            guard.Data.Behavior = DefaultBehavior.Ground;
            guard._PhysicsProcess(Step);
            AssertThat(Mathf.Abs(guard.Velocity.X) > 0f).IsTrue();
            int paceFrames = 0;
            while (Mathf.Abs(guard.GlobalPosition.X - postX) <= 50f && paceFrames++ < 600) {
                guard._PhysicsProcess(Step);
            }
            AssertThat(Mathf.Abs(guard.GlobalPosition.X - postX) > 50f).IsTrue();
        } finally {
            guard.Free();
        }
    }

    [TestCase]
    public void StandGuardChasesNormallyAndReturnsToItsPostOnDeAggro() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "StandGuardReturnHost" };
        tree.Root.AddChild(host);
        EnemyController knight = null;
        try {
            FTT.Characters.PlayerController player =
                FTT.Characters.CharacterFactory.CreateCharacter("einstein", 0);
            host.AddChild(player);
            player.GlobalPosition = new Vector2(400f, 0f);

            knight = CreateEnemy("neural_linked_knight");
            knight.GlobalPosition = Vector2.Zero;

            // Aggro and chase work exactly like a patroller's. (Distance is walked
            // to under a step-count cap rather than assumed from a fixed frame
            // count: an out-of-band MoveAndSlide uses the engine's process delta.)
            for (int frame = 0; frame < 30; frame++) knight._PhysicsProcess(Step);
            AssertThat(knight.CurrentState).IsEqual(EnemyState.Chase);
            AssertThat(knight.Velocity.X > 0f).IsTrue();
            int chaseFrames = 0;
            while (knight.GlobalPosition.X <= 50f && chaseFrames++ < 600) {
                knight._PhysicsProcess(Step);
            }
            AssertThat(knight.CurrentState).IsEqual(EnemyState.Chase);
            AssertThat(knight.GlobalPosition.X > 50f).IsTrue();

            // De-aggro: the guard walks back to its post, not to a waypoint pace.
            player.GlobalPosition = new Vector2(5000f, 0f);
            int guardFrames = 0;
            while (knight.CurrentState != EnemyState.Patrol && guardFrames++ < 300) {
                knight._PhysicsProcess(Step);
            }
            AssertThat(knight.CurrentState).IsEqual(EnemyState.Patrol);
            AssertThat(Mathf.Abs(knight.GlobalPosition.X) < 16f).IsTrue();

            // Back on post it stands guard again rather than resuming a pace.
            float postX = knight.GlobalPosition.X;
            for (int frame = 0; frame < 60; frame++) knight._PhysicsProcess(Step);
            AssertThat(knight.GlobalPosition.X).IsEqualApprox(postX, 1f);
        } finally {
            knight?.Free();
            host.Free();
        }
    }

    [TestCase]
    public void DeAggroReturnsToTheNearestWaypointAndResumesPatrol() {
        // design-godot.md:2298: "the mob returns to its nearest waypoint and
        // resumes patrol" — previously it steered to the spawn midpoint.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "NearestWaypointHost" };
        tree.Root.AddChild(host);
        EnemyController slasher = null;
        try {
            FTT.Characters.PlayerController player =
                FTT.Characters.CharacterFactory.CreateCharacter("einstein", 0);
            host.AddChild(player);
            player.GlobalPosition = new Vector2(300f, 0f);

            slasher = CreateEnemy("chrono_slasher");
            slasher.GlobalPosition = Vector2.Zero;

            // Chase right, past the authored +150 waypoint's near side. Velocity is
            // deterministic; per-step displacement is not (an out-of-band
            // MoveAndSlide uses the engine's process delta, not the stepped 1/60),
            // so walk until the chase has clearly crossed the midpoint.
            for (int frame = 0; frame < 25; frame++) slasher._PhysicsProcess(Step);
            AssertThat(slasher.CurrentState).IsEqual(EnemyState.Chase);
            AssertThat(slasher.Velocity.X > 0f).IsTrue();
            int chaseFrames = 0;
            while (slasher.GlobalPosition.X <= 100f && chaseFrames++ < 600) {
                slasher._PhysicsProcess(Step);
            }
            AssertThat(slasher.CurrentState).IsEqual(EnemyState.Chase);
            AssertThat(slasher.GlobalPosition.X > 100f).IsTrue();

            // De-aggro: the nearest waypoint is Right (+150), not the spawn (0).
            player.GlobalPosition = new Vector2(5000f, 0f);
            int guardFrames = 0;
            while (slasher.CurrentState != EnemyState.Patrol && guardFrames++ < 300) {
                slasher._PhysicsProcess(Step);
            }
            AssertThat(slasher.CurrentState).IsEqual(EnemyState.Patrol);
            AssertThat(slasher.GlobalPosition.X > 130f).IsTrue();

            // And patrol resumes toward the opposite waypoint, no fresh idle hold:
            // the very next stepped frame is already pacing left (an idle hold
            // would leave Velocity.X at zero for its full second).
            float resumeX = slasher.GlobalPosition.X;
            slasher._PhysicsProcess(Step);
            AssertThat(slasher.Velocity.X < 0f).IsTrue();
            int resumeFrames = 0;
            while (slasher.GlobalPosition.X >= resumeX - 50f && resumeFrames++ < 600) {
                slasher._PhysicsProcess(Step);
            }
            AssertThat(slasher.GlobalPosition.X < resumeX - 50f).IsTrue();
        } finally {
            slasher?.Free();
            host.Free();
        }
    }

    [TestCase]
    public void DeadEnemiesAreNotResurrectedByARewindRestore() {
        // Audit M-6: Die() zeroed collision/hurtbox/pushbox and paid the kill's
        // dust; a rewind restore must skip the corpse rather than stand up an
        // invulnerable ghost.
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            enemy.RewindPolicy = StoryRewindPolicy.ResetToInitialState;
            enemy.TakeDamage(enemy.ScaledMaxHP);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Dead);

            enemy.ApplyStoryRewind();

            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Dead);
            AssertThat(enemy.CurrentHP).IsEqual(0);
            AssertThat(enemy.CollisionLayer).IsEqual(0u);
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            AssertThat(hurtbox.Monitoring).IsFalse();
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void ChargeDashTravelObeysTheStatusMoveMultiplier() {
        // Audit Low: the controller wrote the raw executor DashVelocity, so a
        // Rooted enemy still crossed the room at full dash speed.
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            var dash = new EnemyAbilityData {
                AbilityID = "test.status_dash",
                Archetype = EnemyAbilityArchetype.ChargeDash,
                RangeClass = EnemyAbilityRangeClass.Melee,
                TelegraphFrames = 1,
                ActiveFrames = 30,
                RecoveryFrames = 4,
                Damage = 5f,
                DashSpeed = 600f,
                DashDurationFrames = 30,
                HitboxSize = new Vector2(40f, 40f)
            };
            enemy.BeginAttack(dash);
            enemy._PhysicsProcess(Step); // telegraph -> active
            AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Active);
            AssertThat(enemy.StatusScaledDashVelocityX).IsEqualApprox(600f, 0.001f);

            enemy.ApplyStatusEffect(StatusType.Root, 2f, 1f);
            AssertThat(enemy.StatusScaledDashVelocityX).IsEqualApprox(0f, 0.0001f);

            enemy.ApplyStatusEffect(StatusType.TimeDilation, 2f, 1f);
            AssertThat(enemy.StatusScaledDashVelocityX).IsEqualApprox(300f, 0.001f);
        } finally {
            enemy.Free();
        }
    }

    // === V7.4 Enemy Stagger Discipline (EnemyStaggerRules; PvE only) ===

    [TestCase]
    public void NaturallyExpiredStunArmsGetupArmorThatDeniesStunAndKnockbackButNeverDamage() {
        EnemyController enemy = CreateEnemy("chrono_slasher"); // StunResistance 0
        try {
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            enemy.ApplyStun(0.1f);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            for (int frame = 0; frame < 10; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Patrol);
            AssertThat(enemy.IsGetupArmored)
                .OverrideFailureMessage("A naturally-expiring stun must arm the getup armor window.")
                .IsTrue();

            // The re-engage jab: full damage, but no stun and no knockback.
            int hpBefore = enemy.CurrentHP;
            Vector2 velocityBefore = enemy.Velocity;
            hurtbox.TakeHit(new HitPayload {
                AttackID = "einstein.basic",
                HitboxID = "combo_1",
                AttackClass = AttackClass.Basic,
                Damage = 3f,
                Knockback = new Vector2(2f, -1.5f),
                HitstunDuration = BasicComboRules.HitstunFrames[0] / 60f,
                HitOrigin = Vector2.Zero,
                AttackerFacingRight = true
            });
            AssertThat(enemy.CurrentHP)
                .OverrideFailureMessage("Armor never reduces damage.")
                .IsEqual(hpBefore - 3);
            AssertThat(enemy.CurrentState)
                .OverrideFailureMessage("An armored hit must not re-stun the enemy.")
                .IsNotEqual(EnemyState.Stunned);
            AssertThat(enemy.Velocity)
                .OverrideFailureMessage("An armored hit must not apply knockback.")
                .IsEqual(velocityBefore);

            // Armor expires; the next hit stuns normally again.
            for (int frame = 0; frame < 50; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.IsGetupArmored).IsFalse();
            hurtbox.TakeHit(new HitPayload {
                AttackID = "einstein.basic",
                HitboxID = "combo_1",
                AttackClass = AttackClass.Basic,
                Damage = 3f,
                Knockback = new Vector2(2f, -1.5f),
                HitstunDuration = BasicComboRules.HitstunFrames[0] / 60f,
                HitOrigin = Vector2.Zero,
                AttackerFacingRight = true
            });
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void HitsLandingDuringStunStillRefreshTheStunNormally() {
        // Armor arms only on NATURAL expiry: a hit landing mid-stun refreshes
        // the stun exactly as before — the full string and its finisher work.
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            enemy.ApplyStun(0.2f); // 12 frames
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            for (int frame = 0; frame < 3; frame++) enemy._PhysicsProcess(Step);
            enemy.ApplyStun(0.5f); // refresh mid-stun
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            // 20 frames past the refresh: the original 12-frame stun would have
            // expired long ago; the refreshed 30-frame stun is still holding.
            for (int frame = 0; frame < 20; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.CurrentState)
                .OverrideFailureMessage("A mid-stun hit must refresh the stun, not be denied.")
                .IsEqual(EnemyState.Stunned);
            for (int frame = 0; frame < 15; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Patrol);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void SustainedStunBeyondTheEliteBudgetTriggersArmoredRecoveryWithACommittedAnswer() {
        EnemyController elite = CreateEnemy("tech_enforcer");
        try {
            elite.Data.StunResistance = 0f;
            elite.Data.EliteAbilityCooldown = 0f;
            var hurtbox = elite.GetNode<Hurtbox>("Hurtbox");

            elite.ApplyStun(0.9f);
            elite.ApplyStun(0.9f); // budget 1.8 — still under the 2.0 s budget
            AssertThat(elite.IsArmoredRecovery).IsFalse();
            AssertThat(elite.CurrentState).IsEqual(EnemyState.Stunned);

            elite.ApplyStun(0.9f); // budget 2.7 > 2.0 — Armored Recovery
            AssertThat(elite.IsArmoredRecovery).IsTrue();
            AssertThat(elite.StaggerBudgetSeconds)
                .OverrideFailureMessage("The budget resets on trigger.")
                .IsEqualApprox(0f, 0.0001f);
            // The armored answer commits immediately through the normal
            // BeginAttack path: the elite's signature ability (cooldown is up),
            // with its ordinary telegraph.
            AssertThat(elite.CurrentState).IsEqual(EnemyState.Attacking);
            AssertThat(elite.LastAttackWasElite).IsTrue();
            AssertThat(elite.ActiveAbility.Archetype).IsEqual(EnemyAbilityArchetype.ShieldBubble);
            AssertThat(elite.AbilityPhase == EnemyAbilityPhase.Telegraph
                    || elite.AbilityPhase == EnemyAbilityPhase.Active)
                .OverrideFailureMessage("The committed answer must be executing.")
                .IsTrue();

            // During Armored Recovery: damage lands, flinch and shove do not.
            int hpBefore = elite.CurrentHP;
            hurtbox.TakeHit(new HitPayload {
                AttackID = "einstein.basic",
                HitboxID = "combo_1",
                AttackClass = AttackClass.Basic,
                Damage = 5f,
                Knockback = new Vector2(3f, -1.5f),
                HitstunDuration = BasicComboRules.HitstunFrames[0] / 60f,
                HitOrigin = new Vector2(-40f, 0f),
                AttackerFacingRight = true
            });
            AssertThat(elite.CurrentHP < hpBefore)
                .OverrideFailureMessage("Armored Recovery never prevents damage.")
                .IsTrue();
            AssertThat(elite.CurrentState)
                .OverrideFailureMessage("Armored Recovery is flinch-proof.")
                .IsEqual(EnemyState.Attacking);
        } finally {
            elite.Free();
        }
    }

    [TestCase]
    public void SpacedOutStunsDecayTheStaggerBudgetAndNeverTriggerArmoredRecovery() {
        EnemyController elite = CreateEnemy("tech_enforcer");
        try {
            elite.Data.StunResistance = 0f;
            // 4 x 0.6 s = 2.4 s of raw stun — past the 2.0 s budget if it never
            // decayed — but each stun is followed by ~1.5 s of unstunned time,
            // which the 1 s/s decay fully clears.
            for (int burst = 0; burst < 4; burst++) {
                elite.ApplyStun(0.6f);
                AssertThat(elite.IsArmoredRecovery)
                    .OverrideFailureMessage("A spaced-out sequence must never trip the budget.")
                    .IsFalse();
                for (int frame = 0; frame < 130; frame++) elite._PhysicsProcess(Step);
            }
            AssertThat(elite.IsArmoredRecovery).IsFalse();
            AssertThat(elite.StaggerBudgetSeconds < 1f)
                .OverrideFailureMessage("The budget must decay while unstunned.")
                .IsTrue();
        } finally {
            elite.Free();
        }
    }

    [TestCase]
    public void ASecondSpecialStunInsideTheWindowAppliesHalfStunAndFullDamageWhileBasicsAreUntouched() {
        EnemyController enemy = CreateEnemy("chrono_slasher"); // StunResistance 0
        try {
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            HitPayload SpecialHit() => new() {
                AttackID = "einstein.special_one",
                HitboxID = "special_test",
                AttackClass = AttackClass.Special,
                Damage = 4f,
                HitstunDuration = 0.5f,
                HitOrigin = Vector2.Zero,
                AttackerFacingRight = true
            };

            int hp0 = enemy.CurrentHP;
            hurtbox.TakeHit(SpecialHit());
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            AssertThat(hp0 - enemy.CurrentHP).IsEqual(4);
            for (int frame = 0; frame < 6; frame++) enemy._PhysicsProcess(Step);

            // Second special inside the 4 s window: full damage, half stun.
            int hp1 = enemy.CurrentHP;
            hurtbox.TakeHit(SpecialHit());
            AssertThat(hp1 - enemy.CurrentHP)
                .OverrideFailureMessage("Diminished stun never diminishes damage.")
                .IsEqual(4);
            // Half of 0.5 s is 15 frames (+3 hitstop): expired well before the
            // 24-frame mark where a full 30-frame stun would still hold.
            for (int frame = 0; frame < 24; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.CurrentState)
                .OverrideFailureMessage("The second special's stun must be halved.")
                .IsEqual(EnemyState.Patrol);

            // Basics are untouched by the window: wait out the getup armor,
            // then a Basic-class (non-string) hit keeps its full stun.
            for (int frame = 0; frame < 45; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.IsGetupArmored).IsFalse();
            hurtbox.TakeHit(new HitPayload {
                AttackID = "leonardo.turret",
                HitboxID = "turret_shot",
                AttackClass = AttackClass.Basic,
                Damage = 2f,
                HitstunDuration = 0.5f,
                HitOrigin = Vector2.Zero,
                AttackerFacingRight = true
            });
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            for (int frame = 0; frame < 24; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.CurrentState)
                .OverrideFailureMessage("A Basic-class stun inside the special window must stay full length.")
                .IsEqual(EnemyState.Stunned);
            for (int frame = 0; frame < 15; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Patrol);

            // Window expiry restores the full special stun: run well past the
            // 4 s window (the basic stun above never refreshed it), then the
            // next special holds the full 30 frames again.
            for (int frame = 0; frame < 260; frame++) enemy._PhysicsProcess(Step);
            hurtbox.TakeHit(SpecialHit());
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            for (int frame = 0; frame < 24; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.CurrentState)
                .OverrideFailureMessage("An expired window must restore the full special stun.")
                .IsEqual(EnemyState.Stunned);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void StunEndingWithTheTargetInAttackRangePrefersAnImmediateAttackOverChase() {
        StaticBody2D floor = CreateStandOffFloor();
        EnemyController enemy = CreateEnemy("chrono_slasher");
        PlayerController player = CreateTargetPlayer(new Vector2(60f, 0f));
        try {
            enemy.GlobalPosition = Vector2.Zero;
            // Aggro: chrono_slasher's 1.5-unit reach is 90 px, so 60 px is in
            // range. One frame acquires the target.
            enemy._PhysicsProcess(Step);
            AssertThat(enemy.CurrentState == EnemyState.Chase
                    || enemy.CurrentState == EnemyState.Attacking).IsTrue();

            enemy.ApplyStun(0.15f); // 9 frames
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            for (int frame = 0; frame < 12; frame++) enemy._PhysicsProcess(Step);

            // V7.4 pressure-exit: the getup answers instead of strolling.
            AssertThat(enemy.CurrentState)
                .OverrideFailureMessage("Stun ending in range must produce an attack, not Chase.")
                .IsEqual(EnemyState.Attacking);
            AssertThat(enemy.IsGetupArmored).IsTrue();
        } finally {
            player.Free();
            floor.Free();
            enemy.Free();
        }
    }

    /// <summary>
    /// Instantiates the authored tier scene with a duplicated data resource so a
    /// test may retune fields without leaking into the shared canonical .tres.
    /// </summary>
    // === V7.6 F07 Static Charge stagger accounting (Package 11 A1) ===
    // One hit is ONE stun event: hitstun and a Static Charge riding the same hit
    // run concurrently and the GREATER post-resistance duration is counted once.

    [TestCase]
    public void TeslaFinisherChargesTheEliteBudgetOnceNotTwice() {
        EnemyController elite = CreateEnemy("tech_enforcer");
        try {
            elite.Data.StunResistance = 0f;
            var hurtbox = elite.GetNode<Hurtbox>("Hurtbox");
            // The authored Tesla finisher: 0.4 s hitstun (HitstunFrames[2] = 24)
            // AND a 0.4 s Static Charge. The design forbids the pair becoming
            // 0.8 s of stagger budget.
            BasicStringProfile tesla = BasicComboRules.StringProfileFor("tesla");
            hurtbox.TakeHit(new HitPayload {
                AttackerIndex = 0,
                AttackID = "tesla.basic",
                HitboxID = "combo_3",
                AttackClass = AttackClass.Basic,
                Damage = 5f,
                HitstunDuration = BasicComboRules.HitstunFrames[2] / 60f,
                HitOrigin = Vector2.Zero,
                AttackerFacingRight = true,
                AppliedStatus = StatusType.StaticCharge,
                StatusDuration = tesla.FinisherStatusFrames / 60f,
                StatusIntensity = 1f,
                ComboMark = ComboMarkType.Conductive,
                ComboMarkFrames = tesla.FinisherMarkFrames
            });
            AssertThat(elite.StaggerBudgetSeconds)
                .OverrideFailureMessage(
                    "The finisher must not turn 0.4 s of budget into 0.8 s; budget was "
                    + elite.StaggerBudgetSeconds)
                .IsEqualApprox(0.4f, 0.0001f);
            AssertThat(elite.ControlStatusType)
                .OverrideFailureMessage("The Static Charge still occupies the control slot.")
                .IsEqual(StatusType.StaticCharge);
            AssertThat(elite.HasConductiveMark)
                .OverrideFailureMessage("A mark rides the same hit but is not a status.")
                .IsTrue();
        } finally {
            elite.Free();
        }
    }

    [TestCase]
    public void ConcurrentStaticChargeTakesTheMaximumStunNeverOverwritingALongerOne() {
        EnemyController enemy = CreateEnemy("chrono_slasher"); // StunResistance 0
        try {
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            // 0.5 s of hitstun with a 0.1 s Static Charge riding along. The old
            // second ApplyStun overwrote _stunTimer with 0.1 s; max wins now.
            hurtbox.TakeHit(new HitPayload {
                AttackerIndex = 0,
                AttackID = "tesla.coil",
                HitboxID = "coil_arc",
                AttackClass = AttackClass.Basic,
                Damage = 2f,
                HitstunDuration = 0.5f,
                HitOrigin = Vector2.Zero,
                AttackerFacingRight = true,
                AppliedStatus = StatusType.StaticCharge,
                StatusDuration = 0.1f,
                StatusIntensity = 1f
            });
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            // 12 frames in: a 0.1 s (6-frame) stun would have expired; the
            // 0.5 s (30-frame) stun still holds.
            for (int frame = 0; frame < 12; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.CurrentState)
                .OverrideFailureMessage("A shorter Static Charge must never shorten a longer hitstun.")
                .IsEqual(EnemyState.Stunned);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void ASpecialSourcedStaticChargeDiminishesAndRefreshesTheSpecialWindow() {
        EnemyController enemy = CreateEnemy("chrono_slasher"); // StunResistance 0
        try {
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            HitPayload ChargingSpecial() => new() {
                AttackerIndex = 0,
                AttackID = "tesla.special_two",
                HitboxID = "pulse",
                AttackClass = AttackClass.Special,
                Damage = 3f,
                HitstunDuration = 0f,
                HitOrigin = Vector2.Zero,
                AttackerFacingRight = true,
                AppliedStatus = StatusType.StaticCharge,
                StatusDuration = 0.5f,
                StatusIntensity = 1f
            };

            // The charge alone is the stun, from a Special-class source, so it
            // arms the diminish window. The old path classified it non-special
            // and it never diminished at all.
            hurtbox.TakeHit(ChargingSpecial());
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            for (int frame = 0; frame < 6; frame++) enemy._PhysicsProcess(Step);

            hurtbox.TakeHit(ChargingSpecial());
            // Half of 0.5 s is 15 frames (+3 hitstop); a full 30-frame stun would
            // still hold at frame 24.
            for (int frame = 0; frame < 24; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.CurrentState)
                .OverrideFailureMessage("A special-sourced Static Charge must diminish inside the window.")
                .IsEqual(EnemyState.Patrol);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void ArmoredRecoveryEndsTheStaticChargeLockAndRejectsItsReapplication() {
        EnemyController elite = CreateEnemy("tech_enforcer");
        try {
            elite.Data.StunResistance = 0f;
            elite.Data.EliteAbilityCooldown = 0f;
            var hurtbox = elite.GetNode<Hurtbox>("Hurtbox");

            elite.ApplyStatusEffect(StatusType.StaticCharge, 1.0f, 1f);
            AssertThat(elite.ControlStatusType).IsEqual(StatusType.StaticCharge);

            // Trip the elite budget: protection ends the effective stun, so the
            // charge cannot persist as a separate input lock behind it.
            elite.ApplyStun(0.9f);
            elite.ApplyStun(0.9f);
            elite.ApplyStun(0.9f);
            AssertThat(elite.IsArmoredRecovery).IsTrue();
            AssertThat(elite.ControlStatusType)
                .OverrideFailureMessage("Armored Recovery must end the Static Charge lock.")
                .IsEqual(StatusType.None);

            // Reapplication is rejected for the duration of the protection.
            hurtbox.TakeHit(new HitPayload {
                AttackerIndex = 0,
                AttackID = "tesla.coil",
                HitboxID = "coil_fence",
                AttackClass = AttackClass.Basic,
                Damage = 4f,
                HitstunDuration = 0f,
                HitOrigin = Vector2.Zero,
                AttackerFacingRight = true,
                AppliedStatus = StatusType.StaticCharge,
                StatusDuration = 1.0f,
                StatusIntensity = 1f,
                ComboMark = ComboMarkType.Conductive,
                ComboMarkFrames = BasicComboRules.ConductiveMarkFenceFrames
            });
            AssertThat(elite.ControlStatusType)
                .OverrideFailureMessage("Armor must reject Static Charge reapplication.")
                .IsEqual(StatusType.None);
            // A MARK is not a stun: armor never refuses it.
            AssertThat(elite.HasConductiveMark)
                .OverrideFailureMessage("Armor must not refuse the caster-owned mark.")
                .IsTrue();
        } finally {
            elite.Free();
        }
    }

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
