using System.Collections.Generic;
using FTT.Core;
using GdUnit4;
using Newtonsoft.Json;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 Phase C — the single v6 → v7 step. It composes exactly the three
/// derivations the workstreams declared: W2's H02 completion reconciliation
/// (<see cref="StoryAttemptState.ReconcileUndepositedCompletion"/>), W6's
/// <see cref="CampaignDifficultyRules.SeedLowestDifficultyUsed"/> and W5's
/// <see cref="SavedMatchSettings.DeriveStageHazardsEnabled"/>. Every fixture is
/// a v6 JSON payload. Nothing here reads a resource, but the suite keeps the
/// Godot runtime so that a future step which does cannot turn it into failure
/// signature 7.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SaveSchemaV7MigrationTests {
    private static string Json(object payload) => JsonConvert.SerializeObject(payload);

    [TestCase]
    public void AVersionSixCompletionShapeBanksItsWalletOnceAndHeldDustNeverBanks() {
        // The v6 post-completion shape: an unminted attempt, no checkpoint and a
        // positive wallet, which is what SaveLevelCompletion left for the hub to bank.
        StorySaveData unminted = SaveSchemaMigrator.DeserializeStory(Json(new {
            SaveVersion = 6,
            SelectedCharacterID = "tesla",
            LevelChronalDust = 120,
            DepositedChronalDust = new Dictionary<string, int> { ["tesla"] = 50 }
        }), out int loaded);
        AssertThat(loaded).IsEqual(6);
        AssertThat(unminted.SaveVersion).IsEqual(7);
        AssertThat(unminted.LevelChronalDust).IsEqual(0);
        AssertThat(unminted.DepositedChronalDust["tesla"]).IsEqual(170);

        // A minted record whose completion committed but never banked.
        var completed = StoryAttemptState.CreateFresh("level_03_chicago", 0);
        completed.Status = StoryAttemptStatus.Completed;
        StorySaveData banked = SaveSchemaMigrator.DeserializeStory(Json(new {
            SaveVersion = 6,
            SelectedCharacterID = "joan",
            LevelChronalDust = 30,
            AttemptState = completed
        }));
        AssertThat(banked.LevelChronalDust).IsEqual(0);
        AssertThat(banked.DepositedChronalDust["joan"]).IsEqual(30);
        AssertThat(banked.AttemptState.CompletionTransaction.DepositAmount).IsEqual(30);
        AssertThat(banked.AttemptState.CompletionTransaction.Applied).IsTrue();

        // A Collapse parked in the hub: the open attempt's held dust stays held.
        var collapsed = StoryAttemptState.CreateFresh("level_05_titanic", 0);
        collapsed.Status = StoryAttemptStatus.AwaitingHubResume;
        StorySaveData held = SaveSchemaMigrator.DeserializeStory(Json(new {
            SaveVersion = 6,
            SelectedCharacterID = "joan",
            LevelChronalDust = 80,
            AttemptState = collapsed
        }));
        AssertThat(held.LevelChronalDust).IsEqual(80);
        AssertThat(held.DepositedChronalDust.ContainsKey("joan")).IsFalse();

        // Parked mid-level: live attempt dust is never banked either.
        StorySaveData parked = SaveSchemaMigrator.DeserializeStory(Json(new {
            SaveVersion = 6,
            SelectedCharacterID = "joan",
            LevelChronalDust = 45,
            LastCheckpointID = "level_07_nassau_checkpoint_1"
        }));
        AssertThat(parked.LevelChronalDust).IsEqual(45);
    }

    [TestCase]
    public void AVersionSixStoryPayloadSeedsLowestDifficultyUsedFromItsDifficulty() {
        // Written before G14: the field is absent and would read as Hard.
        StorySaveData easy = SaveSchemaMigrator.DeserializeStory(Json(new {
            SaveVersion = 6, SelectedCharacterID = "mozart", Difficulty = (int)Difficulty.Easy
        }));
        AssertThat(easy.LowestDifficultyUsed).IsEqual(Difficulty.Easy);

        StorySaveData normal = SaveSchemaMigrator.DeserializeStory(Json(new {
            SaveVersion = 6, SelectedCharacterID = "mozart", Difficulty = (int)Difficulty.Normal
        }));
        AssertThat(normal.LowestDifficultyUsed).IsEqual(Difficulty.Normal);

        // Written after W6 with a lowered record: the seed never raises it.
        StorySaveData lowered = SaveSchemaMigrator.DeserializeStory(Json(new {
            SaveVersion = 6, SelectedCharacterID = "mozart",
            Difficulty = (int)Difficulty.Normal, LowestDifficultyUsed = (int)Difficulty.Easy
        }));
        AssertThat(lowered.LowestDifficultyUsed).IsEqual(Difficulty.Easy);
        AssertThat(lowered.Difficulty).IsEqual(Difficulty.Normal);
    }

    [TestCase]
    public void AVersionSixGlobalPayloadDerivesTheHazardToggleFromTheRetiredRate() {
        GlobalSaveData off = SaveSchemaMigrator.DeserializeGlobal(
            "{\"SaveVersion\":6,\"LastMatchSettings\":{\"Saved\":true,\"HazardRate\":0}}");
        AssertThat(off.SaveVersion).IsEqual(7);
        AssertThat(off.LastMatchSettings.StageHazardsEnabled == false).IsTrue();

        foreach (int rate in new[] { 1, 2, 3, 9 }) {
            GlobalSaveData on = SaveSchemaMigrator.DeserializeGlobal(
                "{\"SaveVersion\":6,\"LastMatchSettings\":{\"Saved\":true,\"HazardRate\":" + rate + "}}");
            AssertThat(on.LastMatchSettings.StageHazardsEnabled == true).IsTrue();
        }

        // An explicit toggle already on the payload always wins.
        GlobalSaveData explicitOff = SaveSchemaMigrator.DeserializeGlobal(
            "{\"SaveVersion\":6,\"LastMatchSettings\":{\"Saved\":true,\"HazardRate\":3,\"StageHazardsEnabled\":false}}");
        AssertThat(explicitOff.LastMatchSettings.StageHazardsEnabled == false).IsTrue();

        // No match settings at all: the defaults (hazards on) are built.
        GlobalSaveData missing = SaveSchemaMigrator.DeserializeGlobal("{\"SaveVersion\":6}");
        AssertThat(missing.LastMatchSettings.StageHazardsEnabled == true).IsTrue();
    }

    [TestCase]
    public void TheVersionSevenStepIsIdempotentAcrossARewrite() {
        StorySaveData first = SaveSchemaMigrator.DeserializeStory(Json(new {
            SaveVersion = 6,
            SelectedCharacterID = "cleopatra",
            Difficulty = (int)Difficulty.Normal,
            LevelChronalDust = 64,
            DepositedChronalDust = new Dictionary<string, int> { ["cleopatra"] = 10 }
        }));
        AssertThat(first.DepositedChronalDust["cleopatra"]).IsEqual(74);

        // Saving and reloading the migrated payload runs nothing again.
        StorySaveData second = SaveSchemaMigrator.DeserializeStory(Json(first), out int loaded);
        AssertThat(loaded).IsEqual(7);
        AssertThat(second.DepositedChronalDust["cleopatra"]).IsEqual(74);
        AssertThat(second.LevelChronalDust).IsEqual(0);
        AssertThat(second.LowestDifficultyUsed).IsEqual(Difficulty.Normal);

        // Each derivation is idempotent on its own, too.
        AssertThat(StoryAttemptState.ReconcileUndepositedCompletion(second)).IsEqual(0);
        CampaignDifficultyRules.SeedLowestDifficultyUsed(second);
        AssertThat(second.LowestDifficultyUsed).IsEqual(Difficulty.Normal);

        GlobalSaveData global = SaveSchemaMigrator.DeserializeGlobal(
            "{\"SaveVersion\":6,\"LastMatchSettings\":{\"Saved\":true,\"HazardRate\":0}}");
        GlobalSaveData reloaded = SaveSchemaMigrator.DeserializeGlobal(Json(global));
        AssertThat(reloaded.LastMatchSettings.StageHazardsEnabled == false).IsTrue();
    }

    [TestCase]
    public void AVersionSevenPayloadRoundTripsWithoutReRunningTheStepAndTheFutureIsRejected() {
        // A v7 payload is taken as written: the reconciliation belongs to the
        // v6 shape only, so a wallet here is never re-banked on load.
        var story = new StorySaveData {
            SelectedCharacterID = "lincoln",
            LevelChronalDust = 25,
            Difficulty = Difficulty.Hard,
            LowestDifficultyUsed = Difficulty.Normal
        };
        story.Normalize();
        AssertThat(story.SaveVersion).IsEqual(7);

        StorySaveData restored = SaveSchemaMigrator.DeserializeStory(Json(story), out int loaded);
        AssertThat(loaded).IsEqual(7);
        AssertThat(restored.LevelChronalDust).IsEqual(25);
        AssertThat(restored.DepositedChronalDust.ContainsKey("lincoln")).IsFalse();
        AssertThat(restored.LowestDifficultyUsed).IsEqual(Difficulty.Normal);

        var global = new GlobalSaveData();
        global.LastMatchSettings ??= new SavedMatchSettings();
        global.LastMatchSettings.StageHazardsEnabled = false;
        global.LastMatchSettings.MeterPickupsEnabled = true;
        global.Normalize();
        GlobalSaveData globalRestored = SaveSchemaMigrator.DeserializeGlobal(Json(global), out int globalLoaded);
        AssertThat(globalLoaded).IsEqual(7);
        AssertThat(globalRestored.LastMatchSettings.StageHazardsEnabled == false).IsTrue();
        AssertThat(globalRestored.LastMatchSettings.MeterPickupsEnabled).IsTrue();

        bool rejected = false;
        try {
            SaveSchemaMigrator.DeserializeGlobal("{\"SaveVersion\":8}");
        } catch (SaveVersionException) {
            rejected = true;
        }
        AssertThat(rejected).IsTrue();
    }
}
