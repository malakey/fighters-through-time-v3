using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    public sealed partial class FighterCombatSystem {

        /// <summary>
        /// Meter and Defy update 2026-10-31 (plan section 2.5, P4): resolves the Defy blast of
        /// each fighter whose Defy clock stands on frame 6 and has not fired. Called at the very
        /// top of the combat pass, BEFORE grabs, activation strikes and the attack intents, so a
        /// stagger it lands cancels the victim's swing, grab and Ultimate wind-up before any of
        /// them acts, and a strike on the Defier the same tick whiffs (frame 6 is invulnerable).
        /// The blast never goes through <c>ApplyFighterHit</c> (see
        /// <see cref="FighterDefyBlastRules"/>). Player one's resolves first; two Defies on the
        /// same frame touch neither Defier (both are invulnerable), and a Defy started one tick
        /// or more later is caught in its vulnerable recovery.
        /// </summary>
        private static void ResolveDefyBlasts(ref Frame frame, EntityRef first, EntityRef second) {
            ResolveDefyBlast(ref frame, first, second);
            ResolveDefyBlast(ref frame, second, first);
        }

        private static void ResolveDefyBlast(ref Frame frame, EntityRef defierEntity, EntityRef targetEntity) {
            ref FighterActionStateComponent defierAction = ref frame.Get<FighterActionStateComponent>(defierEntity);
            if (!FighterDefyRules.BlastFiresThisTick(in defierAction)) return;
            ref FighterStateComponent defier = ref frame.Get<FighterStateComponent>(defierEntity);
            if (defier.Stocks <= 0) return;
            defierAction.ActionFlags |= FighterActionStateComponent.ActionFlagDefyBlastFired;
            FighterDefyRules.StepCounter(ref defierAction);

            ref FighterVerbComponent defierVerb = ref frame.Get<FighterVerbComponent>(defierEntity);
            FighterDefyBlastRules.Heal(ref defier, ref defierVerb);

            // The world freeze: 8 frames of hitstop on the Defier (and on a target it reaches).
            int freeze = FTT.Combat.BasicComboRules.DefyBlastHitstopFrames;
            if (defierVerb.HitstopFrames < freeze) defierVerb.HitstopFrames = freeze;

            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            if (!FighterDefyBlastRules.Reaches(in defier, in target)) return;
            ref FighterVerbComponent targetVerb = ref frame.Get<FighterVerbComponent>(targetEntity);
            FighterDefyBlastRules.ApplyToTarget(
                in defier,
                ref target,
                ref frame.Get<FighterRuntimeComponent>(targetEntity),
                ref targetVerb,
                ref frame.Get<FighterActionStateComponent>(targetEntity),
                ref frame.Get<FighterCounterComponent>(targetEntity),
                ref frame.Get<FighterKnockdownComponent>(targetEntity),
                ref frame.Get<FighterUltimateActivationComponent>(targetEntity));
            if (targetVerb.HitstopFrames < freeze) targetVerb.HitstopFrames = freeze;
        }
    }
}
