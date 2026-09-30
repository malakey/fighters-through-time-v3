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

        // --- Package 13 W7a: Einstein, Leonardo, Tesla, Shakespeare ----------
        // Every value below is provisional under A07 (ABILITY_DATA.md). Where a
        // .tres also authors the number (Story reads the resource), a content
        // test pins the resource EQUAL to the constant.

        /// <summary>E04: Relativity Warp's startup before the instant relocation (the visible ghost).</summary>
        public const int RelativityWarpStartupFrames = 10;
        /// <summary>E04: maximum fold distance; <c>DistanceMoved = 240</c> px on the resource.</summary>
        public const float RelativityWarpDistanceUnits = 4.0f;
        /// <summary>The post-fold float window (unchanged from the pre-E04 warp).</summary>
        public const int RelativityWarpFloatFrames = 60;

        /// <summary>A08: Prospero's Flight gust length (frames); then a normal fall.</summary>
        public const int ProsperoGustFrames = 20;
        /// <summary>A08: forward travel over the gust ("about 4 units"); <c>DistanceMoved = 240</c> px.</summary>
        public const float ProsperoGustForwardUnits = 4.0f;
        /// <summary>A08: rise over the gust ("2.5 up").</summary>
        public const float ProsperoGustRiseUnits = 2.5f;
        /// <summary>Story-only Midsummer Gust: 20% more gust distance.</summary>
        public const float MidsummerGustDistanceMultiplier = 1.2f;

        /// <summary>E01: Rift Collapse pulls every caught opponent to the rift centre over this many frames.</summary>
        public const int RiftCollapsePullFrames = 6;
        /// <summary>E01: the upward launch at the end of the pull (units/s, provisional).</summary>
        public const float RiftCollapseLaunchUnits = 6.0f;
        /// <summary>E03: Relativity Rift thrown up to this far ahead of Einstein.</summary>
        public const float RelativityRiftThrowUnits = 5.0f;
        /// <summary>E03: rift radius.</summary>
        public const float RelativityRiftRadiusUnits = 1.5f;
        /// <summary>E03: Time Dilation lingers this long after leaving the rift (re-entry refreshes).</summary>
        public const int RelativityRiftLingerFrames = 30;

        /// <summary>S01/S02: The Tempest's windbox radius.</summary>
        public const float TempestRadiusUnits = 2.0f;
        /// <summary>S02: opponents pushed about 3 units outward over <see cref="TempestPushFrames"/>.</summary>
        public const float TempestPushUnits = 3.0f;
        public const int TempestPushFrames = 12;
        /// <summary>S02: Shakespeare is lifted about 2.5 units.</summary>
        public const float TempestLiftUnits = 2.5f;

        /// <summary>L03: turret bolts fly straight at 12 units/s.</summary>
        public const float TurretBoltSpeedUnits = 12.0f;
        /// <summary>T02: coil arcs reach 4 units.</summary>
        public const float TeslaCoilArcRangeUnits = 4.0f;
        /// <summary>T01: an eligible coil is one of that Tesla's own active coils within 8 units of the target.</summary>
        public const float LorentzChainCoilRangeUnits = 8.0f;
        /// <summary>T01: each eligible coil's chain arc damage (max 16 with both coils).</summary>
        public const int LorentzChainArcDamage = 8;
        /// <summary>T02: Lorentz Pulse radius.</summary>
        public const float LorentzPulseRadiusUnits = 2.5f;
    }
}
