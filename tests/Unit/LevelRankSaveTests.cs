using FTT.Core;
using GdUnit4;
using Newtonsoft.Json;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The level rank's save fields (gameplay review §1.7, §1.9): additive, on schema v8, with no
/// derivation. Pure C#: a save payload stays engine-free.
/// </summary>
[TestSuite]
public class LevelRankSaveTests {
    [TestCase]
    public void TheRankFieldsRoundTripOnTheCurrentSchema() {
        var save = new StorySaveData { SelectedCharacterID = "joan" };
        save.RankByLevel["level_02_orleans"] = "A";
        save.RankDustPaidByLevel["level_02_orleans"] = 35;
        save.AttemptState = StoryAttemptState.CreateFresh("level_03_chicago", 0);
        save.AttemptState.RankBaseDamage = 41.5f;
        save.AttemptState.RankHits = 7;
        save.AttemptState.CompletionTransaction.RankBonusDust = 20;

        StorySaveData loaded = SaveSchemaMigrator.DeserializeStory(JsonConvert.SerializeObject(save));

        AssertThat(SaveSchemaMigrator.CurrentVersion)
            .OverrideFailureMessage("The rank fields are additive: the schema stays v8.")
            .IsEqual(8);
        AssertThat(loaded.RankByLevel["level_02_orleans"]).IsEqual("A");
        AssertThat(loaded.RankDustPaidByLevel["level_02_orleans"]).IsEqual(35);
        AssertThat(loaded.AttemptState.RankBaseDamage).IsEqual(41.5f);
        AssertThat(loaded.AttemptState.RankHits).IsEqual(7);
        AssertThat(loaded.AttemptState.CompletionTransaction.RankBonusDust).IsEqual(20);
        AssertThat(loaded.RatingByLevel.Count)
            .OverrideFailureMessage("The retired rating stays dead: the rank never writes it.")
            .IsEqual(0);
    }

    [TestCase]
    public void AnOlderPayloadLoadsWithAnEmptyRankAndNullFieldsAreRepaired() {
        var older = SaveSchemaMigrator.DeserializeStory(
            "{\"SaveVersion\":8,\"SelectedCharacterID\":\"tesla\",\"RankByLevel\":null,\"RankDustPaidByLevel\":null}");
        AssertThat(older.RankByLevel).IsNotNull();
        AssertThat(older.RankByLevel.Count).IsEqual(0);
        AssertThat(older.RankDustPaidByLevel).IsNotNull();
        AssertThat(older.RankDustPaidByLevel.Count).IsEqual(0);

        var attempt = new StoryAttemptState { RankBaseDamage = -3f, RankHits = -2 };
        attempt.CompletionTransaction.RankBonusDust = -1;
        attempt.Normalize();
        AssertThat(attempt.RankBaseDamage).IsEqual(0f);
        AssertThat(attempt.RankHits).IsEqual(0);
        AssertThat(attempt.CompletionTransaction.RankBonusDust).IsEqual(0);
    }
}
