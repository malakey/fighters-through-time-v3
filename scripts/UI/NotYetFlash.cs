using Godot;

namespace FTT.UI {

    /// <summary>The not-yet flash's shape: the 2 px frame over a slot, or (glass) the 2 px circle around a lens or the frame leaning with a tube.</summary>
    public enum NotYetShape { Frame, Circle, Tube }

    /// <summary>
    /// D10 (user direction 2026-10-11, ledger <c>USER-NOT-YET-FLASH</c>): the
    /// "not yet" flash a HUD plays on a Special 1, Special 2 or Ultimate slot when
    /// the ability lockout alone refused a ready press. The one canonical place
    /// for its look, shared by <see cref="StoryHUD"/> and <see cref="FighterHUD"/>.
    ///
    /// <para><b>Two channels.</b> HUD_CONTRACT says colour alone never carries
    /// meaning, so the flash is a tint <em>and</em> a shape: the tint target's
    /// <c>SelfModulate</c> jumps to <see cref="UIPalette.BossRed"/> and settles
    /// back to white, while a hollow 2 px frame (<see cref="FrameName"/>, a
    /// <c>Panel</c> child of the frame host) appears at
    /// <see cref="StartScale"/>× and contracts to 1× as it fades. The tint uses
    /// <c>SelfModulate</c> and never <c>Modulate</c>, because the Fighter HUD
    /// owns <c>Modulate</c> on the same labels (the cooling alpha and the M25
    /// ready flash), so a refund that readies a slot on the same tick as a
    /// refusal runs two independent tweens on two properties.</para>
    ///
    /// <para><b>Lifetime.</b> One non-repeating motion and fade, so it stands
    /// under Reduced Temporal Effects (C01a allows a smooth fade). A repeat press
    /// kills both running tweens (held in node meta) and restarts. No text, no
    /// sound (placeholder audio is silence), no state anywhere else: presentation
    /// only, never read back.</para>
    ///
    /// <para><b>Glass (TG-WIDGETS step 2).</b> The shape has a second form,
    /// <see cref="NotYetShape.Circle"/>: a contracting 2 px <c>danger</c> circle
    /// around a lens (UI_SPEC_GLASS 3.4.2), beside the frame, and (TG-CLOSE) a
    /// third, <see cref="NotYetShape.Tube"/>: a contracting 2 px <c>danger</c>
    /// frame that leans with a glass tube (its run, mirrored with a P2 tube). A
    /// <see cref="ProgressBar"/> tint target that wears a glass tube
    /// (<c>PulpBarDecor</c>) is tinted as before: the bar draws nothing of its
    /// own and the tube follows its <c>SelfModulate</c>, so the tint shows on
    /// the tube.</para>
    /// </summary>
    public static partial class NotYetFlash {

        /// <summary>Seconds the tint and the frame take to settle (provisional, PROVISIONAL-HIT-CONFIRM-RULES (6)).</summary>
        public const float Seconds = 0.25f;

        /// <summary>The frame's scale when it appears; it contracts to 1×.</summary>
        public const float StartScale = 1.35f;

        /// <summary>The frame's border width in pixels (the theme's border width).</summary>
        public const int BorderPixels = UIPalette.BorderWidth;

        /// <summary>Node name of the frame child, created once per host and reused.</summary>
        public const string FrameName = "NotYetFrame";

        /// <summary>Node name of the circle child (<see cref="NotYetShape.Circle"/>), created once per host and reused.</summary>
        public const string CircleName = "NotYetCircle";

        /// <summary>Node name of the tube frame child (<see cref="NotYetShape.Tube"/>), created once per host and reused.</summary>
        public const string TubeName = "NotYetTube";

        /// <summary>The circle's stroke width, and how far its centre line sits outside the lens's own edge.</summary>
        public const float CircleStroke = 2f, CircleOutset = 2.5f;

        private const string TintTweenMeta = "not_yet_tint_tween";
        private const string FrameTweenMeta = "not_yet_frame_tween";

        /// <summary>The flash's tint, also the frame's border colour.</summary>
        public static Color Tint => UIPalette.BossRed;

