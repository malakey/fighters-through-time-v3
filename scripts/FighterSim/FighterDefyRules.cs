using FTT.Combat;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// The active Defy History's state rules in the Fighter sim (meter and Defy update
    /// 2026-10-31, plan sections 2.4 to 2.6). The action lives on component 323
    /// (<see cref="FighterActionStateComponent.DefyFrames"/>, 0 idle, 1 to 30): frames
    /// 1 to 6 are fully invulnerable and unmoving, the blast and the heal resolve on
    /// frame 6 (<see cref="FighterDefyBlastRules"/>), frames 7 to 30 are a vulnerable,
    /// action-locked recovery, and the fighter is free again on frame 31. Fixed-point
    /// and integers only: the radius test is a squared distance, never a square root,
    /// and nothing here names a Story identifier. The numbers live once in
    /// <see cref="BasicComboRules"/>.
    /// </summary>
    public static class FighterDefyRules {

        /// <summary>Influence a Defy costs (50), exact in fixed point. Never scaled.</summary>
        public static readonly FP64 CostRaw = FP64.FromInt(BasicComboRules.DefyInfluenceCost);

        /// <summary>The blast radius squared (2.5 u, so 6.25), compared against dx squared plus dy squared.</summary>
        public static readonly FP64 RadiusSquaredRaw =
            FP64.FromDouble(BasicComboRules.DefyBlastRadiusUnits) * FP64.FromDouble(BasicComboRules.DefyBlastRadiusUnits);

        /// <summary>Horizontal push speed of the blast, units per second (away from the Defier).</summary>
        public static readonly FP64 PushXRaw = FP64.FromInt(BasicComboRules.DefyBlastPushXUnitsPerSecond);

        /// <summary>Vertical push speed of the blast, units per second (up).</summary>
        public static readonly FP64 PushYRaw = FP64.FromInt(BasicComboRules.DefyBlastPushYUnitsPerSecond);

        /// <summary>True while a Defy runs (frames 1 to 30).</summary>
        public static bool IsDefying(in FighterActionStateComponent action) => action.DefyFrames > 0;

        /// <summary>Frames 1 to 6 are invulnerable (the fighter's InvulnerabilityFrames carry the grant).</summary>
        public static bool IsInvulnerable(in FighterActionStateComponent action) =>
            action.DefyFrames > 0 && action.DefyFrames <= BasicComboRules.DefyInvulnerableFrames;

        /// <summary>The startup, frames 1 to 6: the fighter is held in place with no gravity.</summary>
        public static bool IsStartup(in FighterActionStateComponent action) => IsInvulnerable(in action);

        /// <summary>True on the tick the blast and the heal resolve: frame 6, not yet fired.</summary>
        public static bool BlastFiresThisTick(in FighterActionStateComponent action) =>
            action.DefyFrames == BasicComboRules.DefyBlastFrame
            && (action.ActionFlags & FighterActionStateComponent.ActionFlagDefyBlastFired) == 0;

        /// <summary>The presentation counter, bits 12 to 15 of <c>ActionFlags</c>, wrapping.</summary>
        public static int Counter(in FighterActionStateComponent action) =>
            (action.ActionFlags & FighterActionStateComponent.DefyCounterMask) >> FighterActionStateComponent.DefyCounterShift;

        /// <summary>
        /// Whether a Defy press can begin now (O9). Legal from every state the fighter can
        /// be in while alive; refused when it is dead, out of stocks, on the respawn
        /// platform, captured by an Ultimate hold, the owner of an Ultimate in any phase,
        /// already Defying, short of 50 Influence, or when the match is not live (the
        /// countdown). It ignores the ability lockouts, hitstun, daze, hitstop, grabs, the
        /// ledge, the block stance and the Special cast.
        /// </summary>
        public static bool CanBegin(
            in FighterStateComponent fighter,
            in FighterActionStateComponent action,
            in FighterUltimateActivationComponent activation,
            bool matchLive) =>
            fighter.CurrentHP > 0
            && fighter.RespawnFramesRemaining <= 0
            && CanBeginCore(
                matchLive, action.DefyFrames, fighter.Stocks, fighter.Influence.RawValue,
                FighterUltimateActivationRules.IsCaptured(in activation),
                FighterUltimateActivationRules.IsCasterBusy(in activation));

        /// <summary>
        /// The part of <see cref="CanBegin"/> the CPU can read off its observation: one predicate, so the
        /// CPU's legality cannot drift from the sim's (daze, hitstun and the rest are deliberately not read).
        /// </summary>
        public static bool CanBeginCore(
            bool matchLive, int defyFrames, int stocks, long influenceRaw, bool captured, bool casterBusy) =>
            matchLive
            && defyFrames == 0
            && stocks > 0
            && influenceRaw >= CostRaw.RawValue
            && !captured
            && !casterBusy;

        /// <summary>
        /// The fighter's own state change on acceptance: spends the Influence, grants the
        /// six invulnerable frames, zeroes the velocity and clears everything the press
        /// overrides (hitstun, daze, a pending launch, the tumble, shieldstun, the tech
        /// lock, the knockdown, Foresight's stance, the Special cast and its D3 buffer,
        /// universal movement and kit motion, the string, the ledge hang and the freeze
        /// buffer), then starts the clock at frame 1 (the presentation counter steps on the blast frame).
        /// Neither lockout is read or armed. The grab on either side is released by the
        /// caller, which can reach the partner.
        /// </summary>
        internal static void Begin(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            ref FighterActionStateComponent action,
            ref FighterKnockdownComponent knockdown,
            ref FighterCounterComponent counter) {
            fighter.Influence -= CostRaw;
            // Invulnerability on the shared counter belongs to the action this press cancels (the
            // knockdown, tech, roll, air dodge, throw animation, Foresight sidestep, a ledge grant),
            // so it is replaced by Defy's own six frames; otherwise a Defy out of a knockdown would
            // keep its remaining frames over the whole vulnerable recovery. Respawn protection is
            // independent (Defy is not offensive and ends nothing): it keeps its longer grant.
            bool respawnProtected = (action.ActionFlags & FighterActionStateComponent.ActionFlagRespawnProtection) != 0;
            if (!respawnProtected || fighter.InvulnerabilityFrames < BasicComboRules.DefyInvulnerableFrames) {
                fighter.InvulnerabilityFrames = BasicComboRules.DefyInvulnerableFrames;
            }
            fighter.Velocity = FPVector2.Zero;
            fighter.HitstunFrames = 0;
            fighter.DazeFrames = 0;
            verb.PendingLaunchActive = 0;
            verb.Tumble = FighterVerbRules.TumbleNone;
            verb.ShieldStunFrames = 0;
            verb.HitstunBlockCancelBlocked = 0;
            verb.TechLockoutFrames = 0;
            FighterKnockdownRules.Clear(ref knockdown);
            FighterSlamRules.Clear(ref knockdown);
            FighterForesightRules.Clear(ref counter);
            FighterSpecialCastRules.Clear(ref action);
            action.SpecialCancelBuffer = 0;
            FighterUniversalMovementRules.Cancel(ref runtime);
            FighterBasicAttackRules.CancelString(ref runtime);
            if (FighterLedgeRules.IsHanging(in runtime)) {
                FighterLedgeRules.ClearHang(ref runtime);
                runtime.LedgeRegrabLockoutFrames = FighterLedgeRules.RegrabLockoutFrames;
            }
            runtime.FreezeBufferArmed = 0;
            runtime.FreezeBufferedSpecials = 0;

            action.DefyFrames = 1;
            action.ActionFlags &= ~(FighterActionStateComponent.ActionFlagDefyBlastFired
                | FighterActionStateComponent.ActionFlagDefyBuffered);
        }

        /// <summary>
        /// Steps the presentation counter (wrapping, bits 12 to 15). Called on the blast
        /// frame (frame 6), so the ring, label and cue line up with Story's blast.
        /// </summary>
        internal static void StepCounter(ref FighterActionStateComponent action) {
            int next = (Counter(in action) + 1) & 0xF;
            action.ActionFlags &= ~FighterActionStateComponent.DefyCounterMask;
            action.ActionFlags |= next << FighterActionStateComponent.DefyCounterShift;
        }

        /// <summary>
        /// One unfrozen tick of the clock (frame n to n + 1). Returns false and clears the
        /// action on the tick it would reach frame 31.
        /// </summary>
        public static bool Advance(ref FighterActionStateComponent action) {
            if (action.DefyFrames <= 0) return false;
            action.DefyFrames++;
            if (action.DefyFrames > BasicComboRules.DefyTotalFrames) {
                Clear(ref action);
                return false;
            }
            return true;
        }

        /// <summary>Ends the action: the clock and the blast-fired and buffered bits. The presentation counter stays.</summary>
        public static void Clear(ref FighterActionStateComponent action) {
            action.DefyFrames = 0;
            action.ActionFlags &= ~(FighterActionStateComponent.ActionFlagDefyBlastFired
                | FighterActionStateComponent.ActionFlagDefyBuffered);
        }

        /// <summary>
        /// A hit, daze, grab, Ultimate capture, knockdown, knockout or the respawn
        /// platform ends a Defy in its vulnerable recovery (the startup cannot be reached
        /// by any of them). Same set as a Special cast's.
        /// </summary>
        public static bool IsInterrupted(
            in FighterStateComponent fighter, in FighterVerbComponent verb, bool captured, bool knockedDown) =>
            fighter.HitstunFrames > 0
            || fighter.DazeFrames > 0
            || verb.BeingHeld != 0
            || captured
            || knockedDown
            || fighter.Stocks <= 0
            || FighterMatchFlowRules.IsOnRespawnPlatform(in fighter);

        /// <summary>Clears a running Defy the interrupt set ends. Returns true when it did.</summary>
        public static bool ClearIfInterrupted(
            ref FighterActionStateComponent action,
            in FighterStateComponent fighter,
            in FighterVerbComponent verb,
            bool captured,
            bool knockedDown) {
            if (action.DefyFrames <= 0 || !IsInterrupted(in fighter, in verb, captured, knockedDown)) return false;
            Clear(ref action);
            return true;
        }
    }
}
