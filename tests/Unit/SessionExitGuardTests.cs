using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The campaign session marker and the VOLUNTARY exit fee.
///
/// <b>V7.6 ruling 2.B (Package 11 A3): crashes are free.</b> The V7.3
/// abnormal-exit fee is retired — a marker surviving to the next boot bills
/// nothing and posts no notice. The marker itself stays, because F10
/// repurposes it as the attempt-status router (A3b). The voluntary 20%
/// undeposited-dust fee the Exit button charges is unchanged, and its math
/// still lives in Core (SessionExitGuard) with PauseMenu delegating, so the
/// boot check never references UI.
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

        // V7.6 2.B: no abnormal-exit application exists any more. The type
        // must expose only the two voluntary helpers plus the marker API.
        AssertThat(typeof(SessionExitGuard).GetMethod("ApplyAbnormalExitFee"))
            .OverrideFailureMessage(
                "V7.6 ruling 2.B retired the crash fee: ApplyAbnormalExitFee must not exist.")
            .IsNull();
    }

    [TestCase]
    public void TheBootCheckChargesNothingAndStillConsumesTheMarker() {
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

            AssertThat(saveManager.ConsumeAbnormalExitMarker())
                .OverrideFailureMessage("The boot check must still recognise the marked slot for F10.")
                .IsTrue();
            AssertThat(saveManager.SaveSlots[scratchSlot].LevelChronalDust)
                .OverrideFailureMessage("V7.6 ruling 2.B: a crash costs the player nothing.")
                .IsEqual(50);
            AssertThat(SessionExitGuard.TryReadMarker(out _))
                .OverrideFailureMessage("The boot check must consume the marker.")
                .IsFalse();
            AssertString(saveManager.LastLoadNoticeKey ?? "")
                .OverrideFailureMessage("A crash must post no fee notice.")
                .IsNotEqual("save_notice_abnormal_exit_fee");

            // A second boot check without a marker does nothing at all.
            AssertThat(saveManager.ConsumeAbnormalExitMarker()).IsFalse();
            AssertThat(saveManager.SaveSlots[scratchSlot].LevelChronalDust).IsEqual(50);
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
