using FTT.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Random;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// Package 12 W5 (M23): Chronal Orb spawn timing. Each gap is a seeded draw
    /// from <c>FighterMatchComponent.RandomState0/1</c> inside the selected
    /// frequency's window — the design's "2-3 orbs per minute" is a window, not a
    /// metronome — floored at <see cref="MinimumGapFrames"/> (the design's strict
    /// 10-second minimum cooldown between consecutive spawns). Each window is
    /// centred on the fixed interval it replaces (Low 3600, Medium 1500, High 660),
    /// so the average rate of every band is unchanged.
    /// </summary>
    public static class FighterOrbSpawnRules {
        /// <summary>The strict minimum gap between consecutive spawns: 10 s.</summary>
        public const int MinimumGapFrames = 600;
        /// <summary>An orb dissolves this long after spawning if nobody collects it: 15 s.</summary>
        public const int LifetimeFrames = 900;
        /// <summary>M24: Resonance Surge's meter grant, in Ultimate Meter points.</summary>
        public const int ResonanceSurgeMeter = 15;

        /// <summary>
        /// The inclusive spawn-gap window for a <c>ChronalOrbFrequency</c> ordinal
        /// (1 Low, 2 Medium, 3 High); (0, 0) for Off or an unknown value.
        /// </summary>
        public static (int Min, int Max) Window(int frequency) => frequency switch {
            1 => (3000, 4200),  // ~1 per minute
            2 => (1200, 1800),  // 2-3 per minute
            3 => (600, 720),    // 5-6 per minute
            _ => (0, 0)
        };

        /// <summary>
        /// One seeded gap draw. The caller owns the RNG state and writes it back,
        /// so every draw threads the match's single deterministic stream.
        /// </summary>
        public static int DrawSpawnGap(ref DeterministicRandom random, int frequency) {
            (int min, int max) = Window(frequency);
            if (max <= 0) return 0;
            if (min < MinimumGapFrames) min = MinimumGapFrames;
            if (max < min) max = min;
            return random.NextIntInclusive(min, max);
        }

        /// <summary>
        /// Number of orb types in the draw: the four Fighter orbs, plus Resonance
        /// Surge (type 4) only when the Meter pickups sub-toggle is on.
        /// </summary>
        public static int EffectTypeCount(bool meterPickupsEnabled) => meterPickupsEnabled ? 5 : 4;
    }

    /// <summary>
    /// Package 12 W5 (2026-09-26 design): each stage's hazard runs on its own fixed,
    /// authored cadence; the match-settings frequency selector is retired and
    /// hazards are a single On/Off toggle. Overtime and Sudden Death halve the
    /// idle gap and the recovery phase (the warning phase is untouched).
    ///
    /// <para>The ten per-stage values are the one canonical cadence table. They
    /// were seeded from the retired Medium interval (2700 frames = 45 s), which
    /// was the default every stage actually ran, so a default match behaves
    /// exactly as it did before the toggle. They are authored per stage so a
    /// later tuning pass can change one stage without touching the others.</para>
    /// </summary>
    public static class FighterHazardCadence {
        /// <summary>
        /// The smallest cadence override a test may pass. Anything smaller can only
        /// be a stale frequency ordinal (1-3), so the rules constructor refuses it.
        /// </summary>
        public const int MinimumOverrideFrames = 60;

        /// <summary>The fallback cadence (the legacy flat arena and any unknown type).</summary>
        public const int DefaultFrames = 2700;

        /// <summary>Frames between activations for a <see cref="FighterHazardTypeID"/>.</summary>
        public static int AuthoredFrames(int hazardType) => hazardType switch {
            FighterHazardTypeID.FlorenceSteamPipe => 2700,
            FighterHazardTypeID.OrleansTrebuchetDebris => 2700,
            FighterHazardTypeID.ChicagoTeslaInduction => 2700,
            FighterHazardTypeID.ParisDampeningBeam => 2700,
            FighterHazardTypeID.VesuviusRockfall => 2700,
            FighterHazardTypeID.NassauMortar => 2700,
            FighterHazardTypeID.AlexandriaSinkhole => 2700,
            FighterHazardTypeID.BerlinSearchlight => 2700,
            FighterHazardTypeID.GlobeAudienceHeckle => 2700,
            FighterHazardTypeID.GettysburgArtillery => 2700,
            _ => DefaultFrames
        };
    }

    internal static class FighterEntityQueries {
        public static bool TryFindFighter(ref Frame frame, int playerID, out EntityRef found) {
            var filter = frame.Filter<FighterStateComponent>();
            while (filter.Next(out EntityRef entity)) {
                if (frame.GetReadOnly<FighterStateComponent>(entity).PlayerID == playerID) {
                    found = entity;
                    return true;
                }
            }
            found = default;
            return false;
        }

        public static bool Overlaps(
            in FPVector2 firstPosition,
            in FPVector2 firstHalfExtents,
            in FPVector2 secondPosition,
            in FPVector2 secondHalfExtents) =>
            FP64.Abs(firstPosition.x - secondPosition.x) <= firstHalfExtents.x + secondHalfExtents.x
            && FP64.Abs(firstPosition.y - secondPosition.y) <= firstHalfExtents.y + secondHalfExtents.y;
    }

    /// <summary>
    /// A04 (Package 13 W1, Fighter only): a fighter who loses a stock loses
    /// every persistent object (component 303 — constructs, the turret, the
    /// nest, the snare, Mozart's staff platforms) and every attack zone
    /// (component 309) it owns, on the KO tick, with no final attack and no
    /// pending damage. Projectiles already in flight are untouched and finish.
    /// Story keeps its persist-through-death rule; nothing here reaches it.
    ///
    /// <para>The stock-loss chokepoint <c>FighterSimulationRules.ApplyStockLoss</c>
    /// holds no Frame, so — exactly like the Conductive mark's
    /// <c>ClearMarkIfRespawning</c> — the observable post-KO state is read
    /// instead: a fighter on the Chronal Respawn Platform, or out of stocks, is
    /// by definition past a stock loss. The platform holds every input, so a
    /// fallen owner cannot deploy anything new while this reads true. The sweep
    /// runs at the top of the persistent-object and zone systems (so a KO
    /// earlier in the tick denies the object its next action) and after the
    /// match system's KO bookkeeping (so a late-tick blast-zone KO also clears
    /// on its own tick). It is idempotent and needs no new snapshot field.</para>
    /// </summary>
    internal static class FighterStockLossRules {
        public static bool IsFallen(in FighterStateComponent fighter) =>
            fighter.Stocks <= 0 || fighter.RespawnFramesRemaining > 0;

        /// <summary>Destroys every 303/309 entity owned by a fallen fighter.</summary>
        public static void DespawnOwnedByFallenFighters(ref Frame frame) {
            bool playerOneFallen = false;
            bool playerTwoFallen = false;
            var fighters = frame.Filter<FighterStateComponent>();
            while (fighters.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref frame.GetReadOnly<FighterStateComponent>(entity);
                if (!IsFallen(in fighter)) continue;
                if (fighter.PlayerID == 0) playerOneFallen = true;
                else if (fighter.PlayerID == 1) playerTwoFallen = true;
            }
            if (!playerOneFallen && !playerTwoFallen) return;
            DespawnOwnedBy(ref frame, playerOneFallen, playerTwoFallen);
        }

        /// <summary>Destroys the persistent objects and zones of the flagged owners.</summary>
        public static void DespawnOwnedBy(ref Frame frame, bool playerOne, bool playerTwo) {
            var persistents = frame.Filter<FighterPersistentObjectComponent>();
            while (persistents.Next(out EntityRef entity)) {
                int owner = frame.GetReadOnly<FighterPersistentObjectComponent>(entity).OwnerPlayerID;
                if ((owner == 0 && playerOne) || (owner == 1 && playerTwo)) frame.DestroyEntity(entity);
            }
            var zones = frame.Filter<FighterZoneComponent>();
            while (zones.Next(out EntityRef entity)) {
                int owner = frame.GetReadOnly<FighterZoneComponent>(entity).OwnerPlayerID;
                if ((owner == 0 && playerOne) || (owner == 1 && playerTwo)) frame.DestroyEntity(entity);
            }
        }
    }

    /// <summary>
    /// V7.6 F07 (Package 11 A1): the deterministic half of the caster-owned
    /// Conductive MARK. A mark is NOT a status — it occupies neither status slot,
    /// causes no action lock, and contributes zero stagger budget — so it has its
    /// own component (ID 318) and its own rules, all pure integer state that
    /// round-trips through snapshot and hash.
    ///
    /// Fighter Mode always applies the BASELINE duration: the Story-only
    /// <c>tesla_conductive_hold</c> Resonance node must never reach the sim
    /// (Story/Fighter isolation, Package 11 §2.14).
    /// </summary>
    public static class FighterConductiveRules {

        /// <summary>
        /// One mark per target: a new source replaces the old one, the same source
        /// takes the longer remaining time — never additive. A replacement also
        /// clears the chain-consumed guard, because a fresh mark is a fresh chain
        /// opportunity.
        /// </summary>
        public static void ApplyMark(ref FighterConductiveComponent mark, int sourcePlayerID, int frames) {
            if (frames <= 0) return;
            if (mark.SourcePlayerID != sourcePlayerID || frames > mark.FramesRemaining) {
                mark.SourcePlayerID = sourcePlayerID;
                mark.FramesRemaining = frames;
                mark.ChainConsumedExecutionID = 0;
            }
        }

        /// <summary>
        /// One frame of decay. A frozen (hitstop) fighter's mark holds exactly as
        /// their hitstun does, so the freeze cannot silently shorten it.
        /// </summary>
        public static void Tick(ref FighterConductiveComponent mark, bool frozen) {
            if (frozen || mark.FramesRemaining <= 0) return;
            mark.FramesRemaining--;
            if (mark.FramesRemaining > 0) return;
            mark.SourcePlayerID = -1;
            mark.ChainConsumedExecutionID = 0;
        }

        public static bool HasMarkFrom(in FighterConductiveComponent mark, int sourcePlayerID) =>
            mark.FramesRemaining > 0 && mark.SourcePlayerID == sourcePlayerID;

        /// <summary>
        /// The per-execution chain guard. Returns true exactly once per
        /// <paramref name="executionID"/>, so a restored multi-hit Lorentz Pulse
        /// replaying across a rollback cannot duplicate its chains. Execution IDs
        /// are deterministic sim counters, never wall-clock values, and must be
        /// non-zero (0 is the "nothing consumed" sentinel).
        /// </summary>
        public static bool TryConsumeChain(ref FighterConductiveComponent mark, int sourcePlayerID, int executionID) {
            if (executionID == 0 || !HasMarkFrom(in mark, sourcePlayerID)) return false;
            if (mark.ChainConsumedExecutionID == executionID) return false;
            mark.ChainConsumedExecutionID = executionID;
            return true;
        }

        /// <summary>Stock loss and match reset drop the mark outright.</summary>
        public static void Clear(ref FighterConductiveComponent mark) {
            mark.FramesRemaining = 0;
            mark.SourcePlayerID = -1;
            mark.ChainConsumedExecutionID = 0;
        }
    }

    /// <summary>
    /// V7.6 D01–D04 defensive rules (Package 11 A1b), deterministic half. Pure
    /// integer state transitions on <see cref="FighterDefenseComponent"/> — no
    /// float math, no wall clock, no Godot types — so a rollback replays them
    /// bit-identically.
    /// </summary>
    internal static class FighterDefenseRules {
        /// <summary>D04: 60 active gameplay ticks (1 s at 60 Hz) once control resumes.</summary>
        public const int DefyProtectionTicks = 60;
        /// <summary>D02c: eight seconds of live gameplay for a granted HP shield.</summary>
        public const int GrantedShieldLifetimeTicks = 480;

        /// <summary>
        /// True while a Defy survivor is protected: through the presentation
        /// (awaiting control) and for the resumed ticks 1–60. Tick 61 is
        /// unprotected.
        /// </summary>
        public static bool IsDefyProtected(in FighterDefenseComponent defense) =>
            defense.DefyProtectionAwaitControl != 0 || defense.DefyProtectionFrames > 0;

        /// <summary>
        /// Installs the window on a successful proc. The identity is derived
        /// from the player rather than a counter so a resimulated proc produces
        /// the same value and cannot stage a duplicate presentation.
        /// </summary>
        public static void BeginDefyProtection(ref FighterDefenseComponent defense, int playerID) {
            defense.DefyProtectionAwaitControl = 1;
            defense.DefyProtectionFrames = DefyProtectionTicks;
            defense.DefyProcIdentity = playerID + 1;
        }

        /// <summary>
        /// Advances the window once per authoritative tick.
        /// <paramref name="suspended"/> covers global hitstop, menus, Time
        /// Freeze and suspended-combat presentations — the countdown pauses,
        /// it never banks catch-up ticks. <paramref name="actionable"/> is the
        /// first resumed normal-control tick that starts the countdown;
        /// attacking neither cancels nor refreshes it.
        /// </summary>
        public static void Tick(ref FighterDefenseComponent defense, bool suspended, bool actionable) {
            if (suspended) return;
            if (defense.DefyProtectionAwaitControl != 0) {
                if (!actionable) return;
                defense.DefyProtectionAwaitControl = 0;
                // Resumed tick 1 is protected; the decrement happens at tick end.
            }
            if (defense.DefyProtectionFrames > 0) {
                defense.DefyProtectionFrames--;
                if (defense.DefyProtectionFrames <= 0) defense.DefyProcIdentity = 0;
            }
            if (defense.BarrierRemainingFrames > 0) {
                defense.BarrierRemainingFrames--;
                // D02c expiry: the remaining absorption is discarded with no
                // heal, meter, block event or expiry proc.
                if (defense.BarrierRemainingFrames <= 0) ClearBarrier(ref defense);
            }
        }

        public static void ClearBarrier(ref FighterDefenseComponent defense) {
            defense.BarrierPoints = 0;
            defense.BarrierCapacity = 0;
            defense.BarrierRemainingFrames = 0;
            defense.BarrierEffectID = 0;
        }

        /// <summary>
        /// Death, stock loss, Death Rewind cleanup, scene reconstruction and
        /// F22 Sudden Death all clear the transient window and the barrier.
        /// The spent Defy flag itself lives on the verb component and is
        /// deliberately untouched here.
        /// </summary>
        public static void Clear(ref FighterDefenseComponent defense) {
            defense.DefyProtectionAwaitControl = 0;
            defense.DefyProtectionFrames = 0;
            defense.DefyProcIdentity = 0;
            defense.AegisActive = 0;
            ClearBarrier(ref defense);
            // The Sudden Death bar is a PHASE property, not per-life state: a
            // stock loss inside Sudden Death must not lift it.
        }

        /// <summary>
        /// The F13 read-model source. <c>Spent</c> and <c>Unavailable</c> are
        /// distinct states and the bar never clears the spent flag.
        /// </summary>
        public static FTT.Core.DefySealState Seal(
            in FighterDefenseComponent defense, bool defyUsed, bool alive, FP64 influence) {
            if (!alive || defense.DefyBarred != 0) return FTT.Core.DefySealState.Barred;
            if (defyUsed) return FTT.Core.DefySealState.Spent;
            return influence >= FP64.FromInt(100)
                ? FTT.Core.DefySealState.Ready
                : FTT.Core.DefySealState.Building;
        }
    }

    internal static class FighterDamageRules {
        public const int BasicAttackClass = 1;
        public const int SpecialAttackClass = 2;
        public const int UltimateAttackClass = 3;
        public const int HazardAttackClass = 4;
        /// <summary>A blocked hazard tick costs exactly one shield charge (design §10).</summary>
        public const int HazardBlockChargeCost = 1;
        private const int BlockButton = 1 << 6;
        private static readonly FP64 MaxInfluence = FP64.FromInt(100);
        // Desperation Resonance (V7.1): echoFraction = 0.20 + 0.30 × missingHP,
        // ×1.5 capped at 0.60 during Overtime.
        // Fixed-point mirrors of the shared BasicComboRules Rally numbers
        // (FromDouble of process constants is deterministic).
        private static readonly FP64 EchoFractionBase =
            FP64.FromDouble(FTT.Combat.BasicComboRules.EchoFractionBase);
        private static readonly FP64 EchoFractionSlope =
            FP64.FromDouble(FTT.Combat.BasicComboRules.EchoFractionSlope);
        private static readonly FP64 OvertimeEchoMultiplier = FP64.FromDouble(1.5);
        private static readonly FP64 OvertimeEchoCap = FP64.FromDouble(0.60);
        // V7.3 Rally reclaim cap: one connecting hit reclaims at most its own
        // damage × this multiplier — no more one-poke full-pool cashouts.
        private static readonly FP64 RallyReclaimMultiplier =
            FP64.FromDouble(FTT.Combat.BasicComboRules.RallyReclaimDamageMultiplier);
        /// <summary>Fixed-point mirror of <c>BasicComboRules.GuardBreakPushX</c> (units/s).</summary>
        internal static readonly FP64 GuardBreakPushX =
            FP64.FromDouble(FTT.Combat.BasicComboRules.GuardBreakPushX);
        /// <summary>The Y-down <c>GuardBreakPushYDown</c> negated into the sim's Y-up space.</summary>
        internal static readonly FP64 GuardBreakPushYUp =
            FP64.FromDouble(-FTT.Combat.BasicComboRules.GuardBreakPushYDown);

        /// <summary>
        /// Fixed-point twin of <c>BasicComboRules.GuardBreakPushSign</c>: +1 pushes
        /// toward +X, away from the attacker; a coincident attacker falls back to
        /// backward-from-facing.
        /// </summary>
        internal static int GuardBreakPushSign(FP64 defenderX, FP64 attackerX, bool defenderFacingRight) {
            if (attackerX < defenderX) return 1;
            if (attackerX > defenderX) return -1;
            return defenderFacingRight ? -1 : 1;
        }

        public static bool ApplyFighterHit(
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            ref FighterVerbComponent attackerVerb,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            ref FighterVerbComponent targetVerb,
            ref FighterDefenseComponent targetDefense,
            in FighterTuningComponent targetTuning,
            int attackClass,
            int damage,
            FP64 knockback,
            int hitstunFrames,
            int statusType,
            int statusFrames,
            FP64 statusIntensity,
            FP64 hitOriginX,
            bool creditInfluence = true,
            int blockChargeCost = 0,
            FP64 verticalKnockbackScale = default,
            bool collectsEcho = true,
            bool appliesHitstop = true,
            bool blockCancelableHitstun = true,
            bool bypassesFiniteShields = false,
            bool launches = true,
            bool shieldBreaker = false,
            bool hasKnockbackVector = false,
            FP64 knockbackVertical = default) {
            // A01 (Package 13 W1): shieldBreaker marks an authored Shield-Breaker
            // Special — a valid block spends every charge; an ordinary Special
            // spends min(2, charges).
            //
            // D10 (Package 13 W1): with hasKnockbackVector the impulse is the
            // authored SIGNED vector — `knockback` is the horizontal magnitude
            // (always pushed away from the hit origin) and `knockbackVertical`
            // the Y-up vertical component, negative for a spike. Both are scaled
            // by the same weight / low-HP rule. Without it the historical scalar
            // form stands: one magnitude on both axes, the vertical biased by
            // verticalKnockbackScale.
            if (target.InvulnerabilityFrames > 0 || target.Stocks <= 0) return false;
            // V7.6 D04 (Package 11 A1b): the Defy protected-recovery gate sits
            // ABOVE every other layer. A rejected contact spends no Aegis, no
            // barrier capacity and no block charge, applies no hitstun, launch
            // or status, and grants the attacker no damage, Rally or block
            // reward. It covers every attacker, damaging status tick and
            // damaging stage hazard, and is not immunity to non-hit deaths.
            if (FighterDefenseRules.IsDefyProtected(in targetDefense)) return false;

            // V7.6 D02b (Package 11 A1b): eligible projectile immunity →
            // Temporal Aegis → HP barriers → ordinary block, stopping at the
            // first layer that fully prevents the hit. The simulation has no
            // projectile-immunity source, so the order starts at Aegis.
            //
            // D03c boundary: zero-damage warnings, non-damaging statuses and
            // harmless overlaps consume NO Aegis or barrier HP. Before this, a
            // zero-damage expiry launch or a warning pulse ate the bubble.
            // D03d: a primary throw skips Aegis AND the barrier without
            // consuming either, and keeps its normal damage and launch.
            int resolvedIncoming = damage > 0 ? damage : 0;
            if (resolvedIncoming > 0 && !bypassesFiniteShields) {
                if (targetDefense.AegisActive != 0) {
                    // D03e: Aegis prevents the absorbed contact's damage,
                    // hitstun, knockback and attached status outright.
                    targetDefense.AegisActive = 0;
                    return false;
                }
                if (targetDefense.BarrierPoints > 0) {
                    int absorbed = targetDefense.BarrierPoints < resolvedIncoming
                        ? targetDefense.BarrierPoints
                        : resolvedIncoming;
                    targetDefense.BarrierPoints -= absorbed;
                    if (targetDefense.BarrierPoints <= 0) FighterDefenseRules.ClearBarrier(ref targetDefense);
                    damage = resolvedIncoming - absorbed;
                    // D03a: a full absorb — including one that exactly depletes
                    // the barrier — suppresses the hit's attached effects too.
                    if (damage <= 0) return false;
                }
            }

            // Pure tick/status pulses (zone effects) carry no impulse: they bypass
            // the front-facing shield and must not interrupt movement or zero the
            // target's velocity.
            bool hasImpulse = knockback > FP64.Zero
                || (hasKnockbackVector && knockbackVertical != FP64.Zero);
            bool carriesImpulse = hasImpulse || hitstunFrames > 0;
            // The absorb requires the real grounded stance (mirrors Story's
            // Blocking state): no blocking while airborne, mid-swing, mid
            // roll, or inside hitstun/daze.
            bool targetBlocking = carriesImpulse
                && FighterBasicAttackRules.IsBlockStance(in target, in targetRuntime, in targetVerb);
            bool hitInFront = target.FacingRight != 0
                ? hitOriginX >= target.Position.x
                : hitOriginX <= target.Position.x;
            if (targetBlocking && hitInFront && attackClass != UltimateAttackClass && target.BlockCharges > 0) {
                // blockChargeCost > 0 overrides the class default (a Story
                // Guard-Crush costs 2). A01 (Package 13 W1): an ordinary
                // Special spends min(2, charges) — a full shield keeps one —
                // and only an authored Shield-Breaker (Divine Piercing, The
                // Emancipator, Splitting Strike) spends every charge.
                int cost = blockChargeCost > 0
                    ? blockChargeCost
                    : attackClass == SpecialAttackClass
                        ? shieldBreaker
                            ? target.BlockCharges
                            : target.BlockCharges < FTT.Combat.BasicComboRules.SpecialBlockChargeCost
                                ? target.BlockCharges
                                : FTT.Combat.BasicComboRules.SpecialBlockChargeCost
                        : 1;
                target.BlockCharges -= cost;
                // A spent charge re-arms the regeneration interval, as Story does.
                targetRuntime.BlockRegenFrames = FTT.Combat.BasicComboRules.BlockChargeRegenFrames;
                if (target.BlockCharges <= 0) {
                    // V7.3 shatter: the 5 s lockout arms (no stance, regen held —
                    // charge #1 at shatter + 480f) and the blocked-hit hitstop is
                    // replaced by the longer shared shatter freeze.
                    target.BlockCharges = 0;
                    target.DazeFrames = 60;
                    targetVerb.BlockLockoutFrames = FTT.Combat.BasicComboRules.BlockShatterLockoutFrames;
                    targetVerb.ShieldStunFrames = 0;
                    // Guard-break push (Low-item decision 2026-09-26, Package
                    // 12 W3): the fixed (2.0, -1.0) Y-down vector — (±2, +1)
                    // in this Y-up space — X away from the ATTACKER's side,
                    // unscaled and assigned. Because a block needs the contact
                    // in front, this equals the old facing-derived push on
                    // every reachable input; it now reads the attacker side.
                    target.Velocity.x = GuardBreakPushSign(target.Position.x, hitOriginX, target.FacingRight != 0) > 0
                        ? GuardBreakPushX
                        : -GuardBreakPushX;
                    target.Velocity.y = GuardBreakPushYUp;
                    if (appliesHitstop) {
                        FighterVerbRules.ApplyHitstop(
                            ref attackerVerb, ref targetVerb, FTT.Combat.BasicComboRules.ShatterFreezeFrames);
                    }
                    return false;
                }
                // V7.3 shieldstun: a non-shatter blocked hit locks the blocker
                // into the stance — no grab, roll, jump, drop-through, or release.
                targetVerb.ShieldStunFrames = FTT.Combat.BasicComboRules.ShieldstunFrames;
                // V7 hitstop: blocked hits freeze both parties for a flat window.
                if (appliesHitstop) {
                    FighterVerbRules.ApplyHitstop(
                        ref attackerVerb, ref targetVerb, FTT.Combat.BasicComboRules.BlockedHitstopFrames);
                }
                return false;
            }

            int resolvedDamage = damage > 0 ? damage : 0;
            if (targetRuntime.DamageStatusType == (int)StatusType.RadiantBurn) {
                FP64 multiplier = FP64.One + targetRuntime.DamageStatusIntensity / FP64.FromInt(4);
                long numerator = (long)resolvedDamage * multiplier.RawValue + FP64.One.RawValue / 2;
                resolvedDamage = (int)(numerator / FP64.One.RawValue);
            }

            int previousHP = target.CurrentHP;
            int remainingHP = target.CurrentHP - resolvedDamage;
            target.CurrentHP = remainingHP > 0 ? remainingHP : 0;

            // Defy History (V7.1): a lethal *hit* against a full meter does not
            // KO — the meter shatters to 0 and the fighter survives at 1 HP.
            // Once per match (Sudden Death pre-marks it used on both fighters).
            bool defied = false;
            if (target.CurrentHP <= 0
                && targetVerb.DefyHistoryUsed == 0
                // V7.6 F22: "the next actual death decides the match; there is
                // no Defy survival proc" in Sudden Death. The BAR is a separate
                // read from the spent flag (F13), so a fighter who never used
                // Defy still shows Unavailable rather than Spent.
                && targetDefense.DefyBarred == 0
                && target.Influence >= MaxInfluence) {
                defied = true;
                targetVerb.DefyHistoryUsed = 1;
                target.Influence = FP64.Zero;
                target.CurrentHP = 1;
                // The saved life reads as a hard moment: an extended freeze.
                FighterVerbRules.ApplyHitstop(ref attackerVerb, ref targetVerb, 12);
                // V7.6 D04 (Package 11 A1b): protected recovery. The survivor is
                // RELEASED from the triggering hit — it may not be launched or
                // stunned by the very blow it defied — and from any capture,
                // then is invulnerable through the presentation and for 60
                // active ticks once normal control resumes.
                FighterDefenseRules.BeginDefyProtection(ref targetDefense, target.PlayerID);
                target.HitstunFrames = 0;
                target.DazeFrames = 0;
                targetVerb.PendingLaunchActive = 0;
                targetVerb.Tumble = 0;
                targetVerb.ShieldStunFrames = 0;
                targetVerb.BeingHeld = 0;
                targetVerb.GrabPhase = 0;
                targetVerb.GrabPhaseFrames = 0;
                // The defied hit contributes no forced motion and imposes no new
                // attached control/status effect. Position is kept as it stands.
                knockback = FP64.Zero;
                hitstunFrames = 0;
                statusType = (int)StatusType.None;
                statusFrames = 0;
            }

            int actualDamage = previousHP - target.CurrentHP;
            if (creditInfluence) {
                attacker.Influence = FP64.Min(MaxInfluence, attacker.Influence + FP64.FromInt(actualDamage));
            }

            // Rally / Desperation Resonance (V7.1): a fraction of every hit taken
            // becomes a briefly recoverable echo — 20% at full health sliding to
            // 50% near death (evaluated after this hit's damage), ×1.5 capped at
            // 0.60 during Overtime. Blocked hits never reach here; each accrual
            // restarts the 150-frame linear drain. Victim meter-from-damage
            // accrues only on the permanent (non-echo) portion at hit time — the
            // echo portion's meter accrues if and when it drains (TickCounters).
            // A defied hit generates neither echo nor victim meter: the
            // shattered meter consumed the entire blow, and "shatters to 0"
            // must read as exactly that on the HUD.
            FP64 echoAmount = FP64.Zero;
            if (!defied && actualDamage > 0 && target.CurrentHP > 0 && target.MaxHP > 0) {
                FP64 missing = FP64.FromInt(target.MaxHP - target.CurrentHP) / FP64.FromInt(target.MaxHP);
                FP64 echoFraction = EchoFractionBase + EchoFractionSlope * missing;
                if (targetVerb.OvertimeActive == 1) {
                    echoFraction = FP64.Min(OvertimeEchoCap, echoFraction * OvertimeEchoMultiplier);
                }
                echoAmount = FP64.FromInt(actualDamage) * echoFraction;
                targetVerb.EchoPool += echoAmount;
                targetVerb.EchoDrainPerFrame =
                    targetVerb.EchoPool / FP64.FromInt(FTT.Combat.BasicComboRules.EchoDrainFrames);
            }
            if (!defied) {
                target.Influence = FP64.Min(
                    MaxInfluence,
                    target.Influence + (FP64.FromInt(actualDamage) - echoAmount) / FP64.FromInt(4));
            }

            // Rally reclaim (V7.3): a connecting *direct* hit (never a construct
            // tick or hazard) converts the attacker's remaining Echo Pool back
            // into real HP, capped at the reclaiming hit's own damage ×
            // RallyReclaimDamageMultiplier. The unreclaimed remainder persists
            // and keeps draining at the unchanged per-frame rate; the drain only
            // zeroes when the pool empties. Reclaimed HP grants no meter to
            // either player.
            // V7.6 D03f/D03g (Package 11 A1b): the reclaim is
            // min(pool, creditedDamage × 2.0, missingHP), and ONLY the HP
            // actually healed leaves the pool. Before this the full capped
            // amount was debited even when the heal was clamped at MaxHP, so a
            // full-health attacker burned their whole pool for nothing.
            int missingHP = attacker.MaxHP - attacker.CurrentHP;
            if (collectsEcho && attackClass != HazardAttackClass
                && attackerVerb.EchoPool > FP64.Zero && actualDamage > 0
                && attacker.MaxHP > 0 && missingHP > 0) {
                FP64 reclaimAmount = FP64.Min(
                    FP64.Min(
                        attackerVerb.EchoPool,
                        FP64.FromInt(actualDamage) * RallyReclaimMultiplier),
                    FP64.FromInt(missingHP));
                long reclaimRaw = (reclaimAmount.RawValue + FP64.One.RawValue / 2) / FP64.One.RawValue;
                int reclaim = (int)reclaimRaw;
                if (reclaim > missingHP) reclaim = missingHP;
                if (reclaim > 0) {
                    attacker.CurrentHP += reclaim;
                    // Debit exactly what became HP, never more than the pool held.
                    attackerVerb.EchoPool -= FP64.Min(attackerVerb.EchoPool, FP64.FromInt(reclaim));
                    if (attackerVerb.EchoPool <= FP64.Zero) {
                        attackerVerb.EchoPool = FP64.Zero;
                        attackerVerb.EchoDrainPerFrame = FP64.Zero;
                    }
                }
            }

            if (carriesImpulse) {
                FighterUniversalMovementRules.Cancel(ref targetRuntime);
            }

			if (carriesImpulse && (target.HyperArmorFrames <= 0 || attackClass == UltimateAttackClass)) {
                if (hasImpulse) {
                    // Gameplay-feel plan §2.5 — low-health knockback scaling. The
                    // impulse is multiplied by 1 + the victim's missing-HP fraction
                    // measured *after* this hit's damage, a linear 1x at full HP to
                    // 2x at 0 HP. This is the one chokepoint every Fighter-side
                    // source funnels through (basics, specials, ultimates,
                    // projectiles, zones, constructs, and — via
                    // ApplyEnvironmentHit — stage hazards), so the rule needs no
                    // second copy. Story mirrors it in DamageCalculator.
                    FP64 force = ScaleByMissingHP(knockback, in target) / (FP64.One + target.Weight);
                    // Launcher-class impulses (Nassau's mortar) bias the impulse
                    // upward; everything else keeps the symmetric 1:1 pulse.
                    FP64 verticalScale = verticalKnockbackScale > FP64.Zero ? verticalKnockbackScale : FP64.One;
                    // D10: the signed vertical of an authored vector, scaled by
                    // the same rule; a negative value is a spike.
                    FP64 verticalForce = hasKnockbackVector
                        ? ScaleByMissingHP(knockbackVertical, in target) / (FP64.One + target.Weight)
                        : force * verticalScale;
                    target.Velocity.x = hitOriginX <= target.Position.x ? force : -force;
                    if (launches) {
                        target.Velocity.y = verticalForce;
                        target.IsGrounded = 0;
                        // DI (V7): a launching hit's direction is finalized when the
                        // victim's hitstop ends, bent by their held direction.
                        // M05 (Package 12 W3b): only an authored launcher reaches
                        // here, so only it grants DI, tumble and the landing tech.
                        if (hitstunFrames > 0 && target.CurrentHP > 0) {
                            FighterVerbRules.StashPendingLaunch(ref targetVerb, in target.Velocity);
                        }
                    } else if (target.IsGrounded != 0) {
                        // M05: a non-launching hit on a grounded victim is
                        // grounded knockback — a horizontal slide that keeps
                        // the victim on the floor, with no tumble and no DI. Its
                        // hitstun is ordinary grounded hitstun, which is what
                        // re-opens the hit-2 block escape (M06).
                        target.Velocity.y = FP64.Zero;
                    } else {
                        // M05: a non-launcher on an airborne victim keeps its
                        // authored vector, but it is not a launch — no tumble,
                        // no DI, no tech, and so no knockdown on landing.
                        target.Velocity.y = verticalForce;
                    }
                }
                // A zero-knockback hit with hitstun (a construct arc/bite) stuns
                // without replacing the victim's velocity with a zero vector.
                target.HitstunFrames = hitstunFrames;
                if (target.HitstunFrames > 0) {
                    // Real hitstun replaces the stance outright: shieldstun ends,
                    // and hit 1 of the string arms the V7.3 block-cancel gate
                    // (only after hit two connects may Block escape hitstun).
                    targetVerb.ShieldStunFrames = 0;
                    targetVerb.HitstunBlockCancelBlocked = blockCancelableHitstun ? 0 : 1;
                }
            }

            // V7 universal hitstop, scaled by the damage that landed. Lethal hits
            // skip it — the KO presentation owns that moment. Construct, zone,
            // and hazard ticks are exempt (V7.3: only direct player-authored
            // hits carry hitstop). PROVISIONAL-HITSTOP-WEIGHT (2026-10-04 feel
            // pass, F6): an authored launcher adds the shared launch bonus — the
            // one integer rule Story's PlayerController also calls.
            if (appliesHitstop && target.CurrentHP > 0 && actualDamage > 0) {
                FighterVerbRules.ApplyHitstop(
                    ref attackerVerb, ref targetVerb,
                    FTT.Combat.BasicComboRules.HitstopFrames(actualDamage, launches));
            }
            // A launching hit whose victim ended up with no hitstop (an exempt
            // source) resolves its DI immediately — a stashed launch must never
            // sit armed waiting to replay under a later hit's hitstop.
            if (targetVerb.HitstopFrames == 0) {
                FighterVerbRules.ResolvePendingLaunch(ref target, in targetRuntime, ref targetVerb);
            }

            ApplyStatus(ref target, ref targetRuntime, statusType, statusFrames, statusIntensity);
            // Being hit into hitstun cancels the victim's swing and resets their
            // chain in both modes (design 752). Hyper-armored hits carry no
            // hitstun and leave the string running, matching Story.
            if (target.HitstunFrames > 0) {
                FighterBasicAttackRules.CancelString(ref targetRuntime);
            }
            if (target.CurrentHP <= 0) {
                FighterSimulationRules.ApplyStockLoss(ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning);
            }
            return true;
        }

        /// <summary>
        /// V7.3 unattributed-damage chokepoint — DoT ticks (venom). Replicates
        /// the victim-side pipeline of <see cref="ApplyFighterHit"/> with no
        /// attacker: Defy History first (a lethal tick can no longer kill
        /// through a full meter), then Rally echo accrual, victim meter on the
        /// permanent portion, and finally the stock loss. No attacker credit,
        /// no reclaim, no hitstop, no impulse, no block interaction.
        /// </summary>
        public static void ApplyUnattributedDamage(
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            ref FighterVerbComponent targetVerb,
            ref FighterDefenseComponent targetDefense,
            in FighterTuningComponent targetTuning,
            int damage) {
            if (damage <= 0 || target.Stocks <= 0) return;
            // D04: the protected window rejects damaging status ticks too, and
            // a rejected tick is discarded rather than banked.
            if (FighterDefenseRules.IsDefyProtected(in targetDefense)) return;
            // D03c: an already-attached DoT's damaging ticks are eligible for
            // Aegis and the HP barrier, in the D02b order. An absorbed tick
            // still counts as that scheduled occurrence — no cleanse, no
            // duration refresh, no retry.
            if (targetDefense.AegisActive != 0) {
                targetDefense.AegisActive = 0;
                return;
            }
            if (targetDefense.BarrierPoints > 0) {
                int absorbedTick = targetDefense.BarrierPoints < damage ? targetDefense.BarrierPoints : damage;
                targetDefense.BarrierPoints -= absorbedTick;
                if (targetDefense.BarrierPoints <= 0) FighterDefenseRules.ClearBarrier(ref targetDefense);
                damage -= absorbedTick;
                if (damage <= 0) return;
            }

            int previousHP = target.CurrentHP;
            int remainingHP = target.CurrentHP - damage;
            target.CurrentHP = remainingHP > 0 ? remainingHP : 0;

            bool defied = false;
            if (target.CurrentHP <= 0
                && targetVerb.DefyHistoryUsed == 0
                && target.Influence >= MaxInfluence) {
                defied = true;
                targetVerb.DefyHistoryUsed = 1;
                target.Influence = FP64.Zero;
                target.CurrentHP = 1;
                FighterDefenseRules.BeginDefyProtection(ref targetDefense, target.PlayerID);
                target.HitstunFrames = 0;
                targetVerb.BeingHeld = 0;
            }

            int actualDamage = previousHP - target.CurrentHP;
            FP64 echoAmount = FP64.Zero;
            if (!defied && actualDamage > 0 && target.CurrentHP > 0 && target.MaxHP > 0) {
                FP64 missing = FP64.FromInt(target.MaxHP - target.CurrentHP) / FP64.FromInt(target.MaxHP);
                FP64 echoFraction = EchoFractionBase + EchoFractionSlope * missing;
                if (targetVerb.OvertimeActive == 1) {
                    echoFraction = FP64.Min(OvertimeEchoCap, echoFraction * OvertimeEchoMultiplier);
                }
                echoAmount = FP64.FromInt(actualDamage) * echoFraction;
                targetVerb.EchoPool += echoAmount;
                targetVerb.EchoDrainPerFrame =
                    targetVerb.EchoPool / FP64.FromInt(FTT.Combat.BasicComboRules.EchoDrainFrames);
            }
            if (!defied) {
                target.Influence = FP64.Min(
                    MaxInfluence,
                    target.Influence + (FP64.FromInt(actualDamage) - echoAmount) / FP64.FromInt(4));
            }

            if (target.CurrentHP <= 0) {
                FighterSimulationRules.ApplyStockLoss(ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning);
            }
        }

        /// <summary>
        /// The fixed-point half of <c>BasicComboRules.LowHealthKnockbackScale</c>:
        /// <c>knockback * (2 * MaxHP - CurrentHP) / MaxHP</c>. The ratio is
        /// formed first so the intermediate never leaves the [1, 2] band, and
        /// the whole computation stays in FP64 — no float math on the
        /// deterministic path.
        /// </summary>
        internal static FP64 ScaleByMissingHP(FP64 knockback, in FighterStateComponent target) {
            int maxHP = target.MaxHP;
            if (maxHP <= 0) return knockback;
            int currentHP = target.CurrentHP < 0 ? 0 : target.CurrentHP > maxHP ? maxHP : target.CurrentHP;
            FP64 scale = FP64.FromInt(2 * maxHP - currentHP) / FP64.FromInt(maxHP);
            return knockback * scale;
        }

        public static bool ApplyEnvironmentHit(
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            ref FighterVerbComponent targetVerb,
            ref FighterDefenseComponent targetDefense,
            in FighterTuningComponent targetTuning,
            int damage,
            FP64 knockback,
            int hitstunFrames,
            FP64 hitOriginX) => ApplyEnvironmentHit(
                ref target,
                ref targetRuntime,
                ref targetVerb,
                ref targetDefense,
                in targetTuning,
                damage,
                knockback,
                hitstunFrames,
                hitOriginX,
                (int)StatusType.None,
                0,
                FP64.One);

        /// <summary>
        /// Environment/hazard damage. Per `design-godot.md` §10 ("Block
        /// Compatibility"), a hazard tick is treated as a basic attack: an active
        /// front-facing block absorbs it for exactly one block charge, which is
        /// why <c>blockChargeCost</c> is pinned to 1 here rather than left to the
        /// attack-class default.
        /// </summary>
        public static bool ApplyEnvironmentHit(
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            ref FighterVerbComponent targetVerb,
            ref FighterDefenseComponent targetDefense,
            in FighterTuningComponent targetTuning,
            int damage,
            FP64 knockback,
            int hitstunFrames,
            FP64 hitOriginX,
            int statusType,
            int statusFrames,
            FP64 statusIntensity,
            FP64 verticalKnockbackScale = default) {
            FighterStateComponent environment = default;
            FighterRuntimeComponent environmentRuntime = default;
            FighterVerbComponent environmentVerb = default;
            return ApplyFighterHit(
                ref environment,
                ref environmentRuntime,
                ref environmentVerb,
                ref target,
                ref targetRuntime,
                ref targetVerb,
                ref targetDefense,
                in targetTuning,
                HazardAttackClass,
                damage,
                knockback,
                hitstunFrames,
                statusType,
                statusFrames,
                statusIntensity,
                hitOriginX,
                false,
                HazardBlockChargeCost,
                verticalKnockbackScale,
                collectsEcho: false,
                // V7.3: hazard ticks (and their blocked absorbs) carry no
                // hitstop — only direct player-authored hits freeze.
                appliesHitstop: false);
        }

        /// <summary>
        /// The simulation's single status chokepoint. Internal rather than private
        /// so FighterStatusSlotTests and SuppressionTests can drive it directly —
        /// the stronger-wins comparison and the Suppression refusal are contract,
        /// not incidental behaviour.
        /// </summary>
        internal static void ApplyStatus(
            ref FighterStateComponent targetState,
            ref FighterRuntimeComponent targetRuntime,
            int statusType,
            int statusFrames,
            FP64 statusIntensity) {
            if (statusType == (int)StatusType.None || statusFrames <= 0) return;
            // V7.6 (Package 11 A1): Suppression is a STORY-ONLY status. The
            // design is explicit — "Fighter Mode is untouched: no Suppression
            // source exists in the sim, and none may be added without a separate
            // ruling." An authored .tres carrying it is refused here so it can
            // never enter deterministic state. Pinned by FighterStatusSlotTests.
            if (statusType == (int)StatusType.Suppression) return;
            FP64 intensity = statusIntensity > FP64.Zero ? statusIntensity : FP64.One;
            // V7 two-slot rule (mirrors StatusController): a damaging status and a
            // control status coexist; a new application competes only with its own
            // slot. V7.6 replaces newest-wins with STRONGER-WINS, decided by
            // StatusRouting.ShouldReplaceRaw — an exact int64 product of the FP64
            // raw intensity and the integer frame count, so it is bit-reproducible
            // across a rollback and needs no FP64 division.
            if (FTT.Combat.StatusRouting.SlotOf(statusType) == FTT.Combat.StatusSlot.Damage) {
                if (!FTT.Combat.StatusRouting.ShouldReplaceRaw(
                        targetRuntime.DamageStatusType,
                        targetRuntime.DamageStatusIntensity.RawValue,
                        targetRuntime.DamageStatusFrames,
                        statusType, intensity.RawValue, statusFrames)) return;
                targetRuntime.DamageStatusType = statusType;
                targetRuntime.DamageStatusFrames = statusFrames;
                targetRuntime.DamageStatusIntensity = intensity;
                targetRuntime.StatusTickFrames = statusType == (int)StatusType.Venom ? 60 : 0;
                return;
            }
            if (!FTT.Combat.StatusRouting.ShouldReplaceRaw(
                    targetRuntime.StatusType,
                    targetRuntime.StatusIntensity.RawValue,
                    targetRuntime.StatusFrames,
                    statusType, intensity.RawValue, statusFrames)) return;
            targetRuntime.StatusType = statusType;
            targetRuntime.StatusFrames = statusFrames;
            targetRuntime.StatusIntensity = intensity;
            if (statusType == (int)StatusType.StaticCharge) {
                targetState.HitstunFrames = targetState.HitstunFrames > statusFrames
                    ? targetState.HitstunFrames
                    : statusFrames;
            }
        }
    }

    public sealed class FighterAbilityEntitySystem : ISystem {
        private const int SpecialOneButton = 1 << 3;
        private const int SpecialTwoButton = 1 << 4;
        private const int MovementButton = 1 << 5;
        private const int MeleeExecutionType = 0;
        private const int ProjectileExecutionType = 1;
        private const int AreaExecutionType = 2;
        private const int PersistentExecutionType = 3;

        /// <summary>
        /// Package 13 W7b: the fighters' projected hit contracts and the stage
        /// geometry — static match configuration, never snapshot state. The
        /// ground waves read the surface they ride, the kit hits read their
        /// authored per-hit damage, hitbox and knockback vector.
        /// </summary>
        private readonly FighterHitContractTable _contracts;
        private readonly FighterStageGeometry _geometry;

        public FighterAbilityEntitySystem(FighterHitContractTable contracts = null, FighterStageGeometry geometry = null) {
            _contracts = contracts ?? FighterHitContractTable.Default;
            _geometry = geometry ?? FighterStageGeometry.Default;
        }

        public void Update(ref Frame frame) {
            ref readonly FighterMatchComponent match = ref frame.GetReadOnlySingleton<FighterMatchComponent>();
            if (match.MatchState != 1) return;

            // Package 13 W7b: Divine Piercing's thrusts and Rail Charge's ram
            // resolve on the lunge/charge frames the movement system just
            // advanced, before the per-fighter combat gates below.
            ResolveReachKitHits(ref frame);

            // Package 13 W7a (L03/D14): Leonardo's once-per-flight glide bolt.
            // Read before the combat gates (the basic press also swings, the
            // way Story's glide command sits beside the aerial string).
            ResolveGlideBolts(ref frame);

            var filter = frame.Filter<
                FighterStateComponent,
                FighterRuntimeComponent,
                FighterTuningComponent,
                FighterAbilityModeComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(entity);
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(entity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(entity);
                ref readonly FighterAbilityModeComponent modes = ref frame.GetReadOnly<FighterAbilityModeComponent>(entity);
                ref readonly FighterVerbComponent verbState = ref frame.GetReadOnly<FighterVerbComponent>(entity);
                if (fighter.Stocks <= 0
                    || fighter.HitstunFrames > 0
                    || fighter.DazeFrames > 0
                    // V7.1 hitstop / V7.2 grabs: a frozen, grabbing, or held
                    // fighter takes no ability action (their button presses are
                    // simply not consumed).
                    || verbState.HitstopFrames > 0
                    || FighterGrabRules.IsBusy(in verbState)
                    // A hanging fighter has no specials and no movement ability
                    // (§2.11) — the hang suppresses ability intent the way
                    // hitstun does.
                    || FighterLedgeRules.IsHanging(in runtime)
                    || FighterUniversalMovementRules.IsCombatLocked(in runtime)) continue;

                // Story's Blocking state ignores ability inputs entirely; specials
                // remain usable mid-swing because they cancel the basic string
                // (design 1051 — resolved below via the cooldown edge).
                bool blockStance = FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verbState);
                if (blockStance) continue;
                int specialOneCooldownBefore = runtime.SpecialOneCooldownFrames;
                int specialTwoCooldownBefore = runtime.SpecialTwoCooldownFrames;

                // Package 13 W5: Tubman's Foresight is a counter stance, not a
                // range-gated melee intent. It is accepted whether or not the
                // opponent is near (arming the cooldown here keeps the combat
                // system's melee intent from also firing), and component 322
                // runs the 4 / 20 / 24 stance (FighterForesightRules).
                if (fighter.CharacterID == (int)FighterCharacterID.Tubman
                    && (runtime.PressedButtons & SpecialTwoButton) != 0
                    && runtime.SpecialTwoCooldownFrames <= 0) {
                    runtime.SpecialTwoCooldownFrames = PositiveCooldown(tuning.SpecialTwoCooldownFrames);
                    FighterForesightRules.Begin(ref frame.Get<FighterCounterComponent>(entity));
                }

                // Package 13 W7b (J01): Divine Piercing is no longer a
                // range-gated melee intent that thrusts in place. On press it
                // lunges ~3 units forward across its active frames, and its
                // thrusts land along the way (ResolveReachKitHits). It fires
                // whether or not the opponent is near — it is her gap-closer.
                // Keyed on the authored Melee execution (her Divine Piercing), so a
                // synthetic loadout that gives the slot another shape keeps it.
                if (fighter.CharacterID == (int)FighterCharacterID.Joan
                    && modes.SpecialTwoExecutionType == MeleeExecutionType
                    && (runtime.PressedButtons & SpecialTwoButton) != 0
                    && runtime.SpecialTwoCooldownFrames <= 0) {
                    runtime.SpecialTwoCooldownFrames = PositiveCooldown(tuning.SpecialTwoCooldownFrames);
                    FighterAbilityHitData piercing = _contracts.For(fighter.PlayerID, FighterHitContractTable.SlotSpecialTwo);
                    FighterKitMotion.StartPiercingLunge(
                        ref runtime, fighter.FacingRight != 0 ? 1 : -1, piercing.ActiveFrames);
                }

                if ((runtime.PressedButtons & SpecialOneButton) != 0 && runtime.SpecialOneCooldownFrames <= 0) {
                    if (modes.SpecialOneExecutionType == ProjectileExecutionType) {
                        SpawnSpecialProjectile(
                            ref frame, in fighter, 1, tuning.SpecialOneDamage, tuning.SpecialOneKnockback,
                            tuning.SpecialOneStatusType, tuning.SpecialOneStatusFrames,
                            tuning.SpecialOneStatusIntensity, modes.SpecialOneProjectileLifetimeFrames,
                            modes.SpecialOneProjectileSpeed);
                        runtime.SpecialOneCooldownFrames = PositiveCooldown(tuning.SpecialOneCooldownFrames);
                    } else if (modes.SpecialOneExecutionType == PersistentExecutionType) {
                        SpawnPersistent(
                            ref frame, in fighter, modes.SpecialOnePersistentTypeID,
                            modes.SpecialOneMaxActiveObjects, modes.SpecialOnePersistentLifetimeFrames,
                            tuning.SpecialOneDamage, tuning.SpecialOneKnockback,
                            tuning.SpecialOneStatusType, tuning.SpecialOneStatusFrames);
                        runtime.SpecialOneCooldownFrames = PositiveCooldown(tuning.SpecialOneCooldownFrames);
                    } else if (modes.SpecialOneExecutionType == AreaExecutionType) {
                        SpawnZone(
                            ref frame, ref fighter, 1,
                            modes.SpecialOneMaxActiveObjects, modes.SpecialOnePersistentLifetimeFrames,
                            modes.SpecialOneTickIntervalFrames, tuning.SpecialOneDamage,
                            tuning.SpecialOneStatusType, tuning.SpecialOneStatusFrames,
                            tuning.SpecialOneStatusIntensity, _geometry);
                        runtime.SpecialOneCooldownFrames = PositiveCooldown(tuning.SpecialOneCooldownFrames);
                    }
                }

                if ((runtime.PressedButtons & SpecialTwoButton) != 0 && runtime.SpecialTwoCooldownFrames <= 0) {
                    if (modes.SpecialTwoExecutionType == ProjectileExecutionType) {
                        SpawnSpecialProjectile(
                            ref frame, in fighter, 2, tuning.SpecialTwoDamage, tuning.SpecialTwoKnockback,
                            tuning.SpecialTwoStatusType, tuning.SpecialTwoStatusFrames,
                            tuning.SpecialTwoStatusIntensity, modes.SpecialTwoProjectileLifetimeFrames,
                            modes.SpecialTwoProjectileSpeed);
                        runtime.SpecialTwoCooldownFrames = PositiveCooldown(tuning.SpecialTwoCooldownFrames);
                    } else if (modes.SpecialTwoExecutionType == PersistentExecutionType) {
                        SpawnPersistent(
                            ref frame, in fighter, modes.SpecialTwoPersistentTypeID,
                            modes.SpecialTwoMaxActiveObjects, modes.SpecialTwoPersistentLifetimeFrames,
                            tuning.SpecialTwoDamage, tuning.SpecialTwoKnockback,
                            tuning.SpecialTwoStatusType, tuning.SpecialTwoStatusFrames);
                        runtime.SpecialTwoCooldownFrames = PositiveCooldown(tuning.SpecialTwoCooldownFrames);
                    } else if (modes.SpecialTwoExecutionType == AreaExecutionType) {
                        // Package 13 W7b (C01): the Sandstorm Vortex is thrown up
                        // to five units ahead and ground-snapped.
                        bool vortex = fighter.CharacterID * 10 + 2 == FighterReachKitRules.SandstormVortexZone;
                        SpawnZone(
                            ref frame, ref fighter, 2,
                            modes.SpecialTwoMaxActiveObjects, modes.SpecialTwoPersistentLifetimeFrames,
                            modes.SpecialTwoTickIntervalFrames, tuning.SpecialTwoDamage,
                            tuning.SpecialTwoStatusType, tuning.SpecialTwoStatusFrames,
                            tuning.SpecialTwoStatusIntensity, _geometry,
                            hasAnchor: vortex,
                            anchor: vortex ? FighterReachKitRules.VortexAnchor(_geometry, in fighter) : default);
                        runtime.SpecialTwoCooldownFrames = PositiveCooldown(tuning.SpecialTwoCooldownFrames);
                    }
                }

                bool armedSlotOne = runtime.SpecialOneCooldownFrames != specialOneCooldownBefore;
                bool armedSlotTwo = runtime.SpecialTwoCooldownFrames != specialTwoCooldownBefore;
                if (armedSlotOne || armedSlotTwo) {
                    // An executed special cancels an in-progress basic and resets
                    // the chain, matching Story and the melee-intent path.
                    FighterBasicAttackRules.CancelString(ref runtime);
                    // A fresh cooldown cycle re-arms its Resonance Momentum refunds.
                    ref FighterVerbComponent verb = ref frame.Get<FighterVerbComponent>(entity);
                    if (armedSlotOne) verb.MomentumRefundsSlotOne = 0;
                    if (armedSlotTwo) verb.MomentumRefundsSlotTwo = 0;
                }

                // Story never polls the movement ability during a swing.
                // A05/D11 (Package 13 W1): Root is a true immobilize — it refuses
                // the movement ability here as Story's IsMovementRooted does
                // (attacks, specials and block remain). A refused press spends
                // nothing: the cooldown is only armed by ApplyMovement.
                if (runtime.AttackPhase == FighterBasicAttackRules.PhaseNone
                    && (runtime.PressedButtons & MovementButton) != 0
                    && runtime.MovementCooldownFrames <= 0
                    && runtime.StatusType != (int)StatusType.Root) {
                    ApplyMovement(ref frame, ref fighter, ref runtime, in tuning, in modes);
                }
            }
        }

        private static int PositiveCooldown(int frames) => frames > 0 ? frames : 1;

        private static void ResolveGlideBolts(ref Frame frame) {
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterAbilityModeComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.CharacterID != (int)FighterCharacterID.Leonardo || fighter.Stocks <= 0) continue;
                ref readonly FighterRuntimeComponent runtime = ref frame.GetReadOnly<FighterRuntimeComponent>(entity);
                ref readonly FighterAbilityModeComponent modes = ref frame.GetReadOnly<FighterAbilityModeComponent>(entity);
                FighterTurretRules.TryCommandGlideBolt(ref frame, in fighter, in runtime, in modes);
            }
        }

        /// <summary>
        /// Package 13 W7b: a Projectile-execution Special. The three ground
        /// waves (Righteous Smite, The Emancipator, Fortissimo Wave) ride the
        /// surface under the caster; the Requiem Chord's contact carries its
        /// authored per-hit damage (its burst pulses carry the rest); everything
        /// else is the ordinary flat shot.
        /// </summary>
        private void SpawnSpecialProjectile(
            ref Frame frame, in FighterStateComponent fighter, int slot, int damage, FP64 knockback,
            int statusType, int statusFrames, FP64 statusIntensity, int lifetimeFrames, FP64 speed) {
            FighterAbilityHitData contract = _contracts.For(fighter.PlayerID, slot);
            if (FighterGroundWave.IsGroundWave(fighter.CharacterID, slot)) {
                FighterGroundWave.Spawn(
                    ref frame, _geometry, in fighter, slot, in contract, damage, knockback,
                    statusType, statusFrames, statusIntensity, lifetimeFrames, speed);
                return;
            }
            if (fighter.CharacterID * 10 + slot == FighterReachKitRules.RequiemChordProjectile && contract.Damage > 0) {
                damage = contract.Damage;
            }
            SpawnProjectile(
                ref frame, in fighter, slot, damage, knockback,
                statusType, statusFrames, statusIntensity, lifetimeFrames, speed);
        }

        /// <summary>
        /// Package 13 W7b. <b>Divine Piercing (J01/J03):</b> on each thrust frame
        /// of the lunge, a thrust box ahead of Joan strikes once for the authored
        /// per-hit damage — a Shield-Breaker Special; only the last thrust carries
        /// the authored knockback, so the flurry lands in full. <b>Rail Charge
        /// (LN02):</b> the first frame the contact box ahead of Lincoln overlaps
        /// the opponent, the ram deals the movement ability's authored damage with
        /// its horizontal, non-launching knockback, and the charge stops dead —
        /// travel and armor both end, blocked or not.
        /// </summary>
        private void ResolveReachKitHits(ref Frame frame) {
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(entity);
                int state = runtime.UniversalMovementState;
                if (state != FighterKitMotion.PiercingLunge && state != FighterKitMotion.RailCharge) continue;
                ref FighterStateComponent attacker = ref frame.Get<FighterStateComponent>(entity);
                ref FighterVerbComponent attackerVerb = ref frame.Get<FighterVerbComponent>(entity);
                if (attackerVerb.HitstopFrames > 0 || attacker.Stocks <= 0) continue;
                int targetPlayerID = attacker.PlayerID == 0 ? 1 : 0;
                if (!FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) continue;
                ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);

                FPVector2 center;
                FPVector2 halfExtents;
                FighterAbilityHitData contract;
                bool finalThrust = false;
                if (state == FighterKitMotion.PiercingLunge) {
                    contract = _contracts.For(attacker.PlayerID, FighterHitContractTable.SlotSpecialTwo);
                    int total = FighterKitMotion.PiercingTotalFrames(runtime.UniversalMovementDirection);
                    int elapsed = FighterKitMotion.PiercingElapsedFrames(in runtime);
                    if (!FighterReachKitRules.IsThrustFrame(elapsed, total, contract.HitCount)) continue;
                    // One resolution per thrust, however long a hitstop holds
                    // the lunge on its thrust frame. A whiff still spends it.
                    if (FighterKitMotion.PiercingThrustsResolved(runtime.UniversalMovementDirection)
                        >= FighterReachKitRules.ThrustIndex(elapsed, total, contract.HitCount)) continue;
                    FighterKitMotion.MarkPiercingThrustResolved(ref runtime);
                    finalThrust = FighterReachKitRules.IsFinalThrust(elapsed, total, contract.HitCount);
                    FighterReachKitRules.ThrustBox(
                        in attacker, FighterKitMotion.PiercingFacing(runtime.UniversalMovementDirection), in contract,
                        out center, out halfExtents);
                } else {
                    contract = _contracts.For(attacker.PlayerID, -1);
                    FighterReachKitRules.RailContactBox(
                        in attacker, FighterKitMotion.ChargeFacing(runtime.UniversalMovementDirection), out center);
                    halfExtents = FighterReachKitRules.RailContactExtents;
                }
                if (!FighterEntityQueries.Overlaps(in center, in halfExtents, in target.Position, in FighterHalfExtents)) continue;

                ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
                ref FighterVerbComponent targetVerb = ref frame.Get<FighterVerbComponent>(targetEntity);
                ref FighterDefenseComponent targetDefense = ref frame.Get<FighterDefenseComponent>(targetEntity);
                ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
                if (state == FighterKitMotion.PiercingLunge) {
                    ref readonly FighterTuningComponent attackerTuning = ref frame.GetReadOnly<FighterTuningComponent>(entity);
                    // Per-hit damage from the contract; an unprojected slot splits
                    // the folded tuning total across the thrusts.
                    int thrustDamage = contract.Damage > 0
                        ? contract.Damage
                        : attackerTuning.SpecialTwoDamage / (contract.HitCount > 0 ? contract.HitCount : 1);
                    FighterDamageRules.ApplyFighterHit(
                        ref attacker, ref runtime, ref attackerVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                        FighterDamageRules.SpecialAttackClass,
                        thrustDamage,
                        finalThrust ? contract.KnockbackX : FP64.Zero,
                        contract.HitstunFrames > 0 ? contract.HitstunFrames : 12,
                        attackerTuning.SpecialTwoStatusType,
                        attackerTuning.SpecialTwoStatusFrames,
                        attackerTuning.SpecialTwoStatusIntensity,
                        attacker.Position.x,
                        launches: finalThrust && contract.Launches,
                        shieldBreaker: contract.ShieldBreaker,
                        hasKnockbackVector: finalThrust && contract.HasKnockbackVector,
                        knockbackVertical: finalThrust ? contract.KnockbackY : FP64.Zero);
                } else {
                    FighterDamageRules.ApplyFighterHit(
                        ref attacker, ref runtime, ref attackerVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                        FighterDamageRules.SpecialAttackClass,
                        contract.Damage,
                        contract.KnockbackX,
                        contract.HitstunFrames > 0 ? contract.HitstunFrames : 12,
                        (int)StatusType.None, 0, FP64.One,
                        attacker.Position.x,
                        launches: contract.Launches,
                        hasKnockbackVector: contract.HasKnockbackVector,
                        knockbackVertical: contract.KnockbackY);
                    FighterKitMotion.StopRailCharge(ref attacker, ref runtime);
                }
            }
        }

        private static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);

        internal static void SpawnProjectile(
            ref Frame frame,
            in FighterStateComponent owner,
            int projectileTypeID,
            int damage,
            FP64 knockback,
            int statusType,
            int statusFrames,
            FP64 statusIntensity,
            int lifetimeFrames,
            FP64 speed,
            bool ultimateOrigin = false) {
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            int facing = owner.FacingRight != 0 ? 1 : -1;
            FP64 resolvedSpeed = speed > FP64.Zero ? speed : FP64.FromInt(6);
            // Package 13 W7b (M01): Mozart's Fortissimo Wave is no longer a lob —
            // it is a ground wave (FighterGroundWave), so no shot here arcs.
            EntityRef projectile = frame.CreateEntity();
            frame.Add(projectile, new FighterProjectileComponent {
                EntityID = match.NextEntityID++,
                OwnerPlayerID = owner.PlayerID,
                ProjectileTypeID = owner.CharacterID * 10 + projectileTypeID,
                LifetimeFrames = lifetimeFrames > 0 ? lifetimeFrames : 180,
                Damage = damage,
                AttackClass = FighterDamageRules.SpecialAttackClass,
                StatusType = statusType,
                StatusFrames = statusFrames,
                HitstunFrames = 18,
                // V7.6 D03h: origin rides the projectile, so a delayed Ultimate
                // shot landing long after the cinematic still earns zero meter.
                UltimateOrigin = ultimateOrigin ? 1 : 0,
                StatusIntensity = statusIntensity,
                GravityPerSecond = FP64.Zero,
                Position = owner.Position + new FPVector2(FP64.FromInt(facing), FP64.One),
                Velocity = new FPVector2(resolvedSpeed * FP64.FromInt(facing), FP64.Zero),
                HalfExtents = new FPVector2(FP64.FromDouble(0.35), FP64.FromDouble(0.35)),
                Knockback = new FPVector2(knockback, knockback)
            });
        }

        internal static void SpawnPersistent(
            ref Frame frame,
            in FighterStateComponent owner,
            int objectTypeID,
            int maxActive,
            int lifetimeFrames,
            int damage,
            FP64 knockback,
            int statusType,
            int statusFrames,
            bool hasAnchor = false,
            FPVector2 anchor = default) {
            if (objectTypeID <= 0) return;
            int deployLimit = maxActive > 0 ? maxActive : 1;
            int activeCount = 0;
            int oldestID = int.MaxValue;
            EntityRef oldest = default;
            bool foundOldest = false;
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef existing)) {
                ref readonly FighterPersistentObjectComponent persistent =
                    ref frame.GetReadOnly<FighterPersistentObjectComponent>(existing);
                if (persistent.OwnerPlayerID != owner.PlayerID || persistent.ObjectTypeID != objectTypeID) continue;
                activeCount++;
                if (persistent.EntityID < oldestID) {
                    oldestID = persistent.EntityID;
                    oldest = existing;
                    foundOldest = true;
                }
            }
            if (activeCount >= deployLimit && foundOldest) frame.DestroyEntity(oldest);

            ResolvePersistentSpec(
                objectTypeID, lifetimeFrames, damage, knockback,
                out int hp, out int resolvedLifetime, out int resolvedDamage,
                out int actionCooldown, out int remainingAttacks,
                out FP64 attackRange, out FP64 resolvedKnockback);

            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            EntityRef created = frame.CreateEntity();
            frame.Add(created, new FighterPersistentObjectComponent {
                EntityID = match.NextEntityID++,
                OwnerPlayerID = owner.PlayerID,
                ObjectTypeID = objectTypeID,
                CurrentHP = hp,
                MaxHP = hp,
                LifetimeFrames = resolvedLifetime,
                ActionCooldownFrames = actionCooldown,
                MaxDeployLimit = deployLimit,
                Damage = resolvedDamage,
                BaseActionCooldownFrames = actionCooldown,
                RemainingAttacks = remainingAttacks,
                StatusType = statusType,
                StatusFrames = statusFrames,
                AttackRange = attackRange,
                Knockback = resolvedKnockback,
                // Bottom-anchored: the fighter's grounded position is its feet
                // on the floor line, so a box centered there rendered (and read
                // as) half-buried. Raising the center by the half height sets
                // the construct's base on the ground.
                // Package 13 W7b (M04): a Sonata staff is a thin 2.0-unit
                // walkable surface whose Position IS the surface (the fighter's
                // feet land on it) — placed where the glissando ends.
                Position = objectTypeID == FighterReachKitRules.StaffPlatformTypeID
                    ? (hasAnchor ? anchor : owner.Position)
                    : (hasAnchor ? anchor : owner.Position) + new FPVector2(FP64.Zero, FP64.FromDouble(0.6)),
                HalfExtents = objectTypeID == FighterReachKitRules.StaffPlatformTypeID
                    ? FighterReachKitRules.StaffHalfExtents
                    : new FPVector2(FP64.FromDouble(0.6), FP64.FromDouble(0.6))
            });
        }

        private static readonly FP64 TeslaCoilArcRange = FP64.FromDouble(FTT.Combat.KitMotionRules.TeslaCoilArcRangeUnits);

        private static void ResolvePersistentSpec(
            int objectTypeID,
            int requestedLifetime,
            int requestedDamage,
            FP64 requestedKnockback,
            out int hp,
            out int lifetime,
            out int damage,
            out int actionCooldown,
            out int remainingAttacks,
            out FP64 attackRange,
            out FP64 knockback) {
            hp = objectTypeID == 1 ? 25 : objectTypeID == 2 ? 20 : 15;
            lifetime = requestedLifetime > 0
                ? requestedLifetime
                : objectTypeID == 1 ? 1800 : objectTypeID == 2 ? 900 : 600;
            // Type 5 (Mozart's Sonata staff platform) is a harmless marker in the
            // deterministic sim: walkable platform collision is deferred, so it
            // must never attack.
            damage = objectTypeID == 1 ? 5
                : objectTypeID == 2 ? 6
                : objectTypeID == 5 ? 0
                : requestedDamage > 0 ? requestedDamage : 4;
            // V7 tuning batch: cadence mirrors the authored
            // DamageTickIntervalFrames — coil/turret every 2 s (120), nest every
            // 1 s (60) — replacing the flat 4 s that left a lone construct
            // ignorable. (Type 4, the vine snare, is retired with Pocahontas.)
            actionCooldown = objectTypeID == 1 || objectTypeID == 2 ? 120 : 60;
            // Clockwork Turret: the authored HitCount (V7: 4 bolts, then it
            // self-destructs).
            remainingAttacks = objectTypeID == 2 ? 4 : -1;
            // Clockwork Turret (type 2) targets at the design's 30-unit range,
            // bounded by the visible arena (half-width 10 units).
            // T02 (Package 13 W7a): a Tesla coil (type 1) arcs within 4 units.
            attackRange = objectTypeID == 4 ? FP64.FromInt(2)
                : objectTypeID == 2 ? FP64.FromInt(10)
                : objectTypeID == 1 ? TeslaCoilArcRange
                : objectTypeID == 3 ? FP64.FromDouble(FTT.Combat.KitReachRules.SerpentNestWidthUnits / 2.0)
                : FP64.FromInt(5);
            // Construct attacks carry whatever the resource authors — the zeroed
            // KnockbackForce means impulse-free hits (no legacy 2-unit fallback).
            knockback = requestedKnockback > FP64.Zero ? requestedKnockback : FP64.Zero;
        }

        internal static void SpawnZone(
            ref Frame frame,
            ref FighterStateComponent owner,
            int specialSlot,
            int maxActive,
            int lifetimeFrames,
            int tickIntervalFrames,
            int damage,
            int statusType,
            int statusFrames,
            FP64 statusIntensity,
            FighterStageGeometry geometry = null,
            bool hasAnchor = false,
            FPVector2 anchor = default) {
            int zoneTypeID = owner.CharacterID * 10 + specialSlot;
            int deployLimit = maxActive > 0 ? maxActive : 1;
            int activeCount = 0;
            int oldestID = int.MaxValue;
            EntityRef oldest = default;
            bool foundOldest = false;
            var filter = frame.Filter<FighterZoneComponent>();
            while (filter.Next(out EntityRef existing)) {
                ref readonly FighterZoneComponent zone = ref frame.GetReadOnly<FighterZoneComponent>(existing);
                if (zone.OwnerPlayerID != owner.PlayerID || zone.ZoneTypeID != zoneTypeID) continue;
                activeCount++;
                if (zone.EntityID < oldestID) {
                    oldestID = zone.EntityID;
                    oldest = existing;
                    foundOldest = true;
                }
            }
            if (activeCount >= deployLimit && foundOldest) frame.DestroyEntity(oldest);

            ResolveZoneSpec(zoneTypeID, out FPVector2 halfExtents, out int grantsOwnerSpeedBonus, out bool centersOnOwner);
            // Shakespeare's Tempest (design Section 5) lifts the caster into the
            // air as the storm spawns; the storm itself only shoves the opponent
            // (see FighterZoneSystem's per-type pulse impulse).
            if (zoneTypeID == (int)FighterCharacterID.Shakespeare * 10 + 2) {
                // S02 (Package 13 W7a): lifted about 2.5 units, then a normal fall.
                owner.Velocity.y = FighterTempestRules.LiftSpeed;
                owner.IsGrounded = 0;
            }
            int resolvedTick = tickIntervalFrames > 0 ? tickIntervalFrames : 30;
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            int facing = owner.FacingRight != 0 ? 1 : -1;
            FPVector2 zonePosition = hasAnchor
                ? anchor
                : centersOnOwner
                    ? owner.Position
                    : owner.Position + new FPVector2(FP64.FromInt(facing * 2), FP64.Zero);
            // E03 (Package 13 W7a): the Relativity Rift is thrown up to 5 units
            // ahead inside the walls, ground-snapped when cast grounded.
            if (zoneTypeID == FighterRiftRules.RiftZoneTypeID) {
                zonePosition = FighterRiftRules.Placement(geometry, in owner);
            }
            EntityRef created = frame.CreateEntity();
            frame.Add(created, new FighterZoneComponent {
                EntityID = match.NextEntityID++,
                OwnerPlayerID = owner.PlayerID,
                ZoneTypeID = zoneTypeID,
                LifetimeFrames = lifetimeFrames > 0 ? lifetimeFrames : 300,
                TickIntervalFrames = resolvedTick,
                TickFramesRemaining = 1,
                Damage = damage,
                StatusType = statusType,
                StatusFrames = statusFrames,
                GrantsOwnerSpeedBonus = grantsOwnerSpeedBonus,
                StatusIntensity = statusIntensity,
                Position = zonePosition,
                HalfExtents = halfExtents
            });
        }

        /// <summary>
        /// Per-zone-identity deterministic tuning. Einstein's Relativity Rift
        /// (zone type 2) is wide and buffs the owner's movement while inside.
        /// Tesla's Lorentz Pulse is a radial burst centered on Tesla himself.
        /// Lincoln's Emancipator (zone type 31) is a wide, low forward ground wave.
        /// Cleopatra's Sandstorm Vortex is a forward zone whose pull runs in
        /// FighterZoneSystem.
        /// Shakespeare's Tempest is a wind storm centered on the caster.
        /// </summary>
        private static readonly FP64 LorentzPulseRadius = FP64.FromDouble(FTT.Combat.KitMotionRules.LorentzPulseRadiusUnits);

        private static void ResolveZoneSpec(
            int zoneTypeID,
            out FPVector2 halfExtents,
            out int grantsOwnerSpeedBonus,
            out bool centersOnOwner) {
            // Package 13 W7a: the kit radii (E03 rift 1.5, T02 Lorentz 2.5, L02
            // spiral 2.0, S02 Tempest 2.0) as square boxes — the sim's zones are
            // AABBs, the radius-equivalent of the design's circles.
            if (zoneTypeID == (int)FighterCharacterID.Einstein * 10 + 2) {
                halfExtents = new FPVector2(FighterRiftRules.Radius, FighterRiftRules.Radius);
                grantsOwnerSpeedBonus = 1;
                centersOnOwner = false;
                return;
            }
            if (zoneTypeID == (int)FighterCharacterID.Tesla * 10 + 2) {
                halfExtents = new FPVector2(LorentzPulseRadius, LorentzPulseRadius);
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            // Package 13 W7b: the Emancipator is a ground wave now (LN03) and no
            // longer spawns a zone. The Sandstorm Vortex has the C01 1.8-unit
            // radius and is placed by FighterReachKitRules.VortexAnchor.
            if (zoneTypeID == FighterReachKitRules.SandstormVortexZone) {
                halfExtents = FighterReachKitRules.VortexHalfExtents;
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = false;
                return;
            }
            // Leonardo's Golden Ratio (zone type 21): the expanding spiral is
            // approximated by its final footprint (Story max radius 120 px =
            // 2 units), centered on the cast point; the zone system applies a
            // radial knockback pulse when it expires.
            if (zoneTypeID == (int)FighterCharacterID.Leonardo * 10 + 1) {
                halfExtents = new FPVector2(FP64.FromDouble(2.0), FP64.FromDouble(2.0));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            if (zoneTypeID == (int)FighterCharacterID.Shakespeare * 10 + 2) {
                halfExtents = new FPVector2(FighterTempestRules.Radius, FighterTempestRules.Radius);
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            // Joan's Grand Crusade (ultimate-slot zone type 13): the cavalry
            // charge lane — wide and forward-offset so the stampede tramples
            // everything ahead of Joan, well beyond melee range.
            if (zoneTypeID == (int)FighterCharacterID.Joan * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromDouble(3.5), FP64.One);
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = false;
                return;
            }
            // Einstein's Cosmological Constant (ultimate zone type 3): the
            // screen-clearing micro black hole is a wide singularity centered on
            // the caster; FighterZoneSystem pulls the opponent toward it and
            // fires the final launch when it expires.
            if (zoneTypeID == (int)FighterCharacterID.Einstein * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromInt(6), FP64.FromInt(3));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            // Cleopatra's Wrath of the Nile (zone type 43): the ultimate
            // sandstorm engulfs the whole arena (half-width 10 units) around
            // Cleopatra, tall enough to catch airborne opponents.
            if (zoneTypeID == (int)FighterCharacterID.Cleopatra * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromInt(10), FP64.FromInt(4));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            // Leonardo's Vitruvian Matrix (ultimate zone type 23): the thrown
            // trap sphere resolves as a wide circle 2 units ahead of the caster
            // (radius 2.5 units = the Story matrix's 150 px), so the trap-and-
            // bombard ultimate lands well beyond melee range.
            if (zoneTypeID == (int)FighterCharacterID.Leonardo * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromDouble(2.5), FP64.FromDouble(2.0));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = false;
                return;
            }
            // Lincoln's Union Indestructible (ultimate-slot zone type 33): the
            // split-rail fence pen raised in front of Lincoln — wide and tall
            // enough to hold the trapped opponent through the smash sequence.
            if (zoneTypeID == (int)FighterCharacterID.Lincoln * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromDouble(2.5), FP64.FromDouble(1.25));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = false;
                return;
            }
            // Tesla's Wardenclyffe Cataclysm (ultimate zone type 53): the massive
            // alternating-current column centered on Tesla — wider than melee
            // range so the ultimate connects beyond arm's reach, and tall so
            // launched targets stay inside; FighterZoneSystem drags the opponent
            // toward the column center while it lives.
            if (zoneTypeID == (int)FighterCharacterID.Tesla * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromDouble(2.5), FP64.FromDouble(3.0));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            // Zone type 83 was Pocahontas's Tidewater Tempest — retired with her
            // in the Package 13 roster swap (D3) and never reused.
            // Tubman's The Freedom Line (ultimate zone type 93, Package 13 W5):
            // the spectral train of lantern light. A02 re-anchors the zone on the
            // held victim, so the footprint is the train's carriage around them
            // (a 6 x 2-unit box), not a stage sweep.
            if (zoneTypeID == (int)FighterCharacterID.Tubman * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromInt(3), FP64.One);
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            // Mozart's Symphony of Sorrow (ultimate zone type 73): piano-key
            // meteors rain across the entire stage, so the bombardment footprint
            // spans the full 20-unit arena width and reaches jump height —
            // there is nowhere on stage to walk out of it.
            if (zoneTypeID == (int)FighterCharacterID.Mozart * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromInt(10), FP64.FromInt(6));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            // Shakespeare's All the World's a Stage ultimate (zone type 63): the
            // summoned Globe Theatre set is a wide owner-centered stage (12 x 4
            // units, mirroring the authored 720 x 240 px HitboxSize) on which the
            // tragic phantoms strike; the strike cadence/count come from
            // FighterUltimateRules.
            if (zoneTypeID == (int)FighterCharacterID.Shakespeare * 10 + FighterUltimateRules.UltimateSlot) {
                halfExtents = new FPVector2(FP64.FromDouble(6.0), FP64.FromDouble(2.0));
                grantsOwnerSpeedBonus = 0;
                centersOnOwner = true;
                return;
            }
            halfExtents = new FPVector2(FP64.FromDouble(1.5), FP64.One);
            grantsOwnerSpeedBonus = 0;
            centersOnOwner = false;
        }

        private void ApplyMovement(
            ref Frame frame,
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning,
            in FighterAbilityModeComponent modes) {
            runtime.MovementCooldownFrames = PositiveCooldown(modes.MovementCooldownFrames);
            int facing = fighter.FacingRight != 0 ? 1 : -1;
            FP64 distance = modes.MovementDistance > FP64.Zero ? modes.MovementDistance : FP64.FromInt(2);
            FP64 speed = modes.MovementSpeed > FP64.Zero ? modes.MovementSpeed : FP64.FromInt(8);

            if (fighter.CharacterID == (int)FighterCharacterID.Tubman) {
                // Package 13 W5: North Star Leap — a guided leap of the loadout's
                // MovementDistance (4 units) in any of eight directions over its
                // MovementDurationFrames (18), then a settle frame; the extended
                // ledge snap rides the kit phase (FighterKitMotion). Its authored
                // MovementType is Blink so the CPU plans it as a directional move.
                FighterKitMotion.StartNorthStarLeap(ref runtime, facing, modes.MovementDurationFrames);
            // Package 13 W7b: Ascendant Wings (A08), Desert Mirage (C03), Rail
            // Charge (LN02, any Dash) and Sonata Drift (M04) are kit phases.
            } else if (modes.MovementType == (int)FTT.Combat.MovementType.WingDive) {
                FighterKitMotion.StartWingRise(
                    ref fighter, ref runtime, speed, facing,
                    _contracts.For(fighter.PlayerID, -1).TotalFrames);
            } else if (modes.MovementType == (int)FTT.Combat.MovementType.SandRush) {
                FighterKitMotion.StartSandRush(ref runtime, facing, modes.MovementDurationFrames);
            } else if (modes.MovementType == 1) {
                // Glide (Leonardo's Ornithopter, Joan's Ascendant Wings,
                // Shakespeare's Prospero's Flight):
                // a forward-and-upward boost that cancels into a reduced-gravity
                // float for the authored duration (design: up to 3 s = 180
                // frames). FloatFrames is a snapshotted FighterRuntimeComponent
                // field, so this stays rollback-safe.
                fighter.Velocity.x = speed * FP64.FromInt(facing);
                fighter.Velocity.y = speed / FP64.FromInt(2);
                fighter.IsGrounded = 0;
                runtime.FloatFrames = modes.MovementDurationFrames > 0 ? modes.MovementDurationFrames : 180;
                // L03/D14 (Package 13 W7a): a fresh flight re-arms the once-per-
                // flight turret bolt (Leonardo's is the only glide that fires one).
                FighterTurretRules.BeginFlight(ref frame, fighter.PlayerID);
            } else if (modes.MovementType == (int)FTT.Combat.MovementType.Gust) {
                // A08 (Package 13 W7a): Prospero's Flight is a single gust burst —
                // a 20-frame kit phase along facing, then a normal fall, no glide.
                FighterKitMotion.StartGust(ref runtime, facing);
            } else if (modes.MovementType == (int)FTT.Combat.MovementType.Warp) {
                // E04 (Package 13 W7a): the spacetime fold — a 10-frame startup
                // (hittable; a hit cancels it with the cooldown spent), then an
                // instant relocation along the held direction, clearance-shortened
                // inside the stage, then the float window.
                FighterKitMotion.StartWarpFold(ref runtime, facing);
            } else if (modes.MovementType == 5) {
                // M04 (Package 13 W7b): Sonata Drift's glissando rises about 3
                // units along the held direction over the authored frames, and
                // its staff platform is placed under his feet where it ends,
                // not at the cast point.
                int glissando = FighterKitMotion.GlissandoDirection(in runtime);
                FighterKitMotion.StartGlissando(ref runtime, modes.MovementDurationFrames);
                fighter.IsGrounded = 0;
                if (modes.MovementPersistentTypeID > 0) {
                    FP64 travel = modes.MovementDistance > FP64.Zero ? modes.MovementDistance : FP64.FromInt(3);
                    SpawnPersistent(
                        ref frame, in fighter, modes.MovementPersistentTypeID,
                        modes.MovementMaxActiveObjects, modes.MovementPersistentLifetimeFrames,
                        0, FP64.Zero, (int)StatusType.None, 0,
                        hasAnchor: true,
                        anchor: FighterReachKitRules.GlissandoEnd(_geometry, in fighter, glissando, travel));
                }
            } else if (modes.MovementType == 2) {
                // LN02 (Package 13 W7b): a Dash is an armored charge over the
                // authored distance and frames that stops on contact; the armor
                // below covers exactly the travel.
                FighterKitMotion.StartRailCharge(ref runtime, facing, modes.MovementDurationFrames);
            } else if (modes.MovementType == 0) {
                // Package 12 W4: Tesla's Lightning Blink is no longer an instant
                // teleport. It runs 6 startup / 12 translation / 10 recovery
                // frames as a kit phase (FighterKitMotion), and projectiles pass
                // through him during the translation only.
                FighterKitMotion.StartBlink(ref runtime, facing);
            } else {
                // Teleport/Warp travel in the held input direction (world Y is
                // up, so a negative MoveY stick value means an upward warp).
                int directionX = runtime.MoveX > 30 ? 1 : runtime.MoveX < -30 ? -1 : 0;
                int directionY = runtime.MoveY < -30 ? 1 : runtime.MoveY > 30 ? -1 : 0;
                if (directionX == 0 && directionY == 0) directionX = facing;
                fighter.Position.x += distance * FP64.FromInt(directionX);
                fighter.Position.x = FP64.Clamp(fighter.Position.x, FP64.FromInt(-10), FP64.FromInt(10));
                if (directionY != 0) {
                    fighter.Position.y += distance * FP64.FromInt(directionY);
                    if (fighter.Position.y < FP64.Zero) fighter.Position.y = FP64.Zero;
                    if (directionY > 0) fighter.IsGrounded = 0;
                }
                // Warp cancels into a brief float glide (reduced gravity).
                if (modes.MovementType == 4) runtime.FloatFrames = 60;
            }

            if (modes.MovementResetsJump != 0) fighter.RemainingJumps = tuning.MaxJumpCount;
            if (modes.MovementGrantsHyperArmor != 0) {
                fighter.HyperArmorFrames = modes.MovementDurationFrames > 0 ? modes.MovementDurationFrames : 12;
            }
            if (modes.MovementPersistentTypeID > 0 && modes.MovementType != 5) {
                SpawnPersistent(
                    ref frame, in fighter, modes.MovementPersistentTypeID,
                    modes.MovementMaxActiveObjects, modes.MovementPersistentLifetimeFrames,
                    0, FP64.Zero, (int)StatusType.None, 0);
            }
        }
    }

    public sealed class FighterProjectileSystem : ISystem {
        private static readonly FP64 FixedDelta = FP64.One / FP64.FromInt(60);
        private readonly FighterHitContractTable _contracts;
        /// <summary>Package 13 W7b: ground waves ride the stage's surfaces; shots stop at its walls.</summary>
        private readonly FighterStageGeometry _geometry;

        public FighterProjectileSystem(FighterHitContractTable contracts = null, FighterStageGeometry geometry = null) {
            _contracts = contracts ?? FighterHitContractTable.Default;
            _geometry = geometry ?? FighterStageGeometry.Default;
        }

        /// <summary>
        /// M08/M05 (Package 12 W3b): a projectile's hitstun and launch flag come
        /// from its owner's authored contract for the slot the projectile type
        /// encodes (<c>CharacterID * 10 + slot</c>; an Ultimate-origin shot reads
        /// the Ultimate). A slot with no projected contract keeps the value the
        /// projectile was spawned with and launches, the legacy behaviour.
        /// </summary>
        internal static void ResolveProjectileContract(
            FighterHitContractTable contracts, in FighterProjectileComponent projectile,
            out int hitstunFrames, out bool launches) =>
            ResolveProjectileContract(contracts, in projectile, out hitstunFrames, out launches, out _);

        /// <summary>
        /// A01/D10 (Package 13 W1): the same resolution, also returning the
        /// slot's contract so the hit reads its Shield-Breaker class and its
        /// signed knockback vector. <paramref name="contract"/> is default when
        /// the projectile's slot has no projected contract.
        /// </summary>
        internal static void ResolveProjectileContract(
            FighterHitContractTable contracts, in FighterProjectileComponent projectile,
            out int hitstunFrames, out bool launches, out FighterAbilityHitData contract) {
            int slot = projectile.UltimateOrigin != 0
                ? FighterHitContractTable.SlotUltimate
                : projectile.ProjectileTypeID % 10;
            hitstunFrames = projectile.HitstunFrames;
            launches = true;
            contract = default;
            if (slot != FighterHitContractTable.SlotSpecialOne
                && slot != FighterHitContractTable.SlotSpecialTwo
                && slot != FighterHitContractTable.SlotUltimate) return;
            FighterAbilityHitData authored = contracts.For(projectile.OwnerPlayerID, slot);
            if (authored.HitstunFrames <= 0) return;
            contract = authored;
            hitstunFrames = authored.HitstunFrames;
            launches = authored.Launches;
        }
        private static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);

        public void Update(ref Frame frame) {
            var filter = frame.Filter<FighterProjectileComponent>();
            while (filter.Next(out EntityRef projectileEntity)) {
                ref FighterProjectileComponent projectile = ref frame.Get<FighterProjectileComponent>(projectileEntity);
                projectile.LifetimeFrames--;
                projectile.Position += projectile.Velocity * FixedDelta;
                if (projectile.GravityPerSecond > FP64.Zero) {
                    projectile.Velocity.y -= projectile.GravityPerSecond * FixedDelta;
                }
                bool groundWave = FighterGroundWave.IsGroundWave(projectile.ProjectileTypeID);
                bool requiem = projectile.UltimateOrigin == 0
                    && projectile.ProjectileTypeID == FighterReachKitRules.RequiemChordProjectile;
                // Package 13 W7a: a two-stage (bursting) Special projectile reads
                // its slot's burst radius/contact damage from the loadout contract.
                ResolveProjectileContract(
                    _contracts, in projectile, out int projectileHitstun, out bool projectileLaunches,
                    out FighterAbilityHitData projectileContract);
                bool bursting = projectile.UltimateOrigin == 0 && projectileContract.Bursts;
                bool burstsOnTerrain = bursting && projectileContract.BurstsOnTerrain;
                if (burstsOnTerrain
                    && (projectile.LifetimeFrames <= 0
                        || FighterProjectileBurstRules.StruckTerrain(_geometry, in projectile))) {
                    // Straight until it strikes a fighter, terrain, a wall or its
                    // maximum range — and bursts where it stops.
                    FPVector2 burstPoint = projectile.Position;
                    FighterProjectileBurstRules.Burst(ref frame, in projectile, in projectileContract, in burstPoint);
                    frame.DestroyEntity(projectileEntity);
                    continue;
                }
                // Package 13 W7b (M03): the Requiem Chord has no range limit — it
                // crosses the stage until it strikes a fighter or a wall, and a
                // wall bursts it. (W7a owns the shared burst-on-terrain primitive;
                // this is the chord's local rule until it lands.)
                if (requiem
                    && (projectile.Position.x <= _geometry.LeftWall || projectile.Position.x >= _geometry.RightWall)) {
                    FP64 wallX = projectile.Position.x <= _geometry.LeftWall ? _geometry.LeftWall : _geometry.RightWall;
                    FighterReachKitRules.SpawnRequiemBurst(
                        ref frame, projectile.OwnerPlayerID, new FPVector2(wallX, projectile.Position.y),
                        _contracts.For(projectile.OwnerPlayerID, FighterHitContractTable.SlotSpecialOne),
                        alreadyShaved: false);
                    frame.DestroyEntity(projectileEntity);
                    continue;
                }
                if (projectile.LifetimeFrames <= 0
                    || FP64.Abs(projectile.Position.x) > FP64.FromInt(12)
                    || projectile.Position.y < FP64.Zero
                    // Package 13 W7b: a ground wave dissipates at a wall or where
                    // its surface ends (a platform edge, an Open stage's pit).
                    || (groundWave
                        && (projectile.Position.x <= _geometry.LeftWall
                            || projectile.Position.x >= _geometry.RightWall
                            || !FighterGroundWave.SurfaceContinues(
                                _geometry, projectile.Position.x, FighterGroundWave.SurfaceY(in projectile))))) {
                    frame.DestroyEntity(projectileEntity);
                    continue;
                }

                int targetPlayerID = projectile.OwnerPlayerID == 0 ? 1 : 0;
                if (!FighterEntityQueries.TryFindFighter(ref frame, projectile.OwnerPlayerID, out EntityRef ownerEntity)
                    || !FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) continue;

                ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
                if (!FighterEntityQueries.Overlaps(
                        in projectile.Position, in projectile.HalfExtents,
                        in target.Position, in FighterHalfExtents)) continue;
                // Package 13 W7b: a grounded-only ground wave passes under a
                // jumper (and anyone on another surface) without being consumed.
                if (groundWave && !FighterGroundWave.CanStrike(in projectile, in target)) continue;
                // Package 12 W4 (Tesla kit rule): during the Lightning Blink
                // translation the shot passes straight through — no contact, and
                // it is not consumed. Derived from the existing movement phase.
                // A ground wave is not a projectile for this rule (W7b).
                if (!groundWave && FighterKitMotion.PassesThroughProjectiles(
                        in frame.GetReadOnly<FighterRuntimeComponent>(targetEntity))) continue;
                // Package 13 W5: Conductor's Call rushes along the ground and
                // strikes grounded targets only — an airborne target is not
                // contacted and the rush is not consumed.
                if (!FighterConductorsCallRules.CanStrike(projectile.ProjectileTypeID, in target)) continue;
                // Package 13 W5: a projectile is one of the two things Foresight
                // catches. A caught shot is consumed and applies nothing; the
                // combat system lands the answer if its owner is within 2.5 units.
                if (FighterForesightRules.TryCounter(
                        ref frame.Get<FighterCounterComponent>(targetEntity), ref target)) {
                    frame.DestroyEntity(projectileEntity);
                    continue;
                }

                ref FighterStateComponent owner = ref frame.Get<FighterStateComponent>(ownerEntity);
                ref FighterRuntimeComponent ownerRuntime = ref frame.Get<FighterRuntimeComponent>(ownerEntity);
                ref FighterVerbComponent ownerVerb = ref frame.Get<FighterVerbComponent>(ownerEntity);
                ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
                ref FighterVerbComponent targetVerb = ref frame.Get<FighterVerbComponent>(targetEntity);
                ref FighterDefenseComponent targetDefense = ref frame.Get<FighterDefenseComponent>(targetEntity);
                ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
                // Package 13 W7a (L03): a Clockwork Turret bolt is a construct
                // hit — Basic-class, impulse-free, non-launching, no Rally
                // reclaim (D03g) and no hitstop (V7.3), exactly like the
                // construct's old instant strike.
                bool constructBolt = FighterTurretRules.IsConstructBolt(in projectile);
                // D10: an Ultimate-origin shot keeps its spawn-time scalar (the
                // Ultimate finale knockbacks are W6's region); a Special shot
                // reads its slot's signed vector. A bursting shot's contact is
                // the minor impulse-free stage: the burst carries the knockback
                // and the status.
                bool projectileVector = projectile.UltimateOrigin == 0 && projectileContract.HasKnockbackVector
                    && !bursting;
                // M03 (Package 13 W7b): the chord's contact is the first of its four
                // hits — it holds the target in hitstun with no impulse, so the
                // burst's pulses land; the last pulse carries the authored knockback.
                if (requiem) projectileVector = false;
                bool projectileLanded = FighterDamageRules.ApplyFighterHit(
                    ref owner, ref ownerRuntime, ref ownerVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                    projectile.AttackClass,
                    bursting ? projectileContract.ContactDamage : projectile.Damage,
                    bursting || requiem ? FP64.Zero : projectileVector ? projectileContract.KnockbackX : projectile.Knockback.x,
                    projectileHitstun,
                    bursting ? (int)StatusType.None : projectile.StatusType,
                    bursting ? 0 : projectile.StatusFrames,
                    projectile.StatusIntensity, projectile.Position.x,
                    // V7.6 D03h (Package 11 A1b): an Ultimate-spawned projectile
                    // awards its caster no damage-dealt meter. A player-fired
                    // ordinary projectile still reclaims Rally (D03g) either way.
                    creditInfluence: projectile.UltimateOrigin == 0,
                    collectsEcho: !constructBolt,
                    appliesHitstop: !constructBolt,
                    launches: !constructBolt && !bursting && projectileLaunches,
                    shieldBreaker: projectile.AttackClass == FighterDamageRules.SpecialAttackClass
                        && projectileContract.ShieldBreaker,
                    hasKnockbackVector: projectileVector,
                    knockbackVertical: projectileContract.KnockbackY);
                if (bursting) {
                    FPVector2 burstPoint = projectile.Position;
                    FighterProjectileBurstRules.Burst(ref frame, in projectile, in projectileContract, in burstPoint);
                }
                if (requiem) {
                    // M02: a chord whose contact HIT (not a block) shaves
                    // Fortissimo's remaining cooldown now, and its burst is
                    // marked so the pulses cannot shave again.
                    if (projectileLanded) FighterReachKitRules.ShaveFortissimo(ref ownerRuntime);
                    FighterReachKitRules.SpawnRequiemBurst(
                        ref frame, projectile.OwnerPlayerID, in projectile.Position,
                        _contracts.For(projectile.OwnerPlayerID, FighterHitContractTable.SlotSpecialOne),
                        alreadyShaved: projectileLanded);
                }
                frame.DestroyEntity(projectileEntity);
            }
        }
    }

    public sealed class FighterPersistentObjectSystem : ISystem {
        // Tesla Coil alternating-current link (design Section 4): two active coils
        // within 8 units connect into a fence dealing 8 basic damage per 0.5 s
        // tick and applying a brief StaticCharge to the fighter caught between
        // them. The lower-EntityID coil of the pair drives the tick.
        private const int CoilObjectTypeID = 1;
        // 2026-08-11 construct rebalance: fence cadence and damage halved,
        // impulse removed (mirrors the Story TeslaCoilNode fence retune).
        private const int FenceTickFrames = 60;
        private const int FenceDamage = 4;
        private const int FenceHitstunFrames = 8;
        private const int FenceStaticChargeFrames = 30;
        // Cleopatra's Serpent Nest bite delivers the design's brief Root (1 s) as
        // hitstun so the Venom status carried on the bite is not immediately
        // replaced (single-status rule: the newest status replaces the previous).
        private const int NestObjectTypeID = 3;
        private const int NestBiteHitstunFrames = 60;
        private static readonly FP64 CoilLinkRangeSquared = FP64.FromInt(64);
        private static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);

        public void Update(ref Frame frame) {
            // A04 (Package 13 W1): a fallen owner's objects are gone before
            // any of them can act again — no final attack, no pending damage.
            FighterStockLossRules.DespawnOwnedByFallenFighters(ref frame);
            UpdateCoilLinks(ref frame);
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef persistentEntity)) {
                ref FighterPersistentObjectComponent persistent =
                    ref frame.Get<FighterPersistentObjectComponent>(persistentEntity);
                persistent.LifetimeFrames--;
                if (persistent.CurrentHP <= 0 || persistent.LifetimeFrames <= 0 || persistent.RemainingAttacks == 0) {
                    frame.DestroyEntity(persistentEntity);
                    continue;
                }
                if (persistent.Damage <= 0) continue;
                if (persistent.ActionCooldownFrames > 0) {
                    persistent.ActionCooldownFrames--;
                    continue;
                }

                // L03 (Package 13 W7a): the Clockwork Turret fires a real bolt —
                // a straight 12 u/s projectile at the opponent — and only with
                // the opponent in range; the lifetime is the idle cap.
                if (persistent.ObjectTypeID == FighterTurretRules.TurretObjectTypeID) {
                    FighterTurretRules.TryFireBolt(ref frame, ref persistent);
                    continue;
                }

                int targetPlayerID = persistent.OwnerPlayerID == 0 ? 1 : 0;
                if (!FighterEntityQueries.TryFindFighter(ref frame, persistent.OwnerPlayerID, out EntityRef ownerEntity)
                    || !FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) continue;
                ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
                if (FP64.Abs(target.Position.x - persistent.Position.x) > persistent.AttackRange) continue;

                ref FighterStateComponent owner = ref frame.Get<FighterStateComponent>(ownerEntity);
                ref FighterRuntimeComponent ownerRuntime = ref frame.Get<FighterRuntimeComponent>(ownerEntity);
                ref FighterVerbComponent ownerVerb = ref frame.Get<FighterVerbComponent>(ownerEntity);
                ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
                ref FighterVerbComponent targetVerb = ref frame.Get<FighterVerbComponent>(targetEntity);
                ref FighterDefenseComponent targetDefense = ref frame.Get<FighterDefenseComponent>(targetEntity);
                ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
                // Construct hits never reclaim the owner's Rally echo (V7.1: only
                // direct hits collect), and never apply hitstop (V7.3: only
                // direct player-authored hits carry the freeze).
                FighterDamageRules.ApplyFighterHit(
                    ref owner, ref ownerRuntime, ref ownerVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                    FighterDamageRules.BasicAttackClass, persistent.Damage, persistent.Knockback,
                    persistent.ObjectTypeID == NestObjectTypeID ? NestBiteHitstunFrames : 10,
                    persistent.StatusType, persistent.StatusFrames, FP64.One, persistent.Position.x,
                    collectsEcho: false,
                    appliesHitstop: false,
                    // M05: construct hits are authored non-launching (all four
                    // construct abilities carry Launches = false).
                    launches: false);
                persistent.ActionCooldownFrames = persistent.BaseActionCooldownFrames;
                if (persistent.RemainingAttacks > 0) persistent.RemainingAttacks--;
            }
        }

        private static void UpdateCoilLinks(ref Frame frame) {
            for (int ownerID = 0; ownerID <= 1; ownerID++) {
                // Deploy limit for coils is 2, so tracking the two lowest-ID live
                // coils fully describes the possible link.
                EntityRef firstCoil = default, secondCoil = default;
                int firstID = int.MaxValue, secondID = int.MaxValue;
                int found = 0;
                var filter = frame.Filter<FighterPersistentObjectComponent>();
                while (filter.Next(out EntityRef entity)) {
                    ref readonly FighterPersistentObjectComponent coil =
                        ref frame.GetReadOnly<FighterPersistentObjectComponent>(entity);
                    if (coil.OwnerPlayerID != ownerID
                        || coil.ObjectTypeID != CoilObjectTypeID
                        || coil.CurrentHP <= 0
                        || coil.LifetimeFrames <= 0) continue;
                    found++;
                    if (coil.EntityID < firstID) {
                        secondID = firstID; secondCoil = firstCoil;
                        firstID = coil.EntityID; firstCoil = entity;
                    } else if (coil.EntityID < secondID) {
                        secondID = coil.EntityID; secondCoil = entity;
                    }
                }
                if (found < 2) continue;

                FPVector2 firstPosition = frame.GetReadOnly<FighterPersistentObjectComponent>(firstCoil).Position;
                FPVector2 secondPosition = frame.GetReadOnly<FighterPersistentObjectComponent>(secondCoil).Position;
                FP64 dx = firstPosition.x - secondPosition.x;
                FP64 dy = firstPosition.y - secondPosition.y;
                if (dx * dx + dy * dy > CoilLinkRangeSquared) continue;

                ref FighterPersistentObjectComponent driver = ref frame.Get<FighterPersistentObjectComponent>(firstCoil);
                if (driver.LinkTickFramesRemaining > 0) {
                    driver.LinkTickFramesRemaining--;
                    continue;
                }
                driver.LinkTickFramesRemaining = FenceTickFrames;

                FPVector2 fenceCenter = new(
                    (firstPosition.x + secondPosition.x) / FP64.FromInt(2),
                    (firstPosition.y + secondPosition.y) / FP64.FromInt(2));
                FPVector2 fenceHalfExtents = new(
                    FP64.Abs(dx) / FP64.FromInt(2) + FP64.FromDouble(0.3),
                    FP64.Max(FP64.Abs(dy) / FP64.FromInt(2), FP64.One));

                int targetPlayerID = ownerID == 0 ? 1 : 0;
                if (!FighterEntityQueries.TryFindFighter(ref frame, ownerID, out EntityRef ownerEntity)
                    || !FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) continue;
                ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
                if (!FighterEntityQueries.Overlaps(
                        in fenceCenter, in fenceHalfExtents,
                        in target.Position, in FighterHalfExtents)) continue;

                ref FighterStateComponent owner = ref frame.Get<FighterStateComponent>(ownerEntity);
                ref FighterRuntimeComponent ownerRuntime = ref frame.Get<FighterRuntimeComponent>(ownerEntity);
                ref FighterVerbComponent ownerVerb = ref frame.Get<FighterVerbComponent>(ownerEntity);
                ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
                ref FighterVerbComponent targetVerb = ref frame.Get<FighterVerbComponent>(targetEntity);
                ref FighterDefenseComponent targetDefense = ref frame.Get<FighterDefenseComponent>(targetEntity);
                ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
                // Fence ticks are construct damage — no Rally echo reclaim,
                // no hitstop (V7.3).
                bool fenceLanded = FighterDamageRules.ApplyFighterHit(
                    ref owner, ref ownerRuntime, ref ownerVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                    FighterDamageRules.BasicAttackClass, FenceDamage, FP64.Zero, FenceHitstunFrames,
                    (int)StatusType.StaticCharge, FenceStaticChargeFrames, FP64.FromDouble(0.5),
                    fenceCenter.x, collectsEcho: false,
                    appliesHitstop: false,
                    launches: false);
                // F07/T02 (Package 13 W7a): a landed fence tick also lays the
                // owning Tesla's baseline Conductive mark (Story's fence already
                // did), so a fence catch primes the Lorentz chain in both modes.
                if (fenceLanded) {
                    FighterConductiveRules.ApplyMark(
                        ref frame.Get<FighterConductiveComponent>(targetEntity),
                        ownerID, FTT.Combat.BasicComboRules.ConductiveMarkFenceFrames);
                }
            }
        }
    }

    /// <summary>
    /// Canonical era-hazard identities (`design-godot.md` §10 stage table). The ID
    /// is authored on <c>FighterStageData.HazardTypeID</c> and carried into the
    /// simulation through <c>FighterMatchComponent.StageHazardTypeID</c>.
    /// </summary>
    public static class FighterHazardTypeID {
        public const int FlorenceSteamPipe = 1;
        public const int OrleansTrebuchetDebris = 2;
        public const int ChicagoTeslaInduction = 3;
        public const int ParisDampeningBeam = 4;
        public const int VesuviusRockfall = 5;
        public const int NassauMortar = 6;
        public const int AlexandriaSinkhole = 7;
        public const int BerlinSearchlight = 8;
        public const int GlobeAudienceHeckle = 9;
        public const int GettysburgArtillery = 10;
        public const int Count = 10;
    }

    /// <summary>
    /// Deterministic era hazards. Every hazard runs warning → active → recovery;
    /// only the active phase damages. Per-type behaviour (rolling debris, sweeping
    /// beams, falling rocks and their residue pools, dwell timers, idle punishment,
    /// one-shot artillery) is authored in <see cref="FighterHazardSpec"/> and driven
    /// here. Hazard damage ticks are basic-attack class, so an active front-facing
    /// block absorbs a tick for one shield charge (design §10 "Block Compatibility").
    ///
    /// <para>All movement is integrated in units per frame from the component's
    /// <c>Velocity</c> field, and all randomness threads
    /// <c>FighterMatchComponent.RandomState0/1</c> through
    /// <see cref="DeterministicRandom"/> inside <c>SpawnHazard</c> in a fixed draw
    /// order, so hazards stay rollback-identical.</para>
    /// </summary>
    public sealed class FighterHazardSystem : ISystem {
        internal const int DefaultWarningFrames = 90;
        internal const int RecoveryFrames = 60;
        internal const int DamageTickFrames = 30;
        internal const int WarningPhase = 0;
        internal const int ActivePhase = 1;
        internal const int RecoveryPhase = 2;
        /// <summary>Vesuvius sub-states: the falling rock, then its ground pool.</summary>
        internal const int RockFallingSubType = 0;
        internal const int RockPoolSubType = 1;
        internal const int RockPoolFrames = 180;
        /// <summary>Berlin's drone fires after this many consecutive frames in the beam.</summary>
        internal const int SearchlightDwellFrames = 90;
        /// <summary>The Globe crowd pelts a fighter that has stood still this long.</summary>
        internal const int HeckleIdleFrames = 120;
        private const int HitstunFrames = 10;
        private static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);
        private static readonly FP64 DebrisSpeed = FP64.FromDouble(0.09);
        private static readonly FP64 BeamSweepSpeed = FP64.FromDouble(0.05);
        private static readonly FP64 RockFallSpeed = FP64.FromDouble(0.15);
        /// <summary>
        /// Paris Dampening Beam meter denial per 30-frame tick. 2.5 points x
        /// 2 ticks/second = the doc's 5%/s (design §10; V7.3 halved the old 5
        /// which drained double the authored rate).
        /// </summary>
        private static readonly FP64 InfluenceDrainPerTick = FP64.FromDouble(2.5);
        /// <summary>Below this |velocity.x| a grounded fighter counts as idle for the Globe crowd.</summary>
        private static readonly FP64 IdleSpeedThreshold = FP64.FromDouble(0.5);
        /// <summary>The mortar is a launcher: its vertical impulse is doubled.</summary>
        private static readonly FP64 MortarVerticalScale = FP64.FromInt(2);

        private readonly FighterStageGeometry _geometry;

        public FighterHazardSystem(FighterStageGeometry geometry = null) {
            _geometry = geometry ?? FighterStageGeometry.Default;
        }

        public void Update(ref Frame frame) {
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            if (match.MatchState != 1 || match.HazardsEnabled == 0 || match.HazardCadenceFrames <= 0) return;

            // V7.1 Overtime / Sudden Death: hazard cadence doubles — the idle
            // gap between hazards and the recovery phase are halved; the 1.5 s
            // warning phase is deliberately unchanged so readability survives.
            bool accelerated = match.SuddenDeathActive == 1
                || (match.TimerEnabled == 1
                    && match.RemainingFrames > 0
                    && match.RemainingFrames <= FighterMatchSystem.OvertimeFrames);

            if (match.NextHazardSpawnFrames > 0) match.NextHazardSpawnFrames--;
            if (match.NextHazardSpawnFrames <= 0) {
                SpawnHazard(ref frame, ref match);
                int interval = match.HazardCadenceFrames;
                match.NextHazardSpawnFrames = accelerated ? interval / 2 > 0 ? interval / 2 : 1 : interval;
            }

            var filter = frame.Filter<FighterHazardComponent>();
            while (filter.Next(out EntityRef hazardEntity)) {
                ref FighterHazardComponent hazard = ref frame.Get<FighterHazardComponent>(hazardEntity);
                hazard.PhaseFramesRemaining--;

                if (hazard.Phase == WarningPhase) {
                    if (hazard.PhaseFramesRemaining <= 0) BeginActivePhase(ref hazard);
                    continue;
                }
                if (hazard.Phase == RecoveryPhase) {
                    if (hazard.PhaseFramesRemaining <= 0) frame.DestroyEntity(hazardEntity);
                    continue;
                }

                AdvanceActiveHazard(ref frame, ref hazard);
                if (hazard.TickFramesRemaining > 0) hazard.TickFramesRemaining--;
                if (hazard.TickFramesRemaining <= 0) {
                    ApplyPeriodicTick(ref frame, ref hazard);
                    hazard.TickFramesRemaining = DamageTickFrames;
                }
                if (hazard.PhaseFramesRemaining <= 0) {
                    BeginRecoveryPhase(ref hazard);
                    if (accelerated && hazard.PhaseFramesRemaining > 1) {
                        hazard.PhaseFramesRemaining /= 2;
                    }
                }
            }
        }

        private void SpawnHazard(ref Frame frame, ref FighterMatchComponent match) {
            var random = new DeterministicRandom(1);
            random.SetFullState(match.RandomState0, match.RandomState1);
            // Authored stages pick a deterministic anchor; the legacy arena keeps
            // the historical random spawn range. Both draws always run, in this
            // order, so the RNG stream does not depend on the hazard identity.
            FP64 positionX = _geometry.HazardAnchorXs.Length > 0
                ? _geometry.HazardAnchorXs[random.NextInt(0, _geometry.HazardAnchorXs.Length)]
                : random.NextFixed(FP64.FromInt(-7), FP64.FromInt(7));
            int hazardType = match.StageHazardTypeID >= 1 && match.StageHazardTypeID <= FighterHazardTypeID.Count
                ? match.StageHazardTypeID
                : FighterHazardTypeID.FlorenceSteamPipe;
            int steamDamage = random.NextIntInclusive(5, 10);
            (match.RandomState0, match.RandomState1) = random.GetFullState();

            FighterHazardSpec spec = FighterHazardSpec.For(hazardType, steamDamage);

            EntityRef entity = frame.CreateEntity();
            frame.Add(entity, new FighterHazardComponent {
                EntityID = match.NextEntityID++,
                HazardTypeID = hazardType,
                SubTypeID = 0,
                Phase = WarningPhase,
                PhaseFramesRemaining = spec.WarningFrames,
                Damage = spec.Damage,
                TickFramesRemaining = DamageTickFrames,
                WarningFrames = spec.WarningFrames,
                ActiveFrames = spec.ActiveFrames,
                CooldownFrames = RecoveryFrames,
                HitMask = 0,
                DwellFramesPlayerOne = 0,
                DwellFramesPlayerTwo = 0,
                Position = new FPVector2(positionX, spec.CenterY),
                HalfExtents = spec.HalfExtents,
                Knockback = new FPVector2(spec.Knockback, spec.Knockback),
                Velocity = FPVector2.Zero
            });
        }

        /// <summary>
        /// Warning → active. Types that move or relocate at ignition (rolling
        /// debris, the sweeping beam, the rock dropping from the ceiling) set their
        /// travel state here; everything else simply arms.
        /// </summary>
        private void BeginActivePhase(ref FighterHazardComponent hazard) {
            hazard.Phase = ActivePhase;
            hazard.PhaseFramesRemaining = hazard.ActiveFrames;
            hazard.TickFramesRemaining = 1;
            hazard.HitMask = 0;

            if (hazard.HazardTypeID == FighterHazardTypeID.OrleansTrebuchetDebris) {
                // Debris rolls away from the wall it was launched over.
                hazard.Velocity = new FPVector2(
                    hazard.Position.x < FP64.Zero ? DebrisSpeed : -DebrisSpeed, FP64.Zero);
            } else if (hazard.HazardTypeID == FighterHazardTypeID.ParisDampeningBeam) {
                hazard.Velocity = new FPVector2(
                    hazard.Position.x <= FP64.Zero ? BeamSweepSpeed : -BeamSweepSpeed, FP64.Zero);
            } else if (hazard.HazardTypeID == FighterHazardTypeID.VesuviusRockfall) {
                // The ground marker becomes the rock, which now falls from the sky.
                hazard.Position = new FPVector2(hazard.Position.x, _geometry.Ceiling);
                hazard.HalfExtents = new FPVector2(FP64.FromDouble(0.6), FP64.FromDouble(0.6));
                hazard.Velocity = new FPVector2(FP64.Zero, -RockFallSpeed);
            }
        }

        private static void BeginRecoveryPhase(ref FighterHazardComponent hazard) {
            hazard.Phase = RecoveryPhase;
            hazard.PhaseFramesRemaining = hazard.CooldownFrames > 0 ? hazard.CooldownFrames : RecoveryFrames;
            hazard.Velocity = FPVector2.Zero;
        }

        /// <summary>
        /// Per-frame active behaviour: movement, contact hits for one-shot hazards,
        /// and the dwell/idle counters. The periodic damage tick runs separately.
        /// </summary>
        private void AdvanceActiveHazard(ref Frame frame, ref FighterHazardComponent hazard) {
            switch (hazard.HazardTypeID) {
                case FighterHazardTypeID.OrleansTrebuchetDebris:
                    hazard.Position += hazard.Velocity;
                    ApplyOneShotContact(ref frame, ref hazard, FP64.Zero);
                    // Rolled off the far side of the ramparts.
                    if (hazard.Position.x - hazard.HalfExtents.x > _geometry.RightWall
                        || hazard.Position.x + hazard.HalfExtents.x < _geometry.LeftWall) {
                        hazard.PhaseFramesRemaining = 0;
                    }
                    break;

                case FighterHazardTypeID.ParisDampeningBeam:
                    hazard.Position += hazard.Velocity;
                    if (hazard.Position.x + hazard.HalfExtents.x >= _geometry.RightWall && hazard.Velocity.x > FP64.Zero) {
                        hazard.Velocity = new FPVector2(-hazard.Velocity.x, FP64.Zero);
                    } else if (hazard.Position.x - hazard.HalfExtents.x <= _geometry.LeftWall && hazard.Velocity.x < FP64.Zero) {
                        hazard.Velocity = new FPVector2(-hazard.Velocity.x, FP64.Zero);
                    }
                    break;

                case FighterHazardTypeID.VesuviusRockfall:
                    if (hazard.SubTypeID == RockFallingSubType) {
                        hazard.Position += hazard.Velocity;
                        ApplyOneShotContact(ref frame, ref hazard, FP64.Zero);
                        if (hazard.Position.y <= FP64.Zero) {
                            // Impact: the rock shatters into a time-dilation pool.
                            hazard.Position = new FPVector2(hazard.Position.x, FP64.Zero);
                            hazard.Velocity = FPVector2.Zero;
                            hazard.SubTypeID = RockPoolSubType;
                            hazard.HalfExtents = new FPVector2(FP64.One, FP64.FromDouble(0.5));
                            hazard.PhaseFramesRemaining = RockPoolFrames;
                            hazard.TickFramesRemaining = 1;
                        }
                    }
                    break;

                case FighterHazardTypeID.NassauMortar:
                case FighterHazardTypeID.GettysburgArtillery:
                    ApplyOneShotContact(
                        ref frame, ref hazard,
                        hazard.HazardTypeID == FighterHazardTypeID.NassauMortar
                            ? MortarVerticalScale
                            : FP64.Zero);
                    break;

                case FighterHazardTypeID.BerlinSearchlight:
                    AdvanceSearchlightDwell(ref frame, ref hazard);
                    break;

                case FighterHazardTypeID.GlobeAudienceHeckle:
                    AdvanceHeckleIdle(ref frame, ref hazard);
                    break;
            }
        }

        /// <summary>
        /// The periodic 0.5 s damage tick shared by the dwell-free damage-over-time
        /// hazards. One-shot and counter-driven identities deliberately opt out.
        /// </summary>
        private void ApplyPeriodicTick(ref Frame frame, ref FighterHazardComponent hazard) {
            int type = hazard.HazardTypeID;
            bool rockPool = type == FighterHazardTypeID.VesuviusRockfall && hazard.SubTypeID == RockPoolSubType;
            bool ticks = type == FighterHazardTypeID.FlorenceSteamPipe
                || type == FighterHazardTypeID.ChicagoTeslaInduction
                || type == FighterHazardTypeID.ParisDampeningBeam
                || type == FighterHazardTypeID.AlexandriaSinkhole
                || rockPool;
            if (!ticks) return;

            var fighterFilter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterTuningComponent>();
            while (fighterFilter.Next(out EntityRef fighterEntity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(fighterEntity);
                if (!FighterEntityQueries.Overlaps(
                        in hazard.Position, in hazard.HalfExtents,
                        in fighter.Position, in FighterHalfExtents)) continue;

                // The dampening beam is pure meter denial: no damage, no impulse.
                // The drain honours the same pre-effect gates ApplyFighterHit
                // applies (audit M-11): a fighter pinned on the respawn platform is
                // "completely invulnerable" (design-godot.md ~1570) and loses
                // nothing, and a dead fighter is skipped.
                //
                // V7.6 D03c (Package 11 A1b): it no longer spends an Aegis
                // charge. "Integrity loss, Siphon Snare and other non-HP meter
                // drains retain their separate rules and do not spend finite
                // shields to prevent them" — the bubble is for hits, and a pure
                // drain eating it was a shipped defect.
                if (type == FighterHazardTypeID.ParisDampeningBeam) {
                    if (fighter.InvulnerabilityFrames > 0 || fighter.Stocks <= 0) continue;
                    fighter.Influence = FP64.Max(FP64.Zero, fighter.Influence - InfluenceDrainPerTick);
                    continue;
                }
                // Quicksand only grips fighters standing in it.
                if (type == FighterHazardTypeID.AlexandriaSinkhole && fighter.IsGrounded == 0) continue;

                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(fighterEntity);
                ref FighterVerbComponent verb = ref frame.Get<FighterVerbComponent>(fighterEntity);
                ref FighterDefenseComponent defense = ref frame.Get<FighterDefenseComponent>(fighterEntity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(fighterEntity);
                ResolveTickStatus(type, rockPool, out int statusType, out int statusFrames, out FP64 statusIntensity);
                // The residue pool slows, it does not wound.
                int damage = rockPool ? 0 : hazard.Damage;
                FP64 knockback = rockPool ? FP64.Zero : hazard.Knockback.x;
                int hitstun = rockPool ? 0 : HitstunFrames;
                FighterDamageRules.ApplyEnvironmentHit(
                    ref fighter, ref runtime, ref verb, ref defense, in tuning,
                    damage, knockback, hitstun, hazard.Position.x,
                    statusType, statusFrames, statusIntensity);
            }
        }

        private static void ResolveTickStatus(
            int hazardType,
            bool rockPool,
            out int statusType,
            out int statusFrames,
            out FP64 statusIntensity) {
            if (rockPool) {
                statusType = (int)StatusType.TimeDilation;
                statusFrames = 120;
                statusIntensity = FP64.FromDouble(0.5);
                return;
            }
            if (hazardType == FighterHazardTypeID.ChicagoTeslaInduction) {
                statusType = (int)StatusType.StaticCharge;
                statusFrames = 30;
                statusIntensity = FP64.FromDouble(0.5);
                return;
            }
            if (hazardType == FighterHazardTypeID.AlexandriaSinkhole) {
                // The design's "reducing speed by 50%" is TimeDilation at full intensity.
                statusType = (int)StatusType.TimeDilation;
                statusFrames = 60;
                statusIntensity = FP64.One;
                return;
            }
            statusType = (int)StatusType.None;
            statusFrames = 0;
            statusIntensity = FP64.FromDouble(0.5);
        }

        /// <summary>
        /// One damaging contact per fighter for the whole hazard instance. The mask
        /// is consumed on the first overlapping frame whether or not the hit landed
        /// (a blocked mortar shell is spent, and so is one the fighter rolled
        /// through) so that a single shell can never chain-hit across frames.
        /// </summary>
        private static void ApplyOneShotContact(
            ref Frame frame,
            ref FighterHazardComponent hazard,
            FP64 verticalKnockbackScale) {
            var fighterFilter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterTuningComponent>();
            while (fighterFilter.Next(out EntityRef fighterEntity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(fighterEntity);
                int bit = 1 << fighter.PlayerID;
                if ((hazard.HitMask & bit) != 0) continue;
                if (!FighterEntityQueries.Overlaps(
                        in hazard.Position, in hazard.HalfExtents,
                        in fighter.Position, in FighterHalfExtents)) continue;

                hazard.HitMask |= bit;
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(fighterEntity);
                ref FighterVerbComponent verb = ref frame.Get<FighterVerbComponent>(fighterEntity);
                ref FighterDefenseComponent defense = ref frame.Get<FighterDefenseComponent>(fighterEntity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(fighterEntity);
                FighterDamageRules.ApplyEnvironmentHit(
                    ref fighter, ref runtime, ref verb, ref defense, in tuning,
                    hazard.Damage, hazard.Knockback.x, HitstunFrames, hazard.Position.x,
                    (int)StatusType.None, 0, FP64.One, verticalKnockbackScale);
            }
        }

        /// <summary>
        /// Berlin: the searchlight itself is harmless. Staying inside its column for
        /// 1.5 consecutive seconds calls down a drone laser on that fighter alone;
        /// stepping out of the light resets the counter.
        /// </summary>
        private static void AdvanceSearchlightDwell(ref Frame frame, ref FighterHazardComponent hazard) {
            var fighterFilter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterTuningComponent>();
            while (fighterFilter.Next(out EntityRef fighterEntity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(fighterEntity);
                bool inside = FighterEntityQueries.Overlaps(
                    in hazard.Position, in hazard.HalfExtents,
                    in fighter.Position, in FighterHalfExtents);
                int dwell = ReadDwell(in hazard, fighter.PlayerID);
                if (!inside) {
                    WriteDwell(ref hazard, fighter.PlayerID, 0);
                    continue;
                }
                dwell++;
                if (dwell < SearchlightDwellFrames) {
                    WriteDwell(ref hazard, fighter.PlayerID, dwell);
                    continue;
                }
                WriteDwell(ref hazard, fighter.PlayerID, 0);
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(fighterEntity);
                ref FighterVerbComponent verb = ref frame.Get<FighterVerbComponent>(fighterEntity);
                ref FighterDefenseComponent defense = ref frame.Get<FighterDefenseComponent>(fighterEntity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(fighterEntity);
                FighterDamageRules.ApplyEnvironmentHit(
                    ref fighter, ref runtime, ref verb, ref defense, in tuning,
                    hazard.Damage, hazard.Knockback.x, HitstunFrames, hazard.Position.x,
                    (int)StatusType.None, 0, FP64.One);
            }
        }

        /// <summary>
        /// Globe: the crowd punishes camping. A grounded fighter that has barely
        /// moved for two seconds is pelted from the nearest gallery anchor; a moving
        /// fighter is never hit, and the hazard has no damaging region at all.
        /// </summary>
        private void AdvanceHeckleIdle(ref Frame frame, ref FighterHazardComponent hazard) {
            var fighterFilter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterTuningComponent>();
            while (fighterFilter.Next(out EntityRef fighterEntity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(fighterEntity);
                bool idle = fighter.IsGrounded != 0 && FP64.Abs(fighter.Velocity.x) < IdleSpeedThreshold;
                int idleFrames = ReadDwell(in hazard, fighter.PlayerID);
                if (!idle) {
                    WriteDwell(ref hazard, fighter.PlayerID, 0);
                    continue;
                }
                idleFrames++;
                if (idleFrames < HeckleIdleFrames) {
                    WriteDwell(ref hazard, fighter.PlayerID, idleFrames);
                    continue;
                }
                WriteDwell(ref hazard, fighter.PlayerID, 0);
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(fighterEntity);
                ref FighterVerbComponent verb = ref frame.Get<FighterVerbComponent>(fighterEntity);
                ref FighterDefenseComponent defense = ref frame.Get<FighterDefenseComponent>(fighterEntity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(fighterEntity);
                FighterDamageRules.ApplyEnvironmentHit(
                    ref fighter, ref runtime, ref verb, ref defense, in tuning,
                    hazard.Damage, hazard.Knockback.x, HitstunFrames,
                    NearestGalleryAnchorX(fighter.Position.x),
                    (int)StatusType.None, 0, FP64.One);
            }
        }

        private FP64 NearestGalleryAnchorX(FP64 fighterX) {
            if (_geometry.HazardAnchorXs.Length == 0) return fighterX;
            FP64 best = _geometry.HazardAnchorXs[0];
            FP64 bestDistance = FP64.Abs(best - fighterX);
            for (int index = 1; index < _geometry.HazardAnchorXs.Length; index++) {
                FP64 candidate = _geometry.HazardAnchorXs[index];
                FP64 distance = FP64.Abs(candidate - fighterX);
                if (distance < bestDistance) {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static int ReadDwell(in FighterHazardComponent hazard, int playerID) =>
            playerID == 0 ? hazard.DwellFramesPlayerOne : hazard.DwellFramesPlayerTwo;

        private static void WriteDwell(ref FighterHazardComponent hazard, int playerID, int frames) {
            if (playerID == 0) hazard.DwellFramesPlayerOne = frames;
            else hazard.DwellFramesPlayerTwo = frames;
        }
    }

    /// <summary>
    /// Authored per-identity hazard tuning (damage, impulse, footprint, phase
    /// lengths). Kept beside the system rather than in a `.tres` because
    /// hazard geometry is deterministic simulation data that must be identical on
    /// every peer without loading a Godot resource.
    /// </summary>
    internal readonly struct FighterHazardSpec {
        public readonly int Damage;
        public readonly int WarningFrames;
        public readonly int ActiveFrames;
        public readonly FP64 Knockback;
        public readonly FP64 CenterY;
        public readonly FPVector2 HalfExtents;

        private FighterHazardSpec(
            int damage, int warningFrames, int activeFrames,
            FP64 knockback, FP64 centerY, FPVector2 halfExtents) {
            Damage = damage;
            WarningFrames = warningFrames;
            ActiveFrames = activeFrames;
            Knockback = knockback;
            CenterY = centerY;
            HalfExtents = halfExtents;
        }

        /// <param name="steamDamage">The Florence pipe's authored random 5–10 roll.</param>
        public static FighterHazardSpec For(int hazardType, int steamDamage) => hazardType switch {
            // Orléans: a narrow ground-band boulder that rolls the whole stage.
            FighterHazardTypeID.OrleansTrebuchetDebris => new FighterHazardSpec(
                7, 90, 240, FP64.FromInt(3), FP64.Zero,
                new FPVector2(FP64.FromDouble(0.8), FP64.One)),
            // Chicago: a wide static induction grid across stage centre.
            FighterHazardTypeID.ChicagoTeslaInduction => new FighterHazardSpec(
                6, 90, 360, FP64.FromInt(3), FP64.Zero,
                new FPVector2(FP64.FromInt(4), FP64.FromInt(2))),
            // Paris: a narrow full-height sweeping column; 0 damage, drains meter.
            FighterHazardTypeID.ParisDampeningBeam => new FighterHazardSpec(
                0, 90, 360, FP64.Zero, FP64.FromInt(3),
                new FPVector2(FP64.One, FP64.FromInt(6))),
            // Vesuvius: the ground warning marker before the rock drops.
            FighterHazardTypeID.VesuviusRockfall => new FighterHazardSpec(
                8, 90, 240, FP64.FromInt(4), FP64.Zero,
                new FPVector2(FP64.One, FP64.FromDouble(0.25))),
            // Nassau: a targeting grid, then a half-second launching explosion.
            FighterHazardTypeID.NassauMortar => new FighterHazardSpec(
                10, 90, 30, FP64.FromInt(5), FP64.Zero,
                new FPVector2(FP64.FromInt(2), FP64.FromInt(2))),
            // Alexandria: a long-lived quicksand patch that only grips the grounded.
            FighterHazardTypeID.AlexandriaSinkhole => new FighterHazardSpec(
                2, 90, 480, FP64.One, FP64.Zero,
                new FPVector2(FP64.FromInt(2), FP64.One)),
            // Berlin: a tall narrow light column; harmless until you loiter in it.
            FighterHazardTypeID.BerlinSearchlight => new FighterHazardSpec(
                8, 90, 480, FP64.FromInt(3), FP64.FromInt(3),
                new FPVector2(FP64.FromDouble(1.2), FP64.FromInt(5))),
            // Globe: no damaging region at all — the crowd watches the whole stage.
            FighterHazardTypeID.GlobeAudienceHeckle => new FighterHazardSpec(
                5, 90, 600, FP64.FromInt(2), FP64.Zero,
                new FPVector2(FP64.FromInt(10), FP64.FromInt(10))),
            // Gettysburg: the widest band, a 2 s sight line, then a heavy strike.
            FighterHazardTypeID.GettysburgArtillery => new FighterHazardSpec(
                12, 120, 30, FP64.FromInt(6), FP64.FromDouble(0.75),
                new FPVector2(FP64.FromInt(5), FP64.FromDouble(0.75))),
            // Florence (and the legacy fallback): the shipped steam pipe.
            _ => new FighterHazardSpec(
                steamDamage, 90, 360, FP64.FromInt(3), FP64.Zero,
                new FPVector2(FP64.FromInt(2), FP64.FromInt(2)))
        };
    }

    /// <summary>
    /// Deterministic Chronal Orbs (design §10 "Chronal Orb Pickups", M23/M24).
    /// Orbs materialize at a seeded pick among the stage's authored orb anchors,
    /// skipping any anchor that already holds an orb, at a seeded time inside the
    /// selected frequency window (<see cref="FighterOrbSpawnRules"/>), live for
    /// 15 s, and are collected by walking or jumping through them. Every draw —
    /// timing, type, anchor and the contested tie-break — threads
    /// <c>FighterMatchComponent.RandomState0/1</c>.
    ///
    /// <para>Effect types: 0 Temporal Restoration, 1 Chronal Haste, 2 Tectonic
    /// Uplift, 3 Temporal Aegis, and — only when the Meter pickups sub-toggle is
    /// on — 4 Resonance Surge (+15 meter).</para>
    /// </summary>
    public sealed class FighterOrbSystem : ISystem {
        private const int OrbLifetimeFrames = FighterOrbSpawnRules.LifetimeFrames;
        private const int BuffDurationFrames = 480;
        /// <summary>M24 effect type 4.</summary>
        public const int ResonanceSurgeEffectType = 4;
        private static readonly FP64 MaxInfluence = FP64.FromInt(100);
        private static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);

        private readonly FighterStageGeometry _geometry;

        public FighterOrbSystem(FighterStageGeometry geometry = null) {
            _geometry = geometry ?? FighterStageGeometry.Default;
        }

        public void Update(ref Frame frame) {
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            if (match.MatchState != 1 || match.ItemsEnabled == 0 || match.ItemFrequency <= 0) return;
            // V7.3 ruling #9: Chronal Orbs are off in Sudden Death (hazards
            // stay forced on). EnterSuddenDeath also clears any live orb, so
            // nothing spawns, lingers, or awards past the transition.
            if (match.SuddenDeathActive == 1) return;

            if (match.NextOrbSpawnFrames > 0) match.NextOrbSpawnFrames--;
            if (match.NextOrbSpawnFrames <= 0) {
                SpawnOrb(ref frame, ref match);
            }

            var filter = frame.Filter<FighterOrbComponent>();
            while (filter.Next(out EntityRef orbEntity)) {
                ref FighterOrbComponent orb = ref frame.Get<FighterOrbComponent>(orbEntity);
                orb.LifetimeFrames--;
                if (orb.LifetimeFrames <= 0) {
                    frame.DestroyEntity(orbEntity);
                    continue;
                }

                // A freshly spawned orb exists for at least one full tick before
                // it can be collected: a fighter camping the anchor otherwise
                // consumes it inside the spawn Update, and the orb never renders
                // (or registers in OrbCount) for even a single frame.
                if (orb.LifetimeFrames >= OrbLifetimeFrames - 1) continue;

                int picker = FindPicker(ref frame, ref match, in orb);
                if (picker < 0 || !FighterEntityQueries.TryFindFighter(ref frame, picker, out EntityRef fighterEntity)) continue;
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(fighterEntity);
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(fighterEntity);
                ref FighterDefenseComponent defense = ref frame.Get<FighterDefenseComponent>(fighterEntity);
                ApplyEffect(ref fighter, ref runtime, ref defense, orb.EffectType);
                frame.DestroyEntity(orbEntity);
            }
        }

        /// <summary>
        /// One spawn attempt plus the next gap. Draw order is fixed — next gap,
        /// effect type, then the anchor among the free ones. An attempt with every
        /// authored anchor occupied spawns nothing (the "skip an anchor already
        /// holding an orb" rule taken to its end) and skips only the anchor draw;
        /// occupancy is itself deterministic state, so both rollback peers take
        /// the same branch.
        /// </summary>
        private void SpawnOrb(ref Frame frame, ref FighterMatchComponent match) {
            var random = new DeterministicRandom(1);
            random.SetFullState(match.RandomState0, match.RandomState1);
            match.NextOrbSpawnFrames = FighterOrbSpawnRules.DrawSpawnGap(ref random, match.ItemFrequency);
            int effect = random.NextInt(0, FighterOrbSpawnRules.EffectTypeCount(match.MeterPickupsEnabled == 1));

            bool spawn = true;
            FPVector2 position;
            if (_geometry.OrbAnchors.Length > 0) {
                int freeCount = CountFreeAnchors(ref frame);
                if (freeCount <= 0) {
                    spawn = false;
                    position = FPVector2.Zero;
                } else {
                    position = FreeAnchorAt(ref frame, random.NextInt(0, freeCount));
                }
            } else {
                // The legacy flat arena keeps its historical random spawn range.
                position = new FPVector2(
                    random.NextFixed(FP64.FromInt(-7), FP64.FromInt(7)),
                    FP64.FromDouble(0.5));
            }
            (match.RandomState0, match.RandomState1) = random.GetFullState();
            if (!spawn) return;

            EntityRef entity = frame.CreateEntity();
            frame.Add(entity, new FighterOrbComponent {
                EntityID = match.NextEntityID++,
                EffectType = effect,
                LifetimeFrames = OrbLifetimeFrames,
                Position = position,
                HalfExtents = new FPVector2(FP64.FromDouble(0.45), FP64.FromDouble(0.45))
            });
        }

        /// <summary>True when a live orb already sits on this authored anchor.</summary>
        private static bool AnchorOccupied(ref Frame frame, in FPVector2 anchor) {
            var filter = frame.Filter<FighterOrbComponent>();
            while (filter.Next(out EntityRef orbEntity)) {
                FPVector2 position = frame.GetReadOnly<FighterOrbComponent>(orbEntity).Position;
                if (position.x.RawValue == anchor.x.RawValue && position.y.RawValue == anchor.y.RawValue) return true;
            }
            return false;
        }

        private int CountFreeAnchors(ref Frame frame) {
            int free = 0;
            for (int index = 0; index < _geometry.OrbAnchors.Length; index++) {
                if (!AnchorOccupied(ref frame, in _geometry.OrbAnchors[index])) free++;
            }
            return free;
        }

        /// <summary>The <paramref name="freeIndex"/>-th unoccupied anchor, in authored order.</summary>
        private FPVector2 FreeAnchorAt(ref Frame frame, int freeIndex) {
            for (int index = 0; index < _geometry.OrbAnchors.Length; index++) {
                if (AnchorOccupied(ref frame, in _geometry.OrbAnchors[index])) continue;
                if (freeIndex-- == 0) return _geometry.OrbAnchors[index];
            }
            return _geometry.OrbAnchors[0];
        }

        private static int FindPicker(ref Frame frame, ref FighterMatchComponent match, in FighterOrbComponent orb) {
            bool playerOne = false;
            bool playerTwo = false;
            var filter = frame.Filter<FighterStateComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref frame.GetReadOnly<FighterStateComponent>(entity);
                if (!FighterEntityQueries.Overlaps(
                        in orb.Position, in orb.HalfExtents,
                        in fighter.Position, in FighterHalfExtents)) continue;
                if (fighter.PlayerID == 0) playerOne = true;
                else if (fighter.PlayerID == 1) playerTwo = true;
            }
            if (playerOne && playerTwo) return ResolveContestedPicker(ref match);
            if (playerOne) return 0;
            if (playerTwo) return 1;
            return -1;
        }

        /// <summary>
        /// Design ~3134: a same-tick contested orb resolves by a seeded coin flip,
        /// not the old <c>EntityID % 2</c> parity (with the first orb entity ID
        /// fixed, that parity always awarded the same slot). The draw threads
        /// <c>FighterMatchComponent.RandomState0/1</c> through
        /// <see cref="DeterministicRandom"/> like every other match draw, so it is
        /// random per contest yet bit-identical under rollback resimulation.
        /// </summary>
        internal static int ResolveContestedPicker(ref FighterMatchComponent match) {
            var random = new DeterministicRandom(1);
            random.SetFullState(match.RandomState0, match.RandomState1);
            int picker = random.NextInt(0, 2);
            (match.RandomState0, match.RandomState1) = random.GetFullState();
            return picker;
        }

        internal static void ApplyEffect(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            ref FighterDefenseComponent defense,
            int effectType) {
            if (effectType == 0) {
                int heal = fighter.MaxHP * 20 / 100;
                int healed = fighter.CurrentHP + heal;
                fighter.CurrentHP = healed < fighter.MaxHP ? healed : fighter.MaxHP;
            } else if (effectType == 1) {
                runtime.SpeedBuffFrames = BuffDurationFrames;
            } else if (effectType == 2) {
                runtime.JumpBuffFrames = BuffDurationFrames;
            } else if (effectType == ResonanceSurgeEffectType) {
                // M24 Resonance Surge: +15 Ultimate Meter, clamped at the full bar.
                FP64 surged = fighter.Influence + FP64.FromInt(FighterOrbSpawnRules.ResonanceSurgeMeter);
                fighter.Influence = surged < MaxInfluence ? surged : MaxInfluence;
            } else {
                // V7.6 D02e (Package 11 A1b): Temporal Aegis is an ACTIVE FLAG
                // with at most one per recipient and no time expiry, not a
                // growing charge count. A pickup while already protected is
                // consumed with no second charge, reserve, duration, HP, meter
                // or replacement reward — which is exactly what an idempotent
                // assignment to 1 expresses. AegisHits on the runtime component
                // is retained as a presentation mirror only.
                defense.AegisActive = 1;
                runtime.AegisHits = 1;
            }
        }
    }

    /// <summary>
    /// Deterministic Area-execution effects. Zones periodically pulse damage and/or
    /// status onto the opponent standing inside them and can grant the owner a
    /// movement-speed bonus while the owner overlaps (Einstein's Relativity Rift).
    /// Pulses carry no knockback or hitstun, so they never interrupt movement —
    /// with one per-type exception: Shakespeare's Tempest pulses shove the
    /// opponent away from the storm center with real knockback and zero damage.
    /// </summary>
    public sealed class FighterZoneSystem : ISystem {
        // Refreshed every frame the owner overlaps; decays one frame at a time in
        // TickCounters, so the bonus expires immediately after leaving the zone.
        private const int OwnerBonusRefreshFrames = 2;
        private static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);

        /// <summary>
        /// A01/D10 (Package 13 W1): the two fighters' authored hit contracts,
        /// static match configuration like the combat and projectile systems
        /// hold. A Special zone pulse reads its slot's block class and signed
        /// knockback here.
        /// </summary>
        private readonly FighterHitContractTable _contracts;

        public FighterZoneSystem(FighterHitContractTable contracts = null) {
            _contracts = contracts ?? FighterHitContractTable.Default;
        }

        public void Update(ref Frame frame) {
            // A04 (Package 13 W1): a fallen owner's zones are gone before any of
            // them can pulse again — no final attack, no pending damage.
            FighterStockLossRules.DespawnOwnedByFallenFighters(ref frame);
            var filter = frame.Filter<FighterZoneComponent>();
            while (filter.Next(out EntityRef zoneEntity)) {
                ref FighterZoneComponent zone = ref frame.Get<FighterZoneComponent>(zoneEntity);
                // E01 (Package 13 W7a): a collapsing Relativity Rift no longer
                // ticks, buffs or expires on its own clock — it pulls its caught
                // opponents to its centre and launches them, then is gone.
                if (FighterRiftRules.IsCollapsing(in zone)) {
                    if (FighterRiftRules.AdvanceCollapse(ref frame, ref zone, _contracts)) {
                        frame.DestroyEntity(zoneEntity);
                    }
                    continue;
                }
                // S01/S02 (Package 13 W7a): the Tempest windbox pushes on every
                // frame of its authored life (12 pushes of 0.25 = 3 units).
                if (FighterTempestRules.IsTempest(in zone)) {
                    FighterTempestRules.Push(ref frame, ref zone);
                }
                zone.LifetimeFrames--;
                if (zone.LifetimeFrames <= 0) {
                    // Leonardo's Golden Ratio (zone type 21) ends with a radial
                    // knockback pulse shoving the fighter away from the spiral
                    // center (damage-free; the 3 damage ticks land beforehand).
                    if (zone.ZoneTypeID == (int)FighterCharacterID.Leonardo * 10 + 1) {
                        ApplySpiralExpiryKnockback(ref frame, in zone);
                    }
                    // A02/D15 (Package 13 W6): the Cosmological Constant's final
                    // launch and the Vitruvian Matrix's explosion are no longer
                    // zero-damage expiry pulses here — they are the authored
                    // FinaleDamage hits the combat system delivers when the
                    // cinematic hold ends.
                    frame.DestroyEntity(zoneEntity);
                    continue;
                }

                if (zone.GrantsOwnerSpeedBonus != 0
                    && FighterEntityQueries.TryFindFighter(ref frame, zone.OwnerPlayerID, out EntityRef ownerEntity)) {
                    ref readonly FighterStateComponent owner = ref frame.GetReadOnly<FighterStateComponent>(ownerEntity);
                    if (FighterEntityQueries.Overlaps(
                            in zone.Position, in zone.HalfExtents,
                            in owner.Position, in FighterHalfExtents)) {
                        ref FighterRuntimeComponent ownerRuntime = ref frame.Get<FighterRuntimeComponent>(ownerEntity);
                        ownerRuntime.ZoneSpeedBonusFrames = OwnerBonusRefreshFrames;
                    }
                }

                // E03 (Package 13 W7a): Time Dilation inside the rift is refreshed
                // every frame and lingers the authored 0.5 s after leaving.
                if (FighterRiftRules.IsRift(in zone)) {
                    FighterRiftRules.RefreshLinger(ref frame, in zone);
                }

                // S01 (Package 13 W7a): The Tempest is a windbox, not a hit — a
                // push applied above, and it never reaches the pulse below.
                if (FighterTempestRules.IsTempest(in zone)) continue;

                // Cleopatra's Sandstorm Vortex drags the opponent toward its
                // center every frame: a small positional shift that never touches
                // velocity, so the pull stays impulse-free and snapshot-safe with
                // no extra state.
                if (zone.ZoneTypeID == (int)FighterCharacterID.Cleopatra * 10 + 2) {
                    ApplyVortexPull(ref frame, in zone);
                }

                // Einstein's Cosmological Constant sucks the opponent toward the
                // singularity every frame: the same impulse-free positional drag
                // as the vortex, at black-hole strength.
                if (zone.ZoneTypeID == (int)FighterCharacterID.Einstein * 10 + FighterUltimateRules.UltimateSlot) {
                    ApplySingularityPull(ref frame, in zone);
                }
                // Cleopatra's Wrath of the Nile (ultimate-slot zone type 43)
                // reuses the same impulse-free positional drag toward the
                // storm's eye for its flood current.
                if (zone.ZoneTypeID == (int)FighterCharacterID.Cleopatra * 10 + FighterUltimateRules.UltimateSlot) {
                    ApplyVortexPull(ref frame, in zone);
                }
                // Tesla's Wardenclyffe Cataclysm column (ultimate zone type 53)
                // draws the opponent toward its center with the same impulse-free
                // positional drag as the Sandstorm Vortex, so its pull stays
                // snapshot-safe with no extra state.
                if (zone.ZoneTypeID == (int)FighterCharacterID.Tesla * 10 + FighterUltimateRules.UltimateSlot) {
                    ApplyVortexPull(ref frame, in zone);
                }

                if (zone.TickFramesRemaining > 0) zone.TickFramesRemaining--;
                if (zone.TickFramesRemaining > 0) continue;
                zone.TickFramesRemaining = zone.TickIntervalFrames;

                int targetPlayerID = zone.OwnerPlayerID == 0 ? 1 : 0;
                if (!FighterEntityQueries.TryFindFighter(ref frame, zone.OwnerPlayerID, out EntityRef attackerEntity)
                    || !FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) continue;
                ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
                if (!FighterEntityQueries.Overlaps(
                        in zone.Position, in zone.HalfExtents,
                        in target.Position, in FighterHalfExtents)) continue;

                ref FighterStateComponent attacker = ref frame.Get<FighterStateComponent>(attackerEntity);
                ref FighterRuntimeComponent attackerRuntime = ref frame.Get<FighterRuntimeComponent>(attackerEntity);
                ref FighterVerbComponent attackerVerb = ref frame.Get<FighterVerbComponent>(attackerEntity);
                ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
                ref FighterVerbComponent targetVerb = ref frame.Get<FighterVerbComponent>(targetEntity);
                ref FighterDefenseComponent targetDefense = ref frame.Get<FighterDefenseComponent>(targetEntity);
                ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);

                // Package 12 W4 (GAP-10a): the Lorentz chain no longer keys off
                // Static Charge (F07 made that a pure interrupt). It reads this
                // Tesla's Conductive MARK after the pulse lands — see
                // TryResolveLorentzChain below.
                int pulseDamage = zone.Damage;

                // Two zones carry real impulse on their pulses; every other zone
                // stays an impulse-free tick by design. Lincoln's Emancipator
                // (zone type 31) knocks the target up with the authored Special 1
                // knockback. (Shakespeare's Tempest is a windbox since Package 13
                // W7a and never reaches this pulse — see FighterTempestRules.)
                FP64 pulseKnockback = FP64.Zero;
                int pulseHitstunFrames = 0;
                // A01/D10 (Package 13 W1): the two Special impulse pulses read
                // their slot's contract — its signed knockback vector and its
                // block class (the Emancipator is a Shield-Breaker).
                FighterAbilityHitData pulseContract = default;
                // Package 13 W7b (M03): the Requiem Chord's burst. Every pulse is a
                // Special direct hit that holds the target in the authored
                // hitstun; only the last carries the authored knockback, so the
                // three pulses land together. The Emancipator (formerly zone 31)
                // is a ground wave now and has no zone branch.
                bool requiemBurst = zone.ZoneTypeID == FighterReachKitRules.RequiemBurstZone;
                bool requiemFinalPulse = requiemBurst && zone.LifetimeFrames <= zone.TickIntervalFrames;
                if (requiemBurst) {
                    pulseContract = _contracts.For(zone.OwnerPlayerID, FighterHitContractTable.SlotSpecialOne);
                    pulseHitstunFrames = pulseContract.HitstunFrames > 0 ? pulseContract.HitstunFrames : 12;
                    if (!requiemFinalPulse) pulseContract = default;
                    else pulseKnockback = pulseContract.KnockbackX;
                } else if (zone.ZoneTypeID == (int)FighterCharacterID.Lincoln * 10 + FighterUltimateRules.UltimateSlot
                    && zone.LifetimeFrames <= zone.TickIntervalFrames) {
                    // Union Indestructible's final smash shatters the fence pen:
                    // only the last tick of the ultimate zone carries the massive
                    // authored finisher knockback (earlier smashes stay
                    // impulse-free so the Root pen keeps holding).
                    pulseKnockback = frame.GetReadOnly<FighterTuningComponent>(attackerEntity).UltimateKnockback;
                    pulseHitstunFrames = UnionFinisherHitstunFrames;
                }
                // A02/D15 (Package 13 W6): the Grand Crusade's final charge,
                // Symphony of Sorrow's closing strike and Hamlet's finale are the
                // authored FinaleDamage hits the combat system delivers when the
                // cinematic hold ends, so every pulse of those zones is an
                // impulse-free ultimate tick. Union Indestructible (no finale)
                // keeps its final-pulse knockback above. Package 13 W5: The
                // Freedom Line (zone 93) is a finale Ultimate too — its seven
                // pulses are impulse-free and its 20-damage final rush is the
                // finale; the retired Tidewater Tempest (83) branch is gone.

                // Ultimate-slot zones (ZoneTypeID % 10 == FighterUltimateRules
                // .UltimateSlot) hit with the ultimate attack class so they
                // bypass shields like every other ultimate.
                bool ultimateZone = zone.ZoneTypeID % 10 == FighterUltimateRules.UltimateSlot;
                int pulseAttackClass = ultimateZone
                    ? FighterDamageRules.UltimateAttackClass
                    : FighterDamageRules.SpecialAttackClass;
                // Zone pulses are construct-class damage: no hitstop (V7.3 —
                // only direct player-authored hits carry the freeze).
                //
                // V7.6 D03g (Package 11 A1b): a persistent zone tick NEVER
                // reclaims the owner's Rally pool — "Relativity Rift's chip
                // ticks and every Sandstorm Vortex tick are excluded, including
                // the Vortex's final launching tick". These pulses defaulted to
                // collectsEcho: true and therefore did reclaim.
                // V7.6 D03h: an Ultimate-spawned zone earns its caster zero
                // damage-dealt meter; ZoneTypeID's slot digit is the origin.
                bool pulseVector = !ultimateZone && pulseContract.HasKnockbackVector;
                bool pulseLanded = FighterDamageRules.ApplyFighterHit(
                    ref attacker, ref attackerRuntime, ref attackerVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                    pulseAttackClass, pulseDamage,
                    pulseVector ? pulseContract.KnockbackX : pulseKnockback,
                    pulseHitstunFrames,
                    zone.StatusType, zone.StatusFrames, zone.StatusIntensity, zone.Position.x,
                    creditInfluence: !ultimateZone,
                    // The Requiem burst is the chord's own direct hit, not a
                    // persistent zone tick, so it reclaims Rally (D03g) like
                    // Story's pulses.
                    collectsEcho: requiemBurst,
                    appliesHitstop: false,
                    shieldBreaker: !ultimateZone && pulseContract.ShieldBreaker,
                    hasKnockbackVector: pulseVector,
                    knockbackVertical: pulseContract.KnockbackY,
                    launches: !requiemBurst || (requiemFinalPulse && pulseContract.Launches));

                // M02: the first pulse of a chord execution that lands shaves
                // Fortissimo's remaining cooldown, once per execution.
                if (requiemBurst && pulseLanded && (zone.ExecutionFlags & FighterReachKitRules.FlagFortissimoShaved) == 0) {
                    zone.ExecutionFlags |= FighterReachKitRules.FlagFortissimoShaved;
                    FighterReachKitRules.ShaveFortissimo(ref attackerRuntime);
                }

                if (pulseLanded && zone.ZoneTypeID == (int)FighterCharacterID.Tesla * 10 + 2) {
                    TryResolveLorentzChain(
                        ref frame, in zone, targetEntity,
                        ref attacker, ref attackerRuntime, ref attackerVerb,
                        ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning);
                }
            }
        }

        /// <summary>
        /// Package 12 W4 (GAP-10a, F07): the Lorentz chain consumer. A pulse tick
        /// that LANDED (not blocked, absorbed or invulnerable) on a target
        /// carrying THIS Tesla's unexpired Conductive mark, while he has at least
        /// one live coil, consumes the mark through
        /// <see cref="FighterConductiveRules.TryConsumeChain"/> — keyed on the
        /// pulse's own entity ID, so the pulse chains that target at most once
        /// however many ticks it lands and a rollback replay cannot duplicate it
        /// — and resolves one chain: each live coil arcs
        /// <see cref="CoilArcDamage"/> into the target. Another Tesla's mark,
        /// Static Charge alone, or no coil never chains, and with no coil the
        /// mark is left to expire. Chain hits create no mark and never chain
        /// recursively; like every coil arc they reclaim no Rally (D03g) and
        /// carry no hitstop.
        /// </summary>
        private static void TryResolveLorentzChain(
            ref Frame frame,
            in FighterZoneComponent zone,
            EntityRef targetEntity,
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            ref FighterVerbComponent attackerVerb,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            ref FighterVerbComponent targetVerb,
            ref FighterDefenseComponent targetDefense,
            in FighterTuningComponent targetTuning) {
            // T01 (Package 13 W7a): only this Tesla's own live coils within 8
            // units of the marked target are eligible; with none the mark is
            // left to expire.
            int coils = FighterLorentzChainRules.EligibleCoils(ref frame, zone.OwnerPlayerID, in target.Position);
            if (coils <= 0) return;
            ref FighterConductiveComponent mark = ref frame.Get<FighterConductiveComponent>(targetEntity);
            // Execution IDs must be non-zero; entity IDs start at zero.
            if (!FighterConductiveRules.TryConsumeChain(ref mark, zone.OwnerPlayerID, zone.EntityID + 1)) return;
            // Consumed: the mark is spent, the per-execution guard stays set.
            mark.FramesRemaining = 0;
            mark.SourcePlayerID = -1;
            // Each eligible coil fires its own instant 8-damage arc — a direct
            // Special-class hit (meter and Rally as normal), no added launch,
            // never recursive.
            FighterLorentzChainRules.FireArcs(
                ref frame, zone.OwnerPlayerID,
                ref attacker, ref attackerRuntime, ref attackerVerb,
                ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning);
        }

        /// <summary>T01 per-coil chain arc damage (Package 13 W7a: 5 → 8; see <see cref="FighterLorentzChainRules"/>).</summary>
        internal const int CoilArcDamage = FighterLorentzChainRules.ArcDamage;
        private const int CoilObjectTypeID = 1;
        // Union Indestructible finisher: 0.5 s, matching the authored
        // HitstunDuration on lincoln/ultimate.tres.
        private const int UnionFinisherHitstunFrames = 30;

        // 0.05 world units per frame (3 px at 60 px/unit), mirrored by the Story
        // vortex's 180 px/s positional drag.
        private static readonly FP64 VortexPullPerFrame = FP64.FromDouble(0.05);

        private static void ApplyVortexPull(ref Frame frame, in FighterZoneComponent zone) {
            int targetPlayerID = zone.OwnerPlayerID == 0 ? 1 : 0;
            if (!FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) return;
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            if (target.Stocks <= 0 || target.InvulnerabilityFrames > 0) return;
            if (!FighterEntityQueries.Overlaps(
                    in zone.Position, in zone.HalfExtents,
                    in target.Position, in FighterHalfExtents)) return;
            FP64 dx = zone.Position.x - target.Position.x;
            if (dx > VortexPullPerFrame) target.Position.x += VortexPullPerFrame;
            else if (dx < -VortexPullPerFrame) target.Position.x -= VortexPullPerFrame;
            else target.Position.x = zone.Position.x;
        }

        private const int SpiralExpiryHitstunFrames = 12;
        private static readonly FP64 SpiralExpiryKnockback = FP64.FromInt(3);

        /// <summary>
        /// Golden Ratio expiry pulse: a zero-damage knockback hit pushing the
        /// opponent away from the spiral center (ApplyFighterHit resolves the push
        /// direction from the hit origin, giving the design's radial shove).
        /// </summary>
        private static void ApplySpiralExpiryKnockback(ref Frame frame, in FighterZoneComponent zone) {
            int targetPlayerID = zone.OwnerPlayerID == 0 ? 1 : 0;
            if (!FighterEntityQueries.TryFindFighter(ref frame, zone.OwnerPlayerID, out EntityRef attackerEntity)
                || !FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) return;
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            if (!FighterEntityQueries.Overlaps(
                    in zone.Position, in zone.HalfExtents,
                    in target.Position, in FighterHalfExtents)) return;

            ref FighterStateComponent attacker = ref frame.Get<FighterStateComponent>(attackerEntity);
            ref FighterRuntimeComponent attackerRuntime = ref frame.Get<FighterRuntimeComponent>(attackerEntity);
            ref FighterVerbComponent attackerVerb = ref frame.Get<FighterVerbComponent>(attackerEntity);
            ref FighterRuntimeComponent targetRuntime = ref frame.Get<FighterRuntimeComponent>(targetEntity);
            ref FighterVerbComponent targetVerb = ref frame.Get<FighterVerbComponent>(targetEntity);
            ref FighterDefenseComponent targetDefense = ref frame.Get<FighterDefenseComponent>(targetEntity);
            ref readonly FighterTuningComponent targetTuning = ref frame.GetReadOnly<FighterTuningComponent>(targetEntity);
            FighterDamageRules.ApplyFighterHit(
                ref attacker, ref attackerRuntime, ref attackerVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
                FighterDamageRules.SpecialAttackClass, 0, SpiralExpiryKnockback, SpiralExpiryHitstunFrames,
                (int)StatusType.None, 0, FP64.One, zone.Position.x,
                collectsEcho: false,
                appliesHitstop: false);
        }

        // 0.08 world units per frame — a stronger drag than the sandstorm
        // vortex's 0.05, befitting a black hole. Mirrored by the Story
        // singularity's positional pull.
        private static readonly FP64 SingularityPullPerFrame = FP64.FromDouble(0.08);

        /// <summary>
        /// Cosmological Constant per-frame pull (mirrors ApplyVortexPull): an
        /// impulse-free horizontal positional drag toward the singularity center
        /// that never touches velocity, so it stays snapshot-safe with no extra
        /// state.
        /// </summary>
        private static void ApplySingularityPull(ref Frame frame, in FighterZoneComponent zone) {
            int targetPlayerID = zone.OwnerPlayerID == 0 ? 1 : 0;
            if (!FighterEntityQueries.TryFindFighter(ref frame, targetPlayerID, out EntityRef targetEntity)) return;
            ref FighterStateComponent target = ref frame.Get<FighterStateComponent>(targetEntity);
            if (target.Stocks <= 0 || target.InvulnerabilityFrames > 0) return;
            if (!FighterEntityQueries.Overlaps(
                    in zone.Position, in zone.HalfExtents,
                    in target.Position, in FighterHalfExtents)) return;
            FP64 dx = zone.Position.x - target.Position.x;
            if (dx > SingularityPullPerFrame) target.Position.x += SingularityPullPerFrame;
            else if (dx < -SingularityPullPerFrame) target.Position.x -= SingularityPullPerFrame;
            else target.Position.x = zone.Position.x;
        }

        private static int CountLiveCoils(ref Frame frame, int ownerPlayerID) {
            int count = 0;
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterPersistentObjectComponent persistent =
                    ref frame.GetReadOnly<FighterPersistentObjectComponent>(entity);
                if (persistent.OwnerPlayerID == ownerPlayerID
                    && persistent.ObjectTypeID == CoilObjectTypeID
                    && persistent.CurrentHP > 0
                    && persistent.LifetimeFrames > 0) count++;
            }
            return count;
        }
    }
}
