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
        /// A <i>second or later</i> Special-class hit landing on a mob already in
        /// hitstun deals this fraction. The chain also spans the Armored Recovery
        /// that the chain's own S3 poise trip opens: the rest of a confirm and a
        /// second Special landed on that armored window stay prorated, and the
        /// chain ends when the window does. "Already in hitstun" is judged once
        /// per Special per chain, at that Special's first hit (keyed by
        /// <c>AttackID</c>): a Special fired into a string is prorated on every
        /// one of its hits, while a multi-hit Special's own follow-up hits never
        /// count the hitstun its first hit applied (the controller keeps the
        /// per-chain record).
        ///
        /// <para><b>D4 (user direction 2026-10-11, ledger
        /// <c>USER-FIRST-SPECIAL-UNTAXED</c>):</b> the chain's <i>first</i>
        /// Special (<see cref="ClassifyChainSpecial"/>) is untaxed: no x0.6 and
        /// no chain decay on any of its hits (<see cref="ChainHitScale"/>), and
        /// it charges standard poise x1.0. Its hits still advance the chain
        /// counter, so the hits after it decay as before
        /// (<c>PROVISIONAL-FIRST-SPECIAL-TAX-RULES</c>). A second or later
        /// Special keeps this x0.6 and the decay.</para>
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

        // === D4 (user direction 2026-10-11): the first Special in a chain is untaxed ===

        /// <summary>
        /// D4: a hit's role in a mob's stun chain, for the S2 damage scale and the
        /// S3 standard poise cost.
        /// </summary>
        public enum ChainSpecialRole {
            /// <summary>Not a Special: a basic, a construct, a hazard, or an Ultimate-origin hit.</summary>
            NotSpecial,
            /// <summary>A hit of the chain's first Special: untaxed damage, poise x1.0.</summary>
            FirstSpecial,
            /// <summary>
            /// A second or later Special, a movement-ability hit or a Special with
            /// no <c>AttackID</c>: the pre-D4 S2/S3 tax.
            /// </summary>
            OtherSpecial
        }

        /// <summary>
        /// D4: classifies one hit against the chain's recorded first Special
        /// (<paramref name="chainFirstSpecialID"/>, null or empty when none).
        /// <list type="bullet">
        /// <item>Not the Special class, or Ultimate origin:
        /// <see cref="ChainSpecialRole.NotSpecial"/>.</item>
        /// <item>A movement-ability hit (Rail Charge, Wing-Dive and the Midsummer
        /// Gust author <c>Origin = Special</c>) or one with no <c>AttackID</c>:
        /// <see cref="ChainSpecialRole.OtherSpecial"/>. It never claims the slot.</item>
        /// <item>No first Special recorded yet: <see cref="ChainSpecialRole.FirstSpecial"/>;
        /// the caller records <paramref name="attackID"/> as the chain's first.</item>
        /// <item>The recorded first Special's own <c>AttackID</c>:
        /// <see cref="ChainSpecialRole.FirstSpecial"/>. Identity is by ability ID,
        /// so every hit of one Special shares one role (Divine Piercing's thrusts;
        /// E=mc2's contact, burst and Rift collapse; Requiem's pulses; Lorentz and
        /// its arcs), and a recast of the same Special inside one chain matches the
        /// recorded first Special (<c>PROVISIONAL-FIRST-SPECIAL-TAX-RULES</c>).</item>
        /// <item>Anything else: <see cref="ChainSpecialRole.OtherSpecial"/>.</item>
        /// </list>
        /// </summary>
        public static ChainSpecialRole ClassifyChainSpecial(
            AttackClass attackClass, HitOrigin origin, bool sourceIsMovementAbility,
            string attackID, string chainFirstSpecialID) {
            if (attackClass != AttackClass.Special || origin == HitOrigin.Ultimate) return ChainSpecialRole.NotSpecial;
            if (sourceIsMovementAbility || string.IsNullOrEmpty(attackID)) return ChainSpecialRole.OtherSpecial;
            if (string.IsNullOrEmpty(chainFirstSpecialID)) return ChainSpecialRole.FirstSpecial;
            return string.Equals(attackID, chainFirstSpecialID, System.StringComparison.Ordinal)
                ? ChainSpecialRole.FirstSpecial
                : ChainSpecialRole.OtherSpecial;
        }

        /// <summary>
        /// D4: the S2 damage scale for chain hit <paramref name="chainHitIndex"/>
        /// (1-based). A hit of the chain's first Special lands at x1.0 at any
        /// index (no confirm cut, no decay); every other hit keeps
        /// <see cref="ConfirmProrationScale"/>.
        /// </summary>
        public static float ChainHitScale(ChainSpecialRole role, int chainHitIndex, bool specialConfirm) =>
            role == ChainSpecialRole.FirstSpecial ? 1f : ConfirmProrationScale(chainHitIndex, specialConfirm);

        // === S3 (Provisional): standard poise ===

        /// <summary>
        /// Standard-tier stagger budget (post-resistance applied stun, the V7.4
        /// elite rule extended down a tier), re-derived for D4 (2026-10-11,
        /// provisional, <c>PROVISIONAL-FIRST-SPECIAL-TAX-RULES</c>; it was 1.9 s).
        /// One full basic string under the S5 floors applies 34 + 44 + 24 = 102
        /// frames. The chain's first Special charges x1.0, at most
        /// <see cref="FirstSpecialStunAllowanceFrames"/> (36) in total, so a
        /// string plus any first Special fits with 1.5 frames to spare
        /// (102 + 36 = 138 &lt;= 139.5). That includes Joan's ruled route,
        /// Righteous Smite -> hit 1 -> hit 2 -> hit 3 (30 + 102 = 132). A second
        /// Special is taxed and V7.4-diminished: its smallest stunning charge is
        /// 12 x 0.5 x 1.5 = 9 frames, so the ruled route plus any second Special
        /// (132 + 9 = 141) trips Armored Recovery, again with 1.5 frames of
        /// margin. There is no count rule (DECISIONS addendum, 2026-10-11): a
        /// second Special trips only through this arithmetic, so a string plus
        /// both Specials does not trip where first + second &lt;= 37.5 frames
        /// (Lincoln without Kinetic Splitting, Tubman).
        /// 139.5 frames = 2.325 s.
        /// </summary>
        public const float StandardStaggerBudgetSeconds = 2.325f;

        /// <summary>
        /// D4 (provisional): the design bound <see cref="StandardStaggerBudgetSeconds"/>
        /// is sized for. It is the largest worst-case <i>total</i> standard-poise
        /// charge any Special can put on a chain as its first Special, kit and
        /// Resonance extras included: Requiem Chord with the Requiem Crescendo
        /// perk, 12 + 4 x 6 = 36 frames (the later pulses are V7.4-diminished).
        /// <c>StoryFirstSpecialTaxTests</c> recomputes every authored Special
        /// against it.
        /// </summary>
        public const int FirstSpecialStunAllowanceFrames = 36;

        /// <summary>Standard Armored Recovery: short, flinch/knockback-proof, with the committed counterattack.</summary>
        public const float StandardArmoredRecoverySeconds = 0.6f;

        /// <summary>
        /// A <i>taxed</i> Special-sourced stun charges a standard mob's budget at
        /// this multiple of its applied duration (the actual stun is not
        /// lengthened). Since D4 (2026-10-11) "taxed" means the hit's chain role is
        /// <see cref="ChainSpecialRole.OtherSpecial"/>: a second or later Special,
        /// a movement-ability hit or a Special with no <c>AttackID</c>. The
        /// chain's first Special and every non-Special stun charge x1.0. Elites
        /// keep the V7.4 one-for-one accrual.
        /// </summary>
        public const float StandardSpecialStunBudgetCostMultiplier = 1.5f;

        /// <summary>
        /// The budget a stun charges on a standard mob: the applied stun, times
        /// <see cref="StandardSpecialStunBudgetCostMultiplier"/> only when
        /// <paramref name="taxedSpecial"/> (D4).
        /// </summary>
        public static float StandardBudgetCost(float appliedStunSeconds, bool taxedSpecial) =>
            taxedSpecial ? appliedStunSeconds * StandardSpecialStunBudgetCostMultiplier : appliedStunSeconds;

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
        /// while it is not stunned: four times the V7.4 elite rate (D4,
        /// 2026-10-11, provisional; it was 3), so the V7.4 getup-armor window that
        /// follows a naturally expiring chain (0.6 s x 4 = 2.4 s, at least the
        /// 2.325 s budget) clears the credit of any chain that did not trip.
        /// Poise is therefore spent inside one stun chain: back-to-back chains
        /// each start empty, while a second Special landed inside the same chain
        /// trips Armored Recovery.
        /// </summary>
        public const float StandardStaggerDecayPerSecond = 4f;

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

        // === Shared: a mob melee swing's pivot reach (D8 whiff "near", D6c commit) ===

        /// <summary>
        /// The horizontal distance, pivot to pivot (both bodies' feet), at which a
        /// mob's forward melee box still touches a target: the box's far edge
        /// (<c>|HitboxOffset.X| + HitboxSize.X / 2</c>), plus how far a charge
        /// carries it (<paramref name="dashTravelPixels"/>: a ChargeDash's
        /// <c>|DashSpeed| x ResolvedActiveFrames / 60</c>, else 0), plus the
        /// target's hurtbox half-width. Against the Story hero that is
        /// <see cref="StoryCombatRules.HeroHurtboxWidthPixels"/> / 2 = 20 px, so
        /// the legacy scalar melee (a 40 px box at offset 30) reaches 70 px.
        ///
        /// <para>The one statement of this reach (spec B §0.3 / spec D §1.5):
        /// D8's Punish whiff "near" reads it with no dash travel (positions are
        /// read where the swing ended), and D6c's melee commit cap reads it too.
        /// Pure; nothing in <c>scripts/FighterSim/</c> reads it.</para>
        /// </summary>
        public static float MeleePivotReachPixels(
            float hitboxOffsetX, float hitboxWidth, float dashTravelPixels, float targetHalfWidthPixels) =>
            System.Math.Abs(hitboxOffsetX)
            + System.Math.Max(0f, hitboxWidth) / 2f
            + System.Math.Max(0f, dashTravelPixels)
            + System.Math.Max(0f, targetHalfWidthPixels);

        // === D8 (user direction 2026-10-11): the Punish window ===

        /// <summary>
        /// D8 (user direction, ledger <c>USER-PUNISH-WINDOW</c>): a mob or elite
        /// whose attack the hero blocks without a shatter, Foresight-counters, or
        /// sees whiff nearby takes ×<see cref="PunishDamageScale"/> from the
        /// hero's direct hits for this many of its own world-clock ticks. The
        /// window pauses only while the mob's body is frozen (Time Freeze, the
        /// rewind freeze, the Post-Landing Hold) and keeps running through its
        /// victim hitstop, a grab hold and a thrown flight; a re-trigger refreshes
        /// it to this value and never stacks
        /// (<c>PROVISIONAL-PUNISH-WINDOW-RULES</c>).
        /// </summary>
        public const int PunishWindowFrames = 60;

        /// <summary>D8 (user direction): the damage multiplier on an eligible hit against an open window.</summary>
        public const float PunishDamageScale = 1.2f;

        /// <summary>
        /// D8 (provisional): a mob's melee whiff opens its window only while the
        /// hero stands within its swing's pivot reach
        /// (<see cref="MeleePivotReachPixels"/>) plus this margin, horizontally.
        /// </summary>
        public const float PunishWhiffNearMarginPixels = 40f;

        /// <summary>D8 (provisional): ... and within this many pixels vertically, feet to feet.</summary>
        public const float PunishWhiffNearVerticalPixels = 96f;

        /// <summary>
        /// D8 (provisional): a punished hit's floating number is drawn at this
        /// scale as well as in <c>UIPalette.GoldBright</c>, so the payoff is not
        /// carried by colour alone.
        /// </summary>
        public const float PunishDamageNumberScale = 1.25f;

        /// <summary>
        /// D8: the hits an open window multiplies: a player-dealt
        /// (<paramref name="attackerIndex"/> ≥ 0) direct hit that is not a hazard
        /// — basics, directional strikes, Specials and Ultimates. Ticks, construct
        /// hits and hazards never punish, matching the "only direct hits confirm"
        /// rule; Story throws and bowling never reach the intake path at all.
        /// </summary>
        public static bool IsPunishEligible(int attackerIndex, AttackClass attackClass, HitDelivery delivery) =>
            attackerIndex >= 0
            && delivery == HitDelivery.DirectHit
            && attackClass != AttackClass.Hazard;

        /// <summary>
        /// D8: a mob's melee swing that whiffed counts toward a Punish window only
        /// when the hero was near at the end of its active window:
        /// <paramref name="absDx"/> ≤ <paramref name="pivotReach"/> +
        /// <see cref="PunishWhiffNearMarginPixels"/> and <paramref name="absDy"/> ≤
        /// <see cref="PunishWhiffNearVerticalPixels"/> (feet to feet).
        /// </summary>
        public static bool PunishWhiffIsNear(float absDx, float absDy, float pivotReach) =>
            absDx <= pivotReach + PunishWhiffNearMarginPixels
            && absDy <= PunishWhiffNearVerticalPixels;

        // === D6a (user direction 2026-10-11): the finisher lands one step out ===
        // Ledger USER-FINISHER-CARRY; the agent-chosen details are
        // PROVISIONAL-SPACING-WALLS-RULES item 1. A hero's basic-string finisher
        // on a Standard or (user addendum, 2026-10-11) Elite mob decelerates the
        // launch so the mob lands about one step outside that hero's hit-1 reach,
        // instead of S1's long carry. Story only.

        /// <summary>D6a (provisional): "one step" outside the attacker's reach, 0.5 u.</summary>
        public const float FinisherLandingStepPixels = 30f;

        /// <summary>
        /// D6a (provisional): an authored horizontal finisher force
        /// (<c>BasicAttackKnockback</c> x the profile's finisher tenths / 10) at or
        /// above this is a heavy finisher, whose mob lands past the widest-reach
        /// hero's target. Only Lincoln's 22.5 reaches it ("Lincoln stays the
        /// longest carry").
        /// </summary>
        public const float HeavyFinisherForce = 18f;

        /// <summary>D6a (provisional): how far past the widest reach's one-step target a heavy finisher lands its mob.</summary>
        public const float HeavyFinisherMarginPixels = 15f;

        /// <summary>
        /// D6a (provisional): the safety cap on a finisher flight that outlives
        /// its stun, in post-stun frames. Never reached on authored floors (the
        /// longest modelled flight, Lincoln's, ends 43 frames after the stun).
        /// </summary>
        public const int FinisherFlightMaxPostStunFrames = 120;

        /// <summary>
        /// The template string's hit-1 far edge in Story pixels, before any
        /// profile width or Story minor: <c>(StringBoxOffsetXUnits[0] +
        /// StringBoxHalfWidthUnits[0])</c> x 60 = 96 px.
        /// </summary>
        public static float TemplateHit1FarEdgePixels =>
            (float)((BasicComboRules.StringBoxOffsetXUnits[0] + BasicComboRules.StringBoxHalfWidthUnits[0])
                * KitMotionRules.StoryPixelsPerUnit);

        /// <summary>
        /// D6a: the widest authored profile's hit-1 pivot reach against a mob:
        /// the template far edge x <see cref="BasicComboRules.WidestStringReachWidthPercent"/>
        /// (base profiles only, no Story minor) + the mob's hurtbox half-width.
        /// </summary>
        public static float WidestHit1PivotReachPixels(float mobHurtboxHalfWidthPixels) =>
            TemplateHit1FarEdgePixels * BasicComboRules.WidestStringReachWidthPercent / 100f
            + System.Math.Max(0f, mobHurtboxHalfWidthPixels);

        /// <summary>
        /// D6a: a hit arms the finisher flight only when it is a basic-string
        /// finisher: Basic class, the <c>combo_3</c> hitbox, and authored to
        /// launch. Lincoln's launching hit 2, the Up-Attack, the Down-Air, a
        /// non-launching <c>combo_3</c> and every Special or Ultimate do not.
        /// </summary>
        public static bool IsFinisherCarry(AttackClass attackClass, string hitboxID, bool launches) =>
            attackClass == AttackClass.Basic
            && launches
            && StringStepOf(hitboxID) == BasicComboRules.ComboHits - 1;

        /// <summary>
        /// D6a: where, pivot to pivot, the finisher should land its mob: one step
        /// (<see cref="FinisherLandingStepPixels"/>) outside
        /// <paramref name="ownHit1PivotReachPx"/>, the attacker's live hit-1 far
        /// edge + the mob's hurtbox half-width. A heavy finisher
        /// (<paramref name="authoredFinisherForce"/> ≥ <see cref="HeavyFinisherForce"/>)
        /// lands it at least <see cref="HeavyFinisherMarginPixels"/> beyond the
        /// widest-reach hero's one-step target (<paramref name="widestHit1PivotReachPx"/>).
        /// </summary>
        public static float FinisherLandingTargetPixels(
            float ownHit1PivotReachPx, float widestHit1PivotReachPx, float authoredFinisherForce) {
            float target = ownHit1PivotReachPx + FinisherLandingStepPixels;
            if (authoredFinisherForce >= HeavyFinisherForce) {
                float heavy = widestHit1PivotReachPx + FinisherLandingStepPixels + HeavyFinisherMarginPixels;
                if (heavy > target) target = heavy;
            }
            return target;
        }

        /// <summary>D6a: how much farther the finisher must carry its mob from the gap it struck at (never negative).</summary>
        public static float FinisherCarryPixels(float landingTargetPx, float gapAtFinisherPx) =>
            System.Math.Max(0f, landingTargetPx - gapAtFinisherPx);

        /// <summary>
        /// D6a: the constant horizontal deceleration (px/s²) that brings a launch
        /// of <paramref name="launchSpeedPxPerSec"/> to rest after
        /// <paramref name="carryPx"/>, for a 60 Hz step that slows the speed
        /// before each move: <c>v² / (2 (D + v/120))</c>; the <c>v/120</c> term
        /// corrects the discrete step's undershoot. 0 for no launch speed. A
        /// carry of 0 stops the mob on the first step. The caller takes the max
        /// with S1's friction, so the rule never lengthens a carry.
        /// </summary>
        public static float FinisherCarryDeceleration(float launchSpeedPxPerSec, float carryPx) {
            if (launchSpeedPxPerSec <= 0f) return 0f;
            float carry = System.Math.Max(0f, carryPx);
            return launchSpeedPxPerSec * launchSpeedPxPerSec / (2f * (carry + launchSpeedPxPerSec / 120f));
        }

        // === D6c (user direction 2026-10-11): melee mobs stay in range ===
        // Ledger USER-MOB-MELEE-RANGE; PROVISIONAL-SPACING-WALLS-RULES item 3. A
        // mob whose next attack is its MeleeStrike / ChargeDash primary commits
        // only inside its own box's reach, and holds just outside pushbox contact
        // while it cannot commit. Story only.

        /// <summary>D6c (provisional): a capped melee primary commits this far inside its pivot reach.</summary>
        public const float MeleeCommitMarginPixels = 10f;

        /// <summary>D6c (provisional): the stand-off engage line keeps this clearance beyond pushbox contact.</summary>
        public const float StandOffContactClearancePixels = 4f;

        /// <summary>
        /// D6c: the commit range of a capped melee primary,
        /// <c>min(<paramref name="attackRangePixels"/>, <paramref name="pivotReachPixels"/> −
        /// <see cref="MeleeCommitMarginPixels"/>)</c>; a reach no longer than the
        /// margin keeps the authored range.
        /// </summary>
        public static float MeleeCommitRangePixels(float attackRangePixels, float pivotReachPixels) =>
            pivotReachPixels <= MeleeCommitMarginPixels
                ? attackRangePixels
                : System.Math.Min(attackRangePixels, pivotReachPixels - MeleeCommitMarginPixels);

        /// <summary>
        /// D6c: the stand-off engage line of a capped melee primary while it
        /// cannot commit: the larger of <paramref name="engageFraction"/> x the
        /// commit range and pushbox contact (<paramref name="contactGapPixels"/>) +
        /// <see cref="StandOffContactClearancePixels"/> + the 60 Hz worst-case
        /// braking overshoot (one undetected frame at full speed and the
        /// <paramref name="decelerationFrames"/> ramp: <c>v (frames + 1) / 120</c>).
        /// </summary>
        public static float MeleeStandOffEngagePixels(
            float commitRangePixels, float engageFraction, float contactGapPixels,
            float chaseSpeedPxPerSec, float decelerationFrames) =>
            System.Math.Max(
                engageFraction * commitRangePixels,
                contactGapPixels + StandOffContactClearancePixels
                    + System.Math.Max(0f, chaseSpeedPxPerSec) * (decelerationFrames + 1f) / 120f);

        /// <summary>
        /// D6c: whether a melee box's vertical span (centre
        /// <paramref name="boxCentreY"/>, height <paramref name="boxHeight"/>,
        /// relative to the mob's feet, y down) overlaps a target hurtbox standing
        /// <paramref name="targetHeight"/> tall on feet <paramref name="targetFeetYRelative"/>
        /// (relative to the mob's feet). Touching edges do not overlap.
        /// </summary>
        public static bool MeleeBoxReachesVertically(
            float boxCentreY, float boxHeight, float targetFeetYRelative, float targetHeight) {
            float halfHeight = System.Math.Max(0f, boxHeight) / 2f;
            float boxTop = boxCentreY - halfHeight;
            float boxBottom = boxCentreY + halfHeight;
            float targetTop = targetFeetYRelative - System.Math.Max(0f, targetHeight);
            return boxTop < targetFeetYRelative && targetTop < boxBottom;
        }

        // === D9 (user direction 2026-10-11): the Story wall bounce ===
        // Ledger USER-STORY-WALL-BOUNCE; PROVISIONAL-SPACING-WALLS-RULES item 4.
        // A player-launched (or, user addendum 2026-10-11, thrown) mob that hits
        // an Environment wall fast bounces back and takes a small impact, once
        // per launch. Story only; never bosses, never the Fighter sim.

        /// <summary>D9 (user direction, "~300 px/s"): the strict minimum pre-move horizontal speed into the wall.</summary>
        public const float WallBounceMinSpeedPixelsPerSecond = 300f;

        /// <summary>D9 (user direction): the mob bounces back at this fraction of its speed into the wall.</summary>
        public const float WallBounceRestitution = 0.4f;

        /// <summary>D9 (user direction): the impact takes this fraction of the launching hit's damage.</summary>
        public const float WallImpactDamageFraction = 0.1f;

        /// <summary>D9 (provisional): a wall faces the mob when its normal's x is at least this against the motion.</summary>
        public const float WallBounceMinNormalX = 0.7f;

        /// <summary>
        /// D9: a hit arms the wall bounce when it launches, is player-dealt (no
        /// hazard, no enemy source) and the victim does not phase through walls.
        /// </summary>
        public static bool ArmsWallBounce(
            int attackerIndex, AttackClass attackClass, HitDelivery delivery, bool launches, bool phasesThroughWalls) =>
            launches && !phasesThroughWalls && IsPlayerDealt(attackerIndex, attackClass, delivery);

        /// <summary>
        /// D9: the mob was moving into the wall faster than
        /// <see cref="WallBounceMinSpeedPixelsPerSecond"/> (strict) and the wall
        /// faces it (<c>normal.x x sign(v) ≤ −</c><see cref="WallBounceMinNormalX"/>).
        /// </summary>
        public static bool BouncesOffWall(float preMoveVelocityX, float wallNormalX) =>
            System.Math.Abs(preMoveVelocityX) > WallBounceMinSpeedPixelsPerSecond
            && wallNormalX * System.Math.Sign(preMoveVelocityX) <= -WallBounceMinNormalX;

        /// <summary>D9: the horizontal speed after the bounce, back off the wall.</summary>
        public static float WallBounceVelocityX(float preMoveVelocityX) =>
            -WallBounceRestitution * preMoveVelocityX;

        /// <summary>
        /// D9: the impact for one launch: 10 % of the launching hit's pre-defense
        /// Story intake, rounded half away from zero and never below 1 for a
        /// damaging hit (<see cref="ScaleIntakeDamage"/>); defenses then apply
        /// once, at the impact.
        /// </summary>
        public static int WallImpactDamage(int launchingHitIntake) =>
            ScaleIntakeDamage(launchingHitIntake, WallImpactDamageFraction);
    }
}
