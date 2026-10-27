using FTT.UI.Motion;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// TG-SCREENS. The two screen types of UI_SPEC_GLASS 9.1, built from the kit's parts for the screens no mockup draws
    /// (results, loading, credits, Game Over, the Resonance grid, the drills, the play options, the network select, the
    /// restart choice):
    /// <list type="bullet">
    /// <item><b>Full-screen menus</b> carry their art (<see cref="AddMenuArt"/>: the sub-screen art, or the <c>bg0</c> field
    /// of loading and credits), the <c>dir-a</c> screen streak (<see cref="PulpScreenKit.AddScreenStreak"/>), a T03 heading
    /// with flourishes top-centre at y 40 (<see cref="AddScreenHeading"/>) and the prompt bar bottom-right.</item>
    /// <item><b>Overlays over gameplay</b> carry the build dim (<see cref="UIPalette.PauseDim"/>, Game Over the stronger
    /// <see cref="UIPalette.GameOverDim"/>), one centred glass slab, no streak (AC19, plan 0.4 P3) and the prompt bar
    /// bottom-centre at 34 (<see cref="AddOverlayPromptBar"/>); the slab clears the bar through
    /// <see cref="MenuSafeAreaFit.AttachClearOfPromptBar"/> (TG-CLOSE).</item>
    /// </list>
    /// Plus two parts the kit lacks (listed as kit gaps for the closeout): flourishes placed around an existing centred
    /// title label without moving it (<see cref="FlankTitle"/>, so a node path a test binds to survives), and the results
    /// row (<see cref="ResultRow"/>: a caption left, a tabular value right) drawn over a line whose own text stays the one
    /// formatted string the results tests read. Nothing here sets a font size.
    /// </summary>
    public static class GlassScreenDressing {

        /// <summary>The art of every full-screen sub-screen (UI_SPEC_GLASS 7.5, 9.1).</summary>
        public const string SubmenuArtPath = "res://assets/backgrounds/menu/temporal_glass_submenu_background.png";

        public const string MenuArtName = "MenuArt";
        public const string ScreenHeadingName = "ScreenHeading";

        /// <summary>A screen heading's top edge (9.1: "a T03 heading with flourishes top-centre at y 40").</summary>
        public const float HeadingTop = 40f;

        /// <summary>
        /// The clear space a centred block keeps above (and, <see cref="MenuSafeAreaFit"/> being symmetric, below) itself
        /// for the screen heading at UI scale 1.0: the heading's top at 40 plus the T03 line, rounded up.
        /// </summary>
        public const float HeadingReserve = 100f;

        /// <summary>
        /// <see cref="HeadingReserve"/> at the applied UI scale: the heading's text grows with the scale (at 1.4 the T03
        /// line ends near y 125), so a fixed reserve let the block's first line run into it. Read once at attach.
        /// </summary>
        public static float ScaledHeadingReserve => HeadingReserve * Mathf.Max(1f, UIPalette.AppliedUiScale);

        /// <summary>
        /// A chevron stack's D6 cap (3.2: 700 for menu stacks) at the applied UI scale. The kit leaves its cap unscaled, so at
        /// 1.4 a label that fits one line at 1.0 broke over three or four lines inside a 680 px row; scaled, it keeps about
        /// the line count it has at 1.0, and the screen's safe-area fit shrinks the slab if it then outgrows the view.
        /// </summary>
        public static float ScaledStackCap(float cap) => cap * Mathf.Max(1f, UIPalette.AppliedUiScale);

        /// <summary>
        /// The menu art behind a full-screen menu: a mouse-transparent <see cref="TextureRect"/> covering the screen (the
        /// art is cropped, never stretched), inserted at <paramref name="index"/> among <paramref name="host"/>'s children.
        /// Idempotent. A missing texture leaves the screen's own <c>bg0</c> field showing.
        /// </summary>
        public static TextureRect AddMenuArt(Control host, string texturePath = SubmenuArtPath, int index = 0) {
            if (host == null) return null;
            if (host.GetNodeOrNull<TextureRect>(MenuArtName) is { } existing) return existing;
            var art = new TextureRect {
                Name = MenuArtName,
                Texture = ResourceLoader.Exists(texturePath) ? GD.Load<Texture2D>(texturePath) : null,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            host.AddChild(art);
            host.MoveChild(art, Mathf.Clamp(index, 0, host.GetChildCount() - 1));
            art.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return art;
        }

        /// <summary>
        /// The screen heading of a full-screen menu (9.1 item 2): the T03 text between flourishes, centred on the screen's
        /// width with its top at <see cref="HeadingTop"/>. The holder is a <see cref="CenterContainer"/> (never a box that
        /// a layout sweep would mistake for the screen's panel). Returns the heading label (its <c>Text</c> the raw key).
        /// </summary>
        public static Label AddScreenHeading(Control host, string textKey) {
            if (host == null) return null;
            if (host.GetNodeOrNull<CenterContainer>(ScreenHeadingName) is { } existing) {
                return existing.FindChild("Heading", recursive: true, owned: false) as Label;
            }
            var holder = new CenterContainer { Name = ScreenHeadingName, MouseFilter = Control.MouseFilterEnum.Ignore };
            host.AddChild(holder);
            holder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
            holder.OffsetTop = HeadingTop;
            holder.OffsetBottom = HeadingTop;
            holder.GrowVertical = Control.GrowDirection.End;
            return PulpScreenKit.AddFlourishHeading(holder, textKey);
        }

        /// <summary>
        /// An overlay's prompt bar (9.1: bottom-centre, 34 px from the bottom): the shared <see cref="PulpScreenKit.AddPromptBar"/>
        /// (so it is the same <see cref="PulpPromptBar"/> named <see cref="PulpScreenKit.PromptBarName"/>) centred.
        /// </summary>
        public static PulpPromptBar AddOverlayPromptBar(Control host, params (string Action, string CaptionKey)[] prompts) {
            PulpPromptBar bar = PulpScreenKit.AddPromptBar(host, prompts);
            if (bar == null) return null;
            bar.Alignment = BoxContainer.AlignmentMode.Center;
            bar.AnchorLeft = 0.5f;
            bar.AnchorRight = 0.5f;
            bar.GrowHorizontal = Control.GrowDirection.Both;
            bar.OffsetLeft = 0f;
            bar.OffsetRight = 0f;
            bar.OffsetBottom = -PulpScreenKit.PromptBottomMargin;
            return bar;
        }

        /// <summary>
        /// An overlay panel appearing (MOT-A row 4 without its 24 px rise): alpha 0 to 1 over 0.22 s (Cubic Out) and the
        /// sheen's light band crossing once; under Reduced Temporal Effects a 0.15 s fade and no band. The rise is left out
        /// on purpose: the results, Game Over and restart panels are measured centred a few frames after they open
        /// (<c>MenuLayoutBoundsTests</c>), and a panel caught mid-rise reads as off-centre.
        /// </summary>
        public static Tween FadeIn(Control panel) {
            if (panel == null || !panel.IsInsideTree()) return null;
            Tween tween = PulpMotion.NewTween(panel);
            panel.Modulate = new Color(panel.Modulate, 0f);
            if (PulpMotion.Reduced) {
                tween.TweenProperty(panel, "modulate:a", 1f, PulpMotionRules.PanelFadeSeconds);
                return tween;
            }
            tween.TweenProperty(panel, "modulate:a", 1f, PulpMotionRules.PanelInSeconds)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            for (int i = 0, n = panel.GetChildCount(); i < n; i++) {
                if (panel.GetChild(i) is IPanelLightBand band) band.PlayLightBand(PulpMotionRules.PanelBandSeconds);
            }
            return tween;
        }

        private static readonly StringName[] HaloConstants = { "outline_size", "shadow_offset_x", "shadow_offset_y", "shadow_outline_size" };
        private static readonly StringName[] HaloColors = { "font_outline_color", "font_shadow_color" };

        /// <summary>
        /// Gives a plate role the scene halo of UI_SPEC_GLASS 2.4 when its label stands on art or a dim instead of glass:
        /// the halo L tier (display text, from <c>BannerHeading</c>) or halo S (from <c>SceneCaption</c>), copied from the
        /// shared theme so the values live once, in the generator. Never touches the font size.
        /// </summary>
        public static void ApplySceneHalo(Label label, bool large) {
            Theme theme = UIPalette.LoadTheme();
            if (label == null || theme == null) return;
            string source = large ? UIPalette.LabelRoles.BannerHeading : UIPalette.LabelRoles.SceneCaption;
            foreach (StringName constant in HaloConstants) {
                if (theme.HasConstant(constant, source)) label.AddThemeConstantOverride(constant, theme.GetConstant(constant, source));
            }
            foreach (StringName color in HaloColors) {
                if (theme.HasColor(color, source)) label.AddThemeColorOverride(color, theme.GetColor(color, source));
            }
        }

        /// <summary>A full-rect dim (<see cref="ColorRect"/>) that swallows the mouse, inserted at <paramref name="index"/>.</summary>
        public static ColorRect AddDim(Control host, string name, Color color, int index = 0) {
            if (host == null) return null;
            if (host.GetNodeOrNull<ColorRect>(name) is { } existing) {
                existing.Color = color;
                return existing;
            }
            var dim = new ColorRect { Name = name, Color = color, MouseFilter = Control.MouseFilterEnum.Stop };
            host.AddChild(dim);
            host.MoveChild(dim, Mathf.Clamp(index, 0, host.GetChildCount() - 1));
            dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return dim;
        }

        /// <summary>Adds (or returns) the flourishes flanking <paramref name="title"/>'s centred text; see <see cref="TitleFlourishes"/>.</summary>
        public static TitleFlourishes FlankTitle(Label title, float ruleLength = GlassFlourish.HeadingRule) {
            if (title == null) return null;
            if (title.GetNodeOrNull<TitleFlourishes>(TitleFlourishes.NodeName) is { } existing) return existing;
            var flourishes = new TitleFlourishes(ruleLength);
            title.AddChild(flourishes);
            return flourishes;
        }

        /// <summary>
        /// Splits a "Caption: {0}" style format into a results row: the caption is the text before the last ": " that a
        /// placeholder follows (formatted, so "Player {0} stocks lost: {1}" reads "Player 2 stocks lost" | "1"), else the text
        /// before the first placeholder ("Secrets {0}/{1}" reads "Secrets" | "0/1"); a format with neither is all caption.
        /// Placeholder numbers keep their meaning in either half. Pure; a malformed format falls back to the whole line.
        /// </summary>
        public static (string Caption, string Value) SplitRow(string format, params object[] args) {
            format ??= "";
            try {
                int colon = format.LastIndexOf(": ", System.StringComparison.Ordinal);
                if (colon > 0 && format.IndexOf('{', colon) >= 0) {
                    return (string.Format(format.Substring(0, colon), args).TrimEnd(), string.Format(format.Substring(colon + 2), args).Trim());
                }
                int brace = format.IndexOf('{');
                if (brace > 0) {
                    return (format.Substring(0, brace).TrimEnd(' ', ':', ' '), string.Format(format.Substring(brace), args).Trim());
                }
                return (string.Format(format, args), "");
            } catch (System.FormatException) {
                return (format, "");
            }
        }
    }

    /// <summary>
    /// TG-SCREENS. The heading flourishes (UI_SPEC_GLASS 3.7: a 240 px rule, 24 px, the 11 px accent diamond, 24 px, then
    /// the text) placed around a centred title <see cref="Label"/> as its own children, so the label keeps its node path.
    /// The rules shorten to the room the text leaves and both flourishes hide when less than
    /// <see cref="MinimumRule"/> is left (a long or wrapped title). Placement runs on a resize or a theme change, deferred
    /// once, and after <see cref="Refresh"/> (call it after setting the text); the draw belongs to <see cref="GlassFlourish"/>.
    /// </summary>
    public partial class TitleFlourishes : Control {
        public const string NodeName = "TitleFlourishes";
        public const float MinimumRule = 24f;
        public const float Diamond = 11f;

        private readonly float _maxRule;
        private Label _title;
        private GlassFlourish _left, _right;
        private bool _queued;

        /// <summary>The rule length placed last (0 when the flourishes are hidden).</summary>
        public float PlacedRule { get; private set; }
        public GlassFlourish Left => _left;
        public GlassFlourish Right => _right;

        public TitleFlourishes() : this(GlassFlourish.HeadingRule) { }

        public TitleFlourishes(float maxRule) {
            _maxRule = maxRule;
            Name = NodeName;
            MouseFilter = MouseFilterEnum.Ignore;
            _left = GlassFlourish.Create(GlassFlourish.FlourishShape.RuleDiamond, maxRule, GlassFlourish.HeadingGap);
            _left.Name = "FlourishLeft";
            _right = GlassFlourish.Create(GlassFlourish.FlourishShape.DiamondRule, maxRule, GlassFlourish.HeadingGap);
            _right.Name = "FlourishRight";
            AddChild(_left);
            AddChild(_right);
        }

        public override void _EnterTree() {
            _title = GetParent() as Label;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            if (_title == null) return;
            _title.Resized += Refresh;
            _title.ThemeChanged += Refresh;
            Refresh();
        }

        public override void _ExitTree() {
            if (_title != null && IsInstanceValid(_title)) {
                _title.Resized -= Refresh;
                _title.ThemeChanged -= Refresh;
            }
            _title = null;
        }

        /// <summary>Queues one placement after the label's layout and font size have settled.</summary>
        public void Refresh() {
            if (_queued) return;
            _queued = true;
            Callable.From(Place).CallDeferred();
        }

        private void Place() {
            _queued = false;
            if (!IsInstanceValid(this) || _title == null || !IsInstanceValid(_title)) return;
            Font font = _title.GetThemeFont("font");
            int size = _title.GetThemeFontSize("font_size");
            string text = _title.Tr(_title.Text);
            if (_title.Uppercase) text = PulpButtonDecor.ToDrawnText(text);
            float textWidth = font == null ? 0f : font.GetStringSize(text, HorizontalAlignment.Left, -1f, size).X;
            float side = (_title.Size.X - textWidth) / 2f;
            // Text, 24 px, diamond, 24 px (the flourish's own gap), rule.
            float rule = Mathf.Min(_maxRule, side - GlassFlourish.HeadingGap - Diamond - GlassFlourish.HeadingGap);
            bool show = rule >= MinimumRule && _title.GetLineCount() <= 1 && !string.IsNullOrEmpty(text);
            _left.Visible = show;
            _right.Visible = show;
            PlacedRule = show ? rule : 0f;
            if (!show) return;
            _left.RuleLength = rule;
            _right.RuleLength = rule;
            float width = rule + GlassFlourish.HeadingGap + Diamond;
            float centreX = _title.Size.X / 2f;
            float centreY = _title.Size.Y / 2f;
            _left.Size = new Vector2(width, Diamond);
            _right.Size = new Vector2(width, Diamond);
            _left.Position = new Vector2(centreX - textWidth / 2f - GlassFlourish.HeadingGap - width, centreY - Diamond / 2f);
            _right.Position = new Vector2(centreX + textWidth / 2f + GlassFlourish.HeadingGap, centreY - Diamond / 2f);
        }
    }

    /// <summary>
    /// TG-SCREENS. A results row (UI_SPEC_GLASS 9.2, level results: "label Outfit 500 20 .10em upper case ink2 left, value
    /// Outfit 600 26 ink right (tabular)") drawn over an existing line <see cref="Label"/>. The line keeps its whole
    /// formatted text (the results tests and screen readers read it) but does not draw it; its two children do: the
    /// caption (<c>StatLabel</c>, the nearest role, upper-case) on the left and the value (<c>SettingsRowValue</c>, Outfit
    /// 600 26 tabular) on the right. The line takes the value's role so it is as tall as the value. Idempotent per line.
    /// </summary>
    public partial class ResultRow : Control {
        public const string NodeName = "ResultRow";

        private Label _line;
        private readonly Label _caption;
        private readonly Label _value;

        public Label Caption => _caption;
        public Label Value => _value;

        public ResultRow() {
            Name = NodeName;
            MouseFilter = MouseFilterEnum.Ignore;
            _caption = new Label {
                Name = "Caption", ThemeTypeVariation = UIPalette.LabelRoles.StatLabel, Uppercase = true,
                VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled, ClipText = true
            };
            _value = new Label {
                Name = "Value", ThemeTypeVariation = UIPalette.LabelRoles.SettingsRowValue,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled
            };
            AddChild(_caption);
            AddChild(_value);
        }

        /// <summary>Dresses <paramref name="line"/> as a row (a second call returns the first).</summary>
        public static ResultRow Attach(Label line) {
            if (line == null) return null;
            if (line.GetNodeOrNull<ResultRow>(NodeName) is { } existing) return existing;
            var row = new ResultRow { _line = line };
            line.ThemeTypeVariation = UIPalette.LabelRoles.SettingsRowValue;
            line.SelfModulate = new Color(1f, 1f, 1f, 0f);
            line.AddChild(row);
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row._caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row._value.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            return row;
        }

        /// <summary>
        /// Sets the line's whole text (<c>string.Format(format, args)</c>) and the row's caption and value from it
        /// (<see cref="GlassScreenDressing.SplitRow"/>); the caption is clipped, never the value.
        /// </summary>
        public void SetText(string format, params object[] args) {
            if (_line != null) _line.Text = string.Format(format ?? "", args);
            (string caption, string value) = GlassScreenDressing.SplitRow(format, args);
            _caption.Text = caption;
            _value.Text = value;
            InsetCaption();
        }

        /// <summary>The gap kept between the caption's end and the value.</summary>
        public const float CaptionGap = 24f;

        // Keep the caption clear of the value: it may run to the value's left edge less a gap. Re-run after a theme
        // (UI-scale) change, once the value's font size has settled.
        private void InsetCaption() => _caption.OffsetRight = -(_value.GetMinimumSize().X + CaptionGap);

        private bool _insetQueued;

        public override void _Notification(int what) {
            if (what != NotificationThemeChanged || _insetQueued) return;
            _insetQueued = true;
            Callable.From(() => {
                _insetQueued = false;
                if (IsInstanceValid(this)) InsetCaption();
            }).CallDeferred();
        }

        /// <summary>Colours the value (the dust total reads in <c>res</c>).</summary>
        public void SetValueColor(Color color) => _value.AddThemeColorOverride("font_color", color);
    }
}
