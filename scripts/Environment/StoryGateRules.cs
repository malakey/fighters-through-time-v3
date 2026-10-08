namespace FTT.Environment {

    /// <summary>
    /// P5 (playtest pass 2026-10-04): the one rule for a Story gate's overhead.
    ///
    /// <para>A gate — a shield barrier, a sealed door, a rockfall — has to be
    /// passed by the mechanic that opens it, never jumped (docs/PACKAGE5_CAMPAIGN_PLAN.md,
    /// Level 2: "gates cannot be jumped"). The Story levels have no ceiling: the
    /// camera stops at y = 0 but the hero does not, so a column that ends at the top
    /// of the level is hopped by any double jump launched from a perch within about
    /// 400 px of it (the bots went over Orléans Gate B from the battlement
    /// Extractor). A gate column therefore rises from the gate to
    /// <see cref="ColumnTopY"/>, far above anything the roster can reach from any
    /// standable surface — even on the Moon's 0.36 gravity with a movement ability
    /// on top. It is invisible: the camera never shows y &lt; 0.</para>
    ///
    /// <para><c>tests/ContentValidation/ArenaGeometryContentTests</c> proves every
    /// registered column against the roster's real jump numbers, so this value is
    /// a ceiling with a test behind it rather than a guess.</para>
    /// </summary>
    public static class StoryGateRules {
        /// <summary>Global Y every gate column rises to (Story pixels, y-down).</summary>
        public const float ColumnTopY = -2400f;

        /// <summary>
        /// How far a gate column overhangs the gate below it on each side. A column
        /// narrower than (or offset from) its gate leaves the gate's top exposed as
        /// a standable lip with open sky over it — Orléans Gate B's 48 px barrier is
        /// centred on its x while a wall is anchored at its left edge, so the old
        /// 20 px arch left a 24 px ledge on the barrier's west side. A column is
        /// therefore centred on its gate and this much wider on each side, so its
        /// underside is ceiling, never a ledge.
        /// </summary>
        public const float ColumnMarginPixels = 8f;

        /// <summary>The width of the column over a gate <paramref name="gateWidth"/> wide.</summary>
        public static float ColumnWidth(float gateWidth, float margin = ColumnMarginPixels) =>
            gateWidth + 2f * margin;

        /// <summary>
        /// Group every puzzle- or objective-sealed Story door joins at build time
        /// (<c>StoryLevelControllerBase.BuildDoor</c>, Florence's workshop door).
        /// <c>tests/ContentValidation/ArenaGeometryContentTests</c> enumerates the
        /// group — not a hand list — and fails on any door (or forcefield barrier)
        /// that has no gate column over it, so a future gate cannot be left
        /// jumpable. Level 4's courtyard gate and Level 11's breastwork were (the
        /// first P5 audit was a hand list and missed them).
        /// </summary>
        public const string DoorGroup = "story_gate_door";
    }
}
