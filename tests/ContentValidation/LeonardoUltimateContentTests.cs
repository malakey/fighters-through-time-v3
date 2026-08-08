using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Canonical Vitruvian Matrix content contract: the authored ultimate resource
/// carries the trap-and-bombardment structure both modes execute — 8 hits of
/// 10 at 18-frame intervals across a 2.4 s window, the refreshing Root hold,
/// and the final-explosion knockback.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LeonardoUltimateContentTests {

    [TestCase]
    public void VitruvianMatrixResourceCarriesTheTrapAndBombardmentStructure() {
        var data = FTT.Core.AuthoredResources.Load<AbilityData>("res://resources/Abilities/leonardo/ultimate.tres");
        AssertObject(data).IsNotNull();
        AssertThat(data.AbilityID).IsEqual("leonardo_vitruvian_matrix");
        AssertThat(data.CharacterID).IsEqual("leonardo");
        AssertThat(data.Slot).IsEqual(AbilitySlot.Ultimate);
        AssertThat(data.ExecutionType).IsEqual(AbilityExecutionType.Cinematic);

        // Bombardment: 8 hits of 10 (80 total) every 18 frames across 2.4 s.
        AssertThat(data.BaseDamage).IsEqual(10f);
        AssertThat(data.IsMultiHit).IsTrue();
        AssertThat(data.HitCount).IsEqual(8);
        AssertThat(data.DamageTickIntervalFrames).IsEqual(18);
        AssertThat(data.Lifetime).IsEqual(2.4f);

        // The trap window equals the bombardment window: the active phase spans
        // the authored lifetime (144 frames at 60 Hz).
        AssertThat(data.ActiveFrames).IsEqual(144);
        AssertThat(Mathf.RoundToInt(data.Lifetime * 60f)).IsEqual(data.ActiveFrames);

        // Trap hold: each bombardment hit refreshes a short Root so the hold
        // lapses shortly after the final hit in both modes.
        AssertThat(data.AppliedStatus).IsEqual(StatusType.Root);
        AssertThat(data.StatusDuration).IsEqual(0.4f);

        // The closing explosion carries real launch knockback.
        AssertThat(data.KnockbackForce).IsEqual(new Vector2(5, -3));
    }
}
