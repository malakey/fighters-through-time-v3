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
/// Gameplay review 2026-11-03 (rulings 2b, 2b+; plan section 1.12): the three
/// soft-counter traits on a real <see cref="EnemyController"/>. Armoured on
/// <c>shock_shield_legionnaire</c>, Catcher on <c>cyber_guard</c>, Anti-air on
/// <c>plasma_spear_ward</c>. The pure rules are <c>EnemyTraitRulesTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EnemyControllerTraitTests {
    private const float Step = 1f / 60f;
    // Away from every other suite's geometry.
    private const float OriginX = 121000f;
    private static readonly Vector2 Origin = new(OriginX, 0f);

    // === Armoured ============================================================

    [TestCase]
    public void AnIntactArmouredMobTakesHalfFromBasicAndSpecialAndWearsTheGold() {
        EnemyController basicTarget = CreateMob("shock_shield_legionnaire", Origin);
        EnemyController specialTarget = CreateMob("shock_shield_legionnaire", Origin + new Vector2(400f, 0f));
        try {
            AssertThat(basicTarget.ActiveTrait).IsEqual(EnemyTrait.Armoured);
            AssertThat(basicTarget.IsTraitArmourIntact).IsTrue();
            AssertThat(basicTarget.IsArmourGlowShowing)
                .OverrideFailureMessage("An intact armour wears the shared gold glow.").IsTrue();

            // Basic: S4's pacing and the armour inside the one rounding.
            AssertThat(Dealt(basicTarget, StringHit("combo_3", 12f)))
                .IsEqual(StoryEnemyCombatRules.ScaleIntakeDamage(12,
                    StoryEnemyCombatRules.StandardBasicPacingScale(12) * EnemyTraitRules.ArmouredDamageScale));
            AssertThat(Dealt(specialTarget, Hit(AttackClass.Special, HitOrigin.Special, 20f))).IsEqual(10);
            AssertThat(basicTarget.IsTraitArmourIntact)
                .OverrideFailureMessage("Damage alone never takes the armour off.").IsTrue();
        } finally {
            basicTarget.Free();
            specialTarget.Free();
        }
    }

    [TestCase]
    public void TheThrowsLaunchTakesTheArmourAndTheGoldOffAndAHoldDoesNot() {
        EnemyController enemy = CreateMob("shock_shield_legionnaire", Origin);
        try {
            enemy.BeginHeld();
            AssertThat(enemy.IsTraitArmourIntact).OverrideFailureMessage("A hold alone is no throw.").IsTrue();
            enemy.ReleaseHeld();
            AssertThat(enemy.IsTraitArmourIntact).IsTrue();

            int strips = EnemyTraitTally.ArmourStrips;
            enemy.BeginHeld();
            enemy.LaunchThrown(new Vector2(200f, -200f), bowlingDamage: 1, throwDamage: 6);
            AssertThat(enemy.IsTraitArmourIntact).IsFalse();
            AssertThat(enemy.IsArmourGlowShowing).OverrideFailureMessage("The gold goes with the armour.").IsFalse();
            AssertThat(EnemyTraitTally.ArmourStrips).IsEqual(strips + 1);

            // From here it takes a whole hit, and a later hold changes nothing.
            AssertThat(Dealt(enemy, Hit(AttackClass.Special, HitOrigin.Special, 20f))).IsEqual(20);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public async Task UltimatesThrowDamageBowlingAndHazardsPassTheArmourWhole() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            EnemyController armoured = CreateMob("shock_shield_legionnaire", Origin);
            EnemyController thrown = CreateMob("chrono_slasher", Origin + new Vector2(20f, 0f));
            try {
                AssertThat(Dealt(armoured, Hit(AttackClass.Ultimate, HitOrigin.Ultimate, 10f))).IsEqual(10);
                AssertThat(Dealt(armoured, Hit(AttackClass.Hazard, HitOrigin.Environment, 6f, HitDelivery.Hazard))).IsEqual(6);
                // The throw's own damage arrives through this overload.
                int hp = armoured.CurrentHP;
                armoured.TakeDamage(5);
                AssertThat(hp - armoured.CurrentHP).IsEqual(5);

                // Crowd bowling.
                hp = armoured.CurrentHP;
                thrown.LaunchThrown(Vector2.Zero, bowlingDamage: 7);
                thrown._PhysicsProcess(Step);
                AssertThat(hp - armoured.CurrentHP).IsEqual(7);
                AssertThat(armoured.IsTraitArmourIntact)
                    .OverrideFailureMessage("Being bowled is not being thrown.").IsTrue();
            } finally {
                armoured.Free();
                thrown.Free();
            }
        });
    }

    [TestCase]
    public async Task AWallImpactIsReckonedFromTheHitBeforeTheArmour() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateBlock(new Rect2(OriginX - 1500f, 0f, 3000f, 40f));
            EnemyController enemy = CreateMob("shock_shield_legionnaire", Origin);
            float halfWidth = ((RectangleShape2D)enemy.GetNode<CollisionShape2D>("CollisionShape2D").Shape).Size.X * 0.5f;
            StaticBody2D wall = CreateBlock(new Rect2(OriginX + halfWidth + 50f, -400f, 40f, 400f));
            try {
                for (int frame = 0; frame < 8 && !enemy.IsOnFloor(); frame++) enemy._PhysicsProcess(Step);
                AssertThat(enemy.IsOnFloor()).IsTrue();

                HitPayload finisher = StringHit("combo_3", 20f);
                finisher.Knockback = new Vector2(15.75f, -9f);
                finisher.Launches = true;
                finisher.HitstunDuration = BasicComboRules.HitstunFrames[2] / 60f;
                enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(finisher);
                AssertThat(enemy.IsWallBounceArmed).IsTrue();

                for (int frame = 0; frame < 60 && enemy.LastWallBouncePreMoveVelocityX == 0f; frame++) {
                    enemy._PhysicsProcess(Step);
                }
                AssertThat(enemy.LastWallBouncePreMoveVelocityX != 0f).OverrideFailureMessage("No bounce.").IsTrue();
                int wholeIntake = StoryEnemyCombatRules.ScaleIntakeDamage(20, StoryEnemyCombatRules.StandardBasicPacingScale(20));
                AssertThat(enemy.LastWallImpactDealt).IsEqual(StoryEnemyCombatRules.WallImpactDamage(wholeIntake));
            } finally {
                enemy.Free();
                wall.Free();
                floor.Free();
            }
        });
    }

    [TestCase]
    public void TraitsFollowTheBodysDataAndASummonCarriesNone() {
        EnemyController enemy = CreateMob("shock_shield_legionnaire", Origin);
        try {
            // A pool cycle gives the armour back.
            enemy.LaunchThrown(Vector2.Zero, bowlingDamage: 0);
            AssertThat(enemy.IsTraitArmourIntact).IsFalse();
            enemy.OnSpawn();
            AssertThat(enemy.IsTraitArmourIntact).IsTrue();
            AssertThat(enemy.IsArmourGlowShowing).IsTrue();

            // The shared pool hands the body to a mob with no trait, and back.
            EnemyData slasher = AuthoredResources.Load<EnemyData>("res://resources/Enemies/chrono_slasher.tres");
            EnemyData legionnaire = enemy.Data;
            enemy.ApplyData(slasher);
            AssertThat(enemy.ActiveTrait).IsEqual(EnemyTrait.None);
            AssertThat(enemy.IsArmourGlowShowing).IsFalse();
            enemy.ApplyData(legionnaire);
            AssertThat(enemy.IsArmourGlowShowing)
                .OverrideFailureMessage("ApplyData clears the glow; the armour puts it back.").IsTrue();

            // An elite never acts on a trait, however its resource is authored.
            var elite = (EnemyData)legionnaire.Duplicate();
            elite.Tier = EnemyTier.Elite;
            enemy.ApplyData(elite);
            AssertThat(enemy.ActiveTrait).IsEqual(EnemyTrait.None);
            enemy.ApplyData(legionnaire);
        } finally {
            enemy.Free();
        }

        // Level 0's hold spawns its era locals as summons: the lesson's string stuns.
        EnemyController local = CreateMob("cyber_guard", Origin);
        try {
            local.IsSummoned = true;
            AssertThat(local.ActiveTrait).IsEqual(EnemyTrait.None);
            local.GetNode<Hurtbox>("Hurtbox").TakeHit(StringHit("combo_1", 6f));
            AssertThat(local.CurrentState).IsEqual(EnemyState.Stunned);
        } finally {
            local.Free();
        }
    }

    // === Catcher =============================================================

    [TestCase]
    public void StringHitsOneAndTwoDealDamageButNoStunOrKnockback() {
        EnemyController enemy = CreateMob("cyber_guard", Origin);
        try {
            AssertThat(enemy.ActiveTrait).IsEqual(EnemyTrait.Catcher);
            HitPayload first = StringHit("combo_1", 6f);
            first.Knockback = new Vector2(4f, -2f);
            first.AppliedStatus = StatusType.StaticCharge;
            first.StatusDuration = 1f;
            int catches = EnemyTraitTally.CatcherCatches;
            int dealt = Dealt(enemy, first);

            AssertThat(dealt).IsEqual(StoryEnemyCombatRules.ScaleIntakeDamage(6, StoryEnemyCombatRules.StandardBasicPacingScale(6)));
            AssertThat(enemy.CurrentState).OverrideFailureMessage("A caught hit opens no stun.").IsNotEqual(EnemyState.Stunned);
            AssertThat(enemy.Velocity).OverrideFailureMessage("A caught hit writes no knockback.").IsEqual(Vector2.Zero);
            AssertThat(enemy.StaggerBudgetSeconds).OverrideFailureMessage("A caught hit charges no poise.").IsEqual(0f);
            AssertThat(enemy.HasStatusEffect(StatusType.StaticCharge))
                .OverrideFailureMessage("Static Charge's lock is a stun in PvE.").IsFalse();
            AssertThat(EnemyTraitTally.CatcherCatches).IsEqual(catches + 1);

            // The finisher is not caught: it stuns and launches as on any mob.
            HitPayload finisher = StringHit("combo_3", 6f);
            finisher.HitstunDuration = 0.4f;
            enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(finisher);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void TheSecondHitCommitsABlockableCounterOnATwelveFrameDiamondTelegraph() {
        Node host = Host("CatcherCounterHost");
        try {
            PlayerController hero = SpawnHero(host, Origin + new Vector2(-90f, 0f));
            EnemyController enemy = CreateMob("cyber_guard", Origin);
            host.AddChild(new Node { Name = "Keep" });
            enemy.SetChaseTargetForTest(hero);
            var hitbox = enemy.GetNode<Hitbox>("Hitbox");
            int counters = EnemyTraitTally.CatcherCounters;

            enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(StringHit("combo_1", 6f));
            AssertThat(enemy.AbilityPhase).OverrideFailureMessage("Hit 1 earns nothing.").IsEqual(EnemyAbilityPhase.Idle);
            AssertThat(EnemyTraitTally.CatcherCounters).IsEqual(counters);

            enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(StringHit("combo_2", 6f));
            AssertThat(EnemyTraitTally.CatcherCounters).IsEqual(counters + 1);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Attacking);
            AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Telegraph);
            AssertThat(enemy.ActiveAbility).IsEqual(enemy.CatcherCounterAbility);
            AssertThat(enemy.IsFacingRight).OverrideFailureMessage("The counter turns to the hero.").IsFalse();
            AssertThat(enemy.TelegraphGlyphNode.Visible).IsTrue();
            AssertThat(enemy.TelegraphGlyphNode.Shape).IsEqual(TelegraphGlyphShape.GuardCrush);
            AssertThat(enemy.TraitCooldownFramesRemaining).IsEqual(StoryEnemyCombatRules.CatcherCounterCooldownFrames);

            for (int frame = 0; frame < StoryEnemyCombatRules.CatcherCounterTelegraphFrames - 1; frame++) {
                enemy.TickAbilityExecutor(Step);
                AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Telegraph);
            }
            enemy.TickAbilityExecutor(Step);
            AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Active);
            AssertThat(hitbox.IsActive).IsTrue();
            AssertThat(hitbox.Unblockable).OverrideFailureMessage("The counter is blockable.").IsFalse();
            AssertThat(hitbox.BlockChargeCost).IsEqual(BasicComboRules.GuardCrushBlockChargeCost);
            AssertThat(hitbox.Position.X < 0f).OverrideFailureMessage("The box swings toward the hero.").IsTrue();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheCooldownHoldsTheNextCounterAndACaughtHitLeavesAnAttackInFlight() {
        Node host = Host("CatcherCooldownHost");
        try {
            PlayerController hero = SpawnHero(host, Origin + new Vector2(-90f, 0f));
            EnemyController enemy = CreateMob("cyber_guard", Origin);
            enemy.SetChaseTargetForTest(hero);
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");

            // Its own swing is not cancelled, and no counter replaces it.
            enemy.BeginAttack(enemy.Data.PrimaryAttack);
            EnemyAbilityData swing = enemy.ActiveAbility;
            hurtbox.TakeHit(StringHit("combo_2", 6f));
            AssertThat(enemy.ActiveAbility).IsEqual(swing);
            AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Telegraph);
            AssertThat(enemy.TraitCooldownFramesRemaining).OverrideFailureMessage("No counter, no cooldown.").IsEqual(0);
            enemy.TickAbilityExecutor(0f);
            for (int frame = 0; frame < 120 && enemy.AbilityPhase != EnemyAbilityPhase.Idle; frame++) enemy.TickAbilityExecutor(Step);

            // The counter, ended; then a second hit 2 on the cooldown is only damage.
            hurtbox.TakeHit(StringHit("combo_2", 6f));
            AssertThat(enemy.ActiveAbility).IsEqual(enemy.CatcherCounterAbility);
            for (int frame = 0; frame < 120 && enemy.AbilityPhase != EnemyAbilityPhase.Idle; frame++) enemy.TickAbilityExecutor(Step);
            AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Idle);
            int before = enemy.CurrentHP;
            hurtbox.TakeHit(StringHit("combo_2", 6f));
            AssertThat(enemy.CurrentHP < before).IsTrue();
            AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Idle);
            AssertThat(enemy.CurrentState).OverrideFailureMessage("The catch still holds on the cooldown.").IsNotEqual(EnemyState.Stunned);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void ASpecialStunThenAStringLandsNormallyAndAGrabAnswersTheCounter() {
        Node host = Host("CatcherAnswersHost");
        try {
            PlayerController hero = SpawnHero(host, Origin + new Vector2(-90f, 0f));
            EnemyController enemy = CreateMob("cyber_guard", Origin);
            enemy.SetChaseTargetForTest(hero);
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");

            // The Special is not caught: it stuns, and a string on the stunned body lands
            // with its knockback and earns no counter.
            HitPayload special = Hit(AttackClass.Special, HitOrigin.Special, 6f);
            special.HitstunDuration = 0.5f;
            hurtbox.TakeHit(special);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            HitPayload second = StringHit("combo_2", 6f);
            second.Knockback = new Vector2(4f, 0f);
            hurtbox.TakeHit(second);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            AssertThat(enemy.Velocity.X > 0f).OverrideFailureMessage("The string's knockback lands on a stunned Catcher.").IsTrue();
            AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Idle);
        } finally {
            Teardown(host);
        }

        host = Host("CatcherGrabHost");
        try {
            PlayerController hero = SpawnHero(host, Origin + new Vector2(-90f, 0f));
            EnemyController enemy = CreateMob("cyber_guard", Origin);
            enemy.SetChaseTargetForTest(hero);
            enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(StringHit("combo_2", 6f));
            AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Telegraph);
            AssertThat(enemy.IsGrabbable).OverrideFailureMessage("A throw is always open on a Catcher.").IsTrue();
            enemy.BeginHeld();
            AssertThat(enemy.AbilityPhase).OverrideFailureMessage("The grab ends the counter.").IsEqual(EnemyAbilityPhase.Idle);
        } finally {
            Teardown(host);
        }
    }

    // === Anti-air ============================================================

    [TestCase]
    public async Task TheRisingStrikeMeetsAHeroDescendingIntoReachWithADiamond() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            Node host = Host("AntiAirFiresHost");
            StaticBody2D floor = CreateBlock(new Rect2(OriginX - 1500f, 0f, 3000f, 40f));
            try {
                EnemyController enemy = CreateSettledWard(host);
                PlayerController hero = SpawnHero(host, enemy.GlobalPosition + new Vector2(60f, -200f));
                PoseHero(hero, CharacterState.Airborne, 300f);
                int strikes = EnemyTraitTally.AntiAirStrikes;

                StepUntilChasing(enemy);
                enemy._PhysicsProcess(Step);

                AssertThat(enemy.ActiveAbility).IsEqual(enemy.AntiAirStrikeAbility);
                AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Telegraph);
                AssertThat(enemy.TelegraphGlyphNode.Shape).IsEqual(TelegraphGlyphShape.GuardCrush);
                AssertThat(enemy.IsFacingRight).IsTrue();
                AssertThat(enemy.TraitCooldownFramesRemaining).IsEqual(EnemyTraitRules.AntiAirCooldownFrames);
                AssertThat(EnemyTraitTally.AntiAirStrikes).IsEqual(strikes + 1);
                // An aerial swing is a jump-in too.
                AssertThat(EnemyTraitRules.HeroIsJumpingIn(true, hero.IsOnFloor(), hero.Velocity.Y)).IsTrue();
            } finally {
                Teardown(host);
                floor.Free();
            }
        });
    }

    [TestCase]
    public async Task ItNeverAnswersARollAMovementAbilityARiseAStandingHeroOrAHeroOutOfReach() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            Node host = Host("AntiAirRefusesHost");
            StaticBody2D floor = CreateBlock(new Rect2(OriginX - 1500f, 0f, 3000f, 40f));
            StaticBody2D ceiling = null;
            try {
                EnemyController enemy = CreateSettledWard(host);
                Vector2 mob = enemy.GlobalPosition;
                PlayerController hero = SpawnHero(host, mob + new Vector2(60f, -200f));
                PoseHero(hero, CharacterState.Idle, 300f);
                StepUntilChasing(enemy);

                void Refuses(string label, CharacterState state, float velocityY, Vector2 offset) {
                    PoseHero(hero, state, velocityY);
                    hero.GlobalPosition = mob + offset;
                    for (int frame = 0; frame < 3; frame++) enemy._PhysicsProcess(Step);
                    AssertThat(enemy.TraitCooldownFramesRemaining)
                        .OverrideFailureMessage($"The rising strike answered {label}.").IsEqual(0);
                }

                Refuses("a roll (the air dodge)", CharacterState.Rolling, 300f, new Vector2(60f, -200f));
                Refuses("a movement ability", CharacterState.UsingMovementAbility, 300f, new Vector2(60f, -200f));
                Refuses("a rise", CharacterState.Airborne, -300f, new Vector2(60f, -200f));
                Refuses("a hero on the mob's floor", CharacterState.Idle, 0f, new Vector2(60f, 0f));
                Refuses("a hero too far along", CharacterState.Airborne, 300f, new Vector2(200f, -200f));
                Refuses("a hero too high", CharacterState.Airborne, 300f, new Vector2(60f, -400f));
                Refuses("a hero about to land", CharacterState.Airborne, 300f, new Vector2(60f, -100f));

                // A solid ceiling between them shelters the hero.
                ceiling = CreateBlock(new Rect2(mob.X - 200f, mob.Y - 120f, 400f, 20f));
                Refuses("a hero over a ceiling", CharacterState.Airborne, 300f, new Vector2(60f, -200f));
                ceiling.Free();
                ceiling = null;

                // Positive control: the same mob answers the plain jump-in.
                PoseHero(hero, CharacterState.Airborne, 300f);
                enemy._PhysicsProcess(Step);
                AssertThat(enemy.ActiveAbility).IsEqual(enemy.AntiAirStrikeAbility);
            } finally {
                ceiling?.Free();
                Teardown(host);
                floor.Free();
            }
        });
    }

    [TestCase]
    public async Task AStrikeThatMeetsTheHeroLandsWithAShortRecoveryAndAMissCostsFortyFrames() {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node host = Host("AntiAirRecoveryHost");
        try {
            // The hero hangs inside the box; its own tick is off.
            EnemyController enemy = CreateMob("plasma_spear_ward", Origin);
            enemy.SetPhysicsProcess(false);
            enemy.SetFacingForTest(true);
            PlayerController hero = SpawnHero(host, Origin + new Vector2(40f, -120f));
            PoseHero(hero, CharacterState.Airborne, 0f);
            hero.SetPhysicsProcess(false);
            enemy.SetChaseTargetForTest(hero);
            int hp = hero.CurrentHP;

            enemy.BeginAttack(enemy.AntiAirStrikeAbility);
            for (int frame = 0; frame < EnemyTraitRules.AntiAirTelegraphFrames; frame++) enemy.TickAbilityExecutor(Step);
            AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Active);
            int connected = EnemyTraitTally.AntiAirStrikesConnected;
            for (int frame = 0; frame < 30 && enemy.AbilityPhase == EnemyAbilityPhase.Active; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                enemy.TickAbilityExecutor(Step);
            }
            AssertThat(hero.CurrentHP < hp).OverrideFailureMessage("The strike's box must reach a hero in it.").IsTrue();
            AssertThat(EnemyTraitTally.AntiAirStrikesConnected).IsEqual(connected + 1);
            AssertThat(RecoveryTicks(enemy)).IsEqual(EnemyTraitRules.AntiAirHitRecoveryFrames);

            // Nobody in the box: the whole 40, and the hero's floor space is safe.
            hero.GlobalPosition = Origin + new Vector2(40f, 0f);
            hp = hero.CurrentHP;
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            enemy.BeginAttack(enemy.AntiAirStrikeAbility);
            for (int frame = 0; frame < EnemyTraitRules.AntiAirTelegraphFrames; frame++) enemy.TickAbilityExecutor(Step);
            for (int frame = 0; frame < 30 && enemy.AbilityPhase == EnemyAbilityPhase.Active; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                enemy.TickAbilityExecutor(Step);
            }
            AssertThat(hero.CurrentHP).OverrideFailureMessage("A hero standing under the strike is not hit.").IsEqual(hp);
            AssertThat(RecoveryTicks(enemy)).IsEqual(EnemyTraitRules.AntiAirMissRecoveryFrames);
            enemy.SetPhysicsProcess(true);
        } finally {
            Teardown(host);
        }
    }

    // === Helpers =============================================================

    /// <summary>Executor ticks until the recovery ends; the tick that enters Idle counts.</summary>
    private static int RecoveryTicks(EnemyController enemy) {
        AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Recovery);
        int ticks = 0;
        while (enemy.AbilityPhase == EnemyAbilityPhase.Recovery && ticks < 200) {
            enemy.TickAbilityExecutor(Step);
            ticks++;
        }
        return ticks;
    }

    private static void PoseHero(PlayerController hero, CharacterState state, float velocityY) {
        hero.TransitionTo(state);
        hero.Velocity = new Vector2(0f, velocityY);
    }

    /// <summary>A ward standing on the floor (Patrol until a hero is in its aggro radius).</summary>
    private static EnemyController CreateSettledWard(Node host) {
        EnemyController enemy = CreateMob("plasma_spear_ward", Origin, aggroRadius: 540f);
        enemy.GetParent().RemoveChild(enemy);
        host.AddChild(enemy);
        enemy.GlobalPosition = Origin + new Vector2(0f, -40f);
        for (int frame = 0; frame < 30 && !enemy.IsOnFloor(); frame++) enemy._PhysicsProcess(Step);
        AssertThat(enemy.IsOnFloor()).IsTrue();
        return enemy;
    }

    private static void StepUntilChasing(EnemyController enemy) {
        for (int frame = 0; frame < 4 && enemy.CurrentState != EnemyState.Chase; frame++) enemy._PhysicsProcess(Step);
        AssertThat(enemy.CurrentState).IsEqual(EnemyState.Chase);
    }

    private static int Dealt(EnemyController enemy, HitPayload hit) {
        int before = enemy.CurrentHP;
        enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(hit);
        return before - enemy.CurrentHP;
    }

    private static HitPayload StringHit(string hitboxID, float damage) => new() {
        AttackerIndex = 0,
        TargetIndex = -1,
        AttackID = "test.trait.basic",
        HitboxID = hitboxID,
        AttackClass = AttackClass.Basic,
        Origin = HitOrigin.Basic,
        Delivery = HitDelivery.DirectHit,
        Damage = damage,
        HitstunDuration = 0.5f,
        HitOrigin = new Vector2(OriginX - 200f, -30f),
        AttackerFacingRight = true,
        ContactId = HitClassification.NextContactId()
    };

    private static HitPayload Hit(AttackClass attackClass, HitOrigin origin, float damage, HitDelivery delivery = HitDelivery.DirectHit) => new() {
        AttackerIndex = 0,
        TargetIndex = -1,
        AttackID = "test.trait.other",
        HitboxID = "other",
        AttackClass = attackClass,
        Origin = origin,
        Delivery = delivery,
        Damage = damage,
        HitOrigin = new Vector2(OriginX - 200f, -30f),
        AttackerFacingRight = true,
        ContactId = HitClassification.NextContactId()
    };

    private static EnemyController CreateMob(string enemyID, Vector2 position, float aggroRadius = 0f) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        data.AggroRadius = aggroRadius;
        data.Behavior = DefaultBehavior.StandGuard;
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        return enemy;
    }

    private static Node Host(string name) {
        var host = new Node { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }

    private static PlayerController SpawnHero(Node host, Vector2 position) {
        PlayerController hero = CharacterFactory.CreateCharacter(
            "joan", 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
        hero.Position = position;
        host.AddChild(hero);
        hero.GlobalPosition = position;
        return hero;
    }

    /// <summary>Frees the host and every mob parented to the runner root for the case.</summary>
    private static void Teardown(Node host) {
        InputManager.Instance?.ClearInputSource(0);
        var tree = (SceneTree)Engine.GetMainLoop();
        Godot.Collections.Array<Node> enemies = tree.GetNodesInGroup("Enemies");
        using (enemies.AsDisposable()) {
            foreach (Node node in enemies) {
                if (node is EnemyController { Name: var _ } enemy
                    && Mathf.Abs(enemy.GlobalPosition.X - OriginX) < 3000f) enemy.Free();
            }
        }
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }

    private static StaticBody2D CreateBlock(Rect2 rect) {
        var block = new StaticBody2D {
            Name = "TraitTestBlock",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = rect.GetCenter()
        };
        block.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(block);
        return block;
    }
}
