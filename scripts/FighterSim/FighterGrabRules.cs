using FTT.Core;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// V7.2 universal grab &amp; throws — the third side of the attack/block/
    /// grab triangle. All state lives on <see cref="FighterVerbComponent"/>
    /// (GrabPhase / GrabPhaseFrames / ThrowDirection / BeingHeld /
    /// ThrowImmunityFrames); every number comes from
    /// <see cref="FTT.Combat.BasicComboRules"/>. Driven per tick from
    /// <c>FighterCombatSystem</c>, which owns both fighters.
    ///
    /// Phases: 0 none · 1 startup (10f) · 2 active (4f) · 3 recovery (24f
    /// whiff / 8f clash bounce) · 4 holding (30f decision window) · 5 throw
    /// animation (12f, both invulnerable).
    /// </summary>
    internal static class FighterGrabRules {
        public const int PhaseNone = 0;
        public const int PhaseStartup = 1;
        public const int PhaseActive = 2;
        public const int PhaseRecovery = 3;
        public const int PhaseHolding = 4;
        public const int PhaseThrowAnimation = 5;

        public const int ThrowForward = 0;
        public const int ThrowUp = 1;
        public const int ThrowBack = 2;

        /// <summary>Throws hit like a finisher-class launch for hitstun purposes.</summary>
        public const int ThrowHitstunFrames = 24;

        private const int BasicButton = 1 << 2;
        private const int BlockButton = 1 << 6;
        /// <summary>C01c logical Grab request (direct bind or enabled chord).</summary>
        private const int GrabButton = 1 << 13;
        /// <summary>C01c "chords are off on the originating device"; see GameplayButtons.DirectOrigin.</summary>
        private const int DirectOriginButton = 1 << 14;
        private static readonly FP64 GrabReach = FP64.FromDouble(FTT.Combat.BasicComboRules.GrabReachUnits);
        private static readonly FP64 ClashPushSpeed = FP64.FromInt(3);

        /// <summary>The preset chord: BasicAttack pressed while Block is held.</summary>
        public static bool ChordPressed(in FighterRuntimeComponent runtime) =>
            (runtime.PressedButtons & BasicButton) != 0 && (runtime.HeldButtons & BlockButton) != 0;

        /// <summary>
        /// C01c (Package 11 A1c): whether this tick requests a grab, from
        /// <b>either</b> route — the direct <c>gameplay_grab</c> bind or the
        /// Block+BasicAttack preset chord — deduplicated to one request per actor
        /// per tick.
        ///
        /// <para>Neither route synthesizes a component press, and the direct bind
        /// grants no extra priority, cancel, leniency or resource bypass: it is the
        /// same verb, asked for a different way. When the originating machine has
        /// its chords switched off it sets
        /// <see cref="FTT.Core.GameplayButtons.DirectOrigin"/>, and the chord route
        /// is skipped here — which is what stops a peer re-recognizing a remote
        /// chord with its own shortcut settings.</para>
        /// </summary>
        public static bool Requested(in FighterRuntimeComponent runtime) {
            if ((runtime.PressedButtons & GrabButton) != 0) return true;
            if ((runtime.HeldButtons & DirectOriginButton) != 0) return false;
            return ChordPressed(in runtime);
        }

        /// <summary>A fighter occupied by any part of the grab/held state.</summary>
        public static bool IsBusy(in FighterVerbComponent verb) =>
            verb.GrabPhase != PhaseNone || verb.BeingHeld != 0;

        /// <summary>
        /// Grab initiation gate: grounded only (like the block stance it
        /// answers), free of hitstun/daze/roll/swing/hang/respawn, not already
        /// grabbing or held.
        /// </summary>
        public static bool CanStartGrab(
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            in FighterVerbComponent verb) =>
            verb.GrabPhase == PhaseNone
            && verb.BeingHeld == 0
            // V7.3 shieldstun locks the stance up completely — including the
            // grab chord out of it.
            && verb.ShieldStunFrames <= 0
            && fighter.IsGrounded != 0
            && fighter.Stocks > 0
            && fighter.HitstunFrames <= 0
            && fighter.DazeFrames <= 0
            && fighter.RespawnFramesRemaining <= 0
            && runtime.AttackPhase == FighterBasicAttackRules.PhaseNone
            && runtime.UniversalMovementState == (int)UniversalMovementPhase.None
            && !FighterLedgeRules.IsHanging(in runtime);

        /// <summary>
        /// Whether a grab's active window seizes this target: in reach, in
        /// front, grounded, and not in any of the whiff states (hitstun, daze,
        /// airborne, rolling, invulnerable, throw-immune, already held).
        /// Blocking is deliberately NOT a whiff state — grab beats block.
        /// </summary>
        public static bool ActiveWindowSeizes(
            in FighterStateComponent grabber,
            in FighterStateComponent target,
            in FighterRuntimeComponent targetRuntime,
            in FighterVerbComponent targetVerb) {
            if (target.Stocks <= 0 || target.IsGrounded == 0) return false;
            if (target.HitstunFrames > 0 || target.DazeFrames > 0) return false;
            // V7.3 amendment: grab beats the *stance*, never the *stun* — a
            // target locked in shieldstun whiffs the grab.
            if (targetVerb.ShieldStunFrames > 0) return false;
            if (target.InvulnerabilityFrames > 0) return false;
            if (targetVerb.ThrowImmunityFrames > 0 || targetVerb.BeingHeld != 0) return false;
            if (targetRuntime.UniversalMovementState != (int)UniversalMovementPhase.None) return false;
            if (FighterMatchFlowRules.IsOnRespawnPlatform(in target)) return false;

            FP64 dx = target.Position.x - grabber.Position.x;
            FP64 front = grabber.FacingRight != 0 ? dx : -dx;
            return front >= FP64.Zero && front <= GrabReach
                && FP64.Abs(target.Position.y - grabber.Position.y) <= FP64.One;
        }

        /// <summary>
        /// Resolves the throw direction from the grabber's held input when the
        /// decision window closes: up wins, then away-from-facing is the back
        /// throw; toward or neutral is the forward throw.
        /// </summary>
        public static int ResolveThrowDirection(
            in FighterStateComponent grabber,
            in FighterRuntimeComponent grabberRuntime) {
            if (grabberRuntime.MoveY < -30) return ThrowUp;
            if (grabberRuntime.MoveX > 30) return grabber.FacingRight != 0 ? ThrowForward : ThrowBack;
            if (grabberRuntime.MoveX < -30) return grabber.FacingRight != 0 ? ThrowBack : ThrowForward;
            return ThrowForward;
        }

        /// <summary>Releases a held victim without a throw (the grabber was hit).</summary>
        public static void ReleaseHeldVictim(ref FighterVerbComponent victimVerb) {
            victimVerb.BeingHeld = 0;
        }

        /// <summary>Clash bounce: both grabs whiffed into a short shared recovery.</summary>
        public static void ApplyClash(
            ref FighterStateComponent fighter,
            ref FighterVerbComponent verb) {
            verb.GrabPhase = PhaseRecovery;
            verb.GrabPhaseFrames = FTT.Combat.BasicComboRules.GrabClashBounceFrames;
            fighter.Velocity = new FPVector2(
                fighter.FacingRight != 0 ? -ClashPushSpeed : ClashPushSpeed,
                FP64.Zero);
        }

        /// <summary>The held victim's pinned position: on the grabber's front.</summary>
        public static FPVector2 HeldPosition(in FighterStateComponent grabber) => new(
            grabber.Position.x + (grabber.FacingRight != 0 ? GrabReach : -GrabReach),
            grabber.Position.y);
    }
}
