using FTT.Tests.ContentValidation;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>
/// The icon widget (TG-WIDGETS, ICN-A): one white glass texture per glyph and weight, bold below 30 px, the never-throws
/// rule for unknown names, and (TG-CLOSE) the retired pulp names and transition aliases drawing nothing.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpIconTests {

    [TestCase]
    public void EveryGlassIconDrawsItsRegularTextureFromThirtyPixelsUp() {
        AssertThat(PulpIcon.GlassNames.Count).IsEqual(30);
        foreach (string name in PulpIcon.GlassNames) {
            Texture2D regular = PulpIcon.TextureFor(name, 30f);
            if (regular == null) AssertThat($"glass icon '{name}' has no regular texture").IsEqual("");
            AssertThat(regular).IsEqual(PulpKit.GlassIconTexture(name, false));
            AssertThat(regular.GetSize()).IsEqual(new Vector2(64f, 64f));
            AssertThat(PulpIcon.Exists(name)).IsTrue();
            PulpIcon icon = PulpIcon.Create(name, Colors.White, 46f);
            AssertThat(icon.GlassName).IsEqual(name);
            AssertThat(icon.DrawsBold).IsFalse();
            icon.Free();
        }
    }

    /// <summary>TG-CLOSE rewrite of EveryLegacyPulpNameStillDrawsThroughGlassAnAliasOrThePulpFallback.</summary>
    [TestCase]
    public void TheRetiredPulpOnlyNamesDrawNothingAndTheSharedNamesDrawGlass() {
        var issues = new System.Collections.Generic.List<string>();
        foreach (string name in UiLegacyKitReferenceScanTests.RetiredIconNames) {
            if (PulpIcon.Exists(name) || PulpIcon.ResolveGlass(name) != null || PulpIcon.TextureFor(name, 46f) != null) {
                issues.Add($"the retired icon name '{name}' still draws");
            }
            PulpIcon icon = PulpIcon.Create(name, Colors.White, 46f);
            if (icon.GlassName != null) issues.Add($"a PulpIcon named '{name}' draws {icon.GlassName}");
            icon.Free();
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        // A name the pulp kit and the glass kit both had draws the glass texture.
        PulpIcon shield = PulpIcon.Create("shield", Colors.White, 30f);
        AssertThat(shield.GlassName).IsEqual("shield");
        shield.Free();
    }

    [TestCase]
    public void AnUnknownNameIsSafeAndDrawsNothing() {
        AssertThat(PulpIcon.Exists("no_such_icon")).IsFalse();
        AssertObject(PulpIcon.ResolveGlass(null)).IsNull();
        AssertObject(PulpIcon.TextureFor(null, 40f)).IsNull();
        PulpIcon icon = PulpIcon.Create("no_such_icon", Colors.White, 40f);
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(icon);
        try {
            icon.QueueRedraw();
            AssertThat(icon.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
        } finally {
            icon.Free();
        }
    }

    /// <summary>TG-CLOSE rewrite of ALegacyNameResolvesThroughItsAlias: the aliases are gone, the glass names draw.</summary>
    [TestCase]
    public void TheBeaconAnchorDrawsByItsGlassNamesWithNoAlias() {
        foreach (string name in new[] { "beacon_anchor", "beacon_anchor_hollow" }) {
            AssertThat(PulpIcon.ResolveGlass(name)).IsEqual(name);
            PulpIcon anchor = PulpIcon.Create(name, Colors.White, 46f);
            AssertThat(anchor.GlassName).IsEqual(name);
            AssertThat(PulpIcon.TextureFor(name, 46f)).IsEqual(PulpKit.GlassIconTexture(name, false));
            AssertThat(PulpIcon.TextureFor(name, 24f)).IsEqual(PulpKit.GlassIconTexture(name, true));
            anchor.Free();
        }
    }

    [TestCase]
    public void BelowThirtyPixelsTheBoldWeightIsDrawn() {
        AssertThat(PulpIcon.UseBold(29.9f)).IsTrue();
        AssertThat(PulpIcon.UseBold(30f)).IsFalse();
        foreach (string name in new[] { "shield", "star4", "dilation", "flag" }) {
            Texture2D bold = PulpIcon.TextureFor(name, 24f);
            Texture2D regular = PulpIcon.TextureFor(name, 30f);
            AssertObject(bold).IsNotNull();
            AssertThat(bold).IsEqual(PulpKit.GlassIconTexture(name, true));
            AssertThat(ReferenceEquals(bold, regular)).OverrideFailureMessage($"{name}: the bold weight is the regular texture").IsFalse();
        }
        PulpIcon pip = PulpIcon.Create("shield", Colors.White, 24f);
        PulpIcon big = PulpIcon.Create("shield", Colors.White, 30f);
        AssertThat(pip.DrawsBold).IsTrue();
        AssertThat(big.DrawsBold).IsFalse();
        pip.Free();
        big.Free();
    }
}
