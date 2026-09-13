using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A8 / F24 Option A. The Fighter player-slot ownership edge is a
/// separate persistent layer that no effect can touch.
///
/// <para>Until this package the slot colour was pushed onto the same arbitrated
/// outline stack as status, armor and spawn protection, as its <em>lowest</em>
/// layer — so the first status effect of the match outranked it and the player
/// lost the only thing telling them which fighter was theirs. HUD_CONTRACT is
/// blunt about it: "damage, statuses, armor, invulnerability, ability decoys and
/// an effect expiring cannot recolor, pulse, disable or replace it."</para>
///
/// <para>These cases drive the real shader material, so they pin the uniforms the
/// renderer actually reads rather than an intermediate model.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class OwnershipOutlineTests {

    [TestCase]
    public void TheOwnerEdgeSurvivesAStatusStartingOverlappingAndExpiring() {
        (Node2D owner, GlowPresentationController glow) = CreateSubject();
        try {
            glow.SetSlotIndicator(0);
            AssertThat(glow.HasOwnershipOutline).IsTrue();
            AssertOwnerUniforms(glow, GlowPalette.SlotColor(0));

            // Status, then armor on top of it, then spawn protection: every one of
            // these used to replace the slot colour outright.
            glow.SetStatus(FTT.Core.StatusType.Venom);
            AssertOwnerUniforms(glow, GlowPalette.SlotColor(0));
            glow.SetHyperArmor(true);
            AssertOwnerUniforms(glow, GlowPalette.SlotColor(0));
            glow.SetSpawnInvulnerability(true);
            AssertOwnerUniforms(glow, GlowPalette.SlotColor(0));

            // ...and every one of these expiring used to be able to clear it.
            glow.SetSpawnInvulnerability(false);
            glow.SetHyperArmor(false);
            glow.SetStatus(FTT.Core.StatusType.None);
            glow.ClearAllStates();
            AssertThat(glow.HasOwnershipOutline)
                .OverrideFailureMessage("ClearAllStates clears the EFFECT stack, never the ownership edge.")
                .IsTrue();
            AssertOwnerUniforms(glow, GlowPalette.SlotColor(0));
        } finally {
            owner.Free();
        }
    }

    [TestCase]
    public void PlayerOneAndPlayerTwoCarryTheirAuthoredColours() {
        (Node2D ownerOne, GlowPresentationController one) = CreateSubject();
        (Node2D ownerTwo, GlowPresentationController two) = CreateSubject();
        try {
            one.SetSlotIndicator(0);
            two.SetSlotIndicator(1);

            // P1 cyan #00f0ff, P2 red #ff3366, at the 1 px reference thickness.
            AssertThat(one.OwnershipOutlineColor).IsEqual(new Color(0f, 0.941f, 1f));
            AssertThat(two.OwnershipOutlineColor).IsEqual(new Color(1f, 0.2f, 0.4f));
            AssertFloat(GlowPresentationController.OwnerOutlineThickness).IsEqual(1f);
        } finally {
            ownerOne.Free();
            ownerTwo.Free();
        }
    }

    /// <summary>
    /// "Do not mutate a shared material resource so that changing one actor
    /// recolors another." <c>gl_compatibility</c> has no canvas instance uniforms,
    /// so per-actor isolation means a per-actor <c>ShaderMaterial</c> — and this is
    /// what proves the duplication actually happened.
    /// </summary>
    [TestCase]
    public void EachActorOwnsAnIsolatedMaterial() {
        (Node2D ownerOne, GlowPresentationController one) = CreateSubject();
        (Node2D ownerTwo, GlowPresentationController two) = CreateSubject();
        try {
            one.SetSlotIndicator(0);
            two.SetSlotIndicator(1);

            AssertObject(one.GlowMaterial).IsNotNull();
            AssertObject(two.GlowMaterial).IsNotNull();
            AssertThat(one.GlowMaterial == two.GlowMaterial)
                .OverrideFailureMessage("Two fighters must not share one ShaderMaterial.").IsFalse();

            AssertOwnerUniforms(one, GlowPalette.SlotColor(0));
            AssertOwnerUniforms(two, GlowPalette.SlotColor(1));

            // Driving P2's effect layer hard leaves P1's edge exactly where it was.
            two.SetHyperArmor(true);
            two.SetStatus(FTT.Core.StatusType.StaticCharge);
            AssertOwnerUniforms(one, GlowPalette.SlotColor(0));
            AssertOwnerUniforms(two, GlowPalette.SlotColor(1));
        } finally {
            ownerOne.Free();
            ownerTwo.Free();
        }
    }

    /// <summary>
    /// Story entities have no player slot, so they carry no ownership edge and
    /// keep using the effect layer as their outline — the contract explicitly
    /// preserves that. A pooled proxy released and rebound must not keep a stale
    /// slot colour either.
    /// </summary>
    [TestCase]
    public void AnActorWithNoSlotCarriesNoOwnerEdge() {
        (Node2D owner, GlowPresentationController glow) = CreateSubject();
        try {
            AssertThat(glow.HasOwnershipOutline).IsFalse();
            AssertThat(glow.OwnershipSlot).IsEqual(-1);
            AssertThat(glow.GlowMaterial
                .GetShaderParameter(GlowPresentationController.OwnerOutlineEnabledUniform).AsBool()).IsFalse();

            glow.SetSlotIndicator(1);
            AssertThat(glow.HasOwnershipOutline).IsTrue();

            glow.ClearSlotIndicator();
            AssertThat(glow.HasOwnershipOutline).IsFalse();
            AssertThat(glow.GlowMaterial
                .GetShaderParameter(GlowPresentationController.OwnerOutlineEnabledUniform).AsBool()).IsFalse();
        } finally {
            owner.Free();
        }
    }

    private static void AssertOwnerUniforms(GlowPresentationController glow, Color expected) {
        AssertThat(glow.GlowMaterial
            .GetShaderParameter(GlowPresentationController.OwnerOutlineEnabledUniform).AsBool()).IsTrue();
        AssertThat(glow.GlowMaterial
            .GetShaderParameter(GlowPresentationController.OwnerOutlineColorUniform).AsColor())
            .IsEqual(expected);
        AssertFloat(glow.GlowMaterial
            .GetShaderParameter(GlowPresentationController.OwnerOutlineThicknessUniform).AsSingle())
            .IsEqual(GlowPresentationController.OwnerOutlineThickness);
    }

    private static (Node2D Owner, GlowPresentationController Glow) CreateSubject() {
        var owner = new Node2D { Name = "OwnershipSubject" };
        var sprite = new Sprite2D { Name = "Body" };
        owner.AddChild(sprite);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(owner);
        GlowPresentationController glow = GlowPresentationController.AttachTo(
            owner, sprite, ownerPlayerIndex: -1, subscribeToStoryEvents: false);
        return (owner, glow);
    }
}
