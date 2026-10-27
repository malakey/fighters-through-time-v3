using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>The CheckButton toggle skin (TG-WIDGETS: G13; first written by UR-WIDGETS): geometry, the kept behaviour and the hidden default icons.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpToggleDecorTests {

    [TestCase]
    public void TheTrackSitsAtTheRightEndAndTheKnobSlidesBetweenItsInsets() {
        Rect2 track = PulpToggleDecor.TrackRect(new Vector2(520f, 52f));
        AssertThat(track.Size).IsEqual(new Vector2(132f, 44f));
        AssertThat(track.End.X).IsEqual(520f);
        AssertThat(track.Position.Y).IsEqual(4f);
        // 3.3: the knob 34 x 34, 5 px in from the left when off and from the right when on.
        AssertThat(PulpToggleDecor.KnobX(0f)).IsEqual(5f);
        AssertThat(PulpToggleDecor.KnobX(1f)).IsEqual(132f - 5f - 34f);
        AssertThat(PulpToggleDecor.TrackRun).IsEqual(14f);
        AssertThat(PulpToggleDecor.KnobRun).IsEqual(10f);
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
            // T54: "ON" white, "OFF" ink2, in the ToggleText role.
            AssertThat(first.OffLabel.ThemeTypeVariation.ToString()).IsEqual(FTT.UI.UIPalette.LabelRoles.ToggleText);
            AssertThat(first.OffLabel.GetThemeColor("font_color")).IsEqual(FTT.UI.UIPalette.Ink2);
            AssertThat(first.OnLabel.GetThemeColor("font_color")).IsEqual(Colors.White);

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
