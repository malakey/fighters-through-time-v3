using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// The heading flourishes (UI_SPEC_GLASS 3.7: a rule up to 240 px, 24 px, the 11 px accent diamond, 24 px, then the
    /// text) placed around a centred title <see cref="Label"/> as its own children, so the label keeps its node path.
    /// TG-CLOSE folded TG-SCREENS' copy (<c>GlassScreenDressing.FlankTitle</c>) and TG-MENU's (<c>MainMenu.HeadingFlourishes</c>,
    /// the sub-screen slabs' titles) into this one kit widget; the two hid their rules under 24 and 32 px of room, and the
    /// kit keeps 24. The rules shorten to the room the text leaves; both flourishes hide when less than
    /// <see cref="MinimumRule"/> is left, or the title wraps. Placement runs on a resize, a theme or a locale change,
    /// deferred once, and after <see cref="Refresh"/> (call it after setting the text); the draw belongs to
    /// <see cref="GlassFlourish"/>.
    /// </summary>
    public partial class TitleFlourishes : Control {
        public const string NodeName = "TitleFlourishes";
        public const float MinimumRule = 24f;

        private readonly float _maxRule;
        private Label _title;
        private readonly GlassFlourish _left, _right;
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

        /// <summary>Adds (or returns) the flourishes flanking <paramref name="title"/>'s centred text.</summary>
        public static TitleFlourishes Attach(Label title, float ruleLength = GlassFlourish.HeadingRule) {
            if (title == null) return null;
            if (title.GetNodeOrNull<TitleFlourishes>(NodeName) is { } existing) return existing;
            var flourishes = new TitleFlourishes(ruleLength);
            title.AddChild(flourishes);
            return flourishes;
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

        public override void _Notification(int what) {
            if (what == NotificationTranslationChanged) Refresh();
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
            float diamond = _left.DiamondSize;
            float side = (_title.Size.X - textWidth) / 2f;
            // Text, 24 px, diamond, 24 px (the flourish's own gap), rule.
            float rule = Mathf.Min(_maxRule, side - GlassFlourish.HeadingGap - diamond - GlassFlourish.HeadingGap);
            bool show = rule >= MinimumRule && _title.GetLineCount() <= 1 && !string.IsNullOrEmpty(text);
            _left.Visible = show;
            _right.Visible = show;
            PlacedRule = show ? rule : 0f;
            if (!show) return;
            _left.RuleLength = rule;
            _right.RuleLength = rule;
            float width = rule + GlassFlourish.HeadingGap + diamond;
            float centreX = _title.Size.X / 2f;
            float y = Mathf.Round((_title.Size.Y - diamond) / 2f);
            _left.Size = new Vector2(width, diamond);
            _right.Size = new Vector2(width, diamond);
            _left.Position = new Vector2(centreX - textWidth / 2f - GlassFlourish.HeadingGap - width, y);
            _right.Position = new Vector2(centreX + textWidth / 2f + GlassFlourish.HeadingGap, y);
        }
    }
}
