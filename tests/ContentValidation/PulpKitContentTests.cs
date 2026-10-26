using System.Collections.Generic;
using System.Text.Json;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// UR-ART: every texture and icon layer the pulp UI kit's catalog
/// (<c>assets/sprites/ui/pulp/pulp_kit.json</c>, written by
/// <c>tools/asset_src/pulp/write_catalog.py</c>) lists exists, loads as a texture of the
/// stated size, and every nine-slice entry has margins that leave a centre column (a pill may use its whole height as corner).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpKitContentTests {

    private const string Root = "res://assets/sprites/ui/pulp/";

    private static JsonDocument Catalog() {
        using FileAccess fa = FileAccess.Open(Root + "pulp_kit.json", FileAccess.ModeFlags.Read);
        return JsonDocument.Parse(fa.GetAsText());
    }

    [TestCase]
    public void EveryCatalogFileLoadsAsATextureOfTheStatedSize() {
        var issues = new List<string>();
        int files = 0;
        using JsonDocument doc = Catalog();
        foreach (JsonProperty p in doc.RootElement.EnumerateObject()) {
            JsonElement e = p.Value;
            if (e.ValueKind != JsonValueKind.Object) {
                continue;
            }
            bool sized = e.TryGetProperty("size", out JsonElement size) && size.GetArrayLength() == 2;
            var names = new List<string>();
            bool layered = e.TryGetProperty("files", out JsonElement list);
            if (layered) {
                foreach (JsonElement f in list.EnumerateArray()) {
                    names.Add(f.GetString());
                }
            } else if (e.TryGetProperty("file", out JsonElement file) && !file.GetString().Contains('{')) {
                names.Add(file.GetString());
            }
            foreach (string name in names) {
                files++;
                string path = Root + name;
                if (name.EndsWith(".tres")) {
                    if (!ResourceLoader.Exists(path)) {
                        issues.Add($"{p.Name}: missing {name}");
                    }
                    continue;
                }
                Texture2D tex = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
                if (tex == null) {
                    issues.Add($"{p.Name}: cannot load {name}");
                } else if (sized && !layered && (tex.GetWidth() != size[0].GetInt32() || tex.GetHeight() != size[1].GetInt32())) {
                    issues.Add($"{p.Name}: {name} is {tex.GetWidth()}x{tex.GetHeight()}, catalog says {size[0]}x{size[1]}");
                } else if (sized && layered && p.Name.StartsWith("ico_") && (tex.GetWidth() != size[0].GetInt32() || tex.GetHeight() != size[1].GetInt32())) {
                    issues.Add($"{p.Name}: layer {name} is {tex.GetWidth()}x{tex.GetHeight()}, catalog says {size[0]}x{size[1]}");
                }
            }
        }
        if (issues.Count > 0) {
            AssertThat(string.Join(" | ", issues)).IsEqual("");
        }
        AssertThat(files >= 120).IsTrue();
    }

    [TestCase]
    public void EveryNineSliceLeavesACentreSlice() {
        var issues = new List<string>();
        int slices = 0;
        using JsonDocument doc = Catalog();
        foreach (JsonProperty p in doc.RootElement.EnumerateObject()) {
            JsonElement e = p.Value;
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("nine_slice_margins_LTRB", out JsonElement m)) {
                continue;
            }
            slices++;
            int w = e.GetProperty("size")[0].GetInt32();
            int h = e.GetProperty("size")[1].GetInt32();
            if (m[0].GetInt32() + m[2].GetInt32() >= w || m[1].GetInt32() + m[3].GetInt32() > h) {
                issues.Add($"{p.Name}: margins leave no centre column or overrun the height in {w}x{h}");
            }
        }
        if (issues.Count > 0) {
            AssertThat(string.Join(" | ", issues)).IsEqual("");
        }
        AssertThat(slices >= 30).IsTrue();
    }

    [TestCase]
    public void EveryIconHasItsThreeLayers() {
        var issues = new List<string>();
        int icons = 0;
        using JsonDocument doc = Catalog();
        foreach (JsonProperty p in doc.RootElement.EnumerateObject()) {
            if (!p.Name.StartsWith("ico_")) {
                continue;
            }
            icons++;
            int n = p.Value.GetProperty("files").GetArrayLength();
            if (n != 3) {
                issues.Add($"{p.Name}: {n} layers");
            }
        }
        if (issues.Count > 0) {
            AssertThat(string.Join(" | ", issues)).IsEqual("");
        }
        AssertThat(icons >= 26).IsTrue();
    }
}
