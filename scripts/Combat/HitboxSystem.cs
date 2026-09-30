using Godot;
using FTT.Core;

namespace FTT.Combat {

    public enum AttackClass {
        Basic,
        Special,
        Ultimate,
        Hazard
    }

    /// <summary>
    /// M08 (Package 12 W3): how a hit meets the ordinary charge-based block — the
    /// GDD's <c>BlockClass</c> enum. <see cref="AttackClass"/> survives as the
    /// runtime classification the block and hyper-armor code already switch on;
    /// <see cref="HitClassification.AttackClassFor"/> is the one mapping between
    /// the two, so neither becomes a second canonical value.
    /// </summary>
    public enum BlockClass {
        /// <summary>Spends one charge.</summary>
        Basic = 0,
        /// <summary>
        /// A01 (Package 13 W1, 2026-09-29): an ordinary player Special spends
        /// <c>min(2, charges)</c> — leaving one charge from a full shield with
        /// the ordinary shieldstun, shattering at one or two. It no longer
        /// full-shatters; only <see cref="ShieldBreaker"/> does.
        /// </summary>
        Special = 1,
        /// <summary>Spends two charges (the V7.2 enemy classification).</summary>
        GuardCrush = 2,
        /// <summary>Bypasses the ordinary block (Ultimates, boss red telegraphs).</summary>
        Unblockable = 3,
        /// <summary>
        /// A01 (Package 13 W1): a player Special that consumes <b>every</b>
        /// remaining charge and shatters the shield outright. Appended — never
        /// renumber. The set is exactly Divine Piercing, The Emancipator and
        /// Splitting Strike; adding another needs an explicit design decision.
        /// It is a separate player classification from Story Guard-Crush, so
        /// Shield of Orléans' Guard-Crush refund never triggers from it.
        /// </summary>
        ShieldBreaker = 4
    }

    /// <summary>
    /// M08: the delivery channel of a hit. Drives the D03g Rally reclaim (only a
    /// <see cref="DirectHit"/> collects an echo), the V7.3 hitstop exemptions and
    /// the absorption rules.
    /// </summary>
    public enum HitDelivery {
        DirectHit = 0,
        /// <summary>A periodic zone / DoT pulse.</summary>
        Tick = 1,
        /// <summary>A hit authored by a persistent construct (coil, turret, nest, snare).</summary>
        Construct = 2,
        /// <summary>An environmental hazard contact.</summary>
        Hazard = 3
    }

    /// <summary>
    /// M08: what authored the hit. Drives the D03h meter rule (an
    /// <see cref="Ultimate"/>-origin hit earns its caster zero damage-dealt meter)
    /// and origin inheritance by projectiles and constructs.
    /// </summary>
    public enum HitOrigin {
        Basic = 0,
        Special = 1,
        Ultimate = 2,
        Throw = 3,
        Environment = 4
    }

    /// <summary>
    /// M08 pure mappings over the three hit enums. Engine-free; both modes read
    /// them so the "which flag means what" answer exists exactly once.
    /// </summary>
    public static class HitClassification {
        /// <summary>D03g: only a direct hit reclaims a Rally echo.</summary>
        public static bool CollectsEcho(HitDelivery delivery) => delivery == HitDelivery.DirectHit;

        /// <summary>D03h: Ultimate-origin damage earns its caster zero damage-dealt meter.</summary>
        public static bool IsUltimateOrigin(HitOrigin origin) => origin == HitOrigin.Ultimate;

        /// <summary>
        /// The runtime <see cref="AttackClass"/> an authored ability hit carries.
        /// Origin wins for the Ultimate (hyper-armor and the D03b bypass read
        /// <c>AttackClass.Ultimate</c>); otherwise the block class selects Basic
        /// or Special. GuardCrush has no AttackClass of its own — it rides as
        /// Special plus an explicit two-charge <c>BlockChargeCost</c>.
        /// </summary>
        public static AttackClass AttackClassFor(BlockClass blockClass, HitOrigin origin) {
            if (origin == HitOrigin.Ultimate) return AttackClass.Ultimate;
            return blockClass == BlockClass.Basic ? AttackClass.Basic : AttackClass.Special;
        }

        /// <summary>A01: true for the full-shatter player Special classification.</summary>
        public static bool IsShieldBreaker(BlockClass blockClass) => blockClass == BlockClass.ShieldBreaker;

        /// <summary>The explicit per-hit charge cost a block class implies (0 = the AttackClass default).</summary>
        public static int BlockChargeCostFor(BlockClass blockClass) =>
            blockClass == BlockClass.GuardCrush ? 2 : 0;

        /// <summary>
        /// Allocates a fresh per-contact identity for Story hits. Monotonic for
        /// the process; 0 is reserved for "unassigned". Story-only — the Fighter
        /// simulation never reads it, so it carries no determinism obligation.
        /// </summary>
        public static ulong NextContactId() => ++_nextContactId;
        private static ulong _nextContactId;
    }

    /// <summary>
    /// M08 (Package 12 W3): the single damage-receiver contract for Story hit
    /// targets. <see cref="IStatusEffectTarget"/> coexists with it (the GDD
    /// sketches it as a sub-interface; it is kept separate so a status-only
    /// target never has to take damage).
    ///
    /// <para>Deviation from the GDD sketch (<c>void TakeDamage</c>): the call
    /// returns the actual HP damage dealt, because Influence gain, Rally reclaim
    /// and the projectile detonate-on-contact rule all read that number — the
    /// same contract <see cref="Hurtbox.TakeHit"/> already honours.</para>
    /// </summary>
    public interface IDamageable {
        /// <summary>Resolves one hit through the receiver's full defensive pipeline; returns the HP damage dealt.</summary>
        float TakeDamage(in HitPayload hit);
        bool IsAlive { get; }
    }

