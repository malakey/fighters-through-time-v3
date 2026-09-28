using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// V7 pillar-#4 verb rules shared by the deterministic systems: universal
    /// hitstop, directional influence, and the landing tech. All state lives on
    /// <see cref="FighterVerbComponent"/> (Klotho ID 310); all numbers come from
    /// <see cref="FTT.Combat.BasicComboRules"/> — never author a second copy.
    /// </summary>
    public static class FighterVerbRules {

        // DI rotation tables for the quantized half-steps (0°, ±7.5°, ±15°).
        // FromDouble of process constants is deterministic; no float math runs
        // per tick.
        private static readonly FP64[] DiSin = {
            FP64.Zero, FP64.FromDouble(0.13053), FP64.FromDouble(0.25882)
        };
        private static readonly FP64[] DiCos = {
            FP64.One, FP64.FromDouble(0.99144), FP64.FromDouble(0.96593)
        };
        private static readonly FP64 FullStepThreshold = FP64.FromDouble(0.5);
        private static readonly FP64 HalfStepThreshold = FP64.FromDouble(0.17);

        /// <summary>
        /// Applies the shared hitstop window to both parties of a connected hit.
        /// Lethal hits skip hitstop entirely — the KO presentation owns that
        /// moment — and existing hitstop is never shortened.
        /// </summary>
        public static void ApplyHitstop(
            ref FighterVerbComponent attackerVerb,
            ref FighterVerbComponent targetVerb,
            int frames) {
            if (frames <= 0) return;
            if (attackerVerb.HitstopFrames < frames) attackerVerb.HitstopFrames = frames;
            if (targetVerb.HitstopFrames < frames) targetVerb.HitstopFrames = frames;
        }

        /// <summary>
        /// Stashes a launching hit's velocity for DI resolution when the victim's
        /// hitstop ends. The velocity is also written immediately (position is
        /// frozen through hitstop, so the pre-write is inert until then).
        /// </summary>
        public static void StashPendingLaunch(ref FighterVerbComponent targetVerb, in FPVector2 launch) {
            targetVerb.PendingLaunchActive = 1;
            targetVerb.PendingLaunchX = launch.x;
            targetVerb.PendingLaunchY = launch.y;
            targetVerb.Tumble = 1;
        }

        /// <summary>
        /// Directional influence (V7): the victim's held direction at hitstop end
        /// bends the launch by up to ±15°, quantized to half-steps so the
        /// rotation uses process-constant tables. Magnitude never changes.
        /// Proportionality uses the input component perpendicular to the launch,
        /// scaled against the launch's Manhattan magnitude.
        /// </summary>
        public static void ResolvePendingLaunch(
            ref FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb) {
            if (verb.PendingLaunchActive != 1) return;
            verb.PendingLaunchActive = 0;

            FP64 lx = verb.PendingLaunchX;
            FP64 ly = verb.PendingLaunchY;
            // Input in sim space: MoveY < 0 is "up held" (matches SelectVariant),
            // and sim Y is up-positive.
            FP64 ix = FP64.FromInt(runtime.MoveX) / FP64.FromInt(127);
            FP64 iy = FP64.FromInt(-runtime.MoveY) / FP64.FromInt(127);

            FP64 cross = lx * iy - ly * ix;
            FP64 scale = FP64.Abs(lx) + FP64.Abs(ly);
            int step = 0;
            if (scale > FP64.Zero) {
                FP64 magnitude = FP64.Abs(cross);
                if (magnitude >= FullStepThreshold * scale) step = 2;
                else if (magnitude >= HalfStepThreshold * scale) step = 1;
            }
            if (step == 0) {
                fighter.Velocity = new FPVector2(lx, ly);
                return;
            }

            FP64 sin = DiSin[step];
            FP64 cos = DiCos[step];
            // Rotate toward the held perpendicular: positive cross rotates CCW.
            if (cross < FP64.Zero) sin = -sin;
            fighter.Velocity = new FPVector2(lx * cos - ly * sin, lx * sin + ly * cos);
        }

        /// <summary>
        /// Landing tech (ukemi): a victim in launched hitstun who holds Block on
        /// ground contact ends hitstun and gets an invulnerable in-place
        /// recovery. Returns true when the tech fired.
        /// </summary>
        public static bool TryLandingTech(
            ref FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb) {
            const int blockButton = 1 << 6;
            if (fighter.HitstunFrames <= 0 || verb.Tumble != 1) return false;
            if (fighter.DazeFrames > 0 || fighter.Stocks <= 0) return false;
            if ((runtime.HeldButtons & blockButton) == 0) return false;

            fighter.HitstunFrames = 0;
            verb.Tumble = 0;
            verb.PendingLaunchActive = 0;
            verb.TechLockoutFrames = FTT.Combat.BasicComboRules.LandingTechRecoveryFrames;
            if (fighter.InvulnerabilityFrames < FTT.Combat.BasicComboRules.LandingTechRecoveryFrames) {
                fighter.InvulnerabilityFrames = FTT.Combat.BasicComboRules.LandingTechRecoveryFrames;
            }
            fighter.Velocity = FPVector2.Zero;
            return true;
        }
    }

    /// <summary>
    /// M05 knockdown and get-up (Package 12 W3b). All state lives on
    /// <see cref="FighterKnockdownComponent"/> (Klotho ID 320); every number
    /// comes from <see cref="FTT.Combat.BasicComboRules"/>, which Story's
    /// <c>PlayerController</c> mirror reads too.
    ///
    /// <para>Timeline, counted in simulation ticks from the tumble's landing
    /// tick L: L starts the knockdown; L+1..L+30 are the 30 knockdown frames,
    /// invulnerable (so are L's own combat resolution) and action-locked; the
    /// get-up is chosen from the held direction on L+30 and runs L+31..L+40
    /// (neutral) or L+31..L+44 (roll) with no invulnerability and no actions;
    /// normal control returns on the next tick.</para>
    ///
    /// <para>Invulnerability rides the ordinary <c>InvulnerabilityFrames</c>
    /// field — the one gate every hit path, grab and pull already reads — so no
    /// damage pipeline needs a knockdown special case. The action lock is the
    /// countdown system's rule: the downed fighter's action buttons for the
    /// tick are discarded before any later system reads them.</para>
    /// </summary>
    public static class FighterKnockdownRules {
        private static readonly FP64 RollSpeedMultiplier =
            FP64.FromDouble(FTT.Core.UniversalMovementRules.RollSpeedMultiplier);

        /// <summary>|MoveX| above this (of 127) picks the roll get-up; the Story twin is 0.25.</summary>
        public const int GetUpRollInputThreshold = 30;

        /// <summary>True while the fighter is down or getting up.</summary>
        public static bool IsActive(in FighterKnockdownComponent knockdown) =>
            knockdown.KnockdownFrames > 0 || knockdown.GetUpFrames > 0;

        /// <summary>True during the invulnerable down phase only.</summary>
        public static bool IsDown(in FighterKnockdownComponent knockdown) => knockdown.KnockdownFrames > 0;

        public static void Clear(ref FighterKnockdownComponent knockdown) {
            knockdown.KnockdownFrames = 0;
            knockdown.GetUpKind = FTT.Combat.BasicComboRules.GetUpNone;
            knockdown.GetUpFrames = 0;
            knockdown.GetUpDirection = 0;
        }

        /// <summary>
        /// A missed tech: a tumbling victim touched down without Block held.
        /// Ends the tumble's hitstun, stops the body and starts the knockdown.
        /// Returns true when the knockdown began. Call only after
        /// <see cref="FighterVerbRules.TryLandingTech"/> declined the landing.
        /// </summary>
        public static bool TryBeginKnockdown(
            ref FighterStateComponent fighter,
            ref FighterVerbComponent verb,
            ref FighterKnockdownComponent knockdown) {
            if (fighter.HitstunFrames <= 0 || verb.Tumble != 1) return false;
            if (fighter.DazeFrames > 0 || fighter.Stocks <= 0) return false;
            if (fighter.RespawnFramesRemaining > 0) return false;

            fighter.HitstunFrames = 0;
            verb.Tumble = 0;
            verb.PendingLaunchActive = 0;
            verb.HitstunBlockCancelBlocked = 0;
            fighter.Velocity = FPVector2.Zero;
            knockdown.KnockdownFrames = FTT.Combat.BasicComboRules.KnockdownFrames;
            knockdown.GetUpKind = FTT.Combat.BasicComboRules.GetUpNone;
            knockdown.GetUpFrames = 0;
            knockdown.GetUpDirection = 0;
            // +1: the landing tick's own combat resolution is covered too, and
            // the grant has decayed to zero by the first get-up tick.
            int invulnerable = FTT.Combat.BasicComboRules.KnockdownFrames + 1;
            if (fighter.InvulnerabilityFrames < invulnerable) fighter.InvulnerabilityFrames = invulnerable;
            return true;
        }

        /// <summary>
        /// A hit that stuns during the (vulnerable) get-up, a stock loss or a
        /// respawn ends the whole sub-phase. Velocity is left alone: the hit
        /// already wrote its knockback.
        /// </summary>
        public static void ClearIfInterrupted(in FighterStateComponent fighter, ref FighterKnockdownComponent knockdown) {
            if (!IsActive(in knockdown)) return;
            if (fighter.HitstunFrames > 0
                || fighter.DazeFrames > 0
                || fighter.Stocks <= 0
                || fighter.RespawnFramesRemaining > 0) {
                Clear(ref knockdown);
            }
        }

        /// <summary>
        /// One tick of the knockdown or the get-up. Writes only velocity; the
        /// movement system integrates position. The get-up is chosen from
        /// <paramref name="moveX"/> on the tick the knockdown reaches zero.
        /// </summary>
        public static void Advance(
            ref FighterStateComponent fighter,
            int moveX,
            in FighterTuningComponent tuning,
            ref FighterKnockdownComponent knockdown) {
            if (fighter.InvulnerabilityFrames > 0) fighter.InvulnerabilityFrames--;
            if (knockdown.KnockdownFrames > 0) {
                fighter.Velocity = FPVector2.Zero;
                knockdown.KnockdownFrames--;
                if (knockdown.KnockdownFrames == 0) {
                    int direction = moveX > GetUpRollInputThreshold ? 1
                        : moveX < -GetUpRollInputThreshold ? -1
                        : 0;
                    knockdown.GetUpKind = FTT.Combat.BasicComboRules.SelectGetUp(direction);
                    knockdown.GetUpFrames = FTT.Combat.BasicComboRules.GetUpFramesFor(knockdown.GetUpKind);
                    knockdown.GetUpDirection = direction;
                }
                return;
            }
            if (knockdown.GetUpFrames <= 0) return;
            knockdown.GetUpFrames--;
            if (knockdown.GetUpKind == FTT.Combat.BasicComboRules.GetUpRoll && knockdown.GetUpFrames >= 0) {
                fighter.Velocity = new FPVector2(
                    FP64.FromInt(knockdown.GetUpDirection) * tuning.MoveSpeed * RollSpeedMultiplier,
                    FP64.Zero);
            } else {
                fighter.Velocity = FPVector2.Zero;
            }
            if (knockdown.GetUpFrames == 0) {
                // The last get-up tick still travels; the stop lands with control.
                knockdown.GetUpKind = FTT.Combat.BasicComboRules.GetUpNone;
                knockdown.GetUpDirection = 0;
            }
        }
    }
}
