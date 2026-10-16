using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The authored Fighter Mode character-select flow, reworked for audit M-28 to
/// carry the designed select-screen mechanics (design-godot.md:2586-2599):
/// per-player selection tokens driven by each player's own device, Reserved/
/// Occupied tile painting (V7.3 ruling #17 removed the old duplicate-pick
/// prevention — mirror matches are allowed), a
/// per-player READY state, the 3.0-second cancelable countdown, and a distinct
/// full-screen stage-select state that reuses the catalog population,
/// ProductionReady gating, preview plates, and match rules.
///
/// <para>Package 12 W5 (H05 per adopted D4(a)): the CPU toggle is gone from the
/// Local Versus select — Versus CPU is its own Fighter-menu entry, a P1-only
/// select that opens the CPU configuration panel. Hazards are a single On/Off
/// toggle, Items gained the Meter pickups toggle, and the grid and stage list
/// end in Random entries (G15a).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CharacterSelectSceneTests {

    private const string ScenePath = "res://scenes/menus/CharacterSelect.tscn";
    private const string SelectRoot = "SelectPhase/Root/";
    private const string StageRoot = "StagePhase/Root/";

    private static SessionData? _savedSession;

    [TestCase]
    public void TheAuthoredSceneCarriesEveryControlTheScriptBindsByPath() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            AssertThat(screen.GetNodeOrNull<ColorRect>("Background") != null).IsTrue();
            foreach (string path in new[] {
                         SelectRoot + "Title", SelectRoot + "PlayersRow/Grid",
                         SelectRoot + "PlayersRow/P1Panel/P1Name",
                         SelectRoot + "PlayersRow/P1Panel/P1Ready",
                         SelectRoot + "PlayersRow/P1Panel/P1Stats",
                         SelectRoot + "PlayersRow/P2Panel/P2Name",
                         SelectRoot + "PlayersRow/P2Panel/P2Ready",
                         SelectRoot + "PlayersRow/P2Panel/P2Stats",
                         SelectRoot + "PlayersRow/Grid/RandomTile",
                         SelectRoot + "FeedbackLabel", SelectRoot + "CountdownLabel",
                         SelectRoot + "HintLabel", SelectRoot + "ButtonRow/BackButton",
                         StageRoot + "StageTitle",
                         StageRoot + "StageRow/StageSelect", StageRoot + "StageRow/StagePreview",
                         StageRoot + "RulesRow/MatchMode", StageRoot + "RulesRow/StockCount",
                         StageRoot + "RulesRow/TimeLimit", StageRoot + "RulesRow/ItemFrequency",
                         StageRoot + "RulesRow/MeterPickups", StageRoot + "RulesRow/StageHazards",
                         StageRoot + "StageButtonRow/StageBackButton",
                         StageRoot + "StageButtonRow/FightButton" }) {
                AssertThat(screen.GetNodeOrNull<Control>(path) != null)
                    .OverrideFailureMessage($"Missing authored control {path}")
                    .IsTrue();
            }
            // D4(a): the Local Versus select no longer carries a CPU toggle or a
            // CPU difficulty selector — Versus CPU is its own Fighter-menu entry.
            AssertThat(screen.GetNodeOrNull(SelectRoot + "ModeRow") == null)
                .OverrideFailureMessage("The retired CPU toggle row is still authored.")
                .IsTrue();
            AssertThat(screen.GetNodeOrNull(StageRoot + "RulesRow/HazardFrequency") == null)
                .OverrideFailureMessage("The retired hazard frequency selector is still authored.")
                .IsTrue();
            // The screen opens in the selection state with the stage state hidden.
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.Selection);
            AssertThat(screen.GetNode<Control>("SelectPhase").Visible).IsTrue();
            AssertThat(screen.GetNode<Control>("StagePhase").Visible).IsFalse();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheRosterTilesAreCursorDrivenClickableButtonsOutsideTheFocusChain() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            var grid = screen.GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
            // Package 11 A6b: the scene must carry one authored tile per
            // ROSTER member, not per the number nine — plus (G15a) the Random
            // tile. The screen itself binds defensively (a roster larger than the
            // grid leaves the tail unbound rather than throwing), so this
            // assertion is the signal that says "a character was added — author
            // its tile".
            int rosterCount = FTT.Core.CharacterRoster.Count;
            AssertThat(grid.GetChildCount()).IsEqual(rosterCount + 1);
            AssertThat(screen.RandomTileIndex).IsEqual(rosterCount);
            for (int index = 0; index <= rosterCount; index++) {
                bool random = index == rosterCount;
                var tile = grid.GetNodeOrNull<Button>(random ? "RandomTile" : $"CharacterButton{index}");
                AssertThat(tile != null)
                    .OverrideFailureMessage($"Tile {index} is not a Button")
                    .IsTrue();
                var nameLabel = tile.GetNodeOrNull<Label>("CharacterName");
                AssertObject(nameLabel)
                    .OverrideFailureMessage($"Tile {index} has no dedicated name label")
                    .IsNotNull();
                AssertThat(nameLabel.Text.Trim().Length > 0)
                    .OverrideFailureMessage($"Tile {index} has no name")
                    .IsTrue();
                AssertThat(nameLabel.MouseFilter)
                    .OverrideFailureMessage($"Tile {index} name intercepts tile input")
                    .IsEqual(Control.MouseFilterEnum.Ignore);
                var portrait = tile.GetNodeOrNull<TextureRect>("Portrait");
                AssertObject(portrait)
                    .OverrideFailureMessage($"Tile {index} has no portrait")
                    .IsNotNull();
                if (random) {
                    // G15a: a silhouette placeholder until Package 10 art.
                    AssertThat(tile.GetNodeOrNull<ColorRect>("Silhouette") != null)
                        .OverrideFailureMessage("The Random tile has no silhouette placeholder.")
                        .IsTrue();
                } else {
                    AssertObject(portrait.Texture)
                        .OverrideFailureMessage($"Tile {index} portrait texture is missing")
                        .IsNotNull();
                }
                AssertThat(portrait.Size)
                    .OverrideFailureMessage($"Tile {index} portrait does not have a dedicated 72px layer")
                    .IsEqual(new Vector2(72f, 72f));
                AssertThat(portrait.MouseFilter)
                    .OverrideFailureMessage($"Tile {index} portrait intercepts tile input")
                    .IsEqual(Control.MouseFilterEnum.Ignore);
                // Tiles are driven by the per-player polled cursors (and mouse
                // clicks), never by global focus navigation: any device can steer
                // focus, which would let Player 2's pad move Player 1's token.
                AssertThat(tile.FocusMode)
                    .OverrideFailureMessage($"Tile {index} is focus-navigable")
                    .IsEqual(Control.FocusModeEnum.None);
                AssertThat(tile.IsInGroup(FocusChainBuilder.SkipGroup))
                    .OverrideFailureMessage($"Tile {index} is not focus_skip")
                    .IsTrue();
            }
        } finally {
            Teardown(host);
        }
    }

    /// <summary>
    /// V7.3 UI-scale pass: the tile name labels ride the SmallLabel theme
    /// variation instead of a font-size override, so they follow the
    /// accessibility UI scale.
    /// </summary>
    [TestCase]
    public void TileNameLabelsUseTheSmallVariationWithNoFontSizeOverride() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            var grid = screen.GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
            for (int index = 0; index < 9; index++) {
                var nameLabel = grid.GetNode<Button>($"CharacterButton{index}")
                    .GetNodeOrNull<Label>("CharacterName");
                AssertObject(nameLabel).IsNotNull();
                AssertThat(nameLabel.ThemeTypeVariation.ToString())
                    .IsEqual(UIPalette.SmallLabelVariation);
                AssertThat(nameLabel.HasThemeFontSizeOverride("font_size"))
                    .OverrideFailureMessage($"Tile {index} name label carries a font-size override.")
                    .IsFalse();
            }
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void EachPhaseAuthorsItsOwnFocusChainAndTheStageChainReachesTheSpinBoxEditors() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            // V7.3 Fighter Onboarding added the Move List and Systems Card footer
            // buttons (3 -> 5); Package 11 A6 added the M01 Steam Remote Play
            // Together notice (5 -> 6); Package 12 W5 removed the CPU toggle and
            // CPU difficulty with the Versus CPU split (6 -> 4).
            IReadOnlyList<Control> selectionChain = screen.FocusChain;
            AssertThat(selectionChain.Count).IsEqual(4);
            AssertThat(selectionChain.Contains(
                screen.GetNode<Button>(SelectRoot + "ButtonRow/MoveListButton"))).IsTrue();
            AssertThat(selectionChain.Contains(
                screen.GetNode<Button>(SelectRoot + "ButtonRow/SystemsCardButton"))).IsTrue();
            AssertChainAuthored(selectionChain);
            AssertThat(screen.GetNode<Label>(SelectRoot + "RemotePlayNotice").HasFocus()).IsTrue();

            DriveLocalFlowToStagePhase(screen);
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.StageSelect);

            // Stage, mode, two spin-box editors, items, meter pickups, hazards,
            // back, fight.
            IReadOnlyList<Control> stageChain = screen.FocusChain;
            AssertThat(stageChain.Count).IsEqual(9);
            AssertChainAuthored(stageChain);

            // A SpinBox keeps its editable LineEdit as an INTERNAL child; without
            // the explicit splice the stock count and time limit are unreachable
            // by keyboard and controller.
            AssertThat(stageChain.Contains(screen.GetNode<SpinBox>(StageRoot + "RulesRow/StockCount").GetLineEdit()))
                .OverrideFailureMessage("The stock-count editor is not reachable by focus.").IsTrue();
            AssertThat(stageChain.Contains(screen.GetNode<SpinBox>(StageRoot + "RulesRow/TimeLimit").GetLineEdit()))
                .OverrideFailureMessage("The time-limit editor is not reachable by focus.").IsTrue();
            AssertThat(stageChain.Contains(screen.GetNode<CheckButton>(StageRoot + "RulesRow/StageHazards")))
                .OverrideFailureMessage("The hazard toggle is not reachable by focus.").IsTrue();
            AssertThat(screen.GetNode<OptionButton>(StageRoot + "StageRow/StageSelect").HasFocus()).IsTrue();
        } finally {
            Teardown(host);
        }
    }

    /// <summary>
    /// Package 11 A6 (M01). Steam Remote Play Together is the launch netplay
    /// message, and it needs no netcode: Remote Play is host-side streamed local
    /// multiplayer, so the shipped local 1v1 path <i>is</i> the feature and the
    /// implementation is a notice on the Fighter local-play surface. The scene
    /// stores the raw translation key per the repo convention, so the assertion is
    /// on the key rather than the English.
    /// </summary>
    [TestCase]
    public void TheFighterFooterCarriesTheSteamRemotePlayNoticeInsideTheFocusChain() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            var notice = screen.GetNodeOrNull<Label>(SelectRoot + "RemotePlayNotice");
            AssertObject(notice)
                .OverrideFailureMessage("The Remote Play Together notice is missing from the footer.")
                .IsNotNull();
            AssertString(notice.Text)
                .OverrideFailureMessage("Authored .tscn text must be the raw key, not the English.")
                .IsEqual("fighter_remote_play_notice");

            // Focusable and chained, so a controller-only player can reach and read
            // it; the chain is re-authored whenever the surface changes.
            AssertThat(notice.FocusMode).IsEqual(Control.FocusModeEnum.All);
            AssertThat(screen.FocusChain.Contains(notice))
                .OverrideFailureMessage("The notice is not in the selection focus chain.").IsTrue();
            AssertChainAuthored(screen.FocusChain);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void ClickingATileHoversItAndClickingTheHoveredTileConfirmsIt() {
        TranslationServer.SetLocale("en");
        CharacterSelectScreen screen = Open(out Node host);
        try {
            AssertThat(screen.SelectedIndex).IsEqual(0);
            string first = screen.GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1Name").Text;

            var tile = screen.GetNode<Button>(SelectRoot + "PlayersRow/Grid/CharacterButton4");
            tile.EmitSignal(BaseButton.SignalName.Pressed);

            // First click reserves (hover): the cursor moved, nothing locked yet.
            AssertThat(screen.SelectedIndex).IsEqual(4);
            AssertThat(screen.IsSlotReady(0)).IsFalse();
            string second = screen.GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1Name").Text;
            AssertThat(second).IsNotEqual(first);
            AssertThat(screen.GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1Stats").Text)
                .IsNotEqual(TranslationServer.Translate("fighter_stats_unavailable").ToString());

            // Second click on the hovered tile confirms: READY banner up.
            tile.EmitSignal(BaseButton.SignalName.Pressed);
            AssertThat(screen.IsSlotReady(0)).IsTrue();
            AssertThat(screen.GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1Ready").Visible).IsTrue();
        } finally {
            Teardown(host);
        }
    }

    /// <summary>
    /// V7.3 ruling #17: mirror matches are allowed — the old duplicate-pick
    /// refusal (and its unavailable-feedback flash) is gone. Both players may
    /// lock the same tile, and doing so readies the match like any other pair
    /// of picks. G12: the shared tile carries both slot shapes.
    /// </summary>
    [TestCase]
    public void BothPlayersMayLockTheSameCharacter() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            // P1 locks tile 0.
            AssertThat(screen.TryConfirm(0)).IsTrue();
            AssertThat(screen.IsSlotReady(0)).IsTrue();

            // P2 moves onto the SAME tile and locks it freely.
            AssertThat(screen.TryMoveCursor(1, -1, 0)).IsTrue();
            AssertThat(screen.GetCursor(1)).IsEqual(0);
            AssertThat(screen.TryConfirm(1))
                .OverrideFailureMessage("A mirror pick must be allowed, not refused.")
                .IsTrue();
            AssertThat(screen.IsSlotReady(1)).IsTrue();
            AssertThat(screen.SelectedIndex).IsEqual(0);
            AssertThat(screen.OpponentIndex).IsEqual(0);
            AssertThat(screen.TileBadgeText(0)).IsEqual(
                FTT.Combat.PlayerSlotPalettes.PlayerOneGlyph + FTT.Combat.PlayerSlotPalettes.PlayerTwoGlyph);

            // Both locks on one tile ready the match: the countdown is running.
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.Countdown);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void BothPlayersReadyStartsAThreeSecondCountdownAnyPlayerCanAbort() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            AssertThat(screen.TryConfirm(0)).IsTrue();
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.Selection);
            AssertThat(screen.TryConfirm(1)).IsTrue();

            // Both ready: the designed 3.0 s countdown is running.
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.Countdown);
            AssertThat(screen.CountdownRemaining).IsEqual(3.0f);
            AssertThat(screen.GetNode<Label>(SelectRoot + "CountdownLabel").Visible).IsTrue();

            // P2 cancels mid-countdown: the timer aborts, P2 returns to selection
            // unreadied, and P1's lock survives.
            screen.AdvanceCountdown(1.0f);
            AssertThat(screen.TryCancel(1)).IsTrue();
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.Selection);
            AssertThat(screen.IsSlotReady(1)).IsFalse();
            AssertThat(screen.IsSlotReady(0)).IsTrue();
            AssertThat(screen.GetNode<Label>(SelectRoot + "CountdownLabel").Visible).IsFalse();

            // Re-ready restarts a full countdown; at zero the screen swaps to the
            // stage-select state.
            AssertThat(screen.TryConfirm(1)).IsTrue();
            AssertThat(screen.CountdownRemaining).IsEqual(3.0f);
            screen.AdvanceCountdown(3.1f);
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.StageSelect);
            AssertThat(screen.GetNode<Control>("SelectPhase").Visible).IsFalse();
            AssertThat(screen.GetNode<Control>("StagePhase").Visible).IsTrue();

            // Stage-phase Back returns everyone to selection unreadied.
            screen.ReturnToSelection();
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.Selection);
            AssertThat(screen.IsSlotReady(0)).IsFalse();
            AssertThat(screen.IsSlotReady(1)).IsFalse();
        } finally {
            Teardown(host);
        }
    }

    /// <summary>
    /// H05 (adopted D4(a)): Versus CPU is a P1-only select. Player 2 drives no
    /// token, and locking Player 1 opens the Holodeck console panel as the
    /// front-end CPU configuration screen, whose Back unlocks Player 1 and whose
    /// launch writes a Versus CPU session.
    /// </summary>
    [TestCase]
    public void VersusCpuIsAPlayerOneOnlySelectThatOpensTheCpuConfigurationPanel() {
        if (GameManager.Instance == null) return;
        CharacterSelectScreen screen = Open(out Node host, FighterMatchOrigin.VersusCpu);
        try {
            AssertThat(screen.IsVersusCpuFlow).IsTrue();
            // Player 2 has no token here.
            AssertThat(screen.TryMoveCursor(1, 1, 0)).IsFalse();
            AssertThat(screen.TryConfirm(1)).IsFalse();

            // P1 picks leonardo (tile 2) and locks: no countdown, straight to the panel.
            AssertThat(screen.TryMoveCursor(0, 2, 0)).IsTrue();
            AssertThat(screen.TryConfirm(0)).IsTrue();
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.StageSelect);
            HolodeckConsolePanel panel = screen.CpuConfigPanel;
            AssertObject(panel).IsNotNull();
            AssertThat(panel.FrontEnd).IsTrue();
            AssertThat(panel.GetNodeOrNull("Panel/Layout/CalibrationDrillsButton") == null)
                .OverrideFailureMessage("The front-end Versus CPU screen must not offer the hub's drills entry.")
                .IsTrue();

            panel.CpuDifficultySelect.Select((int)CpuDifficulty.Hard);
            panel.CpuCharacterSelect.Select(1); // joan
            FTT.Environment.FighterStageData stage = panel.ApplyToSession();
            AssertThat(stage != null).IsTrue();
            SessionData session = GameManager.Instance.CurrentSession;
            AssertThat(session.SelectedCharacterID).IsEqual("leonardo");
            AssertThat(session.OpponentCharacterID).IsEqual("joan");
            AssertThat(session.FighterOpponentType).IsEqual(FighterOpponentType.Cpu);
            AssertThat(session.FighterMatchOrigin).IsEqual(FighterMatchOrigin.VersusCpu);
            AssertThat(session.ReturnToHubAfterFighterMatch).IsFalse();
            AssertThat(session.CpuDifficulty).IsEqual(CpuDifficulty.Hard);

            // Back out of the panel: P1 unlocks and the select is live again.
            screen.ReturnToSelection();
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.Selection);
            AssertThat(screen.IsSlotReady(0)).IsFalse();
            AssertThat(screen.ResolveBackScenePath()).IsEqual(FighterFlowRoutes.FighterMenuScenePath);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void PlayerTwoTokenIsDrivenByPlayerTwoDeviceOnly() {
        if (InputManager.Instance == null) return;
        CharacterSelectScreen screen = Open(out Node host);
        var neutral = new BufferedInputSource();
        var playerTwo = new BufferedInputSource();
        try {
            AssertThat(screen.GetCursor(0)).IsEqual(0);
            AssertThat(screen.GetCursor(1)).IsEqual(1);

            neutral.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
            playerTwo.SetNextFrame(PlayerInputFrame.Create(0, 1f, 0f, GameplayButtons.None));
            InputManager.Instance.SetInputSource(0, neutral);
            InputManager.Instance.SetInputSource(1, playerTwo);

            // One polled tick: P2's held-right frame steps P2's token once; P1's
            // token does not move, because P1's device reports neutral.
            screen._PhysicsProcess(1.0 / 60.0);
            AssertThat(screen.GetCursor(1)).IsEqual(2);
            AssertThat(screen.GetCursor(0)).IsEqual(0);

            // Holding the axis does not auto-repeat: movement is edge-triggered.
            screen._PhysicsProcess(1.0 / 60.0);
            AssertThat(screen.GetCursor(1)).IsEqual(2);
        } finally {
            InputManager.Instance.ClearInputSource(0);
            InputManager.Instance.ClearInputSource(1);
            Teardown(host);
        }
    }

    // ---- 9.1 press-to-join (2026-10-16, CM-INPUT) ---------------------------
    // Real physics frames: the screen polls InputManager once per engine tick,
    // and the manager polls the (fake) devices before sampling, so the join and
    // the seeded first frame are exercised end to end. Since CM-FIXA-INPUT
    // (2026-10-16) the join pre-claims no slot: the first device to press is
    // Player 1, the next different one Player 2.

    [TestCase]
    public async Task NoPlayerTwoTokenAppearsUntilPadBPressesAndThatPressConfirmsNothing() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0, 1);
        input.AutoAssignDevices();
        CharacterSelectScreen screen = Open(out Node host);
        SceneTree tree = screen.GetTree();
        JoyButton confirm = FakeInputDeviceProbe.PadButtonFor(InputManager.Actions.BasicAttack);
        Key interact = FakeInputDeviceProbe.KeyFor(InputManager.Actions.Interact);
        var prompt = screen.GetNode<Label>(SelectRoot + "PlayersRow/P2Panel/P2JoinPrompt");
        try {
            AssertThat(input.JoinMode).IsEqual(LocalJoinMode.Joined);
            await Frames(tree, 2);
            // Player 1 joins first, on the keyboard.
            probe.SetKey(interact, true);
            await Frames(tree, 3);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.KeyboardDevice);
            AssertThat(screen.IsSlotReady(0)).IsFalse();
            AssertThat(screen.IsPlayerTwoJoined).IsFalse();
            AssertThat(prompt.Visible).IsTrue();
            AssertThat(prompt.Text).IsEqual("select_press_to_join");
            AssertThat(screen.TileBadgeText(screen.GetCursor(1)).Contains(FTT.Combat.PlayerSlotPalettes.PlayerTwoGlyph))
                .OverrideFailureMessage("An unjoined Player 2 already shows a token.")
                .IsFalse();

            probe.SetButton(1, confirm, true);
            await Frames(tree, 3);
            AssertThat(input.GetDeviceForPlayer(1)).IsEqual(1);
            AssertThat(screen.IsPlayerTwoJoined).IsTrue();
            AssertThat(prompt.Visible).IsFalse();
            AssertThat(screen.TileBadgeText(screen.GetCursor(1)).Contains(FTT.Combat.PlayerSlotPalettes.PlayerTwoGlyph))
                .IsTrue();
            AssertThat(screen.IsSlotReady(1))
                .OverrideFailureMessage("The press that joined Player 2 also confirmed a tile.")
                .IsFalse();

            // The next press is an ordinary confirm.
            probe.SetButton(1, confirm, false);
            await Frames(tree, 2);
            probe.SetButton(1, confirm, true);
            await Frames(tree, 2);
            AssertThat(screen.IsSlotReady(1)).IsTrue();
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    [TestCase]
    public async Task PlayerOnesDeviceCannotTakeTheSecondSlot() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0, 1);
        input.AutoAssignDevices();
        input.AssignJoypadToPlayer(0, 0);
        CharacterSelectScreen screen = Open(out Node host);
        SceneTree tree = screen.GetTree();
        JoyButton confirm = FakeInputDeviceProbe.PadButtonFor(InputManager.Actions.BasicAttack);
        try {
            // The pad that drove the menus is not pre-claimed: its own press joins it.
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.UnassignedDevice);
            await Frames(tree, 2);
            probe.SetButton(0, JoyButton.A, true);
            await Frames(tree, 3);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(0);
            // Further presses on it are Player 1's own, never a second join.
            probe.SetButton(0, confirm, true);
            await Frames(tree, 3);
            probe.SetButton(0, JoyButton.A, false);
            probe.SetButton(0, confirm, false);
            await Frames(tree, 2);
            probe.SetButton(0, JoyButton.A, true);
            await Frames(tree, 3);
            AssertThat(input.GetDeviceForPlayer(1))
                .OverrideFailureMessage("Player 1's pad also claimed Player 2's slot.")
                .IsEqual(InputManager.UnassignedDevice);
            AssertThat(input.GetPlayerForDevice(0)).IsEqual(0);
            AssertThat(screen.IsPlayerTwoJoined).IsFalse();
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    [TestCase]
    public async Task TheKeyboardCanJoinAsPlayerTwo() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        input.AssignJoypadToPlayer(0, 0);
        CharacterSelectScreen screen = Open(out Node host);
        SceneTree tree = screen.GetTree();
        Key interact = FakeInputDeviceProbe.KeyFor(InputManager.Actions.Interact);
        try {
            AssertThat(interact).IsNotEqual(Key.None);
            await Frames(tree, 2);
            probe.SetButton(0, JoyButton.A, true);
            await Frames(tree, 3);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(0);
            AssertThat(screen.IsPlayerTwoJoined).IsFalse();
            probe.SetKey(interact, true);
            await Frames(tree, 3);
            AssertThat(input.GetDeviceForPlayer(1)).IsEqual(InputManager.KeyboardDevice);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(0);
            AssertThat(screen.IsPlayerTwoJoined).IsTrue();
            AssertThat(screen.IsSlotReady(1)).IsFalse();
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    /// <summary>
    /// CM-FIXA-INPUT (2026-10-16; decision 9.1 read literally): with the menus
    /// driven by the keyboard or mouse, the keyboard used to stay pre-claimed as
    /// Player 1, so pad A joined as Player 2 and pad B could not join at all. Now
    /// no slot is pre-claimed, so two pads always both join; neither joining press
    /// confirms a tile, and the keyboard, now a spare device, can neither join nor
    /// back the players out. Before anyone has joined, any device's cancel still
    /// leaves the screen.
    /// </summary>
    [TestCase]
    public async Task TwoPadsBothJoinAfterKeyboardDrivenMenus() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0, 1);
        input.AutoAssignDevices();
        AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.KeyboardDevice);
        CharacterSelectScreen screen = Open(out Node host);
        SceneTree tree = screen.GetTree();
        int backs = 0;
        screen.BackOverrideForTesting = () => backs++;
        try {
            await Frames(tree, 2);
            AssertThat(input.AnySlotJoined())
                .OverrideFailureMessage("The device that drove the menus was pre-claimed as Player 1.")
                .IsFalse();
            // Nobody has joined yet: any device's Escape leaves the select, as Back does.
            screen.GetViewport().PushInput(KeyPress(Key.Escape));
            AssertThat(backs).IsEqual(1);
            backs = 0;

            probe.SetButton(0, JoyButton.A, true);
            await Frames(tree, 3);
            probe.SetButton(1, JoyButton.A, true);
            await Frames(tree, 3);
            AssertThat(input.GetDeviceForPlayer(0))
                .OverrideFailureMessage("Pad A did not join as Player 1 after keyboard-driven menus.")
                .IsEqual(0);
            AssertThat(input.GetDeviceForPlayer(1))
                .OverrideFailureMessage("Pad B could not join as Player 2 after keyboard-driven menus.")
                .IsEqual(1);
            AssertThat(screen.IsPlayerTwoJoined).IsTrue();
            AssertThat(screen.IsSlotReady(0) || screen.IsSlotReady(1))
                .OverrideFailureMessage("A joining press also confirmed a tile.")
                .IsFalse();

            // The keyboard is now no player: its join press claims nothing, and its
            // Escape backs no one out.
            probe.SetKey(FakeInputDeviceProbe.KeyFor(InputManager.Actions.Interact), true);
            screen.GetViewport().PushInput(KeyPress(Key.Escape));
            await Frames(tree, 3);
            AssertThat(input.GetPlayerForDevice(InputManager.KeyboardDevice)).IsEqual(-1);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(0);
            AssertThat(backs)
                .OverrideFailureMessage("A device no player holds backed both players out of the select.")
                .IsEqual(0);
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    /// <summary>
    /// CM-FIXA-INPUT (2026-10-16): a join press does nothing but join. On this
    /// engine build <c>ui_accept</c> is Enter, Keypad Enter and Space, so a
    /// keyboard player who joins with Enter while a footer button has focus used
    /// to press that button as well — here Back, which threw both players out.
    /// The select now consumes a join press from an unclaimed device in
    /// <c>_Input</c>, before the GUI sees it.
    /// </summary>
    [TestCase]
    public async Task AKeyboardJoinPressOnAFocusedButtonOnlyJoins() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        input.AssignJoypadToPlayer(0, 0);
        CharacterSelectScreen screen = Open(out Node host);
        SceneTree tree = screen.GetTree();
        int backs = 0;
        screen.BackOverrideForTesting = () => backs++;
        try {
            await Frames(tree, 2);
            probe.SetButton(0, JoyButton.A, true);
            await Frames(tree, 3);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(0);

            // Focus sits on Back; the keyboard joins with Enter.
            screen.GetNode<Button>(SelectRoot + "ButtonRow/BackButton").GrabFocus();
            probe.SetKey(Key.Enter, true);
            screen.GetViewport().PushInput(KeyPress(Key.Enter));
            screen.GetViewport().PushInput(KeyRelease(Key.Enter));
            await Frames(tree, 3);
            AssertThat(backs)
                .OverrideFailureMessage("The keyboard's joining Enter also pressed the focused Back button.")
                .IsEqual(0);
            AssertThat(input.GetDeviceForPlayer(1)).IsEqual(InputManager.KeyboardDevice);
            AssertThat(screen.IsSlotReady(1)).IsFalse();

            // Once joined, Enter is an ordinary key again: it presses the button.
            probe.SetKey(Key.Enter, false);
            await Frames(tree, 1);
            screen.GetViewport().PushInput(KeyPress(Key.Enter));
            screen.GetViewport().PushInput(KeyRelease(Key.Enter));
            AssertThat(backs)
                .OverrideFailureMessage("Control: a claimed keyboard's Enter no longer pressed the focused button.")
                .IsEqual(1);
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    /// <summary>
    /// CM-FIXA-INPUT (2026-10-16): the select read every <c>ui_cancel</c> as Player
    /// 1's, so a keyboard Player 2's Escape unlocked Player 1's pick (or backed
    /// both players out). A cancel now belongs to the player whose device pressed
    /// it, and only Player 1's leaves the screen.
    /// </summary>
    [TestCase]
    public async Task EscapeCancelsOnlyThePickOfThePlayerWhoPressedIt() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        input.AssignJoypadToPlayer(0, 0);
        CharacterSelectScreen screen = Open(out Node host);
        SceneTree tree = screen.GetTree();
        int backs = 0;
        screen.BackOverrideForTesting = () => backs++;
        Key interact = FakeInputDeviceProbe.KeyFor(InputManager.Actions.Interact);
        try {
            await Frames(tree, 2);
            probe.SetButton(0, JoyButton.A, true);
            await Frames(tree, 3);
            probe.SetKey(interact, true);
            await Frames(tree, 3);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(0);
            AssertThat(input.GetDeviceForPlayer(1)).IsEqual(InputManager.KeyboardDevice);
            AssertThat(screen.TryConfirm(0)).IsTrue();

            // Player 2 (the keyboard) has nothing to unwind: nothing happens.
            screen.GetViewport().PushInput(KeyPress(Key.Escape));
            AssertThat(screen.IsSlotReady(0))
                .OverrideFailureMessage("Player 2's Escape unlocked Player 1's pick.")
                .IsTrue();
            AssertThat(backs)
                .OverrideFailureMessage("Player 2's Escape backed both players out of the select.")
                .IsEqual(0);

            // Player 2 locks; the countdown runs; Player 2's Escape aborts it and
            // unlocks Player 2 alone.
            probe.SetKey(interact, false);
            await Frames(tree, 2);
            probe.SetKey(interact, true);
            await Frames(tree, 2);
            AssertThat(screen.IsSlotReady(1)).IsTrue();
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.Countdown);
            screen.GetViewport().PushInput(KeyPress(Key.Escape));
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.Selection);
            AssertThat(screen.IsSlotReady(1)).IsFalse();
            AssertThat(screen.IsSlotReady(0))
                .OverrideFailureMessage("Player 2's Escape during the countdown unlocked Player 1's pick.")
                .IsTrue();
            AssertThat(backs).IsEqual(0);
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    /// <summary>
    /// CM-FIXA-INPUT (2026-10-16): Godot's documented default <c>ui_cancel</c>
    /// carries the pad's B, which is also a face (join) button and the token
    /// flow's Interact confirm. This engine build's <c>ui_cancel</c> is Escape
    /// only, so the case adds B for its duration — the configuration the review
    /// found the defect in. A friend's B join press must not back out of the
    /// select, and Player 2's B confirm must not unlock Player 1's pick.
    /// </summary>
    [TestCase]
    public async Task WithBInUiCancelAPadsBJoinOrConfirmNeverCancelsOrLeaves() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0, 1);
        input.AutoAssignDevices();
        var padB = new InputEventJoypadButton { Device = -1, ButtonIndex = JoyButton.B };
        bool added = !InputMap.ActionHasEvent("ui_cancel", padB);
        if (added) InputMap.ActionAddEvent("ui_cancel", padB);
        CharacterSelectScreen screen = Open(out Node host);
        SceneTree tree = screen.GetTree();
        int backs = 0;
        screen.BackOverrideForTesting = () => backs++;
        Key interact = FakeInputDeviceProbe.KeyFor(InputManager.Actions.Interact);
        try {
            AssertThat(PadPress(1, JoyButton.B).IsActionPressed("ui_cancel"))
                .OverrideFailureMessage("Harness: pad B is not ui_cancel.")
                .IsTrue();
            await Frames(tree, 2);
            probe.SetKey(interact, true);
            await Frames(tree, 3);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(InputManager.KeyboardDevice);

            // A friend presses B on pad 1 to join, with Player 1 unlocked.
            probe.SetButton(1, JoyButton.B, true);
            screen.GetViewport().PushInput(PadPress(1, JoyButton.B));
            await Frames(tree, 3);
            AssertThat(backs)
                .OverrideFailureMessage("Pad B's join press also backed out of the select.")
                .IsEqual(0);
            AssertThat(input.GetDeviceForPlayer(1)).IsEqual(1);
            AssertThat(screen.IsSlotReady(1)).IsFalse();

            // Player 1 locks; Player 2's next B is its confirm, never Player 1's cancel.
            AssertThat(screen.TryConfirm(0)).IsTrue();
            probe.SetButton(1, JoyButton.B, false);
            await Frames(tree, 2);
            probe.SetButton(1, JoyButton.B, true);
            screen.GetViewport().PushInput(PadPress(1, JoyButton.B));
            await Frames(tree, 2);
            AssertThat(screen.IsSlotReady(0))
                .OverrideFailureMessage("Player 2's B confirm unlocked Player 1's pick.")
                .IsTrue();
            AssertThat(screen.IsSlotReady(1)).IsTrue();
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.Countdown);
            AssertThat(backs).IsEqual(0);
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            if (added) InputMap.ActionEraseEvent("ui_cancel", padB);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    /// <summary>
    /// CM-FIXA-INPUT (2026-10-16): since 9.1 a pad can be Player 1 in Versus CPU,
    /// and the token flow confirms with Interact (B). With B in <c>ui_cancel</c>
    /// (Godot's documented default; see the case above) that confirm also left
    /// the select in the same frame. The pad's B now confirms only.
    /// </summary>
    [TestCase]
    public async Task APadOnlyPlayerOneConfirmsWithBInVersusCpuWithoutLeaving() {
        InputManager input = InputManager.Instance;
        if (input == null) return;
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, 0);
        input.AutoAssignDevices();
        input.AssignJoypadToPlayer(0, 0);
        var padB = new InputEventJoypadButton { Device = -1, ButtonIndex = JoyButton.B };
        bool added = !InputMap.ActionHasEvent("ui_cancel", padB);
        if (added) InputMap.ActionAddEvent("ui_cancel", padB);
        CharacterSelectScreen screen = Open(out Node host, FighterMatchOrigin.VersusCpu);
        SceneTree tree = screen.GetTree();
        int backs = 0;
        screen.BackOverrideForTesting = () => backs++;
        try {
            AssertThat(input.JoinMode).IsEqual(LocalJoinMode.FollowLastUsed);
            await Frames(tree, 2);
            AssertThat(input.GetDeviceForPlayer(0)).IsEqual(0);
            probe.SetButton(0, JoyButton.B, true);
            screen.GetViewport().PushInput(PadPress(0, JoyButton.B));
            await Frames(tree, 3);
            AssertThat(backs)
                .OverrideFailureMessage("Player 1's B confirm also backed out of the select.")
                .IsEqual(0);
            AssertThat(screen.IsSlotReady(0)).IsTrue();
            AssertThat(screen.CpuConfigPanel != null).IsTrue();
        } finally {
            probe.ReleaseAll();
            Teardown(host);
            if (added) InputMap.ActionEraseEvent("ui_cancel", padB);
            FakeInputDeviceProbe.Restore(input);
        }
    }

    private static InputEventKey KeyPress(Key key) =>
        new() { Keycode = key, PhysicalKeycode = key, Pressed = true };

    private static InputEventKey KeyRelease(Key key) =>
        new() { Keycode = key, PhysicalKeycode = key, Pressed = false };

    private static InputEventJoypadButton PadPress(int device, JoyButton button) =>
        new() { Device = device, ButtonIndex = button, Pressed = true };

    private static async Task Frames(SceneTree tree, int count) {
        for (int frame = 0; frame < count; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }

    [TestCase]
    public void TheHolodeckOriginStillBacksOutToTheHub() {
        CharacterSelectScreen screen = Open(out Node host, FighterMatchOrigin.Holodeck);
        try {
            AssertThat(screen.ResolveBackScenePath()).IsEqual("res://scenes/campaign/HubWorld.tscn");
        } finally {
            Teardown(host);
        }
        CharacterSelectScreen local = Open(out Node localHost);
        try {
            // Both front-end flows back out to the Fighter menu they came from.
            AssertThat(local.ResolveBackScenePath()).IsEqual(FighterFlowRoutes.FighterMenuScenePath);
        } finally {
            Teardown(localHost);
        }
    }

    [TestCase]
    public void ItemBandsSurviveAndHazardsAreAnOnOffToggleDefaultingOn() {
        // V7 "Match Settings Persist": the rule controls initialize from the
        // session's settings, so normalize the session first — this test pins
        // the first-run defaults, not another test's leftover house rules.
        // V7.3 ruling #18: items default to Medium; Package 12 W5: hazards are
        // On/Off (default On) and Meter pickups default Off (D5(b)).
        if (GameManager.Instance != null) {
            SessionData normalized = GameManager.Instance.CurrentSession;
            normalized.MatchSettings = MatchSettings.GetDefault();
            GameManager.Instance.CurrentSession = normalized;
        }
        CharacterSelectScreen screen = Open(out Node host);
        try {
            var items = screen.GetNode<OptionButton>(StageRoot + "RulesRow/ItemFrequency");
            AssertThat(items.ItemCount).IsEqual(4);
            AssertThat(items.GetSelectedId()).IsEqual((int)ChronalOrbFrequency.Medium);
            AssertThat(screen.GetNode<CheckButton>(StageRoot + "RulesRow/StageHazards").ButtonPressed).IsTrue();
            AssertThat(screen.GetNode<CheckButton>(StageRoot + "RulesRow/MeterPickups").ButtonPressed).IsFalse();
            // F21 (Package 11 A1c): two selectable modes — Stock and Time. The
            // retired Stock + Time combination is gone from the selector.
            AssertThat(screen.GetNode<OptionButton>(StageRoot + "RulesRow/MatchMode").ItemCount).IsEqual(2);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void StageSelectIsPopulatedFromTheCatalogAndEndsInARandomStage() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            var stages = screen.GetNode<OptionButton>(StageRoot + "StageRow/StageSelect");
            AssertThat(stages.ItemCount > 1).IsTrue();
            AssertThat(stages.Disabled).IsFalse();
            AssertThat(stages.GetItemId(stages.ItemCount - 1))
                .OverrideFailureMessage("G15a: the stage list must end in a Random entry.")
                .IsEqual(CharacterSelectScreen.RandomStageItemID);

            FTT.Environment.FighterStageCatalog catalog = FTT.Environment.FighterStageCatalog.LoadDefault();
            AssertThat(catalog != null).IsTrue();
            for (int index = 0; index < stages.ItemCount; index++) {
                // A raw key here would mean the catalog's DisplayNameKey is missing
                // from the compiled translation table.
                AssertThat(stages.GetItemText(index).StartsWith("stage_"))
                    .OverrideFailureMessage($"Stage row {index} shows a raw key")
                    .IsFalse();
            }
        } finally {
            Teardown(host);
        }
    }

    /// <summary>
    /// G15a: a Random character and a Random stage resolve from the per-match
    /// seed — the same seed the match then consumes — so a given seed always
    /// reveals the same picks.
    /// </summary>
    [TestCase]
    public void RandomTilesResolveFromThePerMatchSeed() {
        if (GameManager.Instance == null) return;
        const int seed = 424242;
        string[] roster = FTT.Core.CharacterRoster.ToArray();
        CharacterSelectScreen screen = Open(out Node host);
        try {
            screen.SeedOverride = seed;
            // Both players move to the Random tile (the last grid cell).
            MoveCursorTo(screen, 0, screen.RandomTileIndex);
            MoveCursorTo(screen, 1, screen.RandomTileIndex);
            AssertThat(screen.TryConfirm(0)).IsTrue();
            AssertThat(screen.TryConfirm(1)).IsTrue();
            screen.AdvanceCountdown(3.5f);
            var stages = screen.GetNode<OptionButton>(StageRoot + "StageRow/StageSelect");
            stages.Select(stages.ItemCount - 1);

            FTT.Environment.FighterStageData stage = screen.ApplySelectionToSession();
            AssertThat(stage != null).IsTrue();
            SessionData session = GameManager.Instance.CurrentSession;
            AssertThat(session.HasPendingMatchSeed).IsTrue();
            AssertThat(session.PendingMatchSeed).IsEqual(seed);
            AssertThat(session.SelectedCharacterID).IsEqual(
                roster[FighterRandomPick.Index(seed, FighterRandomPick.PlayerOneCharacterSalt, roster.Length)]);
            AssertThat(session.OpponentCharacterID).IsEqual(
                roster[FighterRandomPick.Index(seed, FighterRandomPick.PlayerTwoCharacterSalt, roster.Length)]);
            AssertThat(roster.Contains(session.SelectedCharacterID)).IsTrue();
            AssertThat(session.SelectedStageID.Length > 0).IsTrue();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheSelectionWritesTheWholeSessionAndMatchSettingsRoundTrip() {
        if (GameManager.Instance == null) return;
        CharacterSelectScreen screen = Open(out Node host);
        try {
            // P1 locks lincoln (tile 3) through the mouse path: hover, then confirm.
            var lincolnTile = screen.GetNode<Button>(SelectRoot + "PlayersRow/Grid/CharacterButton3");
            lincolnTile.EmitSignal(BaseButton.SignalName.Pressed);
            lincolnTile.EmitSignal(BaseButton.SignalName.Pressed);

            // P2 locks leonardo (tile 2) through the token API.
            AssertThat(screen.TryMoveCursor(1, 1, 0)).IsTrue();
            AssertThat(screen.TryConfirm(1)).IsTrue();
            screen.AdvanceCountdown(3.5f);
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.StageSelect);

            screen.GetNode<OptionButton>(StageRoot + "RulesRow/MatchMode").Select(1);
            screen.GetNode<SpinBox>(StageRoot + "RulesRow/StockCount").Value = 5;
            screen.GetNode<SpinBox>(StageRoot + "RulesRow/TimeLimit").Value = 120;
            screen.GetNode<OptionButton>(StageRoot + "RulesRow/ItemFrequency").Select(0);
            screen.GetNode<CheckButton>(StageRoot + "RulesRow/MeterPickups").ButtonPressed = true;
            screen.GetNode<CheckButton>(StageRoot + "RulesRow/StageHazards").ButtonPressed = false;

            // ApplySelectionToSession is the seam OnFight uses; pressing Fight for
            // real would change the scene out from under the test runner.
            FTT.Environment.FighterStageData stage = screen.ApplySelectionToSession();
            AssertThat(stage != null).IsTrue();
            AssertThat(ResourceLoader.Exists(stage.ScenePath)).IsTrue();

            SessionData session = GameManager.Instance.CurrentSession;
            AssertThat(session.SelectedCharacterID).IsEqual("lincoln");
            AssertThat(session.OpponentCharacterID).IsEqual("leonardo");
            AssertThat(session.FighterOpponentType).IsEqual(FighterOpponentType.LocalHuman);
            AssertThat(session.FighterMatchOrigin).IsEqual(FighterMatchOrigin.LocalVersus);
            AssertThat(session.SelectedStageID.Length > 0).IsTrue();

            MatchSettings settings = session.MatchSettings;
            AssertThat(settings.Mode).IsEqual(MatchMode.TimeLimit);
            AssertThat(settings.StockCount).IsEqual(5);
            AssertThat(settings.TimeLimit).IsEqual(120f);
            // Off must clear the boolean as well as the band.
            AssertThat(settings.ItemSpawnRate).IsEqual(ChronalOrbFrequency.Off);
            AssertThat(settings.ItemsEnabled).IsFalse();
            AssertThat(settings.MeterPickupsEnabled).IsTrue();
            AssertThat(settings.StageHazardsEnabled).IsFalse();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void StaticCopyIsStoredAsRawTranslationKeysAndTheCarriedKeysResolve() {
        TranslationServer.SetLocale("en");
        CharacterSelectScreen screen = Open(out Node host);
        try {
            // Raw keys in the authored scene; Godot's control auto-translation
            // resolves them at render time.
            AssertThat(screen.GetNode<Label>(SelectRoot + "Title").Text).IsEqual("fighter_select_title");
            AssertThat(screen.GetNode<Label>(StageRoot + "StageTitle").Text).IsEqual("fighter_stage_select_title");
            AssertThat(screen.GetNode<Button>(StageRoot + "StageButtonRow/FightButton").Text).IsEqual("fighter_start_match");
            AssertThat(screen.GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1Ready").Text).IsEqual("fighter_ready_banner");
            AssertThat(screen.GetNode<Label>(SelectRoot + "FeedbackLabel").Text).IsEqual("fighter_slot_unavailable");
            AssertThat(screen.GetNode<Label>(SelectRoot + "HintLabel").Text).IsEqual("fighter_select_hint");
            AssertThat(screen.GetNode<CheckButton>(StageRoot + "RulesRow/StageHazards").Text).IsEqual("fighter_hazards");
            AssertThat(screen.GetNode<CheckButton>(StageRoot + "RulesRow/MeterPickups").Text).IsEqual("fighter_meter_pickups");

            // Keys carried over from the pre-rework screen must still resolve
            // through the compiled table. (The M-28 keys are swept by
            // FighterLocalizationTests' fighter_* family once en.en.translation
            // is reimported; asserting them here too would only duplicate it.)
            foreach (string key in new[] {
                         "fighter_select_title", "fighter_start_match", "common_back",
                         "fighter_stage", "fighter_items", "fighter_hazards",
                         "fighter_meter_pickups", "fighter_random_character", "fighter_random_stage",
                         "hud_stocks", "hud_timer" }) {
                AssertThat(TranslationServer.Translate(key).ToString())
                    .OverrideFailureMessage($"{key} does not resolve through the compiled translation")
                    .IsNotEqual(key);
            }
        } finally {
            Teardown(host);
        }
    }

    // ---- helpers ------------------------------------------------------------

    /// <summary>Local Versus: P1 confirms tile 0, P2 tile 1, the countdown runs out.</summary>
    private static void DriveLocalFlowToStagePhase(CharacterSelectScreen screen) {
        AssertThat(screen.TryConfirm(0)).IsTrue();
        AssertThat(screen.TryConfirm(1)).IsTrue();
        AssertThat(screen.Phase).IsEqual(SelectScreenPhase.Countdown);
        screen.AdvanceCountdown(3.5f);
    }

    /// <summary>
    /// Steps a token onto a grid index: column first (a horizontal step wraps
    /// within its row), then row. Bounded, so a layout change fails instead of
    /// hanging the runner.
    /// </summary>
    private static void MoveCursorTo(CharacterSelectScreen screen, int player, int target) {
        const int columns = 3;
        for (int step = 0; step < 12 && screen.GetCursor(player) % columns != target % columns; step++) {
            screen.TryMoveCursor(player, 1, 0);
        }
        for (int step = 0; step < 12 && screen.GetCursor(player) != target; step++) {
            screen.TryMoveCursor(player, 0, 1);
        }
        AssertThat(screen.GetCursor(player)).IsEqual(target);
    }

    private static void AssertChainAuthored(IReadOnlyList<Control> chain) {
        foreach (Control control in chain) {
            AssertThat(control.FocusMode)
                .OverrideFailureMessage($"{control.Name} is not focusable")
                .IsEqual(Control.FocusModeEnum.All);
            bool linked = !control.FocusNeighborTop.IsEmpty || !control.FocusNeighborBottom.IsEmpty;
            AssertThat(linked)
                .OverrideFailureMessage($"{control.Name} has no focus neighbour")
                .IsTrue();
        }
    }

    /// <summary>
    /// Opens the screen under a chosen match origin (the screen reads it once in
    /// <c>_Ready</c>). The caller's session is restored by <see cref="Teardown"/>.
    /// </summary>
    private static CharacterSelectScreen Open(
        out Node host, FighterMatchOrigin origin = FighterMatchOrigin.LocalVersus) {
        if (GameManager.Instance != null) {
            _savedSession ??= GameManager.Instance.CurrentSession;
            SessionData session = GameManager.Instance.CurrentSession;
            session.FighterMatchOrigin = origin;
            session.ResumeAtStageSelect = false;
            session.HasPendingMatchSeed = false;
            GameManager.Instance.CurrentSession = session;
        }
        host = new Node { Name = "CharacterSelectHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        var screen = packed.Instantiate<CharacterSelectScreen>();
        host.AddChild(screen);
        return screen;
    }

    private static void Teardown(Node host) {
        if (GameManager.Instance != null && _savedSession.HasValue) {
            GameManager.Instance.CurrentSession = _savedSession.Value;
            _savedSession = null;
        }
        // 9.1: a Local Versus screen begins a local join, which only a scene load
        // out of the Fighter flow ends; a test that never loads one must end it,
        // or the join would outlive the suite.
        InputManager.Instance?.EndLocalJoin();
        if (host == null || !GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
