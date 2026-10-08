namespace FTT.Combat {

    /// <summary>
    /// Story enemy combat rulebook for the 2026-10-04 playtest-feel pass
    /// (<c>docs/PLAYTEST_FEEL_2026-10-04_PLAN.md</c> §2.2 S1–S5, F5's victim half,
    /// F9 and P4). It sits beside <see cref="EnemyStaggerRules"/> (the V7.4
    /// constants, unchanged) and holds every number this pass added to the Story
    /// mob/boss intake, stagger and chase code — one canonical copy, read by
    /// <c>EnemyController</c> and <c>BossController</c> only.
    ///
    /// <para><b>Scope guard:</b> Story Mode only. Plain constants and pure
    /// functions with no Godot types, so the pure-C# test host can pin them.
    /// Nothing in <c>scripts/FighterSim/</c> may consume this class — Fighter PvP
    /// keeps the cross-mode <see cref="BasicComboRules"/> as its whole answer.</para>
    ///
    /// <para>Every value marked <i>Provisional</i> is a ruling made under the
    /// user's 2026-10-04 "iterate on feel" request and is recorded in the plan's
    /// §3 deviation log so it can be reversed.</para>
    /// </summary>
    public static class StoryEnemyCombatRules {

        // === S1: enemy knockback while stunned decays instead of zeroing ===

        /// <summary>
        /// Horizontal friction on a stunned, grounded mob's knockback, in px/s².
        /// The string's first two hits (≈120–140 px/s on a standard) slide a few
        /// pixels and stay in reach; the 4.5× finisher (≈525 px/s) carries the
        /// mob away. Replaces the old per-frame zeroing that discarded every
        /// knockback the instant it was written.
        /// </summary>
        public const float StunnedGroundFrictionPixelsPerSecondSquared = 1800f;

        /// <summary>
        /// Horizontal drag on a stunned mob's knockback while airborne (a launch)
        /// or flying, in px/s² — lower than ground friction so a launch reads as
        /// a launch. Flying mobs keep their stunned hover (vertical zeroed).
        /// </summary>
        public const float StunnedAirDragPixelsPerSecondSquared = 600f;

        /// <summary>
        /// Depth of the "is there anything under the next step?" probe for an
        /// <i>airborne</i> stunned ground mob. Knockback that would carry a
        /// launched mob over a drop deeper than this is cancelled, so a finisher
        /// never throws a mob off a ledge into the void.
        /// </summary>
        public const float KnockbackAirborneLedgeProbeDepthPixels = 480f;

        // === S2 (Provisional): confirm proration ===

        /// <summary>
        /// A Special-class hit landing on a mob already in hitstun deals this
        /// fraction. The chain also spans the Armored Recovery that the chain's
        /// own S3 poise trip opens: the rest of a confirm and a second Special
        /// landed on that armored window stay prorated, and the chain ends when
        /// the window does. "Already in hitstun" is judged once per Special per chain, at
        /// that Special's first hit (keyed by <c>AttackID</c>): a Special fired into
        /// a string is prorated on every one of its hits, while a multi-hit
        /// Special's own follow-up hits never count the hitstun its first hit
        /// applied (the controller keeps the per-chain record).
        /// </summary>
        public const float ConfirmSpecialOnHitstunDamageScale = 0.6f;

        /// <summary>Hits in one uninterrupted stun chain that land at full value.</summary>
        public const int ConfirmChainFreeHits = 3;

        /// <summary>Per-hit decay applied to every chain hit after <see cref="ConfirmChainFreeHits"/>.</summary>
        public const float ConfirmChainDecayPerHit = 0.85f;

        /// <summary>Floor of the chain decay term (the Special-on-hitstun factor multiplies on top).</summary>
        public const float ConfirmChainDamageFloor = 0.5f;

        /// <summary>
        /// Ultimates (by class or authored origin), DoT/zone ticks and hazards are
        /// exempt from confirm proration: they neither scale nor advance the chain.
        /// </summary>
        public static bool IsProrationExempt(AttackClass attackClass, HitOrigin origin, HitDelivery delivery) =>
            attackClass == AttackClass.Ultimate
            || attackClass == AttackClass.Hazard
            || origin == HitOrigin.Ultimate
            || delivery == HitDelivery.Tick
            || delivery == HitDelivery.Hazard;

        /// <summary>
        /// The damage scale for chain hit <paramref name="chainHitIndex"/> (1-based)
        /// of one uninterrupted stun chain: full value through the third hit, then
        /// ×<see cref="ConfirmChainDecayPerHit"/> per further hit floored at
        /// ×<see cref="ConfirmChainDamageFloor"/>, times
        /// <see cref="ConfirmSpecialOnHitstunDamageScale"/> when a Special lands on
        /// a mob that was already in hitstun.
        /// </summary>
        public static float ConfirmProrationScale(int chainHitIndex, bool specialOnHitstun) {
            float decay = 1f;
            int decayedHits = chainHitIndex - ConfirmChainFreeHits;
            for (int hit = 0; hit < decayedHits; hit++) decay *= ConfirmChainDecayPerHit;
            if (decay < ConfirmChainDamageFloor) decay = ConfirmChainDamageFloor;
            return specialOnHitstun ? decay * ConfirmSpecialOnHitstunDamageScale : decay;
        }

        // === S3 (Provisional): standard poise ===

        /// <summary>
        /// Standard-tier stagger budget (post-resistance applied stun, the V7.4
        /// elite rule extended down a tier). One full basic string under the S5
        /// floors applies 34 + 44 + 24 = 102 frames (1.70 s) and completes; a
        /// Special-sourced stun on top (12 frames × the cost multiplier = 0.30 s)
        /// crosses it and trips Armored Recovery.
        /// </summary>
        public const float StandardStaggerBudgetSeconds = 1.9f;

        /// <summary>Standard Armored Recovery: short, flinch/knockback-proof, with the committed counterattack.</summary>
        public const float StandardArmoredRecoverySeconds = 0.6f;

        /// <summary>
        /// A Special-sourced stun charges a standard mob's budget at this multiple
        /// of its applied duration (the actual stun is not lengthened). Elites
        /// keep the V7.4 one-for-one accrual.
        /// </summary>
        public const float StandardSpecialStunBudgetCostMultiplier = 1.5f;

        /// <summary>The budget a stun charges on a standard mob.</summary>
        public static float StandardBudgetCost(float appliedStunSeconds, bool fromSpecial) =>
            fromSpecial ? appliedStunSeconds * StandardSpecialStunBudgetCostMultiplier : appliedStunSeconds;

        /// <summary>
        /// Whether a hit's stun charges a <i>standard</i> mob's poise budget. An
        /// Ultimate-sourced stun — the Ultimate attack class or an authored
        /// Ultimate origin — charges nothing, as S2 proration and V7.4's
        /// diminishing special stun already exempt Ultimates: a meter-paid
        /// multi-hit Ultimate (Lincoln's five 0.5 s smashes) must not trip the
        /// standard's Armored Recovery mid-cast and have its own last hits
        /// refused by the counterattack (2026-10-04 review). The stun still
        /// applies. The V7.4 <i>elite</i> budget is untouched and keeps charging
        /// Ultimate stuns.
        /// </summary>
        public static bool ChargesStandardPoise(AttackClass attackClass, HitOrigin origin) =>
            attackClass != AttackClass.Ultimate && origin != HitOrigin.Ultimate;

        /// <summary>
        /// A standard's budget drains at this rate (seconds of credit per second)
        /// while it is not stunned — three times the V7.4 elite rate, so the
        /// V7.4 getup-armor window that follows a naturally expiring string
        /// (0.6 s × 3 = 1.8 s) clears a full string's credit. Poise is therefore
        /// spent inside one stun chain: back-to-back strings each complete, while
        /// a Special confirm landed inside the same chain trips Armored Recovery.
        /// </summary>
        public const float StandardStaggerDecayPerSecond = 3f;

        // === S4 (Provisional): basics pacing ===

        /// <summary>
        /// The universal basic set (the three string hits and the two directional
        /// strikes) deals this multiple against Standard-tier mobs, on Story intake
        /// only — the shared <c>.tres</c> damages and the Fighter sim are untouched.
        /// </summary>
        public const float StandardBasicDamageScale = 1.25f;

        /// <summary>True for a Basic-class hit from the universal basic set.</summary>
        public static bool IsBasicsPacingHit(AttackClass attackClass, string hitboxID) =>
            attackClass == AttackClass.Basic && BasicComboRules.IsBasicStringHitbox(hitboxID);

        /// <summary>
        /// Applies an intake scale to a whole-number hit. Rounds half away from zero
        /// and never takes a damaging hit below 1; a unit scale is the identity.
        /// </summary>
        public static int ScaleIntakeDamage(int damage, float scale) {
            if (damage <= 0) return 0;
            if (scale == 1f) return damage;
            int scaled = (int)System.MathF.Round(damage * scale, System.MidpointRounding.AwayFromZero);
            return scaled < 1 ? 1 : scaled;
        }

        // === S5: hit-indexed basic stun floor ===

        /// <summary>
        /// Frames of slack the S5 floor holds beyond the template string's
        /// connect-to-connect gap. Six covers the slowest authored finisher startup
        /// (Lincoln, two frames over the template) with four frames to spare, so a
        /// resistant mob is never released between buffered string hits.
        /// </summary>
        public const int ChainGapMarginFrames = 6;

        /// <summary>
        /// Story-only floor on the post-<c>StunResistance</c> stun of a basic-set
        /// hit, indexed by string hit. Hit N holds the mob through the gap to hit
        /// N+1: the template's active + recovery of hit N + startup of hit N+1
        /// (hitstop freezes both parties alike, so it is not counted) plus
        /// <see cref="ChainGapMarginFrames"/> — 34 frames after hit 1, 44 after
        /// hit 2. The finisher and the two directional strikes end the exchange
        /// and keep the cross-mode <see cref="BasicComboRules.EnemyBasicStunFloorFrames"/>.
        /// Non-basic sources get 0 (no floor).
        /// </summary>
        public static int BasicStringStunFloorFrames(string hitboxID) {
            if (!BasicComboRules.IsBasicStringHitbox(hitboxID)) return 0;
            int step = StringStepOf(hitboxID);
            if (step < 0 || step >= BasicComboRules.ComboHits - 1) return BasicComboRules.EnemyBasicStunFloorFrames;
            int gap = BasicComboRules.GroundActiveFrames[step]
                + BasicComboRules.GroundRecoveryFrames[step]
                + BasicComboRules.GroundStartupFrames[step + 1];
            int floor = gap + ChainGapMarginFrames;
            return floor > BasicComboRules.EnemyBasicStunFloorFrames ? floor : BasicComboRules.EnemyBasicStunFloorFrames;
        }

        /// <summary>
        /// The floor the V7.4 <i>elite</i> stagger budget is charged at for a
        /// basic-set hit: the pre-S5 cross-mode
        /// <see cref="BasicComboRules.EnemyBasicStunFloorFrames"/>, not the longer
        /// S5 floor. The S5 floor exists to hold a resistant mob through the
        /// string's connect gaps; it lengthens the stun, never the elite's poise
        /// cost, so back-to-back strings on a resistant elite still complete as
        /// they did under V7.4 (2026-10-04 review). Standards keep charging the
        /// applied S5 stun — <see cref="StandardStaggerBudgetSeconds"/> is sized
        /// against it. Non-basic sources get 0 (the charge is the applied stun).
        /// </summary>
        public static int EliteBudgetStunFloorFrames(string hitboxID) =>
            BasicComboRules.IsBasicStringHitbox(hitboxID) ? BasicComboRules.EnemyBasicStunFloorFrames : 0;

        /// <summary>0-based string step of a <c>combo_N</c> hitbox ID, or -1.</summary>
        public static int StringStepOf(string hitboxID) {
            const string prefix = "combo_";
            if (hitboxID == null || !hitboxID.StartsWith(prefix, System.StringComparison.Ordinal)) return -1;
            return int.TryParse(hitboxID.Substring(prefix.Length), out int number) && number >= 1
                ? number - 1
                : -1;
        }

        // === F5 (victim half): hitstop exemptions ===

        /// <summary>
        /// A mob or boss takes no victim hitstop from a tick, construct or hazard
        /// delivery, nor from a payload stamped <c>ExemptFromHitstop</c> — only a
        /// direct player-authored hit freezes it.
        /// </summary>
        public static bool VictimSkipsHitstop(HitDelivery delivery, bool exemptFromHitstop) =>
            exemptFromHitstop
            || delivery == HitDelivery.Tick
            || delivery == HitDelivery.Construct
            || delivery == HitDelivery.Hazard;

        /// <summary>
        /// The one victim-hitstop rule for Story mobs and bosses: the mob's hit
        /// freeze and a corpse's kill freeze both read it, and so does
        /// <c>BossController</c>. It is the shared launch-aware
        /// <see cref="BasicComboRules.HitstopFrames(int, bool)"/> — the same call,
        /// with the same dealt damage and the payload's <c>Launches</c>, that the
        /// attacker's freeze makes (<c>PlayerController.OnMeleeHitConfirmed</c> /
        /// <c>ApplyAbilityCasterHitstop</c>), so a launching hit (the finisher,
        /// Up-Attack, Down-Air, a launching Special) freezes attacker and victim
        /// for the same frames, F6's launch bonus included.
        /// </summary>
        public static int VictimHitstopFrames(int damageApplied, bool launches) =>
            BasicComboRules.HitstopFrames(damageApplied, launches);

        // === F9: dealt-hit feedback on mobs and bosses ===

        /// <summary>Camera shake floor for a player-dealt direct hit, in pixels of camera offset.</summary>
        public const float DealtHitShakeBasePixels = 1.0f;

        /// <summary>Additional shake per point of damage dealt.</summary>
        public const float DealtHitShakePixelsPerDamage = 0.15f;

        /// <summary>Cap on the damage-scaled part of the shake.</summary>
        public const float DealtHitShakeMaxPixels = 6.0f;

        /// <summary>Extra shake when the hit is an authored launch.</summary>
        public const float DealtHitShakeLaunchBonusPixels = 2.0f;

        /// <summary>Shake duration of a player-dealt direct hit.</summary>
        public const float DealtHitShakeSeconds = 0.08f;

        /// <summary>Extra duration for an authored launch.</summary>
        public const float DealtHitShakeLaunchBonusSeconds = 0.06f;

        /// <summary>Clearance between a mob's head and its floating damage number, in pixels.</summary>
        public const float DamageNumberHeadClearancePixels = 12f;

        /// <summary>Shake intensity (before the accessibility scale) for a dealt hit.</summary>
        public static float DealtHitShakeIntensity(int damage, bool launches) {
            float scaled = DealtHitShakeBasePixels + System.Math.Max(0, damage) * DealtHitShakePixelsPerDamage;
            if (scaled > DealtHitShakeMaxPixels) scaled = DealtHitShakeMaxPixels;
            return launches ? scaled + DealtHitShakeLaunchBonusPixels : scaled;
        }

        /// <summary>Shake duration for a dealt hit.</summary>
        public static float DealtHitShakeDuration(bool launches) =>
            launches ? DealtHitShakeSeconds + DealtHitShakeLaunchBonusSeconds : DealtHitShakeSeconds;

        /// <summary>
        /// Pixels of camera offset per unit of an authored
        /// <c>AbilityData.ScreenShakeIntensity</c> (a 0–1 design value). Matches
        /// the Story player-hit path's ×12 scaling, so an ability shakes the
        /// camera the same whichever side of it the player stands on.
        /// </summary>
        public const float AuthoredShakePixelsPerIntensity = 12f;

        /// <summary>
        /// A dealt hit uses the payload's authored shake when it is an ability
        /// hit (not Basic-class — basics carry the hitbox's default 0.2, not a
        /// per-move value) and the payload carries a positive intensity
        /// (design-godot.md "Camera Shake System": the shake comes from the
        /// ability's <c>ScreenShakeIntensity</c>/<c>Duration</c>). Everything
        /// else falls back to the damage-and-launch curve.
        /// </summary>
        public static bool UsesAuthoredShake(AttackClass attackClass, float authoredIntensity) =>
            attackClass != AttackClass.Basic && authoredIntensity > 0f;

        /// <summary>
        /// Shake intensity (before the accessibility scale) for a dealt hit: the
        /// authored ability value × <see cref="AuthoredShakePixelsPerIntensity"/>
        /// when <see cref="UsesAuthoredShake"/>, else
        /// <see cref="DealtHitShakeIntensity(int, bool)"/>.
        /// </summary>
        public static float DealtHitShakeIntensity(
            AttackClass attackClass, float authoredIntensity, int damage, bool launches) =>
            UsesAuthoredShake(attackClass, authoredIntensity)
                ? authoredIntensity * AuthoredShakePixelsPerIntensity
                : DealtHitShakeIntensity(damage, launches);

        /// <summary>
        /// Shake duration for a dealt hit: the authored ability duration when
        /// <see cref="UsesAuthoredShake"/> and it is positive, else
        /// <see cref="DealtHitShakeDuration(bool)"/>.
        /// </summary>
        public static float DealtHitShakeDuration(
            AttackClass attackClass, float authoredIntensity, float authoredDuration, bool launches) =>
            UsesAuthoredShake(attackClass, authoredIntensity) && authoredDuration > 0f
                ? authoredDuration
                : DealtHitShakeDuration(launches);

        /// <summary>
        /// Only a player-dealt direct hit shakes the camera: tick, construct and
        /// hazard deliveries (and payloads stamped <c>ExemptFromHitstop</c>) only
        /// float a number.
        /// </summary>
        public static bool DealtHitShakes(HitDelivery delivery, bool exemptFromHitstop) =>
            !VictimSkipsHitstop(delivery, exemptFromHitstop);

        /// <summary>A payload is player-dealt when its attacker slot is a player (index ≥ 0) and it is no hazard.</summary>
        public static bool IsPlayerDealt(int attackerIndex, AttackClass attackClass, HitDelivery delivery) =>
            attackerIndex >= 0 && attackClass != AttackClass.Hazard && delivery != HitDelivery.Hazard;

        // === P4 (Provisional): chase persistence and ledge/wall awareness ===

        /// <summary>
        /// An aggroed mob keeps chasing while its target is inside the mob's room
        /// x-bounds, or within this multiple of its authored <c>DeAggroRadius</c>
        /// when there is no room (or the target left it).
        /// </summary>
        public const float ChaseLeashDeAggroFactor = 1.5f;

        /// <summary>
        /// True while an aggroed mob should keep chasing. <paramref name="targetInRoom"/>
        /// is false when the mob has no room bounds.
        /// </summary>
        public static bool KeepsChasing(float distanceToTarget, float deAggroRadius, bool targetInRoom) =>
            targetInRoom || distanceToTarget <= deAggroRadius * ChaseLeashDeAggroFactor;

        /// <summary>
        /// A ground mob whose target is within this horizontal distance (and
        /// above it — see <see cref="ChaseHoldUnderTargetMinRisePixels"/>) holds
        /// its position and facing instead of flipping back and forth beneath it.
        /// </summary>
        public const float ChaseHoldUnderTargetPixels = 24f;

        /// <summary>
        /// How far above the mob's feet the target's feet must be for the
        /// overhead hold. A target at the mob's own height is never "overhead":
        /// the ordinary facing and stand-off rules handle it.
        /// </summary>
        public const float ChaseHoldUnderTargetMinRisePixels = 48f;

        /// <summary>
        /// True when a ground mob should hold under its target: the target is
        /// (nearly) straight above. <paramref name="toTargetY"/> is negative upward.
        /// </summary>
        public static bool HoldsUnderTarget(float toTargetX, float toTargetY) =>
            System.Math.Abs(toTargetX) <= ChaseHoldUnderTargetPixels
            && -toTargetY >= ChaseHoldUnderTargetMinRisePixels;

        /// <summary>How far past the mob's front edge the ledge probe looks.</summary>
        public const float LedgeProbeAheadMarginPixels = 8f;

        /// <summary>Height above the feet the ledge probe starts at (so a rising slope still counts as floor).</summary>
        public const float LedgeProbeRisePixels = 24f;

        /// <summary>A drop deeper than this below the feet is a ledge a ground mob will not walk off.</summary>
        public const float LedgeProbeDepthPixels = 64f;

        /// <summary>How far past the mob's front edge the wall probe looks.</summary>
        public const float WallProbeMarginPixels = 8f;
    }
}
