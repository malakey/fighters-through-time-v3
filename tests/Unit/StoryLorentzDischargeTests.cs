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
/// Lingering-kit pass (KL-SCON, decision 25 and plan 1.2) — the Story Lorentz discharge, which replaces the
/// 8-damage mark chain: when the Pulse fires, each of Tesla's coils within 8 units of him spends ALL its remaining
/// charges as one burst (6 damage x charges, 20 frames of hitstun) on the nearest enemy within the coil's 4 units, or on a
/// Conductive-marked enemy within 8 units of the coil, and then fizzles. The burst counts as combo: the cast
/// arms D3 and the Pulse pays one Detonation (0.2 pip) however many coils discharge. The Cataclysm still
/// detonates a coil whatever its charges.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryLorentzDischargeTests {
    private static readonly Vector2 Origin = new(-74000f, 600f);

    [TestCase]
    public void TheDischargeIsSixDamagePerRemainingChargeOnTheNearestEnemyThenTheCoilFizzles() {
        InHost("DischargeScale", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            TeslaCoilNode full = Coil(host, tesla, Origin + new Vector2(300f, 0f));
            TeslaCoilNode spent = Coil(host, tesla, Origin + new Vector2(-300f, 0f));
            // Two strikes' worth of charges spent on a decoy, leaving one.
            EnemyController decoy = Mob(host, Origin + new Vector2(-300f, -200f));
            OwnerLands(tesla, decoy);
            OwnerLands(tesla, decoy);
            AssertThat(spent.Charges).IsEqual(1);

            EnemyController eastMob = Mob(host, Origin + new Vector2(400f, 0f));
            EnemyController westMob = Mob(host, Origin + new Vector2(-400f, 0f));
            int eastBefore = eastMob.CurrentHP;
            int westBefore = westMob.CurrentHP;
            Pulse(tesla);
            AssertThat(eastBefore - eastMob.CurrentHP)
                .OverrideFailureMessage("A full coil bursts 6 x 3 = 18 (the mob stands outside the 2.5-unit Pulse).")
                .IsEqual(LingeringKitRules.DischargeDamagePerCharge * 3);
            AssertThat(westBefore - westMob.CurrentHP)
                .OverrideFailureMessage("A coil with one charge left bursts 6.")
                .IsEqual(LingeringKitRules.DischargeDamagePerCharge);
            AssertThat(full.IsCoilDestroyed).OverrideFailureMessage("The coil fizzles after its burst.").IsTrue();
            AssertThat(spent.IsCoilDestroyed).IsTrue();
        });
    }

    [TestCase]
    public void TheOldMarkChainIsGoneSoAMarkedMobInThePulsePaysThePulseAndTheBurstOnly() {
        InHost("DischargeNoChain", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            Coil(host, tesla, Origin + new Vector2(200f, 0f));
            EnemyController marked = Mob(host, Origin + new Vector2(80f, 0f));
            marked.ApplyConductiveMark(sourcePlayerID: 0, frames: BasicComboRules.ConductiveMarkBaselineFrames);
            int before = marked.CurrentHP;
            Pulse(tesla);
            int loss = before - marked.CurrentHP;
            // The pulse itself (a control run with no coil) plus 18; the old 8-damage arc is not added.
            int pulseOnly = ControlPulseLoss(host);
            AssertThat(loss - pulseOnly)
                .OverrideFailureMessage($"Pulse {pulseOnly} + one 18 burst, no chain arc (loss {loss}).")
                .IsEqual(LingeringKitRules.DischargeDamagePerCharge * LingeringKitRules.CoilCharges);
        });
    }

    [TestCase]
    public void ACoilBeyondEightUnitsOfTeslaOrWithNoTargetKeepsItsCharges() {
        InHost("DischargeRange", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            TeslaCoilNode farCoil = Coil(host, tesla, Origin + new Vector2(700f, 0f));
            EnemyController nearFar = Mob(host, Origin + new Vector2(760f, 0f));
            TeslaCoilNode idleCoil = Coil(host, tesla, Origin + new Vector2(-300f, 0f));
            int before = nearFar.CurrentHP;
            Pulse(tesla);
            AssertThat(nearFar.CurrentHP).OverrideFailureMessage("A coil 11.7 units from Tesla is out of the 8-unit discharge range.").IsEqual(before);
            AssertThat(farCoil.Charges).IsEqual(3);
            AssertThat(idleCoil.IsCoilDestroyed).OverrideFailureMessage("No target: the coil keeps its charges.").IsFalse();
            AssertThat(idleCoil.Charges).IsEqual(3);
        });
    }

    [TestCase]
    public void AMarkedEnemyWithinEightUnitsOfTheCoilIsHitButAnUnmarkedOneOutsideFourIsNot() {
        InHost("DischargeUnmarked", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            TeslaCoilNode coil = Coil(host, tesla, Origin + new Vector2(200f, 0f));
            EnemyController unmarked = Mob(host, Origin + new Vector2(200f, 380f));
            int unmarkedBefore = unmarked.CurrentHP;
            Pulse(tesla);
            AssertThat(unmarked.CurrentHP).OverrideFailureMessage("6.3 units from the coil and unmarked: not a target.").IsEqual(unmarkedBefore);
            AssertThat(coil.Charges).IsEqual(3);
        });
        InHost("DischargeMarked", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            TeslaCoilNode coil = Coil(host, tesla, Origin + new Vector2(200f, 0f));
            EnemyController marked = Mob(host, Origin + new Vector2(200f, 380f));
            marked.ApplyConductiveMark(sourcePlayerID: 0, frames: BasicComboRules.ConductiveMarkBaselineFrames);
            int markedBefore = marked.CurrentHP;
            Pulse(tesla);
            AssertThat(markedBefore - marked.CurrentHP)
                .OverrideFailureMessage("A marked enemy within 8 units of the coil takes the burst from anywhere in that range.")
                .IsEqual(LingeringKitRules.DischargeDamagePerCharge * 3);
            AssertThat(coil.IsCoilDestroyed).IsTrue();
        });
    }

    [TestCase]
    public void TheDischargePaysOneDetonationPerPulseAndArmsD3ForTheCast() {
        InHost("DischargeRewards", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            Coil(host, tesla, Origin + new Vector2(300f, 0f));
            Coil(host, tesla, Origin + new Vector2(-300f, 0f));
            Mob(host, Origin + new Vector2(400f, 0f));
            Mob(host, Origin + new Vector2(-400f, 0f));
            var pulse = tesla.GetNode<TeslaLorentzPulse>("Special2");
            float before = tesla.MomentumPips;
            Pulse(tesla);
            // The pip is spent by the input path, not by TryExecute: only the one 0.2-pip Detonation shows,
            // however many coils discharged.
            AssertFloat(tesla.MomentumPips - before)
                .OverrideFailureMessage("+0.2 for the detonation, once.")
                .IsEqualApprox(1440f / 7200f, 0.0005f);
            AssertThat(pulse.LandedDirectHitThisExecution)
                .OverrideFailureMessage("A landed burst inside the cast arms D3 (CountsAsCombo).")
                .IsTrue();
        });
    }

    [TestCase]
    public void ABurstThatDoesNotLandPaysNothingAndLeavesD3Unarmed() {
        InHost("DischargeRefused", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            TeslaCoilNode coil = Coil(host, tesla, Origin + new Vector2(300f, 0f));
            EnemyController mob = Mob(host, Origin + new Vector2(400f, 0f));
            mob.DrillInvulnerable = true;
            var pulse = tesla.GetNode<TeslaLorentzPulse>("Special2");
            float before = tesla.MomentumPips;
            Pulse(tesla);
            AssertFloat(tesla.MomentumPips - before).IsEqualApprox(0f, 0.0005f);
            AssertThat(pulse.LandedDirectHitThisExecution).IsFalse();
            AssertThat(coil.IsCoilDestroyed).IsTrue();
        });
    }

    [TestCase]
    public void TheCataclysmStillDetonatesACoilWhateverItsCharges() {
        InHost("DischargeCataclysm", host => {
            PlayerController tesla = Owner(host, "tesla", Origin);
            TeslaCoilNode coil = Coil(host, tesla, Origin + new Vector2(300f, 0f));
            EnemyController decoy = Mob(host, Origin + new Vector2(330f, 0f));
            OwnerLands(tesla, decoy);
            OwnerLands(tesla, decoy);
            AssertThat(coil.Charges).IsEqual(1);
            EnemyController mob = Mob(host, Origin + new Vector2(310f, 0f));
            int before = mob.CurrentHP;
            coil.Explode();
            AssertThat(before - mob.CurrentHP).IsEqual(UltimateActivationRules.CoilDetonationDamage);
            AssertThat(coil.IsCoilDestroyed).IsTrue();
        });
    }

    /// <summary>Casts the Pulse and ticks the cast to its end.</summary>
    private static void Pulse(PlayerController tesla) {
        var pulse = tesla.GetNode<TeslaLorentzPulse>("Special2");
        AssertThat(pulse.TryExecute()).IsTrue();
        for (int frame = 0; frame < 120 && pulse.IsExecuting; frame++) pulse._PhysicsProcess(Step);
    }

    /// <summary>What the bare Pulse (no coil) takes off a mob 80 px from a fresh Tesla.</summary>
    private static int ControlPulseLoss(Node2D host) {
        Node2D control = NewHost("DischargeControl");
        try {
            Vector2 at = Origin + new Vector2(0f, 3000f);
            PlayerController tesla = Owner(control, "tesla", at);
            EnemyController mob = Mob(control, at + new Vector2(80f, 0f));
            int before = mob.CurrentHP;
            Pulse(tesla);
            return before - mob.CurrentHP;
        } finally {
            control.Free();
        }
    }
}
