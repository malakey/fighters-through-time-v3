using FTT.Core;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// Deterministic ledge grab (gameplay-feel plan §2.11), the sim's first slice
    /// of audit M-16. Only one-way platform ends are grabbable: a stage's base
    /// floor spans wall to wall and its side walls are solid, so there is no other
    /// edge to catch. The whole feature is three snapshotted ints in
    /// <see cref="FighterRuntimeComponent"/> — the hang position is re-derived from
    /// the stage geometry every tick, since geometry is constant for the match and
    /// never enters a snapshot.
    /// </summary>
    internal static class FighterLedgeRules {
        /// <summary><see cref="FighterRuntimeComponent.LedgeAnchor"/> for "not hanging".</summary>
        public const int NoAnchor = -1;
        /// <summary>Hang ends on its own after five seconds, with the regrab lockout.</summary>
        public const int AutoReleaseFrames = 300;
        /// <summary>Half a second of no regrab after a Down release or an auto-release.</summary>
        public const int RegrabLockoutFrames = 30;

        /// <summary>Capture box half-width around the platform end.</summary>
        public static readonly FP64 CaptureHalfWidth = FP64.FromDouble(0.5);
        /// <summary>Capture box depth below the platform surface.</summary>
        public static readonly FP64 CaptureDepth = FP64.FromDouble(1.2);
        /// <summary>How far outside the platform the hang position sits.</summary>
        public static readonly FP64 HangOutwardOffset = FP64.FromDouble(0.25);
        /// <summary>How far below the surface the hang position sits.</summary>
        public static readonly FP64 HangDepth = FP64.One;
        /// <summary>
        /// Vertical-speed ceiling for a grab. Positive so a fighter still rising
        /// slowly near the apex catches the ledge they jumped up to; anything
        /// faster than this is a jump that clears the edge, not a grab.
        /// </summary>
        public static readonly FP64 MaxGrabVerticalSpeed = FP64.FromInt(2);
        /// <summary>The climb jump is nine-tenths of a ground jump.</summary>
        public static readonly FP64 ClimbJumpMultiplier = FP64.FromDouble(0.9);

        private const int JumpButton = 1 << 0;
        private const int DownButton = 1 << 1;
        private const int BasicButton = 1 << 2;
        private const int BlockButton = 1 << 6;
        private const int RollButton = 1 << 10;
        private const int EchoStepButton = 1 << 12;
        private const int GrabButton = 1 << 13;

        // === CM-MOVE: the Fighter ledge get-ups (decision 5.2, 2026-10-16; plan
        // §2.5.2; ledger PROVISIONAL-LEDGE-GETUP-FRAMES). The frame numbers live
        // once in FTT.Combat.BasicComboRules (the ledge section); the climbs run in
        // component 305's universal-movement slot as values 8–10 (plan §1.5), with
        // the anchor packed in UniversalMovementDirection — no new bytes. ===

        /// <summary>
        /// The quantized stick a ledge tilt needs: |MoveX| or −MoveY above 30 of
        /// 127 — the sim's "direction held" everywhere else (the air dodge, the
        /// knockdown get-up, the Up-Attack's <c>MoveY &lt; −30</c>).
        /// </summary>
        public const int TiltThreshold = 30;

        /// <summary>What one hang tick did (<see cref="Process(ref FighterStateComponent, ref FighterRuntimeComponent, ref FighterVerbComponent, ref FighterDefenseComponent, ref FighterActionStateComponent, in FighterTuningComponent, in FPVector2, in FPVector2)"/>).</summary>
        public enum HangTick {
            /// <summary>Still hanging.</summary>
            Hanging = 0,
            /// <summary>Jump: the climb jump (0.9× a ground jump).</summary>
            ClimbJumped = 1,
            /// <summary>Down held or the five-second auto-release: a fall with the regrab lockout.</summary>
            Released = 2,
            /// <summary>A get-up began: the neutral climb, the ledge roll or the ledge attack.</summary>
            GotUp = 3
        }

        /// <summary>What one get-up tick did (<see cref="AdvanceGetUp"/>).</summary>
        public enum GetUpTick {
            /// <summary>Still climbing (or standing out the neutral climb): the movement system ends the tick.</summary>
            Climbing = 0,
            /// <summary>The climb ended and the fighter acts normally from this tick (the neutral climb's end, the ledge roll's first roll tick).</summary>
            Finished = 1,
            /// <summary>The climb ended into the ledge strike, whose startup begins on this tick: the movement system ends the tick.</summary>
            StrikeStarted = 2
        }

        public static bool IsHanging(in FighterRuntimeComponent runtime) => runtime.LedgeAnchor >= 0;

        /// <summary>CM-MOVE: one of the three ledge climbs (universal-movement values 8–10) is running.</summary>
        public static bool IsGetUpClimb(in FighterRuntimeComponent runtime) => IsGetUpClimb(runtime.UniversalMovementState);

        /// <summary>CM-MOVE: <paramref name="universalMovementState"/> is a ledge climb (8–10).</summary>
        public static bool IsGetUpClimb(int universalMovementState) =>
            universalMovementState is (int)UniversalMovementPhase.LedgeClimb
                or (int)UniversalMovementPhase.LedgeRoll
                or (int)UniversalMovementPhase.LedgeAttackClimb;

        /// <summary>Total climb ticks of a get-up phase (the fighter acts on the next tick).</summary>
        public static int ClimbFramesFor(int phase) =>
            phase == (int)UniversalMovementPhase.LedgeClimb
                ? FTT.Combat.BasicComboRules.LedgeNeutralClimbFrames
                : FTT.Combat.BasicComboRules.LedgeActionClimbFrames;

        /// <summary>Climb ticks over which the fighter moves from the hang to the stand position.</summary>
        public static int ClimbMoveFramesFor(int phase) =>
            phase == (int)UniversalMovementPhase.LedgeClimb
                ? FTT.Combat.BasicComboRules.LedgeNeutralClimbIntangibleFrames
                : FTT.Combat.BasicComboRules.LedgeActionClimbFrames;

        /// <summary>
        /// The action-chokepoint kind a get-up reports at its press (plan
        /// §2.5.2): the ledge attack is a <see cref="FighterActionKind.Swing"/> —
        /// offensive, so it ends respawn and Defy protection — and the neutral
        /// climb and the ledge roll are <see cref="FighterActionKind.LedgeGetUp"/>.
        /// </summary>
        public static FighterActionKind GetUpActionKind(int phase) =>
            phase == (int)UniversalMovementPhase.LedgeAttackClimb ? FighterActionKind.Swing : FighterActionKind.LedgeGetUp;

        /// <summary>Intangible frames a get-up grants from its first tick.</summary>
        public static int ClimbIntangibleFramesFor(int phase) =>
            phase == (int)UniversalMovementPhase.LedgeClimb
                ? FTT.Combat.BasicComboRules.LedgeNeutralClimbIntangibleFrames
                : FTT.Combat.BasicComboRules.LedgeActionClimbFrames;

        public static void ClearHang(ref FighterRuntimeComponent runtime) {
            runtime.LedgeAnchor = NoAnchor;
            runtime.LedgeStateFrames = 0;
        }

        /// <summary>
        /// The fighter-state half of the grab test. Geometry is the caller's job
        /// (<see cref="FighterStageGeometry.TryFindLedge"/>) so the two halves stay
        /// independently testable. V7.3 regrab cap: only
        /// <see cref="FTT.Combat.BasicComboRules.LedgeRegrabsPerAirtime"/> grabs
        /// per airtime — the next is refused until grounding (or a stock loss)
        /// resets the budget.
        /// </summary>
        public static bool CanGrab(
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            in FighterVerbComponent verb) =>
            fighter.Stocks > 0
            && fighter.IsGrounded == 0
            && fighter.RespawnFramesRemaining <= 0
            && fighter.DropThroughFrames == 0
            && fighter.HitstunFrames <= 0
            && fighter.DazeFrames <= 0
            && runtime.LedgeRegrabLockoutFrames <= 0
            && verb.LedgeGrabsThisAirtime < FTT.Combat.BasicComboRules.LedgeRegrabsPerAirtime
            && !IsHanging(in runtime)
            && fighter.Velocity.y <= MaxGrabVerticalSpeed;

        /// <summary>
        /// Latches the hang: pinned position, zeroed velocity, refilled jumps, and
        /// a cancelled swing/roll. Gravity is skipped for as long as the anchor is
        /// set, so nothing else has to know about the state.
        /// </summary>
        public static void Grab(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            in FighterTuningComponent tuning,
            int anchor,
            in FPVector2 hangPosition) {
            // CM-MOVE (decision 5.2): the FIRST grab of an airtime is intangible
            // for 30 frames, max-assigned so a longer grant survives. A regrab
            // gets nothing — the budget resets only on grounding or a stock loss,
            // which is what keeps the grant from feeding a planking loop.
            if (verb.LedgeGrabsThisAirtime == 0
                && fighter.InvulnerabilityFrames < FTT.Combat.BasicComboRules.LedgeFirstGrabIntangibleFrames) {
                fighter.InvulnerabilityFrames = FTT.Combat.BasicComboRules.LedgeFirstGrabIntangibleFrames;
            }
            // V7.3 regrab budget: every latched grab spends one of the
            // per-airtime allowance CanGrab enforces.
            verb.LedgeGrabsThisAirtime++;
            runtime.LedgeAnchor = anchor;
            runtime.LedgeStateFrames = 0;
            fighter.Position = hangPosition;
            fighter.Velocity = FPVector2.Zero;
            // Jumps refill on the grab, which is what makes the climb jump the
            // ground-jump equivalent rather than an air jump.
            fighter.RemainingJumps = tuning.MaxJumpCount;
            // The fighter faces the stage they are about to climb onto: side 0 is
            // the platform's left edge, so the platform is to their right.
            fighter.FacingRight = anchor % 2 == 0 ? 1 : 0;
            FighterUniversalMovementRules.Cancel(ref runtime);
            FighterBasicAttackRules.CancelString(ref runtime);
            runtime.FloatFrames = 0;
        }

        /// <summary>
        /// Advances one hang tick on hand-built components with no action state
        /// (the rulebook suites). Returns true while the fighter is still
        /// hanging, which is the movement system's signal to skip gravity,
        /// movement, and the attack phase machine entirely — the same
        /// short-circuit hitstun gets. The get-ups need component 323 (the stick
        /// re-arm) and the chokepoint's components, so this form only sees the
        /// presses: it never neutral-climbs.
        /// </summary>
        public static bool Process(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning,
            in FPVector2 hangPosition) {
            FighterVerbComponent verb = default;
            FighterDefenseComponent defense = default;
            FighterActionStateComponent action = default;
            return Process(
                ref fighter, ref runtime, ref verb, ref defense, ref action,
                in tuning, in hangPosition, in hangPosition) == HangTick.Hanging;
        }

        /// <summary>
        /// Advances one hang tick (gameplay-feel plan §2.11; CM-MOVE decision
        /// 5.2, 2026-10-16). One action per tick, in this priority order: a
        /// <b>Jump</b> press climb-jumps; a <b>Roll</b> press (not with Block
        /// held, never the Echo Step request) starts the ledge roll; a
        /// <b>BasicAttack</b> press (not with Block held, never the grab request)
        /// starts the ledge attack; <b>Down</b> held releases; and a <b>fresh</b>
        /// toward-stage or Up tilt neutral-climbs. "Fresh" is component 323's
        /// <see cref="FighterActionStateComponent.ActionFlagLedgeStickRearmed"/>:
        /// cleared while the fighter is not hanging, set on any hang tick whose
        /// stick is neither toward the stage nor up, and read only once the hang
        /// is <see cref="FTT.Combat.BasicComboRules.LedgeGetUpMinHangFrames"/>
        /// old — so a fighter still steering in from its recovery never climbs on
        /// its first hang tick, and the CPU's climb jump (Jump with a neutral
        /// stick) always wins its tick. Each get-up calls the action chokepoint
        /// FIRST and then max-assigns its own climb intangibility, so a respawn or
        /// Defy protection the chokepoint ends never takes the climb frames with
        /// it. The get-up's first climb tick runs here.
        /// </summary>
        public static HangTick Process(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            ref FighterDefenseComponent defense,
            ref FighterActionStateComponent action,
            in FighterTuningComponent tuning,
            in FPVector2 hangPosition,
            in FPVector2 standPosition) {
            fighter.Position = hangPosition;
            fighter.Velocity = FPVector2.Zero;
            fighter.IsGrounded = 0;
            int pressed = runtime.PressedButtons;
            bool blockHeld = (runtime.HeldButtons & BlockButton) != 0;

            // Jump climbs. It does not spend a jump: the grab refilled them and the
            // climb is the ground-jump equivalent, so the fighter leaves with the
            // same budget a jump off the floor would have left. CM-MOVE (Q5): the
            // climb jump is the fighter's own jump, so a released Jump short-hops it.
            if ((pressed & JumpButton) != 0) {
                ClearHang(ref runtime);
                fighter.Velocity.y = tuning.JumpSpeed * ClimbJumpMultiplier;
                fighter.RemainingJumps = tuning.MaxJumpCount - 1;
                action.ActionFlags |= FighterActionStateComponent.ActionFlagRiseFromOwnJump;
                FighterActionRules.OnActionAccepted(
                    ref fighter, ref verb, ref defense, ref action, FighterActionKind.Jump);
                return HangTick.ClimbJumped;
            }

            if ((pressed & RollButton) != 0 && !blockHeld && (pressed & EchoStepButton) == 0) {
                StartGetUp(ref fighter, ref runtime, ref verb, ref defense, ref action,
                    (int)UniversalMovementPhase.LedgeRoll, in hangPosition, in standPosition);
                return HangTick.GotUp;
            }
            if ((pressed & BasicButton) != 0 && !blockHeld && (pressed & GrabButton) == 0) {
                // The ledge attack is an offensive action from its press: it ends
                // respawn and Defy protection (CM-HIT's chokepoint body).
                StartGetUp(ref fighter, ref runtime, ref verb, ref defense, ref action,
                    (int)UniversalMovementPhase.LedgeAttackClimb, in hangPosition, in standPosition);
                return HangTick.GotUp;
            }

            // Down drops off. Fast-fall (§2.9) never fights this: release wins, and
            // because Down is still held the fighter fast-falls immediately after,
            // which is the intended "drop fast off the ledge" input.
            if ((runtime.HeldButtons & DownButton) != 0) {
                Release(ref fighter, ref runtime);
                return HangTick.Released;
            }

            if (IsClimbTilt(runtime.LedgeAnchor, runtime.MoveX, runtime.MoveY)) {
                if (runtime.LedgeStateFrames >= FTT.Combat.BasicComboRules.LedgeGetUpMinHangFrames
                    && (action.ActionFlags & FighterActionStateComponent.ActionFlagLedgeStickRearmed) != 0) {
                    StartGetUp(ref fighter, ref runtime, ref verb, ref defense, ref action,
                        (int)UniversalMovementPhase.LedgeClimb, in hangPosition, in standPosition);
                    return HangTick.GotUp;
                }
            } else {
                action.ActionFlags |= FighterActionStateComponent.ActionFlagLedgeStickRearmed;
            }

            runtime.LedgeStateFrames++;
            if (runtime.LedgeStateFrames >= AutoReleaseFrames) {
                Release(ref fighter, ref runtime);
                return HangTick.Released;
            }
            return HangTick.Hanging;
        }

        /// <summary>
        /// The neutral-climb stick: toward the stage (the platform is to the right
        /// of a side-0 anchor) or Up, past <see cref="TiltThreshold"/>.
        /// </summary>
        public static bool IsClimbTilt(int anchor, int moveX, int moveY) {
            bool towardStage = anchor % 2 == 0 ? moveX > TiltThreshold : moveX < -TiltThreshold;
            return towardStage || moveY < -TiltThreshold;
        }

        private static void StartGetUp(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            ref FighterDefenseComponent defense,
            ref FighterActionStateComponent action,
            int phase,
            in FPVector2 hangPosition,
            in FPVector2 standPosition) {
            int anchor = runtime.LedgeAnchor;
            // The hang ends now, so the ledge trump, the regrab budget and the hit
            // pipeline see an ordinary fighter; the anchor rides in the slot.
            ClearHang(ref runtime);
            FighterUniversalMovementRules.Cancel(ref runtime);
            runtime.UniversalMovementState = phase;
            runtime.UniversalMovementFramesRemaining = ClimbFramesFor(phase);
            runtime.UniversalMovementDirection = anchor;
            FighterActionRules.OnActionAccepted(ref fighter, ref verb, ref defense, ref action, GetUpActionKind(phase));
            int intangible = ClimbIntangibleFramesFor(phase);
            if (fighter.InvulnerabilityFrames < intangible) fighter.InvulnerabilityFrames = intangible;
            StepClimb(ref fighter, ref runtime, in hangPosition, in standPosition);
        }

        /// <summary>
        /// One climb tick: the frame count runs down and the fighter moves along
        /// the straight line from the hang to the stand position over the phase's
        /// move frames, then stands there (the neutral climb's vulnerable end).
        /// Integer frame arithmetic on FP64 positions; no velocity, no gravity.
        /// </summary>
        private static void StepClimb(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FPVector2 hangPosition,
            in FPVector2 standPosition) {
            int phase = runtime.UniversalMovementState;
            runtime.UniversalMovementFramesRemaining--;
            int elapsed = ClimbFramesFor(phase) - runtime.UniversalMovementFramesRemaining;
            int moveFrames = ClimbMoveFramesFor(phase);
            fighter.Velocity = FPVector2.Zero;
            if (elapsed >= moveFrames) {
                fighter.Position = standPosition;
                fighter.IsGrounded = 1;
                return;
            }
            FP64 t = FP64.FromInt(elapsed) / FP64.FromInt(moveFrames);
            fighter.Position = new FPVector2(
                hangPosition.x + (standPosition.x - hangPosition.x) * t,
                hangPosition.y + (standPosition.y - hangPosition.y) * t);
            fighter.IsGrounded = 0;
        }

        /// <summary>
        /// Advances a running ledge climb (universal-movement values 8–10) one
        /// tick. While frames remain the fighter climbs (<see cref="GetUpTick.Climbing"/>).
        /// On the tick after the last one the climb completes on the stand
        /// position with its jumps refilled: the neutral climb hands the tick
        /// back (<see cref="GetUpTick.Finished"/>); the ledge roll starts the
        /// ordinary grounded roll toward the stage and hands the tick back, so the
        /// roll's startup runs this tick exactly as a pressed roll's does; the
        /// ledge attack starts the ledge strike (<see cref="GetUpTick.StrikeStarted"/>),
        /// whose startup counts from this tick exactly as a pressed swing's does.
        /// A hit (hitstun or daze) is the caller's to check first: it cancels the
        /// climb (<see cref="FighterUniversalMovementRules.Cancel"/>).
        /// </summary>
        public static GetUpTick AdvanceGetUp(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning,
            in FPVector2 hangPosition,
            in FPVector2 standPosition) {
            if (runtime.UniversalMovementFramesRemaining > 0) {
                StepClimb(ref fighter, ref runtime, in hangPosition, in standPosition);
                return GetUpTick.Climbing;
            }
            int phase = runtime.UniversalMovementState;
            int anchor = runtime.UniversalMovementDirection;
            FighterUniversalMovementRules.Cancel(ref runtime);
            fighter.Position = standPosition;
            fighter.Velocity = FPVector2.Zero;
            fighter.IsGrounded = 1;
            fighter.RemainingJumps = tuning.MaxJumpCount;
            if (phase == (int)UniversalMovementPhase.LedgeRoll) {
                runtime.UniversalMovementState = (int)UniversalMovementPhase.RollStartup;
                runtime.UniversalMovementFramesRemaining = UniversalMovementRules.RollStartupFrames;
                // Toward the stage: the platform lies right of a side-0 anchor.
                runtime.UniversalMovementDirection = anchor % 2 == 0 ? 1 : -1;
                return GetUpTick.Finished;
            }
            if (phase == (int)UniversalMovementPhase.LedgeAttackClimb) {
                FighterBasicAttackRules.StartLedgeAttack(ref fighter, ref runtime);
                return GetUpTick.StrikeStarted;
            }
            return GetUpTick.Finished;
        }

        /// <summary>Drops off into a fall and arms the regrab lockout.</summary>
        public static void Release(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime) {
            ClearHang(ref runtime);
            runtime.LedgeRegrabLockoutFrames = RegrabLockoutFrames;
            fighter.Velocity = FPVector2.Zero;
            fighter.IsGrounded = 0;
        }
    }
}