        /// <summary>
        /// Plays the flash: <paramref name="tintTarget"/>'s <c>SelfModulate</c>
        /// and a contracting frame over <paramref name="frameHost"/>. Every host
        /// the HUDs pass (a <c>Label</c>, a plain <c>Control</c>, a
        /// <c>ProgressBar</c>) is a non-Container, so nothing resets the frame's
        /// scale. A host outside the tree plays nothing.
        /// </summary>
        public static void Play(Control frameHost, CanvasItem tintTarget) => Play(frameHost, tintTarget, NotYetShape.Frame);

        /// <summary>
        /// Plays the flash with the given shape: the frame over <paramref name="frameHost"/>, or a circle around it
        /// (<paramref name="frameHost"/> is then the lens: the circle sits <see cref="CircleOutset"/> outside its edge).
        /// </summary>
        public static void Play(Control frameHost, CanvasItem tintTarget, NotYetShape shape) {
            if (frameHost == null || tintTarget == null) return;
            if (!GodotObject.IsInstanceValid(frameHost) || !GodotObject.IsInstanceValid(tintTarget)) return;
            if (!frameHost.IsInsideTree() || !tintTarget.IsInsideTree()) return;

            KillRunning(tintTarget, TintTweenMeta);
            tintTarget.SelfModulate = Tint;
            Tween tint = tintTarget.CreateTween();
            tint.TweenProperty(tintTarget, "self_modulate", Colors.White, Seconds);
            tintTarget.SetMeta(TintTweenMeta, tint);

            if (shape == NotYetShape.Circle) {
                PlayCircle(frameHost);
                return;
            }
            if (shape == NotYetShape.Tube) {
                PlayTube(frameHost);
                return;
            }
            Panel frame = EnsureFrame(frameHost);
            KillRunning(frame, FrameTweenMeta);
            frame.PivotOffset = frameHost.Size / 2f;
            frame.Scale = new Vector2(StartScale, StartScale);
            frame.Modulate = Colors.White;
            frame.Visible = true;
            Tween contract = frame.CreateTween();
            contract.SetParallel(true);
            contract.TweenProperty(frame, "scale", Vector2.One, Seconds);
            contract.TweenProperty(frame, "modulate:a", 0f, Seconds);
            contract.Chain().TweenCallback(Callable.From(() => HideFrame(frame)));
            frame.SetMeta(FrameTweenMeta, contract);
        }

        /// <summary>The host's frame child, or null before the first flash. Test seam.</summary>
        public static Panel FrameOf(Control frameHost) =>
            frameHost?.GetNodeOrNull<Panel>(FrameName);

        /// <summary>The host's tube frame child, or null before the first tube flash. Test seam.</summary>
        public static Control TubeOf(Control frameHost) => frameHost?.GetNodeOrNull<Control>(TubeName);

        /// <summary>True when the host's tube frame leans "\" (a mirrored tube). Test seam.</summary>
        public static bool TubeLeansMirrored(Control frameHost) => TubeOf(frameHost) is FlashTube tube && tube.Mirrored;

        /// <summary>The host's circle child, or null before the first circle flash. Test seam.</summary>
        public static Control CircleOf(Control frameHost) =>
            frameHost?.GetNodeOrNull<Control>(CircleName);

        private static void PlayCircle(Control host) {
            Control circle = CircleOf(host);
            if (circle == null) {
                circle = new FlashCircle { Name = CircleName, MouseFilter = Control.MouseFilterEnum.Ignore, FocusMode = Control.FocusModeEnum.None, Visible = false };
                host.AddChild(circle);
                circle.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            }
            KillRunning(circle, FrameTweenMeta);
            circle.PivotOffset = host.Size / 2f;
            circle.Scale = new Vector2(StartScale, StartScale);
            circle.Modulate = Colors.White;
            circle.Visible = true;
            circle.QueueRedraw();
            Tween contract = circle.CreateTween();
            contract.SetParallel(true);
            contract.TweenProperty(circle, "scale", Vector2.One, Seconds);
            contract.TweenProperty(circle, "modulate:a", 0f, Seconds);
            contract.Chain().TweenCallback(Callable.From(() => HideFrame(circle)));
            circle.SetMeta(FrameTweenMeta, contract);
        }

