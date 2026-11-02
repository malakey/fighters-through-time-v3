using FTT.Combat;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// The Fighter sim's fixed-point view of <see cref="LingeringKitRules"/> (the
    /// lingering-ability kit pass, 2026-10-31) plus the sim's two seams for it:
    /// the OWED-STRIKE storage and the <c>CountsAsCombo</c> marker. Every
    /// distance is the rulebook's integer thousandths converted with an integer
    /// division, so no float or double reaches <c>scripts/FighterSim/</c>
    /// (the <see cref="FighterMeterRules"/> pattern). Nothing here is state: the
    /// owed count lives in <c>FighterVerbComponent.OwedSyncedStrikes</c> (310).
    /// </summary>
    public static class FighterLingeringKitRules {
        private static readonly FP64 Thousand = FP64.FromInt(1000);

        /// <summary>Thousandths of a unit as a fixed-point unit value.</summary>
        public static FP64 FromMilli(int milli) => FP64.FromInt(milli) / Thousand;

        // --- Synced reach: a 2D circle, tested as a squared distance (plan 1.6) -----
        public static readonly FP64 CoilSyncedReachSquared = Square(FromMilli(LingeringKitRules.CoilSyncedReachMilli));
        public static readonly FP64 TurretSyncedReachSquared = Square(FromMilli(LingeringKitRules.TurretSyncedReachMilli));
        public static readonly FP64 NestSyncedReachSquared = Square(FromMilli(LingeringKitRules.NestSyncedReachMilli));

        // --- Lorentz discharge ranges (squared) ---------------------------------------
        public static readonly FP64 DischargeOwnerRangeSquared = Square(FromMilli(LingeringKitRules.DischargeOwnerRangeMilli));
        public static readonly FP64 DischargeCoilReachSquared = Square(FromMilli(LingeringKitRules.DischargeCoilReachMilli));
        public static readonly FP64 DischargeMarkedReachSquared = Square(FromMilli(LingeringKitRules.DischargeMarkedReachMilli));

        // --- Turret synced bolt speed, units per frame -----------------------------------
        public static readonly FP64 TurretSyncedBoltSpeedPerFrame =
            FromMilli(LingeringKitRules.TurretSyncedBoltSpeedMilli) / FP64.FromInt(60);

        // --- Vortex pull, units per frame (7.0 u/s) ---------------------------------------
        public static readonly FP64 VortexPullPerFrame =
            FromMilli(LingeringKitRules.VortexPullMilliPerSecondSim) / FP64.FromInt(60);

        // --- Golden Ratio pull ----------------------------------------------------------
        public static readonly FP64 GoldenRatioPullFraction =
            FP64.FromInt(LingeringKitRules.GoldenRatioPullPercent) / FP64.FromInt(100);
        public static readonly FP64 GoldenRatioPullCap = FromMilli(LingeringKitRules.GoldenRatioPullCapMilli);

        private static FP64 Square(FP64 value) => value * value;

        /// <summary>The squared distance between two points (no square root).</summary>
        public static FP64 DistanceSquared(in FPVector2 a, in FPVector2 b) {
            FP64 dx = a.x - b.x;
            FP64 dy = a.y - b.y;
            return dx * dx + dy * dy;
        }

        /// <summary>True when <paramref name="target"/> lies inside the circle of squared radius <paramref name="reachSquared"/> about <paramref name="origin"/>.</summary>
        public static bool WithinReach(in FPVector2 origin, in FPVector2 target, FP64 reachSquared) =>
            DistanceSquared(in origin, in target) <= reachSquared;

        // --- Owed strikes (plan 1.5) ----------------------------------------------------
        /// <summary>The owed count saturates here (it is the low byte of the field).</summary>
        public const int OwedStrikeCap = 255;

        /// <summary>
        /// A hit site records one owed synced strike on the attacker for a landed
        /// (D1a) basic swing or direct Special hit. A construct hit, a tick, a throw
        /// and an Ultimate never call this; the Lorentz Pulse's own zone pulse is
        /// excluded by its caller (the discharge replaces that chain). The persistent-
        /// object system consumes it at the top of its update and always clears it, so
        /// it never accumulates across ticks.
        /// </summary>
        public static void NoteOwedStrike(ref FighterVerbComponent attackerVerb) {
            if ((attackerVerb.OwedSyncedStrikes & OwedStrikeCap) < OwedStrikeCap) attackerVerb.OwedSyncedStrikes++;
        }

        /// <summary>The owed strikes waiting on this attacker (0 when none).</summary>
        public static int OwedStrikes(in FighterVerbComponent verb) => verb.OwedSyncedStrikes & OwedStrikeCap;

        /// <summary>
        /// Reads and clears the owed count. <c>FighterPersistentObjectSystem.Update</c>
        /// calls this at its top for each fighter, whatever it then does with the count
        /// (KL-SIM builds the strikes from it; until then it simply clears).
        /// </summary>
        public static int TakeOwedStrikes(ref FighterVerbComponent verb) {
            int owed = verb.OwedSyncedStrikes & OwedStrikeCap;
            verb.OwedSyncedStrikes = 0;
            return owed;
        }

        /// <summary>
        /// Records the owed strike for a landed direct Special hit. Landed is D1a
        /// (<c>Landed</c> or <c>Absorbed</c> with authored damage); the contract must be a
        /// <c>DirectHit</c> delivery and not Ultimate-origin (the caller never calls this for
        /// an Ultimate). Independent of whether the cast is still busy: a shot that outlives
        /// its cast is still a Special hit of the owner's.
        /// </summary>
        internal static void NoteOwnerSpecialHit(
            ref FighterVerbComponent attackerVerb,
            FighterHitOutcome outcome,
            int damage,
            in FighterAbilityHitData contract) {
            if (damage <= 0) return;
            if (outcome != FighterHitOutcome.Landed && outcome != FighterHitOutcome.Absorbed) return;
            if (contract.Delivery != (int)HitDelivery.DirectHit) return;
            NoteOwedStrike(ref attackerVerb);
        }

        /// <summary>The same for a landed basic swing (string, air strike, Up-Attack, Down-Air, ledge attack).</summary>
        internal static void NoteOwnerBasicHit(
            ref FighterVerbComponent attackerVerb, FighterHitOutcome outcome, int damage) {
            if (damage <= 0) return;
            if (outcome != FighterHitOutcome.Landed && outcome != FighterHitOutcome.Absorbed) return;
            NoteOwedStrike(ref attackerVerb);
        }

        // --- The CountsAsCombo marker (plan 1.1) ---------------------------------------
        /// <summary>
        /// The sim's <c>CountsAsCombo</c> marker is this call, not a field: a hit site that
        /// resolves a synced strike, a Lorentz discharge burst, a Rift collapse hit, a
        /// Vortex final launch or a Golden Ratio tick calls it once with the contact's
        /// outcome and damage. It (1) arms the owner's D3 landed-hit flag, only when the
        /// owner's busy cast is <paramref name="hitSlot"/> and <paramref name="sourceCastId"/>
        /// is the cast in progress (the existing attribution rule), regardless of the
        /// contract's delivery; and (2) pays <paramref name="payout"/> Momentum once, only
        /// for a D1a-landed contact with damage. It does NOT set the swing latch C, give
        /// Rally echo or add hitstop. Returns true when the contact counted.
        /// <paramref name="armD3"/> false skips the arming (a synced strike outside any
        /// cast); <paramref name="payoutAlreadyPaid"/> lets a caller pay once per event
        /// when it resolves several bodies.
        /// </summary>
        internal static bool CountsAsCombo(
            ref FighterActionStateComponent cast,
            ref FighterVerbComponent attackerVerb,
            int hitSlot,
            int sourceCastId,
            FighterHitOutcome outcome,
            int damage,
            LingeringMomentumEvent payout,
            bool armD3 = true,
            bool payoutAlreadyPaid = false) {
            if (damage <= 0) return false;
            if (outcome != FighterHitOutcome.Landed && outcome != FighterHitOutcome.Absorbed) return false;
            if (armD3
                && sourceCastId == cast.SpecialCastId
                && FighterSpecialCastRules.IsBusy(in cast)
                && cast.SpecialSlot == hitSlot) {
                cast.SpecialFlags |= FighterSpecialCastRules.FlagLandedDirectHit;
            }
            if (!payoutAlreadyPaid) {
                attackerVerb.MomentumUnits = FighterMomentumRules.Gain(
                    attackerVerb.MomentumUnits, LingeringKitRules.MomentumUnitsFor(payout));
            }
            return true;
        }

        // --- The owner's landed string finisher (Rift collapse trigger) -------------------
        /// <summary>Bit 8 of the owed field: this tick's landed basic hit was the string finisher (combo_3).</summary>
        public const int OwedFinisherBit = 256;

        /// <summary>The finisher site records that its hit landed this tick (the Rift collapse reads it).</summary>
        public static void NoteOwedFinisher(ref FighterVerbComponent attackerVerb) => attackerVerb.OwedSyncedStrikes |= OwedFinisherBit;

        /// <summary>Reads and clears both the owed count and the finisher bit.</summary>
        public static int TakeOwed(ref FighterVerbComponent verb, out bool finisherLanded) {
            finisherLanded = (verb.OwedSyncedStrikes & OwedFinisherBit) != 0;
            int owed = verb.OwedSyncedStrikes & OwedStrikeCap;
            verb.OwedSyncedStrikes = 0;
            return owed;
        }

        // --- One construct / discharge hit ------------------------------------------------
        /// <summary>
        /// A blockable, impulse-free construct-class hit with no hitstop and no Rally. Its hitstun only
        /// lengthens a stun the victim already has (<c>preserveStun</c>): a launch or a tumble survives it.
        /// Returns how the contact resolved (the caller pays Momentum through <see cref="CountsAsCombo"/>).
        /// </summary>
        internal static FighterHitOutcome Strike(
            ref Frame frame,
            EntityRef ownerEntity,
            EntityRef targetEntity,
            int damage,
            int hitstunFrames,
            int statusType,
            int statusFrames,
            FP64 statusIntensity,
            FP64 hitOriginX,
            int attackClass = FighterDamageRules.BasicAttackClass) {
            ref FighterStateComponent owner = ref frame.Get<FighterStateComponent>(ownerEntity);
            ref FighterRuntimeComponent ownerRuntime = ref frame.Get<FighterRuntimeComponent>(ownerEntity);
            ref FighterVerbComponent ownerVerb = ref frame.Get<FighterVerbComponent>(ownerEntity);
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
            ref FighterVerbComponent targetVerb = ref frame.Get<FighterVerbComponent>(targetEntity);
            ref FighterDefenseComponent targetDefense = ref frame.Get<FighterDefenseComponent>(targetEntity);
            ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
            return FighterDamageRules.ResolveFighterHit(
                ref frame.Get<FighterActionStateComponent>(targetEntity),
                ref owner, ref ownerRuntime, ref ownerVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                attackClass, damage, FP64.Zero, hitstunFrames,
                statusType, statusFrames, statusIntensity, hitOriginX,
                collectsEcho: false,
                appliesHitstop: false,
                launches: false,
                preserveStun: true);
        }

        // --- Venom -------------------------------------------------------------------
        /// <summary>HP of one Venom tick: 3 times intensity, at least 1 (the sim's rounding is <c>ScaleIntegerByFP</c>).</summary>
        public const int VenomDamagePerSecondPerIntensity = LingeringKitRules.VenomDamagePerSecondPerIntensity;
        /// <summary>Frames between Venom ticks.</summary>
        public const int VenomTickFrames = LingeringKitRules.VenomTickFrames;
    }
}
