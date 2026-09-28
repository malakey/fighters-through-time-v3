using Godot;

namespace FTT.Combat {

    /// <summary>Package 12 W5 (G12): the three selectable player-slot palettes.</summary>
    public enum PlayerSlotPalette {
        /// <summary>P1 cyan #00f0ff / P2 red #ff3366 — the F24 ownership colours.</summary>
        Default = 0,
        /// <summary>P1 blue / P2 orange.</summary>
        BlueOrange = 1,
        /// <summary>White / black edge pairs.</summary>
        HighContrast = 2
    }

    /// <summary>
    /// Package 12 W5 (G12 Color Independence). Every player-slot signal carries a
    /// <b>shape</b> as well as a colour — P1 ▲ / P2 ● — and the colour half comes
    /// from a local palette the player picks in Settings → Gameplay.
    ///
    /// <para><b>Presentation only.</b> The palette recolors the ownership outline
    /// and HUD slot colours on this machine. Nothing in <c>scripts/FighterSim/</c>
    /// reads it, it never enters a snapshot or a hash, and a remote peer (or a
    /// replay) sees whatever palette its own player chose — exactly like Reduced
    /// Temporal Effects.</para>
    ///
    /// <para><see cref="GlowPalette.SlotColor(int)"/> stays the Default palette's
    /// canonical table; this class selects between palettes and never holds a
    /// second copy of the Default colours.</para>
    /// </summary>
    public static class PlayerSlotPalettes {

        /// <summary>P1's slot shape glyph (an upward triangle).</summary>
        public const string PlayerOneGlyph = "▲";
        /// <summary>P2's slot shape glyph (a filled circle).</summary>
        public const string PlayerTwoGlyph = "●";

        private static readonly Color BlueOrangeOne = new(0.227f, 0.553f, 1f);   // #3a8dff
        private static readonly Color BlueOrangeTwo = new(1f, 0.604f, 0.122f);   // #ff9a1f
        private static readonly Color HighContrastLight = new(1f, 1f, 1f);
        private static readonly Color HighContrastDark = new(0.067f, 0.067f, 0.067f);

        /// <summary>Palette count; the Settings row lists exactly these.</summary>
        public const int Count = 3;

        /// <summary>Localized label key for a palette.</summary>
        public static string LabelKey(PlayerSlotPalette palette) => palette switch {
            PlayerSlotPalette.BlueOrange => "settings_slot_palette_blue_orange",
            PlayerSlotPalette.HighContrast => "settings_slot_palette_high_contrast",
            _ => "settings_slot_palette_default"
        };

        /// <summary>Shape glyph for a player slot (P1 ▲, P2 ●).</summary>
        public static string ShapeGlyph(int playerIndex) => playerIndex == 1 ? PlayerTwoGlyph : PlayerOneGlyph;

        /// <summary>Clamps a stored value onto a defined palette (Default when unknown).</summary>
        public static PlayerSlotPalette Sanitize(int stored) =>
            stored >= 0 && stored < Count ? (PlayerSlotPalette)stored : PlayerSlotPalette.Default;

        /// <summary>
        /// The palette the local player chose, read live from the global save so a
        /// change in Settings reaches the next surface that asks. Default when there
        /// is no save (menus in tests, first boot).
        /// </summary>
        public static PlayerSlotPalette Active =>
            Sanitize(FTT.Core.SaveManager.Instance?.GlobalData?.PlayerSlotPalette ?? 0);

        /// <summary>The slot colour for a palette. P3/P4 stay on the default table.</summary>
        public static Color SlotColor(PlayerSlotPalette palette, int playerIndex) {
            if (playerIndex is not (0 or 1)) return GlowPalette.SlotColor(playerIndex);
            return palette switch {
                PlayerSlotPalette.BlueOrange => playerIndex == 0 ? BlueOrangeOne : BlueOrangeTwo,
                PlayerSlotPalette.HighContrast => playerIndex == 0 ? HighContrastLight : HighContrastDark,
                _ => GlowPalette.SlotColor(playerIndex)
            };
        }

        /// <summary>
        /// The contrasting edge a slot-coloured glyph or name sits on, so a
        /// High-Contrast P2 (black) still reads on a dark HUD panel.
        /// </summary>
        public static Color SlotEdgeColor(PlayerSlotPalette palette, int playerIndex) =>
            palette == PlayerSlotPalette.HighContrast
                ? (playerIndex == 1 ? HighContrastLight : HighContrastDark)
                : new Color(0.02f, 0.03f, 0.08f);

        /// <summary><see cref="SlotColor(PlayerSlotPalette,int)"/> for the active palette.</summary>
        public static Color ActiveSlotColor(int playerIndex) => SlotColor(Active, playerIndex);

        /// <summary><see cref="SlotEdgeColor(PlayerSlotPalette,int)"/> for the active palette.</summary>
        public static Color ActiveSlotEdgeColor(int playerIndex) => SlotEdgeColor(Active, playerIndex);
    }
}
