using FTT.Combat;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Ability VFX are one 3-frame animation per slot: frame 0 onset, frame 1 the
/// sustained body, frame 2 the burst. Travelling things hold frame 1, and the
/// Fighter proxy path must not restart the animation on every sync.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AbilityVisualPresentationTests {
    private const string AbilityID = "einstein_mass_energy_conversion";

    [TestCase]
    public void HoldFrameShowsTheTravelFrameStatically() {
        var sprite = new AnimatedSprite2D();
        try {
            bool applied = AbilityVisualLibrary.Apply(sprite, AbilityID, 0.38f,
                holdFrame: AbilityVisualLibrary.TravelFrame);
            AssertThat(applied).IsTrue();
            AssertThat(sprite.IsPlaying()).IsFalse();
            AssertThat(sprite.Frame).IsEqual(AbilityVisualLibrary.TravelFrame);
            AssertThat(sprite.Visible).IsTrue();
        } finally {
            sprite.Free();
        }
    }

    [TestCase]
    public void WithoutHoldFrameTheAnimationPlaysFromFrameZero() {
        var sprite = new AnimatedSprite2D();
        try {
            AssertThat(AbilityVisualLibrary.Apply(sprite, AbilityID, 0.38f)).IsTrue();
            AssertThat(sprite.IsPlaying()).IsTrue();
            AssertThat(sprite.Frame).IsEqual(0);
            AssertThat(AbilityVisualLibrary.IsShowing(sprite, AbilityID)).IsTrue();
            AssertThat(AbilityVisualLibrary.IsShowing(sprite, "einstein_relativity_rift")).IsFalse();
        } finally {
            sprite.Free();
        }
    }

    [TestCase]
    public void ASyncedProxyKeepsAdvancingInsteadOfResettingToFrameZero() {
        var proxy = new ColorRect { Size = new Vector2(40f, 40f) };
        var ability = new AbilityData { AbilityID = AbilityID };
        try {
            FighterSimulationDriver.ConfigureAbilityProxy(proxy, ability, 0.48f);
            var authored = proxy.GetNode<AnimatedSprite2D>("AuthoredVisual");
            authored.Frame = 2;
            FighterSimulationDriver.ConfigureAbilityProxy(proxy, ability, 0.48f);
            AssertThat(authored.Frame).IsEqual(2);
            AssertThat(authored.Visible).IsTrue();
            AssertThat(authored.Position).IsEqual(proxy.Size / 2f);
        } finally {
            proxy.Free();
        }
    }

    [TestCase]
    public void AProjectileProxyHoldsTheTravelFrameAcrossSyncs() {
        var proxy = new ColorRect { Size = new Vector2(40f, 40f) };
        var ability = new AbilityData { AbilityID = AbilityID };
        try {
            for (int i = 0; i < 2; i++) {
                FighterSimulationDriver.ConfigureAbilityProxy(proxy, ability, 0.38f, true,
                    holdTravelFrame: true);
            }
            var authored = proxy.GetNode<AnimatedSprite2D>("AuthoredVisual");
            AssertThat(authored.IsPlaying()).IsFalse();
            AssertThat(authored.Frame).IsEqual(AbilityVisualLibrary.TravelFrame);
            AssertThat(authored.FlipH).IsTrue();
        } finally {
            proxy.Free();
        }
    }

    [TestCase]
    public void AReleasedOrAbilitylessProxyHidesItsAuthoredVisual() {
        var proxy = new ColorRect { Size = new Vector2(40f, 40f) };
        var ability = new AbilityData { AbilityID = AbilityID };
        try {
            FighterSimulationDriver.ConfigureAbilityProxy(proxy, ability, 0.48f);
            var authored = proxy.GetNode<AnimatedSprite2D>("AuthoredVisual");
            FighterSimulationDriver.HideAuthoredVisual(proxy);
            AssertThat(authored.Visible).IsFalse();
            AssertThat(authored.IsPlaying()).IsFalse();

            FighterSimulationDriver.ConfigureAbilityProxy(proxy, ability, 0.48f);
            AssertThat(authored.Visible).IsTrue();
            FighterSimulationDriver.ConfigureAbilityProxy(proxy, null, 0.48f);
            AssertThat(authored.Visible).IsFalse();
        } finally {
            proxy.Free();
        }
    }
}
