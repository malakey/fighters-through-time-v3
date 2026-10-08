using System.Collections.Generic;
using Godot;

namespace FTT.Core {

    /// <summary>
    /// Process-lifetime cache for authored, immutable <c>.tres</c> data resources
    /// (<c>CharacterData</c>, <c>AbilityData</c>, <c>EnemyData</c>, <c>BossData</c>,
    /// <c>ResonanceGridData</c>, ...).
    ///
    /// <para><b>Why this exists.</b> Godot's resource cache only keeps a resource
    /// alive while something holds a reference. Code that loads an authored
    /// resource, reads it, and drops the reference therefore destroys the whole
    /// graph and rebuilds it on the next call. For a C#-scripted <c>.tres</c> that
    /// means a <c>CSharpInstance</c> per resource - including every scripted
    /// sub-resource reached through an <c>Array[ExtResource(script)]</c> export -
    /// is constructed and destroyed on every cycle.</para>
    ///
    /// <para>Those wrappers are <c>RefCounted</c>, and the .NET finalizer thread
    /// reaps them at unpredictable times. When a finalizer disposes one at the same
    /// moment the main thread destroys the same native object, Godot double-disposes
    /// the script instance and trips
    /// <c>CRASH_COND(gchandle.is_released())</c> in <c>mono_object_disposed_baseref</c>.
    /// The child process dies with <c>0xC000001D</c>, and the collateral before it
    /// dies is unrelated <c>ResourceLoader.Load</c> calls returning null or resources
    /// reading back zeroed. See CLAUDE.md failure signature 2.</para>
    ///
    /// <para><b>The rule.</b> Load authored data resources through here, not through
    /// <c>ResourceLoader.Load</c>/<c>GD.Load</c>, so each one is marshalled exactly
    /// once per process and never torn down. They are immutable tuning data, so a
    /// shared instance is also the semantically correct thing to hand out - runtime
    /// mutable state belongs on controllers and snapshots, never on the resource.</para>
    ///
    /// <para>Do NOT route scenes, textures, audio, or anything pooled/streamed
    /// through this cache: those are meant to be released.</para>
    /// </summary>
    public static class AuthoredResources {

        private static readonly Dictionary<string, Resource> Cache = new();

        /// <summary>Number of distinct resources currently pinned. Diagnostics only.</summary>
        public static int PinnedCount => Cache.Count;

        /// <summary>True when a live resource is pinned under <paramref name="path"/>. Diagnostics and tests only.</summary>
        public static bool IsPinned(string path) =>
            !string.IsNullOrEmpty(path) && Cache.TryGetValue(path, out Resource cached) && GodotObject.IsInstanceValid(cached);

        /// <summary>
        /// Loads an authored data resource once and pins it for the lifetime of the
        /// process. Returns null (without caching) when the path is empty or the
        /// resource does not load.
        /// </summary>
        public static T Load<T>(string path) where T : Resource {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Cache.TryGetValue(path, out Resource cached)) {
                if (GodotObject.IsInstanceValid(cached) && cached is T stillValid) return stillValid;
                Cache.Remove(path);
            }
            var loaded = ResourceLoader.Load<T>(path);
            if (loaded != null) Cache[path] = loaded;
            return loaded;
        }

        /// <summary>
        /// Pins an authored data resource that reached C# without going through
        /// <see cref="Load{T}"/>: an <c>[Export]</c> a scene file assigns, such as
        /// <c>ContentTemplateMarker.Contract</c> on every enemy, checkpoint and
        /// pickup scene. Unpinned, such a resource cycles exactly as described
        /// above: when the last scene instance holding it is freed its wrapper
        /// becomes collectable, and if the next scene load takes the still-cached
        /// native resource back <i>before</i> the finalizer has run, Godot cannot
        /// re-strengthen the collected wrapper, releases its handle, and the
        /// late finalizer then trips the same <c>gchandle.is_released()</c>
        /// FATAL. Call it from the export's setter, so the resource is pinned the
        /// first time any instance sees it. Resources without a file path
        /// (scene-local or built in code) are returned untouched, since they are
        /// owned by their scene. Returns <paramref name="resource"/>.
        /// </summary>
        public static T Pin<T>(T resource) where T : Resource {
            if (resource == null) return null;
            string path = resource.ResourcePath;
            if (string.IsNullOrEmpty(path) || path.Contains("::")) return resource;
            if (!Cache.TryGetValue(path, out Resource cached) || !GodotObject.IsInstanceValid(cached)) {
                Cache[path] = resource;
            }
            return resource;
        }
    }
}
