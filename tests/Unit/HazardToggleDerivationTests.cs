using FTT.Core;
using GdUnit4;
using Newtonsoft.Json;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W5 — the declared v7 derivation for the retired hazard selector
/// (plan §3.2): <c>Off → false</c>, anything else → <c>true</c>. The save version
/// was bumped once, by Package 12 Phase C, which composes
/// <see cref="SavedMatchSettings.DeriveStageHazardsEnabled"/> into its single
/// v6→v7 step; <see cref="SavedMatchSettings.Normalize"/> still tolerates
/// a pre-toggle payload. Pure C#: the payload class stays engine-free.
/// </summary>
[TestSuite]
public class HazardToggleDerivationTests {

    [TestCase]
    public void OffDerivesFalseAndEveryOtherStoredRateDerivesTrue() {
        AssertThat(SavedMatchSettings.DeriveStageHazardsEnabled(0)).IsFalse();
        foreach (int rate in new[] { 1, 2, 3, 7, -1 }) {
            AssertThat(SavedMatchSettings.DeriveStageHazardsEnabled(rate))
                .OverrideFailureMessage($"legacy rate {rate} must derive On").IsTrue();
        }
    }

    [TestCase]
    public void NormalizeDerivesOnlyAMissingToggleAndNeverOverwritesAnExplicitOne() {
        var legacyOff = new SavedMatchSettings { Saved = true, HazardRate = 0 };
        legacyOff.Normalize();
        AssertThat(legacyOff.StageHazardsEnabled == false).IsTrue();

        var legacyHigh = new SavedMatchSettings { Saved = true, HazardRate = 3 };
        legacyHigh.Normalize();
        AssertThat(legacyHigh.StageHazardsEnabled == true).IsTrue();

        // An explicit Off chosen after the toggle shipped survives, whatever the
        // stale legacy field still holds; and a second pass changes nothing.
        var explicitOff = new SavedMatchSettings { Saved = true, HazardRate = 3, StageHazardsEnabled = false };
        explicitOff.Normalize();
        explicitOff.Normalize();
        AssertThat(explicitOff.StageHazardsEnabled == false).IsTrue();
    }

    [TestCase]
    public void APreTogglePayloadDeserializesWithNoToggleAndMeterPickupsOff() {
        const string legacyJson = "{\"Saved\":true,\"Mode\":0,\"StockCount\":3,\"TimeLimit\":480.0,"
            + "\"ItemSpawnRate\":2,\"HazardRate\":0}";
        var loaded = JsonConvert.DeserializeObject<SavedMatchSettings>(legacyJson);
        AssertThat(loaded.StageHazardsEnabled.HasValue).IsFalse();
        AssertThat(loaded.MeterPickupsEnabled).IsFalse();
        loaded.Normalize();
        AssertThat(loaded.StageHazardsEnabled == false).IsTrue();

        // Round trip: a written payload carries the explicit toggle.
        var written = new SavedMatchSettings { Saved = true, StageHazardsEnabled = true, MeterPickupsEnabled = true };
        var reread = JsonConvert.DeserializeObject<SavedMatchSettings>(JsonConvert.SerializeObject(written));
        AssertThat(reread.StageHazardsEnabled == true).IsTrue();
        AssertThat(reread.MeterPickupsEnabled).IsTrue();
    }
}
