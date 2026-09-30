using System.Collections.Generic;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W2 — the declared v7 → v8 S27 derivation,
/// <see cref="StoryAttemptState.RetireLegacyLevelV8"/> (plan D2). Phase C wires it
/// into the single v8 step; this suite pins the derivation itself.
///
/// <para><b>Pure C#, no Godot runtime</b> — deliberately. Save payloads and their
/// derivations must stay engine-free (CLAUDE.md failure signature 7), so nothing
/// here touches <c>StoryManager</c>, a resource or the manifest. The literals the
/// derivation re-parks to are pinned against <c>StoryManager</c> separately, in
/// <c>RetireLegacyLevelResumeTests</c>, which does run under Godot.</para>
/// </summary>
[TestSuite]
public class RetireLegacyLevelV8Tests {

    private const string LegacyScene = "res://scenes/campaign/Level_04A_einstein.tscn";

    [TestCase]
    public void AParkedLegacySaveBanksItsHeldWalletOnceAndReparksAtLevelFiveOnAFreshAttempt() {
        StorySaveData save = ParkedLegacySave(wallet: 17);
        string oldAttempt = save.AttemptState.AttemptID;

        int banked = StoryAttemptState.RetireLegacyLevelV8(save);

        AssertThat(banked).IsEqual(17);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(40 + 17);
        AssertThat(save.LevelChronalDust).IsEqual(0);

        AssertString(save.CurrentLevelID).IsEqual(StoryAttemptState.LegacyRetirementScenePath);
        AssertString(save.CurrentLevelID).IsEqual("res://scenes/campaign/Level_05_Titanic.tscn");
        AssertString(save.LastCheckpointID)
            .OverrideFailureMessage("The next Level 5 entry must be a fresh entry, never a resume.")
            .IsEqual("");
        AssertThat(save.ActivatedCheckpointIDs.Count).IsEqual(0);
        AssertThat(save.DestroyedExtractorIDs.Count).IsEqual(0);
        AssertThat(save.FoundSecretIDs.Count).IsEqual(0);
        AssertThat(save.FontUsesConsumed.Count).IsEqual(0);
        AssertThat(save.ClaimedRewardSourceIDs.Count).IsEqual(0);
        AssertThat(save.LevelIntegrityPercent).IsEqual(100f);
        AssertThat(save.CheckpointIntegrityPercent).IsEqual(100f);
        AssertThat(save.StoryDefyHistoryUsed).IsFalse();

        // A new attempt for Level 5: fresh ID, Active, revision 1, no anchors.
        AssertThat(save.AttemptState.HasAttempt).IsTrue();
        AssertString(save.AttemptState.AttemptID).IsNotEqual(oldAttempt);
        AssertString(save.AttemptState.LevelID).IsEqual("level_05_titanic");
        AssertThat(save.AttemptState.Status).IsEqual(StoryAttemptStatus.Active);
        AssertThat(save.AttemptState.Revision).IsEqual(1L);
        AssertThat(save.AttemptState.AnchorChargesRemaining).IsEqual(0);
        AssertThat(save.AttemptState.RewardSources.Count).IsEqual(0);
    }

    [TestCase]
    public void TheDerivationIsIdempotentAndLeavesEveryOtherPayloadUntouched() {
        StorySaveData save = ParkedLegacySave(wallet: 12);
        AssertThat(StoryAttemptState.RetireLegacyLevelV8(save)).IsEqual(12);
        string attempt = save.AttemptState.AttemptID;

        // A second application finds nothing parked on a retired scene.
        AssertThat(StoryAttemptState.RetireLegacyLevelV8(save)).IsEqual(-1);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(40 + 12);
        AssertString(save.AttemptState.AttemptID).IsEqual(attempt);

        // A save parked anywhere else — mid-level with a held wallet — is untouched.
        var other = new StorySaveData {
            SelectedCharacterID = "joan",
            CurrentLevelID = "res://scenes/campaign/Level_07_Nassau.tscn",
            LastCheckpointID = "level_07_nassau_checkpoint_1",
            LevelChronalDust = 9
        };
        other.AttemptState = StoryAttemptState.CreateFresh("level_07_nassau", 0);
        string otherAttempt = other.AttemptState.AttemptID;
        AssertThat(StoryAttemptState.RetireLegacyLevelV8(other)).IsEqual(-1);
        AssertThat(other.LevelChronalDust).IsEqual(9);
        AssertString(other.LastCheckpointID).IsEqual("level_07_nassau_checkpoint_1");
        AssertString(other.AttemptState.AttemptID).IsEqual(otherAttempt);

        AssertThat(StoryAttemptState.RetireLegacyLevelV8(null)).IsEqual(-1);
        AssertThat(StoryAttemptState.IsRetiredLegacyLevelScene(LegacyScene)).IsTrue();
        AssertThat(StoryAttemptState.IsRetiredLegacyLevelScene("res://scenes/campaign/Level_04_Paris.tscn")).IsFalse();
        AssertThat(StoryAttemptState.IsRetiredLegacyLevelScene("")).IsFalse();
    }

