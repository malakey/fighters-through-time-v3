using System;

namespace FTT.Combat {

    /// <summary>What a hazard impact does to a mob, by kind. Bosses are never a class here: they never qualify.</summary>
    public enum HazardMobClass { Standard, Fodder, Elite }

    /// <summary>
    /// Hazards against launched or thrown mobs (gameplay review 2026-11-03, ruling 7d, plan
    /// section 1.13). Pure, Story only: nothing under <c>scripts/FighterSim/</c> reads it. Every
    /// number here is provisional. <c>EnemyController.Hazard.cs</c> is the one reader that applies it.
    /// </summary>
    public static class HazardMobRules {
        /// <summary>A Standard mob loses this many HP, or dies if it has no more.</summary>
        public const int StandardImpactDamage = 30;
        /// <summary>An elite loses this share of its max HP, and one impact never kills it.</summary>
        public const float EliteImpactMaxHPFraction = 0.25f;
        /// <summary>The swarm wave's fodder mob (plan section 1.11), which any impact kills.</summary>
        public const string FodderEnemyID = "chrono_swarmling";

        /// <summary>The single statement of which mob is fodder.</summary>
        public static bool IsFodder(string enemyID) => enemyID == FodderEnemyID;

        /// <summary>
        /// A mob a hazard can hurt: thrown, or in a launch or finisher flight, and neither dormant
        /// nor dead. A walking, merely stunned or Defy-pushed mob carries none of the three.
        /// </summary>
        public static bool IsVulnerable(
            bool thrownFlight, bool wallBounceArmed, bool finisherFlight, bool dormant, bool alive) =>
            (thrownFlight || wallBounceArmed || finisherFlight) && !dormant && alive;

        public static HazardMobClass Classify(bool isElite, string enemyID) =>
            isElite ? HazardMobClass.Elite
            : IsFodder(enemyID) ? HazardMobClass.Fodder
            : HazardMobClass.Standard;

        /// <summary>The HP one impact takes, never more than the mob has and, for an elite, never its last.</summary>
        public static int ImpactDamage(HazardMobClass mobClass, int currentHP, int maxHP) {
            int hp = Math.Max(0, currentHP);
            return mobClass switch {
                HazardMobClass.Fodder => hp,
                HazardMobClass.Elite => Math.Clamp(
                    (int)MathF.Round(Math.Max(0, maxHP) * EliteImpactMaxHPFraction, MidpointRounding.AwayFromZero),
                    0, Math.Max(0, hp - 1)),
                _ => Math.Min(StandardImpactDamage, hp)
            };
        }
    }
}
