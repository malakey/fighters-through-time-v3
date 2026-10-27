using Godot;

namespace FTT.UI {

    /// <summary>
    /// FU-MENU (2026-10-25): the one shared mechanism that keeps a centred menu
    /// panel on screen. Attach it as a child of a panel that already sits in a
    /// centring parent (a full-rect <c>CenterContainer</c>); whenever the panel's
    /// combined minimum size would not fit the viewport minus
    /// <see cref="MarginPixels"/> on every side (the 1.4 UI scale grows the font
    /// sizes, and so every panel, in place), it is scaled down uniformly about
    /// its centre until it does, never below <see cref="MinScale"/>. Layout,
    /// node paths and focus chains are untouched, because a control's
    /// <c>Scale</c> is not part of its container layout.
    ///
    /// <para>A <c>Container</c> resets the scale of its children every time it
    /// sorts them, so when the panel's parent is a container (the usual
    /// <c>CenterContainer</c>) the scale is written to that parent instead; it is a
    /// full-rect centring node, so the pivot at its centre is the screen centre.</para>
    ///
    /// <para>TG-CLOSE: a fit attached through <see cref="AttachClearOfPromptBar"/> also keeps the panel
    /// <see cref="PromptGapPixels"/> clear of the screen's prompt bar (the kit's <see cref="PulpScreenKit.PromptBarName"/>
    /// under the given host), measured from the bar's real top every time the bar is laid out: one rule for every
    /// overlay and the Resonance grid, where TG-SCREENS kept a 56 px reserve scaled with the UI scale and TG-PAUSE an
    /// unscaled one (the bar is 34 px at 1.0 and 35 at 1.4).</para>
    /// </summary>
    public partial class MenuSafeAreaFit : Node {
        public const string NodeName = "SafeAreaFit";

        /// <summary>Clear space kept on each edge, in viewport pixels.</summary>
        public const float MarginPixels = 24f;

        /// <summary>The smallest scale ever applied; below it the panel clips rather than shrinks.</summary>
        public const float MinScale = 0.5f;

        /// <summary>The clear gap a panel keeps from the screen's prompt bar (TG-CLOSE).</summary>
        public const float PromptGapPixels = 10f;

        private Control _target;
        private Control _scaled;
        private Viewport _viewport;
        private float _extraVertical;
        private float _appliedExtra;
        private Control _barHost;
        private Control _bar;
        private bool _applyQueued;

        /// <summary>
        /// Pure rule: the clear space a centred panel keeps above and below itself so its bottom edge stays
        /// <see cref="PromptGapPixels"/> above a prompt bar whose top is at <paramref name="barTop"/> (the fit's own
        /// <see cref="MarginPixels"/> counted in).
        /// </summary>
        public static float PromptClearance(float viewHeight, float barTop) =>
            Mathf.Max(0f, viewHeight - barTop + PromptGapPixels - MarginPixels);

        /// <summary>The clear space above and below the panel the last <see cref="Apply"/> used.</summary>
        public float ExtraVertical => _appliedExtra;

        /// <summary>The node whose prompt bar the panel clears (null: none).</summary>
        public Control PromptBarHost => _barHost;

        /// <summary>The panel this fit keeps on screen.</summary>
        public Control Target => _target;

        /// <summary>Pure rule: the uniform scale (at most 1) that fits <paramref name="size"/> into <paramref name="view"/>.</summary>
        public static float ScaleFor(Vector2 size, Vector2 view, float extraVertical = 0f) {
            if (size.X <= 0f || size.Y <= 0f) return 1f;
            float availableX = Mathf.Max(1f, view.X - 2f * MarginPixels);
            float availableY = Mathf.Max(1f, view.Y - 2f * (MarginPixels + extraVertical));
            float scale = Mathf.Min(1f, Mathf.Min(availableX / size.X, availableY / size.Y));
            return Mathf.Max(MinScale, scale);
        }

        /// <summary>
        /// Attaches the fit to <paramref name="panel"/> once (a second call returns the first). <paramref name="extraVertical"/>
        /// keeps that much more clear above and below (UR-POLISH: the Resonance grid clears the bottom-right prompt bar).
        /// </summary>
        public static MenuSafeAreaFit Attach(Control panel, float extraVertical = 0f) {
            if (panel == null) return null;
            // Parked beside the panel (under its centring parent) so the panel's own
            // child order, which scripts and tests index into, is unchanged.
            Node home = panel.GetParent() ?? panel;
            if (home.GetNodeOrNull<MenuSafeAreaFit>(NodeName) is { } existing) return existing;
            var fit = new MenuSafeAreaFit { Name = NodeName, _target = panel, _extraVertical = extraVertical, _appliedExtra = extraVertical };
            home.AddChild(fit);
            return fit;
        }

