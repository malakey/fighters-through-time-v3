using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W5 — Harriet Tubman's kit in the deterministic sim (replaces the
/// retired Pocahontas kit / Spirit Strike sim suites). Conductor's Call rushes
/// along the ground and strikes grounded targets only; Foresight (component
/// 322) nullifies one strike or projectile and answers within 2.5 units with a
/// 20-damage launch, while grabs beat it; North Star Leap is a guided 8-way,
/// 4-unit leap over 18 frames whose extended ledge snap reaches 0.5 units past
/// the normal capture box. Everything is built from the authored
/// <c>tubman_data.tres</c> through the normalized loadout.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TubmanKitTests {

    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    // === Conductor's Call ======================================================

    [TestCase]
    public void ConductorsCallStrikesAGroundedTargetOnceForItsAuthoredDamage() {
        FighterLoadout tubman = TubmanLoadout();
        var simulation = new FighterSimulation(
            tubman, FighterLoadout.Default(FighterCharacterID.Lincoln),
            seed: 1401, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        simulation.Advance(Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstProjectile(out FighterProjectileComponent call)).IsTrue();
        AssertThat(call.ProjectileTypeID).IsEqual(FighterConductorsCallRules.ProjectileTypeID);
        for (int tick = 1; tick < 40; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(before.CurrentHP - after.CurrentHP)
            .OverrideFailureMessage("The scouts strike a grounded target once for the authored 18.")
            .IsEqual(tubman.SpecialOneDamage);
        AssertThat(tubman.SpecialOneDamage).IsEqual(18);
    }

    [TestCase]
    public void ConductorsCallPassesUnderAnAirborneTargetAndIsNotConsumed() {
        var simulation = new FighterSimulation(
            TubmanLoadout(), FighterLoadout.Default(FighterCharacterID.Lincoln),
            seed: 1402, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        // The target jumps first; the call is raised while they are in the air.
        simulation.Advance(Frame(0, GameplayButtons.None), Frame(0, GameplayButtons.Jump));
        simulation.Advance(Frame(1, GameplayButtons.None), Frame(1, GameplayButtons.None));
        simulation.Advance(Frame(2, GameplayButtons.Special1), Frame(2, GameplayButtons.None));
        bool sawOverlapWhileAirborne = false;
        for (int tick = 3; tick < 30; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            simulation.TryGetFighter(1, out FighterStateComponent target);
            AssertThat(target.IsGrounded)
                .OverrideFailureMessage("The drill needs the target airborne while the rush passes.")
                .IsEqual(0);
            if (simulation.TryGetFirstProjectile(out FighterProjectileComponent call)
                && FP64.Abs(call.Position.x - target.Position.x) < FP64.FromDouble(0.5)) {
                sawOverlapWhileAirborne = true;
            }
        }
        AssertThat(sawOverlapWhileAirborne)
            .OverrideFailureMessage("The rush never passed the airborne target — the drill proves nothing.")
            .IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.CurrentHP).IsEqual(before.CurrentHP);
    }

    // === Foresight =============================================================

    [TestCase]
    public void ForesightNullifiesAStringHitAndAnswersWithALaunchingCounterStrike() {
        FighterLoadout tubman = TubmanLoadout();
        FighterSimulation simulation = ForesightDuel(seed: 1403);
        int tick = Approach(simulation, 0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent attackerBefore)).IsTrue();

        // Window: 4..23 ticks after the press. The opponent swings into it.
        simulation.Advance(Frame(tick, GameplayButtons.Special2), Frame(tick, GameplayButtons.None));
        tick++;
        AssertThat(Counter(simulation, 0).Phase).IsEqual(FighterForesightRules.PhaseStartup);
        simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.BasicAttack));
        tick++;
        bool caught = false;
        for (int i = 0; i < 40 && !caught; i++, tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            caught = Counter(simulation, 0).CountersLanded > 0;
        }
        AssertThat(caught).OverrideFailureMessage("The swing was never caught by the window.").IsTrue();
        for (int i = 0; i < 6; i++, tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent tubmanAfter)).IsTrue();
        AssertThat(tubmanAfter.CurrentHP)
            .OverrideFailureMessage("A caught strike deals no damage.")
            .IsEqual(tubmanAfter.MaxHP);
        AssertThat(tubmanAfter.BlockCharges)
            .OverrideFailureMessage("A caught strike spends no block charge.")
            .IsEqual(3);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent attackerAfter)).IsTrue();
        AssertThat(attackerBefore.CurrentHP - attackerAfter.CurrentHP)
            .OverrideFailureMessage("The attacker within 2.5 units takes the 20-damage answer.")
            .IsEqual(tubman.SpecialTwoDamage);
        AssertThat(tubman.SpecialTwoDamage).IsEqual(20);
        AssertThat(Counter(simulation, 0).CountersLanded).IsEqual(1);
    }

    [TestCase]
    public void ForesightConsumesAProjectileFromAfarAndLeavesTheDistantShooterUnanswered() {
        var simulation = new FighterSimulation(
            TubmanLoadout(), EinsteinLoadout(),
            seed: 1404, spawnDistance: 4, rules: FighterMatchRules.Disabled);
        // Einstein (right) faces Tubman and fires; Tubman reads it late.
        simulation.Advance(Frame(0, GameplayButtons.None), Frame(0, GameplayButtons.Special1));
        AssertThat(simulation.TryGetFirstProjectile(out FighterProjectileComponent _)).IsTrue();
        int tick = 1;
        bool pressed = false;
        for (; tick < 200 && !pressed; tick++) {
            simulation.TryGetFighter(0, out FighterStateComponent tubman);
            bool close = simulation.TryGetFirstProjectile(out FighterProjectileComponent shot)
                && FP64.Abs(shot.Position.x - tubman.Position.x) < FP64.FromDouble(1.6);
            simulation.Advance(Frame(tick, close ? GameplayButtons.Special2 : GameplayButtons.None), Frame(tick, GameplayButtons.None));
            pressed = close;
        }
        AssertThat(pressed).OverrideFailureMessage("The shot never approached Tubman.").IsTrue();
        for (int i = 0; i < 40; i++, tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(Counter(simulation, 0).CountersLanded).IsEqual(1);
        AssertThat(simulation.TryGetFirstProjectile(out FighterProjectileComponent _))
            .OverrideFailureMessage("The caught shot is consumed.")
            .IsFalse();
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent tubmanAfter)).IsTrue();
        AssertThat(tubmanAfter.CurrentHP).IsEqual(tubmanAfter.MaxHP);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent shooter)).IsTrue();
        AssertThat(shooter.CurrentHP)
            .OverrideFailureMessage("A shooter beyond 2.5 units is not answered.")
            .IsEqual(shooter.MaxHP);
    }

    [TestCase]
    public void AWhiffedForesightLocksHerThroughTheStanceAndArmsTheTenSecondCooldown() {
        FighterLoadout tubman = TubmanLoadout();
        var simulation = new FighterSimulation(
            tubman, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 1405, spawnDistance: 4, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent armed)).IsTrue();
        AssertThat(armed.SpecialTwoCooldownFrames).IsEqual(tubman.SpecialTwoCooldownFrames);
        AssertThat(tubman.SpecialTwoCooldownFrames).IsEqual(600);

        int startup = 0, window = 0, whiff = 0;
        for (int tick = 1; tick <= 60; tick++) {
            // She mashes Jump the whole time: the stance discards it.
            simulation.Advance(Frame(tick, GameplayButtons.Jump), Frame(tick, GameplayButtons.None));
            int phase = Counter(simulation, 0).Phase;
            if (phase == FighterForesightRules.PhaseStartup) startup++;
            if (phase == FighterForesightRules.PhaseWindow) window++;
            if (phase == FighterForesightRules.PhaseWhiffRecovery) whiff++;
            if (phase != FighterForesightRules.PhaseNone) {
                simulation.TryGetFighter(0, out FighterStateComponent locked);
                AssertThat(locked.IsGrounded)
                    .OverrideFailureMessage($"A Jump press escaped the stance on tick {tick}.")
                    .IsEqual(1);
            }
        }
        // The press tick itself is the first startup frame.
        AssertThat(startup + 1).IsEqual(TubmanKitRules.ForesightStartupFrames);
        AssertThat(window).IsEqual(TubmanKitRules.ForesightWindowFrames);
        AssertThat(whiff).IsEqual(TubmanKitRules.ForesightWhiffRecoveryFrames);
        AssertThat(Counter(simulation, 0).CountersLanded).IsEqual(0);
    }

    [TestCase]
    public void AGrabBeatsForesight() {
        var simulation = new FighterSimulation(
            TubmanLoadout(), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 1406, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        // The grabber walks in to grab range first.
        int tick = Approach(simulation, 0);
        simulation.Advance(Frame(tick, GameplayButtons.Special2), Frame(tick, GameplayButtons.None));
        tick++;
        for (int i = 0; i < 5; i++, tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(Counter(simulation, 0).Phase).IsEqual(FighterForesightRules.PhaseWindow);
        simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.Grab));
        tick++;
        bool held = false;
        for (int i = 0; i < 20 && !held; i++, tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            held = simulation.TryGetFighterVerb(0, out FighterVerbComponent verb) && verb.BeingHeld != 0;
        }
        AssertThat(held).OverrideFailureMessage("The grab did not take a fighter in the Foresight window.").IsTrue();
        // Past the grab's hitstop, the held fighter's stance is gone.
        for (int i = 0; i < 12; i++, tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(Counter(simulation, 0).CountersLanded).IsEqual(0);
        AssertThat(Counter(simulation, 0).Phase)
            .OverrideFailureMessage("A grab ends the stance.")
            .IsEqual(FighterForesightRules.PhaseNone);
    }

    [TestCase]
    public void TheForesightStanceAndItsAnswerAreSnapshotAndRollbackSafe() {
        FighterSimulation uninterrupted = ForesightDuel(seed: 1407);
        FighterSimulation restored = ForesightDuel(seed: 1407);
        int start = Approach(uninterrupted, 0);
        uninterrupted.Advance(Frame(start, GameplayButtons.Special2), Frame(start, GameplayButtons.None));
        uninterrupted.Advance(Frame(start + 1, GameplayButtons.None), Frame(start + 1, GameplayButtons.BasicAttack));
        for (int tick = start + 2; tick < start + 6; tick++) {
            uninterrupted.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        // Mid-window, before the swing lands.
        restored.RestoreFullState(uninterrupted.CaptureFullState());
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);
        for (int tick = start + 6; tick < start + 90; tick++) {
            long expected = uninterrupted.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            long actual = restored.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
        AssertThat(Counter(uninterrupted, 0).CountersLanded)
            .OverrideFailureMessage("The drill must actually exercise a counter.")
            .IsEqual(1);
        AssertThat(Counter(restored, 0).CountersLanded).IsEqual(1);
    }

    // === North Star Leap =======================================================

    [TestCase]
    public void NorthStarLeapTravelsFourUnitsInTheHeldDirectionOverEighteenFrames() {
        foreach ((sbyte moveX, sbyte moveY, double expectedDx, double expectedDy) in new (sbyte, sbyte, double, double)[] {
                     (127, 0, 4.0, 0.0),                       // forward
                     (0, -127, 0.0, 4.0),                      // straight up (stick Y is down)
                     (127, -127, 2.8284271, 2.8284271)        // up-forward diagonal
                 }) {
            var simulation = new FighterSimulation(
                TubmanLoadout(), FighterLoadout.Default(FighterCharacterID.Joan),
                seed: 1408, spawnDistance: 6, rules: FighterMatchRules.Disabled);
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent start)).IsTrue();
            simulation.Advance(Frame(0, GameplayButtons.MovementAbility, moveX, moveY), Frame(0, GameplayButtons.None));
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent armed)).IsTrue();
            AssertThat(armed.UniversalMovementState).IsEqual(FighterKitMotion.LeapTravel);
            int travel = 0;
            for (int tick = 1; tick <= TubmanKitRules.NorthStarLeapTravelFrames + 1; tick++) {
                simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
                simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime);
                if (runtime.UniversalMovementState == FighterKitMotion.LeapTravel) travel++;
            }
            AssertThat(travel).IsEqual(TubmanKitRules.NorthStarLeapTravelFrames);
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent end)).IsTrue();
            FP64 tolerance = FP64.FromDouble(0.05);
            AssertThat(FP64.Abs(end.Position.x - start.Position.x - FP64.FromDouble(expectedDx)) < tolerance)
                .OverrideFailureMessage($"({moveX},{moveY}): dx {end.Position.x - start.Position.x}, expected {expectedDx}.")
                .IsTrue();
            AssertThat(FP64.Abs(end.Position.y - start.Position.y - FP64.FromDouble(expectedDy)) < tolerance)
                .OverrideFailureMessage($"({moveX},{moveY}): dy {end.Position.y - start.Position.y}, expected {expectedDy}.")
                .IsTrue();
        }
    }

    [TestCase]
    public void TheLeapsLedgeSnapReachesHalfAUnitPastTheNormalCaptureBox() {
        // Paris's courtyard pit: the left floor segment ends in a true ledge.
        FighterStageGeometry paris = FighterStageGeometry.ForStage("paris_bastille");
        AssertThat(paris.TryFindLedge(new FPVector2(FP64.FromDouble(-2.5), FP64.FromDouble(-0.5)), out int anchor)).IsTrue();
        AssertThat(paris.TryGetHangPosition(anchor, out FPVector2 hang)).IsTrue();
        // Side 1 is a segment's right end: the hang sits outward (+x) of the edge.
        AssertThat(anchor % 2).IsEqual(1);
        FP64 edgeX = hang.x - FighterLedgeRules.HangOutwardOffset;
        // 0.8 units out over the pit: beyond the normal 0.5 box, inside 0.5 + 0.5.
        var outOfReach = new FPVector2(edgeX + FP64.FromDouble(0.8), FP64.FromDouble(-0.5));
        AssertThat(paris.TryFindLedge(in outOfReach, out int _)).IsFalse();
        AssertThat(paris.TryFindLedge(in outOfReach, FighterKitMotion.LeapLedgeSnapBonus, out int snapped)).IsTrue();
        AssertThat(snapped).IsEqual(anchor);
        // Beyond the extended box nothing snaps.
        var tooFar = new FPVector2(edgeX + FP64.FromDouble(1.1), FP64.FromDouble(-0.5));
        AssertThat(paris.TryFindLedge(in tooFar, FighterKitMotion.LeapLedgeSnapBonus, out int _)).IsFalse();
        // The bonus rides the leap's travel and settle frames, and nothing else.
        AssertThat(FighterKitMotion.LeapLedgeSnapBonus).IsEqual(FP64.FromDouble(TubmanKitRules.NorthStarLeapLedgeSnapBonusUnits));
        var runtime = new FighterRuntimeComponent { UniversalMovementState = FighterKitMotion.LeapTravel };
        AssertThat(FighterKitMotion.HasLedgeSnapBonus(in runtime)).IsTrue();
        runtime.UniversalMovementState = FighterKitMotion.LeapEnd;
        AssertThat(FighterKitMotion.HasLedgeSnapBonus(in runtime)).IsTrue();
        runtime.UniversalMovementState = FighterKitMotion.BlinkTravel;
        AssertThat(FighterKitMotion.HasLedgeSnapBonus(in runtime)).IsFalse();
    }

    // === helpers ================================================================

    private static FighterSimulation ForesightDuel(int seed) => new(
        TubmanLoadout(), FighterLoadout.Default(FighterCharacterID.Joan),
        seed: seed, spawnDistance: 1, rules: FighterMatchRules.Disabled);

    /// <summary>Walks player two in until the pair is inside string and grab reach; returns the next tick.</summary>
    private static int Approach(FighterSimulation simulation, int tick) {
        for (int i = 0; i < 120; i++, tick++) {
            simulation.TryGetFighter(0, out FighterStateComponent a);
            simulation.TryGetFighter(1, out FighterStateComponent b);
            if (b.Position.x - a.Position.x < FP64.FromDouble(1.1)) break;
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None, moveX: -127));
        }
        // Let the walk's momentum settle so the swing lands from a standstill.
        for (int i = 0; i < 12; i++, tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        return tick;
    }

    private static FighterCounterComponent Counter(FighterSimulation simulation, int playerID) {
        simulation.TryGetFighterCounter(playerID, out FighterCounterComponent counter);
        return counter;
    }

    internal static FighterLoadout TubmanLoadout() =>
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/tubman_data.tres"));

    private static FighterLoadout EinsteinLoadout() =>
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/einstein_data.tres"));

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed, sbyte moveX = 0, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };
}
