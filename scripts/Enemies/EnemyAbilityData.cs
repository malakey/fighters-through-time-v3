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
        Teleport
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
                _ => HitboxSize.X > 0f && HitboxSize.Y > 0f
            };
        }
    }
}
