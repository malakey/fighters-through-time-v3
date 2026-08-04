using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

[TestSuite]
[RequireGodotRuntime]
public class FighterSimulationTests {
    [TestCase]
    public void IdenticalInputsProduceIdenticalHashesForTwoThousandTicks() {
        var first = new FighterSimulation(seed: 42);
        var second = new FighterSimulation(seed: 42);

        for (int tick = 0; tick < 2000; tick++) {
            PlayerInputFrame p1 = InputFor(0, tick);
            PlayerInputFrame p2 = InputFor(1, tick);
            long firstHash = first.Advance(p1, p2);
            long secondHash = second.Advance(p1, p2);
            AssertThat(firstHash).IsEqual(secondHash);
        }
    }

    [TestCase]
    public void FullStateRestoreResumesTheSameHashSequence() {
        var uninterrupted = new FighterSimulation(seed: 99);
        for (int tick = 0; tick < 90; tick++) {
            uninterrupted.Advance(InputFor(0, tick), InputFor(1, tick));
        }
        byte[] snapshot = uninterrupted.CaptureFullState();

        var restored = new FighterSimulation(seed: 99);
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        for (int tick = 90; tick < 240; tick++) {
            long expected = uninterrupted.Advance(InputFor(0, tick), InputFor(1, tick));
            long actual = restored.Advance(InputFor(0, tick), InputFor(1, tick));
            AssertThat(actual).IsEqual(expected);
        }
    }

    [TestCase]
    public void CorrectedPredictedInputsRollbackAndConverge() {
        var authoritative = new FighterSimulation(seed: 7);
        var predicted = new FighterSimulation(seed: 7);

        for (int tick = 0; tick < 80; tick++) {
            PlayerInputFrame p1 = InputFor(0, tick);
            PlayerInputFrame p2 = InputFor(1, tick);
            authoritative.Advance(p1, p2);
            if (tick >= 20 && tick < 30) predicted.AdvanceWithPredictedPlayerTwo(p1);
            else predicted.Advance(p1, p2);
        }
        AssertThat(predicted.CurrentHash == authoritative.CurrentHash).IsFalse();

        for (int tick = 20; tick < 30; tick++) {
            AssertThat(predicted.CorrectPlayerTwoInput(tick, InputFor(1, tick))).IsTrue();
        }

        AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
    }

    [TestCase]
    public void StockLossRetainsSeventyFivePercentOfInfluence() {
        var simulation = new FighterSimulation(seed: 1, spawnDistance: 1);
        int attackTick = 0;
        while (simulation.TryGetFighter(1, out FighterStateComponent target) && target.Stocks == 3) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
            sbyte attackerAxis = target.Position.x >= attacker.Position.x ? (sbyte)127 : (sbyte)-127;
            simulation.Advance(
                Frame(attackTick, attackerAxis, GameplayButtons.BasicAttack),
                Frame(attackTick, (sbyte)-attackerAxis, GameplayButtons.None));
            for (int cooldown = 0; cooldown < 18; cooldown++) {
                attackTick++;
                simulation.TryGetFighter(0, out attacker);
                simulation.TryGetFighter(1, out target);
                attackerAxis = target.Position.x >= attacker.Position.x ? (sbyte)127 : (sbyte)-127;
                simulation.Advance(
                    Frame(attackTick, attackerAxis, GameplayButtons.None),
                    Frame(attackTick, (sbyte)-attackerAxis, GameplayButtons.None));
            }
            attackTick++;
            AssertThat(attackTick < 400).IsTrue();
        }

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent defeated)).IsTrue();
        AssertThat(defeated.Stocks).IsEqual(2);
        AssertThat(defeated.Influence.RawValue).IsEqual(
            xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(18.75).RawValue);
    }

    [TestCase]
    public void LoadoutFactoryUsesNormalizedCharacterResources() {
        CharacterData joan = ResourceLoader.Load<CharacterData>("res://resources/Characters/joan_data.tres");
        AssertObject(joan).IsNotNull();

        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(joan);
        AssertThat(loadout.CharacterID).IsEqual((int)FighterCharacterID.Joan);
        AssertThat(loadout.MaxHP).IsEqual(110);
        AssertThat(loadout.MaxBlockCharges).IsEqual(3);
        AssertThat(loadout.BasicDamage).IsEqual(12);
        AssertThat(loadout.SpecialOneDamage).IsEqual(14);
        AssertThat(loadout.SpecialOneStatusType).IsEqual((int)StatusType.RadiantBurn);
        AssertThat(loadout.SpecialOneStatusFrames).IsEqual(180);
    }

    [TestCase]
    public void NewSpecialStatusCompletelyReplacesPreviousStatus() {
        CharacterData character = BuildStatusTestCharacter();
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(character),
            FighterLoadout.Default(FighterCharacterID.Einstein),
            seed: 15,
            spawnDistance: 1);

        simulation.Advance(
            Frame(0, 0, GameplayButtons.Special1),
            Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent firstStatus)).IsTrue();
        AssertThat(firstStatus.StatusType).IsEqual((int)StatusType.RadiantBurn);

        simulation.Advance(
            Frame(1, 0, GameplayButtons.Special2),
            Frame(1, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent replacement)).IsTrue();
        AssertThat(replacement.StatusType).IsEqual((int)StatusType.Root);
        AssertThat(replacement.StatusFrames).IsEqual(120);
        AssertThat(replacement.StatusTickFrames).IsEqual(0);
    }

    private static CharacterData BuildStatusTestCharacter() => new() {
        CharacterID = "joan",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData {
            BaseDamage = 0f,
            KnockbackForce = Vector2.Zero,
            CooldownDuration = 1f,
            AppliedStatus = StatusType.RadiantBurn,
            StatusDuration = 3f,
            StatusIntensity = 1f
        },
        SpecialAttackTwo = new AbilityData {
            BaseDamage = 0f,
            KnockbackForce = Vector2.Zero,
            CooldownDuration = 1f,
            AppliedStatus = StatusType.Root,
            StatusDuration = 2f,
            StatusIntensity = 1f
        },
        UltimateAttack = new AbilityData {
            BaseDamage = 20f,
            KnockbackForce = new Vector2(5f, -3f)
        }
    };

    private static PlayerInputFrame InputFor(int playerID, int tick) {
        int cycle = (tick + playerID * 11) % 120;
        sbyte axis = cycle < 40 ? (sbyte)90 : cycle < 80 ? (sbyte)-90 : (sbyte)0;
        GameplayButtons held = GameplayButtons.None;
        if (tick % 47 == playerID) held |= GameplayButtons.Jump;
        if (tick % 31 == playerID * 3) held |= GameplayButtons.BasicAttack;
        if (tick % 211 == 50 + playerID) held |= GameplayButtons.Special1;
        return Frame(tick, axis, held);
    }

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = pressed,
        Pressed = pressed
    };
}
