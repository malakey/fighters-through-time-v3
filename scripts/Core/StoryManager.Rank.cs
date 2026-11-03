using System.Collections.Generic;

namespace FTT.Core {

    /// <summary>
    /// The level rank (gameplay review §1.9): the attempt's damage tally and the dust bonus
    /// paid at completion, on top of the F05 ledger. The numbers live in
    /// <see cref="LevelRankRules"/>.
    /// </summary>
    public partial class StoryManager {
        /// <summary>The rank of the most recent completed level, or null (Level 0, a bare panel). Session-scoped.</summary>
        public LevelRankResult? LastLevelRank { get; private set; }

        /// <summary>The rank dust the most recent completion paid: what the letter is worth less what the level had paid.</summary>
        public int LastLevelRankBonusDust { get; private set; }

        /// <summary>This attempt's tally: authored-hit HP lost to discrete hits and hazard contacts.</summary>
        public float RankBaseDamage => _attempt?.RankBaseDamage ?? 0f;

        /// <summary>The discrete hits in <see cref="RankBaseDamage"/>.</summary>
        public int RankHits => _attempt?.RankHits ?? 0;

        /// <summary>
        /// Adds one discrete hit to the attempt's tally. Runs from a damage observation, possibly
        /// inside a physics flush, so it only adds numbers; the durable snapshot carries them.
        /// Nothing counts outside a live run, or while a scene load has the outgoing scene's
        /// hero in the tree (the new attempt is already minted by then).
        /// </summary>
        public void AddRankDamage(int appliedDamage) {
            if (appliedDamage <= 0 || !IsLevelTimerRunning || !HasLiveAttempt || IsSceneTransitionInProgress) return;
            _attempt.RankBaseDamage += LevelRankRules.BaseDamage(appliedDamage, StoryDifficultyTuning.CurrentStoryDifficulty);
            _attempt.RankHits++;
        }

        /// <summary>
        /// Evaluates the letter and pays its dust. Called after the tier bonus, so the tier
        /// percentage never compounds on rank dust (F05), and before the completion transaction,
        /// so the bonus banks with the wallet and the paid record rides the same save write.
        /// </summary>
        private void ApplyLevelRankBonus(string levelID) {
            LastLevelRank = null;
            LastLevelRankBonusDust = 0;
            if (!LevelRankRules.IsRankedLevel((int)CurrentLevel)) return;
            Difficulty difficulty = StoryDifficultyTuning.CurrentStoryDifficulty;
            LevelRankResult rank = LevelRankRules.Evaluate(
                _attempt.RankBaseDamage,
                GetSelectedCharacterMaximumHP(),
                LevelRankRules.SpeedIntegrity(_attempt.FinalGateIntegrity, LastLevelIntegrityPercent),
                difficulty,
                timed: ParSecondsForCurrentLevel > 0f);
            LastLevelRank = rank;
            StorySaveData save = GetActiveSave();
            int paid = save == null ? 0 : save.RankDustPaidByLevel.GetValueOrDefault(levelID);
            LastLevelRankBonusDust = LevelRankRules.DustOwed(rank.Dust, paid);
            // CollectDust, like the tier bonus: it banks with the wallet, and the level
            // controllers' category tallies never count it, so it keeps its own results line.
            CollectDust(LastLevelRankBonusDust);
            if (save == null) return;
            save.LevelChronalDust = ChronalDustCollected;
            save.RankByLevel[levelID] = rank.Letter.ToString();
            save.RankDustPaidByLevel[levelID] = paid + LastLevelRankBonusDust;
        }
    }
}
