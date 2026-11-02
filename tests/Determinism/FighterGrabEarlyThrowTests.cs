using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Grab pass 2026-10-31 (step A): the early throw, the 20-frame auto throw and
/// the catch freeze in the deterministic simulation. Tick counts are measured
/// from the tick whose Advance connected the grab ("catch tick").
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterGrabEarlyThrowTests {

    // The thaw tick is hold tick 0; an early throw needs ThrowEarlyMinFrames completed hold ticks.
    /// <summary>Combat ticks frozen by the catch hitstop (the thaw tick reads 0).</summary>
    private static int FrozenCombatTicks => BasicComboRules.GrabCatchHitstopFrames - 1;

    [TestCase]
    public void TheCatchFreezesBothFightersWithoutShorteningTheHold() {
        var sim = NewSimulation(5001);
        int tick = StartGrab(sim, 0, 0);
        AdvanceUntilHeld(sim, ref tick, 0, 0);
        AssertThat(sim.TryGetFighterVerb(0, out FighterVerbComponent grabber)).IsTrue();
        AssertThat(sim.TryGetFighterVerb(1, out FighterVerbComponent victim)).IsTrue();
        AssertThat(grabber.HitstopFrames).IsEqual(BasicComboRules.GrabCatchHitstopFrames);
        AssertThat(victim.HitstopFrames).IsEqual(BasicComboRules.GrabCatchHitstopFrames);
        int framesAtCatch = grabber.GrabPhaseFrames;

        for (int i = 0; i < FrozenCombatTicks - 1; i++) {
            sim.Advance(Idle(tick, 0, 0), Idle(tick, 0, 0));
            tick++;
            AssertThat(sim.TryGetFighterVerb(0, out grabber)).IsTrue();
            AssertThat(sim.TryGetFighterVerb(1, out victim)).IsTrue();
            AssertThat(grabber.GrabPhaseFrames)
                .OverrideFailureMessage("The hold must not advance while the catch freeze runs.")
                .IsEqual(framesAtCatch);
            AssertThat(victim.BeingHeld).IsEqual(1);
        }
    }

    [TestCase]
    public void TheChordsOwnPressNeverCountsAndNothingHeldThrowsForwardAtTwenty() {
        var sim = NewSimulation(5002);
        int tick = StartGrab(sim, 0, 0);
        int catchTick = AdvanceUntilHeld(sim, ref tick, 0, 0);
        int animationTick = AdvanceUntilThrowAnimation(sim, ref tick, 0, 0, GameplayButtons.None, out int direction);
        AssertThat(direction).IsEqual(FighterGrabRules.ThrowForward);
        AssertThat(animationTick - catchTick)
            .OverrideFailureMessage("With nothing pressed the throw starts after the freeze plus the 20-tick auto hold.")
            .IsEqual(FrozenCombatTicks + BasicComboRules.ThrowDecisionFrames);
    }

    [TestCase]
    public void AnAttackPressThrowsAtTheMinimumAndNotBefore() {
        var sim = NewSimulation(5003);
        int tick = StartGrab(sim, 0, 0);
        int catchTick = AdvanceUntilHeld(sim, ref tick, 0, 0);
        int animationTick = AdvanceUntilThrowAnimation(sim, ref tick, 0, 0, GameplayButtons.BasicAttack, out int direction);
        AssertThat(direction).IsEqual(FighterGrabRules.ThrowForward);
        AssertThat(animationTick - catchTick)
            .OverrideFailureMessage("A fresh Attack press every tick throws on the first tick the minimum allows.")
            .IsEqual(FrozenCombatTicks + 1 + BasicComboRules.ThrowEarlyMinFrames);
    }

    [TestCase]
    public void AHeldBackOrUpFromBeforeTheCatchThrowsAtTheMinimumInThatDirection() {
        foreach ((sbyte x, sbyte y, int expected) in new[] {
                     ((sbyte)-127, (sbyte)0, FighterGrabRules.ThrowBack),
                     ((sbyte)0, (sbyte)-127, FighterGrabRules.ThrowUp) }) {
            var sim = NewSimulation(5004);
            int tick = StartGrab(sim, x, y);
            int catchTick = AdvanceUntilHeld(sim, ref tick, x, y);
            int animationTick = AdvanceUntilThrowAnimation(sim, ref tick, x, y, GameplayButtons.None, out int direction);
            AssertThat(direction).IsEqual(expected);
            AssertThat(animationTick - catchTick)
                .IsEqual(FrozenCombatTicks + 1 + BasicComboRules.ThrowEarlyMinFrames);
        }
    }

    // ---- Harness -------------------------------------------------------------

    /// <summary>Walks into range and chords the grab; the stick stays as given afterwards.</summary>
    private static int StartGrab(FighterSimulation sim, sbyte moveX, sbyte moveY) {
        int tick = 0;
        for (; tick < 90; tick++) {
            sim.TryGetFighter(0, out FighterStateComponent one);
            sim.TryGetFighter(1, out FighterStateComponent two);
            if (FP64.Abs(two.Position.x - one.Position.x) <= FP64.FromDouble(BasicComboRules.GrabReachUnits)) break;
            sim.Advance(Press(tick, 127, 0, GameplayButtons.None), Idle(tick, 0, 0));
        }
        sim.Advance(
            new PlayerInputFrame {
                Tick = (uint)tick, MoveX = moveX, MoveY = moveY,
                Held = GameplayButtons.Block | GameplayButtons.BasicAttack, Pressed = GameplayButtons.BasicAttack
            },
            Idle(tick, 0, 0));
        return tick + 1;
    }

    /// <summary>Advances (no presses, so the chord's own is the only one) until the victim is held; returns the connect tick.</summary>
    private static int AdvanceUntilHeld(FighterSimulation sim, ref int tick, sbyte moveX, sbyte moveY) {
        for (int i = 0; i < 40; i++) {
            sim.Advance(Idle(tick, moveX, moveY), Idle(tick, 0, 0));
            tick++;
            sim.TryGetFighterVerb(1, out FighterVerbComponent victim);
            if (victim.BeingHeld != 0) return tick - 1;
        }
        AssertThat(false).OverrideFailureMessage("The grab never connected.").IsTrue();
        return -1;
    }

    /// <summary>Advances until the throw animation begins, asserting the hold persists until then. Returns that tick.</summary>
    private static int AdvanceUntilThrowAnimation(
        FighterSimulation sim, ref int tick, sbyte moveX, sbyte moveY, GameplayButtons pressed, out int direction) {
        direction = -1;
        for (int i = 0; i < 60; i++) {
            sim.Advance(Press(tick, moveX, moveY, pressed), Idle(tick, 0, 0));
            tick++;
            sim.TryGetFighterVerb(0, out FighterVerbComponent grabber);
            if (grabber.GrabPhase == FighterGrabRules.PhaseThrowAnimation) {
                direction = grabber.ThrowDirection;
                return tick - 1;
            }
            AssertThat(grabber.GrabPhase).IsEqual(FighterGrabRules.PhaseHolding);
        }
        AssertThat(false).OverrideFailureMessage("The throw never began.").IsTrue();
        return -1;
    }

    private static FighterSimulation NewSimulation(int seed) => new(
        FighterLoadoutFactory.FromCharacterData(Grappler()),
        FighterLoadoutFactory.FromCharacterData(Grappler()),
        seed: seed,
        spawnDistance: 1,
        rules: FighterMatchRules.Disabled);

    private static CharacterData Grappler() => new() {
        CharacterID = "tesla",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static PlayerInputFrame Press(int tick, sbyte moveX, sbyte moveY, GameplayButtons pressed) => new() {
        Tick = (uint)tick, MoveX = moveX, MoveY = moveY, Held = pressed, Pressed = pressed
    };

    private static PlayerInputFrame Idle(int tick, sbyte moveX, sbyte moveY) => new() {
        Tick = (uint)tick, MoveX = moveX, MoveY = moveY, Held = GameplayButtons.None, Pressed = GameplayButtons.None
    };
}
