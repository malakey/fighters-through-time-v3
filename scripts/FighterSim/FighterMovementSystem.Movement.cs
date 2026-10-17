using FTT.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// CM-MOVE (combat and movement update 2026-10-16; plan §2.5.1, §2.5.2,
    /// §2.6.1–§2.6.3): the movement half of the update in the Fighter sim — the
    /// short-hop latches, the double-tap drop-through, the one-way lip pop and
    /// the ledge get-up wiring. Every rule here is integer or fixed-point
    /// arithmetic on snapshotted fields (component 323's <c>DownTapFrames</c> and
    /// <c>ActionFlags</c> bits 1–3, component 305's universal-movement slot and
    /// <c>AttackFlags</c> 512); nothing reads Story state, InputManager or Godot.
    /// <see cref="FighterMovementSystem.Update"/> holds one call line per site.
    /// </summary>
    public sealed partial class FighterMovementSystem {
        // === The short-hop latches (5.1, Q5) ===

        /// <summary>
        /// 323 <c>ActionFlags</c> bit 1, <c>JumpReleasedSincePress</c>, from this
        /// tick's raw edges: a Jump press clears it, a Jump release sets it. Read
        /// at the top of the tick, above the hitstop gate and every input
        /// discard, so for any <see cref="PlayerInputFrame.Create"/> stream (the
        /// driver, the CPU, the wire) it equals "Jump not held" tick for tick from
        /// the first press on, a freeze included. Hand-built frames that never set
        /// <c>Released</c> stay "held" — full hops — which is why the edge-reading
        /// rules are pinned with Create-built input (plan §1.8). Prediction zeroes
        /// both edges, so a predicted remote frame keeps the latch where it was,
        /// as it keeps <c>Held</c>.
        /// </summary>
        internal static void UpdateJumpLatch(ref FighterActionStateComponent action, in FighterRuntimeComponent runtime) {
            if ((runtime.PressedButtons & JumpButton) != 0) {
                action.ActionFlags &= ~FighterActionStateComponent.ActionFlagJumpReleasedSincePress;
            } else if ((runtime.ReleasedButtons & JumpButton) != 0) {
                action.ActionFlags |= FighterActionStateComponent.ActionFlagJumpReleasedSincePress;
            }
        }

        /// <summary>The short hop's Jump read: true unless the latch says Jump was released since its last press.</summary>
        internal static bool JumpHeldForGravity(in FighterActionStateComponent action) =>
            (action.ActionFlags & FighterActionStateComponent.ActionFlagJumpReleasedSincePress) == 0;

        /// <summary>The Q5 gate: the current rise began with this airtime's own Jump press.</summary>
        internal static bool RiseFromOwnJump(in FighterActionStateComponent action) =>
            (action.ActionFlags & FighterActionStateComponent.ActionFlagRiseFromOwnJump) != 0;

        /// <summary>Sets the Q5 bit: a ground jump, an air jump or the ledge climb jump was accepted.</summary>
        internal static void MarkOwnJump(ref FighterActionStateComponent action) =>
            action.ActionFlags |= FighterActionStateComponent.ActionFlagRiseFromOwnJump;

        /// <summary>
        /// The once-a-tick latch upkeep, after the hitstop gate and the respawn
        /// platform's zeroing of component 323 (plan §1.5):
        /// <list type="bullet">
        /// <item><b>RiseFromOwnJump</b> (Q5) is cleared once the rise is no longer
        /// the fighter's own jump: on the ground; in hitstun or daze (every launch
        /// and every airborne non-launching hit); in a grab on either side or a
        /// throw; hanging from a ledge or climbing one; in a kit motion or a
        /// movement ability's float window (they write the fighter's velocity);
        /// casting or captured by an Ultimate; and once Shakespeare's Tempest has
        /// lifted its caster (<see cref="CastLiftedTheCaster"/>). Each of these
        /// was set by an earlier tick's system, so it is read here before this
        /// tick's gravity step, and a jump accepted this tick sets the bit again
        /// after it.</item>
        /// <item><b>LedgeStickRearmed</b> (5.2) is cleared whenever the fighter is
        /// not hanging, so every grab starts un-armed.</item>
        /// <item><b>DownTapFrames</b> (6.3) counts down, here — after the hitstop
        /// gate, before any press is read — exactly as Story decrements its window
        /// at the top of its unfrozen tick.</item>
        /// </list>
        /// </summary>
        internal static void TickMovementLatches(
            ref FighterActionStateComponent action,
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            in FighterVerbComponent verb,
            in FighterUltimateActivationComponent ultimateActivation) {
            if (fighter.IsGrounded != 0
                || fighter.HitstunFrames > 0
                || fighter.DazeFrames > 0
                || FighterGrabRules.IsBusy(in verb)
                || FighterLedgeRules.IsHanging(in runtime)
                || FighterLedgeRules.IsGetUpClimb(in runtime)
                || FighterKitMotion.IsKitPhase(runtime.UniversalMovementState)
                || runtime.FloatFrames > 0
                || FighterUltimateActivationRules.IsCasterBusy(in ultimateActivation)
                || FighterUltimateActivationRules.IsCaptured(in ultimateActivation)
                || CastLiftedTheCaster(in fighter, in action)) {
                action.ActionFlags &= ~FighterActionStateComponent.ActionFlagRiseFromOwnJump;
            }
            if (!FighterLedgeRules.IsHanging(in runtime)) {
                action.ActionFlags &= ~FighterActionStateComponent.ActionFlagLedgeStickRearmed;
            }
            if (action.DownTapFrames > 0) action.DownTapFrames--;
        }

        /// <summary>
        /// True once the fighter's running Special has written its vertical
        /// velocity: Shakespeare's Tempest (his Special 2), whose zone spawn lifts
        /// the caster (<c>FighterAbilityEntitySystem</c>,
        /// <see cref="FighterTempestRules.LiftSpeed"/>) on its active entry — the
        /// only Special in the roster whose fire pass writes the caster's
        /// velocity. A kit lift is never cut by the short hop (Q5).
        /// </summary>
        internal static bool CastLiftedTheCaster(in FighterStateComponent fighter, in FighterActionStateComponent action) =>
            fighter.CharacterID == (int)FighterCharacterID.Shakespeare
            && action.SpecialSlot == 2
            && FighterSpecialCastRules.IsBusy(in action)
            && (action.SpecialFlags & FighterSpecialCastRules.FlagEffectFired) != 0;

        // === Drop-through: both inputs (6.3) ===

        /// <summary>
        /// Decision 6.3 (2026-10-16): the double tap of Down, Story's
        /// <c>CheckDropThrough</c> rule in the sim. A grounded Down press with
        /// drop-through allowed opens the
        /// <see cref="UniversalMovementRules.DropThroughDoubleTapFrames"/> (18)
        /// window when none is open; a press inside an open window closes it and
        /// drops through a one-way platform (never the solid base floor, where
        /// it does nothing). So taps 17 ticks apart drop and taps 18 apart reopen
        /// the window. Down+Jump keeps dropping (<see cref="ApplyNormalMovement"/>).
        /// Returns true when this tick's press dropped the fighter.
        /// </summary>
        private static bool TryDoubleTapDropThrough(
            ref FighterStateComponent fighter,
            ref FighterActionStateComponent action,
            in FighterRuntimeComponent runtime,
            bool onSolidBaseFloor) {
            if ((runtime.PressedButtons & DownButton) == 0) return false;
            if (action.DownTapFrames <= 0) {
                action.DownTapFrames = UniversalMovementRules.DropThroughDoubleTapFrames;
                return false;
            }
            action.DownTapFrames = 0;
            if (onSolidBaseFloor) return false;
            BeginDropThrough(ref fighter);
            return true;
        }

        /// <summary>The sim's drop-through, whichever input asked for it: 30 frames of no one-way landing, −2 u/s.</summary>
        private static void BeginDropThrough(ref FighterStateComponent fighter) {
            fighter.DropThroughFrames = 30;
            fighter.IsGrounded = 0;
            fighter.Velocity.y = FP64.FromInt(-2);
        }

        // === The one-way lip pop (6.2, Q6) ===

        /// <summary>
        /// Decision 6.2 with the Q6 ruling (<c>PROVISIONAL-SIM-LIP-POP</c>): on a
        /// normal-control airborne tick — never in hitstun or daze, a Special
        /// cast, a kit motion or a ledge climb — a fighter moving down or at rest
        /// this tick (vertical velocity ≤ 0: the apex or a fall) whose feet came
        /// from below a one-way platform's top and now sit at most
        /// <see cref="FighterAirPhysics.OneWayLipPopBand"/> (0.2 u) under it,
        /// horizontally over the platform, is placed on the top and lands through
        /// the ordinary landing path (jumps refilled here; the air dodge and the
        /// regrab budget refresh in <see cref="Update"/>). Stateless. A fighter
        /// dropping through a platform (<c>DropThroughFrames</c> &gt; 0) is exempt,
        /// as the caller's guard already exempts it from every one-way landing.
        /// Floor segments are never popped: their per-x snap already lifts from
        /// any depth. The caller runs it before the ledge capture, so a near-edge
        /// capture inside the band becomes a landing.
        /// </summary>
        private void TryLipPop(
            ref FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            in FighterActionStateComponent action,
            in FighterUltimateActivationComponent ultimateActivation,
            in FighterTuningComponent tuning,
            FP64 previousY) {
            if (fighter.IsGrounded != 0 || fighter.DropThroughFrames > 0) return;
            if (fighter.Velocity.y > FP64.Zero) return;
            if (fighter.HitstunFrames > 0 || fighter.DazeFrames > 0) return;
            if (FighterSpecialCastRules.IsBusy(in action)) return;
            if (FighterUltimateActivationRules.IsCasterBusy(in ultimateActivation)) return;
            if (FighterKitMotion.IsKitPhase(runtime.UniversalMovementState)) return;
            if (FighterLedgeRules.IsGetUpClimb(in runtime)) return;
            for (int index = 0; index < _geometry.Platforms.Length; index++) {
                ref readonly FighterStagePlatform platform = ref _geometry.Platforms[index];
                if (previousY >= platform.SurfaceY) continue;
                if (fighter.Position.y >= platform.SurfaceY) continue;
                if (platform.SurfaceY - fighter.Position.y > FighterAirPhysics.OneWayLipPopBand) continue;
                if (!platform.Supports(fighter.Position.x)) continue;
                fighter.Position.y = platform.SurfaceY;
                if (fighter.Velocity.y < FP64.Zero) fighter.Velocity.y = FP64.Zero;
                fighter.IsGrounded = 1;
                fighter.RemainingJumps = tuning.MaxJumpCount;
                return;
            }
        }

        // === Ledge get-ups (5.2) ===

        /// <summary>
        /// One hang tick through <see cref="FighterLedgeRules.Process(ref FighterStateComponent, ref FighterRuntimeComponent, ref FighterVerbComponent, ref FighterDefenseComponent, ref FighterActionStateComponent, in FighterTuningComponent, in FPVector2, in FPVector2)"/>
        /// with the anchor's stand position. The caller ends the tick.
        /// </summary>
        private void ProcessHang(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            ref FighterDefenseComponent defense,
            ref FighterActionStateComponent action,
            in FighterTuningComponent tuning,
            in FPVector2 hangPosition) {
            if (!_geometry.TryGetStandPosition(runtime.LedgeAnchor, out FPVector2 standPosition)) {
                standPosition = hangPosition;
            }
            FighterLedgeRules.Process(
                ref fighter, ref runtime, ref verb, ref defense, ref action,
                in tuning, in hangPosition, in standPosition);
        }

        /// <summary>
        /// A running ledge climb (universal-movement values 8–10). Returns true
        /// when the climb owns the whole tick (the caller ends it): a climb tick,
        /// or the tick the ledge strike starts. Returns false when the tick goes
        /// on normally: the climb finished (the neutral climb's free tick, the
        /// ledge roll's first roll tick), or a hit landed in the climb's
        /// vulnerable frames and cancelled it — hitstun or daze then takes the
        /// tick like any other hit.
        /// </summary>
        private bool AdvanceLedgeGetUp(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning) {
            if (fighter.HitstunFrames > 0 || fighter.DazeFrames > 0) {
                FighterUniversalMovementRules.Cancel(ref runtime);
                return false;
            }
            int anchor = runtime.UniversalMovementDirection;
            if (!_geometry.TryGetHangPosition(anchor, out FPVector2 hangPosition)
                || !_geometry.TryGetStandPosition(anchor, out FPVector2 standPosition)) {
                // Defensive, as the hang branch: an anchor that no longer resolves
                // (a stage swap under a restored snapshot) ends the climb.
                FighterUniversalMovementRules.Cancel(ref runtime);
                return false;
            }
            return FighterLedgeRules.AdvanceGetUp(
                    ref fighter, ref runtime, in tuning, in hangPosition, in standPosition)
                != FighterLedgeRules.GetUpTick.Finished;
        }
    }
}
