using Godot;
using System;

namespace FTT.Enemies {

    /// <summary>
    /// Execution shapes shared by elite secondary abilities and every boss attack.
    /// Package 4 Section 3.1: one archetype system so a single executor can drive
    /// both, and no enemy ability ever mixes into character/Fighter ability data.
    /// </summary>
    public enum EnemyAbilityArchetype {
        MeleeStrike,
        Projectile,
        Shockwave,
        ChargeDash,
        ShieldBubble,
        AreaPulse,
        SummonMinions,
        Teleport,
        /// <summary>
        /// V7.3 (Chrono-Warden rework): spawns a persistent zone at the target
        /// position CAPTURED at cast (the player's position when the telegraph
        /// began), radius <see cref="EnemyAbilityData.PulseRadius"/>, living
        /// <see cref="EnemyAbilityData.FieldDurationSeconds"/>, applying and
        /// refreshing the authored status while a player stands inside.
        /// APPEND-ONLY enum: resources serialize the ordinal — never reorder.
        /// </summary>
        PersistentFieldAtTarget,
        /// <summary>
        /// V7.6 F14 (Package 11 A7a — the Eraser's Siphon Snare): a
        /// <b>cast → attachment check → channel</b>, not a hitbox. At active-start
        /// the executor runs ONE attachment check against the living player within
        /// <see cref="EnemyAbilityData.PulseRadius"/> centre-to-centre with
        /// unobstructed line of sight through authored solid cover, and on success
        /// builds a <see cref="SiphonTetherChannel"/> that drains
        /// <see cref="EnemyAbilityData.MeterDrainPerSecond"/> Influence points per
        /// live second, capped at <see cref="EnemyAbilityData.MeterDrainCap"/> for
        /// the whole cast and at <see cref="EnemyAbilityData.FieldDurationSeconds"/>
        /// of tether. It deals no HP damage and applies no hitstun, knockback,
        /// status, Rally echo or hitstop; the caster is stationary and cannot
        /// attack while it maintains.
        /// APPEND-ONLY enum: resources serialize the ordinal — never reorder.
        /// </summary>
        SiphonTether
    }

    /// <summary>Distance band a boss may select this ability from.</summary>
    public enum EnemyAbilityRangeClass {
        Melee,
        Ranged,
        Any
    }

    /// <summary>
    /// Story-only enemy/boss ability description. Deliberately NOT
    /// <see cref="FTT.Combat.AbilityData"/>: that resource requires a CharacterID
    /// and feeds Fighter loadouts, which enemy content must never touch.
    /// </summary>
    [GlobalClass]
    public partial class EnemyAbilityData : Resource {
        [ExportGroup("Identity")]
        [Export] public int SchemaVersion = 1;
        [Export] public string AbilityID = "";
        [Export] public string DisplayNameKey = "";
        [Export] public EnemyAbilityArchetype Archetype = EnemyAbilityArchetype.MeleeStrike;
        [Export] public EnemyAbilityRangeClass RangeClass = EnemyAbilityRangeClass.Any;

        [ExportGroup("Timing")]
        [Export(PropertyHint.Range, "0,240,1")] public int TelegraphFrames = 12;
        [Export(PropertyHint.Range, "1,240,1")] public int ActiveFrames = 8;
        [Export(PropertyHint.Range, "0,240,1")] public int RecoveryFrames = 14;
        [Export] public float CooldownSeconds = 3.0f;
        /// <summary>Boss weighted-random selection weight. Ignored for elite cycling.</summary>
        [Export] public float SelectionWeight = 1.0f;

        [ExportGroup("Damage")]
        [Export] public float Damage = 10f;
        /// <summary>Negative X pulls the target toward the caster (grav-beam archetypes).</summary>
        [Export] public Vector2 KnockbackForce = new(3f, -1.5f);
        [Export] public float HitstunDuration = 0.2f;

        [ExportGroup("Block Classification (V7.2)")]
        /// <summary>
        /// Guard-Crush: 2 charges when blocked (orange telegraph). Elite-tier
        /// signature abilities are Guard-Crush implicitly regardless of this
        /// flag; author it on boss abilities that threaten the stance.
        /// </summary>
        [Export] public bool IsGuardCrushing;
        /// <summary>
        /// Unblockable (red telegraph). Honored on bosses only — never authored
        /// on standard or elite mobs; the executor ignores it for them.
        /// </summary>
        [Export] public bool IsUnblockable;
        /// <summary>
        /// V7.6 (Package 11 A7a): forces this ability to resolve <b>Basic-class</b>
        /// against the block — one charge, white/yellow telegraph, circle glyph —
        /// even when it is an elite signature ability, which
        /// <see cref="EnemyController.BeginAttack"/> would otherwise flag
        /// Guard-Crush implicitly. Authored on the Eraser's Null Lance because the
        /// design is explicit that "blocking is never a trap": a suppression bolt
        /// the stance cannot answer for one charge would make raising the shield
        /// the wrong move. Overrides <see cref="IsGuardCrushing"/> as well.
        /// </summary>
        [Export] public bool ForcesBasicBlockClass;

        [ExportGroup("Hitbox")]
        [Export] public Vector2 HitboxSize = new(48f, 48f);
        [Export] public Vector2 HitboxOffset = new(36f, -34f);

