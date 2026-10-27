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

    /// <summary>
    /// TG-CLOSE (TG-PAUSE kit gap 5, TG-SELECT kit gap 4): the toggle's track, knob, insets and glows follow the UI scale as the
    /// chevrons' heights and paddings do (it drew a fixed 132 x 44 at every scale), and the toggle draws its own text as the
    /// T52 settings-row label, upper case, in a RowLabel child while the button's own glyphs draw nothing (the Settings
    /// rows had this through SettingsMenu.SkinToggle; the console's and the stage rules' toggles showed their mixed-case
    /// text). In a Settings row the track still starts on the control column at UI scale 1.4.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task TheToggleFollowsTheUiScaleAndDrawsItsTextAsTheSettingsRowLabel() {
        float saved = FTT.UI.UIPalette.AppliedUiScale;
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = new Control { Name = "ToggleScaleHost", Size = new Vector2(1600f, 400f) };
        FTT.UI.UIPalette.ApplyTheme(host);
        tree.Root.AddChild(host);
        try {
            FTT.UI.UIPalette.ApplyUiScale(1.4f);
            var loose = new CheckButton { Text = "fighter_meter_pickups", CustomMinimumSize = new Vector2(700f, 0f) };
            host.AddChild(loose);
            PulpToggleDecor decor = PulpToggleDecor.Apply(loose);
            var layout = new FTT.UI.SettingsRowLayout { Name = "Rows", Position = new Vector2(0f, 120f), Size = new Vector2(1500f, 200f) };
            host.AddChild(layout);
            var row = new CheckButton { Name = "Row", Text = "settings_reduced_temporal_effects" };
            layout.AddChild(row);
            PulpToggleDecor rowDecor = PulpToggleDecor.Apply(row);
            for (int i = 0; i < 4; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var problems = new System.Collections.Generic.List<string>();

            Rect2 track = PulpToggleDecor.TrackRect(loose.Size);
            if (!track.Size.IsEqualApprox(new Vector2(132f, 44f) * 1.4f)) problems.Add($"the track is {track.Size} at 1.4, not 184.8 x 61.6");
            if (!Mathf.IsEqualApprox(PulpToggleDecor.KnobX(1f), (132f - 5f - 34f) * 1.4f)) problems.Add($"the knob's far stop is {PulpToggleDecor.KnobX(1f)}");
            if (loose.GetCombinedMinimumSize().Y < 44f * 1.4f) problems.Add($"the toggle is {loose.GetCombinedMinimumSize().Y} px tall at 1.4");
            Label label = loose.GetNodeOrNull<Label>(PulpToggleDecor.RowLabelName);
            if (label == null) {
                problems.Add("the toggle has no RowLabel");
            } else {
                if (label.ThemeTypeVariation.ToString() != FTT.UI.UIPalette.LabelRoles.SettingsRowLabel || !label.Uppercase) problems.Add("the RowLabel is not the upper-case T52 role");
                if (label.Text != loose.Text) problems.Add($"the RowLabel reads {label.Text}");
                if (label.Position.X + label.Size.X > track.Position.X - 1f) problems.Add("the RowLabel runs under the track");
            }
            foreach (string slot in new[] { "font_color", "font_focus_color", "font_hover_color", "font_pressed_color", "font_disabled_color" }) {
                if (loose.GetThemeColor(slot).A > 0f) problems.Add($"the button's own {slot} still draws");
            }
            // In a Settings row the track starts on the control column: (label 360 + gap 22) x 1.4.
            Rect2 rowTrack = PulpToggleDecor.TrackRect(row.Size);
            if (Mathf.Abs(rowTrack.Position.X - (360f + 22f) * 1.4f) > 1f) problems.Add($"the row's track starts at {rowTrack.Position.X}, not the control column {(360f + 22f) * 1.4f}");
            if (row.GetNodeOrNull<Label>(PulpToggleDecor.RowLabelName) == null) problems.Add("the row's toggle has no RowLabel");
            if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
            AssertObject(decor).IsNotNull();
            AssertObject(rowDecor).IsNotNull();
        } finally {
            FTT.UI.UIPalette.ApplyUiScale(saved);
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }
}
