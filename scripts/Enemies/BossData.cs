using Godot;

namespace FTT.Enemies {

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

        [ExportGroup("Abilities")]
        [Export] public FTT.Combat.AbilityData[] BossAbilities;
        [Export] public float[] AbilityWeights;

        [ExportGroup("Animation")]
        [Export] public SpriteFrames SpriteFramesResource;
    }
}
