using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Grand Crusade content contracts: the authored ultimate resource
/// is a directional multi-hit cavalry charge — per-hit BaseDamage with a
/// HitCount that fills the active window at the authored tick interval, a
/// data-driven charge travel speed, and a heavy horizontal final knockback
/// carrying opponents toward the blast zone.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class JoanUltimateContentTests {

    [TestCase]
    public void GrandCrusadeResourceIsAMultiHitDirectionalCavalryCharge() {
        var data = ResourceLoader.Load<AbilityData>("res://resources/Abilities/joan/ultimate.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.AbilityID).IsEqual("joan_grand_crusade");
        AssertThat(data.Slot).IsEqual(AbilitySlot.Ultimate);
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.BaseDamage).IsEqual(12f);
        AssertThat(data.HitCount).IsEqual(6);
        // The charge travels forward at an authored speed; the Story executor
        // reads it from ProjectileSpeed.
        AssertThat(data.ProjectileSpeed > 0f).IsTrue();
        // Carries opponents toward the blast zone: horizontal-dominant knockback.
        AssertThat(Mathf.Abs(data.KnockbackForce.X) > Mathf.Abs(data.KnockbackForce.Y)).IsTrue();
    }

    [TestCase]
    public void GrandCrusadeHitCadenceFitsTheAuthoredActiveWindow() {
        var data = ResourceLoader.Load<AbilityData>("res://resources/Abilities/joan/ultimate.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.DamageTickIntervalFrames).IsEqual(6);
        AssertThat(data.ActiveFrames).IsEqual(36);
        // All authored hits land within the active window at the authored tick
        // interval (both modes deliver one hit per interval).
        AssertThat(data.DamageTickIntervalFrames * data.HitCount <= data.ActiveFrames).IsTrue();
        AssertThat(data.StartupFrames > 0).IsTrue();
        AssertThat(data.RecoveryFrames > 0).IsTrue();
    }
}
