using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// V7.3 quit-fee session marker (design "one loss rule"): a marker written at
/// campaign start and cleared on clean shutdown means an abnormal exit
/// (crash, kill, power loss) is detected at the next boot and pays the same
/// 20% undeposited-dust fee the Exit button pays — Alt-F4 is no longer
/// strictly better than pressing Exit. Fee math lives in Core
/// (SessionExitGuard) with PauseMenu delegating, so the boot check never
/// references UI.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SessionExitGuardTests {

    [TestCase]
    public void TheMarkerRoundTripsAndClears() {
        try {
            SessionExitGuard.ClearMarker();
            AssertThat(SessionExitGuard.TryReadMarker(out _)).IsFalse();

            SessionExitGuard.WriteMarker(2);
            AssertThat(SessionExitGuard.TryReadMarker(out int slot)).IsTrue();
            AssertThat(slot).IsEqual(2);

            // A refresh overwrites in place.
            SessionExitGuard.WriteMarker(1);
            AssertThat(SessionExitGuard.TryReadMarker(out slot)).IsTrue();
            AssertThat(slot).IsEqual(1);

            // No-session sentinel never writes a marker.
            SessionExitGuard.ClearMarker();
            SessionExitGuard.WriteMarker(-1);
            AssertThat(SessionExitGuard.TryReadMarker(out _)).IsFalse();

            SessionExitGuard.ClearMarker();
            AssertThat(SessionExitGuard.TryReadMarker(out _)).IsFalse();
        } finally {
            SessionExitGuard.ClearMarker();
        }
    }

    [TestCase]
    public void TheFeeMathIsTheOneExitRuleAndPauseMenuDelegatesToIt() {
        // 20% of undeposited dust forfeited, floor-rounded retention.
        AssertThat(SessionExitGuard.CalculateExitRetainedDust(40)).IsEqual(32);
        AssertThat(SessionExitGuard.CalculateExitRetainedDust(41)).IsEqual(32);
        AssertThat(SessionExitGuard.CalculateExitRetainedDust(0)).IsEqual(0);
        AssertThat(SessionExitGuard.CalculateExitWalletAfterPenalty(100, 40)).IsEqual(92);
        AssertThat(SessionExitGuard.CalculateExitWalletAfterPenalty(40, 40)).IsEqual(32);

        // The PauseMenu wrappers and the Core home must be the same rule.
        AssertThat(PauseMenu.CalculateExitRetainedDust(41))
            .IsEqual(SessionExitGuard.CalculateExitRetainedDust(41));
        AssertThat(PauseMenu.CalculateExitWalletAfterPenalty(100, 40))
            .IsEqual(SessionExitGuard.CalculateExitWalletAfterPenalty(100, 40));

        // The abnormal-exit application: whole undeposited wallet at risk.
        var save = new StorySaveData { LevelChronalDust = 100 };
        AssertThat(SessionExitGuard.ApplyAbnormalExitFee(save)).IsEqual(20);
        AssertThat(save.LevelChronalDust).IsEqual(80);

        // A marker left parked at the hub costs nothing: the auto-deposit
        // zeroed the at-risk wallet.
        var hubParked = new StorySaveData { LevelChronalDust = 0 };
        AssertThat(SessionExitGuard.ApplyAbnormalExitFee(hubParked)).IsEqual(0);
        AssertThat(hubParked.LevelChronalDust).IsEqual(0);
    }

    [TestCase]
    public void TheBootCheckBillsTheMarkedSlotOnceAndClearsTheMarker() {
        const int scratchSlot = 2;
        SaveManager saveManager = SaveManager.Instance;
        AssertObject(saveManager).IsNotNull();
        StorySaveData original = saveManager.SaveSlots[scratchSlot];
        try {
            saveManager.SaveSlots[scratchSlot] = new StorySaveData {
                SelectedCharacterID = "einstein",
                LevelChronalDust = 50
            };
            SessionExitGuard.WriteMarker(scratchSlot);

            saveManager.ApplyAbnormalExitFeeIfMarked();
            AssertThat(saveManager.SaveSlots[scratchSlot].LevelChronalDust)
                .OverrideFailureMessage("The abnormal exit must pay the same 20% the Exit button pays.")
                .IsEqual(40);
            AssertThat(SessionExitGuard.TryReadMarker(out _))
                .OverrideFailureMessage("The boot check must consume the marker.")
                .IsFalse();
            AssertString(saveManager.LastLoadNoticeKey).IsEqual("save_notice_abnormal_exit_fee");

            // A second boot check without a marker bills nothing.
            saveManager.ApplyAbnormalExitFeeIfMarked();
            AssertThat(saveManager.SaveSlots[scratchSlot].LevelChronalDust).IsEqual(40);
        } finally {
            SessionExitGuard.ClearMarker();
            int activeSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            saveManager.SaveSlots[scratchSlot] = original;
            // DeleteStorySlot clears the session pointer when it matches; put it back.
            if (original == null) saveManager.DeleteStorySlot(scratchSlot);
            else saveManager.SaveStorySlot(scratchSlot);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = activeSlot;
        }
    }
}
