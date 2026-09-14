using Godot;
using System;

namespace FTT.Enemies {

    /// <summary>design-godot.md Section 6 boss attack-selection modes.</summary>
    public enum BossAttackPattern {
        WeightedRandom,
        DistanceBased
    }

    [GlobalClass]
    public partial class BossData : Resource {
        [ExportGroup("Identity")]
        [Export] public int SchemaVersion = 1;
        [Export] public string BossID = "";
        [Export] public string DisplayName = "";
        [Export] public string DisplayNameKey = "";

        [ExportGroup("Stats")]
        [Export] public int MaxHP = 500;
        [Export] public bool IsKnockbackImmune = true;
        [Export] public float MoveSpeed = 4.0f;
        [Export] public float AttackDamage = 18.0f;
        [Export] public float AttackKnockback = 4.0f;
        [Export] public float AttackRange = 2.0f;
        [Export] public float RestCooldown = 1.5f;
        [Export] public float MeleeRangeThreshold = 3.0f;
        [Export] public float RangedRangeThreshold = 8.0f;
        [Export] public int ReactionDelayMinFrames = 4;
        [Export] public int ReactionDelayMaxFrames = 8;

        [ExportGroup("Phases")]
        [Export] public float[] PhaseThresholds = { 0.75f, 0.5f, 0.25f };
        [Export] public float PhaseTransitionInvincibilityDuration = 2.0f;
        /// <summary>Per-phase move-speed multiplier; index 0 is phase 0 (opening phase).</summary>
        [Export] public float[] PhaseSpeedMultipliers = Array.Empty<float>();

        [ExportGroup("Loot")]
        /// <summary>Package 11 A10 (F05): every one of the sixteen bosses pays
        /// <b>25</b>, as a single physical Large pickup at the arena centre.
        /// Repeated phases of one boss share this one reward.</summary>
        [Export] public int ChronalDustDrop = 25;

        [ExportGroup("Abilities")]
        [Export] public BossAttackPattern AttackPattern = BossAttackPattern.DistanceBased;
        [Export] public EnemyAbilityData[] BossAbilities;
        /// <summary>
        /// Parallel to <see cref="BossAbilities"/>: the earliest phase index each
        /// ability becomes selectable in. Missing entries default to phase 0.
        /// </summary>
        [Export] public int[] AbilityMinPhase = Array.Empty<int>();

        [ExportGroup("Interruption")]
        [Export] public bool InterruptibleDuringTelegraph;
        [Export] public float InterruptDamageThreshold = 25f;

        [ExportGroup("Animation")]
        [Export] public SpriteFrames SpriteFramesResource;
        [Export] public Color PlaceholderTint = Colors.White;

        public int PhaseCount => (PhaseThresholds?.Length ?? 0) + 1;

        /// <summary>Earliest phase an ability index unlocks in; 0 when unauthored.</summary>
        public int GetAbilityMinPhase(int abilityIndex) {
            if (AbilityMinPhase == null || abilityIndex < 0 || abilityIndex >= AbilityMinPhase.Length) return 0;
            return Math.Max(0, AbilityMinPhase[abilityIndex]);
        }

        /// <summary>Move-speed multiplier for a phase; 1.0 when unauthored.</summary>
        public float GetPhaseSpeedMultiplier(int phaseIndex) {
            if (PhaseSpeedMultipliers == null || phaseIndex < 0 || phaseIndex >= PhaseSpeedMultipliers.Length) {
                return 1f;
            }
            float value = PhaseSpeedMultipliers[phaseIndex];
            return value <= 0f ? 1f : value;
        }
    }
}
