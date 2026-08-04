using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
[RequireGodotRuntime]
public class UltimateMeterTests {
    [TestCase]
    public void GainRatesCapAtOneHundred() {
        var meter = new UltimateMeter();

        meter.AddFromDamageDealt(80f);
        meter.AddFromDamageTaken(100f);

        AssertThat(meter.CurrentValue).IsEqual(100f);
        AssertThat(meter.IsFull).IsTrue();
        meter.Free();
    }

    [TestCase]
    public void StockLossRetainsSeventyFivePercent() {
        var meter = new UltimateMeter();
        meter.SetValue(80f);

        meter.ApplyStockLossRetention();

        AssertThat(meter.CurrentValue).IsEqual(60f);
        meter.Free();
    }

    [TestCase]
    public void ConsumeResetsMeter() {
        var meter = new UltimateMeter();
        meter.SetValue(100f);

        meter.Consume();

        AssertThat(meter.CurrentValue).IsEqual(0f);
        meter.Free();
    }
}
