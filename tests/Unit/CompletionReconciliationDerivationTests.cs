using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W2 — the declared v6→v7 H02 reconciliation,
/// <see cref="StoryAttemptState.ReconcileUndepositedCompletion"/>. Phase C
/// composed it into the single v7 migration step (pinned end-to-end by
/// <c>SaveSchemaV7MigrationTests</c>); these cases pin the function alone. A v6 payload saved after a level completed but before the hub loaded
/// relied on the hub's (now deleted) arrival deposit; it must bank exactly once,
/// while an open attempt's held dust must not bank at all.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CompletionReconciliationDerivationTests {

    [TestCase]
    public void AV6PostCompletionPayloadDepositsItsWalletExactlyOnce() {
        // SaveLevelCompletion's v6 shape: an unminted attempt, no checkpoint.
        var save = new StorySaveData { SelectedCharacterID = "joan", LevelChronalDust = 137 };
        save.DepositedChronalDust["joan"] = 400;

        AssertThat(StoryAttemptState.ReconcileUndepositedCompletion(save)).IsEqual(137);
        AssertThat(save.DepositedChronalDust["joan"]).IsEqual(537);
        AssertThat(save.LevelChronalDust).IsEqual(0);

        AssertThat(StoryAttemptState.ReconcileUndepositedCompletion(save))
            .OverrideFailureMessage("A second application must deposit nothing.")
            .IsEqual(0);
        AssertThat(save.DepositedChronalDust["joan"]).IsEqual(537);
    }

    [TestCase]
    public void ACompletedAttemptRecordDepositsOnceAndRecordsTheAmountOnItsTransaction() {
        var save = new StorySaveData { SelectedCharacterID = "tesla", LevelChronalDust = 60 };
        save.AttemptState = StoryAttemptState.CreateFresh("level_05_titanic", 0);
        save.AttemptState.Status = StoryAttemptStatus.Completed;
        save.LastCheckpointID = "level_05_titanic_checkpoint_2";

        AssertThat(StoryAttemptState.ReconcileUndepositedCompletion(save)).IsEqual(60);
        AssertThat(save.DepositedChronalDust["tesla"]).IsEqual(60);
        AssertThat(save.AttemptState.CompletionTransaction.DepositAmount).IsEqual(60);
        AssertThat(StoryAttemptState.ReconcileUndepositedCompletion(save)).IsEqual(0);
    }

    [TestCase]
    public void AnOpenAttemptsHeldDustNeverBanksThroughTheMigration() {
        // Acts I-II Collapse parked in the hub: a minted AwaitingHubResume record.
        var collapsed = new StorySaveData {
            SelectedCharacterID = "einstein",
            LevelChronalDust = 80,
            LastCheckpointID = "level_03_chicago_checkpoint_1"
        };
        collapsed.AttemptState = StoryAttemptState.CreateFresh("level_03_chicago", 0);
        collapsed.AttemptState.Status = StoryAttemptStatus.AwaitingHubResume;
        AssertThat(StoryAttemptState.ReconcileUndepositedCompletion(collapsed)).IsEqual(0);
        AssertThat(collapsed.LevelChronalDust).IsEqual(80);
        AssertThat(collapsed.DepositedChronalDust.ContainsKey("einstein")).IsFalse();

        // A live attempt mid-level.
        var active = new StorySaveData { SelectedCharacterID = "einstein", LevelChronalDust = 30 };
        active.AttemptState = StoryAttemptState.CreateFresh("level_03_chicago", 0);
        AssertThat(StoryAttemptState.ReconcileUndepositedCompletion(active)).IsEqual(0);
        AssertThat(active.LevelChronalDust).IsEqual(30);

        // Nothing to bank, or no character to bank it to.
        AssertThat(StoryAttemptState.ReconcileUndepositedCompletion(new StorySaveData { SelectedCharacterID = "joan" }))
            .IsEqual(0);
        AssertThat(StoryAttemptState.ReconcileUndepositedCompletion(new StorySaveData { LevelChronalDust = 9 }))
            .IsEqual(0);
        AssertThat(StoryAttemptState.ReconcileUndepositedCompletion(null)).IsEqual(0);
    }
}
