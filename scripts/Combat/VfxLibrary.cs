using System.Collections.Generic;
using Godot;

namespace FTT.Combat {

    /// <summary>
    /// Resolves a <see cref="VfxEffectFamily"/> to its authored placeholder scene
    /// under <c>res://scenes/vfx/</c> (Package 8 B6).
    ///
    /// <para>These are <em>scenes</em>, not authored tuning data, so they load
    /// through <c>ResourceLoader</c> and not <c>AuthoredResources</c> — the
    /// crash-hygiene rule in CLAUDE.md scopes the pinning cache to C#-scripted data
    /// resources. The small dictionary here is a convenience cache, not a pin: the
    /// pools registered by <see cref="VfxEmitter.EmitScene"/> already hold a
    /// reference to each of these six scenes for as long as the pool exists.</para>
    /// </summary>
    public static class VfxLibrary {
        public const string Directory = "res://scenes/vfx/";

        private static readonly Dictionary<VfxEffectFamily, string> Paths = new() {
            { VfxEffectFamily.Burst, Directory + "VfxBurst.tscn" },
            { VfxEffectFamily.Slash, Directory + "VfxSlash.tscn" },
            { VfxEffectFamily.Beam, Directory + "VfxBeam.tscn" },
            { VfxEffectFamily.Shockwave, Directory + "VfxShockwave.tscn" },
            { VfxEffectFamily.Summon, Directory + "VfxSummon.tscn" },
            { VfxEffectFamily.Impact, Directory + "VfxImpact.tscn" }
        };

        private static readonly Dictionary<VfxEffectFamily, PackedScene> Cache = new();

        /// <summary>Authored scene path for a family. Never null.</summary>
        public static string PathFor(VfxEffectFamily family) =>
            Paths.TryGetValue(family, out string path) ? path : Paths[VfxEffectFamily.Burst];

        /// <summary>Every authored family path, for content validation.</summary>
        public static IReadOnlyDictionary<VfxEffectFamily, string> AllPaths => Paths;

        /// <summary>
        /// Loads a family's scene. Returns null when the scene is missing rather
        /// than throwing — nothing gameplay-side may depend on a VFX resource.
        /// </summary>
        public static PackedScene Load(VfxEffectFamily family) {
            if (Cache.TryGetValue(family, out PackedScene cached) && GodotObject.IsInstanceValid(cached)) {
                return cached;
            }
            string path = PathFor(family);
            if (!ResourceLoader.Exists(path)) return null;
            var scene = ResourceLoader.Load<PackedScene>(path);
            if (scene != null) Cache[family] = scene;
            return scene;
        }
    }
}
