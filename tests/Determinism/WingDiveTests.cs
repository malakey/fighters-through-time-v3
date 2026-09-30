using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7b (A08) — Joan's Ascendant Wings in the sim, on her authored
/// data: the rising slash-leap climbs at the authored speed for the cast's
/// frames (D13: rise kept; <c>VERIFY-WING-DIVE-LEAP</c>), a held movement
/// button at the end of the rise becomes the Wing-Dive — a steep forward
/// descent of up to one second — releasing ends it into a normal fall, and
/// Attack during the dive ends it into her aerial string.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WingDiveTests {
    // Joan's authored cast: 12 startup + 6 active + 18 recovery frames.
    private const int RiseFrames = 36;

    [TestCase]
    public void HoldingTheButtonTurnsTheEndOfTheRiseIntoASteepForwardDive() {
        var simulation = NewMatch();
        simulation.Advance(Held(0, pressed: true), Idle(0));
        for (int tick = 1; tick <= RiseFrames; tick++) simulation.Advance(Held(tick), Idle(tick));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent apex)).IsTrue();
        // 7 units/s for 36 frames: the rise is 4.2 units.
        AssertThat(apex.Position.y > FP64.FromDouble(4.1)).IsTrue();
        AssertThat(apex.Position.y < FP64.FromDouble(4.3)).IsTrue();

        simulation.Advance(Held(RiseFrames + 1), Idle(RiseFrames + 1));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.UniversalMovementState).IsEqual(FighterKitMotion.WingDive);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent diving)).IsTrue();
        AssertThat(diving.Velocity.x).IsEqual(FP64.FromInt(5));
        AssertThat(diving.Velocity.y).IsEqual(FP64.FromInt(-9));
    }

    [TestCase]
    public void ReleasingTheButtonEndsTheDiveIntoANormalFall() {
        var simulation = NewMatch();
        simulation.Advance(Held(0, pressed: true), Idle(0));
        for (int tick = 1; tick <= RiseFrames + 3; tick++) simulation.Advance(Held(tick), Idle(tick));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent diving)).IsTrue();
        AssertThat(diving.UniversalMovementState).IsEqual(FighterKitMotion.WingDive);

        simulation.Advance(Idle(RiseFrames + 4), Idle(RiseFrames + 4));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent released)).IsTrue();
        AssertThat(released.UniversalMovementState).IsEqual(0);
        // Normal gravity takes over from the dive's velocity.
        simulation.Advance(Idle(RiseFrames + 5), Idle(RiseFrames + 5));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent falling)).IsTrue();
        AssertThat(falling.Velocity.y < FP64.FromInt(-9)).IsTrue();
    }

    [TestCase]
    public void AttackDuringTheDiveStartsTheAerialString() {
        var simulation = NewMatch();
        simulation.Advance(Held(0, pressed: true), Idle(0));
        for (int tick = 1; tick <= RiseFrames + 3; tick++) simulation.Advance(Held(tick), Idle(tick));
        var attack = new PlayerInputFrame {
            Tick = (uint)(RiseFrames + 4),
            Held = GameplayButtons.MovementAbility | GameplayButtons.BasicAttack,
            Pressed = GameplayButtons.BasicAttack
        };
        simulation.Advance(attack, Idle(RiseFrames + 4));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.UniversalMovementState).IsEqual(0);
        AssertThat(runtime.AttackPhase != 0).IsTrue();
        // Flag 1 = an aerial string.
        AssertThat(runtime.AttackFlags & 1).IsEqual(1);
    }

    private static FighterSimulation NewMatch() => new(
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>("res://resources/Characters/joan_data.tres")),
        FighterLoadout.Default(FighterCharacterID.Tesla),
        seed: 94,
        spawnDistance: 6,
        rules: FighterMatchRules.Disabled);

    private static PlayerInputFrame Held(int tick, bool pressed = false) => new() {
        Tick = (uint)tick,
        Held = GameplayButtons.MovementAbility,
        Pressed = pressed ? GameplayButtons.MovementAbility : GameplayButtons.None
    };

    private static PlayerInputFrame Idle(int tick) => new() { Tick = (uint)tick };
}
