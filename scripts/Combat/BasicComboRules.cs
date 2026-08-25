namespace FTT.Combat {

    /// <summary>
    /// One character's authored basic-string profile (V7.1 "Normals Are the
    /// Character", applied 2026-08-22). The three-hit chassis is universal —
    /// hitstun, active/recovery frames, hit-2 startup, knockback multipliers,
    /// the chain window, and every cancel rule are shared — while three axes
    /// are authored per character:
    /// <list type="bullet">
    /// <item><b>Speed</b>: opener and finisher startup (ground 5–8 / 14–17).</item>
    /// <item><b>Damage shape</b>: per-hit multipliers in tenths whose sum is
    /// pinned at 33, so a full string is always 3.3x BasicAttackDamage and the
    /// special/ultimate damage anchors survive untouched.</item>
    /// <item><b>Reach</b>: hitbox width/height percent of the template (Story
    /// pixels and the sim's world-unit reach scale together).</item>
    /// </list>
    /// Guarantees preserved by construction: hit 1 -> hit 2 stays uncancelable
    /// on a standing victim (only opener/finisher startups vary; the connect-to-
    /// connect gap to hit 2 never exceeds hit 1's 30-frame hitstun), and the
    /// slowest finisher startup (17) keeps hit 2 -> 3 inside hit 2's 40 frames.
    /// </summary>
    public sealed class BasicStringProfile {
        public readonly int[] GroundStartupFrames;
        public readonly int[] AerialStartupFrames;
        /// <summary>Per-hit damage multipliers in tenths; always sums to 33.</summary>
        public readonly int[] DamageTenths;
        /// <summary>Hitbox width / sim horizontal reach, percent of template (85–120).</summary>
        public readonly int ReachWidthPercent;
        /// <summary>Hitbox height / sim vertical reach, percent of template.</summary>
        public readonly int ReachHeightPercent;

        // === V7.1 string riders (Tier 2, applied 2026-08-22) ===
        // One small authored rule per character, cashing the kit briefs'
        // standing promises through the ordinary hit-payload path — no rider
        // gets bespoke collision or spawning logic.

        /// <summary>
        /// Status the finisher applies ((int)FTT.Core.StatusType; 0 = none).
        /// Tesla's magnetizing jab (StaticCharge — its stun window equals the
        /// finisher's own 24-frame hitstun, so it is pure Lorentz-chain priming)
        /// and Cleopatra's venom mark live here.
        /// </summary>
        public readonly int FinisherStatusType;
        public readonly int FinisherStatusFrames;
        public readonly int FinisherStatusIntensityMilli;

        /// <summary>
        /// Hit 2's vertical launch scale in tenths (10 = the template bridge
        /// lift). Lincoln's heavy upward second swing authors 20.
        /// </summary>
        public readonly int Hit2VerticalLaunchTenths;

        /// <summary>
        /// Finisher knockback multiplier in tenths (45 = the shared 4.5x).
        /// Mozart's spacing shove authors 55; the finisher-separation guarantee
        /// only ever grows with this.
        /// </summary>
        public readonly int FinisherKnockbackTenths;

        public BasicStringProfile(
            int groundOpenerStartup, int groundFinisherStartup,
            int aerialOpenerStartup, int aerialFinisherStartup,
            int damageTenthsHit1, int damageTenthsHit2, int damageTenthsHit3,
            int reachWidthPercent, int reachHeightPercent,
            int finisherStatusType = 0,
            int finisherStatusFrames = 0,
            int finisherStatusIntensityMilli = 1000,
            int hit2VerticalLaunchTenths = 10,
            int finisherKnockbackTenths = 45) {
            GroundStartupFrames = new[] {
                groundOpenerStartup, BasicComboRules.GroundStartupFrames[1], groundFinisherStartup };
            AerialStartupFrames = new[] {
                aerialOpenerStartup, BasicComboRules.AerialStartupFrames[1], aerialFinisherStartup };
            DamageTenths = new[] { damageTenthsHit1, damageTenthsHit2, damageTenthsHit3 };
            ReachWidthPercent = reachWidthPercent;
            ReachHeightPercent = reachHeightPercent;
            FinisherStatusType = finisherStatusType;
            FinisherStatusFrames = finisherStatusFrames;
            FinisherStatusIntensityMilli = finisherStatusIntensityMilli;
            Hit2VerticalLaunchTenths = hit2VerticalLaunchTenths;
            FinisherKnockbackTenths = finisherKnockbackTenths;
        }
    }

    /// <summary>
    /// Canonical 60 Hz timings and chain rules for the universal three-hit basic
    /// combo, shared by both modes. Story consumes these through
    /// <c>PlayerController</c>'s frame timelines (and the authored combat
    /// animation library mirrors the same numbers); Fighter Mode applies them in
    /// fixed-point authoritative state (<c>scripts/FighterSim/</c>). One source —
    /// the modes must not drift (design pillar: the same move must behave the
    /// same in Story and Fighter). This file must stay free of Godot types so the
    /// deterministic simulation can reference it.
    /// </summary>
    public static class BasicComboRules {
        public const int ComboHits = 3;

        // Grounded string TEMPLATE: startup / active / recovery per hit
        // (totals 27/30/45). Active and recovery frames are universal; per-
        // character opener/finisher startups live in the string profiles below.
        public static readonly int[] GroundStartupFrames = { 6, 7, 15 };
        public static readonly int[] GroundActiveFrames = { 6, 7, 9 };
        public static readonly int[] GroundRecoveryFrames = { 15, 16, 21 };

        // Aerial string TEMPLATE (totals 25/28/40).
        public static readonly int[] AerialStartupFrames = { 5, 6, 12 };
        public static readonly int[] AerialActiveFrames = { 7, 8, 10 };
        public static readonly int[] AerialRecoveryFrames = { 13, 14, 18 };

        /// <summary>
        /// The template profile: the exact universal string every character
        /// shipped with before V7.1. Unknown character IDs resolve to this.
        /// </summary>
        public static readonly BasicStringProfile TemplateStringProfile =
            new(6, 15, 5, 12, 8, 10, 15, 100, 100);

        // === V7.1 per-character string profiles (applied 2026-08-22) ===
        // The differentiation table the roster's Range and Skill Floor columns
        // promise. Rushdown/scout kits open in 5, heavies in 8; damage shapes
        // deviate only where the archetype demands it (Joan front-loaded
        // pressure, Lincoln back-loaded payoff) and always sum to 33 tenths;
        // reach makes Cleopatra's long scepter and Lincoln's short fat rail
        // true in melee. Authored here — the one rulebook — never in a second
        // copy. Pinned by BasicStringProfileTests.
        private static readonly System.Collections.Generic.Dictionary<string, BasicStringProfile> StringProfiles =
            new() {
                //                                 gOpen gFin aOpen aFin  d1  d2  d3  width height
                { "joan",        new BasicStringProfile(5, 14, 4, 11, 10, 10, 13,  90, 110) },
                { "pocahontas",  new BasicStringProfile(5, 14, 4, 11,  8, 10, 15,  95, 100) },
                // Cleopatra's finisher "marks" (kit brief): a light Venom
                // (0.5 intensity, 2 s = 2 chip) riding the damage status slot,
                // so it survives her own vortex slow into the nest loop.
                { "cleopatra",   new BasicStringProfile(6, 15, 5, 12,  8, 10, 15, 120, 100,
                    finisherStatusType: (int)FTT.Core.StatusType.Venom,
                    finisherStatusFrames: 120,
                    finisherStatusIntensityMilli: 500) },
                { "leonardo",    new BasicStringProfile(6, 15, 5, 12,  8, 10, 15, 115, 100) },
                // Mozart's finisher "pushes enemies away" (kit brief): the
                // spacing shove, 5.5x knockback on a low base.
                { "mozart",      new BasicStringProfile(6, 15, 5, 12,  8, 10, 15, 105, 100,
                    finisherKnockbackTenths: 55) },
                // Tesla's finisher magnetizes "with a brief Static Charge"
                // (kit brief): 0.4 s — equal to the finisher's own hitstun, so
                // it adds no lockdown and exists purely to prime Lorentz chains.
                { "tesla",       new BasicStringProfile(6, 15, 5, 12,  8, 10, 15, 100, 100,
                    finisherStatusType: (int)FTT.Core.StatusType.StaticCharge,
                    finisherStatusFrames: 24) },
                { "einstein",    new BasicStringProfile(7, 16, 6, 13,  8, 10, 15, 110, 100) },
                { "shakespeare", new BasicStringProfile(7, 16, 6, 13,  8, 10, 15, 105, 100) },
                // Lincoln's "heavy upward vertical swing that launches" hit 2
                // (kit brief): double the bridge's vertical lift.
                { "lincoln",     new BasicStringProfile(8, 17, 7, 14,  7,  9, 17,  85, 115,
                    hit2VerticalLaunchTenths: 20) }
            };

        /// <summary>
        /// Resolves a character's authored string profile; null/unknown IDs get
        /// the template. Both modes call this — Story by <c>CharacterData</c> ID,
        /// the Fighter sim through its enum-indexed cache.
        /// </summary>
        public static BasicStringProfile StringProfileFor(string characterID) =>
            characterID != null && StringProfiles.TryGetValue(characterID, out BasicStringProfile profile)
                ? profile
                : TemplateStringProfile;

        /// <summary>
        /// Victim hitstun per hit (0.5 / 0.667 / 0.4 s at 60 Hz). Hits one and
        /// two cover the gap to the next chain hit (roughly 28 frames from hit
        /// one's connect to hit two's, 38 from hit two's to the finisher's, on
        /// the buffered grounded string); the finisher's shorter stun hands off
        /// to its launch knockback, which creates the separation that ends the
        /// exchange. A *grounded* victim is no longer held helpless through the
        /// string: holding Block exits hitstun into the block stance in both
        /// modes (gameplay-feel plan §2.4). Daze and airborne hitstun stay
        /// uncancelable.
        /// </summary>
        public static readonly int[] HitstunFrames = { 30, 40, 24 };

        /// <summary>
        /// Knockback multipliers per hit, applied to the character's base
        /// basic-attack knockback in both modes. The finisher is deliberately
        /// far above the first two: at 4.5x it launches even the lightest
        /// knockback kit (Cleopatra, base 2.0 into weight 0.7) past the 2-unit
        /// melee range with margin by the time the victim's hitstun ends, so
        /// the string always ends in separation rather than an unbroken loop
        /// (gameplay-feel plan §2.5; pinned by
        /// <c>FighterFinisherSeparationTests</c> across all nine kits).
        /// The effective impulse is additionally scaled by the victim's missing
        /// HP — see <see cref="LowHealthKnockbackScale"/>.
        /// </summary>
        public static readonly float[] KnockbackMultipliers = { 1f, 1.2f, 4.5f };

        /// <summary>
        /// Low-health knockback scaling (gameplay-feel plan §2.5), shared by
        /// every damage source in both modes: the impulse is multiplied by
        /// <c>1 + missingHPFraction</c> of the victim measured *after* the
        /// hit's damage is applied — a linear 1.0x at full HP to 2.0x at 0 HP.
        /// The Fighter sim applies the same ratio in fixed point inside
        /// <c>FighterDamageRules.ApplyFighterHit</c>, which is the single
        /// chokepoint for basics, specials, ultimates, projectiles, zones,
        /// constructs and hazards; Story applies it through
        /// <c>DamageCalculator.CalculateKnockback</c>'s victim-HP overload.
        /// </summary>
        public static float LowHealthKnockbackScale(float currentHP, float maxHP) {
            if (maxHP <= 0f) return 1f;
            float clamped = currentHP < 0f ? 0f : currentHP > maxHP ? maxHP : currentHP;
            return (2f * maxHP - clamped) / maxHP;
        }

        /// <summary>
        /// Story-enemy floor on post-<c>StunResistance</c> hitstun for the
        /// basic string's hits (the melee combo's <c>combo_N</c> hitboxes;
        /// <c>EnemyController.ApplyStun</c>): however high an enemy's authored
        /// resistance, a landed string hit keeps it stunned for at least this
        /// long, so no roster enemy can act between chain hits. Other
        /// Basic-class sources (constructs) keep their authored stuns, and the
        /// Fighter sim has no stun resistance and never consumes this.
        /// </summary>
        public const int EnemyBasicStunFloorFrames = 24;

        /// <summary>
        /// Post-recovery chain window (and the mid-swing input buffer length).
        /// Design 3080: the next basic pressed inside this window continues the
        /// string; jumping, rolling, or blocking inside the recovery
        /// or this window cancels the swing and resets the chain. Held
        /// horizontal movement steers the attacker but never cancels — letting
        /// it cancel allowed a moving attacker to restart hit one faster than
        /// the authored string pace.
        /// </summary>
        public const int ChainHoldFrames = 24;

        /// <summary>
        /// Frames between block-charge regenerations while not blocking (3.0 s,
        /// settled 2026-08-22 — the design's earlier 2.0 s ask is retired).
        /// Story's <c>BlockSystem</c> divides this by 60; the Fighter sim
        /// counts it directly.
        /// </summary>
        public const int BlockChargeRegenFrames = 180;

        // === Hitstop / hitlag (V7 universal rule, applied 2026-08-23) ===
        // Every landed hit freezes BOTH the attacker and the victim for a shared
        // window scaled by the hit's damage; blocked hits use a flat 2 frames.
        // During hitstop both parties' animation, velocity, position, and timers
        // are suspended (world objects — projectiles, constructs, hazards, the
        // match clock — keep running). Lethal hits skip hitstop entirely: the
        // KO presentation owns that moment. Both modes read these numbers.

        /// <summary>Hitstop at ≤5 damage.</summary>
        public const int HitstopMinFrames = 3;
        /// <summary>Hitstop at ≥25 damage.</summary>
        public const int HitstopMaxFrames = 8;
        /// <summary>Hitstop on a blocked hit.</summary>
        public const int BlockedHitstopFrames = 2;

        /// <summary>3 frames at ≤5 damage scaling linearly to 8 at ≥25 (integer floor).</summary>
        public static int HitstopFrames(int damage) {
            if (damage <= 5) return HitstopMinFrames;
            if (damage >= 25) return HitstopMaxFrames;
            return HitstopMinFrames + (damage - 5) * (HitstopMaxFrames - HitstopMinFrames) / 20;
        }

        // === Grabs & Throws (V7.2, applied 2026-08-24) ===
        // The third side of the attack/block/grab triangle: BasicAttack pressed
        // while Block is held. Grounded only; unblockable; ignores hyper armor;
        // whiffs against hitstun, daze, airborne, rolling, and invulnerable
        // targets. One grab, one throw — no pummel, no tech, no mash-out.
        // Both modes read these numbers.

        /// <summary>Grab startup frames (attack beats grab).</summary>
        public const int GrabStartupFrames = 10;
        /// <summary>Grab active window.</summary>
        public const int GrabActiveFrames = 4;
        /// <summary>Whiff recovery — the most punishable committal in the kit.</summary>
        public const int GrabWhiffRecoveryFrames = 24;
        /// <summary>Grab reach in world units (inside jab range).</summary>
        public const float GrabReachUnits = 0.8f;
        /// <summary>Two grabs connecting the same frame bounce both back this long.</summary>
        public const int GrabClashBounceFrames = 8;
        /// <summary>The held victim's decision window before the throw resolves.</summary>
        public const int ThrowDecisionFrames = 30;
        /// <summary>Both fighters are fully invulnerable through the throw animation.</summary>
        public const int ThrowAnimationFrames = 12;
        /// <summary>No-regrab window after being thrown.</summary>
        public const int ThrowImmunityFrames = 20;
        /// <summary>Every throw deals 1.0x BasicAttackDamage — priced as position.</summary>
        public const float ThrowDamageMultiplier = 1.0f;
        /// <summary>Forward throw: horizontal 3.0x BasicAttackKnockback.</summary>
        public const float ForwardThrowKnockbackMultiplier = 3.0f;
        /// <summary>Up throw: vertical 2.5x BasicAttackKnockback (the launcher).</summary>
        public const float UpThrowKnockbackMultiplier = 2.5f;
        /// <summary>Back throw: horizontal 3.0x, both fighters turn around.</summary>
        public const float BackThrowKnockbackMultiplier = 3.0f;
        /// <summary>Story crowd bowling: a thrown mob is a projectile at this fraction.</summary>
        public const float ThrownMobCollisionDamageMultiplier = 0.5f;

        // === Echo Step (V7.1, applied 2026-08-24) ===
        // Block + Roll during the recovery frames of your own basic,
        // directional, or special attack: after a short wind-up (a ghost
        // telegraphs the destination) you snap to the position you occupied
        // ~30 frames earlier. Position only — HP, cooldowns, the Echo Pool,
        // and the world are untouched. Both modes read these numbers.

        /// <summary>Influence Meter spent per Echo Step.</summary>
        public const int EchoStepMeterCost = 30;
        /// <summary>Wind-up before the snap (the opponent's read window).</summary>
        public const int EchoStepWindupFrames = 8;
        /// <summary>Internal cooldown after use — it cannot be chained.</summary>
        public const int EchoStepCooldownFrames = 120;
        /// <summary>How far back in time the snap reaches.</summary>
        public const int EchoStepLookbackFrames = 30;

        // === Resonance Momentum (V7.1, applied 2026-08-24) ===
        // A CONNECTING basic-string finisher (Hit 3) refunds frames on both
        // special cooldowns — never hits 1-2, the directional strikes, a
        // blocked finisher, or construct/hazard damage. Capped per cooldown
        // cycle per slot so a 7 s tool cannot cycle into a 3 s tool. Both
        // modes read these numbers.

        /// <summary>Frames refunded from each running special cooldown (1.0 s).</summary>
        public const int MomentumRefundFrames = 60;
        /// <summary>Maximum refunds per cooldown cycle per slot.</summary>
        public const int MomentumRefundCapPerCycle = 2;

        // === Rally / Desperation Resonance (V7.1, applied 2026-08-24) ===
        // A fraction of every hit taken becomes a briefly reclaimable "echo":
        // 20% at full health sliding linearly to 50% near death (evaluated
        // after the hit's damage), draining to nothing over 150 frames unless
        // the victim lands a direct hit first. Both modes read these numbers;
        // the sim's fixed-point copies in FighterDamageRules derive from here.

        /// <summary>Echo fraction at full health.</summary>
        public const float EchoFractionBase = 0.20f;
        /// <summary>Added echo fraction per 1.0 of missing-HP fraction.</summary>
        public const float EchoFractionSlope = 0.30f;
        /// <summary>Linear drain window for the Echo Pool (2.5 s).</summary>
        public const int EchoDrainFrames = 150;

        // === Directional influence & landing tech (V7 pillar #4, applied 2026-08-23) ===

        /// <summary>
        /// Maximum launch-angle deflection from DI, in table steps. The victim's
        /// held direction during hitstop bends a launching hit's angle by up to
        /// ±15°, proportional to the input component perpendicular to the launch
        /// — quantized to half steps (0°, ±7.5°, ±15°) so the rotation uses
        /// process-constant sin/cos tables and stays fixed-point deterministic.
        /// DI never changes knockback magnitude, only direction.
        /// </summary>
        public const int DirectionalInfluenceSteps = 2;

        /// <summary>
        /// Story-side DI resolution — the float mirror of the sim's fixed-point
        /// <c>FighterVerbRules.ResolvePendingLaunch</c> (same thresholds, same
        /// quantized sin/cos tables). Rotates the launch (lx, ly) toward the held
        /// input (ix, iy, each in [-1, 1]) by 0°, 7.5°, or 15°; magnitude never
        /// changes. Coordinate-frame agnostic: pass launch and input in the SAME
        /// frame (Godot y-down works — the cross product flips sign consistently,
        /// so the bend still lands on the held side).
        /// </summary>
        public static void ResolveDirectionalInfluence(
            float lx, float ly, float ix, float iy, out float outX, out float outY) {
            float cross = lx * iy - ly * ix;
            float scale = System.Math.Abs(lx) + System.Math.Abs(ly);
            int step = 0;
            if (scale > 0f) {
                float magnitude = System.Math.Abs(cross);
                if (magnitude >= 0.5f * scale) step = 2;
                else if (magnitude >= 0.17f * scale) step = 1;
            }
            if (step == 0) { outX = lx; outY = ly; return; }
            float sin = step == 2 ? 0.25882f : 0.13053f;
            float cos = step == 2 ? 0.96593f : 0.99144f;
            if (cross < 0f) sin = -sin;
            outX = lx * cos - ly * sin;
            outY = lx * sin + ly * cos;
        }

        /// <summary>
        /// Landing tech (ukemi): a victim in launched hitstun who holds Block on
        /// ground contact ends hitstun and gets this many frames of invulnerable
        /// in-place recovery (no actions, no movement). Missing the tech rides
        /// the full hitstun out.
        /// </summary>
        public const int LandingTechRecoveryFrames = 12;

        // === Directional attacks (gameplay feel batch §2.8) ===
        // Up-attack and down-air are SINGLE strikes outside the three-hit chain:
        // no chain-hold, no buffering into the string, and the combo index
        // resets. Both modes read every number below; nothing here is duplicated
        // in PlayerController or the deterministic simulation.

        /// <summary>The normal three-hit string.</summary>
        public const int VariantChain = 0;
        /// <summary>Up held + BasicAttack. Available grounded and airborne.</summary>
        public const int VariantUpAttack = 1;
        /// <summary>Airborne + Down held + BasicAttack. Grounded Down is the normal string.</summary>
        public const int VariantDownAir = 2;

        /// <summary>
        /// The one selection rule, shared by both modes. Up wins over Down, and
        /// a grounded Down-attack is deliberately just a standard string opener
        /// (Story also reads Down as Crouching, whose attack is the same string).
        /// </summary>
        public static int SelectAttackVariant(bool upHeld, bool downHeld, bool airborne) {
            if (upHeld) return VariantUpAttack;
            if (downHeld && airborne) return VariantDownAir;
            return VariantChain;
        }

        // Up-attack: startup / active / recovery (total 33). One table for both
        // grounded and airborne — there is no aerial variant of the up-attack.
        public const int UpAttackStartupFrames = 7;
        public const int UpAttackActiveFrames = 8;
        public const int UpAttackRecoveryFrames = 18;

        // Down-air: startup / active / recovery (total 32). Aerial only; landing
        // cancels it with no lag, exactly like the aerial string.
        public const int DownAirStartupFrames = 6;
        public const int DownAirActiveFrames = 10;
        public const int DownAirRecoveryFrames = 16;

        /// <summary>Victim hitstun for both directional attacks (0.5 s at 60 Hz).</summary>
        public const int DirectionalAttackHitstunFrames = 30;

        /// <summary>Both directional attacks deal a flat 1.0x <c>BasicAttackDamage</c>.</summary>
        public const float DirectionalAttackDamageMultiplier = 1.0f;

        /// <summary>
        /// Horizontal knockback factor for both directional attacks, applied to
        /// the character's base basic-attack knockback. Deliberately small: the
        /// point of these moves is vertical displacement, not spacing.
        /// </summary>
        public const float DirectionalAttackHorizontalKnockback = 0.3f;

        /// <summary>
        /// Vertical knockback factor: both directional attacks launch the victim
        /// upward at 2.5x the base knockback. The Fighter sim reaches this by
        /// passing <c>base * Horizontal</c> as the impulse magnitude with
        /// <c>Vertical / Horizontal</c> as <c>verticalKnockbackScale</c>, because
        /// <c>ApplyFighterHit</c> derives both axes from one magnitude; Story
        /// authors the two components directly on the hitbox.
        /// </summary>
        public const float DirectionalAttackVerticalKnockback = 2.5f;

        /// <summary>
        /// Story's <c>PlayerInputFrame.Vertical</c> threshold for "Up held". The
        /// deterministic simulation uses the quantized equivalent, MoveY &lt; -30.
        /// </summary>
        public const float StoryUpInputThreshold = -0.25f;

        /// <summary>Hitbox IDs for the two directional attacks (Story hit payloads).</summary>
        public const string UpAttackHitboxID = "up_attack";
        public const string DownAirHitboxID = "down_air";

        /// <summary>
        /// True for every hitbox the universal basic set produces: the three
        /// chain hits plus the two directional strikes. Story's enemy stun floor
        /// (<see cref="EnemyBasicStunFloorFrames"/>) keys on this so a landed
        /// up-attack or down-air holds a high-<c>StunResistance</c> enemy just
        /// as a chain hit does; other Basic-class sources (constructs, turrets)
        /// keep their authored short stuns.
        /// </summary>
        public static bool IsBasicStringHitbox(string hitboxID) =>
            hitboxID != null
            && (hitboxID.StartsWith("combo_", System.StringComparison.Ordinal)
                || hitboxID == UpAttackHitboxID
                || hitboxID == DownAirHitboxID);
    }
}
