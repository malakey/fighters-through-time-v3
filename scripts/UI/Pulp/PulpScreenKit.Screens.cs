using FTT.UI.Motion;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    // TG-SCREENS' screen dressing, folded into the screen kit by TG-CLOSE (it was the static GlassScreenDressing): the two
    // screen types of UI_SPEC_GLASS 9.1, built from the kit's parts for the screens no mockup draws (results, loading,
    // credits, Game Over, the Resonance grid, the drills, the play options, the network select, the restart choice).
    //  - Full-screen menus carry their art (AddMenuArt: the sub-screen art, or the bg0 field of loading and credits), the
    //    dir-a screen streak (AddScreenStreak), a T03 heading with flourishes top-centre at y 40 (AddScreenHeading) and the
    //    prompt bar bottom-right (AddPromptBar).
    //  - Overlays over gameplay carry the build dim (UIPalette.PauseDim, Game Over the stronger UIPalette.GameOverDim), one
    //    centred glass slab, no streak (AC19, plan 0.4 P3) and the prompt bar bottom-centre at 34 (AddOverlayPromptBar); the
    //    slab clears the bar through MenuSafeAreaFit.AttachClearOfPromptBar.
    // The two parts this dressing lacked are kit widgets now: TitleFlourishes (flourishes placed around an existing centred
    // title) and ResultRow (a caption left, a tabular value right). Nothing here sets a font size.
    public static partial class PulpScreenKit {

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
            return AddFlourishHeading(holder, textKey);
        }

        /// <summary>
        /// An overlay's prompt bar (9.1: bottom-centre, 34 px from the bottom): the shared <see cref="AddPromptBar"/>
        /// (so it is the same <see cref="PulpPromptBar"/> named <see cref="PromptBarName"/>) centred.
        /// </summary>
        public static PulpPromptBar AddOverlayPromptBar(Control host, params (string Action, string CaptionKey)[] prompts) {
            PulpPromptBar bar = AddPromptBar(host, prompts);
            if (bar == null) return null;
            bar.Alignment = BoxContainer.AlignmentMode.Center;
            bar.AnchorLeft = 0.5f;
            bar.AnchorRight = 0.5f;
            bar.GrowHorizontal = Control.GrowDirection.Both;
            bar.OffsetLeft = 0f;
            bar.OffsetRight = 0f;
            bar.OffsetBottom = -PromptBottomMargin;
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
    }
}
