using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
public class DamageCalculatorTests {
    [TestCase]
    public void BaseDamageUsesTheProvidedMultiplier() {
        AssertThat(DamageCalculator.CalculateDamage(10.0f, 1.5f)).IsEqual(15.0f);
    }

    [TestCase]
    public void ComboFinisherUsesOnePointFiveMultiplier() {
        AssertThat(DamageCalculator.CalculateComboFinisherDamage(12.0f)).IsEqual(18.0f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void KnockbackUsesOnePlusWeightAndFacesLeft() {
        Vector2 knockback = DamageCalculator.CalculateKnockback(new Vector2(3.0f, -2.0f), 1.0f, false);

        AssertThat(knockback.X).IsEqual(-1.5f);
        AssertThat(knockback.Y).IsEqual(-1.0f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void NegativeWeightCannotAmplifyKnockback() {
        Vector2 knockback = DamageCalculator.CalculateKnockback(new Vector2(3.0f, -2.0f), -5.0f, true);

        AssertThat(knockback.X).IsEqual(3.0f);
        AssertThat(knockback.Y).IsEqual(-2.0f);
    }
}
