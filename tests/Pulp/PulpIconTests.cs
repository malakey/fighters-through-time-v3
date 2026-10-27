using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>
/// The icon widget (TG-WIDGETS, ICN-A): one white glass texture per glyph and weight, bold below 30 px, the legacy pulp
/// names through their aliases or the frozen pulp fallback, and the never-throws rule for unknown names.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpIconTests {

    [TestCase]
    public void EveryGlassIconDrawsItsRegularTextureFromThirtyPixelsUp() {
        AssertThat(PulpIcon.GlassNames.Count).IsEqual(34);
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

    [TestCase]
    public void EveryLegacyPulpNameStillDrawsThroughGlassAnAliasOrThePulpFallback() {
        AssertThat(PulpIcon.KitNames.Count).IsEqual(26);
        foreach (string name in PulpIcon.KitNames) {
            AssertThat(PulpIcon.Exists(name)).IsTrue();
            bool glass = PulpIcon.ResolveGlass(name) != null;
            bool pulp = PulpIcon.LoadLayers(name) != null;
            if (!glass && !pulp) AssertThat($"legacy icon '{name}' would draw nothing").IsEqual("");
        }
        // A name both kits have draws the glass texture (one tinted layer), not the pulp layers.
        PulpIcon shield = PulpIcon.Create("shield", Colors.White, 30f);
        AssertThat(shield.GlassName).IsEqual("shield");
        shield.Free();
    }

    [TestCase]
    public void AnUnknownNameIsSafeAndDrawsNothing() {
        AssertThat(PulpIcon.Exists("no_such_icon")).IsFalse();
        AssertObject(PulpIcon.LoadLayers("no_such_icon")).IsNull();
        AssertObject(PulpIcon.LoadLayers(null)).IsNull();
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

    [TestCase]
    public void ALegacyNameResolvesThroughItsAlias() {
        AssertThat(PulpIcon.ResolveGlass("anchor")).IsEqual("beacon_anchor");
        AssertThat(PulpIcon.ResolveGlass("anchor_hollow")).IsEqual("beacon_anchor_hollow");
        PulpIcon anchor = PulpIcon.Create("anchor", Colors.White, 46f);
        AssertThat(anchor.IconName).IsEqual("anchor");
        AssertThat(anchor.GlassName).IsEqual("beacon_anchor");
        AssertThat(PulpIcon.TextureFor("anchor", 46f)).IsEqual(PulpKit.GlassIconTexture("beacon_anchor", false));
        anchor.Free();
        // A name with neither a glass icon nor an alias falls back to its pulp layers until the closeout.
        foreach (string pulpOnly in new[] { "anchor_pip", "anchor_pip_cracked", "marker", "prompt_circle" }) {
            AssertObject(PulpIcon.ResolveGlass(pulpOnly)).IsNull();
            AssertObject(PulpIcon.LoadLayers(pulpOnly)).IsNotNull();
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
