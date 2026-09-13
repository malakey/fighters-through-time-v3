using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// Package 11 A8 / F13. The Defy History seal's four-state rule, in both modes.
    ///
    /// <para>The seal sits beside the Ultimate meter at 20 × 20 px and answers one
    /// question: can this fighter still cheat death once? It is <b>derived</b> from
    /// authoritative meter / Defy-used / life / mode state after the complete
    /// gameplay update — never persisted as a separate cosmetic flag, and never
    /// replayed by a load. That is the whole reason this is a pure static resolver
    /// rather than a little state machine that remembers things: a state machine
    /// would need rollback handling, and the correct answer is always recomputable
    /// from four inputs.</para>
    ///
    /// <para><b>Spent is permanent.</b> HUD_CONTRACT: "Spent (visibly broken at any
    /// meter value after the proc — refilling never repairs)". The meter going back
    /// to 100 is exactly the case a naive `meter >= 100 ? Ready : Building` gets
    /// wrong, and it is the one the player would misread as a second free life.</para>
    ///
    /// <para><b>Barred does not erase Spent.</b> Sudden Death disables Defy
    /// regardless of meter (F22), and a dead fighter has no seal to light — but
    /// neither may clear the spent flag underneath, so leaving the phase restores
    /// Spent rather than Ready.</para>
    ///
    /// <para>Shape and fill distinguish the states, not colour alone, and nothing
    /// here pulses or chimes: the contract asks for a quiet indicator.</para>
    /// </summary>
    public static class DefySealModel {

        /// <summary>Meter value at which an unspent Defy becomes available.</summary>
        public const float ReadyMeter = 100f;

        /// <summary>Authored size, in reference pixels (HUD_CONTRACT).</summary>
        public const int SealSize = 20;

        /// <summary>
        /// The state to draw.
        /// </summary>
        /// <param name="meter">Influence meter, 0-100.</param>
        /// <param name="used">Whether the proc has already fired this level/match.</param>
        /// <param name="alive">False for a dead or respawning fighter.</param>
        /// <param name="modeDisabled">
        /// True where the mode forbids Defy outright — F22 Sudden Death is the only
        /// current source.
        /// </param>
        public static DefySealState Resolve(float meter, bool used, bool alive, bool modeDisabled) {
            if (modeDisabled || !alive) return DefySealState.Barred;
            if (used) return DefySealState.Spent;
            return meter >= ReadyMeter ? DefySealState.Ready : DefySealState.Building;
        }

        /// <summary>Localized label key. Reads "Defy: Not Ready / Ready / Spent / Unavailable".</summary>
        public static string LabelKey(DefySealState state) => state switch {
            DefySealState.Ready => "hud_defy_ready",
            DefySealState.Spent => "hud_defy_spent",
            DefySealState.Barred => "hud_defy_unavailable",
            _ => "hud_defy_not_ready"
        };

        /// <summary>
        /// Glyph per state. Distinct shapes, so the seal is readable without
        /// colour: an open ring building, a filled ring ready, a visibly broken
        /// ring spent, a barred ring unavailable.
        /// </summary>
        public static string Glyph(DefySealState state) => state switch {
            DefySealState.Ready => "◉",
            DefySealState.Spent => "◌",
            DefySealState.Barred => "⊘",
            _ => "○"
        };

        /// <summary>
        /// Tint per state. Secondary to the glyph by contract — the shape carries
        /// the meaning — but a ready seal is warm gold and a spent one is dead
        /// slate, which is what makes it readable at a glance.
        /// </summary>
        public static Godot.Color Tint(DefySealState state) => state switch {
            DefySealState.Ready => UIPalette.GoldBright,
            DefySealState.Spent => UIPalette.SlateDim,
            DefySealState.Barred => UIPalette.TextDisabled,
            _ => UIPalette.Slate
        };
    }
}
