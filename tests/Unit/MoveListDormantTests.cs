using System.Collections.Generic;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A5 — the Move List's V7.5 Dormant badges. A Story reader looking
/// at a slot the Legacy Unlock Schedule has not restored sees the row (locked
/// slots are shown, never hidden) carrying "Dormant — restored after Level N"
/// in place of its frame data. Fighter Mode, the hub Holodeck and the
/// Calibration Drills always show the full kit.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MoveListDormantTests {

    private const string AbilityRowPath = "Center/Panel/Layout/Scroll/Sections";

    [TestCase]
    public void ALockedStoryRowShowsTheDormantBadgeInsteadOfItsFrameData() {
        WithFreshCampaignSlot(_ => {
            MoveListScreen screen = Mount();
            try {
                screen.Populate("einstein", MoveListScreen.MoveListMode.Story);
                AssertThat(screen.Mode).IsEqual(MoveListScreen.MoveListMode.Story);

                var sections = screen.Root.GetNode<VBoxContainer>(AbilityRowPath);
                var issues = new List<string>();
                foreach (string slotKey in new[] {
                    "movelist_slot_special1", "movelist_slot_special2",
                    "movelist_slot_movement", "movelist_slot_ultimate"
                }) {
                    var row = sections.GetNodeOrNull<HBoxContainer>($"Ability_{slotKey}");
                    if (row == null) {
                        issues.Add($"{slotKey}: the row was hidden — locked slots must still be SHOWN");
                        continue;
                    }
                    if (row.GetNodeOrNull<Label>("Body/AbilityStats") != null) {
                        issues.Add($"{slotKey}: frame data is still exposed on a dormant slot");
                    }
                    if (row.GetNodeOrNull<Label>("Body/AbilityDormant") == null) {
                        issues.Add($"{slotKey}: no dormant badge");
                    }
                }
                if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
            } finally {
                screen.Free();
            }
        });
    }

    [TestCase]
    public void FighterModeAlwaysShowsTheFullKitEvenOnAFreshCampaignSave() {
        WithFreshCampaignSlot(_ => {
            MoveListScreen screen = Mount();
            try {
                // Same save, same character — only the mode differs.
                screen.Populate("einstein", MoveListScreen.MoveListMode.Fighter);
                var sections = screen.Root.GetNode<VBoxContainer>(AbilityRowPath);
                var issues = new List<string>();
                foreach (string slotKey in new[] {
                    "movelist_slot_special1", "movelist_slot_special2",
                    "movelist_slot_movement", "movelist_slot_ultimate"
                }) {
                    var row = sections.GetNodeOrNull<HBoxContainer>($"Ability_{slotKey}");
                    if (row == null) { issues.Add($"{slotKey}: missing row"); continue; }
                    if (row.GetNodeOrNull<Label>("Body/AbilityStats") == null) {
                        issues.Add($"{slotKey}: Fighter Mode must show full frame data");
                    }
                    if (row.GetNodeOrNull<Label>("Body/AbilityDormant") != null) {
                        issues.Add($"{slotKey}: Fighter Mode must never mark a slot dormant");
                    }
                }
                if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

                // The default overload is the Fighter reading.
                screen.Populate("einstein");
                AssertThat(screen.Mode).IsEqual(MoveListScreen.MoveListMode.Fighter);
            } finally {
                screen.Free();
            }
        });
    }

    [TestCase]
    public void TheBadgeNamesTheMilestoneLevelAndClearsAsSlotsAreRestored() {
        WithFreshCampaignSlot(save => {
            MoveListScreen screen = Mount();
            try {
                TranslationServer.SetLocale("en");
                screen.Populate("einstein", MoveListScreen.MoveListMode.Story);
                var sections = screen.Root.GetNode<VBoxContainer>(AbilityRowPath);

                var movement = sections.GetNode<HBoxContainer>("Ability_movelist_slot_movement")
                    .GetNode<Label>("Body/AbilityDormant");
                AssertThat(movement.Text).IsEqual(string.Format(
                    TranslationServer.Translate("movelist_dormant_until").ToString(),
                    LegacyUnlockSchedule.MilestoneLevelFor(AbilitySlot.MovementAbility)));

                var ultimate = sections.GetNode<HBoxContainer>("Ability_movelist_slot_ultimate")
                    .GetNode<Label>("Body/AbilityDormant");
                AssertThat(ultimate.Text).IsEqual(string.Format(
                    TranslationServer.Translate("movelist_dormant_until").ToString(),
                    LegacyUnlockSchedule.MilestoneLevelFor(AbilitySlot.Ultimate)));
                AssertThat(movement.Text == ultimate.Text)
                    .OverrideFailureMessage("Different milestones must read as different levels.")
                    .IsFalse();

                // Earn the Movement Ability: its badge clears, the rest stay.
                save.CompletedLevels.Add("level_01_florence");
                screen.Populate("einstein", MoveListScreen.MoveListMode.Story);
                sections = screen.Root.GetNode<VBoxContainer>(AbilityRowPath);
                AssertObject(sections.GetNode<HBoxContainer>("Ability_movelist_slot_movement")
                        .GetNodeOrNull<Label>("Body/AbilityStats"))
                    .OverrideFailureMessage("A restored slot must show its frame data again.")
                    .IsNotNull();
                AssertObject(sections.GetNode<HBoxContainer>("Ability_movelist_slot_special1")
                        .GetNodeOrNull<Label>("Body/AbilityDormant"))
                    .OverrideFailureMessage("Special 1 is still dormant after Level 1.")
                    .IsNotNull();
            } finally {
                screen.Free();
            }
        });
    }

    // === Helpers =======================================================

    private static MoveListScreen Mount() {
        var screen = new MoveListScreen { Name = "MoveListDormantUnderTest" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(screen);
        return screen;
    }

    private static void WithFreshCampaignSlot(System.Action<StorySaveData> body) {
        const int scratchSlot = 2;
        SaveManager saveManager = SaveManager.Instance;
        GameManager gameManager = GameManager.Instance;
        StorySaveData original = saveManager.SaveSlots[scratchSlot];
        SessionData originalSession = gameManager.CurrentSession;
        try {
            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            saveManager.SaveSlots[scratchSlot] = save;
            SessionData session = gameManager.CurrentSession;
            session.ActiveSaveSlot = scratchSlot;
            session.SelectedCharacterID = "einstein";
            gameManager.CurrentSession = session;
            body(save);
        } finally {
            saveManager.SaveSlots[scratchSlot] = original;
            gameManager.CurrentSession = originalSession;
        }
    }
}
