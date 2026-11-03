using System;
using System.Collections.Generic;

namespace FTT.Environment {

    /// <summary>
    /// Swarm waves (rulings 1b, 1b+, 1b++; numbers provisional,
    /// <c>PROVISIONAL-FODDER-STATS</c>). A swarm row is a manifest row and an
    /// <see cref="EncounterLockWave"/> whose whole table is fodder
    /// (<c>EnemyData.DustWeightZero</c>). Pure: fodder is a predicate the caller
    /// supplies, so a level owner's rows are checked without a scene.
    /// </summary>
    public static class SwarmWaveRules {
        /// <summary>A row's table is authored at the Hard truth; Easy's prefix is its 0.7 floor.</summary>
        public const int RowMinMobs = 6;
        public const int RowMaxMobs = 10;
        public const int RowDefaultMobs = 8;
        public const int RowLargeLockMobs = 10;
        /// <summary>A lock at least this wide, after carving, takes the large row.</summary>
        public const float LargeLockWidthPixels = 2800f;
        /// <summary>The wave's <c>WhenAliveAtMost</c> threshold.</summary>
        public const int TriggerAliveAtMost = 2;
        /// <summary>One row a level, two only on the largest locks (1b++).</summary>
        public const int MaxRowsPerLevel = 2;
        /// <summary>What a swarm level adds to its config's warm <c>standard_enemy</c> count (the 30-combatant cap stands).</summary>
        public const int WarmUpBonus = 6;
        /// <summary>The lowest warm count of a swarm level: the lowest level's 10 plus the bonus.</summary>
        public const int MinimumWarmOnSwarmLevel = 10 + WarmUpBonus;

        /// <summary>Levels with no swarm row (1b++): the tutorial, low gravity and the corridor grids.</summary>
        public static bool IsLevelExcluded(int levelIndex) => levelIndex is 0 or 12 or 13 or 14;

        /// <summary>The row length a lock of this width takes unless its author has a reason.</summary>
        public static int RowMobsForLockWidth(float widthPixels) =>
            widthPixels >= LargeLockWidthPixels ? RowLargeLockMobs : RowDefaultMobs;

        /// <summary>True when a table is non-empty and every entry is fodder.</summary>
        public static bool IsSwarmRow(IReadOnlyList<string> enemyIDs, Func<string, bool> isFodder) {
            if (enemyIDs == null || enemyIDs.Count == 0) return false;
            foreach (string enemyID in enemyIDs) {
                if (!isFodder(enemyID)) return false;
            }
            return true;
        }

        /// <summary>
        /// Every way one level's manifest rows break the swarm rules; empty when they
        /// hold. A row with some fodder and some not is an error: a swarm is all fodder.
        /// </summary>
        public static List<string> ValidateRows(int levelIndex,
            IReadOnlyList<(string WaveID, string[] EnemyIDs)> rows, Func<string, bool> isFodder) {
            var issues = new List<string>();
            int swarmRows = 0;
            foreach ((string waveID, string[] enemyIDs) in rows) {
                int fodder = 0;
                foreach (string enemyID in enemyIDs) {
                    if (isFodder(enemyID)) fodder++;
                }
                if (fodder == 0) continue;
                swarmRows++;
                if (fodder != enemyIDs.Length) issues.Add($"Level {levelIndex}: '{waveID}' mixes fodder with other mobs");
                if (enemyIDs.Length < RowMinMobs || enemyIDs.Length > RowMaxMobs) {
                    issues.Add($"Level {levelIndex}: '{waveID}' has {enemyIDs.Length} mobs, not {RowMinMobs}-{RowMaxMobs}");
                }
            }
            if (swarmRows > 0 && IsLevelExcluded(levelIndex)) issues.Add($"Level {levelIndex} takes no swarm row");
            if (swarmRows > MaxRowsPerLevel) issues.Add($"Level {levelIndex} has {swarmRows} swarm rows, at most {MaxRowsPerLevel}");
            return issues;
        }

        /// <summary>What is wrong with a swarm row's lock wave trigger, or null.</summary>
        public static string ValidateWaveTrigger(EncounterWaveTrigger trigger, int aliveAtMost) =>
            trigger == EncounterWaveTrigger.WhenAliveAtMost && aliveAtMost == TriggerAliveAtMost
                ? null
                : $"trigger {trigger}({aliveAtMost}), not WhenAliveAtMost({TriggerAliveAtMost})";

        /// <summary>What is wrong with a swarm level's warm <c>standard_enemy</c> count, or null.</summary>
        public static string ValidateWarmUp(int levelIndex, int standardWarmUp, int largestRow) {
            int needed = Math.Max(MinimumWarmOnSwarmLevel, largestRow);
            return standardWarmUp >= needed
                ? null
                : $"Level {levelIndex} warms {standardWarmUp} standard_enemy, needs at least {needed}";
        }
    }
}
