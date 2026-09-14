using System.Collections.Generic;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A3b — N05, the equal-weight ending average.
///
/// <para>Every timed mission contributes equally. The counted set is exactly
/// fifteen unique level IDs — the fourteen shared Levels 2–15 plus the saved
/// hero's <b>one</b> Level 4A — and the comparison is the <b>unrounded sum
/// against 750 percentage points</b>, never a rounded average. A ten-point
/// improvement anywhere raises the average by 10/15 of a point, and no amount of
/// display rounding can change which of the two authored endings plays.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EndingSelectionTests {

    private static readonly string[] SharedLevels = {
        "level_02_orleans", "level_03_chicago", "level_04_paris", "level_05_titanic",
        "level_06_pompeii", "level_07_nassau", "level_08_egypt", "level_09_berlin",
        "level_10_globe", "level_11_gettysburg", "level_12_lunar",
        "level_13_chronal_void", "level_14_neo_earth", "level_15_alexandria"
    };

    [TestCase]
    public void ExactlyFifteenLevelIDsAreCountedAndTheOtherEightVariantsAreExcluded() {
        List<string> required = StoryManager.RequiredEndingLevelIDs("einstein");
        AssertThat(required.Count)
            .OverrideFailureMessage("Fourteen shared levels plus one 4A. Never a smaller denominator.")
            .IsEqual(15);
        foreach (string id in SharedLevels) AssertThat(required).Contains(id);

        AssertThat(required).Contains("level_04a_einstein");
        // The other eight variants are not the hero's campaign and contribute nothing.
        foreach (string hero in new[] { "joan", "leonardo", "lincoln", "cleopatra",
                                        "tesla", "shakespeare", "mozart", "pocahontas" }) {
            AssertThat(required.Contains($"level_04a_{hero}"))
                .OverrideFailureMessage($"level_04a_{hero} must not count toward einstein's ending.")
                .IsFalse();
        }
        // Levels 0 and 1 are untimed and explicitly excluded.
        AssertThat(required.Contains("level_00_tutorial")).IsFalse();
        AssertThat(required.Contains("level_01_florence")).IsFalse();

        // And the set follows the saved hero, not a hardcoded roster position.
        AssertThat(StoryManager.RequiredEndingLevelIDs("mozart")).Contains("level_04a_mozart");
    }

    [TestCase]
    public void TheComparisonIsTheUnroundedSumAgainstSevenHundredAndFifty() {
        AssertThat(StoryManager.CleanEndingThresholdPoints).IsEqual(750f);

        // Exactly 50% across fifteen levels selects the clean restoration.
        StorySaveData exact = BuildSave("einstein", 50f);
        AssertThat(StoryManager.ResolveEndingPointTotal(exact, "einstein")).IsEqual(750f);
        AssertThat(StoryManager.IsCleanRestorationEnding(exact, "einstein"))
            .OverrideFailureMessage("Exactly 50% is the clean ending.")
            .IsTrue();

        // A hair below sums to 749.x and must select the scarred ending even
        // though every level would DISPLAY as 50%.
        StorySaveData justUnder = BuildSave("einstein", 50f);
        justUnder.IntegrityByLevel["level_09_berlin"] = 49.6f;
        AssertThat(StoryManager.ResolveEndingPointTotal(justUnder, "einstein") < 750f).IsTrue();
        AssertThat(StoryManager.IsCleanRestorationEnding(justUnder, "einstein"))
            .OverrideFailureMessage("Display rounding must never change the ending.")
            .IsFalse();
    }

    [TestCase]
    public void AMissingRecordContributesZeroRatherThanShrinkingTheDenominator() {
        StorySaveData save = BuildSave("einstein", 60f);
        save.IntegrityByLevel.Remove("level_10_globe");
        // 14 x 60 = 840 by itself, but the fifteenth slot is a real zero: the
        // denominator is fixed at fifteen and a missing record is never guessed.
        AssertThat(StoryManager.ResolveEndingPointTotal(save, "einstein")).IsEqual(840f);
        AssertThat(StoryManager.IsCleanRestorationEnding(save, "einstein")).IsTrue();

        StorySaveData sparse = BuildSave("einstein", 50f);
        sparse.IntegrityByLevel.Remove("level_10_globe");
        AssertThat(StoryManager.ResolveEndingPointTotal(sparse, "einstein")).IsEqual(700f);
        AssertThat(StoryManager.IsCleanRestorationEnding(sparse, "einstein"))
            .OverrideFailureMessage("Fourteen good levels do not make a fifteen-level campaign.")
            .IsFalse();
    }

    [TestCase]
    public void TheSelectionIsIdenticalOnEveryDifficultyAndForEveryHero() {
        // The difficulty multiplier scales the drain, not the threshold: two runs
        // that end on the same recorded gauge get the same ending.
        foreach (string hero in new[] { "einstein", "joan", "pocahontas" }) {
            StorySaveData clean = BuildSave(hero, 50f);
            StorySaveData scarred = BuildSave(hero, 49.9f);
            foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
                clean.Difficulty = difficulty;
                scarred.Difficulty = difficulty;
                AssertThat(StoryManager.IsCleanRestorationEnding(clean, hero))
                    .OverrideFailureMessage($"{hero} on {difficulty} at 50% must reach the clean ending.")
                    .IsTrue();
                AssertThat(StoryManager.IsCleanRestorationEnding(scarred, hero))
                    .OverrideFailureMessage($"{hero} on {difficulty} below 50% must reach the scarred ending.")
                    .IsFalse();
            }
        }
    }

    private static StorySaveData BuildSave(string heroID, float percentPerLevel) {
        var save = new StorySaveData { SelectedCharacterID = heroID };
        foreach (string id in StoryManager.RequiredEndingLevelIDs(heroID)) {
            save.IntegrityByLevel[id] = percentPerLevel;
        }
        return save;
    }
}