        // TG-CLOSE (TG-HUDF kit gap 4): a glass tube's refusal, a 2 px danger frame leaning with the tube (its run, mirrored with
        // a P2 tube), just outside it, contracting and fading like the circle.
        private static void PlayTube(Control host) {
            if (TubeOf(host) is not FlashTube tube) {
                tube = new FlashTube { Name = TubeName, MouseFilter = Control.MouseFilterEnum.Ignore, FocusMode = Control.FocusModeEnum.None, Visible = false };
                host.AddChild(tube);
                tube.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            }
            var decor = host is ProgressBar bar ? FTT.UI.Pulp.PulpBarDecor.Of(bar) : null;
            tube.Run = decor?.Run ?? FTT.UI.Pulp.PulpBarDecor.TubeRun;
            tube.Mirrored = decor?.Mirrored ?? false;
            KillRunning(tube, FrameTweenMeta);
            tube.PivotOffset = host.Size / 2f;
            tube.Scale = new Vector2(StartScale, StartScale);
            tube.Modulate = Colors.White;
            tube.Visible = true;
            tube.QueueRedraw();
            Tween contract = tube.CreateTween();
            contract.SetParallel(true);
            contract.TweenProperty(tube, "scale", Vector2.One, Seconds);
            contract.TweenProperty(tube, "modulate:a", 0f, Seconds);
            contract.Chain().TweenCallback(Callable.From(() => HideFrame(tube)));
            tube.SetMeta(FrameTweenMeta, contract);
        }

        // The tube frame's outline, cached per size, run and lean; the draw allocates nothing.
        private sealed partial class FlashTube : Control {
            private readonly Vector2[] _corners = new Vector2[4];
            private readonly Vector2[] _outline = new Vector2[5];
            private Vector2 _builtSize = new(-1f, -1f);
            private float _builtRun = -1f;
            private bool _builtMirrored;
            public float Run { get; set; } = FTT.UI.Pulp.PulpBarDecor.TubeRun;
            public bool Mirrored { get; set; }

            public override void _Draw() {
                if (Size != _builtSize || !Mathf.IsEqualApprox(Run, _builtRun) || Mirrored != _builtMirrored) {
                    _builtSize = Size;
                    _builtRun = Run;
                    _builtMirrored = Mirrored;
                    // Grown CircleOutset round the tube, its run scaled with the height so the slanted ends stay parallel.
                    Rect2 box = new Rect2(Vector2.Zero, Size).Grow(CircleOutset);
                    FTT.UI.Pulp.GlassGeometry.Parallelogram(box, Run * box.Size.Y / Mathf.Max(1f, Size.Y), Mirrored, _corners);
                    for (int i = 0; i < 4; i++) _outline[i] = _corners[i];
                    _outline[4] = _corners[0];
                }
                DrawPolyline(_outline, UIPalette.Danger, CircleStroke, true);
            }
        }

        // A 2 px danger circle just outside the host's own circle (a lens is as wide as its control).
        private sealed partial class FlashCircle : Control {
            public override void _Draw() {
                float radius = Mathf.Min(Size.X, Size.Y) / 2f + CircleOutset;
                DrawArc(Size / 2f, radius, 0f, Mathf.Tau, 64, UIPalette.Danger, CircleStroke, true);
            }
        }

        private static Panel EnsureFrame(Control frameHost) {
            Panel frame = FrameOf(frameHost);
            if (frame != null) return frame;
            var style = new StyleBoxFlat {
                DrawCenter = false,
                BorderColor = Tint
            };
            style.SetBorderWidthAll(BorderPixels);
            frame = new Panel {
                Name = FrameName,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                FocusMode = Control.FocusModeEnum.None,
                Visible = false
            };
            frame.AddThemeStyleboxOverride("panel", style);
            frameHost.AddChild(frame);
            frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return frame;
        }

        private static void HideFrame(Control frame) {
            if (GodotObject.IsInstanceValid(frame)) frame.Visible = false;
        }

        private static void KillRunning(GodotObject owner, string key) {
            if (!owner.HasMeta(key)) return;
            if (owner.GetMeta(key).AsGodotObject() is Tween running
                && GodotObject.IsInstanceValid(running)
                && running.IsValid()) {
                running.Kill();
            }
            owner.RemoveMeta(key);
        }
    }
}
