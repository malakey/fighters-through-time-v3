using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 12 W4 (GAP-10a, F07) — the sim's Lorentz chain consumer. A pulse that
/// lands on a target carrying THIS Tesla's Conductive mark, while he has a live
/// coil, consumes the mark through <c>FighterConductiveRules.TryConsumeChain</c>
/// and chains once: each coil arcs 5 into the target. Static Charge alone,
/// another Tesla's mark, or no coil never chains. The pulse here is authored as
/// three ticks (5-frame interval over 0.25 s), which is what makes "once per
/// pulse" observable.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TeslaLorentzChainTests {
    private const int PulseTicks = 3;
    private const int PulseDamage = 12;

    [TestCase]
    public void AMarkedTargetChainsOncePerPulseAndTheMarkIsConsumed() {
        FighterSimulation simulation = DeployCoilThenPulse(markSource: 0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP)
            .OverrideFailureMessage("Three pulse ticks plus exactly one 5-damage chain from the one coil.")
            .IsEqual(100 - PulseTicks * PulseDamage - FighterZoneSystemCoilArc);
        AssertThat(simulation.TryGetFighterConductive(1, out FighterConductiveComponent mark)).IsTrue();
        AssertThat(FighterConductiveRules.HasMarkFrom(in mark, 0))
            .OverrideFailureMessage("A chain consumes the mark.").IsFalse();
    }

    [TestCase]
    public void StaticChargeAloneAnotherTeslasMarkOrNoCoilNeverChains() {
        // Another source's mark: no chain, and the mark is left alone.
        FighterSimulation foreign = DeployCoilThenPulse(markSource: 1);
        foreign.TryGetFighter(1, out FighterStateComponent foreignTarget);
        AssertThat(foreignTarget.CurrentHP).IsEqual(100 - PulseTicks * PulseDamage);

        // No mark at all (the coil's Static Charge is never seeded here).
        FighterSimulation unmarked = DeployCoilThenPulse(markSource: -1);
        unmarked.TryGetFighter(1, out FighterStateComponent plain);
        AssertThat(plain.CurrentHP).IsEqual(100 - PulseTicks * PulseDamage);

        // Marked, but no coil: nothing to chain through, the mark survives.
        FighterSimulation coilless = PulseWithoutCoil();
        coilless.TryGetFighter(1, out FighterStateComponent unchained);
        AssertThat(unchained.CurrentHP).IsEqual(100 - PulseTicks * PulseDamage);
        AssertThat(coilless.TryGetFighterConductive(1, out FighterConductiveComponent kept)).IsTrue();
        AssertThat(FighterConductiveRules.HasMarkFrom(in kept, 0)).IsTrue();
    }

    [TestCase]
    public void ARollbackAcrossTheChainConvergesAndNeverDuplicatesIt() {
        FighterLoadout tesla = FighterLoadoutFactory.FromCharacterData(ChainTesla());
        var authoritative = new FighterSimulation(
            tesla, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 1403, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var predicted = new FighterSimulation(
            tesla, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 1403, spawnDistance: 1, rules: FighterMatchRules.Disabled);

        for (int tick = 0; tick < 40; tick++) {
            if (tick == 5) {
                // Seeded between ticks, before every snapshot the correction
                // below can roll back to.
                authoritative.SeedFighterConductiveForTest(1, 0, BasicComboRules.ConductiveMarkBaselineFrames);
                predicted.SeedFighterConductiveForTest(1, 0, BasicComboRules.ConductiveMarkBaselineFrames);
            }
            PlayerInputFrame one = tick == 0 ? Frame(tick, GameplayButtons.Special1)
                : tick == 6 ? Frame(tick, GameplayButtons.Special2)
                : Frame(tick, GameplayButtons.None);
            // The remote's real input (drift right) arrives late for ticks 7-12,
            // straddling all three pulse ticks and the chain.
            PlayerInputFrame two = tick >= 7 && tick <= 12 ? Frame(tick, GameplayButtons.None, 127) : Frame(tick, GameplayButtons.None);
            authoritative.Advance(one, two);
            if (tick >= 7 && tick <= 12) predicted.AdvanceWithPredictedPlayerTwo(one);
            else predicted.Advance(one, two);
        }
        for (int tick = 7; tick <= 12; tick++) {
            AssertThat(predicted.CorrectPlayerTwoInput(tick, Frame(tick, GameplayButtons.None, 127))).IsTrue();
        }

        AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
        authoritative.TryGetFighter(1, out FighterStateComponent a);
        predicted.TryGetFighter(1, out FighterStateComponent b);
        AssertThat(b.CurrentHP).IsEqual(a.CurrentHP);
        AssertThat(a.CurrentHP)
            .OverrideFailureMessage("The replay must chain exactly once, like the live run.")
            .IsEqual(100 - PulseTicks * PulseDamage - FighterZoneSystemCoilArc);
    }

    /// <summary>The coil arc the chain deals per live coil (the sim's CoilArcDamage).</summary>
    private const int FighterZoneSystemCoilArc = 5;

    private static FighterSimulation DeployCoilThenPulse(int markSource) {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(ChainTesla()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 1401, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.None));
        for (int tick = 1; tick < 5; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(simulation.PersistentObjectCount).IsEqual(1);
        if (markSource >= 0) {
            simulation.SeedFighterConductiveForTest(1, markSource, BasicComboRules.ConductiveMarkBaselineFrames);
        }
        simulation.Advance(Frame(5, GameplayButtons.Special2), Frame(5, GameplayButtons.None));
        for (int tick = 6; tick < 30; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        return simulation;
    }

    private static FighterSimulation PulseWithoutCoil() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(ChainTesla()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 1402, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.None), Frame(0, GameplayButtons.None));
        simulation.SeedFighterConductiveForTest(1, 0, BasicComboRules.ConductiveMarkBaselineFrames);
        simulation.Advance(Frame(1, GameplayButtons.Special2), Frame(1, GameplayButtons.None));
        for (int tick = 2; tick < 26; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        return simulation;
    }

    private static CharacterData ChainTesla() => new() {
        CharacterID = "tesla",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData {
            ExecutionType = AbilityExecutionType.PersistentObject,
            BaseDamage = 5f,
            PersistentObjectID = "tesla_coil",
            MaxActiveObjects = 2,
            Lifetime = 30f,
            CooldownDuration = 0f
        },
        SpecialAttackTwo = new AbilityData {
            ExecutionType = AbilityExecutionType.Area,
            BaseDamage = PulseDamage,
            KnockbackForce = Vector2.Zero,
            CooldownDuration = 10f,
            Lifetime = 0.25f,
            DamageTickIntervalFrames = 5,
            AppliedStatus = StatusType.Root,
            StatusDuration = 2f,
            StatusIntensity = 1f
        },
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed, sbyte moveX = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = pressed,
        Pressed = pressed
    };
}