    /// <summary>
    /// Complete, mode-independent description of a confirmed hit. Fighter rollback
    /// uses the same fields in its deterministic state boundary; Godot vectors here
    /// are the Story/presentation adapter and are converted at that boundary.
    /// </summary>
    public struct HitPayload {
        public int AttackerIndex;
        public int TargetIndex;
        public string AttackID;
        public string HitboxID;
        public AttackClass AttackClass;
        public float Damage;
        public Vector2 Knockback;
        public float HitstunDuration;
        public Vector2 HitOrigin;
        public bool AttackerFacingRight;
        public StatusType AppliedStatus;
        public float StatusDuration;
        public float StatusIntensity;
        public float ScreenShakeIntensity;
        public float ScreenShakeDuration;
        /// <summary>
        /// V7.2 enemy-attack classification: charges a block spends for this
        /// hit. 0 uses the class default (Basic/Hazard 1, Special 2 — A01);
        /// Guard-Crush attacks author 2. Player Specials deliberately leave it
        /// at 0: Shield of Orléans keys its refund on an explicit 2 here.
        /// </summary>
        public int BlockChargeCost;
        /// <summary>
        /// A01 (Package 13 W1): a Shield-Breaker Special — a valid block spends
        /// every remaining charge. Stamped from the ability's authored
        /// <see cref="BlockClass.ShieldBreaker"/>; never set on a construct hit.
        /// </summary>
        public bool ShieldBreaker;
        /// <summary>V7.2: boss-only red-telegraph attacks that no block answers.</summary>
        public bool Unblockable;
        /// <summary>V7.3: construct/DoT ticks carry no hitstop — only direct
        /// player-authored hits freeze. The four construct nodes (turret, coil,
        /// nest, snare) set this; the victim's hit handler skips ApplyHitstop.</summary>
        public bool ExemptFromHitstop;
        /// <summary>
        /// V7.6 F07 (Package 11 A1): a caster-owned combo MARK this hit applies,
        /// alongside — and independent of — <see cref="AppliedStatus"/>. A mark
        /// occupies no status slot, causes no action lock, and contributes zero
        /// stagger budget. <see cref="AttackerIndex"/> identifies the owner.
        /// </summary>
        public ComboMarkType ComboMark;
        /// <summary>Mark duration in frames; 0 applies nothing.</summary>
        public int ComboMarkFrames;
        /// <summary>
        /// V7.6 D03d (Package 11 A1b): the PRIMARY hit of a validated paired
        /// grab/throw event. A legal primary throw deals normal damage and
        /// applies its normal launch WITHOUT consuming Temporal Aegis or finite
        /// HP-barrier capacity — no decrement, no absorption or break, no block
        /// perk, and the skipped protection is not treated as partial damage
        /// reduction. Ordinary block is already unreachable for a held victim.
        ///
        /// <para>It is never inferred from a projectile's visual, from a broad
        /// Unblockable flag, or from all hits sharing the attacker's execution:
        /// Story's secondary thrown-mob collision is a separate projectile hit
        /// with normal defenses and must NOT set this.</para>
        /// </summary>
        public bool BypassesFiniteShields;

        // --- M08 (Package 12 W3) ---
        /// <summary>
        /// Godot instance ID of the actor that owns the hit (credit, ownership
        /// tint, self-hit rules); 0 when unknown. The Fighter simulation carries
        /// its own PlayerID and never reads this.
        /// </summary>
        public ulong SourceActorId;
        /// <summary>
        /// Per-contact identity for multi-hit, barrier and Rally dedup. 0 means
        /// "not assigned"; <see cref="Hitbox"/> allocates one per confirmed contact
        /// through <see cref="HitClassification.NextContactId"/>.
        /// </summary>
        public ulong ContactId;
        /// <summary>
        /// M05 launch flag. Data only in W3 — W3b implements grounded knockback
        /// vs launch; until then every hit with a knockback vector still launches.
        /// </summary>
        public bool Launches;
        /// <summary>Delivery channel; see <see cref="FTT.Combat.HitDelivery"/>.</summary>
        public FTT.Combat.HitDelivery Delivery;
        /// <summary>
        /// What authored the hit; see <see cref="FTT.Combat.HitOrigin"/>. The
        /// field is <c>Origin</c> because the older Vector2 <c>HitOrigin</c>
        /// field above is the contact POSITION and keeps its name.
        /// </summary>
        public FTT.Combat.HitOrigin Origin;

        // --- Package 12 W4 (GAP-14) ---
        /// <summary>
        /// F04 fence: a hit produced by a Nexus-authorized (puzzle) Ultimate
        /// cast. <see cref="Hurtbox.TakeHit"/> rejects it outright on every
        /// receiver — no damage, stagger, status, knockback, meter, Rally or
        /// drop — because the puzzle target resolves through
        /// <c>NexusResonanceSource.ResolvePuzzleTarget</c>, never through damage.
        /// A free puzzle Ultimate can therefore never become free combat.
        /// </summary>
        public bool PuzzleOnly;
    }
}
