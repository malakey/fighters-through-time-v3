using Godot;

namespace FTT.UI {

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
    /// </summary>
    public static class NotYetFlash {

        /// <summary>Seconds the tint and the frame take to settle (provisional, PROVISIONAL-HIT-CONFIRM-RULES (6)).</summary>
        public const float Seconds = 0.25f;

        /// <summary>The frame's scale when it appears; it contracts to 1×.</summary>
        public const float StartScale = 1.35f;

        /// <summary>The frame's border width in pixels (the theme's border width).</summary>
        public const int BorderPixels = UIPalette.BorderWidth;

        /// <summary>Node name of the frame child, created once per host and reused.</summary>
        public const string FrameName = "NotYetFrame";

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
        public static void Play(Control frameHost, CanvasItem tintTarget) {
            if (frameHost == null || tintTarget == null) return;
            if (!GodotObject.IsInstanceValid(frameHost) || !GodotObject.IsInstanceValid(tintTarget)) return;
            if (!frameHost.IsInsideTree() || !tintTarget.IsInsideTree()) return;

            KillRunning(tintTarget, TintTweenMeta);
            tintTarget.SelfModulate = Tint;
            Tween tint = tintTarget.CreateTween();
            tint.TweenProperty(tintTarget, "self_modulate", Colors.White, Seconds);
            tintTarget.SetMeta(TintTweenMeta, tint);

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

        private static void HideFrame(Panel frame) {
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
