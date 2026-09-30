using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Package 13 W4 (S19): a placeholder extraction beam taking a figure — Level 5's
    /// "Captain Smith is taken as the Tidal Overseer falls".
    ///
    /// <para>The beat reuses Level 0's two-colour grammar (design §2, "The Visual
    /// Grammar of Resonance"): a <b>jagged cold column</b> of offset shards in the
    /// Severed's synthetic light locks onto the figure; a <b>faint gold flicker</b>
    /// rises in him and is <b>smothered grey</b> before it can catch; the column
    /// closes around him like a cradle and he is gone. Every pigment comes from
    /// <see cref="FTT.UI.UIPalette"/>'s grammar block or
    /// <see cref="FTT.Combat.GlowPalette.SuppressionColor"/> — the same grey the
    /// Siphon Snare smother paints on the hero — never a local literal for the beam
    /// or the gold.</para>
    ///
    /// <para>Placeholder <c>ColorRect</c> geometry at the campaign's placeholder
    /// bar; the illustrated still is Package 10. The node runs at
    /// <see cref="Node.ProcessModeEnum.Always"/> so the beat plays out underneath
    /// the post-boss dialogue that pauses the tree, exactly like Level 0's
    /// fracture beat.</para>
    ///
    /// <para>State is a single latch: <see cref="IsTaken"/> flips the moment
    /// <see cref="Play"/> is called (tests read it with no frames stepped), and
    /// <see cref="MarkTaken"/> reconstructs the aftermath with no beat for a
    /// pre-seal reload — the captain is never taken twice and never comes back.</para>
    /// </summary>
    public partial class ExtractionBeamPresentation : Node2D {

        /// <summary>Seconds from the column biting to the cradle closing.</summary>
        public const float BeatSeconds = 1.6f;

        /// <summary>The figure's body colour (a placeholder uniform navy).</summary>
        public static readonly Color FigureColor = new(0.10f, 0.13f, 0.24f);

        /// <summary>The figure's cap / trim.</summary>
        public static readonly Color FigureTrimColor = new(0.92f, 0.92f, 0.88f);

        public Node2D Figure { get; private set; }
        public Node2D ColdColumn { get; private set; }
        public ColorRect GoldFlicker { get; private set; }

        /// <summary>True once the figure has been taken (by the beat or a reload).</summary>
        public bool IsTaken { get; private set; }

        /// <summary>True once <see cref="Play"/> has run the beat.</summary>
        public bool BeatPlayed { get; private set; }

        public override void _Ready() {
            ProcessMode = ProcessModeEnum.Always;
            if (Figure == null) Build();
        }

        private void Build() {
            Figure = new Node2D { Name = "Figure" };
            AddChild(Figure);
            Figure.AddChild(new ColorRect {
                Name = "Body",
                Size = new Vector2(26f, 62f),
                Position = new Vector2(-13f, -62f),
                Color = FigureColor,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            Figure.AddChild(new ColorRect {
                Name = "Cap",
                Size = new Vector2(30f, 8f),
                Position = new Vector2(-15f, -70f),
                Color = FigureTrimColor,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            GoldFlicker = new ColorRect {
                Name = "GoldFlicker",
                Size = new Vector2(40f, 40f),
                Position = new Vector2(-20f, -54f),
                Color = FTT.UI.UIPalette.ResonanceGoldBright,
                Modulate = new Color(1f, 1f, 1f, 0f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(GoldFlicker);

            // Jagged, offset shards — never a clean column (a Warden portal is the
            // clean shape; a Severed beam is always torn).
            ColdColumn = new Node2D {
                Name = "ColdColumn",
                Modulate = new Color(1f, 1f, 1f, 0f)
            };
            AddChild(ColdColumn);
            float[] offsets = { -22f, -7f, 8f, 20f };
            float[] heights = { 360f, 420f, 390f, 340f };
            float[] widths = { 12f, 18f, 11f, 14f };
            for (int index = 0; index < offsets.Length; index++) {
                ColdColumn.AddChild(new ColorRect {
                    Name = $"ColdShard{index}",
                    Size = new Vector2(widths[index], heights[index]),
                    Position = new Vector2(offsets[index], -heights[index]),
                    Color = FTT.UI.UIPalette.UnboundColdDischarge,
                    RotationDegrees = index % 2 == 0 ? -3f : 4f,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                });
            }
        }

        /// <summary>
        /// Runs the taking. Idempotent: a second call (or a call after
        /// <see cref="MarkTaken"/>) does nothing. Returns true when the beat started.
        /// </summary>
        public bool Play() {
            if (IsTaken) return false;
            if (Figure == null) Build();
            IsTaken = true;
            BeatPlayed = true;
            if (!IsInsideTree()) {
                ShowAftermath();
                return true;
            }

            Tween beat = CreateTween();
            // 1. The cold column locks on.
            beat.TweenProperty(ColdColumn, "modulate:a", 1f, 0.35);
            // 2. A faint gold flicker rises in him...
            beat.TweenProperty(GoldFlicker, "modulate:a", 0.85f, 0.2);
            // 3. ...and is smothered grey before it can catch.
            beat.TweenProperty(GoldFlicker, "color", FTT.Combat.GlowPalette.SuppressionColor, 0.3);
            // 4. The column closes around him like a cradle, and he is gone.
            beat.TweenProperty(ColdColumn, "scale:x", 0.15f, 0.4)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
            beat.Parallel().TweenProperty(Figure, "modulate:a", 0f, 0.4);
            beat.Parallel().TweenProperty(GoldFlicker, "modulate:a", 0f, 0.4);
            beat.TweenProperty(ColdColumn, "modulate:a", 0f, 0.35);
            return true;
        }

        /// <summary>The aftermath with no beat: a pre-seal reload lands after the taking.</summary>
        public void MarkTaken() {
            if (Figure == null) Build();
            IsTaken = true;
            ShowAftermath();
        }

        private void ShowAftermath() {
            Figure.Visible = false;
            GoldFlicker.Visible = false;
            ColdColumn.Visible = false;
        }
    }
}
