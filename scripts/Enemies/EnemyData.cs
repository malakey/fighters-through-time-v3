using Godot;

namespace FTT.Enemies {

    public enum EnemyTier { Standard, Elite, Boss }
    public enum DefaultBehavior { Ground, Flying }

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
        [Export] public FTT.Combat.AbilityData[] EliteAbilities;
        [Export] public float EliteAbilityCooldown = 5.0f;
        [Export] public DefaultBehavior Behavior = DefaultBehavior.Ground;

        [ExportGroup("Loot")]
        [Export] public int ChronalDustDrop = 10;
        [Export] public float ItemDropChance = 0.15f;

        [ExportGroup("Animation")]
        [Export] public SpriteFrames SpriteFramesResource;
    }

}
