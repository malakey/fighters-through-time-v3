using System;
using System.Collections.Generic;
using System.IO;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W3 (S02): the skip path's first-use tooltips — the ledger rules
/// (<see cref="FirstUseTooltips"/>) and the two additive save fields that carry
/// them. A Full-Calibration slot (every pre-v8 slot included) never shows one; a
/// Skip slot shows each of the nine once. Pure C# (failure signature 7).
/// </summary>
[TestSuite]
public class FirstUseTooltipsTests {

    [TestCase]
    public void AFullCalibrationSlotNeverShowsATooltip() {
        var save = new StorySaveData();
        AssertThat(save.SkippedCalibration).IsFalse();
        foreach (string id in FirstUseTooltips.All) {
            AssertThat(FirstUseTooltips.ShouldShow(save, id)).IsFalse();
            AssertThat(FirstUseTooltips.TryConsume(save, id)).IsFalse();
        }
        AssertThat(save.ShownFirstUseTooltips.Count).IsEqual(0);
        AssertThat(FirstUseTooltips.TryConsume(null, FirstUseTooltips.Ledge)).IsFalse();
    }

    [TestCase]
    public void TheSkipChoiceArmsEachOfTheNineTooltipsExactlyOncePerSlot() {
        var save = new StorySaveData();
        FirstUseTooltips.ArmForSkippedCalibration(save);
        AssertThat(save.SkippedCalibration).IsTrue();
        AssertThat(FirstUseTooltips.All.Count).IsEqual(9);
        foreach (string id in FirstUseTooltips.All) {
            AssertThat(FirstUseTooltips.TryConsume(save, id))
                .OverrideFailureMessage($"{id} did not show the first time.").IsTrue();
            AssertThat(FirstUseTooltips.TryConsume(save, id))
                .OverrideFailureMessage($"{id} showed twice on one slot.").IsFalse();
        }
        // Re-arming (a second Skip, a reload) never un-shows anything.
        FirstUseTooltips.ArmForSkippedCalibration(save);
        AssertThat(FirstUseTooltips.ShouldShow(save, FirstUseTooltips.Grab)).IsFalse();
        // An unknown ID is refused rather than recorded.
        AssertThat(FirstUseTooltips.TryConsume(save, "not_a_tooltip")).IsFalse();
        AssertThat(save.ShownFirstUseTooltips.Count).IsEqual(9);
    }

    [TestCase]
    public void TheLedgerSurvivesASaveRoundTripAndNormalizesAMissingList() {
        var save = new StorySaveData();
        FirstUseTooltips.ArmForSkippedCalibration(save);
        FirstUseTooltips.TryConsume(save, FirstUseTooltips.DeathRewind);
        var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<StorySaveData>(
            Newtonsoft.Json.JsonConvert.SerializeObject(save));
        AssertThat(loaded.SkippedCalibration).IsTrue();
        AssertThat(FirstUseTooltips.ShouldShow(loaded, FirstUseTooltips.DeathRewind)).IsFalse();
        AssertThat(FirstUseTooltips.ShouldShow(loaded, FirstUseTooltips.TimeFreeze)).IsTrue();

        var legacy = new StorySaveData { ShownFirstUseTooltips = null };
        legacy.Normalize();
        AssertObject(legacy.ShownFirstUseTooltips).IsNotNull();
    }

    [TestCase]
    public void EveryTooltipHasAnAuthoredEnglishLine() {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            if (line.Length == 0 || line[0] == '#') continue;
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        var missing = new List<string>();
        foreach (string id in FirstUseTooltips.All) {
            string key = FirstUseTooltips.KeyFor(id);
            if (string.IsNullOrEmpty(key) || !keys.Contains(key)) missing.Add(id);
        }
        if (missing.Count > 0) AssertThat(string.Join(" | ", missing)).IsEqual("");
    }
}
