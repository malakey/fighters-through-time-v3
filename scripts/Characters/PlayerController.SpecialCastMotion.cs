using FTT.Combat;

namespace FTT.Characters {

    /// <summary>
    /// The Q1 ruling (combat and movement update 2026-10-16, plan §2.1.6), Story
    /// half: a Special cast's horizontal motion follows one rule in both modes.
    /// </summary>
    public partial class PlayerController {

        /// <summary>
        /// A GROUNDED cast brakes its horizontal velocity to zero over the
        /// run-stop ramp — the same per-tick step every Story stop site takes
        /// (<see cref="DecelerateHorizontal"/>, from
        /// <c>UniversalMovementRules.RunDecelerationFrames</c>, 4 frames) — so a
        /// running caster stops instead of sliding at its entry speed for the
        /// whole cast. An AIRBORNE cast keeps its drift (no stick, no added
        /// friction). A kit that writes its own velocity owns the tick:
        /// Divine Piercing's lunge through its active frames. The Fighter sim
        /// applies the same rule (<c>FighterMovementSystem.CastKeepsAirDrift</c>).
        /// Called from <c>ProcessUsingSpecial</c> beside the aerial-gravity line.
        /// </summary>
        private void ApplySpecialCastBrake(BaseSpecial ability, float dt) {
            if (!IsOnFloor()) return;
            if (SpecialCastDrivesOwnMotion(ability)) return;
            DecelerateHorizontal(dt);
        }

        /// <summary>The Story Specials that write their caster's horizontal velocity themselves, while they do.</summary>
        private static bool SpecialCastDrivesOwnMotion(BaseSpecial ability) =>
            ability is Abilities.JoanDivinePiercing { CurrentPhase: AbilityPhase.Active };
    }
}
