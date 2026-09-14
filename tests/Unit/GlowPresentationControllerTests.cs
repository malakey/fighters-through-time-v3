using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A3: the glow arbiter node — per-entity material duplication, the
/// three independent channels (base tint, tint override, glow stack), the child
/// PointLight2D, and the shader-driven hyper-armor shell that replaced the gold
/// ChronalArmorOverlay ColorRect.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GlowPresentationControllerTests {

    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    private static (Node2D owner, Sprite2D sprite, GlowPresentationController glow) CreateSubject() {
        var owner = new Node2D { Name = "GlowOwner" };
        var sprite = new Sprite2D { Name = "Visual" };
        owner.AddChild(sprite);
        GlowPresentationController glow = GlowPresentationController.AttachTo(
            owner, sprite, ownerPlayerIndex: -1, subscribeToStoryEvents: false);
        return (owner, sprite, glow);
    }

    [TestCase]
    public void AttachInstallsAPerEntityMaterialDuplicateAndALight() {
        (Node2D owner, Sprite2D sprite, GlowPresentationController glow) = CreateSubject();

        AssertObject(glow).IsNotNull();
        AssertThat(glow.HasMaterial).IsTrue();
        AssertObject(sprite.Material).IsSame(glow.GlowMaterial);
        AssertObject(glow.Light).IsNotNull();
        AssertObject(glow.Light.Texture).IsNotNull();

        // A second entity must not share the first entity's material:
        // gl_compatibility has no canvas instance uniforms, so one material per
        // entity is the whole point.
        (Node2D otherOwner, Sprite2D otherSprite, GlowPresentationController otherGlow) = CreateSubject();
        AssertThat(ReferenceEquals(glow.GlowMaterial, otherGlow.GlowMaterial)).IsFalse();
        AssertObject(otherSprite.Material).IsSame(otherGlow.GlowMaterial);

        owner.Free();
        otherOwner.Free();
    }

    [TestCase]
    public void TheGlowStackDrivesTheShaderUniformsByPriority() {
        (Node2D owner, Sprite2D _, GlowPresentationController glow) = CreateSubject();

        AssertThat(glow.IsGlowing).IsFalse();
        AssertThat(glow.GlowMaterial
            .GetShaderParameter(GlowPresentationController.OutlineThicknessUniform).AsSingle()).IsEqual(0f);

        // Package 11 A8 / F24: the slot indicator is NOT part of this stack any
        // more, so the effect layer starts empty even with an ownership edge set.
        glow.SetSlotIndicator(1);
        AssertThat(glow.IsGlowing).IsFalse();

        glow.SetStatus(FTT.Core.StatusType.Root);
        AssertThat(glow.IsGlowing).IsTrue();
        AssertThat(glow.ResolvedState.Layer).IsEqual(GlowLayer.Status);

        glow.SetHyperArmor(true);
        AssertThat(glow.ResolvedState.Layer).IsEqual(GlowLayer.HyperArmor);
        AssertThat(glow.GlowMaterial
            .GetShaderParameter(GlowPresentationController.OutlineThicknessUniform).AsSingle()).IsEqual(3f);
        AssertThat(glow.GlowMaterial
            .GetShaderParameter(GlowPresentationController.PulseSpeedUniform).AsSingle()).IsEqual(1.5f);
        AssertThat(glow.Light.Enabled).IsTrue();

        glow.SetHyperArmor(false);
        AssertThat(glow.ResolvedState.Layer).IsEqual(GlowLayer.Status);

        glow.ClearAllStates();
        AssertThat(glow.IsGlowing).IsFalse();
        AssertThat(glow.Light.Enabled).IsFalse();

        owner.Free();
    }

    [TestCase]
    public void ATelegraphTintSurvivesAStatusEndingAndViceVersa() {
        (Node2D owner, Sprite2D sprite, GlowPresentationController glow) = CreateSubject();
        var placeholderTint = new Color(0.4f, 0.7f, 0.4f);
        var telegraphTint = new Color(1f, 0f, 0f);

        glow.SetBaseTint(placeholderTint);
        AssertThat(sprite.Modulate).IsEqual(placeholderTint);

        glow.SetStatus(StatusType.RadiantBurn);
        glow.SetTintOverride(telegraphTint);
        AssertThat(sprite.Modulate).IsEqual(telegraphTint);

        // The status ends: the outline reverts, but the telegraph tint stays.
        glow.ClearState(GlowLayer.Status);
        AssertThat(glow.IsGlowing).IsFalse();
        AssertThat(sprite.Modulate).IsEqual(telegraphTint);

        // The telegraph ends: the base tint comes back untouched.
        glow.ClearTintOverride();
        AssertThat(sprite.Modulate).IsEqual(placeholderTint);
        AssertThat(glow.HasTintOverride).IsFalse();

        owner.Free();
    }

    [TestCase]
    public void AHitFlashRestoresTheBaseTintWhenItExpires() {
        (Node2D owner, Sprite2D sprite, GlowPresentationController glow) = CreateSubject();
        var baseTint = new Color(0.2f, 0.3f, 0.9f);
        glow.SetBaseTint(baseTint);

        glow.FlashHit(Colors.White, 0.05f);
        AssertThat(sprite.Modulate).IsEqual(Colors.White);

        glow._Process(0.03);
        AssertThat(sprite.Modulate).IsEqual(Colors.White);
        glow._Process(0.03);
        AssertThat(sprite.Modulate).IsEqual(baseTint);
        AssertThat(glow.HasTintOverride).IsFalse();

        owner.Free();
    }

    [TestCase]
    public void FactoryBuiltPlayersGetTheArbiterInsteadOfTheGoldOverlayColorRect() {
        PlayerController player = CharacterFactory.CreateCharacter("joan", applyStoryProgression: false);

        // The retired placeholder: a gold ColorRect toggled by hyper-armor.
        AssertObject(player.GetNodeOrNull("ChronalArmorOverlay")).IsNull();

        var glow = player.GetNodeOrNull<GlowPresentationController>(GlowPresentationController.NodeName);
        AssertObject(glow).IsNotNull();
        AssertObject(glow.Target).IsSame(player.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D"));
        AssertThat(glow.HasMaterial).IsTrue();

        glow.SetHyperArmor(true);
        AssertThat(glow.IsGlowing).IsTrue();
        AssertThat(glow.ResolvedState.OutlineColor).IsEqual(GlowPalette.HyperArmorColor);
        glow.SetHyperArmor(false);
        AssertThat(glow.IsGlowing).IsFalse();

        player.Free();
    }

    [TestCase]
    public void SpawnInvulnerabilityOutranksAStatusButNotHyperArmor() {
        (Node2D owner, Sprite2D _, GlowPresentationController glow) = CreateSubject();

        glow.SetStatus(StatusType.Venom);
        AssertThat(glow.ResolvedState.Layer).IsEqual(GlowLayer.Status);

        glow.SetSpawnInvulnerability(true);
        AssertThat(glow.ResolvedState.Layer).IsEqual(GlowLayer.SpawnInvulnerability);
        AssertThat(glow.ResolvedState.OutlineColor).IsEqual(GlowPalette.SpawnInvulnerabilityColor);

        glow.SetHyperArmor(true);
        AssertThat(glow.ResolvedState.Layer).IsEqual(GlowLayer.HyperArmor);

        glow.SetHyperArmor(false);
        AssertThat(glow.ResolvedState.Layer).IsEqual(GlowLayer.SpawnInvulnerability);
        glow.SetSpawnInvulnerability(false);
        AssertThat(glow.ResolvedState.Layer).IsEqual(GlowLayer.Status);

        owner.Free();
    }

    [TestCase]
    public void SettingStatusToNoneClearsTheStatusLayer() {
        (Node2D owner, Sprite2D _, GlowPresentationController glow) = CreateSubject();
        glow.SetStatus(StatusType.StaticCharge);
        AssertThat(glow.IsLayerActive(GlowLayer.Status)).IsTrue();
        glow.SetStatus(StatusType.None);
        AssertThat(glow.IsLayerActive(GlowLayer.Status)).IsFalse();
        AssertThat(glow.IsGlowing).IsFalse();
        owner.Free();
    }

    /// <summary>
    /// Package 11 A6b — the persistent hero resonance aura (design §2's
    /// two-colour grammar). The hero carries warm gold for the <b>entire
    /// game</b>, which means every transient channel has to leave it alone.
    ///
    /// <para>This case is why the aura is its own channel rather than a
    /// <c>GlowState</c> layer or a tint override. On the stack, the first status
    /// would outrank it; as an override, the first
    /// <see cref="GlowPresentationController.FlashHit()"/> would clear it. Both
    /// are ordinary combat events, so the aura would vanish seconds into the
    /// first fight — and nothing would report that it was supposed to be
    /// there.</para>
    /// </summary>
    [TestCase]
    public void TheHeroAuraSurvivesEveryTransientChannel() {
        (Node2D owner, Sprite2D sprite, GlowPresentationController glow) = CreateSubject();

        Color plainTint = glow.EffectiveTint;
        glow.SetHeroAura(true);

        AssertThat(glow.HasHeroAura).IsTrue();
        AssertThat(glow.HeroAuraColor).IsEqual(FTT.UI.UIPalette.ResonanceAura);
        // The aura warms the tint rather than replacing it: the character keeps
        // their own identity colour underneath the gold.
        Color aura = glow.EffectiveTint;
        AssertThat(aura).IsNotEqual(plainTint);
        AssertThat(aura.B < plainTint.B).IsTrue();
        // It owns its own light, because the effect light is off whenever no
        // effect is resolved — which is most of the game.
        AssertObject(glow.AuraLight).IsNotNull();
        AssertThat(glow.AuraLight.Enabled).IsTrue();

        // A status starting and ending leaves it exactly where it was.
        glow.SetStatus(StatusType.Venom);
        AssertThat(glow.HasHeroAura).IsTrue();
        AssertThat(glow.EffectiveTint).IsEqual(aura);
        glow.SetStatus(StatusType.None);
        AssertThat(glow.HasHeroAura).IsTrue();
        AssertThat(glow.EffectiveTint).IsEqual(aura);

        // So do a hit flash and its expiry, hyper-armor, spawn protection, the
        // F24 ownership edge, and a full stack clear.
        glow.FlashHit();
        AssertThat(glow.HasHeroAura).IsTrue();
        glow.ClearTintOverride();
        AssertThat(glow.EffectiveTint).IsEqual(aura);

        glow.SetHyperArmor(true);
        glow.SetSpawnInvulnerability(true);
        glow.SetSlotIndicator(1);
        glow.ClearAllStates();
        AssertThat(glow.HasHeroAura).IsTrue();
        AssertThat(glow.EffectiveTint).IsEqual(aura);
        // The two persistent channels are independent of each other as well.
        AssertThat(glow.HasOwnershipOutline).IsTrue();
        glow.ClearSlotIndicator();
        AssertThat(glow.HasHeroAura).IsTrue();
        AssertThat(sprite.Modulate).IsEqual(aura);

        owner.Free();
    }

    /// <summary>
    /// Package 11 A6b × A1 — <i>"what their machines touch drains grey"</i>. The
    /// Unbound's Suppression smothers the hero's gold, and when it lifts the
    /// aura returns at full strength: the smother <b>drains</b> the channel, it
    /// does not clear it.
    /// </summary>
    [TestCase]
    public void SuppressionSmothersTheHeroAuraAndRestoresIt() {
        (Node2D owner, Sprite2D _, GlowPresentationController glow) = CreateSubject();
        glow.SetHeroAura(true);
        Color lit = glow.EffectiveTint;
        float litEnergy = glow.AuraLight.Energy;

        glow.SetAuraSmothered(true);
        AssertThat(glow.IsAuraSmothered).IsTrue();
        // Still on underneath — only its appearance is drained.
        AssertThat(glow.HasHeroAura).IsTrue();
        AssertThat(glow.EffectiveTint).IsNotEqual(lit);
        // Drained, not merely recoloured: the aura's own light dims too, so a
        // Suppressed hero reads as unlit rather than as wearing a grey costume.
        AssertThat(glow.AuraLight.Energy < litEnergy).IsTrue();

        glow.SetAuraSmothered(false);
        AssertThat(glow.IsAuraSmothered).IsFalse();
        AssertThat(glow.EffectiveTint).IsEqual(lit);
        AssertThat(glow.AuraLight.Energy).IsEqual(litEnergy);

        owner.Free();
    }
}
