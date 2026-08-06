using Godot;

namespace FTT.Core {

    [GlobalClass]
    public partial class PoolDefinition : Resource {
        [Export] public string PoolID = "";
        [Export] public PackedScene SceneTemplate;
        [Export] public int WarmUpCount = 10;
        [Export] public int MaxCapacity = 50;
        [Export] public PoolOverflowPolicy OverflowPolicy = PoolOverflowPolicy.Grow;

        public bool HasValidBudget() =>
            !string.IsNullOrWhiteSpace(PoolID) &&
            SceneTemplate != null &&
            WarmUpCount >= 0 &&
            MaxCapacity > 0 &&
            WarmUpCount <= MaxCapacity;
    }
}
