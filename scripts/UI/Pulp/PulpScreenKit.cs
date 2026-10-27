using System.Collections.Generic;
using FTT.UI.Motion;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// The few helpers the screens share, built only from the kit's parts (the class keeps its legacy name; TG-WIDGETS
    /// restyled it for Temporal Glass): a panel tag that does not change the panel's node path, the glass sheen, a prompt
    /// bar, button decoration for a whole container, the focus row gap, the full-screen menu dressing (scrim, screen
    /// streak, flourish heading) and a leading icon for a Label whose text must stay one string.
    /// </summary>
    public static partial class PulpScreenKit {

        // ---- The focus row gap (UI_SPEC_GLASS 3.2, 9.1 item 4; plan 0.4 P1) ---------------------------------

        /// <summary>The widest decorated button that sits in a horizontal row (the 480 px Move List card button).</summary>
        public const float RowButtonMaxWidth = 480f;

        /// <summary>The spec's least gap between two buttons in a horizontal row: the 14 px slide plus 20 (9.1 item 4).</summary>
        public const float FocusRowGapFloor = 34f;

        /// <summary>Air left between a focused button's drawn extent and its neighbour's own plate.</summary>
        public const float FocusRowAir = 2f;

        /// <summary>The chevron variations a row of decorated buttons is built from (the shared-theme gap covers all three).</summary>
        private static readonly string[] ChevronVariations = {
            UIPalette.Variations.PulpButton, UIPalette.Variations.PulpButtonCompact, UIPalette.Variations.TemporalGlassButton
        };

        private static readonly StringName FocusName = "focus";
        private static readonly StringName[] StateNames = { "normal", "hover", "pressed", "hover_pressed", "disabled" };

        private static float? _focusRowGap;

        /// <summary>
        /// The least gap between two neighbouring decorated chevrons of the shared theme, derived, never typed: the largest
        /// required gap over the three chevron variations at <see cref="RowButtonMaxWidth"/>, from the theme's own focus
        /// overlay and state boxes and the real <see cref="FocusPose"/> (slide, press and its tween's peak), floored at
        /// <see cref="FocusRowGapFloor"/>. With the glass kit: a 14 px slide plus the overlay's 14 px expand plus 2 px of air
        /// is 30, so the spec's 34 holds.
        /// </summary>
        public static float FocusRowGap => _focusRowGap ??= Mathf.Max(FocusRowGapFloor, SharedThemeRowGap());

        /// <summary>The derivation behind <see cref="FocusRowGap"/> before the spec's floor (30 with the glass kit).</summary>
        public static float DerivedFocusRowGap => SharedThemeRowGap();

        /// <summary>
        /// The shared theme's chevron focus overlay's right expand margin (derived from the theme; it was a typed constant
        /// in the pulp kit, which is the defect review REVIEW-UI-PULP-CODEX-3 found).
        /// </summary>
        public static float FocusPlateExpandRight => SharedFocusExpand(Side.Right);

        /// <summary>The shared theme's chevron focus overlay's left expand margin (derived from the theme).</summary>
        public static float FocusPlateExpandLeft => SharedFocusExpand(Side.Left);

        private static float SharedFocusExpand(Side side) {
            Theme theme = UIPalette.LoadTheme();
            float most = 0f;
            if (theme == null) return 0f;
            foreach (string variation in ChevronVariations) {
                if (theme.HasStylebox(FocusName, variation)) most = Mathf.Max(most, Expand(theme.GetStylebox(FocusName, variation), side));
            }
            return most;
        }

        private static float SharedThemeRowGap() {
            Theme theme = UIPalette.LoadTheme();
            if (theme == null) return 0f;
            PoseModel pose = PoseModel.Default;
            float most = 0f;
            foreach (string variation in ChevronVariations) {
                Vector4 focus = theme.HasStylebox(FocusName, variation) ? Expands(theme.GetStylebox(FocusName, variation)) : Vector4.Zero;
                Vector4 state = Vector4.Zero;
                foreach (StringName name in StateNames) {
                    if (theme.HasStylebox(name, variation)) state = Max(state, Expands(theme.GetStylebox(name, variation)));
                }
                var size = new Vector2(RowButtonMaxWidth, 64f);
                most = Mathf.Max(most, RequiredGap(size, Max(focus, state), state, pose, size, Max(focus, state), state, pose));
            }
            return most;
        }

        /// <summary>
        /// The gap two neighbouring buttons need so that neither draws into the other: a focused button draws its focus
        /// overlay and state boxes from the frame focus lands, through its slide and its press (the tween's peak included);
        /// its neighbour draws its own state boxes, possibly still easing back from a slide of its own. Both directions are
        /// checked, plus <see cref="FocusRowAir"/>. Reads each button's real boxes (local overrides included) and its own
        /// <see cref="FocusPose"/>; a button without one does not move.
        /// </summary>
        public static float RequiredRowGap(Button left, Button right) {
            if (left == null || right == null) return 0f;
            ButtonReach a = ReachOf(left), b = ReachOf(right);
            return RequiredGap(a.Size, a.Focused, a.Unfocused, a.Pose, b.Size, b.Focused, b.Unfocused, b.Pose);
        }

        private readonly struct ButtonReach {
            public readonly Vector2 Size;
            public readonly Vector4 Focused, Unfocused;
            public readonly PoseModel Pose;
            public ButtonReach(Vector2 size, Vector4 focused, Vector4 unfocused, PoseModel pose) {
                Size = size; Focused = focused; Unfocused = unfocused; Pose = pose;
            }
        }

        private static ButtonReach ReachOf(Button button) {
            Vector4 state = Vector4.Zero;
            foreach (StringName name in StateNames) state = Max(state, Expands(button.GetThemeStylebox(name)));
            Vector4 focus = button.FocusMode == Control.FocusModeEnum.None ? Vector4.Zero : Expands(button.GetThemeStylebox(FocusName));
            FocusPose pose = null;
            for (int i = 0, n = button.GetChildCount(); i < n; i++) if (button.GetChild(i) is FocusPose p) pose = p;
            Vector2 size = button.Size;
            if (size.X <= 0f || size.Y <= 0f) size = button.GetCombinedMinimumSize();
            return new ButtonReach(size, Max(focus, state), state, pose != null ? PoseModel.Of(pose) : PoseModel.Still);
        }

        /// <summary>The pose numbers the gap derivation samples (a <see cref="FocusPose"/>'s, or none).</summary>
        private readonly struct PoseModel {
            public readonly Vector2 Focus, Press;
            public readonly float FocusScale, PressScale, Tilt;
            public readonly bool Moves;

            private PoseModel(Vector2 focus, float focusScale, float tilt, Vector2 press, float pressScale, bool moves) {
                Focus = focus; FocusScale = focusScale; Tilt = tilt; Press = press; PressScale = pressScale; Moves = moves;
            }

            public static PoseModel Of(FocusPose pose) =>
                new(pose.FocusOffset, pose.PoseScale, pose.TiltDegrees, pose.PressOffset, pose.PressScale, true);

            public static PoseModel Default => new(new Vector2(PulpMotionRules.FocusTranslateX, 0f), PulpMotionRules.FocusScale,
                PulpMotionRules.FocusTiltDegrees, new Vector2(PulpMotionRules.PressTranslate, 0f), PulpMotionRules.PressScale, true);

            public static PoseModel Still => new(Vector2.Zero, 1f, 0f, Vector2.Zero, 1f, false);
        }

        private static float? _peak;

        /// <summary>The focus tween's highest progress (above 1 when its curve overshoots), sampled from the real transition.</summary>
        public static float FocusTweenPeak() {
            if (_peak.HasValue) return _peak.Value;
            float peak = 1f;
            for (int i = 0; i <= 64; i++) {
                float t = i / 64f;
                float v = Tween.InterpolateValue(0f, 1f, t, 1f, FocusPose.Transition, FocusPose.Ease).AsSingle();
                peak = Mathf.Max(peak, v);
            }
            _peak = peak;
            return peak;
        }

        /// <summary>
        /// How far a button's drawn rect reaches past its rest rect (left, right) over its pose path: progress 0 to the
        /// tween's peak, pressed or not. <paramref name="grow"/> is the expand of the boxes it draws on that path.
        /// </summary>
        private static Vector2 Reach(Vector2 size, Vector4 grow, PoseModel pose) {
            var local = new Rect2(-grow.X, -grow.Y, size.X + grow.X + grow.Z, size.Y + grow.Y + grow.W);
            float left = grow.X, right = grow.Z;
            if (!pose.Moves) return new Vector2(left, right);
            float peak = FocusTweenPeak();
            Vector2 pivot = size / 2f;
            for (int i = 0; i <= 16; i++) {
                float t = peak * i / 16f;
                for (int pressed = 0; pressed <= 1; pressed++) {
                    Vector2 offset = (pose.Focus * t).Lerp(pose.Press, pressed);
                    float scale = Mathf.Lerp(1f + (pose.FocusScale - 1f) * t, pose.PressScale, pressed);
                    float tilt = Mathf.DegToRad(pose.Tilt) * t * (1 - pressed);
                    Transform2D xform = new Transform2D(tilt, new Vector2(scale, scale), 0f, offset + pivot)
                        * Transform2D.Identity.Translated(-pivot);
                    Rect2 posed = new Rect2(xform * local.Position, Vector2.Zero)
                        .Expand(xform * new Vector2(local.End.X, local.Position.Y)).Expand(xform * local.End)
                        .Expand(xform * new Vector2(local.Position.X, local.End.Y));
                    left = Mathf.Max(left, -posed.Position.X);
                    right = Mathf.Max(right, posed.End.X - size.X);
                }
            }
            return new Vector2(left, right);
        }

        private static float RequiredGap(Vector2 leftSize, Vector4 leftFocused, Vector4 leftUnfocused, PoseModel leftPose,
            Vector2 rightSize, Vector4 rightFocused, Vector4 rightUnfocused, PoseModel rightPose) {
            // The left button focused against the right one's own plate; then the right one focused against the left one's.
            float leftFocusRight = Reach(leftSize, leftFocused, leftPose).Y;
            float rightOwnLeft = Reach(rightSize, rightUnfocused, rightPose).X;
            float rightFocusLeft = Reach(rightSize, rightFocused, rightPose).X;
            float leftOwnRight = Reach(leftSize, leftUnfocused, leftPose).Y;
            return Mathf.Max(leftFocusRight + rightOwnLeft, rightFocusLeft + leftOwnRight) + FocusRowAir;
        }

        private static float Expand(StyleBox box, Side side) => box switch {
            StyleBoxTexture t => t.GetExpandMargin(side),
            StyleBoxFlat f => f.GetExpandMargin(side),
            _ => 0f
        };

        private static Vector4 Expands(StyleBox box) =>
            new(Expand(box, Side.Left), Expand(box, Side.Top), Expand(box, Side.Right), Expand(box, Side.Bottom));

        private static Vector4 Max(Vector4 a, Vector4 b) =>
            new(Mathf.Max(a.X, b.X), Mathf.Max(a.Y, b.Y), Mathf.Max(a.Z, b.Z), Mathf.Max(a.W, b.W));

        /// <summary>
        /// Widens a horizontal row's separation so a focused button never draws into its neighbour: the largest
        /// <see cref="RequiredRowGap"/> of its neighbouring buttons (their real boxes and poses), never less than the shared
        /// theme's <see cref="FocusRowGap"/> (so a row built before its theme resolves still keeps the spec's floor). A
        /// vertical stack needs nothing.
        /// </summary>
        public static void SeparateFocusRow(BoxContainer row) {
            if (row == null || row.Vertical) return;
            float wanted = FocusRowGap;
            Button previous = null;
            for (int i = 0, n = row.GetChildCount(); i < n; i++) {
                if (row.GetChild(i) is not Button button || !button.Visible) continue;
                if (previous != null) wanted = Mathf.Max(wanted, RequiredRowGap(previous, button));
                previous = button;
            }
            int separation = Mathf.CeilToInt(wanted);
            if (row.GetThemeConstant("separation") < separation) row.AddThemeConstantOverride("separation", separation);
        }

        // ---- Tags and dressing --------------------------------------------------------------------------

        public const string TagAnchorName = "PanelTagAnchor";
        public const string PromptBarName = "ScreenPromptBar";

        /// <summary>The tag's offset from the panel rectangle's corner (UI_SPEC_GLASS 3.1: chamfer 18 + 8, -11).</summary>
        public static readonly Vector2 TagFromPanelCorner = PulpTaggedPanel.TagOffset;

        /// <summary>
        /// Draws the panel's title tag without wrapping the panel (so its node path and its parent are
        /// untouched): a plain <see cref="Control"/> child, which a <see cref="PanelContainer"/> fits to its
        /// content area, holds a <c>PanelTagPlate</c> positioned back out to the panel's corner. An empty
        /// key hides the tag. Idempotent: a second call re-keys the first tag.
        /// </summary>
        public static Label AddTag(PanelContainer panel, string tagKey) {
            if (panel == null) return null;
            var anchor = panel.GetNodeOrNull<PanelTagAnchor>(TagAnchorName);
            if (anchor == null) {
                anchor = new PanelTagAnchor { Name = TagAnchorName };
                panel.AddChild(anchor);
            }
            anchor.SetKey(tagKey);
            return anchor.TagLabel;
        }

        /// <summary>
        /// The glass sheen (<see cref="GlassSheen"/>: over the slab, behind the content), and, when a key is given, the
        /// title tag; <paramref name="buttonInset"/> adds the 14 px button-stack inset (<see cref="InsetRight"/>).
        /// </summary>
        public static void DressPanel(PanelContainer panel, string tagKey = null, bool sheen = true, bool buttonInset = false) {
            if (panel == null) return;
            if (buttonInset) InsetRight(panel);
            if (sheen) GlassSheen.Attach(panel);
            if (!string.IsNullOrEmpty(tagKey)) AddTag(panel, tagKey);
        }

        /// <summary>
        /// The right inset a button stack keeps inside its panel's own padding (UI_SPEC_GLASS 3.2, D13): the focused row
        /// slides 14 px right, so the slide never crosses the panel edge and its 14 px glow stays inside the padding.
        /// </summary>
        public const float ButtonStackRightInset = PulpMotionRules.FocusTranslateX;

        private static readonly StringName StackInsetMeta = "glass_stack_inset";

        /// <summary>
        /// Gives a panel its own copy of its plate with <see cref="ButtonStackRightInset"/> added to its right padding
        /// (pause: 20 + 14). Idempotent; the shared theme plate is untouched.
        /// </summary>
        public static void InsetRight(PanelContainer panel) {
            if (panel == null || panel.HasMeta(StackInsetMeta)) return;
            if (panel.GetThemeStylebox("panel") is not StyleBox plate) return;
            var copy = (StyleBox)plate.Duplicate();
            copy.ContentMarginRight = Mathf.Max(0f, plate.GetContentMargin(Side.Right)) + ButtonStackRightInset;
            panel.AddThemeStyleboxOverride("panel", copy);
            panel.SetMeta(StackInsetMeta, true);
        }

        /// <summary>The full-screen prompt bar's margins (UI_SPEC_GLASS 3.8, 9.1: right 70, bottom 44).</summary>
        public const float PromptRightMargin = 70f, PromptCornerBottomMargin = 44f;

        /// <summary>
        /// A <see cref="PulpPromptBar"/> pinned bottom-right of <paramref name="host"/> (UI_SPEC_GLASS 3.8: 70 px from
        /// the right edge, 44 px from the bottom). Returns it.
        /// </summary>
        public static PulpPromptBar AddPromptBar(Control host, params (string Action, string CaptionKey)[] prompts) {
            if (host == null) return null;
            var bar = new PulpPromptBar { Name = PromptBarName };
            foreach ((string action, string caption) in prompts) bar.AddPrompt(action, caption);
            host.AddChild(bar);
            bar.AnchorLeft = 1f;
            bar.AnchorRight = 1f;
            bar.AnchorTop = 1f;
            bar.AnchorBottom = 1f;
            bar.GrowHorizontal = Control.GrowDirection.Begin;
            bar.GrowVertical = Control.GrowDirection.Begin;
            bar.OffsetRight = -PromptRightMargin;
            bar.OffsetBottom = -PromptCornerBottomMargin;
            return bar;
        }

        /// <summary>Gives every <see cref="Button"/> under <paramref name="root"/> the chevron decor.</summary>
        public static void DecorateButtons(Node root, bool compact = false) {
            if (root == null) return;
            for (int index = 0; index < root.GetChildCount(); index++) {
                Node child = root.GetChild(index);
                if (child is Button button && button.GetNodeOrNull<PulpButtonDecor>("PulpButtonDecor") == null
                    && !HasDecor(button)) {
                    PulpButtonDecor.Apply(button, compact);
                }
                DecorateButtons(child, compact);
            }
        }

        private static bool HasDecor(Button button) {
            for (int index = 0; index < button.GetChildCount(); index++) {
                if (button.GetChild(index) is PulpButtonDecor) return true;
            }
            return false;
        }

        /// <summary>
        /// Puts a kit icon at the left of a Label while the Label's <c>Text</c> stays one string: the left
        /// content margin makes room and the icon is a child centred on the line. Idempotent per label.
        /// </summary>
        public static PulpIcon AddLeadingIcon(Label label, string iconName, Color tint, float size = 28f) {
            if (label == null) return null;
            if (label.GetNodeOrNull<PulpIcon>("LeadingIcon") is { } existing) {
                existing.IconName = iconName;
                existing.Tint = tint;
                return existing;
            }
            float gap = 10f;
            var box = new StyleBoxEmpty { ContentMarginLeft = size + gap };
            label.AddThemeStyleboxOverride("normal", box);
            PulpIcon icon = PulpIcon.Create(iconName, tint, size);
            icon.Name = "LeadingIcon";
            label.AddChild(icon);
            icon.AnchorTop = 0.5f;
            icon.AnchorBottom = 0.5f;
            icon.OffsetLeft = 0f;
            icon.OffsetRight = size;
            icon.OffsetTop = -size / 2f;
            icon.OffsetBottom = size / 2f;
            return icon;
        }

        // ---- Full-screen menu dressing (UI_SPEC_GLASS 1.4, 3.7, 3.8, 9.1) --------------------------------

        public const string ScreenStreakName = ScreenStreak.NodeName;
        public const string MenuScrimName = "MenuScrim";

        /// <summary>
        /// The <c>dir-a</c> screen streak on a full-screen menu (never an overlay or gameplay, AC19): added as
        /// <paramref name="host"/>'s last child, full rect, above its content. Idempotent.
        /// </summary>
        public static ScreenStreak AddScreenStreak(Control host) {
            if (host == null) return null;
            if (host.GetNodeOrNull<ScreenStreak>(ScreenStreakName) is { } existing) return existing;
            var streak = new ScreenStreak();
            host.AddChild(streak);
            streak.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return streak;
        }

        /// <summary>
        /// The menu scrim (1.3: 90 degrees, rgba(4,7,20) at .96, .84 at 26 %, .35 at 48 %, 0 at 66 %): the kit's
        /// <c>glass_menu_scrim</c> gradient stretched full screen, darkest at the left; <paramref name="mirrored"/> darkens the
        /// right for right-hand content (9.1). Inserted at <paramref name="index"/> among <paramref name="host"/>'s children
        /// (0: behind everything; pass the art's index + 1 to sit just above the art). Idempotent.
        /// </summary>
        public static TextureRect AddMenuScrim(Control host, bool mirrored = false, int index = 0) {
            if (host == null) return null;
            if (host.GetNodeOrNull<TextureRect>(MenuScrimName) is { } existing) {
                existing.FlipH = mirrored;
                return existing;
            }
            var scrim = new TextureRect {
                Name = MenuScrimName, Texture = PulpKit.Texture("glass_menu_scrim"), FlipH = mirrored,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            host.AddChild(scrim);
            host.MoveChild(scrim, Mathf.Clamp(index, 0, host.GetChildCount() - 1));
            scrim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return scrim;
        }

        /// <summary>
        /// A screen heading between flourishes (3.7, 9.1 item 2): a row of a left flourish (a 240 px rule, 24 px, the 11 px
        /// accent diamond), 24 px, the heading in <paramref name="role"/> (T03 <c>BannerHeading</c> by default, upper-case,
        /// its <c>Text</c> the raw key), 24 px and the mirrored flourish. Added to <paramref name="parent"/>; returns the label.
        /// </summary>
        public static Label AddFlourishHeading(Control parent, string textKey, string role = null, float ruleLength = GlassFlourish.HeadingRule) {
            if (parent == null) return null;
            var row = new HBoxContainer { Name = "FlourishHeading", Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", (int)GlassFlourish.HeadingGap);
            GlassFlourish left = GlassFlourish.Create(GlassFlourish.FlourishShape.RuleDiamond, ruleLength, GlassFlourish.HeadingGap);
            left.Name = "FlourishLeft";
            left.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            var label = new Label {
                Name = "Heading", Text = textKey ?? "", Uppercase = true,
                ThemeTypeVariation = role ?? UIPalette.LabelRoles.BannerHeading, VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            GlassFlourish right = GlassFlourish.Create(GlassFlourish.FlourishShape.DiamondRule, ruleLength, GlassFlourish.HeadingGap);
            right.Name = "FlourishRight";
            right.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(left);
            row.AddChild(label);
            row.AddChild(right);
            parent.AddChild(row);
            return label;
        }

        /// <summary>The buttons of a stack, in order (for <see cref="PulpButtonDecor.ApplyStackWidth"/>).</summary>
        public static List<Button> ButtonsOf(Node stack) {
            var buttons = new List<Button>();
            if (stack == null) return buttons;
            for (int i = 0, n = stack.GetChildCount(); i < n; i++) if (stack.GetChild(i) is Button button) buttons.Add(button);
            return buttons;
        }
    }
}
