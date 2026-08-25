using System;

namespace FTT.Core {

    /// <summary>
    /// Story-only difficulty scaling from the Unified Difficulty Scaling Table
    /// (design Section 5). Applies to enemies, bosses, and spawn pressure at
    /// runtime; canonical base values in .tres resources stay unscaled and
    /// Fighter Mode never consults this class.
    /// </summary>
    public static class StoryDifficultyTuning {

        public static float GetEnemyHPMultiplier(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => 0.7f,
            Difficulty.Normal => 1.0f,
            Difficulty.Hard => 1.5f,
            _ => 1.0f
        };

        public static float GetEnemyDamageMultiplier(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => 0.5f,
            Difficulty.Normal => 1.0f,
            Difficulty.Hard => 1.5f,
            _ => 1.0f
        };

        public static float GetEnemySpawnRateMultiplier(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => 0.7f,
            Difficulty.Normal => 1.0f,
            Difficulty.Hard => 1.25f,
            _ => 1.0f
        };

        /// <summary>
        /// Reaction-delay pacing: Easy gives the player longer to read a windup,
        /// Hard shortens it. Applied to authored ReactionDelayMin/MaxFrames.
        /// </summary>
        public static float GetReactionDelayMultiplier(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => 1.5f,
            Difficulty.Normal => 1.0f,
            Difficulty.Hard => 0.6f,
            _ => 1.0f
        };

        /// <summary>
        /// Scales an authored reaction delay. Never returns less than one frame so
        /// an enemy always spends at least a tick reacting.
        /// </summary>
        public static int ScaleReactionDelayFrames(int baseFrames, Difficulty difficulty) =>
            Math.Max(1, (int)MathF.Round(Math.Max(0, baseFrames) * GetReactionDelayMultiplier(difficulty)));

        public static int ScaleEnemyHP(int baseHP, Difficulty difficulty) =>
            Math.Max(1, (int)MathF.Round(Math.Max(0, baseHP) * GetEnemyHPMultiplier(difficulty)));

        public static float ScaleEnemyDamage(float baseDamage, Difficulty difficulty) =>
            MathF.Max(0f, baseDamage) * GetEnemyDamageMultiplier(difficulty);

        /// <summary>
        /// Applies the spawn-rate multiplier to an authored encounter count.
        /// Easy trims mobs (floor), Hard adds pressure (round), and at least one
        /// enemy always remains so encounters cannot scale away entirely.
        /// </summary>
        public static int ScaleEncounterCount(int authoredCount, Difficulty difficulty) {
            if (authoredCount <= 0) return 0;
            float scaled = authoredCount * GetEnemySpawnRateMultiplier(difficulty);
            int result = difficulty == Difficulty.Easy
                ? (int)MathF.Floor(scaled)
                : (int)MathF.Round(scaled);
            return Math.Max(1, result);
        }

        /// <summary>
        /// Rally echo fraction multiplier (V7.1, Story only): Hard halves the
        /// reclaimable fraction of each hit taken; Easy and Normal keep the
        /// full 20-50% curve.
        /// </summary>
        public static float GetRallyEchoMultiplier(Difficulty difficulty) => difficulty switch {
            Difficulty.Hard => 0.5f,
            _ => 1.0f
        };

        // === Story Mode healing loop (V7.2 — surviving must beat dying) ===
        // Three authored recovery sources, all difficulty-scaled and capped at
        // max HP. Nothing here exists in Fighter Mode (whose recovery is Rally).

        /// <summary>Checkpoint Mending: fraction of max HP restored once when a
        /// Chronal Fracture activates (Easy 100% / Normal 50% / Hard 25%).</summary>
        public static float GetCheckpointMendingFraction(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => 1.0f,
            Difficulty.Hard => 0.25f,
            _ => 0.5f
        };

        /// <summary>Restoration Font potency per use (Easy/Normal 50% / Hard 25%).</summary>
        public static float GetRestorationFontFraction(Difficulty difficulty) =>
            difficulty == Difficulty.Hard ? 0.25f : 0.5f;

        /// <summary>Restoration Font uses per level (Easy 2 / Normal 1 / Hard 1).</summary>
        public static int GetRestorationFontUses(Difficulty difficulty) =>
            difficulty == Difficulty.Easy ? 2 : 1;

        /// <summary>Chronal Feast instant heal (Easy 50% / Normal 35% / Hard 20%).</summary>
        public static float GetChronalFeastFraction(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => 0.5f,
            Difficulty.Hard => 0.2f,
            _ => 0.35f
        };

        /// <summary>Chronal Feasts populated per level (Easy 3 / Normal 2 / Hard 1).
        /// The authored anchor spots exist once; difficulty selects how many fill.</summary>
        public static int GetChronalFeastCount(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => 3,
            Difficulty.Hard => 1,
            _ => 2
        };

        /// <summary>A fractional heal in HP, rounded, never negative.</summary>
        public static int ScaleHeal(int maxHP, float fraction) =>
            Math.Max(0, (int)MathF.Round(Math.Max(0, maxHP) * MathF.Max(0f, fraction)));

        public static Difficulty CurrentStoryDifficulty =>
            GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
    }
}
