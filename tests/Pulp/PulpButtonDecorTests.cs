using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>
/// The chevron button decor (TG-WIDGETS, BTN-A; first written by UR-WIDGETS): the diamond marker per state, the
/// focus-row badge, hover as focus, the glass selected look, the per-state label colours, shrink then ellipsis, and the
/// D6 stack width (bound to the tree, the UI scale and the locale).
/// </summary>
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

    private static float CentreX(Control marker) => marker.Position.X + marker.Size.X / 2f;

    private static bool SameColor(Color a, Color b) =>
        Mathf.Abs(a.R - b.R) < 1e-4f && Mathf.Abs(a.G - b.G) < 1e-4f && Mathf.Abs(a.B - b.B) < 1e-4f && Mathf.Abs(a.A - b.A) < 1e-4f;

    [TestCase]
    public void TheDiamondMarkerFollowsTheSpecTablePerState() {
        // 3.2: disabled and selected win; a focused row keeps the focus diamond while pressed; a held unfocused row is Pressed.
        AssertThat(PulpButtonDecor.ComputeMarkerState(false, false, false, false)).IsEqual(PulpButtonDecor.MarkerState.Normal);
        AssertThat(PulpButtonDecor.ComputeMarkerState(false, false, false, true)).IsEqual(PulpButtonDecor.MarkerState.Focus);
        AssertThat(PulpButtonDecor.ComputeMarkerState(false, false, true, true)).IsEqual(PulpButtonDecor.MarkerState.Focus);
        AssertThat(PulpButtonDecor.ComputeMarkerState(false, false, true, false)).IsEqual(PulpButtonDecor.MarkerState.Pressed);
        AssertThat(PulpButtonDecor.ComputeMarkerState(false, true, false, true)).IsEqual(PulpButtonDecor.MarkerState.Selected);
        AssertThat(PulpButtonDecor.ComputeMarkerState(true, true, true, true)).IsEqual(PulpButtonDecor.MarkerState.Disabled);

        // Normal: a 1.5 px edge outline, no fill. Focus: filled focus #FFD45C, white outline, ring and glow.
        // Selected: filled and outlined accent. Disabled: the edge dashed at half opacity.
        AssertThat(PulpButtonDecor.MarkerTint(PulpButtonDecor.MarkerState.Normal)).IsEqual(UIPalette.Edge);
        AssertThat(PulpButtonDecor.MarkerTint(PulpButtonDecor.MarkerState.Pressed)).IsEqual(UIPalette.Edge);
        AssertThat(PulpButtonDecor.MarkerTint(PulpButtonDecor.MarkerState.Focus)).IsEqual(UIPalette.Focus);
        AssertThat(PulpButtonDecor.MarkerTint(PulpButtonDecor.MarkerState.Selected)).IsEqual(UIPalette.Accent);
        AssertThat(PulpButtonDecor.MarkerTint(PulpButtonDecor.MarkerState.Disabled).A).IsEqual(UIPalette.Edge.A * 0.5f);
        AssertThat(PulpButtonDecor.MarkerOutline(PulpButtonDecor.MarkerState.Focus)).IsEqual(Colors.White);
        AssertThat(PulpButtonDecor.MarkerOutline(PulpButtonDecor.MarkerState.Selected)).IsEqual(UIPalette.Accent);
        foreach (PulpButtonDecor.MarkerState state in System.Enum.GetValues<PulpButtonDecor.MarkerState>()) {
            bool focus = state == PulpButtonDecor.MarkerState.Focus;
            AssertThat(PulpButtonDecor.MarkerHasShadow(state)).IsEqual(focus);
            AssertThat(PulpButtonDecor.MarkerFilled(state)).IsEqual(focus || state == PulpButtonDecor.MarkerState.Selected);
            AssertThat(PulpButtonDecor.MarkerDashed(state)).IsEqual(state == PulpButtonDecor.MarkerState.Disabled);
        }

        // The live marker follows focus.
        Button button = BuildButton();
        try {
            PulpButtonDecor decor = PulpButtonDecor.Apply(button);
            var marker = (GlassMarker)decor.Marker;
            AssertThat(marker.State).IsEqual(PulpButtonDecor.MarkerState.Normal);
            button.GrabFocus();
            decor.Refresh();
            AssertThat(marker.State).IsEqual(PulpButtonDecor.MarkerState.Focus);
            AssertThat(marker.Tint).IsEqual(UIPalette.Focus);
            AssertThat(marker.ShadowEnabled).IsTrue();
        } finally {
            button.Free();
        }
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
            // 3.2: a 14 px square rotated 45 degrees (a 19.8 px diagonal) centred at (x + 31, y + H / 2).
            AssertThat(first.Marker is GlassMarker).IsTrue();
            AssertThat(Mathf.Abs(first.Marker.Size.X - 14f * Mathf.Sqrt2) < 0.01f).IsTrue();
            AssertThat(Mathf.Abs(CentreX(first.Marker) - 31f) < 0.01f).IsTrue();
            AssertThat(Mathf.Abs(first.Marker.Position.Y + first.Marker.Size.Y / 2f - 40f) < 0.01f).IsTrue();
            AssertThat(first.Marker.Tint).IsEqual(UIPalette.Edge);
            AssertThat(first.Marker.ShadowEnabled).IsFalse();
        } finally {
            PulpPromptGlyphs.DeviceOverrideForTesting = null;
            button.Free();
        }
    }

    [TestCase]
    public void CompactButtonsCentreTheirMarkerAtTwentyNine() {
        Button button = BuildButton(false, "PulpButtonCompact");
        button.CustomMinimumSize = new Vector2(400f, 58f);
        button.Size = new Vector2(400f, 58f);
        try {
            PulpButtonDecor decor = PulpButtonDecor.Apply(button, compact: true);
            decor.Refresh();
            AssertThat(Mathf.Abs(CentreX(decor.Marker) - 29f) < 0.01f).IsTrue();
            AssertThat(Mathf.Abs(decor.Marker.Size.X - 14f * Mathf.Sqrt2) < 0.01f).IsTrue();
        } finally {
            button.Free();
        }
    }

    [TestCase]
    public void TheBadgeShowsOnlyOnTheFocusedRowSwitchesBetweenPadAndKeyCapAndHoverTakesFocus() {
        PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Pad;
        Button focused = BuildButton();
        Button other = BuildButton();
        Button disabled = BuildButton();
        disabled.Disabled = true;
        try {
            PulpButtonDecor onFocused = PulpButtonDecor.Apply(focused);
            PulpButtonDecor onOther = PulpButtonDecor.Apply(other);
            PulpButtonDecor.Apply(disabled);
            focused.GrabFocus();
            onFocused.Refresh();
            onOther.Refresh();
            AssertThat(onFocused.Badge.Visible).IsTrue();
            AssertThat(onOther.Badge.Visible).IsFalse();
            AssertThat(onFocused.Marker.Tint).IsEqual(UIPalette.Focus);
            AssertThat(onFocused.BadgeDevice).IsEqual(PromptDevice.Pad);
            // 3.2: a 36 px ring at the label's right end: the box from W - 90 to W - 54 on a PulpButton row.
            AssertThat(onFocused.Badge.Size).IsEqual(new Vector2(36f, 36f));
            AssertThat(onFocused.Badge.Position.X).IsEqual(640f - 54f - 36f);
            bool hasA = false, hasRing = false;
            foreach (Node child in onFocused.Badge.GetChildren()) {
                if (child is Label l && l.Text == "A") hasA = true;
                if (child is GlassPromptRing ring && ring.RingColor == onFocused.CurrentLabelColor) hasRing = true;
            }
            AssertThat(hasA).IsTrue();
            AssertThat(hasRing).OverrideFailureMessage("The badge's ring takes the focused label's colour.").IsTrue();

            PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Keyboard;
            onFocused.Refresh();
            AssertThat(onFocused.BadgeDevice).IsEqual(PromptDevice.Keyboard);
            AssertThat(onFocused.Badge.GetChild(0) is PanelContainer).IsTrue();
            AssertThat(onFocused.Badge.Visible).IsTrue();

            // Hover is focus (AC8): the mouse entering another row moves the focus, badge and all; a disabled row does not take it.
            other.EmitSignal(Control.SignalName.MouseEntered);
            AssertThat(other.HasFocus()).IsTrue();
            onFocused.Refresh();
            AssertThat(onOther.Badge.Visible).IsTrue();
            AssertThat(onFocused.Badge.Visible).IsFalse();
            disabled.EmitSignal(Control.SignalName.MouseEntered);
            AssertThat(disabled.HasFocus()).IsFalse();
            AssertThat(other.HasFocus()).IsTrue();
        } finally {
            PulpPromptGlyphs.DeviceOverrideForTesting = null;
            focused.Free();
            other.Free();
            disabled.Free();
        }
    }

    [TestCase]
    public void ADisabledButtonDashesItsMarkerAndShowsNoBadge() {
        Button button = BuildButton();
        try {
            PulpButtonDecor decor = PulpButtonDecor.Apply(button);
            button.Disabled = true;
            decor.Refresh();
            AssertThat(((GlassMarker)decor.Marker).State).IsEqual(PulpButtonDecor.MarkerState.Disabled);
            AssertThat(decor.Marker.Tint).IsEqual(PulpButtonDecor.MarkerDisabledTint);
            AssertThat(decor.Marker.ShadowEnabled).IsFalse();
            AssertThat(decor.Badge.Visible).IsFalse();
        } finally {
            button.Free();
        }
    }

    [TestCase]
    public void SelectedSwapsToTheGlassSelectedChevronAndBackAndFollowsToggleMode() {
        Button button = BuildButton(toggle: true);
        try {
            PulpButtonDecor decor = PulpButtonDecor.Apply(button);
            AssertThat(button.HasThemeStyleboxOverride("normal")).IsFalse();
            StyleBox themeSelected = UIPalette.LoadTheme().GetStylebox("selected", "PulpButton");
            AssertObject(themeSelected).IsNotNull();

            button.ButtonPressed = true;
            AssertThat(decor.Selected).IsTrue();
            var plate = button.GetThemeStylebox("normal") as StyleBoxTexture;
            AssertObject(plate).IsNotNull();
            // 3.2 selected: the theme's selected chevron (glass_btn_selected, the edge hairline), label ink, marker filled accent.
            AssertThat(plate.Texture.ResourcePath.EndsWith("glass_btn_selected.png")).IsTrue();
            AssertThat(button.GetThemeStylebox("hover")).IsEqual(plate);
            AssertThat(decor.CurrentLabelColor).IsEqual(UIPalette.Ink);
            AssertThat(((GlassMarker)decor.Marker).State).IsEqual(PulpButtonDecor.MarkerState.Selected);
            AssertThat(decor.Marker.Tint).IsEqual(UIPalette.Accent);

            button.ButtonPressed = false;
            AssertThat(decor.Selected).IsFalse();
            AssertThat(button.HasThemeStyleboxOverride("normal")).IsFalse();
            AssertThat(button.HasThemeStyleboxOverride("hover")).IsFalse();
            AssertThat(decor.CurrentLabelColor).IsEqual(UIPalette.Ink2);
            AssertThat(decor.Marker.Tint).IsEqual(UIPalette.Edge);
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

    [TestCase]
    public void TheDrawnLabelIsUpperCaseWhileTheButtonKeepsItsRawKey() {
        Button button = BuildButton();
        button.Text = "menu_story_mode";
        try {
            PulpButtonDecor decor = PulpButtonDecor.Apply(button);
            AssertThat(button.Text).IsEqual("menu_story_mode");
            AssertThat(decor.DrawnText).IsEqual("STORY MODE");
            AssertThat(PulpButtonDecor.ToDrawnText("Quick play (Einstein vs CPU)")).IsEqual("QUICK PLAY (EINSTEIN VS CPU)");
        } finally {
            button.Free();
        }
    }

    [TestCase]
    public void TheButtonsOwnGlyphsAreTransparentAndTheDecorDrawsTheGlassColourPerState() {
        Button button = BuildButton();
        button.Text = "menu_settings";
        try {
            Color normal = button.GetThemeColor("font_color");
            Color focus = button.GetThemeColor("font_focus_color");
            Color disabled = button.GetThemeColor("font_disabled_color");
            // T10: normal ink2, focused white, disabled ink3 (the theme file stores seven digits, so compare to 1e-4).
            AssertThat(SameColor(normal, UIPalette.Ink2)).OverrideFailureMessage($"normal is {normal}").IsTrue();
            AssertThat(focus).IsEqual(Colors.White);
            AssertThat(SameColor(disabled, UIPalette.Ink3)).OverrideFailureMessage($"disabled is {disabled}").IsTrue();
            PulpButtonDecor decor = PulpButtonDecor.Apply(button);
            decor.Refresh();
            foreach (string slot in new[] { "font_color", "font_hover_color", "font_focus_color", "font_pressed_color",
                "font_hover_pressed_color", "font_disabled_color", "font_outline_color" }) {
                AssertThat(button.GetThemeColor(slot).A).IsEqual(0f);
            }
            AssertThat(decor.CurrentLabelColor).IsEqual(normal);
            button.GrabFocus();
            AssertThat(decor.CurrentLabelColor).IsEqual(focus);
            button.Disabled = true;
            AssertThat(decor.CurrentLabelColor).IsEqual(disabled);
            AssertThat(PulpButtonDecor.LabelColorSlot(false, BaseButton.DrawMode.Hover, false)).IsEqual("font_hover_color");
            AssertThat(PulpButtonDecor.LabelColorSlot(false, BaseButton.DrawMode.Pressed, true)).IsEqual("font_pressed_color");

            decor.Uppercase = false;
            AssertThat(button.HasThemeColorOverride("font_color")).IsFalse();
            AssertThat(button.GetThemeColor("font_color")).IsEqual(normal);
        } finally {
            button.Free();
        }
    }

    [TestCase]
    public void ALongLabelShrinksToItsFloorThenEllipsizesAndKeepsTheBadgeRoom() {
        PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Pad;
        Button button = BuildButton();
        Button medium = BuildButton();
        // Fixed-width rows, as a capped stack makes them (ApplyStackWidth clips): the hidden glyphs do not widen the button.
        button.ClipText = true;
        medium.ClipText = true;
        button.Text = "A very long menu label that cannot possibly fit on one banner button row";
        medium.Text = "Quick play (Einstein vs CPU)";
        try {
            PulpButtonDecor decor = PulpButtonDecor.Apply(button);
            PulpButtonDecor mid = PulpButtonDecor.Apply(medium);
            // The medium row is 40 px short of what its label needs at the theme size, so it shrinks a little.
            float midWidth = PulpButtonDecor.RequiredWidth(medium) - 40f;
            medium.CustomMinimumSize = new Vector2(midWidth, 80f);
            medium.Size = new Vector2(midWidth, 80f);
            AssertThat(button.Size.X).IsEqual(640f);
            decor.Refresh();
            mid.Refresh();
            float unfocused = decor.LabelRightLimit;
            int themeSize = button.GetThemeFontSize("font_size");
            // 3.2: a label too long shrinks toward its floor (26 px for T10 at 100 %), then ellipsizes.
            AssertThat(decor.DrawnFontSize).IsEqual(PulpButtonDecor.ShrinkFloorFor(false));
            AssertThat(PulpButtonDecor.ShrinkFloorFor(false)).IsEqual(26);
            AssertThat(decor.LabelIsTrimmed).IsTrue();
            // A label that fits at a smaller size shrinks to it and is not trimmed.
            AssertThat(mid.DrawnFontSize < themeSize).IsTrue();
            AssertThat(mid.DrawnFontSize > PulpButtonDecor.ShrinkFloorFor(false)).IsTrue();
            AssertThat(mid.LabelIsTrimmed).IsFalse();
            button.GrabFocus();
            decor.Refresh();
            float focused = decor.LabelRightLimit;
            AssertThat(focused < unfocused).IsTrue();
            AssertThat(focused <= decor.Badge.Position.X - PulpButtonDecor.BadgeGap + 0.01f).IsTrue();
            AssertThat(decor.DrawnTextWidth > decor.LabelAvailableWidth).IsTrue();
            AssertThat(button.Text.StartsWith("A very long")).IsTrue();
        } finally {
            PulpPromptGlyphs.DeviceOverrideForTesting = null;
            button.Free();
            medium.Free();
        }
    }

    [TestCase]
    public void ADecoratedTwiceOrRefreshingButtonStillDrawsItsThemeColourNeverTransparent() {
        // The button re-enters Refresh from its own theme notification, as a screen that re-themes does: the
        // override the decor writes must never be read back as the theme colour.
        var button = new DecorRefreshingButton {
            Text = "menu_settings", ThemeTypeVariation = "TemporalGlassButton", FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(400f, 64f), Size = new Vector2(400f, 64f)
        };
        UIPalette.ApplyTheme(button);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(button);
        Color themeColor = button.GetThemeColor("font_color");
        try {
            AssertThat(themeColor.A).IsEqual(1f);
            PulpButtonDecor decor = PulpButtonDecor.Apply(button);
            PulpButtonDecor.Apply(button);
            decor.Refresh();
            decor.Refresh();
            button.Notification((int)Control.NotificationThemeChanged);
            AssertThat(decor.CurrentLabelColor.A).IsEqual(1f);
            AssertThat(decor.CurrentLabelColor).IsEqual(themeColor);
            AssertThat(button.GetThemeColor("font_color").A).IsEqual(0f);
            // Hiding the glyphs and restoring them leaves the theme colour as it was.
            decor.Uppercase = false;
            AssertThat(button.GetThemeColor("font_color")).IsEqual(themeColor);
            decor.Uppercase = true;
            decor.Refresh();
            AssertThat(decor.CurrentLabelColor).IsEqual(themeColor);
        } finally {
            button.Free();
        }
    }

    [TestCase]
    public void AMultiLineLabelWrapsWithinTheAvailableWidthAndKeepsTheBadgeRoom() {
        PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Pad;
        Button button = BuildButton(false, "TemporalGlassButton");
        button.CustomMinimumSize = new Vector2(800f, 140f);
        button.Size = new Vector2(800f, 140f);
        button.Text = "Easy\n5 rewinds; 70% HP restoration; checkpoints refill your rewinds every time you strike one";
        try {
            PulpButtonDecor decor = PulpButtonDecor.Apply(button);
            button.GrabFocus();
            decor.Refresh();
            AssertThat(decor.WrappedLineCount >= 3).IsTrue();
            AssertThat(decor.WidestWrappedLine <= decor.LabelAvailableWidth + 0.5f).IsTrue();
            AssertThat(decor.LabelRightLimit <= decor.Badge.Position.X).IsTrue();
        } finally {
            PulpPromptGlyphs.DeviceOverrideForTesting = null;
            button.Free();
        }
    }

    [TestCase]
    public void AStackSharesItsWidestRequiredWidthUpToItsCap() {
        var stack = new List<Button>();
        try {
            foreach (string key in new[] { "menu_settings", "menu_calibration_drills", "menu_quit" }) {
                Button row = BuildButton();
                row.Text = key;
                row.CustomMinimumSize = new Vector2(0f, 68f);
                PulpButtonDecor.Apply(row);
                stack.Add(row);
            }
            // 3.2 D6: 58 + label + 16 + 36 + 54, the label measured upper-case in the row's own font; "CALIBRATION DRILLS" ~ 636.
            Button widest = stack[1];
            string drawn = PulpButtonDecor.ToDrawnText(widest.Tr(widest.Text));
            float label = widest.GetThemeFont("font").GetStringSize(drawn, HorizontalAlignment.Left, -1f, widest.GetThemeFontSize("font_size")).X;
            float expected = Mathf.Ceil(58f + label + 16f + 36f + 54f);
            AssertThat(Mathf.Abs(PulpButtonDecor.RequiredWidth(widest) - expected) < 1f).IsTrue();
            AssertThat(expected > 560f && expected < 700f).OverrideFailureMessage($"CALIBRATION DRILLS needs {expected}").IsTrue();
            AssertThat(Mathf.Abs(PulpButtonDecor.RequiredWidth(widest, reservePrompt: false) - (expected - 52f)) < 1f).IsTrue();
            float width = PulpButtonDecor.ApplyStackWidth(stack, 560f, 700f);
            AssertThat(width).IsEqual(PulpButtonDecor.RequiredWidth(widest));
            foreach (Button row in stack) {
                AssertThat(row.CustomMinimumSize.X).IsEqual(width);
                AssertThat(row.CustomMinimumSize.Y).IsEqual(68f);
            }
            // Short labels only: the stack keeps its minimum.
            var shortOnes = new List<Button> { stack[0], stack[2] };
            AssertThat(PulpButtonDecor.ApplyStackWidth(shortOnes, 560f, 700f)).IsEqual(560f);
            // A label wider than the cap: the stack stops at 700 and that row's label shrinks (and stays at least its floor).
            Button quick = BuildButton();
            quick.Text = "Quick play (Einstein vs CPU)";
            quick.CustomMinimumSize = new Vector2(0f, 68f);
            PulpButtonDecor quickDecor = PulpButtonDecor.Apply(quick);
            stack.Add(quick);
            AssertThat(PulpButtonDecor.ApplyStackWidth(stack, 560f, 700f)).IsEqual(700f);
            // The cap holds even for a label whose own (hidden) glyphs are wider than it: the stack clips the button text.
            Button overlong = BuildButton();
            overlong.Text = "A very long menu label that cannot possibly fit on one banner button row";
            overlong.CustomMinimumSize = new Vector2(0f, 68f);
            PulpButtonDecor.Apply(overlong);
            stack.Add(overlong);
            AssertThat(PulpButtonDecor.ApplyStackWidth(stack, 560f, 700f)).IsEqual(700f);
            AssertThat(overlong.ClipText).IsTrue();
            AssertThat(overlong.GetCombinedMinimumSize().X).IsEqual(700f);
            quick.Size = new Vector2(700f, 68f);
            quickDecor.Refresh();
            AssertThat(quickDecor.DrawnFontSize < quick.GetThemeFontSize("font_size")).IsTrue();
            AssertThat(quickDecor.DrawnFontSize >= PulpButtonDecor.ShrinkFloorFor(false)).IsTrue();
        } finally {
            foreach (Button row in stack) row.Free();
        }
    }

    [TestCase]
    public async Task AStackBuiltBeforeItsScreenIsInTheTreeTakesItsWidthThereAndFollowsTheUiScale() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        float original = UIPalette.AppliedUiScale;
        var host = new VBoxContainer { Name = "StackHost" };
        UIPalette.ApplyTheme(host);
        var rows = new List<Button>();
        foreach (string key in new[] { "menu_settings", "menu_calibration_drills", "menu_quit" }) {
            var row = new Button {
                Text = key, ThemeTypeVariation = "PulpButton", FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(0f, 68f)
            };
            host.AddChild(row);
            PulpButtonDecor.Apply(row);
            rows.Add(row);
        }
        async Task Frames() {
            for (int i = 0; i < 4; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        try {
            // A screen that builds its stack before it is added to the tree (as a code-built menu does).
            PulpButtonDecor.ApplyStackWidth(rows, 560f, 700f);
            tree.Root.AddChild(host);
            await Frames();
            float expected = PulpButtonDecor.RequiredWidth(rows[1]);
            AssertThat(expected > 600f && expected < 700f).OverrideFailureMessage($"Harness: CALIBRATION DRILLS needs {expected}").IsTrue();
            foreach (Button row in rows) AssertThat(row.CustomMinimumSize.X).IsEqual(expected);
            // At UI scale 1.4 the widest label needs more than the cap: the bound rule runs again and the stack stops at 700.
            UIPalette.ApplyUiScale(1.4f);
            await Frames();
            AssertThat(PulpButtonDecor.RequiredWidth(rows[1]) > 700f).IsTrue();
            foreach (Button row in rows) AssertThat(row.CustomMinimumSize.X).IsEqual(700f);
            UIPalette.ApplyUiScale(1f);
            await Frames();
            foreach (Button row in rows) AssertThat(row.CustomMinimumSize.X).IsEqual(expected);
        } finally {
            UIPalette.ApplyUiScale(original);
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }
}

/// <summary>A button that calls the decor's Refresh from its own theme notification (non-suite helper node).</summary>
public partial class DecorRefreshingButton : Button {
    public override void _Notification(int what) {
        if (what != NotificationThemeChanged) return;
        foreach (Node child in GetChildren()) if (child is PulpButtonDecor decor) decor.Refresh();
    }
}
