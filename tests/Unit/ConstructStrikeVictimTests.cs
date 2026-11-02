using System.Reflection;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using static FTT.Tests.Unit.ConstructStrikeKit;

namespace FTT.Tests.Unit;

/// <summary>
/// Lingering-kit pass (KL-SCON, plan 1.3) — how a victim treats a construct strike or discharge
/// (<see cref="ConstructStrikes.IsStrike"/>: <c>CountsAsCombo</c> on a Construct delivery). A mob takes it at
/// full damage with no S2 chain proration and no dim, charges no standard poise (the elite V7.4 budget still
/// charges), and the hitstun only ever EXTENDS a stun already running; the Level 13 clone's launch or tumble is
/// never cancelled or shortened. Each case runs the same hit WITHOUT the marker as the control that shows the old
/// behaviour.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ConstructStrikeVictimTests {
    private static readonly Vector2 Origin = new(-78000f, 600f);

    private static HitPayload Construct(bool countsAsCombo, float damage = 10f, float hitstun = 0.5f,
        HitDelivery delivery = HitDelivery.Construct) => new() {
        AttackerIndex = 0,
        TargetIndex = -1,
        AttackID = "tesla_tesla_coil",
        HitboxID = ConstructStrikes.CoilSyncedArcHitboxID,
        AttackClass = AttackClass.Basic,
        Damage = damage,
        HitstunDuration = hitstun,
        Delivery = delivery,
        Origin = HitOrigin.Special,
        ExemptFromHitstop = true,
        CountsAsCombo = countsAsCombo,
        ContactId = 0
    };

    private static void Hit(EnemyController mob, HitPayload payload) {
        payload.ContactId = HitClassification.NextContactId();
        HurtboxOf(mob).TakeHit(payload);
    }

    [TestCase]
    public void AStrikeIsExemptFromChainProrationWhileAPlainConstructHitAdvancesTheChain() {
        InHost("VictimProration", host => {
            EnemyController strikeMob = Mob(host, Origin);
            EnemyController controlMob = Mob(host, Origin + new Vector2(400f, 0f));
            for (int i = 0; i < 6; i++) {
                Hit(strikeMob, Construct(countsAsCombo: true));
                Hit(controlMob, Construct(countsAsCombo: false));
            }
            AssertThat(strikeMob.ConfirmChainHits).OverrideFailureMessage("A strike advances no chain.").IsEqual(0);
            AssertThat(9999 - strikeMob.CurrentHP).OverrideFailureMessage("Six full-value strikes.").IsEqual(60);
            AssertThat(controlMob.ConfirmChainHits).OverrideFailureMessage("Control: a plain construct hit does advance the chain.").IsGreater(0);
            AssertThat(9999 - controlMob.CurrentHP).OverrideFailureMessage("Control: the chain decay cuts it.").IsLess(60);
        });
    }

    [TestCase]
    public void AStrikeChargesNoStandardPoiseButTheEliteBudgetStillCharges() {
        InHost("VictimPoise", host => {
            EnemyController strikeMob = Mob(host, Origin);
            EnemyController controlMob = Mob(host, Origin + new Vector2(400f, 0f));
            for (int i = 0; i < 6; i++) {
                Hit(strikeMob, Construct(countsAsCombo: true));
                Hit(controlMob, Construct(countsAsCombo: false));
            }
            AssertThat(strikeMob.IsArmoredRecovery).OverrideFailureMessage("Six 0.5 s strikes charge no standard poise.").IsFalse();
            AssertFloat(strikeMob.StaggerBudgetSeconds).IsEqualApprox(0f, 0.0001f);
            AssertThat(controlMob.IsArmoredRecovery || controlMob.StaggerBudgetSeconds > 0f)
                .OverrideFailureMessage("Control: the same stuns without the marker do charge standard poise.").IsTrue();

            EnemyController elite = Mob(host, Origin + new Vector2(800f, 0f), "chrono_warden");
            Hit(elite, Construct(countsAsCombo: true));
            AssertThat(elite.StaggerBudgetSeconds > 0f)
                .OverrideFailureMessage("The elite V7.4 budget still charges for a strike.").IsTrue();
        });
    }

    [TestCase]
    public void AStrikeOnlyExtendsAStunAlreadyRunningAndNeverShortensIt() {
        InHost("VictimStunMax", host => {
            EnemyController longStun = Mob(host, Origin);
            longStun.ApplyStun(1.0f);
            AssertFloat(StunSeconds(longStun)).IsEqualApprox(1.0f, 0.001f);
            Hit(longStun, Construct(countsAsCombo: true, hitstun: 20f / 60f));
            AssertFloat(StunSeconds(longStun))
                .OverrideFailureMessage("A 20-frame strike must not shorten a 1.0 s stun (a launch's flight).")
                .IsEqualApprox(1.0f, 0.001f);

            EnemyController control = Mob(host, Origin + new Vector2(400f, 0f));
            control.ApplyStun(1.0f);
            Hit(control, Construct(countsAsCombo: false, hitstun: 20f / 60f, delivery: HitDelivery.DirectHit));
            AssertFloat(StunSeconds(control)).OverrideFailureMessage("Control: without the marker the shorter stun replaces it.")
                .IsEqualApprox(20f / 60f, 0.001f);

            EnemyController extend = Mob(host, Origin + new Vector2(800f, 0f));
            extend.ApplyStun(0.1f);
            Hit(extend, Construct(countsAsCombo: true, hitstun: 1.0f));
            AssertFloat(StunSeconds(extend)).OverrideFailureMessage("A longer strike stun extends it.").IsEqualApprox(1.0f, 0.001f);
        });
    }

    [TestCase]
    public void AStrikeNeverCancelsTheLaunchOrTumbleOfAHeroBodyOrShortensItsStun() {
        InHost("VictimHero", host => {
            PlayerController clone = CharacterFactory.CreateCharacter("einstein", 1, applyStoryProgression: false);
            host.AddChild(clone);
            clone.GlobalPosition = Origin;
            FieldInfo stunTimer = typeof(PlayerController).GetField("_stunTimer", BindingFlags.NonPublic | BindingFlags.Instance)!;
            FieldInfo stunTumble = typeof(PlayerController).GetField("_stunTumble", BindingFlags.NonPublic | BindingFlags.Instance)!;

            clone.ApplyStun(1.0f, thrown: false);
            stunTumble.SetValue(clone, true);   // a launched stun: tumble owed
            HitPayload strike = Construct(countsAsCombo: true, damage: 5f, hitstun: 20f / 60f);
            strike.ContactId = HitClassification.NextContactId();
            HurtboxOf(clone).TakeHit(strike);
            AssertThat((bool)stunTumble.GetValue(clone)!)
                .OverrideFailureMessage("A strike must not cancel a launch's tumble.").IsTrue();
            AssertThat((float)stunTimer.GetValue(clone)! >= 0.999f)
                .OverrideFailureMessage("Nor shorten its stun (max semantics).").IsTrue();
            AssertThat(clone.CurrentState).IsEqual(CharacterState.Stunned);

            // Control: the same hit without the marker replaces the stun and drops the tumble.
            HitPayload plain = Construct(countsAsCombo: false, damage: 5f, hitstun: 20f / 60f, delivery: HitDelivery.DirectHit);
            plain.ContactId = HitClassification.NextContactId();
            HurtboxOf(clone).TakeHit(plain);
            AssertThat((bool)stunTumble.GetValue(clone)!).IsFalse();
            AssertThat((float)stunTimer.GetValue(clone)! < 0.5f).IsTrue();
        });
    }
}
