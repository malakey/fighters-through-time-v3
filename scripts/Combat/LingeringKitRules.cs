using System;

namespace FTT.Combat {

    /// <summary>The three events that pay a lingering-kit Momentum bonus (plan 1.2).</summary>
    public enum LingeringMomentumEvent {
        /// <summary>Lorentz discharge, Rift collapse, Vortex final launch: once per detonation.</summary>
        Detonation = 0,
        /// <summary>A construct's synced strike: once per strike.</summary>
        SyncedStrike = 1,
        /// <summary>Golden Ratio's hold: once per cast.</summary>
        HoldCast = 2
    }

    /// <summary>
    /// The one canonical rulebook of the 2026-10-31 lingering-ability kit pass
    /// (constructs with charges and synced strikes, the Lorentz discharge, the
    /// Rift and Vortex, Golden Ratio, the heavier slow, Venom and its healing
    /// block). Plan: <c>docs/KIT_LINGERING_2026-10-31_PLAN.md</c> sections 1 and 2.
    ///
    /// <para>Pure: ints and doubles only, no Godot types, so the deterministic
    /// simulation and engine-free tests can reference it. <b>Every value is
    /// provisional</b> (decision 19: judged by hand in the hands-on session) and
    /// is recorded under <c>PROVISIONAL-LINGERING-KIT-RULES</c>.</para>
    ///
    /// <para>Distances the simulation reads are stored once as integer
    /// thousandths of a unit (<c>...Milli</c>) and exposed to Story as the exact
    /// <c>double</c> view (<c>...Units</c>). The sim converts with integer
    /// division in <c>FighterLingeringKitRules</c>; no float reaches
    /// <c>scripts/FighterSim/</c>. A per-mode difference is a named pair
    /// (<c>...Story</c> / <c>...Sim</c>). Authored per-ability values (damage of
    /// a Special, a zone's tick) stay in their <c>.tres</c>.</para>
    /// </summary>
    public static class LingeringKitRules {

        // --- Charges (plan 1.4) -----------------------------------------------
        /// <summary>Provisional: Tesla coil synced strikes before it fizzles.</summary>
        public const int CoilCharges = 3;
        /// <summary>Provisional: Leonardo turret charges (synced bolts and the glide bolt).</summary>
        public const int TurretCharges = 4;
        /// <summary>Provisional: the Clockwork Overdrive perk adds this many turret charges (Story).</summary>
        public const int TurretOverdriveBonusCharges = 1;
        /// <summary>Provisional: Cleopatra serpent nest synced lunges.</summary>
        public const int NestCharges = 3;

        // --- Tesla coil --------------------------------------------------------
        /// <summary>Provisional: the coil's own arc (unchanged): 5 damage.</summary>
        public const int CoilOwnArcDamage = 5;
        /// <summary>Provisional: the coil's own arc cadence (unchanged): 120 frames.</summary>
        public const int CoilOwnArcIntervalFrames = 120;
        /// <summary>Provisional: the coil's own arc reach (unchanged), thousandths of a unit.</summary>
        public const int CoilOwnReachMilli = 4000;
        /// <summary>Provisional: a synced coil arc deals this much.</summary>
        public const int CoilSyncedArcDamage = 8;
        /// <summary>Provisional: a synced coil arc's real hitstun.</summary>
        public const int CoilSyncedArcHitstunFrames = 20;
        /// <summary>Provisional: the synced reach (2D circle in both modes), thousandths of a unit.</summary>
        public const int CoilSyncedReachMilli = 4000;
        public const double CoilSyncedReachUnits = CoilSyncedReachMilli / 1000.0;

        // --- Lorentz discharge (replaces the 8-damage mark chain) -------------------
        /// <summary>Provisional: each own coil within this distance of Tesla discharges, thousandths of a unit.</summary>
        public const int DischargeOwnerRangeMilli = 8000;
        public const double DischargeOwnerRangeUnits = DischargeOwnerRangeMilli / 1000.0;
        /// <summary>Provisional: burst damage is this times the coil's remaining charges.</summary>
        public const int DischargeDamagePerCharge = 6;
        /// <summary>Provisional: the burst's hitstun.</summary>
        public const int DischargeHitstunFrames = 20;
        /// <summary>Provisional: the nearest enemy inside the coil's own reach is the target, thousandths.</summary>
        public const int DischargeCoilReachMilli = 4000;
        /// <summary>Provisional: a Conductive-marked enemy within this of the coil may be hit instead, thousandths.</summary>
        public const int DischargeMarkedReachMilli = 8000;
        public const double DischargeCoilReachUnits = DischargeCoilReachMilli / 1000.0;
        public const double DischargeMarkedReachUnits = DischargeMarkedReachMilli / 1000.0;

