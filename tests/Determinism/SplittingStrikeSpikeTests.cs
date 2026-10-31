using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7b (LN03) — Lincoln's Splitting Strike in the sim, on his
/// authored data: a 2.2-unit overhead arc that catches grounded and airborne
/// targets alike, SPIKES an airborne target straight down (W1's signed
/// knockback), and gives a grounded one a horizontal shove with no launch.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SplittingStrikeSpikeTests {

    [TestCase]
    public void TheArcSpikesAnAirborneTargetDownward() {
        // Decision 1.2 (2026-10-16): the arc strikes on the cast's active entry,
        // after the authored 14-frame startup. Lincoln presses first; the
        // opponent hops so they are rising in front of him when it lands — six
        // ticks after the hop, as when the arc struck on the press tick.
        int startup = Lincoln().AbilityModes.SpecialTwoHit.StartupFrames;
        AssertThat(startup).IsEqual(14);
        var simulation = NewMatch();
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        int hop = startup - 6;
        for (int tick = 1; tick < startup; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, tick == hop ? GameplayButtons.Jump : GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent rising)).IsTrue();
        AssertThat(rising.IsGrounded).IsEqual(0);
        AssertThat(rising.Velocity.y > FP64.Zero).IsTrue();
        AssertThat(rising.CurrentHP).IsEqual(100);

        simulation.Advance(Frame(startup, GameplayButtons.None), Frame(startup, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent spiked)).IsTrue();
        AssertThat(spiked.CurrentHP).IsEqual(64);
        AssertThat(spiked.Velocity.y < FP64.Zero)
            .OverrideFailureMessage("LN03: an airborne target is spiked straight down.")
            .IsTrue();
    }

    [TestCase]
    public void AGroundedTargetTwoUnitsAwayTakesAHorizontalShoveAndStaysDown() {
        var simulation = NewMatch();
        // Two units apart: past the generic 2-unit melee gate's edge but inside
        // the 2.2-unit arc.
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        // Decision 1.2 (2026-10-16): the arc strikes on the cast's active entry.
        SpecialCastTestKit.StepUntilFired(simulation, 0, 1);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(64);
        AssertThat(target.IsGrounded).IsEqual(1);
        AssertThat(target.Velocity.y).IsEqual(FP64.Zero);
        AssertThat(target.Velocity.x > FP64.Zero).IsTrue();
    }

    /// <summary>
    /// Decision 1.1(2) (plan §2.1.2, FIX-SPLITTING-STRIKE-FREE-WHIFF): an
    /// accepted press always spends. Out of reach, the arc is still accepted on
    /// press — its cooldown and the 30-frame ability lockout armed — plays its
    /// 14 / 6 / 22 frames and lands nothing.
    /// </summary>
    [TestCase]
    public void AnOutOfReachPressSpendsItsCostAndPlaysItsWholeWhiff() {
        AbilityData arc = AuthoredResources.Load<CharacterData>("res://resources/Characters/lincoln_data.tres").SpecialAttackTwo;
        AssertThat(arc.StartupFrames).IsEqual(14);
        AssertThat(arc.ActiveFrames).IsEqual(6);
        AssertThat(arc.RecoveryFrames).IsEqual(22);
        var simulation = new FighterSimulation(
            Lincoln(), FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: 98, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent spent)).IsTrue();
        AssertThat(spent.SpecialTwoReuseFrames > 0)
            .OverrideFailureMessage("An out-of-reach press spends its cooldown.")
            .IsTrue();
        AssertThat(spent.AbilityLockoutFrames).IsEqual(BasicComboRules.AbilityLockoutFrames);
        int free = arc.StartupFrames + arc.ActiveFrames + arc.RecoveryFrames;
        int swingOn = -1;
        for (int tick = 1; tick <= free + 2 && swingOn < 0; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.BasicAttack), Frame(tick, GameplayButtons.None));
            AssertThat(simulation.TryGetFighterActionState(0, out FighterActionStateComponent cast)).IsTrue();
            int expected = tick < arc.StartupFrames ? FighterSpecialCastRules.PhaseStartup
                : tick < arc.StartupFrames + arc.ActiveFrames ? FighterSpecialCastRules.PhaseActive
                : tick < free ? FighterSpecialCastRules.PhaseRecovery
                : FighterSpecialCastRules.PhaseNone;
            AssertThat(cast.SpecialPhase).OverrideFailureMessage($"T+{tick}: the cast phase.").IsEqual(expected);
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            if (runtime.AttackPhase != FighterBasicAttackRules.PhaseNone) swingOn = tick;
        }
        AssertThat(swingOn).OverrideFailureMessage("The whiff is punishable to its last frame.").IsEqual(free);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent untouched)).IsTrue();
        AssertThat(untouched.CurrentHP).IsEqual(untouched.MaxHP);
    }

    /// <summary>
    /// Decision 1.1(2): acceptance runs before the movement-ability branch, so a
    /// same-tick Splitting Strike + movement press casts Splitting Strike — even
    /// out of reach — and Rail Charge does not start (it used to take the tick
    /// when the arc missed, ledger VERIFY-SAME-TICK-VERB-PARITY (5)).
    /// </summary>
    [TestCase]
    public void ASameTickSplittingStrikeAndMovementPressCastsSplittingStrike() {
        var simulation = new FighterSimulation(
            Lincoln(), FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: 97, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        simulation.Advance(
            Frame(0, GameplayButtons.Special2 | GameplayButtons.MovementAbility), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterActionState(0, out FighterActionStateComponent cast)).IsTrue();
        AssertThat(cast.SpecialSlot).IsEqual(2);
        AssertThat(FighterSpecialCastRules.IsStartup(in cast)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.SpecialTwoReuseFrames > 0).IsTrue();
        AssertThat(runtime.MovementCooldownFrames)
            .OverrideFailureMessage("The accepted Special takes the tick from the movement ability.")
            .IsEqual(0);
        AssertThat(runtime.UniversalMovementState).IsEqual(0);
    }

    private static FighterLoadout Lincoln() => FighterLoadoutFactory.FromCharacterData(
        AuthoredResources.Load<CharacterData>("res://resources/Characters/lincoln_data.tres"));

    private static FighterSimulation NewMatch() => new(
        Lincoln(),
        FighterLoadout.Default(FighterCharacterID.Tesla),
        seed: 99,
        spawnDistance: 1,
        rules: FighterMatchRules.Disabled);

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        Held = pressed,
        Pressed = pressed
    };
}
