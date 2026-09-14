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

        [ExportGroup("V7.6 boss behaviours (Package 11 A7b)")]
        /// <summary>
        /// T01b Option A — capped historical recovery. Once per encounter, on the
        /// first <b>nonlethal</b> crossing of the boss's first phase threshold, the
        /// boss rewinds its own position to
        /// <see cref="HistoricalRecoveryLookbackFrames"/> ago and recovers toward the
        /// HP it had then, capped at
        /// <see cref="HistoricalRecoveryHealCapFraction"/> of its difficulty-scaled
        /// maximum. Authored on the First Unbound (<c>apex_eraser</c>) alone today;
        /// every other boss loads the <c>false</c> default and behaves exactly as
        /// before. Additive export — no schema bump.
        /// </summary>
        [Export] public bool HasHistoricalRecovery;
        /// <summary>
        /// V7.5 Borrowed Legacies. In its <b>final</b> phase the boss fights with the
        /// projected Special 1 of every roster character the player did not pick
        /// (<see cref="BorrowedLegacies"/>). The set is built from the content
        /// manifest at encounter start, never authored, so it scales with the roster.
        /// Authored on the First Unbound alone today. Additive export.
        /// </summary>
        [Export] public bool BorrowsRosterLegacies;

        /// <summary>
        /// T01b: the lookback, in 60 Hz simulation frames — the contract's
        /// "180 simulation frames (3 seconds)". A global rule, not per-boss tuning,
        /// so it lives here as a constant rather than as a second authored number.
        /// </summary>
        public const int HistoricalRecoveryLookbackFrames = 180;

        /// <summary>
        /// T01b: consecutive samples the history ring retains — the contract's
        /// "181 consecutive samples with tick IDs" (the lookback plus the trigger
        /// tick itself).
        /// </summary>
        public const int HistoricalRecoveryHistorySamples = HistoricalRecoveryLookbackFrames + 1;

        /// <summary>
        /// T01b: the heal ceiling, as a fraction of the boss's current
        /// difficulty-scaled maximum HP — the contract's 20%, explicitly
        /// <b>provisional pending balance validation</b>.
        /// </summary>
        public const float HistoricalRecoveryHealCapFraction = 0.20f;

        /// <summary>
        /// T01b: frames combat stays suspended for the rewind presentation — the
        /// contract's "1.5-second (90 presentation-tick) rewind".
        /// </summary>
        public const int HistoricalRecoverySuspendFrames = 90;

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
