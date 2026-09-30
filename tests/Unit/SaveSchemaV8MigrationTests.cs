using System.Collections.Generic;
using FTT.Core;
using GdUnit4;
using Newtonsoft.Json;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 Phase C — the single v7 → v8 step. It composes exactly the
/// declared derivations, in this order: W2's
/// <see cref="StoryAttemptState.RetireLegacyLevelV8"/> (plan D2), then W5's
/// <see cref="RosterSwapV8.MigrateRosterSwapV8"/> (D4) on the story payload, and
/// W5's <see cref="RosterSwapV8.EnsureReplacementUnlocked"/> on the global one.
/// W3's four additive story fields need no derivation and are pinned to load
/// on their initializers. Every fixture is a v7 JSON payload.
///
/// <para><b>Pure C#, no Godot runtime</b> — deliberately: a v7 payload must
/// migrate without the engine (save payloads stay engine-free; CLAUDE.md
/// failure signature 7). Only v7+ fixtures are used here, because the pre-v6
/// grid refund reads authored grids and needs the runtime.</para>
/// </summary>
[TestSuite]
public class SaveSchemaV8MigrationTests {
    private static string Json(object payload) => JsonConvert.SerializeObject(payload);

    private const string RetiredEinsteinScene = "res://scenes/campaign/Level_04A_Einstein.tscn";

    [TestCase]
    public void AVersionSevenPocahontasSaveParkedOnLevelFourABanksToTubmanAndReParksAtLevelFive() {
        StorySaveData migrated = SaveSchemaMigrator.DeserializeStory(Json(new {
            SaveVersion = 7,
            SelectedCharacterID = "pocahontas",
            CurrentLevelID = "res://scenes/campaign/Level_04A_Pocahontas.tscn",
            LastCheckpointID = "level_04a_pocahontas_checkpoint_1",
            LevelChronalDust = 40,
            DepositedChronalDust = new Dictionary<string, int> { ["pocahontas"] = 100 },
            GridProgress = new Dictionary<string, List<string>> {
                ["pocahontas"] = new() { "pocahontas_windstep", "pocahontas_glide_duration" }
            },
            UnlockedLegacyAbilities = new Dictionary<string, List<string>> {
                ["pocahontas"] = new() { "movement", "special1" }
            },
            CompletedLevels = new List<string> { "level_04_paris", "level_04a_pocahontas" }
        }), out int loaded);

        AssertThat(loaded).IsEqual(7);
        AssertThat(migrated.SaveVersion).IsEqual(8);

        // D2: the held 4A wallet banks once and the save re-parks on a fresh Level 5 attempt.
        AssertThat(migrated.LevelChronalDust).IsEqual(0);
        AssertString(migrated.CurrentLevelID).IsEqual(StoryAttemptState.LegacyRetirementScenePath);
        AssertString(migrated.LastCheckpointID).IsEqual("");
        AssertString(migrated.AttemptState.LevelID).IsEqual(StoryAttemptState.LegacyRetirementLevelID);

        // D4, after D2: the banked dust followed Pocahontas's balance to Tubman,
        // along with the grid refund (50 + 75) and the Legacy unlocks.
        AssertString(migrated.SelectedCharacterID).IsEqual("tubman");
        AssertThat(migrated.DepositedChronalDust["tubman"]).IsEqual(100 + 40 + 50 + 75);
        AssertThat(migrated.DepositedChronalDust.ContainsKey("pocahontas")).IsFalse();
        AssertThat(migrated.GridProgress.ContainsKey("pocahontas")).IsFalse();
        AssertThat(migrated.UnlockedLegacyAbilities["tubman"].Contains("special1")).IsTrue();

        // 4A history stays as dead data.
        AssertThat(migrated.CompletedLevels.Contains("level_04a_pocahontas")).IsTrue();
    }

    [TestCase]
    public void APlainVersionSevenStoryPayloadChangesOnlyItsVersionAndLoadsTheW3Fields() {
        StorySaveData migrated = SaveSchemaMigrator.DeserializeStory(Json(new {
            SaveVersion = 7,
            SelectedCharacterID = "joan",
            CurrentLevelID = "res://scenes/campaign/Level_07_Nassau.tscn",
            LastCheckpointID = "level_07_nassau_checkpoint_1",
            LevelChronalDust = 33,
            DepositedChronalDust = new Dictionary<string, int> { ["joan"] = 210 },
            GridProgress = new Dictionary<string, List<string>> { ["joan"] = new() { "joan_spine_1" } }
        }), out int loaded);

        AssertThat(loaded).IsEqual(7);
        AssertThat(migrated.SaveVersion).IsEqual(SaveSchemaMigrator.CurrentVersion);
        AssertString(migrated.SelectedCharacterID).IsEqual("joan");
        AssertString(migrated.CurrentLevelID).IsEqual("res://scenes/campaign/Level_07_Nassau.tscn");
        AssertString(migrated.LastCheckpointID).IsEqual("level_07_nassau_checkpoint_1");
        AssertThat(migrated.LevelChronalDust).IsEqual(33);
        AssertThat(migrated.DepositedChronalDust["joan"]).IsEqual(210);
        AssertThat(migrated.DepositedChronalDust.ContainsKey("tubman")).IsFalse();
        AssertThat(migrated.GridProgress["joan"].Count).IsEqual(1);

        // W3's additive fields load on their legacy-meaning initializers.
        AssertThat(migrated.SkippedCalibration).IsFalse();
        AssertThat(migrated.ShownFirstUseTooltips.Count).IsEqual(0);
        AssertString(migrated.LastHubLineVisit).IsEqual("");
        AssertThat(migrated.HomecomingSeen).IsFalse();

        // A non-Pocahontas hero parked on a retired 4A scene banks to its own balance.
        StorySaveData einstein = SaveSchemaMigrator.DeserializeStory(Json(new {
            SaveVersion = 7,
            SelectedCharacterID = "einstein",
            CurrentLevelID = RetiredEinsteinScene,
            LevelChronalDust = 12
        }));
        AssertString(einstein.SelectedCharacterID).IsEqual("einstein");
        AssertThat(einstein.DepositedChronalDust["einstein"]).IsEqual(12);
        AssertString(einstein.CurrentLevelID).IsEqual(StoryAttemptState.LegacyRetirementScenePath);
    }

