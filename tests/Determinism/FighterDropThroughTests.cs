using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// CM-MOVE (combat and movement update 2026-10-16, decision 6.3; plan §2.6.3;
/// ledger <c>USER-DROP-THROUGH-BOTH-INPUTS</c>, closing the M-18 divergence):
/// the Fighter sim drops through a one-way platform on a double tap of Down
/// inside <see cref="UniversalMovementRules.DropThroughDoubleTapFrames"/> (18,
/// Story's window) as well as on Down+Jump; neither drops through the solid base
/// floor; shieldstun refuses both; and the window is snapshotted state
/// (component 323 <c>DownTapFrames</c>) that a restored snapshot resumes.
/// </summary>
/// <remarks>
/// Pure C#: default loadouts and <see cref="FighterStageGeometry.ForTest"/>
/// geometry. The taps are <see cref="PlayerInputFrame.Create"/>-built, so the
/// Down press edges are the ones the wire and the CPU produce (plan §1.8).
/// </remarks>
[TestSuite]
public class FighterDropThroughTests {
    private static readonly FP64 PlatformY = FP64.FromDouble(1.5);

    /// <summary>
    /// Two Down taps 17 ticks apart drop through the platform underfoot; taps 18
    /// or 19 apart do not (the first tap's window has closed, so the second only
    /// opens a new one), and a third tap inside that new window drops.
    /// </summary>
    [TestCase]
    public void ADoubleTapInsideEighteenTicksDropsAndSlowerTapsDoNot() {
        AssertThat(UniversalMovementRules.DropThroughDoubleTapFrames).IsEqual(18);
        AssertThat(FTT.Combat.StoryCombatRules.DownDoubleTapFrames).IsEqual(UniversalMovementRules.DropThroughDoubleTapFrames);
        foreach (int gap in new[] { 2, 5, 17 }) {
            FighterSimulation simulation = OnPlatform(out int tick);
            tick = Tap(simulation, tick);
            tick = Idle(simulation, tick, gap - 2);
            AssertThat(Dropped(simulation)).OverrideFailureMessage($"gap {gap}: one tap dropped.").IsFalse();
            tick = Tap(simulation, tick);
            AssertThat(Dropped(simulation))
                .OverrideFailureMessage($"taps {gap} ticks apart must drop through.")
                .IsTrue();
        }
        foreach (int gap in new[] { 18, 19 }) {
            FighterSimulation simulation = OnPlatform(out int tick);
            tick = Tap(simulation, tick);
            tick = Idle(simulation, tick, gap - 2);
            tick = Tap(simulation, tick);
            AssertThat(Dropped(simulation))
                .OverrideFailureMessage($"taps {gap} ticks apart must not drop through.")
                .IsFalse();
            AssertThat(Grounded(simulation, PlatformY)).IsTrue();
            // The late tap opened a fresh window: a third tap inside it drops.
            tick = Idle(simulation, tick, 3);
            Tap(simulation, tick);
            AssertThat(Dropped(simulation))
                .OverrideFailureMessage($"gap {gap}: a tap inside the reopened window must drop.")
                .IsTrue();
        }
    }