    [TestCase]
    public void HistoryIsDeadDataAndAnEmptyWalletStillReparks() {
        // A 4A that was entered but never earned anything (or one reached via
        // Level 4's advance and never started) re-parks with nothing to bank.
        StorySaveData save = ParkedLegacySave(wallet: 0);
        save.CompletedLevels.Add("level_04a_einstein");
        save.IntegrityByLevel["level_04a_einstein"] = 88f;
        save.UnlockedLegacyAbilities["einstein"] = new List<string> { "movement", "special1", "special2", "ultimate" };
        save.GridProgress["einstein"] = new List<string> { "einstein_node_1" };

        AssertThat(StoryAttemptState.RetireLegacyLevelV8(save)).IsEqual(0);
        AssertString(save.CurrentLevelID).IsEqual(StoryAttemptState.LegacyRetirementScenePath);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(40);

        // Plan D2: the 4A history stays as dead data; N05 never reads it.
        AssertThat(save.CompletedLevels).Contains("level_04a_einstein");
        AssertThat(save.IntegrityByLevel["level_04a_einstein"]).IsEqual(88f);
        // Progress the player owns is never touched.
        AssertThat(save.UnlockedLegacyAbilities["einstein"].Count).IsEqual(4);
        AssertThat(save.GridProgress["einstein"].Count).IsEqual(1);
        AssertThat(save.CompletedLevels).Contains("level_04_paris");
    }

    [TestCase]
    public void ACollapsedLegacyAttemptParkedInTheHubBanksItsPostFeeWalletToo() {
        // H02: a Collapse or voluntary exit leaves the post-fee wallet held on the
        // open attempt (AwaitingHubResume). The level can never be completed now,
        // so that held dust banks once as well — no fee on top, no tier bonus.
        StorySaveData save = ParkedLegacySave(wallet: 8);
        save.AttemptState.Status = StoryAttemptStatus.AwaitingHubResume;
        save.AttemptState.RecoveryEvent.EventID = "collapse_1";
        save.DepositedChronalDust.Remove("einstein");

        AssertThat(StoryAttemptState.RetireLegacyLevelV8(save)).IsEqual(8);
        AssertThat(save.DepositedChronalDust["einstein"]).IsEqual(8);
        AssertThat(save.AttemptState.Status).IsEqual(StoryAttemptStatus.Active);
        AssertThat(save.AttemptState.RecoveryEvent.IsPending).IsFalse();
    }

    private static StorySaveData ParkedLegacySave(int wallet) {
        var save = new StorySaveData {
            SelectedCharacterID = "einstein",
            CurrentLevelID = LegacyScene,
            LastCheckpointID = "level_04a_einstein_checkpoint_1",
            LevelChronalDust = wallet,
            LevelIntegrityPercent = 41f,
            CheckpointIntegrityPercent = 55f,
            StoryDefyHistoryUsed = true,
            CompletedLevels = new List<string> { "level_00_tutorial", "level_01_florence",
                "level_02_orleans", "level_03_chicago", "level_04_paris" }
        };
        save.DepositedChronalDust["einstein"] = 40;
        save.ActivatedCheckpointIDs.Add("level_04a_einstein_checkpoint_0");
        save.FoundSecretIDs.Add("level_04a_einstein.secret");
        save.FontUsesConsumed["level_04a_einstein_font"] = 1;
        save.ClaimedRewardSourceIDs.Add("level_04a_einstein.approach#0");
        save.AttemptState = StoryAttemptState.CreateFresh("level_04a_einstein", 0);
        save.AttemptState.RewardSources["level_04a_einstein.approach#0"] = new StoryRewardSourceRecord {
            SourceID = "level_04a_einstein.approach#0"
        };
        return save;
    }
}
