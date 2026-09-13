using System.Collections.Generic;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Serialized as an int in every authored <c>.tres</c> (<c>Type = 1</c> is
    /// Major) — APPEND ONLY, never reorder, never renumber.
    /// <see cref="Traversal"/> (V7.6) is the per-character movement-identity
    /// rule node: it carries an <c>AbilityModifierKey</c> and no stat lane.
    /// </summary>
    public enum ResonanceNodeType { Minor, Major, Traversal = 2 }

    [GlobalClass]
    public partial class ResonanceGridData : Resource {
        /// <summary>2 = V7.6 topologies.</summary>
        [Export] public int SchemaVersion = 1;
        [Export] public string GridID = "";
        [Export] public string CharacterID = "";
        [Export] public ResonanceNodeData[] Nodes;
    }

    public enum ResonanceUnlockResult {
        Unlocked,
        AlreadyUnlocked,
        MissingNode,
        MissingPrerequisite,
        InsufficientDust,
        WrongCharacter,
        /// <summary>
        /// V7.6: the node's <c>GatedAbilityID</c> names an ability slot the
        /// Legacy Unlock Schedule has not cleared yet. The node renders as a
        /// dormant star and cannot be purchased.
        /// </summary>
        AbilityLocked
    }

    /// <summary>
    /// Compile-time names for every <c>StatModifierKey</c> the resolver routes.
    /// The keys stay free-form strings in the <c>.tres</c> (the authoring and
    /// the <c>GetUnlockedStatTotal(grid, save, key)</c> API both depend on
    /// that); this class exists so code never spells one by hand and so
    /// <c>ResonanceProgressionTests</c> can prove every authored key is known.
    /// </summary>
    public static class ResonanceStatKeys {
        // === Character-wide lanes ===
        public const string MaxHP = "MaxHP";
        public const string BlockCharges = "BlockCharges";
        public const string MoveSpeed = "MoveSpeed";
        public const string JumpForce = "JumpForce";
        public const string BasicAttackDamage = "BasicAttackDamage";
        public const string SpecialDamage = "SpecialDamage";
        public const string CooldownReduction = "CooldownReduction";
        public const string AttackRange = "AttackRange";
        public const string ComboSpeed = "ComboSpeed";
        public const string BlockRecovery = "BlockRecovery";
        public const string KnockbackForce = "KnockbackForce";
        public const string ProjectileSpeed = "ProjectileSpeed";
        public const string ProjectileDamage = "ProjectileDamage";
        public const string GlideSpeed = "GlideSpeed";
        public const string GlideDuration = "GlideDuration";
        public const string ZoneRadius = "ZoneRadius";
        public const string ZoneDuration = "ZoneDuration";
        public const string PersistentDuration = "PersistentDuration";
        public const string PersistentRange = "PersistentRange";
        public const string PersistentHealth = "PersistentHealth";
        public const string StatusDuration = "StatusDuration";
        public const string StatusDamage = "StatusDamage";

        // === V7.6 new character-wide lanes ===
        /// <summary>Scales the Rally echo fraction (Einstein, Joan, Shakespeare, Lincoln).</summary>
        public const string RallyEchoFraction = "RallyEchoFraction";
        /// <summary>Scales Influence-meter accrual (Mozart's Minor Resonance).</summary>
        public const string UltimateBuildRate = "UltimateBuildRate";
        /// <summary>
        /// Scales damage dealt to Chronal Extractors. Declared for
        /// future-proofing — V7.6's three Extractor effects are all Majors, so
        /// no authored minor uses this lane. Do not invent one.
        /// </summary>
        public const string ExtractorDamage = "ExtractorDamage";

        // === V7.6 ability-scoped lanes (always paired with AbilityScope) ===
        public const string AbilityDamage = "AbilityDamage";
        public const string AbilityRange = "AbilityRange";
        public const string AbilityDuration = "AbilityDuration";
        public const string ConstructHP = "ConstructHP";

        /// <summary>The scoped lanes, which REQUIRE a non-empty AbilityScope.</summary>
        public static readonly string[] ScopedKeys = {
            AbilityDamage, AbilityRange, AbilityDuration, ConstructHP
        };

        /// <summary>
        /// Every lane that MAY carry an <c>AbilityScope</c>. V7.6 re-points
        /// <see cref="CooldownReduction"/> at single abilities (Joan's Divine
        /// Piercing, Mozart's Fortissimo Wave, Lincoln's Splitting Strike)
        /// while the character-wide spelling stays legal for older authoring.
        /// </summary>
        public static readonly string[] ScopableKeys = {
            AbilityDamage, AbilityRange, AbilityDuration, ConstructHP, CooldownReduction
        };

        /// <summary>Every key the resolver understands.</summary>
        public static readonly string[] All = {
            MaxHP, BlockCharges, MoveSpeed, JumpForce, BasicAttackDamage, SpecialDamage,
            CooldownReduction, AttackRange, ComboSpeed, BlockRecovery, KnockbackForce,
            ProjectileSpeed, ProjectileDamage, GlideSpeed, GlideDuration, ZoneRadius,
            ZoneDuration, PersistentDuration, PersistentRange, PersistentHealth,
            StatusDuration, StatusDamage, RallyEchoFraction, UltimateBuildRate,
            ExtractorDamage, AbilityDamage, AbilityRange, AbilityDuration, ConstructHP
        };

        /// <summary>
        /// V7.6 hygiene: keys a MINOR node may never carry. Stun and hitstun
        /// belong to the V7.4 Stagger Discipline; rewind charges and the
        /// Timeline Integrity drain rate are the V7.6 level timer's own
        /// numbers. Grids touch the timer only indirectly (e.g. ExtractorDamage
        /// on a Major). Enforced by <c>ResonanceHygieneTests</c>.
        /// </summary>
        public static readonly string[] BarredFromMinors = {
            "StunDuration", "HitstunDuration", "RewindCharges", "IntegrityDrainRate"
        };

        /// <summary>Ability scopes a stun/hitstun-adjacent key may never be routed into.</summary>
        public const string ConductiveFinisherMarkScope = "conductive_finisher_mark";
    }

    /// <summary>
    /// The four Legacy ability slot names a node's <c>GatedAbilityID</c> may
    /// carry. Mirrors the slot names the Legacy Unlock Schedule (A5) keys on.
    /// </summary>
    public static class ResonanceAbilitySlots {
        public const string Special1 = "special1";
        public const string Special2 = "special2";
        public const string Movement = "movement";
        public const string Ultimate = "ultimate";
        public static readonly string[] All = { Special1, Special2, Movement, Ultimate };
    }

    public readonly struct StoryStatProfile {
        public readonly int MaxHPBonus;
        public readonly int BlockChargeBonus;
        public readonly float MoveSpeedMultiplier;
        public readonly float JumpForceMultiplier;
        public readonly float BasicDamageMultiplier;
        public readonly float SpecialDamageMultiplier;
        /// <summary>Cooldown time multiplier (&lt; 1 with CooldownReduction nodes).</summary>
        public readonly float CooldownMultiplier;
        public readonly float AttackRangeMultiplier;
        public readonly float ComboSpeedMultiplier;
        public readonly float BlockRecoveryMultiplier;
        public readonly float KnockbackMultiplier;
        public readonly float ProjectileSpeedMultiplier;
        public readonly float ProjectileDamageMultiplier;
        public readonly float GlideSpeedMultiplier;
        /// <summary>Glide-window length multiplier (from "GlideDuration" nodes, e.g. Pocahontas wr2).</summary>
        public readonly float GlideDurationMultiplier;
        /// <summary>Ability-zone radius multiplier (from "ZoneRadius" nodes: Einstein u2, Leonardo a2, Cleopatra dm1).</summary>
        public readonly float ZoneRadiusMultiplier;
        /// <summary>Ability-zone lifetime multiplier (from "ZoneDuration" nodes, e.g. Cleopatra dm2).</summary>
        public readonly float ZoneDurationMultiplier;
        public readonly float PersistentDurationMultiplier;
        public readonly float PersistentRangeMultiplier;
        public readonly float PersistentHealthMultiplier;
        public readonly float StatusDurationMultiplier;
        /// <summary>Damaging-status potency multiplier (from "StatusDamage" nodes).</summary>
        public readonly float StatusIntensityMultiplier;
        /// <summary>V7.6 lane: scales the Rally echo fraction on damage taken.</summary>
        public readonly float RallyEchoFractionMultiplier;
        /// <summary>V7.6 lane: scales Influence-meter accrual.</summary>
        public readonly float UltimateBuildRateMultiplier;
        /// <summary>V7.6 lane: scales damage dealt to Chronal Extractors.</summary>
        public readonly float ExtractorDamageMultiplier;

        /// <summary>
        /// V7.6 ability-scoped lanes, keyed (statKey, abilityScope). One
        /// dictionary instead of six-times-N fields — <see cref="GetScoped"/>
        /// is the only read path and <c>CharacterFactory</c> copies the whole
        /// bucket in one assignment. Never null after construction.
        /// </summary>
        public readonly IReadOnlyDictionary<ScopedStatKey, float> ScopedMultipliers;

        /// <summary>
        /// Reads an ability-scoped lane. Returns <paramref name="fallback"/>
        /// (1.0 by default, the neutral multiplier) when nothing was authored
        /// for that pair — locked and unauthored nodes contribute nothing.
        /// </summary>
        public float GetScoped(string statKey, string abilityScope, float fallback = 1f) {
            if (ScopedMultipliers == null
                || string.IsNullOrWhiteSpace(statKey)
                || string.IsNullOrWhiteSpace(abilityScope)) return fallback;
            return ScopedMultipliers.TryGetValue(new ScopedStatKey(statKey, abilityScope), out float value)
                ? value
                : fallback;
        }

        public StoryStatProfile(
            int maxHPBonus,
            int blockChargeBonus,
            float moveSpeedMultiplier,
            float jumpForceMultiplier,
            float basicDamageMultiplier,
            float specialDamageMultiplier,
            float cooldownMultiplier = 1f,
            float attackRangeMultiplier = 1f,
            float comboSpeedMultiplier = 1f,
            float blockRecoveryMultiplier = 1f,
            float knockbackMultiplier = 1f,
            float projectileSpeedMultiplier = 1f,
            float projectileDamageMultiplier = 1f,
            float glideSpeedMultiplier = 1f,
            float persistentDurationMultiplier = 1f,
            float persistentRangeMultiplier = 1f,
            float persistentHealthMultiplier = 1f,
            float statusDurationMultiplier = 1f,
            float statusIntensityMultiplier = 1f,
            float glideDurationMultiplier = 1f,
            float zoneRadiusMultiplier = 1f,
            float zoneDurationMultiplier = 1f,
            float rallyEchoFractionMultiplier = 1f,
            float ultimateBuildRateMultiplier = 1f,
            float extractorDamageMultiplier = 1f,
            IReadOnlyDictionary<ScopedStatKey, float> scopedMultipliers = null) {
            RallyEchoFractionMultiplier = rallyEchoFractionMultiplier;
            UltimateBuildRateMultiplier = ultimateBuildRateMultiplier;
            ExtractorDamageMultiplier = extractorDamageMultiplier;
            ScopedMultipliers = scopedMultipliers ?? EmptyScopes;
            MaxHPBonus = maxHPBonus;
            BlockChargeBonus = blockChargeBonus;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            JumpForceMultiplier = jumpForceMultiplier;
            BasicDamageMultiplier = basicDamageMultiplier;
            SpecialDamageMultiplier = specialDamageMultiplier;
            CooldownMultiplier = cooldownMultiplier;
            AttackRangeMultiplier = attackRangeMultiplier;
            ComboSpeedMultiplier = comboSpeedMultiplier;
            BlockRecoveryMultiplier = blockRecoveryMultiplier;
            KnockbackMultiplier = knockbackMultiplier;
            ProjectileSpeedMultiplier = projectileSpeedMultiplier;
            ProjectileDamageMultiplier = projectileDamageMultiplier;
            GlideSpeedMultiplier = glideSpeedMultiplier;
            GlideDurationMultiplier = glideDurationMultiplier;
            ZoneRadiusMultiplier = zoneRadiusMultiplier;
            ZoneDurationMultiplier = zoneDurationMultiplier;
            PersistentDurationMultiplier = persistentDurationMultiplier;
            PersistentRangeMultiplier = persistentRangeMultiplier;
            PersistentHealthMultiplier = persistentHealthMultiplier;
            StatusDurationMultiplier = statusDurationMultiplier;
            StatusIntensityMultiplier = statusIntensityMultiplier;
        }

        public static StoryStatProfile Default => new(0, 0, 1f, 1f, 1f, 1f);

        internal static readonly IReadOnlyDictionary<ScopedStatKey, float> EmptyScopes =
            new Dictionary<ScopedStatKey, float>();
    }

    /// <summary>
    /// A (stat key, ability scope) pair. A named readonly struct rather than a
    /// value tuple so the dictionary type reads cleanly across the factory,
    /// the controller and the tests.
    /// </summary>
    public readonly struct ScopedStatKey : System.IEquatable<ScopedStatKey> {
        public readonly string StatKey;
        public readonly string AbilityScope;

        public ScopedStatKey(string statKey, string abilityScope) {
            StatKey = statKey ?? "";
            AbilityScope = abilityScope ?? "";
        }

        public bool Equals(ScopedStatKey other) =>
            string.Equals(StatKey, other.StatKey, System.StringComparison.Ordinal)
            && string.Equals(AbilityScope, other.AbilityScope, System.StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ScopedStatKey other && Equals(other);

        public override int GetHashCode() => System.HashCode.Combine(StatKey, AbilityScope);

        public override string ToString() => $"{StatKey}({AbilityScope})";
    }

    /// <summary>
    /// The ability-scoped Resonance lanes a <c>PlayerController</c> carries.
    /// A null-safe wrapper over the resolver's dictionary so the controller
    /// exposes one field instead of six-times-N, and so an unpopulated Fighter
    /// loadout reads neutral without a null check at every call site.
    /// </summary>
    public readonly struct ScopedStoryStats {
        private readonly IReadOnlyDictionary<ScopedStatKey, float> _values;

        public ScopedStoryStats(IReadOnlyDictionary<ScopedStatKey, float> values) {
            _values = values;
        }

        public static ScopedStoryStats Empty => new(null);

        /// <summary>Number of authored, unlocked scoped lanes. Test surface.</summary>
        public int Count => _values?.Count ?? 0;

        public float Get(string statKey, string abilityScope, float fallback = 1f) {
            if (_values == null
                || string.IsNullOrWhiteSpace(statKey)
                || string.IsNullOrWhiteSpace(abilityScope)) return fallback;
            return _values.TryGetValue(new ScopedStatKey(statKey, abilityScope), out float value)
                ? value
                : fallback;
        }
    }
}
