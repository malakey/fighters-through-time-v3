using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A11 (F18 Option B). The Calibration Drills route: a standalone
/// Main Menu entry available on a first boot with <b>no Story save</b>, a
/// single-player picker with no Ready gate / countdown / stage select, the shared
/// drill list, and the retained hub console entry that returns to its own hub.
///
/// <para>The through-line of the whole suite is isolation: nothing on this route
/// creates, loads, writes or advances a Story save, and nothing it does can be
/// resumed as a campaign attempt.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CalibrationDrillsRouteTests {

    private const string RootLayout = "RootScreen/Center/Panel/Layout/";
    private const string PickerRoot = "Select/Root/";
    private const string ListRoot = "List/Root/";

    [TestCase]
    public void TheMainMenuOffersCalibrationDrillsAndArmsTheMenuReturnRouteWithNoStorySave() {
        GameManager manager = GameManager.Instance;
        if (manager == null) return;
        SessionData saved = manager.CurrentSession;
        StorySaveData[] originals = ClearAllSlots();
        try {
            // A first boot: no save in any slot, no active slot.
            SessionData firstBoot = saved;
            firstBoot.ActiveSaveSlot = -1;
            firstBoot.CalibrationReturnScenePath = "";
            manager.CurrentSession = firstBoot;

            var packed = ResourceLoader.Load<PackedScene>("res://scenes/menus/MainMenu.tscn");
            var menu = packed.Instantiate<MainMenu>();
            var host = new Node { Name = "DrillRouteMenuHost" };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
            host.AddChild(menu);
            try {
                var button = menu.GetNodeOrNull<Button>(RootLayout + "CalibrationDrillsButton");
                AssertThat(button != null)
                    .OverrideFailureMessage("The root screen has no CalibrationDrillsButton")
                    .IsTrue();
                AssertThat(button.Visible).IsTrue();
                // The A1 convention: the scene stores the raw key.
                AssertThat(button.Text).IsEqual("menu_calibration_drills");
                AssertThat(button.ThemeTypeVariation.ToString()).IsEqual("TemporalGlassButton");

                // The handler's session half, without the scene change: pressing
                // the button in the test host would hand the runner's current scene
                // to GameManager (the reason no menu test presses Fighter either).
                AssertThat(MainMenu.ArmCalibrationRoute())
                    .IsEqual(CalibrationRoute.PickerScenePath);

                SessionData session = manager.CurrentSession;
                AssertThat(session.CalibrationReturnScenePath)
                    .IsEqual(CalibrationRoute.MainMenuScenePath);
                AssertThat(session.CalibrationDrillIndex).IsEqual(0);
                // The route never creates or selects a save slot.
                AssertThat(session.ActiveSaveSlot).IsEqual(-1);
                for (int slot = 0; slot < 3; slot++) {
                    AssertThat(SaveManager.Instance?.SaveSlots[slot] == null)
                        .OverrideFailureMessage($"Calibration Drills created a save in slot {slot}")
                        .IsTrue();
                }
            } finally {
                Teardown(host);
            }
        } finally {
            manager.CurrentSession = saved;
            RestoreSlots(originals);
        }
    }

    [TestCase]
    public void ThePickerIsSinglePlayerWithNoReadyGateCountdownOrStageSelect() {
        (Node host, CalibrationDrillPicker picker) = OpenPicker("DrillPickerShapeHost");
        try {
            // All nine, in the locked roster order.
            var grid = picker.GetNode<GridContainer>(PickerRoot + "Grid");
            AssertThat(grid.GetChildCount()).IsEqual(9);
            AssertThat(CalibrationDrillPicker.Roster.Count).IsEqual(9);

            // The two-player select's machinery must not be here.
            foreach (string absent in new[] {
                         "P1Ready", "P2Ready", "CountdownLabel", "StageSelect",
                         "LocalHumanToggle", "CpuDifficulty", "FightButton" }) {
                AssertThat(picker.FindChild(absent, recursive: true, owned: false) == null)
                    .OverrideFailureMessage($"The single-player picker carries '{absent}'")
                    .IsTrue();
            }

            // The shared onboarding footer is reused, per F18.
            AssertThat(picker.GetNodeOrNull<Button>(PickerRoot + "ButtonRow/MoveListButton") != null).IsTrue();
            AssertThat(picker.GetNodeOrNull<Button>(PickerRoot + "ButtonRow/SystemsCardButton") != null).IsTrue();
            AssertThat(picker.GetNodeOrNull<Button>(PickerRoot + "ButtonRow/BackButton") != null).IsTrue();

            // Nine tiles plus the three footer buttons, all focus-authored.
            AssertChainCovers(picker.GetNode<Control>("Select"), 12);

            // Every tile shows a resolved name, never a raw key.
            for (int index = 0; index < 9; index++) {
                var label = grid.GetNode<Button>($"CharacterButton{index}")
                    .GetNodeOrNull<Label>("CharacterName");
                AssertThat(label != null)
                    .OverrideFailureMessage($"Tile {index} has no name layer")
                    .IsTrue();
                AssertThat(label.Text.StartsWith("character_"))
                    .OverrideFailureMessage($"Tile {index} shows a raw key: {label.Text}")
                    .IsFalse();
            }
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void ChoosingACharacterWritesOnlyTheFighterSelectionAndNeverACampaignValue() {
        GameManager manager = GameManager.Instance;
        if (manager == null || StoryManager.Instance == null) return;
        SessionData saved = manager.CurrentSession;
        try {
            SessionData primed = saved;
            primed.ActiveSaveSlot = 2;
            primed.Difficulty = Difficulty.Hard;
            primed.SelectedCharacterID = "einstein";
            primed.CalibrationReturnScenePath = CalibrationRoute.MainMenuScenePath;
            manager.CurrentSession = primed;

            CampaignLevel levelBefore = StoryManager.Instance.CurrentLevel;
            int dustBefore = StoryManager.Instance.ChronalDustCollected;
            int rewindsBefore = StoryManager.Instance.ChronalRewindsRemaining;

            (Node host, CalibrationDrillPicker picker) = OpenPicker("DrillPickerChooseHost");
            try {
                AssertThat(picker.ApplyChoiceToSession(5)) // tesla
                    .IsEqual(CalibrationRoute.DrillListScenePath);
            } finally {
                Teardown(host);
            }

            SessionData session = manager.CurrentSession;
            AssertThat(session.SelectedCharacterID).IsEqual("tesla");
            // Campaign state is untouched: no slot moved, no difficulty rewritten,
            // no level advanced, no dust or rewind charge granted or spent.
            AssertThat(session.ActiveSaveSlot).IsEqual(2);
            AssertThat(session.Difficulty).IsEqual(Difficulty.Hard);
            AssertThat(StoryManager.Instance.CurrentLevel).IsEqual(levelBefore);
            AssertThat(StoryManager.Instance.ChronalDustCollected).IsEqual(dustBefore);
            AssertThat(StoryManager.Instance.ChronalRewindsRemaining).IsEqual(rewindsBefore);
        } finally {
            manager.CurrentSession = saved;
        }
    }

    [TestCase]
    public void BackFromThePickerLeavesTheRouteAndDisarmsIt() {
        GameManager manager = GameManager.Instance;
        if (manager == null) return;
        SessionData saved = manager.CurrentSession;
        try {
            SessionData primed = saved;
            primed.CalibrationReturnScenePath = CalibrationRoute.MainMenuScenePath;
            primed.CalibrationDrillIndex = 3;
            manager.CurrentSession = primed;

            (Node host, CalibrationDrillPicker picker) = OpenPicker("DrillPickerBackHost");
            try {
                AssertThat(picker.ApplyBackToSession()).IsEqual(CalibrationRoute.MainMenuScenePath);
            } finally {
                Teardown(host);
            }

            // Disarmed on the way out: an ordinary Fighter match pause afterwards
            // must still exit to the Fighter lobby, not to calibration.
            AssertThat(CalibrationRoute.IsActive(manager.CurrentSession)).IsFalse();
            AssertThat(manager.CurrentSession.CalibrationDrillIndex).IsEqual(0);
        } finally {
            manager.CurrentSession = saved;
        }
    }

    [TestCase]
    public void TheDrillListBuildsTheCatalogsSixDrillsAndIsFullyFocusAuthored() {
        (Node host, CalibrationDrillList list) = OpenList("DrillListShapeHost", CalibrationRoute.MainMenuScenePath);
        try {
            AssertThat(list.DrillButtons.Count).IsEqual(CalibrationDrillCatalog.Count);
            var issues = new List<string>();
            for (int index = 0; index < list.DrillButtons.Count; index++) {
                string text = list.DrillButtons[index].Text;
                CalibrationDrillDefinition drill = CalibrationDrillCatalog.At(index);
                if (!text.StartsWith($"{index + 1}.  ")) issues.Add($"row {index} is not numbered: '{text}'");
                if (text.Contains(drill.NameKey)) issues.Add($"row {index} shows the raw key {drill.NameKey}");
                if (text.Contains(drill.ObjectiveKey)) issues.Add($"row {index} shows the raw objective key");
            }
            if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

            // Six drill rows plus Back and Exit Calibration.
            AssertChainCovers(list.GetNode<Control>("List"), CalibrationDrillCatalog.Count + 2);

            // The character line resolves a name rather than leaking a raw key.
            string line = list.GetNode<Label>(ListRoot + "CharacterLine").Text;
            AssertThat(line.Contains("drill_list_character")).IsFalse();
            AssertThat(line.Contains("character_")).IsFalse();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheMenuRouteBacksToThePickerAndTheHubRouteBacksToItsHub() {
        GameManager manager = GameManager.Instance;
        if (manager == null) return;
        SessionData saved = manager.CurrentSession;
        try {
            // Standalone route: Back returns to the picker and stays armed.
            (Node menuHost, CalibrationDrillList menuList) =
                OpenList("DrillListMenuBackHost", CalibrationRoute.MainMenuScenePath);
            try {
                AssertThat(CalibrationRoute.DrillListBackDestination(
                        manager.CurrentSession.CalibrationReturnScenePath))
                    .IsEqual(CalibrationRoute.PickerScenePath);
                AssertThat(menuList.ApplyBackToSession()).IsEqual(CalibrationRoute.PickerScenePath);
                AssertThat(CalibrationRoute.IsActive(manager.CurrentSession))
                    .OverrideFailureMessage("Backing to the picker must stay inside the route")
                    .IsTrue();
            } finally {
                Teardown(menuHost);
            }

            // Hub console route: there is no picker, so Back leaves for the hub.
            (Node hubHost, CalibrationDrillList hubList) =
                OpenList("DrillListHubBackHost", CalibrationRoute.HubScenePath);
            try {
                AssertThat(CalibrationRoute.DrillListBackDestination(
                        manager.CurrentSession.CalibrationReturnScenePath))
                    .IsEqual(CalibrationRoute.HubScenePath);
                AssertThat(hubList.ApplyBackToSession()).IsEqual(CalibrationRoute.HubScenePath);
                AssertThat(CalibrationRoute.IsActive(manager.CurrentSession)).IsFalse();
            } finally {
                Teardown(hubHost);
            }
        } finally {
            manager.CurrentSession = saved;
        }
    }

    [TestCase]
    public void TheHubConsoleKeepsItsPracticeBoutAndItsDrillsEntryCostsTheCampaignNothing() {
        GameManager manager = GameManager.Instance;
        if (manager == null) return;
        SessionData saved = manager.CurrentSession;
        bool markerBefore = SessionExitGuard.TryReadMarker(out int markerSlot);
        try {
            SessionData campaign = saved;
            campaign.ActiveSaveSlot = 1;
            campaign.SelectedCharacterID = "joan";
            campaign.ReturnToHubAfterFighterMatch = true;
            campaign.CalibrationReturnScenePath = "";
            manager.CurrentSession = campaign;

            string destination = HolodeckConsolePanel.ApplyCalibrationRouteToSession();
            AssertThat(destination).IsEqual(CalibrationRoute.DrillListScenePath);

            SessionData session = manager.CurrentSession;
            AssertThat(session.CalibrationReturnScenePath).IsEqual(CalibrationRoute.HubScenePath);
            // The campaign attempt and its resources survive the detour untouched.
            AssertThat(session.ActiveSaveSlot).IsEqual(1);
            AssertThat(session.SelectedCharacterID).IsEqual("joan");
            AssertThat(session.Difficulty).IsEqual(campaign.Difficulty);
            // No exit fee: the abnormal-exit session marker is not written, cleared
            // or re-slotted by entering calibration (ruling 2.B deleted the fee
            // itself; this pins that the drill route never trips its machinery).
            bool markerAfter = SessionExitGuard.TryReadMarker(out int slotAfter);
            AssertThat(markerAfter).IsEqual(markerBefore);
            if (markerBefore && markerAfter) AssertThat(slotAfter).IsEqual(markerSlot);
        } finally {
            manager.CurrentSession = saved;
        }
    }

    [TestCase]
    public void NeitherRouteEverReachesAStorySaveSlotOrACampaignReward() {
        GameManager manager = GameManager.Instance;
        if (manager == null || SaveManager.Instance == null) return;
        SessionData saved = manager.CurrentSession;
        StorySaveData[] originals = ClearAllSlots();
        try {
            // Walk the whole standalone route with every slot empty. Nothing on it
            // may write a slot, and the drill index is session-only — so quitting
            // or relaunching can never resume a drill as a Story attempt.
            SessionData session = saved;
            session.ActiveSaveSlot = -1;
            session.CalibrationReturnScenePath = CalibrationRoute.MainMenuScenePath;
            manager.CurrentSession = session;

            (Node pickerHost, CalibrationDrillPicker picker) = OpenPicker("DrillIsolationPickerHost");
            try {
                picker.ApplyChoiceToSession(0);
            } finally {
                Teardown(pickerHost);
            }

            (Node listHost, CalibrationDrillList list) =
                OpenList("DrillIsolationListHost", CalibrationRoute.MainMenuScenePath);
            try {
                AssertThat(list.ApplyDrillToSession(4)).IsEqual(CalibrationRoute.DrillScenePath);
            } finally {
                Teardown(listHost);
            }

            AssertThat(manager.CurrentSession.CalibrationDrillIndex).IsEqual(4);
            AssertThat(manager.CurrentSession.ActiveSaveSlot).IsEqual(-1);
            for (int slot = 0; slot < 3; slot++) {
                AssertThat(SaveManager.Instance.SaveSlots[slot] == null)
                    .OverrideFailureMessage($"The drill route wrote save slot {slot}")
                    .IsTrue();
            }
        } finally {
            manager.CurrentSession = saved;
            RestoreSlots(originals);
        }
    }

    // ---- helpers -------------------------------------------------------------

    private static (Node, CalibrationDrillPicker) OpenPicker(string hostName) {
        var host = new Node { Name = hostName };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var packed = ResourceLoader.Load<PackedScene>(CalibrationRoute.PickerScenePath);
        var picker = packed.Instantiate<CalibrationDrillPicker>();
        host.AddChild(picker);
        return (host, picker);
    }

    private static (Node, CalibrationDrillList) OpenList(string hostName, string returnPath) {
        if (GameManager.Instance != null) {
            SessionData session = GameManager.Instance.CurrentSession;
            session.CalibrationReturnScenePath = returnPath;
            GameManager.Instance.CurrentSession = session;
        }
        var host = new Node { Name = hostName };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var packed = ResourceLoader.Load<PackedScene>(CalibrationRoute.DrillListScenePath);
        var list = packed.Instantiate<CalibrationDrillList>();
        host.AddChild(list);
        return (host, list);
    }

    private static void AssertChainCovers(Control root, int expected) {
        List<Control> chain = FocusChainBuilder.Collect(root);
        AssertThat(chain.Count)
            .OverrideFailureMessage($"{root.Name} focus chain size")
            .IsEqual(expected);
        foreach (Control control in chain) {
            bool linked = !control.FocusNeighborTop.IsEmpty || !control.FocusNeighborBottom.IsEmpty;
            AssertThat(linked)
                .OverrideFailureMessage($"{root.Name}/{control.Name} has no focus neighbour")
                .IsTrue();
        }
    }

    private static StorySaveData[] ClearAllSlots() {
        if (SaveManager.Instance == null) return null;
        var originals = new StorySaveData[3];
        for (int slot = 0; slot < 3; slot++) {
            originals[slot] = SaveManager.Instance.SaveSlots[slot];
            SaveManager.Instance.SaveSlots[slot] = null;
        }
        return originals;
    }

    private static void RestoreSlots(StorySaveData[] originals) {
        if (originals == null || SaveManager.Instance == null) return;
        for (int slot = 0; slot < originals.Length; slot++) {
            SaveManager.Instance.SaveSlots[slot] = originals[slot];
        }
    }

    private static void Teardown(Node host) {
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
