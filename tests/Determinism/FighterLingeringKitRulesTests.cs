using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The lingering-kit pass's Fighter-sim half of KL-RULES: the fixed-point view of the rulebook, the
/// owed-strike storage (310 <c>OwedSyncedStrikes</c>) written at the basic and direct-Special landed
/// sites and read-and-cleared by the persistent-object system, the <c>CountsAsCombo</c> call, Venom's
/// 3 HP per second and healing block (Rally reclaim and the orb; Defy's heal untouched), and the x0.4
/// hero slow from the three <c>.tres</c> files.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterLingeringKitRulesTests {

    // === the fixed-point view =================================================

    [TestCase]
    public void TheSimValuesAreTheRulebookValuesInIntegerRationals() {
        AssertThat(FighterLingeringKitRules.FromMilli(2500).RawValue).IsEqual((FP64.FromInt(5) / FP64.FromInt(2)).RawValue);
        AssertThat(FighterLingeringKitRules.VortexPullPerFrame.RawValue)
            .IsEqual((FP64.FromInt(7) / FP64.FromInt(60)).RawValue);
        AssertThat(FighterLingeringKitRules.GoldenRatioPullFraction.RawValue)
            .IsEqual((FP64.FromInt(1) / FP64.FromInt(2)).RawValue);
        AssertThat(FighterLingeringKitRules.GoldenRatioPullCap.RawValue)
            .IsEqual((FP64.FromInt(3) / FP64.FromInt(2)).RawValue);
        AssertThat(FighterLingeringKitRules.TurretSyncedBoltSpeedPerFrame.RawValue)
            .IsEqual((FP64.FromInt(24) / FP64.FromInt(60)).RawValue);
        AssertThat(FighterLingeringKitRules.CoilSyncedReachSquared.RawValue).IsEqual(FP64.FromInt(16).RawValue);
        AssertThat(FighterLingeringKitRules.TurretSyncedReachSquared.RawValue).IsEqual(FP64.FromInt(100).RawValue);
        AssertThat(FighterLingeringKitRules.DischargeOwnerRangeSquared.RawValue).IsEqual(FP64.FromInt(64).RawValue);
    }

    [TestCase]
    public void SyncedReachIsATwoDimensionalCircle() {
        FPVector2 coil = new(FP64.Zero, FP64.Zero);
        FP64 reach = FighterLingeringKitRules.CoilSyncedReachSquared;
        AssertThat(FighterLingeringKitRules.WithinReach(in coil, new FPVector2(FP64.FromInt(4), FP64.Zero), reach)).IsTrue();
        // x alone is inside 4 u, but the 3-3 corner is 4.24 u away: outside the circle.
        AssertThat(FighterLingeringKitRules.WithinReach(in coil, new FPVector2(FP64.FromInt(3), FP64.FromInt(3)), reach))
            .OverrideFailureMessage("The synced reach is a circle, not an x-only test.")
            .IsFalse();
        AssertThat(FighterLingeringKitRules.WithinReach(in coil, new FPVector2(FP64.FromInt(2), FP64.FromInt(3)), reach)).IsTrue();
    }

    // === the owed strike ======================================================

    [TestCase]
    public void ALandedBasicHitOwesOneStrikeAndNothingElseDoes() {
        FighterRuntimeComponent runtime = default;
        FighterVerbComponent verb = default;

        FighterCombatSystem.OnSwingResolved(ref runtime, ref verb, FighterHitOutcome.Landed, 6, 360);
        AssertThat(FighterLingeringKitRules.OwedStrikes(in verb)).IsEqual(1);
        FighterCombatSystem.OnSwingResolved(ref runtime, ref verb, FighterHitOutcome.Absorbed, 6, 360);
        AssertThat(FighterLingeringKitRules.OwedStrikes(in verb)).IsEqual(2);

        verb = default;
        FighterCombatSystem.OnSwingResolved(ref runtime, ref verb, FighterHitOutcome.Refused, 6, 360);
        FighterCombatSystem.OnSwingResolved(ref runtime, ref verb, FighterHitOutcome.Blocked, 6, 360);
        FighterCombatSystem.OnSwingResolved(ref runtime, ref verb, FighterHitOutcome.Landed, 0, 360);
        AssertThat(verb.OwedSyncedStrikes)
            .OverrideFailureMessage("A whiff, a block and a zero-damage contact owe nothing.")
            .IsEqual(0);
    }

    [TestCase]
    public void ADirectSpecialHitOwesAStrikeButATickAConstructHitAndAZeroDamageHitDoNot() {
        var direct = new FighterAbilityHitData { Delivery = (int)HitDelivery.DirectHit };
        var tick = new FighterAbilityHitData { Delivery = (int)HitDelivery.Tick };
        var construct = new FighterAbilityHitData { Delivery = (int)HitDelivery.Construct };
        FighterVerbComponent verb = default;

        FighterLingeringKitRules.NoteOwnerSpecialHit(ref verb, FighterHitOutcome.Landed, 18, in direct);
        AssertThat(verb.OwedSyncedStrikes).IsEqual(1);
        FighterLingeringKitRules.NoteOwnerSpecialHit(ref verb, FighterHitOutcome.Absorbed, 18, in direct);
        AssertThat(verb.OwedSyncedStrikes).IsEqual(2);

        verb = default;
        FighterLingeringKitRules.NoteOwnerSpecialHit(ref verb, FighterHitOutcome.Landed, 3, in tick);
        FighterLingeringKitRules.NoteOwnerSpecialHit(ref verb, FighterHitOutcome.Landed, 4, in construct);
        FighterLingeringKitRules.NoteOwnerSpecialHit(ref verb, FighterHitOutcome.Landed, 0, in direct);
        FighterLingeringKitRules.NoteOwnerSpecialHit(ref verb, FighterHitOutcome.Blocked, 18, in direct);
        FighterLingeringKitRules.NoteOwnerSpecialHit(ref verb, FighterHitOutcome.Refused, 18, in direct);
        AssertThat(verb.OwedSyncedStrikes).IsEqual(0);
    }

    [TestCase]
    public void TheOwedCountSaturatesAndTheReaderClearsIt() {
        FighterVerbComponent verb = default;
        for (int i = 0; i < 300; i++) FighterLingeringKitRules.NoteOwedStrike(ref verb);
        AssertThat(FighterLingeringKitRules.OwedStrikes(in verb)).IsEqual(FighterLingeringKitRules.OwedStrikeCap);
        AssertThat(FighterLingeringKitRules.TakeOwedStrikes(ref verb)).IsEqual(255);
        AssertThat(verb.OwedSyncedStrikes).IsEqual(0);
        AssertThat(FighterLingeringKitRules.TakeOwedStrikes(ref verb)).IsEqual(0);
    }

    [TestCase]
    public void ThePersistentObjectSystemClearsTheOwedCountEveryTick() {
        var simulation = new FighterSimulation(
            FighterCharacterID.Joan, FighterCharacterID.Joan, seed: 7, spawnDistance: 4, rules: FighterMatchRules.Disabled);
        AssertThat(simulation.SeedFighterOwedStrikesForTest(0, 5)).IsTrue();
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent seeded)).IsTrue();
        AssertThat(seeded.OwedSyncedStrikes).IsEqual(5);

        simulation.Advance(Frame(0), Frame(0));
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent after)).IsTrue();
        AssertThat(after.OwedSyncedStrikes)
            .OverrideFailureMessage("The count must never accumulate across ticks.")
            .IsEqual(0);
    }

    // === CountsAsCombo ========================================================

    [TestCase]
    public void CountsAsComboArmsD3OnlyInsideTheOwnersCastAndPaysOncePerEvent() {
        var cast = new FighterActionStateComponent {
            SpecialSlot = FighterHitContractTable.SlotSpecialTwo,
            SpecialPhase = 3,
            SpecialCastId = 11
        };
        FighterRuntimeComponent runtime = default;
        FighterVerbComponent verb = default;
        verb.MomentumUnits = 0;

        // The right slot and cast: D3 arms and the detonation pays 0.2 pip.
        bool counted = FighterLingeringKitRules.CountsAsCombo(
            ref cast, ref verb, FighterHitContractTable.SlotSpecialTwo, 11,
            FighterHitOutcome.Landed, 8, LingeringMomentumEvent.Detonation);
        AssertThat(counted).IsTrue();
        AssertThat(FighterSpecialCancelRules.LandedDirectHit(in cast)).IsTrue();
        AssertThat(verb.MomentumUnits).IsEqual(LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.Detonation));
        AssertThat(runtime.SwingLanded)
            .OverrideFailureMessage("It must not set the D1 swing latch C.")
            .IsEqual(0);

        // A second body of the same detonation pays nothing more.
        FighterLingeringKitRules.CountsAsCombo(
            ref cast, ref verb, FighterHitContractTable.SlotSpecialTwo, 11,
            FighterHitOutcome.Landed, 8, LingeringMomentumEvent.Detonation, payoutAlreadyPaid: true);
        AssertThat(verb.MomentumUnits).IsEqual(LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.Detonation));

        // An earlier cast's late hit does not arm a later cast, but a landed strike still pays.
        cast.SpecialFlags = 0;
        FighterLingeringKitRules.CountsAsCombo(
            ref cast, ref verb, FighterHitContractTable.SlotSpecialTwo, 3,
            FighterHitOutcome.Landed, 6, LingeringMomentumEvent.SyncedStrike);
        AssertThat(FighterSpecialCancelRules.LandedDirectHit(in cast))
            .OverrideFailureMessage("Cast attribution: a stale cast id never arms D3.")
            .IsFalse();
        AssertThat(verb.MomentumUnits).IsEqual(LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.Detonation) + LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.SyncedStrike));

        // A blocked, refused or zero-damage contact pays nothing.
        int before = verb.MomentumUnits;
        FighterLingeringKitRules.CountsAsCombo(ref cast, ref verb, 2, 11, FighterHitOutcome.Blocked, 8, LingeringMomentumEvent.Detonation);
        FighterLingeringKitRules.CountsAsCombo(ref cast, ref verb, 2, 11, FighterHitOutcome.Refused, 8, LingeringMomentumEvent.Detonation);
        bool zero = FighterLingeringKitRules.CountsAsCombo(ref cast, ref verb, 2, 11, FighterHitOutcome.Landed, 0, LingeringMomentumEvent.Detonation);
        AssertThat(zero).IsFalse();
        AssertThat(verb.MomentumUnits).IsEqual(before);
    }

    // === Venom ================================================================

    [TestCase]
    public void VenomDealsThreeHPASecondInTheSim() {
        var simulation = new FighterSimulation(
            FighterCharacterID.Joan, FighterCharacterID.Joan, seed: 3, spawnDistance: 8, rules: FighterMatchRules.Disabled);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent start)).IsTrue();
        AssertThat(simulation.SeedFighterStatusForTest(1, (int)StatusType.Venom, 600, FP64.One)).IsTrue();
        int firstLoss = -1;
        for (int tick = 0; tick < 70; tick++) {
            simulation.Advance(Frame(tick), Frame(tick));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent now)).IsTrue();
            if (now.CurrentHP < start.CurrentHP && firstLoss < 0) firstLoss = start.CurrentHP - now.CurrentHP;
        }
        AssertThat(firstLoss).OverrideFailureMessage("The Venom tick never landed.").IsEqual(3);
    }

    [TestCase]
    public void VenomBlocksRallyReclaimAndBurnsNoPoolButDefyStillHeals() {
        // The reclaim of a landed direct hit: heals and debits the pool when healthy ...
        (FighterStateComponent healthy, FighterVerbComponent healthyVerb) = Reclaim(venomous: false);
        AssertThat(healthy.CurrentHP > 50).OverrideFailureMessage("Control: the reclaim must heal.").IsTrue();
        AssertThat(healthyVerb.EchoPool < FP64.FromInt(20)).IsTrue();

        // ... and does neither while poisoned.
        (FighterStateComponent poisoned, FighterVerbComponent poisonedVerb) = Reclaim(venomous: true);
        AssertThat(poisoned.CurrentHP).OverrideFailureMessage("Venom must block the Rally reclaim.").IsEqual(50);
        AssertThat(poisonedVerb.EchoPool.RawValue)
            .OverrideFailureMessage("A blocked reclaim burns no pool.")
            .IsEqual(FP64.FromInt(20).RawValue);

        // Defy's heal is the one exception.
        FighterStateComponent defier = Fighter(0);
        defier.CurrentHP = 50;
        FighterVerbComponent defierVerb = default;
        defierVerb.EchoPool = FP64.FromInt(20);
        int healed = FighterDefyBlastRules.Heal(ref defier, ref defierVerb);
        AssertThat(healed).OverrideFailureMessage("Defy still heals under Venom.").IsEqual(20);
        AssertThat(defier.CurrentHP).IsEqual(70);
    }

    [TestCase]
    public void VenomBlocksTheHealingOrb() {
        FighterStateComponent fighter = Fighter(0);
        fighter.CurrentHP = 40;
        FighterDefenseComponent defense = default;
        FighterRuntimeComponent clean = default;
        FighterOrbSystem.ApplyEffect(ref fighter, ref clean, ref defense, 0);
        AssertThat(fighter.CurrentHP).OverrideFailureMessage("Control: the orb heals 20%.").IsEqual(60);

        fighter.CurrentHP = 40;
        FighterRuntimeComponent poisoned = Poisoned();
        FighterOrbSystem.ApplyEffect(ref fighter, ref poisoned, ref defense, 0);
        AssertThat(fighter.CurrentHP).OverrideFailureMessage("Venom must block the healing orb.").IsEqual(40);
        AssertThat(FighterDamageRules.CanHeal(in poisoned)).IsFalse();
        AssertThat(FighterDamageRules.CanHeal(in clean)).IsTrue();
    }

    // === the slow =============================================================

    [TestCase]
    public void TheThreeHeroSlowsAreTheSameFortyPercentSlowFromTheirResources() {
        string[] paths = {
            "res://resources/Abilities/einstein/special_2.tres",
            "res://resources/Abilities/cleopatra/special_2.tres",
            "res://resources/Abilities/shakespeare/special_1.tres"
        };
        foreach (string path in paths) {
            AbilityData data = AuthoredResources.Load<AbilityData>(path);
            AssertThat(data.AppliedStatus).OverrideFailureMessage(path).IsEqual(StatusType.TimeDilation);
            AssertThat(data.StatusIntensity).OverrideFailureMessage(path).IsEqual((float)LingeringKitRules.HeroSlowIntensity);
            AssertThat(data.StatusDuration).OverrideFailureMessage(path).IsEqual((float)LingeringKitRules.HeroSlowLingerSeconds);
        }

        double free = Travel(slowed: false);
        double slow = Travel(slowed: true);
        double ratio = slow / free;
        AssertThat(ratio > 0.35 && ratio < 0.45)
            .OverrideFailureMessage($"A hero-slowed fighter should cover about x0.4 of the ground, covered x{ratio:0.###}.")
            .IsTrue();
    }

    // === helpers ==============================================================

    private static double Travel(bool slowed) {
        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/joan_data.tres"));
        var simulation = new FighterSimulation(loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            spawnDistance: 18, rules: FighterMatchRules.Disabled);
        AbilityData rift = AuthoredResources.Load<AbilityData>("res://resources/Abilities/einstein/special_2.tres");
        if (slowed) {
            AssertThat(simulation.SeedFighterStatusForTest(
                0, (int)StatusType.TimeDilation, 600, FP64.FromDouble(rift.StatusIntensity))).IsTrue();
        }
        int tick = 0;
        for (; tick < 10; tick++) simulation.Advance(Frame(tick), Frame(tick));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
        for (int i = 0; i < 40; i++, tick++) simulation.Advance(Frame(tick, 127), Frame(tick));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent after)).IsTrue();
        return (after.Position.x - before.Position.x).ToFloat();
    }

    private static (FighterStateComponent attacker, FighterVerbComponent verb) Reclaim(bool venomous) {
        FighterStateComponent attacker = Fighter(0);
        attacker.CurrentHP = 50;
        FighterRuntimeComponent attackerRuntime = venomous ? Poisoned() : default;
        FighterVerbComponent attackerVerb = default;
        attackerVerb.EchoPool = FP64.FromInt(20);
        FighterStateComponent target = Fighter(1);
        FighterRuntimeComponent targetRuntime = default;
        FighterVerbComponent targetVerb = default;
        FighterDefenseComponent targetDefense = default;
        FighterTuningComponent tuning = default;
        FighterDamageRules.ApplyFighterHit(
            ref attacker, ref attackerRuntime, ref attackerVerb,
            ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in tuning,
            FighterDamageRules.SpecialAttackClass,
            10, FP64.Zero, 0,
            (int)StatusType.None, 0, FP64.One,
            target.Position.x - FP64.One,
            appliesHitstop: false);
        return (attacker, attackerVerb);
    }

    private static FighterRuntimeComponent Poisoned() {
        FighterRuntimeComponent runtime = default;
        runtime.DamageStatusType = (int)StatusType.Venom;
        runtime.DamageStatusFrames = 120;
        runtime.DamageStatusIntensity = FP64.One;
        return runtime;
    }

    private static FighterStateComponent Fighter(int playerID) => new() {
        PlayerID = playerID,
        Stocks = 3,
        MaxHP = 100,
        CurrentHP = 100,
        BlockCharges = 3,
        IsGrounded = 1,
        FacingRight = 1
    };

    private static PlayerInputFrame Frame(int tick, sbyte moveX = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX
    };
}
