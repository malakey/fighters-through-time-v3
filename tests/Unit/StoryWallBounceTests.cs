using System.Threading.Tasks;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// D9 (user direction 2026-10-11, ledger <c>USER-STORY-WALL-BOUNCE</c>): a
/// player-launched mob — and, by the user's addendum, a thrown standard — that
/// hits an Environment wall faster than 300 px/s bounces back at 40 % of that
/// speed and takes 10 % of the launching hit's pre-defense intake once, with
/// defenses applied once at the impact. Never a slow launch, a plain shove, a
/// floor seam, a phaser or a frozen mob. Story only.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryWallBounceTests {
    private const float Step = 1f / 60f;
    private const float OriginX = 61000f;

    [TestCase]
    public async Task AFinisherIntoAWallBouncesTheMobBackAtFortyPercentAndHurtsItOnce() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor();
            EnemyController mob = CreateMob("chrono_slasher", new Vector2(OriginX, 0f));
            float wallFace = OriginX + BodyHalfWidth(mob) + 50f;
            StaticBody2D wall = CreateWall(wallFace);
            try {
                SettleOnFloor(mob);
                int hp0 = mob.CurrentHP;
                // Damage 20 on a Standard: S4 intake 25, so the impact is 3.
                mob.GetNode<Hurtbox>("Hurtbox").TakeHit(Finisher(damage: 20f, forceX: 15.75f));
                AssertThat(hp0 - mob.CurrentHP).IsEqual(25);
                AssertThat(mob.IsWallBounceArmed).IsTrue();

                int bounceFrame = StepUntilBounce(mob, 60, wallFace);
                AssertThat(bounceFrame >= 0)
                    .OverrideFailureMessage("The finisher never bounced off the wall.")
                    .IsTrue();
                float pre = mob.LastWallBouncePreMoveVelocityX;
                AssertThat(pre > StoryEnemyCombatRules.WallBounceMinSpeedPixelsPerSecond).IsTrue();
                AssertThat(Mathf.Abs(mob.Velocity.X - (-0.4f * pre)) <= 1f)
                    .OverrideFailureMessage($"Bounced at {mob.Velocity.X:F1} px/s from {pre:F1}; expected -40 %.")
                    .IsTrue();
                AssertThat(mob.LastWallImpactDealt).IsEqual(3);
                AssertThat(hp0 - mob.CurrentHP).IsEqual(25 + 3);
                AssertThat(mob.CurrentState)
                    .OverrideFailureMessage("The impact adds no stun and ends none.")
                    .IsEqual(EnemyState.Stunned);
                AssertThat(mob.IsWallBounceArmed).IsFalse();

                // Once per launch: nothing more through the end of the stun.
                for (int frame = 0; frame < 90; frame++) {
                    mob._PhysicsProcess(Step);
                    AssertThat(mob.GlobalPosition.X + BodyHalfWidth(mob) <= wallFace + 0.5f).IsTrue();
                }
                AssertThat(hp0 - mob.CurrentHP).IsEqual(25 + 3);
            } finally {
                mob.Free();
                wall.Free();
                floor.Free();
            }
        });
    }

    [TestCase]
    public async Task AnEliteLaunchedIntoAWallBouncesToo() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor();
            EnemyController knight = CreateMob("neural_linked_knight", new Vector2(OriginX, 0f));
            float wallFace = OriginX + BodyHalfWidth(knight) + 15f;
            StaticBody2D wall = CreateWall(wallFace);
            try {
                SettleOnFloor(knight);
                int hp0 = knight.CurrentHP;
                // A Lincoln-shape finisher: 5.0 base x 4.5. No S4 on an elite:
                // intake 13, impact 1.
                knight.GetNode<Hurtbox>("Hurtbox").TakeHit(Finisher(damage: 13f, forceX: 22.5f));
                AssertThat(hp0 - knight.CurrentHP).IsEqual(13);
                AssertThat(StepUntilBounce(knight, 60, wallFace) >= 0)
                    .OverrideFailureMessage("The elite never bounced off the wall.")
                    .IsTrue();
                AssertThat(knight.LastWallImpactDealt).IsEqual(1);
                AssertThat(hp0 - knight.CurrentHP).IsEqual(13 + 1);
                AssertThat(knight.Velocity.X < 0f).IsTrue();
            } finally {
                knight.Free();
                wall.Free();
                floor.Free();
            }
        });
    }

    [TestCase]
    public async Task ASlowLaunchAPlainShoveAFloorSeamAndAPhaserNeverWallBounce() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            // (a) A non-launching hit, however fast.
            AssertNoBounce("shove", "chrono_slasher", wallGap: 30f, seam: false, overlapWall: false,
                hit: StringHit("combo_1", damage: 4f, knockback: new Vector2(20f, -1f), launches: false),
                expectedIntake: 5, expectArmed: false);
            // (b) A launch that reaches the wall below 300 px/s.
            AssertNoBounce("slow launch", "chrono_slasher", wallGap: 30f, seam: false, overlapWall: false,
                hit: StringHit("combo_3", damage: 4f, knockback: new Vector2(4f, -2f), launches: true),
                expectedIntake: 5, expectArmed: true);
            // (c) A fast, low launch across a seam between two floor pieces, no wall.
            AssertNoBounce("floor seam", "chrono_slasher", wallGap: -1f, seam: true, overlapWall: false,
                hit: StringHit("combo_3", damage: 4f, knockback: new Vector2(20f, -0.5f), launches: true),
                expectedIntake: 5, expectArmed: true);
            // (d) A Rift Phantom launched while its body overlaps a wall's edge:
            // its depenetration must not read as an impact, and it never arms.
            AssertNoBounce("phaser", "rift_phantom", wallGap: 0f, seam: false, overlapWall: true,
                hit: StringHit("combo_3", damage: 4f, knockback: new Vector2(15f, -2f), launches: true),
                expectedIntake: 5, expectArmed: false);
        });
    }

    [TestCase]
    public async Task AFrozenOrHitstoppedMobDefersItsBounceAndALethalImpactKillsCleanly() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor();
            // (a) Time Freeze mid-carry: no motion, no impact; the bounce lands after the thaw.
            EnemyController frozen = CreateMob("chrono_slasher", new Vector2(OriginX, 0f));
            float wallFace = OriginX + BodyHalfWidth(frozen) + 120f;
            StaticBody2D wall = CreateWall(wallFace);
            try {
                SettleOnFloor(frozen);
                frozen.GetNode<Hurtbox>("Hurtbox").TakeHit(Finisher(damage: 20f, forceX: 15.75f));
                int guard = 0;
                while (frozen.HitstopFramesRemaining > 0 && guard++ < 30) frozen._PhysicsProcess(Step);
                for (int frame = 0; frame < 3; frame++) frozen._PhysicsProcess(Step);
                AssertThat(frozen.LastWallImpactDealt).IsEqual(0);
                Vector2 heldAt = frozen.GlobalPosition;
                frozen.SetTimeFrozen(true);
                for (int frame = 0; frame < 10; frame++) frozen._PhysicsProcess(Step);
                AssertThat(frozen.GlobalPosition).IsEqual(heldAt);
                AssertThat(frozen.LastWallImpactDealt).IsEqual(0);
                AssertThat(frozen.IsWallBounceArmed).IsTrue();
                frozen.SetTimeFrozen(false);
                AssertThat(StepUntilBounce(frozen, 60, wallFace) >= 0)
                    .OverrideFailureMessage("The bounce must land after the thaw.")
                    .IsTrue();
                AssertThat(frozen.LastWallImpactDealt).IsEqual(3);
            } finally {
                frozen.Free();
                wall.Free();
            }

            // (b) A lethal impact dies normally: Dead, HP 0, one kill.
            EnemyController doomed = CreateMob("chrono_slasher", new Vector2(OriginX, 0f));
            wallFace = OriginX + BodyHalfWidth(doomed) + 50f;
            wall = CreateWall(wallFace);
            int kills = 0;
            void CountKill(EnemyKilledPayload payload) => kills++;
            EventBus.Instance.OnEnemyKilled += CountKill;
            try {
                SettleOnFloor(doomed);
                doomed.CurrentHP = 27; // 27 - 25 leaves 2; the 3 HP impact kills.
                doomed.GetNode<Hurtbox>("Hurtbox").TakeHit(Finisher(damage: 20f, forceX: 15.75f));
                AssertThat(doomed.CurrentHP).IsEqual(2);
                AssertThat(StepUntilBounce(doomed, 60, wallFace) >= 0).IsTrue();
                AssertThat(doomed.CurrentState).IsEqual(EnemyState.Dead);
                AssertThat(doomed.CurrentHP).IsEqual(0);
                AssertThat(kills).IsEqual(1);
                for (int frame = 0; frame < 10; frame++) doomed._PhysicsProcess(Step);
                AssertThat(kills).IsEqual(1);
            } finally {
                EventBus.Instance.OnEnemyKilled -= CountKill;
                if (GodotObject.IsInstanceValid(doomed)) doomed.Free();
                wall.Free();
            }

            // (c) The rewind freeze disarms the launch: pushed into the wall
            // afterwards, the mob takes no impact.
            EnemyController rewound = CreateMob("chrono_slasher", new Vector2(OriginX, 0f));
            wallFace = OriginX + BodyHalfWidth(rewound) + 50f;
            wall = CreateWall(wallFace);
            try {
                SettleOnFloor(rewound);
                int hp0 = rewound.CurrentHP;
                rewound.GetNode<Hurtbox>("Hurtbox").TakeHit(Finisher(damage: 20f, forceX: 15.75f));
                rewound.SetStoryRewindFrozen(true);
                rewound.SetStoryRewindFrozen(false);
                AssertThat(rewound.IsWallBounceArmed).IsFalse();
                for (int frame = 0; frame < 20; frame++) {
                    rewound.Velocity = new Vector2(600f, rewound.Velocity.Y);
                    rewound._PhysicsProcess(Step);
                }
                AssertThat(rewound.LastWallBouncePreMoveVelocityX).IsEqual(0f);
                AssertThat(rewound.LastWallImpactDealt).IsEqual(0);
                AssertThat(hp0 - rewound.CurrentHP).IsEqual(25);
            } finally {
                rewound.Free();
                wall.Free();
                floor.Free();
            }
        });
    }

    [TestCase]
    public async Task TheImpactTakesDefensesOnceAndALaterKnockbackDisarmsIt() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor();
            // (a) RadiantBurn (taken x1.5) applies to the impact once: 10 % of the
            // pre-defense intake (25 -> 3), then x1.5 -> 4 (banker's rounding of
            // 4.5), never 10 % of the already-defended 38 (which would be 6).
            EnemyController guard = CreateMob("cyber_guard", new Vector2(OriginX, 0f));
            float wallFace = OriginX + BodyHalfWidth(guard) + 50f;
            StaticBody2D wall = CreateWall(wallFace);
            try {
                SettleOnFloor(guard);
                guard.ApplyStatusEffect(StatusType.RadiantBurn, 5f, 2f);
                AssertThat(guard.StatusDamageTakenMultiplier).IsEqualApprox(1.5f, 0.0001f);
                int hp0 = guard.CurrentHP;
                guard.GetNode<Hurtbox>("Hurtbox").TakeHit(Finisher(damage: 20f, forceX: 15.75f));
                AssertThat(hp0 - guard.CurrentHP).IsEqual(38);
                AssertThat(StepUntilBounce(guard, 60, wallFace) >= 0).IsTrue();
                AssertThat(guard.LastWallImpactDealt).IsEqual(4);
                AssertThat(guard.CurrentState).IsNotEqual(EnemyState.Dead);
            } finally {
                guard.Free();
                wall.Free();
            }

            // (b) A later knockback write before the wall disarms the bounce.
            EnemyController shoved = CreateMob("chrono_slasher", new Vector2(OriginX, 0f));
            wallFace = OriginX + BodyHalfWidth(shoved) + 50f;
            wall = CreateWall(wallFace);
            try {
                SettleOnFloor(shoved);
                int hp0 = shoved.CurrentHP;
                shoved.GetNode<Hurtbox>("Hurtbox").TakeHit(Finisher(damage: 20f, forceX: 15.75f));
                AssertThat(shoved.IsWallBounceArmed).IsTrue();
                shoved.ApplyKnockback(new Vector2(15.75f, -9f), attackerFacingRight: true);
                AssertThat(shoved.IsWallBounceArmed).IsFalse();
                for (int frame = 0; frame < 60; frame++) {
                    shoved._PhysicsProcess(Step);
                    AssertThat(shoved.Velocity.X >= -1f).IsTrue();
                }
                AssertThat(shoved.LastWallBouncePreMoveVelocityX).IsEqual(0f);
                AssertThat(hp0 - shoved.CurrentHP).IsEqual(25);
            } finally {
                shoved.Free();
                wall.Free();
                floor.Free();
            }
        });
    }

    [TestCase]
    public async Task AThrownStandardBouncesOffAWallLikeALaunchedOneButASlowThrowDoesNot() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor();
            // User addendum (2026-10-11): thrown standard mobs wall-bounce too,
            // the impact a tenth of the throw's own damage.
            EnemyController thrown = CreateMob("chrono_slasher", new Vector2(OriginX, 0f));
            float wallFace = OriginX + BodyHalfWidth(thrown) + 40f;
            StaticBody2D wall = CreateWall(wallFace);
            try {
                SettleOnFloor(thrown);
                int hp0 = thrown.CurrentHP;
                thrown.BeginHeld();
                thrown.LaunchThrown(new Vector2(600f, -150f), bowlingDamage: 1, throwDamage: 10);
                AssertThat(thrown.IsThrownFlight).IsTrue();
                AssertThat(thrown.IsWallBounceArmed).IsTrue();
                AssertThat(StepUntilBounce(thrown, 60, wallFace) >= 0)
                    .OverrideFailureMessage("The thrown mob never bounced off the wall.")
                    .IsTrue();
                float pre = thrown.LastWallBouncePreMoveVelocityX;
                AssertThat(pre > StoryEnemyCombatRules.WallBounceMinSpeedPixelsPerSecond).IsTrue();
                AssertThat(Mathf.Abs(thrown.Velocity.X - (-0.4f * pre)) <= 1f).IsTrue();
                AssertThat(thrown.LastWallImpactDealt).IsEqual(1);
                AssertThat(hp0 - thrown.CurrentHP).IsEqual(1);

                // The flight ends on the floor as before, and its latch with it.
                int guard = 0;
                while (thrown.IsThrownFlight && guard++ < 120) thrown._PhysicsProcess(Step);
                AssertThat(thrown.IsThrownFlight).IsFalse();
                AssertThat(thrown.IsWallBounceArmed).IsFalse();
                AssertThat(hp0 - thrown.CurrentHP).IsEqual(1);
            } finally {
                thrown.Free();
                wall.Free();
            }

            // A throw that reaches the wall at 300 px/s or slower does not bounce.
            EnemyController lobbed = CreateMob("chrono_slasher", new Vector2(OriginX, 0f));
            wallFace = OriginX + BodyHalfWidth(lobbed) + 40f;
            wall = CreateWall(wallFace);
            try {
                SettleOnFloor(lobbed);
                int hp0 = lobbed.CurrentHP;
                lobbed.BeginHeld();
                lobbed.LaunchThrown(new Vector2(250f, -150f), bowlingDamage: 1, throwDamage: 10);
                for (int frame = 0; frame < 60; frame++) {
                    lobbed._PhysicsProcess(Step);
                    AssertThat(lobbed.Velocity.X >= -1f).IsTrue();
                }
                AssertThat(lobbed.LastWallBouncePreMoveVelocityX).IsEqual(0f);
                AssertThat(hp0 - lobbed.CurrentHP).IsEqual(0);
            } finally {
                lobbed.Free();
                wall.Free();
                floor.Free();
            }
        });
    }

    // ---- helpers ------------------------------------------------------------

    /// <summary>
    /// Steps <paramref name="mob"/> until it bounces off the wall (returns the
    /// frame) or <paramref name="frames"/> run out (returns −1). Asserts the body
    /// never passes the wall face.
    /// </summary>
    private static int StepUntilBounce(EnemyController mob, int frames, float wallFace) {
        for (int frame = 0; frame < frames; frame++) {
            mob._PhysicsProcess(Step);
            AssertThat(mob.GlobalPosition.X + BodyHalfWidth(mob) <= wallFace + 0.5f)
                .OverrideFailureMessage($"The body passed the wall face at x {mob.GlobalPosition.X:F1}.")
                .IsTrue();
            if (mob.LastWallBouncePreMoveVelocityX != 0f) return frame;
        }
        return -1;
    }

    private static void AssertNoBounce(
        string label, string enemyID, float wallGap, bool seam, bool overlapWall, HitPayload hit,
        int expectedIntake, bool expectArmed) {
        StaticBody2D floorA;
        StaticBody2D floorB = null;
        if (seam) {
            floorA = CreateFloorPiece(OriginX - 1500f, OriginX + 40f);
            floorB = CreateFloorPiece(OriginX + 40f, OriginX + 1500f);
        } else {
            floorA = CreateFloor();
        }
        EnemyController mob = CreateMob(enemyID, new Vector2(OriginX, overlapWall ? -80f : 0f));
        StaticBody2D wall = null;
        if (overlapWall) {
            // The phaser's body overlaps the wall's face by 10 px.
            wall = CreateWall(OriginX + BodyHalfWidth(mob) - 10f, topY: -400f, bottomY: 0f);
        } else if (wallGap >= 0f) {
            wall = CreateWall(OriginX + BodyHalfWidth(mob) + wallGap);
        }
        try {
            if (!overlapWall) SettleOnFloor(mob);
            int hp0 = mob.CurrentHP;
            mob.GetNode<Hurtbox>("Hurtbox").TakeHit(hit);
            AssertThat(hp0 - mob.CurrentHP)
                .OverrideFailureMessage($"{label}: the hit dealt {hp0 - mob.CurrentHP}.")
                .IsEqual(expectedIntake);
            AssertThat(mob.IsWallBounceArmed)
                .OverrideFailureMessage($"{label}: armed {mob.IsWallBounceArmed}, expected {expectArmed}.")
                .IsEqual(expectArmed);
            for (int frame = 0; frame < 60; frame++) {
                mob._PhysicsProcess(Step);
                AssertThat(mob.Velocity.X >= -1f)
                    .OverrideFailureMessage($"{label}: the mob reversed (vx {mob.Velocity.X:F1}) at frame {frame}.")
                    .IsTrue();
            }
            AssertThat(mob.LastWallBouncePreMoveVelocityX)
                .OverrideFailureMessage($"{label}: a bounce was recorded.")
                .IsEqual(0f);
            AssertThat(mob.LastWallImpactDealt).IsEqual(0);
            AssertThat(hp0 - mob.CurrentHP)
                .OverrideFailureMessage($"{label}: HP dropped by more than the hit.")
                .IsEqual(expectedIntake);
        } finally {
            mob.Free();
            wall?.Free();
            floorA.Free();
            floorB?.Free();
        }
    }

    private static HitPayload StringHit(string hitboxID, float damage, Vector2 knockback, bool launches) => new() {
        AttackerIndex = 0,
        AttackID = "test.basic",
        HitboxID = hitboxID,
        AttackClass = AttackClass.Basic,
        Origin = HitOrigin.Basic,
        Delivery = HitDelivery.DirectHit,
        Damage = damage,
        HitstunDuration = BasicComboRules.HitstunFrames[2] / 60f,
        Knockback = knockback,
        Launches = launches,
        // Behind a right-facing mob, so no frontal reduction can apply.
        HitOrigin = new Vector2(OriginX - 5000f, 0f),
        AttackerFacingRight = true
    };

    /// <summary>A string finisher with the template's -2 x 4.5 lift; no hero is named, so no D6a flight.</summary>
    private static HitPayload Finisher(float damage, float forceX) =>
        StringHit("combo_3", damage, new Vector2(forceX, -2f * BasicComboRules.KnockbackMultipliers[2]), launches: true);

    private static float BodyHalfWidth(EnemyController mob) =>
        ((RectangleShape2D)mob.GetNode<CollisionShape2D>("CollisionShape2D").Shape).Size.X * 0.5f;

    private static void SettleOnFloor(EnemyController mob) {
        for (int frame = 0; frame < 8 && !mob.IsOnFloor(); frame++) mob._PhysicsProcess(Step);
        AssertThat(mob.IsOnFloor()).IsTrue();
    }

    /// <summary>A passive mob: never aggroes, holds its post.</summary>
    private static EnemyController CreateMob(string enemyID, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        data.AggroRadius = 0f;
        if (data.Behavior != DefaultBehavior.Flying) data.Behavior = DefaultBehavior.StandGuard;
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        return enemy;
    }

    private static StaticBody2D CreateFloor() => CreateFloorPiece(OriginX - 1500f, OriginX + 1500f);

    /// <summary>An Environment floor from <paramref name="left"/> to <paramref name="right"/> whose top is at y = 0.</summary>
    private static StaticBody2D CreateFloorPiece(float left, float right) {
        var floor = new StaticBody2D {
            Name = "StoryWallBounceFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(right - left, 40f) },
            Position = new Vector2((left + right) * 0.5f, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }

    /// <summary>A 40-wide Environment wall whose left face is at <paramref name="faceX"/>.</summary>
    private static StaticBody2D CreateWall(float faceX, float topY = -400f, float bottomY = 0f) {
        var wall = new StaticBody2D {
            Name = "StoryWallBounceWall",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        wall.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(40f, bottomY - topY) },
            Position = new Vector2(faceX + 20f, (topY + bottomY) * 0.5f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(wall);
        return wall;
    }
}
