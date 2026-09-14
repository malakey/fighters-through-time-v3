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
        // V7.3 ruling #18: items and hazards default to Medium, still enabled.
        AssertThat(settings.ItemsEnabled).IsTrue();
        AssertThat(settings.ItemSpawnRate).IsEqual(ChronalOrbFrequency.Medium);
        AssertThat(settings.StageHazardsEnabled).IsTrue();
        AssertThat(settings.HazardRate).IsEqual(HazardTriggerFrequency.Medium);
    }

    // === F21 legacy-Hybrid normalization (Package 11 A1c) ===
    //
    // SavedMatchSettings.Mode is a raw int in the global payload, so a save written
    // before F21 can still carry the retired Hybrid ordinal (2). Normalize() runs
    // before loaded settings are displayed or the next match is created — never
    // against a running match — and is idempotent.

    /// <summary>
    /// A recognized legacy Hybrid becomes <b>timed Stock</b>, keeping a valid
    /// positive timer and stock count and every other house rule. A valid timed
    /// Hybrid must never become untimed Stock.
    /// </summary>
    [TestCase]
    public void ALegacyHybridBecomesTimedStockKeepingItsValidSettings() {
        var saved = new SavedMatchSettings {
            Saved = true,
            Mode = SavedMatchSettings.LegacyHybridMode,
            StockCount = 5,
            TimeLimit = 120f,
            ItemSpawnRate = (int)ChronalOrbFrequency.Low,
            HazardRate = (int)HazardTriggerFrequency.High
        };

        saved.Normalize();

        AssertThat(saved.Mode).IsEqual((int)MatchMode.Stock);
        AssertThat(saved.TimeLimit)
            .OverrideFailureMessage("A valid timed Hybrid must not become untimed Stock.")
            .IsEqual(120f);
        AssertThat(saved.StockCount).IsEqual(5);
        // Every other house rule is preserved untouched.
        AssertThat(saved.ItemSpawnRate).IsEqual((int)ChronalOrbFrequency.Low);
        AssertThat(saved.HazardRate).IsEqual((int)HazardTriggerFrequency.High);
        AssertThat(saved.TimerRepaired).IsFalse();

        // Idempotent: a second pass changes nothing.
        saved.Normalize();
        AssertThat(saved.Mode).IsEqual((int)MatchMode.Stock);
        AssertThat(saved.TimeLimit).IsEqual(120f);
    }

    /// <summary>
    /// A Hybrid timer that is Off, missing or nonfinite becomes the default 480 s,
    /// and the repair is flagged so the settings summary can show it before launch.
    /// </summary>
    [TestCase]
    public void AnInvalidHybridTimerIsRepairedToTheDefaultAndFlagged() {
        foreach (float broken in new[] { 0f, float.NaN, float.PositiveInfinity, -30f }) {
            var saved = new SavedMatchSettings {
                Saved = true,
                Mode = SavedMatchSettings.LegacyHybridMode,
                StockCount = 3,
                TimeLimit = broken
            };

            saved.Normalize();

            AssertThat(saved.Mode).IsEqual((int)MatchMode.Stock);
            AssertThat(saved.TimeLimit)
                .OverrideFailureMessage($"A Hybrid timer of {broken} must repair to 480 s.")
                .IsEqual(SavedMatchSettings.DefaultTimeLimitSeconds);
            AssertThat(saved.TimerRepaired)
                .OverrideFailureMessage("A repaired timer must be visible before launch.")
                .IsTrue();
        }
    }

    /// <summary>
    /// An invalid or missing stock count clamps to the existing 1-5 range with the
    /// default 3; valid values are kept. Stock's timer may legitimately be Off, and
    /// Time's may not — Time repairs to 480 s.
    /// </summary>
    [TestCase]
    public void StockCountsClampAndTimeAlwaysKeepsAPositiveTimer() {
        foreach (int broken in new[] { 0, -2, 6, 99 }) {
            var saved = new SavedMatchSettings { Saved = true, Mode = (int)MatchMode.Stock, StockCount = broken };
            saved.Normalize();
            AssertThat(saved.StockCount)
                .OverrideFailureMessage($"A stock count of {broken} must default to 3.")
                .IsEqual(SavedMatchSettings.DefaultStockCount);
        }

        // Stock keeps timer Off — that is a valid configuration there.
        var untimed = new SavedMatchSettings { Saved = true, Mode = (int)MatchMode.Stock, TimeLimit = 0f };
        untimed.Normalize();
        AssertThat(untimed.TimeLimit).IsEqual(0f);
        AssertThat(untimed.TimerRepaired).IsFalse();

        // Time requires a positive timer; Off is unavailable there.
        var timed = new SavedMatchSettings { Saved = true, Mode = (int)MatchMode.TimeLimit, TimeLimit = 0f };
        timed.Normalize();
        AssertThat(timed.TimeLimit).IsEqual(SavedMatchSettings.DefaultTimeLimitSeconds);
        AssertThat(timed.TimerRepaired).IsTrue();
    }

    /// <summary>
    /// Unknown mode values are <b>not</b> Hybrid. The source settings are preserved
    /// and a valid selection is required rather than a winner rule being guessed —
    /// so an unknown mode must not inherit Hybrid's timer repair.
    /// </summary>
    [TestCase]
    public void UnknownModeValuesAreNotTreatedAsHybrid() {
        var saved = new SavedMatchSettings {
            Saved = true,
            Mode = 77,
            StockCount = 4,
            TimeLimit = 0f,
            ItemSpawnRate = (int)ChronalOrbFrequency.High
        };

        saved.Normalize();

        AssertThat(saved.Mode)
            .OverrideFailureMessage("An unknown mode must resolve to a selectable one.")
            .IsEqual((int)MatchMode.Stock);
        AssertThat(saved.TimeLimit)
            .OverrideFailureMessage("An unknown mode must not inherit Hybrid's timer repair.")
            .IsEqual(0f);
        AssertThat(saved.StockCount).IsEqual(4);
        AssertThat(saved.ItemSpawnRate).IsEqual((int)ChronalOrbFrequency.High);
    }
}

