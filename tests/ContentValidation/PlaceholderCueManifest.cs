using System.Collections.Generic;
using Godot;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GR-AUDIO test helper (not a suite): reads <c>audio/sfx/placeholder_cue_manifest.json</c>, which
/// <c>tools/generate_placeholder_audio.py</c> writes beside the assets. A Vorbis stream cannot be
/// decoded in the test host, so the generator states each asset's audible flag, peak and length.
/// </summary>
internal static class PlaceholderCueManifest {
    public const string Path = "res://audio/sfx/placeholder_cue_manifest.json";

    internal readonly record struct Asset(bool Audible, double Peak, double Seconds);

    /// <summary>Asset path under <c>audio/sfx/</c> (without the <c>res://</c> prefix) to its record; empty when unreadable.</summary>
    public static Dictionary<string, Asset> Load() {
        var result = new Dictionary<string, Asset>();
        using Godot.FileAccess file = Godot.FileAccess.Open(Path, Godot.FileAccess.ModeFlags.Read);
        if (file == null) return result;
        Variant parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary) return result;
        foreach (var pair in parsed.AsGodotDictionary()) {
            var record = pair.Value.AsGodotDictionary();
            result[pair.Key.AsString()] = new Asset(
                record["audible"].AsBool(), record["peak"].AsDouble(), record["seconds"].AsDouble());
        }
        return result;
    }

    /// <summary>The manifest key for a stream's resource path, or null when it is not under the generator's tree.</summary>
    public static string KeyFor(AudioStream stream) {
        string path = stream?.ResourcePath ?? "";
        return path.StartsWith("res://audio/sfx/") ? path.Substring("res://".Length) : null;
    }
}
