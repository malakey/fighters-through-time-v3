using FTT.Combat;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// The Defy History blast and heal (meter and Defy update 2026-10-31, plan sections 2.5
    /// and 2.6), applied on Defy frame 6 by <c>FighterCombatSystem.ResolveDefyBlasts</c>. It is
    /// deliberately NOT a hit: nothing here goes through <c>ApplyFighterHit</c>, so there is
    /// no damage, no block interaction (the block stance and the shield charges are
    /// untouched), no hyper-armour check, no meter on either side, no Rally echo, no D1a
    /// landed signal and so no Momentum. A target within 2.5 u of the Defier's body centre
    /// that is not invulnerable is pushed away at (6, up 6) u/s as an airborne,
    /// non-launching 30-frame stagger with 8 frames of hitstop; its swing, Special cast, grab,
    /// Foresight stance, knockdown get-up and an Ultimate wind-up or active window are
    /// cancelled. The Defier heals the larger of its Rally pool and a tenth of its maximum
    /// HP (never above the HP it is missing) and the pool is zeroed. Fixed-point and
    /// integers only; the radius test is a squared distance.
    /// </summary>
    internal static class FighterDefyBlastRules {

        /// <summary>
        /// Whether <paramref name="target"/> is inside the blast ring: dx squared plus dy
        /// squared at most 6.25 on the fighters' positions (both share the same body offset,
        /// so the difference of positions is the difference of body centres).
        /// </summary>
        public static bool InRadius(in FighterStateComponent defier, in FighterStateComponent target) {
            FP64 dx = target.Position.x - defier.Position.x;
            FP64 dy = target.Position.y - defier.Position.y;
            return dx * dx + dy * dy <= FighterDefyRules.RadiusSquaredRaw;
        }

        /// <summary>Whether the blast reaches this fighter: alive, in play, not invulnerable and inside the ring.</summary>
        public static bool Reaches(in FighterStateComponent defier, in FighterStateComponent target) =>
            target.Stocks > 0
            && target.RespawnFramesRemaining <= 0
            && target.InvulnerabilityFrames <= 0
            && InRadius(in defier, in target);

        /// <summary>
        /// The heal on the blast frame (P10): the larger of the Rally pool (rounded) and a
        /// tenth of the maximum HP, capped at the missing HP. Zeroes the pool and its drain,
        /// which also forfeits the uncredited damage-taken meter it would have paid.
        /// </summary>
        public static int Heal(ref FighterStateComponent defier, ref FighterVerbComponent verb) {
            long rounded = (verb.EchoPool.RawValue + FP64.One.RawValue / 2) / FP64.One.RawValue;
            if (rounded > int.MaxValue) rounded = int.MaxValue;
            int heal = BasicComboRules.DefyHealPoints(defier.MaxHP - defier.CurrentHP, (int)rounded, defier.MaxHP);
            defier.CurrentHP += heal;
            verb.EchoPool = FP64.Zero;
            verb.EchoDrainPerFrame = FP64.Zero;
            return heal;
        }

        /// <summary>
        /// The target's side of the blast (see the class remarks). Writes the push and the
        /// stagger directly and cancels everything the stagger interrupts.
        /// </summary>
        public static void ApplyToTarget(
            in FighterStateComponent defier,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            ref FighterActionStateComponent action,
            ref FighterCounterComponent counter,
            ref FighterKnockdownComponent knockdown,
            ref FighterUltimateActivationComponent activation) {
            FP64 away = target.Position.x >= defier.Position.x ? FighterDefyRules.PushXRaw : -FighterDefyRules.PushXRaw;
            target.Velocity = new FPVector2(away, FighterDefyRules.PushYRaw);
            target.IsGrounded = 0;
            if (target.HitstunFrames < BasicComboRules.DefyBlastStaggerFrames) {
                target.HitstunFrames = BasicComboRules.DefyBlastStaggerFrames;
            }
            // A non-launching stagger: no tumble, no pending launch, no DI, and no
            // Block escape out of it (unblockable).
            verb.Tumble = FighterVerbRules.TumbleNone;
            verb.PendingLaunchActive = 0;
            verb.ShieldStunFrames = 0;
            verb.HitstunBlockCancelBlocked = 1;
            if (verb.GrabPhase != FighterGrabRules.PhaseNone && verb.GrabPhase != FighterGrabRules.PhaseThrowAnimation) {
                verb.GrabPhase = FighterGrabRules.PhaseNone;
                verb.GrabPhaseFrames = 0;
                verb.ClearGrabPartner();
            }
            FighterBasicAttackRules.CancelString(ref runtime);
            FighterUniversalMovementRules.Cancel(ref runtime);
            runtime.FreezeBufferArmed = 0;
            runtime.FreezeBufferedSpecials = 0;
            FighterSpecialCastRules.Clear(ref action);
            action.SpecialCancelBuffer = 0;
            FighterForesightRules.Clear(ref counter);
            FighterKnockdownRules.Clear(ref knockdown);
            FighterSlamRules.Clear(ref knockdown);
            // R3 / P6: an Ultimate in its wind-up or active window is cancelled; the
            // meter it spent stays spent. A whiff recovery runs on (and the cinematic
            // cannot occur: the Defier would be captured, and a captured fighter cannot Defy).
            if (activation.Phase == FighterUltimateActivationRules.PhaseWindup
                || activation.Phase == FighterUltimateActivationRules.PhaseActive) {
                FighterUltimateActivationRules.ClearCaster(ref target, ref activation);
            }
        }
    }
}
