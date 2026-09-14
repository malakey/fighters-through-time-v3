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
/// <para>The suite also pins that the rework preserved every session write, the
/// stage routing through the catalog, all four frequency bands, and the
/// Holodeck hub-practice entry path.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CharacterSelectSceneTests {

    private const string ScenePath = "res://scenes/menus/CharacterSelect.tscn";
    private const string SelectRoot = "SelectPhase/Root/";
    private const string StageRoot = "StagePhase/Root/";

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
                         SelectRoot + "ModeRow/LocalHumanToggle", SelectRoot + "ModeRow/CpuDifficulty",
                         SelectRoot + "FeedbackLabel", SelectRoot + "CountdownLabel",
                         SelectRoot + "HintLabel", SelectRoot + "ButtonRow/BackButton",
                         StageRoot + "StageTitle",
                         StageRoot + "StageRow/StageSelect", StageRoot + "StageRow/StagePreview",
                         StageRoot + "RulesRow/MatchMode", StageRoot + "RulesRow/StockCount",
                         StageRoot + "RulesRow/TimeLimit", StageRoot + "RulesRow/ItemFrequency",
                         StageRoot + "RulesRow/HazardFrequency",
                         StageRoot + "StageButtonRow/StageBackButton",
                         StageRoot + "StageButtonRow/FightButton" }) {
                AssertThat(screen.GetNodeOrNull<Control>(path) != null)
                    .OverrideFailureMessage($"Missing authored control {path}")
                    .IsTrue();
            }
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
            // ROSTER member, not per the number nine. The screen itself binds
            // defensively (a roster larger than the grid leaves the tail
            // unbound rather than throwing), so this assertion is the signal
            // that says "a character was added — author its tile".
            int rosterCount = FTT.Core.CharacterRoster.Count;
            AssertThat(grid.GetChildCount()).IsEqual(rosterCount);
            for (int index = 0; index < rosterCount; index++) {
                var tile = grid.GetNodeOrNull<Button>($"CharacterButton{index}");
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
                AssertObject(portrait.Texture)
                    .OverrideFailureMessage($"Tile {index} portrait texture is missing")
                    .IsNotNull();
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
            screen.GetNode<CheckButton>(SelectRoot + "ModeRow/LocalHumanToggle").ButtonPressed = false;

            // V7.3 Fighter Onboarding added the Move List and Systems Card
            // footer buttons to the selection chain (3 -> 5); Package 11 A6 added
            // the M01 Steam Remote Play Together notice (5 -> 6).
            IReadOnlyList<Control> selectionChain = screen.FocusChain;
            AssertThat(selectionChain.Count).IsEqual(6);
            AssertThat(selectionChain.Contains(
                screen.GetNode<Button>(SelectRoot + "ButtonRow/MoveListButton"))).IsTrue();
            AssertThat(selectionChain.Contains(
                screen.GetNode<Button>(SelectRoot + "ButtonRow/SystemsCardButton"))).IsTrue();
            AssertChainAuthored(selectionChain);
            AssertThat(screen.GetNode<CheckButton>(SelectRoot + "ModeRow/LocalHumanToggle").HasFocus()).IsTrue();

            DriveCpuFlowToStagePhase(screen);
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.StageSelect);

            IReadOnlyList<Control> stageChain = screen.FocusChain;
            AssertThat(stageChain.Count).IsEqual(8);
            AssertChainAuthored(stageChain);

            // A SpinBox keeps its editable LineEdit as an INTERNAL child; without
            // the explicit splice the stock count and time limit are unreachable
            // by keyboard and controller.
            AssertThat(stageChain.Contains(screen.GetNode<SpinBox>(StageRoot + "RulesRow/StockCount").GetLineEdit()))
                .OverrideFailureMessage("The stock-count editor is not reachable by focus.").IsTrue();
            AssertThat(stageChain.Contains(screen.GetNode<SpinBox>(StageRoot + "RulesRow/TimeLimit").GetLineEdit()))
                .OverrideFailureMessage("The time-limit editor is not reachable by focus.").IsTrue();
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
            screen.GetNode<CheckButton>(SelectRoot + "ModeRow/LocalHumanToggle").ButtonPressed = false;
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
    /// of picks.
    /// </summary>
    [TestCase]
    public void BothPlayersMayLockTheSameCharacter() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            screen.GetNode<CheckButton>(SelectRoot + "ModeRow/LocalHumanToggle").ButtonPressed = true;

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
            screen.GetNode<CheckButton>(SelectRoot + "ModeRow/LocalHumanToggle").ButtonPressed = true;

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

    [TestCase]
    public void CpuModeLetsPlayerOneLockThenPickTheCpuOpponent() {
        if (GameManager.Instance == null) return;
        SessionData original = GameManager.Instance.CurrentSession;
        CharacterSelectScreen screen = Open(out Node host);
        try {
            screen.GetNode<CheckButton>(SelectRoot + "ModeRow/LocalHumanToggle").ButtonPressed = false;

            // P1 confirms their own pick, then drives the CPU token.
            AssertThat(screen.TryConfirm(0)).IsTrue();
            AssertThat(screen.IsPickingCpu).IsTrue();

            // V7.3 ruling #17: the CPU pick MAY mirror P1's lock. Hover it to
            // prove the confirm is no longer refused, then move on to the
            // distinct pick this flow actually wants.
            AssertThat(screen.TryMoveCursor(0, -1, 0)).IsTrue();
            AssertThat(screen.GetCursor(1)).IsEqual(0);

            // Pick leonardo (index 2): countdown starts.
            AssertThat(screen.TryMoveCursor(0, 2, 0)).IsTrue();
            AssertThat(screen.GetCursor(1)).IsEqual(2);
            AssertThat(screen.TryConfirm(0)).IsTrue();
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.Countdown);

            // P1's cancel during the countdown unwinds the CPU pick first.
            AssertThat(screen.TryCancel(0)).IsTrue();
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.Selection);
            AssertThat(screen.IsPickingCpu).IsTrue();
            AssertThat(screen.IsSlotReady(0)).IsTrue();

            AssertThat(screen.TryConfirm(0)).IsTrue();
            screen.AdvanceCountdown(3.5f);
            AssertThat(screen.Phase).IsEqual(SelectScreenPhase.StageSelect);

            // The session writes are the pre-rework contract.
            FTT.Environment.FighterStageData stage = screen.ApplySelectionToSession();
            AssertThat(stage != null).IsTrue();
            SessionData session = GameManager.Instance.CurrentSession;
            AssertThat(session.SelectedCharacterID).IsEqual("einstein");
            AssertThat(session.OpponentCharacterID).IsEqual("leonardo");
            AssertThat(session.FighterOpponentType).IsEqual(FighterOpponentType.Cpu);
            AssertThat(session.CpuDifficulty).IsEqual(CpuDifficulty.Normal);
        } finally {
            Teardown(host);
            GameManager.Instance.CurrentSession = original;
        }
    }

    [TestCase]
    public void PlayerTwoTokenIsDrivenByPlayerTwoDeviceOnly() {
        if (InputManager.Instance == null) return;
        CharacterSelectScreen screen = Open(out Node host);
        var neutral = new BufferedInputSource();
        var playerTwo = new BufferedInputSource();
        try {
            screen.GetNode<CheckButton>(SelectRoot + "ModeRow/LocalHumanToggle").ButtonPressed = true;
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
    public void TheHolodeckEntryStaysACpuPracticeSessionAndBacksOutToTheHub() {
        if (GameManager.Instance == null) return;
        SessionData original = GameManager.Instance.CurrentSession;
        Node host = null;
        try {
            // HubWorldController.OpenHolodeckLobby configures exactly this.
            SessionData session = GameManager.Instance.CurrentSession;
            session.FighterOpponentType = FighterOpponentType.Cpu;
            session.ReturnToHubAfterFighterMatch = true;
            GameManager.Instance.CurrentSession = session;

            CharacterSelectScreen screen = Open(out host);
            AssertThat(screen.GetNode<CheckButton>(SelectRoot + "ModeRow/LocalHumanToggle").ButtonPressed)
                .IsFalse();
            AssertThat(screen.ResolveBackScenePath()).IsEqual("res://scenes/campaign/HubWorld.tscn");

            DriveCpuFlowToStagePhase(screen);
            FTT.Environment.FighterStageData stage = screen.ApplySelectionToSession();
            AssertThat(stage != null).IsTrue();
            AssertThat(GameManager.Instance.CurrentSession.FighterOpponentType)
                .IsEqual(FighterOpponentType.Cpu);
            AssertThat(GameManager.Instance.CurrentSession.ReturnToHubAfterFighterMatch).IsTrue();
        } finally {
            Teardown(host);
            GameManager.Instance.CurrentSession = original;
        }
    }

    [TestCase]
    public void AllFourFrequencyBandsSurviveTheReworkAndDefaultToMedium() {
        // V7 "Match Settings Persist": the rule controls now initialize from the
        // session's settings, so normalize the session first — this test pins
        // the first-run defaults, not another test's leftover house rules.
        // V7.3 ruling #18: the first-run default band is Medium on both axes.
        if (GameManager.Instance != null) {
            SessionData normalized = GameManager.Instance.CurrentSession;
            normalized.MatchSettings = MatchSettings.GetDefault();
            GameManager.Instance.CurrentSession = normalized;
        }
        CharacterSelectScreen screen = Open(out Node host);
        try {
            foreach (string path in new[] { StageRoot + "RulesRow/ItemFrequency", StageRoot + "RulesRow/HazardFrequency" }) {
                var select = screen.GetNode<OptionButton>(path);
                AssertThat(select.ItemCount)
                    .OverrideFailureMessage($"{path} does not offer four bands")
                    .IsEqual(4);
                AssertThat(select.GetSelectedId())
                    .OverrideFailureMessage($"{path} default band")
                    .IsEqual((int)ChronalOrbFrequency.Medium);
            }
            // F21 (Package 11 A1c): two selectable modes — Stock and Time. The
            // retired Stock + Time combination is gone from the selector.
            AssertThat(screen.GetNode<OptionButton>(StageRoot + "RulesRow/MatchMode").ItemCount).IsEqual(2);
            AssertThat(screen.GetNode<OptionButton>(SelectRoot + "ModeRow/CpuDifficulty").ItemCount).IsEqual(3);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void StageSelectIsPopulatedFromTheCatalogAndRoutesToARealScene() {
        CharacterSelectScreen screen = Open(out Node host);
        try {
            var stages = screen.GetNode<OptionButton>(StageRoot + "StageRow/StageSelect");
            AssertThat(stages.ItemCount > 0).IsTrue();
            AssertThat(stages.Disabled).IsFalse();

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

    [TestCase]
    public void TheSelectionWritesTheWholeSessionAndMatchSettingsRoundTrip() {
        if (GameManager.Instance == null) return;
        SessionData original = GameManager.Instance.CurrentSession;
        CharacterSelectScreen screen = Open(out Node host);
        try {
            screen.GetNode<CheckButton>(SelectRoot + "ModeRow/LocalHumanToggle").ButtonPressed = true;
            screen.GetNode<OptionButton>(SelectRoot + "ModeRow/CpuDifficulty").Select(2);

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
            screen.GetNode<OptionButton>(StageRoot + "RulesRow/HazardFrequency").Select(1);

            // ApplySelectionToSession is the seam OnFight uses; pressing Fight for
            // real would change the scene out from under the test runner.
            FTT.Environment.FighterStageData stage = screen.ApplySelectionToSession();
            AssertThat(stage != null).IsTrue();
            AssertThat(ResourceLoader.Exists(stage.ScenePath)).IsTrue();

            SessionData session = GameManager.Instance.CurrentSession;
            AssertThat(session.SelectedCharacterID).IsEqual("lincoln");
            AssertThat(session.OpponentCharacterID).IsEqual("leonardo");
            AssertThat(session.FighterOpponentType).IsEqual(FighterOpponentType.LocalHuman);
            AssertThat(session.CpuDifficulty).IsEqual(CpuDifficulty.Hard);
            AssertThat(session.SelectedStageID.Length > 0).IsTrue();

            MatchSettings settings = session.MatchSettings;
            AssertThat(settings.Mode).IsEqual(MatchMode.TimeLimit);
            AssertThat(settings.StockCount).IsEqual(5);
            AssertThat(settings.TimeLimit).IsEqual(120f);
            // Off must clear the boolean as well as the band; Low must set both.
            AssertThat(settings.ItemSpawnRate).IsEqual(ChronalOrbFrequency.Off);
            AssertThat(settings.ItemsEnabled).IsFalse();
            AssertThat(settings.HazardRate).IsEqual(HazardTriggerFrequency.Low);
            AssertThat(settings.StageHazardsEnabled).IsTrue();
        } finally {
            Teardown(host);
            GameManager.Instance.CurrentSession = original;
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

            // Keys carried over from the pre-rework screen must still resolve
            // through the compiled table. (The M-28 keys are swept by
            // FighterLocalizationTests' fighter_* family once en.en.translation
            // is reimported; asserting them here too would only duplicate it.)
            foreach (string key in new[] {
                         "fighter_select_title", "fighter_start_match", "common_back",
                         "fighter_stage", "fighter_items", "fighter_hazards",
                         "fighter_local_human", "fighter_cpu_difficulty",
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

    /// <summary>CPU mode: P1 confirms einstein, picks joan for the CPU, countdown runs out.</summary>
    private static void DriveCpuFlowToStagePhase(CharacterSelectScreen screen) {
        screen.GetNode<CheckButton>(SelectRoot + "ModeRow/LocalHumanToggle").ButtonPressed = false;
        AssertThat(screen.TryConfirm(0)).IsTrue();   // P1 locks tile 0.
        AssertThat(screen.TryConfirm(0)).IsTrue();   // CPU pick locks tile 1.
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

    private static CharacterSelectScreen Open(out Node host) {
        host = new Node { Name = "CharacterSelectHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var packed = ResourceLoader.Load<PackedScene>(ScenePath);
        var screen = packed.Instantiate<CharacterSelectScreen>();
        host.AddChild(screen);
        return screen;
    }

    private static void Teardown(Node host) {
        if (host == null || !GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
