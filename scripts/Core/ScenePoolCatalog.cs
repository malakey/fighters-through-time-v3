using Godot;
using System;

namespace FTT.Core {

    [GlobalClass]
    public partial class ScenePoolCatalog : Resource {
        public const string DefaultPath = "res://resources/Pools/scene_pool_catalog.tres";

        [Export] public int SchemaVersion = 1;
        [Export] public ScenePoolCatalogEntry[] Scenes = Array.Empty<ScenePoolCatalogEntry>();

        public ScenePoolConfig Find(string scenePath) {
            foreach (ScenePoolCatalogEntry entry in Scenes ?? Array.Empty<ScenePoolCatalogEntry>()) {
                if (entry?.ScenePath == scenePath) return entry.PoolConfig;
            }
            return null;
        }

        public static ScenePoolCatalog LoadDefault() => ResourceLoader.Load<ScenePoolCatalog>(DefaultPath);
    }
}
