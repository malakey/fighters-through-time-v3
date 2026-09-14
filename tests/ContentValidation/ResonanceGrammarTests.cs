using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 11 A6b — the two-colour visual grammar, pinned.
///
/// <para>design-godot.md §2 "The Visual Grammar of Resonance" is an authored
/// visual language: <b>cold synthetic light is the Unbound</b> (extraction
/// beams, siphons, visors, plasma — jagged, desaturating) and <b>warm gold is
/// history's resonance</b> (the hero's ignition and persistent aura, Chronal
/// Dust, restoration). The recurrence contract makes it structural: every
/// Chronal Extractor is Level 0 in miniature, and every Dust pickup restates
/// the origin.</para>
///
/// <para><b>Why a test and not a style note.</b> The pigments already existed
/// before Package 11 — the extractor was already cold and Dust was already gold
/// — and the language still eroded, because nothing said the families were
/// load-bearing. Level 0's own fracture, the beat that is supposed to
/// <i>establish</i> the grammar, had drifted to violet: a colour that belongs
/// to neither half. Prose cannot catch that. A site that defines its own cold
/// literal is the exact failure mode, so that is what these cases refuse.</para>
///
/// <para>The grammar cases are deliberately <b>source</b> assertions rather than
/// scene assertions. The treatment is placeholder <c>ColorRect</c> geometry at
/// the era-placeholder bar the rest of the campaign meets; what is being
/// protected is which constants the builders reach for, and that is a property
/// of the source. Runtime values are asserted directly where a constant is
/// public.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ResonanceGrammarTests {

    private const string ScriptRoot = "scripts";

    /// <summary>
    /// The regions that paint the Unbound's machines: the whole extractor, and
    /// Level 0's ignition beat. A cold literal inside one of these is the
    /// regression — a siphon site re-inventing the pigment instead of reading
    /// the shared one.
    ///
    /// <para>Scoped to regions rather than whole files on purpose. Level 0 also
    /// draws cyan-ish <i>gate markers</i> for the double-jump and roll drills;
    /// those are UI affordances pointing the player somewhere, not Unbound
    /// machines, and the grammar has nothing to say about them. A pin that
    /// cannot tell the two apart would either be ignored or would push gate
    /// markers into a faction colour they have no business wearing.</para>
    /// </summary>
    private static readonly (string Path, string FromMember)[] GrammarGovernedRegions = {
        ("scripts/Environment/ChronalExtractor.cs", null),
        ("scripts/Environment/Level00Controller.cs", "private void BuildFracturePresentation()")
    };

    /// <summary>
    /// Matches <c>new Color(r, g, b[, a])</c> and its target-typed
    /// <c>new(r, g, b[, a])</c> form.
    /// </summary>
    private static readonly Regex ColorLiteral = new(
        @"new\s*(?:Color)?\s*\(\s*(\d*\.?\d+)f?\s*,\s*(\d*\.?\d+)f?\s*,\s*(\d*\.?\d+)f?\s*(?:,\s*(\d*\.?\d+)f?\s*)?\)",
        RegexOptions.Compiled);

    /// <summary>Cyan-family: strong blue, strong green, little red.</summary>
    private static bool IsColdFamily(float r, float g, float b) =>
        b >= 0.7f && g >= 0.5f && r <= 0.45f;

    /// <summary>Violet/purple: strong blue, real red, little green.</summary>
    private static bool IsViolet(float r, float g, float b) =>
        b >= 0.6f && r >= 0.25f && g <= 0.5f;

    [TestCase]
    public void NoSiphonOrBeamSiteDefinesItsOwnColdColourLiteral() {
        var issues = new List<string>();

        foreach ((string path, string fromMember) in GrammarGovernedRegions) {
            if (!File.Exists(path)) { issues.Add($"{path} is missing"); continue; }
            string source = File.ReadAllText(path);
            int lineOffset = 0;
            if (fromMember != null) {
                int start = source.IndexOf(fromMember, System.StringComparison.Ordinal);
                if (start < 0) { issues.Add($"{path} no longer declares {fromMember}"); continue; }
                lineOffset = source[..start].Split('\n').Length - 1;
                source = source[start..];
            }
            string[] lines = source.Split('\n');
            for (int index = 0; index < lines.Length; index++) {
                string line = lines[index];
                // Doc comments quote values freely; only real code counts.
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("///")) continue;

                foreach (Match match in ColorLiteral.Matches(line)) {
                    float r = float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    float g = float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                    float b = float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                    if (IsColdFamily(r, g, b)) {
                        issues.Add($"{path}:{lineOffset + index + 1} defines a cold literal " +
                                   $"({r},{g},{b}); use UIPalette.UnboundCold / " +
                                   "UnboundColdDischarge / UnboundColdDim");
                    }
                }
            }
        }

        // And the one public cold constant in the game is the shared one.
        if (FTT.Environment.ChronalExtractor.DischargeColor != UIPalette.UnboundColdDischarge) {
            issues.Add("ChronalExtractor.DischargeColor no longer reads UIPalette.UnboundColdDischarge");
        }

        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void DustRestatesTheOriginInTheSharedResonanceGold() {
        var issues = new List<string>();

        // The recurrence contract: a Dust pickup's glow is the same gold the
        // hero's aura carries, not a look-alike.
        Color glow = FTT.Environment.ChronalDustPickup.GlowColor;
        if (!new Color(glow.R, glow.G, glow.B).IsEqualApprox(UIPalette.ResonanceAura)) {
            issues.Add($"ChronalDustPickup.GlowColor is {glow} but the aura gold is {UIPalette.ResonanceAura}");
        }

        // The grammar's warm constants are the pigments the game already paints,
        // not a second set of numbers beside them.
        if (!UIPalette.ResonanceGold.IsEqualApprox(UIPalette.Gold)) {
            issues.Add("UIPalette.ResonanceGold drifted from UIPalette.Gold");
        }
        if (!UIPalette.ResonanceGoldBright.IsEqualApprox(UIPalette.GoldBright)) {
            issues.Add("UIPalette.ResonanceGoldBright drifted from UIPalette.GoldBright");
        }

        // Cold and warm must stay unmistakably apart; a grammar whose two halves
        // converge says nothing.
        if (UIPalette.UnboundCold.R > UIPalette.ResonanceGold.R) {
            issues.Add("the cold pigment is warmer (more red) than the resonance gold");
        }
        if (UIPalette.ResonanceGold.B > UIPalette.UnboundCold.B) {
            issues.Add("the resonance gold is colder (more blue) than the Unbound cold");
        }

        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void LevelZeroOpensColdThenGoldThenTwoKindsOfDoor() {
        const string path = "scripts/Environment/Level00Controller.cs";
        AssertThat(File.Exists(path)).IsTrue();
        string source = File.ReadAllText(path);

        int start = source.IndexOf("private void BuildFracturePresentation()", System.StringComparison.Ordinal);
        AssertThat(start >= 0)
            .OverrideFailureMessage("Level00Controller no longer builds the Part 1 ignition beat")
            .IsTrue();
        string beat = source[start..];

        var issues = new List<string>();

        // Beat 1 and 3: the Unbound's cold beam, from the shared pigment.
        if (!beat.Contains("UIPalette.UnboundColdDischarge")) {
            issues.Add("the beat has no cold beam from UIPalette.UnboundColdDischarge");
        }
        // Beat 2: the hero's gold ignition — the half that was entirely absent.
        if (!beat.Contains("UIPalette.ResonanceGold")) {
            issues.Add("the beat has no warm-gold ignition from UIPalette.ResonanceGold*");
        }
        // Beat 4: two kinds of door, established side by side.
        if (!beat.Contains("UIPalette.WardenPortal")) {
            issues.Add("the beat has no Warden portal from UIPalette.WardenPortal");
        }
        if (!beat.Contains("WardenPortal\"") || !beat.Contains("UnboundBeam\"")) {
            issues.Add("the beat does not stand a Warden portal beside the Unbound tear");
        }
        if (!beat.Contains("ResonanceIgnition\"")) {
            issues.Add("the beat has no ResonanceIgnition node");
        }

        // No violet. The old fracture was Color(0.5, 0.2, 0.9, 0.85) with a
        // Color(0.4, 0.1, 0.8, 0.25) halo and a Color(0.7, 0.4, 1) label —
        // a generic rift in a colour that belongs to neither faction.
        foreach (string line in beat.Split('\n')) {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("///")) continue;
            foreach (Match match in ColorLiteral.Matches(line)) {
                float r = float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                float g = float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                float b = float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (IsViolet(r, g, b)) {
                    issues.Add($"the beat reintroduced a violet literal ({r},{g},{b})");
                }
            }
        }

        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }
}
