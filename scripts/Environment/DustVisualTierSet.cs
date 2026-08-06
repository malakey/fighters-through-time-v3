using Godot;

namespace FTT.Environment {

    [GlobalClass]
    public partial class DustVisualTierSet : Resource {
        [Export] public int MediumThreshold = 6;
        [Export] public int LargeThreshold = 25;
        [Export] public Texture2D SmallTexture;
        [Export] public Texture2D MediumTexture;
        [Export] public Texture2D LargeTexture;

        public Texture2D GetTexture(int dustAmount) {
            if (dustAmount >= LargeThreshold) return LargeTexture ?? MediumTexture ?? SmallTexture;
            if (dustAmount >= MediumThreshold) return MediumTexture ?? SmallTexture;
            return SmallTexture;
        }
    }
}
