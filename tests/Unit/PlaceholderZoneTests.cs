using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Audit Low "Kits/talents" (Einstein rift incoherence): every Story zone tick
/// shares one contract — damage rounds half-up like the Fighter loadout
/// convention (authored 1.5 means 2 in both modes), the owner's Story
/// special-damage multiplier applies, and dealt damage credits the owner's
/// Ultimate Meter. Zero-damage zones (the bespoke-tick visuals like Cleopatra's
/// vortex) must stay inert so nothing double-applies.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PlaceholderZoneTests {

    [TestCase]
    public void TickDamageRoundsHalfUpMatchingTheFighterConvention() {
        AssertThat(PlaceholderZone.ComputeTickDamage(1.5f, 1f)).IsEqual(2);
        AssertThat(PlaceholderZone.ComputeTickDamage(1.4f, 1f)).IsEqual(1);
        AssertThat(PlaceholderZone.ComputeTickDamage(2.5f, 1f)).IsEqual(3);
        AssertThat(PlaceholderZone.ComputeTickDamage(0f, 1f)).IsEqual(0);
        // The Story special-damage multiplier scales before rounding.
        AssertThat(PlaceholderZone.ComputeTickDamage(1.5f, 2f)).IsEqual(3);
        AssertThat(PlaceholderZone.ComputeTickDamage(1.5f, 1.1f)).IsEqual(2);
    }

    [TestCase]
    public void ZoneTickAppliesRoundedScaledDamageAndCreditsTheOwnersMeter() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController owner = CharacterFactory.CreateCharacter("einstein", 0);
        PlayerController target = CharacterFactory.CreateCharacter("joan", 1);
        var zone = new PlaceholderZone();
        tree.Root.AddChild(owner);
        tree.Root.AddChild(target);
        tree.Root.AddChild(zone);
        try {
            // The Einstein rift's authored tick: 1.5 damage per pulse.
            zone.Setup(1.5f, 3f, 0.5f, ownerIndex: 0, new Color(0.4f, 0.3f, 0.9f), 100f,
                ownerPlayer: owner);

            int startingHP = target.CurrentHP;
            zone.ApplyTickTo(target);
            // 1.5 rounds half-up to 2 — the truncated 1 was the audit bug.
            AssertThat(target.CurrentHP).IsEqual(startingHP - 2);
            // Damage dealt credits the owner's meter at 1 point per HP.
            AssertThat(owner.CurrentUltimateMeter).IsEqualApprox(2f, 0.001f);

            // The owner's Story special-damage multiplier now reaches zone ticks.
            owner.StorySpecialDamageMultiplier = 2f;
            zone.ApplyTickTo(target);
            AssertThat(target.CurrentHP).IsEqual(startingHP - 2 - 3);
            AssertThat(owner.CurrentUltimateMeter).IsEqualApprox(5f, 0.001f);
        } finally {
            zone.Free();
            target.Free();
            owner.Free();
        }
    }

    [TestCase]
    public void ZeroDamageVisualZonesStayInertSoBespokeTickPathsNeverDoubleApply() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController owner = CharacterFactory.CreateCharacter("cleopatra", 0);
        PlayerController target = CharacterFactory.CreateCharacter("joan", 1);
        var zone = new PlaceholderZone();
        tree.Root.AddChild(owner);
        tree.Root.AddChild(target);
        tree.Root.AddChild(zone);
        try {
            // Cleopatra's vortex (and every bespoke-tick ability) spawns its
            // placeholder zone with zero damage: the visual must deal nothing
            // and credit nothing, or the bespoke path would double-apply.
            zone.Setup(0f, 2f, 1f, ownerIndex: 0, new Color(0.8f, 0.7f, 0.3f), 120f,
                ownerPlayer: owner);

            int startingHP = target.CurrentHP;
            zone.ApplyTickTo(target);
            AssertThat(target.CurrentHP).IsEqual(startingHP);
            AssertThat(owner.CurrentUltimateMeter).IsEqualApprox(0f, 0.001f);
        } finally {
            zone.Free();
            target.Free();
            owner.Free();
        }
    }
}