        // --- Leonardo turret ---------------------------------------------------
        /// <summary>Provisional: the turret's own shot damage.</summary>
        public const int TurretOwnShotDamage = 4;
        /// <summary>Provisional: the turret's own shot cadence.</summary>
        public const int TurretOwnShotIntervalFrames = 180;
        /// <summary>Provisional: the turret's lifetime in seconds (it no longer self-destructs at 4 bolts).</summary>
        public const int TurretLifetimeSeconds = 15;
        /// <summary>Provisional: the Story own-shot reach (kept from before), in units.</summary>
        public const double TurretOwnShotReachStoryUnits = 30.0;
        /// <summary>Provisional: a synced bolt's damage.</summary>
        public const int TurretSyncedBoltDamage = 8;
        /// <summary>Provisional: a synced bolt's real hitstun.</summary>
        public const int TurretSyncedBoltHitstunFrames = 20;
        /// <summary>Provisional: a synced bolt flies straight at 24 u/s, thousandths of a unit per second.</summary>
        public const int TurretSyncedBoltSpeedMilli = 24000;
        public const double TurretSyncedBoltSpeedUnitsPerSecond = TurretSyncedBoltSpeedMilli / 1000.0;
        /// <summary>Provisional: the synced reach (2D circle in both modes), thousandths of a unit.</summary>
        public const int TurretSyncedReachMilli = 10000;
        public const double TurretSyncedReachUnits = TurretSyncedReachMilli / 1000.0;
        /// <summary>Provisional (Story perk): Overdrive shortens the own-shot interval against Extractors only, by this percent of the interval.</summary>
        public const int TurretOverdriveIntervalPercent = 50;

        // --- Serpent nest --------------------------------------------------------
        /// <summary>Provisional: the nest's own nip: 3 damage.</summary>
        public const int NestOwnNipDamage = 3;
        /// <summary>Provisional: the nest's own nip cadence in frames.</summary>
        public const int NestOwnNipIntervalFrames = 60;
        /// <summary>Provisional: the nest's own nip hitstun in frames (0.15 s), and it carries no Venom.</summary>
        public const int NestOwnNipHitstunFrames = 9;
        /// <summary>Provisional: the nest's own reach in Story pixels (unchanged, 60 px).</summary>
        public const int NestOwnReachPixels = 60;
        /// <summary>Provisional: a synced lunge's damage.</summary>
        public const int NestSyncedLungeDamage = 6;
        /// <summary>Provisional: a synced lunge's hold, in frames (Root-length).</summary>
        public const int NestSyncedLungeHoldFrames = 60;
        /// <summary>Provisional: the lunge's Venom, intensity 1.0 for 4 s.</summary>
        public const int NestSyncedLungeVenomIntensityPercent = 100;
        public const double NestSyncedLungeVenomSeconds = 4.0;
        /// <summary>Provisional: the synced reach (2D circle in both modes), thousandths of a unit.</summary>
        public const int NestSyncedReachMilli = 2500;
        public const double NestSyncedReachUnits = NestSyncedReachMilli / 1000.0;

        // --- Venom (plan 1.8) --------------------------------------------------------
        /// <summary>Provisional: Venom deals this many HP per second per point of intensity (was 2, four literal copies).</summary>
        public const int VenomDamagePerSecondPerIntensity = 3;
        /// <summary>Venom ticks once a second (unchanged), in frames.</summary>
        public const int VenomTickFrames = 60;

        /// <summary>
        /// Story: the HP of one Venom tick at <paramref name="intensity"/> (a
        /// non-positive intensity reads as 1), never below 1. Rounds half to even
        /// like the <c>MathF.Round</c> the four old copies used.
        /// </summary>
        public static int VenomDamagePerTick(double intensity) {
            double scaled = VenomDamagePerSecondPerIntensity * (intensity <= 0.0 ? 1.0 : intensity);
            return Math.Max(1, (int)Math.Round(scaled));
        }

