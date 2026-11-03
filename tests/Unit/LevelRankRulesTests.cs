using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The level rank's rulebook (gameplay review §1.9; rulings 3a+5, 3c, 3c+). Pure C#:
/// the letters are difficulty-normalised, the dust table is the user's, and the speed
/// score reads the Integrity gauge as a share of authored par.
/// </summary>
[TestSuite]
public class LevelRankRulesTests {
    private const double Tolerance = 0.01;

    private static void AssertNear(double actual, double expected, string what) =>
        AssertThat(System.Math.Abs(actual - expected) <= Tolerance)
            .OverrideFailureMessage($"{what}: expected {expected}, read {actual}.")
            .IsTrue();

    /// <summary>The gauge after exactly authored par at full drain, on this difficulty.</summary>
    private static float IntegrityAtPar(Difficulty difficulty) =>
        100f - 100f / (TimelineIntegrityRules.DifficultyTimeMultiplier(difficulty) * TimelineIntegrityRules.ProvisionalParScale);

    [TestCase]
    public void TheDamageScoreRunsFromFullToZeroAcrossTheBudgetAndStopsThere() {
        AssertNear(LevelRankRules.DamageScore(0f, 100), 100.0, "no damage");
        AssertNear(LevelRankRules.DamageScore(75f, 100), 50.0, "half the 1.5 bar budget");
        AssertNear(LevelRankRules.DamageScore(150f, 100), 0.0, "the whole budget");
        AssertNear(LevelRankRules.DamageScore(900f, 100), 0.0, "past the budget");
    }

    [TestCase]
    public void TheSameShareOfAHealthBarScoresTheSameOnEveryHero() {
        // Mozart (85 HP) and Lincoln (130 HP) losing half a bar each.
        AssertNear(LevelRankRules.DamageScore(42.5f, 85), LevelRankRules.DamageScore(65f, 130), "half a bar");
        AssertNear(LevelRankRules.DamageScore(65f, 130), 100.0 * (1.0 - 0.5 / LevelRankRules.BudgetBars), "half a bar's score");
    }

