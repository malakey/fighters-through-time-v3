namespace FTT.FighterSim {

    /// <summary>
    /// CM-BLOCK (combat and movement update 2026-10-16, decision 3.4, plan
    /// §2.3.2): the Fighter perfect-block clock, kept in component 323's
    /// <see cref="FighterActionStateComponent.BlockTiming"/> int. Integer state
    /// and integer rules only; the shared numbers and the arming / applying
    /// predicates live once in <c>FTT.Combat.BasicComboRules</c>, which Story's
    /// <c>PlayerController.PerfectBlock</c> reads too.
    ///
    /// <para><b>The bits.</b></para>
    /// <list type="bullet">
    /// <item>0–7: perfect-block frames left (the window). Set to
    /// <c>PerfectBlockWindowFrames</c> (5) on the tick the window arms, so a
    /// contact resolved that same tick is age 0; decremented once per unfrozen
    /// tick (pausing in the fighter's own hitstop), so contact ages 0–4 are
    /// perfect; cleared when the stance drops.</item>
    /// <item>8–15: frames since the stance last dropped — 0 = never raised this
    /// stock, otherwise 1..255 saturating. The anti-mash rearm reads it.</item>
    /// <item>16: the stance was up at the last observation.</item>
    /// <item>17: this tick has been observed (set by <see cref="Observe"/> only
    /// while the fighter has any block history, so a fighter who never blocks
    /// keeps the whole field at zero). A tick that ends unobserved — hitstun,
    /// daze, the knockdown, the tech lock, a grab, a capture, a ledge hang — is
    /// finalized by the next <see cref="BeginTick"/> as a lowered tick.</item>
    /// <item>18: this tick began in hitstun that outlives this tick's
    /// decrement, so the only way the stance can come up this tick is the
    /// hit-2 Block escape — which never arms the window.</item>
    /// <item>20–23: perfect blocks resolved, cycling 1..15 (0 = none this
    /// stock, so a reset to 0 is never mistaken for a block). Written by the
    /// block branch of <c>FighterDamageRules.ResolveFighterHit</c>, read by the
    /// presentation flash and the Calibration Drill — never by a rule.</item>
    /// </list>
    ///
    /// <para><b>Arming.</b> The window arms only on a tick where all three hold
    /// (<c>BasicComboRules.PerfectBlockArms</c>): the stance came up (down at the
    /// last observation, up now), Block is pressed this tick, and the stance
    /// had been down at least 20 frames or never raised. So a stance held up
    /// through a swing's or Special's recovery, a stance locked up by
    /// shieldstun and a stance raised by the hit-2 escape never arm, and a
    /// hand-built <c>Pressed = Held = Block</c> stream arms once, on its first
    /// stance tick. Component 323 is zeroed on the respawn platform, so a fresh
    /// stock is eligible at once.</para>
    ///
    /// <para><b>Calls.</b> <see cref="BeginTick"/> once per unfrozen tick from
    /// <c>FighterMovementSystem.Update</c>, right after the Special cast clock,
    /// before any branch can end the tick; <see cref="Observe"/> once on the
    /// normal-control path, right after the block stance is computed.</para>
    /// </summary>
    public static class FighterBlockTimingRules {
        public const int WindowMask = 0xFF;
        public const int LoweredShift = 8;
        public const int LoweredMask = 0xFF << LoweredShift;
        public const int LoweredMaxFrames = 255;
        public const int StanceUpBit = 1 << 16;
        public const int ObservedBit = 1 << 17;
        public const int EscapeTickBit = 1 << 18;
        public const int PerfectCountShift = 20;
        public const int PerfectCountMask = 0xF << PerfectCountShift;

        /// <summary>Perfect-block frames left; positive means the window is open.</summary>
        public static int WindowFrames(in FighterActionStateComponent action) => action.BlockTiming & WindowMask;

        /// <summary>Frames since the stance last dropped (0 = never raised this stock).</summary>
        public static int LoweredFrames(in FighterActionStateComponent action) =>
            (action.BlockTiming & LoweredMask) >> LoweredShift;

        /// <summary>True while a contact the ordinary block absorbs would be perfect-blocked.</summary>
        public static bool IsPerfectBlockOpen(in FighterActionStateComponent action) => WindowFrames(in action) > 0;

        /// <summary>The perfect blocks resolved this stock, cycling 1..15 (0 = none); presentation and the drill read it.</summary>
        public static int PerfectBlockCount(in FighterActionStateComponent action) =>
            (action.BlockTiming & PerfectCountMask) >> PerfectCountShift;

        /// <summary>The block branch's record of one perfect-blocked contact.</summary>
        public static void NotePerfectBlock(ref FighterActionStateComponent action) {
            int count = PerfectBlockCount(in action) % 15 + 1;
            action.BlockTiming = (action.BlockTiming & ~PerfectCountMask) | (count << PerfectCountShift);
        }

        /// <summary>
        /// The start of one unfrozen tick. A tick that ended unobserved is
        /// finalized as lowered (the stance drops if it was up; otherwise the
        /// lowered age grows), the window counts down, and the escape-tick bit
        /// is set when hitstun outlives this tick's decrement. Nothing happens
        /// while the respawn platform holds the fighter (323 is held at zero).
        /// </summary>
        public static void BeginTick(ref FighterActionStateComponent action, in FighterStateComponent fighter) {
            if (FighterMatchFlowRules.IsOnRespawnPlatform(in fighter)) return;
            int timing = action.BlockTiming;
            if ((timing & ObservedBit) == 0) timing = LowerOneTick(timing);
            if ((timing & WindowMask) > 0) timing--;
            timing &= ~(ObservedBit | EscapeTickBit);
            if (fighter.HitstunFrames > 1) timing |= EscapeTickBit;
            action.BlockTiming = timing;
        }

        /// <summary>
        /// The normal-control tick's observation of the block stance, right after
        /// it is computed. A rising edge arms the window under
        /// <c>BasicComboRules.PerfectBlockArms</c> (never on the escape tick); a
        /// falling edge clears it and restarts the lowered age.
        /// </summary>
        public static void Observe(ref FighterActionStateComponent action, bool stanceUp, bool blockPressed) {
            int timing = action.BlockTiming;
            if (stanceUp) {
                if ((timing & StanceUpBit) == 0) {
                    int lowered = (timing & LoweredMask) >> LoweredShift;
                    bool escapeTick = (timing & EscapeTickBit) != 0;
                    if (!escapeTick && FTT.Combat.BasicComboRules.PerfectBlockArms(true, blockPressed, lowered)) {
                        timing = (timing & ~WindowMask) | FTT.Combat.BasicComboRules.PerfectBlockWindowFrames;
                    }
                    timing |= StanceUpBit;
                }
            } else {
                timing = LowerOneTick(timing);
            }
            timing &= ~EscapeTickBit;
            bool history = (timing & (WindowMask | LoweredMask | StanceUpBit)) != 0;
            timing = history ? timing | ObservedBit : timing & ~ObservedBit;
            action.BlockTiming = timing;
        }

        /// <summary>One lowered tick: a drop if the stance was up, else the age grows (saturating; 0 stays "never raised").</summary>
        private static int LowerOneTick(int timing) {
            if ((timing & StanceUpBit) != 0) {
                timing &= ~(StanceUpBit | WindowMask | LoweredMask);
                return timing | (1 << LoweredShift);
            }
            int lowered = (timing & LoweredMask) >> LoweredShift;
            if (lowered > 0 && lowered < LoweredMaxFrames) {
                timing = (timing & ~LoweredMask) | ((lowered + 1) << LoweredShift);
            }
            return timing;
        }
    }
}
