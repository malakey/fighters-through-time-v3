using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>UR-WIDGETS step 2. The CheckButton toggle skin: geometry, the kept behaviour and the hidden default icons.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpToggleDecorTests {

    [TestCase]
    public void TheTrackSitsAtTheRightEndAndTheKnobSlidesBetweenItsInsets() {
        Rect2 track = PulpToggleDecor.TrackRect(new Vector2(520f, 52f));
        AssertThat(track.Size).IsEqual(new Vector2(132f, 44f));
        AssertThat(track.End.X).IsEqual(520f);
        AssertThat(track.Position.Y).IsEqual(4f);
        AssertThat(PulpToggleDecor.KnobX(0f)).IsEqual(5f);
        AssertThat(PulpToggleDecor.KnobX(1f) > PulpToggleDecor.KnobX(0f)).IsTrue();
        AssertThat(PulpToggleDecor.KnobX(1f) + PulpToggleDecor.KnobSize <= 132f - 5f).IsTrue();
    }

    [TestCase]
    public void ApplyIsIdempotentHidesTheSwitchIconsAndKeepsTheCheckButtonBehaviour() {
        var button = new CheckButton { Text = "setting", CustomMinimumSize = new Vector2(500f, 52f) };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(button);
        try {
            PulpToggleDecor first = PulpToggleDecor.Apply(button);
            AssertThat(PulpToggleDecor.Apply(button)).IsEqual(first);
            AssertThat(button.HasThemeIconOverride("checked")).IsTrue();
            AssertThat(button.HasThemeIconOverride("unchecked_disabled")).IsTrue();
            AssertThat(first.Layer.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
            AssertThat(first.KnobPosition).IsEqual(0f);
            AssertThat(first.OffLabel.Text).IsEqual("ui_toggle_off");
            AssertThat(first.OffLabel.Visible).IsTrue();

            bool toggled = false;
            button.Toggled += pressed => toggled = pressed;
            button.ButtonPressed = true;
            AssertThat(toggled).IsTrue();
            AssertThat(button.ButtonPressed).IsTrue();
            AssertThat(button.FocusMode).IsEqual(Control.FocusModeEnum.All);
        } finally {
            button.Free();
        }
    }
}
