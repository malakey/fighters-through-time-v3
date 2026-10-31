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
/// Combat and movement update 2026-10-16, decision 11.1 (plan §2.11.1,
/// CM-CANCEL): D3 in Fighter, the sim twin of <c>StorySpecialRecoveryCancelTests</c>.
/// A Special that LANDED a direct hit (D1a: damage applied, or a shield or
/// barrier layer absorbed it) cancels its recovery into a roll or a jump from
/// recovery frame 0 and into a basic (by the direction held at the press) or the
/// movement ability from frame 6; a whiff or a block keeps its whole recovery,
/// whose only exit is the grounded Block cancel. One cancel press is buffered
/// for six unfrozen ticks on component 323 (<see cref="FighterSpecialCancelRules"/>).
/// Production loadouts from the authored <c>*_data.tres</c>, through
/// <see cref="FighterLoadoutFactory"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterSpecialRecoveryCancelTests {

    // === 1. each verb, only after a landed hit, only from its frame ==========

    /// <summary>
    /// Lincoln's Splitting Strike (14 / 6 / 22) lands on its active entry. With
    /// the verb pressed on every tick from T+1, a roll and a jump start on
    /// recovery frame 0 and a basic and the movement ability on frame 6 — never
    /// sooner — and the cast ends on that tick.
    /// </summary>
    [TestCase]
    public void EachVerbFiresOnlyFromItsOwnRecoveryFrameAfterALandedHit() {
        var failures = new List<string>();
        foreach ((string name, GameplayButtons press, int expectedFrame, Func<FighterSimulation, bool> started) in Verbs()) {
            FighterSimulation simulation = LincolnSplitting(seed: 11101, spawnDistance: 1, out int hpBefore);
            int firedFrame = int.MinValue;
            for (int offset = 1; offset <= 80 && firedFrame == int.MinValue; offset++) {
                int frame = UpcomingRecoveryFrame(simulation, 0);
                bool startedBefore = started(simulation);
                simulation.Advance(In(press), Neutral());
                if (!startedBefore && started(simulation)) firedFrame = frame;
            }
            AssertThat(Fighter(simulation, 1).CurrentHP < hpBefore)
                .OverrideFailureMessage($"Harness ({name}): the arc must land.")
                .IsTrue();
            if (firedFrame != expectedFrame) failures.Add($"{name} started on recovery frame {firedFrame}, not {expectedFrame}");
            if (FighterSpecialCastRules.IsBusy(Cast(simulation, 0))) failures.Add($"{name}: the cast survived its cancel");
        }
        if (failures.Count > 0) AssertThat(string.Join(" | ", failures)).IsEqual("");
    }

    // === 2. a whiff or a block keeps its recovery ===========================

    /// <summary>
    /// A whiffed Splitting Strike (the target six units away) and a blocked
    /// Righteous Smite keep their whole recovery: every D3 verb pressed on every
    /// tick waits for the cast's natural end (T+S+A+R). The grounded Block cancel
    /// stays the one exit, on recovery frame 0.
    /// </summary>
    [TestCase]
    public void AWhiffOrABlockKeepsItsWholeRecoveryAndBlockIsTheOnlyExit() {
        var failures = new List<string>();
        CharacterData lincoln = Data("lincoln");
        int splittingFree = Total(lincoln.SpecialAttackTwo);
        foreach ((string name, GameplayButtons press, int _, Func<FighterSimulation, bool> started) in Verbs()) {
            FighterSimulation whiff = LincolnSplitting(seed: 11201, spawnDistance: 6, out int hpBefore);
            int startedOn = StartedOn(whiff, press, started, splittingFree + 4);
            if (Fighter(whiff, 1).CurrentHP != hpBefore) failures.Add($"Harness ({name}): the whiff connected");
            if (startedOn != splittingFree) failures.Add($"whiffed: {name} started on T+{startedOn}, not T+{splittingFree}");
        }

        // A blocked Smite: the target holds Block from the start; the wave is
        // blocked (D1a: not landed), so Joan's roll waits for the cast's end —
        // S+A+R plus the blocked contact's freeze on Joan, which pauses the clock.
        CharacterData joan = Data("joan");
        int smiteFree = Total(joan.SpecialAttackOne);
        FighterSimulation blocked = JoanSmiteIntoABlocker(seed: 11202);
        int rollOn = -1;
        int frozenTicks = 0;
        for (int offset = 1; offset <= smiteFree + 20 && rollOn < 0; offset++) {
            if (Verb(blocked, 0).HitstopFrames > 0) frozenTicks++;
            blocked.Advance(In(GameplayButtons.Roll), Held(GameplayButtons.Block));
            if (offset < smiteFree && FighterSpecialCancelRules.LandedDirectHit(Cast(blocked, 0))) {
                failures.Add($"blocked: the cast armed D3 on T+{offset}");
            }
            if (Runtime(blocked, 0).UniversalMovementState != 0) rollOn = offset;
        }
        AssertThat(Fighter(blocked, 1).BlockCharges < 3)
            .OverrideFailureMessage("Harness: the wave must be blocked.")
            .IsTrue();
        if (rollOn != smiteFree + frozenTicks) {
            failures.Add($"blocked: the roll started on T+{rollOn}, not T+{smiteFree + frozenTicks} ({frozenTicks} frozen)");
        }

        // The Block cancel stays the exit: held from T+1, the stance rises on
        // the whiffed arc's recovery frame 0.
        FighterSimulation exit = LincolnSplitting(seed: 11203, spawnDistance: 6, out _);
        int raisedOn = -1;
        int s = lincoln.SpecialAttackTwo.StartupFrames;
        int a = lincoln.SpecialAttackTwo.ActiveFrames;
        for (int offset = 1; offset <= splittingFree && raisedOn < 0; offset++) {
            exit.Advance(Held(GameplayButtons.Block), Neutral());
            if (IsBlockStance(exit, 0)) raisedOn = offset;
        }
        if (raisedOn != s + a) failures.Add($"the Block cancel rose on T+{raisedOn}, not T+{s + a}");
        if (failures.Count > 0) AssertThat(string.Join(" | ", failures)).IsEqual("");
    }

    // === 3. what arms it ======================================================

    /// <summary>
    /// <see cref="FighterSpecialCancelRules.NoteLanded"/> arms only a busy cast
    /// of the hit's own slot, for a Landed or Absorbed contact with positive
    /// damage on a DirectHit slot: never a block, a refusal, zero damage, a Tick
    /// (the Rift, the Vortex) or Construct delivery. In the sim, the Relativity
    /// Rift's landed tick never arms Einstein's cast, and an Aegis-absorbed
    /// Splitting Strike does arm Lincoln's (his roll fires on frame 0).
    /// </summary>
    [TestCase]
    public void TicksConstructsZonesAndZeroDamageNeverArmItAndAnAegisAbsorbedContactDoes() {
        var contract = new FighterAbilityHitData { Delivery = (int)HitDelivery.DirectHit, ActiveFrames = 6 };
        var tick = contract with { Delivery = (int)HitDelivery.Tick };
        var construct = contract with { Delivery = (int)HitDelivery.Construct };
        var rows = new (string Name, FighterHitOutcome Outcome, int Damage, int Slot, FighterAbilityHitData Contract, bool Busy, bool Arms)[] {
            ("landed direct hit", FighterHitOutcome.Landed, 20, 2, contract, true, true),
            ("absorbed direct hit", FighterHitOutcome.Absorbed, 20, 2, contract, true, true),
            ("blocked", FighterHitOutcome.Blocked, 20, 2, contract, true, false),
            ("refused", FighterHitOutcome.Refused, 20, 2, contract, true, false),
            ("zero damage", FighterHitOutcome.Landed, 0, 2, contract, true, false),
            ("a tick delivery", FighterHitOutcome.Landed, 3, 2, tick, true, false),
            ("a construct delivery", FighterHitOutcome.Landed, 6, 2, construct, true, false),
            ("the other slot", FighterHitOutcome.Landed, 20, 1, contract, true, false),
            ("no cast", FighterHitOutcome.Landed, 20, 2, contract, false, false)
        };
        var failures = new List<string>();
        foreach (var row in rows) {
            var cast = new FighterActionStateComponent();
            if (row.Busy) FighterSpecialCastRules.Begin(ref cast, 2, in contract, startedAerial: false);
            FighterSpecialCancelRules.NoteLanded(ref cast, row.Slot, row.Outcome, row.Damage, in row.Contract);
            if (FighterSpecialCancelRules.LandedDirectHit(in cast) != row.Arms) {
                failures.Add($"{row.Name}: armed {FighterSpecialCancelRules.LandedDirectHit(in cast)}, expected {row.Arms}");
            }
        }

        // The Rift (a Tick delivery) lands its pulses during Einstein's own cast:
        // it is thrown 5 units ahead (radius 1.5) and the target stands 4 units
        // away (spawnDistance is the half-gap), inside it from the first pulse.
        var rift = new FighterSimulation(
            Authored("einstein"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 11301, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        Idle(rift, 2);
        int targetHP = Fighter(rift, 1).CurrentHP;
        rift.Advance(In(GameplayButtons.Special2), Neutral());
        bool tickedDuringCast = false;
        for (int offset = 1; offset <= 80 && FighterSpecialCastRules.IsBusy(Cast(rift, 0)); offset++) {
            rift.Advance(In(GameplayButtons.Roll), Neutral());
            if (Fighter(rift, 1).CurrentHP < targetHP && FighterSpecialCastRules.IsBusy(Cast(rift, 0))) {
                tickedDuringCast = true;
                if (FighterSpecialCancelRules.LandedDirectHit(Cast(rift, 0))) failures.Add("a Rift tick armed D3");
            }
            if (Runtime(rift, 0).UniversalMovementState != 0 && FighterSpecialCastRules.IsBusy(Cast(rift, 0))) {
                failures.Add("a roll came out of the Rift's recovery");
            }
        }
        AssertThat(tickedDuringCast)
            .OverrideFailureMessage("Harness: the Rift never ticked on the target during the cast.")
            .IsTrue();

        // An Aegis-absorbed arc is a D1a contact: the roll fires on frame 0.
        FighterSimulation aegis = LincolnSplitting(seed: 11302, spawnDistance: 1, out int aegisHP, seedTarget: sim => {
            AssertThat(sim.SeedFighterAegisForTest(1)).IsTrue();
        });
        int rollFrame = int.MinValue;
        for (int offset = 1; offset <= 80 && rollFrame == int.MinValue; offset++) {
            int frame = UpcomingRecoveryFrame(aegis, 0);
            aegis.Advance(In(GameplayButtons.Roll), Neutral());
            if (Runtime(aegis, 0).UniversalMovementState == (int)UniversalMovementPhase.RollStartup) rollFrame = frame;
        }
        AssertThat(Fighter(aegis, 1).CurrentHP)
            .OverrideFailureMessage("Harness: the Aegis must absorb the arc.")
            .IsEqual(aegisHP);
        if (rollFrame != 0) failures.Add($"after an Aegis-absorbed arc the roll started on recovery frame {rollFrame}, not 0");
        if (failures.Count > 0) AssertThat(string.Join(" | ", failures)).IsEqual("");
    }

    // === 4. an earlier cast's projectile =====================================

    /// <summary>
    /// Einstein fires E=mc² (Special 1) at a distant target, then casts the
    /// Relativity Rift (Special 2) as the lockout allows; the shot lands during
    /// the Rift's cast. Its slot is not the running cast's slot, so it never
    /// arms the Rift's recovery cancel.
    /// </summary>
    [TestCase]
    public void AnEarlierCastsProjectileNeverArmsTheRunningCast() {
        var simulation = new FighterSimulation(
            Authored("einstein"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 11401, spawnDistance: 8, rules: FighterMatchRules.Disabled);
        Idle(simulation, 2);
        int targetHP = Fighter(simulation, 1).CurrentHP;
        simulation.Advance(In(GameplayButtons.Special1), Neutral());
        bool riftCast = false;
        bool landedDuringRift = false;
        for (int offset = 1; offset <= 160 && !landedDuringRift; offset++) {
            bool castingRift = FighterSpecialCastRules.IsBusy(Cast(simulation, 0)) && Cast(simulation, 0).SpecialSlot == 2;
            // Special 2 every tick: accepted once Special 1's cast and lockout allow.
            simulation.Advance(riftCast ? In(GameplayButtons.Roll) : In(GameplayButtons.Special2), Neutral());
            FighterActionStateComponent cast = Cast(simulation, 0);
            if (cast.SpecialSlot == 2) riftCast = true;
            if (Fighter(simulation, 1).CurrentHP < targetHP) {
                AssertThat(castingRift && cast.SpecialSlot == 2)
                    .OverrideFailureMessage($"Harness: the shot landed on T+{offset}, outside the Rift's cast.")
                    .IsTrue();
                landedDuringRift = true;
                AssertThat(FighterSpecialCancelRules.LandedDirectHit(in cast))
                    .OverrideFailureMessage("Special 1's shot armed Special 2's recovery cancel.")
                    .IsFalse();
            }
        }
        AssertThat(landedDuringRift)
            .OverrideFailureMessage("Harness: E=mc² never landed during the Rift's cast.")
            .IsTrue();
        // The Rift's recovery runs out: the roll pressed through it never fires early.
        for (int offset = 0; offset < 80 && FighterSpecialCastRules.IsBusy(Cast(simulation, 0)); offset++) {
            simulation.Advance(In(GameplayButtons.Roll), Neutral());
            if (FighterSpecialCastRules.IsBusy(Cast(simulation, 0))) {
                AssertThat(Runtime(simulation, 0).UniversalMovementState).IsEqual(0);
            }
        }
    }

    // === 5. no Special or Ultimate ================================

    /// <summary>
    /// A landed recovery cancels only into the four D3 verbs: Special 1, Special
    /// 2 and a paid Ultimate, each pressed on every tick, start nothing and leave
    /// the cast to its natural end.
    /// </summary>
    [TestCase]
    public void NoSpecialOrUltimateComesOutOfALandedRecovery() {
        var rows = new (string Name, PlayerInputFrame Input, Func<FighterSimulation, bool> Started)[] {
            ("Special 1", In(GameplayButtons.Special1), sim => Runtime(sim, 0).SpecialOneReuseFrames > 0),
            ("Special 2", In(GameplayButtons.Special2), sim => Cast(sim, 0).SpecialSlot == 2 && Cast(sim, 0).SpecialPhase == FighterSpecialCastRules.PhaseStartup),
            ("the Ultimate", In(GameplayButtons.Ultimate), sim => UltimateActivationTestKit.Phase(sim, 0) != FighterUltimateActivationRules.PhaseNone),
        };
        int recovery = Data("lincoln").SpecialAttackTwo.RecoveryFrames;
        var failures = new List<string>();
        foreach ((string name, PlayerInputFrame input, Func<FighterSimulation, bool> started) in rows) {
            FighterSimulation simulation = LincolnSplitting(seed: 11501, spawnDistance: 1, out int hpBefore, seedTarget: sim => {
                AssertThat(sim.SeedFighterInfluenceForTest(0, 100)).IsTrue();
            });
            bool armed = false;
            int lastRecoveryFrame = -1;
            for (int offset = 1; offset <= 120 && Cast(simulation, 0).SpecialSlot == 2; offset++) {
                simulation.Advance(input, Neutral());
                FighterActionStateComponent cast = Cast(simulation, 0);
                armed |= FighterSpecialCancelRules.LandedDirectHit(in cast);
                // Still the landed Splitting Strike, in its recovery.
                if (cast.SpecialSlot != 2 || !FighterSpecialCastRules.IsRecovery(in cast)) continue;
                lastRecoveryFrame = FighterSpecialCastRules.RecoveryFrame(in cast);
                if (started(simulation)) {
                    failures.Add($"{name} started inside the landed recovery on frame {lastRecoveryFrame}");
                    break;
                }
                if (Fighter(simulation, 0).Influence < FighterMeterRules.Cap) {
                    failures.Add($"{name}: meter was spent inside the recovery");
                    break;
                }
            }
            if (!armed) failures.Add($"Harness ({name}): the arc never armed D3");
            if (Fighter(simulation, 1).CurrentHP >= hpBefore) failures.Add($"Harness ({name}): the arc never landed");
            if (lastRecoveryFrame != recovery - 1) {
                failures.Add($"{name}: the recovery ended on frame {lastRecoveryFrame}, not its last ({recovery - 1})");
            }
        }
        if (failures.Count > 0) AssertThat(string.Join(" | ", failures)).IsEqual("");
    }

    // === 6. the Up / Down variant ============================================

    /// <summary>
    /// The basic cancel swings the variant of the direction held at the PRESS,
    /// so a direction held since never eats it: Up held at the press (released
    /// before frame 6) starts the Up-Attack; a neutral press with Up held at
    /// frame 6 starts string hit 1. The input rewrite carries a press-time Down
    /// into the Down-Air for an airborne fighter and clears a stale held Down.
    /// </summary>
    [TestCase]
    public void TheBasicCancelSwingsTheVariantOfTheDirectionHeldAtThePress() {
        foreach ((string name, sbyte pressY, sbyte laterY, bool expectUp) in new (string, sbyte, sbyte, bool)[] {
                     ("Up at the press", -127, 0, true),
                     ("neutral at the press, Up held after", 0, -127, false)
                 }) {
            FighterSimulation simulation = LincolnSplitting(seed: 11601, spawnDistance: 1, out _);
            bool pressed = false;
            int swungOn = -1;
            for (int offset = 1; offset <= 80 && swungOn < 0; offset++) {
                int frame = UpcomingRecoveryFrame(simulation, 0);
                PlayerInputFrame input = !pressed && frame == 2
                    ? In(GameplayButtons.BasicAttack, moveY: pressY)
                    : In(GameplayButtons.None, moveY: pressed ? laterY : (sbyte)0);
                if (!pressed && frame == 2) pressed = true;
                simulation.Advance(input, Neutral());
                if (Runtime(simulation, 0).AttackPhase == FighterBasicAttackRules.PhaseStartup) swungOn = frame;
            }
            AssertThat(swungOn).OverrideFailureMessage($"{name}: the swing must start on recovery frame 6.").IsEqual(6);
            int flags = Runtime(simulation, 0).AttackFlags;
            AssertThat((flags & FighterBasicAttackRules.FlagUpAttack) != 0)
                .OverrideFailureMessage($"{name}: Up-Attack expected {expectUp}.")
                .IsEqual(expectUp);
            if (!expectUp) {
                AssertThat(flags & FighterBasicAttackRules.VariantMask).IsEqual(0);
                AssertThat(Runtime(simulation, 0).ComboIndex).IsEqual(0);
            }
        }

        // The rewrite itself, airborne: a press-time Down is the Down-Air even
        // with the stick neutral now; a neutral press clears a held Down.
        var airborne = new FighterStateComponent { IsGrounded = 0, Stocks = 3 };
        var downPress = new FighterRuntimeComponent { MoveY = 0 };
        FighterSpecialCancelRules.RewriteInput(ref downPress, FighterSpecialCancelRules.VerbBasic, upHeld: false, downHeld: true);
        AssertThat(FighterBasicAttackRules.SelectVariant(in airborne, in downPress)).IsEqual(BasicComboRules.VariantDownAir);
        var neutralPress = new FighterRuntimeComponent { MoveY = 127, HeldButtons = (int)GameplayButtons.Down };
        FighterSpecialCancelRules.RewriteInput(ref neutralPress, FighterSpecialCancelRules.VerbBasic, upHeld: false, downHeld: false);
        AssertThat(FighterBasicAttackRules.SelectVariant(in airborne, in neutralPress)).IsEqual(BasicComboRules.VariantAirAttack);
        AssertThat(neutralPress.PressedButtons).IsEqual((int)GameplayButtons.BasicAttack);
    }

    // === 7. the Ultimate chord never takes the movement cancel ===============

    /// <summary>
    /// The pad Ultimate chord carries Movement: a Movement press on a tick that
    /// presses the Ultimate is never buffered, and an Ultimate press drops a
    /// buffered Movement press, so the movement cancel never comes out of the
    /// chord (Story's rule).
    /// </summary>
    [TestCase]
    public void TheUltimateChordNeverTakesTheMovementCancel() {
        // The chord on every tick through the recovery.
        FighterSimulation chord = LincolnSplitting(seed: 11701, spawnDistance: 1, out _, seedTarget: sim => {
            AssertThat(sim.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        });
        for (int offset = 1; offset <= 120 && FighterSpecialCastRules.IsBusy(Cast(chord, 0)); offset++) {
            chord.Advance(In(GameplayButtons.MovementAbility | GameplayButtons.Ultimate), Neutral());
            if (FighterSpecialCastRules.IsBusy(Cast(chord, 0))) {
                AssertThat(Runtime(chord, 0).MovementCooldownFrames)
                    .OverrideFailureMessage($"T+{offset}: the chord's Movement half cancelled the recovery.")
                    .IsEqual(0);
            }
        }

        // Movement buffered on frame 1, the Ultimate pressed alone on frame 3:
        // the buffered press is dropped and nothing fires on frame 6.
        FighterSimulation split = LincolnSplitting(seed: 11702, spawnDistance: 1, out _, seedTarget: sim => {
            AssertThat(sim.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        });
        bool sawFrameSix = false;
        for (int offset = 1; offset <= 120 && FighterSpecialCastRules.IsBusy(Cast(split, 0)); offset++) {
            int frame = UpcomingRecoveryFrame(split, 0);
            PlayerInputFrame input = frame == 1 ? In(GameplayButtons.MovementAbility)
                : frame == 3 ? In(GameplayButtons.Ultimate)
                : Neutral();
            split.Advance(input, Neutral());
            if (frame == 2) {
                AssertThat(FighterSpecialCancelRules.BufferedVerb(Cast(split, 0)))
                    .OverrideFailureMessage("Harness: the Movement press must be buffered.")
                    .IsEqual(FighterSpecialCancelRules.VerbMovement);
            }
            if (frame == 6) {
                sawFrameSix = true;
                AssertThat(Runtime(split, 0).MovementCooldownFrames)
                    .OverrideFailureMessage("An Ultimate press must drop a buffered Movement press.")
                    .IsEqual(0);
                AssertThat(FighterSpecialCastRules.IsBusy(Cast(split, 0))).IsTrue();
            }
        }
        AssertThat(sawFrameSix).OverrideFailureMessage("Harness: the recovery never reached frame 6.").IsTrue();
    }

    // === 8. the buffer =========================================================

    /// <summary>
    /// The buffer holds one press for six unfrozen ticks and the caster's own
    /// hitstop never ages it: a Roll pressed on the first frozen tick of the
    /// arc's landing freeze still fires on recovery frame 0; a basic pressed on
    /// recovery frame 0 (six ticks before frame 6) fires on frame 6, one pressed
    /// on the last active tick (seven ticks before) has expired.
    /// </summary>
    [TestCase]
    public void ACancelPressIsBufferedForSixUnfrozenTicksAndHitstopNeverAgesIt() {
        // A Roll in the freeze.
        FighterSimulation frozen = LincolnSplitting(seed: 11801, spawnDistance: 1, out _);
        bool pressedInFreeze = false;
        int freezeTicks = 0;
        int rollFrame = int.MinValue;
        for (int offset = 1; offset <= 80 && rollFrame == int.MinValue; offset++) {
            bool frozenTick = Verb(frozen, 0).HitstopFrames > 0;
            if (frozenTick) freezeTicks++;
            int frame = UpcomingRecoveryFrame(frozen, 0);
            PlayerInputFrame input = frozenTick && !pressedInFreeze ? In(GameplayButtons.Roll) : Neutral();
            if (frozenTick && !pressedInFreeze) pressedInFreeze = true;
            frozen.Advance(input, Neutral());
            if (Runtime(frozen, 0).UniversalMovementState == (int)UniversalMovementPhase.RollStartup) rollFrame = frame;
        }
        AssertThat(pressedInFreeze).OverrideFailureMessage("Harness: the arc never froze its caster.").IsTrue();
        AssertThat(freezeTicks >= 3)
            .OverrideFailureMessage($"Harness: a {freezeTicks}-tick freeze cannot tell aging from not aging.")
            .IsTrue();
        AssertThat(rollFrame)
            .OverrideFailureMessage("A Roll pressed in the caster's own freeze must still fire on recovery frame 0.")
            .IsEqual(0);

        // A basic six and seven unfrozen ticks before recovery frame 6.
        foreach ((string name, int pressFrame, bool fires) in new (string, int, bool)[] {
                     ("pressed on recovery frame 0", 0, true),
                     ("pressed on the last active tick", -1, false)
                 }) {
            FighterSimulation simulation = LincolnSplitting(seed: 11802, spawnDistance: 1, out _);
            bool pressed = false;
            int swungFrame = int.MinValue;
            for (int offset = 1; offset <= 80 && FighterSpecialCastRules.IsBusy(Cast(simulation, 0)); offset++) {
                int frame = UpcomingRecoveryFrame(simulation, 0);
                bool lastActive = FighterSpecialCastRules.IsActive(Cast(simulation, 0))
                    && Cast(simulation, 0).SpecialPhaseFrames == 2 && Verb(simulation, 0).HitstopFrames == 0;
                bool pressNow = !pressed && (pressFrame == 0 ? frame == 0 : lastActive);
                if (pressNow) pressed = true;
                simulation.Advance(pressNow ? In(GameplayButtons.BasicAttack) : Neutral(), Neutral());
                if (Runtime(simulation, 0).AttackPhase == FighterBasicAttackRules.PhaseStartup && swungFrame == int.MinValue) {
                    swungFrame = frame;
                }
                if (frame == 5) {
                    // Still held on frame 5: five or six unfrozen ticks old.
                    FighterActionStateComponent held = Cast(simulation, 0);
                    AssertThat(FighterSpecialCancelRules.BufferedVerb(in held))
                        .OverrideFailureMessage($"{name}: the press must still be buffered on frame 5.")
                        .IsEqual(FighterSpecialCancelRules.VerbBasic);
                    AssertThat(FighterSpecialCancelRules.BufferedAge(in held)).IsEqual(fires ? 5 : 6);
                }
                if (frame == 6 && !fires) {
                    AssertThat(FighterSpecialCancelRules.BufferedVerb(Cast(simulation, 0)))
                        .OverrideFailureMessage($"{name}: a seven-tick-old press must have expired on frame 6.")
                        .IsEqual(FighterSpecialCancelRules.VerbNone);
                }
            }
            AssertThat(pressed).OverrideFailureMessage($"Harness ({name}): the press was never made.").IsTrue();
            AssertThat(swungFrame)
                .OverrideFailureMessage($"{name}: the buffered basic fired on recovery frame {swungFrame}.")
                .IsEqual(fires ? 6 : int.MinValue);
        }
    }

    // === 9. Foresight's sidestep ==============================================

    /// <summary>
    /// Tubman's Foresight catches a basic swing and its answer lands, which arms
    /// D3 from the sidestep (Story's TubmanForesight rule). A roll fires out of
    /// the sidestep, ending the stance and cutting the sidestep's remaining
    /// invulnerability — and only that: a larger grant (a respawn's, say) seeded
    /// during the sidestep survives the cancel. Held Block cancels the sidestep
    /// the same way (the grounded Block cancel).
    /// </summary>
    [TestCase]
    public void ForesightsSidestepCancelEndsTheStanceAndCutsOnlyItsOwnInvulnerability() {
        foreach ((string name, int seededInvulnerability, PlayerInputFrame cancel) in new (string, int, PlayerInputFrame)[] {
                     ("a roll", 0, In(GameplayButtons.Roll)),
                     ("a roll over a larger grant", 200, In(GameplayButtons.Roll)),
                     ("the Block cancel", 0, Held(GameplayButtons.Block))
                 }) {
            FighterSimulation simulation = ForesightCatch(seed: 11901, out int tick);
            FighterCounterComponent counter = Counter(simulation, 0);
            AssertThat(counter.Phase)
                .OverrideFailureMessage($"Harness ({name}): the read must be caught (sidestep).")
                .IsEqual(FighterForesightRules.PhaseSidestep);
            // Let the answer land and the answer's freeze run out, without cancelling.
            for (int i = 0; i < 30 && !FighterSpecialCancelRules.LandedDirectHit(Cast(simulation, 0)); i++, tick++) {
                simulation.Advance(Neutral(), Neutral());
            }
            AssertThat(FighterSpecialCancelRules.LandedDirectHit(Cast(simulation, 0)))
                .OverrideFailureMessage($"Harness ({name}): the answer must land and arm D3.")
                .IsTrue();
            while (Verb(simulation, 0).HitstopFrames > 0) simulation.Advance(Neutral(), Neutral());
            AssertThat(Counter(simulation, 0).Phase).IsEqual(FighterForesightRules.PhaseSidestep);
            if (seededInvulnerability > 0) {
                AssertThat(simulation.SeedFighterInvulnerabilityForTest(0, seededInvulnerability)).IsTrue();
            }
            AssertThat(Fighter(simulation, 0).InvulnerabilityFrames > 0)
                .OverrideFailureMessage($"Harness ({name}): the sidestep is invulnerable.")
                .IsTrue();

            simulation.Advance(cancel, Neutral());
            AssertThat(Counter(simulation, 0).Phase)
                .OverrideFailureMessage($"{name}: the cancel must end the stance.")
                .IsEqual(FighterForesightRules.PhaseNone);
            AssertThat(FighterSpecialCastRules.IsBusy(Cast(simulation, 0))).IsFalse();
            int invulnerability = Fighter(simulation, 0).InvulnerabilityFrames;
            if (seededInvulnerability > 0) {
                AssertThat(invulnerability >= seededInvulnerability - 2)
                    .OverrideFailureMessage($"{name}: the cancel cut a grant that was not the sidestep's ({invulnerability}).")
                    .IsTrue();
            } else {
                AssertThat(invulnerability)
                    .OverrideFailureMessage($"{name}: the sidestep's invulnerability must end with the cancel.")
                    .IsEqual(0);
            }
            if (cancel.IsHeld(GameplayButtons.Block)) {
                AssertThat(IsBlockStance(simulation, 0)).IsTrue();
            } else {
                AssertThat(Runtime(simulation, 0).UniversalMovementState)
                    .IsEqual((int)UniversalMovementPhase.RollStartup);
            }
        }
    }

    /// <summary>
    /// CM-FIXB-SIM (B12): a caught projectile owes its answer to the next tick's
    /// combat pass (projectiles resolve after the combat system), and a Block
    /// held through the window used to cancel the sidestep in that tick's
    /// movement pass first, dropping the answer. Point blank (two units, inside
    /// the 2.5-unit reach), Einstein's E=mc² is caught while Tubman holds Block
    /// from the tick after her press: the 20-damage answer still lands, and the
    /// Block cancel then ends the stance on a later tick, the stance rising.
    /// </summary>
    [TestCase]
    public void AHeldBlockWaitsForTheAnswerForesightOwesAPointBlankProjectile() {
        // The probe: when does the shot reach a Tubman who does nothing?
        FighterSimulation probe = PointBlankShot();
        int contact = -1;
        for (int tick = 0; tick < 90 && contact < 0; tick++) {
            probe.Advance(Neutral(), tick == 0 ? In(GameplayButtons.Special1) : Neutral());
            if (Fighter(probe, 0).CurrentHP < Fighter(probe, 0).MaxHP) contact = tick;
        }
        AssertThat(contact)
            .OverrideFailureMessage("Harness: the point-blank shot never reached Tubman.")
            .IsGreaterEqual(9);

        // Tubman presses Foresight so the shot meets the fourth window tick, and
        // holds Block from the next tick on.
        FighterSimulation simulation = PointBlankShot();
        int press = contact - 8;
        int caught = -1;
        int answered = -1;
        int einsteinHP = Fighter(simulation, 1).CurrentHP;
        for (int tick = 0; tick < 120; tick++) {
            PlayerInputFrame tubman = tick == press
                ? In(GameplayButtons.Special2)
                : tick > press ? Held(GameplayButtons.Block) : Neutral();
            simulation.Advance(tubman, tick == 0 ? In(GameplayButtons.Special1) : Neutral());
            if (caught < 0 && Counter(simulation, 0).CountersLanded > 0) caught = tick;
            if (answered < 0 && Fighter(simulation, 1).CurrentHP < einsteinHP) answered = tick;
            if (caught >= 0 && !FighterSpecialCastRules.IsBusy(Cast(simulation, 0))) break;
        }
        AssertThat(caught).OverrideFailureMessage("Harness: Foresight never caught the shot.").IsGreaterEqual(0);
        AssertThat(Fighter(simulation, 0).CurrentHP).OverrideFailureMessage("A caught shot deals nothing.").IsEqual(Fighter(simulation, 0).MaxHP);
        AssertThat(answered)
            .OverrideFailureMessage("The held Block must not drop the answer the caught shot owes.")
            .IsEqual(caught + 1);
        AssertThat(einsteinHP - Fighter(simulation, 1).CurrentHP)
            .OverrideFailureMessage("The shooter within 2.5 units takes the 20-damage answer.")
            .IsEqual(Authored("tubman").SpecialTwoDamage);
        AssertThat(Counter(simulation, 0).Phase)
            .OverrideFailureMessage("The held Block cancels the sidestep once the answer has landed.")
            .IsEqual(FighterForesightRules.PhaseNone);
        AssertThat(FighterSpecialCastRules.IsBusy(Cast(simulation, 0))).IsFalse();
        AssertThat(IsBlockStance(simulation, 0)).OverrideFailureMessage("The Block cancel raises the stance.").IsTrue();
    }

    /// <summary>Tubman (player one) and Einstein (player two) two units apart (spawned at ∓1) on the flat arena, Einstein facing her.</summary>
    private static FighterSimulation PointBlankShot() => new(
        Authored("tubman"), Authored("einstein"),
        seed: 11902, spawnDistance: 1, rules: FighterMatchRules.Disabled);

    // === 10. the freeze buffer and D3 never double-fire =====================

    /// <summary>
    /// Lincoln's opener lands and a Special 2 press in its freeze rides the
    /// freeze-only buffer into one Splitting Strike on the thaw tick; the arc's
    /// own landing freeze then sees a BasicAttack press, which D3 buffers — not
    /// the string's chain buffer, since no swing runs — and which, with the
    /// arc's five remaining active ticks and recovery frames 0–5 to age through,
    /// has expired by frame 6, so no swing starts at all. The same press made on
    /// recovery frame 1 fires exactly one swing (string hit 1). One press, at
    /// most one action, in each buffer.
    /// </summary>
    [TestCase]
    public void TheFreezeBufferAndTheCancelBufferNeverFireOnePressTwice() {
        var simulation = new FighterSimulation(
            Authored("lincoln"), FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: 12001, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        // Two units apart is past Lincoln's opener (1.36 + the 0.5 hurtbox
        // half-width): walk in to 1.4 and stop first.
        for (int i = 0; i < 60; i++) {
            if (Fighter(simulation, 1).Position.x - Fighter(simulation, 0).Position.x < FP64.FromDouble(1.4)) break;
            simulation.Advance(In(GameplayButtons.None, moveX: 127), Neutral());
        }
        Idle(simulation, 12);
        simulation.Advance(In(GameplayButtons.BasicAttack), Neutral());
        int splittingCasts = 0;
        int swingStarts = 0;
        bool specialPressed = false;
        bool basicPressed = false;
        bool castSeen = false;
        int previousCooldown = 0;
        int previousAttackPhase = Runtime(simulation, 0).AttackPhase;
        for (int offset = 1; offset <= 200; offset++) {
            bool frozen = Verb(simulation, 0).HitstopFrames > 0;
            bool casting = FighterSpecialCastRules.IsBusy(Cast(simulation, 0));
            PlayerInputFrame input = Neutral();
            if (frozen && !casting && !specialPressed && !castSeen) {
                input = In(GameplayButtons.Special2);
                specialPressed = true;
            } else if (frozen && casting && !basicPressed) {
                // The arc's own freeze: one BasicAttack press.
                input = In(GameplayButtons.BasicAttack);
                basicPressed = true;
            }
            simulation.Advance(input, Neutral());
            int cooldown = Runtime(simulation, 0).SpecialTwoReuseFrames;
            if (cooldown > previousCooldown) splittingCasts++;
            previousCooldown = cooldown;
            if (FighterSpecialCastRules.IsBusy(Cast(simulation, 0))) castSeen = true;
            int attackPhase = Runtime(simulation, 0).AttackPhase;
            if (attackPhase == FighterBasicAttackRules.PhaseStartup && previousAttackPhase != FighterBasicAttackRules.PhaseStartup) {
                swingStarts++;
            }
            previousAttackPhase = attackPhase;
        }
        AssertThat(specialPressed).OverrideFailureMessage("Harness: the opener never froze Lincoln.").IsTrue();
        AssertThat(splittingCasts)
            .OverrideFailureMessage("The freeze-buffered Special 2 press must cast Splitting Strike exactly once.")
            .IsEqual(1);
        AssertThat(basicPressed).OverrideFailureMessage("Harness: the arc never froze its caster.").IsTrue();
        // The arc-freeze press sat in D3's buffer through the five active ticks
        // after the thaw and expired before frame 6; the string's chain buffer,
        // with no swing running, never held it. So no swing at all.
        AssertThat(swingStarts)
            .OverrideFailureMessage("The arc-freeze BasicAttack press started a swing.")
            .IsEqual(0);
        // Pressed in the freeze (5 active ticks after it), the basic expired
        // before frame 6. The same press on recovery frame 1 fires exactly once.
        FighterSimulation once = LincolnSplitting(seed: 12002, spawnDistance: 1, out _);
        int swings = 0;
        int prior = 0;
        for (int offset = 1; offset <= 120; offset++) {
            int frame = UpcomingRecoveryFrame(once, 0);
            once.Advance(frame == 1 ? In(GameplayButtons.BasicAttack) : Neutral(), Neutral());
            int phase = Runtime(once, 0).AttackPhase;
            if (phase == FighterBasicAttackRules.PhaseStartup && prior != FighterBasicAttackRules.PhaseStartup) swings++;
            prior = phase;
        }
        AssertThat(swings).OverrideFailureMessage("One buffered press fires one swing.").IsEqual(1);
        AssertThat(Runtime(once, 0).ComboIndex).IsEqual(0);
    }

    // === 11. rollback ===========================================================

    /// <summary>
    /// A snapshot taken before the arc lands, restored into a second simulation
    /// and replayed with the same inputs, reproduces every tick's hash through
    /// the landing, the buffered roll and the cancel; component 323 matches.
    /// </summary>
    [TestCase]
    public void ARestoredSnapshotReplaysTheCancelTickForTick() {
        FighterSimulation original = LincolnSplitting(seed: 12101, spawnDistance: 1, out _);
        var restored = new FighterSimulation(
            Authored("lincoln"), FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: 12101, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        restored.RestoreFullState(original.CaptureFullState());
        AssertThat(restored.CurrentHash).IsEqual(original.CurrentHash);
        bool rolled = false;
        for (int offset = 1; offset <= 70; offset++) {
            PlayerInputFrame input = offset >= 10 ? In(GameplayButtons.Roll) : Neutral();
            long expected = original.Advance(input, Neutral());
            long actual = restored.Advance(input, Neutral());
            AssertThat(actual).OverrideFailureMessage($"T+{offset}: the replay diverged.").IsEqual(expected);
            FighterActionStateComponent a = Cast(original, 0);
            FighterActionStateComponent b = Cast(restored, 0);
            AssertThat(b.SpecialFlags).IsEqual(a.SpecialFlags);
            AssertThat(b.SpecialCancelBuffer).IsEqual(a.SpecialCancelBuffer);
            AssertThat(b.SpecialPhase).IsEqual(a.SpecialPhase);
            rolled |= Runtime(original, 0).UniversalMovementState == (int)UniversalMovementPhase.RollStartup;
        }
        AssertThat(rolled).OverrideFailureMessage("Harness: the run must exercise the cancel.").IsTrue();
    }

    // === 12. the projectile-lifetime invariant ================================

    /// <summary>
    /// Until the meter and Defy update the slot attribution needed no per-projectile
    /// execution record: no Special shot, ground wave, zone or Requiem burst of a
    /// DirectHit slot outlived its slot's earliest re-cast (the cooldown less the
    /// Momentum cap). A Special's earliest re-cast is now the 2 s reuse delay (120
    /// frames), so a shot with a longer flight can still be in the air when the same
    /// slot casts again. This pins exactly which slots can (a tripwire: a new one, or
    /// one that stops, fails here) and the ledger row records the consequence, an
    /// earlier cast's hit that lands during the next cast's recovery arming the D3
    /// landed-hit flag.
    /// </summary>
    [TestCase]
    public void OnlyTheKnownDirectHitSlotsCanOutliveTheReuseDelay() {
        var failures = new List<string>();
        int checkedSlots = 0;
        foreach (string hero in Heroes) {
            CharacterData data = Data(hero);
            FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(data);
            FighterAbilityLoadout modes = loadout.AbilityModes;
            for (int slot = 1; slot <= 2; slot++) {
                FighterAbilityHitData contract = slot == 1 ? modes.SpecialOneHit : modes.SpecialTwoHit;
                int executionType = slot == 1 ? modes.SpecialOneExecutionType : modes.SpecialTwoExecutionType;
                // Melee slots (Splitting Strike, Divine Piercing, Foresight) hit
                // only inside their own cast; constructs are never DirectHit.
                bool outlivesTheCast = executionType == 1 || executionType == 2;
                if (contract.Delivery != (int)HitDelivery.DirectHit || !outlivesTheCast) continue;
                checkedSlots++;
                int lifetime = Math.Max(
                    slot == 1 ? modes.SpecialOneProjectileLifetimeFrames : modes.SpecialTwoProjectileLifetimeFrames,
                    slot == 1 ? modes.SpecialOnePersistentLifetimeFrames : modes.SpecialTwoPersistentLifetimeFrames);
                int window = contract.StartupFrames + lifetime;
                if (hero == "mozart" && slot == 1) {
                    // The Requiem burst rides a zone after the contact.
                    int pulses = Math.Max(1, contract.HitCount);
                    window += KitReachRules.RequiemPulseIntervalFrames * pulses + 1;
                }
                int earliestRecast = BasicComboRules.SpecialReuseDelayFrames;
                if (window >= earliestRecast) failures.Add($"{hero} S{slot}");
            }
        }
        AssertThat(checkedSlots >= 8)
            .OverrideFailureMessage($"Harness: only {checkedSlots} DirectHit projectile/zone slots were checked.")
            .IsTrue();
        failures.Sort(StringComparer.Ordinal);
        AssertThat(string.Join(" | ", failures))
            .OverrideFailureMessage("The DirectHit slots whose shot or zone can outlive the 120-frame reuse delay changed.")
            .IsEqual("einstein S1 | leonardo S1 | mozart S1 | tesla S2");
    }

    // === 13. real input: only an edge is a press ================================

    /// <summary>
    /// With <see cref="PlayerInputFrame.Create"/>-built input (plan §1.8: the
    /// hand-built frames above set Pressed on every tick, so they cannot show
    /// it), a button held down is one press, made on its first tick. Roll pressed
    /// in Splitting Strike's startup and held through the landed recovery has
    /// aged out of the buffer long before recovery frame 0 and never cancels the
    /// cast, which runs to its natural end, and the hold starts no roll after it.
    /// The same Roll released on recovery frame 1 and tapped again on frame 2
    /// fires on frame 2.
    /// </summary>
    [TestCase]
    public void WithRealInputAHeldButtonIsOnePressAndOnlyAFreshTapCancels() {
        // Held from T+1 to the end.
        FighterSimulation held = LincolnSplitting(seed: 13101, spawnDistance: 1, out int hpBefore);
        GameplayButtons previous = GameplayButtons.Special2;
        int recovery = Data("lincoln").SpecialAttackTwo.RecoveryFrames;
        int lastRecoveryFrame = -1;
        bool rolledInCast = false;
        uint tick = 1;
        for (; tick <= 120 && FighterSpecialCastRules.IsBusy(Cast(held, 0)); tick++) {
            held.Advance(PlayerInputFrame.Create(tick, 0f, 0f, GameplayButtons.Roll, previous), Neutral());
            previous = GameplayButtons.Roll;
            FighterActionStateComponent cast = Cast(held, 0);
            if (FighterSpecialCastRules.IsRecovery(in cast)) lastRecoveryFrame = FighterSpecialCastRules.RecoveryFrame(in cast);
            if (FighterSpecialCastRules.IsBusy(in cast) && Runtime(held, 0).UniversalMovementState != 0) rolledInCast = true;
        }
        AssertThat(Fighter(held, 1).CurrentHP < hpBefore)
            .OverrideFailureMessage("Harness: the arc must land.")
            .IsTrue();
        AssertThat(rolledInCast)
            .OverrideFailureMessage("A Roll held since the startup cancelled the landed recovery.")
            .IsFalse();
        AssertThat(lastRecoveryFrame)
            .OverrideFailureMessage("The held Roll cut the recovery short.")
            .IsEqual(recovery - 1);
        for (uint end = tick + 20; tick < end; tick++) {
            held.Advance(PlayerInputFrame.Create(tick, 0f, 0f, GameplayButtons.Roll, previous), Neutral());
            AssertThat(Runtime(held, 0).UniversalMovementState)
                .OverrideFailureMessage("A held Roll is no press: nothing rolls after the cast either.")
                .IsEqual(0);
        }

        // Held, released on recovery frame 1, tapped again on frame 2.
        FighterSimulation tapped = LincolnSplitting(seed: 13102, spawnDistance: 1, out _);
        previous = GameplayButtons.Special2;
        int rollFrame = int.MinValue;
        for (tick = 1; tick <= 120 && rollFrame == int.MinValue; tick++) {
            int frame = UpcomingRecoveryFrame(tapped, 0);
            GameplayButtons now = frame == 1 ? GameplayButtons.None : GameplayButtons.Roll;
            tapped.Advance(PlayerInputFrame.Create(tick, 0f, 0f, now, previous), Neutral());
            previous = now;
            if (Runtime(tapped, 0).UniversalMovementState == (int)UniversalMovementPhase.RollStartup) rollFrame = frame;
        }
        AssertThat(rollFrame)
            .OverrideFailureMessage("A fresh tap after a release must fire the roll on its own recovery frame.")
            .IsEqual(2);
    }

    // === harnesses =============================================================

    private static readonly string[] Heroes = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra", "tesla", "shakespeare", "mozart", "tubman"
    };

    /// <summary>The four D3 verbs on Lincoln: the press, its frame gate and its start test.</summary>
    private static IEnumerable<(string Name, GameplayButtons Press, int Frame, Func<FighterSimulation, bool> Started)> Verbs() {
        yield return ("a roll", GameplayButtons.Roll, 0,
            sim => Runtime(sim, 0).UniversalMovementState == (int)UniversalMovementPhase.RollStartup);
        yield return ("a jump", GameplayButtons.Jump, 0,
            sim => Fighter(sim, 0).IsGrounded == 0 && Fighter(sim, 0).Velocity.y > FP64.Zero);
        yield return ("a basic", GameplayButtons.BasicAttack, 6,
            sim => Runtime(sim, 0).AttackPhase == FighterBasicAttackRules.PhaseStartup);
        yield return ("the movement ability", GameplayButtons.MovementAbility, 6,
            sim => Runtime(sim, 0).MovementCooldownFrames > 0);
    }

    /// <summary>
    /// Lincoln presses Splitting Strike at T with the target
    /// <paramref name="spawnDistance"/> units away. <paramref name="hpBefore"/>
    /// is the target's HP before the press; <paramref name="seedTarget"/> runs
    /// before it.
    /// </summary>
    private static FighterSimulation LincolnSplitting(
        int seed, int spawnDistance, out int hpBefore, Action<FighterSimulation> seedTarget = null) {
        var simulation = new FighterSimulation(
            Authored("lincoln"), FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: seed, spawnDistance: spawnDistance, rules: FighterMatchRules.Disabled);
        Idle(simulation, 2);
        seedTarget?.Invoke(simulation);
        hpBefore = Fighter(simulation, 1).CurrentHP;
        simulation.Advance(In(GameplayButtons.Special2), Neutral());
        AssertThat(FighterSpecialCastRules.IsStartup(Cast(simulation, 0)))
            .OverrideFailureMessage("Harness: Splitting Strike must be accepted into its startup.")
            .IsTrue();
        return simulation;
    }

    /// <summary>
    /// Joan casts Righteous Smite (a 2.5-unit ground wave) at a target two units
    /// away (spawnDistance is the half-gap) that holds Block from the start.
    /// </summary>
    private static FighterSimulation JoanSmiteIntoABlocker(int seed) {
        var simulation = new FighterSimulation(
            Authored("joan"), FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: seed, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        for (int tick = 0; tick < 4; tick++) simulation.Advance(Neutral(), Held(GameplayButtons.Block));
        AssertThat(IsBlockStance(simulation, 1)).OverrideFailureMessage("Harness: the target must be blocking.").IsTrue();
        simulation.Advance(In(GameplayButtons.Special1), Held(GameplayButtons.Block));
        return simulation;
    }

    /// <summary>
    /// Tubman (production) against a default Joan one unit away: Foresight is
    /// pressed, Joan swings into its window, and the read is caught. Returns on
    /// the tick after the catch; <paramref name="tick"/> is the next tick.
    /// </summary>
    private static FighterSimulation ForesightCatch(int seed, out int tick) {
        var simulation = new FighterSimulation(
            Authored("tubman"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: seed, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        tick = 0;
        for (int i = 0; i < 120; i++, tick++) {
            if (Fighter(simulation, 1).Position.x - Fighter(simulation, 0).Position.x < FP64.FromDouble(1.1)) break;
            simulation.Advance(Neutral(), In(GameplayButtons.None, moveX: -127));
        }
        for (int i = 0; i < 12; i++, tick++) simulation.Advance(Neutral(), Neutral());
        simulation.Advance(In(GameplayButtons.Special2), Neutral());
        tick++;
        simulation.Advance(Neutral(), In(GameplayButtons.BasicAttack));
        tick++;
        for (int i = 0; i < 40 && Counter(simulation, 0).CountersLanded == 0; i++, tick++) {
            simulation.Advance(Neutral(), Neutral());
        }
        AssertThat(Counter(simulation, 0).CountersLanded)
            .OverrideFailureMessage("Harness: the swing was never caught by Foresight's window.")
            .IsEqual(1);
        return simulation;
    }

    /// <summary>
    /// The recovery frame the NEXT Advance runs for player <paramref name="playerID"/>'s
    /// clocked cast — 0 when the cast's last active tick is behind it, the next
    /// elapsed index in recovery — or −1 (a frozen tick, startup, active, no cast).
    /// </summary>
    private static int UpcomingRecoveryFrame(FighterSimulation simulation, int playerID) {
        FighterActionStateComponent cast = Cast(simulation, playerID);
        if (Verb(simulation, playerID).HitstopFrames > 0) return -1;
        if (FighterSpecialCastRules.IsActive(in cast) && cast.SpecialPhaseFrames == 1) return 0;
        if (FighterSpecialCastRules.IsRecovery(in cast)) return cast.SpecialPhaseElapsed + 1;
        return -1;
    }

    /// <summary>The offset (from T) on which <paramref name="started"/> first holds, pressing <paramref name="press"/> on every tick.</summary>
    private static int StartedOn(
        FighterSimulation simulation, GameplayButtons press, Func<FighterSimulation, bool> started, int maxOffset) {
        for (int offset = 1; offset <= maxOffset; offset++) {
            simulation.Advance(In(press), Neutral());
            if (started(simulation)) return offset;
        }
        return -1;
    }

    private static int Total(AbilityData ability) => ability.StartupFrames + ability.ActiveFrames + ability.RecoveryFrames;

    private static CharacterData Data(string hero) =>
        AuthoredResources.Load<CharacterData>($"res://resources/Characters/{hero}_data.tres");

    private static FighterLoadout Authored(string hero) => FighterLoadoutFactory.FromCharacterData(Data(hero));

    private static FighterActionStateComponent Cast(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent cast)).IsTrue();
        return cast;
    }

    private static FighterCounterComponent Counter(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterCounter(playerID, out FighterCounterComponent counter)).IsTrue();
        return counter;
    }

    private static FighterStateComponent Fighter(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent fighter)).IsTrue();
        return fighter;
    }

    private static FighterRuntimeComponent Runtime(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)).IsTrue();
        return runtime;
    }

    private static FighterVerbComponent Verb(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)).IsTrue();
        return verb;
    }

    private static bool IsBlockStance(FighterSimulation simulation, int playerID) {
        FighterStateComponent fighter = Fighter(simulation, playerID);
        FighterRuntimeComponent runtime = Runtime(simulation, playerID);
        FighterVerbComponent verb = Verb(simulation, playerID);
        return FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verb);
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
