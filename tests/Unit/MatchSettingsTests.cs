using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
public class MatchSettingsTests {
    [TestCase]
    public void DefaultsMatchInitialReleaseRules() {
        MatchSettings settings = MatchSettings.GetDefault();

        AssertThat(settings.Mode).IsEqual(MatchMode.Stock);
        AssertThat(settings.StockCount).IsEqual(3);
        AssertThat(settings.TimeLimit).IsEqual(480.0f);
        AssertThat(settings.ItemsEnabled).IsTrue();
        AssertThat(settings.ItemSpawnRate).IsEqual(ChronalOrbFrequency.High);
        AssertThat(settings.StageHazardsEnabled).IsTrue();
        AssertThat(settings.HazardRate).IsEqual(HazardTriggerFrequency.High);
    }
}

