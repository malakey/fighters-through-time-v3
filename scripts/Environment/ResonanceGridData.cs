using Godot;

namespace FTT.Environment {

    public enum ResonanceNodeType { Minor, Major }

    [GlobalClass]
    public partial class ResonanceGridData : Resource {
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
        WrongCharacter
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
            float zoneDurationMultiplier = 1f) {
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
    }
}
