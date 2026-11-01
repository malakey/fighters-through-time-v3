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
/// Lingering-kit pass (KL-SCON, plan 1.3 and 1.4) — the Story Tesla coil: three charges; a landed basic or
/// direct-Special hit of the owner's on a target within 4 units fires one 8-damage synced arc with 20 frames of
/// hitstun and spends one; two coils both fire; its own arc and the fence spend nothing; no whiff, tick,
/// construct hit or strike ever triggers one (no loops); the strike pays 0.05 pip; Time Freeze and pooling
/// behave. The owner's hit is raised through the real seam
/// (<see cref="PlayerController.RaiseOwnerHitLanded"/>), plus one case with a real swing.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryCoilSyncedStrikeTests {
    private static readonly Vector2 Origin = new(-70000f, 600f);

    [TestCase]
    public void ACoilHasThreeChargesAndEachStrikeSpendsOneAndTheThirdFizzlesIt() {
        InHost("CoilCharges", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            TeslaCoilNode coil = Coil(host, tesla, Origin + new Vector2(150f, 0f));
            EnemyController mob = Mob(host, Origin + new Vector2(200f, 0f));
            AssertThat(coil.Charges).IsEqual(LingeringKitRules.CoilCharges);
            AssertThat(LingeringKitRules.CoilCharges).IsEqual(3);

            for (int strike = 1; strike <= 3; strike++) {
                int before = mob.CurrentHP;
                OwnerLands(tesla, mob);
                AssertThat(before - mob.CurrentHP)
                    .OverrideFailureMessage($"Strike {strike} must deal the 8-damage synced arc.")
                    .IsEqual(LingeringKitRules.CoilSyncedArcDamage);
                AssertThat(coil.Charges).IsEqual(3 - strike);
            }
            AssertThat(coil.IsCoilDestroyed)
                .OverrideFailureMessage("At 0 charges the coil fizzles through its destroy path.")
                .IsTrue();

            int after = mob.CurrentHP;
            OwnerLands(tesla, mob);
            AssertThat(mob.CurrentHP).OverrideFailureMessage("A fizzled coil strikes no more.").IsEqual(after);
        });
    }

    [TestCase]
    public void TheSyncedArcHoldsTwentyFramesAndTheOwnArcAndFenceSpendNoCharge() {
        InHost("CoilOwnArc", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            TeslaCoilNode coil = Coil(host, tesla, Origin + new Vector2(150f, 0f));
            EnemyController mob = Mob(host, Origin + new Vector2(200f, 0f));

            // The coil's own arc (5 damage, every 120 frames) spends nothing, over many arcs.
            for (int frame = 0; frame < 125; frame++) coil._PhysicsProcess(Step);
            AssertThat(9999 - mob.CurrentHP).OverrideFailureMessage("The own arc is 5 damage.")
                .IsEqual(LingeringKitRules.CoilOwnArcDamage);
            AssertThat(coil.Charges).OverrideFailureMessage("An own arc spends no charge.").IsEqual(3);

            // A partner coil links a fence; its ticks spend no charge on either coil.
            TeslaCoilNode partner = Coil(host, tesla, Origin + new Vector2(250f, 0f));
            coil.LinkPartner(partner, drivesFence: true);
            partner.LinkPartner(coil, drivesFence: false);
            for (int frame = 0; frame < 130; frame++) { coil._PhysicsProcess(Step); partner._PhysicsProcess(Step); }
            AssertThat(coil.Charges).IsEqual(3);
            AssertThat(partner.Charges).IsEqual(3);

            // A fresh mob: the strike's stun is 20 frames (0.333 s).
            EnemyController fresh = Mob(host, Origin + new Vector2(180f, 40f));
            OwnerLands(tesla, fresh);
            AssertThat(fresh.CurrentState).IsEqual(EnemyState.Stunned);
            AssertFloat(StunSeconds(fresh)).IsEqualApprox(LingeringKitRules.CoilSyncedArcHitstunFrames / 60f, 0.001f);
        });
    }

    [TestCase]
    public void TwoCoilsInReachBothFireOnOneHitAndNeitherTriggersTheOther() {
        InHost("CoilsBoth", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            TeslaCoilNode first = Coil(host, tesla, Origin + new Vector2(120f, 0f));
            TeslaCoilNode second = Coil(host, tesla, Origin + new Vector2(260f, 0f));
            EnemyController mob = Mob(host, Origin + new Vector2(200f, 0f));
            int before = mob.CurrentHP;
            OwnerLands(tesla, mob);
            AssertThat(before - mob.CurrentHP)
                .OverrideFailureMessage("Both coils fire once each; a strike raising the seam must not trigger the other (no loop).")
                .IsEqual(2 * LingeringKitRules.CoilSyncedArcDamage);
            AssertThat(first.Charges).IsEqual(2);
            AssertThat(second.Charges).IsEqual(2);
        });
    }

    [TestCase]
    public void ACoilOutOfItsFourUnitReachDoesNotStrikeAndOneInsideDoes() {
        InHost("CoilReach", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            TeslaCoilNode coil = Coil(host, tesla, Origin);
            EnemyController far = Mob(host, Origin + new Vector2(300f, 0f));
            int before = far.CurrentHP;
            OwnerLands(tesla, far);
            AssertThat(far.CurrentHP).OverrideFailureMessage("5 units away is outside the 4-unit circle.").IsEqual(before);
            AssertThat(coil.Charges).IsEqual(3);

            // A target 4 units out on the diagonal is judged as a circle, not a box.
            EnemyController diagonal = Mob(host, Origin + new Vector2(200f, 200f));
            before = diagonal.CurrentHP;
            OwnerLands(tesla, diagonal);
            AssertThat(diagonal.CurrentHP).OverrideFailureMessage("A 2D circle: (200, 200) is 283 px, outside 240 px.").IsEqual(before);

            EnemyController near = Mob(host, Origin + new Vector2(100f, 0f));
            before = near.CurrentHP;
            OwnerLands(tesla, near);
            AssertThat(before - near.CurrentHP).IsEqual(LingeringKitRules.CoilSyncedArcDamage);
        });
    }

    [TestCase]
    public void NoWhiffTickConstructHitThrowUltimateOrPulseTriggersAStrike() {
        InHost("CoilNoTrigger", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            TeslaCoilNode coil = Coil(host, tesla, Origin + new Vector2(100f, 0f));
            EnemyController mob = Mob(host, Origin + new Vector2(150f, 0f));
            int before = mob.CurrentHP;
            // A whiff, a block or a refusal is not landed: the seam raises nothing at all.
            OwnerLands(tesla, mob, OwnerHit(landed: false));
            OwnerLands(tesla, mob, OwnerHit("tick", HitDelivery.Tick, HitOrigin.Special));
            OwnerLands(tesla, mob, OwnerHit("coil_arc", HitDelivery.Construct, HitOrigin.Special));
            OwnerLands(tesla, mob, OwnerHit("throw"));
            OwnerLands(tesla, mob, OwnerHit("burst", HitDelivery.DirectHit, HitOrigin.Ultimate));
            OwnerLands(tesla, mob, OwnerHit("pulse", HitDelivery.DirectHit, HitOrigin.Special));
            OwnerLands(tesla, mob, OwnerHit("chain_lightning", HitDelivery.DirectHit, HitOrigin.Special));
            AssertThat(mob.CurrentHP).IsEqual(before);
            AssertThat(coil.Charges).IsEqual(3);

            // A direct Special hit does trigger.
            OwnerLands(tesla, mob, OwnerHit("sonic_wave", HitDelivery.DirectHit, HitOrigin.Special));
            AssertThat(coil.Charges).IsEqual(2);
        });
    }

    [TestCase]
    public void ASyncedArcPaysAZeroPointZeroFivePipAndAnUnlandedOneNothing() {
        InHost("CoilMomentum", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            Coil(host, tesla, Origin + new Vector2(100f, 0f));
            EnemyController mob = Mob(host, Origin + new Vector2(150f, 0f));
            float start = tesla.MomentumPips;
            OwnerLands(tesla, mob);
            AssertFloat(tesla.MomentumPips - start).IsEqualApprox(0.05f, 0.0005f);
            // Invulnerable (a drill dummy): the strike does not land, so it pays nothing.
            mob.DrillInvulnerable = true;
            float paid = tesla.MomentumPips;
            OwnerLands(tesla, mob);
            AssertFloat(tesla.MomentumPips).IsEqualApprox(paid, 0.0005f);
        });
    }

    [TestCase]
    public void ARealSwingThatLandsFiresBothCoils() {
        InHost("CoilRealSwing", host => {
            try {
                var floor = new StaticBody2D { CollisionLayer = CollisionLayers.Environment, Position = Origin };
                floor.AddChild(new CollisionShape2D {
                    Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) }, Position = new Vector2(0f, 20f)
                });
                host.AddChild(floor);
                PlayerController tesla = Owner(host, "tesla", Origin + new Vector2(0f, -8f));
                for (int frame = 0; frame < 120 && !tesla.IsOnFloor(); frame++) {
                    tesla.Velocity = new Vector2(tesla.Velocity.X, 400f);
                    Tick(tesla, GameplayButtons.None);
                }
                for (int frame = 0; frame < 60 && tesla.CurrentState != CharacterState.Idle; frame++) Tick(tesla, GameplayButtons.None);
                TeslaCoilNode a = Coil(host, tesla, Origin + new Vector2(30f, 0f));
                TeslaCoilNode b = Coil(host, tesla, Origin + new Vector2(60f, 0f));
                EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));

                var box = tesla.GetNode<Hitbox>("MeleeHitbox");
                Tick(tesla, GameplayButtons.BasicAttack);
                for (int tick = 0; tick < 30 && !box.IsActive; tick++) Tick(tesla, GameplayButtons.None);
                AssertThat(box.IsActive).OverrideFailureMessage("Harness: the swing's box never opened.").IsTrue();
                int before = mob.CurrentHP;
                box.EmitSignal(Area2D.SignalName.AreaEntered, HurtboxOf(mob));
                // The swing's own damage plus two 8-damage arcs.
                AssertThat(a.Charges).IsEqual(2);
                AssertThat(b.Charges).IsEqual(2);
                AssertThat(before - mob.CurrentHP >= 2 * LingeringKitRules.CoilSyncedArcDamage).IsTrue();
            } finally {
                InputManager.Instance?.ClearInputSource(0);
            }
        });
    }

    [TestCase]
    public void ChargesSurviveTimeFreezeAndAFrozenCoilDoesNotStrike() {
        InHost("CoilFreeze", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            TeslaCoilNode coil = Coil(host, tesla, Origin + new Vector2(100f, 0f));
            EnemyController mob = Mob(host, Origin + new Vector2(150f, 0f));
            OwnerLands(tesla, mob);
            AssertThat(coil.Charges).IsEqual(2);
            coil.SetTimeFrozen(true);
            int before = mob.CurrentHP;
            OwnerLands(tesla, mob);
            AssertThat(mob.CurrentHP).OverrideFailureMessage("A frozen coil fires nothing.").IsEqual(before);
            coil.SetTimeFrozen(false);
            AssertThat(coil.Charges).OverrideFailureMessage("The freeze neither spends nor refills.").IsEqual(2);
            OwnerLands(tesla, mob);
            AssertThat(coil.Charges).IsEqual(1);
        });
    }

    [TestCase]
    public void APooledCoilResetsItsChargesAndStopsListeningWhenDespawned() {
        InHost("CoilPool", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            TeslaCoilNode coil = Coil(host, tesla, Origin + new Vector2(100f, 0f));
            EnemyController mob = Mob(host, Origin + new Vector2(150f, 0f));
            OwnerLands(tesla, mob);
            AssertThat(coil.Charges).IsEqual(2);

            coil.OnDespawn();
            AssertThat(coil.Charges).IsEqual(0);
            int before = mob.CurrentHP;
            OwnerLands(tesla, mob);
            AssertThat(mob.CurrentHP).OverrideFailureMessage("A despawned coil has unsubscribed.").IsEqual(before);

            // Re-initialised from the pool: full charges again, listening again.
            AbilityData data = AuthoredResources.Load<AbilityData>("res://resources/Abilities/tesla/special_1.tres");
            coil.Initialize(data, tesla, resonantOverdrive: false);
            AssertThat(coil.Charges).IsEqual(3);
            OwnerLands(tesla, mob);
            AssertThat(coil.Charges).IsEqual(2);

            // Leaving the tree unsubscribes too: a later hit reaches no freed handler.
            coil.GetParent().RemoveChild(coil);
            coil.Free();
            OwnerLands(tesla, mob);
        });
    }

    private static void Tick(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Step);
    }
}
