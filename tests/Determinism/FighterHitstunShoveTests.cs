using FTT.Core;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static FTT.Tests.Determinism.KnockbackFeelTestKit;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// FU-KNOCK R3 (feel and UI update 2026-10-25; ledger
/// <c>USER-FIGHTER-HITSTUN-SHOVE</c>): a grounded victim of a non-launching hit
/// no longer glides at constant speed for the whole stun and stops dead. The
/// hit site scales its speed and strong friction stops it within about 15
/// ticks, after which it is planted for the rest of the hitstun. Light hits move
/// about 0.2 units and heavy ones about 0.5. Launches, airborne non-launchers
/// and the guard-break push keep their frictionless motion.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterHitstunShoveTests {

    /// <summary>The victim's x after each tick from the connect (index 0 = the connect tick), plus the rest of its state.</summary>
    private static List<(double X, double Vx, int Hitstun, bool Grounded)> Trace(
        FighterSimulation sim, int connectTick, int ticks) {
        var trace = new List<(double, double, int, bool)>();
        sim.TryGetFighter(1, out FighterStateComponent f);
        trace.Add((D(f.Position.x), D(f.Velocity.x), f.HitstunFrames, f.IsGrounded != 0));
        int tick = connectTick + 1;
        for (int i = 0; i < ticks; i++) {
            Idle(sim, ref tick);
            sim.TryGetFighter(1, out f);
            trace.Add((D(f.Position.x), D(f.Velocity.x), f.HitstunFrames, f.IsGrounded != 0));
        }
        return trace;
    }

    private static List<(double X, double Vx, int Hitstun, bool Grounded)> StringHit(float knockback, int step) {
        FighterLoadout attacker = FighterLoadoutFactory.FromCharacterData(Attacker(knockback));
        FighterLoadout victim = FighterLoadoutFactory.FromCharacterData(Victim());
        (FighterSimulation sim, int connect, _) = WalkToHit(attacker, victim, step);
        return Trace(sim, connect, 45);
    }

    [TestCase]
    public void ALightHitMovesTheVictimAboutTwoTenthsOfAUnit() {
        var t = StringHit(2f, step: 0);
        double moved = System.Math.Abs(t[30].X - t[0].X);
        AssertThat(moved >= 0.15 && moved <= 0.23)
            .OverrideFailureMessage($"a light hit should move about 0.2 u, moved {moved:0.000}").IsTrue();
    }

    [TestCase]
    public void AHeavyHitMovesTheVictimAboutHalfAUnit() {
        var t = StringHit(5f, step: 0);
        double moved = System.Math.Abs(t[30].X - t[0].X);
        AssertThat(moved >= 0.43 && moved <= 0.56)
            .OverrideFailureMessage($"a heavy hit should move about 0.5 u, moved {moved:0.000}").IsTrue();
    }

    [TestCase]
    public void MostOfTheShoveHappensInSevenTicksItStopsByFifteenAndTheVictimIsPlantedForTheRestOfTheStun() {
        foreach (float knockback in new[] { 2f, 5f }) {
            foreach (int step in new[] { 0, 1 }) {
                var t = StringHit(knockback, step);
                // The first tick the victim moves (the impact freeze holds it still before that).
                int first = 1;
                while (first < t.Count && t[first].X == t[0].X) first++;
                double total = System.Math.Abs(t[44].X - t[0].X);
                double inSeven = System.Math.Abs(t[first + 6].X - t[0].X);
                string label = $"kb {knockback} hit {step + 1}";
                AssertThat(inSeven >= 0.75 * total)
                    .OverrideFailureMessage($"{label}: only {inSeven / total:0.00} of the shove in its first seven ticks").IsTrue();
                AssertThat(t[first + 16].Vx).OverrideFailureMessage($"{label}: still moving 16 ticks in").IsEqual(0.0);
                // Planted: no movement from there to the end of the stun (hitstun is still running).
                AssertThat(t[first + 16].Hitstun).OverrideFailureMessage($"{label}: setup, hitstun must outlast the shove").IsGreater(0);
                AssertThat(t[first + 16].X).IsEqual(t[Last(t, first + 16)].X);
            }
        }
    }

    /// <summary>The last trace index still inside the victim's hitstun, from <paramref name="from"/>.</summary>
    private static int Last(List<(double X, double Vx, int Hitstun, bool Grounded)> t, int from) {
        int last = from;
        for (int i = from; i < t.Count && t[i].Hitstun > 0; i++) last = i;
        return last;
    }

    [TestCase]
    public void TheShoveScalesWithTheHitSoAHarderHitMovesTheVictimFarther() {
        double light = System.Math.Abs(StringHit(2f, 0)[30].X - StringHit(2f, 0)[0].X);
        double heavy = System.Math.Abs(StringHit(5f, 0)[30].X - StringHit(5f, 0)[0].X);
        AssertThat(heavy / light >= 2.2 && heavy / light <= 2.9)
            .OverrideFailureMessage($"distance should track the hit (5/2 = 2.5x): {heavy:0.00} / {light:0.00}").IsTrue();
    }

    [TestCase]
    public void ALaunchKeepsItsFlightUntouchedByTheShove() {
        // The finisher launches; its horizontal speed holds through the launch hitstun (no decay).
        FighterLoadout attacker = FighterLoadoutFactory.FromCharacterData(Attacker(2.0f));
        FighterLoadout victim = FighterLoadoutFactory.FromCharacterData(Victim());
        (FighterSimulation sim, int connect, _) = WalkToHit(attacker, victim, step: 2);
        var t = Trace(sim, connect, 20);
        // Past the impact freeze, while the launch hitstun runs, vx is constant.
        int first = 0;
        while (first < t.Count && t[first].Grounded) first++;
        AssertThat(first).OverrideFailureMessage("setup: the finisher must launch").IsLess(t.Count - 8);
        double v = t[first].Vx;
        AssertThat(v).IsGreater(0.0);
        for (int i = first; i < first + 8; i++) {
            AssertThat(t[i].Vx).OverrideFailureMessage($"tick {i}: the flight must not decay").IsEqual(v);
        }
    }

    [TestCase]
    public void AnAirborneVictimOfANonLaunchingHitKeepsItsVectorWithoutTheShove() {
        FighterLoadout attacker = FighterLoadoutFactory.FromCharacterData(Attacker(4.0f));
        FighterLoadout victim = FighterLoadoutFactory.FromCharacterData(Victim());
        var sim = new FighterSimulation(attacker, victim, seed: 4777, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        // Both fighters jump at tick 0; the attacker's air strike meets the victim at the same height.
        int connect = -1;
        for (int tick = 0; tick < 40 && connect < 0; tick++) {
            bool press = tick == 8;
            sim.TryGetFighter(1, out FighterStateComponent v0);
            int hp = v0.CurrentHP;
            sim.Advance(Frame(tick, press ? GameplayButtons.BasicAttack : tick == 0 ? GameplayButtons.Jump : GameplayButtons.None),
                Frame(tick, tick == 0 ? GameplayButtons.Jump : GameplayButtons.None));
            sim.TryGetFighter(1, out FighterStateComponent v1);
            if (v1.CurrentHP < hp) connect = tick;
        }
        AssertThat(connect).OverrideFailureMessage("setup: the swing must hit the airborne victim").IsGreater(0);
        sim.TryGetFighter(1, out FighterStateComponent struck);
        AssertThat(struck.IsGrounded).OverrideFailureMessage("setup: the victim must be airborne").IsEqual(0);
        double force = D(struck.Velocity.x);
        // No shove multiplier at the hit site: the plain force, 4.0 / (1 + weight 1) times the missing-HP ramp.
        double expectedForce = 4.0 / 2.0 * (2.0 - (double)struck.CurrentHP / struck.MaxHP);
        AssertThat(System.Math.Abs(force - expectedForce) < 0.01).OverrideFailureMessage($"force {force:0.000} != {expectedForce:0.000}").IsTrue();
        sim.TryGetFighterVerb(1, out FighterVerbComponent verb);
        AssertThat(verb.Tumble).OverrideFailureMessage("a non-launcher is not a tumble").IsEqual(FighterVerbRules.TumbleNone);
        int next = connect + 1;
        for (int i = 0; i < 5; i++) {
            Idle(sim, ref next);
            sim.TryGetFighter(1, out FighterStateComponent f);
            if (f.IsGrounded != 0 || f.HitstunFrames <= 0) break;
            AssertThat(D(f.Velocity.x)).OverrideFailureMessage("an airborne non-launcher keeps its vector").IsEqual(force);
        }
    }

    [TestCase]
    public void TheGuardBreakPushKeepsItsFrictionlessSlide() {
        // The victim blocks all three string hits; the third shatters the guard and daze carries the
        // authored push (2.0 u/s away, 60 frames) at constant speed.
        FighterLoadout attacker = FighterLoadoutFactory.FromCharacterData(Attacker(2.0f));
        FighterLoadout victim = FighterLoadoutFactory.FromCharacterData(Victim());
        var sim = new FighterSimulation(attacker, victim, seed: 4600, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        int daze = -1;
        for (int tick = 0; tick < 400 && daze < 0; tick++) {
            sim.TryGetFighterRuntime(0, out FighterRuntimeComponent rt);
            bool press = rt.AttackPhase == FighterBasicAttackRules.PhaseNone
                || rt.AttackPhase == FighterBasicAttackRules.PhaseChainHold;
            sim.Advance(Frame(tick, press ? GameplayButtons.BasicAttack : GameplayButtons.None),
                Frame(tick, GameplayButtons.Block));
            sim.TryGetFighter(1, out FighterStateComponent v);
            if (v.DazeFrames > 0) daze = tick;
        }
        AssertThat(daze).OverrideFailureMessage("setup: three blocked hits must shatter the guard").IsGreater(-1);
        sim.TryGetFighter(1, out FighterStateComponent d);
        double pushed = D(d.Velocity.x);
        AssertThat(System.Math.Abs(pushed)).OverrideFailureMessage("the guard-break push is 2.0 u/s").IsEqual(2.0);
        int next = daze + 1;
        for (int i = 0; i < 20; i++) {
            Idle(sim, ref next);
            sim.TryGetFighter(1, out FighterStateComponent f);
            AssertThat(D(f.Velocity.x)).OverrideFailureMessage($"daze tick {i}: the push must not decay").IsEqual(pushed);
        }
    }
}
