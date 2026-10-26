using FTT.Core;
using FTT.FighterSim;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>UR-SCREENS. The match results show both fighters on roundels, mark the winner with a star and dim the loser.</summary>
[TestSuite]
[RequireGodotRuntime]
public class MatchResultsPulpTests {

    [TestCase]
    public void TheWinnerCarriesAStarAndTheLoserIsDimmed() {
        GameManager gm = GameManager.Instance;
        SessionData saved = gm != null ? gm.CurrentSession : default;
        var results = ResourceLoader.Load<PackedScene>("res://scenes/ui/MatchResults.tscn").Instantiate<MatchResults>();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(results);
        try {
            if (gm != null) {
                SessionData session = gm.CurrentSession;
                session.SelectedCharacterID = "joan";
                session.OpponentCharacterID = "lincoln";
                gm.CurrentSession = session;
            }
            results.ShowResult(new FighterMatchResult(winnerPlayerID: 1, isTrueTie: false, completedTick: 900, finalHash: 7L));
            AssertThat(results.WinnerStarVisible(1)).IsTrue();
            AssertThat(results.WinnerStarVisible(0)).IsFalse();
            AssertThat(results.RoundelFor(0) != null && results.RoundelFor(1) != null).IsTrue();
            var loser = results.FindChild("Fighter1", recursive: true, owned: false) as Control;
            AssertThat(loser != null && loser.Modulate.A < 1f).IsTrue();
        } finally {
            if (gm != null) gm.CurrentSession = saved;
            results.Free();
        }
    }
}
