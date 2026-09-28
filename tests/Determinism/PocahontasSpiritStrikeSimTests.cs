using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 12 W4 (GAP-10b) — Spirit Strike in the deterministic sim. The eagle
/// carries Pocahontas about 3 units up-forward at 45° over 15 frames while its
/// hitbox dives down-forward ahead of her. Until W4 the sim special was a
/// range-gated melee intent that translated nobody, which is why the CPU could
/// not treat it as a recovery tool.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PocahontasSpiritStrikeSimTests {

    [TestCase]
    public void TheEagleCarriesHerThreeUnitsUpForwardAtFortyFiveDegreesWithNoTargetInRange() {
        // Spawned 16 units apart: nothing is in melee range, so the old intent
        // path would have refused the press outright.
        var simulation = new FighterSimulation(
            PocahontasLoadout(), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 1301, spawnDistance: 8, rules: FighterMatchRules.Disabled);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent start)).IsTrue();

        simulation.Advance(Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent armed)).IsTrue();
        AssertThat(armed.UniversalMovementState).IsEqual(FighterKitMotion.SpiritStartup);
        AssertThat(armed.SpecialOneCooldownFrames > 0)
            .OverrideFailureMessage("The cast arms Special 1's cooldown whether or not anything is near.")
            .IsTrue();

        int carryTicks = 0;
        for (int tick = 1; tick <= KitMotionRules.SpiritStrikeStartupFrames + KitMotionRules.SpiritStrikeCarryFrames; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime);
            if (runtime.UniversalMovementState == FighterKitMotion.SpiritCarry) carryTicks++;
        }
        AssertThat(carryTicks).IsEqual(KitMotionRules.SpiritStrikeCarryFrames);

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent end)).IsTrue();
        FP64 dx = end.Position.x - start.Position.x;
        FP64 dy = end.Position.y - start.Position.y;
        FP64 axis = FP64.FromDouble(KitMotionRules.SpiritStrikeCarryUnits / System.Math.Sqrt(2.0));
        FP64 tolerance = FP64.FromDouble(0.03);
        AssertThat(FP64.Abs(dx - axis) < tolerance)
            .OverrideFailureMessage($"Forward carry {dx}, expected {axis}.").IsTrue();
        AssertThat(FP64.Abs(dy - axis) < tolerance)
            .OverrideFailureMessage($"Rise {dy}, expected {axis} — the carry is 45 degrees.").IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent far)).IsTrue();
        AssertThat(far.CurrentHP).IsEqual(far.MaxHP);
    }

    [TestCase]
    public void TheEagleDivesAheadOfHerAndStrikesTheOpponentExactlyOnce() {
        FighterLoadout pocahontas = PocahontasLoadout();
        var simulation = new FighterSimulation(
            pocahontas, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 1302, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        simulation.Advance(Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.None));
        for (int tick = 1; tick < 90; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(before.CurrentHP - after.CurrentHP)
            .OverrideFailureMessage("The eagle strikes once for the authored Special 1 damage.")
            .IsEqual(pocahontas.SpecialOneDamage);
    }

    [TestCase]
    public void ACorrectedInputReplaysTheCarryAndTheStrikeIdentically() {
        FighterLoadout pocahontas = PocahontasLoadout();
        var authoritative = new FighterSimulation(
            pocahontas, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 1303, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        var predicted = new FighterSimulation(
            pocahontas, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 1303, spawnDistance: 2, rules: FighterMatchRules.Disabled);

        for (int tick = 0; tick < 60; tick++) {
            PlayerInputFrame one = tick == 0 ? Frame(tick, GameplayButtons.Special1) : Frame(tick, GameplayButtons.None);
            // The remote actually held Block from tick 16 — mid-carry — which
            // the predicting side learns late.
            PlayerInputFrame two = tick >= 16 && tick < 30
                ? Frame(tick, GameplayButtons.Block)
                : Frame(tick, GameplayButtons.None);
            authoritative.Advance(one, two);
            if (tick >= 16 && tick < 30) predicted.AdvanceWithPredictedPlayerTwo(one);
            else predicted.Advance(one, two);
        }
        for (int tick = 16; tick < 30; tick++) {
            AssertThat(predicted.CorrectPlayerTwoInput(tick, Frame(tick, GameplayButtons.Block))).IsTrue();
        }
        AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
        authoritative.TryGetFighter(1, out FighterStateComponent a);
        predicted.TryGetFighter(1, out FighterStateComponent b);
        AssertThat(b.CurrentHP).IsEqual(a.CurrentHP);
    }

    private static FighterLoadout PocahontasLoadout() =>
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/pocahontas_data.tres"));

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        Held = pressed,
        Pressed = pressed
    };
}
