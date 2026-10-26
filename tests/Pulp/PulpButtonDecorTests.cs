using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>UR-WIDGETS. The banner-button decor: marker states, the focus-row badge, the selected plate.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpButtonDecorTests {

    private static Button BuildButton(bool toggle = false, string variation = "PulpButton") {
        var button = new Button {
            Text = "Go", ThemeTypeVariation = variation, ToggleMode = toggle,
            FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(640f, 80f), Size = new Vector2(640f, 80f)
        };
        UIPalette.ApplyTheme(button);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(button);
        return button;
    }

    [TestCase]
    public void TheMarkerTintAndShadowFollowTheSpecTable() {
        AssertThat(PulpButtonDecor.ComputeMarkerState(false, false, false, false)).IsEqual(PulpButtonDecor.MarkerState.Normal);
        AssertThat(PulpButtonDecor.ComputeMarkerState(false, false, false, true)).IsEqual(PulpButtonDecor.MarkerState.Focus);
        AssertThat(PulpButtonDecor.ComputeMarkerState(false, true, false, true)).IsEqual(PulpButtonDecor.MarkerState.Selected);
        AssertThat(PulpButtonDecor.ComputeMarkerState(false, false, true, true)).IsEqual(PulpButtonDecor.MarkerState.Pressed);
        AssertThat(PulpButtonDecor.ComputeMarkerState(true, true, true, true)).IsEqual(PulpButtonDecor.MarkerState.Disabled);

        AssertThat(PulpButtonDecor.MarkerTint(PulpButtonDecor.MarkerState.Normal)).IsEqual(UIPalette.Ink3);
        AssertThat(PulpButtonDecor.MarkerTint(PulpButtonDecor.MarkerState.Pressed)).IsEqual(UIPalette.Ink3);
        AssertThat(PulpButtonDecor.MarkerTint(PulpButtonDecor.MarkerState.Focus)).IsEqual(UIPalette.LabelDark);
        AssertThat(PulpButtonDecor.MarkerTint(PulpButtonDecor.MarkerState.Selected)).IsEqual(PulpButtonDecor.MarkerSelectedTint);
        AssertThat(PulpButtonDecor.MarkerTint(PulpButtonDecor.MarkerState.Disabled)).IsEqual(PulpButtonDecor.MarkerDisabledTint);

        AssertThat(PulpButtonDecor.MarkerHasShadow(PulpButtonDecor.MarkerState.Normal)).IsTrue();
        AssertThat(PulpButtonDecor.MarkerHasShadow(PulpButtonDecor.MarkerState.Selected)).IsTrue();
        AssertThat(PulpButtonDecor.MarkerHasShadow(PulpButtonDecor.MarkerState.Pressed)).IsTrue();
        AssertThat(PulpButtonDecor.MarkerHasShadow(PulpButtonDecor.MarkerState.Focus)).IsFalse();
        AssertThat(PulpButtonDecor.MarkerHasShadow(PulpButtonDecor.MarkerState.Disabled)).IsFalse();
    }

    [TestCase]
    public void ApplyAddsTheMarkerAndFocusPoseOnceAndIsHarmlessTwice() {
        PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Keyboard;
        Button button = BuildButton();
        try {
            PulpButtonDecor first = PulpButtonDecor.Apply(button);
            PulpButtonDecor second = PulpButtonDecor.Apply(button);
            AssertThat(second).IsEqual(first);
            int decors = 0, poses = 0;
            foreach (Node child in button.GetChildren()) {
                if (child is PulpButtonDecor) decors++;
                if (child is FTT.UI.Motion.FocusPose) poses++;
            }
            AssertThat(decors).IsEqual(1);
            AssertThat(poses).IsEqual(1);
            AssertThat(first.Marker.IconName).IsEqual("marker");
            AssertThat(first.Marker.Size).IsEqual(new Vector2(20f, 28f));
            AssertThat(first.Marker.Position.X).IsEqual(26f);
            AssertThat(first.Marker.Tint).IsEqual(UIPalette.Ink3);
            AssertThat(first.Marker.ShadowEnabled).IsTrue();
        } finally {
            PulpPromptGlyphs.DeviceOverrideForTesting = null;
            button.Free();
        }
    }

    [TestCase]
    public void CompactButtonsGetTheSmallMarker() {
        Button button = BuildButton(false, "PulpButtonCompact");
        button.CustomMinimumSize = new Vector2(400f, 58f);
        button.Size = new Vector2(400f, 58f);
        try {
            PulpButtonDecor decor = PulpButtonDecor.Apply(button, compact: true);
            decor.Refresh();
            AssertThat(decor.Marker.Size).IsEqual(new Vector2(16f, 22f));
            AssertThat(decor.Marker.Position.X).IsEqual(20f);
        } finally {
            button.Free();
        }
    }

    [TestCase]
    public void TheBadgeShowsOnlyOnTheFocusedRowAndSwitchesBetweenPadAndKeyCap() {
        PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Pad;
        Button focused = BuildButton();
        Button other = BuildButton();
        try {
            PulpButtonDecor onFocused = PulpButtonDecor.Apply(focused);
            PulpButtonDecor onOther = PulpButtonDecor.Apply(other);
            focused.GrabFocus();
            onFocused.Refresh();
            onOther.Refresh();
            AssertThat(onFocused.Badge.Visible).IsTrue();
            AssertThat(onOther.Badge.Visible).IsFalse();
            AssertThat(onFocused.Marker.Tint).IsEqual(UIPalette.LabelDark);
            AssertThat(onFocused.Marker.ShadowEnabled).IsFalse();
            AssertThat(onFocused.BadgeDevice).IsEqual(PromptDevice.Pad);
            AssertThat(onFocused.Badge.Size).IsEqual(new Vector2(36f, 36f));
            AssertThat(onFocused.Badge.Position.X).IsEqual(640f - 30f - 36f);
            bool hasA = false;
            foreach (Node child in onFocused.Badge.GetChildren()) if (child is Label l && l.Text == "A") hasA = true;
            AssertThat(hasA).IsTrue();

            PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Keyboard;
            onFocused.Refresh();
            AssertThat(onFocused.BadgeDevice).IsEqual(PromptDevice.Keyboard);
            AssertThat(onFocused.Badge.GetChild(0) is PanelContainer).IsTrue();
            AssertThat(onFocused.Badge.Visible).IsTrue();
        } finally {
            PulpPromptGlyphs.DeviceOverrideForTesting = null;
            focused.Free();
            other.Free();
        }
    }

    [TestCase]
    public void ADisabledButtonDimsItsMarkerAndShowsNoBadge() {
        Button button = BuildButton();
        try {
            PulpButtonDecor decor = PulpButtonDecor.Apply(button);
            button.Disabled = true;
            decor.Refresh();
            AssertThat(decor.Marker.Tint).IsEqual(PulpButtonDecor.MarkerDisabledTint);
            AssertThat(decor.Marker.ShadowEnabled).IsFalse();
            AssertThat(decor.Badge.Visible).IsFalse();
        } finally {
            button.Free();
        }
    }

    [TestCase]
    public void SelectedSwapsToThePaperPlateAndBackAndFollowsToggleMode() {
        Button button = BuildButton(toggle: true);
        try {
            PulpButtonDecor decor = PulpButtonDecor.Apply(button);
            AssertThat(button.HasThemeStyleboxOverride("normal")).IsFalse();

            AssertThat(PulpButtonDecor.SelectedPlateHeight("PulpButtonCompact")).IsEqual(58);
            AssertThat(PulpButtonDecor.SelectedPlateHeight("TemporalGlassButton")).IsEqual(64);
            button.ButtonPressed = true;
            AssertThat(decor.Selected).IsTrue();
            var plate = button.GetThemeStylebox("normal") as StyleBoxTexture;
            AssertObject(plate).IsNotNull();
            AssertThat(plate.Texture.ResourcePath.EndsWith("btn_selected_h80.png")).IsTrue();
            AssertThat(button.GetThemeStylebox("hover")).IsEqual(plate);
            AssertThat(button.GetThemeColor("font_color")).IsEqual(UIPalette.LabelDark);
            AssertThat(decor.Marker.Tint).IsEqual(PulpButtonDecor.MarkerSelectedTint);
            AssertThat(decor.Marker.ShadowEnabled).IsTrue();

            button.ButtonPressed = false;
            AssertThat(decor.Selected).IsFalse();
            AssertThat(button.HasThemeStyleboxOverride("normal")).IsFalse();
            AssertThat(button.HasThemeStyleboxOverride("hover")).IsFalse();
            AssertThat(button.HasThemeColorOverride("font_color")).IsFalse();
            AssertThat(decor.Marker.Tint).IsEqual(UIPalette.Ink3);
        } finally {
            button.Free();
        }
    }

    [TestCase]
    public void ApplyLeftAlignsTheLabelUnlessAskedNotTo() {
        Button left = BuildButton();
        Button centred = BuildButton();
        try {
            PulpButtonDecor.Apply(left);
            PulpButtonDecor.Apply(centred, compact: false, alignLeft: false);
            AssertThat(left.Alignment).IsEqual(HorizontalAlignment.Left);
            AssertThat(centred.Alignment).IsEqual(HorizontalAlignment.Center);
        } finally {
            left.Free();
            centred.Free();
        }
    }
}
