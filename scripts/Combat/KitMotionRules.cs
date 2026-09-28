using System;

namespace FTT.Combat {

    /// <summary>
    /// Package 12 W4 — the cross-mode motion rulebook for the kit moves whose
    /// Fighter half cannot read an <c>AbilityData</c> field from inside the
    /// deterministic tick (the loadout reaches the sim, the per-phase frames do
    /// not — see <c>DEFER-SIM-ABILITY-HITSTUN</c>). Plain constants and pure
    /// helpers, no Godot types, so both Story and <c>scripts/FighterSim/</c>
    /// consume exactly one copy, the way they consume <see cref="BasicComboRules"/>.
    ///
    /// <para>Where the same frame count is also authored on the ability's
    /// <c>.tres</c> (Story's phase timer reads the resource), the resource is
    /// pinned EQUAL to the constant by <c>KitAlignmentTests</c>, so the two can
    /// never drift apart silently.</para>
    ///
    /// <para>Distances are world units; Story converts at
    /// <see cref="StoryPixelsPerUnit"/> — the convention
    /// <c>FighterLoadoutFactory.WorldDistance</c> already uses to project
    /// <c>MovementAbilityData.DistanceMoved</c> into the sim.</para>
    /// </summary>
    public static class KitMotionRules {

        /// <summary>The Story↔sim distance scale the loadout projection uses.</summary>
        public const float StoryPixelsPerUnit = 60f;

        // --- Tesla: Lightning Blink (design §5, 2026-09-26) ------------------

        /// <summary>Spark gather before the translation.</summary>
        public const int LightningBlinkStartupFrames = 6;
        /// <summary>
        /// The 0.2 s translation. The ONLY window in which projectiles pass
        /// through Tesla, in both modes. Mirrors <c>MovementDuration = 0.2</c>
        /// on <c>tesla/movement.tres</c>, which is what feeds the sim loadout.
        /// </summary>
        public const int LightningBlinkTravelFrames = 12;
        /// <summary>Recovery after the translation; 6 + 12 + 10 = 28 frames.</summary>
        public const int LightningBlinkRecoveryFrames = 10;
        /// <summary>The design's hard cap on the whole action (1 s).</summary>
        public const int LightningBlinkActionCapFrames = 60;
        /// <summary>Base translation distance; <c>DistanceMoved = 180</c> px on the resource.</summary>
        public const float LightningBlinkDistanceUnits = 3.0f;
        /// <summary>Story-only Long Blink traversal node: +0.5 units (3.5 total).</summary>
        public const float LongBlinkBonusUnits = 0.5f;

        public static int LightningBlinkTotalFrames =>
            LightningBlinkStartupFrames + LightningBlinkTravelFrames + LightningBlinkRecoveryFrames;

        // --- Pocahontas: Spirit Strike (design §5, 2026-09-26) ----------------

        /// <summary>
        /// The eagle carries Pocahontas up-forward at 45° for this many frames;
        /// equal to <c>ActiveFrames</c> on <c>pocahontas/special_1.tres</c>.
        /// </summary>
        public const int SpiritStrikeCarryFrames = 15;
        /// <summary>Wind-up before the carry; equal to the resource's <c>StartupFrames</c>.</summary>
        public const int SpiritStrikeStartupFrames = 12;
        /// <summary>The forced dash length along the 45° diagonal ("about 3 units").</summary>
        public const float SpiritStrikeCarryUnits = 3.0f;

        /// <summary>
        /// Where the eagle's hitbox starts, relative to Pocahontas at the first
        /// carry frame: ahead of her and above her head (units, +up).
        /// PROVISIONAL placeholder geometry — the design gives the two vectors,
        /// not the eagle's box.
        /// </summary>
        public const float SpiritEagleStartForwardUnits = 1.5f;
        public const float SpiritEagleStartUpUnits = 1.0f;
        /// <summary>Half extent of the eagle's square hitbox (units).</summary>
        public const float SpiritEagleHalfExtentUnits = 0.75f;

        /// <summary>Per-axis carry per frame: (3 / √2) / 15 units.</summary>
        public static double SpiritStrikeAxisStepUnits =>
            SpiritStrikeCarryUnits / Math.Sqrt(2.0) / SpiritStrikeCarryFrames;

        /// <summary>
        /// The eagle's offset from Pocahontas on carry frame
        /// <paramref name="carryFrame"/> (0-based), in units with +up. She rises
        /// up-forward while the eagle dives down-forward at the same speed, so
        /// relative to her the eagle keeps its forward lead and drops at twice
        /// the per-axis step — "the two vectors are separate".
        /// </summary>
        public static (double Forward, double Up) SpiritEagleOffsetUnits(int carryFrame) =>
            (SpiritEagleStartForwardUnits,
             SpiritEagleStartUpUnits - 2.0 * SpiritStrikeAxisStepUnits * Math.Max(0, carryFrame));
    }
}
