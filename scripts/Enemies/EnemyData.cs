using Godot;

namespace FTT.Enemies {

    public enum EnemyTier { Standard, Elite, Boss }
    public enum DefaultBehavior { Ground, Flying }

    [GlobalClass]
    public partial class EnemyData : Resource {
        [ExportGroup("Identity")]
        [Export] public string EnemyID = "";
        [Export] public string DisplayName = "";
        [Export] public EnemyTier Tier = EnemyTier.Standard;

        [ExportGroup("Stats")]
        [Export] public int MaxHP = 50;
        [Export] public float MoveSpeed = 3.0f;
        [Export] public float AggroRadius = 8.0f;
        [Export] public float DeAggroRadius = 12.0f;
        [Export] public float AttackRange = 1.5f;
        [Export] public float AttackDamage = 5.0f;
        [Export] public float AttackCooldown = 2.0f;
        [Export] public float StunResistance = 0f;
        [Export] public DefaultBehavior Behavior = DefaultBehavior.Ground;

        [ExportGroup("Loot")]
        [Export] public int ChronalDustDrop = 10;
        [Export] public float ItemDropChance = 0.15f;

        [ExportGroup("Animation")]
        [Export] public SpriteFrames SpriteFramesResource;
    }

    [GlobalClass]
    public partial class BossData : Resource {
        [ExportGroup("Identity")]
        [Export] public string BossID = "";
        [Export] public string DisplayName = "";

        [ExportGroup("Stats")]
        [Export] public int MaxHP = 500;
        [Export] public float MoveSpeed = 4.0f;
        [Export] public float AttackRange = 2.0f;
        [Export] public float RestCooldown = 1.5f;

        [ExportGroup("Phases")]
        [Export] public float[] PhaseThresholds = { 0.75f, 0.5f, 0.25f };

        [ExportGroup("Abilities")]
        [Export] public FTT.Combat.AbilityData[] BossAbilities;
        [Export] public float[] AbilityWeights;

        [ExportGroup("Animation")]
        [Export] public SpriteFrames SpriteFramesResource;
    }
}
