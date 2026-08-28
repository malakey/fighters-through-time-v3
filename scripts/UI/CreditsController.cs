using Godot;

namespace FTT.UI {

    /// <summary>
    /// End-of-campaign credits roll (Package 5 A1). Scrolls the localized
    /// <see cref="CreditLines"/> table upward at <see cref="ScrollSpeed"/>, can be
    /// skipped with Confirm at any time, and raises <see cref="CreditsFinished"/>
    /// exactly once whether it ran to the end or was skipped.
    ///
    /// Runs on <see cref="Node.ProcessModeEnum.Always"/> so a paused tree still rolls.
    /// </summary>
    public partial class CreditsController : CanvasLayer {
        public const string SceneResourcePath = "res://scenes/ui/Credits.tscn";

        [Signal] public delegate void CreditsFinishedEventHandler();

        /// <summary>One credit row: a translation key plus its display weight.</summary>
        public readonly record struct CreditLine(string Key, bool IsHeading);

        /// <summary>
        /// The authored credit roll. Every key has an entry in localization/en.csv
        /// and is covered by the A1 localization test.
        /// </summary>
        public static readonly CreditLine[] CreditLines = {
            new("credits_title", true),
            new("credits_role_direction", true),
            new("credits_name_placeholder", false),
            new("credits_role_design", true),
            new("credits_name_placeholder", false),
            new("credits_role_engineering", true),
            new("credits_name_placeholder", false),
            new("credits_role_narrative", true),
            new("credits_name_placeholder", false),
            new("credits_role_art", true),
            new("credits_placeholder_era", false),
            new("credits_role_audio", true),
            new("credits_placeholder_era", false),
            new("credits_role_qa", true),
            new("credits_name_placeholder", false),
            new("credits_thanks", true),
            new("credits_thanks_body", false),
            new("credits_the_end", true)
        };

        [Export] public float ScrollSpeed = 110f;
        [Export] public float HeadingSpacing = 46f;
        [Export] public float LineSpacing = 30f;
        [Export] public float ReferenceViewportHeight = 1080f;

        /// <summary>True once the roll ended or was skipped; the signal fires only on the transition.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>Distance the roll has travelled, in pixels.</summary>
        public float ScrolledDistance { get; private set; }

        /// <summary>Total travel needed for the last line to clear the top of the screen.</summary>
        public float TotalScrollDistance { get; private set; }

        /// <summary>Package 8 B3: the roll's backdrop, tied to the shared palette.</summary>
        public static readonly Color BackdropColor = UIPalette.NavyDeep;

        private Control _scrollRoot;
        private ColorRect _shade;
        private VBoxContainer _lines;
        private Label _skipHint;
        private float _startY;

        /// <summary>Instantiates the authored Credits scene, or a code-built fallback.</summary>
        public static CreditsController CreateDefault() {
            if (ResourceLoader.Exists(SceneResourcePath)) {
                var packed = ResourceLoader.Load<PackedScene>(SceneResourcePath);
                if (packed?.Instantiate() is CreditsController authored) return authored;
            }
            return new CreditsController { Name = "Credits" };
        }

        public override void _Ready() {
            Layer = 98;
            ProcessMode = ProcessModeEnum.Always;
            ResolveOrBuildUI();
            if (_skipHint != null) _skipHint.Text = Tr("credits_skip_hint");
            PopulateLines();

            float viewportHeight = GetViewportHeight();
            _startY = viewportHeight;
            if (_scrollRoot != null) _scrollRoot.Position = new Vector2(_scrollRoot.Position.X, _startY);
            TotalScrollDistance = viewportHeight + EstimateContentHeight();
        }

        public override void _Process(double delta) {
            if (IsFinished) return;
            ScrolledDistance += ScrollSpeed * (float)delta;
            if (_scrollRoot != null) {
                _scrollRoot.Position = new Vector2(_scrollRoot.Position.X, _startY - ScrolledDistance);
            }
            if (ScrolledDistance >= TotalScrollDistance) Finish();
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (IsFinished || @event == null) return;
            bool confirm = @event.IsActionPressed(FTT.Core.InputManager.Actions.Interact)
                || @event.IsActionPressed(FTT.Core.InputManager.Actions.BasicAttack)
                || @event.IsActionPressed("ui_accept")
                || @event.IsActionPressed(FTT.Core.InputManager.Actions.Pause);
            if (!confirm) return;
            GetViewport()?.SetInputAsHandled();
            Skip();
        }

