using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Combat and movement update 2026-10-16 (plan §1.5, CM-SIM Phase A0): Klotho
/// component 323, <see cref="FighterActionStateComponent"/>. All-zero on spawn,
/// held at zero on the respawn platform after a stock loss, zeroed by the F22
/// Sudden Death setup, and snapshot and hash state like every other
/// component. Pure C#: the hand-built default loadouts touch no engine API.
/// </summary>
[TestSuite]
public class FighterActionStateTests {

    [TestCase]
    public void EveryFighterSpawnsWithAnAllZeroActionState() {
        // Ten ints since the meter and Defy update appended DefyFrames (36 to 40 bytes).
        AssertThat(System.Runtime.CompilerServices.Unsafe.SizeOf<FighterActionStateComponent>()).IsEqual(40);
        var simulation = new FighterSimulation(rules: FighterMatchRules.Disabled);
        for (int playerID = 0; playerID < 2; playerID++) {
            AssertThat(simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent state))
                .OverrideFailureMessage($"Player {playerID} carries no component 323.")
                .IsTrue();
            AssertThat(IsAllZero(in state))
                .OverrideFailureMessage($"Player {playerID} spawned with a non-zero action state.")
                .IsTrue();
        }
    }

    [TestCase]
    public void TheRespawnPlatformHoldsTheActionStateAtZeroAfterAStockLoss() {
        var simulation = new FighterSimulation(
            stocks: 5, matchSeconds: 600,
            rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, false, 0));
        DriveOffTheBottom(simulation, 1);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent fallen)).IsTrue();
        AssertThat(FighterMatchFlowRules.IsOnRespawnPlatform(in fallen))
            .OverrideFailureMessage("Harness: the fall did not land the fighter on the respawn platform.")
            .IsTrue();

        AssertThat(simulation.SeedFighterActionStateForTest(1, NonZeroState())).IsTrue();
        int held = 0;
        for (int tick = 0; tick < 400; tick++) {
            int current = simulation.CurrentTick;
            simulation.Advance(Neutral(current), Neutral(current));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent fighter)).IsTrue();
            if (!FighterMatchFlowRules.IsOnRespawnPlatform(in fighter)) break;
            AssertThat(simulation.TryGetFighterActionState(1, out FighterActionStateComponent state)).IsTrue();
            AssertThat(IsAllZero(in state))
                .OverrideFailureMessage($"Tick {tick} on the platform left component 323 non-zero.")
                .IsTrue();
            held++;
        }
        AssertThat(held > 0).OverrideFailureMessage("Harness: the platform never held the fighter.").IsTrue();
    }

    [TestCase]
    public void TheSuddenDeathSetupZeroesTheActionState() {
        var simulation = new FighterSimulation(
            matchSeconds: 1, rules: new FighterMatchRules((int)MatchMode.TimeLimit, false, 0, false, 0));
        bool seeded = false;
        bool entered = false;
        for (int tick = 0; tick < 120 && !entered; tick++) {
            if (!seeded && tick == 40) {
                AssertThat(simulation.SeedFighterActionStateForTest(0, NonZeroState())).IsTrue();
                AssertThat(simulation.SeedFighterActionStateForTest(1, NonZeroState())).IsTrue();
                seeded = true;
            }
            simulation.Advance(Neutral(tick), Neutral(tick));
            entered = simulation.GetMatchState().SuddenDeathActive == 1;
        }
        AssertThat(seeded && entered).OverrideFailureMessage("Harness: the timer never reached Sudden Death.").IsTrue();
        for (int playerID = 0; playerID < 2; playerID++) {
            AssertThat(simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent state)).IsTrue();
            AssertThat(IsAllZero(in state))
                .OverrideFailureMessage($"Sudden Death left player {playerID}'s component 323 non-zero.")
                .IsTrue();
        }
    }

    [TestCase]
    public void TheActionStateIsHashedAndRoundTripsThroughAFullSnapshot() {
        var simulation = new FighterSimulation(rules: FighterMatchRules.Disabled);
        for (int tick = 0; tick < 5; tick++) simulation.Advance(Neutral(tick), Neutral(tick));
        long untouched = simulation.CurrentHash;

        FighterActionStateComponent seeded = NonZeroState();
        AssertThat(simulation.SeedFighterActionStateForTest(0, in seeded)).IsTrue();
        long withState = simulation.CurrentHash;
        AssertThat(withState)
            .OverrideFailureMessage("Component 323 must be hash state: seeding it left the hash unchanged.")
            .IsNotEqual(untouched);

        byte[] snapshot = simulation.CaptureFullState();
        AssertThat(simulation.SeedFighterActionStateForTest(0, default)).IsTrue();
        for (int tick = 5; tick < 15; tick++) simulation.Advance(Neutral(tick), Neutral(tick));
        simulation.RestoreFullState(snapshot);

        AssertThat(simulation.TryGetFighterActionState(0, out FighterActionStateComponent restored)).IsTrue();
        AssertThat(restored.SpecialSlot).IsEqual(seeded.SpecialSlot);
        AssertThat(restored.SpecialPhase).IsEqual(seeded.SpecialPhase);
        AssertThat(restored.SpecialPhaseFrames).IsEqual(seeded.SpecialPhaseFrames);
        AssertThat(restored.SpecialPhaseElapsed).IsEqual(seeded.SpecialPhaseElapsed);
        AssertThat(restored.SpecialFlags).IsEqual(seeded.SpecialFlags);
        AssertThat(restored.SpecialCancelBuffer).IsEqual(seeded.SpecialCancelBuffer);
        AssertThat(restored.BlockTiming).IsEqual(seeded.BlockTiming);
        AssertThat(restored.DownTapFrames).IsEqual(seeded.DownTapFrames);
        AssertThat(restored.ActionFlags).IsEqual(seeded.ActionFlags);
        AssertThat(restored.DefyFrames).IsEqual(seeded.DefyFrames);
        AssertThat(simulation.CurrentHash).IsEqual(withState);
    }

    /// <summary>Distinct non-zero values in all ten fields.</summary>
    private static FighterActionStateComponent NonZeroState() => new() {
        SpecialSlot = 2,
        SpecialPhase = 3,
        SpecialPhaseFrames = 7,
        SpecialPhaseElapsed = 5,
        SpecialFlags = 3,
        SpecialCancelBuffer = 0x19,
        BlockTiming = 0x10203,
        DownTapFrames = 11,
        ActionFlags = 0x5,
        DefyFrames = 9
    };

    private static bool IsAllZero(in FighterActionStateComponent state) =>
        state.SpecialSlot == 0
        && state.SpecialPhase == 0
        && state.SpecialPhaseFrames == 0
        && state.SpecialPhaseElapsed == 0
        && state.SpecialFlags == 0
        && state.SpecialCancelBuffer == 0
        && state.BlockTiming == 0
        && state.DownTapFrames == 0
        && state.ActionFlags == 0
        && state.DefyFrames == 0;

    /// <summary>Drop-through on the legacy flat arena falls straight to the blast zone.</summary>
    private static void DriveOffTheBottom(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent start)).IsTrue();
        int before = start.KnockoutsSuffered;
        for (int offset = 0; offset < 400; offset++) {
            int tick = simulation.CurrentTick;
            PlayerInputFrame falling = offset == 0
                ? Frame(tick, GameplayButtons.Jump | GameplayButtons.Down)
                : Frame(tick, GameplayButtons.Down);
            PlayerInputFrame one = playerID == 0 ? falling : Neutral(tick);
            PlayerInputFrame two = playerID == 1 ? falling : Neutral(tick);
            simulation.Advance(one, two);
            AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)).IsTrue();
            if (runtime.KnockoutsSuffered > before) return;
        }
        AssertThat(false)
            .OverrideFailureMessage($"Harness: player {playerID}'s fall never reached the blast zone.")
            .IsTrue();
    }

    private static PlayerInputFrame Neutral(int tick) => Frame(tick, GameplayButtons.None);

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        Held = pressed,
        Pressed = pressed
    };
}
