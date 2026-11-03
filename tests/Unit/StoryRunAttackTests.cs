using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Diagnostics;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The running attack, Story side (GR-RUN, user ruling 9b, 2026-11-03). Under ruling
/// 15 Story leads: a fresh BasicAttack press from the <c>Running</c> state at the
/// shared fraction of the maximum run speed or more is one committed lunge outside
/// the three-hit string, posed on <c>basic_attack_2</c>. The sim half is
/// <c>FighterRunAttackTests</c>.
///
/// <para>Contacts are driven by emitting the melee hitbox's AreaEntered on the
/// target's hurtbox (the FeelPassStoryTests idiom).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryRunAttackTests {
    private const double Step = 1.0 / 60.0;
    // Away from every other suite's geometry.
    private static readonly Vector2 Origin = new(97000f, 0f);

    [TestCase]
    public void APressAtARunIsOneStrikeOnItsProfilesFramesPosedOnHitTwosSheet() {
        foreach ((string hero, int startup) in new[] { ("tesla", 9), ("joan", 7), ("einstein", 10), ("lincoln", 11) }) {
            Run(hero, (player, _) => {
                var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
                RunUp(player, 10);
                SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None, moveX: 1f);
                AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
                AssertThat(hitbox.HitboxID)
                    .OverrideFailureMessage($"{hero}: a press at a run must be the running attack.")
                    .IsEqual(BasicComboRules.RunAttackHitboxID);
                AssertThat(player.ComboCounter).IsEqual(0);
                AssertThat(player.NextChainStepForPress).IsEqual(0);
                AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(BasicComboRules.AbilityLockoutFrames);
                AssertThat(player.ActiveAnimationName).IsEqual("basic_attack_2");
                AssertThat(player.ActiveAnimationFrame).IsEqual(AttackPoseRules.WindUpPose);

                int active = BasicComboRules.RunAttackActiveFrames;
                int total = startup + active + BasicComboRules.RunAttackRecoveryFrames;
                // Mash through the strike. The press tick runs no attack tick, so
                // call N is elapsed frame N-1.
                int openings = 0;
                bool wasActive = false;
                for (int call = 1; call <= total; call++) {
                    bool press = call % 2 == 1 && call < total;
                    SendEdge(player, GameplayButtons.BasicAttack, press ? GameplayButtons.None : GameplayButtons.BasicAttack, moveX: 1f);
                    if (hitbox.IsActive && !wasActive) openings++;
                    wasActive = hitbox.IsActive;
                    if (call == total) break;
                    int elapsed = call - 1;
                    bool expectActive = elapsed >= startup && elapsed < startup + active;
                    AssertThat(player.CurrentState)
                        .OverrideFailureMessage($"{hero}: the strike ended early at elapsed frame {elapsed}.")
                        .IsEqual(CharacterState.Attacking);
                    AssertThat(hitbox.IsActive)
                        .OverrideFailureMessage($"{hero}: elapsed frame {elapsed}: the box must be {(expectActive ? "live" : "closed")}.")
                        .IsEqual(expectActive);
                    AssertThat(player.ComboCounter).IsEqual(0);
                    AssertThat(player.ActiveAnimationName).IsEqual("basic_attack_2");
                }
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage($"{hero}: it exits straight out, no buffered hit and no chain hold.")
                    .IsNotEqual(CharacterState.Attacking);
                AssertThat(openings).IsEqual(1);
            });
        }
    }

    [TestCase]
    public void TheFirstFramesOfAPushASlideAndAStandingPressAreTheStringsOpener() {
        // The ramp reaches full speed in 6 ticks: 3 is one half, 4 is two thirds.
        foreach ((int ticks, string expected) in new[] {
            (0, "combo_1"), (1, "combo_1"), (3, "combo_1"), (4, BasicComboRules.RunAttackHitboxID), (6, BasicComboRules.RunAttackHitboxID)
        }) {
            Run("tesla", (player, _) => {
                RunUp(player, ticks);
                SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None, moveX: ticks == 0 ? 0f : 1f);
                AssertThat(player.GetNode<Hitbox>("MeleeHitbox").HitboxID)
                    .OverrideFailureMessage($"A press after {ticks} ticks of a push.")
                    .IsEqual(expected);
            });
        }

        // A released stick sliding at speed has left the Running state.
        Run("tesla", (player, _) => {
            RunUp(player, 10);
            SendEdge(player, GameplayButtons.None, GameplayButtons.None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None);
            AssertThat(player.GetNode<Hitbox>("MeleeHitbox").HitboxID).IsEqual("combo_1");
        });
    }

    [TestCase]
    public void TheDirectionsAndTheGrabChordWinOverTheRun() {
        Run("tesla", (player, _) => {
            RunUp(player, 10);
            SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None, moveX: 1f, vertical: -1f);
            AssertThat(player.GetNode<Hitbox>("MeleeHitbox").HitboxID).IsEqual(BasicComboRules.UpAttackHitboxID);
        });
        Run("tesla", (player, _) => {
            RunUp(player, 10);
            SendEdge(player, GameplayButtons.BasicAttack | GameplayButtons.Down, GameplayButtons.None, moveX: 1f);
            AssertThat(player.GetNode<Hitbox>("MeleeHitbox").HitboxID)
                .OverrideFailureMessage("A grounded Down-attack stays the string's opener.")
                .IsEqual("combo_1");
        });
        Run("tesla", (player, _) => {
            RunUp(player, 10);
            SendEdge(player, GameplayButtons.BasicAttack | GameplayButtons.Block, GameplayButtons.None, moveX: 1f);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The grab chord must start the grab, at a run or not.")
                .IsEqual(CharacterState.Grabbing);
        });
    }

    [TestCase]
    public void ItIsOneAndAFifthBasicsAtOneAndAHalfKnockbackWithNoLaunchAndNoLeftoverRider() {
        // Lincoln's hit 1 deals 0.7x, so the flat 1.2x is told apart from its shape.
        foreach (string hero in new[] { "tesla", "lincoln" }) {
            Run(hero, (player, _) => {
                var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
                hitbox.AppliedStatus = StatusType.StaticCharge;
                hitbox.StatusDuration = 0.4f;
                hitbox.ComboMark = ComboMarkType.Conductive;
                hitbox.ComboMarkFrames = BasicComboRules.ConductiveMarkBaselineFrames;
                RunUp(player, 10);
                SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None, moveX: 1f);
                AssertThat(hitbox.HitboxID).IsEqual(BasicComboRules.RunAttackHitboxID);
                AssertThat(hitbox.AttackClass).IsEqual(AttackClass.Basic);
                AssertThat(hitbox.Delivery).IsEqual(HitDelivery.DirectHit);
                AssertThat(hitbox.Origin).IsEqual(HitOrigin.Basic);
                AssertThat(hitbox.Damage)
                    .IsEqualApprox(player.Data.BasicAttackDamage * BasicComboRules.RunAttackDamageMultiplier, 0.001f);
                AssertThat(hitbox.Launches).OverrideFailureMessage($"{hero}: it never launches.").IsFalse();
                AssertThat(hitbox.HitstunDuration).IsEqualApprox(BasicComboRules.RunAttackHitstunFrames / 60f, 0.0001f);
                AssertThat(hitbox.KnockbackForce.X)
                    .IsEqualApprox(player.Data.BasicAttackKnockback * BasicComboRules.RunAttackKnockbackMultiplier, 0.001f);
                AssertThat(hitbox.KnockbackForce.Y).IsEqualApprox(-1f, 0.001f);
                AssertThat(hitbox.AppliedStatus).IsEqual(StatusType.None);
                AssertThat(hitbox.StatusDuration).IsEqual(0f);
                AssertThat(hitbox.ComboMark).IsEqual(ComboMarkType.None);
                AssertThat(hitbox.ComboMarkFrames).IsEqual(0);
            });
        }
    }

    [TestCase]
    public void TheActiveFramesTravelTheLungeWhateverTheStickAndTheRecoveryBrakes() {
        float lunge = (float)(BasicComboRules.RunAttackLungeUnits * KitMotionRules.StoryPixelsPerUnit);
        foreach ((string name, float startupStick, float committedStick) in new[] {
            ("the stick held", 1f, 1f),
            ("the stick released", 0f, 0f),
            ("the stick pushed back after the startup", 1f, -1f)
        }) {
            Run("tesla", (player, _) => {
                var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
                int startup = BasicComboRules.StringProfileFor("tesla").RunAttackStartupFrames;
                RunUp(player, 10);
                SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None, moveX: 1f);
                float travelled = 0f;
                int activeTicks = 0;
                int recovery = BasicComboRules.RunAttackRecoveryFrames;
                int active = BasicComboRules.RunAttackActiveFrames;
                for (int elapsed = 0; elapsed < startup + active + recovery - 1; elapsed++) {
                    float before = player.GlobalPosition.X;
                    SendEdge(player, GameplayButtons.None, GameplayButtons.None, moveX: elapsed < startup ? startupStick : committedStick);
                    if (hitbox.IsActive) {
                        activeTicks++;
                        travelled += player.GlobalPosition.X - before;
                    }
                    if (elapsed >= startup) {
                        AssertThat(player.IsFacingRight)
                            .OverrideFailureMessage($"{name}: the committed swing must keep its facing.")
                            .IsTrue();
                    }
                }
                AssertThat(activeTicks).IsEqual(active);
                AssertThat(Mathf.Abs(travelled - lunge) < 0.5f)
                    .OverrideFailureMessage($"{name}: the active frames travelled {travelled:F3} px, not {lunge}.")
                    .IsTrue();
                AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
                AssertThat(player.Velocity.X)
                    .OverrideFailureMessage($"{name}: the recovery must brake to a stop.")
                    .IsEqual(0f);
            });
        }
    }

    [TestCase]
    public void ALandedRunAttackConfirmsGainsTheSingleStrikesMomentumAndHoldsAResistantMob() {
        Run("einstein", (player, host) => {
            player.MomentumRegenMultiplier = 0f;
            player.RestoreMomentumPips(0f);
            var box = player.GetNode<Hitbox>("MeleeHitbox");
            RunUp(player, 10);
            SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None, moveX: 1f);
            AssertThat(box.HitboxID).IsEqual(BasicComboRules.RunAttackHitboxID);
            for (int tick = 0; tick < 30 && !box.IsActive; tick++) {
                SendEdge(player, GameplayButtons.None, GameplayButtons.None, moveX: 1f);
            }
            AssertThat(box.IsActive).OverrideFailureMessage("Harness: the box never opened.").IsTrue();

            EnemyController mob = Mob(host, player.GlobalPosition + new Vector2(50f, 0f));
            mob.Data.StunResistance = 0.65f;
            int hpBefore = mob.CurrentHP;
            box.EmitSignal(Area2D.SignalName.AreaEntered, mob.GetNode<Hurtbox>("Hurtbox"));

            // S4 with the 8.1 floor, on the hit's rounded pre-intake damage.
            int baseDamage = Mathf.RoundToInt(box.Damage);
            int expected = Mathf.RoundToInt(baseDamage * StoryEnemyCombatRules.StandardBasicPacingScale(baseDamage));
            AssertThat(hpBefore - mob.CurrentHP)
                .OverrideFailureMessage("S4 must read the running attack as a basic.")
                .IsEqual(expected);
            AssertThat(player.SwingLanded)
                .OverrideFailureMessage("A landed running attack opens the D1 hit-confirm like any basic swing.")
                .IsTrue();
            AssertFloat(player.MomentumPips).IsEqualApprox(
                BasicComboRules.MomentumDirectionalGainUnits / (float)BasicComboRules.MomentumUnitsPerPip, 0.0001f);
            AssertThat(player.IsInHitstop).IsTrue();
            AssertThat(mob.IsInFinisherFlight)
                .OverrideFailureMessage("It is no finisher: no carry.")
                .IsFalse();
            AssertThat(mob.CurrentState).IsEqual(EnemyState.Stunned);

            // S5: a 0.65 resistance leaves 10.5 of the 30 frames, plus the freeze
            // (at most 11); hit 1's floor holds it at least 34.
            int floor = StoryEnemyCombatRules.BasicStringStunFloorFrames(BasicComboRules.RunAttackHitboxID);
            int stunned = 0;
            for (; stunned < 90 && mob.CurrentState == EnemyState.Stunned; stunned++) mob._PhysicsProcess(Step);
            AssertThat(stunned >= floor)
                .OverrideFailureMessage($"The mob was stunned {stunned} ticks, under the {floor}-frame floor.")
                .IsTrue();

            AssertThat(PlaytestCombatMath.HitVariant(box.HitboxID)).IsEqual("run_attack");
        });
    }

    [TestCase]
    public void AStepForwardMidStringNeverLosesTheStringAndOnlyAFreshPressRuns() {
        Run("tesla", (player, _) => {
            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            // Hit 1 from a standstill, then the stick held: by its chain hold the
            // hero is at a full run, and a press still continues the string.
            SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None);
            AssertThat(hitbox.HitboxID).IsEqual("combo_1");
            int hitOne = BasicComboRules.StringProfileFor("tesla").GroundStartupFrames[0]
                + BasicComboRules.GroundActiveFrames[0] + BasicComboRules.GroundRecoveryFrames[0];
            for (int call = 0; call < hitOne + 2; call++) {
                SendEdge(player, GameplayButtons.None, GameplayButtons.None, moveX: 1f);
            }
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(hitbox.IsActive).IsFalse();
            AssertThat(player.Velocity.X > 100f).OverrideFailureMessage("Harness: the hero must be moving.").IsTrue();

            SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None, moveX: 1f);
            AssertThat(hitbox.HitboxID)
                .OverrideFailureMessage("Held movement never breaks the chain.")
                .IsEqual("combo_2");
            AssertThat(player.ComboCounter).IsEqual(1);
        });

        // Once the chain hold has run out, the same press is a fresh one.
        Run("tesla", (player, _) => {
            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None);
            for (int call = 0; call < 120 && player.CurrentState == CharacterState.Attacking; call++) {
                SendEdge(player, GameplayButtons.None, GameplayButtons.None, moveX: 1f);
            }
            AssertThat(player.CurrentState).IsNotEqual(CharacterState.Attacking);
            RunUp(player, 2);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Running);
            SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None, moveX: 1f);
            AssertThat(hitbox.HitboxID).IsEqual(BasicComboRules.RunAttackHitboxID);
        });
    }

    [TestCase]
    public void TheBoxIsHitOnesWithItsFarEdgeOutAndMirrorsWithTheFacingAndTheReachMinor() {
        foreach ((float stick, float reach) in new[] { (1f, 1f), (-1f, 1f), (1f, 1.1f) }) {
            Run("lincoln", (player, _) => {
                player.StoryAttackRangeMultiplier = reach;
                var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
                RunUp(player, 10, stick);
                SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None, moveX: stick);
                AssertThat(hitbox.HitboxID).IsEqual(BasicComboRules.RunAttackHitboxID);
                for (int tick = 0; tick < 30 && !hitbox.IsActive; tick++) {
                    SendEdge(player, GameplayButtons.None, GameplayButtons.None, moveX: stick);
                }
                AssertThat(hitbox.IsActive).IsTrue();

                var shape = hitbox.GetNode<CollisionShape2D>("CollisionShape2D");
                float half = ((RectangleShape2D)shape.Shape).Size.X * 0.5f;
                float facing = stick > 0f ? 1f : -1f;
                float far = facing * shape.Position.X + half;
                float near = facing * shape.Position.X - half;

                (float hitNear, float hitFar) = player.StoryStringSpanPixels(BasicComboRules.RunAttackStringStep);
                float bonus = (float)(BasicComboRules.RunAttackReachBonusUnits * KitMotionRules.StoryPixelsPerUnit) * reach;
                AssertThat(Mathf.Abs(far - (hitFar + bonus)) < 0.01f)
                    .OverrideFailureMessage($"stick {stick}, minor {reach}: far edge {far:F3}, hit 1's {hitFar:F3} + {bonus:F3}.")
                    .IsTrue();
                AssertThat(Mathf.Abs(near - hitNear) < 0.01f)
                    .OverrideFailureMessage($"stick {stick}, minor {reach}: near edge {near:F3}, hit 1's {hitNear:F3}.")
                    .IsTrue();
            });
        }
    }

    // === helpers ==============================================================

    private static void RunUp(PlayerController player, int ticks, float stick = 1f) {
        for (int tick = 0; tick < ticks; tick++) {
            SendEdge(player, GameplayButtons.None, GameplayButtons.None, moveX: stick);
        }
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

    /// <summary>A grounded, idle hero at <see cref="Origin"/> on a wide floor, with a pool manager for hit effects.</summary>
    private static void Run(string hero, Action<PlayerController, Node2D> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "RunAttackHost" };
        tree.Root.AddChild(host);
        var pools = new PoolManager { Name = "RunAttackPools" };
        tree.Root.AddChild(pools);
        var floor = new StaticBody2D {
            Name = "RunAttackFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = Origin
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        host.AddChild(floor);
        PlayerController player = CharacterFactory.CreateCharacter(hero, 0, applyStoryProgression: false);
        player.Position = Origin + new Vector2(0f, -8f);
        host.AddChild(player);
        try {
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                player.Velocity = new Vector2(player.Velocity.X, 400f);
                SendEdge(player, GameplayButtons.None, GameplayButtons.None);
            }
            for (int frame = 0; frame < 60 && player.CurrentState != CharacterState.Idle; frame++) {
                SendEdge(player, GameplayButtons.None, GameplayButtons.None);
            }
            AssertThat(player.CurrentState)
                .OverrideFailureMessage($"setup: {player.CurrentState} onFloor={player.IsOnFloor()} pos={player.GlobalPosition}")
                .IsEqual(CharacterState.Idle);
            body(player, host);
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            pools.ClearAllPools();
            tree.Root.RemoveChild(pools);
            pools.Free();
            host.Free();
        }
    }

    /// <summary>One tick holding <paramref name="held"/> against last tick's <paramref name="previousHeld"/>.</summary>
    private static void SendEdge(
        PlayerController player, GameplayButtons held, GameplayButtons previousHeld,
        float moveX = 0f, float vertical = 0f) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, moveX, vertical, held, previousHeld));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Step);
    }
}
