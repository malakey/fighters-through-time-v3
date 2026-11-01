using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using static FTT.Tests.Unit.ConstructStrikeKit;

namespace FTT.Tests.Unit;

/// <summary>
/// Lingering-kit pass (KL-SCON, plan 1.3, 1.4 and decision 12) — the Story Serpent Nest: its own bite is a nip
/// (3 damage every 60 frames, 0.15 s of hitstun, no Venom, 60 px reach) that spends nothing; it holds three
/// charges, and a landed owner hit within 2.5 units makes a serpent lunge for 6 damage, a 60-frame hold and
/// Venom (intensity 1, 4 s), one charge, fizzling at 0.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryNestStrikeTests {
    private static readonly Vector2 Origin = new(-76000f, 600f);

    [TestCase]
    public void TheOwnBiteIsAThreeDamageNipWithShortHitstunNoVenomAndNoChargeSpent() {
        InHost("NestNip", host => {
            PlayerController cleopatra = Owner(host, "cleopatra", Origin + new Vector2(-300f, 0f));
            SerpentNestNode nest = Nest(host, cleopatra, Origin);
            EnemyController mob = Mob(host, Origin + new Vector2(20f, 0f));
            for (int frame = 0; frame < 61; frame++) nest._PhysicsProcess(Step);
            AssertThat(9999 - mob.CurrentHP).IsEqual((int)AuthoredResources.Load<AbilityData>("res://resources/Abilities/cleopatra/special_1.tres").BaseDamage);
            AssertThat(mob.CurrentState).IsEqual(EnemyState.Stunned);
            AssertFloat(StunSeconds(mob)).IsEqualApprox(AuthoredResources.Load<AbilityData>("res://resources/Abilities/cleopatra/special_1.tres").HitstunFrames / 60f, 0.001f);
            AssertThat(mob.HasStatusEffect(StatusType.Venom)).OverrideFailureMessage("The nip carries no Venom.").IsFalse();
            AssertThat(nest.Charges).OverrideFailureMessage("The own nip spends nothing.").IsEqual(3);
            AssertThat(SerpentNestNode.BiteRangePixels).IsEqual(60f);
        });
    }

    [TestCase]
    public void TheSyncedLungeIsSixDamageWithAFullSecondHoldAndVenomAndSpendsOneCharge() {
        InHost("NestLunge", host => {
            PlayerController cleopatra = Owner(host, "cleopatra", Origin + new Vector2(-300f, 0f));
            SerpentNestNode nest = Nest(host, cleopatra, Origin);
            EnemyController mob = Mob(host, Origin + new Vector2(110f, 0f));
            float momentum = cleopatra.MomentumPips;
            int before = mob.CurrentHP;
            OwnerLands(cleopatra, mob);
            AssertThat(before - mob.CurrentHP).IsEqual(LingeringKitRules.NestSyncedLungeDamage);
            AssertThat(nest.Charges).IsEqual(2);
            AssertThat(mob.CurrentState).IsEqual(EnemyState.Stunned);
            AssertFloat(StunSeconds(mob)).IsEqualApprox(LingeringKitRules.NestSyncedLungeHoldFrames / 60f, 0.001f);
            AssertThat(mob.HasStatusEffect(StatusType.Venom)).OverrideFailureMessage("The lunge carries Venom.").IsTrue();
            AssertFloat(cleopatra.MomentumPips - momentum).IsEqualApprox(0.05f, 0.0005f);
        });
    }

    [TestCase]
    public void AStrikeTheTargetRefusesPaysNoMomentumAndTheTriggeringHitNeverPaysTheSyncedPayout() {
        InHost("NestRefused", host => {
            PlayerController cleopatra = Owner(host, "cleopatra", Origin + new Vector2(-300f, 0f));
            SerpentNestNode nest = Nest(host, cleopatra, Origin);
            EnemyController mob = Mob(host, Origin + new Vector2(110f, 0f));
            mob.DrillInvulnerable = true; // refuses every hit: D1a not met
            float start = cleopatra.MomentumPips;
            OwnerLands(cleopatra, mob);
            AssertThat(nest.Charges).OverrideFailureMessage("The lunge still fires and spends its charge.").IsEqual(2);
            AssertFloat(cleopatra.MomentumPips).OverrideFailureMessage("A refused strike earns nothing.").IsEqualApprox(start, 0.0005f);
            mob.DrillInvulnerable = false;
            OwnerLands(cleopatra, mob);
            AssertFloat(cleopatra.MomentumPips - start)
                .OverrideFailureMessage("Only the strike that lands pays, once; the triggering hit pays no synced bonus.")
                .IsEqualApprox(0.05f, 0.0005f);
        });
    }

    [TestCase]
    public void TheLungeReachIsTwoAndAHalfUnitsAndThreeLungesFizzleTheNest() {
        InHost("NestReach", host => {
            PlayerController cleopatra = Owner(host, "cleopatra", Origin + new Vector2(-300f, 0f));
            SerpentNestNode nest = Nest(host, cleopatra, Origin);
            EnemyController far = Mob(host, Origin + new Vector2(260f, 0f));
            int farBefore = far.CurrentHP;
            OwnerLands(cleopatra, far);
            AssertThat(far.CurrentHP).OverrideFailureMessage("260 px is beyond the 2.5-unit (150 px) lunge reach.").IsEqual(farBefore);
            AssertThat(nest.Charges).IsEqual(3);

            EnemyController mob = Mob(host, Origin + new Vector2(120f, 0f));
            for (int lunge = 0; lunge < 3; lunge++) OwnerLands(cleopatra, mob);
            AssertThat(nest.Charges).IsEqual(0);
            AssertThat(nest.IsNestDestroyed).OverrideFailureMessage("At 0 charges the nest fizzles.").IsTrue();
        });
    }

    [TestCase]
    public void ALungeNeverTriggersAnotherAndAnUnlandedOrTickHitTriggersNone() {
        InHost("NestNoLoop", host => {
            PlayerController cleopatra = Owner(host, "cleopatra", Origin + new Vector2(-300f, 0f));
            SerpentNestNode a = Nest(host, cleopatra, Origin);
            EnemyController mob = Mob(host, Origin + new Vector2(100f, 0f));
            OwnerLands(cleopatra, mob, OwnerHit(landed: false));
            OwnerLands(cleopatra, mob, OwnerHit("tick", HitDelivery.Tick, HitOrigin.Special));
            OwnerLands(cleopatra, mob, OwnerHit(ConstructStrikes.NestSyncedLungeHitboxID, HitDelivery.Construct, HitOrigin.Special));
            AssertThat(a.Charges).IsEqual(3);
            OwnerLands(cleopatra, mob);
            AssertThat(a.Charges).OverrideFailureMessage("One landed hit, one lunge; the lunge raised the seam but is filtered.").IsEqual(2);
        });
    }

    [TestCase]
    public void NestChargesSurviveTimeFreezeAndAPooledNestStartsFullAndDeaf() {
        InHost("NestFreezePool", host => {
            PlayerController cleopatra = Owner(host, "cleopatra", Origin + new Vector2(-300f, 0f));
            SerpentNestNode nest = Nest(host, cleopatra, Origin);
            EnemyController mob = Mob(host, Origin + new Vector2(100f, 0f));
            OwnerLands(cleopatra, mob);
            nest.SetTimeFrozen(true);
            int before = mob.CurrentHP;
            OwnerLands(cleopatra, mob);
            AssertThat(mob.CurrentHP).IsEqual(before);
            nest.SetTimeFrozen(false);
            AssertThat(nest.Charges).IsEqual(2);

            nest.OnDespawn();
            AssertThat(nest.Charges).IsEqual(0);
            before = mob.CurrentHP;
            OwnerLands(cleopatra, mob);
            AssertThat(mob.CurrentHP).OverrideFailureMessage("A despawned nest has unsubscribed.").IsEqual(before);
            AbilityData data = AuthoredResources.Load<AbilityData>("res://resources/Abilities/cleopatra/special_1.tres");
            nest.Initialize(data, cleopatra, aspsBite: false);
            AssertThat(nest.Charges).IsEqual(3);
            nest.GetParent().RemoveChild(nest);
            nest.Free();
            OwnerLands(cleopatra, mob);
        });
    }
}
