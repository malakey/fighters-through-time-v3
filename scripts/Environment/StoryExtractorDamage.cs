using FTT.Characters;

namespace FTT.Environment {

    /// <summary>
    /// Package 11 A4 (Resonance V7.6). The single Story-only resolver for the
    /// three Major "extra damage to Chronal Extractors" riders and the generic
    /// <c>ExtractorDamage</c> lane.
    ///
    /// <para>V7.6 hygiene: a Resonance grid may only touch the Timeline
    /// Integrity timer INDIRECTLY, and bonus damage to an Extractor is the one
    /// sanctioned channel. No minor authors the lane — the three riders are all
    /// Majors — so this resolver is the whole surface.</para>
    ///
    /// <para>Pure: no Godot types, no scene tree, no simulation reach. Nothing
    /// in <c>scripts/FighterSim/</c> calls it and nothing may.</para>
    /// </summary>
    public static class StoryExtractorDamage {

        /// <summary>Leonardo's Clockwork Overdrive: turret bolts deal double to Extractors.</summary>
        public const float ClockworkOverdriveExtractorMultiplier = 2.0f;

        /// <summary>Lincoln's Kinetic Splitting: +50% to Extractors and enemy constructs.</summary>
        public const float KineticSplittingStructureMultiplier = 1.5f;

        public const string ClockworkTurretAttackID = "leonardo_clockwork_turret";
        public const string SplittingStrikeAttackID = "lincoln_splitting_strike";

        /// <summary>
        /// The damage multiplier a Chronal Extractor applies to an incoming
        /// player hit. Returns 1.0 for a null attacker (an enemy, a hazard, a
        /// Fighter-mode body) so the isolation boundary is a no-op by default.
        /// </summary>
        public static float ExtractorMultiplier(PlayerController attacker, string attackID) {
            if (attacker == null) return 1f;
            float multiplier = attacker.StoryExtractorDamageMultiplier;
            if (attacker.HasStoryPerk(Characters.Abilities.LeonardoClockworkTurret.ClockworkOverdrivePerkKey)
                && attackID == ClockworkTurretAttackID) {
                multiplier *= ClockworkOverdriveExtractorMultiplier;
            }
            if (attacker.HasStoryPerk(Characters.Abilities.LincolnSplittingStrike.KineticSplittingPerkKey)
                && attackID == SplittingStrikeAttackID) {
                multiplier *= KineticSplittingStructureMultiplier;
            }
            return multiplier;
        }

        /// <summary>
        /// The damage multiplier an enemy CONSTRUCT applies to an incoming
        /// player hit. Only Lincoln's Kinetic Splitting rider reaches
        /// constructs; the Extractor-only lane and Leonardo's bolt rider do
        /// not.
        /// </summary>
        public static float ConstructMultiplier(PlayerController attacker, string attackID) {
            if (attacker == null) return 1f;
            return attacker.HasStoryPerk(Characters.Abilities.LincolnSplittingStrike.KineticSplittingPerkKey)
                && attackID == SplittingStrikeAttackID
                ? KineticSplittingStructureMultiplier
                : 1f;
        }
    }
}
