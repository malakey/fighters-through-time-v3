using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B2. <see cref="TestArenaHUD"/> after its demotion from "the Fighter
/// HUD" to a developer overlay.
///
/// The node is still authored into the eleven Fighter scenes that carried it, so
/// the only thing standing between a player and a screen full of tick counts and
/// state hashes is that it hides itself on load. That is worth a test: a
/// regression here is invisible in review and glaring in a build.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterDebugOverlayTests {

    [TestCase]
    public void TheDebugOverlayIsHiddenOnLoad() {
        TestArenaHUD hud = Mount();
        try {
            AssertThat(hud.DebugVisible).OverrideFailureMessage(
                "The debug overlay must default to off; the production FighterHUD "
                + "owns the screen.").IsFalse();
            AssertThat(hud.Visible).IsFalse();
        } finally {
            hud.Free();
        }
    }

    [TestCase]
    public void TheToggleFlipsBothTheFlagAndTheLayer() {
        TestArenaHUD hud = Mount();
        try {
            AssertThat(hud.ToggleDebugVisible()).IsTrue();
            AssertThat(hud.Visible).IsTrue();

            AssertThat(hud.ToggleDebugVisible()).IsFalse();
            AssertThat(hud.Visible).IsFalse();

            hud.SetDebugVisible(true);
            AssertThat(hud.DebugVisible).IsTrue();
            AssertThat(hud.Visible).IsTrue();
        } finally {
            hud.Free();
        }
    }

    [TestCase]
    public void TheToggleKeyIsNotAnInputMapActionAndCannotBeRebound() {
        // Deliberate: an InputMap action would show up as a rebindable row in the
        // Package 8 A4 Controls tab, advertising a developer affordance as a
        // player feature. F3 is read as a raw key instead.
        AssertThat(TestArenaHUD.ToggleKey).IsEqual(Key.F3);
        foreach (StringName action in InputMap.GetActions()) {
            foreach (InputEvent authored in InputMap.ActionGetEvents(action)) {
                if (authored is not InputEventKey key) continue;
                bool collides = key.PhysicalKeycode == TestArenaHUD.ToggleKey
                    || key.Keycode == TestArenaHUD.ToggleKey;
                AssertThat(collides).OverrideFailureMessage(
                    $"InputMap action '{action}' is bound to the debug-overlay key.").IsFalse();
            }
        }
    }

    [TestCase]
    public void TheOverlayRunsWhilePausedSoAFrozenFrameCanBeInspected() {
        TestArenaHUD hud = Mount();
        try {
            AssertThat(hud.ProcessMode).IsEqual(Node.ProcessModeEnum.Always);
        } finally {
            hud.Free();
        }
    }

    private static TestArenaHUD Mount() {
        var hud = new TestArenaHUD { Name = "DebugHUD" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(hud);
        return hud;
    }
}
