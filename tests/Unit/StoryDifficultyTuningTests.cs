using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
public class StoryDifficultyTuningTests {
    [TestCase]
    public void EnemyHPMultipliersMatchTheUnifiedScalingTable() {
        AssertThat(StoryDifficultyTuning.GetEnemyHPMultiplier(Difficulty.Easy)).IsEqual(0.7f);
        AssertThat(StoryDifficultyTuning.GetEnemyHPMultiplier(Difficulty.Normal)).IsEqual(1.0f);
        AssertThat(StoryDifficultyTuning.GetEnemyHPMultiplier(Difficulty.Hard)).IsEqual(1.5f);
    }

    [TestCase]
    public void EnemyDamageMultipliersMatchTheUnifiedScalingTable() {
        AssertThat(StoryDifficultyTuning.GetEnemyDamageMultiplier(Difficulty.Easy)).IsEqual(0.5f);
        AssertThat(StoryDifficultyTuning.GetEnemyDamageMultiplier(Difficulty.Normal)).IsEqual(1.0f);
        AssertThat(StoryDifficultyTuning.GetEnemyDamageMultiplier(Difficulty.Hard)).IsEqual(1.5f);
    }

    [TestCase]
    public void ScaledEnemyHPRoundsAndNeverDropsBelowOne() {
        AssertThat(StoryDifficultyTuning.ScaleEnemyHP(50, Difficulty.Easy)).IsEqual(35);
        AssertThat(StoryDifficultyTuning.ScaleEnemyHP(50, Difficulty.Normal)).IsEqual(50);
        AssertThat(StoryDifficultyTuning.ScaleEnemyHP(50, Difficulty.Hard)).IsEqual(75);
        AssertThat(StoryDifficultyTuning.ScaleEnemyHP(1, Difficulty.Easy)).IsEqual(1);
        AssertThat(StoryDifficultyTuning.ScaleEnemyHP(0, Difficulty.Hard)).IsEqual(1);
    }

    [TestCase]
    public void ScaledEnemyDamageAppliesTheDifficultyMultiplier() {
        AssertThat(StoryDifficultyTuning.ScaleEnemyDamage(8f, Difficulty.Easy)).IsEqual(4f);
        AssertThat(StoryDifficultyTuning.ScaleEnemyDamage(8f, Difficulty.Normal)).IsEqual(8f);
        AssertThat(StoryDifficultyTuning.ScaleEnemyDamage(8f, Difficulty.Hard)).IsEqual(12f);
        AssertThat(StoryDifficultyTuning.ScaleEnemyDamage(-5f, Difficulty.Hard)).IsEqual(0f);
    }

    [TestCase]
    public void EncounterCountsScaleWithSpawnPressureAndKeepAtLeastOneEnemy() {
        // Easy trims 30 percent (floor), Hard adds 25 percent (round).
        AssertThat(StoryDifficultyTuning.ScaleEncounterCount(4, Difficulty.Easy)).IsEqual(2);
        AssertThat(StoryDifficultyTuning.ScaleEncounterCount(4, Difficulty.Normal)).IsEqual(4);
        AssertThat(StoryDifficultyTuning.ScaleEncounterCount(4, Difficulty.Hard)).IsEqual(5);
        AssertThat(StoryDifficultyTuning.ScaleEncounterCount(1, Difficulty.Easy)).IsEqual(1);
        AssertThat(StoryDifficultyTuning.ScaleEncounterCount(0, Difficulty.Hard)).IsEqual(0);
    }
}