        /// <summary>
        /// Attaches the fit (as <see cref="Attach"/>) so the panel also stays <see cref="PromptGapPixels"/> clear of the
        /// prompt bar under <paramref name="barHost"/>, measured from the bar's top (the bar may be added after this call).
        /// </summary>
        public static MenuSafeAreaFit AttachClearOfPromptBar(Control panel, Control barHost) {
            MenuSafeAreaFit fit = Attach(panel);
            if (fit == null) return null;
            fit._barHost = barHost;
            fit.QueueApply();
            return fit;
        }

        public override void _Ready() {
            if (_target == null) return;
            _scaled = _target.GetParent() is Container && _target.GetParent() is Control parent ? parent : _target;
            // Grow both ways so a centring parent that outgrows the screen stays centred on it.
            _scaled.GrowHorizontal = Control.GrowDirection.Both;
            _scaled.GrowVertical = Control.GrowDirection.Both;
            _viewport = _target.GetViewport();
            _target.Resized += Apply;
            _target.MinimumSizeChanged += Apply;
            _target.VisibilityChanged += Apply;
            if (_viewport != null) _viewport.SizeChanged += Apply;
            Apply();
            QueueApply();   // once more after the build that attached the fit, which may add the prompt bar after it
        }

        public override void _ExitTree() {
            if (_target != null && GodotObject.IsInstanceValid(_target)) {
                _target.Resized -= Apply;
                _target.MinimumSizeChanged -= Apply;
                _target.VisibilityChanged -= Apply;
            }
            if (_viewport != null && GodotObject.IsInstanceValid(_viewport)) _viewport.SizeChanged -= Apply;
            UnhookBar();
            if (_scaled != null && GodotObject.IsInstanceValid(_scaled)) _scaled.Scale = Vector2.One;
            _target = null;
            _scaled = null;
            _viewport = null;
        }

        /// <summary>Recomputes and writes the scale and pivot. Public so a test can force it.</summary>
        public void Apply() {
            if (_target == null || _scaled == null || !_target.IsInsideTree() || _viewport == null) return;
            Vector2 size = _target.GetCombinedMinimumSize();
            size = new Vector2(Mathf.Max(size.X, _target.Size.X), Mathf.Max(size.Y, _target.Size.Y));
            Vector2 view = _viewport.GetVisibleRect().Size;
            _appliedExtra = Mathf.Max(_extraVertical, PromptBarReserve(view.Y));
            float scale = ScaleFor(size, view, _appliedExtra);
            _scaled.PivotOffset = _scaled.Size / 2f;
            Vector2 wanted = new(scale, scale);
            if (_scaled.Scale != wanted) _scaled.Scale = wanted;
        }

        private void QueueApply() {
            if (_applyQueued) return;
            _applyQueued = true;
            Callable.From(() => {
                _applyQueued = false;
                if (GodotObject.IsInstanceValid(this)) Apply();
            }).CallDeferred();
        }

        // The clearance the host's prompt bar needs, from its real top; follows the bar's layout (a key cap, a caption or
        // the UI scale changing its height). Measured whether or not the bar shows, so a panel never jumps when a screen
        // hides its bar under a child overlay.
        private float PromptBarReserve(float viewHeight) {
            if (_barHost == null || !GodotObject.IsInstanceValid(_barHost)) return 0f;
            Control bar = _barHost.GetNodeOrNull<Control>(PulpScreenKit.PromptBarName);
            if (!ReferenceEquals(bar, _bar)) {
                UnhookBar();
                _bar = bar;
                if (_bar != null) _bar.ItemRectChanged += Apply;
            }
            if (_bar == null || _bar.Size.Y <= 0f) return 0f;
            float top = (_bar.GetGlobalTransform() * new Rect2(Vector2.Zero, _bar.Size)).Position.Y;
            return PromptClearance(viewHeight, top);
        }

        private void UnhookBar() {
            if (_bar != null && GodotObject.IsInstanceValid(_bar)) _bar.ItemRectChanged -= Apply;
            _bar = null;
        }
    }
}
