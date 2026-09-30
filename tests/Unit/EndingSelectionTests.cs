using System.Collections.Generic;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A3b — N05, the equal-weight ending average; rewritten by
/// Package 13 W2 for S27.
///
/// <para>Every timed mission contributes equally. Since S27 retired Level 4A the
/// counted set is exactly <b>fourteen</b> unique level IDs — Levels 2–15, with no
/// hero dependence — and the comparison is the <b>unrounded sum against 700
/// percentage points</b>, never a rounded average. A ten-point improvement
/// anywhere raises the average by 10/14 of a point, and no amount of display
/// rounding can change which of the two authored endings plays. A retired
/// <c>level_04a_*</c> record a v7 save still carries is dead data (plan D2).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EndingSelectionTests {

    private static readonly string[] CountedLevels = {
        "level_02_orleans", "level_03_chicago", "level_04_paris", "level_05_titanic",
        "level_06_pompeii", "level_07_nassau", "level_08_egypt", "level_09_berlin",
        "level_10_globe", "level_11_gettysburg", "level_12_lunar",
        "level_13_chronal_void", "level_14_neo_earth", "level_15_alexandria"
    };

    [TestCase]
    public void ExactlyFourteenLevelIDsAreCountedWithNoHeroDependence() {
        List<string> required = StoryManager.RequiredEndingLevelIDs();
        AssertThat(required.Count)
            .OverrideFailureMessage("Levels 2-15. Never a smaller denominator.")
            .IsEqual(14);
        foreach (string id in CountedLevels) AssertThat(required).Contains(id);

        // No retired Level 4A variant counts, for any hero.
        foreach (string id in required) {
            AssertThat(id.StartsWith("level_04a_"))
                .OverrideFailureMessage($"{id} is a retired Level 4A ID.")
                .IsFalse();
        }
        // Levels 0 and 1 are untimed and explicitly excluded.
        AssertThat(required.Contains("level_00_tutorial")).IsFalse();
        AssertThat(required.Contains("level_01_florence")).IsFalse();
    }

    [TestCase]
    public void TheComparisonIsTheUnroundedSumAgainstSevenHundred() {
        AssertThat(StoryManager.CleanEndingThresholdPoints).IsEqual(700f);

        // Exactly 50% across fourteen levels selects the clean restoration.
        StorySaveData exact = BuildSave("einstein", 50f);
        AssertThat(StoryManager.ResolveEndingPointTotal(exact)).IsEqual(700f);
        AssertThat(StoryManager.IsCleanRestorationEnding(exact))
            .OverrideFailureMessage("Exactly 50% is the clean ending.")
            .IsTrue();

        // A hair below sums to 699.x and must select the scarred ending even
        // though every level would DISPLAY as 50%.
        StorySaveData justUnder = BuildSave("einstein", 50f);
        justUnder.IntegrityByLevel["level_09_berlin"] = 49.6f;
        AssertThat(StoryManager.ResolveEndingPointTotal(justUnder) < 700f).IsTrue();
        AssertThat(StoryManager.IsCleanRestorationEnding(justUnder))
            .OverrideFailureMessage("Display rounding must never change the ending.")
            .IsFalse();
    }

    [TestCase]
    public void AMissingRecordContributesZeroAndARetiredLegacyRecordNeverCounts() {
        StorySaveData save = BuildSave("einstein", 60f);
        save.IntegrityByLevel.Remove("level_10_globe");
        // 13 x 60 = 780 by itself, but the fourteenth slot is a real zero: the
        // denominator is fixed at fourteen and a missing record is never guessed.
        AssertThat(StoryManager.ResolveEndingPointTotal(save)).IsEqual(780f);
        AssertThat(StoryManager.IsCleanRestorationEnding(save)).IsTrue();

        // A v7 save that completed its hero's Level 4A keeps that record as dead
        // data (plan D2): a perfect 100 there cannot rescue a scarred run.
        StorySaveData sparse = BuildSave("einstein", 50f);
        sparse.IntegrityByLevel.Remove("level_10_globe");
        sparse.IntegrityByLevel["level_04a_einstein"] = 100f;
        AssertThat(StoryManager.ResolveEndingPointTotal(sparse)).IsEqual(650f);
        AssertThat(StoryManager.IsCleanRestorationEnding(sparse))
            .OverrideFailureMessage("Thirteen good levels plus a dead 4A record are not a clean campaign.")
            .IsFalse();
    }

    [TestCase]
    public void TheSelectionIsIdenticalOnEveryDifficultyAndForEveryHero() {
        // The difficulty multiplier scales the drain, not the threshold: two runs
        // that end on the same recorded gauge get the same ending, whoever the hero.
        foreach (string hero in new[] { "einstein", "joan", "mozart" }) {
            StorySaveData clean = BuildSave(hero, 50f);
            StorySaveData scarred = BuildSave(hero, 49.9f);
            foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
                clean.Difficulty = difficulty;
                scarred.Difficulty = difficulty;
                AssertThat(StoryManager.IsCleanRestorationEnding(clean))
                    .OverrideFailureMessage($"{hero} on {difficulty} at 50% must reach the clean ending.")
                    .IsTrue();
                AssertThat(StoryManager.IsCleanRestorationEnding(scarred))
                    .OverrideFailureMessage($"{hero} on {difficulty} below 50% must reach the scarred ending.")
                    .IsFalse();
            }
        }
    }

    private static StorySaveData BuildSave(string heroID, float percentPerLevel) {
        var save = new StorySaveData { SelectedCharacterID = heroID };
        foreach (string id in StoryManager.RequiredEndingLevelIDs()) {
            save.IntegrityByLevel[id] = percentPerLevel;
        }
        return save;
    }
}
