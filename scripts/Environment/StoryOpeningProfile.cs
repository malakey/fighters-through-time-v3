using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace FTT.Environment {
    /// <summary>World registration for an opening plate and its painted walkable surfaces.</summary>
    public sealed class StoryOpeningProfile {
        public const string ResourcePath = "res://resources/Presentation/story_openings.json";
        public int Level { get; init; }
        public string TexturePath { get; init; }
        public Rect2 Frame { get; init; }
        public Vector2 Spawn { get; init; }
        public float HeroHeightFraction { get; init; }
        public float ReleaseX { get; init; }
        public readonly record struct Surface(string Kind, Rect2 Rect);
        public IReadOnlyList<Surface> Surfaces { get; init; }
        private static readonly HashSet<int> WarnedLevels = new();

        public static StoryOpeningProfile ForLevel(int level) {
            try {
                if (!Godot.FileAccess.FileExists(ResourcePath)) return Missing(level, "profile resource is missing");
                using var file = Godot.FileAccess.Open(ResourcePath, Godot.FileAccess.ModeFlags.Read);
                using var document = JsonDocument.Parse(file.GetAsText());
                JsonElement root = document.RootElement;
                if (root.GetProperty("schema").GetInt32() != 1) return Missing(level, "unsupported profile schema");
                foreach (JsonElement row in root.GetProperty("profiles").EnumerateArray()) {
                    if (row.GetProperty("level").GetInt32() != level) continue;
                    Rect2 frame = Rect(row.GetProperty("frame"));
                    JsonElement spawn = row.GetProperty("spawn");
                    string texture = row.GetProperty("texture").GetString();
                    float release = row.GetProperty("release_x").GetSingle();
                    var surfaces = new List<Surface>();
                    foreach (JsonElement surface in row.GetProperty("surfaces").EnumerateArray()) {
                        string kind = surface.GetProperty("kind").GetString();
                        if (kind != "floor" && kind != "platform") throw new FormatException("unknown surface kind");
                        Rect2 rect = Rect(surface.GetProperty("rect"));
                        if (rect.Size.X <= 0 || rect.Size.Y <= 0) throw new FormatException("surface size must be positive");
                        surfaces.Add(new Surface(kind, rect));
                    }
                    if (frame.Size.X <= 0 || frame.Size.Y <= 0 || release <= frame.Position.X || surfaces.Count == 0)
                        throw new FormatException("invalid opening frame, release or surfaces");
                    if (string.IsNullOrEmpty(texture) || !ResourceLoader.Exists(texture)) return Missing(level, $"plate is missing: {texture}");
                    return new StoryOpeningProfile {
                        Level = level, TexturePath = texture, Frame = frame,
                        Spawn = new Vector2(spawn[0].GetSingle(), spawn[1].GetSingle()),
                        HeroHeightFraction = row.TryGetProperty("hero_height_fraction", out var height) ? height.GetSingle() : 0f,
                        ReleaseX = release, Surfaces = surfaces
                    };
                }
                return Missing(level, "no registered profile");
            } catch (Exception exception) {
                return Missing(level, $"invalid profile: {exception.Message}");
            }
        }

        private static Rect2 Rect(JsonElement values) => new(values[0].GetSingle(), values[1].GetSingle(), values[2].GetSingle(), values[3].GetSingle());
        private static StoryOpeningProfile Missing(int level, string reason) {
            if (WarnedLevels.Add(level)) GD.PushWarning($"Story opening {level:00}: {reason}; using the existing scene.");
            return null;
        }
    }
}
