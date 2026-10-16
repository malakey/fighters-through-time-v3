using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7b (J01/J03) — Divine Piercing in the sim, on Joan's authored
/// data: a ~3-unit forward lunge across the 12 active frames (her ground
/// gap-closer), three 8-damage thrusts along the way that each resolve exactly
/// once however long a hitstop holds the lunge, and the 11 s cooldown.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DivinePiercingLungeTests {

    [TestCase]
    public void TheLungeCarriesJoanAboutThreeUnitsForwardWhetherOrNotAnyoneIsThere() {
        var simulation = NewMatch(spawnDistance: 8);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        // J03: the 11 s cooldown is armed on the press.
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent pressed)).IsTrue();
        AssertThat(pressed.SpecialTwoCooldownFrames).IsEqual(660);
        // Decision 1.2 (2026-10-16): the lunge starts on the cast's active
        // entry, after Divine Piercing's authored 13-frame startup.
        int lungeStart = SpecialCastTestKit.StepUntilFired(simulation, 0, 1);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent lunging)).IsTrue();
        AssertThat(lunging.UniversalMovementState).IsEqual(FighterKitMotion.PiercingLunge);
        for (int tick = lungeStart; tick < lungeStart + 14; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent after)).IsTrue();
        FP64 travelled = after.Position.x - before.Position.x;
        AssertThat(travelled > FP64.FromDouble(2.95)).IsTrue();
        AssertThat(travelled < FP64.FromDouble(3.05)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent done)).IsTrue();
        AssertThat(done.UniversalMovementState).IsEqual(0);
    }

    [TestCase]
    public void EachOfTheThreeThrustsResolvesExactlyOnceThroughHitstop() {
        var simulation = NewMatch(spawnDistance: 1);
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        int hits = 0;
        int previousHP = 100;
        for (int tick = 1; tick <= 90; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent probe)).IsTrue();
            if (probe.CurrentHP < previousHP) {
                AssertThat(previousHP - probe.CurrentHP).IsEqual(8);
                hits++;
            }
            previousHP = probe.CurrentHP;
        }
        AssertThat(hits).IsEqual(3);
        AssertThat(previousHP).IsEqual(76);
    }

    /// <summary>
    /// Decision 1.2 (2026-10-16): Divine Piercing is 13 / 12 / 20 — Joan stands
    /// in place through the 13 startup ticks (the press tick included), lunges
    /// on each of the 12 active ticks, and stands in place through the 20
    /// recovery ticks. A hit in the startup ends the cast before the lunge.
    /// </summary>
    [TestCase]
    public void JoanHoldsThirteenTicksLungesTwelveAndHoldsTwentyAndAStartupHitLeavesNoLunge() {
        CharacterData data = AuthoredResources.Load<CharacterData>("res://resources/Characters/joan_data.tres");
        AbilityData piercing = data.SpecialAttackTwo;
        AssertThat(piercing.StartupFrames).IsEqual(13);
        AssertThat(piercing.ActiveFrames).IsEqual(12);
        AssertThat(piercing.RecoveryFrames).IsEqual(20);
        var simulation = NewMatch(spawnDistance: 8);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
        var xs = new System.Collections.Generic.List<FP64>();
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent pressed)).IsTrue();
        xs.Add(pressed.Position.x);
        int free = piercing.StartupFrames + piercing.ActiveFrames + piercing.RecoveryFrames;
        for (int tick = 1; tick <= free; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent joan)).IsTrue();
            xs.Add(joan.Position.x);
        }
        // xs[k] is the position after tick T+k.
        for (int k = 0; k < piercing.StartupFrames; k++) {
            AssertThat(xs[k].RawValue).OverrideFailureMessage($"Startup tick T+{k} moved Joan.").IsEqual(before.Position.x.RawValue);
        }
        for (int k = piercing.StartupFrames; k < piercing.StartupFrames + piercing.ActiveFrames; k++) {
            AssertThat(xs[k] > xs[k - 1]).OverrideFailureMessage($"Active tick T+{k} did not lunge.").IsTrue();
        }
        for (int k = piercing.StartupFrames + piercing.ActiveFrames; k <= free; k++) {
            AssertThat(xs[k].RawValue)
                .OverrideFailureMessage($"Recovery tick T+{k} moved Joan.")
                .IsEqual(xs[piercing.StartupFrames + piercing.ActiveFrames - 1].RawValue);
        }

        // A startup hit: the opponent's opener lands inside the 13 frames.
        var interrupted = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(data), FighterLoadout.Default(FighterCharacterID.Leonardo),
            seed: 94, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        interrupted.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        bool hit = false;
        for (int tick = 1; tick <= free; tick++) {
            interrupted.Advance(Frame(tick, GameplayButtons.None), Frame(tick, tick == 1 ? GameplayButtons.BasicAttack : GameplayButtons.None));
            AssertThat(interrupted.TryGetFighter(0, out FighterStateComponent joan)).IsTrue();
            AssertThat(interrupted.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            if (joan.HitstunFrames > 0 && tick < piercing.StartupFrames) hit = true;
            AssertThat(runtime.UniversalMovementState)
                .OverrideFailureMessage($"T+{tick}: an interrupted startup still lunged.")
                .IsNotEqual(FighterKitMotion.PiercingLunge);
        }
        AssertThat(hit).OverrideFailureMessage("Harness: the opener never landed in the startup.").IsTrue();
        AssertThat(interrupted.TryGetFighterRuntime(0, out FighterRuntimeComponent spent)).IsTrue();
        AssertThat(spent.SpecialTwoCooldownFrames > 0).OverrideFailureMessage("The cost stays spent.").IsTrue();
    }

    private static FighterSimulation NewMatch(int spawnDistance) => new(
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/joan_data.tres")),
        FighterLoadout.Default(FighterCharacterID.Tesla),
        seed: 93,
        spawnDistance: spawnDistance,
        rules: FighterMatchRules.Disabled);

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        Held = pressed,
        Pressed = pressed
    };
}
