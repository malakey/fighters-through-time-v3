using System;
using System.Collections.Generic;
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
/// 2026-10-04 playtest-feel pass, workstream ENEMY
/// (<c>docs/PLAYTEST_FEEL_2026-10-04_PLAN.md</c> §2.2–2.3): the Story mob and boss
/// behaviour behind S1–S5, F5's victim half, the victim half of the kill weight,
/// F9, P3's AbilityOrigin mirroring and P4. The numbers themselves are pinned
/// engine-free in <c>StoryEnemyCombatRulesTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryEnemyCombatTests {
    private const float Step = 1f / 60f;

    // === S1: knockback decays while stunned ===

    [TestCase]
    public async Task TheFinisherCarriesAStunnedMobInsteadOfHavingItsKnockbackZeroed() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor(centerX: 0f, width: 4000f);
            EnemyController enemy = CreateEnemy("chrono_slasher");
            try {
                SettleOnFloor(enemy);
                float startX = enemy.GlobalPosition.X;
                enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(Finisher(damage: 1f));
                AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
                float launchSpeed = enemy.Velocity.X;
                AssertThat(launchSpeed > 0f).IsTrue();

                // Past the victim hitstop the knockback decays under drag rather
                // than vanishing on the first stunned frame.
                int guard = 0;
                while (enemy.HitstopFramesRemaining > 0 && guard++ < 30) enemy._PhysicsProcess(Step);
                enemy._PhysicsProcess(Step);
                AssertThat(enemy.Velocity.X > 0f && enemy.Velocity.X < launchSpeed)
                    .OverrideFailureMessage("Stunned knockback must decay, not zero: vx=" + enemy.Velocity.X)
                    .IsTrue();

                guard = 0;
                while (enemy.CurrentState == EnemyState.Stunned && guard++ < 240) enemy._PhysicsProcess(Step);
                float travelled = enemy.GlobalPosition.X - startX;
                AssertThat(travelled > 90f)
                    .OverrideFailureMessage("The 4.5x finisher must carry the mob away; travelled " + travelled)
                    .IsTrue();
            } finally {
                enemy.Free();
                floor.Free();
            }
        });
    }

    [TestCase]
    public async Task KnockbackNeverCarriesAGroundMobOffALedge() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor(centerX: 0f, width: 400f); // x in [-200, 200]
            EnemyController enemy = CreateEnemy("chrono_slasher");
            try {
                enemy.GlobalPosition = new Vector2(120f, 0f);
                SettleOnFloor(enemy);
                enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(Finisher(damage: 1f));
                for (int frame = 0; frame < 120; frame++) enemy._PhysicsProcess(Step);

                AssertThat(enemy.GlobalPosition.X < 200f)
                    .OverrideFailureMessage("The mob must stop at the edge; x=" + enemy.GlobalPosition.X)
                    .IsTrue();
                AssertThat(Mathf.Abs(enemy.GlobalPosition.Y) < 2f)
                    .OverrideFailureMessage("The mob must still be standing on the floor; y=" + enemy.GlobalPosition.Y)
                    .IsTrue();
            } finally {
                enemy.Free();
                floor.Free();
            }
        });
    }

    // === S2: confirm proration ===

    [TestCase]
    public void ASpecialOnHitstunIsProratedAndTheFourthChainHitDecaysWhileUltimatesAndTicksAreExempt() {
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            int hp = enemy.CurrentHP;
            int Dealt(HitPayload hit) {
                hurtbox.TakeHit(hit);
                int dealt = hp - enemy.CurrentHP;
                hp = enemy.CurrentHP;
                return dealt;
            }

            int Expected(int chainHit, bool specialConfirm) => StoryEnemyCombatRules.ScaleIntakeDamage(
                4, StoryEnemyCombatRules.ConfirmProrationScale(chainHit, specialConfirm));

            // Chain hit 1: Special A opens the chain in full and stuns.
            AssertThat(Dealt(Hit(AttackClass.Special, "special_test", 4f, hitstun: 0.5f, attackID: "test.special_a")))
                .IsEqual(4);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            AssertThat(enemy.ConfirmChainHits).IsEqual(1);
            // Chain hit 2: Special A's own follow-up hit lands on the hitstun its
            // first hit applied — that is not a confirm.
            AssertThat(Dealt(Hit(AttackClass.Special, "special_test", 4f, hitstun: 0.5f, attackID: "test.special_a")))
                .OverrideFailureMessage("A multi-hit Special's own follow-up must not be prorated.")
                .IsEqual(4);
            // Chain hit 3: Special B fired into that hitstun is a confirm: x0.6.
            AssertThat(Dealt(Hit(AttackClass.Special, "special_test", 4f, hitstun: 0.2f, attackID: "test.special_b")))
                .IsEqual(Expected(3, specialConfirm: true));
            // Chain hit 4: every later hit of the confirm keeps x0.6, and the decay starts.
            AssertThat(Dealt(Hit(AttackClass.Special, "special_test", 4f, hitstun: 0.2f, attackID: "test.special_b")))
                .IsEqual(Expected(4, specialConfirm: true));
            // Chain hit 5: a non-Special only decays.
            AssertThat(Dealt(Hit(AttackClass.Basic, "turret_shot", 4f, hitstun: 0f)))
                .IsEqual(Expected(5, specialConfirm: false));
            AssertThat(enemy.ConfirmChainHits).IsEqual(5);

            // Ultimates and ticks are exempt and never advance the chain.
            AssertThat(Dealt(Hit(AttackClass.Ultimate, "ult_test", 4f, hitstun: 0f, origin: HitOrigin.Ultimate)))
                .IsEqual(4);
            HitPayload tick = Hit(AttackClass.Special, "zone_tick", 4f, hitstun: 0f);
            tick.Delivery = HitDelivery.Tick;
            AssertThat(Dealt(tick)).IsEqual(4);
            AssertThat(enemy.ConfirmChainHits).IsEqual(5);

            // Natural stun expiry ends the chain.
            int guard = 0;
            while (enemy.CurrentState == EnemyState.Stunned && guard++ < 240) enemy._PhysicsProcess(Step);
            AssertThat(enemy.CurrentState).IsNotEqual(EnemyState.Stunned);
            AssertThat(enemy.ConfirmChainHits).IsEqual(0);
        } finally {
            enemy.Free();
        }
    }

    // === S3: standard poise ===

    [TestCase]
    public void AStandardTakesAFullStringButAStringPlusSpecialTripsItsShortArmoredRecovery() {
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            for (int step = 0; step < BasicComboRules.ComboHits; step++) {
                hurtbox.TakeHit(StringHit(step, damage: 1f));
                AssertThat(enemy.IsArmoredRecovery)
                    .OverrideFailureMessage("One full basic string must complete; tripped at hit " + (step + 1))
                    .IsFalse();
                AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
                for (int frame = 0; frame < 6; frame++) enemy._PhysicsProcess(Step);
            }
            AssertThat(enemy.StaggerBudgetSeconds > 1.5f).IsTrue();

            // The Special confirm crosses the budget: short armored answer.
            hurtbox.TakeHit(Hit(AttackClass.Special, "special_test", 1f, hitstun: 12 / 60f));
            AssertThat(enemy.IsArmoredRecovery).IsTrue();
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Attacking);
            AssertThat(enemy.ActiveAbility).IsNotNull();
            AssertThat(enemy.StaggerBudgetSeconds).IsEqualApprox(0f, 0.0001f);
            // The trip does not end the stun chain: the armored window is the
            // same exchange (S2 keeps prorating through it).
            AssertThat(enemy.ConfirmChainHits).IsEqual(BasicComboRules.ComboHits + 1);

            int armorFrames = Mathf.RoundToInt(StoryEnemyCombatRules.StandardArmoredRecoverySeconds * 60f);
            for (int frame = 0; frame < armorFrames - 6; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.IsArmoredRecovery).IsTrue();
            for (int frame = 0; frame < 20; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.IsArmoredRecovery)
                .OverrideFailureMessage("A standard's armored window is the short one.")
                .IsFalse();
            AssertThat(enemy.ConfirmChainHits)
                .OverrideFailureMessage("The chain ends with the armored window.")
                .IsEqual(0);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void AStringPlusAMultiHitSpecialStaysProratedThroughThePoiseTripAndItsArmoredWindow() {
        const int specialDamage = 8;
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            int hp = enemy.CurrentHP;
            int Dealt(HitPayload hit) {
                hurtbox.TakeHit(hit);
                int dealt = hp - enemy.CurrentHP;
                hp = enemy.CurrentHP;
                return dealt;
            }
            int Expected(int chainHit) => StoryEnemyCombatRules.ScaleIntakeDamage(
                specialDamage, StoryEnemyCombatRules.ConfirmProrationScale(chainHit, specialOnHitstun: true));
            HitPayload Special(string attackID) =>
                Hit(AttackClass.Special, "special_test", specialDamage, hitstun: 12 / 60f, attackID: attackID);

            // A full string completes inside the standard's poise.
            for (int step = 0; step < BasicComboRules.ComboHits; step++) {
                Dealt(StringHit(step, damage: 1f));
                for (int frame = 0; frame < 6; frame++) enemy._PhysicsProcess(Step);
            }
            AssertThat(enemy.IsArmoredRecovery).IsFalse();
            AssertThat(enemy.ConfirmChainHits).IsEqual(BasicComboRules.ComboHits);

            // A three-hit Special (one AttackID) confirms off the string. Its
            // first hit trips the poise budget; every one of its hits is prorated.
            AssertThat(Dealt(Special("test.special_a"))).IsEqual(Expected(4));
            AssertThat(enemy.IsArmoredRecovery)
                .OverrideFailureMessage("String + Special must trip the standard's poise.")
                .IsTrue();
            AssertThat(Dealt(Special("test.special_a")))
                .OverrideFailureMessage("The confirm's second hit lands on the armored window and stays prorated.")
                .IsEqual(Expected(5));
            AssertThat(Dealt(Special("test.special_a"))).IsEqual(Expected(6));
            AssertThat(Expected(6) < specialDamage).IsTrue();

            // A second Special fired during the armored window is a confirm too.
            for (int frame = 0; frame < 6; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.IsArmoredRecovery).IsTrue();
            AssertThat(Dealt(Special("test.special_b")))
                .OverrideFailureMessage("A second Special during the armored window must be prorated.")
                .IsEqual(Expected(7));
            AssertThat(enemy.ConfirmChainHits).IsEqual(BasicComboRules.ComboHits + 4);

            // The chain ends with the window; the next Special opens a fresh one in full.
            int guard = 0;
            while (enemy.IsArmoredRecovery && guard++ < 120) enemy._PhysicsProcess(Step);
            AssertThat(enemy.IsArmoredRecovery).IsFalse();
            AssertThat(enemy.ConfirmChainHits).IsEqual(0);
            AssertThat(Dealt(Special("test.special_c"))).IsEqual(specialDamage);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void AFrozenOrDrillInvulnerableMobBanksNoPoiseAndNeverCommitsAnArmoredAttack() {
        EnemyController frozen = CreateEnemy("chrono_slasher");
        EnemyController frozenElite = CreateEnemy("steam_automaton");
        EnemyController drill = CreateEnemy("chrono_slasher");
        try {
            // The Level 0 guard-lesson local: frozen (and drill-invulnerable)
            // while the player is free to swing at it.
            frozen.SetStoryRewindFrozen(true);
            frozenElite.SetStoryRewindFrozen(true);
            drill.DrillInvulnerable = true;
            foreach (EnemyController enemy in new[] { frozen, frozenElite, drill }) {
                var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
                // Two full strings and six Specials: far past either tier's budget.
                for (int hit = 0; hit < 6; hit++) {
                    hurtbox.TakeHit(StringHit(hit % BasicComboRules.ComboHits, damage: 1f));
                    hurtbox.TakeHit(Hit(AttackClass.Special, "special_test", 1f, hitstun: 0.5f,
                        attackID: $"test.frozen_{hit}"));
                }
                AssertThat(enemy.StaggerBudgetSeconds)
                    .OverrideFailureMessage("A frozen or drill-invulnerable body must bank no poise.")
                    .IsEqualApprox(0f, 0.0001f);
                AssertThat(enemy.IsArmoredRecovery).IsFalse();
                AssertThat(enemy.ActiveAbility)
                    .OverrideFailureMessage("No armored counterattack may be committed on a frozen body.")
                    .IsNull();
            }

            // Released, the same mob banks poise again.
            frozen.SetStoryRewindFrozen(false);
            frozen.GetNode<Hurtbox>("Hurtbox").TakeHit(StringHit(0, damage: 1f));
            AssertThat(frozen.StaggerBudgetSeconds > 0f).IsTrue();
        } finally {
            frozen.Free();
            frozenElite.Free();
            drill.Free();
        }
    }

    [TestCase]
    public void BackToBackStringsEachCompleteOnAStandardOnceItsGetupHasPassed() {
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            for (int round = 0; round < 2; round++) {
                for (int step = 0; step < BasicComboRules.ComboHits; step++) {
                    hurtbox.TakeHit(StringHit(step, damage: 1f));
                    AssertThat(enemy.IsArmoredRecovery)
                        .OverrideFailureMessage($"String {round + 1} tripped poise at hit {step + 1}.")
                        .IsFalse();
                    AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
                    for (int frame = 0; frame < 6; frame++) enemy._PhysicsProcess(Step);
                }
                // The string's stun expires naturally, the V7.4 getup armor runs,
                // and the standard's faster drain clears the credit meanwhile.
                int guard = 0;
                while (enemy.CurrentState == EnemyState.Stunned && guard++ < 240) enemy._PhysicsProcess(Step);
                guard = 0;
                while (enemy.IsGetupArmored && guard++ < 120) enemy._PhysicsProcess(Step);
                AssertThat(enemy.StaggerBudgetSeconds).IsEqualApprox(0f, 0.0001f);
            }
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void AThrowKnockdownSpendsNoPoiseBudgetWhileAnOrdinaryStunDoes() {
        EnemyController enemy = CreateEnemy("chrono_slasher");
        try {
            enemy.LaunchThrown(Vector2.Zero, bowlingDamage: 0);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            AssertThat(enemy.StaggerBudgetSeconds).IsEqualApprox(0f, 0.0001f);
            enemy.ApplyStun(0.5f);
            AssertThat(enemy.StaggerBudgetSeconds).IsEqualApprox(0.5f, 0.0001f);
        } finally {
            enemy.Free();
        }
    }

    // === S4: basics pacing ===

    [TestCase]
    public void TheBasicSetHitsStandardsHarderButLeavesElitesAndOtherSourcesAlone() {
        EnemyController standard = CreateEnemy("chrono_slasher");
        EnemyController elite = CreateEnemy("steam_automaton");
        try {
            int expected = StoryEnemyCombatRules.ScaleIntakeDamage(8, StoryEnemyCombatRules.StandardBasicDamageScale);
            AssertThat(expected > 8).IsTrue();

            int before = standard.CurrentHP;
            standard.GetNode<Hurtbox>("Hurtbox").TakeHit(StringHit(0, damage: 8f));
            AssertThat(before - standard.CurrentHP).IsEqual(expected);

            before = standard.CurrentHP;
            standard.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(AttackClass.Basic, "turret_shot", 8f, hitstun: 0f));
            AssertThat(before - standard.CurrentHP)
                .OverrideFailureMessage("A non-string Basic source is not a basic.")
                .IsEqual(8);

            before = elite.CurrentHP;
            elite.GetNode<Hurtbox>("Hurtbox").TakeHit(StringHit(0, damage: 8f));
            AssertThat(before - elite.CurrentHP)
                .OverrideFailureMessage("Basics pacing is a Standard-tier rule.")
                .IsEqual(8);
        } finally {
            standard.Free();
            elite.Free();
        }
    }

    // === S5: hit-indexed stun floor ===

    [TestCase]
    public void AResistantMobIsHeldThroughTheGapAfterStringHitTwo() {
        EnemyController elite = CreateEnemy("steam_automaton"); // StunResistance 0.5
        try {
            // Hit 2's 40-frame hitstun halves to 20 at 0.5 resistance; the old
            // shared 24-frame floor released the mob 14+ frames before a
            // buffered finisher connects. The S5 floor holds it 44.
            elite.GetNode<Hurtbox>("Hurtbox").TakeHit(StringHit(1, damage: 1f));
            int hitstop = elite.HitstopFramesRemaining;
            int floor = StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_2");
            for (int frame = 0; frame < hitstop + floor - 2; frame++) elite._PhysicsProcess(Step);
            AssertThat(elite.CurrentState)
                .OverrideFailureMessage("The hit-indexed floor must outlast the hit 2 -> 3 gap.")
                .IsEqual(EnemyState.Stunned);
            for (int frame = 0; frame < 6; frame++) elite._PhysicsProcess(Step);
            AssertThat(elite.CurrentState).IsNotEqual(EnemyState.Stunned);
        } finally {
            elite.Free();
        }
    }

    [TestCase]
    public void AResistantElitesBudgetKeepsThePreS5ChargeSoBackToBackStringsComplete() {
        EnemyController elite = CreateEnemy("steam_automaton"); // StunResistance 0.5
        try {
            var hurtbox = elite.GetNode<Hurtbox>("Hurtbox");
            float preS5Charge = BasicComboRules.EnemyBasicStunFloorFrames / 60f;
            for (int round = 0; round < 2; round++) {
                for (int step = 0; step < BasicComboRules.ComboHits; step++) {
                    hurtbox.TakeHit(StringHit(step, damage: 1f));
                    AssertThat(elite.IsArmoredRecovery)
                        .OverrideFailureMessage($"String {round + 1} tripped the elite at hit {step + 1}.")
                        .IsFalse();
                    AssertThat(elite.CurrentState).IsEqual(EnemyState.Stunned);
                    for (int frame = 0; frame < 6; frame++) elite._PhysicsProcess(Step);
                }
                if (round == 0) {
                    // Each resisted hit (15/20/12 frames) charges the pre-S5
                    // 24-frame floor, while the S5 floor still holds the stun 34/44.
                    AssertThat(elite.StaggerBudgetSeconds)
                        .IsEqualApprox(BasicComboRules.ComboHits * preS5Charge, 0.001f);
                }
                int guard = 0;
                while (elite.CurrentState == EnemyState.Stunned && guard++ < 240) elite._PhysicsProcess(Step);
                guard = 0;
                while (elite.IsGetupArmored && guard++ < 120) elite._PhysicsProcess(Step);
            }
        } finally {
            elite.Free();
        }
    }

    // === F5 (victim half) ===

    [TestCase]
    public void TicksConstructsAndExemptPayloadsNeverFreezeAMobOrABoss() {
        EnemyController enemy = CreateEnemy("chrono_slasher");
        BossController boss = CreateBoss();
        try {
            var enemyHurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            var bossHurtbox = boss.GetNode<Hurtbox>("Hurtbox");
            foreach (HitPayload exempt in ExemptHits()) {
                enemyHurtbox.TakeHit(exempt);
                bossHurtbox.TakeHit(exempt);
                AssertThat(enemy.HitstopFramesRemaining).IsEqual(0);
                AssertThat(boss.HitstopFramesRemaining).IsEqual(0);
            }

            HitPayload direct = Hit(AttackClass.Basic, "turret_shot", 4f, hitstun: 0f);
            enemyHurtbox.TakeHit(direct);
            bossHurtbox.TakeHit(direct);
            AssertThat(enemy.HitstopFramesRemaining > 0).IsTrue();
            AssertThat(boss.HitstopFramesRemaining > 0).IsTrue();
        } finally {
            enemy.Free();
            boss.Free();
        }
    }

    // === Kill weight (victim half) ===

    [TestCase]
    public void ALethalDirectHitHoldsAKillFreezeBeforeTheDeathAnimation() {
        int kills = 0;
        void OnKilled(EnemyKilledPayload payload) => kills++;
        EventBus.Instance.OnEnemyKilled += OnKilled;
        EnemyController enemy = CreateEnemy("chrono_slasher");
        EnemyController ticked = CreateEnemy("chrono_slasher");
        try {
            enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(AttackClass.Special, "special_test", 100f, hitstun: 0.2f));
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Dead);
            AssertThat(kills)
                .OverrideFailureMessage("The kill's gameplay consequences never wait for the freeze.")
                .IsEqual(1);
            AssertThat(enemy.IsInKillFreeze).IsTrue();
            int freeze = enemy.HitstopFramesRemaining;
            AssertThat(freeze).IsEqual(StoryEnemyCombatRules.VictimHitstopFrames(enemy.ScaledMaxHP, launches: false));
            for (int frame = 0; frame < freeze - 1; frame++) enemy._PhysicsProcess(Step);
            AssertThat(enemy.IsInKillFreeze).IsTrue();
            enemy._PhysicsProcess(Step);
            AssertThat(enemy.IsInKillFreeze).IsFalse();

            // A lethal tick has no freeze to hold.
            HitPayload tick = Hit(AttackClass.Special, "zone_tick", 100f, hitstun: 0f);
            tick.Delivery = HitDelivery.Tick;
            ticked.GetNode<Hurtbox>("Hurtbox").TakeHit(tick);
            AssertThat(ticked.CurrentState).IsEqual(EnemyState.Dead);
            AssertThat(ticked.IsInKillFreeze).IsFalse();
        } finally {
            EventBus.Instance.OnEnemyKilled -= OnKilled;
            enemy.Free();
            ticked.Free();
        }
    }

    // === F9: dealt-hit feedback ===

    [TestCase]
    public void PlayerDealtHitsFloatADamageNumberOverTheMobAndRespectTheSetting() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var parent = new Node2D { Name = "DealtHitFeedbackParent" };
        tree.Root.AddChild(parent);
        GlobalSaveData global = SaveManager.Instance?.GlobalData;
        bool numbersBefore = global?.DamageNumbersVisible ?? true;
        EnemyController enemy = CreateEnemy("chrono_slasher", parent);
        try {
            if (global != null) global.DamageNumbersVisible = true;
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            hurtbox.TakeHit(Hit(AttackClass.Basic, "turret_shot", 4f, hitstun: 0f));
            AssertThat(CountDamageNumbers(parent)).IsEqual(1);

            // Not player-dealt: no number.
            HitPayload foreign = Hit(AttackClass.Basic, "turret_shot", 4f, hitstun: 0f);
            foreign.AttackerIndex = -1;
            hurtbox.TakeHit(foreign);
            AssertThat(CountDamageNumbers(parent)).IsEqual(1);

            // The existing damage-number setting turns them off.
            if (global != null) {
                global.DamageNumbersVisible = false;
                hurtbox.TakeHit(Hit(AttackClass.Basic, "turret_shot", 4f, hitstun: 0f));
                AssertThat(CountDamageNumbers(parent)).IsEqual(1);
            }
        } finally {
            if (global != null) global.DamageNumbersVisible = numbersBefore;
            ReturnDamageNumbers(parent);
            enemy.Free();
            parent.Free();
        }
    }

    [TestCase]
    public void ADealtAbilityHitShakesByItsAuthoredValueWhileABasicKeepsTheDamageCurve() {
        CameraShake shake = CameraShake.Instance;
        AssertThat(shake).IsNotNull();
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        System.Reflection.FieldInfo intensityField = typeof(CameraShake).GetField("_shakeIntensity", flags);
        System.Reflection.FieldInfo durationField = typeof(CameraShake).GetField("_shakeDuration", flags);
        System.Reflection.FieldInfo timerField = typeof(CameraShake).GetField("_shakeTimer", flags);
        System.Reflection.FieldInfo scaleField = typeof(CameraShake).GetField("_intensityScale", flags);
        AssertThat(intensityField != null && durationField != null && timerField != null && scaleField != null)
            .IsTrue();
        object savedIntensity = intensityField.GetValue(shake);
        object savedDuration = durationField.GetValue(shake);
        object savedTimer = timerField.GetValue(shake);
        object savedScale = scaleField.GetValue(shake);
        void ResetShake() {
            intensityField.SetValue(shake, 0f);
            durationField.SetValue(shake, 0f);
            timerField.SetValue(shake, 0f);
        }

        EnemyController enemy = CreateEnemy("chrono_slasher");
        BossController boss = CreateBoss();
        try {
            shake.SetIntensityScale(1f);
            foreach (Hurtbox hurtbox in new[] { enemy.GetNode<Hurtbox>("Hurtbox"), boss.GetNode<Hurtbox>("Hurtbox") }) {
                // An Ultimate payload carries its AbilityData shake (0.6 / 0.3 s).
                HitPayload ultimate = Hit(AttackClass.Ultimate, "ult_test", 4f, hitstun: 0f, origin: HitOrigin.Ultimate);
                ultimate.ScreenShakeIntensity = 0.6f;
                ultimate.ScreenShakeDuration = 0.3f;
                ResetShake();
                AssertThat(hurtbox.TakeHit(ultimate) > 0f).IsTrue();
                AssertThat((float)intensityField.GetValue(shake))
                    .OverrideFailureMessage("An ability hit must shake by its authored intensity.")
                    .IsEqualApprox(0.6f * StoryEnemyCombatRules.AuthoredShakePixelsPerIntensity, 0.0001f);
                AssertThat((float)durationField.GetValue(shake)).IsEqualApprox(0.3f, 0.0001f);

                // A basic carries only the hitbox default: the damage curve applies.
                HitPayload basic = StringHit(0, damage: 4f);
                basic.ScreenShakeIntensity = 0.2f;
                basic.ScreenShakeDuration = 0.1f;
                ResetShake();
                int dealt = Mathf.RoundToInt(hurtbox.TakeHit(basic));
                AssertThat(dealt > 0).IsTrue();
                AssertThat((float)intensityField.GetValue(shake))
                    .IsEqualApprox(StoryEnemyCombatRules.DealtHitShakeIntensity(dealt, launches: false), 0.0001f);
                AssertThat((float)durationField.GetValue(shake))
                    .IsEqualApprox(StoryEnemyCombatRules.DealtHitShakeDuration(false), 0.0001f);
            }
        } finally {
            intensityField.SetValue(shake, savedIntensity);
            durationField.SetValue(shake, savedDuration);
            timerField.SetValue(shake, savedTimer);
            scaleField.SetValue(shake, savedScale);
            enemy.Free();
            boss.Free();
        }
    }

    // === P3: AbilityOrigin mirroring ===

    [TestCase]
    public void TheAbilityOriginMirrorsWithFacingOnElitesAndBosses() {
        EnemyController elite = CreateEnemy("steam_automaton");
        BossController boss = CreateBoss();
        try {
            Vector2 authored = elite.AbilityOriginLocalPosition ?? Vector2.Zero;
            AssertThat(authored.X > 0f).IsTrue();
            elite.SetFacingForTest(false);
            AssertThat(elite.AbilityOriginLocalPosition).IsEqual(new Vector2(-authored.X, authored.Y));
            elite.SetFacingForTest(true);
            AssertThat(elite.AbilityOriginLocalPosition).IsEqual(authored);

            Vector2 bossAuthored = boss.AbilityOriginLocalPosition ?? Vector2.Zero;
            AssertThat(bossAuthored.X > 0f).IsTrue();
            boss.SetFacingForTest(false);
            AssertThat(boss.AbilityOriginLocalPosition).IsEqual(new Vector2(-bossAuthored.X, bossAuthored.Y));
            boss.SetFacingForTest(true);
            AssertThat(boss.AbilityOriginLocalPosition).IsEqual(bossAuthored);
        } finally {
            elite.Free();
            boss.Free();
        }
    }

    // === P4: chase persistence, ledges, walls, overhead targets ===

    [TestCase]
    public async Task AnAggroedMobKeepsChasingInsideItsRoomOrTheWidenedRadius() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor(centerX: 0f, width: 8000f);
            PlayerController player = CreateTargetPlayer(new Vector2(300f, 0f));
            EnemyController enemy = CreateEnemy("chrono_slasher");
            try {
                enemy.Data.AttackCooldown = 999f;
                float deAggro = enemy.Data.DeAggroRadius;
                float leash = deAggro * StoryEnemyCombatRules.ChaseLeashDeAggroFactor;
                AssertThat(enemy.HasChaseLeashRoom).IsFalse();

                enemy._PhysicsProcess(Step);
                AssertThat(enemy.CurrentState).IsEqual(EnemyState.Chase);

                // No room: past DeAggroRadius but inside the widened leash it keeps on.
                player.GlobalPosition = new Vector2(enemy.GlobalPosition.X + (deAggro + leash) * 0.5f, 0f);
                enemy._PhysicsProcess(Step);
                AssertThat(enemy.CurrentState).IsEqual(EnemyState.Chase);
                // Beyond the leash it lets go.
                player.GlobalPosition = new Vector2(enemy.GlobalPosition.X + leash + 200f, 0f);
                enemy._PhysicsProcess(Step);
                AssertThat(enemy.CurrentState).IsEqual(EnemyState.Returning);

                // In a room, distance alone never ends the chase while the target is inside it.
                enemy.SetChaseLeashRoom(-500f, 3500f);
                player.GlobalPosition = new Vector2(enemy.GlobalPosition.X + 300f, 0f);
                enemy._PhysicsProcess(Step);
                AssertThat(enemy.CurrentState).IsEqual(EnemyState.Chase);
                player.GlobalPosition = new Vector2(3400f, 0f);
                enemy._PhysicsProcess(Step);
                AssertThat(enemy.CurrentState)
                    .OverrideFailureMessage("A target still in the mob's room must keep it chasing.")
                    .IsEqual(EnemyState.Chase);
                player.GlobalPosition = new Vector2(3700f, 0f);
                enemy._PhysicsProcess(Step);
                AssertThat(enemy.CurrentState).IsEqual(EnemyState.Returning);
            } finally {
                enemy.Free();
                player.Free();
                floor.Free();
            }
        });
    }

    [TestCase]
    public async Task AChasingGroundMobStopsAtALedgeInsteadOfWalkingOff() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor(centerX: 0f, width: 600f); // x in [-300, 300]
            PlayerController player = CreateTargetPlayer(new Vector2(450f, 0f));
            EnemyController enemy = CreateEnemy("chrono_slasher");
            try {
                enemy.Data.AttackCooldown = 999f;
                for (int frame = 0; frame < 240; frame++) enemy._PhysicsProcess(Step);
                AssertThat(enemy.CurrentState).IsEqual(EnemyState.Chase);
                AssertThat(enemy.IsOnFloor())
                    .OverrideFailureMessage("The mob walked off the ledge; y=" + enemy.GlobalPosition.Y)
                    .IsTrue();
                AssertThat(enemy.GlobalPosition.X < 300f && enemy.GlobalPosition.X > 200f).IsTrue();
                AssertThat(enemy.Velocity.X).IsEqualApprox(0f, 0.001f);
                AssertThat(enemy.IsFacingRight).IsTrue();
            } finally {
                enemy.Free();
                player.Free();
                floor.Free();
            }
        });
    }

    [TestCase]
    public async Task TheLedgeAndWallProbesNeverQueryTheSpaceInsideAPhysicsCallbackFlush() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor(centerX: 0f, width: 400f); // x in [-200, 200]
            EnemyController enemy = CreateEnemy("chrono_slasher");
            try {
                enemy.GlobalPosition = new Vector2(180f, 0f);
                SettleOnFloor(enemy);
                AssertThat(enemy.IsChaseStepBlockedForTest(1f))
                    .OverrideFailureMessage("Outside a flush the ledge probe must see the drop.")
                    .IsTrue();
                using (PhysicsCallbackGuard.Enter()) {
                    // The direct space state is off limits mid-flush: both probes
                    // answer their safe fallback (floor ahead, no wall) unqueried.
                    AssertThat(enemy.IsChaseStepBlockedForTest(1f))
                        .OverrideFailureMessage("Inside a flush the probes must not query the space.")
                        .IsFalse();
                }
                AssertThat(PhysicsCallbackGuard.IsInPhysicsCallback).IsFalse();
                AssertThat(enemy.IsChaseStepBlockedForTest(1f)).IsTrue();
            } finally {
                enemy.Free();
                floor.Free();
            }
        });
    }

    [TestCase]
    public async Task AChasingGroundMobStopsAtAWallInsteadOfPushingIntoIt() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor(centerX: 0f, width: 4000f);
            StaticBody2D wall = CreateWall(centerX: 200f);
            PlayerController player = CreateTargetPlayer(new Vector2(400f, 0f));
            EnemyController enemy = CreateEnemy("chrono_slasher");
            try {
                enemy.Data.AttackCooldown = 999f;
                for (int frame = 0; frame < 180; frame++) enemy._PhysicsProcess(Step);
                AssertThat(enemy.CurrentState).IsEqual(EnemyState.Chase);
                AssertThat(enemy.GlobalPosition.X < 180f).IsTrue();
                AssertThat(enemy.Velocity.X)
                    .OverrideFailureMessage("A walled-off chase must stop pushing.")
                    .IsEqualApprox(0f, 0.001f);
            } finally {
                enemy.Free();
                player.Free();
                wall.Free();
                floor.Free();
            }
        });
    }

    [TestCase]
    public async Task AGroundMobHoldsStillAndKeepsItsFacingUnderAnOverheadTarget() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            StaticBody2D floor = CreateFloor(centerX: 0f, width: 4000f);
            PlayerController player = CreateTargetPlayer(new Vector2(300f, 0f));
            EnemyController enemy = CreateEnemy("chrono_slasher");
            try {
                enemy.Data.AttackCooldown = 999f;
                enemy._PhysicsProcess(Step);
                AssertThat(enemy.CurrentState).IsEqual(EnemyState.Chase);
                AssertThat(enemy.IsFacingRight).IsTrue();

                float baseX = enemy.GlobalPosition.X;
                for (int frame = 0; frame < 40; frame++) {
                    // A player bobbing either side of the mob's head.
                    float side = frame % 2 == 0 ? 10f : -10f;
                    player.GlobalPosition = new Vector2(enemy.GlobalPosition.X + side, -250f);
                    enemy._PhysicsProcess(Step);
                    AssertThat(enemy.IsFacingRight)
                        .OverrideFailureMessage("The mob must not flip under an overhead target.")
                        .IsTrue();
                }
                AssertThat(enemy.Velocity.X).IsEqualApprox(0f, 0.001f);
                AssertThat(Mathf.Abs(enemy.GlobalPosition.X - baseX) < 40f).IsTrue();

                // A target beside the mob at its own height is not overhead:
                // the mob still turns to face it. (A sliver of attack range keeps
                // the attack commit, which faces on its own, out of the way.)
                enemy.Data.AttackRange = 0.05f;
                player.GlobalPosition = new Vector2(enemy.GlobalPosition.X - 10f, enemy.GlobalPosition.Y);
                enemy._PhysicsProcess(Step);
                AssertThat(enemy.IsFacingRight)
                    .OverrideFailureMessage("A level target behind the mob must turn it around.")
                    .IsFalse();
            } finally {
                enemy.Free();
                player.Free();
                floor.Free();
            }
        });
    }

    // === Helpers ===

    private static HitPayload Hit(
        AttackClass attackClass, string hitboxID, float damage, float hitstun,
        HitOrigin origin = HitOrigin.Basic, string attackID = "test.hit") => new() {
            AttackerIndex = 0,
            AttackID = attackID,
            HitboxID = hitboxID,
            AttackClass = attackClass,
            Origin = attackClass == AttackClass.Special && origin == HitOrigin.Basic ? HitOrigin.Special : origin,
            Delivery = HitDelivery.DirectHit,
            Damage = damage,
            HitstunDuration = hitstun,
            // Behind a right-facing mob, so no frontal reduction can apply.
            HitOrigin = new Vector2(-5000f, 0f),
            AttackerFacingRight = true
        };

    private static HitPayload StringHit(int step, float damage) => new() {
        AttackerIndex = 0,
        AttackID = "einstein.basic",
        HitboxID = $"combo_{step + 1}",
        AttackClass = AttackClass.Basic,
        Origin = HitOrigin.Basic,
        Delivery = HitDelivery.DirectHit,
        Damage = damage,
        HitstunDuration = BasicComboRules.HitstunFrames[step] / 60f,
        Launches = BasicComboRules.StringHitLaunches[step],
        HitOrigin = new Vector2(-5000f, 0f),
        AttackerFacingRight = true
    };

    /// <summary>Joan's authored finisher shape: 3.5 base x 4.5 forward, -2 x 4.5 up.</summary>
    private static HitPayload Finisher(float damage) {
        HitPayload hit = StringHit(2, damage);
        hit.Knockback = new Vector2(3.5f * BasicComboRules.KnockbackMultipliers[2], -2f * BasicComboRules.KnockbackMultipliers[2]);
        return hit;
    }

    private static IEnumerable<HitPayload> ExemptHits() {
        HitPayload tick = Hit(AttackClass.Special, "zone_tick", 2f, hitstun: 0f);
        tick.Delivery = HitDelivery.Tick;
        yield return tick;
        HitPayload construct = Hit(AttackClass.Basic, "coil_arc", 2f, hitstun: 0f);
        construct.Delivery = HitDelivery.Construct;
        yield return construct;
        HitPayload exempt = Hit(AttackClass.Basic, "turret_shot", 2f, hitstun: 0f);
        exempt.ExemptFromHitstop = true;
        yield return exempt;
    }

    private static void SettleOnFloor(EnemyController enemy) {
        enemy.Data.Behavior = DefaultBehavior.StandGuard;
        for (int frame = 0; frame < 8 && !enemy.IsOnFloor(); frame++) enemy._PhysicsProcess(Step);
        AssertThat(enemy.IsOnFloor()).IsTrue();
    }

    private static int CountDamageNumbers(Node parent) {
        int count = 0;
        Godot.Collections.Array<Node> children = parent.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is FTT.UI.FloatingDamageNumber && child.IsInsideTree()) count++;
        }
        return count;
    }

    private static void ReturnDamageNumbers(Node parent) {
        var numbers = new List<FTT.UI.FloatingDamageNumber>();
        Godot.Collections.Array<Node> children = parent.GetChildren();
        using (children.AsDisposable()) {
            foreach (Node child in children) {
                if (child is FTT.UI.FloatingDamageNumber number) numbers.Add(number);
            }
        }
        foreach (FTT.UI.FloatingDamageNumber number in numbers) number.ReturnToPool();
    }

    private static EnemyController CreateEnemy(string enemyID, Node parent = null) {
        EnemyData canonical = FTT.Core.AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        (parent ?? ((SceneTree)Engine.GetMainLoop()).Root).AddChild(enemy);
        enemy.OnSpawn();
        return enemy;
    }

    private static BossController CreateBoss() {
        var data = new BossData {
            BossID = "test_boss",
            DisplayNameKey = "boss_borgia_inquisitor_name",
            MaxHP = 500,
            MeleeRangeThreshold = 3f,
            RangedRangeThreshold = 8f,
            RestCooldown = 1f,
            AttackPattern = BossAttackPattern.DistanceBased,
            PhaseThresholds = Array.Empty<float>(),
            BossAbilities = Array.Empty<EnemyAbilityData>()
        };
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/enemies/Boss.tscn");
        var boss = scene.Instantiate<BossController>();
        boss.SelectionSeed = 2026;
        boss.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(boss);
        boss.ApplyData(data);
        return boss;
    }

    /// <summary>A bare chase target in the "Players" group (no hurtbox, never ticks).</summary>
    private static PlayerController CreateTargetPlayer(Vector2 position) {
        var player = new PlayerController { Name = "StoryEnemyCombatTarget", Position = position };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        player.AddToGroup("Players");
        return player;
    }

    /// <summary>An Environment floor whose top surface is y = 0.</summary>
    private static StaticBody2D CreateFloor(float centerX, float width) {
        var floor = new StaticBody2D {
            Name = "StoryEnemyCombatFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(width, 40f) },
            Position = new Vector2(centerX, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }

    /// <summary>A 40-wide Environment wall standing on the floor.</summary>
    private static StaticBody2D CreateWall(float centerX) {
        var wall = new StaticBody2D {
            Name = "StoryEnemyCombatWall",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        wall.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(40f, 400f) },
            Position = new Vector2(centerX, -200f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(wall);
        return wall;
    }
}