    [TestCase]
    public void BaseDamageTakesTheDifficultyMultiplierBackOffAHit() {
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
            int applied = (int)(30f * StoryDifficultyTuning.GetEnemyDamageMultiplier(difficulty));
            AssertNear(LevelRankRules.BaseDamage(applied, difficulty), 30.0, $"a 30 HP hit on {difficulty}");
        }
    }

    [TestCase]
    public void ParUseIsOneAtAuthoredParOnEveryDifficulty() {
        // The corrected formula (plan §1.9): the dossier's band was not normalised.
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
            AssertNear(LevelRankRules.ParUse(IntegrityAtPar(difficulty), difficulty), 1.0, $"par on {difficulty}");
        }
        AssertNear(LevelRankRules.ParUse(100f, Difficulty.Normal), 0.0, "a full gauge");
    }

    [TestCase]
    public void TheSpeedBandOnNormalIsNinetyFullAndFiftyEmpty() {
        AssertNear(LevelRankRules.SpeedScore(90f, Difficulty.Normal), 100.0, "90 %");
        AssertNear(LevelRankRules.SpeedScore(70f, Difficulty.Normal), 50.0, "70 %");
        AssertNear(LevelRankRules.SpeedScore(50f, Difficulty.Normal), 0.0, "50 %");
        AssertNear(LevelRankRules.SpeedScore(100f, Difficulty.Normal), 100.0, "a full gauge stays at full");
        AssertNear(LevelRankRules.SpeedScore(10f, Difficulty.Normal), 0.0, "a nearly empty gauge stays at zero");
    }

    [TestCase]
    public void AParRunScoresTheSameSpeedOnEveryDifficulty() {
        double expected = (LevelRankRules.SpeedZeroParUse - 1.0) / LevelRankRules.SpeedBandParUse * 100.0;
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
            AssertNear(LevelRankRules.SpeedScore(IntegrityAtPar(difficulty), difficulty), expected, $"par on {difficulty}");
        }
    }

    [TestCase]
    public void TimedPointsWeighDamageSixtyAndSpeedForty() {
        // Half the budget lost (50) at 70 % Integrity on Normal (50) is 50 points;
        // no damage (100) on an empty speed band (0) is 60.
        AssertNear(LevelRankRules.Evaluate(75f, 100, 70f, Difficulty.Normal, timed: true).Points, 50.0, "both halves");
        AssertNear(LevelRankRules.Evaluate(0f, 100, 50f, Difficulty.Normal, timed: true).Points, 60.0, "damage only earned");
        AssertNear(LevelRankRules.Evaluate(150f, 100, 90f, Difficulty.Normal, timed: true).Points, 40.0, "speed only earned");
    }

    [TestCase]
    public void AnUntimedLevelRanksOnDamageAloneAndCanStillReachS() {
        LevelRankResult clean = LevelRankRules.Evaluate(0f, 100, 0f, Difficulty.Normal, timed: false);
        AssertThat(clean.Letter).IsEqual(LevelRankLetter.S);
        AssertThat(clean.SpeedPercent).IsEqual(LevelRankRules.SpeedPercentUntimed);
        AssertNear(LevelRankRules.Evaluate(75f, 100, 0f, Difficulty.Normal, timed: false).Points, 50.0, "half the budget");
    }

    [TestCase]
    public void TheLetterLinesAreInclusive() {
        AssertThat(LevelRankRules.LetterFor(100.0)).IsEqual(LevelRankLetter.S);
        AssertThat(LevelRankRules.LetterFor(90.0)).IsEqual(LevelRankLetter.S);
        AssertThat(LevelRankRules.LetterFor(89.99)).IsEqual(LevelRankLetter.A);
        AssertThat(LevelRankRules.LetterFor(75.0)).IsEqual(LevelRankLetter.A);
        AssertThat(LevelRankRules.LetterFor(74.99)).IsEqual(LevelRankLetter.B);
        AssertThat(LevelRankRules.LetterFor(55.0)).IsEqual(LevelRankLetter.B);
        AssertThat(LevelRankRules.LetterFor(54.99)).IsEqual(LevelRankLetter.C);
        AssertThat(LevelRankRules.LetterFor(35.0)).IsEqual(LevelRankLetter.C);
        AssertThat(LevelRankRules.LetterFor(34.99)).IsEqual(LevelRankLetter.D);
        AssertThat(LevelRankRules.LetterFor(0.0)).IsEqual(LevelRankLetter.D);
    }

    [TestCase]
    public void NormalAndHardPayTheFullTable() {
        foreach (Difficulty difficulty in new[] { Difficulty.Normal, Difficulty.Hard }) {
            AssertThat(LevelRankRules.DustFor(LevelRankLetter.S, difficulty)).IsEqual(50);
            AssertThat(LevelRankRules.DustFor(LevelRankLetter.A, difficulty)).IsEqual(35);
            AssertThat(LevelRankRules.DustFor(LevelRankLetter.B, difficulty)).IsEqual(20);
            AssertThat(LevelRankRules.DustFor(LevelRankLetter.C, difficulty)).IsEqual(8);
            AssertThat(LevelRankRules.DustFor(LevelRankLetter.D, difficulty)).IsEqual(0);
        }
    }

    [TestCase]
    public void EasyPaysItsOwnTable() {
        AssertThat(LevelRankRules.DustFor(LevelRankLetter.S, Difficulty.Easy)).IsEqual(25);
        AssertThat(LevelRankRules.DustFor(LevelRankLetter.A, Difficulty.Easy)).IsEqual(18);
        AssertThat(LevelRankRules.DustFor(LevelRankLetter.B, Difficulty.Easy)).IsEqual(10);
        AssertThat(LevelRankRules.DustFor(LevelRankLetter.C, Difficulty.Easy)).IsEqual(4);
        AssertThat(LevelRankRules.DustFor(LevelRankLetter.D, Difficulty.Easy)).IsEqual(0);
    }

    [TestCase]
    public void ARankPaysOnlyWhatTheLevelHasNotPaid() {
        AssertThat(LevelRankRules.DustOwed(50, 0)).IsEqual(50);
        AssertThat(LevelRankRules.DustOwed(50, 35)).IsEqual(15);
        AssertThat(LevelRankRules.DustOwed(50, 50)).IsEqual(0);
        AssertThat(LevelRankRules.DustOwed(20, 35)).IsEqual(0);
    }

    [TestCase]
    public void OnlyLevelsOneToFifteenRankAndTheyCeilingAtSevenHundredFifty() {
        AssertThat(LevelRankRules.IsRankedLevel(-1)).IsFalse();
        AssertThat(LevelRankRules.IsRankedLevel(0)).IsFalse();
        AssertThat(LevelRankRules.IsRankedLevel(16)).IsFalse();
        int ceiling = 0;
        for (int level = 0; level <= 16; level++) {
            if (LevelRankRules.IsRankedLevel(level)) ceiling += LevelRankRules.MaximumDustPerLevel;
        }
        AssertThat(ceiling).IsEqual(750);
    }

    [TestCase]
    public void TheTallyHearsDiscreteHitsAndTheSpeedReadsTheGateWithTheN05Fallback() {
        AssertThat(LevelRankRules.CountsTowardTally(DamageObservationKind.Hit, HitDelivery.DirectHit)).IsTrue();
        AssertThat(LevelRankRules.CountsTowardTally(DamageObservationKind.Hit, HitDelivery.Construct)).IsTrue();
        AssertThat(LevelRankRules.CountsTowardTally(DamageObservationKind.Hazard, HitDelivery.Hazard)).IsTrue();
        AssertThat(LevelRankRules.CountsTowardTally(DamageObservationKind.Hit, HitDelivery.Tick)).IsFalse();
        AssertThat(LevelRankRules.CountsTowardTally(DamageObservationKind.Hazard, HitDelivery.Tick)).IsFalse();
        AssertThat(LevelRankRules.CountsTowardTally(DamageObservationKind.Status, HitDelivery.Tick)).IsFalse();

        AssertThat(LevelRankRules.SpeedIntegrity(70f, 40f)).IsEqual(70f);
        AssertThat(LevelRankRules.SpeedIntegrity(0f, 40f)).IsEqual(0f);
        AssertThat(LevelRankRules.SpeedIntegrity(-1f, 40f)).IsEqual(40f);
    }
}
