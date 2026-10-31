using FTT.Core;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// Meter and Defy update 2026-10-31 (plan section 2.4): the movement system's half of the
    /// active Defy History - reading the press above the hitstop gate, the one-deep buffer for
    /// a press made on a frozen tick, and the clock. The blast and the heal are the combat
    /// system's (<c>FighterCombatSystem.DefyBlast.cs</c>); the rules are
    /// <see cref="FighterDefyRules"/>.
    /// </summary>
    public sealed partial class FighterMovementSystem {

        private const int DefyButton = (int)GameplayButtons.Defy;

        /// <summary>
        /// Reads this tick's Defy press at the top of the per-fighter loop. On an unfrozen
        /// tick a legal press begins the Defy now and returns true (it claims the tick: the
        /// fighter's input is discarded by <see cref="ProcessDefy"/>). On a frozen tick (the
        /// fighter's own hitstop) a legal press sets the buffered bit and begins nothing;
        /// the thaw tick starts it (<see cref="BeginBufferedDefy"/>).
        /// </summary>
        private bool ReadDefyPress(
            ref Frame frame,
            EntityRef entity,
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            ref FighterActionStateComponent action,
            in FighterUltimateActivationComponent activation,
            ref FighterKnockdownComponent knockdown,
            bool matchLive) {
            bool pressed = (runtime.PressedButtons & DefyButton) != 0;
            if (verb.HitstopFrames > 0) {
                if (pressed && FighterDefyRules.CanBegin(in fighter, in action, in activation, matchLive)) {
                    action.ActionFlags |= FighterActionStateComponent.ActionFlagDefyBuffered;
                }
                return false;
            }
            action.ActionFlags &= ~FighterActionStateComponent.ActionFlagDefyBuffered;
            if (!pressed || !FighterDefyRules.CanBegin(in fighter, in action, in activation, matchLive)) return false;
            BeginDefy(ref frame, entity, ref fighter, ref runtime, ref verb, ref action, ref knockdown);
            return true;
        }

        /// <summary>
        /// The thaw tick of a hitstop (HitstopFrames just reached 0): a Defy press held
        /// through the freeze begins now if it is still legal, overriding the hit that
        /// caused the freeze (its pending launch, hitstun and knockdown are cleared by the
        /// Defy). Returns true when it began.
        /// </summary>
        private bool BeginBufferedDefy(
            ref Frame frame,
            EntityRef entity,
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            ref FighterActionStateComponent action,
            in FighterUltimateActivationComponent activation,
            ref FighterKnockdownComponent knockdown,
            bool matchLive) {
            if ((action.ActionFlags & FighterActionStateComponent.ActionFlagDefyBuffered) == 0) return false;
            action.ActionFlags &= ~FighterActionStateComponent.ActionFlagDefyBuffered;
            if (!FighterDefyRules.CanBegin(in fighter, in action, in activation, matchLive)) return false;
            BeginDefy(ref frame, entity, ref fighter, ref runtime, ref verb, ref action, ref knockdown);
            FighterUltimateActivationRules.DiscardButtons(ref runtime);
            return true;
        }

        /// <summary>
        /// Accepts the Defy: the fighter's own state change (<see cref="FighterDefyRules.Begin"/>),
        /// the grab on either side released (a held fighter frees itself, a grabber lets its
        /// victim go), and the action chokepoint told (not an offensive action, so it ends
        /// neither the respawn protection nor anything else).
        /// </summary>
        private void BeginDefy(
            ref Frame frame,
            EntityRef entity,
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            ref FighterActionStateComponent action,
            ref FighterKnockdownComponent knockdown) {
            ref FighterCounterComponent counter = ref frame.Get<FighterCounterComponent>(entity);
            ref FighterDefenseComponent defense = ref frame.Get<FighterDefenseComponent>(entity);
            // A fighter holding a victim lets it go; the victim is the partner on the verb.
            if ((verb.GrabPhase == FighterGrabRules.PhaseHolding || verb.GrabPhase == FighterGrabRules.PhaseThrowAnimation)
                && verb.GrabPartnerPlayerID >= 0
                && FighterEntityQueries.TryFindFighter(ref frame, verb.GrabPartnerPlayerID, out EntityRef victimEntity)) {
                ref FighterVerbComponent victimVerb = ref frame.Get<FighterVerbComponent>(victimEntity);
                FighterGrabRules.ReleaseHeldVictim(ref victimVerb);
                victimVerb.ClearGrabPartner();
            }
            // Any grab of its own ends; a hold on the Defier itself is simply let go (the
            // grabber's phase machine ends the grab when the victim is no longer held).
            verb.GrabPhase = FighterGrabRules.PhaseNone;
            verb.GrabPhaseFrames = 0;
            verb.BeingHeld = 0;
            verb.ClearGrabPartner();

            FighterDefyRules.Begin(ref fighter, ref runtime, ref verb, ref action, ref knockdown, ref counter);
            FighterActionRules.OnActionAccepted(ref fighter, ref verb, ref defense, ref action, FighterActionKind.Defy);
        }

        /// <summary>
        /// The Defy's clock for one unfrozen tick, after the hitstop gate. An interrupt
        /// (a hit, daze, grab, capture, knockdown, knockout) ends it first. The tick that
        /// would reach frame 31 returns the fighter to control. While it runs the fighter
        /// is action-locked: the tick's input is discarded, claiming the tick from every
        /// other verb. Frames 1 to 6 hold the fighter in place with no gravity, counting the
        /// grant of invulnerability down (frame 1, the accepting tick, leaves the full six);
        /// that tick is finished here and the method returns true. Frames 7 to 30 return
        /// false so the ordinary movement below runs gravity and landing on the discarded
        /// input.
        /// </summary>
        private static bool ProcessDefy(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterVerbComponent verb,
            ref FighterDefenseComponent defense,
            ref FighterActionStateComponent action,
            in FighterTuningComponent tuning,
            in FighterUltimateActivationComponent activation,
            in FighterKnockdownComponent knockdown,
            bool beganThisTick,
            bool matchLive) {
            if (action.DefyFrames <= 0) return false;
            if (FighterDefyRules.ClearIfInterrupted(
                    ref action, in fighter, in verb,
                    FighterUltimateActivationRules.IsCaptured(in activation),
                    FighterKnockdownRules.IsActive(in knockdown))) return false;
            if (!beganThisTick && !FighterDefyRules.Advance(ref action)) return false;
            FighterUltimateActivationRules.DiscardInput(ref runtime);
            if (!FighterDefyRules.IsStartup(in action)) return false;
            if (!beganThisTick && fighter.InvulnerabilityFrames > 0) fighter.InvulnerabilityFrames--;
            fighter.Velocity = FPVector2.Zero;
            TickCounters(ref fighter, ref runtime, ref verb, ref defense, in tuning, matchLive);
            return true;
        }
    }
}