    /// <summary>
    /// The solid base floor never drops: on a stage with platforms (the Sealed
    /// rule) a double tap of Down on the floor leaves the fighter standing, and
    /// Down+Jump there is a plain jump — the same rule on a real Sealed stage's
    /// base floor (Florence).
    /// </summary>
    [TestCase]
    public void TheSolidBaseFloorNeverDropsForEitherInput() {
        foreach (FighterStageGeometry geometry in new[] { PlatformGeometry(), FighterStageGeometry.Florence }) {
            var simulation = new FighterSimulation(seed: 6301, rules: FighterMatchRules.Disabled, stageGeometry: geometry);
            int tick = Idle(simulation, 0, 5);
            AssertThat(Grounded(simulation, FP64.Zero)).IsTrue();
            tick = Tap(simulation, tick);
            tick = Idle(simulation, tick, 2);
            tick = Tap(simulation, tick);
            for (int step = 0; step < 6; step++, tick++) {
                simulation.Advance(PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.None), Neutral(tick));
                AssertThat(simulation.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
                AssertThat(fighter.DropThroughFrames)
                    .OverrideFailureMessage($"{geometry.StageID}: a double tap dropped through the base floor.")
                    .IsEqual(0);
                AssertThat(fighter.IsGrounded).IsEqual(1);
            }
            simulation.Advance(PlayerInputFrame.Create((uint)tick, 0f, 1f, GameplayButtons.Down | GameplayButtons.Jump), Neutral(tick));
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent jumped)).IsTrue();
            AssertThat(jumped.DropThroughFrames)
                .OverrideFailureMessage($"{geometry.StageID}: Down+Jump dropped through the base floor.")
                .IsEqual(0);
            AssertThat(jumped.IsGrounded == 0 && jumped.Velocity.y > FP64.Zero)
                .OverrideFailureMessage($"{geometry.StageID}: Down+Jump on the base floor must jump.")
                .IsTrue();
        }
    }

    /// <summary>
    /// Shieldstun refuses both inputs, and a tap inside it opens no window: a
    /// blocker standing on the platform double-taps Down (and presses Down+Jump)
    /// inside the shieldstun of a blocked swing and stays on the top; a single
    /// tap just after the shieldstun does not drop either; a fresh double tap
    /// then does.
    /// </summary>
    [TestCase]
    public void ShieldstunRefusesTheDropThroughAndOpensNoWindow() {
        FighterStageGeometry geometry = FighterStageGeometry.ForTest("cm_move_drop_through_block",
            new[] { new FighterStagePlatform(FP64.Zero, PlatformY, FP64.FromInt(3)) }, spawnDistance: 1);
        var simulation = new FighterSimulation(seed: 6302, rules: FighterMatchRules.Disabled, stageGeometry: geometry);
        int tick = 0;
        // Both fighters jump onto the wide platform.
        for (int step = 0; step < 120 && !(Grounded(simulation, PlatformY, 0) && Grounded(simulation, PlatformY, 1)); step++, tick++) {
            simulation.Advance(HeldJump(tick), HeldJump(tick));
        }
        AssertThat(Grounded(simulation, PlatformY, 0) && Grounded(simulation, PlatformY, 1))
            .OverrideFailureMessage("the fighters never stood on the platform.")
            .IsTrue();
        tick = Idle(simulation, tick, 10);

        // Player one raises the block; player two walks in and swings.
        GameplayButtons previousOne = GameplayButtons.None;
        GameplayButtons previousTwo = GameplayButtons.None;
        int stunTick = -1;
        for (int step = 0; step < 120 && stunTick < 0; step++, tick++) {
            simulation.TryGetFighter(0, out FighterStateComponent one);
            simulation.TryGetFighter(1, out FighterStateComponent two);
            bool close = two.Position.x - one.Position.x < FP64.FromDouble(1.1);
            GameplayButtons twoHeld = close && step % 2 == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None;
            simulation.Advance(
                PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.Block, previousOne),
                PlayerInputFrame.Create((uint)tick, close ? 0f : -1f, 0f, twoHeld, previousTwo));
            previousOne = GameplayButtons.Block;
            previousTwo = twoHeld;
            simulation.TryGetFighterVerb(0, out FighterVerbComponent verb);
            if (verb.ShieldStunFrames > 0) stunTick = tick;
        }
        AssertThat(stunTick >= 0).OverrideFailureMessage("the swing never put the blocker in shieldstun.").IsTrue();
        tick = stunTick + 1;

        // Inside the shieldstun: Down taps and Down+Jump, Block held throughout.
        GameplayButtons[] script = {
            GameplayButtons.Block | GameplayButtons.Down, GameplayButtons.Block,
            GameplayButtons.Block | GameplayButtons.Down, GameplayButtons.Block | GameplayButtons.Down | GameplayButtons.Jump
        };
        GameplayButtons previous = GameplayButtons.Block;
        foreach (GameplayButtons held in script) {
            simulation.TryGetFighterVerb(0, out FighterVerbComponent before);
            AssertThat(before.ShieldStunFrames > 0 || before.HitstopFrames > 0)
                .OverrideFailureMessage($"tick {tick}: the script outran the shieldstun.")
                .IsTrue();
            simulation.Advance(PlayerInputFrame.Create((uint)tick, 0f, 0f, held, previous), Neutral(tick));
            previous = held;
            tick++;
            AssertThat(Dropped(simulation)).OverrideFailureMessage($"tick {tick - 1}: shieldstun let a drop through.").IsFalse();
        }
        // Wait out the shieldstun, Block held.
        for (int step = 0; step < 30; step++, tick++) {
            simulation.TryGetFighterVerb(0, out FighterVerbComponent verb);
            if (verb.ShieldStunFrames <= 0 && verb.HitstopFrames <= 0) break;
            simulation.Advance(PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.Block, previous), Neutral(tick));
            previous = GameplayButtons.Block;
        }
        AssertThat(simulation.TryGetFighterActionState(0, out FighterActionStateComponent action)).IsTrue();
        AssertThat(action.DownTapFrames).OverrideFailureMessage("a tap in shieldstun opened the window.").IsEqual(0);
        // One tap after it: no drop (no window was open). A second, now: drop.
        simulation.Advance(PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.Block | GameplayButtons.Down, previous), Neutral(tick));
        tick++;
        AssertThat(Dropped(simulation)).IsFalse();
        simulation.Advance(PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.Block, GameplayButtons.Block | GameplayButtons.Down), Neutral(tick));
        tick++;
        simulation.Advance(PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.Block | GameplayButtons.Down, GameplayButtons.Block), Neutral(tick));
        AssertThat(Dropped(simulation)).OverrideFailureMessage("a double tap from the stance after shieldstun must drop.").IsTrue();
    }

    /// <summary>
    /// Down+Jump still drops through on its own tick, and the double tap's window
    /// is snapshotted: a full-state snapshot taken between the two taps and
    /// restored into a fresh simulation resumes identically, hash for hash, and
    /// drops on the same tick.
    /// </summary>
    [TestCase]
    public void DownJumpStillDropsAndASnapshotBetweenTapsResumesIdentically() {
        FighterSimulation downJump = OnPlatform(out int start);
        downJump.Advance(PlayerInputFrame.Create((uint)start, 0f, 1f, GameplayButtons.Down | GameplayButtons.Jump), Neutral(start));
        AssertThat(Dropped(downJump)).OverrideFailureMessage("Down+Jump must still drop through.").IsTrue();

        FighterSimulation original = OnPlatform(out int tick);
        tick = Tap(original, tick);
        tick = Idle(original, tick, 4);
        AssertThat(original.TryGetFighterActionState(0, out FighterActionStateComponent window)).IsTrue();
        AssertThat(window.DownTapFrames > 0).OverrideFailureMessage("the first tap opened no window.").IsTrue();
        byte[] snapshot = original.CaptureFullState();
        long snapshotHash = original.CurrentHash;

        var restored = new FighterSimulation(seed: 6300, rules: FighterMatchRules.Disabled, stageGeometry: PlatformGeometry());
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(snapshotHash);
        var frames = new List<PlayerInputFrame>();
        frames.Add(PlayerInputFrame.Create((uint)tick, 0f, 1f, GameplayButtons.Down));
        for (int step = 1; step < 40; step++) {
            frames.Add(PlayerInputFrame.Create((uint)(tick + step), 0f, 0f, GameplayButtons.None,
                step == 1 ? GameplayButtons.Down : GameplayButtons.None));
        }
        for (int step = 0; step < frames.Count; step++) {
            long hash = original.Advance(frames[step], Neutral(tick + step));
            AssertThat(restored.Advance(frames[step], Neutral(tick + step)))
                .OverrideFailureMessage($"step {step}: the restored simulation diverged.")
                .IsEqual(hash);
            if (step == 0) {
                AssertThat(Dropped(original)).OverrideFailureMessage("the second tap must drop.").IsTrue();
                AssertThat(Dropped(restored)).OverrideFailureMessage("the restored second tap must drop.").IsTrue();
            }
        }
    }

    // === helpers ===

    /// <summary>A one-way platform 1.5 u up, centred on player one's spawn (x = −4).</summary>
    private static FighterStageGeometry PlatformGeometry() => FighterStageGeometry.ForTest(
        "cm_move_drop_through", new[] { new FighterStagePlatform(FP64.FromInt(-4), PlatformY, FP64.FromDouble(1.5)) });

    /// <summary>Player one jumped onto the platform and settled; returns the simulation and the next tick.</summary>
    private static FighterSimulation OnPlatform(out int tick) {
        var simulation = new FighterSimulation(seed: 6300, rules: FighterMatchRules.Disabled, stageGeometry: PlatformGeometry());
        tick = 0;
        for (int step = 0; step < 120 && !(tick > 2 && Grounded(simulation, PlatformY)); step++, tick++) {
            simulation.Advance(HeldJump(tick), Neutral(tick));
        }
        AssertThat(Grounded(simulation, PlatformY)).OverrideFailureMessage("player one never stood on the platform.").IsTrue();
        tick = Idle(simulation, tick, 20);
        AssertThat(Grounded(simulation, PlatformY)).IsTrue();
        return simulation;
    }

    /// <summary>
    /// One Create-built Down tap: pressed on <paramref name="tick"/>, released
    /// on the next. Returns the tick after the release (press + 2), so the next
    /// press lands <c>2 + idle ticks</c> after this one; a tap that dropped
    /// returns the tick after its press.
    /// </summary>
    private static int Tap(FighterSimulation simulation, int tick) {
        simulation.Advance(PlayerInputFrame.Create((uint)tick, 0f, 1f, GameplayButtons.Down), Neutral(tick));
        if (Dropped(simulation)) return tick + 1;
        simulation.Advance(PlayerInputFrame.Create((uint)(tick + 1), 0f, 0f, GameplayButtons.None, GameplayButtons.Down), Neutral(tick + 1));
        return tick + 2;
    }

    /// <summary>Advances <paramref name="ticks"/> neutral ticks (Create-built), returning the next tick.</summary>
    private static int Idle(FighterSimulation simulation, int tick, int ticks) {
        for (int step = 0; step < ticks; step++, tick++) {
            simulation.Advance(PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.None), Neutral(tick));
        }
        return tick;
    }

    private static bool Dropped(FighterSimulation simulation) {
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
        return fighter.DropThroughFrames > 0;
    }

    private static bool Grounded(FighterSimulation simulation, FP64 surface, int playerID = 0) {
        simulation.TryGetFighter(playerID, out FighterStateComponent fighter);
        return fighter.IsGrounded != 0 && fighter.Position.y.RawValue == surface.RawValue;
    }

    /// <summary>A hand-built held jump: pressed on tick 0, held after (a full hop).</summary>
    private static PlayerInputFrame HeldJump(int tick) => new() {
        Tick = (uint)tick,
        Held = GameplayButtons.Jump,
        Pressed = tick == 0 ? GameplayButtons.Jump : GameplayButtons.None
    };

    private static PlayerInputFrame Neutral(int tick) => new() { Tick = (uint)tick };
}
