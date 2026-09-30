using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7b (C03) — Cleopatra's Desert Mirage in the sim, on her authored
/// data: a sand rush of 4 units over 15 frames in any of eight directions
/// (diagonals normalized), passing through the opponent (pushbox off), with no
/// invulnerability. The retired "teleport" reading and its 3 s cap are gone.
/// The sim decoy stays deferred (<c>DEFER-FIGHTER-SAND-DECOY</c>).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SandRushTests {

    [TestCase]
    public void ADiagonalRushCoversFourUnitsAlongTheNormalizedDirection() {
        var simulation = NewMatch(spawnDistance: 6);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
        simulation.Advance(Frame(0, 127, -127, GameplayButtons.MovementAbility), Frame(0, 0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent rushing)).IsTrue();
        AssertThat(rushing.UniversalMovementState).IsEqual(FighterKitMotion.SandRush);
        for (int tick = 1; tick <= 15; tick++) {
            simulation.Advance(Frame(tick, 0, 0, GameplayButtons.None), Frame(tick, 0, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent after)).IsTrue();
        FP64 dx = after.Position.x - before.Position.x;
        FP64 dy = after.Position.y - before.Position.y;
        // 4 / √2 ≈ 2.83 on each axis.
        AssertThat(dx > FP64.FromDouble(2.78) && dx < FP64.FromDouble(2.88)).IsTrue();
        AssertThat(dy > FP64.FromDouble(2.78) && dy < FP64.FromDouble(2.88)).IsTrue();
    }

    [TestCase]
    public void TheRushPassesThroughTheOpponent() {
        var simulation = NewMatch(spawnDistance: 1);
        simulation.Advance(Frame(0, 0, 0, GameplayButtons.MovementAbility), Frame(0, 0, 0, GameplayButtons.None));
        for (int tick = 1; tick <= 16; tick++) {
            simulation.Advance(Frame(tick, 0, 0, GameplayButtons.None), Frame(tick, 0, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent cleopatra)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent opponent)).IsTrue();
        AssertThat(cleopatra.Position.x > opponent.Position.x)
            .OverrideFailureMessage("C03: the rush passes through opponents — the pushbox is off while it travels.")
            .IsTrue();
        AssertThat(cleopatra.Position.x > FP64.FromDouble(2.9)).IsTrue();
    }

    [TestCase]
    public void TheRushGrantsNoInvulnerability() {
        var simulation = NewMatch(spawnDistance: 6);
        simulation.Advance(Frame(0, 0, 0, GameplayButtons.MovementAbility), Frame(0, 0, 0, GameplayButtons.None));
        for (int tick = 1; tick <= 15; tick++) {
            simulation.Advance(Frame(tick, 0, 0, GameplayButtons.None), Frame(tick, 0, 0, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent probe)).IsTrue();
            AssertThat(probe.InvulnerabilityFrames).IsEqual(0);
        }
    }

    private static FighterSimulation NewMatch(int spawnDistance) => new(
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/cleopatra_data.tres")),
        FighterLoadout.Default(FighterCharacterID.Tesla),
        seed: 97,
        spawnDistance: spawnDistance,
        rules: FighterMatchRules.Disabled);

    private static PlayerInputFrame Frame(int tick, sbyte moveX, sbyte moveY, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };
}
