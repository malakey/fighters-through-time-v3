using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Audit 2026-08-08 Low: <c>SessionData.ActiveSaveSlot</c> used to boot as the
/// struct default 0, so any autosave path reached outside a story session would
/// fabricate a slot-0 save — guarded only by UI flow discipline.
/// <see cref="GameManager"/> now boots the session at -1 ("no story session"),
/// and the <see cref="SaveManager"/> write paths refuse rather than fabricate.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SessionSaveSlotGuardTests {

    [TestCase]
    public void CheckpointAndPuzzleWritesRefuseWithoutAnActiveSlot() {
        GameManager gameManager = GameManager.Instance;
        SaveManager saveManager = SaveManager.Instance;
        AssertObject(gameManager).IsNotNull();
        AssertObject(saveManager).IsNotNull();

        SessionData original = gameManager.CurrentSession;
        try {
            SessionData session = gameManager.CurrentSession;
            session.ActiveSaveSlot = -1;
            gameManager.CurrentSession = session;

            var slotsBefore = new StorySaveData[saveManager.SaveSlots.Length];
            for (int index = 0; index < slotsBefore.Length; index++) {
                slotsBefore[index] = saveManager.SaveSlots[index];
            }

            saveManager.SaveCheckpoint("session_guard_test_checkpoint");
            saveManager.SetPuzzleCompleted("session_guard_test_puzzle", true);

            // No slot was fabricated or replaced by either write path.
            for (int index = 0; index < slotsBefore.Length; index++) {
                AssertThat(ReferenceEquals(slotsBefore[index], saveManager.SaveSlots[index]))
                    .OverrideFailureMessage($"Slot {index} was touched by a no-session autosave path.")
                    .IsTrue();
            }
            AssertThat(saveManager.IsPuzzleCompleted("session_guard_test_puzzle")).IsFalse();

            // The slot-explicit completion writer also refuses -1.
            AssertThat(saveManager.MarkCampaignCompleted(-1)).IsFalse();
        } finally {
            gameManager.CurrentSession = original;
        }
    }
}
