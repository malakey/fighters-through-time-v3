using System;
using FTT.Characters;
using FTT.Combat;

namespace FTT.Core {

    /// <summary>The level rank's letters, worst first. <see cref="object.ToString"/> is the saved form.</summary>
    public enum LevelRankLetter {
        D = 0,
        C = 1,
        B = 2,
        A = 3,
        S = 4
    }

    /// <summary>One evaluated level rank, for the results panel and the save.</summary>
    public readonly struct LevelRankResult {
        public readonly LevelRankLetter Letter;
        public readonly double Points;
        /// <summary>The dust this letter is worth on this difficulty, before anything already paid.</summary>
        public readonly int Dust;
        /// <summary>HP bars lost, as a percentage of the hero's health bar. Can pass 100.</summary>
        public readonly int DamagePercent;
        /// <summary><see cref="LevelRankRules.SpeedPercentUntimed"/> on an untimed level.</summary>
        public readonly int SpeedPercent;

        public LevelRankResult(LevelRankLetter letter, double points, int dust, int damagePercent, int speedPercent) {
            Letter = letter;
            Points = points;
            Dust = dust;
            DamagePercent = damagePercent;
            SpeedPercent = speedPercent;
        }
    }

    /// <summary>
    /// The level rank (user rulings 3a, 3a+, 3a++, 3a+5, 3c, 3c+; gameplay review
    /// package §1.9). Levels 1–15 rank on damage avoided and speed, and the letter
    /// pays dust as a bonus on top of the F05 ledger. Plain C#, the one home of
    /// every rank number; nothing under <c>scripts/FighterSim/</c> reads it.
    ///
    /// <para>The dust table is the user's. Everything else here is provisional
    /// (<c>PROVISIONAL-LEVEL-RANK-RULES</c>).</para>
    ///
    /// <para>The speed score reads the Timeline Integrity gauge at the PreBoss gate,
    /// so a boss fight never counts toward it and there is one clock (3c+). The
    /// gauge is converted back into a share of the level's authored par, which is
    /// 1.0 at par on every difficulty, so the letters are difficulty-normalised
    /// (3a+5).</para>
    /// </summary>
    public static class LevelRankRules {
        public const int FirstRankedLevel = 1;
        public const int LastRankedLevel = 15;

        /// <summary>HP bars lost at which the damage score reaches zero.</summary>
        public const double BudgetBars = 1.5;

        public const double DamageWeight = 0.6;
        public const double SpeedWeight = 0.4;

        /// <summary>The share of authored par at which the speed score reaches zero.</summary>
        public const double SpeedZeroParUse = 1.125;
        /// <summary>The width of the speed band: full score at <c>SpeedZeroParUse − SpeedBandParUse</c>.</summary>
        public const double SpeedBandParUse = 0.9;

        public const double SThreshold = 90.0;
        public const double AThreshold = 75.0;
        public const double BThreshold = 55.0;
        public const double CThreshold = 35.0;

        /// <summary><see cref="LevelRankResult.SpeedPercent"/> on a level with no clock.</summary>
        public const int SpeedPercentUntimed = -1;

        // Ruling 3a+5, indexed by LevelRankLetter (D, C, B, A, S).
        private static readonly int[] StandardDust = { 0, 8, 20, 35, 50 };
        private static readonly int[] EasyDust = { 0, 4, 10, 18, 25 };

        /// <summary>The most one level can pay, on Normal and Hard.</summary>
        public static int MaximumDustPerLevel => StandardDust[(int)LevelRankLetter.S];

        /// <summary>Level 0 is scripted and pays nothing; a retired or unknown ordinal never ranks.</summary>
        public static bool IsRankedLevel(int levelNumber) =>
            levelNumber >= FirstRankedLevel && levelNumber <= LastRankedLevel;

        /// <summary>
        /// Whether one <see cref="PlayerController.DamageTaken"/> observation feeds the
        /// tally: a discrete hit or hazard contact, never a status tick or a periodic pulse.
        /// </summary>
        public static bool CountsTowardTally(DamageObservationKind kind, HitDelivery delivery) =>
            kind != DamageObservationKind.Status && delivery != HitDelivery.Tick;

        /// <summary>HP lost, taken back to the authored hit, so equal hits score equally on every difficulty.</summary>
        public static float BaseDamage(int appliedDamage, Difficulty difficulty) =>
            Math.Max(0, appliedDamage) / StoryDifficultyTuning.GetEnemyDamageMultiplier(difficulty);

        /// <summary>The N05 rule: the gauge frozen at the PreBoss gate, else the gauge at completion.</summary>
        public static float SpeedIntegrity(float finalGateIntegrity, float lastLevelIntegrity) =>
            finalGateIntegrity >= 0f ? finalGateIntegrity : lastLevelIntegrity;

        public static double DamageScore(float baseDamage, int maxHP) =>
            100.0 * Math.Clamp(1.0 - Bars(baseDamage, maxHP) / BudgetBars, 0.0, 1.0);

        /// <summary>The share of authored par the gauge has used: 1.0 at par on every difficulty.</summary>
        public static double ParUse(float integrityPercent, Difficulty difficulty) =>
            (TimelineIntegrityRules.StartPercent - (double)integrityPercent) / TimelineIntegrityRules.StartPercent
                * TimelineIntegrityRules.DifficultyTimeMultiplier(difficulty)
                * TimelineIntegrityRules.ProvisionalParScale;

        public static double SpeedScore(float integrityPercent, Difficulty difficulty) =>
            100.0 * Math.Clamp((SpeedZeroParUse - ParUse(integrityPercent, difficulty)) / SpeedBandParUse, 0.0, 1.0);

        public static LevelRankLetter LetterFor(double points) {
            if (points >= SThreshold) return LevelRankLetter.S;
            if (points >= AThreshold) return LevelRankLetter.A;
            if (points >= BThreshold) return LevelRankLetter.B;
            if (points >= CThreshold) return LevelRankLetter.C;
            return LevelRankLetter.D;
        }

        public static int DustFor(LevelRankLetter letter, Difficulty difficulty) =>
            (difficulty == Difficulty.Easy ? EasyDust : StandardDust)[(int)letter];

        /// <summary>A rank pays only what the level has not paid yet.</summary>
        public static int DustOwed(int rankDust, int alreadyPaid) => Math.Max(0, rankDust - alreadyPaid);

        public static LevelRankResult Evaluate(
            float baseDamage, int maxHP, float integrityPercent, Difficulty difficulty, bool timed) {
            double damage = DamageScore(baseDamage, maxHP);
            double speed = timed ? SpeedScore(integrityPercent, difficulty) : 0.0;
            double points = timed ? DamageWeight * damage + SpeedWeight * speed : damage;
            LevelRankLetter letter = LetterFor(points);
            return new LevelRankResult(
                letter,
                points,
                DustFor(letter, difficulty),
                (int)Math.Round(Bars(baseDamage, maxHP) * 100.0, MidpointRounding.AwayFromZero),
                timed ? (int)Math.Round(speed, MidpointRounding.AwayFromZero) : SpeedPercentUntimed);
        }

        private static double Bars(float baseDamage, int maxHP) => Math.Max(0f, baseDamage) / (double)Math.Max(1, maxHP);
    }
}
