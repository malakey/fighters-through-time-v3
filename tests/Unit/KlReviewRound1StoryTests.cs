using System.Collections.Generic;
using System.Reflection;
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
/// Lingering-kit pass, review round 1 (Story half): a synced construct bolt never arms D3 for the
/// current cast; Golden Ratio's ticks trigger constructs; a construct's own attack never shortens a
/// stun, hold or launch; Golden Ratio carries hitstun on tick 1 only; the Lorentz discharge is Special
/// class and the synced strikes Basic; a launched hero's hitstun extends to max(current, incoming).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class KlReviewRound1StoryTests {
    private static readonly Vector2 Origin = new(-86000f, 600f);

    private static HitPayload SyncedBolt(int serial) {
        HitPayload bolt = OwnerHit(ConstructStrikes.TurretSyncedBoltHitboxID, HitDelivery.Construct, HitOrigin.Special);
        bolt.CountsAsCombo = true;
        bolt.SourceExecutionSerial = serial;
        return bolt;
    }

    [TestCase]
    public void ASyncedConstructBoltNeverArmsD3EvenWithSerialZeroAfterARecast() {
        InHost("D3Bolt", host => {
            PlayerController leonardo = Owner(host, "leonardo", Origin);
            var turretCast = leonardo.GetNode<BaseSpecial>("Special2");
            AssertThat(turretCast.TryExecute()).IsTrue();
            // The bolt of the PREVIOUS cast lands now: pooled synced bolts carry serial 0.
            HitPayload bolt = SyncedBolt(0);
            turretCast.ConfirmAbilityHit(in bolt, 8f, Origin);
            AssertThat(turretCast.LandedDirectHitThisExecution)
                .OverrideFailureMessage("A synced construct strike never arms D3 (plan 1.3).").IsFalse();
            HitPayload attributed = SyncedBolt(turretCast.ExecutionSerial);
            turretCast.ConfirmAbilityHit(in attributed, 8f, Origin);
            AssertThat(turretCast.LandedDirectHitThisExecution)
                .OverrideFailureMessage("Not even a serial-matching synced strike.").IsFalse();
        });
    }

    [TestCase]
    public void ADetonationArmsD3OnlyWithAnExplicitAttributionToTheCurrentCast() {
        InHost("D3Discharge", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            var pulse = tesla.GetNode<BaseSpecial>("Special2");
            AssertThat(pulse.TryExecute()).IsTrue();
            HitPayload burst = OwnerHit(ConstructStrikes.CoilDischargeHitboxID, HitDelivery.Construct, HitOrigin.Special);
            burst.CountsAsCombo = true;
            burst.SourceExecutionSerial = 0;
            pulse.ConfirmAbilityHit(in burst, 12f, Origin);
            AssertThat(pulse.LandedDirectHitThisExecution).OverrideFailureMessage("Serial 0 is no attribution.").IsFalse();
            burst.SourceExecutionSerial = pulse.ExecutionSerial + 7;
            pulse.ConfirmAbilityHit(in burst, 12f, Origin);
            AssertThat(pulse.LandedDirectHitThisExecution).OverrideFailureMessage("Another cast's serial.").IsFalse();
            // Review round 2: a matching serial from ANOTHER ability (serials are per ability) arms nothing.
            burst.SourceExecutionSerial = pulse.ExecutionSerial;
            burst.AttackID = "einstein_relativity_rift";
            pulse.ConfirmAbilityHit(in burst, 12f, Origin);
            AssertThat(pulse.LandedDirectHitThisExecution).OverrideFailureMessage("A matching serial of another ability.").IsFalse();
            burst.AttackID = pulse.Data.AbilityID;
            pulse.ConfirmAbilityHit(in burst, 12f, Origin);
            AssertThat(pulse.LandedDirectHitThisExecution).OverrideFailureMessage("The Pulse's own discharge arms it.").IsTrue();
        });
    }

    [TestCase]
    public void GoldenRatioTicksTriggerATurretStrikeButConstructHitsAndTicksDoNot() {
        LeonardoTurretNode.InstantStrikesForTesting = true;
        try {
            InHost("GoldenTriggers", host => {
                PlayerController leonardo = Owner(host, "leonardo", Origin + new Vector2(-100f, 0f));
                LeonardoTurretNode turret = Turret(host, leonardo, Origin);
                EnemyController mob = Mob(host, Origin + new Vector2(300f, 0f));

                HitPayload tick = OwnerHit("spiral_tick", HitDelivery.DirectHit, HitOrigin.Special);
                tick.CountsAsCombo = true; // Golden Ratio's marker must not exclude it (decision 18)
                OwnerLands(leonardo, mob, tick);
                AssertThat(turret.Charges).OverrideFailureMessage("A landed Golden Ratio tick fires a bolt.").IsEqual(3);

                // Excluded by delivery or identity, never by the marker.
                HitPayload construct = OwnerHit(ConstructStrikes.NestSyncedLungeHitboxID, HitDelivery.Construct, HitOrigin.Special);
                construct.CountsAsCombo = true;
                OwnerLands(leonardo, mob, construct);
                HitPayload zoneTick = OwnerHit("vortex_tick", HitDelivery.Tick, HitOrigin.Special);
                OwnerLands(leonardo, mob, zoneTick);
                HitPayload pulse = OwnerHit("pulse", HitDelivery.DirectHit, HitOrigin.Special);
                OwnerLands(leonardo, mob, pulse);
                AssertThat(turret.Charges).OverrideFailureMessage("None of those triggers a strike.").IsEqual(3);

                var info = new OwnerHitLandedInfo(HurtboxOf(mob), in tick, true, Origin);
                AssertThat(info.TriggersSyncedStrike).IsTrue();
            });
        } finally {
            LeonardoTurretNode.InstantStrikesForTesting = false;
        }
    }

    private static HitPayload ConstructOwn(float damage, float hitstun) => new() {
        AttackerIndex = 0,
        TargetIndex = -1,
        AttackID = "test_own_attack",
        HitboxID = "own_nip",
        AttackClass = AttackClass.Basic,
        Damage = damage,
        HitstunDuration = hitstun,
        Delivery = HitDelivery.Construct,
        Origin = HitOrigin.Special,
        ExemptFromHitstop = true,
        CountsAsCombo = false
    };

    [TestCase]
    public void AConstructsOwnAttackNeverShortensAHoldOnAMobOrAHero() {
        InHost("OwnAttackVictim", host => {
            EnemyController held = Mob(host, Origin);
            held.ApplyStun(1.0f);
            HitPayload own = ConstructOwn(damage: 3f, hitstun: 12f / 60f);
            own.ContactId = HitClassification.NextContactId();
            HurtboxOf(held).TakeHit(own);
            AssertFloat(StunSeconds(held)).OverrideFailureMessage("An own nip must not cut a 60-frame hold.").IsEqualApprox(1.0f, 0.001f);

            // The real nest node: its own nip on a mob under a fresh hold.
            PlayerController cleopatra = Owner(host, "cleopatra", Origin + new Vector2(-300f, 0f));
            EnemyController victim = Mob(host, Origin + new Vector2(400f, 0f));
            SerpentNestNode nest = Nest(host, cleopatra, Origin + new Vector2(400f, 0f));
            victim.ApplyStun(1.0f);
            int before = victim.CurrentHP;
            for (int frame = 0; frame < 400 && victim.CurrentHP == before; frame++) nest._PhysicsProcess(Step);
            AssertThat(victim.CurrentHP).OverrideFailureMessage("The nest's own nip landed.").IsLess(before);
            AssertFloat(StunSeconds(victim)).IsEqualApprox(1.0f, 0.001f);

            PlayerController clone = CharacterFactory.CreateCharacter("einstein", 1, applyStoryProgression: false);
            host.AddChild(clone);
            clone.GlobalPosition = Origin + new Vector2(0f, -400f);
            FieldInfo stunTimer = typeof(PlayerController).GetField("_stunTimer", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clone.ApplyStun(1.0f, thrown: false);
            HitPayload ownAgain = ConstructOwn(damage: 3f, hitstun: 12f / 60f);
            ownAgain.ContactId = HitClassification.NextContactId();
            HurtboxOf(clone).TakeHit(ownAgain);
            AssertThat((float)stunTimer.GetValue(clone)! >= 0.999f).IsTrue();
        });
    }

    [TestCase]
    public void ALaunchedHeroWithLittleHitstunLeftIsExtendedByALongerStrikeAndKeepsItsTumble() {
        InHost("LaunchedHero", host => {
            PlayerController clone = CharacterFactory.CreateCharacter("einstein", 1, applyStoryProgression: false);
            host.AddChild(clone);
            clone.GlobalPosition = Origin;
            FieldInfo stunTimer = typeof(PlayerController).GetField("_stunTimer", BindingFlags.NonPublic | BindingFlags.Instance)!;
            FieldInfo stunTumble = typeof(PlayerController).GetField("_stunTumble", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clone.ApplyStun(10f / 60f, thrown: false);
            stunTumble.SetValue(clone, true);
            clone.Velocity = new Vector2(120f, -300f);
            HitPayload lunge = ConstructOwn(damage: 6f, hitstun: 1.0f);
            lunge.CountsAsCombo = true;
            lunge.HitboxID = ConstructStrikes.NestSyncedLungeHitboxID;
            lunge.ContactId = HitClassification.NextContactId();
            HurtboxOf(clone).TakeHit(lunge);
            AssertThat((float)stunTimer.GetValue(clone)! >= 0.999f)
                .OverrideFailureMessage("max(current, incoming): a launched hero holds for the full 60 frames.").IsTrue();
            AssertThat((bool)stunTumble.GetValue(clone)!).OverrideFailureMessage("The tumble stays.").IsTrue();
            AssertThat(clone.Velocity.X).OverrideFailureMessage("Velocity is untouched.").IsEqual(120f);
        });
    }

    [TestCase]
    public void AMobWithLittleHitstunLeftIsExtendedByALongerStrike() {
        InHost("LaunchedMob", host => {
            EnemyController mob = Mob(host, Origin);
            mob.ApplyStun(10f / 60f);
            HitPayload lunge = ConstructOwn(damage: 6f, hitstun: 1.0f);
            lunge.CountsAsCombo = true;
            lunge.HitboxID = ConstructStrikes.NestSyncedLungeHitboxID;
            lunge.ContactId = HitClassification.NextContactId();
            HurtboxOf(mob).TakeHit(lunge);
            AssertFloat(StunSeconds(mob)).IsEqualApprox(1.0f, 0.001f);
        });
    }

    [TestCase]
    public void GoldenRatioCarriesHitstunOnTickOneAndItsAuthoredFinalLaunchOnly() {
        InHost("GoldenTicks", host => {
            PlayerController leonardo = Owner(host, "leonardo", Origin);
            var spiral = leonardo.GetNode<LeonardoGoldenRatio>("Special1");
            var payloads = new List<HitPayload>();
            leonardo.OwnerHitLanded += info => { if (info.Payload.HitboxID.StartsWith("spiral")) payloads.Add(info.Payload); };
            AssertThat(spiral.TryExecute()).IsTrue();
            EnemyController mob = Mob(host, Origin + new Vector2(36f, 0f));
            for (int frame = 0; frame < 240 && payloads.Count < 3; frame++) spiral._PhysicsProcess(Step);
            AssertThat(payloads.Count).IsGreaterEqual(3);
            AssertFloat(payloads[0].HitstunDuration * 60f).OverrideFailureMessage("Tick 1 carries the normal hitstun.")
                .IsEqualApprox(LingeringKitRules.GoldenRatioFirstTickHitstunFrames, 0.01f);
            AssertFloat(payloads[1].HitstunDuration).OverrideFailureMessage("Tick 2 holds by the pull alone.").IsEqual(0f);
            AssertFloat(payloads[2].HitstunDuration * 60f).OverrideFailureMessage("The final tick keeps its authored hitstun, as the sim's expiry pulse.")
                .IsEqualApprox(12f, 0.01f);
            AssertThat(payloads[2].Knockback).OverrideFailureMessage("...and its authored shove.").IsNotEqual(Vector2.Zero);
            AssertThat(payloads[1].Knockback).OverrideFailureMessage("The middle tick has no impulse.").IsEqual(Vector2.Zero);
            AssertThat(mob != null).IsTrue();
        });
    }

    [TestCase]
    public void TheDischargeIsSpecialClassCostingTwoChargesAndASyncedStrikeIsBasicCostingOne() {
        InHost("BlockClasses", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            TeslaCoilNode coil = Coil(host, tesla, Origin + new Vector2(150f, 0f));
            AssertThat(coil.TryBuildDischarge(out HitPayload burst)).IsTrue();
            AssertThat(burst.AttackClass).IsEqual(AttackClass.Special);
            AssertThat(burst.BlockChargeCost).IsEqual(0);
            AssertThat(BlockRules.ChargeCost(burst.AttackClass, 3)).OverrideFailureMessage("Two charges via the Special rule.").IsEqual(2);

            EnemyController mob = Mob(host, Origin + new Vector2(200f, 0f));
            var strikes = new List<HitPayload>();
            tesla.OwnerHitLanded += info => { if (ConstructStrikes.IsSyncedStrike(in info.Payload)) strikes.Add(info.Payload); };
            OwnerLands(tesla, mob);
            AssertThat(strikes.Count).IsEqual(1);
            AssertThat(strikes[0].AttackClass).IsEqual(AttackClass.Basic);
            AssertThat(BlockRules.ChargeCost(strikes[0].AttackClass, 3)).IsEqual(1);
        });
    }
}
