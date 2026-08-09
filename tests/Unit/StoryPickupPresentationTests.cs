using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B6: the Story pickup visual upgrade.
///
/// <para><c>ChronalOrbData.Icon</c> was an exported <c>Texture2D</c> that nothing in
/// the project ever read, and <c>ChronalOrbItem</c> had no authored scene at all —
/// it bobbed an invisible node. The Chronal Dust pickup rendered a tier texture with
/// no glow. Both now carry an additive glow and a breathing pulse, and the orb's
/// colour is the same canonical value the Fighter driver's proxies use.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryPickupPresentationTests {

    private const string OrbTemplatePath = "res://scenes/templates/ChronalOrbTemplate.tscn";
    private const string DustTemplatePath = "res://scenes/templates/ChronalDustPickupTemplate.tscn";

    [TestCase]
    public void TheOrbTemplateRendersItsAuthoredIconAndEffectColour() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PackedScene scene = ResourceLoader.Load<PackedScene>(OrbTemplatePath);
        AssertObject(scene).IsNotNull();
        var orb = scene.Instantiate<ChronalOrbItem>();
        tree.Root.AddChild(orb);

        // A data resource built for the test, not an authored .tres, so it is not an
        // AuthoredResources cache entry and must not be disposed.
        var icon = new PlaceholderTexture2D();
        orb.Data = new ChronalOrbData { Effect = OrbEffect.MeterBoost, Icon = icon };

        try {
            orb.ApplyDataVisual();

            var iconSprite = orb.GetNode<Sprite2D>("Icon");
            var glow = orb.GetNode<Sprite2D>("Glow");
            AssertObject(iconSprite.Texture).IsSame(icon);

            Color expected = ChronalOrbItem.EffectColor(OrbEffect.MeterBoost);
            AssertFloat(iconSprite.Modulate.R).IsEqualApprox(expected.R, 0.0001);
            AssertFloat(iconSprite.Modulate.G).IsEqualApprox(expected.G, 0.0001);
            AssertFloat(glow.Modulate.B).IsEqualApprox(expected.B, 0.0001);
            // The glow is a wash behind the icon, never opaque over it.
            AssertThat(glow.Modulate.A < 1f).IsTrue();
        } finally {
            orb.Free();
        }
    }

    [TestCase]
    public void EveryOrbEffectResolvesAColourAndTheDistinctOnesDiffer() {
        AssertThat(ChronalOrbItem.EffectColor(OrbEffect.HPRestore))
            .IsNotEqual(ChronalOrbItem.EffectColor(OrbEffect.MeterBoost));
        AssertThat(ChronalOrbItem.EffectColor(OrbEffect.SpeedBuff))
            .IsNotEqual(ChronalOrbItem.EffectColor(OrbEffect.HPRestore));
        foreach (OrbEffect effect in System.Enum.GetValues<OrbEffect>()) {
            Color color = ChronalOrbItem.EffectColor(effect);
            AssertThat(color.R + color.G + color.B > 0.5f).IsTrue();
        }
    }

    [TestCase]
    public void TheOrbGlowBreathesWithoutEverGoingDark() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var orb = ResourceLoader.Load<PackedScene>(OrbTemplatePath).Instantiate<ChronalOrbItem>();
        tree.Root.AddChild(orb);
        try {
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int frame = 0; frame < 300; frame++) {
                float pulse = orb.GlowPulseAt(frame / 60f);
                min = Mathf.Min(min, pulse);
                max = Mathf.Max(max, pulse);
            }
            AssertThat(min > 0.6f).IsTrue();
            AssertThat(max <= 1.0001f).IsTrue();
            // A flat pulse would be a pointless node.
            AssertThat(max - min > 0.1f).IsTrue();
        } finally {
            orb.Free();
        }
    }

    [TestCase]
    public void ADespawnedOrbResetsItsGlowForTheNextSpawn() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var orb = ResourceLoader.Load<PackedScene>(OrbTemplatePath).Instantiate<ChronalOrbItem>();
        tree.Root.AddChild(orb);
        try {
            orb.OnSpawn();
            for (int frame = 0; frame < 20; frame++) orb._PhysicsProcess(1.0 / 60.0);
            orb.OnDespawn();

            var glow = orb.GetNode<Sprite2D>("Glow");
            // Pool discipline: every mutable presentation field resets on despawn.
            AssertThat(glow.Scale).IsEqual(Vector2.One);
            AssertThat(glow.SelfModulate).IsEqual(Colors.White);
        } finally {
            orb.Free();
        }
    }

    [TestCase]
    public void TheDustPickupCarriesAGlowBehindItsTierTexture() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PackedScene scene = ResourceLoader.Load<PackedScene>(DustTemplatePath);
        var dust = scene.Instantiate<FTT.Environment.ChronalDustPickup>();
        tree.Root.AddChild(dust);
        try {
            var glow = dust.GetNodeOrNull<Sprite2D>("Glow");
            AssertObject(glow).IsNotNull();
            AssertObject(dust.GetNodeOrNull<Sprite2D>("Visual")).IsNotNull();
            AssertThat(glow.Modulate).IsEqual(FTT.Environment.ChronalDustPickup.GlowColor);
            // The glow sits behind the tier sprite in child order.
            AssertThat(glow.GetIndex() < dust.GetNode<Sprite2D>("Visual").GetIndex()).IsTrue();
        } finally {
            dust.Free();
        }
    }
}
