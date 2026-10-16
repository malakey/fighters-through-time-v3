using Godot;

namespace FTT.Combat {

    /// <summary>
    /// CM-ULT (2026-10-16, decision 2.1; closes <c>DEFER-ULT-TELEGRAPH</c>): the
    /// A02 Ultimate activation strike's wind-up telegraph, shared by the Fighter
    /// driver and the Level 13 Mirror clone so both modes draw exactly the same
    /// thing. For the whole wind-up (and only the wind-up) the caster shows the
    /// two-channel Unblockable promise the enemy telegraphs already use:
    /// <list type="bullet">
    /// <item>colour: a steady Unblockable-red tint on the glow arbiter's dedicated
    /// telegraph flag (<see cref="GlowPresentationController.SetUltimateTelegraph"/>);</item>
    /// <item>shape: the Unblockable X (<see cref="TelegraphGlyph"/>), floating
    /// above the caster at <see cref="GlyphOffset"/>, clear of the Fighter slot
    /// badge at −150 px.</item>
    /// </list>
    /// Neither pulses, so the telegraph is C01a-safe (Reduced Temporal Effects
    /// changes nothing). Presentation only: callers derive it from state and
    /// nothing here reads or writes simulation state.
    ///
    /// <para><b>Provisional look</b> (<c>PROVISIONAL-ULT-TELEGRAPH-LOOK</c>): the
    /// offset, the radius (<see cref="TelegraphGlyph.RadiusPixels"/>, 14 px) and
    /// the steady tint are agent choices under the user's "red flash + the
    /// Unblockable X" ruling.</para>
    /// </summary>
    public static class UltimateActivationTelegraph {

        /// <summary>Name of the glyph child under the caster's body.</summary>
        public const string GlyphNodeName = "UltimateTelegraphGlyph";

        /// <summary>
        /// Glyph position relative to the caster's body origin (its feet), in
        /// pixels: above the head, below the Fighter slot badge at −150.
        /// </summary>
        public static readonly Vector2 GlyphOffset = new(0f, -120f);

        /// <summary>The telegraph colour: the Unblockable class red.</summary>
        public static Color Color => TelegraphGlyph.UnblockableColor;

        /// <summary>The caster's existing telegraph glyph, or null if none was created yet.</summary>
        public static TelegraphGlyph FindGlyph(Node owner) =>
            owner == null || !GodotObject.IsInstanceValid(owner)
                ? null
                : owner.GetNodeOrNull<TelegraphGlyph>(GlyphNodeName);

        /// <summary>Finds or creates (hidden) the caster's telegraph glyph.</summary>
        public static TelegraphGlyph EnsureGlyph(Node owner) {
            if (owner == null || !GodotObject.IsInstanceValid(owner)) return null;
            TelegraphGlyph glyph = FindGlyph(owner);
            if (glyph != null) return glyph;
            glyph = new TelegraphGlyph {
                Name = GlyphNodeName,
                Position = GlyphOffset,
                Visible = false
            };
            owner.AddChild(glyph);
            return glyph;
        }

        /// <summary>
        /// Shows (<paramref name="windup"/> true) or hides the telegraph on one
        /// caster. Idempotent and null-tolerant (a driver without arbiters, a
        /// caster that never cast): a hide never creates the glyph. Call it with
        /// the state's wind-up predicate every frame, or at every phase edge.
        /// </summary>
        public static void Apply(Node owner, GlowPresentationController glow, bool windup) {
            if (glow != null && GodotObject.IsInstanceValid(glow)) glow.SetUltimateTelegraph(windup);
            TelegraphGlyph glyph = windup ? EnsureGlyph(owner) : FindGlyph(owner);
            if (glyph == null) return;
            if (windup) {
                if (!glyph.Visible || glyph.Shape != TelegraphGlyphShape.Unblockable) {
                    glyph.Present(TelegraphGlyphShape.Unblockable, Color);
                }
            } else if (glyph.Visible) {
                glyph.Visible = false;
            }
        }

        /// <summary>True while <paramref name="owner"/>'s telegraph glyph is shown. Test/presentation read.</summary>
        public static bool IsGlyphShown(Node owner) {
            TelegraphGlyph glyph = FindGlyph(owner);
            return glyph != null && glyph.Visible;
        }
    }
}
