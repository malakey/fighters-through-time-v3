using System.Collections.Generic;
using System.Linq;
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
            while (screen.GetCursor(0) != screen.RandomTileIndex) screen.TryMoveCursor(0, 1, 0);
            while (screen.GetCursor(1) != screen.RandomTileIndex) screen.TryMoveCursor(1, 1, 0);
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
        if (host == null || !GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
