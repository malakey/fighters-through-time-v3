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
/// The Fighter Momentum gauge (meter and Defy update 2026-10-31, plan sections
/// 2.1 to 2.3): four pips shared by Special 1 and 2 in integer units, a 2 s
/// reuse delay per slot, regeneration of 10 units a frame while the match is
/// live and the fighter is not frozen, the Sudden Death restart, and the
/// stock-loss carry-over. The landed-swing gains are pinned beside the
/// hit-confirm cases (<c>FighterHitConfirmTests</c>).
///
/// <para>Specials here are slow Projectile executions, so a spawned shot is the
/// proof of acceptance.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterMomentumGaugeTests {

    private const int Pip = BasicComboRules.MomentumUnitsPerPip;
    private const int Lockout = BasicComboRules.AbilityLockoutFrames;
    private const int Reuse = BasicComboRules.SpecialReuseDelayFrames;

    [TestCase]
    public void EveryMatchStartsAtTwoPipsWithNoReuseDelay() {
        FighterSimulation simulation = NewSimulation(seed: 801);
        for (int player = 0; player < 2; player++) {
            AssertThat(Verb(simulation, player).MomentumUnits).IsEqual(BasicComboRules.MomentumStartUnits);
            AssertThat(Verb(simulation, player).MomentumUnits).IsEqual(2 * Pip);
            AssertThat(Runtime(simulation, player).SpecialOneReuseFrames).IsEqual(0);
            AssertThat(Runtime(simulation, player).SpecialTwoReuseFrames).IsEqual(0);
        }
    }

    [TestCase]
    public void NothingRegeneratesInTheCountdownAndTenUnitsAFrameAfterGo() {
        var simulation = new FighterSimulation(
            Loadout(FighterCharacterID.Einstein), Loadout(FighterCharacterID.Einstein),
            seed: 802, spawnDistance: 4,
            rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0, 1, 60));
        // Spend the start fill so a head start would show against the bare 2 pips.
        AssertThat(simulation.SeedFighterMomentumForTest(0, Pip)).IsTrue();
        for (int tick = 0; tick < 59; tick++) Step(simulation, GameplayButtons.None);
        AssertThat(simulation.GetMatchState().MatchState).IsEqual(FighterMatchStates.Countdown);
        AssertThat(Verb(simulation, 0).MomentumUnits)
            .OverrideFailureMessage("The countdown must not give the gauge a head start (P2).")
            .IsEqual(Pip);

        // The countdown's last tick flips the match live; regeneration starts on the next.
        for (int tick = 0; tick < 4 && simulation.GetMatchState().MatchState != FighterMatchStates.InProgress; tick++) {
            Step(simulation, GameplayButtons.None);
        }
        AssertThat(simulation.GetMatchState().MatchState).IsEqual(FighterMatchStates.InProgress);
        int live = Verb(simulation, 0).MomentumUnits;
        Idle(simulation, 100);
        AssertThat(Verb(simulation, 0).MomentumUnits - live)
            .OverrideFailureMessage("A live match regenerates 10 units a frame (a pip per 720 frames).")
            .IsEqual(100 * BasicComboRules.MomentumRegenUnitsPerFrame);
        AssertThat(BasicComboRules.MomentumRegenUnitsPerFrame * BasicComboRules.MomentumRegenFramesPerPip)
            .IsEqual(Pip);
    }

    [TestCase]
    public void AnAcceptedSpecialSpendsOnePipAndArmsOnlyItsOwnTwoSecondDelay() {
        FighterSimulation simulation = NewSimulation(seed: 803);
        int before = Verb(simulation, 0).MomentumUnits;
        Step(simulation, GameplayButtons.Special1);
        AssertThat(Shots(simulation, 0, 1)).OverrideFailureMessage("Harness: Special 1 must be accepted.").IsEqual(1);
        AssertThat(Verb(simulation, 0).MomentumUnits)
            .OverrideFailureMessage("One pip is spent at acceptance, then this tick's regeneration is already in.")
            .IsEqual(before - Pip + BasicComboRules.MomentumRegenUnitsPerFrame);
        AssertThat(Runtime(simulation, 0).SpecialOneReuseFrames).IsEqual(Reuse);
        AssertThat(Runtime(simulation, 0).SpecialTwoReuseFrames)
            .OverrideFailureMessage("Special 2 is untouched by Special 1's delay.")
            .IsEqual(0);

        // After the 30-frame lockout the other slot casts at once, for another pip.
        Idle(simulation, Lockout - 1);
        Step(simulation, GameplayButtons.Special2);
        AssertThat(Shots(simulation, 0, 2)).OverrideFailureMessage("Special 2 is free of Special 1's delay.").IsEqual(1);
        AssertThat(Runtime(simulation, 0).SpecialTwoReuseFrames).IsEqual(Reuse);
    }

    [TestCase]
    public void APressWithUnderOnePipIsRefusedAndSpendsAndArmsNothing() {
        FighterSimulation simulation = NewSimulation(seed: 804);
        AssertThat(simulation.SeedFighterMomentumForTest(0, Pip - 12)).IsTrue();
        Step(simulation, GameplayButtons.Special1);
        AssertThat(Shots(simulation, 0, 1))
            .OverrideFailureMessage("One regeneration frame still leaves the gauge 2 units short of a pip.")
            .IsEqual(0);
        AssertThat(Runtime(simulation, 0).SpecialOneReuseFrames).IsEqual(0);
        AssertThat(Runtime(simulation, 0).AbilityLockoutFrames)
            .OverrideFailureMessage("A refused press arms no lockout.")
            .IsEqual(0);
        AssertThat(Verb(simulation, 0).MomentumUnits).IsEqual(Pip - 12 + 10);

        // The next tick's regeneration completes the pip: now it is accepted.
        Step(simulation, GameplayButtons.Special1);
        AssertThat(Shots(simulation, 0, 1)).IsEqual(1);
        AssertThat(Verb(simulation, 0).MomentumUnits)
            .OverrideFailureMessage("Regeneration brought the gauge to a pip and 8 units; the cast took the pip.")
            .IsEqual(8);
    }

    [TestCase]
    public void TheReuseDelayRefusesTheSameSlotForExactlyOneHundredTwentyFrames() {
        FighterSimulation simulation = NewSimulation(seed: 805);
        AssertThat(simulation.SeedFighterMomentumForTest(0, BasicComboRules.MomentumMaxUnits)).IsTrue();
        Step(simulation, GameplayButtons.Special1);
        for (int elapsed = 1; elapsed < Reuse; elapsed++) {
            // Probe past the 30-frame lockout, where only the reuse delay refuses.
            bool probe = elapsed >= Lockout && elapsed % 30 == 0 || elapsed == Reuse - 1;
            Step(simulation, probe ? GameplayButtons.Special1 : GameplayButtons.None);
            if (probe) {
                AssertThat(Shots(simulation, 0, 1))
                    .OverrideFailureMessage($"Special 1 on T+{elapsed} must be refused by the reuse delay.")
                    .IsEqual(1);
            }
        }
        AssertThat(Runtime(simulation, 0).SpecialOneReuseFrames).IsEqual(1);
        Step(simulation, GameplayButtons.Special1);
        AssertThat(Shots(simulation, 0, 1)).OverrideFailureMessage("Special 1 is legal again on T+120.").IsEqual(2);
        AssertThat(Runtime(simulation, 0).SpecialOneReuseFrames).IsEqual(Reuse);
    }

    [TestCase]
    public void SwingsAndTheMovementAbilityCostNoPipAndOnlyRegenerationMovesTheGauge() {
        FighterSimulation simulation = NewSimulation(seed: 806, spawnDistance: 8);
        int before = Verb(simulation, 0).MomentumUnits;
        Step(simulation, GameplayButtons.BasicAttack);
        Idle(simulation, 40);
        Step(simulation, GameplayButtons.MovementAbility);
        Idle(simulation, 40);
        AssertThat(Verb(simulation, 0).MomentumUnits)
            .OverrideFailureMessage("A whiffed swing and the movement ability pay nothing and cost nothing.")
            .IsEqual(before + 82 * BasicComboRules.MomentumRegenUnitsPerFrame);
    }

    [TestCase]
    public void TheGaugeDoesNotRegenerateInTheFightersOwnHitstop() {
        var simulation = new FighterSimulation(
            Loadout(FighterCharacterID.Einstein), Loadout(FighterCharacterID.Einstein),
            seed: 807, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        AssertThat(simulation.SeedFighterMomentumForTest(1, Pip)).IsTrue();
        int frozenTicks = 0;
        int liveTicks = 0;
        Step(simulation, GameplayButtons.BasicAttack);
        for (int tick = 0; tick < 40; tick++) {
            bool frozen = Verb(simulation, 1).HitstopFrames > 0;
            int units = Verb(simulation, 1).MomentumUnits;
            Step(simulation, GameplayButtons.None);
            int delta = Verb(simulation, 1).MomentumUnits - units;
            if (frozen) {
                frozenTicks++;
                AssertThat(delta).OverrideFailureMessage($"Tick {tick}: a frozen fighter's gauge holds.").IsEqual(0);
            } else {
                liveTicks++;
            }
        }
        AssertThat(frozenTicks > 0).OverrideFailureMessage("Harness: the swing must freeze its victim.").IsTrue();
        AssertThat(liveTicks > 0).IsTrue();
        AssertThat(Verb(simulation, 1).MomentumUnits)
            .IsEqual(Pip + (liveTicks + 1) * BasicComboRules.MomentumRegenUnitsPerFrame);
    }

    [TestCase]
    public void AStockLossKeepsTheGaugeAndTheReuseDelay() {
        // Player one's one basic is 200 damage; the 100 HP victim loses a stock.
        FighterLoadout killer = Loadout(FighterCharacterID.Tesla, basicDamage: 200);
        var simulation = new FighterSimulation(
            killer, Loadout(FighterCharacterID.Einstein), seed: 808, spawnDistance: 1,
            rules: FighterMatchRules.Disabled);
        AssertThat(simulation.SeedFighterMomentumForTest(1, 3 * Pip)).IsTrue();
        AssertThat(simulation.SeedFighterSpecialReuseForTest(1, 77, 55)).IsTrue();
        Step(simulation, GameplayButtons.BasicAttack);
        int lossTick = -1;
        for (int tick = 0; tick < 60 && lossTick < 0; tick++) {
            Step(simulation, GameplayButtons.None);
            if (Fighter(simulation, 1).Stocks < 3) lossTick = tick;
        }
        AssertThat(lossTick >= 0).OverrideFailureMessage("Harness: the victim must lose a stock.").IsTrue();
        AssertThat(Verb(simulation, 1).MomentumUnits >= 3 * Pip)
            .OverrideFailureMessage("A stock loss keeps the gauge (G3.2); regeneration only adds.")
            .IsTrue();
        AssertThat(Runtime(simulation, 1).SpecialOneReuseFrames > 0 && Runtime(simulation, 1).SpecialOneReuseFrames <= 77)
            .OverrideFailureMessage("The reuse delay keeps running through the respawn.")
            .IsTrue();
        AssertThat(Runtime(simulation, 1).SpecialTwoReuseFrames > 0 && Runtime(simulation, 1).SpecialTwoReuseFrames <= 55)
            .IsTrue();
    }

    [TestCase]
    public void SuddenDeathRestartsBothGaugesAtTwoPipsWithClearReuseDelays() {
        var simulation = new FighterSimulation(
            Loadout(FighterCharacterID.Einstein), Loadout(FighterCharacterID.Einstein),
            matchSeconds: 1, seed: 809, spawnDistance: 4,
            rules: new FighterMatchRules((int)MatchMode.Stock, false, 0, false, 0));
        AssertThat(simulation.SeedFighterMomentumForTest(0, BasicComboRules.MomentumMaxUnits)).IsTrue();
        AssertThat(simulation.SeedFighterMomentumForTest(1, 0)).IsTrue();
        AssertThat(simulation.SeedFighterSpecialReuseForTest(0, 100, 90)).IsTrue();
        for (int tick = 0; tick < 60 && simulation.GetMatchState().SuddenDeathActive == 0; tick++) {
            Step(simulation, GameplayButtons.None);
        }
        AssertThat(simulation.GetMatchState().SuddenDeathActive).IsEqual(1);
        for (int player = 0; player < 2; player++) {
            AssertThat(Verb(simulation, player).MomentumUnits)
                .OverrideFailureMessage($"Player {player}'s gauge restarts at the match-start fill whatever it held (O22).")
                .IsEqual(BasicComboRules.MomentumStartUnits);
            AssertThat(Runtime(simulation, player).SpecialOneReuseFrames).IsEqual(0);
            AssertThat(Runtime(simulation, player).SpecialTwoReuseFrames).IsEqual(0);
        }
    }

    [TestCase]
    public void TheGaugeAndTheReuseDelayAreRollbackStateAndReplayIdentically() {
        FighterSimulation first = NewSimulation(seed: 810);
        FighterSimulation second = NewSimulation(seed: 810);
        AssertThat(first.SeedFighterMomentumForTest(0, BasicComboRules.MomentumMaxUnits)).IsTrue();
        AssertThat(second.SeedFighterMomentumForTest(0, BasicComboRules.MomentumMaxUnits)).IsTrue();
        var hashes = new List<long>();
        for (int tick = 0; tick < 140; tick++) {
            GameplayButtons buttons = tick == 4 ? GameplayButtons.Special1 : tick == 40 ? GameplayButtons.Special2 : GameplayButtons.None;
            long a = Step(first, buttons);
            long b = Step(second, buttons);
            AssertThat(a).IsEqual(b);
            hashes.Add(a);
            if (tick == 60) {
                byte[] snapshot = first.CaptureFullState();
                FighterSimulation restored = NewSimulation(seed: 810);
                restored.RestoreFullState(snapshot);
                AssertThat(restored.CurrentHash).IsEqual(first.CurrentHash);
                AssertThat(Verb(restored, 0).MomentumUnits).IsEqual(Verb(first, 0).MomentumUnits);
                AssertThat(Runtime(restored, 0).SpecialOneReuseFrames).IsEqual(Runtime(first, 0).SpecialOneReuseFrames);
                for (int replay = 61; replay < 140; replay++) {
                    GameplayButtons replayButtons = replay == 40 ? GameplayButtons.Special2 : GameplayButtons.None;
                    long expected = Step(first, replayButtons);
                    long actual = Step(restored, replayButtons);
                    AssertThat(actual).OverrideFailureMessage($"Restored run diverged at tick {replay}.").IsEqual(expected);
                }
                return;
            }
        }
    }

    [TestCase]
    public void ARequiemChordThatHitsLeavesFortissimosReuseDelayRunningDown() {
        // The old Tempo shave (two seconds off Fortissimo per landed chord) is a
        // documented no-op until Mozart's kit pass (G4.2, P17).
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(
                AuthoredResources.Load<CharacterData>("res://resources/Characters/mozart_data.tres")),
            FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: 811, spawnDistance: 5, rules: FighterMatchRules.Disabled);
        MomentumTestKit.KeepFull(simulation, 0);
        Step(simulation, GameplayButtons.Special2);
        int chordTick = 1;
        for (; chordTick < 120 && Runtime(simulation, 0).AbilityLockoutFrames > 0; chordTick++) {
            Step(simulation, GameplayButtons.None);
        }
        // Wait out Fortissimo's own cast as well, then fire the chord.
        for (int guard = 0; guard < 60 && SpecialCastTestKit.IsCasting(simulation, 0); guard++) {
            Step(simulation, GameplayButtons.None);
        }
        Step(simulation, GameplayButtons.Special1);
        AssertThat(Runtime(simulation, 0).SpecialOneReuseFrames).IsEqual(Reuse);
        int previous = Runtime(simulation, 0).SpecialTwoReuseFrames;
        AssertThat(previous > 0).OverrideFailureMessage("Harness: Fortissimo's delay is running.").IsTrue();
        int hpBefore = Fighter(simulation, 1).CurrentHP;
        for (int tick = 0; tick < 100; tick++) {
            Step(simulation, GameplayButtons.None);
            int now = Runtime(simulation, 0).SpecialTwoReuseFrames;
            AssertThat(previous - now <= 1)
                .OverrideFailureMessage("Fortissimo's reuse delay only counts down, never jumps by the old shave.")
                .IsTrue();
            previous = now;
        }
        AssertThat(Fighter(simulation, 1).CurrentHP < hpBefore)
            .OverrideFailureMessage("Harness: the chord must have landed for the shave to matter.")
            .IsTrue();
    }

    // === helpers ==============================================================

    private static FighterAbilityLoadout ProjectileModes() => FighterAbilityLoadout.Default with {
        SpecialOneExecutionType = 1,
        SpecialTwoExecutionType = 1,
        SpecialOneProjectileSpeed = FP64.FromDouble(0.5),
        SpecialTwoProjectileSpeed = FP64.FromDouble(0.5),
        SpecialOneProjectileLifetimeFrames = 900,
        SpecialTwoProjectileLifetimeFrames = 900
    };

    private static FighterSimulation NewSimulation(int seed, int spawnDistance = 4) => new(
        Loadout(FighterCharacterID.Einstein), Loadout(FighterCharacterID.Einstein),
        seed: seed, spawnDistance: spawnDistance, rules: FighterMatchRules.Disabled);

    private static FighterLoadout Loadout(FighterCharacterID character, int basicDamage = 10) => new(
        (int)character,
        100, 3, 2,
        basicDamage, 14, 12, 20,
        (int)StatusType.None, 0,
        (int)StatusType.None, 0,
        (int)StatusType.None, 0,
        FP64.One,
        FP64.FromInt(7),
        FP64.FromDouble(11.5),
        FP64.FromInt(3),
        FP64.FromInt(4),
        FP64.FromInt(4),
        FP64.FromInt(5),
        FP64.One, FP64.One, FP64.One,
        ProjectileModes());

    private static int Shots(FighterSimulation simulation, int ownerID, int slot) {
        int typeID = Fighter(simulation, ownerID).CharacterID * 10 + slot;
        var projectiles = new List<FighterProjectileComponent>();
        simulation.CopyProjectilesTo(projectiles);
        int count = 0;
        foreach (FighterProjectileComponent projectile in projectiles) {
            if (projectile.OwnerPlayerID == ownerID && projectile.ProjectileTypeID == typeID) count++;
        }
        return count;
    }

    private static FighterStateComponent Fighter(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent state)).IsTrue();
        return state;
    }

    private static FighterRuntimeComponent Runtime(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)).IsTrue();
        return runtime;
    }

    private static FighterVerbComponent Verb(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)).IsTrue();
        return verb;
    }

    private static long Step(FighterSimulation simulation, GameplayButtons playerOne) =>
        simulation.Advance(
            Input(simulation.CurrentTick, playerOne),
            Input(simulation.CurrentTick, GameplayButtons.None));

    private static void Idle(FighterSimulation simulation, int frames) {
        for (int frame = 0; frame < frames; frame++) Step(simulation, GameplayButtons.None);
    }

    private static PlayerInputFrame Input(int tick, GameplayButtons buttons) => new() {
        Tick = (uint)tick,
        Held = buttons,
        Pressed = buttons
    };
}
