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
/// The single air strike, Story side (GU-A, user request 2026-10-12: "For
/// aerial attacks, it should just be a standard attack and no longer chain the
/// combo. It should be a single attack."). An air press with neither Up nor
/// Down held is one strike outside the three-hit string: it never buffers,
/// never holds, never continues and exits straight to <c>Airborne</c>. It is
/// string hit 1 in the air — the profile's air startup, hit 1's knockback and
/// 30-frame hitstun, a flat 1.0x basic, no launch, no rider — and a landed one
/// confirms and refunds 20 frames of per-hit Momentum like hit 1. No chain hit
/// ever starts in the air: a grounded string that walks off a ledge ends at the
/// recovery that would have continued it, and a fresh press in its airborne
/// chain hold is an air press. The box is pinned by
/// <c>StoryAerialStringReachTests</c>; the sim half by
/// <c>FighterAirAttackTests</c>.
///
/// <para>Contacts are driven by emitting the melee hitbox's AreaEntered on the
/// target's hurtbox (the FeelPassStoryTests idiom).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryAirAttackTests {
    private const double Step = 1.0 / 60.0;
    // Away from every other suite's geometry.
    private static readonly Vector2 Origin = new(83000f, 0f);
    private static readonly Vector2 LedgeEdge = new(87000f, 0f);
    private static readonly Vector2 AirborneSpawn = new(91000f, -4000f);

    [TestCase]
    public void AnAirPressIsOneStrikeThatNeverBuffersHoldsOrContinuesAndLeavesToAirborne() {
        PlayerController player = CreateAirborne("tesla");
        try {
            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            int startup = BasicComboRules.StringProfileFor("tesla").AirAttackStartupFrames;
            int active = BasicComboRules.AirAttackActiveFrames;
            int total = startup + active + BasicComboRules.AirAttackRecoveryFrames;

            SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(hitbox.HitboxID).IsEqual(BasicComboRules.AirAttackHitboxID);
            AssertThat(player.ComboCounter).IsEqual(0);
            AssertThat(player.NextChainStepForPress).IsEqual(0);
            AssertThat(player.AbilityLockoutFramesRemaining)
                .OverrideFailureMessage("The air strike arms the swing lockout like every swing start.")
                .IsEqual(BasicComboRules.AbilityLockoutFrames);
            AssertPose(player, AttackPoseRules.WindUpPose, "the press");

            // Mash through the whole strike: a fresh press on every other tick,
            // BasicAttack held in between. The last tick only holds. The press
            // tick runs no attack tick, so call N is elapsed frame N-1.
            int openings = 0;
            bool wasActive = false;
            for (int call = 1; call <= total; call++) {
                bool press = call % 2 == 1 && call < total;
                SendEdge(player, GameplayButtons.BasicAttack, press ? GameplayButtons.None : GameplayButtons.BasicAttack);
                if (hitbox.IsActive && !wasActive) openings++;
                wasActive = hitbox.IsActive;
                if (call == total) break;
                int elapsed = call - 1;
                bool expectActive = elapsed >= startup && elapsed < startup + active;
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage($"The strike ended early at elapsed frame {elapsed}.")
                    .IsEqual(CharacterState.Attacking);
                AssertThat(hitbox.IsActive)
                    .OverrideFailureMessage($"Elapsed frame {elapsed}: the box must be {(expectActive ? "live" : "closed")}.")
                    .IsEqual(expectActive);
                AssertThat(player.ComboCounter)
                    .OverrideFailureMessage("A press during the air strike must never advance the string.")
                    .IsEqual(0);
                AssertThat(player.NextChainStepForPress).IsEqual(0);
            }
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The air strike exits straight to Airborne: no buffered hit, no chain hold.")
                .IsEqual(CharacterState.Airborne);
            AssertThat(openings)
                .OverrideFailureMessage($"Mashing through the air strike opened {openings} boxes.")
                .IsEqual(1);

            // Holding the button never repeats it.
            for (int call = 0; call < 12; call++) {
                SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.BasicAttack);
                AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
            }
            AssertThat(hitbox.IsActive).IsFalse();
        } finally {
            Release(player);
        }
    }

    [TestCase]
    public void TheAirStrikeIsOneBasicAtHitOnesKnockbackWithNoLaunchAndNoLeftoverRider() {
        // Lincoln's string hit 1 deals 0.7x, so his air strike's flat 1.0x is
        // told apart from hit 1's damage shape.
        foreach (string hero in new[] { "tesla", "lincoln" }) {
            PlayerController player = CreateAirborne(hero);
            try {
                var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
                LeaveFinisherRider(hitbox);
                SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None);
                AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
                AssertThat(hitbox.HitboxID).IsEqual(BasicComboRules.AirAttackHitboxID);
                AssertThat(hitbox.AttackClass).IsEqual(AttackClass.Basic);
                AssertThat(hitbox.Delivery).IsEqual(HitDelivery.DirectHit);
                AssertThat(hitbox.Origin).IsEqual(HitOrigin.Basic);
                AssertThat(hitbox.Damage)
                    .OverrideFailureMessage($"{hero}: the air strike deals a flat 1.0x basic.")
                    .IsEqualApprox(player.Data.BasicAttackDamage * BasicComboRules.AirAttackDamageMultiplier, 0.001f);
                AssertThat(hitbox.Launches)
                    .OverrideFailureMessage($"{hero}: the air strike never launches.")
                    .IsFalse();
                AssertThat(hitbox.HitstunDuration)
                    .IsEqualApprox(BasicComboRules.AirAttackHitstunFrames / 60f, 0.0001f);
                float baseKnockback = player.Data.BasicAttackKnockback;
                AssertThat(hitbox.KnockbackForce.X)
                    .OverrideFailureMessage($"{hero}: the air strike shoves at string hit 1's multiplier.")
                    .IsEqualApprox(baseKnockback * BasicComboRules.KnockbackMultipliers[BasicComboRules.AirAttackStringStep], 0.001f);
                AssertThat(hitbox.KnockbackForce.Y).IsEqualApprox(-1f, 0.001f);
                AssertNoRider(hitbox, hero);
                AssertPose(player, AttackPoseRules.WindUpPose, $"{hero}'s press");
                AssertThat(player.ActiveAnimationName).IsEqual("basic_attack_1");
            } finally {
                Release(player);
            }
        }

        // The Up-Attack clears a leftover rider too: Tesla's Conductive mark
        // used to ride an Up-Attack or Down-Air swung after his finisher.
        PlayerController tesla = CreateAirborne("tesla");
        try {
            var hitbox = tesla.GetNode<Hitbox>("MeleeHitbox");
            LeaveFinisherRider(hitbox);
            SendEdge(tesla, GameplayButtons.BasicAttack, GameplayButtons.None, vertical: -1f);
            AssertThat(hitbox.HitboxID).IsEqual(BasicComboRules.UpAttackHitboxID);
            AssertNoRider(hitbox, "tesla up-attack");
        } finally {
            Release(tesla);
        }
    }

    [TestCase]
    public void ALandedAirStrikeConfirmsAndRefundsTwentyLikeStringHitOne() {
        Run((player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            player.SpecialOneCooldownTimer = 5f;
            player.SpecialTwoCooldownTimer = 5f;
            LiftIntoTheAir(player, 240f);
            Hitbox box = OpenBox(player);
            AssertThat(box.HitboxID).IsEqual(BasicComboRules.AirAttackHitboxID);
            AssertThat(player.SwingLanded).IsFalse();
            float one = player.SpecialOneCooldownTimer;
            float two = player.SpecialTwoCooldownTimer;
            int hpBefore = mob.CurrentHP;

            Contact(box, mob);

            AssertThat(mob.CurrentHP < hpBefore)
                .OverrideFailureMessage("Harness: the air strike must land on the mob.")
                .IsTrue();
            AssertThat(player.SwingLanded)
                .OverrideFailureMessage("A landed air strike opens the D1 hit-confirm like any basic swing.")
                .IsTrue();
            AssertThat(player.SpecialOneCooldownTimer).IsEqual(one - 20f / 60f);
            AssertThat(player.SpecialTwoCooldownTimer).IsEqual(two - 20f / 60f);
            AssertThat(player.MomentumRefundedFramesSlotOne).IsEqual(BasicComboRules.MomentumStringHitRefundFrames);
            AssertThat(player.MomentumRefundedFramesSlotTwo).IsEqual(BasicComboRules.MomentumStringHitRefundFrames);
            AssertThat(player.IsInHitstop)
                .OverrideFailureMessage("A landed air strike freezes the attacker like any landed basic.")
                .IsTrue();
        });
    }

    [TestCase]
    public void AGroundedStringThatWalksOffALedgeEndsInsteadOfChainingInTheAir() {
        int startup = BasicComboRules.StringProfileFor("tesla").GroundStartupFrames[0];
        int hitOne = startup + BasicComboRules.GroundActiveFrames[0] + BasicComboRules.GroundRecoveryFrames[0];

        // Control: on an unbroken floor, holding Attack carries hit 1 into hit 2.
        RunWalk(LedgeEdge.X - 1200f, (player, hitbox) => {
            SwingWalking(player, hitOne, call => GameplayButtons.BasicAttack);
            AssertThat(player.IsOnFloor()).OverrideFailureMessage("Harness: the control left the floor.").IsTrue();
            AssertThat(player.ComboCounter)
                .OverrideFailureMessage("Control: a held grounded string must continue into hit 2.")
                .IsEqual(1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
        });

        // Held through hit 1 while walking off the edge: the held continuation
        // would start in the air, so the string ends instead.
        RunWalk(LedgeEdge.X - 25f, (player, hitbox) => {
            int openings = SwingWalking(player, hitOne, call => GameplayButtons.BasicAttack);
            AssertEndedInTheAir(player, hitbox, openings, "held");
        });

        // A press buffered during hit 1's first active frame (call startup + 1
        // is elapsed frame startup), then released: the same end.
        RunWalk(LedgeEdge.X - 25f, (player, hitbox) => {
            int openings = SwingWalking(player, hitOne,
                call => call == startup + 1 ? GameplayButtons.BasicAttack : GameplayButtons.None,
                pressEveryHeldTick: true);
            AssertEndedInTheAir(player, hitbox, openings, "buffered");
        });

        // No input: hit 1 enters its chain hold in the air, and a fresh press
        // there is an air press — the air strike, never hit 2.
        RunWalk(LedgeEdge.X - 25f, (player, hitbox) => {
            SwingWalking(player, hitOne, call => GameplayButtons.None);
            AssertThat(player.IsOnFloor()).OverrideFailureMessage("Harness: the hero never left the ledge.").IsFalse();
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Hit 1 with no input enters its chain hold.")
                .IsEqual(CharacterState.Attacking);
            AssertThat(hitbox.IsActive).IsFalse();
            SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None, moveX: 1f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(hitbox.HitboxID)
                .OverrideFailureMessage("A press in an airborne chain hold is an air press.")
                .IsEqual(BasicComboRules.AirAttackHitboxID);
            AssertThat(player.ComboCounter).IsEqual(0);
        });
    }

    // === helpers ==============================================================

    private static void LeaveFinisherRider(Hitbox hitbox) {
        hitbox.AppliedStatus = StatusType.StaticCharge;
        hitbox.StatusDuration = 0.4f;
        hitbox.StatusIntensity = 0.5f;
        hitbox.ComboMark = ComboMarkType.Conductive;
        hitbox.ComboMarkFrames = BasicComboRules.ConductiveMarkBaselineFrames;
    }

    private static void AssertNoRider(Hitbox hitbox, string what) {
        AssertThat(hitbox.AppliedStatus)
            .OverrideFailureMessage($"{what}: a leftover finisher status leaked in.")
            .IsEqual(StatusType.None);
        AssertThat(hitbox.StatusDuration).IsEqual(0f);
        AssertThat(hitbox.ComboMark)
            .OverrideFailureMessage($"{what}: a leftover combo mark leaked in.")
            .IsEqual(ComboMarkType.None);
        AssertThat(hitbox.ComboMarkFrames).IsEqual(0);
    }

    private static void AssertPose(PlayerController player, int pose, string where) {
        AssertThat(player.ActiveAnimationName)
            .OverrideFailureMessage($"Wrong sheet at {where}: the air strike is posed on hit 1's.")
            .IsEqual("basic_attack_1");
        AssertThat(player.ActiveAnimationFrame)
            .OverrideFailureMessage($"Wrong pose at {where}.")
            .IsEqual(pose);
    }

    private static void AssertEndedInTheAir(PlayerController player, Hitbox hitbox, int openings, string how) {
        AssertThat(player.IsOnFloor())
            .OverrideFailureMessage($"{how}: harness — the hero never left the ledge.")
            .IsFalse();
        AssertThat(player.CurrentState)
            .OverrideFailureMessage($"{how}: a continuation that would start airborne must end the string.")
            .IsEqual(CharacterState.Airborne);
        AssertThat(player.ComboCounter).IsEqual(0);
        AssertThat(player.GroundComboCounter).IsEqual(0);
        AssertThat(hitbox.IsActive).IsFalse();
        // Nothing starts afterwards while the button is only held.
        bool wasActive = false;
        for (int call = 0; call < 30; call++) {
            SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.BasicAttack, moveX: 1f);
            if (hitbox.IsActive && !wasActive) openings++;
            wasActive = hitbox.IsActive;
        }
        AssertThat(openings)
            .OverrideFailureMessage($"{how}: a second box opened after the string ended in the air.")
            .IsEqual(1);
        AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
    }

    /// <summary>
    /// Presses BasicAttack walking right, then runs <paramref name="hitOneFrames"/>
    /// ticks walking right with <paramref name="buttons"/> per call (call 1 is
    /// elapsed frame 0), so the last call is hit 1's last recovery frame. A
    /// button returned on a call reads as held from the previous call, unless
    /// <paramref name="pressEveryHeldTick"/> makes every returned button a
    /// fresh press. Returns the number of boxes that opened.
    /// </summary>
    private static int SwingWalking(
        PlayerController player, int hitOneFrames, Func<int, GameplayButtons> buttons, bool pressEveryHeldTick = false) {
        var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
        SendEdge(player, GameplayButtons.BasicAttack, GameplayButtons.None, moveX: 1f);
        AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
        AssertThat(hitbox.HitboxID).IsEqual("combo_1");
        int openings = 0;
        bool wasActive = false;
        GameplayButtons previous = GameplayButtons.BasicAttack;
        for (int call = 1; call <= hitOneFrames; call++) {
            GameplayButtons held = buttons(call);
            SendEdge(player, held, pressEveryHeldTick ? GameplayButtons.None : previous, moveX: 1f);
            previous = held;
            if (hitbox.IsActive && !wasActive) openings++;
            wasActive = hitbox.IsActive;
        }
        return openings;
    }

    /// <summary>
    /// A Tesla standing on a floor slab whose east edge is <see cref="LedgeEdge"/>
    /// (nothing beyond it), placed at <paramref name="startX"/>.
    /// </summary>
    private static void RunWalk(float startX, Action<PlayerController, Hitbox> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var floor = new StaticBody2D {
            Name = "AirAttackLedge",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = LedgeEdge
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(2000f, 40f) },
            Position = new Vector2(-1000f, 20f)
        });
        tree.Root.AddChild(floor);
        PlayerController player = CharacterFactory.CreateCharacter("tesla", 0, applyStoryProgression: false);
        tree.Root.AddChild(player);
        try {
            player.GlobalPosition = new Vector2(startX, LedgeEdge.Y - 8f);
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                player.Velocity = new Vector2(0f, 400f);
                SendEdge(player, GameplayButtons.None, GameplayButtons.None);
            }
            for (int frame = 0; frame < 30 && player.CurrentState != CharacterState.Idle; frame++) {
                SendEdge(player, GameplayButtons.None, GameplayButtons.None);
            }
            AssertThat(player.CurrentState)
                .OverrideFailureMessage($"Harness: {player.CurrentState} on floor {player.IsOnFloor()} at {player.GlobalPosition}.")
                .IsEqual(CharacterState.Idle);
            body(player, player.GetNode<Hitbox>("MeleeHitbox"));
        } finally {
            Release(player);
            floor.Free();
        }
    }

    /// <summary>Starts a swing and ticks until its box opens.</summary>
    private static Hitbox OpenBox(PlayerController player) {
        var box = player.GetNode<Hitbox>("MeleeHitbox");
        Tick(player, GameplayButtons.BasicAttack);
        AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
        for (int tick = 0; tick < 30 && !box.IsActive; tick++) Tick(player, GameplayButtons.None);
        AssertThat(box.IsActive).OverrideFailureMessage("Harness: the swing's box never opened.").IsTrue();
        return box;
    }

    private static void Contact(Hitbox box, Node target) {
        Hurtbox hurtbox = target as Hurtbox ?? target.GetNode<Hurtbox>("Hurtbox");
        box.EmitSignal(Area2D.SignalName.AreaEntered, hurtbox);
    }

    /// <summary>
    /// Lifts the grounded hero <paramref name="height"/> px and ticks until the
    /// controller itself reads it as airborne.
    /// </summary>
    private static void LiftIntoTheAir(PlayerController player, float height) {
        player.GlobalPosition += new Vector2(0f, -height);
        player.Velocity = Vector2.Zero;
        player.TransitionTo(CharacterState.Airborne);
        for (int tick = 0; tick < 20 && (player.CurrentState != CharacterState.Airborne || player.IsOnFloor()); tick++) {
            Tick(player, GameplayButtons.None);
        }
        AssertThat(player.IsOnFloor()).IsFalse();
        AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
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

    /// <summary>An Einstein on a floor at <see cref="Origin"/>, with a pool manager for hit effects.</summary>
    private static void Run(Action<PlayerController, Node2D> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "AirAttackHost" };
        tree.Root.AddChild(host);
        var pools = new PoolManager { Name = "AirAttackPools" };
        tree.Root.AddChild(pools);
        var floor = new StaticBody2D {
            Name = "AirAttackFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = Origin
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        host.AddChild(floor);
        PlayerController player = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        player.Position = Origin + new Vector2(0f, -8f);
        host.AddChild(player);
        try {
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                player.Velocity = new Vector2(player.Velocity.X, 400f);
                Tick(player, GameplayButtons.None);
            }
            for (int frame = 0; frame < 60 && player.CurrentState != CharacterState.Idle; frame++) {
                Tick(player, GameplayButtons.None);
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

    private static PlayerController CreateAirborne(string characterID) {
        PlayerController player = CharacterFactory.CreateCharacter(characterID, 0, applyStoryProgression: false);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        player.GlobalPosition = AirborneSpawn;
        player.TransitionTo(CharacterState.Airborne);
        player.Velocity = Vector2.Down;
        return player;
    }

    private static void Release(PlayerController player) {
        InputManager.Instance?.ClearInputSource(player.PlayerIndex);
        player.Free();
    }

    /// <summary>One controller tick; every held button reads as freshly pressed.</summary>
    private static void Tick(PlayerController player, GameplayButtons buttons, float vertical = 0f) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, vertical, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Step);
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