        [ExportGroup("Projectile")]
        [Export] public float ProjectileSpeed = 420f;
        [Export(PropertyHint.Range, "1,16,1")] public int ProjectileCount = 1;
        [Export] public float ProjectileSpreadDegrees;
        [Export] public float ProjectileLifetime = 3f;
        /// <summary>Downward acceleration in px/s^2; non-zero produces lobbed arcs.</summary>
        [Export] public float ProjectileGravity;
        [Export] public bool PiercesTargets;

        [ExportGroup("Status")]
        [Export] public FTT.Core.StatusType AppliedStatus = FTT.Core.StatusType.None;
        [Export] public float StatusDuration;
        [Export] public float StatusIntensity = 1f;

        [ExportGroup("Special")]
        [Export] public string SummonEnemyID = "";
        [Export(PropertyHint.Range, "0,8,1")] public int SummonCount = 2;
        [Export] public float ShieldDuration = 4f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float ShieldDamageReduction = 0.5f;
        [Export] public float DashSpeed = 700f;
        [Export(PropertyHint.Range, "1,240,1")] public int DashDurationFrames = 18;
        [Export] public float PulseRadius = 140f;
        [Export] public float TeleportRangeMin = 160f;
        [Export] public float TeleportRangeMax = 320f;
        /// <summary>V7.3: lifetime of a PersistentFieldAtTarget zone. Additive
        /// export — older resources load the 4 s default with no schema bump.</summary>
        [Export] public float FieldDurationSeconds = 4f;
        /// <summary>
        /// V7.6 F14 (A7a): Influence points a <see cref="EnemyAbilityArchetype.SiphonTether"/>
        /// drains per LIVE second of tether. Fractions are preserved — the drain is
        /// <c>min(currentMeter, MeterDrainPerSecond × liveDeltaSeconds)</c> per tick,
        /// never a lump sum at attachment.
        /// </summary>
        [Export] public float MeterDrainPerSecond = 10f;
        /// <summary>
        /// V7.6 F14 (A7a): the hard ceiling on Influence a single Siphon cast may
        /// take, regardless of channel length. 10/s × 3 s = 30, so the cap and the
        /// duration agree; the cap is authored separately so a retune of one cannot
        /// silently move the other.
        /// </summary>
        [Export] public float MeterDrainCap = 30f;

        [ExportGroup("Presentation")]
        [Export] public Color TelegraphTint = new(1f, 0.55f, 0.2f);
        /// <summary>String hook consumed by Package 8 VFX/SFX binding; no asset required now.</summary>
        [Export] public string PresentationEventID = "";

        /// <summary>Active-phase length in frames, accounting for the dash override.</summary>
        public int ResolvedActiveFrames => Archetype == EnemyAbilityArchetype.ChargeDash
            ? Math.Max(1, DashDurationFrames)
            : Math.Max(1, ActiveFrames);

        /// <summary>Telegraph + active + recovery, the full 60 Hz cost of one use.</summary>
        public int TotalFrames =>
            Math.Max(0, TelegraphFrames) + ResolvedActiveFrames + Math.Max(0, RecoveryFrames);

        /// <summary>Negative authored horizontal knockback means "drag the target in".</summary>
        public bool IsPullKnockback => KnockbackForce.X < 0f;

        public bool SpawnsProjectiles =>
            Archetype is EnemyAbilityArchetype.Projectile or EnemyAbilityArchetype.Shockwave;

        public bool UsesMeleeHitbox =>
            Archetype is EnemyAbilityArchetype.MeleeStrike
                or EnemyAbilityArchetype.ChargeDash
                or EnemyAbilityArchetype.AreaPulse;

        public bool HasValidIdentity() => !string.IsNullOrWhiteSpace(AbilityID);

        /// <summary>
        /// Archetype-required field validation used by the content tests: every
        /// archetype must carry the data its executor branch reads.
        /// </summary>
        public bool HasValidArchetypeFields() {
            if (!HasValidIdentity()) return false;
            if (TelegraphFrames < 0 || ActiveFrames < 1 || RecoveryFrames < 0) return false;
            return Archetype switch {
                EnemyAbilityArchetype.Projectile or EnemyAbilityArchetype.Shockwave =>
                    ProjectileSpeed > 0f && ProjectileCount >= 1 && ProjectileLifetime > 0f,
                EnemyAbilityArchetype.ChargeDash => DashSpeed > 0f && DashDurationFrames >= 1,
                EnemyAbilityArchetype.ShieldBubble => ShieldDuration > 0f && ShieldDamageReduction > 0f,
                EnemyAbilityArchetype.AreaPulse => PulseRadius > 0f,
                EnemyAbilityArchetype.SummonMinions =>
                    !string.IsNullOrWhiteSpace(SummonEnemyID) && SummonCount >= 1,
                EnemyAbilityArchetype.Teleport =>
                    TeleportRangeMax >= TeleportRangeMin && TeleportRangeMax > 0f,
                EnemyAbilityArchetype.PersistentFieldAtTarget =>
                    PulseRadius > 0f && FieldDurationSeconds > 0f,
                EnemyAbilityArchetype.SiphonTether =>
                    PulseRadius > 0f && FieldDurationSeconds > 0f && MeterDrainPerSecond > 0f,
                _ => HitboxSize.X > 0f && HitboxSize.Y > 0f
            };
        }
    }
}
