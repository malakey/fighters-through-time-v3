using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W1 — A12 (Fighter half). The Down-Air is a slam: it spikes the
/// victim straight down, and a slammed victim on or reaching the floor
/// ground-bounces ONCE — forced, so a held Block cannot tech it — into a tumble
/// with the Up-Attack's vertical profile; the next landing is techable
/// normally. The owed bounce is <c>SlamBouncePending</c> on component 320.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DownAirSlamTests {

    [TestCase]
    public void TheSlamSpikesAGroundedVictimWhoBouncesOnceUntechablyThenTechsTheNextLanding() {
        // Tesla jumps over a grounded Joan (spawn distance 0) and slams her.
        var simulation = new FighterSimulation(
            FighterCharacterID.Tesla, FighterCharacterID.Joan,
            seed: 1321, spawnDistance: 0, rules: FighterMatchRules.Disabled);
        int tick = JumpToApex(simulation, 0);
        bool armed = false;
        for (int step = 0; step <= BasicComboRules.DownAirStartupFrames + 2 && !armed; step++, tick++) {
            Advance(simulation, tick,
                p1Pressed: step == 0 ? GameplayButtons.BasicAttack | GameplayButtons.Down : GameplayButtons.None,
                p1Held: GameplayButtons.Down);
            simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent k);
            armed = FighterSlamRules.IsPending(in k);
        }
        AssertThat(armed)
            .OverrideFailureMessage("A connecting slam must owe its victim a ground bounce.")
            .IsTrue();
        simulation.TryGetFighter(1, out FighterStateComponent spiked);
        AssertThat(spiked.Velocity.y < FP64.Zero)
            .OverrideFailureMessage("The slam spikes the victim downward.")
            .IsTrue();

        // Hold Block the whole way: the first contact must still bounce.
        bool bounced = false;
        for (int step = 0; step < 90 && !bounced; step++, tick++) {
            Advance(simulation, tick, p2Held: GameplayButtons.Block);
            simulation.TryGetFighter(1, out FighterStateComponent victim);
            simulation.TryGetFighterVerb(1, out FighterVerbComponent verb);
            simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent k);
            if (!FighterSlamRules.IsPending(in k)) {
                bounced = true;
                AssertThat(verb.TechLockoutFrames)
                    .OverrideFailureMessage("The bounce is forced: holding Block must not tech it.")
                    .IsEqual(0);
                AssertThat(victim.IsGrounded).IsEqual(0);
                AssertThat(victim.Velocity.y > FP64.Zero)
                    .OverrideFailureMessage("The bounce pops the victim back up.")
                    .IsTrue();
                AssertThat(verb.Tumble).IsEqual(1);
            }
        }
        AssertThat(bounced).IsTrue();

        // The next landing techs normally (Block is still held).
        bool teched = false;
        for (int step = 0; step < 120 && !teched; step++, tick++) {
            Advance(simulation, tick, p2Held: GameplayButtons.Block);
            simulation.TryGetFighterVerb(1, out FighterVerbComponent verb);
            teched = verb.TechLockoutFrames > 0;
        }
        AssertThat(teched)
            .OverrideFailureMessage("After the one bounce, the next landing is techable.")
            .IsTrue();
        simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent after);
        AssertThat(FighterSlamRules.IsPending(in after)).IsFalse();
    }

    [TestCase]
    public void TheBounceNeverRepeatsAndIsDroppedWhenTheTumbleEnds() {
        FighterStateComponent fighter = new() { Stocks = 3, HitstunFrames = 20, IsGrounded = 1 };
        FighterRuntimeComponent runtime = default;
        FighterVerbComponent verb = new() { Tumble = 1 };
        FighterKnockdownComponent knockdown = new() { SlamBouncePending = 1, SlamBounceSpeed = FP64.FromInt(6) };
        AssertThat(FighterSlamRules.TryBounce(ref fighter, in runtime, ref verb, ref knockdown)).IsTrue();
        AssertThat(fighter.Velocity.y.RawValue).IsEqual(FP64.FromInt(6).RawValue);
        AssertThat(fighter.HitstunFrames).IsEqual(BasicComboRules.DirectionalAttackHitstunFrames);
        AssertThat(FighterSlamRules.TryBounce(ref fighter, in runtime, ref verb, ref knockdown))
            .OverrideFailureMessage("One bounce per slam.")
            .IsFalse();

        knockdown = new FighterKnockdownComponent { SlamBouncePending = 1, SlamBounceSpeed = FP64.One };
        fighter.HitstunFrames = 0;
        FighterSlamRules.ClearIfStale(in fighter, in verb, ref knockdown);
        AssertThat(FighterSlamRules.IsPending(in knockdown)).IsFalse();
    }

    private static int JumpToApex(FighterSimulation simulation, int startTick) {
        Advance(simulation, startTick, p1Pressed: GameplayButtons.Jump);
        for (int tick = startTick + 1; tick < startTick + 120; tick++) {
            simulation.TryGetFighter(0, out FighterStateComponent fighter);
            if (fighter.IsGrounded == 0 && fighter.Velocity.y <= FP64.Zero) return tick;
            Advance(simulation, tick);
        }
        AssertThat(false).OverrideFailureMessage("The jump never reached its apex.").IsTrue();
        return startTick;
    }

    private static void Advance(
        FighterSimulation simulation, int tick,
        GameplayButtons p1Pressed = GameplayButtons.None,
        GameplayButtons p1Held = GameplayButtons.None,
        GameplayButtons p2Held = GameplayButtons.None) =>
        simulation.Advance(
            new PlayerInputFrame { Tick = (uint)tick, Held = p1Pressed | p1Held, Pressed = p1Pressed },
            new PlayerInputFrame { Tick = (uint)tick, Held = p2Held });
}
