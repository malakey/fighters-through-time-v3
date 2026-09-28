using System.Linq;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W5 — H05/G01 (adopted D4(a)) and M26. The Fighter menu's Local
/// Versus / Versus CPU entries, and the origin-aware exits: every pause and
/// results destination is resolved by <see cref="FighterFlowRoutes"/> from
/// <see cref="SessionData.FighterMatchOrigin"/>, which is never persisted.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterFlowRoutesTests {

    [TestCase]
    public void LocalVersusKeepsItsExistingDestinations() {
        SessionData session = FighterFlowRoutes.EnterLocalVersus(default);
        AssertThat(session.FighterMatchOrigin).IsEqual(FighterMatchOrigin.LocalVersus);
        AssertThat(session.FighterOpponentType).IsEqual(FighterOpponentType.LocalHuman);
        AssertThat(FighterFlowRoutes.ResultsActions(FighterMatchOrigin.LocalVersus).ToArray()).IsEqual(new[] {
            FighterResultsAction.Rematch, FighterResultsAction.NewStage,
            FighterResultsAction.ChangeFighters, FighterResultsAction.Exit });

        AssertThat(FighterFlowRoutes.PauseExit(ref session)).IsEqual(FighterFlowRoutes.CharacterSelectScenePath);
        AssertThat(FighterFlowRoutes.ResultsDestination(FighterResultsAction.Exit, ref session))
            .IsEqual(FighterFlowRoutes.MainMenuScenePath);
        AssertThat(FighterFlowRoutes.ResultsLabelKey(FighterResultsAction.Exit, FighterMatchOrigin.LocalVersus))
            .IsEqual("fighter_main_menu");
        AssertThat(FighterFlowRoutes.ResultsDestination(FighterResultsAction.NewStage, ref session))
            .IsEqual(FighterFlowRoutes.CharacterSelectScenePath);
        AssertThat(session.ResumeAtStageSelect).IsTrue();
    }

    [TestCase]
    public void VersusCpuRoutesBackIntoItsOwnFlowAndOutToTheFighterMenu() {
        SessionData session = FighterFlowRoutes.EnterVersusCpu(default);
        AssertThat(session.FighterMatchOrigin).IsEqual(FighterMatchOrigin.VersusCpu);
        AssertThat(session.FighterOpponentType).IsEqual(FighterOpponentType.Cpu);
        AssertThat(session.ReturnToHubAfterFighterMatch).IsFalse();

        AssertThat(FighterFlowRoutes.PauseExit(ref session)).IsEqual(FighterFlowRoutes.FighterMenuScenePath);
        AssertThat(FighterFlowRoutes.ResultsDestination(FighterResultsAction.Exit, ref session))
            .IsEqual(FighterFlowRoutes.FighterMenuScenePath);
        AssertThat(FighterFlowRoutes.ResultsLabelKey(FighterResultsAction.Exit, FighterMatchOrigin.VersusCpu))
            .IsEqual("fighter_return_to_fighter_menu");
        // New Stage → the CPU configuration panel (the select's stage phase for this origin).
        AssertThat(FighterFlowRoutes.ResultsDestination(FighterResultsAction.NewStage, ref session))
            .IsEqual(FighterFlowRoutes.CharacterSelectScenePath);
        AssertThat(session.ResumeAtStageSelect).IsTrue();
        // Change Fighters → the P1-only select, fresh.
        AssertThat(FighterFlowRoutes.ResultsDestination(FighterResultsAction.ChangeFighters, ref session))
            .IsEqual(FighterFlowRoutes.CharacterSelectScenePath);
        AssertThat(session.ResumeAtStageSelect).IsFalse();
        AssertThat(session.FighterMatchOrigin).IsEqual(FighterMatchOrigin.VersusCpu);
    }

    [TestCase]
    public void HolodeckReturnsToTheConsoleAndReconfigureReopensIt() {
        var session = new SessionData { FighterMatchOrigin = FighterMatchOrigin.Holodeck };
        AssertThat(session.ReturnToHubAfterFighterMatch).IsTrue();
        AssertThat(FighterFlowRoutes.ResultsActions(FighterMatchOrigin.Holodeck).ToArray()).IsEqual(new[] {
            FighterResultsAction.Rematch, FighterResultsAction.Reconfigure, FighterResultsAction.Exit });

        SessionData paused = session;
        AssertThat(FighterFlowRoutes.PauseExit(ref paused)).IsEqual(FighterFlowRoutes.HubScenePath);
        AssertThat(paused.ArriveAtHolodeckConsole).IsTrue();
        AssertThat(paused.ReopenHolodeckConsole).IsFalse();

        SessionData ship = session;
        AssertThat(FighterFlowRoutes.ResultsDestination(FighterResultsAction.Exit, ref ship))
            .IsEqual(FighterFlowRoutes.HubScenePath);
        AssertThat(ship.ArriveAtHolodeckConsole).IsTrue();
        AssertThat(FighterFlowRoutes.ResultsLabelKey(FighterResultsAction.Exit, FighterMatchOrigin.Holodeck))
            .IsEqual("fighter_return_to_ship");

        SessionData reconfigure = session;
        AssertThat(FighterFlowRoutes.ResultsDestination(FighterResultsAction.Reconfigure, ref reconfigure))
            .IsEqual(FighterFlowRoutes.HubScenePath);
        AssertThat(reconfigure.ArriveAtHolodeckConsole).IsTrue();
        AssertThat(reconfigure.ReopenHolodeckConsole).IsTrue();

        // The legacy flag is a view over the origin, not a second source of truth.
        session.ReturnToHubAfterFighterMatch = false;
        AssertThat(session.FighterMatchOrigin).IsEqual(FighterMatchOrigin.LocalVersus);
    }

    [TestCase]
    public void TheResultsScreenBuildsTheButtonsItsOriginCallsFor() {
        GameManager manager = GameManager.Instance;
        if (manager == null) return;
        SessionData saved = manager.CurrentSession;
        try {
            foreach ((FighterMatchOrigin origin, string[] expected) in new[] {
                         (FighterMatchOrigin.Holodeck, new[] { "RematchButton", "ReconfigureButton", "ExitButton" }),
                         (FighterMatchOrigin.VersusCpu,
                             new[] { "RematchButton", "NewStageButton", "ChangeFightersButton", "ExitButton" }) }) {
                SessionData session = saved;
                session.FighterMatchOrigin = origin;
                manager.CurrentSession = session;
                var results = new MatchResults { Name = "ResultsUnderTest" };
                ((SceneTree)Engine.GetMainLoop()).Root.AddChild(results);
                try {
                    AssertThat(results.ActionButtonNames.ToArray()).IsEqual(expected);
                    AssertThat(MatchResults.ExitScenePath()).IsEqual(origin == FighterMatchOrigin.Holodeck
                        ? FighterFlowRoutes.HubScenePath
                        : FighterFlowRoutes.FighterMenuScenePath);
                } finally {
                    results.GetParent()?.RemoveChild(results);
                    results.Free();
                }
            }
        } finally {
            manager.CurrentSession = saved;
        }
    }

    [TestCase]
    public void TheFighterMenuOffersLocalVersusAndVersusCpuWithTheRemotePlayNotice() {
        GameManager manager = GameManager.Instance;
        if (manager == null) return;
        SessionData saved = manager.CurrentSession;
        var packed = ResourceLoader.Load<PackedScene>(FighterFlowRoutes.FighterMenuScenePath);
        AssertObject(packed).IsNotNull();
        Node root = packed.Instantiate();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);
        try {
            AssertThat(root is FighterPlayOptionsScreen).IsTrue();
            const string layout = "Center/Panel/Layout/";
            foreach ((string path, string key) in new[] {
                         ("LocalVersusButton", "fighter_play_local_versus"),
                         ("VersusCpuButton", "fighter_play_versus_cpu"),
                         ("RemotePlayNotice", "fighter_remote_play_notice"),
                         ("BackButton", "common_back") }) {
                var control = root.GetNodeOrNull<Control>(layout + path);
                AssertObject(control).OverrideFailureMessage($"missing {path}").IsNotNull();
                string text = control is Button button ? button.Text : ((Label)control).Text;
                AssertThat(text).IsEqual(key);
            }

            AssertThat(FighterPlayOptionsScreen.ApplyVersusCpu()).IsEqual(FighterFlowRoutes.CharacterSelectScenePath);
            AssertThat(manager.CurrentSession.FighterMatchOrigin).IsEqual(FighterMatchOrigin.VersusCpu);
            AssertThat(FighterPlayOptionsScreen.ApplyLocalVersus()).IsEqual(FighterFlowRoutes.CharacterSelectScenePath);
            AssertThat(manager.CurrentSession.FighterMatchOrigin).IsEqual(FighterMatchOrigin.LocalVersus);
        } finally {
            root.GetParent()?.RemoveChild(root);
            root.Free();
            manager.CurrentSession = saved;
        }
    }

    [TestCase]
    public void CpuDifficultyReadsEasyMediumHardWithTheOrdinalsUnchanged() {
        AssertThat((int)CpuDifficulty.Easy).IsEqual(0);
        AssertThat((int)CpuDifficulty.Normal).IsEqual(1);
        AssertThat((int)CpuDifficulty.Hard).IsEqual(2);
        AssertThat(CpuDifficultyLabels.Key(CpuDifficulty.Normal)).IsEqual("cpu_difficulty_medium");
        var select = new OptionButton();
        try {
            CpuDifficultyLabels.Fill(select, CpuDifficulty.Normal);
            AssertThat(select.ItemCount).IsEqual(3);
            AssertThat(select.GetItemId(1)).IsEqual((int)CpuDifficulty.Normal);
            AssertThat(select.GetSelectedId()).IsEqual((int)CpuDifficulty.Normal);
        } finally {
            select.Free();
        }
    }

    /// <summary>
    /// M24 / One Orb Taxonomy: Story campaign pickups are not orbs. The only
    /// player-facing copy allowed to say "orb" is the Fighter / Holodeck family.
    /// </summary>
    [TestCase]
    public void NoStorySurfaceCallsAPickupAnOrb() {
        string path = ProjectSettings.GlobalizePath("res://localization/en.csv");
        var offenders = new System.Collections.Generic.List<string>();
        foreach (string line in System.IO.File.ReadAllLines(path)) {
            int comma = line.IndexOf(',');
            if (comma <= 0 || line.StartsWith("#")) continue;
            string key = line[..comma];
            string value = line[(comma + 1)..];
            if (key.StartsWith("fighter_") || key.StartsWith("holodeck_") || key.StartsWith("match_")) continue;
            if (System.Text.RegularExpressions.Regex.IsMatch(value, @"\borbs?\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase)) {
                offenders.Add(key);
            }
        }
        if (offenders.Count > 0) AssertThat(string.Join(" | ", offenders)).IsEqual("");
    }

    [TestCase]
    public void RandomPicksAreSeededInRangeAndIndependentPerSalt() {
        for (int seed = 0; seed < 200; seed++) {
            int pick = FighterRandomPick.Index(seed, FighterRandomPick.StageSalt, 10);
            AssertThat(pick is >= 0 and < 10).IsTrue();
            AssertThat(FighterRandomPick.Index(seed, FighterRandomPick.StageSalt, 10)).IsEqual(pick);
        }
        AssertThat(FighterRandomPick.Index(5, FighterRandomPick.StageSalt, 1)).IsEqual(0);
        bool differs = false;
        for (int seed = 0; seed < 50 && !differs; seed++) {
            differs = FighterRandomPick.Index(seed, FighterRandomPick.PlayerOneCharacterSalt, 9)
                != FighterRandomPick.Index(seed, FighterRandomPick.PlayerTwoCharacterSalt, 9);
        }
        AssertThat(differs).OverrideFailureMessage("the two Random tiles must not always mirror").IsTrue();
    }
}
