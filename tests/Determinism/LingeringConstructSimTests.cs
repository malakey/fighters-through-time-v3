using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The lingering-ability kit pass (KL-SIM, 2026-10-31), constructs in the Fighter sim: charges, the
/// synced strikes the owner's landed hits fire, the weak slow own attacks that spend no charge, the
/// turret that no longer self-destructs, and the nest's nip and lunge. A synced strike is driven by
/// the owed count on 310 (<c>SeedFighterOwedStrikesForTest</c>) in most cases, and by real swings in
/// the integration cases. Joan vs Joan duels, 2 units apart.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LingeringConstructSimTests {
    private const int CoilType = FighterConstructCharges.CoilObjectTypeID;
    private const int TurretType = FighterConstructCharges.TurretObjectTypeID;
    private const int NestType = FighterConstructCharges.NestObjectTypeID;

    [TestCase]
    public void ACoilFiresOneSyncedArcPerOwedHitSpendsACharge() {
        FighterSimulation sim = Duel(8101);
        PlaceConstruct(sim, 0, CoilType, 0f);
        int hpBefore = Hp(sim, 1);
        int momentumBefore = MomentumTestKit.Units(sim, 0);
        FighterSimulation control = Duel(8101);
        PlaceConstruct(control, 0, CoilType, 0f);
        sim.SeedFighterOwedStrikesForTest(0, 1);
        Step(sim, 1);
        Step(control, 1);

        AssertThat(Hp(sim, 1)).IsEqual(hpBefore - LingeringKitRules.CoilSyncedArcDamage);
        AssertThat(Hp(control, 1)).IsEqual(hpBefore);
        sim.TryGetFighter(1, out FighterStateComponent target);
        AssertThat(target.HitstunFrames).IsEqual(LingeringKitRules.CoilSyncedArcHitstunFrames);
        sim.TryGetFirstPersistentObject(out FighterPersistentObjectComponent coil);
        AssertThat(coil.RemainingAttacks).IsEqual(LingeringKitRules.CoilCharges - 1);
        AssertThat(MomentumTestKit.Units(sim, 0) - MomentumTestKit.Units(control, 0))
            .OverrideFailureMessage("A synced strike pays 0.05 pip once.")
            .IsEqual(LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.SyncedStrike));
        AssertThat(OwedOf(sim, 0)).OverrideFailureMessage("The owed count is read and cleared.").IsEqual(0);
    }

    [TestCase]
    public void TwoCoilsInReachBothFire() {
        FighterSimulation sim = Duel(8102);
        PlaceConstruct(sim, 0, CoilType, 0f);
        PlaceConstruct(sim, 0, CoilType, 0.2f);
        int hpBefore = Hp(sim, 1);
        sim.SeedFighterOwedStrikesForTest(0, 1);
        Step(sim, 1);
        AssertThat(Hp(sim, 1)).IsEqual(hpBefore - 2 * LingeringKitRules.CoilSyncedArcDamage);
        var coils = new System.Collections.Generic.List<FighterPersistentObjectComponent>();
        sim.CopyPersistentObjectsTo(coils);
        AssertThat(coils.Count).IsEqual(2);
        AssertThat(coils[0].RemainingAttacks).IsEqual(2);
        AssertThat(coils[1].RemainingAttacks).IsEqual(2);
    }

    [TestCase]
    public void ACoilBeyondItsFourUnitReachDoesNotFire() {
        FighterSimulation sim = Duel(8103, spawn: 3); // 6 units apart
        PlaceConstruct(sim, 0, CoilType, 0f);
        int hpBefore = Hp(sim, 1);
        sim.SeedFighterOwedStrikesForTest(0, 1);
        Step(sim, 1);
        AssertThat(Hp(sim, 1)).IsEqual(hpBefore);
        sim.TryGetFirstPersistentObject(out FighterPersistentObjectComponent coil);
        AssertThat(coil.RemainingAttacks).OverrideFailureMessage("A coil out of reach spends nothing.").IsEqual(LingeringKitRules.CoilCharges);
    }

    [TestCase]
    public void ThreeLandedHitsSpendThreeChargesAndTheCoilFizzles() {
        FighterSimulation sim = Duel(8104);
        PlaceConstruct(sim, 0, CoilType, 0f);
        int hpBefore = Hp(sim, 1);
        sim.SeedFighterOwedStrikesForTest(0, 3);
        Step(sim, 1);
        AssertThat(Hp(sim, 1)).IsEqual(hpBefore - 3 * LingeringKitRules.CoilSyncedArcDamage);
        Step(sim, 1);
        AssertThat(sim.PersistentObjectCount).OverrideFailureMessage("0 charges: the coil fizzles through the destroy path.").IsEqual(0);
    }

    [TestCase]
    public void ACoilsOwnArcSpendsNoCharge() {
        FighterSimulation sim = Duel(8105);
        PlaceConstruct(sim, 0, CoilType, 0f);
        int hpBefore = Hp(sim, 1);
        Step(sim, ConstructOwnAttack.IntervalFrames(CoilType) + 10);
        AssertThat(Hp(sim, 1)).OverrideFailureMessage("The coil's own arc landed.").IsEqual(hpBefore - ConstructOwnAttack.Damage(CoilType));
        sim.TryGetFirstPersistentObject(out FighterPersistentObjectComponent coil);
        AssertThat(coil.RemainingAttacks).IsEqual(LingeringKitRules.CoilCharges);
    }

    [TestCase]
    public void AStrikeNeverShortensOrEndsALaunchButLengthensAShortStun() {
        FighterSimulation sim = Duel(8106);
        PlaceConstruct(sim, 0, CoilType, 0f);
        sim.MutateFighterForTest(1, (ref FighterStateComponent state, ref FighterRuntimeComponent runtime, ref FighterVerbComponent verb) => {
            state.HitstunFrames = 40;
            verb.Tumble = FighterVerbRules.TumbleInHitstun;
            state.Velocity = new FPVector2(FP64.FromInt(3), FP64.FromInt(4));
            state.IsGrounded = 0;
        });
        sim.SeedFighterOwedStrikesForTest(0, 1);
        Step(sim, 1);
        sim.TryGetFighter(1, out FighterStateComponent launched);
        sim.TryGetFighterVerb(1, out FighterVerbComponent launchedVerb);
        AssertThat(launched.HitstunFrames)
            .OverrideFailureMessage("The strike's 20 frames must not replace a longer stun (the old overwrite did).")
            .IsGreaterEqual(39);
        AssertThat(launchedVerb.Tumble).OverrideFailureMessage("A strike never ends a launch's tumble.").IsEqual(FighterVerbRules.TumbleInHitstun);

        FighterSimulation brief = Duel(8107);
        PlaceConstruct(brief, 0, CoilType, 0f);
        brief.MutateFighterForTest(1, (ref FighterStateComponent state, ref FighterRuntimeComponent runtime, ref FighterVerbComponent verb) => state.HitstunFrames = 5);
        brief.SeedFighterOwedStrikesForTest(0, 1);
        Step(brief, 1);
        brief.TryGetFighter(1, out FighterStateComponent lengthened);
        AssertThat(lengthened.HitstunFrames).IsEqual(LingeringKitRules.CoilSyncedArcHitstunFrames);
    }

    [TestCase]
    public void AStrikeOnABlockerIsBlockedAndPaysNothing() {
        FighterSimulation sim = Duel(8108);
        PlaceConstruct(sim, 0, CoilType, 0f);
        for (int i = 0; i < 12; i++) {
            sim.Advance(Frame(sim.CurrentTick, GameplayButtons.None), Frame(sim.CurrentTick, GameplayButtons.Block));
        }
        sim.TryGetFighter(1, out FighterStateComponent before);
        int momentum = MomentumTestKit.Units(sim, 0);
        sim.SeedFighterOwedStrikesForTest(0, 1);
        sim.Advance(Frame(sim.CurrentTick, GameplayButtons.None), Frame(sim.CurrentTick, GameplayButtons.Block));
        sim.TryGetFighter(1, out FighterStateComponent after);
        AssertThat(after.CurrentHP).IsEqual(before.CurrentHP);
        AssertThat(after.BlockCharges).IsLess(before.BlockCharges);
        AssertThat(MomentumTestKit.Units(sim, 0) - momentum)
            .OverrideFailureMessage("A blocked strike pays no Momentum (only regen).")
            .IsLess(LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.SyncedStrike));
    }

    [TestCase]
    public void AStrikeOnAnInvulnerableTargetSpendsTheChargeAndPaysNothing() {
        FighterSimulation sim = Duel(8120);
        PlaceConstruct(sim, 0, CoilType, 0f);
        sim.MutateFighterForTest(1, (ref FighterStateComponent state, ref FighterRuntimeComponent runtime, ref FighterVerbComponent verb) =>
            state.InvulnerabilityFrames = 60);
        sim.TryGetFighter(1, out FighterStateComponent before);
        int momentum = MomentumTestKit.Units(sim, 0);
        sim.SeedFighterOwedStrikesForTest(0, 1);
        Step(sim, 1);
        sim.TryGetFighter(1, out FighterStateComponent after);
        AssertThat(after.CurrentHP).IsEqual(before.CurrentHP);
        AssertThat(MomentumTestKit.Units(sim, 0) - momentum)
            .OverrideFailureMessage("A refused strike pays no Momentum (only regen).")
            .IsLess(LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.SyncedStrike));
    }

    [TestCase]
    public void ARealLandedSwingFiresTheCoilAndAWhiffDoesNot() {
        FighterSimulation landed = Duel(8109);
        PlaceConstruct(landed, 0, CoilType, 0f);
        landed.TryGetFighter(0, out FighterStateComponent swinger);
        landed.MutateFighterForTest(1, (ref FighterStateComponent state, ref FighterRuntimeComponent runtime, ref FighterVerbComponent verb) =>
            state.Position = new FPVector2(swinger.Position.x + FP64.FromDouble(0.9), state.Position.y));
        for (int i = 0; i < 40; i++) {
            landed.Advance(Frame(landed.CurrentTick, i == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None), Frame(landed.CurrentTick, GameplayButtons.None));
        }
        landed.TryGetFirstPersistentObject(out FighterPersistentObjectComponent coil);
        AssertThat(coil.RemainingAttacks)
            .OverrideFailureMessage("A real landed hit-1 is one owed hit: one charge.")
            .IsEqual(LingeringKitRules.CoilCharges - 1);

        FighterSimulation whiff = Duel(8110, spawn: 4); // 8 units apart: the swing cannot reach
        PlaceConstruct(whiff, 0, CoilType, 0f);
        for (int i = 0; i < 40; i++) {
            whiff.Advance(Frame(whiff.CurrentTick, i == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None), Frame(whiff.CurrentTick, GameplayButtons.None));
        }
        whiff.TryGetFirstPersistentObject(out FighterPersistentObjectComponent untouched);
        AssertThat(untouched.RemainingAttacks).IsEqual(LingeringKitRules.CoilCharges);
    }

    [TestCase]
    public void AStrikeNeverTriggersAnotherStrike() {
        FighterSimulation sim = Duel(8111);
        PlaceConstruct(sim, 0, CoilType, 0f);
        PlaceConstruct(sim, 0, CoilType, 0.1f);
        int hpBefore = Hp(sim, 1);
        sim.SeedFighterOwedStrikesForTest(0, 1);
        Step(sim, 6);
        AssertThat(Hp(sim, 1))
            .OverrideFailureMessage("One owed hit, two coils, two strikes - and no more.")
            .IsEqual(hpBefore - 2 * LingeringKitRules.CoilSyncedArcDamage);
        AssertThat(OwedOf(sim, 0)).IsEqual(0);
    }

    [TestCase]
    public void ATurretFiresAFastSyncedBoltAndSpendsACharge() {
        FighterSimulation sim = Duel(8112);
        PlaceConstruct(sim, 0, TurretType, 0f);
        FighterSimulation control = Duel(8112);
        PlaceConstruct(control, 0, TurretType, 0f);
        int hpBefore = Hp(sim, 1);
        sim.SeedFighterOwedStrikesForTest(0, 1);
        Step(sim, 1);
        Step(control, 1);
        AssertThat(sim.TryGetFirstProjectile(out FighterProjectileComponent bolt)).IsTrue();
        AssertThat(bolt.ProjectileTypeID % 10).IsEqual(FighterSyncedStrikeRules.SyncedBoltSlot);
        AssertThat(bolt.Damage).IsEqual(LingeringKitRules.TurretSyncedBoltDamage);
        AssertThat(bolt.HitstunFrames).IsEqual(LingeringKitRules.TurretSyncedBoltHitstunFrames);
        FP64 speed = FighterKitGeometry.Sqrt(bolt.Velocity.x * bolt.Velocity.x + bolt.Velocity.y * bolt.Velocity.y);
        AssertThat(FP64.Abs(speed - FP64.FromInt(24)) < FP64.FromDouble(0.05)).IsTrue();
        sim.TryGetFirstPersistentObject(out FighterPersistentObjectComponent turret);
        AssertThat(turret.RemainingAttacks).IsEqual(LingeringKitRules.TurretCharges - 1);
        AssertThat(control.ProjectileCount).IsEqual(0);

        Step(sim, 12);
        Step(control, 12);
        AssertThat(Hp(sim, 1)).IsEqual(hpBefore - LingeringKitRules.TurretSyncedBoltDamage);
        sim.TryGetFighter(1, out FighterStateComponent target);
        AssertThat(target.HitstunFrames > 0).OverrideFailureMessage("The bolt carries its hitstun.").IsTrue();
        AssertThat(MomentumTestKit.Units(sim, 0) - MomentumTestKit.Units(control, 0))
            .IsEqual(LingeringKitRules.MomentumUnitsFor(LingeringMomentumEvent.SyncedStrike));
    }

    [TestCase]
    public void TheTurretLivesOutItsLifetimeInsteadOfSelfDestructingAfterFourOwnShots() {
        FighterSimulation sim = Duel(8113, spawn: 2);
        PlaceConstruct(sim, 0, TurretType, 0f);
        int hpBefore = Hp(sim, 1);
        // Four own shots at 180 frames each are done by tick 720; the old turret died with its fourth.
        Step(sim, 4 * ConstructOwnAttack.IntervalFrames(TurretType) + 30);
        AssertThat(sim.PersistentObjectCount).OverrideFailureMessage("The turret outlives four own shots.").IsEqual(1);
        sim.TryGetFirstPersistentObject(out FighterPersistentObjectComponent turret);
        AssertThat(turret.RemainingAttacks).OverrideFailureMessage("Own shots spend no charge.").IsEqual(LingeringKitRules.TurretCharges);
        AssertThat(hpBefore - Hp(sim, 1))
            .OverrideFailureMessage("Four own shots of 4 (the old turret dealt 6 a shot).")
            .IsEqual(4 * ConstructOwnAttack.Damage(TurretType));
        Step(sim, 400);
        AssertThat(sim.PersistentObjectCount).OverrideFailureMessage("It still ends with its 15 s lifetime.").IsEqual(0);
    }

    [TestCase]
    public void ANestNipsForThreeWithNoVenomAndSpendsNoCharge() {
        FighterSimulation sim = Duel(8114);
        PlaceConstruct(sim, 0, NestType, 2f); // on the target (its own reach is 1 u)
        int hpBefore = Hp(sim, 1);
        Step(sim, ConstructOwnAttack.IntervalFrames(NestType) + 5);
        AssertThat(Hp(sim, 1)).IsEqual(hpBefore - ConstructOwnAttack.Damage(NestType));
        sim.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime);
        AssertThat(runtime.DamageStatusType).OverrideFailureMessage("The nip carries no Venom.").IsNotEqual((int)StatusType.Venom);
        sim.TryGetFirstPersistentObject(out FighterPersistentObjectComponent nest);
        AssertThat(nest.RemainingAttacks).IsEqual(LingeringKitRules.NestCharges);
    }

    [TestCase]
    public void ANestLungesForSixWithAVenomHold() {
        FighterSimulation sim = Duel(8115);
        PlaceConstruct(sim, 0, NestType, 0f); // 2 u from the target: inside 2.5 u, outside its own 1 u reach
        int hpBefore = Hp(sim, 1);
        sim.SeedFighterOwedStrikesForTest(0, 1);
        Step(sim, 1);
        AssertThat(Hp(sim, 1)).IsEqual(hpBefore - LingeringKitRules.NestSyncedLungeDamage);
        sim.TryGetFighter(1, out FighterStateComponent target);
        AssertThat(target.HitstunFrames).IsEqual(LingeringKitRules.NestSyncedLungeHoldFrames);
        sim.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime);
        AssertThat(runtime.DamageStatusType).IsEqual((int)StatusType.Venom);
        AssertThat(runtime.DamageStatusFrames > LingeringKitRules.NestSyncedLungeVenomFrames - 5).IsTrue();
        sim.TryGetFirstPersistentObject(out FighterPersistentObjectComponent nest);
        AssertThat(nest.RemainingAttacks).IsEqual(LingeringKitRules.NestCharges - 1);
    }

    [TestCase]
    public void TwoOwedHitsSpendTwoTurretChargesAndFireTwoBolts() {
        FighterSimulation sim = Duel(8116);
        PlaceConstruct(sim, 0, TurretType, 0f);
        sim.SeedFighterOwedStrikesForTest(0, 2);
        Step(sim, 1);
        sim.TryGetFirstPersistentObject(out FighterPersistentObjectComponent turret);
        AssertThat(turret.RemainingAttacks).IsEqual(LingeringKitRules.TurretCharges - 2);
        AssertThat(sim.ProjectileCount).IsEqual(2);
    }

    [TestCase]
    public void ARollbackAcrossASyncedStrikeConvergesWithoutDuplicatingIt() {
        FighterSimulation authoritative = Duel(8117);
        FighterSimulation predicted = Duel(8117);
        PlaceConstruct(authoritative, 0, CoilType, 0f);
        PlaceConstruct(predicted, 0, CoilType, 0f);
        for (int tick = 0; tick < 30; tick++) {
            if (tick == 10) {
                authoritative.SeedFighterOwedStrikesForTest(0, 1);
                predicted.SeedFighterOwedStrikesForTest(0, 1);
            }
            PlayerInputFrame one = Frame(tick, GameplayButtons.None);
            PlayerInputFrame two = tick >= 12 && tick <= 16 ? Frame(tick, GameplayButtons.None, 127) : Frame(tick, GameplayButtons.None);
            authoritative.Advance(one, two);
            if (tick >= 12 && tick <= 16) predicted.AdvanceWithPredictedPlayerTwo(one);
            else predicted.Advance(one, two);
        }
        for (int tick = 12; tick <= 16; tick++) {
            AssertThat(predicted.CorrectPlayerTwoInput(tick, Frame(tick, GameplayButtons.None, 127))).IsTrue();
        }
        AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
        AssertThat(Hp(predicted, 1)).IsEqual(Hp(authoritative, 1));
        authoritative.TryGetFirstPersistentObject(out FighterPersistentObjectComponent coil);
        AssertThat(coil.RemainingAttacks).IsEqual(LingeringKitRules.CoilCharges - 1);
    }

    // --- helpers ------------------------------------------------------------------

    private static FighterSimulation Duel(int seed, int spawn = 1) =>
        new(FighterLoadout.Default(FighterCharacterID.Joan), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: seed, spawnDistance: spawn, rules: FighterMatchRules.Disabled);

    /// <summary>Places a construct for <paramref name="player"/> at its feet, shifted by <paramref name="dx"/> units.</summary>
    private static void PlaceConstruct(FighterSimulation sim, int player, int type, float dx) {
        sim.TryGetFighter(player, out FighterStateComponent owner);
        ConstructOwnAttack.Spawn(sim, player, type, new FPVector2(owner.Position.x + FP64.FromDouble(dx), owner.Position.y));
    }

    private static void Step(FighterSimulation sim, int ticks) {
        for (int i = 0; i < ticks; i++) {
            sim.Advance(Frame(sim.CurrentTick, GameplayButtons.None), Frame(sim.CurrentTick, GameplayButtons.None));
        }
    }

    private static int Hp(FighterSimulation sim, int player) {
        sim.TryGetFighter(player, out FighterStateComponent state);
        return state.CurrentHP;
    }

    private static int OwedOf(FighterSimulation sim, int player) {
        sim.TryGetFighterVerb(player, out FighterVerbComponent verb);
        return verb.OwedSyncedStrikes;
    }

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed, sbyte moveX = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = pressed,
        Pressed = pressed
    };
}
