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

        public static Difficulty CurrentStoryDifficulty =>
            GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
    }
}
