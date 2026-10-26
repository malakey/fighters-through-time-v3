using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>UR-SCREENS. The Fighter VS loading screen fills two portrait roundels from the session.</summary>
[TestSuite]
[RequireGodotRuntime]
public class LoadingRoundelTests {

    [TestCase]
    public void TheVersusScreenFillsBothPortraitRoundels() {
        LoadingScreen screen = LoadingScreen.Create(LoadingVariant.FighterVersus);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(screen);
        try {
            var session = new SessionData { SelectedCharacterID = "einstein", OpponentCharacterID = "joan" };
            screen.Configure("res://scenes/arenas/TestArena.tscn", session);
            var one = screen.FindChild("PlayerPortrait", recursive: true, owned: false) as PulpPortraitRoundel;
            var two = screen.FindChild("OpponentPortrait", recursive: true, owned: false) as PulpPortraitRoundel;
            AssertThat(one != null && two != null).OverrideFailureMessage("Both VS portraits must be roundels.").IsTrue();
            AssertThat(one.Portrait != null && two.Portrait != null).IsTrue();
            AssertThat(one.Visible && two.Visible).IsTrue();
            // No opponent: that roundel hides rather than showing an empty disc.
            screen.Configure("res://scenes/arenas/TestArena.tscn", new SessionData { SelectedCharacterID = "einstein" });
            AssertThat(two.Visible).IsFalse();
        } finally {
            screen.Free();
        }
    }
}
