using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using static FTT.Tests.Unit.ConstructStrikeKit;

namespace FTT.Tests.Unit;

/// <summary>
/// Lingering-kit pass (KL-SCON, plan 1.3, 1.4 and decision 26) — the Story Clockwork Turret: four charges (five
/// with Clockwork Overdrive); a landed owner hit within 10 units fires a fast 8-damage synced bolt with 20
/// frames of hitstun and spends one; the own 4-damage shot every 180 frames spends nothing and the turret no
/// longer self-destructs, living out its 15 s; the glide bolt spends a charge; at 0 it fizzles; Overdrive's extra
/// own shot is for Extractors only. The harness has no projectile pool, so bolts take the instant fallback;
/// one case registers the pool and reads the pooled bolt's Hitbox.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryTurretChargesTests {
    private static readonly Vector2 Origin = new(-72000f, 600f);

    private static void InHostInstant(string name, System.Action<Node2D> body) {
        LeonardoTurretNode.InstantStrikesForTesting = true;
        try {
            InHost(name, body);
        } finally {
            LeonardoTurretNode.InstantStrikesForTesting = false;
        }
    }

    [TestCase]
    public void TheOwnShotSpendsNothingAndTheTurretLivesOutItsFifteenSeconds() {
        InHostInstant("TurretOwn", host => {
            PlayerController leonardo = Owner(host, "leonardo", Origin + new Vector2(-100f, 0f));
            LeonardoTurretNode turret = Turret(host, leonardo, Origin);
            EnemyController mob = Mob(host, Origin + new Vector2(200f, 0f));

            // Four own shots (4 damage every 180 frames, frames 180/360/540/720) and the old self-destruct
            // at the fourth bolt is gone: it is still standing at frame 800.
            for (int frame = 0; frame < 800; frame++) turret._PhysicsProcess(Step);
            AssertThat(turret.BoltsFired).OverrideFailureMessage($"One own shot per 180 frames (fired {turret.BoltsFired}).").IsEqual(4);
            // (A plain construct hit is not proration-exempt, so a fourth hit in one stun chain is decayed.)
            AssertThat(9999 - mob.CurrentHP >= 3 * (int)AuthoredResources.Load<AbilityData>("res://resources/Abilities/leonardo/special_2.tres").BaseDamage).IsTrue();
            AssertThat(turret.Charges).OverrideFailureMessage("The own shot spends no charge.").IsEqual(4);
            AssertThat(turret.IsTurretDestroyed).OverrideFailureMessage("No longer self-destructs after four bolts.").IsFalse();

            // Its lifetime is the 15 s cap: alive just before, gone just after.
            for (int frame = 800; frame < 15 * 60 - 2; frame++) turret._PhysicsProcess(Step);
            AssertThat(turret.IsTurretDestroyed).IsFalse();
            for (int frame = 0; frame < 6; frame++) turret._PhysicsProcess(Step);
            AssertThat(turret.IsTurretDestroyed).OverrideFailureMessage("It lives out its 15 s lifetime.").IsTrue();
        });
    }

    [TestCase]
    public void ASyncedBoltIsEightDamageWithTwentyFramesOfHitstunAndSpendsOneCharge() {
        InHostInstant("TurretSynced", host => {
            PlayerController leonardo = Owner(host, "leonardo", Origin + new Vector2(-100f, 0f));
            LeonardoTurretNode turret = Turret(host, leonardo, Origin);
            EnemyController mob = Mob(host, Origin + new Vector2(300f, 0f));
            float momentum = leonardo.MomentumPips;
            int before = mob.CurrentHP;
            OwnerLands(leonardo, mob);
            AssertThat(before - mob.CurrentHP).IsEqual(LingeringKitRules.TurretSyncedBoltDamage);
            AssertThat(turret.Charges).IsEqual(3);
            AssertThat(turret.SyncedBoltsFired).IsEqual(1);
            AssertThat(mob.CurrentState).IsEqual(EnemyState.Stunned);
            AssertFloat(StunSeconds(mob)).IsEqualApprox(LingeringKitRules.TurretSyncedBoltHitstunFrames / 60f, 0.001f);
            AssertFloat(leonardo.MomentumPips - momentum).IsEqualApprox(0.05f, 0.0005f);

            // Beyond 10 units: nothing.
            EnemyController far = Mob(host, Origin + new Vector2(800f, 0f));
            before = far.CurrentHP;
            OwnerLands(leonardo, far);
            AssertThat(far.CurrentHP).IsEqual(before);
            AssertThat(turret.Charges).IsEqual(3);

            // The strike does not trigger itself, and a tick or a construct hit does not either.
            OwnerLands(leonardo, mob, OwnerHit("tick", HitDelivery.Tick, HitOrigin.Special));
            OwnerLands(leonardo, mob, OwnerHit(ConstructStrikes.TurretSyncedBoltHitboxID, HitDelivery.Construct, HitOrigin.Special));
            AssertThat(turret.Charges).IsEqual(3);
        });
    }

    [TestCase]
    public void FourSyncedBoltsFizzleTheTurretAndTheFifthHitFiresNothing() {
        InHostInstant("TurretFizzle", host => {
            PlayerController leonardo = Owner(host, "leonardo", Origin + new Vector2(-100f, 0f));
            LeonardoTurretNode turret = Turret(host, leonardo, Origin);
            EnemyController mob = Mob(host, Origin + new Vector2(300f, 0f));
            for (int strike = 0; strike < 4; strike++) OwnerLands(leonardo, mob);
            AssertThat(turret.Charges).IsEqual(0);
            AssertThat(turret.IsTurretDestroyed).OverrideFailureMessage("At 0 charges the turret fizzles.").IsTrue();
            int after = mob.CurrentHP;
            OwnerLands(leonardo, mob);
            AssertThat(mob.CurrentHP).IsEqual(after);
        });
    }

    [TestCase]
    public void TheGlideBoltSpendsAChargeAndTheLastOneFizzlesTheTurret() {
        InHostInstant("TurretGlide", host => {
            PlayerController leonardo = Owner(host, "leonardo", Origin + new Vector2(-100f, 0f));
            LeonardoTurretNode turret = Turret(host, leonardo, Origin);
            Mob(host, Origin + new Vector2(300f, 0f));
            for (int shot = 1; shot <= 4; shot++) {
                AssertThat(turret.TryCommandBolt()).IsTrue();
                AssertThat(turret.Charges).IsEqual(4 - shot);
            }
            AssertThat(turret.IsTurretDestroyed).IsTrue();
            AssertThat(turret.TryCommandBolt()).IsFalse();
        });
    }

    [TestCase]
    public void ClockworkOverdriveAddsAChargeAndItsFasterShotIsForExtractorsOnly() {
        InHostInstant("TurretOverdrive", host => {
            PlayerController leonardo = Owner(host, "leonardo", Origin + new Vector2(-100f, 0f));
            leonardo.StoryAbilityPerks.Add(LeonardoClockworkTurret.ClockworkOverdrivePerkKey);
            LeonardoTurretNode turret = Turret(host, leonardo, Origin, overdrive: true);
            AssertThat(turret.Charges).IsEqual(LingeringKitRules.TurretCharges + LingeringKitRules.TurretOverdriveBonusCharges);
            AssertThat(turret.Charges).IsEqual(5);

            PackedScene extractorScene = ResourceLoader.Load<PackedScene>("res://scenes/templates/ChronalExtractorTemplate.tscn");
            var extractor = extractorScene.Instantiate<ChronalExtractor>();
            host.AddChild(extractor);
            extractor.GlobalPosition = Origin + new Vector2(300f, 0f);
            int hp = extractor.CurrentHP;
            // First shot at frame 180, the next at half the interval (90 frames) later.
            for (int frame = 0; frame < 180 + 91 + 2; frame++) turret._PhysicsProcess(Step);
            AssertThat(turret.BoltsFired).OverrideFailureMessage("Two Extractor shots in 180 + 91 frames.").IsEqual(2);
            AssertThat(hp - extractor.CurrentHP > 0).IsTrue();
            AssertThat(turret.Charges).OverrideFailureMessage("An own shot spends no charge.").IsEqual(5);

            // The same turret without the perk never shoots an Extractor.
            LeonardoTurretNode plain = Turret(host, leonardo, Origin + new Vector2(0f, 400f), overdrive: false);
            plain.GlobalPosition = Origin + new Vector2(0f, 0f);
            int hpBefore = extractor.CurrentHP;
            for (int frame = 0; frame < 400; frame++) plain._PhysicsProcess(Step);
            AssertThat(plain.BoltsFired).IsEqual(0);
            AssertThat(plain.Charges).IsEqual(4);
            AssertThat(extractor.CurrentHP <= hpBefore).IsTrue();
        });
    }

    [TestCase]
    public void TheSyncedBoltIsAConstructHitThatCountsAsComboWithItsOwnHitstun() {
        InHost("TurretPooledBolt", host => {
            {
                PlayerController leonardo = Owner(host, "leonardo", Origin + new Vector2(-100f, 0f));
                LeonardoTurretNode turret = Turret(host, leonardo, Origin);
                EnemyController mob = Mob(host, Origin + new Vector2(300f, 0f));
                OwnerLands(leonardo, mob);
                AssertThat(turret.SyncedBoltsFired).IsEqual(1);
                PlaceholderProjectile bolt = null;
                foreach (Node node in host.GetChildren()) if (node is PlaceholderProjectile p) bolt = p;
                AssertThat(bolt).OverrideFailureMessage("The synced bolt is a pooled projectile.").IsNotNull();
                HitPayload payload = bolt.GetNode<Hitbox>("Hitbox").CreatePayload(-1);
                AssertThat(payload.CountsAsCombo).IsTrue();
                AssertThat(payload.Delivery).IsEqual(HitDelivery.Construct);
                AssertThat(payload.HitboxID).IsEqual(ConstructStrikes.TurretSyncedBoltHitboxID);
                AssertFloat(payload.HitstunDuration).IsEqualApprox(LingeringKitRules.TurretSyncedBoltHitstunFrames / 60f, 0.001f);
                AssertThat(payload.Knockback).IsEqual(Vector2.Zero);
                AssertThat(payload.ExemptFromHitstop).IsTrue();

                // Re-Setup for an ordinary shot clears the marker (pooled).
                bolt.Setup(5f, Vector2.Zero, 300f, true, 0, Colors.White, default, 1f, leonardo, null);
                AssertThat(bolt.GetNode<Hitbox>("Hitbox").CreatePayload(-1).CountsAsCombo).IsFalse();
                bolt.GetParent()?.RemoveChild(bolt);
            }
        });
    }

    [TestCase]
    public void TurretChargesSurviveTimeFreezeAndAPooledTurretStartsFullAndDeaf() {
        InHostInstant("TurretFreezePool", host => {
            PlayerController leonardo = Owner(host, "leonardo", Origin + new Vector2(-100f, 0f));
            LeonardoTurretNode turret = Turret(host, leonardo, Origin);
            EnemyController mob = Mob(host, Origin + new Vector2(300f, 0f));
            OwnerLands(leonardo, mob);
            turret.SetTimeFrozen(true);
            int before = mob.CurrentHP;
            OwnerLands(leonardo, mob);
            AssertThat(mob.CurrentHP).IsEqual(before);
            turret.SetTimeFrozen(false);
            AssertThat(turret.Charges).IsEqual(3);

            turret.OnDespawn();
            AssertThat(turret.Charges).IsEqual(0);
            before = mob.CurrentHP;
            OwnerLands(leonardo, mob);
            AssertThat(mob.CurrentHP).OverrideFailureMessage("A despawned turret has unsubscribed.").IsEqual(before);
            AbilityData data = AuthoredResources.Load<AbilityData>("res://resources/Abilities/leonardo/special_2.tres");
            turret.Initialize(data, leonardo, clockworkOverdrive: false);
            AssertThat(turret.Charges).IsEqual(4);
            turret.GetParent().RemoveChild(turret);
            turret.Free();
            OwnerLands(leonardo, mob);
        });
    }
}
