using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7b (M02/M03) — the Requiem Chord in the sim, on Mozart's
/// authored data: a straight 14 units/s shot with no range limit that bursts
/// into three pulses (at a fighter, or at a wall), and Mozart's Tempo rule —
/// an execution that HITS shortens Fortissimo Wave's remaining cooldown by
/// two seconds, exactly once however many of its contact and pulses land.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RequiemCooldownShaveTests {

    [TestCase]
    public void AHittingExecutionShavesTwoSecondsOffFortissimoExactlyOnce() {
        var simulation = NewMatch(spawnDistance: 5);
        // Fortissimo first (its 6-unit wall stops short of the opponent 10
        // units away), then the chord once Fortissimo's 30-frame ability
        // lockout and its 16 / 6 / 22 cast have run out.
        simulation.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));
        int chordTick = IdleUntilAbilityReady(simulation, 1);
        simulation.Advance(Frame(chordTick, 0, GameplayButtons.Special1), Frame(chordTick, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent start)).IsTrue();
        AssertThat(start.SpecialOneCooldownFrames > 0).OverrideFailureMessage("The chord was not cast.").IsTrue();
        // 720 frames, minus the ticks spent waiting: the shave still has room.
        AssertThat(start.SpecialTwoCooldownFrames > 600).IsTrue();
        // Decision 1.2 (2026-10-16): the chord leaves on the cast's active entry.
        int fired = SpecialCastTestKit.StepUntilFired(simulation, 0, chordTick + 1);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent leaving)).IsTrue();
        int previous = leaving.SpecialTwoCooldownFrames;

        int shaves = 0;
        for (int tick = fired; tick < fired + 89; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            int drop = previous - runtime.SpecialTwoCooldownFrames;
            if (drop >= 100) {
                // One tick's countdown plus the 120-frame shave.
                AssertThat(drop == 120 || drop == 121).IsTrue();
                shaves++;
            }
            previous = runtime.SpecialTwoCooldownFrames;
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        // Contact 4 + three 4-damage pulses.
        AssertThat(target.CurrentHP).IsEqual(84);
        AssertThat(shaves).IsEqual(1);
    }

    [TestCase]
    public void AChordThatHitsNothingBurstsAtTheWallAndShavesNothing() {
        var simulation = NewMatch(spawnDistance: 5);
        simulation.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));
        // Wait out Fortissimo's ability lockout, then turn away from the
        // opponent: the chord flies to the left wall.
        int turnTick = IdleUntilAbilityReady(simulation, 1);
        simulation.Advance(Frame(turnTick, -127, GameplayButtons.None), Frame(turnTick, 0, GameplayButtons.None));
        int chordTick = turnTick + 1;
        simulation.Advance(Frame(chordTick, 0, GameplayButtons.Special1), Frame(chordTick, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent start)).IsTrue();
        AssertThat(start.SpecialOneCooldownFrames > 0).OverrideFailureMessage("The chord was not cast.").IsTrue();
        int fired = SpecialCastTestKit.StepUntilFired(simulation, 0, chordTick + 1);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent leaving)).IsTrue();
        int previous = leaving.SpecialTwoCooldownFrames;

        bool burstAtWall = false;
        var zones = new List<FighterZoneComponent>();
        for (int tick = fired; tick < fired + 58; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            AssertThat(previous - runtime.SpecialTwoCooldownFrames <= 1).IsTrue();
            previous = runtime.SpecialTwoCooldownFrames;
            zones.Clear();
            simulation.CopyZonesTo(zones);
            foreach (FighterZoneComponent zone in zones) {
                if (zone.ZoneTypeID == FighterReachKitRules.RequiemBurstZone
                    && zone.Position.x == FP64.FromInt(-10)) burstAtWall = true;
            }
        }
        AssertThat(burstAtWall)
            .OverrideFailureMessage("With no range limit the chord crosses the stage and bursts at the wall.")
            .IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(100);
    }

    [TestCase]
    public void TheChordFliesStraightAtFourteenUnitsPerSecond() {
        var simulation = NewMatch(spawnDistance: 8);
        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        SpecialCastTestKit.StepUntilFired(simulation, 0, 1);
        AssertThat(simulation.TryGetFirstProjectile(out FighterProjectileComponent chord)).IsTrue();
        AssertThat(chord.Velocity.x).IsEqual(FP64.FromInt(14));
        AssertThat(chord.Velocity.y).IsEqual(FP64.Zero);
        AssertThat(chord.Damage).IsEqual(4);
    }

    private static FighterSimulation NewMatch(int spawnDistance) => new(
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/mozart_data.tres")),
        FighterLoadout.Default(FighterCharacterID.Tesla),
        seed: 95,
        spawnDistance: spawnDistance,
        rules: FighterMatchRules.Disabled);

    /// <summary>
    /// Idles both players until Mozart's ability lockout has run out and (decision
    /// 1.2, 2026-10-16) his Fortissimo cast has ended; returns the next tick.
    /// </summary>
    private static int IdleUntilAbilityReady(FighterSimulation simulation, int startTick) {
        int tick = startTick;
        for (int limit = startTick + 90; tick < limit; tick++) {
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            if (runtime.AbilityLockoutFrames == 0 && !SpecialCastTestKit.IsCasting(simulation, 0)) break;
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent ready)).IsTrue();
        AssertThat(ready.AbilityLockoutFrames).IsEqual(0);
        return tick;
    }

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = pressed,
        Pressed = pressed
    };
}
