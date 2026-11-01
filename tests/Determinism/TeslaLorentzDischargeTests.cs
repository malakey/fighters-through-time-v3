using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The lingering-ability kit pass (decision 25, KL-SIM) - the Lorentz DISCHARGE, which replaced the
/// Package 12/13 mark chain in this file (the old suite pinned an 8-damage arc per eligible coil).
/// When Tesla's Lorentz Pulse fires, each of his coils within 8 u of him that has a recipient spends
/// ALL its remaining charges as one burst (6 damage per charge, 20 frames of hitstun) and fizzles. The
/// recipient is the opponent inside the coil's own 4 u, or a Conductive-marked opponent within 8 u of
/// the coil. The burst is a Detonation: 0.2 pip once per Pulse, and it arms D3 for the Pulse cast. Every
/// case compares with a control that casts the same Pulse with no coil.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TeslaLorentzDischargeTests {
    private const int Charges = LingeringKitRules.CoilCharges;

    [TestCase]
    public void ThePulseDischargesACoilForSixPerChargeAndTheCoilFizzles() {
        FighterSimulation withCoil = Cast(8201, coils: 1);
        FighterSimulation control = Cast(8201, coils: 0);
        AssertThat(Hp(control, 1) - Hp(withCoil, 1))
            .OverrideFailureMessage("One coil, all its charges at once.")
            .IsEqual(LingeringKitRules.DischargeDamagePerCharge * Charges);
        AssertThat(withCoil.PersistentObjectCount).OverrideFailureMessage("The coil fizzled.").IsEqual(0);
    }

    [TestCase]
    public void TheBurstScalesWithTheRemainingChargesAndEachCoilBursts() {
        FighterSimulation spent = Cast(8202, coils: 1, charges: 1);
        FighterSimulation control = Cast(8202, coils: 0);
        AssertThat(Hp(control, 1) - Hp(spent, 1)).IsEqual(LingeringKitRules.DischargeDamagePerCharge * 1);

        FighterSimulation two = Cast(8203, coils: 2);
        FighterSimulation twoControl = Cast(8203, coils: 0);
        AssertThat(Hp(twoControl, 1) - Hp(two, 1))
            .OverrideFailureMessage("Two coils, two bursts.")
            .IsEqual(2 * LingeringKitRules.DischargeDamagePerCharge * Charges);
    }

    [TestCase]
    public void AMarkedOpponentBeyondTheCoilsReachButWithinEightOfItIsHitAndAnUnmarkedOneIsNot() {
        FighterSimulation marked = CastAtRange(8204, targetX: 5, mark: true, coilOffset: 0f);
        FighterSimulation unmarked = CastAtRange(8204, targetX: 5, mark: false, coilOffset: 0f);
        AssertThat(Hp(unmarked, 1) - Hp(marked, 1))
            .OverrideFailureMessage("Marked, 6 u from the coil: the full discharge. Unmarked: nothing.")
            .IsEqual(LingeringKitRules.DischargeDamagePerCharge * Charges);
        AssertThat(marked.PersistentObjectCount).IsEqual(0);
        AssertThat(unmarked.PersistentObjectCount).OverrideFailureMessage("No recipient: the coil keeps its charges.").IsEqual(1);
    }

    [TestCase]
    public void ACoilBeyondEightUnitsOfTeslaNeverDischarges() {
        // Tesla at -1; the coil is moved to x 8 (9 away) with the opponent beside it.
        FighterSimulation far = CastAtRange(8205, targetX: 8, mark: false, coilOffset: 9f);
        far.TryGetFirstPersistentObject(out FighterPersistentObjectComponent coil);
        AssertThat(coil.RemainingAttacks).IsEqual(Charges);
        AssertThat(Hp(far, 1)).IsEqual(100);
    }

    [TestCase]
    public void ACoilWithARecipientSpendsEveryChargeAndFizzlesEvenWhenTheBurstIsRefusedAndPaysNothing() {
        FighterSimulation sim = Build(ProjectileBurstSimTests.Authored("tesla"), 8210);
        sim.TryGetFighter(0, out FighterStateComponent owner);
        ConstructOwnAttack.Spawn(sim, 0, FighterConstructCharges.CoilObjectTypeID, new FPVector2(owner.Position.x, owner.Position.y));
        // Beyond the Pulse itself but inside the coil's reach; invulnerable, so the burst is refused.
        sim.MutateFighterForTest(1, (ref FighterStateComponent state, ref FighterRuntimeComponent runtime, ref FighterVerbComponent verb) => {
            state.Position = new FPVector2(owner.Position.x + FP64.FromInt(3), state.Position.y);
            state.InvulnerabilityFrames = 300;
        });
        FighterSimulation control = Build(ProjectileBurstSimTests.Authored("tesla"), 8210);
        control.MutateFighterForTest(1, (ref FighterStateComponent state, ref FighterRuntimeComponent runtime, ref FighterVerbComponent verb) => {
            state.Position = new FPVector2(owner.Position.x + FP64.FromInt(3), state.Position.y);
            state.InvulnerabilityFrames = 300;
        });
        Run(sim, 40, GameplayButtons.Special2);
        Run(control, 40, GameplayButtons.Special2);
        AssertThat(sim.PersistentObjectCount).OverrideFailureMessage("A recipient existed: the coil spent its charges and fizzled.").IsEqual(0);
        AssertThat(Hp(sim, 1)).IsEqual(Hp(control, 1));
        AssertThat(MomentumTestKit.Units(sim, 0)).OverrideFailureMessage("A refused burst pays no Detonation.")
            .IsEqual(MomentumTestKit.Units(control, 0));
    }

    [TestCase]
    public void TheDischargeIsADetonationPayingTheMomentumOnceAndArmingD3() {
        FighterSimulation two = CastAtRange(8206, targetX: 5, mark: true, coilOffset: 0f, secondCoil: true);
        FighterSimulation control = CastAtRange(8206, targetX: 5, mark: true, coilOffset: 0f, noCoil: true);
        AssertThat(MomentumTestKit.Units(two, 0) - MomentumTestKit.Units(control, 0))
            .OverrideFailureMessage("Two coils burst; the Detonation pays 0.2 pip ONCE.")
            .IsEqual(LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.Detonation));
        // The opponent is outside the Pulse itself, so only the discharge could have landed in the cast.
        two.TryGetFighterActionState(0, out FighterActionStateComponent action);
        control.TryGetFighterActionState(0, out FighterActionStateComponent idle);
        AssertThat((action.SpecialFlags & FighterSpecialCastRules.FlagLandedDirectHit) != 0).IsTrue();
        AssertThat((idle.SpecialFlags & FighterSpecialCastRules.FlagLandedDirectHit) != 0).IsFalse();
    }

    [TestCase]
    public void ARollbackAcrossTheDischargeConvergesAndNeverDuplicatesIt() {
        FighterLoadout tesla = ProjectileBurstSimTests.Authored("tesla");
        FighterSimulation authoritative = Build(tesla, 8207);
        FighterSimulation predicted = Build(tesla, 8207);
        ConstructOwnAttack.Spawn(authoritative, 0, FighterConstructCharges.CoilObjectTypeID, new FPVector2(FP64.FromInt(-1), FP64.Zero));
        ConstructOwnAttack.Spawn(predicted, 0, FighterConstructCharges.CoilObjectTypeID, new FPVector2(FP64.FromInt(-1), FP64.Zero));
        for (int tick = 0; tick < 40; tick++) {
            PlayerInputFrame one = tick == 0 ? Frame(tick, GameplayButtons.Special2) : Frame(tick, GameplayButtons.None);
            PlayerInputFrame two = tick >= 2 && tick <= 12 ? Frame(tick, GameplayButtons.None, 127) : Frame(tick, GameplayButtons.None);
            authoritative.Advance(one, two);
            if (tick >= 2 && tick <= 12) predicted.AdvanceWithPredictedPlayerTwo(one);
            else predicted.Advance(one, two);
        }
        for (int tick = 2; tick <= 12; tick++) {
            AssertThat(predicted.CorrectPlayerTwoInput(tick, Frame(tick, GameplayButtons.None, 127))).IsTrue();
        }
        AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
        AssertThat(Hp(predicted, 1)).IsEqual(Hp(authoritative, 1));
        AssertThat(authoritative.PersistentObjectCount).IsEqual(0);
    }

    // --- helpers ------------------------------------------------------------------

    private static FighterSimulation Build(FighterLoadout tesla, int seed) =>
        new(tesla, FighterLoadout.Default(FighterCharacterID.Joan), seed: seed, spawnDistance: 1, rules: FighterMatchRules.Disabled);

    /// <summary>Tesla casts the Pulse beside the opponent with <paramref name="coils"/> coils at his feet.</summary>
    private static FighterSimulation Cast(int seed, int coils, int charges = Charges) {
        FighterSimulation sim = Build(ProjectileBurstSimTests.Authored("tesla"), seed);
        sim.TryGetFighter(0, out FighterStateComponent owner);
        for (int i = 0; i < coils; i++) {
            ConstructOwnAttack.Spawn(sim, 0, FighterConstructCharges.CoilObjectTypeID, new FPVector2(owner.Position.x + FP64.FromDouble(0.1 * i), owner.Position.y));
            sim.MutatePersistentForTest(i, (ref FighterPersistentObjectComponent coil) => coil.RemainingAttacks = charges);
        }
        Run(sim, 40, GameplayButtons.Special2);
        return sim;
    }

    /// <summary>The opponent stands at <paramref name="targetX"/> (outside the Pulse itself when far); the coil sits at Tesla's feet shifted by <paramref name="coilOffset"/>.</summary>
    private static FighterSimulation CastAtRange(
        int seed, int targetX, bool mark, float coilOffset, bool secondCoil = false, bool noCoil = false) {
        FighterSimulation sim = Build(ProjectileBurstSimTests.Authored("tesla"), seed);
        sim.TryGetFighter(0, out FighterStateComponent owner);
        sim.MutateFighterForTest(1, (ref FighterStateComponent state, ref FighterRuntimeComponent runtime, ref FighterVerbComponent verb) =>
            state.Position = new FPVector2(FP64.FromInt(targetX), state.Position.y));
        if (!noCoil) {
            int coils = secondCoil ? 2 : 1;
            for (int i = 0; i < coils; i++) {
                ConstructOwnAttack.Spawn(sim, 0, FighterConstructCharges.CoilObjectTypeID,
                    new FPVector2(owner.Position.x + FP64.FromDouble(coilOffset + 0.1 * i), owner.Position.y));
            }
        }
        if (mark) sim.SeedFighterConductiveForTest(1, 0, BasicComboRules.ConductiveMarkBaselineFrames);
        Run(sim, 40, GameplayButtons.Special2);
        return sim;
    }

    private static void Run(FighterSimulation sim, int ticks, GameplayButtons firstPress) {
        for (int i = 0; i < ticks; i++) {
            GameplayButtons press = i == 0 ? firstPress : GameplayButtons.None;
            sim.Advance(Frame(sim.CurrentTick, press), Frame(sim.CurrentTick, GameplayButtons.None));
        }
    }

    private static int Hp(FighterSimulation sim, int player) {
        sim.TryGetFighter(player, out FighterStateComponent state);
        return state.CurrentHP;
    }

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed, sbyte moveX = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = pressed,
        Pressed = pressed
    };
}
