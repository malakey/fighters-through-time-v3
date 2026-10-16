using System;
using FTT.Core;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// D10 (user direction 2026-10-11, ledger <c>USER-NOT-YET-FLASH</c>): which
    /// of a fighter's Special 1, Special 2 and Ultimate presses this tick the
    /// ability lockout ALONE refused. Flags, because a same-tick Special 1 +
    /// Special 2 press can flash both slots.
    /// </summary>
    [Flags]
    public enum FighterLockoutRefusal {
        None = 0,
        SpecialOne = 1,
        SpecialTwo = 2,
        Ultimate = 4
    }

    /// <summary>
    /// The pre-tick values <see cref="FighterLockoutRefusalRules.Evaluate"/>
    /// needs to reconstruct what the ability and combat systems saw when they
    /// read the press. Presentation state, captured by
    /// <c>FighterSimulationDriver</c> before <c>Simulation.Advance</c>; never in
    /// a snapshot or a hash. Integers, a bool and <see cref="FP64"/> only.
    /// </summary>
    public struct LockoutRefusalBaseline {
        /// <summary>The fighter's hitstop at tick start (component 310).</summary>
        public int HitstopFrames;
        /// <summary>Special 1's cooldown at tick start (component 305).</summary>
        public int SpecialOneCooldownFrames;
        /// <summary>Special 2's cooldown at tick start.</summary>
        public int SpecialTwoCooldownFrames;
        /// <summary>The meter at tick start.</summary>
        public FP64 Influence;
        /// <summary>Whether the fighter already owned an Ultimate (any A02 phase) at tick start.</summary>
        public bool UltimateBusy;

        /// <summary>Reads the baseline from the fighter's components before the tick.</summary>
        public static LockoutRefusalBaseline Capture(
            in FighterRuntimeComponent runtime,
            in FighterVerbComponent verb,
            in FighterStateComponent fighter,
            in FighterUltimateActivationComponent activation) => new() {
                HitstopFrames = verb.HitstopFrames,
                SpecialOneCooldownFrames = runtime.SpecialOneCooldownFrames,
                SpecialTwoCooldownFrames = runtime.SpecialTwoCooldownFrames,
                Influence = fighter.Influence,
                UltimateBusy = FighterUltimateActivationRules.IsCasterBusy(in activation)
            };
    }

    /// <summary>
    /// The Fighter half of the D10 "not yet" flash: a pure, engine-free
    /// classifier the driver runs after each tick to decide which HUD slot to
    /// flash. <b>Presentation only; never called by an ISystem</b> — nothing it
    /// computes enters the simulation, a snapshot or a hash. Integers and
    /// <see cref="FP64"/> only.
    ///
    /// <para>It flashes a slot only when the lockout was the <em>sole</em>
    /// reason the press was refused: the slot off cooldown (the Ultimate: a full
    /// meter) at the moment the press was read, the fighter in a state the
    /// ability system reads presses in, and no Special or Ultimate accepted that
    /// tick. The press itself is read from the post-tick
    /// <see cref="FighterRuntimeComponent.PressedButtons"/>, never from the
    /// submitted frame: every input-discard path (knockdown, the activation
    /// caster and Foresight stance, the respawn platform, a held victim, the
    /// countdown, Sudden Death setup) zeroes it before any ability reads it,
    /// and nothing clears it on acceptance. A freeze-buffered Special released
    /// on the thaw tick joins it there, so a held press the lockout refuses at
    /// the thaw flashes too. The cooldowns are reconstructed for the moment of
    /// the read (one tick lower on an unfrozen tick, whose
    /// <c>TickCounters</c> ran first; untouched on the last freeze tick, which
    /// the systems read but <c>TickCounters</c> skips), so a same-tick Momentum
    /// refund or Requiem shave cannot make a cooldown-refused press flash; the
    /// Ultimate needs the pre-tick meter at the cap, so a meter that fills later
    /// in the tick cannot either.</para>
    ///
    /// <para>Accepted misses, never false flashes: a press on the tick the
    /// fighter is hit or its own swing lands with damage (post-tick hitstop or
    /// hitstun), on the tick an absorbed landing sets the swing-landed latch, on
    /// the tick the meter fills inside <c>TickCounters</c>, and a fighter
    /// captured or knocked out later in the tick. An Ultimate pressed from the
    /// block stance or in shieldstun is refused by the Ultimate gate itself
    /// (combat and movement update 2026-10-16, decision 2.3) and, like every
    /// press in the stance, never flashes. A press during the landing tech's
    /// lock or the fighter's own Special cast is discarded before any system
    /// reads it (1.1(3), 1.2), so it never flashes either.</para>
    /// </summary>
    public static class FighterLockoutRefusalRules {
        private const int SpecialOneButton = (int)GameplayButtons.Special1;
        private const int SpecialTwoButton = (int)GameplayButtons.Special2;
        private const int UltimateButton = (int)GameplayButtons.Ultimate;

        /// <summary>
        /// Classifies one fighter's tick from its pre-tick
        /// <paramref name="before"/> and its post-tick components.
        /// <paramref name="actionState"/> is the post-tick component 323 and
        /// <paramref name="knockdown"/> the post-tick component 320; a caller
        /// without them passes nothing (all zero: no cast, not down), which is
        /// safe because a busy caster's and a downed fighter's buttons are
        /// discarded before the read (fix round A: on their hitstop ticks too).
        /// </summary>
        public static FighterLockoutRefusal Evaluate(
            in LockoutRefusalBaseline before,
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            in FighterVerbComponent verb,
            in FighterUltimateActivationComponent activation,
            in FighterActionStateComponent actionState = default,
            in FighterKnockdownComponent knockdown = default) {

            // 1. Frozen at tick start and not on the freeze's last tick: no
            // system read a press (the last tick, 1 → 0, is read).
            if (before.HitstopFrames > 1) return FighterLockoutRefusal.None;

            // 2. Read-time cooldowns, then: anything accepted this tick flashes
            // nothing. The movement cooldown is deliberately not read — the
            // movement ability is outside the lockout family.
            bool lastFreezeTick = before.HitstopFrames == 1;
            int specialOneRead = ReadTimeCooldown(before.SpecialOneCooldownFrames, lastFreezeTick);
            int specialTwoRead = ReadTimeCooldown(before.SpecialTwoCooldownFrames, lastFreezeTick);
            if (runtime.SpecialOneCooldownFrames > specialOneRead
                || runtime.SpecialTwoCooldownFrames > specialTwoRead) return FighterLockoutRefusal.None;
            if (!before.UltimateBusy && FighterUltimateActivationRules.IsCasterBusy(in activation)) {
                return FighterLockoutRefusal.None;
            }

            // 3. The press as the systems saw it, in a state that reads it.
            int pressed = runtime.PressedButtons;
            if ((pressed & (SpecialOneButton | SpecialTwoButton | UltimateButton)) == 0) {
                return FighterLockoutRefusal.None;
            }
            if (!FighterAbilityLockoutRules.CanReadAbilityPress(
                    in fighter, in runtime, in verb, in actionState, in knockdown, in activation)
                || FighterMatchFlowRules.IsOnRespawnPlatform(in fighter)
                || FighterUltimateActivationRules.IsCaptured(in activation)
                || FighterUltimateActivationRules.IsCasterBusy(in activation)) {
                return FighterLockoutRefusal.None;
            }

            // 4. §1.7: a meter-paid Ultimate the lockout alone held back claims
            // the tick from the Specials, so only the Ultimate flashes.
            if ((pressed & UltimateButton) != 0
                && before.Influence >= FighterMeterRules.Cap
                && !FighterAbilityLockoutRules.AllowsUltimate(in runtime)) {
                return FighterLockoutRefusal.Ultimate;
            }

            // 5. A Special the D1 gate refused while its slot was ready.
            if (FighterAbilityLockoutRules.AllowsSpecial(in runtime)) return FighterLockoutRefusal.None;
            FighterLockoutRefusal refused = FighterLockoutRefusal.None;
            if ((pressed & SpecialOneButton) != 0 && specialOneRead <= 0) refused |= FighterLockoutRefusal.SpecialOne;
            if ((pressed & SpecialTwoButton) != 0 && specialTwoRead <= 0) refused |= FighterLockoutRefusal.SpecialTwo;
            return refused;
        }

        /// <summary>
        /// A cooldown as the ability system read it: <c>TickCounters</c> took one
        /// frame off an unfrozen tick before the read, and none off the last
        /// freeze tick.
        /// </summary>
        private static int ReadTimeCooldown(int before, bool lastFreezeTick) =>
            lastFreezeTick ? before : Math.Max(0, before - 1);
    }
}
