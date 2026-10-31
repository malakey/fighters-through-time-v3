namespace FTT.FighterSim {

    /// <summary>
    /// D3 in Fighter (combat and movement update 2026-10-16, decision 11.1,
    /// plan §2.11.1; workstream CM-CANCEL): the on-hit Special recovery cancel,
    /// one rule in both modes. A Special whose execution LANDED a direct hit
    /// cancels its recovery into a roll (the air dodge when airborne) or a jump
    /// from recovery frame 0, and into a basic attack — hit 1, the Up-Attack or
    /// the Down-Air by the direction held at the press — or the movement ability
    /// from recovery frame 6. A whiff keeps its whole recovery, whose only exit is
    /// the grounded Block cancel. The frame table and the buffer length are
    /// <see cref="FTT.Combat.BasicComboRules"/>'s D3 section, the one statement
    /// Story's forwarders read too.
    ///
    /// <para><b>State.</b> Component 323 only: <see cref="FighterSpecialCastRules.FlagLandedDirectHit"/>
    /// in <see cref="FighterActionStateComponent.SpecialFlags"/> (bit 8) and the
    /// one-slot cancel buffer <see cref="FighterActionStateComponent.SpecialCancelBuffer"/>
    /// — bits 0–2 the verb (0 none, 1 roll, 2 jump, 3 basic, 4 movement), bits
    /// 3–6 its age in unfrozen ticks, bit 7 Up held at the press, bit 8 Down held
    /// at the press. No new component, no protocol field, no save field. Both
    /// fields are meaningful only while the cast is busy: the cast's
    /// <c>Clear</c> zeroes the flags, and the buffer is zeroed on the first
    /// <see cref="Feed"/> after the cast ends (and by every cancel).</para>
    ///
    /// <para><b>Arming</b> (<see cref="NoteLanded"/>, called by the hit sites —
    /// the Divine Piercing thrust, a Special projectile or ground-wave contact, a
    /// Special zone pulse, the projectile burst, the Lorentz chain arcs, the
    /// Foresight answer and a melee Special intent): the contact resolved
    /// <c>Landed</c> or <c>Absorbed</c> (D1a) for positive authored damage, its
    /// slot is the running cast's slot, the cast is busy, and the slot's projected
    /// contract is a <c>DirectHit</c> delivery. So Golden Ratio, the Lorentz Pulse
    /// and the Requiem burst — zone pulses Story authors as direct hits — arm it,
    /// and the Rift, the Vortex, the Tempest, constructs and Cleopatra never do.
    /// A projectile cannot arm a later cast of its own slot, because no Special
    /// projectile or zone outlives its slot's cooldown less the 120-frame
    /// Momentum cap (pinned by a test rather than by growing components 302/309).</para>
    ///
    /// <para><b>The buffer</b> (<see cref="Feed"/>): fed on every tick of a busy
    /// cast — its hitstop ticks too, through the movement system's hitstop
    /// branch, which ages nothing — with the first verb pressed in the order
    /// roll, jump, basic, movement; the latest press replaces the slot, and a
    /// press older than <see cref="FTT.Combat.BasicComboRules.SpecialCancelBufferFrames"/>
    /// unfrozen ticks is dropped. Never buffered: the grab chord's BasicAttack, and a
    /// Movement press on a tick that presses the Ultimate (the LB+RB chord), which
    /// also clears a buffered Movement press.</para>
    ///
    /// <para><b>Firing</b> happens in the movement system's cast region, after the
    /// grounded Block cancel (which wins its tick and drops the rest), when the
    /// cast landed, the press is still buffered, the verb's frame gate is open and
    /// the verb can start now; a verb that cannot start yet (no air jump, the air
    /// dodge spent, Root, a cooldown) stays buffered until it ages out, as in
    /// Story. Firing rewrites the tick's input to the verb alone
    /// (<see cref="RewriteInput"/>) and ends the cast: the verb then starts through
    /// its ordinary path — the roll or air dodge, the jump, the
    /// <c>SelectAttackVariant</c> swing, the movement ability — each passing the
    /// action chokepoint. The cooldown, the ability lockout and whatever the cast
    /// put into the world stand. Fixed-point free: integer state only.</para>
    /// </summary>
    public static class FighterSpecialCancelRules {
        public const int VerbNone = 0;
        public const int VerbRoll = 1;
        public const int VerbJump = 2;
        public const int VerbBasic = 3;
        public const int VerbMovement = 4;

        private const int VerbMask = 0x7;
        private const int AgeShift = 3;
        private const int AgeMask = 0xF;
        /// <summary><see cref="FighterActionStateComponent.SpecialCancelBuffer"/> bit 7: Up was held at the press.</summary>
        public const int UpHeldBit = 1 << 7;
        /// <summary><see cref="FighterActionStateComponent.SpecialCancelBuffer"/> bit 8: Down was held at the press.</summary>
        public const int DownHeldBit = 1 << 8;

        private const int JumpButton = 1 << 0;
        private const int DownButton = 1 << 1;
        private const int BasicButton = 1 << 2;
        private const int MovementButton = 1 << 5;
        private const int BlockButton = 1 << 6;
        private const int UltimateButton = 1 << 7;
        private const int RollButton = 1 << 10;
        private const int GrabButton = 1 << 13;
        private const int DirectOriginButton = 1 << 14;

        /// <summary>
        /// The sim's "Up held" stick read (quantized <c>MoveY</c> below −30), the
        /// one <c>FighterBasicAttackRules.SelectVariant</c> uses.
        /// </summary>
        private const int UpHeldMoveY = -30;

        // --- reads ------------------------------------------------------------

        /// <summary>The buffered verb (<see cref="VerbNone"/> … <see cref="VerbMovement"/>).</summary>
        public static int BufferedVerb(in FighterActionStateComponent cast) => cast.SpecialCancelBuffer & VerbMask;

        /// <summary>The buffered press's age in the fighter's unfrozen ticks.</summary>
        public static int BufferedAge(in FighterActionStateComponent cast) =>
            (cast.SpecialCancelBuffer >> AgeShift) & AgeMask;

        public static bool BufferedUpHeld(in FighterActionStateComponent cast) => (cast.SpecialCancelBuffer & UpHeldBit) != 0;

        public static bool BufferedDownHeld(in FighterActionStateComponent cast) => (cast.SpecialCancelBuffer & DownHeldBit) != 0;

        /// <summary>The cast landed a direct hit this execution (D3 is armed).</summary>
        public static bool LandedDirectHit(in FighterActionStateComponent cast) =>
            FighterSpecialCastRules.HasFlag(in cast, FighterSpecialCastRules.FlagLandedDirectHit);

        /// <summary>The buffer word for one press.</summary>
        public static int Encode(int verb, int age, bool upHeld, bool downHeld) =>
            (verb & VerbMask)
            | ((age & AgeMask) << AgeShift)
            | (upHeld ? UpHeldBit : 0)
            | (downHeld ? DownHeldBit : 0);

        // --- arming -------------------------------------------------------------

        /// <summary>
        /// A hit site's report. Arms <see cref="FighterSpecialCastRules.FlagLandedDirectHit"/>
        /// when the contact <paramref name="outcome"/> is the D1a "landed"
        /// (<c>Landed</c> or <c>Absorbed</c>) for positive authored
        /// <paramref name="damage"/>, <paramref name="hitSlot"/> is the busy cast's
        /// slot, and the slot's <paramref name="contract"/> is a <c>DirectHit</c>
        /// delivery. Returns true when it armed (or the flag was already set).
        /// </summary>
        internal static bool NoteLanded(
            ref FighterActionStateComponent cast,
            int hitSlot,
            FighterHitOutcome outcome,
            int damage,
            in FighterAbilityHitData contract) =>
            NoteLanded(ref cast, hitSlot, outcome, damage, in contract, cast.SpecialCastId);

        /// <summary>
        /// The same report from a projectile, burst or zone pulse that outlives its cast
        /// (MM-FIXB2C, FIX-SIM-DIRECT-HIT-CAST-ATTRIBUTION): it arms the flag only when
        /// <paramref name="sourceCastId"/>, the cast identity stamped on the shot or zone when it spawned,
        /// is the cast in progress. An earlier cast's late hit therefore never arms a later cast's D3.
        /// </summary>
        internal static bool NoteLanded(
            ref FighterActionStateComponent cast,
            int hitSlot,
            FighterHitOutcome outcome,
            int damage,
            in FighterAbilityHitData contract,
            int sourceCastId) {
            if (sourceCastId != cast.SpecialCastId) return false;
            if (outcome != FighterHitOutcome.Landed && outcome != FighterHitOutcome.Absorbed) return false;
            if (damage <= 0) return false;
            if (!FighterSpecialCastRules.IsBusy(in cast) || cast.SpecialSlot != hitSlot) return false;
            if (contract.Delivery != (int)FTT.Combat.HitDelivery.DirectHit) return false;
            cast.SpecialFlags |= FighterSpecialCastRules.FlagLandedDirectHit;
            return true;
        }

        // --- the buffer ---------------------------------------------------------

        /// <summary>
        /// The verb this tick's presses ask for, in the order roll, jump, basic,
        /// movement — or <see cref="VerbNone"/>. The grab chord's BasicAttack (the
        /// logical Grab request, or Block held on the ground with the chords on) and
        /// a Movement press on an Ultimate tick never count.
        /// </summary>
        public static int SelectVerb(in FighterStateComponent fighter, in FighterRuntimeComponent runtime) {
            int pressed = runtime.PressedButtons;
            int held = runtime.HeldButtons;
            bool chordsOn = (held & DirectOriginButton) == 0;
            bool blockHeld = (held & BlockButton) != 0;
            if ((pressed & RollButton) != 0) return VerbRoll;
            if ((pressed & JumpButton) != 0) return VerbJump;
            bool grabRequested = (pressed & GrabButton) != 0 || (chordsOn && blockHeld && fighter.IsGrounded != 0);
            if ((pressed & BasicButton) != 0 && !grabRequested) return VerbBasic;
            if ((pressed & MovementButton) != 0 && (pressed & UltimateButton) == 0) return VerbMovement;
            return VerbNone;
        }

        /// <summary>
        /// Feeds the one-slot buffer from this tick's presses (Story's
        /// <c>BufferSpecialCancelPress</c>). Called once per tick by the movement
        /// system: on an unfrozen tick from the cast region (the held press ages
        /// first and expires past <see cref="FTT.Combat.BasicComboRules.SpecialCancelBufferFrames"/>),
        /// and on a hitstop tick (<paramref name="hitstopTick"/>) from the hitstop
        /// branch, before the busy caster's input is discarded, without aging. A
        /// tick whose Ultimate bit is pressed drops a buffered Movement press. Not
        /// busy: the buffer is zeroed.
        /// </summary>
        public static void Feed(
            ref FighterActionStateComponent cast,
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            bool hitstopTick) {
            if (!FighterSpecialCastRules.IsBusy(in cast)) {
                cast.SpecialCancelBuffer = 0;
                return;
            }
            int buffer = cast.SpecialCancelBuffer;
            if ((buffer & VerbMask) != VerbNone && !hitstopTick) {
                int age = ((buffer >> AgeShift) & AgeMask) + 1;
                buffer = FTT.Combat.BasicComboRules.SpecialCancelBufferHolds(age)
                    ? (buffer & ~(AgeMask << AgeShift)) | (age << AgeShift)
                    : 0;
            }
            if ((runtime.PressedButtons & UltimateButton) != 0 && (buffer & VerbMask) == VerbMovement) buffer = 0;
            int verb = SelectVerb(in fighter, in runtime);
            if (verb != VerbNone) {
                bool basic = verb == VerbBasic;
                buffer = Encode(
                    verb,
                    0,
                    upHeld: basic && runtime.MoveY < UpHeldMoveY,
                    downHeld: basic && (runtime.HeldButtons & DownButton) != 0);
            }
            cast.SpecialCancelBuffer = buffer;
        }

        // --- the gates ----------------------------------------------------------

        /// <summary>
        /// The cast's recovery and its 0-based recovery frame. A clocked cast's is
        /// <see cref="FighterSpecialCastRules.RecoveryFrame"/>. Tubman's Foresight
        /// (Delegated) recovers on component 322: its whiff recovery counts from
        /// the tick the window closed, its sidestep from the counter's trigger
        /// (<c>ForesightSidestepFrames − PhaseFrames</c>, so the first sidestep tick
        /// the movement system sees is frame 1, Story's earliest Foresight cancel).
        /// </summary>
        public static bool TryGetRecoveryFrame(
            in FighterActionStateComponent cast, in FighterCounterComponent counter, out int recoveryFrame) {
            recoveryFrame = -1;
            if (FighterSpecialCastRules.IsRecovery(in cast)) {
                recoveryFrame = FighterSpecialCastRules.RecoveryFrame(in cast);
                return true;
            }
            if (!FighterSpecialCastRules.IsDelegated(in cast)) return false;
            if (counter.Phase == FighterForesightRules.PhaseWhiffRecovery) {
                recoveryFrame = FTT.Combat.TubmanKitRules.ForesightWhiffRecoveryFrames - counter.PhaseFrames;
                return true;
            }
            if (counter.Phase == FighterForesightRules.PhaseSidestep) {
                recoveryFrame = FTT.Combat.TubmanKitRules.ForesightSidestepFrames - counter.PhaseFrames;
                return true;
            }
            return false;
        }

        /// <summary>The verb's D3 frame gate at <paramref name="recoveryFrame"/> (the cast landed).</summary>
        public static bool FrameGateOpen(int verb, int recoveryFrame) => verb switch {
            VerbRoll or VerbJump => FTT.Combat.BasicComboRules.SpecialRecoveryAllowsEvade(true, recoveryFrame),
            VerbBasic or VerbMovement => FTT.Combat.BasicComboRules.SpecialRecoveryAllowsAction(true, recoveryFrame),
            _ => false
        };

        // --- firing -------------------------------------------------------------

        /// <summary>
        /// Rewrites this tick's input to the fired verb alone: <c>PressedButtons</c>
        /// becomes the verb's bit (no Special, Ultimate or Grab request
        /// survives), the stick and the held buttons stay. For a basic, the stick's
        /// vertical and the held Down are set from the press-time direction
        /// (Up → up, Down → down, neither → neutral), so <c>SelectAttackVariant</c>
        /// picks the Up-Attack, the Down-Air or the neutral strike the player asked
        /// for and a direction held since never eats the press. For a jump, the
        /// held Down is dropped so a grounded cancel is a jump, never a
        /// drop-through (Story's cancel performs the jump).
        /// </summary>
        public static void RewriteInput(ref FighterRuntimeComponent runtime, int verb, bool upHeld, bool downHeld) {
            switch (verb) {
                case VerbRoll:
                    runtime.PressedButtons = RollButton;
                    break;
                case VerbJump:
                    runtime.PressedButtons = JumpButton;
                    runtime.HeldButtons &= ~DownButton;
                    break;
                case VerbBasic:
                    runtime.PressedButtons = BasicButton;
                    runtime.MoveY = upHeld ? -127 : downHeld ? 127 : 0;
                    runtime.HeldButtons = downHeld
                        ? runtime.HeldButtons | DownButton
                        : runtime.HeldButtons & ~DownButton;
                    break;
                case VerbMovement:
                    runtime.PressedButtons = MovementButton;
                    break;
            }
        }
    }
}
