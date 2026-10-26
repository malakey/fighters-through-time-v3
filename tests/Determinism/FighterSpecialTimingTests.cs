using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Combat and movement update 2026-10-16, decision 1.2 (plan §2.1.6): every
/// Fighter Special honours its authored startup, active and recovery frames on
/// component 323, on the production loadouts <see cref="FighterLoadoutFactory"/>
/// builds from the nine <c>*_data.tres</c>. The effect fires on T+S (the sim's
/// basic-swing tick convention), the caster is action-locked through
/// T+S+A+R−1 and free on T+S+A+R, the clock pauses in the caster's hitstop, a
/// startup hit cancels the cast with its cost spent, a grounded cast brakes to a
/// stop while an aerial one keeps its drift at half gravity (the Q1 ruling), the
/// only exit is the grounded recovery Block cancel, and the cast lock holds on
/// the caster's own hitstop thaw tick.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterSpecialTimingTests {

    private static readonly string[] Heroes = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra", "tesla", "shakespeare", "mozart", "tubman"
    };

    // === 1. the eighteen Specials ==============================================

    /// <summary>
    /// For all eighteen Specials: the effect is absent on T…T+S−1 and present on
    /// T+S; the caster is locked through T+S+A+R−1 (a BasicAttack pressed on
    /// every tick starts nothing) and free on T+S+A+R (the same press starts a
    /// swing). The opponent stands twelve units away, so nothing lands and no
    /// hitstop moves the clock. Foresight runs its own 4 / 20 / 24 timeline on
    /// component 322; 323 records it as Delegated.
    /// </summary>
    [TestCase]
    public void EveryAuthoredSpecialFiresOnItsActiveEntryAndHoldsTheCasterThroughItsRecovery() {
        var failures = new List<string>();
        foreach (string hero in Heroes) {
            CharacterData data = Data(hero);
            for (int slot = 1; slot <= 2; slot++) {
                string name = $"{hero} S{slot}";
                AbilityData ability = slot == 1 ? data.SpecialAttackOne : data.SpecialAttackTwo;
                bool foresight = hero == "tubman" && slot == 2;
                bool meleeWhiff = ability.ExecutionType == AbilityExecutionType.Melee && !foresight && hero != "joan";
                int s = foresight ? TubmanKitRules.ForesightStartupFrames : ability.StartupFrames;
                int a = foresight ? TubmanKitRules.ForesightWindowFrames : ability.ActiveFrames;
                int r = foresight ? TubmanKitRules.ForesightWhiffRecoveryFrames : ability.RecoveryFrames;
                int free = s + a + r;

                var simulation = new FighterSimulation(
                    FighterLoadoutFactory.FromCharacterData(data), FighterLoadout.Default(FighterCharacterID.Joan),
                    seed: 9100 + slot, spawnDistance: 6, rules: FighterMatchRules.Disabled);
                Idle(simulation, 2);
                int entitiesBefore = Entities(simulation);
                simulation.Advance(In(slot == 1 ? GameplayButtons.Special1 : GameplayButtons.Special2), Neutral());
                if (!Cast(simulation, 0).SpecialPhase.Equals(foresight
                        ? FighterSpecialCastRules.PhaseDelegated
                        : s > 0 ? FighterSpecialCastRules.PhaseStartup : FighterSpecialCastRules.PhaseActive)) {
                    failures.Add($"{name}: the press was not accepted into its cast.");
                    continue;
                }

                int effectOn = Effect(simulation, foresight, meleeWhiff, entitiesBefore) ? 0 : -1;
                int swingOn = -1;
                for (int offset = 1; offset <= free + 2 && swingOn < 0; offset++) {
                    simulation.Advance(In(GameplayButtons.BasicAttack), Neutral());
                    AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
                    if (runtime.AttackPhase != FighterBasicAttackRules.PhaseNone) swingOn = offset;
                    if (effectOn < 0 && Effect(simulation, foresight, meleeWhiff, entitiesBefore)) effectOn = offset;
                    if (offset < free && !FighterSpecialCastRules.IsBusy(Cast(simulation, 0))) {
                        failures.Add($"{name}: free on T+{offset}, before T+{free}.");
                        break;
                    }
                }
                if (effectOn != s) failures.Add($"{name}: the effect appeared on T+{effectOn}, not T+{s}.");
                if (swingOn != free) failures.Add($"{name}: the first swing started on T+{swingOn}, not T+{free}.");
            }
        }
        if (failures.Count > 0) AssertThat(string.Join(" | ", failures)).IsEqual("");
    }

    // === 2. a startup hit ======================================================

    [TestCase]
    public void AHitDuringTheStartupCancelsTheCastWithItsCostSpentAndNoEffect() {
        // Fortissimo Wave (16 / 6 / 22): the opponent's opener lands inside the
        // startup and the wall is never raised.
        var simulation = new FighterSimulation(
            Authored("mozart"), FighterLoadout.Default(FighterCharacterID.Leonardo),
            seed: 9201, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        Idle(simulation, 2);
        simulation.Advance(In(GameplayButtons.Special2), Neutral());
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent pressed)).IsTrue();
        int cooldown = pressed.SpecialTwoCooldownFrames;
        AssertThat(cooldown > 0).OverrideFailureMessage("The press spends its cooldown at acceptance.").IsTrue();

        int hitOn = -1;
        for (int offset = 1; offset < 16 && hitOn < 0; offset++) {
            simulation.Advance(Neutral(), offset == 1 ? In(GameplayButtons.BasicAttack) : Neutral());
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent mozart)).IsTrue();
            if (mozart.HitstunFrames > 0) hitOn = offset;
        }
        AssertThat(hitOn > 0)
            .OverrideFailureMessage("Harness: the opener never landed inside the 16-frame startup.")
            .IsTrue();
        // The combat system lands the hit after this tick's movement and fire
        // passes; the interrupt clears the cast at the next tick's movement pass,
        // before any system reads it.
        simulation.Advance(Neutral(), Neutral());
        FighterActionStateComponent cancelled = Cast(simulation, 0);
        AssertThat(FighterSpecialCastRules.IsBusy(in cancelled))
            .OverrideFailureMessage("A hit in the startup ends the cast.")
            .IsFalse();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent spent)).IsTrue();
        AssertThat(spent.SpecialTwoCooldownFrames > 0)
            .OverrideFailureMessage("The cooldown stays spent.")
            .IsTrue();
        for (int step = 0; step < 60; step++) {
            simulation.Advance(Neutral(), Neutral());
            AssertThat(simulation.ProjectileCount)
                .OverrideFailureMessage("An unfired effect never fires.")
                .IsEqual(0);
        }
    }

    // === 3. hitstop pauses the clock ===========================================

    [TestCase]
    public void TheCastersHitstopPausesItsCastClockForExactlyThoseTicks() {
        // Splitting Strike (14 / 6 / 22) lands on its active entry; both
        // fighters freeze N ticks, and the cast ends N ticks later than T+42.
        CharacterData lincoln = Data("lincoln");
        int s = lincoln.SpecialAttackTwo.StartupFrames;
        int free = s + lincoln.SpecialAttackTwo.ActiveFrames + lincoln.SpecialAttackTwo.RecoveryFrames;
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(lincoln), FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: 9301, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        Idle(simulation, 2);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        simulation.Advance(In(GameplayButtons.Special2), Neutral());
        int contactOn = -1;
        for (int offset = 1; offset <= s && contactOn < 0; offset++) {
            simulation.Advance(Neutral(), Neutral());
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
            if (target.CurrentHP < before.CurrentHP) contactOn = offset;
        }
        AssertThat(contactOn).OverrideFailureMessage("The arc lands on T+S.").IsEqual(s);
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent frozen)).IsTrue();
        int hitstop = frozen.HitstopFrames;
        AssertThat(hitstop > 0).OverrideFailureMessage("Harness: the landed arc freezes its caster.").IsTrue();

        int frozenTicks = 0;
        int freeOn = -1;
        for (int offset = s + 1; offset <= free + hitstop + 2 && freeOn < 0; offset++) {
            AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent verb)).IsTrue();
            FighterActionStateComponent clockBefore = Cast(simulation, 0);
            simulation.Advance(Neutral(), Neutral());
            FighterActionStateComponent clockAfter = Cast(simulation, 0);
            if (verb.HitstopFrames > 0) {
                frozenTicks++;
                AssertThat(clockAfter.SpecialPhaseFrames == clockBefore.SpecialPhaseFrames
                        && clockAfter.SpecialPhaseElapsed == clockBefore.SpecialPhaseElapsed
                        && clockAfter.SpecialPhase == clockBefore.SpecialPhase)
                    .OverrideFailureMessage($"T+{offset}: the cast clock moved inside the caster's hitstop.")
                    .IsTrue();
            }
            if (!FighterSpecialCastRules.IsBusy(in clockAfter)) freeOn = offset;
        }
        AssertThat(frozenTicks).IsEqual(hitstop);
        AssertThat(freeOn)
            .OverrideFailureMessage("The cast ends exactly the hitstop's length after T+S+A+R.")
            .IsEqual(free + hitstop);
    }

    // === 4. aerial half gravity ================================================

    [TestCase]
    public void AnAerialCastFallsAtTheSharedHalfGravityThroughItsStartupAndActiveFrames() {
        AssertThat(UniversalMovementRules.AerialSpecialGravityMultiplier).IsEqual(0.5f);
        AssertThat(FighterAirPhysics.AerialSpecialGravityScale.RawValue)
            .IsEqual(FP64.FromDouble(UniversalMovementRules.AerialSpecialGravityMultiplier).RawValue);

        // The Requiem Chord (10 / 6 / 14), cast four ticks into a jump.
        CharacterData mozart = Data("mozart");
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(mozart), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 9401, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        Idle(simulation, 2);
        simulation.Advance(In(GameplayButtons.Jump), Neutral());
        Idle(simulation, 3);
        simulation.Advance(In(GameplayButtons.Special1), Neutral());
        AssertThat(FighterSpecialCastRules.HasFlag(Cast(simulation, 0), FighterSpecialCastRules.FlagStartedAerial))
            .OverrideFailureMessage("Harness: the cast must begin airborne.")
            .IsTrue();

        int scaledTicks = 0, normalTicks = 0;
        for (int step = 0; step < 40; step++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent prior)).IsTrue();
            if (prior.IsGrounded != 0) break;
            simulation.Advance(Neutral(), Neutral());
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent now)).IsTrue();
            if (now.IsGrounded != 0) break;
            FighterActionStateComponent cast = Cast(simulation, 0);
            bool scaled = FighterSpecialCastRules.IsStartup(in cast) || FighterSpecialCastRules.IsActive(in cast);
            bool jumped = FighterAirPhysics.HasJumpedThisAirtime(prior.RemainingJumps, mozart.MaxJumpCount);
            FP64 expected = scaled
                ? FighterAirPhysics.StepMovement(prior.Velocity.y, false, false, jumped, FighterAirPhysics.AerialSpecialGravityScale)
                : FighterAirPhysics.StepMovement(prior.Velocity.y, false, false, jumped);
            AssertThat(now.Velocity.y.RawValue)
                .OverrideFailureMessage($"Step {step} ({(scaled ? "startup/active" : "recovery")}): vertical velocity.")
                .IsEqual(expected.RawValue);
            if (scaled) scaledTicks++; else normalTicks++;
        }
        // The startup's remaining nine ticks and the six active ticks halve gravity.
        AssertThat(scaledTicks).IsEqual(mozart.SpecialAttackOne.StartupFrames - 1 + mozart.SpecialAttackOne.ActiveFrames);
        AssertThat(normalTicks > 0)
            .OverrideFailureMessage("Harness: the recovery must run airborne on the normal step.")
            .IsTrue();
    }

    // === 5. the Q1 motion rule =================================================

    [TestCase]
    public void AGroundedCastBrakesToAStopWithinFourFramesAndAnAerialCastKeepsItsDrift() {
        // Grounded: Joan runs at full speed into Righteous Smite, then holds the
        // stick and Down through the cast. The run-stop ramp stops her within
        // RunDecelerationFrames, and she stays put.
        var grounded = new FighterSimulation(
            Authored("joan"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 9501, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        for (int tick = 0; tick < 20; tick++) grounded.Advance(In(GameplayButtons.None, moveX: 127), Neutral());
        AssertThat(grounded.TryGetFighter(0, out FighterStateComponent running)).IsTrue();
        AssertThat(running.Velocity.x > FP64.Zero).IsTrue();
        grounded.Advance(In(GameplayButtons.Special1, moveX: 127), Neutral());
        int stoppedOn = -1;
        FP64 stoppedAt = FP64.Zero;
        for (int offset = 1; offset <= 30; offset++) {
            grounded.Advance(In(GameplayButtons.None, moveX: 127, moveY: -127, held: GameplayButtons.Down), Neutral());
            AssertThat(grounded.TryGetFighter(0, out FighterStateComponent joan)).IsTrue();
            AssertThat(FighterSpecialCastRules.IsBusy(Cast(grounded, 0))).IsTrue();
            if (stoppedOn < 0 && joan.Velocity.x == FP64.Zero) {
                stoppedOn = offset;
                stoppedAt = joan.Position.x;
            } else if (stoppedOn > 0) {
                AssertThat(joan.Velocity.x).OverrideFailureMessage("A stopped grounded cast stays stopped.").IsEqual(FP64.Zero);
                AssertThat(joan.Position.x.RawValue).IsEqual(stoppedAt.RawValue);
            }
            AssertThat(joan.IsGrounded).IsEqual(1);
        }
        AssertThat(stoppedOn)
            .OverrideFailureMessage("The grounded cast brakes over the run-stop ramp.")
            .IsEqual(UniversalMovementRules.RunDecelerationFrames);

        // Aerial: Mozart jumps forward and casts the Requiem Chord; the stick held
        // backward and Down change nothing — the run below is the same frame for
        // frame as the one with neutral input, and the drift never changes.
        List<long> steeredHashes = AerialDriftRun(steer: true, out List<FP64> steeredX);
        List<long> neutralHashes = AerialDriftRun(steer: false, out List<FP64> _);
        AssertThat(string.Join(",", steeredHashes))
            .OverrideFailureMessage("The stick and Down changed an aerial cast.")
            .IsEqual(string.Join(",", neutralHashes));
        AssertThat(steeredX.Count > 10).OverrideFailureMessage("Harness: the cast must run airborne.").IsTrue();
        for (int i = 1; i < steeredX.Count; i++) {
            AssertThat(steeredX[i].RawValue)
                .OverrideFailureMessage($"Airborne cast tick {i}: the horizontal drift changed.")
                .IsEqual(steeredX[0].RawValue);
        }
        AssertThat(steeredX[0] > FP64.Zero).OverrideFailureMessage("Harness: the jump carried drift.").IsTrue();
    }

    /// <summary>
    /// Mozart's forward jump into an aerial chord. Returns the state hash of every
    /// tick that ends still casting and airborne; <paramref name="driftDuringCast"/>
    /// is the horizontal velocity on those ticks.
    /// </summary>
    private static List<long> AerialDriftRun(bool steer, out List<FP64> driftDuringCast) {
        var simulation = new FighterSimulation(
            Authored("mozart"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 9502, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        for (int tick = 0; tick < 12; tick++) simulation.Advance(In(GameplayButtons.None, moveX: 127), Neutral());
        simulation.Advance(In(GameplayButtons.Jump, moveX: 127), Neutral());
        for (int tick = 0; tick < 3; tick++) simulation.Advance(In(GameplayButtons.None, moveX: 127), Neutral());
        simulation.Advance(In(GameplayButtons.Special1, moveX: 127), Neutral());
        driftDuringCast = new List<FP64>();
        var hashes = new List<long>();
        for (int step = 0; step < 40; step++) {
            PlayerInputFrame input = steer
                ? In(GameplayButtons.None, moveX: -127, moveY: -127, held: GameplayButtons.Down)
                : Neutral();
            long hash = simulation.Advance(input, Neutral());
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent mozart)).IsTrue();
            if (mozart.IsGrounded != 0 || !FighterSpecialCastRules.IsBusy(Cast(simulation, 0))) break;
            driftDuringCast.Add(mozart.Velocity.x);
            hashes.Add(hash);
        }
        return hashes;
    }

    // === 6. the recovery Block cancel ==========================================

    [TestCase]
    public void TheRecoveryBlockCancelIsGroundedUnbufferedAndOpenOnAnyRecoveryFrame() {
        CharacterData joan = Data("joan");
        int s = joan.SpecialAttackOne.StartupFrames;
        int a = joan.SpecialAttackOne.ActiveFrames;

        // A Block press on the last active tick, released at once, is not buffered.
        FighterSimulation tapped = NewJoanSmite(9601);
        for (int offset = 1; offset < s + a + 6; offset++) {
            tapped.Advance(offset == s + a - 1 ? In(GameplayButtons.Block) : Neutral(), Neutral());
            AssertThat(IsBlockStance(tapped, 0)).OverrideFailureMessage($"T+{offset}: a tap was buffered.").IsFalse();
            AssertThat(FighterSpecialCastRules.IsBusy(Cast(tapped, 0))).IsTrue();
        }

        // Held from recovery frame 5: the stance rises on that tick.
        FighterSimulation late = NewJoanSmite(9602);
        int raisedOn = -1;
        for (int offset = 1; offset < s + a + 20 && raisedOn < 0; offset++) {
            late.Advance(offset >= s + a + 5 ? Held(GameplayButtons.Block) : Neutral(), Neutral());
            if (IsBlockStance(late, 0)) raisedOn = offset;
        }
        AssertThat(raisedOn).IsEqual(s + a + 5);
        AssertThat(FighterSpecialCastRules.IsBusy(Cast(late, 0))).IsFalse();

        // Airborne recovery: Block does not cancel (the stance is grounded).
        var aerial = new FighterSimulation(
            Authored("mozart"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 9603, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        Idle(aerial, 2);
        aerial.Advance(In(GameplayButtons.Jump), Neutral());
        aerial.Advance(In(GameplayButtons.Special1), Neutral());
        AbilityData chord = Data("mozart").SpecialAttackOne;
        int chordFree = chord.StartupFrames + chord.ActiveFrames + chord.RecoveryFrames;
        int airborneRecoveryTicks = 0;
        for (int offset = 1; offset <= chordFree; offset++) {
            aerial.Advance(Held(GameplayButtons.Block), Neutral());
            AssertThat(aerial.TryGetFighter(0, out FighterStateComponent mozart)).IsTrue();
            AssertThat(mozart.IsGrounded)
                .OverrideFailureMessage($"Harness: Mozart landed on T+{offset}, inside the chord.")
                .IsEqual(0);
            FighterActionStateComponent cast = Cast(aerial, 0);
            if (offset < chordFree) {
                AssertThat(FighterSpecialCastRules.IsBusy(in cast))
                    .OverrideFailureMessage($"T+{offset}: Block cancelled an airborne recovery.")
                    .IsTrue();
                AssertThat(IsBlockStance(aerial, 0)).IsFalse();
            }
            if (FighterSpecialCastRules.IsRecovery(in cast)) airborneRecoveryTicks++;
        }
        AssertThat(airborneRecoveryTicks).IsEqual(chord.RecoveryFrames);
    }

    private static FighterSimulation NewJoanSmite(int seed) {
        var simulation = new FighterSimulation(
            Authored("joan"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: seed, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        Idle(simulation, 2);
        simulation.Advance(In(GameplayButtons.Special1), Neutral());
        AssertThat(FighterSpecialCastRules.IsStartup(Cast(simulation, 0))).IsTrue();
        return simulation;
    }

    // === 7. no ledge capture while casting =====================================

    [TestCase]
    public void NoLedgeIsCapturedWhileCasting() {
        // Nassau's stern ledge: Tubman walks off and hangs, climb-jumps straight
        // up, and falls back into the capture box. With Conductor's Call
        // (12 / 30 / 18) cast at the climb's apex, she falls through the box
        // while busy and does not catch it.
        FighterStageGeometry nassau = FighterStageGeometry.Nassau;
        int sternAnchor = nassau.Platforms.Length * 2 + 1;
        var reference = NassauTubman(nassau);
        var cast = NassauTubman(nassau);
        int hang = HangThenClimb(reference, nassau, sternAnchor);
        AssertThat(HangThenClimb(cast, nassau, sternAnchor)).IsEqual(hang);

        // Reference: the climb's fall recaptures the ledge.
        bool recaptured = false;
        for (int step = 0; step < 120 && !recaptured; step++) {
            reference.Advance(Neutral(), Neutral());
            AssertThat(reference.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
            recaptured = runtime.LedgeAnchor == sternAnchor;
        }
        AssertThat(recaptured).OverrideFailureMessage("Harness: the climb's fall never recaptured the ledge.").IsTrue();

        // Cast at the climb's apex, so the startup and active frames cover the
        // descent through the box.
        for (int step = 0; step < 60; step++) {
            AssertThat(cast.TryGetFighter(1, out FighterStateComponent rising)).IsTrue();
            if (rising.Velocity.y <= FP64.Zero) break;
            cast.Advance(Neutral(), Neutral());
        }
        cast.Advance(Neutral(), In(GameplayButtons.Special1));
        AssertThat(FighterSpecialCastRules.IsStartup(Cast(cast, 1)))
            .OverrideFailureMessage("Harness: the aerial cast must be accepted.")
            .IsTrue();
        int busyTicksInBox = 0;
        for (int step = 0; step < 120; step++) {
            cast.Advance(Neutral(), Neutral());
            AssertThat(cast.TryGetFighter(1, out FighterStateComponent tubman)).IsTrue();
            AssertThat(cast.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
            FighterActionStateComponent state = Cast(cast, 1);
            if (!FighterSpecialCastRules.IsBusy(in state) || tubman.Stocks < 3) break;
            AssertThat(FighterLedgeRules.IsHanging(in runtime))
                .OverrideFailureMessage($"Step {step}: a ledge was captured during the cast.")
                .IsFalse();
            if (tubman.Velocity.y <= FighterLedgeRules.MaxGrabVerticalSpeed
                && nassau.TryFindLedge(in tubman.Position, out int anchor) && anchor == sternAnchor) {
                busyTicksInBox++;
            }
        }
        AssertThat(busyTicksInBox > 0)
            .OverrideFailureMessage("Harness: the casting fall never crossed the capture box.")
            .IsTrue();
    }

    private static FighterSimulation NassauTubman(FighterStageGeometry nassau) => new(
        FighterLoadout.Default(FighterCharacterID.Joan), Authored("tubman"),
        seed: 9701, rules: FighterMatchRules.Disabled, stageGeometry: nassau);

    /// <summary>Walks player two off the stern edge into the hang, then climb-jumps with a neutral stick. Returns the hang tick.</summary>
    private static int HangThenClimb(FighterSimulation simulation, FighterStageGeometry nassau, int anchor) {
        int segmentIndex = anchor / 2 - nassau.Platforms.Length;
        FP64 edge = nassau.FloorSegments[segmentIndex].EdgeX(anchor % 2);
        int hangTick = -1;
        for (int step = 0; step < 600 && hangTick < 0; step++) {
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent fighter)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
            if (runtime.LedgeAnchor == anchor) {
                hangTick = simulation.CurrentTick;
                break;
            }
            simulation.Advance(Neutral(), In(GameplayButtons.None, moveX: fighter.Position.x < edge ? (sbyte)127 : (sbyte)-127));
        }
        AssertThat(hangTick >= 0).OverrideFailureMessage("Harness: Tubman never caught the stern ledge.").IsTrue();
        Idle(simulation, 20);
        simulation.Advance(Neutral(), In(GameplayButtons.Jump));
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent climbing)).IsTrue();
        AssertThat(FighterLedgeRules.IsHanging(in climbing)).OverrideFailureMessage("Harness: Jump must leave the hang.").IsFalse();
        return hangTick;
    }

    // === 8. Sudden Death and stock loss ========================================

    [TestCase]
    public void SuddenDeathAndAStockLossClearTheCast() {
        // Sudden Death: Conductor's Call pressed five ticks before regulation
        // ends is still in its startup at the transition, which zeroes 323; the
        // scouts never leave.
        int suddenDeathTick = FindSuddenDeathTick();
        FighterSimulation timed = TimedTubmanMatch();
        while (timed.CurrentTick < suddenDeathTick - 5) timed.Advance(Neutral(), Neutral());
        timed.Advance(In(GameplayButtons.Special1), Neutral());
        while (timed.CurrentTick < suddenDeathTick) {
            AssertThat(FighterSpecialCastRules.IsStartup(Cast(timed, 0))).IsTrue();
            timed.Advance(Neutral(), Neutral());
        }
        AssertThat(timed.GetMatchState().SuddenDeathActive).IsEqual(1);
        AssertThat(IsAllZero(Cast(timed, 0))).OverrideFailureMessage("Sudden Death clears the cast.").IsTrue();
        for (int step = 0; step < 30; step++) {
            timed.Advance(Neutral(), Neutral());
            AssertThat(timed.ProjectileCount).OverrideFailureMessage("The cleared cast never fires.").IsEqual(0);
        }

        // Stock loss: Tubman drops through the flat arena's floor, casts on the
        // way down and falls through the bottom blast zone mid-cast.
        var falling = new FighterSimulation(
            Authored("tubman"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 9802, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        Idle(falling, 2);
        AssertThat(falling.TryGetFighter(0, out FighterStateComponent standing)).IsTrue();
        int stocks = standing.Stocks;
        falling.Advance(In(GameplayButtons.Jump | GameplayButtons.Down), Neutral());
        falling.Advance(In(GameplayButtons.Special1), Neutral());
        AssertThat(FighterSpecialCastRules.IsBusy(Cast(falling, 0))).OverrideFailureMessage("Harness: the cast must start.").IsTrue();
        bool knockedOut = false;
        for (int step = 0; step < 120 && !knockedOut; step++) {
            falling.Advance(Neutral(), Neutral());
            AssertThat(falling.TryGetFighter(0, out FighterStateComponent tubman)).IsTrue();
            knockedOut = tubman.Stocks < stocks;
            if (!knockedOut) {
                AssertThat(FighterSpecialCastRules.IsBusy(Cast(falling, 0)))
                    .OverrideFailureMessage("Harness: the KO must come mid-cast.")
                    .IsTrue();
            }
        }
        AssertThat(knockedOut).OverrideFailureMessage("Harness: Tubman never fell out.").IsTrue();
        falling.Advance(Neutral(), Neutral());
        AssertThat(IsAllZero(Cast(falling, 0))).OverrideFailureMessage("A stock loss clears the cast.").IsTrue();
    }

    private static FighterSimulation TimedTubmanMatch() => new(
        Authored("tubman"), FighterLoadout.Default(FighterCharacterID.Joan),
        stocks: 3, matchSeconds: 2, seed: 9801, spawnDistance: 6,
        rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, false, 0));

    private static int FindSuddenDeathTick() {
        FighterSimulation dry = TimedTubmanMatch();
        for (int step = 0; step < 600; step++) {
            dry.Advance(Neutral(), Neutral());
            if (dry.GetMatchState().SuddenDeathActive == 1) return dry.CurrentTick;
        }
        AssertThat(false).OverrideFailureMessage("Harness: regulation never ended.").IsTrue();
        return -1;
    }

    // === 9. one melee contact, on any active tick ==============================

    [TestCase]
    public void AMeleeSpecialResolvesOneContactOnAnyOfItsActiveTicks() {
        // Splitting Strike (14 / 6 / 22) from four units apart: the opponent
        // walks into the 2.2-unit arc, starting a little later each run. Every
        // run lands at most once; some run lands after the first active tick.
        CharacterData lincoln = Data("lincoln");
        int s = lincoln.SpecialAttackTwo.StartupFrames;
        int a = lincoln.SpecialAttackTwo.ActiveFrames;
        int free = s + a + lincoln.SpecialAttackTwo.RecoveryFrames;
        bool sawLateContact = false;
        for (int delay = 0; delay <= 14; delay++) {
            var simulation = new FighterSimulation(
                FighterLoadoutFactory.FromCharacterData(lincoln), FighterLoadout.Default(FighterCharacterID.Tesla),
                seed: 9901, spawnDistance: 2, rules: FighterMatchRules.Disabled);
            Idle(simulation, 2);
            simulation.Advance(In(GameplayButtons.Special2), Neutral());
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent start)).IsTrue();
            int previousHP = start.CurrentHP;
            int contacts = 0;
            for (int offset = 1; offset <= free; offset++) {
                simulation.Advance(Neutral(), offset >= delay ? In(GameplayButtons.None, moveX: -127) : Neutral());
                AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
                if (target.CurrentHP < previousHP) {
                    contacts++;
                    AssertThat(offset >= s && offset < s + a)
                        .OverrideFailureMessage($"Delay {delay}: a contact on T+{offset}, outside the active window.")
                        .IsTrue();
                    AssertThat(FighterSpecialCastRules.HasFlag(Cast(simulation, 0), FighterSpecialCastRules.FlagMeleeResolved))
                        .IsTrue();
                    if (offset > s) sawLateContact = true;
                }
                previousHP = target.CurrentHP;
            }
            AssertThat(contacts <= 1)
                .OverrideFailureMessage($"Delay {delay}: {contacts} contacts in one execution.")
                .IsTrue();
        }
        AssertThat(sawLateContact)
            .OverrideFailureMessage("No run landed on a later active tick.")
            .IsTrue();
    }

    // === 10. the zero-contract fallback ========================================

    [TestCase]
    public void TheZeroContractFallbackReproducesPressTickTiming() {
        // FighterLoadout.Default projects no ability: its Specials run as 0 / 1 / 0.
        var simulation = new FighterSimulation(
            FighterCharacterID.Einstein, FighterCharacterID.Joan, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        Idle(simulation, 2);
        simulation.Advance(In(GameplayButtons.Special1), Neutral());
        FighterActionStateComponent cast = Cast(simulation, 0);
        AssertThat(FighterSpecialCastRules.HasFlag(in cast, FighterSpecialCastRules.FlagFallbackTiming)).IsTrue();
        AssertThat(FighterSpecialCastRules.HasFlag(in cast, FighterSpecialCastRules.FlagEffectFired))
            .OverrideFailureMessage("The fallback fires on the press tick.")
            .IsTrue();
        AssertThat(simulation.ZoneCount).IsEqual(1);
        simulation.Advance(In(GameplayButtons.BasicAttack), Neutral());
        AssertThat(FighterSpecialCastRules.IsBusy(Cast(simulation, 0))).IsFalse();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.AttackPhase)
            .OverrideFailureMessage("The tick after a fallback cast is free.")
            .IsNotEqual(FighterBasicAttackRules.PhaseNone);
    }

    // === 11. every cast-lock refusal ===========================================

    [TestCase]
    public void TheCastLockRefusesEveryVerbUntilTheCastEnds() {
        CharacterData joan = Data("joan");
        int s = joan.SpecialAttackOne.StartupFrames;
        int a = joan.SpecialAttackOne.ActiveFrames;
        int free = s + a + joan.SpecialAttackOne.RecoveryFrames;
        var verbs = new (string Name, PlayerInputFrame Input, Func<FighterSimulation, bool> Started, bool PositiveAtFree, bool UntilRecovery)[] {
            ("walk", In(GameplayButtons.None, moveX: 127), sim => Fighter(sim).Velocity.x > FP64.Zero, true, false),
            ("jump", In(GameplayButtons.Jump), sim => Fighter(sim).IsGrounded == 0, true, false),
            ("roll", In(GameplayButtons.Roll, moveX: 127), sim => Runtime(sim).UniversalMovementState != 0, true, false),
            ("basic swing", In(GameplayButtons.BasicAttack), sim => Runtime(sim).AttackPhase != FighterBasicAttackRules.PhaseNone, true, false),
            ("block", Held(GameplayButtons.Block), sim => IsBlockStance(sim, 0), true, true),
            // The chord carries Block, so in recovery it is the Block cancel.
            ("grab chord", In(GameplayButtons.BasicAttack, held: GameplayButtons.Block), sim => Verb(sim).GrabPhase != 0, true, true),
            ("direct grab", In(GameplayButtons.Grab), sim => Verb(sim).GrabPhase != 0, true, false),
            ("Special 2", In(GameplayButtons.Special2), sim => Runtime(sim).SpecialTwoCooldownFrames > 0, true, false),
            ("the Ultimate", In(GameplayButtons.Ultimate), sim => UltimateActivationTestKit.Phase(sim, 0) != FighterUltimateActivationRules.PhaseNone, true, false),
            ("the movement ability", In(GameplayButtons.MovementAbility), sim => Runtime(sim).MovementCooldownFrames > 0, true, false)
        };
        foreach ((string name, PlayerInputFrame input, Func<FighterSimulation, bool> started, bool positive, bool untilRecovery) in verbs) {
            var simulation = new FighterSimulation(
                FighterLoadoutFactory.FromCharacterData(joan), FighterLoadout.Default(FighterCharacterID.Joan),
                seed: 10001, spawnDistance: 6, rules: FighterMatchRules.Disabled);
            Idle(simulation, 40);
            AssertThat(simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
            simulation.Advance(In(GameplayButtons.Special1), Neutral());
            // Block (and the chord holding it) is refused through the startup and
            // active frames; in recovery it is the cast's one exit, the Block cancel.
            int lockedThrough = untilRecovery ? s + a - 1 : free - 1;
            for (int offset = 1; offset <= lockedThrough; offset++) {
                simulation.Advance(input, Neutral());
                AssertThat(FighterSpecialCastRules.IsBusy(Cast(simulation, 0))).IsTrue();
                if (name == "walk" && offset < UniversalMovementRules.RunDecelerationFrames) continue;
                AssertThat(started(simulation))
                    .OverrideFailureMessage($"{name} started on T+{offset}, inside the cast.")
                    .IsFalse();
            }
            if (untilRecovery) continue;
            simulation.Advance(input, Neutral());
            FighterActionStateComponent after = Cast(simulation, 0);
            AssertThat(FighterSpecialCastRules.IsBusy(in after) && after.SpecialSlot == 1)
                .OverrideFailureMessage($"{name}: Special 1's cast still ran on T+S+A+R.")
                .IsFalse();
            if (positive) {
                AssertThat(started(simulation))
                    .OverrideFailureMessage($"{name} must be accepted on T+S+A+R.")
                    .IsTrue();
            }
        }
    }

    // === 12. the thaw tick =====================================================

    [TestCase]
    public void TheCastLockHoldsOnTheCastersOwnHitstopThawTick() {
        // Splitting Strike lands on T+S and freezes Lincoln; the tick his freeze
        // ends (1 → 0) is read by the ability, combat and grab systems, and the
        // cast — still in its active frames — discards it.
        var thawPresses = new (string Name, PlayerInputFrame Input, Func<FighterSimulation, bool> Started)[] {
            ("the grab chord", In(GameplayButtons.BasicAttack, held: GameplayButtons.Block), sim => Verb(sim).GrabPhase != 0),
            ("held Block", Held(GameplayButtons.Block), sim => IsBlockStance(sim, 0)),
            ("the Ultimate", In(GameplayButtons.Ultimate), sim => UltimateActivationTestKit.Phase(sim, 0) != FighterUltimateActivationRules.PhaseNone)
        };
        foreach ((string name, PlayerInputFrame input, Func<FighterSimulation, bool> started) in thawPresses) {
            var simulation = new FighterSimulation(
                Authored("lincoln"), FighterLoadout.Default(FighterCharacterID.Tesla),
                seed: 10101, spawnDistance: 1, rules: FighterMatchRules.Disabled);
            Idle(simulation, 2);
            AssertThat(simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
            simulation.Advance(In(GameplayButtons.Special2), Neutral());
            bool thawed = false;
            for (int step = 0; step < 60 && !thawed; step++) {
                bool thawTick = Verb(simulation).HitstopFrames == 1;
                simulation.Advance(thawTick ? input : Neutral(), Neutral());
                if (!thawTick) continue;
                thawed = true;
                AssertThat(FighterSpecialCastRules.IsActive(Cast(simulation, 0)))
                    .OverrideFailureMessage("Harness: the thaw tick falls in the active frames.")
                    .IsTrue();
                AssertThat(started(simulation))
                    .OverrideFailureMessage($"{name} started on the caster's thaw tick.")
                    .IsFalse();
            }
            AssertThat(thawed).OverrideFailureMessage("Harness: the arc never froze its caster.").IsTrue();
            AssertThat(Fighter(simulation).Influence).IsEqual(FighterMeterRules.Cap);
        }

        // A projectile reaching a caster frozen by her own landed Special, with
        // Block held, is not blocked: the frozen ticks discard the held input, so
        // the stance never rises. The shooter fires from inside the caster's
        // reach just before the Special lands; the search finds the fire tick
        // whose bolt arrives inside the freeze.
        bool proven = false;
        foreach (string caster in new[] { "mozart", "tubman" }) {
            for (int fireOffset = 4; fireOffset <= 40 && !proven; fireOffset++) {
                proven = ShotArrivesInsideTheFreeze(caster, fireOffset);
            }
            if (proven) break;
        }
        AssertThat(proven)
            .OverrideFailureMessage("Harness: no run delivered the shot inside the caster's own freeze.")
            .IsTrue();
    }

    /// <summary>
    /// One run: the caster presses Special 1 at T and holds Block from T+1; the
    /// shooter fires on T+<paramref name="fireOffset"/>. Returns true when the
    /// bolt struck the caster on a tick that began inside her own freeze while
    /// she was casting — and then asserts it was not blocked.
    /// </summary>
    private static bool ShotArrivesInsideTheFreeze(string caster, int fireOffset) {
        var simulation = new FighterSimulation(
            Authored(caster), FighterLoadoutFactory.FromCharacterData(ShooterData()),
            seed: 10102, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        Idle(simulation, 2);
        simulation.Advance(In(GameplayButtons.Special1), Neutral());
        for (int offset = 1; offset <= 80; offset++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
            bool frozenAndCasting = Verb(simulation).HitstopFrames > 0
                && FighterSpecialCastRules.IsBusy(Cast(simulation, 0));
            int charges = before.BlockCharges;
            simulation.Advance(Held(GameplayButtons.Block),
                offset == fireOffset ? In(GameplayButtons.Special1) : Neutral());
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent after)).IsTrue();
            if (after.CurrentHP < before.CurrentHP) {
                if (!frozenAndCasting) return false;
                AssertThat(after.BlockCharges)
                    .OverrideFailureMessage($"{caster}: the frozen caster blocked the shot.")
                    .IsEqual(charges);
                AssertThat(IsBlockStance(simulation, 0)).IsFalse();
                return true;
            }
        }
        return false;
    }

    /// <summary>A shooter whose projectile leaves on the press tick (no authored startup), at 30 u/s.</summary>
    private static CharacterData ShooterData() => new() {
        CharacterID = "leonardo",
        MaxHP = 400,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData {
            ExecutionType = AbilityExecutionType.Projectile,
            BaseDamage = 10f,
            ProjectileSpeed = 1800f,
            ProjectileLifetime = 1f,
            CooldownDuration = 0f,
            StartupFrames = 0
        },
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    // === 13. rollback across the active entry ==================================

    [TestCase]
    public void ARemotePressCorrectedAcrossTheActiveEntryConverges() {
        // Player two's Requiem Chord (10 / 6 / 14) is pressed on tick P but
        // predicted neutral; the correction arrives two ticks after T+S, so the
        // rollback resimulates the startup, the active entry and the shot.
        CharacterData mozart = Data("mozart");
        int s = mozart.SpecialAttackOne.StartupFrames;
        FighterLoadout one = FighterLoadout.Default(FighterCharacterID.Joan);
        FighterLoadout two = FighterLoadoutFactory.FromCharacterData(mozart);
        var reference = new FighterSimulation(one, two, seed: 10201, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        var rolled = new FighterSimulation(one, two, seed: 10201, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        const int press = 10;
        int correctAt = press + s + 2;
        var pressFrame = new PlayerInputFrame { Pressed = GameplayButtons.Special1 };
        for (int tick = 0; tick < 80; tick++) {
            PlayerInputFrame remote = tick == press ? pressFrame : Neutral();
            reference.Advance(Neutral(), remote);
            rolled.AdvanceWithPredictedPlayerTwo(Neutral());
            if (tick == correctAt) {
                AssertThat(rolled.ProjectileCount)
                    .OverrideFailureMessage("Harness: the mispredicted branch has no shot yet.")
                    .IsEqual(0);
                AssertThat(rolled.CorrectPlayerTwoInput(press, pressFrame)).IsTrue();
                AssertThat(rolled.ProjectileCount).IsEqual(reference.ProjectileCount);
                AssertThat(reference.ProjectileCount)
                    .OverrideFailureMessage("Harness: the chord left on T+S.")
                    .IsEqual(1);
            }
            if (tick >= correctAt) {
                AssertThat(rolled.CurrentHash)
                    .OverrideFailureMessage($"Tick {tick}: the rolled-back branch diverged.")
                    .IsEqual(reference.CurrentHash);
            }
        }
        AssertThat(Cast(rolled, 1).SpecialPhase).IsEqual(Cast(reference, 1).SpecialPhase);
    }

    // === 14. the cross-mode frame table ========================================

    [TestCase]
    public void EverySimContractCarriesItsResourcesFramesAndForesightMatchesItsResource() {
        var failures = new List<string>();
        foreach (string hero in Heroes) {
            CharacterData data = Data(hero);
            FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(data);
            Compare(failures, $"{hero} S1", data.SpecialAttackOne, loadout.AbilityModes.SpecialOneHit);
            Compare(failures, $"{hero} S2", data.SpecialAttackTwo, loadout.AbilityModes.SpecialTwoHit);
        }
        AbilityData foresight = Data("tubman").SpecialAttackTwo;
        if (foresight.StartupFrames != TubmanKitRules.ForesightStartupFrames
            || foresight.ActiveFrames != TubmanKitRules.ForesightWindowFrames
            || foresight.RecoveryFrames != TubmanKitRules.ForesightWhiffRecoveryFrames) {
            failures.Add($"Foresight: tubman/special_2.tres {foresight.StartupFrames}/{foresight.ActiveFrames}/{foresight.RecoveryFrames} "
                + $"against TubmanKitRules {TubmanKitRules.ForesightStartupFrames}/{TubmanKitRules.ForesightWindowFrames}/{TubmanKitRules.ForesightWhiffRecoveryFrames}.");
        }
        if (failures.Count > 0) AssertThat(string.Join(" | ", failures)).IsEqual("");
    }

    private static void Compare(List<string> failures, string name, AbilityData ability, in FighterAbilityHitData contract) {
        if (contract.StartupFrames != ability.StartupFrames
            || contract.ActiveFrames != ability.ActiveFrames
            || contract.RecoveryFrames != ability.RecoveryFrames) {
            failures.Add($"{name}: sim {contract.StartupFrames}/{contract.ActiveFrames}/{contract.RecoveryFrames} "
                + $"against .tres {ability.StartupFrames}/{ability.ActiveFrames}/{ability.RecoveryFrames}.");
        }
        if (FighterSpecialCastRules.IsFallback(in contract)) failures.Add($"{name}: projected as the 0 / 1 / 0 fallback.");
    }

    // === 15. the Block-cancel tick keeps only the stance (fix round A) =========

    /// <summary>
    /// Fix round A (CM-FIXA-SIM, ruling (d)): the recovery Block-cancel tick
    /// raises the stance and does nothing else — Story's Block cancel enters
    /// Blocking and returns, so a roll, a jump, a drop-through or a grab pressed
    /// on recovery frame 0 is lost, and only a fresh press on the next tick acts
    /// (from the stance). Before the fix the cancel tick kept every press live,
    /// so a whiffed Special rolled or jumped out on frame 0, the window D3
    /// reserves for a Special that landed.
    /// </summary>
    [TestCase]
    public void TheRecoveryBlockCancelTickRaisesOnlyTheStanceAndEachVerbNeedsAFreshPress() {
        CharacterData joan = Data("joan");
        int s = joan.SpecialAttackOne.StartupFrames;
        int a = joan.SpecialAttackOne.ActiveFrames;
        var rows = new (string Name, GameplayButtons Pressed, GameplayButtons Held, Func<FighterSimulation, bool> Started)[] {
            ("a roll", GameplayButtons.Roll, GameplayButtons.None, sim => Runtime(sim).UniversalMovementState != 0),
            ("a jump", GameplayButtons.Jump, GameplayButtons.None, sim => Fighter(sim).IsGrounded == 0),
            ("a drop-through", GameplayButtons.Jump, GameplayButtons.Down, sim => Fighter(sim).DropThroughFrames > 0),
            ("the grab chord", GameplayButtons.BasicAttack, GameplayButtons.None, sim => Verb(sim).GrabPhase != 0),
            ("the direct grab", GameplayButtons.Grab, GameplayButtons.None, sim => Verb(sim).GrabPhase != 0)
        };
        foreach ((string name, GameplayButtons pressed, GameplayButtons held, Func<FighterSimulation, bool> started) in rows) {
            FighterSimulation simulation = NewJoanSmite(9611);
            for (int offset = 1; offset < s + a; offset++) simulation.Advance(Neutral(), Neutral());
            // T+S+A, recovery frame 0: Block held and the verb pressed together.
            simulation.Advance(In(pressed, held: GameplayButtons.Block | held), Neutral());
            AssertThat(IsBlockStance(simulation, 0))
                .OverrideFailureMessage($"{name}: the Block cancel must raise the stance on recovery frame 0.")
                .IsTrue();
            AssertThat(FighterSpecialCastRules.IsBusy(Cast(simulation, 0))).IsFalse();
            AssertThat(started(simulation))
                .OverrideFailureMessage($"{name} started on the Block-cancel tick.")
                .IsFalse();
            // The next tick: a fresh press acts from the stance, as in Story.
            simulation.Advance(In(pressed, held: GameplayButtons.Block | held), Neutral());
            AssertThat(started(simulation))
                .OverrideFailureMessage($"{name} must act on the tick after the cancel from a fresh press.")
                .IsTrue();
        }
    }

    // === 16. Divine Piercing's Block cancel on recovery frame 0 (fix round A) ==

    /// <summary>
    /// Fix round A (CM-FIXA-SIM): Divine Piercing's lunge ends with its active
    /// window, so its recovery Block cancel opens on recovery frame 0 like every
    /// other Special's. The lunge's 305 state used to linger one tick (its kit
    /// step cancels it on the tick after its last move, after the cast region
    /// has already refused the stance), so the stance rose on recovery frame 1.
    /// Whiffed and connecting (the three thrusts' hitstops pause both clocks).
    /// </summary>
    [TestCase]
    public void DivinePiercingsRecoveryBlockCancelOpensOnItsRecoveryFrameZero() {
        AbilityData piercing = Data("joan").SpecialAttackTwo;
        foreach ((string name, int spawnDistance) in new[] { ("whiffed", 6), ("connecting", 1) }) {
            // The reference run (no Block) finds the tick the cast enters its recovery.
            FighterSimulation reference = NewJoanPiercing(spawnDistance);
            int recoveryOn = -1;
            for (int offset = 1; offset <= 120 && recoveryOn < 0; offset++) {
                reference.Advance(Neutral(), Neutral());
                FighterActionStateComponent cast = Cast(reference, 0);
                if (FighterSpecialCastRules.RecoveryFrame(in cast) == 0) recoveryOn = offset;
            }
            AssertThat(recoveryOn > 0).OverrideFailureMessage($"Harness ({name}): the cast never reached its recovery.").IsTrue();
            if (spawnDistance == 6) {
                AssertThat(recoveryOn)
                    .OverrideFailureMessage("Harness: the whiff's recovery begins on T+S+A.")
                    .IsEqual(piercing.StartupFrames + piercing.ActiveFrames);
            } else {
                AssertThat(recoveryOn > piercing.StartupFrames + piercing.ActiveFrames)
                    .OverrideFailureMessage("Harness: the connecting lunge's hitstops push the recovery back.")
                    .IsTrue();
            }

            // Block held from T+1: the cast lock discards it until recovery frame 0.
            FighterSimulation blocked = NewJoanPiercing(spawnDistance);
            int raisedOn = -1;
            for (int offset = 1; offset <= 120 && raisedOn < 0; offset++) {
                blocked.Advance(Held(GameplayButtons.Block), Neutral());
                if (IsBlockStance(blocked, 0)) raisedOn = offset;
            }
            AssertThat(raisedOn)
                .OverrideFailureMessage($"{name}: the stance must rise on recovery frame 0 (T+{recoveryOn}).")
                .IsEqual(recoveryOn);
            AssertThat(FighterSpecialCastRules.IsBusy(Cast(blocked, 0))).IsFalse();
            AssertThat(Runtime(blocked).UniversalMovementState).IsEqual(0);
        }
    }

    private static FighterSimulation NewJoanPiercing(int spawnDistance) {
        var simulation = new FighterSimulation(
            Authored("joan"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 9621, spawnDistance: spawnDistance, rules: FighterMatchRules.Disabled);
        Idle(simulation, 2);
        simulation.Advance(In(GameplayButtons.Special2), Neutral());
        AssertThat(FighterSpecialCastRules.IsStartup(Cast(simulation, 0))).IsTrue();
        return simulation;
    }

    // === 17. Foresight in the air and on the ground (fix round A) ==============

    /// <summary>
    /// Fix round A (CM-FIXA-SIM): Tubman's Foresight (the Delegated cast on
    /// component 322) follows the aerial-Special rule — cast airborne, it falls
    /// at the shared half gravity through its startup and window (design :1097,
    /// Story's Startup and Active) and on the normal step after — and, as
    /// Story's <c>TubmanForesight</c> writes X = 0 on every executing tick (a
    /// kit writing its own velocity, which Q1 lets override the brake), the
    /// stance is planted, airborne and grounded alike.
    /// </summary>
    [TestCase]
    public void ForesightFallsAtHalfGravityThroughItsStartupAndWindowAndItsStanceIsPlanted() {
        // Airborne: a forward jump, Foresight four ticks in, the stick held on.
        var aerial = new FighterSimulation(
            Authored("tubman"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 9451, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        for (int tick = 0; tick < 12; tick++) aerial.Advance(In(GameplayButtons.None, moveX: 127), Neutral());
        aerial.Advance(In(GameplayButtons.Jump, moveX: 127), Neutral());
        for (int tick = 0; tick < 3; tick++) aerial.Advance(In(GameplayButtons.None, moveX: 127), Neutral());
        aerial.Advance(In(GameplayButtons.Special2, moveX: 127), Neutral());
        FighterActionStateComponent delegated = Cast(aerial, 0);
        AssertThat(FighterSpecialCastRules.IsDelegated(in delegated)).IsTrue();
        AssertThat(FighterSpecialCastRules.HasFlag(in delegated, FighterSpecialCastRules.FlagStartedAerial))
            .OverrideFailureMessage("An airborne Foresight records its aerial start.")
            .IsTrue();
        int maxJumps = Data("tubman").MaxJumpCount;
        int scaledTicks = 0, normalTicks = 0;
        for (int step = 0; step < 60; step++) {
            FighterStateComponent prior = Fighter(aerial);
            if (prior.IsGrounded != 0) break;
            aerial.Advance(In(GameplayButtons.None, moveX: 127), Neutral());
            FighterStateComponent now = Fighter(aerial);
            AssertThat(aerial.TryGetFighterCounter(0, out FighterCounterComponent counter)).IsTrue();
            if (now.IsGrounded != 0 || !FighterForesightRules.IsBusy(in counter)) break;
            bool scaled = counter.Phase is FighterForesightRules.PhaseStartup or FighterForesightRules.PhaseWindow;
            bool jumped = FighterAirPhysics.HasJumpedThisAirtime(prior.RemainingJumps, maxJumps);
            FP64 expected = scaled
                ? FighterAirPhysics.StepMovement(prior.Velocity.y, false, false, jumped, FighterAirPhysics.AerialSpecialGravityScale)
                : FighterAirPhysics.StepMovement(prior.Velocity.y, false, false, jumped);
            AssertThat(now.Velocity.y.RawValue)
                .OverrideFailureMessage($"Step {step} (322 phase {counter.Phase}): vertical velocity.")
                .IsEqual(expected.RawValue);
            AssertThat(now.Velocity.x.RawValue)
                .OverrideFailureMessage($"Step {step}: the airborne stance must be planted.")
                .IsEqual(FP64.Zero.RawValue);
            if (scaled) scaledTicks++; else normalTicks++;
        }
        AssertThat(scaledTicks)
            .OverrideFailureMessage("The startup's remaining ticks and the whole window halve gravity.")
            .IsEqual(TubmanKitRules.ForesightStartupFrames - 1 + TubmanKitRules.ForesightWindowFrames);
        AssertThat(normalTicks > 0)
            .OverrideFailureMessage("Harness: the whiff recovery must run airborne on the normal step.")
            .IsTrue();

        // Grounded: a running Tubman stops at once, not over the 4-frame brake.
        var grounded = new FighterSimulation(
            Authored("tubman"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 9452, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        for (int tick = 0; tick < 20; tick++) grounded.Advance(In(GameplayButtons.None, moveX: 127), Neutral());
        AssertThat(Fighter(grounded).Velocity.x > FP64.Zero).IsTrue();
        grounded.Advance(In(GameplayButtons.Special2, moveX: 127), Neutral());
        FP64 plantedAt = FP64.Zero;
        for (int offset = 1; offset <= 30; offset++) {
            grounded.Advance(In(GameplayButtons.None, moveX: 127), Neutral());
            AssertThat(grounded.TryGetFighterCounter(0, out FighterCounterComponent stance)).IsTrue();
            if (!FighterForesightRules.IsBusy(in stance)) break;
            FighterStateComponent tubman = Fighter(grounded);
            AssertThat(tubman.Velocity.x.RawValue)
                .OverrideFailureMessage($"T+{offset}: the grounded stance must be planted.")
                .IsEqual(FP64.Zero.RawValue);
            if (offset == 1) plantedAt = tubman.Position.x;
            AssertThat(tubman.Position.x.RawValue).IsEqual(plantedAt.RawValue);
        }
    }

    // === 18. the stock-loss tick (fix round A) =================================

    /// <summary>
    /// Fix round A (CM-FIXA-SIM): component 323 is reset on the tick after a
    /// stock loss, so the stock-loss tick itself must not act from the respawn
    /// platform. A melee Special whose active window is running when its caster
    /// falls through the bottom blast zone resolves no contact from the
    /// platform's anchor that tick; and a Special or movement-ability press on
    /// the KO tick is refused, spending nothing.
    /// </summary>
    [TestCase]
    public void AStockLossTickResolvesNoMeleeContactAndTakesNoPressFromTheRespawnPlatform() {
        // (1) A generic melee Special (2 / 60 / 0) cast on the way down.
        FPVector2 anchor = FighterStageGeometry.Default.RespawnPlatformPosition;
        var falling = new FighterSimulation(
            MeleeCaster(), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 9803, spawnDistance: 4, rules: FighterMatchRules.Disabled);
        Idle(falling, 2);
        int stocks = Fighter(falling).Stocks;
        falling.Advance(In(GameplayButtons.Jump | GameplayButtons.Down), Neutral());
        falling.Advance(In(GameplayButtons.Special1), Neutral());
        AssertThat(falling.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        int targetHP = target.CurrentHP;
        bool knockedOut = false;
        for (int step = 0; step < 150 && !knockedOut; step++) {
            AssertThat(falling.TryGetFighter(1, out FighterStateComponent walker)).IsTrue();
            // The opponent walks in to stand within the generic reach of the anchor.
            sbyte walk = walker.Position.x > anchor.x + FP64.FromDouble(1.5) ? (sbyte)-127 : (sbyte)0;
            FighterActionStateComponent before = Cast(falling, 0);
            falling.Advance(Neutral(), In(GameplayButtons.None, moveX: walk));
            knockedOut = Fighter(falling).Stocks < stocks;
            AssertThat(falling.TryGetFighter(1, out FighterStateComponent struck)).IsTrue();
            if (knockedOut) {
                AssertThat(FighterSpecialCastRules.IsActive(in before)
                        && !FighterSpecialCastRules.HasFlag(in before, FighterSpecialCastRules.FlagMeleeResolved))
                    .OverrideFailureMessage("Harness: the KO must come inside the unresolved active window.")
                    .IsTrue();
                FP64 dx = struck.Position.x - anchor.x;
                AssertThat(dx >= FP64.Zero && dx <= FP64.FromInt(2))
                    .OverrideFailureMessage("Harness: the opponent must stand within the generic reach in front of the anchor.")
                    .IsTrue();
            }
            AssertThat(struck.CurrentHP)
                .OverrideFailureMessage($"Step {step}: the falling caster's melee Special struck (KO tick: {knockedOut}).")
                .IsEqual(targetHP);
        }
        AssertThat(knockedOut).OverrideFailureMessage("Harness: the caster never fell out.").IsTrue();

        // (2) A Special 2 or movement-ability press on the KO tick itself.
        int koTick = -1;
        FighterSimulation dry = NewFallingDefault();
        for (int step = 0; step < 150 && koTick < 0; step++) {
            dry.Advance(Neutral(), Neutral());
            if (Fighter(dry).Stocks < stocks) koTick = step;
        }
        AssertThat(koTick >= 0).OverrideFailureMessage("Harness: the default fighter never fell out.").IsTrue();
        foreach ((string name, GameplayButtons pressed) in new[] {
                     ("Special 2", GameplayButtons.Special2), ("the movement ability", GameplayButtons.MovementAbility) }) {
            FighterSimulation pressing = NewFallingDefault();
            for (int step = 0; step < koTick; step++) pressing.Advance(Neutral(), Neutral());
            pressing.Advance(In(pressed), Neutral());
            AssertThat(Fighter(pressing).Stocks).OverrideFailureMessage("Harness: the press tick is the KO tick.").IsEqual(stocks - 1);
            FighterRuntimeComponent runtime = Runtime(pressing);
            AssertThat(runtime.SpecialTwoCooldownFrames + runtime.MovementCooldownFrames + runtime.AbilityLockoutFrames)
                .OverrideFailureMessage($"{name} pressed on the KO tick spent a cooldown or armed the lockout on the platform.")
                .IsEqual(0);
            AssertThat(FighterSpecialCastRules.IsBusy(Cast(pressing, 0))).IsFalse();
        }
    }

    /// <summary>The default kit, dropped through the legacy arena's floor and falling (no cast).</summary>
    private static FighterSimulation NewFallingDefault() {
        var simulation = new FighterSimulation(
            FighterCharacterID.Einstein, FighterCharacterID.Joan,
            seed: 9804, spawnDistance: 4, rules: FighterMatchRules.Disabled);
        Idle(simulation, 2);
        simulation.Advance(In(GameplayButtons.Jump | GameplayButtons.Down), Neutral());
        return simulation;
    }

    /// <summary>The default kit with Special 1 a generic Melee-execution Special of 2 / 60 / 0.</summary>
    private static FighterLoadout MeleeCaster() {
        FighterLoadout baseline = FighterLoadout.Default(FighterCharacterID.Einstein);
        return new FighterLoadout(
            baseline.CharacterID, baseline.MaxHP, baseline.MaxBlockCharges, baseline.MaxJumpCount,
            baseline.BasicDamage, baseline.SpecialOneDamage, baseline.SpecialTwoDamage, baseline.UltimateDamage,
            baseline.SpecialOneCooldownFrames, baseline.SpecialTwoCooldownFrames,
            baseline.SpecialOneStatusType, baseline.SpecialOneStatusFrames,
            baseline.SpecialTwoStatusType, baseline.SpecialTwoStatusFrames,
            baseline.UltimateStatusType, baseline.UltimateStatusFrames,
            baseline.Weight, baseline.MoveSpeed, baseline.JumpSpeed,
            baseline.BasicKnockback, baseline.SpecialOneKnockback, baseline.SpecialTwoKnockback, baseline.UltimateKnockback,
            baseline.SpecialOneStatusIntensity, baseline.SpecialTwoStatusIntensity, baseline.UltimateStatusIntensity,
            FighterAbilityLoadout.Default with {
                SpecialOneExecutionType = FighterSpecialCastRules.MeleeExecutionType,
                SpecialOneHit = FighterAbilityHitData.DefaultSpecial with {
                    Launches = false, StartupFrames = 2, ActiveFrames = 60, RecoveryFrames = 0, TotalFrames = 62
                }
            });
    }

    // === helpers ==============================================================

    private static CharacterData Data(string hero) =>
        AuthoredResources.Load<CharacterData>($"res://resources/Characters/{hero}_data.tres");

    private static FighterLoadout Authored(string hero) => FighterLoadoutFactory.FromCharacterData(Data(hero));

    private static FighterActionStateComponent Cast(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent cast)).IsTrue();
        return cast;
    }

    private static FighterStateComponent Fighter(FighterSimulation simulation) {
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
        return fighter;
    }

    private static FighterRuntimeComponent Runtime(FighterSimulation simulation) {
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        return runtime;
    }

    private static FighterVerbComponent Verb(FighterSimulation simulation) {
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent verb)).IsTrue();
        return verb;
    }

    private static bool IsBlockStance(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent fighter)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)).IsTrue();
        return FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verb);
    }

    private static bool IsAllZero(in FighterActionStateComponent state) =>
        state.SpecialSlot == 0 && state.SpecialPhase == 0 && state.SpecialPhaseFrames == 0
        && state.SpecialPhaseElapsed == 0 && state.SpecialFlags == 0 && state.SpecialCancelBuffer == 0
        && state.BlockTiming == 0 && state.DownTapFrames == 0 && state.ActionFlags == 0;

    private static int Entities(FighterSimulation simulation) =>
        simulation.ProjectileCount + simulation.ZoneCount + simulation.PersistentObjectCount;

    /// <summary>Whether player one's Special has taken effect: its entity, its lunge, its active frames (a whiffing melee arc) or Foresight's window.</summary>
    private static bool Effect(FighterSimulation simulation, bool foresight, bool meleeWhiff, int entitiesBefore) {
        if (foresight) {
            AssertThat(simulation.TryGetFighterCounter(0, out FighterCounterComponent counter)).IsTrue();
            return counter.Phase == FighterForesightRules.PhaseWindow;
        }
        FighterActionStateComponent cast = Cast(simulation, 0);
        if (meleeWhiff) return !FighterSpecialCastRules.IsStartup(in cast);
        bool fired = FighterSpecialCastRules.HasFlag(in cast, FighterSpecialCastRules.FlagEffectFired);
        bool lunging = Runtime(simulation).UniversalMovementState == FighterKitMotion.PiercingLunge;
        return fired && (Entities(simulation) > entitiesBefore || lunging);
    }

    private static void Idle(FighterSimulation simulation, int ticks) {
        for (int tick = 0; tick < ticks; tick++) simulation.Advance(Neutral(), Neutral());
    }

    private static PlayerInputFrame Neutral() => new();

    private static PlayerInputFrame In(
        GameplayButtons pressed, sbyte moveX = 0, sbyte moveY = 0, GameplayButtons held = GameplayButtons.None) => new() {
        MoveX = moveX,
        MoveY = moveY,
        Pressed = pressed,
        Held = held | pressed
    };

    private static PlayerInputFrame Held(GameplayButtons held) => new() { Held = held };
}
