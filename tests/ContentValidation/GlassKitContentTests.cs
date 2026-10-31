using System;
using System.Collections.Generic;
using System.Text.Json;
using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// TG-ART: the Temporal Glass UI kit (<c>assets/sprites/ui/glass/glass_kit.json</c>, written by
/// <c>tools/asset_src/glass/write_catalog.py</c>; plan <c>docs/UI_GLASS_2026-10-26_PLAN.md</c> 2.3). Every catalog file
/// loads at its stated size with mipmaps on, and the retired pulp kit is gone (TG-CLOSE); every nine-slice keeps a centre
/// slice and the margins of UI_SPEC_GLASS 6.4 (or a departure recorded both here and in its catalog entry); every
/// icon has a regular and a bold 64 px texture; every icon name the HUD models produce exists in the glass set; and
/// every roster member has four baked select idle frames with a feet line.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GlassKitContentTests {

    private const string Root = PulpKit.GlassDirectory;
    /// <summary>The retired pulp kit's folder (TG-CLOSE deleted it with its catalog; the ability icons moved out).</summary>
    private const string RetiredPulpFolder = "res://assets/sprites/ui/" + "pulp/";

    /// <summary>UI_SPEC_GLASS 6.4: canvas w, h; nine-slice margins L, T, R, B; expand margins L, T, R, B.</summary>
    private static readonly Dictionary<string, int[]> Spec64 = new() {
        ["glass_panel"] = new[] { 140, 140, 42, 28, 42, 56, 22, 8, 22, 36 },
        ["glass_tag"] = new[] { 40, 24, 3, 0, 9, 0, 0, 0, 0, 0 },
        ["glass_btn_normal"] = new[] { 96, 70, 2, 2, 32, 2, 0, 0, 0, 0 },
        ["glass_btn_selected"] = new[] { 96, 70, 2, 2, 32, 2, 0, 0, 0, 0 },
        ["glass_btn_pressed"] = new[] { 96, 70, 2, 2, 32, 2, 0, 0, 0, 0 },
        ["glass_btn_focus"] = new[] { 124, 98, 16, 16, 46, 16, 14, 14, 14, 14 },
        ["glass_btn_disabled"] = new[] { 100, 70, 2, 2, 32, 2, 0, 0, 0, 0 },
        ["glass_tab"] = new[] { 64, 48, 1, 0, 17, 0, 0, 0, 0, 0 },
        ["glass_tab_selected"] = new[] { 64, 48, 1, 0, 17, 0, 0, 0, 0, 0 },
        ["glass_tab_focus"] = new[] { 92, 76, 15, 14, 31, 14, 14, 14, 14, 14 },
        ["glass_field"] = new[] { 48, 44, 10, 10, 10, 10, 0, 0, 0, 0 },
        ["glass_field_focus"] = new[] { 76, 72, 24, 24, 24, 24, 14, 14, 14, 14 },
    };

    /// <summary>
    /// The recorded departures from 6.4 (each catalog entry says why in its "deviation"): the disabled chevron's
    /// 104 px canvas (a 70 px centre slice, whole 10 px dash periods for TILE), and the three full-look hover boxes
    /// the plan's 0.4 P5 overlay split needs beside the focus overlays.
    /// </summary>
    private static readonly Dictionary<string, int[]> Recorded = new() {
        ["glass_btn_disabled"] = new[] { 104, 70, 2, 2, 32, 2, 0, 0, 0, 0 },
        ["glass_btn_hover"] = new[] { 124, 98, 16, 16, 46, 16, 14, 14, 14, 14 },
        ["glass_tab_hover"] = new[] { 92, 76, 15, 14, 31, 14, 14, 14, 14, 14 },
        ["glass_field_hover"] = new[] { 76, 72, 24, 24, 24, 24, 14, 14, 14, 14 },
    };

    private static JsonDocument Read(string path) {
        using FileAccess fa = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return JsonDocument.Parse(fa.GetAsText());
    }

    private static int[] Four(JsonElement e, string key) {
        var v = new int[4];
        if (e.TryGetProperty(key, out JsonElement a)) {
            int i = 0;
            foreach (JsonElement x in a.EnumerateArray()) v[i++] = x.GetInt32();
        }
        return v;
    }

    private static bool MipmapsOn(string pngPath) {
        using FileAccess fa = FileAccess.Open(pngPath + ".import", FileAccess.ModeFlags.Read);
        return fa != null && fa.GetAsText().Contains("mipmaps/generate=true");
    }

    [TestCase]
    public void EveryCatalogFileLoadsAtItsStatedSizeWithMipmapsAndTheRetiredPulpKitIsGone() {
        var issues = new List<string>();
        int pngs = 0;
        using JsonDocument glass = Read(Root + "glass_kit.json");
        // TG-CLOSE: the pulp kit, its catalog and its fallback in PulpKit are gone; a pulp stem resolves to nothing.
        if (DirAccess.DirExistsAbsolute(RetiredPulpFolder)) {
            issues.Add($"{RetiredPulpFolder} still exists");
        }
        foreach (string pulpStem in new[] { "panel_dark", "btn_focus_h64", "ico_anchor", "keycap" }) {
            if (PulpKit.Has(pulpStem) || PulpKit.Texture(pulpStem) != null) {
                issues.Add($"the retired pulp stem {pulpStem} still resolves through PulpKit");
            }
        }
        foreach (JsonProperty p in glass.RootElement.EnumerateObject()) {
            if (!PulpKit.IsGlass(p.Name)) {
                issues.Add($"{p.Name}: PulpKit does not read it from the glass catalog");
            }
            if (!p.Name.StartsWith("glass_") && !p.Name.StartsWith("hero_")) {
                issues.Add($"{p.Name}: no glass prefix");
            }
            if (!p.Value.TryGetProperty("file", out JsonElement fileEl)) {
                continue;
            }
            string file = fileEl.GetString();
            string path = Root + file;
            int[] size = p.Value.TryGetProperty("size", out JsonElement s) ? new[] { s[0].GetInt32(), s[1].GetInt32() } : null;
            if (file.EndsWith(".json")) {
                if (!FileAccess.FileExists(path)) {
                    issues.Add($"{p.Name}: missing {file}");
                }
                continue;
            }
            if (file.EndsWith(".tres")) {
                GradientTexture2D g = ResourceLoader.Exists(path) ? ResourceLoader.Load<GradientTexture2D>(path) : null;
                if (g == null || g.Gradient == null) {
                    issues.Add($"{p.Name}: {file} is not a GradientTexture2D with a Gradient");
                } else if (size != null && (g.GetWidth() != size[0] || g.GetHeight() != size[1])) {
                    issues.Add($"{p.Name}: {file} is {g.GetWidth()}x{g.GetHeight()}, catalog says {size[0]}x{size[1]}");
                }
                continue;
            }
            pngs++;
            Texture2D tex = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
            if (tex == null) {
                issues.Add($"{p.Name}: cannot load {file}");
                continue;
            }
            if (size == null || tex.GetWidth() != size[0] || tex.GetHeight() != size[1]) {
                issues.Add($"{p.Name}: {file} is {tex.GetWidth()}x{tex.GetHeight()}, catalog says {(size == null ? "nothing" : $"{size[0]}x{size[1]}")}");
            }
            if (!MipmapsOn(path)) {
                issues.Add($"{p.Name}: {file}.import does not generate mipmaps");
            }
            if (PulpKit.Texture(p.Name) != tex) {
                issues.Add($"{p.Name}: PulpKit.Texture does not resolve it to {file}");
            }
        }
        if (issues.Count > 0) {
            AssertThat(string.Join(" | ", issues)).IsEqual("");
        }
        AssertThat(pngs >= 120).IsTrue();
    }

    [TestCase]
    public void EveryNineSliceKeepsACentreSliceAndItsSpecMarginsOrARecordedDeparture() {
        var issues = new List<string>();
        var seen = new HashSet<string>();
        using JsonDocument glass = Read(Root + "glass_kit.json");
        foreach (JsonProperty p in glass.RootElement.EnumerateObject()) {
            JsonElement e = p.Value;
            if (!e.TryGetProperty("nine_slice_margins_LTRB", out _)) {
                continue;
            }
            seen.Add(p.Name);
            int w = e.GetProperty("size")[0].GetInt32(), h = e.GetProperty("size")[1].GetInt32();
            int[] m = Four(e, "nine_slice_margins_LTRB"), x = Four(e, "expand_margins_LTRB");
            int[] actual = { w, h, m[0], m[1], m[2], m[3], x[0], x[1], x[2], x[3] };
            if (m[0] + m[2] >= w || m[1] + m[3] >= h) {
                issues.Add($"{p.Name}: margins {string.Join("/", m)} leave no centre slice in {w}x{h}");
            }
            bool recorded = Recorded.TryGetValue(p.Name, out int[] expected);
            if (!recorded && !Spec64.TryGetValue(p.Name, out expected)) {
                issues.Add($"{p.Name}: a nine-slice neither in UI_SPEC_GLASS 6.4 nor a recorded departure");
                continue;
            }
            if (string.Join(",", actual) != string.Join(",", expected)) {
                issues.Add($"{p.Name}: {string.Join(",", actual)} where {(recorded ? "the recorded departure" : "6.4")} says {string.Join(",", expected)}");
            }
            string deviation = e.TryGetProperty("deviation", out JsonElement d) ? d.GetString() : "";
            if (recorded && string.IsNullOrWhiteSpace(deviation)) {
                issues.Add($"{p.Name}: departs from 6.4 but its catalog entry records no deviation");
            }
            StyleBoxTexture plate = PulpKit.Plate(p.Name);
            if (plate == null || plate.TextureMarginLeft != m[0] || plate.TextureMarginTop != m[1]
                || plate.TextureMarginRight != m[2] || plate.TextureMarginBottom != m[3]
                || plate.ExpandMarginLeft != x[0] || plate.ExpandMarginRight != x[2]) {
                issues.Add($"{p.Name}: PulpKit.Plate does not carry the catalog margins");
            } else if (e.TryGetProperty("axis_stretch", out JsonElement st) && st.GetString() == "tile"
                       && plate.AxisStretchHorizontal != StyleBoxTexture.AxisStretchMode.Tile) {
                issues.Add($"{p.Name}: tiles in the catalog but not in PulpKit.Plate");
            }
        }
        foreach (string stem in Spec64.Keys) {
            if (!seen.Contains(stem)) {
                issues.Add($"{stem}: the 6.4 nine-slice is missing from the catalog");
            }
        }
        if (issues.Count > 0) {
            AssertThat(string.Join(" | ", issues)).IsEqual("");
        }
        AssertThat(seen.Count >= Spec64.Count).IsTrue();
    }

    [TestCase]
    public void EveryIconNameHasARegularAndABoldSixtyFourPixelTexture() {
        var issues = new List<string>();
        var weights = new Dictionary<string, HashSet<string>>();
        using JsonDocument glass = Read(Root + "glass_kit.json");
        foreach (JsonProperty p in glass.RootElement.EnumerateObject()) {
            if (!p.Value.TryGetProperty("icon", out JsonElement name)) {
                continue;
            }
            string n = name.GetString(), weight = p.Value.GetProperty("weight").GetString();
            if (!weights.TryGetValue(n, out HashSet<string> set)) {
                weights[n] = set = new HashSet<string>();
            }
            set.Add(weight);
            if (p.Name != PulpKit.GlassIconStem(n, weight == "bold")) {
                issues.Add($"{p.Name}: icon '{n}' {weight} should be keyed {PulpKit.GlassIconStem(n, weight == "bold")}");
            }
        }
        var index = new HashSet<string>();
        foreach (JsonElement n in glass.RootElement.GetProperty("glass_icons").GetProperty("names").EnumerateArray()) {
            index.Add(n.GetString());
        }
        foreach (string n in index) {
            if (!weights.ContainsKey(n)) {
                issues.Add($"{n}: in the icon index but no texture");
            }
        }
        foreach (KeyValuePair<string, HashSet<string>> kv in weights) {
            if (!index.Contains(kv.Key)) {
                issues.Add($"{kv.Key}: has textures but is not in the icon index");
            }
            foreach (bool bold in new[] { false, true }) {
                if (!kv.Value.Contains(bold ? "bold" : "regular")) {
                    issues.Add($"{kv.Key}: no {(bold ? "bold" : "regular")} weight");
                    continue;
                }
                Texture2D tex = PulpKit.GlassIconTexture(kv.Key, bold);
                if (tex == null || tex.GetWidth() != 64 || tex.GetHeight() != 64) {
                    issues.Add($"{kv.Key} {(bold ? "bold" : "regular")}: {(tex == null ? "missing" : $"{tex.GetWidth()}x{tex.GetHeight()}")}, not 64x64");
                }
            }
        }
        if (issues.Count > 0) {
            AssertThat(string.Join(" | ", issues)).IsEqual("");
        }
        AssertThat(index.Count >= 30).IsTrue();
    }

    [TestCase]
    public void EveryIconNameTheHudModelsProduceExistsInTheGlassSet() {
        var names = new SortedSet<string>();
        foreach (StatusType status in Enum.GetValues<StatusType>()) {
            names.Add(HudAbilityIndicatorModel.StatusIconName(status));
            names.Add(FighterHudModel.StatusIconName(status));
        }
        names.Remove("");
        var missing = new List<string>();
        foreach (string n in names) {
            if (!PulpKit.HasGlassIcon(n) || PulpKit.GlassIconTexture(n, false) == null || PulpKit.GlassIconTexture(n, true) == null) {
                missing.Add(n);
            }
        }
        if (missing.Count > 0) {
            AssertThat("no glass icon for: " + string.Join(", ", missing)).IsEqual("");
        }
        AssertThat(names.Count >= 6).IsTrue();
    }

    [TestCase]
    public void EveryRosterMemberHasItsBakedIdlePosesPlayOrderAndAFeetLine() {
        var issues = new List<string>();
        using JsonDocument doc = Read(Root + "select_heroes/select_heroes.json");
        var heroes = new Dictionary<string, JsonElement>();
        foreach (JsonElement h in doc.RootElement.GetProperty("heroes").EnumerateArray()) {
            heroes[h.GetProperty("id").GetString()] = h;
        }
        foreach (string id in CharacterRoster.IDs) {
            if (!heroes.TryGetValue(id, out JsonElement hero)) {
                issues.Add($"{id}: no baked idle frames");
                continue;
            }
            if (hero.GetProperty("fps").GetDouble() <= 0) {
                issues.Add($"{id}: no idle frame rate");
            }
            JsonElement frames = hero.GetProperty("frames");
            // The idle is a ping-pong of N unique poses (roster_spec.IDLE_POSES): 0..N-1 then N-2..1.
            int poses = FTT.Tests.ContentValidation.CharacterPresentationTests.IdlePoseCount(id);
            if (frames.GetArrayLength() != poses) {
                issues.Add($"{id}: {frames.GetArrayLength()} baked idle poses, not {poses}");
            }
            var order = new List<int>();
            foreach (JsonElement i in hero.GetProperty("order").EnumerateArray()) order.Add(i.GetInt32());
            var expectedOrder = new List<int>();
            for (int i = 0; i < poses; i++) expectedOrder.Add(i);
            for (int i = poses - 2; i >= 1; i--) expectedOrder.Add(i);
            if (string.Join(",", order) != string.Join(",", expectedOrder)) {
                issues.Add($"{id}: play order [{string.Join(",", order)}], not [{string.Join(",", expectedOrder)}]");
            }
            if (hero.GetProperty("fps").GetDouble() != 10.0) {
                issues.Add($"{id}: idle plays at {hero.GetProperty("fps").GetDouble()} fps, not 10");
            }
            int n = 0;
            foreach (JsonElement f in frames.EnumerateArray()) {
                string path = Root + f.GetProperty("file").GetString();
                Texture2D tex = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
                double feet = f.GetProperty("feet_y").GetDouble(), pivot = f.GetProperty("pivot_x").GetDouble();
                if (tex == null) {
                    issues.Add($"{id} frame {n}: cannot load {path}");
                } else if (feet <= 0 || feet > tex.GetHeight() || pivot <= 0 || pivot >= tex.GetWidth()) {
                    issues.Add($"{id} frame {n}: feet line {feet} / pivot {pivot} outside {tex.GetWidth()}x{tex.GetHeight()}");
                } else if (!MipmapsOn(path)) {
                    issues.Add($"{id} frame {n}: not mipmapped");
                }
                if (!PulpKit.IsGlass($"hero_{id}_idle_{n}")) {
                    issues.Add($"{id} frame {n}: not in the glass catalog");
                }
                n++;
            }
        }
        if (issues.Count > 0) {
            AssertThat(string.Join(" | ", issues)).IsEqual("");
        }
        AssertThat(CharacterRoster.IDs.Count > 0).IsTrue();
    }
}
