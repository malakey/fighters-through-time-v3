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
}
