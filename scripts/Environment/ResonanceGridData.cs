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

        public StoryStatProfile(
            int maxHPBonus,
            int blockChargeBonus,
            float moveSpeedMultiplier,
            float jumpForceMultiplier,
            float basicDamageMultiplier,
            float specialDamageMultiplier) {
            MaxHPBonus = maxHPBonus;
            BlockChargeBonus = blockChargeBonus;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            JumpForceMultiplier = jumpForceMultiplier;
            BasicDamageMultiplier = basicDamageMultiplier;
            SpecialDamageMultiplier = specialDamageMultiplier;
        }

        public static StoryStatProfile Default => new(0, 0, 1f, 1f, 1f, 1f);
    }
}