    [TestCase]
    public void AVersionSevenGlobalPayloadUnlocksTubmanOnce() {
        GlobalSaveData migrated = SaveSchemaMigrator.DeserializeGlobal(Json(new {
            SaveVersion = 7,
            UnlockedCharacters = new List<string> { "einstein", "pocahontas" }
        }), out int loaded);
        AssertThat(loaded).IsEqual(7);
        AssertThat(migrated.SaveVersion).IsEqual(8);
        AssertThat(migrated.UnlockedCharacters.Contains("tubman")).IsTrue();
        AssertThat(migrated.UnlockedCharacters.Contains("pocahontas")).IsTrue();

        // An empty list stays empty: the runtime roster grant owns a fresh install.
        GlobalSaveData empty = SaveSchemaMigrator.DeserializeGlobal("{\"SaveVersion\":7}");
        AssertThat(empty.UnlockedCharacters.Count).IsEqual(0);
    }

    [TestCase]
    public void TheVersionEightStepIsIdempotentAcrossARewrite() {
        StorySaveData first = SaveSchemaMigrator.DeserializeStory(Json(new {
            SaveVersion = 7,
            SelectedCharacterID = "pocahontas",
            CurrentLevelID = "res://scenes/campaign/Level_04A_Pocahontas.tscn",
            LevelChronalDust = 20,
            DepositedChronalDust = new Dictionary<string, int> { ["pocahontas"] = 5 }
        }));
        AssertThat(first.DepositedChronalDust["tubman"]).IsEqual(25);
        string attemptID = first.AttemptState.AttemptID;

        StorySaveData second = SaveSchemaMigrator.DeserializeStory(Json(first), out int loaded);
        AssertThat(loaded).IsEqual(8);
        AssertThat(second.DepositedChronalDust["tubman"]).IsEqual(25);
        AssertString(second.AttemptState.AttemptID).IsEqual(attemptID);

        // Each derivation is idempotent on its own, too.
        AssertThat(StoryAttemptState.RetireLegacyLevelV8(second)).IsEqual(-1);
        AssertThat(RosterSwapV8.MigrateRosterSwapV8(second)).IsEqual(0);
        AssertThat(second.DepositedChronalDust["tubman"]).IsEqual(25);

        GlobalSaveData global = SaveSchemaMigrator.DeserializeGlobal(Json(new {
            SaveVersion = 7, UnlockedCharacters = new List<string> { "pocahontas" }
        }));
        GlobalSaveData reloaded = SaveSchemaMigrator.DeserializeGlobal(Json(global));
        int tubmanEntries = 0;
        foreach (string id in reloaded.UnlockedCharacters) if (id == "tubman") tubmanEntries++;
        AssertThat(tubmanEntries).IsEqual(1);
    }

    [TestCase]
    public void AVersionEightPayloadIsTakenAsWrittenAndTheFutureIsStillRejected() {
        // A v8 payload never re-runs the step: a (hand-edited) pocahontas selection stays put.
        StorySaveData restored = SaveSchemaMigrator.DeserializeStory(Json(new {
            SaveVersion = 8,
            SelectedCharacterID = "pocahontas",
            CurrentLevelID = RetiredEinsteinScene,
            LevelChronalDust = 9
        }), out int loaded);
        AssertThat(loaded).IsEqual(8);
        AssertString(restored.SelectedCharacterID).IsEqual("pocahontas");
        AssertThat(restored.LevelChronalDust).IsEqual(9);

        bool storyRejected = false;
        try {
            SaveSchemaMigrator.DeserializeStory("{\"SaveVersion\":9}");
        } catch (SaveVersionException) {
            storyRejected = true;
        }
        AssertThat(storyRejected).IsTrue();

        bool globalRejected = false;
        try {
            SaveSchemaMigrator.DeserializeGlobal("{\"SaveVersion\":9}");
        } catch (SaveVersionException) {
            globalRejected = true;
        }
        AssertThat(globalRejected).IsTrue();
    }
}
