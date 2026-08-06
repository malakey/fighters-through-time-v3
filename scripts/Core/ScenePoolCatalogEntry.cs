using Godot;

namespace FTT.Core {

    [GlobalClass]
    public partial class ScenePoolCatalogEntry : Resource {
        [Export(PropertyHint.File, "*.tscn")] public string ScenePath = "";
        [Export] public ScenePoolConfig PoolConfig;
    }
}
