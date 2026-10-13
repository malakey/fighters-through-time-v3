using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// D6a (user direction 2026-10-11, ledger <c>USER-FINISHER-CARRY</c>): a hero's
/// basic-string finisher lands a Standard (and, by the user's addendum, an
/// Elite) mob about one step outside that hero's own hit-1 reach, a heavy
/// finisher (Lincoln) past the widest reach, and the launched mob flies with
/// its AI suspended until it touches down. Real strings in real physics frames.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryFinisherCarryTests {
    private const float OriginX = 52000f;
    /// <summary>Pushbox contact between the hero (18 px) and a Standard mob (19 px).</summary>
    private const float StandardContactPixels = 37f;
    /// <summary>Pushbox contact between the hero (18 px) and an Elite mob (24 px).</summary>
    private const float EliteContactPixels = 42f;

    [TestCase]
    public async Task EveryHerosFinisherLandsAStandardAboutOneStepOutsideItsHit1ReachAndAHeavyFinisherLandsFarthest() {
        var runs = new List<CarryRun>();
        foreach (string heroID in CharacterRoster.ToList()) {
            runs.Add(await RunString(heroID, "chrono_slasher", aggroed: true, gap: StandardContactPixels));
        }
        GD.Print(Table("C1 close string, aggroed chrono_slasher", runs));

        foreach (CarryRun run in runs) AssertLandsOneStepOut(run);
        CarryRun heavy = runs.Single(run => run.Force >= StoryEnemyCombatRules.HeavyFinisherForce);
        foreach (CarryRun run in runs.Where(run => run != heavy)) {
            AssertThat(heavy.TouchdownGap > run.TouchdownGap)
                .OverrideFailureMessage(
                    $"The heavy finisher ({heavy.Hero}, {heavy.TouchdownGap:F1} px) must land farther than {run.Hero} ({run.TouchdownGap:F1} px).")
                .IsTrue();
        }
    }

    [TestCase]
    public async Task ATipRangeStringAndARangedMobAlsoLandOneStepOut() {
        // (a) Hit 1 lands at the tip of Einstein's reach on a passive slasher.
        PlayerController probe = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        float tipGap;
        try {
            tipGap = probe.StoryStringSpanPixels(0).Far + 23f - 1f;
        } finally {
            probe.Free();
        }
        CarryRun tip = await RunString("einstein", "chrono_slasher", aggroed: false, gap: tipGap);
        // (b) A close string on a ranged-primary mob, which stops at its stun's end today.
        CarryRun ranged = await RunString("einstein", "laser_rifle_infantry", aggroed: true, gap: StandardContactPixels);
        GD.Print(Table("C2 tip-range slasher and close-string laser_rifle_infantry", new List<CarryRun> { tip, ranged }));

        AssertLandsOneStepOut(tip);
        AssertLandsOneStepOut(ranged);
        AssertThat(System.Math.Abs(tip.TouchdownGap - tip.Target) <= 6f)
            .OverrideFailureMessage($"Tip range: landed {tip.TouchdownGap:F1} px, target {tip.Target:F1} px.")
            .IsTrue();
        AssertThat(System.Math.Abs(ranged.TouchdownGap - ranged.Target) <= 6f)
            .OverrideFailureMessage($"Ranged mob: landed {ranged.TouchdownGap:F1} px, target {ranged.Target:F1} px.")
            .IsTrue();
    }

    [TestCase]
    public async Task AnElitesFinisherAlsoLandsOneStepOutsideItsReachWithoutSteeringBack() {
        // User addendum (2026-10-11): elites get the shorter finisher carry too.
        // Their hit-1 reach is measured to the elite hurtbox (29 px half-width).
        CarryRun joan = await RunString("joan", "chrono_guard_elite", aggroed: true, gap: EliteContactPixels, elite: true);
        CarryRun lincoln = await RunString("lincoln", "chrono_guard_elite", aggroed: true, gap: EliteContactPixels, elite: true);
        GD.Print(Table("C3b close string, aggroed chrono_guard_elite", new List<CarryRun> { joan, lincoln }));
        foreach (CarryRun run in new[] { joan, lincoln }) {
            AssertThat(run.MobHalfWidth).IsEqual(29f);
            AssertLandsOneStepOut(run);
        }
        // Lincoln's heavy finisher still lands the elite past the widest reach's
        // one-step target, and farther than Joan's.
        AssertThat(lincoln.TouchdownGap > joan.TouchdownGap).IsTrue();
        AssertThat(lincoln.Target > StoryEnemyCombatRules.WidestHit1PivotReachPixels(29f)
            + StoryEnemyCombatRules.FinisherLandingStepPixels).IsTrue();
    }

    [TestCase]
    public async Task ASpecialLaunchAnUpAttackAndALaunchingHit2KeepTheS1DragAndTheirAI() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor();
            PlayerController attacker = CreateBareAttacker(new Vector2(OriginX - 60f, 0f));
            try {
                HitPayload special = AttackerHit(attacker, AttackClass.Special, "special_test", launches: true);
                HitPayload upAttack = AttackerHit(attacker, AttackClass.Basic, BasicComboRules.UpAttackHitboxID, launches: true);
                HitPayload launchingHit2 = AttackerHit(attacker, AttackClass.Basic, "combo_2", launches: true);
                foreach (HitPayload hit in new[] { special, upAttack, launchingHit2 }) {
                    EnemyController mob = CreatePassiveMob("chrono_slasher", new Vector2(OriginX, 0f));
                    try {
                        SettleOnFloor(mob);
                        mob.GetNode<Hurtbox>("Hurtbox").TakeHit(hit);
                        string at = hit.HitboxID;
                        AssertThat(mob.CurrentState).IsEqual(EnemyState.Stunned);
                        AssertThat(mob.IsInFinisherFlight)
                            .OverrideFailureMessage($"{at}: only a string finisher arms the finisher flight.")
                            .IsFalse();
                        AssertThat(mob.FinisherCarryDeceleration).IsEqual(0f);
                        AssertThat(mob.IsWallBounceArmed)
                            .OverrideFailureMessage($"{at}: every player-dealt launch arms the wall bounce (D9).")
                            .IsTrue();

                        // S1's grounded friction, nothing more, on the first free frame.
                        int guard = 0;
                        while (mob.HitstopFramesRemaining > 0 && guard++ < 30) mob._PhysicsProcess(Step);
                        float before = mob.Velocity.X;
                        mob._PhysicsProcess(Step);
                        float expected = Mathf.MoveToward(before, 0f,
                            StoryEnemyCombatRules.StunnedGroundFrictionPixelsPerSecondSquared * Step);
                        AssertThat(Mathf.Abs(mob.Velocity.X - expected) < 0.5f)
                            .OverrideFailureMessage($"{at}: vx {mob.Velocity.X:F2}, S1 alone gives {expected:F2}.")
                            .IsTrue();

                        // The AI takes back over at the stun's natural expiry, airborne
                        // or not, and the launch's bounce latch goes with it.
                        guard = 0;
                        while (mob.CurrentState == EnemyState.Stunned && guard++ < 240) mob._PhysicsProcess(Step);
                        AssertThat(mob.CurrentState).IsEqual(EnemyState.Patrol);
                        AssertThat(mob.IsWallBounceArmed).IsFalse();
                        AssertThat(mob.IsInFinisherFlight).IsFalse();
                    } finally {
                        mob.Free();
                    }
                }
            } finally {
                attacker.Free();
                floor.Free();
            }
        });
    }

    [TestCase]
    public async Task TheLaunchLatchesClearOnTouchdownStunExpiryATripAGrabAPoolCycleAKnockbackWriteAndARewind() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor();
            PlayerController attacker = CreateBareAttacker(new Vector2(OriginX - 60f, 0f));
            try {
                // (a) A finisher flight that touches down.
                WithFinisherFlight(attacker, "touchdown", mob => {
                    int guard = 0;
                    while ((mob.CurrentState == EnemyState.Stunned || !mob.IsOnFloor()) && guard++ < 240) {
                        mob._PhysicsProcess(Step);
                    }
                    AssertThat(mob.IsOnFloor() && mob.CurrentState != EnemyState.Stunned).IsTrue();
                });
                // (b) A non-finisher launch (D9 only) at its stun's natural expiry.
                EnemyController upAttacked = CreatePassiveMob("chrono_slasher", new Vector2(OriginX, 0f));
                try {
                    SettleOnFloor(upAttacked);
                    upAttacked.GetNode<Hurtbox>("Hurtbox").TakeHit(
                        AttackerHit(attacker, AttackClass.Basic, BasicComboRules.UpAttackHitboxID, launches: true));
                    AssertThat(upAttacked.IsWallBounceArmed).IsTrue();
                    AssertThat(upAttacked.IsInFinisherFlight).IsFalse();
                    int guard = 0;
                    while (upAttacked.CurrentState == EnemyState.Stunned && guard++ < 240) upAttacked._PhysicsProcess(Step);
                    AssertCleared(upAttacked, "stun expiry");
                } finally {
                    upAttacked.Free();
                }
                // (c) An Armored Recovery trip.
                WithFinisherFlight(attacker, "armored recovery", mob => {
                    for (int stun = 0; stun < 8 && !mob.IsArmoredRecovery; stun++) mob.ApplyStun(1.0f, 0f, true);
                    AssertThat(mob.IsArmoredRecovery).IsTrue();
                });
                // (d) A grab.
                WithFinisherFlight(attacker, "grab", mob => mob.BeginHeld());
                // (d') A throw: its own bounce latch replaces the flight's.
                EnemyController thrown = CreatePassiveMob("chrono_slasher", new Vector2(OriginX, 0f));
                try {
                    SettleOnFloor(thrown);
                    thrown.GetNode<Hurtbox>("Hurtbox").TakeHit(FinisherFrom(attacker));
                    AssertThat(thrown.IsInFinisherFlight).IsTrue();
                    thrown.BeginHeld();
                    thrown.LaunchThrown(new Vector2(400f, -200f), bowlingDamage: 1, throwDamage: 10);
                    AssertThat(thrown.IsInFinisherFlight).IsFalse();
                    AssertThat(thrown.FinisherCarryDeceleration).IsEqual(0f);
                    AssertThat(thrown.IsWallBounceArmed)
                        .OverrideFailureMessage("A throw arms its own bounce (user addendum, D9).")
                        .IsTrue();
                } finally {
                    thrown.Free();
                }
                // (e) A pool cycle.
                WithFinisherFlight(attacker, "pool cycle", mob => {
                    mob.OnDespawn();
                    mob.OnSpawn();
                });
                // (f) The rewind freeze.
                WithFinisherFlight(attacker, "rewind freeze", mob => {
                    mob.SetStoryRewindFrozen(true);
                    mob.SetStoryRewindFrozen(false);
                });
                // (g) A later knockback write (the path crowd bowling uses).
                WithFinisherFlight(attacker, "knockback write", mob => mob.ApplyKnockback(new Vector2(2f, -1f), true));
            } finally {
                attacker.Free();
                floor.Free();
            }
        });
    }

    [TestCase]
    public void NothingInTheFighterSimReadsTheStoryEnemyRulebook() {
        string root = ProjectSettings.GlobalizePath("res://scripts/FighterSim");
        var offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("StoryEnemyCombatRules"))
            .Select(Path.GetFileName)
            .ToList();
        if (offenders.Count > 0) AssertThat(string.Join(" | ", offenders)).IsEqual("");
    }

    // ---- shared assertions --------------------------------------------------

    private static void AssertLandsOneStepOut(CarryRun run) {
        string at = $"{run.Hero} vs {run.Enemy}";
        AssertThat(run.HeroStunned)
            .OverrideFailureMessage($"{at}: harness fault, the mob hit the hero.")
            .IsFalse();
        AssertThat(run.Hits)
            .OverrideFailureMessage($"{at}: only {run.Hits} of the string's three hits landed.")
            .IsEqual(BasicComboRules.ComboHits);
        AssertThat(run.TouchdownFrame >= 0)
            .OverrideFailureMessage($"{at}: the launched mob never touched down.")
            .IsTrue();
        AssertThat(run.ArmedFlight)
            .OverrideFailureMessage($"{at}: the finisher did not arm a finisher flight.")
            .IsTrue();
        AssertThat(System.Math.Abs(run.ArmedTarget - run.Target) < 0.01f)
            .OverrideFailureMessage($"{at}: the controller armed a {run.ArmedTarget:F2} px target, the rule gives {run.Target:F2} px.")
            .IsTrue();
        AssertThat(System.Math.Abs(run.ArmedDeceleration - run.CarryDeceleration) <= 1f)
            .OverrideFailureMessage($"{at}: the controller armed {run.ArmedDeceleration:F1} px/s², the rule gives {run.CarryDeceleration:F1}.")
            .IsTrue();
        AssertThat(run.SteeredBack)
            .OverrideFailureMessage($"{at}: the mob steered back mid-air after its stun ended.")
            .IsFalse();
        AssertThat(run.TouchdownGap > run.OwnReach + 10f)
            .OverrideFailureMessage(
                $"{at}: landed {run.TouchdownGap:F1} px, inside {run.OwnReach:F1} + 10 px of the hero's hit-1 reach.")
            .IsTrue();
        AssertThat(run.TouchdownGap <= run.Target + 2f)
            .OverrideFailureMessage($"{at}: landed {run.TouchdownGap:F1} px, past the {run.Target:F1} px target.")
            .IsTrue();
        if (run.CarryDeceleration > StoryEnemyCombatRules.StunnedGroundFrictionPixelsPerSecondSquared) {
            AssertThat(System.Math.Abs(run.TouchdownGap - run.Target) <= 6f)
                .OverrideFailureMessage(
                    $"{at}: the carry rule binds ({run.CarryDeceleration:F0} px/s²) but the mob landed {run.TouchdownGap:F1} px against a {run.Target:F1} px target.")
                .IsTrue();
        }
    }

    // ---- harness ------------------------------------------------------------

    private sealed class CarryRun {
        public string Hero;
        public string Enemy;
        public int Hits;
        public bool HeroStunned;
        public float MobHalfWidth;
        public float OwnReach;
        public float Force;
        public float Target;
        public int FinisherFrame = -1;
        public float GapAtFinisher;
        public float LaunchVx;
        public float CarryDeceleration;
        public int StunEndFrame = -1;
        public int TouchdownFrame = -1;
        public float TouchdownGap = float.NaN;
        public int ArmorEndFrame = -1;
        public bool SteeredBack;
        public bool ArmedFlight;
        public float ArmedDeceleration;
        public float ArmedTarget;
    }

    /// <summary>
    /// A slotless factory hero at <see cref="OriginX"/> facing right holds
    /// BasicAttack through a full string on a fresh mob <paramref name="gap"/>
    /// px in front of it (aggroed on the hero, or a passive post that never
    /// aggroes), then releases. Records each HP drop, the finisher's gap and
    /// launch speed, the stun's end, the first floor contact after the launch
    /// and the getup armor's end, sampled once per real physics frame.
    /// </summary>
    private static async Task<CarryRun> RunString(string heroID, string enemyID, bool aggroed, float gap, bool elite = false) {
        var tree = (SceneTree)Engine.GetMainLoop();
        Difficulty previousDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        GameManager.Instance.CurrentSession.Difficulty = Difficulty.Normal;
        StaticBody2D floor = CreateFloor();
        PlayerController hero = null;
        EnemyController mob = null;
        var run = new CarryRun { Hero = heroID, Enemy = enemyID };
        try {
            hero = CharacterFactory.CreateCharacter(heroID, 0, applyStoryProgression: false);
            tree.Root.AddChild(hero);
            hero.GlobalPosition = new Vector2(OriginX, -2f);
            var source = new BufferedInputSource();
            InputManager.Instance.SetInputSource(0, source);
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
            for (int frame = 0; frame < 12; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(hero.IsOnFloor() && hero.IsFacingRight)
                .OverrideFailureMessage($"{heroID}: the harness hero must stand on the floor facing right.")
                .IsTrue();

            mob = CreateMob(enemyID, new Vector2(hero.GlobalPosition.X + gap, 0f), aggroed, elite, hero);
            run.MobHalfWidth = HurtboxHalfWidth(mob);
            run.OwnReach = hero.StoryStringSpanPixels(0).Far + run.MobHalfWidth;
            BasicStringProfile profile = BasicComboRules.StringProfileFor(heroID);
            run.Force = hero.Data.BasicAttackKnockback * profile.FinisherKnockbackTenths / 10f;
            run.Target = StoryEnemyCombatRules.FinisherLandingTargetPixels(
                run.OwnReach, StoryEnemyCombatRules.WidestHit1PivotReachPixels(run.MobHalfWidth), run.Force);

            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.BasicAttack));
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.BasicAttack,
                previousHeld: GameplayButtons.BasicAttack));

            int lastHP = mob.CurrentHP;
            bool airborneSinceFinisher = false;
            float lastX = 0f;
            for (int frame = 1; frame < 420; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                run.HeroStunned |= hero.CurrentState == CharacterState.Stunned;
                if (mob.CurrentState == EnemyState.Dead) break;
                if (mob.CurrentHP < lastHP) {
                    lastHP = mob.CurrentHP;
                    if (run.FinisherFrame < 0) run.Hits++;
                    if (run.Hits == BasicComboRules.ComboHits && run.FinisherFrame < 0) {
                        run.FinisherFrame = frame;
                        run.GapAtFinisher = mob.GlobalPosition.X - hero.GlobalPosition.X;
                        run.LaunchVx = mob.Velocity.X;
                        run.CarryDeceleration = StoryEnemyCombatRules.FinisherCarryDeceleration(
                            Mathf.Abs(run.LaunchVx),
                            StoryEnemyCombatRules.FinisherCarryPixels(run.Target, run.GapAtFinisher));
                        run.ArmedFlight = mob.IsInFinisherFlight;
                        run.ArmedDeceleration = mob.FinisherCarryDeceleration;
                        run.ArmedTarget = mob.LastFinisherLandingTargetPixels;
                        // Release: the held button must not open a new string
                        // on a mob that comes back into reach.
                        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None,
                            previousHeld: GameplayButtons.BasicAttack));
                    }
                }
                if (run.FinisherFrame < 0) continue;

                float x = mob.GlobalPosition.X;
                if (run.StunEndFrame < 0 && mob.CurrentState != EnemyState.Stunned) {
                    run.StunEndFrame = frame;
                } else if (run.StunEndFrame >= 0 && run.TouchdownFrame < 0 && x < lastX - 0.01f) {
                    run.SteeredBack = true;
                }
                lastX = x;

                if (run.TouchdownFrame < 0) {
                    if (!mob.IsOnFloor()) {
                        airborneSinceFinisher = true;
                    } else if (airborneSinceFinisher) {
                        run.TouchdownFrame = frame;
                        run.TouchdownGap = x - hero.GlobalPosition.X;
                    }
                }
                if (run.StunEndFrame >= 0 && run.ArmorEndFrame < 0 && !mob.IsGetupArmored) run.ArmorEndFrame = frame;
                if (run.TouchdownFrame >= 0 && run.ArmorEndFrame >= 0) break;
            }
            return run;
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            if (mob != null && GodotObject.IsInstanceValid(mob)) mob.Free();
            if (hero != null && GodotObject.IsInstanceValid(hero)) hero.Free();
            floor.Free();
            GameManager.Instance.CurrentSession.Difficulty = previousDifficulty;
        }
    }

    private static string Table(string title, List<CarryRun> runs) {
        var text = new StringBuilder();
        text.AppendLine($"[D6a] {title}");
        text.AppendLine("[D6a] hero | mob | R1 | F | target | hits | g0 | v0 | carry decel | lands (±R1) | touchdown f | stun end f | armor end f | steered back");
        foreach (CarryRun run in runs) {
            int Rel(int frame) => frame < 0 ? -1 : frame - run.FinisherFrame;
            text.AppendLine(
                $"[D6a] {run.Hero} | {run.Enemy} | {run.OwnReach:F1} | {run.Force:F2} | {run.Target:F1} | {run.Hits} | "
                + $"{run.GapAtFinisher:F1} | {run.LaunchVx:F0} | {run.CarryDeceleration:F0} | "
                + $"{run.TouchdownGap:F1} ({run.TouchdownGap - run.OwnReach:+0.0;-0.0}) | {Rel(run.TouchdownFrame)} | "
                + $"{Rel(run.StunEndFrame)} | {Rel(run.ArmorEndFrame)} | {run.SteeredBack}");
        }
        return text.ToString();
    }

    private static float HurtboxHalfWidth(EnemyController mob) {
        var shape = mob.GetNode<CollisionShape2D>("Hurtbox/CollisionShape2D");
        return ((RectangleShape2D)shape.Shape).Size.X * 0.5f;
    }

    private static EnemyController CreateMob(string enemyID, Vector2 position, bool aggroed, bool elite, PlayerController hero) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        if (!aggroed) {
            // A passive post: never aggroes, holds still when it is free to act.
            data.AggroRadius = 0f;
            data.Behavior = DefaultBehavior.StandGuard;
        }
        if (elite) {
            // The elite's string must complete and its opener must not beat
            // the hero's: no resistance, and its first answer parked.
            data.StunResistance = 0f;
            data.ReactionDelayMinFrames = 120;
            data.ReactionDelayMaxFrames = 120;
        }
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        if (aggroed) enemy.SetChaseTargetForTest(hero);
        return enemy;
    }

    private const float Step = 1f / 60f;

    private static void AssertCleared(EnemyController mob, string where) {
        AssertThat(mob.IsInFinisherFlight)
            .OverrideFailureMessage($"{where}: the finisher flight survived.")
            .IsFalse();
        AssertThat(mob.FinisherCarryDeceleration)
            .OverrideFailureMessage($"{where}: the carry deceleration survived.")
            .IsEqual(0f);
        AssertThat(mob.IsWallBounceArmed)
            .OverrideFailureMessage($"{where}: the wall-bounce latch survived.")
            .IsFalse();
    }

    /// <summary>Arms a finisher flight on a fresh passive slasher, runs <paramref name="clear"/>, and asserts every latch is gone.</summary>
    private static void WithFinisherFlight(PlayerController attacker, string where, System.Action<EnemyController> clear) {
        EnemyController mob = CreatePassiveMob("chrono_slasher", new Vector2(OriginX, 0f));
        try {
            SettleOnFloor(mob);
            mob.GetNode<Hurtbox>("Hurtbox").TakeHit(FinisherFrom(attacker));
            AssertThat(mob.IsInFinisherFlight)
                .OverrideFailureMessage($"{where}: the harness finisher did not arm a flight.")
                .IsTrue();
            AssertThat(mob.FinisherCarryDeceleration > 0f).IsTrue();
            AssertThat(mob.IsWallBounceArmed).IsTrue();
            clear(mob);
            AssertCleared(mob, where);
        } finally {
            if (GodotObject.IsInstanceValid(mob)) mob.Free();
        }
    }

    /// <summary>A hand-built payload stamped with <paramref name="attacker"/>'s instance, so D6a can resolve the hero.</summary>
    private static HitPayload AttackerHit(PlayerController attacker, AttackClass attackClass, string hitboxID, bool launches) => new() {
        AttackerIndex = 0,
        AttackID = attackClass == AttackClass.Special ? "test.special" : "test.basic",
        HitboxID = hitboxID,
        AttackClass = attackClass,
        Origin = attackClass == AttackClass.Special ? HitOrigin.Special : HitOrigin.Basic,
        Delivery = HitDelivery.DirectHit,
        Damage = 1f,
        HitstunDuration = BasicComboRules.HitstunFrames[2] / 60f,
        Knockback = new Vector2(3.5f * BasicComboRules.KnockbackMultipliers[2], -2f * BasicComboRules.KnockbackMultipliers[2]),
        Launches = launches,
        // Behind a right-facing mob, so no frontal reduction can apply.
        HitOrigin = new Vector2(OriginX - 5000f, 0f),
        AttackerFacingRight = true,
        SourceActorId = attacker.GetInstanceId()
    };

    /// <summary>A Joan-shape string finisher from <paramref name="attacker"/>.</summary>
    private static HitPayload FinisherFrom(PlayerController attacker) =>
        AttackerHit(attacker, AttackClass.Basic, "combo_3", launches: true);

    /// <summary>A bare hero (no data, template string) that never ticks: the attacker a hand-built payload names.</summary>
    private static PlayerController CreateBareAttacker(Vector2 position) {
        var attacker = new PlayerController { Name = "StoryFinisherCarryAttacker", Position = position };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(attacker);
        attacker.GlobalPosition = position;
        return attacker;
    }

    /// <summary>A mob at a post it never leaves: no aggro, StandGuard.</summary>
    private static EnemyController CreatePassiveMob(string enemyID, Vector2 position) =>
        CreateMob(enemyID, position, aggroed: false, elite: false, hero: null);

    private static void SettleOnFloor(EnemyController mob) {
        for (int frame = 0; frame < 8 && !mob.IsOnFloor(); frame++) mob._PhysicsProcess(Step);
        AssertThat(mob.IsOnFloor()).IsTrue();
    }

    /// <summary>A 3000-wide Environment floor whose top is at y = 0.</summary>
    private static StaticBody2D CreateFloor() {
        var floor = new StaticBody2D {
            Name = "StoryFinisherCarryFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(3000f, 40f) },
            Position = new Vector2(OriginX, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }
}
