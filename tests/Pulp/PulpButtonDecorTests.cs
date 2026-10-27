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

    /// <summary>
    /// Codex review 1, finding 2: hover is focus (AC8), except while an editable text field holds the keyboard (typing the
    /// LAN address): the hovered row and the hovered select tile show their hover look and leave the focus, and the badge,
    /// with the field.
    /// </summary>
    [TestCase]
    public async Task HoverLeavesTheKeyboardWithATextFieldOnARowAndATile() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Pad;
        var host = new VBoxContainer { Name = "HoverHost" };
        UIPalette.ApplyTheme(host);
        tree.Root.AddChild(host);
        var line = new LineEdit { Name = "Address", CustomMinimumSize = new Vector2(400f, 50f) };
        var text = new TextEdit { Name = "Notes", CustomMinimumSize = new Vector2(400f, 80f) };
        var join = new Button {
            Name = "Join", Text = "menu_settings", ThemeTypeVariation = "PulpButton", FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(560f, 68f)
        };
        var tileHost = new Button { Name = "Tile", FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(164f, 164f) };
        host.AddChild(line);
        host.AddChild(text);
        host.AddChild(join);
        host.AddChild(tileHost);
        async Task Frames(int count) {
            for (int i = 0; i < count; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        // As the viewport moves the mouse over a control: its hover notification (a button's hover state) and the
        // mouse_entered signal; leaving, mouse_exited and the exit notification.
        void Enter(Control control) {
            control.Notification((int)Control.NotificationMouseEnter);
            control.EmitSignal(Control.SignalName.MouseEntered);
        }
        void Leave(Control control) {
            control.EmitSignal(Control.SignalName.MouseExited);
            control.Notification((int)Control.NotificationMouseExit);
        }
        try {
            PulpButtonDecor decor = PulpButtonDecor.Apply(join);
            GlassTileDecor tile = GlassTileDecor.Attach(tileHost);
            await Frames(3);
            foreach (Control field in new Control[] { line, text }) {
                field.GrabFocus();
                AssertThat(field.HasFocus()).OverrideFailureMessage($"Harness: the {field.Name} field has no focus").IsTrue();
                Enter(join);
                await Frames(2);
                AssertThat(field.HasFocus()).OverrideFailureMessage($"hovering the row took the keyboard from {field.Name}").IsTrue();
                AssertThat(((GlassMarker)decor.Marker).State).IsEqual(PulpButtonDecor.MarkerState.Focus);   // the hover look
                AssertThat(decor.Badge.Visible).IsFalse();                                                    // the badge stays with the focus
                Leave(join);
                await Frames(2);
                AssertThat(((GlassMarker)decor.Marker).State).IsEqual(PulpButtonDecor.MarkerState.Normal);  // and leaves with the mouse
                Enter(tileHost);
                await Frames(1);
                AssertThat(field.HasFocus()).OverrideFailureMessage($"hovering the tile took the keyboard from {field.Name}").IsTrue();
                AssertThat(tile.Focused).IsFalse();
                AssertThat(tile.EffectiveEdge).IsEqual(GlassTileDecor.TileEdge.Focus);                        // the tile's hover look
                Leave(tileHost);
                AssertThat(tile.EffectiveEdge).IsEqual(GlassTileDecor.TileEdge.Dim);
            }
            // With no field holding the keyboard, hover takes the focus as before.
            line.ReleaseFocus();
            text.ReleaseFocus();
            Enter(join);
            AssertThat(join.HasFocus()).IsTrue();
            Leave(join);
            Enter(tileHost);
            AssertThat(tileHost.HasFocus()).IsTrue();
        } finally {
            PulpPromptGlyphs.DeviceOverrideForTesting = null;
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    /// <summary>
    /// Codex review 1, finding 3: a wrapping label's layout depends on the button's height (how many lines show), so a
    /// height change alone re-lays it out, both ways.
    /// </summary>
    [TestCase]
    public async Task AWrappingLabelShowsMoreOrFewerLinesWhenOnlyItsButtonsHeightChanges() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Control { Name = "WrapHost" };
        UIPalette.ApplyTheme(host);
        tree.Root.AddChild(host);
        var row = new Button {
            Text = "Go to the next area of the map now and fight on", ThemeTypeVariation = "PulpButton",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300f, 760f)
        };
        host.AddChild(row);
        async Task Frames(int count = 4) {
            for (int i = 0; i < count; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        try {
            PulpButtonDecor decor = PulpButtonDecor.Apply(row);
            await Frames();
            AssertThat(row.Size.Y >= 760f).IsTrue();
            AssertThat(decor.LabelIsTrimmed).OverrideFailureMessage($"a 760 px button should show all {decor.WrappedLineCount} wrapped lines").IsFalse();
            // Shorter: the button's own mixed-case wrap sets its height, and the drawn upper-case label needs more lines.
            row.CustomMinimumSize = new Vector2(300f, 0f);
            row.Size = new Vector2(300f, 0f);
            await Frames();
            StyleBox box = row.GetThemeStylebox("normal");
            float avail = row.Size.Y - box.GetContentMargin(Side.Top) - box.GetContentMargin(Side.Bottom);
            float lineHeight = row.GetThemeFont("font").GetHeight(row.GetThemeFontSize("font_size"));
            int fits = Mathf.FloorToInt((avail + 1f) / lineHeight);
            AssertThat(decor.WrappedLineCount > fits)
                .OverrideFailureMessage($"Harness: {decor.WrappedLineCount} lines, {fits} fit in {avail} px").IsTrue();
            AssertThat(decor.LabelIsTrimmed).OverrideFailureMessage("the shorter button keeps showing every line").IsTrue();
            // Taller again: every line shows.
            row.CustomMinimumSize = new Vector2(300f, 760f);
            await Frames();
            AssertThat(decor.LabelIsTrimmed).OverrideFailureMessage("the taller button keeps its ellipsis").IsFalse();
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    [TestCase]
    public async Task AnUnsizedRowWidensToItsDrawnLabelAndFollowsItsTextAndTheUiScaleWithoutALoop() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        float original = UIPalette.AppliedUiScale;
        PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Pad;
        var host = new Control { Name = "AutoWidthHost" };
        UIPalette.ApplyTheme(host);
        tree.Root.AddChild(host);
        // A row no screen sized: a footer or modal button that took its width from its own text until now.
        var row = new Button { Text = "menu_calibration_drills", ThemeTypeVariation = "PulpButton", FocusMode = Control.FocusModeEnum.All };
        host.AddChild(row);
        async Task Frames(int count = 4) {
            for (int i = 0; i < count; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        try {
            PulpButtonDecor decor = PulpButtonDecor.Apply(row);
            await Frames();
            float needed = PulpButtonDecor.RequiredWidth(row);
            // Harness: the button's own mixed-case text makes it narrower than its drawn upper-case label needs.
            AssertThat(row.GetMinimumSize().X < needed).OverrideFailureMessage($"Harness: own {row.GetMinimumSize().X}, needed {needed}").IsTrue();
            // Ruling 5: the decor widens the row to the drawn label, its paddings and the badge reserve; the label is whole.
            AssertThat(row.CustomMinimumSize.X).IsEqual(needed);
            AssertThat(row.Size.X >= needed).IsTrue();
            AssertThat(decor.DrawnFontSize).IsEqual(row.GetThemeFontSize("font_size"));
            AssertThat(decor.LabelIsTrimmed).IsFalse();
            // A row that never shows its badge reserves none.
            decor.ShowPrompt = false;
            await Frames();
            AssertThat(row.CustomMinimumSize.X).IsEqual(PulpButtonDecor.RequiredWidth(row, reservePrompt: false));
            decor.ShowPrompt = true;
            // A new text (a locale change reaches the same refresh) is measured again.
            row.Text = "menu_quit";
            await Frames();
            float quit = PulpButtonDecor.RequiredWidth(row);
            AssertThat(quit < needed).IsTrue();
            AssertThat(row.CustomMinimumSize.X).IsEqual(quit);
            // The UI scale grows the label and the paddings, and the row with them; and back.
            UIPalette.ApplyUiScale(1.4f);
            await Frames();
            AssertThat(row.CustomMinimumSize.X > quit).IsTrue();
            AssertThat(row.CustomMinimumSize.X).IsEqual(PulpButtonDecor.RequiredWidth(row));
            UIPalette.ApplyUiScale(1f);
            await Frames();
            AssertThat(row.CustomMinimumSize.X).IsEqual(quit);
            // Settled, a redraw and a refresh write nothing: no notification loop.
            int changes = 0;
            void Count() => changes++;
            row.MinimumSizeChanged += Count;
            await Frames(8);
            row.QueueRedraw();
            decor.Refresh();
            await Frames(8);
            row.MinimumSizeChanged -= Count;
            AssertThat(changes).IsEqual(0);
        } finally {
            PulpPromptGlyphs.DeviceOverrideForTesting = null;
            UIPalette.ApplyUiScale(original);
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    [TestCase]
    public async Task ARowWithTheScreensWidthAStacksWidthAWrapOrAClipKeepsThatWidth() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Pad;
        var host = new Control { Name = "KeptWidthHost" };
        UIPalette.ApplyTheme(host);
        tree.Root.AddChild(host);
        Button Row(string key) {
            var button = new Button { Text = key, ThemeTypeVariation = "PulpButton", FocusMode = Control.FocusModeEnum.All };
            host.AddChild(button);
            return button;
        }
        async Task Frames(int count = 4) {
            for (int i = 0; i < count; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        try {
            // A width the screen set before decorating, and one it sets after.
            Button sized = Row("menu_calibration_drills");
            sized.CustomMinimumSize = new Vector2(400f, 0f);
            PulpButtonDecor.Apply(sized);
            Button later = Row("menu_calibration_drills");
            PulpButtonDecor laterDecor = PulpButtonDecor.Apply(later);
            Button clipped = Row("menu_calibration_drills");
            clipped.ClipText = true;
            PulpButtonDecor.Apply(clipped);
            Button wrapping = Row("menu_calibration_drills");
            PulpButtonDecor.Apply(wrapping).GrowToFit = true;
            var stack = new List<Button> { Row("menu_calibration_drills"), Row("menu_quit") };
            foreach (Button button in stack) PulpButtonDecor.Apply(button);
            PulpButtonDecor.ApplyStackWidth(stack, 300f, 420f);
            await Frames();
            later.CustomMinimumSize = new Vector2(380f, later.CustomMinimumSize.Y);
            await Frames();
            float needed = PulpButtonDecor.RequiredWidth(sized);
            AssertThat(needed > 420f).OverrideFailureMessage($"Harness: the label needs {needed}").IsTrue();
            // The screen's width stays, set before or after the decor (the label shrinks inside it).
            AssertThat(sized.CustomMinimumSize.X).IsEqual(400f);
            AssertThat(later.CustomMinimumSize.X).IsEqual(380f);
            // A row that asked to clip, or to wrap, is not widened to the one-line label.
            AssertThat(clipped.CustomMinimumSize.X).IsEqual(0f);
            AssertThat(wrapping.CustomMinimumSize.X < needed).IsTrue();
            // The bound D6 rule owns its stack's width, however the decors refresh.
            foreach (Button button in stack) {
                button.QueueRedraw();
                PulpButtonDecor.Apply(button).Refresh();
            }
            await Frames();
            foreach (Button button in stack) AssertThat(button.CustomMinimumSize.X).IsEqual(420f);
            // A screen that clears its width hands the row back to the decor at its next refresh (focus, hover, a resize, a
            // text, theme or locale change; clearing alone raises nothing when the combined minimum does not move).
            later.CustomMinimumSize = new Vector2(0f, later.CustomMinimumSize.Y);
            laterDecor.Refresh();
            AssertThat(later.CustomMinimumSize.X).IsEqual(needed);
        } finally {
            PulpPromptGlyphs.DeviceOverrideForTesting = null;
            host.GetParent()?.RemoveChild(host);
            host.Free();
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