        /// <summary>
        /// The healing block (plan 1.8), both modes: while Venom is active every
        /// heal is blocked except Defy's. The Story gate is
        /// <c>PlayerController.IsHealingBlocked</c>, the sim's
        /// <c>FighterDamageRules.CanHeal</c>; both state this rule.
        /// </summary>
        public static bool VenomBlocksHeal(bool venomActive, bool isDefyHeal) => venomActive && !isDefyHeal;

        // --- The slow (plan 1.7) ---------------------------------------------------
        /// <summary>Provisional: every hero Time Dilation source uses this one intensity, which gives x0.4 through the unchanged formula max(0.1, 1 - 0.5 I).</summary>
        public const double HeroSlowIntensity = 1.2;
        /// <summary>Provisional: the slow lingers this long after the source stops refreshing.</summary>
        public const double HeroSlowLingerSeconds = 1.75;
        /// <summary>The linger in frames.</summary>
        public const int HeroSlowLingerFrames = 105;
        /// <summary>Provisional: a boss's Time Dilation move multiplier is never below this (Story, BossController only).</summary>
        public const double BossSlowMoveMultiplierFloor = 0.7;

        /// <summary>The Time Dilation move multiplier max(0.1, 1 - 0.5 I) every body uses (a non-positive intensity reads as 1).</summary>
        public static double TimeDilationMoveMultiplier(double intensity) {
            double potency = intensity <= 0.0 ? 1.0 : intensity;
            return Math.Max(0.1, 1.0 - 0.5 * potency);
        }

        /// <summary>The same multiplier on a boss: never below <see cref="BossSlowMoveMultiplierFloor"/>.</summary>
        public static double BossTimeDilationMoveMultiplier(double intensity) =>
            Math.Max(BossSlowMoveMultiplierFloor, TimeDilationMoveMultiplier(intensity));

        // --- Rift and Vortex --------------------------------------------------------
        // Radii live once in KitMotionRules.RelativityRiftRadiusUnits (2.5) and
        // KitReachRules.SandstormVortexRadiusUnits (2.5); the throw distances are unchanged.
        /// <summary>Provisional: the Rift collapse deals this to each target it pulls in.</summary>
        public const int RiftCollapseDamage = 8;
        /// <summary>Provisional (Story): the Vortex pulls toward its centre at this, units per second.</summary>
        public const double VortexPullUnitsPerSecondStory = 7.0;
        /// <summary>Provisional (sim): the same pull, thousandths of a unit per second (the sim steps it per frame).</summary>
        public const int VortexPullMilliPerSecondSim = 7000;

        // --- Golden Ratio -------------------------------------------------------------
        /// <summary>Provisional: each tick draws a target toward the spiral's centre by this percent of the remaining distance.</summary>
        public const int GoldenRatioPullPercent = 50;
        /// <summary>Provisional: ... capped at this per tick, thousandths of a unit.</summary>
        public const int GoldenRatioPullCapMilli = 1500;
        public const double GoldenRatioPullCapUnits = GoldenRatioPullCapMilli / 1000.0;

        // --- Momentum payout (plan 1.2) --------------------------------------------------
        /// <summary>
        /// The Momentum units a lingering-kit event pays, on the existing unit
        /// scale (7200 = 1 pip). Detonation 0.2 pip (the finisher's 1440), a
        /// synced strike and Golden Ratio 0.05 pip (a string hit's 360). The
        /// caller pays once per event, never per body, and only for a contact
        /// that landed (D1a) with damage: a blocked, refused, warded or
        /// zero-damage contact pays nothing. Reads <see cref="BasicComboRules"/>
        /// without owning a second copy.
        /// </summary>
        public static int MomentumUnitsFor(LingeringMomentumEvent kind) => kind switch {
            LingeringMomentumEvent.Detonation => BasicComboRules.MomentumFinisherGainUnits,
            LingeringMomentumEvent.SyncedStrike => BasicComboRules.MomentumStringHitGainUnits,
            LingeringMomentumEvent.HoldCast => BasicComboRules.MomentumStringHitGainUnits,
            _ => 0
        };
    }
}
