using Godot;

namespace FTT.Enemies {

    public enum EnemyTier { Standard, Elite, Boss }
    /// <summary>
    /// design-godot.md:1144 behavior triple. <c>Ground</c> is the design's Patrol;
    /// <c>StandGuard</c> holds its post with no waypoint pacing but aggros, chases,
    /// and returns to the post normally. Appended so authored 0/1 values keep meaning.
    /// </summary>
    public enum DefaultBehavior { Ground, Flying, StandGuard }

    [GlobalClass]
    public partial class EnemyData : Resource {
        [ExportGroup("Identity")]
        [Export] public int SchemaVersion = 1;
        [Export] public string EnemyID = "";
        [Export] public string DisplayName = "";
        [Export] public string DisplayNameKey = "";
        [Export] public EnemyTier Tier = EnemyTier.Standard;

        [ExportGroup("Stats")]
        [Export] public int MaxHP = 50;
        [Export] public float Weight = 1.0f;
        [Export] public float MoveSpeed = 3.0f;
        [Export] public float JumpForce = 10.0f;
        [Export] public float AggroRadius = 8.0f;
        [Export] public float DeAggroRadius = 12.0f;
        [Export] public float AttackRange = 1.5f;
        [Export] public float AttackDamage = 5.0f;
        [Export] public float AttackKnockback = 2.0f;
        [Export] public float AttackCooldown = 2.0f;
        [Export] public float StunResistance = 0f;
        [Export] public int ReactionDelayMinFrames = 30;
        [Export] public int ReactionDelayMaxFrames = 45;
        [Export] public float EliteAbilityCooldown = 5.0f;
        [Export] public DefaultBehavior Behavior = DefaultBehavior.Ground;
        /// <summary>Rift Phantom: drops the Environment bit from the body mask while chasing.</summary>
        [Export] public bool PhasesThroughWalls;
        /// <summary>Shield-carrier damage reduction for hits landing on the facing side.</summary>
        [Export(PropertyHint.Range, "0,0.95,0.01")] public float FrontalDamageReduction;

        [ExportGroup("Attacks")]
        /// <summary>
        /// Authored primary attack. When null the controller falls back to a
        /// MeleeStrike synthesized from the legacy scalar fields below, so older
        /// resources keep working unchanged.
        /// </summary>
        [Export] public EnemyAbilityData PrimaryAttack;
        [Export(PropertyHint.Range, "0,240,1")] public int AttackTelegraphFrames = 14;
        [Export(PropertyHint.Range, "1,240,1")] public int AttackActiveFrames = 12;
        [Export(PropertyHint.Range, "0,240,1")] public int AttackRecoveryFrames = 16;
        /// <summary>Elite secondary abilities, cycled sequentially per design Section 6.</summary>
        [Export] public EnemyAbilityData[] EliteAbilities;
        /// <summary>
        /// M20 (Package 12 W9): the <b>standard-tier</b> secondary-ability path,
        /// opt-in per resource, so a mob line can carry a signature archetype
        /// without being promoted to Elite (no stagger budget, no Guard-Crush
        /// implicit, no elite dust). A Teleport entry is the engage blink (see
        /// <c>EnemyController.TryEngageBlink</c>); any other entry joins the same
        /// sequential alternation elites use. Ignored on elites, which keep
        /// <see cref="EliteAbilities"/>. Additive export — no schema bump.
        /// </summary>
        [Export] public EnemyAbilityData[] SecondaryAbilities;
        /// <summary>Cooldown between standard-tier secondary uses, in seconds.</summary>
        [Export] public float SecondaryAbilityCooldown = 6.0f;

        [ExportGroup("Loot")]
        [Export] public int ChronalDustDrop = 10;
        /// <summary>
        /// Multiplier on the difficulty profile's random-item chance (1.0 neutral),
        /// NOT an absolute probability. Dust remains the flat authored value.
        /// </summary>
        [Export(PropertyHint.Range, "0,4,0.05")] public float ItemDropChance = 1.0f;

        [ExportGroup("Animation")]
        [Export] public SpriteFrames SpriteFramesResource;
        /// <summary>Placeholder silhouette tint so each roster entry reads distinctly.</summary>
        [Export] public Color PlaceholderTint = Colors.White;

        public bool HasEliteAbilities => Tier == EnemyTier.Elite && EliteAbilities is { Length: > 0 };

        /// <summary>M20: a standard-tier resource that opted into secondary abilities.</summary>
        public bool HasSecondaryAbilities => Tier == EnemyTier.Standard && SecondaryAbilities is { Length: > 0 };

        /// <summary>The tier's secondary list: elite abilities for an elite, M20's for a standard, else null.</summary>
        public EnemyAbilityData[] ActiveSecondaryAbilities =>
            HasEliteAbilities ? EliteAbilities : HasSecondaryAbilities ? SecondaryAbilities : null;

        /// <summary>The tier's secondary cooldown.</summary>
        public float ActiveSecondaryCooldown =>
            HasEliteAbilities ? EliteAbilityCooldown : SecondaryAbilityCooldown;
    }

}
