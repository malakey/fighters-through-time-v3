using FTT.Core;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// The sim half of the ability lockout (special global cooldown, user
    /// direction 2026-10-08). The rule and its 30 frames live in
    /// <see cref="FTT.Combat.BasicComboRules"/>; the counter is
    /// <see cref="FighterRuntimeComponent.AbilityLockoutFrames"/>. Every site
    /// that accepts Special 1 or Special 2 checks <see cref="AllowsSpecial"/>
    /// (the D1 hit-confirm gate, 2026-10-11: the Special-sourced lockout once the
    /// current swing has landed, the whole lockout otherwise), the Ultimate
    /// checks <see cref="AllowsUltimate"/>, and each calls
    /// <see cref="ArmAbility"/> the moment it accepts, so a later consumer on the
    /// same tick (S2 after S1, a melee intent after an entity special or an
    /// accepted Ultimate) sees the lock. A swing start calls
    /// <see cref="ArmSwing"/>. A refused press arms nothing. Which action wins a
    /// tick is settled before that: FighterCombatSystem.UltimatePressAccepted
    /// (and UltimatePressHeldBackByLockout, which claims the tick from the
    /// Specials for a meter-paid Ultimate the lockout alone refuses).
    /// </summary>
    internal static class FighterAbilityLockoutRules {
        private const int SpecialOneButton = 1 << 3;
        private const int SpecialTwoButton = 1 << 4;
        private const int UltimateButton = 1 << 7;

        /// <summary>The Ultimate's gate: the whole lockout A (D1 never opens it).</summary>
        public static bool AllowsUltimate(in FighterRuntimeComponent runtime) =>
            !FTT.Combat.BasicComboRules.IsAbilityLocked(runtime.AbilityLockoutFrames);

        /// <summary>
        /// The Specials' gate (D1, 2026-10-11): the Special-sourced lockout S
        /// once the current swing has landed, the whole lockout A otherwise.
        /// </summary>
        public static bool AllowsSpecial(in FighterRuntimeComponent runtime) =>
            !FTT.Combat.BasicComboRules.IsSpecialLocked(
                runtime.AbilityLockoutFrames, runtime.SpecialLockoutFrames, runtime.SwingLanded != 0);

        /// <summary>
        /// Whether <see cref="FighterAbilityEntitySystem"/> reads this fighter's
        /// Special presses at all this tick: its exact early-continue set — out of
        /// stocks, hitstun, daze, hitstop, a grab, a ledge hang, a combat-locked
        /// roll or air dodge, or the block stance. The D10 "not yet" classifier
        /// (<see cref="FighterLockoutRefusalRules"/>) reads the same predicate, so
        /// a press the system never looked at never flashes (2026-10-11).
        /// </summary>
        public static bool CanReadAbilityPress(
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            in FighterVerbComponent verb,
            in FighterActionStateComponent action,
            in FighterKnockdownComponent knockdown,
            in FighterUltimateActivationComponent activation) =>
            fighter.Stocks > 0
            && fighter.HitstunFrames <= 0
            && fighter.DazeFrames <= 0
            // Combat and movement update 2026-10-16: the landing tech's lock
            // (1.1(3)) and the fighter's own Special cast (1.2, component 323),
            // the second gates behind those input discards.
            && verb.TechLockoutFrames <= 0
            && !FighterSpecialCastRules.IsBusy(in action)
            && !FighterDefyRules.IsDefying(in action)
            // Fix round A (CM-FIXA-SIM): three more action locks whose input
            // discard a hitstop thaw tick (or the tick that starts them) does not
            // reach — the M05 knockdown and its get-up, from its landing tick on
            // (component 320); the fighter's own Ultimate, wind-up through whiff
            // recovery and cinematic (A02, component 321); and the respawn
            // platform, whose stock-loss tick reads presses after the KO (as
            // UltimatePressPayable and CanStartGrab already refuse it).
            && !FighterKnockdownRules.IsActive(in knockdown)
            && !FighterUltimateActivationRules.IsCasterBusy(in activation)
            && fighter.RespawnFramesRemaining <= 0
            // V7.1 hitstop / V7.2 grabs: a frozen, grabbing, or held fighter
            // takes no ability action (their button presses are simply not
            // consumed).
            && verb.HitstopFrames <= 0
            && !FighterGrabRules.IsBusy(in verb)
            // A hanging fighter has no specials and no movement ability (§2.11)
            // — the hang suppresses ability intent the way hitstun does.
            && !FighterLedgeRules.IsHanging(in runtime)
            && !FighterUniversalMovementRules.IsCombatLocked(in runtime)
            // Story's Blocking state ignores ability inputs entirely; specials
            // remain usable mid-swing because they cancel the basic string
            // (design 1051), once the swing's own ability lockout allows them.
            && !FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verb);

        /// <summary>A swing start: arms A and clears the landed latch C (and any freeze buffer).</summary>
        public static void ArmSwing(ref FighterRuntimeComponent runtime) {
            runtime.AbilityLockoutFrames = FTT.Combat.BasicComboRules.AbilityLockoutFrames;
            runtime.SwingLanded = 0;
            runtime.FreezeBufferArmed = 0;
            runtime.FreezeBufferedSpecials = 0;
        }

        /// <summary>A Special 1 / 2 or Ultimate acceptance: arms A and S.</summary>
        public static void ArmAbility(ref FighterRuntimeComponent runtime) {
            runtime.AbilityLockoutFrames = FTT.Combat.BasicComboRules.AbilityLockoutFrames;
            runtime.SpecialLockoutFrames = FTT.Combat.BasicComboRules.AbilityLockoutFrames;
        }

        /// <summary>The current swing landed (D1a): true on the 0 → 1 edge only (once per swing).</summary>
        public static bool NoteSwingLanded(ref FighterRuntimeComponent runtime) {
            if (runtime.SwingLanded != 0) return false;
            runtime.SwingLanded = 1;
            return true;
        }

        /// <summary>One unfrozen tick of A and S (TickCounters).</summary>
        public static void Tick(ref FighterRuntimeComponent runtime) {
            runtime.AbilityLockoutFrames = FTT.Combat.BasicComboRules.TickAbilityLockout(runtime.AbilityLockoutFrames);
            runtime.SpecialLockoutFrames = FTT.Combat.BasicComboRules.TickAbilityLockout(runtime.SpecialLockoutFrames);
        }

        /// <summary>The F22 Sudden Death reset: A, S, C and the freeze buffer to zero.</summary>
        public static void Clear(ref FighterRuntimeComponent runtime) {
            runtime.AbilityLockoutFrames = 0;
            runtime.SpecialLockoutFrames = 0;
            runtime.SwingLanded = 0;
            runtime.FreezeBufferArmed = 0;
            runtime.FreezeBufferedSpecials = 0;
        }

        // === The freeze-only Special buffer (user ruling 2026-10-11, addendum) ===
        // A Special 1/2 press read on a frozen tick of the attacker freeze of the
        // fighter's own LANDED basic swing is held (one deep, the latest press)
        // and joins the presses of the fighter's thaw tick — the tick the
        // movement system takes HitstopFrames 1 → 0, whose presses the ability
        // and combat systems read — where the ordinary gates decide it. A tick
        // that also presses the Ultimate buffers nothing. Ledger
        // PROVISIONAL-FREEZE-SPECIAL-BUFFER.

        /// <summary>Called by ApplyBasicSwing when its landed hit froze the attacker.</summary>
        public static void ArmFreezeBuffer(ref FighterRuntimeComponent runtime) {
            runtime.FreezeBufferArmed = 1;
            runtime.FreezeBufferedSpecials = 0;
        }

        /// <summary>A frozen tick (before the decrement): hold this tick's Special press, if any.</summary>
        public static void BufferFreezePress(ref FighterRuntimeComponent runtime) {
            if (runtime.FreezeBufferArmed == 0) return;
            if ((runtime.PressedButtons & UltimateButton) != 0) return;
            int specials = ((runtime.PressedButtons & SpecialOneButton) != 0 ? 1 : 0)
                | ((runtime.PressedButtons & SpecialTwoButton) != 0 ? 2 : 0);
            if (specials != 0) runtime.FreezeBufferedSpecials = specials;
        }

        /// <summary>The thaw tick (HitstopFrames reached 0): re-present the held press, then empty the buffer.</summary>
        public static void ReleaseFreezeBuffer(ref FighterRuntimeComponent runtime) {
            int specials = runtime.FreezeBufferedSpecials;
            if ((specials & 1) != 0) runtime.PressedButtons |= SpecialOneButton;
            if ((specials & 2) != 0) runtime.PressedButtons |= SpecialTwoButton;
            runtime.FreezeBufferArmed = 0;
            runtime.FreezeBufferedSpecials = 0;
        }
    }
}