        /// <summary>Ends the roll immediately. Idempotent.</summary>
        public void Skip() => Finish();

        private void Finish() {
            if (IsFinished) return;
            IsFinished = true;
            if (_skipHint != null) _skipHint.Visible = false;
            EmitSignal(SignalName.CreditsFinished);
        }

        private float GetViewportHeight() {
            Rect2 visible = GetViewport()?.GetVisibleRect() ?? new Rect2(0, 0, 1920, ReferenceViewportHeight);
            return visible.Size.Y > 1f ? visible.Size.Y : ReferenceViewportHeight;
        }

        private float EstimateContentHeight() {
            float height = 0f;
            foreach (CreditLine line in CreditLines) height += line.IsHeading ? HeadingSpacing : LineSpacing;
            // The authored container may lay out taller than the estimate.
            return Mathf.Max(height, _lines?.Size.Y ?? 0f);
        }

        private void PopulateLines() {
            if (_lines == null) return;
            // Indexed removal rather than GetChildren(): the engine-returned
            // Godot.Collections.Array wrapper would land on the .NET finalizer
            // queue (CLAUDE.md failure signature 3).
            for (int index = _lines.GetChildCount() - 1; index >= 0; index--) {
                Node child = _lines.GetChild(index);
                _lines.RemoveChild(child);
                child.QueueFree();
            }
            for (int index = 0; index < CreditLines.Length; index++) {
                CreditLine line = CreditLines[index];
                var label = new Label {
                    Name = $"CreditLine_{index}",
                    Text = Tr(line.Key),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                // Headings ride the TitleLabel role; body lines keep the theme
                // default, so the roll follows the accessibility UI scale.
                if (line.IsHeading) label.ThemeTypeVariation = UIPalette.TitleLabelVariation;
                label.AddThemeColorOverride(
                    "font_color", line.IsHeading ? UIPalette.Cyan : UIPalette.TextPrimary);
                _lines.AddChild(label);
            }
        }

        private void ResolveOrBuildUI() {
            _shade = GetNodeOrNull<ColorRect>("Shade");
            _scrollRoot = GetNodeOrNull<Control>("Shade/Scroll");
            _lines = GetNodeOrNull<VBoxContainer>("Shade/Scroll/Lines");
            _skipHint = GetNodeOrNull<Label>("Shade/SkipHint");
            if (_scrollRoot == null || _lines == null) BuildFallbackUI();
            // Package 8 B3: the roll adopts the shared theme so a later font or
            // type-scale change reaches the credits without a second edit here.
            if (_shade != null) {
                UIPalette.ApplyTheme(_shade);
                _shade.Color = BackdropColor;
            }
        }

        private void BuildFallbackUI() {
            _shade = new ColorRect { Name = "Shade", Color = BackdropColor };
            ColorRect shade = _shade;
            shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            shade.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(shade);

            _scrollRoot = new Control { Name = "Scroll", MouseFilter = Control.MouseFilterEnum.Ignore };
            _scrollRoot.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            shade.AddChild(_scrollRoot);

            _lines = new VBoxContainer { Name = "Lines", MouseFilter = Control.MouseFilterEnum.Ignore };
            _lines.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            _lines.AddThemeConstantOverride("separation", 12);
            _scrollRoot.AddChild(_lines);

            _skipHint = new Label {
                Name = "SkipHint",
                Text = Tr("credits_skip_hint"),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _skipHint.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            _skipHint.Position = new Vector2(760, 1010);
            _skipHint.CustomMinimumSize = new Vector2(400, 24);
            _skipHint.ThemeTypeVariation = UIPalette.SmallLabelVariation;
            _skipHint.AddThemeColorOverride("font_color", UIPalette.SlateDim);
            shade.AddChild(_skipHint);
        }
    }
}
